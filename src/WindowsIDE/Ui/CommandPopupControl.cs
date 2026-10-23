using System;
using System.Drawing;
using System.Windows.Forms;
using WindowsIDE.Commands;
using WindowsIDE.Ui.Fonts;
using WindowsIDE.Workspace;

namespace WindowsIDE.Ui
{
    /// <summary>
    /// コマンドパレットとクイックオープン。MainForm の子。Form にはしない。
    /// </summary>
    public sealed class CommandPopupControl : Control
    {
        /// <summary>幅（96dpi DIP）。</summary>
        public const int WidthDip = 520;

        /// <summary>メニュー下端からの間隔（96dpi DIP）。</summary>
        public const int TopGapDip = 48;

        /// <summary>クライアント端からの余白（96dpi DIP）。</summary>
        public const int EdgeMarginDip = 8;

        /// <summary>リストの最大行数。</summary>
        public const int MaxVisibleRows = 10;

        /// <summary>絞り込みの上限。</summary>
        public const int MaxResults = 100;

        private const int RowPadDip = 4;

        private readonly DualFontField field;
        private readonly ThemedScrollBar vScroll;
        private Font halfFont;
        private Font fullFont;
        private BoundCommand[] commands;
        private QuickOpenFile[] files;
        private int[] currentMatches;
        private PickerMode mode;
        private bool hasWorkspace;
        private string workspaceRoot;
        private string hintText;
        private int selected;
        private int firstRow;
        private int lastDpi;
        private int wheelLeftover;
        private bool suppressQuery;
        private bool ignoreScroll;
        private bool opening;
        private bool suppressClose;
        private bool accepting;
        private bool closeCheckQueued;
        private bool acceptedIsFile;
        private string acceptedPath;
        private EventHandler acceptedHandler;

        /// <summary>
        /// 非表示のポップアップを組む。フォントは後から渡す。
        /// </summary>
        public CommandPopupControl()
        {
            this.commands = new BoundCommand[0];
            this.files = new QuickOpenFile[0];
            this.currentMatches = new int[0];
            this.workspaceRoot = "";
            this.acceptedPath = "";
            this.selected = -1;
            this.Dock = DockStyle.None;
            this.TabStop = false;
            this.Visible = false;
            this.BackColor = Theme.Background;
            this.ForeColor = Theme.Foreground;
            this.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            this.field = new DualFontField();
            this.field.ImeMode = ImeMode.On;
            this.field.BorderColor = Theme.Border;
            this.field.BackColor = Theme.EditorBackground;
            this.field.ForeColor = Theme.Foreground;
            this.field.TextChanged += this.OnQueryTextChanged;
            this.field.KeyDown += this.OnFieldKeyDown;
            this.field.LostFocus += this.OnFieldLostFocus;
            this.field.MouseWheel += this.OnFieldMouseWheel;
            this.vScroll = new ThemedScrollBar(true);
            this.vScroll.TabStop = false;
            this.vScroll.Visible = false;
            this.vScroll.ValueChanged += this.OnScrollValueChanged;
            this.Controls.Add(this.field);
            this.Controls.Add(this.vScroll);
        }

        /// <summary>入力欄が IME 変換中のとき true。START だけでは true にしない。</summary>
        public bool IsQueryComposing
        {
            get { return this.field.IsComposing; }
        }

        /// <summary>確定がファイルのとき true。</summary>
        public bool AcceptedIsFile
        {
            get { return this.acceptedIsFile; }
        }

        /// <summary>確定したファイルのフルパス。命令のときは空。</summary>
        public string AcceptedPath
        {
            get { return this.acceptedPath; }
        }

        /// <summary>確定した命令のハンドラ。ファイルのときは null。</summary>
        public EventHandler AcceptedHandler
        {
            get { return this.acceptedHandler; }
        }

        /// <summary>行を確定したとき。閉じたあとで上がる。</summary>
        public event EventHandler Accepted;

        /// <summary>Esc で閉じたとき。復帰先へ戻す側が受け取る。</summary>
        public event EventHandler Dismissed;

        /// <summary>フォーカスが外へ出て閉じたとき。復帰先へは戻さない。</summary>
        public event EventHandler Abandoned;

        /// <summary>行数に応じて高さが変わったとき。</summary>
        public event EventHandler PreferredHeightChanged;

