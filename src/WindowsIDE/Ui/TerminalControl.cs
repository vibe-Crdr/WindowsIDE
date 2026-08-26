using System;
using System.Drawing;
using System.Drawing.Text;
using System.Windows.Forms;
using WindowsIDE.Editor;
using WindowsIDE.Terminal;
using WindowsIDE.Ui.Fonts;

namespace WindowsIDE.Ui
{
    /// <summary>
    /// 統合ターミナルの描画・キー・IME・スクロール。RichTextBox は使わない。
    /// </summary>
    public sealed class TerminalControl : Control, IImeClient
    {
        private const int WmMouseHWheel = 0x020E;
        private const int ResizeDelayMs = 80;

        private readonly VtScreen screen;
        private readonly VtParser parser;
        private readonly PseudoConsoleSession session;
        private readonly ThemedScrollBar vScroll;
        private readonly Timer caretTimer;
        private readonly Timer resizeTimer;
        private Font halfFont;
        private Font fullFont;
        private StringFormat typographic;
        private ShellKind shellKind;
        private string workspaceRoot;
        private bool started;
        private bool awaitingRestart;
        private bool caretVisible;
        private bool selecting;
        private bool followEnd;
        private bool ignoreScroll;
        private int selARow;
        private int selACol;
        private int selBRow;
        private int selBCol;
        private int wheelLeftover;
        private float asciiWidth;
        private string imeComposition;
        private int imeCursor;
        private int sessionGeneration;

        /// <summary>
        /// 空の画面とセッションを組む。PTY は StartIfNeeded まで遅延。
        /// </summary>
        public TerminalControl()
        {
            this.screen = new VtScreen(VtScreen.MinColumns, VtScreen.MinRows);
            this.parser = new VtParser(this.screen);
            this.session = new PseudoConsoleSession();
            this.shellKind = ShellKind.PowerShell51;
            this.followEnd = true;
            this.imeComposition = "";
            this.typographic = (StringFormat)StringFormat.GenericTypographic.Clone();
            this.typographic.FormatFlags = this.typographic.FormatFlags | StringFormatFlags.MeasureTrailingSpaces | StringFormatFlags.NoWrap | StringFormatFlags.FitBlackBox;
            this.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable | ControlStyles.ResizeRedraw | ControlStyles.EnableNotifyMessage, true);
            this.TabStop = true;
            this.BackColor = Theme.Background;
            this.ForeColor = Theme.Foreground;
            this.ImeMode = ImeMode.On;
            this.Cursor = Cursors.IBeam;

            this.vScroll = new ThemedScrollBar(true);
            this.vScroll.TrackColor = Theme.Background;
            this.vScroll.SmallChange = 1;
            this.vScroll.ValueChanged += this.OnScrollChanged;
            this.Controls.Add(this.vScroll);

            this.caretTimer = new Timer();
            this.caretTimer.Interval = 530;
            this.caretTimer.Tick += this.OnCaretTick;
            this.caretTimer.Start();

            this.resizeTimer = new Timer();
            this.resizeTimer.Interval = ResizeDelayMs;
            this.resizeTimer.Tick += this.OnResizeTimer;

            this.session.OutputReceived += this.OnSessionOutput;
            this.session.Exited += this.OnSessionExited;
            this.session.StartFailed += this.OnSessionStartFailed;
        }

        /// <summary>現在のシェル。</summary>
        public ShellKind ShellKind
        {
            get { return this.shellKind; }
        }

        /// <summary>IME 未確定があるとき true。</summary>
        public bool IsComposing
        {
            get { return !string.IsNullOrEmpty(this.imeComposition); }
        }

        /// <summary>選択があれば true。</summary>
        public bool HasSelection
        {
            get { return this.selARow != this.selBRow || this.selACol != this.selBCol; }
        }

        IntPtr IImeClient.WindowHandle
        {
            get { return this.Handle; }
        }

