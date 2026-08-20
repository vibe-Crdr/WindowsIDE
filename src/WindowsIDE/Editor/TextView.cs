using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using WindowsIDE.Languages;
using WindowsIDE.Ui;
using WindowsIDE.Ui.Fonts;
using WindowsIDE.Workspace;

namespace WindowsIDE.Editor
{
    /// <summary>
    /// Control 継承の行単位編集器。折り返し無し。RichTextBox は使わない。
    /// </summary>
    public sealed class TextView : Control
    {
        private Document document;
        private Font halfFont;
        private Font fullFont;
        private FontLoadResult fonts;
        private int fontSize;
        private int tabSize;
        private int lineHeight;
        private float asciiWidth;
        private float halfAscent;
        private float fullAscent;
        private int gutterWidth;
        private ThemedScrollBar vScroll;
        private ThemedScrollBar hScroll;
        private Timer caretTimer;
        private bool caretVisible;
        private bool selecting;
        private string imeComposition;
        private int imeCursor;
        private StringFormat typographic;
        private bool metricsValid;
        private bool ignoreScroll;
        private int lastDpi;
        private int wheelLeftover;
        private readonly List<Token> paintTokens;

        /// <summary>
        /// 空の編集器を作る。
        /// </summary>
        public TextView()
        {
            this.fontSize = WorkspaceSettings.DefaultFontSize;
            this.tabSize = WorkspaceSettings.DefaultTabSize;
            this.lineHeight = 18;
            this.asciiWidth = 8f;
            this.imeComposition = "";
            this.paintTokens = new List<Token>();
            this.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable | ControlStyles.ResizeRedraw | ControlStyles.EnableNotifyMessage, true);
            this.TabStop = true;
            this.BackColor = Theme.EditorBackground;
            this.ForeColor = Theme.Foreground;
            this.ImeMode = ImeMode.On;
            this.Cursor = Cursors.IBeam;
            this.typographic = (StringFormat)StringFormat.GenericTypographic.Clone();
            this.typographic.FormatFlags = this.typographic.FormatFlags | StringFormatFlags.MeasureTrailingSpaces | StringFormatFlags.NoWrap | StringFormatFlags.FitBlackBox;

            this.vScroll = new ThemedScrollBar(true);
            this.hScroll = new ThemedScrollBar(false);
            this.vScroll.SmallChange = 1;
            this.hScroll.SmallChange = 8;
            this.vScroll.TrackColor = Theme.EditorBackground;
            this.hScroll.TrackColor = Theme.EditorBackground;
            this.vScroll.ValueChanged += this.OnScrollChanged;
            this.hScroll.ValueChanged += this.OnScrollChanged;
            this.Controls.Add(this.vScroll);
            this.Controls.Add(this.hScroll);

            this.caretTimer = new Timer();
            this.caretTimer.Interval = 530;
            this.caretTimer.Tick += this.OnCaretTick;
            this.caretTimer.Start();
        }

        /// <summary>キャレットや本文が変わったとき。</summary>
        public event EventHandler CaretMoved;

        /// <summary>未保存状態が変わったとき。</summary>
        public event EventHandler DocumentChanged;

        /// <summary>編集中の文書。</summary>
        public Document Document
        {
            get { return this.document; }
            set
            {
                this.document = value;
                this.imeComposition = "";
                this.imeCursor = 0;
                this.metricsValid = false;
                // Maximum 縮小の Value クランプが OnScrollChanged 経由で
                // 新しい Document.ScrollY/X を上書きしない（AttachDocument:
                // SaveViewState → setter → RestoreViewState）。
                this.UpdateScrollBars();
                this.Invalidate();
                this.RaiseCaretMoved();
            }
        }

        /// <summary>
        /// 同梱フォントと 96dpi DIP サイズを適用する。物理 px は GetDpi で換算する。
        /// </summary>
        /// <param name="loadResult">同梱フォントの読み込み結果。</param>
        /// <param name="dipSize">本文サイズ（96dpi DIP）。0 以下なら DefaultFontSize。</param>
        /// <param name="tabSpaces">Tab のスペース数。0 以下なら DefaultTabSize。</param>
        public void ApplyFonts(FontLoadResult loadResult, int dipSize, int tabSpaces)
        {
            this.fonts = loadResult;
            this.fontSize = (dipSize > 0) ? dipSize : WorkspaceSettings.DefaultFontSize;
            this.tabSize = (tabSpaces > 0) ? tabSpaces : WorkspaceSettings.DefaultTabSize;
            this.RecreateFonts();
        }