        /// <summary>
        /// 半角・全角フォントを参照する。所有権は移さない。12 DIP。
        /// </summary>
        /// <param name="half">半角。</param>
        /// <param name="full">全角。</param>
        public void SetFonts(Font half, Font full)
        {
            this.halfFont = half;
            this.fullFont = full;
            this.field.SetFonts(half, full, DpiUtil.UiFontDip);
            this.Invalidate();
        }

        /// <summary>開く直前からフォーカスが落ち着くまでのあいだ、外側クリック扱いで閉じない。</summary>
        public void BeginOpenGuard()
        {
            this.opening = true;
        }

        /// <summary>開く処理の終わり。ここから外側クリックで閉じる。</summary>
        public void EndOpenGuard()
        {
            this.opening = false;
        }

        /// <summary>
        /// モードを出してクエリを空にする。クイックオープンのときだけ走査する。
        /// </summary>
        /// <param name="nextMode">出すモード。</param>
        /// <param name="catalog">登録順の命令。null は空。</param>
        /// <param name="workspacePresent">ワークスペースがあるとき true。</param>
        /// <param name="root">ワークスペースルート。無いときは null。</param>
        public void ShowMode(PickerMode nextMode, BoundCommand[] catalog, bool workspacePresent, string root)
        {
            this.commands = (catalog == null) ? new BoundCommand[0] : catalog;
            this.hasWorkspace = workspacePresent;
            this.workspaceRoot = (root == null) ? "" : root;
            this.mode = nextMode;
            if (nextMode == PickerMode.QuickOpen)
            {
                this.LoadFiles();
            }

            this.ClearQuery();
            this.Refilter();
        }

        /// <summary>入力欄へフォーカスし、テキストを全選択する。</summary>
        public void FocusQuery()
        {
            this.field.Focus();
            this.field.SelectAll();
        }

        /// <summary>
        /// いまの行数での外周を含む高さ。
        /// </summary>
        /// <param name="dpi">対象 DPI。</param>
        /// <returns>物理ピクセル。</returns>
        public int MeasureOuterHeight(int dpi)
        {
            int rowH = this.RowHeight(dpi);
            int rows = this.VisibleRowSlots();
            return 1 + this.field.PreferredOuterHeight + (rows * rowH) + 1;
        }

        /// <summary>
        /// 子の入力欄とスクロールバーを現在の幅に合わせる。
        /// </summary>
        /// <param name="dpi">対象 DPI。</param>
        public void LayoutAtDpi(int dpi)
        {
            if (dpi < 1)
            {
                dpi = 96;
            }

            this.lastDpi = dpi;
            int border = 1;
            int fieldH = this.field.PreferredOuterHeight;
            int innerW = this.Width - (border * 2);
            if (innerW < 1)
            {
                innerW = 1;
            }

            this.field.SetBounds(border, border, innerW, fieldH);
            int listTop = border + fieldH;
            int listH = this.Height - listTop - border;
            if (listH < 1)
            {
                listH = 1;
            }

            int bar = DpiUtil.ToPixels(DpiUtil.ScrollBarThicknessDip, dpi);
            if (bar < 1)
            {
                bar = DpiUtil.ScrollBarThicknessDip;
            }

            if (this.NeedsScroll())
            {
                this.vScroll.SetBounds(this.Width - border - bar, listTop, bar, listH);
                this.vScroll.Visible = true;
            }
            else
            {
                this.vScroll.Visible = false;
            }

            this.SyncScroll();
            this.Invalidate();
        }

        /// <summary>変換中は親へ渡さない。欄が取らなかったキーは捨てる。</summary>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (!this.Visible)
            {
                return base.ProcessCmdKey(ref msg, keyData);
            }

            if (this.field.IsComposing)
            {
                return false;
            }

            if (keyData == (Keys.Control | Keys.Shift | Keys.P))
            {
                this.SwitchMode(PickerMode.Palette);
                return true;
            }

            if (keyData == (Keys.Control | Keys.P))
            {
                this.SwitchMode(PickerMode.QuickOpen);
                return true;
            }

            if (keyData == Keys.Escape)
            {
                this.Dismiss();
                return true;
            }

            if (keyData == Keys.Enter)
            {
                this.TryAccept();
                return true;
            }

            if (keyData == Keys.Tab || keyData == (Keys.Shift | Keys.Tab))
            {
                return true;
            }

            Keys code = keyData & Keys.KeyCode;
            if (code == Keys.Left || code == Keys.Right || code == Keys.Home || code == Keys.End
                || code == Keys.Up || code == Keys.Down || code == Keys.Back || code == Keys.Delete)
            {
                return false;
            }