        /// <summary>
        /// ツリーと同じ 12 DIP 双フォント。所有権は移さない。
        /// </summary>
        /// <param name="half">半角。</param>
        /// <param name="full">全角。</param>
        public void SetFonts(Font half, Font full)
        {
            this.halfFont = half;
            this.fullFont = full;
            this.RefreshChrome();
            this.Invalidate();
        }

        /// <summary>
        /// 次回起動の作業ディレクトリ根。走っている PTY の cwd は変えない。
        /// </summary>
        /// <param name="root">ワークスペース根。null 可。</param>
        public void SetWorkspaceRoot(string root)
        {
            this.workspaceRoot = root;
        }

        /// <summary>
        /// 初回 ShowTerminal で PTY を遅延起動する。既に生きていれば何もしない。
        /// </summary>
        public void StartIfNeeded()
        {
            if (this.session.IsRunning)
            {
                return;
            }

            if (this.awaitingRestart)
            {
                return;
            }

            this.LaunchSession();
        }

        /// <summary>
        /// Kill → バッファクリア → 指定シェルで再起動。1 ショットは殺さない。
        /// </summary>
        /// <param name="kind">次のシェル。</param>
        public void SwitchShell(ShellKind kind)
        {
            this.shellKind = kind;
            this.awaitingRestart = false;
            this.started = false;
            this.session.Kill();
            this.screen.ClearAll();
            this.parser.Reset();
            this.ClearSelection();
            this.LaunchSession();
            this.Invalidate();
        }

        /// <summary>
        /// PTY を閉じる。UI で待たない。バッファは残す。
        /// </summary>
        public void CloseSession()
        {
            this.session.Kill();
            this.started = false;
        }

        /// <summary>選択をクリップボードへ。CRLF。</summary>
        public void CopySelection()
        {
            if (!this.HasSelection)
            {
                return;
            }

            string text = this.screen.GetSelectedText(this.selARow, this.selACol, this.selBRow, this.selBCol);
            if (text != null && text.Length > 0)
            {
                Clipboard.SetText(text);
            }
        }

        /// <summary>
        /// クリップボードを PTY へ。改行は CR。シェルへ Ctrl+V は送らない。
        /// </summary>
        public void PasteClipboard()
        {
            if (!Clipboard.ContainsText())
            {
                return;
            }

            string text = Clipboard.GetText();
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            text = text.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r");
            this.session.WriteText(text);
        }

        /// <summary>
        /// バイトを PTY へ。再起動待ちで Enter（0x0D）なら同種を起動。
        /// </summary>
        /// <param name="data">バイト。</param>
        public void SendToPty(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                return;
            }

            if (this.awaitingRestart)
            {
                if (data.Length == 1 && data[0] == 0x0D)
                {
                    this.awaitingRestart = false;
                    this.LaunchSession();
                }

                return;
            }