        /// <summary>ハンドル作成後に GetDpi でフォントを作り直す。</summary>
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (this.fonts != null)
            {
                this.RecreateFonts();
            }
        }

        /// <summary>親の DPI 変更後に DIP を保ったまま物理フォントを作り直す。</summary>
        protected override void OnDpiChangedAfterParent(EventArgs e)
        {
            base.OnDpiChangedAfterParent(e);
            if (this.fonts != null)
            {
                this.RecreateFonts();
            }
        }

        /// <summary>Tab で入れるスペース数。</summary>
        public int TabSize { get { return this.tabSize; } }

        /// <summary>IME 未確定文字列があるとき true。</summary>
        public bool IsComposing
        {
            get { return !string.IsNullOrEmpty(this.imeComposition); }
        }

        /// <summary>
        /// Undo。
        /// </summary>
        public void Undo()
        {
            if (this.document == null || !this.document.Undo.CanUndo)
            {
                return;
            }

            this.document.Undo.Undo(this.document.Buffer);
            this.document.HighlightSession.InvalidateFrom(0);
            this.document.HighlightSession.SyncAfterEdit(this.document.Buffer, 0);
            this.document.MarkDirty();
            this.ClampCaret();
            this.NotifyChanged();
        }

        /// <summary>
        /// Redo。
        /// </summary>
        public void Redo()
        {
            if (this.document == null || !this.document.Undo.CanRedo)
            {
                return;
            }

            this.document.Undo.Redo(this.document.Buffer);
            this.document.HighlightSession.InvalidateFrom(0);
            this.document.HighlightSession.SyncAfterEdit(this.document.Buffer, 0);
            this.document.MarkDirty();
            this.ClampCaret();
            this.NotifyChanged();
        }

        /// <summary>
        /// 選択をクリップボードへ。
        /// </summary>
        public void Copy()
        {
            if (this.document == null || !this.document.HasSelection())
            {
                return;
            }

            BufferPoint a;
            BufferPoint b;
            this.document.GetSelection(out a, out b);
            string text = this.document.Buffer.GetText(a, b);
            if (text.Length > 0)
            {
                Clipboard.SetText(text.Replace("\n", "\r\n").Replace("\r\r", "\r"));
            }
        }

        /// <summary>
        /// 切取り。
        /// </summary>
        public void Cut()
        {
            this.Copy();
            this.DeleteSelection();
        }

        /// <summary>
        /// 貼付け。
        /// </summary>
        public void Paste()
        {
            if (this.document == null || !Clipboard.ContainsText())
            {
                return;
            }

            this.InsertText(Clipboard.GetText());
        }

        /// <summary>
        /// 全文選択。
        /// </summary>
        public void SelectAll()
        {
            if (this.document == null)
            {
                return;
            }

            this.document.AnchorLine = 0;
            this.document.AnchorColumn = 0;
            this.document.CaretLine = this.document.Buffer.LineCount - 1;
            this.document.CaretColumn = this.document.Buffer.GetLineLength(this.document.CaretLine);
            this.Invalidate();
            this.RaiseCaretMoved();
        }

        /// <summary>
        /// 現在位置を文書へ書き戻す（タブ切替用）。
        /// </summary>
        public void SaveViewState()
        {
            if (this.document == null)
            {
                return;
            }

            this.document.ScrollY = this.vScroll.Value;
            this.document.ScrollX = this.hScroll.Value;
        }

        /// <summary>
        /// 文書に保存したキャレットとスクロールを復帰する。
        /// </summary>
        public void RestoreViewState()
        {
            this.UpdateScrollBars();
            if (this.document == null)
            {
                return;
            }

            this.SetScrollQuiet(this.vScroll, this.document.ScrollY);
            this.SetScrollQuiet(this.hScroll, this.document.ScrollX);
            this.Invalidate();
            this.RaiseCaretMoved();
        }

        /// <summary>矢印・Tab を入力キーにする。</summary>
        protected override bool IsInputKey(Keys keyData)
        {
            Keys code = keyData & Keys.KeyCode;
            if (code == Keys.Tab || code == Keys.Left || code == Keys.Right || code == Keys.Up || code == Keys.Down
                || code == Keys.Home || code == Keys.End || code == Keys.PageUp || code == Keys.PageDown)
            {
                return true;
            }

            return base.IsInputKey(keyData);
        }

        /// <summary>Tab でフォーカスを渡さない。</summary>
        protected override bool ProcessDialogKey(Keys keyData)
        {
            if ((keyData & Keys.KeyCode) == Keys.Tab)
            {
                return false;
            }

            return base.ProcessDialogKey(keyData);
        }

        /// <summary>文字入力。</summary>
        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            base.OnKeyPress(e);
            if (this.document == null)
            {
                return;
            }

            if (this.IsComposing)
            {
                return;
            }

            if (e.KeyChar == '\r' || e.KeyChar == '\n')
            {
                e.Handled = true;
                this.InsertText("\n");
                return;
            }

            if (e.KeyChar == '\t' || e.KeyChar == 8 || e.KeyChar < 32)
            {
                return;
            }

            this.InsertText(e.KeyChar.ToString());
            e.Handled = true;
        }

        /// <summary>編集キー。</summary>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (this.document == null)
            {
                return;
            }

            if (this.IsComposing)
            {
                if (e.KeyCode == Keys.Back || e.KeyCode == Keys.Delete || e.KeyCode == Keys.Tab
                    || e.KeyCode == Keys.Left || e.KeyCode == Keys.Right || e.KeyCode == Keys.Up || e.KeyCode == Keys.Down
                    || e.KeyCode == Keys.Home || e.KeyCode == Keys.End || e.KeyCode == Keys.PageUp || e.KeyCode == Keys.PageDown)
                {
                    return;
                }
            }

            if (e.KeyCode == Keys.Tab)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                if (e.Shift)
                {
                    this.UnindentLine();
                }
                else
                {
                    this.InsertTabSpaces();
                }

                return;
            }

            if (e.KeyCode == Keys.Back)
            {
                e.Handled = true;
                this.Backspace();
                return;
            }

            if (e.KeyCode == Keys.Delete)
            {
                e.Handled = true;
                this.DeleteForward();
                return;
            }

            if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Right || e.KeyCode == Keys.Up || e.KeyCode == Keys.Down
                || e.KeyCode == Keys.Home || e.KeyCode == Keys.End || e.KeyCode == Keys.PageUp || e.KeyCode == Keys.PageDown)
            {
                e.Handled = true;
                this.MoveCaret(e.KeyCode, e.Shift, e.Control);
            }
        }

        /// <summary>クリックでキャレット。</summary>
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            this.Focus();
            if (this.document == null || e.Button != MouseButtons.Left)
            {
                return;
            }

            BufferPoint p = this.HitTest(e.Location);
            this.document.CaretLine = p.Line;
            this.document.CaretColumn = p.Column;
            if ((Control.ModifierKeys & Keys.Shift) == 0)
            {
                this.document.CollapseSelection();
            }

            this.selecting = true;
            this.caretVisible = true;
            this.EnsureCaretVisible();
            this.Invalidate();
            this.RaiseCaretMoved();
        }

        /// <summary>ドラッグ選択。</summary>
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!this.selecting || this.document == null)
            {
                return;
            }

            BufferPoint p = this.HitTest(e.Location);
            this.document.CaretLine = p.Line;
            this.document.CaretColumn = p.Column;
            this.EnsureCaretVisible();
            this.Invalidate();
            this.RaiseCaretMoved();
        }

        /// <summary>ドラッグ終了。</summary>
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            this.selecting = false;
        }

        /// <summary>ホイールで行スクロール。Shift なら横。</summary>
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if ((Control.ModifierKeys & Keys.Shift) != 0)
            {
                int dpi = DpiUtil.GetDpi(this.IsHandleCreated ? this.Handle : IntPtr.Zero);
                int dx = DpiUtil.WheelToPixels(e.Delta, DpiUtil.ToPixels(32, dpi));
                this.SetScrollQuiet(this.hScroll, this.hScroll.Value + dx);
                this.Invalidate();
                return;
            }

            base.OnMouseWheel(e);
            int notches = DpiUtil.WheelNotches(e.Delta, ref this.wheelLeftover);
            this.SetScrollQuiet(this.vScroll, this.vScroll.Value + (-notches * 3));
            this.Invalidate();
        }

        /// <summary>スクロールバー配置。</summary>
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            this.UpdateScrollBars();
        }

        /// <summary>行番号・本文・キャレットを描く。</summary>
        protected override void OnPaint(PaintEventArgs e)
        {
            int dpi = DpiUtil.GetDpi(this.IsHandleCreated ? this.Handle : IntPtr.Zero);
            if ((dpi != this.lastDpi || this.halfFont == null) && this.fonts != null)
            {
                this.lastDpi = dpi;
                this.RecreateFonts();
            }

            Graphics g = e.Graphics;
            g.Clear(Theme.EditorBackground);
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            this.EnsureMetrics(g);
            if (this.document == null || this.halfFont == null || this.fullFont == null)
            {
                return;
            }

            Rectangle textArea = this.GetTextArea();
            int first = this.vScroll.Value;
            int visible = Math.Max(1, textArea.Height / this.lineHeight + 1);
            int last = Math.Min(this.document.Buffer.LineCount - 1, first + visible);
            int caretLine = this.document.CaretLine;

            using (SolidBrush gutterBg = new SolidBrush(Theme.Background))
            {
                g.FillRectangle(gutterBg, 0, 0, this.gutterWidth, textArea.Height);
            }

            int leftPad;
            int rightPad;
            int textInset;
            this.GetGutterPads(out leftPad, out rightPad, out textInset);

            Rectangle body = DpiUtil.TextBodyClip(this.gutterWidth, textArea);
            GraphicsState state = g.Save();
            try
            {
                if (body.Width > 0 && body.Height > 0)
                {
                    g.SetClip(body);
                }

                if (caretLine >= first && caretLine <= last)
                {
                    int y = (caretLine - first) * this.lineHeight;
                    using (SolidBrush cur = new SolidBrush(Theme.CurrentLine))
                    {
                        g.FillRectangle(cur, this.gutterWidth, y, textArea.Width, this.lineHeight);
                    }
                }

                this.PaintSelection(g, first, last, textArea);

                using (SolidBrush fg = new SolidBrush(Theme.Foreground))
                using (SolidBrush keywordBrush = new SolidBrush(Theme.Keyword))
                using (SolidBrush stringBrush = new SolidBrush(Theme.StringLiteral))
                using (SolidBrush numberBrush = new SolidBrush(Theme.Number))
                using (SolidBrush commentBrush = new SolidBrush(Theme.Comment))
                using (SolidBrush localBrush = new SolidBrush(Theme.Local))
                using (SolidBrush instanceBrush = new SolidBrush(Theme.Instance))
                using (SolidBrush methodBrush = new SolidBrush(Theme.Method))
                using (SolidBrush typeBrush = new SolidBrush(Theme.Type))
                using (SolidBrush caretBrush = new SolidBrush(Theme.Foreground))
                {
                    for (int i = first; i <= last; i++)
                    {
                        int y = (i - first) * this.lineHeight;
                        this.DrawLineText(g, fg, keywordBrush, stringBrush, numberBrush, commentBrush, localBrush, instanceBrush, methodBrush, typeBrush, i, y, textArea);
                    }

                    float prefixWidth = 0f;
                    if (!string.IsNullOrEmpty(this.imeComposition))
                    {
                        float imeWidth = this.MeasureRun(g, this.imeComposition);
                        int cursor = ImeLayout.ClampCursor(this.imeCursor, this.imeComposition.Length);
                        if (cursor > 0)
                        {
                            prefixWidth = this.MeasureRun(g, this.imeComposition.Substring(0, cursor));
                        }

                        float cx = this.ColumnToX(caretLine, this.document.CaretColumn) - this.hScroll.Value;
                        int cy = (caretLine - first) * this.lineHeight;
                        float x = this.gutterWidth + textInset + cx;
                        using (SolidBrush imeBg = new SolidBrush(Theme.Selection))
                        {
                            g.FillRectangle(imeBg, x, cy, imeWidth, this.lineHeight);
                        }

                        this.DrawRun(g, fg, this.imeComposition, x, cy);
                        using (Pen underline = new Pen(Theme.Foreground))
                        {
                            g.DrawLine(underline, x, cy + this.lineHeight - 2, x + imeWidth, cy + this.lineHeight - 2);
                        }

                        this.caretVisible = true;
                    }

                    if (this.Focused && this.caretVisible)
                    {
                        int cy = (caretLine - first) * this.lineHeight;
                        int x = ImeLayout.ClientX(this.gutterWidth, textInset, this.ColumnToX(caretLine, this.document.CaretColumn), this.hScroll.Value, prefixWidth);
                        if (x >= this.gutterWidth && x < textArea.Right)
                        {
                            g.FillRectangle(caretBrush, x, cy + 1, 1, this.lineHeight - 2);
                        }
                    }
                }
            }
            finally
            {
                g.Restore(state);
            }

            using (SolidBrush ln = new SolidBrush(Theme.LineNumber))
            {
                for (int i = first; i <= last; i++)
                {
                    int y = (i - first) * this.lineHeight;
                    string num = (i + 1).ToString();
                    float numW = 0f;
                    if (this.halfFont != null)
                    {
                        numW = g.MeasureString(num, this.halfFont, new PointF(0, 0), this.typographic).Width;
                    }

                    float nx = this.gutterWidth - rightPad - numW;
                    if (nx < leftPad)
                    {
                        nx = leftPad;
                    }

                    g.DrawString(num, this.halfFont, ln, nx, y + this.BaselineOffset(this.halfFont), this.typographic);
                }
            }

            using (Pen border = new Pen(Theme.Border))
            {
                g.DrawLine(border, this.gutterWidth, 0, this.gutterWidth, textArea.Height);
            }
        }

        /// <summary>フォーカスでキャレット点滅を再開。</summary>
        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            this.caretVisible = true;
            this.Invalidate();
        }

        /// <summary>IME の未確定文字列。HWHEEL は横スクロール。</summary>
        protected override void WndProc(ref Message m)
        {
            const int WM_MOUSEHWHEEL = 0x020E;
            if (m.Msg == WM_MOUSEHWHEEL)
            {
                long wp = m.WParam.ToInt64();
                short delta = (short)((wp >> 16) & 0xFFFF);
                int dpi = DpiUtil.GetDpi(this.IsHandleCreated ? this.Handle : IntPtr.Zero);
                int dx = DpiUtil.WheelToPixels(delta, DpiUtil.ToPixels(32, dpi));
                this.SetScrollQuiet(this.hScroll, this.hScroll.Value + dx);
                this.Invalidate();
                m.Result = (IntPtr)1;
                return;
            }

            const int WM_IME_STARTCOMPOSITION = 0x010D;
            if (m.Msg == WM_IME_STARTCOMPOSITION)
            {
                this.UpdateImeWindow();
                m.Result = IntPtr.Zero;
                return;
            }

            const int WM_IME_REQUEST = 0x0288;
            const int IMR_QUERYCHARPOSITION = 6;
            if (m.Msg == WM_IME_REQUEST && m.WParam.ToInt32() == IMR_QUERYCHARPOSITION)
            {
                if (m.LParam != IntPtr.Zero)
                {
                    this.FillImeCharPosition(m.LParam);
                }

                m.Result = (IntPtr)1;
                return;
            }

            const int WM_IME_SETCONTEXT = 0x0281;
            if (m.Msg == WM_IME_SETCONTEXT)
            {
                long lp = m.LParam.ToInt64();
                lp = lp & ~0x80000000L;
                m.LParam = (IntPtr)lp;
            }

            const int WM_IME_COMPOSITION = 0x010F;
            const int GCS_COMPSTR = 0x0008;
            const int GCS_CURSORPOS = 0x0080;
            const int GCS_RESULTSTR = 0x0800;
            if (m.Msg == WM_IME_COMPOSITION)
            {
                int flag = m.LParam.ToInt32();
                if ((flag & GCS_COMPSTR) != 0)
                {
                    this.imeComposition = NativeIme.GetCompositionString(this.Handle, GCS_COMPSTR);
                }

                if ((flag & GCS_CURSORPOS) != 0)
                {
                    int length = (this.imeComposition == null) ? 0 : this.imeComposition.Length;
                    this.imeCursor = ImeLayout.ClampCursor(NativeIme.GetCursorPos(this.Handle), length);
                }

                if ((flag & GCS_RESULTSTR) != 0)
                {
                    this.imeComposition = "";
                }

                this.UpdateImeWindow();
                this.Invalidate();
            }

            const int WM_IME_ENDCOMPOSITION = 0x010E;
            if (m.Msg == WM_IME_ENDCOMPOSITION)
            {
                this.imeComposition = "";
                this.imeCursor = 0;
                this.Invalidate();
            }

            base.WndProc(ref m);

            if (m.Msg == WM_IME_SETCONTEXT)
            {
                this.UpdateImeWindow();
            }
        }

        /// <summary>破棄。</summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this.caretTimer != null)
                {
                    this.caretTimer.Dispose();
                }

                if (this.typographic != null)
                {
                    this.typographic.Dispose();
                }

                if (this.halfFont != null)
                {
                    this.halfFont.Dispose();
                }

                if (this.fullFont != null)
                {
                    this.fullFont.Dispose();
                }
            }

            base.Dispose(disposing);
        }

        private void InsertTabSpaces()
        {
            int col = this.document.CaretColumn;
            int n = this.tabSize - (col % this.tabSize);
            if (n <= 0)
            {
                n = this.tabSize;
            }

            this.InsertText(new string(' ', n));
        }

        private void UnindentLine()
        {
            int line = this.document.CaretLine;
            string text = this.document.Buffer.GetLine(line);
            int remove = 0;
            while (remove < this.tabSize && remove < text.Length && text[remove] == ' ')
            {
                remove++;
            }

            if (remove == 0)
            {
                return;
            }

            BufferPoint start = new BufferPoint(line, 0);
            BufferPoint end = new BufferPoint(line, remove);
            string deleted = this.document.Buffer.Delete(start, end);
            this.document.Undo.RecordDelete(line, 0, deleted);
            this.document.CaretColumn = Math.Max(0, this.document.CaretColumn - remove);
            this.document.CollapseSelection();
            this.document.MarkDirty();
            this.SyncHighlight(line);
            this.NotifyChanged();
        }

        private void InsertText(string text)
        {
            if (this.document == null || text == null || text.Length == 0)
            {
                return;
            }

            int editLine = this.document.CaretLine;
            bool compound = this.document.HasSelection();
            if (compound)
            {
                BufferPoint selStart;
                BufferPoint selEnd;
                this.document.GetSelection(out selStart, out selEnd);
                editLine = selStart.Line;
                this.document.Undo.BeginCompound();
                this.DeleteSelectionCore();
            }

            int line = this.document.CaretLine;
            int col = this.document.CaretColumn;
            BufferPoint end = this.document.Buffer.Insert(line, col, text);
            this.document.Undo.RecordInsert(line, col, text);
            if (compound)
            {
                this.document.Undo.EndCompound();
            }

            this.document.CaretLine = end.Line;
            this.document.CaretColumn = end.Column;
            this.document.CollapseSelection();
            this.document.MarkDirty();
            this.SyncHighlight(editLine, end.Line);
            this.EnsureCaretVisible();
            this.NotifyChanged();
        }

        private void DeleteSelection()
        {
            if (this.document == null || !this.document.HasSelection())
            {
                return;
            }

            BufferPoint a;
            BufferPoint b;
            this.document.GetSelection(out a, out b);
            this.DeleteSelectionCore();
            this.document.MarkDirty();
            this.SyncHighlight(a.Line);
            this.NotifyChanged();
        }

        private void DeleteSelectionCore()
        {
            BufferPoint a;
            BufferPoint b;
            this.document.GetSelection(out a, out b);
            string deleted = this.document.Buffer.Delete(a, b);
            this.document.Undo.RecordDelete(a.Line, a.Column, deleted);
            this.document.CaretLine = a.Line;
            this.document.CaretColumn = a.Column;
            this.document.CollapseSelection();
        }

        private void Backspace()
        {
            if (this.document.HasSelection())
            {
                this.DeleteSelection();
                return;
            }

            if (this.document.CaretLine == 0 && this.document.CaretColumn == 0)
            {
                return;
            }

            int editLine;
            if (this.document.CaretColumn > 0)
            {
                int line = this.document.CaretLine;
                int col = this.document.CaretColumn - 1;
                string ch = this.document.Buffer.GetLine(line).Substring(col, 1);
                BufferPoint a = new BufferPoint(line, col);
                BufferPoint b = new BufferPoint(line, this.document.CaretColumn);
                this.document.Buffer.Delete(a, b);
                this.document.Undo.RecordDelete(line, col, ch);
                this.document.CaretColumn = col;
                editLine = line;
            }
            else
            {
                int line = this.document.CaretLine;
                int prevLen = this.document.Buffer.GetLineLength(line - 1);
                string deleted = this.document.Buffer.NewLine;
                BufferPoint a = new BufferPoint(line - 1, prevLen);
                BufferPoint b = new BufferPoint(line, 0);
                this.document.Buffer.Delete(a, b);
                this.document.Undo.RecordDelete(line - 1, prevLen, deleted);
                this.document.CaretLine = line - 1;
                this.document.CaretColumn = prevLen;
                editLine = line - 1;
            }

            this.document.CollapseSelection();
            this.document.MarkDirty();
            this.SyncHighlight(editLine);
            this.EnsureCaretVisible();
            this.NotifyChanged();
        }

        private void DeleteForward()
        {
            if (this.document.HasSelection())
            {
                this.DeleteSelection();
                return;
            }

            int line = this.document.CaretLine;
            int col = this.document.CaretColumn;
            string row = this.document.Buffer.GetLine(line);
            if (col < row.Length)
            {
                string ch = row.Substring(col, 1);
                BufferPoint a = new BufferPoint(line, col);
                BufferPoint b = new BufferPoint(line, col + 1);
                this.document.Buffer.Delete(a, b);
                this.document.Undo.RecordDelete(line, col, ch);
            }
            else if (line < this.document.Buffer.LineCount - 1)
            {
                string deleted = this.document.Buffer.NewLine;
                BufferPoint a = new BufferPoint(line, col);
                BufferPoint b = new BufferPoint(line + 1, 0);
                this.document.Buffer.Delete(a, b);
                this.document.Undo.RecordDelete(line, col, deleted);
            }
            else
            {
                return;
            }

            this.document.CollapseSelection();
            this.document.MarkDirty();
            this.SyncHighlight(line);
            this.NotifyChanged();
        }

        private void MoveCaret(Keys key, bool shift, bool control)
        {
            int line = this.document.CaretLine;
            int col = this.document.CaretColumn;
            if (key == Keys.Left)
            {
                if (control)
                {
                    this.MoveWord(-1);
                }
                else if (col > 0)
                {
                    this.document.CaretColumn = col - 1;
                }
                else if (line > 0)
                {
                    this.document.CaretLine = line - 1;
                    this.document.CaretColumn = this.document.Buffer.GetLineLength(line - 1);
                }
            }
            else if (key == Keys.Right)
            {
                if (control)
                {
                    this.MoveWord(1);
                }
                else if (col < this.document.Buffer.GetLineLength(line))
                {
                    this.document.CaretColumn = col + 1;
                }
                else if (line < this.document.Buffer.LineCount - 1)
                {
                    this.document.CaretLine = line + 1;
                    this.document.CaretColumn = 0;
                }
            }
            else if (key == Keys.Up)
            {
                if (line > 0)
                {
                    this.document.CaretLine = line - 1;
                    this.document.CaretColumn = Math.Min(col, this.document.Buffer.GetLineLength(line - 1));
                }
            }
            else if (key == Keys.Down)
            {
                if (line < this.document.Buffer.LineCount - 1)
                {
                    this.document.CaretLine = line + 1;
                    this.document.CaretColumn = Math.Min(col, this.document.Buffer.GetLineLength(line + 1));
                }
            }
            else if (key == Keys.Home)
            {
                if (control)
                {
                    this.document.CaretLine = 0;
                    this.document.CaretColumn = 0;
                }
                else
                {
                    this.document.CaretColumn = 0;
                }
            }
            else if (key == Keys.End)
            {
                if (control)
                {
                    this.document.CaretLine = this.document.Buffer.LineCount - 1;
                    this.document.CaretColumn = this.document.Buffer.GetLineLength(this.document.CaretLine);
                }
                else
                {
                    this.document.CaretColumn = this.document.Buffer.GetLineLength(line);
                }
            }
            else if (key == Keys.PageUp)
            {
                int page = Math.Max(1, this.GetTextArea().Height / this.lineHeight);
                this.document.CaretLine = Math.Max(0, line - page);
                this.document.CaretColumn = Math.Min(this.document.CaretColumn, this.document.Buffer.GetLineLength(this.document.CaretLine));
            }
            else if (key == Keys.PageDown)
            {
                int page = Math.Max(1, this.GetTextArea().Height / this.lineHeight);
                this.document.CaretLine = Math.Min(this.document.Buffer.LineCount - 1, line + page);
                this.document.CaretColumn = Math.Min(this.document.CaretColumn, this.document.Buffer.GetLineLength(this.document.CaretLine));
            }

            this.ClampCaret();
            if (!shift)
            {
                this.document.CollapseSelection();
            }

            this.caretVisible = true;
            this.EnsureCaretVisible();
            this.Invalidate();
            this.RaiseCaretMoved();
        }

        private void MoveWord(int direction)
        {
            int line = this.document.CaretLine;
            int col = this.document.CaretColumn;
            string text = this.document.Buffer.GetLine(line);
            if (direction < 0)
            {
                if (col == 0 && line > 0)
                {
                    this.document.CaretLine = line - 1;
                    this.document.CaretColumn = this.document.Buffer.GetLineLength(line - 1);
                    return;
                }

                int i = Math.Max(0, col - 1);
                while (i > 0 && char.IsWhiteSpace(text[i]))
                {
                    i--;
                }

                while (i > 0 && !char.IsWhiteSpace(text[i - 1]))
                {
                    i--;
                }

                this.document.CaretColumn = i;
            }
            else
            {
                if (col >= text.Length && line < this.document.Buffer.LineCount - 1)
                {
                    this.document.CaretLine = line + 1;
                    this.document.CaretColumn = 0;
                    return;
                }

                int i = col;
                while (i < text.Length && !char.IsWhiteSpace(text[i]))
                {
                    i++;
                }

                while (i < text.Length && char.IsWhiteSpace(text[i]))
                {
                    i++;
                }

                this.document.CaretColumn = i;
            }
        }

        private void ClampCaret()
        {
            BufferPoint p = this.document.Buffer.Clamp(new BufferPoint(this.document.CaretLine, this.document.CaretColumn));
            this.document.CaretLine = p.Line;
            this.document.CaretColumn = p.Column;
            p = this.document.Buffer.Clamp(new BufferPoint(this.document.AnchorLine, this.document.AnchorColumn));
            this.document.AnchorLine = p.Line;
            this.document.AnchorColumn = p.Column;
        }

        private void EnsureCaretVisible()
        {
            this.UpdateScrollBars();
            int first = this.vScroll.Value;
            int visible = Math.Max(1, this.GetTextArea().Height / Math.Max(1, this.lineHeight));
            int line = this.document.CaretLine;
            if (line < first)
            {
                this.SetScrollQuiet(this.vScroll, line);
            }
            else if (line >= first + visible)
            {
                this.SetScrollQuiet(this.vScroll, line - visible + 1);
            }

            float origin = this.ColumnToX(line, this.document.CaretColumn);
            float x = origin;
            if (this.IsComposing && this.halfFont != null)
            {
                using (Graphics g = this.CreateGraphics())
                {
                    float total = this.MeasureRun(g, this.imeComposition);
                    int cursor = ImeLayout.ClampCursor(this.imeCursor, this.imeComposition.Length);
                    float prefix = 0f;
                    if (cursor > 0)
                    {
                        prefix = this.MeasureRun(g, this.imeComposition.Substring(0, cursor));
                    }

                    if (total < prefix)
                    {
                        total = prefix;
                    }

                    x = origin + total;
                }
            }

            int view = this.TextViewportWidth(this.GetTextArea().Width, 0, this.GetTextInset());
            if (origin < this.hScroll.Value)
            {
                this.SetScrollQuiet(this.hScroll, (int)origin);
            }
            else if (x > this.hScroll.Value + view)
            {
                this.SetScrollQuiet(this.hScroll, (int)x - view + 8);
            }
        }

        private BufferPoint HitTest(Point pt)
        {
            if (this.document == null)
            {
                return new BufferPoint(0, 0);
            }

            Rectangle area = this.GetTextArea();
            int line = this.vScroll.Value + (pt.Y / Math.Max(1, this.lineHeight));
            if (line < 0)
            {
                line = 0;
            }

            if (line >= this.document.Buffer.LineCount)
            {
                line = this.document.Buffer.LineCount - 1;
            }

            float x = pt.X - this.gutterWidth - this.GetTextInset() + this.hScroll.Value;
            string text = this.document.Buffer.GetLine(line);
            float acc = 0f;
            int col = 0;
            using (Graphics g = this.CreateGraphics())
            {
                this.EnsureMetrics(g);
                while (col < text.Length)
                {
                    int count;
                    float w = this.MeasureGlyph(g, text, col, out count);
                    if (acc + w / 2f >= x)
                    {
                        break;
                    }

                    acc += w;
                    col += count;
                }
            }

            return new BufferPoint(line, col);
        }

        private void DrawLineText(Graphics g, Brush fg, Brush keywordBrush, Brush stringBrush, Brush numberBrush, Brush commentBrush, Brush localBrush, Brush instanceBrush, Brush methodBrush, Brush typeBrush, int line, int y, Rectangle textArea)
        {
            string text = this.document.Buffer.GetLine(line);
            this.paintTokens.Clear();
            if (this.document.HighlightSession != null)
            {
                int startState = this.document.HighlightSession.GetStartState(line);
                ILineLexer lexer = LexerRegistry.Get(this.document.Language);
                int endState;
                lexer.ScanLine(text, startState, this.paintTokens, out endState);
                this.document.HighlightSession.ApplyIdentifierOverlay(line, this.paintTokens);
            }

            float x = this.gutterWidth + this.GetTextInset() - this.hScroll.Value;
            int i = 0;
            while (i < text.Length)
            {
                int count;
                bool half = GlyphClassifier.UseHalfWidthFont(text, i, out count);
                Font font = half ? this.halfFont : this.fullFont;
                string ch;
                float w;
                if (text[i] == '\t')
                {
                    int visualCol = this.VisualColumn(text, i);
                    int spaces = this.tabSize - (visualCol % this.tabSize);
                    if (spaces <= 0)
                    {
                        spaces = this.tabSize;
                    }

                    w = spaces * this.asciiWidth;
                    ch = "";
                    count = 1;
                }
                else
                {
                    ch = text.Substring(i, count);
                    w = g.MeasureString(ch, font, new PointF(0, 0), this.typographic).Width;
                }

                if (x + w >= this.gutterWidth && x <= textArea.Right && ch.Length > 0)
                {
                    Brush brush = this.BrushForToken(this.TokenKindAt(i), fg, keywordBrush, stringBrush, numberBrush, commentBrush, localBrush, instanceBrush, methodBrush, typeBrush);
                    g.DrawString(ch, font, brush, x, y + this.BaselineOffset(font), this.typographic);
                }

                x += w;
                i += count;
                if (x > textArea.Right + 64)
                {
                    break;
                }
            }
        }

        private TokenKind TokenKindAt(int index)
        {
            for (int t = 0; t < this.paintTokens.Count; t++)
            {
                Token token = this.paintTokens[t];
                if (index >= token.Start && index < token.Start + token.Length)
                {
                    return token.Kind;
                }
            }

            return TokenKind.Text;
        }

        private Brush BrushForToken(TokenKind kind, Brush fg, Brush keywordBrush, Brush stringBrush, Brush numberBrush, Brush commentBrush, Brush localBrush, Brush instanceBrush, Brush methodBrush, Brush typeBrush)
        {
            switch (kind)
            {
                case TokenKind.Keyword:
                    return keywordBrush;
                case TokenKind.String:
                    return stringBrush;
                case TokenKind.Number:
                    return numberBrush;
                case TokenKind.Comment:
                    return commentBrush;
                case TokenKind.Local:
                    return localBrush;
                case TokenKind.Instance:
                    return instanceBrush;
                case TokenKind.Method:
                    return methodBrush;
                case TokenKind.Type:
                    return typeBrush;
                default:
                    return fg;
            }
        }

        private void SyncHighlight(int editLine)
        {
            this.SyncHighlight(editLine, editLine);
        }

        private void SyncHighlight(int editLine, int lastModifiedLine)
        {
            if (this.document == null || this.document.HighlightSession == null)
            {
                return;
            }

            this.document.HighlightSession.SyncAfterEdit(this.document.Buffer, editLine, lastModifiedLine);
        }

        private void DrawRun(Graphics g, Brush fg, string text, float x, int y)
        {
            int i = 0;
            while (i < text.Length)
            {
                int count;
                bool half = GlyphClassifier.UseHalfWidthFont(text, i, out count);
                Font font = half ? this.halfFont : this.fullFont;
                string ch = text.Substring(i, count);
                g.DrawString(ch, font, fg, x, y + this.BaselineOffset(font), this.typographic);
                x += g.MeasureString(ch, font, new PointF(0, 0), this.typographic).Width;
                i += count;
            }
        }

        private float MeasureRun(Graphics g, string text)
        {
            float w = 0f;
            int i = 0;
            while (i < text.Length)
            {
                int count;
                w += this.MeasureGlyph(g, text, i, out count);
                i += count;
            }

            return w;
        }

        private float MeasureGlyph(Graphics g, string text, int index, out int count)
        {
            if (text[index] == '\t')
            {
                count = 1;
                int visualCol = this.VisualColumn(text, index);
                int spaces = this.tabSize - (visualCol % this.tabSize);
                if (spaces <= 0)
                {
                    spaces = this.tabSize;
                }

                return spaces * this.asciiWidth;
            }

            bool half = GlyphClassifier.UseHalfWidthFont(text, index, out count);
            Font font = half ? this.halfFont : this.fullFont;
            string ch = text.Substring(index, count);
            return g.MeasureString(ch, font, new PointF(0, 0), this.typographic).Width;
        }

        private int VisualColumn(string text, int index)
        {
            int col = 0;
            for (int i = 0; i < index && i < text.Length; i++)
            {
                if (text[i] == '\t')
                {
                    int add = this.tabSize - (col % this.tabSize);
                    if (add <= 0)
                    {
                        add = this.tabSize;
                    }

                    col += add;
                }
                else
                {
                    col++;
                }
            }

            return col;
        }

        private float ColumnToX(int line, int column)
        {
            if (this.document == null || this.halfFont == null)
            {
                return 0f;
            }

            string text = this.document.Buffer.GetLine(line);
            if (column > text.Length)
            {
                column = text.Length;
            }

            float x = 0f;
            using (Graphics g = this.CreateGraphics())
            {
                this.EnsureMetrics(g);
                int i = 0;
                while (i < column && i < text.Length)
                {
                    int count;
                    x += this.MeasureGlyph(g, text, i, out count);
                    i += count;
                }
            }

            return x;
        }

        private void PaintSelection(Graphics g, int first, int last, Rectangle textArea)
        {
            if (!this.document.HasSelection())
            {
                return;
            }

            BufferPoint a;
            BufferPoint b;
            this.document.GetSelection(out a, out b);
            using (SolidBrush sel = new SolidBrush(Theme.Selection))
            {
                for (int line = Math.Max(first, a.Line); line <= Math.Min(last, b.Line); line++)
                {
                    int startCol = (line == a.Line) ? a.Column : 0;
                    int endCol = (line == b.Line) ? b.Column : this.document.Buffer.GetLineLength(line);
                    if (line != b.Line && startCol == endCol)
                    {
                        endCol = this.document.Buffer.GetLineLength(line);
                    }

                    float x0 = this.ColumnToX(line, startCol) - this.hScroll.Value;
                    float x1 = this.ColumnToX(line, endCol) - this.hScroll.Value;
                    if (x1 <= x0)
                    {
                        x1 = x0 + 4f;
                    }

                    int y = (line - first) * this.lineHeight;
                    g.FillRectangle(sel, this.gutterWidth + this.GetTextInset() + x0, y, x1 - x0, this.lineHeight);
                }
            }
        }

        private float BaselineOffset(Font font)
        {
            float ascent = (font == this.fullFont) ? this.fullAscent : this.halfAscent;
            float max = Math.Max(this.halfAscent, this.fullAscent);
            return max - ascent + 1f;
        }

        private void EnsureMetrics(Graphics g)
        {
            if (this.metricsValid || this.halfFont == null || this.fullFont == null)
            {
                if (this.halfFont != null)
                {
                    this.gutterWidth = this.ComputeGutterWidth(g);
                }

                return;
            }

            this.halfAscent = Ascent(this.halfFont);
            this.fullAscent = Ascent(this.fullFont);
            int dpi = DpiUtil.GetDpi(this.IsHandleCreated ? this.Handle : IntPtr.Zero);
            int physicalPx = DpiUtil.ToPixels(this.fontSize, dpi);
            int extra = DpiUtil.ToPixels(4, dpi);
            int cell = this.halfFont.Height;
            if (this.fullFont.Height > cell)
            {
                cell = this.fullFont.Height;
            }

            this.lineHeight = DpiUtil.EditorLineHeight(cell, physicalPx, extra);
            this.asciiWidth = g.MeasureString("M", this.halfFont, new PointF(0, 0), this.typographic).Width;
            this.gutterWidth = this.ComputeGutterWidth(g);
            this.metricsValid = true;
        }

        private int ComputeGutterWidth(Graphics g)
        {
            int dpi = DpiUtil.GetDpi(this.IsHandleCreated ? this.Handle : IntPtr.Zero);
            int leftPad;
            int rightPad;
            int textInset;
            this.GetGutterPads(dpi, out leftPad, out rightPad, out textInset);
            int lines = (this.document == null) ? 1 : this.document.Buffer.LineCount;
            string sample = lines.ToString() + "9";
            float measured = 0f;
            if (this.halfFont != null)
            {
                measured = g.MeasureString(sample, this.halfFont, new PointF(0, 0), this.typographic).Width;
            }

            int gw = leftPad + (int)Math.Ceiling(measured) + rightPad;
            int minGutter = DpiUtil.ToPixels(36, dpi);
            if (gw < minGutter)
            {
                gw = minGutter;
            }

            return gw;
        }

        private int GetTextInset()
        {
            int leftPad;
            int rightPad;
            int textInset;
            this.GetGutterPads(out leftPad, out rightPad, out textInset);
            return textInset;
        }

        private void GetGutterPads(out int leftPad, out int rightPad, out int textInset)
        {
            int dpi = DpiUtil.GetDpi(this.IsHandleCreated ? this.Handle : IntPtr.Zero);
            this.GetGutterPads(dpi, out leftPad, out rightPad, out textInset);
        }

        private void GetGutterPads(int dpi, out int leftPad, out int rightPad, out int textInset)
        {
            leftPad = DpiUtil.ToPixels(DpiUtil.LineNumberPadDip, dpi);
            rightPad = leftPad;
            textInset = DpiUtil.ToPixels(DpiUtil.TextInsetDip, dpi);
        }

        private void RecreateFonts()
        {
            if (this.halfFont != null)
            {
                this.halfFont.Dispose();
                this.halfFont = null;
            }

            if (this.fullFont != null)
            {
                this.fullFont.Dispose();
                this.fullFont = null;
            }

            if (this.fonts != null)
            {
                int dpi = DpiUtil.GetDpi(this.IsHandleCreated ? this.Handle : IntPtr.Zero);
                this.lastDpi = dpi;
                int physicalPx = DpiUtil.ToPixels(this.fontSize, dpi);
                this.halfFont = this.fonts.CreateHalfWidth(physicalPx);
                this.fullFont = this.fonts.CreateFullWidth(physicalPx);
            }

            this.metricsValid = false;
            this.Invalidate();
        }

        private static float Ascent(Font font)
        {
            return font.Size * font.FontFamily.GetCellAscent(font.Style) / font.FontFamily.GetEmHeight(font.Style);
        }

        private Rectangle GetTextArea()
        {
            int vw = 0;
            int hh = 0;
            if (this.vScroll != null && this.vScroll.Visible)
            {
                vw = this.vScroll.Width;
            }

            if (this.hScroll != null && this.hScroll.Visible)
            {
                hh = this.hScroll.Height;
            }

            return new Rectangle(0, 0, Math.Max(0, this.Width - vw), Math.Max(0, this.Height - hh));
        }

        /// <summary>
        /// 本文が横に見える幅。ガターとバーは含めない。
        /// </summary>
        /// <param name="areaWidth">クライアントまたは GetTextArea の幅。</param>
        /// <param name="vBar">まだ Bounds に出ていない縦バーの幅。GetTextArea 済みなら 0。</param>
        /// <param name="textInset">ガター右の本文インセット。</param>
        /// <returns>1 以上の幅（物理 px）。</returns>
        private int TextViewportWidth(int areaWidth, int vBar, int textInset)
        {
            int w = areaWidth - vBar - this.gutterWidth - textInset;
            if (w < 1)
            {
                return 1;
            }

            return w;
        }

        /// <summary>
        /// 全行を描画と同じ MeasureRun で測った最大幅。
        /// </summary>
        /// <returns>内容幅（物理 px）。測れなければ 0。</returns>
        private int MeasureContentWidth()
        {
            if (this.document == null || this.halfFont == null || this.fullFont == null || !this.IsHandleCreated)
            {
                return 0;
            }

            int max = 0;
            using (Graphics g = this.CreateGraphics())
            {
                this.EnsureMetrics(g);
                for (int i = 0; i < this.document.Buffer.LineCount; i++)
                {
                    int px = (int)Math.Ceiling(this.MeasureRun(g, this.document.Buffer.GetLine(i)));
                    if (px > max)
                    {
                        max = px;
                    }
                }
            }

            return max;
        }

        private void UpdateScrollBars()
        {
            this.ignoreScroll = true;
            try
            {
                this.UpdateScrollBarsCore();
            }
            finally
            {
                this.ignoreScroll = false;
            }
        }

        private void UpdateScrollBarsCore()
        {
            int dpi = DpiUtil.GetDpi(this.IsHandleCreated ? this.Handle : IntPtr.Zero);
            int bar = DpiUtil.ToPixels(DpiUtil.ScrollBarThicknessDip, dpi);
            int clientW = this.ClientSize.Width;
            int clientH = this.ClientSize.Height;
            int lineCount = (this.document == null) ? 1 : this.document.Buffer.LineCount;
            int lineH = this.lineHeight;
            if (lineH < 1)
            {
                lineH = 1;
            }

            int contentPx = this.MeasureContentWidth();
            int textInset = this.GetTextInset();

            int viewportH = clientH;
            int visibleLines = viewportH / lineH;
            if (visibleLines < 1)
            {
                visibleLines = 1;
            }

            bool needV = lineCount > visibleLines;
            int viewportW = this.TextViewportWidth(clientW, needV ? bar : 0, textInset);
            bool needH = contentPx > viewportW;
            if (needH)
            {
                viewportH = clientH - bar;
                visibleLines = viewportH / lineH;
                if (visibleLines < 1)
                {
                    visibleLines = 1;
                }

                needV = lineCount > visibleLines;
                viewportW = this.TextViewportWidth(clientW, needV ? bar : 0, textInset);
                needH = contentPx > viewportW;
            }

            int vW = needV ? bar : 0;
            int hH = needH ? bar : 0;
            this.vScroll.Visible = needV;
            this.hScroll.Visible = needH;
            this.vScroll.Bounds = new Rectangle(clientW - vW, 0, vW, Math.Max(0, clientH - hH));
            this.hScroll.Bounds = new Rectangle(0, clientH - hH, Math.Max(0, clientW - vW), hH);

            this.vScroll.Minimum = 0;
            this.vScroll.Maximum = Math.Max(0, lineCount - 1);
            this.vScroll.LargeChange = visibleLines;
            this.hScroll.Minimum = 0;
            int hMax = contentPx;
            if (needH)
            {
                hMax = contentPx + textInset;
            }

            this.hScroll.Maximum = Math.Max(0, hMax - 1);
            this.hScroll.LargeChange = Math.Max(1, viewportW);

            if (!needV && this.vScroll.Value != 0)
            {
                this.vScroll.Value = 0;
            }

            if (!needH && this.hScroll.Value != 0)
            {
                this.hScroll.Value = 0;
            }
        }

        private void SetScrollQuiet(ThemedScrollBar bar, int value)
        {
            int max = Math.Max(bar.Minimum, bar.Maximum - bar.LargeChange + 1);
            if (value < bar.Minimum)
            {
                value = bar.Minimum;
            }

            if (value > max)
            {
                value = max;
            }

            this.ignoreScroll = true;
            try
            {
                if (bar.Value != value)
                {
                    bar.Value = value;
                }
            }
            finally
            {
                this.ignoreScroll = false;
            }

            if (this.document != null && bar.Visible)
            {
                if (object.ReferenceEquals(bar, this.vScroll))
                {
                    this.document.ScrollY = bar.Value;
                }
                else
                {
                    this.document.ScrollX = bar.Value;
                }
            }
        }

        private void OnScrollChanged(object sender, EventArgs e)
        {
            if (this.ignoreScroll)
            {
                return;
            }

            if (this.document != null)
            {
                this.document.ApplyBarScroll(this.vScroll.Value, this.hScroll.Value, false);
            }

            this.Invalidate();
            if (this.IsComposing)
            {
                this.UpdateImeWindow();
            }
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

        private void NotifyChanged()
        {
            this.UpdateScrollBars();
            this.Invalidate();
            EventHandler h = this.DocumentChanged;
            if (h != null)
            {
                h(this, EventArgs.Empty);
            }

            this.RaiseCaretMoved();
        }

        private void RaiseCaretMoved()
        {
            EventHandler h = this.CaretMoved;
            if (h != null)
            {
                h(this, EventArgs.Empty);
            }
        }

        private void UpdateImeWindow()
        {
            if (!this.IsHandleCreated || this.document == null)
            {
                return;
            }

            int physicalPx = DpiUtil.ToPixels(this.fontSize, DpiUtil.GetDpi(this.Handle));
            NativeIme.ApplyCompositionFont(this.Handle, physicalPx);

            int leftPad;
            int rightPad;
            int textInset;
            this.GetGutterPads(out leftPad, out rightPad, out textInset);
            int caretLine = this.document.CaretLine;
            int first = this.vScroll.Value;
            float columnX = this.ColumnToX(caretLine, this.document.CaretColumn);
            float prefixWidth = 0f;
            float totalWidth = 0f;
            if (!string.IsNullOrEmpty(this.imeComposition) && this.halfFont != null)
            {
                using (Graphics g = this.CreateGraphics())
                {
                    int cursor = ImeLayout.ClampCursor(this.imeCursor, this.imeComposition.Length);
                    if (cursor > 0)
                    {
                        prefixWidth = this.MeasureRun(g, this.imeComposition.Substring(0, cursor));
                    }

                    totalWidth = this.MeasureRun(g, this.imeComposition);
                }
            }

            int x = ImeLayout.ClientX(this.gutterWidth, textInset, columnX, this.hScroll.Value, prefixWidth);
            int y = ImeLayout.ClientY(caretLine, first, this.lineHeight);
            NativeIme.MoveCompositionWindow(this.Handle, x, y);
            int excludeX = ImeLayout.ClientX(this.gutterWidth, textInset, columnX, this.hScroll.Value, 0f);
            int excludeW = (int)totalWidth;
            if (excludeW < 1)
            {
                excludeW = 1;
            }

            NativeIme.SetCandidateExclude(this.Handle, new Rectangle(excludeX, y, excludeW, this.lineHeight));
        }

        private void FillImeCharPosition(IntPtr lParam)
        {
            int length = (this.imeComposition == null) ? 0 : this.imeComposition.Length;
            int charPos = ImeLayout.ClampCursor(NativeIme.ReadCharPos(lParam), length);
            int leftPad;
            int rightPad;
            int textInset;
            this.GetGutterPads(out leftPad, out rightPad, out textInset);
            float columnX = 0f;
            int caretLine = 0;
            int first = 0;
            if (this.document != null)
            {
                caretLine = this.document.CaretLine;
                first = this.vScroll.Value;
                columnX = this.ColumnToX(caretLine, this.document.CaretColumn);
            }

            float prefixWidth = 0f;
            if (charPos > 0 && !string.IsNullOrEmpty(this.imeComposition) && this.halfFont != null)
            {
                using (Graphics g = this.CreateGraphics())
                {
                    prefixWidth = this.MeasureRun(g, this.imeComposition.Substring(0, charPos));
                }
            }

            int cx = ImeLayout.ClientX(this.gutterWidth, textInset, columnX, this.hScroll.Value, prefixWidth);
            int cy = ImeLayout.ClientY(caretLine, first, this.lineHeight);
            Point screen = this.PointToScreen(new Point(cx, cy));
            Rectangle textArea = this.GetTextArea();
            Point docTl = this.PointToScreen(new Point(this.gutterWidth, 0));
            Point docBr = this.PointToScreen(new Point(textArea.Right, textArea.Bottom));
            NativeIme.WriteCharPosition(lParam, charPos, screen, this.lineHeight, docTl.X, docTl.Y, docBr.X, docBr.Y);
        }

        private static class NativeIme
        {
            private const int GCS_CURSORPOS = 0x0080;
            private const int CFS_POINT = 2;
            private const int CFS_EXCLUDE = 0x0080;

            private static string compositionFace;
            private static bool compositionFaceResolved;

            [DllImport("imm32.dll")]
            private static extern IntPtr ImmGetContext(IntPtr hWnd);

            [DllImport("imm32.dll")]
            private static extern bool ImmReleaseContext(IntPtr hWnd, IntPtr hIMC);

            [DllImport("imm32.dll", CharSet = CharSet.Unicode)]
            private static extern int ImmGetCompositionStringW(IntPtr hIMC, int dwIndex, byte[] lpBuf, int dwBufLen);

            [DllImport("imm32.dll")]
            private static extern bool ImmSetCompositionWindow(IntPtr hIMC, ref COMPOSITIONFORM lpCompForm);

            [DllImport("imm32.dll")]
            private static extern bool ImmSetCandidateWindow(IntPtr hIMC, ref CANDIDATEFORM lpCandidate);

            [DllImport("imm32.dll", CharSet = CharSet.Unicode)]
            private static extern bool ImmSetCompositionFontW(IntPtr hIMC, ref LOGFONT lplf);

            [StructLayout(LayoutKind.Sequential)]
            private struct POINT
            {
                public int x;
                public int y;
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct RECT
            {
                public int left;
                public int top;
                public int right;
                public int bottom;
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct COMPOSITIONFORM
            {
                public int dwStyle;
                public POINT ptCurrentPos;
                public RECT rcArea;
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct CANDIDATEFORM
            {
                public int dwIndex;
                public int dwStyle;
                public POINT ptCurrentPos;
                public RECT rcArea;
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct IMECHARPOSITION
            {
                public int dwSize;
                public int dwCharPos;
                public POINT pt;
                public int cLineHeight;
                public RECT rcDocument;
            }

            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
            private struct LOGFONT
            {
                public int lfHeight;
                public int lfWidth;
                public int lfEscapement;
                public int lfOrientation;
                public int lfWeight;
                public byte lfItalic;
                public byte lfUnderline;
                public byte lfStrikeOut;
                public byte lfCharSet;
                public byte lfOutPrecision;
                public byte lfClipPrecision;
                public byte lfQuality;
                public byte lfPitchAndFamily;
                [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
                public string lfFaceName;
            }

            /// <summary>
            /// 変換フォントを本文の物理サイズと IME 可視のシステム顔にする。失敗は無視する。
            /// </summary>
            /// <param name="hwnd">編集器 HWND。</param>
            /// <param name="physicalPx">本文サイズの物理ピクセル。</param>
            public static void ApplyCompositionFont(IntPtr hwnd, int physicalPx)
            {
                IntPtr himc = ImmGetContext(hwnd);
                if (himc == IntPtr.Zero)
                {
                    return;
                }

                try
                {
                    LOGFONT lf = new LOGFONT();
                    lf.lfHeight = ImeLayout.CompositionFontHeight(physicalPx);
                    lf.lfWeight = 400;
                    lf.lfCharSet = 128;
                    lf.lfFaceName = ResolveCompositionFace();
                    ImmSetCompositionFontW(himc, ref lf);
                }
                catch
                {
                }
                finally
                {
                    ImmReleaseContext(hwnd, himc);
                }
            }

            public static string GetCompositionString(IntPtr hwnd, int flag)
            {
                IntPtr himc = ImmGetContext(hwnd);
                if (himc == IntPtr.Zero)
                {
                    return "";
                }

                try
                {
                    int len = ImmGetCompositionStringW(himc, flag, null, 0);
                    if (len <= 0)
                    {
                        return "";
                    }

                    byte[] buf = new byte[len];
                    ImmGetCompositionStringW(himc, flag, buf, len);
                    return Encoding.Unicode.GetString(buf).TrimEnd('\0');
                }
                finally
                {
                    ImmReleaseContext(hwnd, himc);
                }
            }

            public static int GetCursorPos(IntPtr hwnd)
            {
                IntPtr himc = ImmGetContext(hwnd);
                if (himc == IntPtr.Zero)
                {
                    return 0;
                }

                try
                {
                    int index = ImmGetCompositionStringW(himc, GCS_CURSORPOS, null, 0);
                    if (index < 0)
                    {
                        return 0;
                    }

                    return index;
                }
                finally
                {
                    ImmReleaseContext(hwnd, himc);
                }
            }

            public static void MoveCompositionWindow(IntPtr hwnd, int x, int y)
            {
                IntPtr himc = ImmGetContext(hwnd);
                if (himc == IntPtr.Zero)
                {
                    return;
                }

                try
                {
                    COMPOSITIONFORM form = new COMPOSITIONFORM();
                    form.dwStyle = CFS_POINT;
                    form.ptCurrentPos.x = x;
                    form.ptCurrentPos.y = y;
                    ImmSetCompositionWindow(himc, ref form);
                }
                finally
                {
                    ImmReleaseContext(hwnd, himc);
                }
            }

            public static void SetCandidateExclude(IntPtr hwnd, Rectangle clientExclude)
            {
                IntPtr himc = ImmGetContext(hwnd);
                if (himc == IntPtr.Zero)
                {
                    return;
                }

                try
                {
                    CANDIDATEFORM form = new CANDIDATEFORM();
                    form.dwIndex = 0;
                    form.dwStyle = CFS_EXCLUDE;
                    form.ptCurrentPos.x = clientExclude.Left;
                    form.ptCurrentPos.y = clientExclude.Top;
                    form.rcArea.left = clientExclude.Left;
                    form.rcArea.top = clientExclude.Top;
                    form.rcArea.right = clientExclude.Right;
                    form.rcArea.bottom = clientExclude.Bottom;
                    ImmSetCandidateWindow(himc, ref form);
                }
                finally
                {
                    ImmReleaseContext(hwnd, himc);
                }
            }

            public static int ReadCharPos(IntPtr lParam)
            {
                IMECHARPOSITION pos = (IMECHARPOSITION)Marshal.PtrToStructure(lParam, typeof(IMECHARPOSITION));
                return pos.dwCharPos;
            }

            public static void WriteCharPosition(IntPtr lParam, int charPos, Point screenPt, int lineHeight, int docLeft, int docTop, int docRight, int docBottom)
            {
                IMECHARPOSITION pos = (IMECHARPOSITION)Marshal.PtrToStructure(lParam, typeof(IMECHARPOSITION));
                pos.dwCharPos = charPos;
                pos.pt.x = screenPt.X;
                pos.pt.y = screenPt.Y;
                pos.cLineHeight = lineHeight;
                pos.rcDocument.left = docLeft;
                pos.rcDocument.top = docTop;
                pos.rcDocument.right = docRight;
                pos.rcDocument.bottom = docBottom;
                Marshal.StructureToPtr(pos, lParam, false);
            }

            private static string ResolveCompositionFace()
            {
                if (compositionFaceResolved)
                {
                    return compositionFace;
                }

                compositionFaceResolved = true;
                compositionFace = "MS Gothic";
                try
                {
                    using (InstalledFontCollection installed = new InstalledFontCollection())
                    {
                        if (HasInstalledFamily(installed, "Yu Gothic"))
                        {
                            compositionFace = "Yu Gothic";
                        }
                        else if (HasInstalledFamily(installed, "Yu Gothic UI"))
                        {
                            compositionFace = "Yu Gothic UI";
                        }
                        else if (HasInstalledFamily(installed, "MS Gothic"))
                        {
                            compositionFace = "MS Gothic";
                        }
                    }
                }
                catch
                {
                    compositionFace = "MS Gothic";
                }

                return compositionFace;
            }

            private static bool HasInstalledFamily(InstalledFontCollection installed, string name)
            {
                FontFamily[] families = installed.Families;
                if (families == null)
                {
                    return false;
                }

                int i;
                for (i = 0; i < families.Length; i++)
                {
                    if (string.Equals(families[i].Name, name, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }

                return false;
            }
        }
    }
}