            Keys mods = keyData & Keys.Modifiers;
            if ((mods & (Keys.Control | Keys.Alt)) == 0)
            {
                return false;
            }

            return true;
        }

        /// <summary>背景、枠、リスト行。</summary>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (e == null || e.Graphics == null)
            {
                return;
            }

            Graphics g = e.Graphics;
            g.Clear(Theme.Background);
            if (this.Width > 1 && this.Height > 1)
            {
                using (Pen border = new Pen(Theme.Border))
                {
                    g.DrawRectangle(border, 0, 0, this.Width - 1, this.Height - 1);
                }
            }

            this.PaintRows(g);
        }

        /// <summary>行のシングルクリックで確定する。空表示は無視する。</summary>
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e == null || e.Button != MouseButtons.Left)
            {
                this.FocusQuery();
                return;
            }

            int row = this.HitRow(e.Y);
            if (row >= 0)
            {
                this.selected = row;
                this.Invalidate();
                this.TryAccept();
                return;
            }

            this.FocusQuery();
        }

        /// <summary>リストをスクロールする。</summary>
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (e != null)
            {
                this.ScrollWheel(e.Delta);
            }
        }

        /// <summary>幅が変わったら子を置き直す。</summary>
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            this.LayoutAtDpi(this.CurrentDpi());
        }

        /// <summary>フォーカスがポップアップの外へ出たとき、遅延して閉じるか見る。</summary>
        protected override void OnLeave(EventArgs e)
        {
            base.OnLeave(e);
            this.QueueCloseIfUnfocused();
        }

        private void SwitchMode(PickerMode nextMode)
        {
            this.opening = true;
            try
            {
                this.mode = nextMode;
                if (nextMode == PickerMode.QuickOpen)
                {
                    this.LoadFiles();
                }

                this.ClearQuery();
                this.Refilter();
                this.field.Focus();
                this.field.SelectAll();
            }
            finally
            {
                this.opening = false;
            }
        }

        private void LoadFiles()
        {
            if (!this.hasWorkspace || string.IsNullOrEmpty(this.workspaceRoot))
            {
                this.files = new QuickOpenFile[0];
                return;
            }

            QuickOpenFile[] listed = QuickOpenIndex.List(this.workspaceRoot, QuickOpenIndex.DefaultMax);
            this.files = (listed == null) ? new QuickOpenFile[0] : listed;
        }

        private void ClearQuery()
        {
            this.suppressQuery = true;
            this.field.Text = "";
            this.suppressQuery = false;
        }

        private void Refilter()
        {
            string query = this.field.Text;
            if (this.mode == PickerMode.Palette)
            {
                this.currentMatches = CommandQuery.Match(this.commands, query, MaxResults);
                this.hintText = PickerHint.Palette(this.currentMatches.Length);
            }
            else if (!this.hasWorkspace)
            {
                this.currentMatches = new int[0];
                this.hintText = PickerHint.QuickOpen(false, 0, 0, query);
            }
            else
            {
                int snapshot = (this.files == null) ? 0 : this.files.Length;
                this.currentMatches = QuickOpenQuery.Match(this.files, query, MaxResults);
                if (this.currentMatches == null)
                {
                    this.currentMatches = new int[0];
                }

                this.hintText = PickerHint.QuickOpen(true, snapshot, this.currentMatches.Length, query);
            }

            if (this.currentMatches == null)
            {
                this.currentMatches = new int[0];
            }

            int selectable = (this.hintText == null) ? this.currentMatches.Length : 0;
            this.selected = PickerNavigation.OnQueryChanged(selectable);
            this.firstRow = 0;
            this.SyncScroll();
            this.Invalidate();
            this.RaisePreferredHeight();
        }

        private void TryAccept()
        {
            if (this.accepting || !this.Visible || this.field.IsComposing || this.hintText != null)
            {
                return;
            }

            if (this.currentMatches == null || this.selected < 0 || this.selected >= this.currentMatches.Length)
            {
                return;
            }

            int index = this.currentMatches[this.selected];
            if (this.mode == PickerMode.Palette)
            {
                if (this.commands == null || index < 0 || index >= this.commands.Length)
                {
                    return;
                }

                BoundCommand command = this.commands[index];
                if (command == null)
                {
                    return;
                }

                if (string.Equals(command.Id, CommandIds.ViewQuickOpen, StringComparison.Ordinal))
                {
                    this.SwitchMode(PickerMode.QuickOpen);
                    return;
                }

                if (string.Equals(command.Id, CommandIds.ViewCommandPalette, StringComparison.Ordinal))
                {
                    this.SwitchMode(PickerMode.Palette);
                    return;
                }

                this.acceptedIsFile = false;
                this.acceptedPath = "";
                this.acceptedHandler = command.Handler;
                this.HideAndRaise(this.Accepted);
                return;
            }

            if (this.files == null || index < 0 || index >= this.files.Length)
            {
                return;
            }

            QuickOpenFile file = this.files[index];
            if (file == null || string.IsNullOrEmpty(file.FullPath))
            {
                return;
            }

            this.acceptedIsFile = true;
            this.acceptedPath = file.FullPath;
            this.acceptedHandler = null;
            this.HideAndRaise(this.Accepted);
        }

        private void Dismiss()
        {
            this.HideAndRaise(this.Dismissed);
        }

        private void Abandon()
        {
            if (!this.Visible)
            {
                return;
            }

            this.HideAndRaise(this.Abandoned);
        }

        private void HideAndRaise(EventHandler handler)
        {
            if (this.accepting)
            {
                return;
            }

            this.accepting = true;
            this.suppressClose = true;
            try
            {
                this.Visible = false;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }
            }
            finally
            {
                this.suppressClose = false;
                this.accepting = false;
            }
        }

        private void MoveSelection(int delta)
        {
            int count = (this.hintText == null && this.currentMatches != null) ? this.currentMatches.Length : 0;
            this.selected = PickerNavigation.Move(this.selected, count, delta);
            this.EnsureSelectionVisible();
            this.Invalidate();
        }

        private void EnsureSelectionVisible()
        {
            if (this.hintText != null || this.selected < 0)
            {
                return;
            }

            int slots = this.VisibleRowSlots();
            if (this.selected < this.firstRow)
            {
                this.SetFirstRow(this.selected);
            }
            else if (this.selected >= this.firstRow + slots)
            {
                this.SetFirstRow(this.selected - slots + 1);
            }
        }

        private void SetFirstRow(int next)
        {
            int count = (this.currentMatches == null) ? 0 : this.currentMatches.Length;
            int slots = this.VisibleRowSlots();
            int maxFirst = count - slots;
            if (maxFirst < 0)
            {
                maxFirst = 0;
            }

            if (next < 0)
            {
                next = 0;
            }

            if (next > maxFirst)
            {
                next = maxFirst;
            }

            this.firstRow = next;
            this.ignoreScroll = true;
            try
            {
                if (this.vScroll.Visible)
                {
                    this.vScroll.Value = next;
                }
            }
            finally
            {
                this.ignoreScroll = false;
            }

            this.Invalidate();
        }

        private void SyncScroll()
        {
            int count = (this.currentMatches == null) ? 0 : this.currentMatches.Length;
            this.ignoreScroll = true;
            try
            {
                this.vScroll.Minimum = 0;
                this.vScroll.SmallChange = 1;
                this.vScroll.LargeChange = MaxVisibleRows;
                this.vScroll.Maximum = Math.Max(0, count - 1);
                if (!this.NeedsScroll())
                {
                    this.firstRow = 0;
                    this.vScroll.Value = 0;
                }
                else if (this.firstRow < 0)
                {
                    this.firstRow = 0;
                    this.vScroll.Value = 0;
                }
                else
                {
                    this.vScroll.Value = this.firstRow;
                }
            }
            finally
            {
                this.ignoreScroll = false;
            }
        }

        private void ScrollWheel(int delta)
        {
            if (!this.NeedsScroll())
            {
                return;
            }

            int notches = DpiUtil.WheelNotches(delta, ref this.wheelLeftover);
            if (notches != 0)
            {
                this.SetFirstRow(this.firstRow - notches);
            }
        }

        private void PaintRows(Graphics g)
        {
            if (this.halfFont == null || this.fullFont == null)
            {
                return;
            }

            int dpi = this.CurrentDpi();
            int rowH = this.RowHeight(dpi);
            int listTop = 1 + this.field.Height;
            int listRight = this.ClientSize.Width - 1;
            if (this.vScroll.Visible)
            {
                listRight = this.vScroll.Left;
            }

            int listWidth = listRight - 1;
            if (listWidth < 1 || rowH < 1)
            {
                return;
            }

            int slots = this.VisibleRowSlots();
            using (SolidBrush nameBrush = new SolidBrush(Theme.Foreground))
            using (SolidBrush metaBrush = new SolidBrush(Theme.Comment))
            using (SolidBrush selectedBrush = new SolidBrush(Theme.Selection))
            {
                for (int slot = 0; slot < slots; slot++)
                {
                    Rectangle row = new Rectangle(1, listTop + (slot * rowH), listWidth, rowH);
                    if (this.hintText != null)
                    {
                        if (slot == 0)
                        {
                            this.DrawText(g, this.hintText, row, metaBrush);
                        }

                        continue;
                    }

                    int index = this.firstRow + slot;
                    if (this.currentMatches == null || index < 0 || index >= this.currentMatches.Length)
                    {
                        break;
                    }

                    bool isSelected = index == this.selected;
                    if (isSelected)
                    {
                        g.FillRectangle(selectedBrush, row);
                    }

                    Brush primary = nameBrush;
                    Brush secondary = isSelected ? (Brush)nameBrush : metaBrush;
                    if (this.mode == PickerMode.Palette)
                    {
                        this.PaintCommand(g, this.currentMatches[index], row, primary, secondary);
                    }
                    else
                    {
                        this.PaintFile(g, this.currentMatches[index], row, primary, secondary);
                    }
                }
            }
        }

        private void PaintCommand(Graphics g, int index, Rectangle row, Brush nameBrush, Brush shortcutBrush)
        {
            if (this.commands == null || index < 0 || index >= this.commands.Length)
            {
                return;
            }

            BoundCommand command = this.commands[index];
            if (command == null)
            {
                return;
            }

            int dpi = this.CurrentDpi();
            int pad = DpiUtil.ToPixels(RowPadDip, dpi);
            if (pad < 1)
            {
                pad = 1;
            }

            string shortcut = (command.Shortcut == null) ? "" : command.Shortcut;
            int gap = DpiUtil.ToPixels(8, dpi);
            int nameRight = row.Right - pad;
            if (shortcut.Length > 0)
            {
                float shortcutWidth = DualFontPainter.Measure(g, shortcut, this.halfFont, this.fullFont, null);
                nameRight = row.Right - pad - (int)Math.Ceiling(shortcutWidth) - gap;
            }

            Rectangle nameClip = new Rectangle(row.X + pad, row.Y, Math.Max(0, nameRight - (row.X + pad)), row.Height);
            this.DrawText(g, command.Name, nameClip, nameBrush);
            if (shortcut.Length > 0)
            {
                float shortcutWidth = DualFontPainter.Measure(g, shortcut, this.halfFont, this.fullFont, null);
                float origin = row.Right - pad - shortcutWidth;
                Rectangle shortcutClip = new Rectangle(Math.Max(row.X, nameRight), row.Y, Math.Max(0, row.Right - pad - Math.Max(row.X, nameRight)), row.Height);
                DualFontPainter.Draw(g, shortcut, this.halfFont, this.fullFont, shortcutClip, origin, shortcutBrush, null);
            }
        }

        private void PaintFile(Graphics g, int index, Rectangle row, Brush nameBrush, Brush pathBrush)
        {
            if (this.files == null || index < 0 || index >= this.files.Length)
            {
                return;
            }

            QuickOpenFile file = this.files[index];
            if (file == null)
            {
                return;
            }

            int dpi = this.CurrentDpi();
            int pad = DpiUtil.ToPixels(RowPadDip, dpi);
            if (pad < 1)
            {
                pad = 1;
            }

            Rectangle clip = new Rectangle(row.X + pad, row.Y, Math.Max(0, row.Width - (pad * 2)), row.Height);
            string name = (file.FileName == null) ? "" : file.FileName;
            this.DrawText(g, name, clip, nameBrush);
            string relative = (file.RelativePath == null) ? "" : file.RelativePath;
            if (string.Equals(relative, name, StringComparison.Ordinal) || relative.Length == 0)
            {
                return;
            }

            float nameWidth = DualFontPainter.Measure(g, name, this.halfFont, this.fullFont, null);
            float spaceWidth = DualFontPainter.Measure(g, " ", this.halfFont, this.fullFont, null);
            DualFontPainter.Draw(g, relative, this.halfFont, this.fullFont, clip, clip.X + nameWidth + spaceWidth, pathBrush, null);
        }

        private void DrawText(Graphics g, string text, Rectangle clip, Brush brush)
        {
            if (clip.Width <= 0 || clip.Height <= 0)
            {
                return;
            }

            DualFontPainter.Draw(g, text, this.halfFont, this.fullFont, clip, clip.X, brush, null);
        }

        private int HitRow(int y)
        {
            if (this.hintText != null)
            {
                return -1;
            }

            int rowH = this.RowHeight(this.CurrentDpi());
            int listTop = 1 + this.field.Height;
            if (rowH < 1 || y < listTop)
            {
                return -1;
            }

            int slot = (y - listTop) / rowH;
            if (slot < 0 || slot >= this.VisibleRowSlots())
            {
                return -1;
            }

            int index = this.firstRow + slot;
            if (this.currentMatches == null || index < 0 || index >= this.currentMatches.Length)
            {
                return -1;
            }

            return index;
        }

        private int VisibleRowSlots()
        {
            if (this.hintText != null || this.currentMatches == null || this.currentMatches.Length == 0)
            {
                return 1;
            }

            if (this.currentMatches.Length > MaxVisibleRows)
            {
                return MaxVisibleRows;
            }

            return this.currentMatches.Length;
        }

        private bool NeedsScroll()
        {
            if (this.hintText != null || this.currentMatches == null)
            {
                return false;
            }

            return this.currentMatches.Length > MaxVisibleRows;
        }

        private int RowHeight(int dpi)
        {
            if (dpi < 1)
            {
                dpi = 96;
            }

            int cell = DpiUtil.ToPixels(DpiUtil.UiFontDip, dpi);
            if (this.halfFont != null && this.halfFont.Height > cell)
            {
                cell = this.halfFont.Height;
            }

            if (this.fullFont != null && this.fullFont.Height > cell)
            {
                cell = this.fullFont.Height;
            }

            int pad = DpiUtil.ToPixels(RowPadDip, dpi);
            if (pad < 1)
            {
                pad = 1;
            }

            return cell + pad + pad;
        }

        private int CurrentDpi()
        {
            if (this.lastDpi > 0)
            {
                return this.lastDpi;
            }

            return DpiUtil.GetDpi(this.IsHandleCreated ? this.Handle : IntPtr.Zero);
        }

        private void RaisePreferredHeight()
        {
            if (this.PreferredHeightChanged != null)
            {
                this.PreferredHeightChanged(this, EventArgs.Empty);
            }
        }

        private void QueueCloseIfUnfocused()
        {
            if (this.opening || this.suppressClose || !this.Visible || this.closeCheckQueued || !this.IsHandleCreated || this.IsDisposed)
            {
                return;
            }

            this.closeCheckQueued = true;
            this.BeginInvoke(new MethodInvoker(this.CloseIfUnfocused));
        }

        private void CloseIfUnfocused()
        {
            this.closeCheckQueued = false;
            if (this.opening || this.suppressClose || !this.Visible || this.IsDisposed)
            {
                return;
            }

            if (this.ContainsFocus)
            {
                return;
            }

            this.Abandon();
        }

        private void OnQueryTextChanged(object sender, EventArgs e)
        {
            if (this.suppressQuery)
            {
                return;
            }

            this.Refilter();
        }

        private void OnFieldKeyDown(object sender, KeyEventArgs e)
        {
            if (e == null || this.field.IsComposing)
            {
                return;
            }

            if (e.KeyCode == Keys.Up)
            {
                this.MoveSelection(-1);
                e.Handled = true;
                return;
            }

            if (e.KeyCode == Keys.Down)
            {
                this.MoveSelection(1);
                e.Handled = true;
                return;
            }

            if (e.KeyCode == Keys.Enter)
            {
                this.TryAccept();
                e.Handled = true;
            }
        }

        private void OnFieldLostFocus(object sender, EventArgs e)
        {
            this.QueueCloseIfUnfocused();
        }

        private void OnFieldMouseWheel(object sender, MouseEventArgs e)
        {
            if (e == null)
            {
                return;
            }

            this.ScrollWheel(e.Delta);
            HandledMouseEventArgs handled = e as HandledMouseEventArgs;
            if (handled != null)
            {
                handled.Handled = true;
            }
        }

        private void OnScrollValueChanged(object sender, EventArgs e)
        {
            if (this.ignoreScroll)
            {
                return;
            }

            this.firstRow = this.vScroll.Value;
            this.Invalidate();
        }
    }
}