            this.session.Write(data);
        }

        void IImeClient.SetCompositionString(string text)
        {
            this.imeComposition = (text == null) ? "" : text;
        }

        void IImeClient.SetCompositionCursor(int rawCursor)
        {
            int length = (this.imeComposition == null) ? 0 : this.imeComposition.Length;
            this.imeCursor = ImeLayout.ClampCursor(rawCursor, length);
        }

        void IImeClient.ClearComposition()
        {
            this.imeComposition = "";
            this.imeCursor = 0;
        }

        void IImeClient.UpdateImeWindow()
        {
            this.UpdateImeWindow();
        }

        void IImeClient.FillQueryCharPosition(IntPtr lParam)
        {
            this.FillImeCharPosition(lParam);
        }

        void IImeClient.RequestInvalidate()
        {
            this.Invalidate();
        }

        /// <summary>矢印・Tab・Esc を入力キーにする。</summary>
        protected override bool IsInputKey(Keys keyData)
        {
            Keys code = keyData & Keys.KeyCode;
            if (code == Keys.Tab || code == Keys.Escape
                || code == Keys.Left || code == Keys.Right || code == Keys.Up || code == Keys.Down
                || code == Keys.Enter || code == Keys.Back)
            {
                return true;
            }

            return base.IsInputKey(keyData);
        }

        /// <summary>Tab でフォーカスを移さない。</summary>
        protected override bool ProcessDialogKey(Keys keyData)
        {
            if ((keyData & Keys.KeyCode) == Keys.Tab)
            {
                return false;
            }

            return base.ProcessDialogKey(keyData);
        }

        /// <summary>分類したキーを処理する。変換中は IME に任せる。</summary>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            TerminalInputResult result = TerminalInput.Classify((int)e.KeyData, this.HasSelection, this.IsComposing);
            if (result.Kind == TerminalInputKind.None)
            {
                return;
            }

            e.Handled = true;
            e.SuppressKeyPress = true;
            if (result.Kind == TerminalInputKind.Copy)
            {
                this.CopySelection();
                return;
            }

            if (result.Kind == TerminalInputKind.Paste)
            {
                this.PasteClipboard();
                return;
            }

            if (result.Kind == TerminalInputKind.Send)
            {
                this.SendToPty(result.Payload);
            }
        }

        /// <summary>確定文字を UTF-8 で PTY へ。変換中と制御は入れない。</summary>
        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            base.OnKeyPress(e);
            if (this.IsComposing)
            {
                return;
            }

            if (e.KeyChar == '\r' || e.KeyChar == '\n' || e.KeyChar == '\t' || e.KeyChar == 8 || e.KeyChar < 32)
            {
                return;
            }

            if (this.awaitingRestart)
            {
                e.Handled = true;
                return;
            }

            this.session.WriteText(e.KeyChar.ToString());
            e.Handled = true;
        }

        /// <summary>クリックで選択開始。</summary>
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            this.Focus();
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            int row;
            int col;
            this.HitTest(e.Location, out row, out col);
            this.selARow = row;
            this.selACol = col;
            this.selBRow = row;
            this.selBCol = col;
            this.selecting = true;
            this.caretVisible = true;
            this.Invalidate();
        }

        /// <summary>ドラッグ選択。</summary>
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!this.selecting)
            {
                return;
            }

            int row;
            int col;
            this.HitTest(e.Location, out row, out col);
            this.selBRow = row;
            this.selBCol = col;
            this.Invalidate();
        }

        /// <summary>ドラッグ終了。</summary>
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            this.selecting = false;
        }

        /// <summary>縦ホイール。</summary>
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (this.vScroll.Visible)
            {
                int notches = DpiUtil.WheelNotches(e.Delta, ref this.wheelLeftover);
                if (notches != 0)
                {
                    this.followEnd = false;
                    this.vScroll.Value = this.vScroll.Value - (notches * this.vScroll.SmallChange);
                    this.followEnd = this.IsAtEnd();
                }
            }

            HandledMouseEventArgs handled = e as HandledMouseEventArgs;
            if (handled != null)
            {
                handled.Handled = true;
            }
        }

        /// <summary>サイズ変更でリサイズを遅延する。</summary>
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            this.RefreshChrome();
            this.resizeTimer.Stop();
            this.resizeTimer.Start();
        }

        /// <summary>親の DPI 変更。</summary>
        protected override void OnDpiChangedAfterParent(EventArgs e)
        {
            base.OnDpiChangedAfterParent(e);
            this.RefreshChrome();
            this.ApplyPtySize();
            this.Invalidate();
        }

        /// <summary>ハンドル後にバーを置く。</summary>
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            this.RefreshChrome();
            if (this.halfFont != null)
            {
                NativeIme.ApplyCompositionFont(this.Handle, this.halfFont.Height);
            }
        }

        /// <summary>フォーカスでキャレット点滅。</summary>
        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            this.caretVisible = true;
            this.Invalidate();
        }

        /// <summary>画面・選択・キャレットを描く。</summary>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            using (SolidBrush bg = new SolidBrush(Theme.Background))
            {
                g.FillRectangle(bg, this.ClientRectangle);
            }

            this.DrawCells(g);
            using (Pen border = new Pen(Theme.Border))
            {
                g.DrawRectangle(border, 0, 0, this.ClientSize.Width - 1, this.ClientSize.Height - 1);
            }
        }

        /// <summary>IME。横ホイールは無視（折り返しのみ）。</summary>
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmMouseHWheel)
            {
                m.Result = (IntPtr)1;
                return;
            }

            ImeDispatchKind kind = NativeIme.Dispatch(ref m, this);
            if (kind == ImeDispatchKind.Consumed)
            {
                return;
            }

            base.WndProc(ref m);
            if (kind == ImeDispatchKind.ContinueBaseThenUpdate)
            {
                this.UpdateImeWindow();
            }
        }

        /// <summary>セッションを閉じる。借用フォントは破棄しない。</summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.session.OutputReceived -= this.OnSessionOutput;
                this.session.Exited -= this.OnSessionExited;
                this.session.StartFailed -= this.OnSessionStartFailed;
                this.session.Dispose();
                if (this.caretTimer != null)
                {
                    this.caretTimer.Dispose();
                }

                if (this.resizeTimer != null)
                {
                    this.resizeTimer.Dispose();
                }

                if (this.typographic != null)
                {
                    this.typographic.Dispose();
                    this.typographic = null;
                }
            }

            base.Dispose(disposing);
        }

        private void LaunchSession()
        {
            this.EnsureMetrics();
            int cols;
            int rows;
            this.MeasurePtySize(out cols, out rows);
            this.screen.Resize(cols, rows);
            this.parser.Reset();
            this.sessionGeneration++;
            if (this.sessionGeneration < 1)
            {
                this.sessionGeneration = 1;
            }

            this.session.Start(this.shellKind, this.workspaceRoot, cols, rows, this.sessionGeneration);
            this.started = string.IsNullOrEmpty(this.session.StartError);
            if (!this.started)
            {
                this.awaitingRestart = true;
            }

            this.followEnd = true;
            this.RefreshChrome();
            this.ScrollToEnd();
            this.Invalidate();
        }

        private void OnSessionOutput(object sender, TerminalOutputEventArgs e)
        {
            if (e == null)
            {
                return;
            }

            if (this.IsHandleCreated && this.InvokeRequired)
            {
                this.BeginInvoke(new MethodInvoker(delegate
                {
                    this.OnSessionOutput(sender, e);
                }));
                return;
            }

            if (this.IsDisposed || e.Generation != this.sessionGeneration)
            {
                return;
            }

            this.parser.Feed(e.Text);
            this.RefreshChrome();
            if (this.followEnd)
            {
                this.ScrollToEnd();
            }

            this.Invalidate();
        }

        private void OnSessionExited(object sender, TerminalExitedEventArgs e)
        {
            if (e == null)
            {
                return;
            }

            if (this.IsHandleCreated && this.InvokeRequired)
            {
                this.BeginInvoke(new MethodInvoker(delegate
                {
                    this.OnSessionExited(sender, e);
                }));
                return;
            }

            if (this.IsDisposed || e.Generation != this.sessionGeneration)
            {
                return;
            }

            this.started = false;
            this.awaitingRestart = true;
            this.screen.WritePlainLine("終了コード " + e.ExitCode.ToString() + "。Enter で再起動", ColorSlot.Comment);
            this.RefreshChrome();
            this.ScrollToEnd();
            this.Invalidate();
        }

        private void OnSessionStartFailed(object sender, TerminalStartFailedEventArgs e)
        {
            if (e == null)
            {
                return;
            }

            if (this.IsHandleCreated && this.InvokeRequired)
            {
                this.BeginInvoke(new MethodInvoker(delegate
                {
                    this.OnSessionStartFailed(sender, e);
                }));
                return;
            }

            if (this.IsDisposed || e.Generation != this.sessionGeneration)
            {
                return;
            }

            this.started = false;
            this.awaitingRestart = true;
            this.screen.WritePlainLine(e.Message, ColorSlot.Error);
            this.RefreshChrome();
            this.Invalidate();
        }

        private void OnResizeTimer(object sender, EventArgs e)
        {
            this.resizeTimer.Stop();
            this.ApplyPtySize();
        }

        private void ApplyPtySize()
        {
            int cols;
            int rows;
            this.MeasurePtySize(out cols, out rows);
            this.screen.Resize(cols, rows);
            if (this.session.IsRunning)
            {
                this.session.Resize(cols, rows, this.sessionGeneration);
            }

            this.RefreshChrome();
            this.Invalidate();
        }

        private void MeasurePtySize(out int cols, out int rows)
        {
            this.EnsureMetrics();
            int dpi = DpiUtil.GetDpi(this.IsHandleCreated ? this.Handle : IntPtr.Zero);
            int rowH = this.RowHeight(dpi);
            int pad = DpiUtil.ToPixels(8, dpi);
            int bar = DpiUtil.ToPixels(DpiUtil.ScrollBarThicknessDip, dpi);
            int vW = this.vScroll.Visible ? bar : 0;
            int innerW = this.ClientSize.Width - vW - pad - pad;
            int innerH = this.ClientSize.Height - pad;
            if (innerW < 1)
            {
                innerW = 1;
            }

            if (innerH < 1)
            {
                innerH = 1;
            }

            int cellW = (int)Math.Ceiling((double)this.asciiWidth);
            if (cellW < 1)
            {
                cellW = 8;
            }

            cols = innerW / cellW;
            rows = innerH / rowH;
            if (cols < VtScreen.MinColumns)
            {
                cols = VtScreen.MinColumns;
            }

            if (rows < VtScreen.MinRows)
            {
                rows = VtScreen.MinRows;
            }
        }

        private void DrawCells(Graphics g)
        {
            if (this.halfFont == null || this.fullFont == null)
            {
                return;
            }

            this.EnsureMetrics();
            int dpi = DpiUtil.GetDpi(this.IsHandleCreated ? this.Handle : IntPtr.Zero);
            int rowH = this.RowHeight(dpi);
            int pad = DpiUtil.ToPixels(8, dpi);
            int bar = DpiUtil.ToPixels(DpiUtil.ScrollBarThicknessDip, dpi);
            int vW = this.vScroll.Visible ? bar : 0;
            Rectangle list = new Rectangle(0, 0, Math.Max(0, this.ClientSize.Width - vW), this.ClientSize.Height);
            if (list.Width <= 0 || list.Height <= 0 || rowH < 1)
            {
                return;
            }

            int first = this.vScroll.Value;
            int visible = list.Height / rowH;
            if (visible < 1)
            {
                visible = 1;
            }

            int last = first + visible;
            if (last > this.screen.HistoryCount)
            {
                last = this.screen.HistoryCount;
            }

            int cellW = (int)Math.Ceiling((double)this.asciiWidth);
            if (cellW < 1)
            {
                cellW = 8;
            }

            int r0;
            int c0;
            int r1;
            int c1;
            this.NormalizedSelection(out r0, out c0, out r1, out c1);
            bool hasSel = this.HasSelection;
            int abs;
            for (abs = first; abs < last; abs++)
            {
                int y = list.Y + ((abs - first) * rowH);
                int col = 0;
                while (col < this.screen.Columns)
                {
                    VtCell cell = this.screen.GetHistoryCell(abs, col);
                    int w = 1;
                    if (cell.Continuation)
                    {
                        col++;
                        continue;
                    }

                    if (cell.Character != '\0')
                    {
                        w = CellWidth.Of(cell.Character);
                        if (w < 1)
                        {
                            w = 1;
                        }
                    }

                    int x = pad + (col * cellW);
                    Rectangle cellRect = new Rectangle(x, y, w * cellW, rowH);
                    bool selected = hasSel && this.CellSelected(abs, col, r0, c0, r1, c1);
                    if (selected)
                    {
                        using (SolidBrush sel = new SolidBrush(Theme.Selection))
                        {
                            g.FillRectangle(sel, cellRect);
                        }
                    }

                    if (cell.Character != '\0')
                    {
                        using (SolidBrush brush = new SolidBrush(MapSlot(cell.Slot)))
                        {
                            DualFontPainter.Draw(g, cell.Character.ToString(), this.halfFont, this.fullFont, Rectangle.Intersect(cellRect, list), x, brush, this.typographic);
                        }
                    }

                    col += w;
                }
            }

            float imePrefix = 0f;
            if (!string.IsNullOrEmpty(this.imeComposition) && this.Focused)
            {
                float imeWidth = DualFontPainter.Measure(g, this.imeComposition, this.halfFont, this.fullFont, this.typographic);
                int cursor = ImeLayout.ClampCursor(this.imeCursor, this.imeComposition.Length);
                if (cursor > 0)
                {
                    imePrefix = DualFontPainter.Measure(g, this.imeComposition.Substring(0, cursor), this.halfFont, this.fullFont, this.typographic);
                }

                int cx;
                int cy;
                this.CaretClient(first, rowH, pad, cellW, out cx, out cy);
                using (SolidBrush imeBg = new SolidBrush(Theme.Selection))
                {
                    g.FillRectangle(imeBg, cx, cy, imeWidth, rowH);
                }

                using (SolidBrush fg = new SolidBrush(Theme.Foreground))
                {
                    DualFontPainter.Draw(g, this.imeComposition, this.halfFont, this.fullFont, new Rectangle(cx, cy, (int)Math.Ceiling(imeWidth) + 1, rowH), cx, fg, this.typographic);
                }

                using (Pen underline = new Pen(Theme.Foreground))
                {
                    g.DrawLine(underline, cx, cy + rowH - 2, cx + imeWidth, cy + rowH - 2);
                }

                this.caretVisible = true;
            }

            if (this.Focused && this.caretVisible && this.screen.CaretVisible)
            {
                int cx;
                int cy;
                this.CaretClient(first, rowH, pad, cellW, out cx, out cy);
                int x = cx + (int)imePrefix;
                using (SolidBrush caret = new SolidBrush(Theme.Foreground))
                {
                    g.FillRectangle(caret, x, cy + 1, 1, rowH - 2);
                }
            }
        }

        private static Color MapSlot(ColorSlot slot)
        {
            if (slot == ColorSlot.Error)
            {
                return Theme.Error;
            }

            if (slot == ColorSlot.String)
            {
                return Theme.StringLiteral;
            }

            if (slot == ColorSlot.Number)
            {
                return Theme.Number;
            }

            if (slot == ColorSlot.Local)
            {
                return Theme.Local;
            }

            if (slot == ColorSlot.Keyword)
            {
                return Theme.Keyword;
            }

            if (slot == ColorSlot.Type)
            {
                return Theme.Type;
            }

            if (slot == ColorSlot.Comment)
            {
                return Theme.Comment;
            }

            return Theme.Foreground;
        }

        private void CaretClient(int firstAbs, int rowH, int pad, int cellW, out int x, out int y)
        {
            int abs = this.screen.ScrollbackCount + this.screen.CaretRow;
            int col = this.screen.CaretColumn;
            if (col > this.screen.Columns)
            {
                col = this.screen.Columns;
            }

            x = pad + (col * cellW);
            y = (abs - firstAbs) * rowH;
        }

        private void HitTest(Point location, out int row, out int col)
        {
            int dpi = DpiUtil.GetDpi(this.IsHandleCreated ? this.Handle : IntPtr.Zero);
            int rowH = this.RowHeight(dpi);
            int pad = DpiUtil.ToPixels(8, dpi);
            this.EnsureMetrics();
            int cellW = (int)Math.Ceiling((double)this.asciiWidth);
            if (cellW < 1)
            {
                cellW = 8;
            }

            int first = this.vScroll.Value;
            int rel = 0;
            if (rowH > 0)
            {
                rel = location.Y / rowH;
            }

            row = first + rel;
            if (row < 0)
            {
                row = 0;
            }

            if (row >= this.screen.HistoryCount)
            {
                row = this.screen.HistoryCount;
                if (row > 0)
                {
                    row = this.screen.HistoryCount - 1;
                }
            }

            col = 0;
            if (location.X > pad && cellW > 0)
            {
                col = (location.X - pad) / cellW;
            }

            if (col < 0)
            {
                col = 0;
            }

            if (col > this.screen.Columns)
            {
                col = this.screen.Columns;
            }
        }

        private void NormalizedSelection(out int r0, out int c0, out int r1, out int c1)
        {
            r0 = this.selARow;
            c0 = this.selACol;
            r1 = this.selBRow;
            c1 = this.selBCol;
            if (r1 < r0 || (r1 == r0 && c1 < c0))
            {
                r0 = this.selBRow;
                c0 = this.selBCol;
                r1 = this.selARow;
                c1 = this.selACol;
            }
        }

        private bool CellSelected(int row, int col, int r0, int c0, int r1, int c1)
        {
            if (row < r0 || row > r1)
            {
                return false;
            }

            if (row == r0 && col < c0)
            {
                return false;
            }

            if (row == r1 && col >= c1)
            {
                return false;
            }

            return true;
        }

        private void ClearSelection()
        {
            this.selARow = 0;
            this.selACol = 0;
            this.selBRow = 0;
            this.selBCol = 0;
        }

        private void OnScrollChanged(object sender, EventArgs e)
        {
            if (this.ignoreScroll)
            {
                return;
            }

            this.followEnd = this.IsAtEnd();
            this.Invalidate();
        }

        private bool IsAtEnd()
        {
            if (!this.vScroll.Visible)
            {
                return true;
            }

            int max = this.vScroll.Maximum - this.vScroll.LargeChange + 1;
            if (max < this.vScroll.Minimum)
            {
                max = this.vScroll.Minimum;
            }

            return this.vScroll.Value >= max;
        }

        private void ScrollToEnd()
        {
            if (!this.vScroll.Visible)
            {
                return;
            }

            int max = this.vScroll.Maximum - this.vScroll.LargeChange + 1;
            if (max < this.vScroll.Minimum)
            {
                max = this.vScroll.Minimum;
            }

            this.ignoreScroll = true;
            try
            {
                this.vScroll.Value = max;
            }
            finally
            {
                this.ignoreScroll = false;
            }

            this.followEnd = true;
        }

        private void RefreshChrome()
        {
            if (!this.IsHandleCreated)
            {
                return;
            }

            int dpi = DpiUtil.GetDpi(this.Handle);
            int rowH = this.RowHeight(dpi);
            if (rowH < 1)
            {
                rowH = 1;
            }

            int bar = DpiUtil.ToPixels(DpiUtil.ScrollBarThicknessDip, dpi);
            int itemCount = this.screen.HistoryCount;
            int visibleRows = Math.Max(1, this.ClientSize.Height / rowH);
            bool needV = itemCount > visibleRows;
            this.ignoreScroll = true;
            try
            {
                this.vScroll.Minimum = 0;
                this.vScroll.Maximum = Math.Max(0, itemCount - 1);
                this.vScroll.LargeChange = visibleRows;
                this.vScroll.Visible = needV;
                if (!needV)
                {
                    this.vScroll.Value = 0;
                    this.followEnd = true;
                }
            }
            finally
            {
                this.ignoreScroll = false;
            }

            int vW = needV ? bar : 0;
            this.vScroll.Bounds = new Rectangle(this.ClientSize.Width - vW, 0, vW, this.ClientSize.Height);
            this.vScroll.BringToFront();
        }

        private void EnsureMetrics()
        {
            if (this.halfFont == null)
            {
                this.asciiWidth = 8f;
                return;
            }

            if (this.IsHandleCreated)
            {
                using (Graphics g = this.CreateGraphics())
                {
                    this.asciiWidth = g.MeasureString("M", this.halfFont, PointF.Empty, this.typographic).Width;
                }
            }

            if (this.asciiWidth < 1f)
            {
                this.asciiWidth = 8f;
            }
        }

        private int RowHeight(int dpi)
        {
            int fontH = 12;
            if (this.halfFont != null)
            {
                fontH = this.halfFont.Height;
            }

            if (this.fullFont != null && this.fullFont.Height > fontH)
            {
                fontH = this.fullFont.Height;
            }

            return DpiUtil.TreeItemHeight(fontH, dpi);
        }

        private void OnCaretTick(object sender, EventArgs e)
        {
            if (!this.Focused)
            {
                return;
            }

            if (this.IsComposing)
            {
                this.caretVisible = true;
                this.Invalidate();
                return;
            }

            this.caretVisible = !this.caretVisible;
            this.Invalidate();
        }

        private void UpdateImeWindow()
        {
            if (!this.IsHandleCreated)
            {
                return;
            }

            int dpi = DpiUtil.GetDpi(this.Handle);
            int rowH = this.RowHeight(dpi);
            int pad = DpiUtil.ToPixels(8, dpi);
            this.EnsureMetrics();
            int cellW = (int)Math.Ceiling((double)this.asciiWidth);
            if (cellW < 1)
            {
                cellW = 8;
            }

            int first = this.vScroll.Value;
            int cx;
            int cy;
            this.CaretClient(first, rowH, pad, cellW, out cx, out cy);
            float prefix = 0f;
            float total = 0f;
            if (this.halfFont != null && this.fullFont != null && !string.IsNullOrEmpty(this.imeComposition))
            {
                using (Graphics g = this.CreateGraphics())
                {
                    int cursor = ImeLayout.ClampCursor(this.imeCursor, this.imeComposition.Length);
                    if (cursor > 0)
                    {
                        prefix = DualFontPainter.Measure(g, this.imeComposition.Substring(0, cursor), this.halfFont, this.fullFont, this.typographic);
                    }

                    total = DualFontPainter.Measure(g, this.imeComposition, this.halfFont, this.fullFont, this.typographic);
                }
            }

            int x = ImeLayout.FieldClientX(cx, 0, prefix);
            NativeIme.MoveCompositionWindow(this.Handle, x, cy);
            int excludeW = (int)total;
            if (excludeW < 1)
            {
                excludeW = 1;
            }

            NativeIme.SetCandidateExclude(this.Handle, new Rectangle(cx, cy, excludeW, rowH));
            if (this.halfFont != null)
            {
                NativeIme.ApplyCompositionFont(this.Handle, this.halfFont.Height);
            }
        }

        private void FillImeCharPosition(IntPtr lParam)
        {
            int length = (this.imeComposition == null) ? 0 : this.imeComposition.Length;
            int charPos = ImeLayout.ClampCursor(NativeIme.ReadCharPos(lParam), length);
            int dpi = DpiUtil.GetDpi(this.IsHandleCreated ? this.Handle : IntPtr.Zero);
            int rowH = this.RowHeight(dpi);
            int pad = DpiUtil.ToPixels(8, dpi);
            this.EnsureMetrics();
            int cellW = (int)Math.Ceiling((double)this.asciiWidth);
            if (cellW < 1)
            {
                cellW = 8;
            }

            int first = this.vScroll.Value;
            int cx;
            int cy;
            this.CaretClient(first, rowH, pad, cellW, out cx, out cy);
            float prefix = 0f;
            if (charPos > 0 && this.halfFont != null && this.fullFont != null && !string.IsNullOrEmpty(this.imeComposition))
            {
                using (Graphics g = this.CreateGraphics())
                {
                    prefix = DualFontPainter.Measure(g, this.imeComposition.Substring(0, charPos), this.halfFont, this.fullFont, this.typographic);
                }
            }

            int x = ImeLayout.FieldClientX(cx, 0, prefix);
            Point screenPt = this.PointToScreen(new Point(x, cy));
            Point docTl = this.PointToScreen(new Point(0, 0));
            Point docBr = this.PointToScreen(new Point(this.ClientSize.Width, this.ClientSize.Height));
            NativeIme.WriteCharPosition(lParam, charPos, screenPt, rowH, docTl.X, docTl.Y, docBr.X, docBr.Y);
        }
    }
}
