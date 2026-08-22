using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using WindowsIDE.Build;
using WindowsIDE.Ui.Fonts;

namespace WindowsIDE.Ui
{
    /// <summary>
    /// 問題一覧の行が選ばれたとき。
    /// </summary>
    public sealed class ProblemActivatedEventArgs : EventArgs
    {
        private Diagnostic diagnostic;

        /// <summary>
        /// 対象診断を渡す。
        /// </summary>
        /// <param name="diagnostic">クリックされた診断。</param>
        public ProblemActivatedEventArgs(Diagnostic diagnostic)
        {
            this.diagnostic = diagnostic;
        }

        /// <summary>対象。</summary>
        public Diagnostic Diagnostic
        {
            get { return this.diagnostic; }
        }
    }

    /// <summary>
    /// 下パネルの問題一覧。オーナー描画 + ThemedScrollBar。ListView は使わない。
    /// </summary>
    public sealed class ProblemListControl : Control
    {
        private const int WM_MOUSEHWHEEL = 0x020E;

        private readonly List<Diagnostic> items;
        private readonly List<string> displayTexts;
        private readonly ThemedScrollBar vScroll;
        private readonly ThemedScrollBar hScroll;
        private Font halfFont;
        private Font fullFont;
        private StringFormat typographic;
        private int selectedIndex;
        private int errorCount;
        private int warningCount;
        private Rectangle closeBounds;
        private int contentWidth;
        private int wheelLeftover;
        private bool ignoreScroll;

        /// <summary>
        /// 空の一覧を組む。
        /// </summary>
        public ProblemListControl()
        {
            this.items = new List<Diagnostic>();
            this.displayTexts = new List<string>();
            this.selectedIndex = -1;
            this.typographic = (StringFormat)StringFormat.GenericTypographic.Clone();
            this.typographic.FormatFlags = this.typographic.FormatFlags | StringFormatFlags.MeasureTrailingSpaces | StringFormatFlags.NoWrap | StringFormatFlags.FitBlackBox;
            this.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            this.TabStop = true;
            this.BackColor = Theme.Background;
            this.ForeColor = Theme.Foreground;

            this.vScroll = new ThemedScrollBar(true);
            this.hScroll = new ThemedScrollBar(false);
            this.vScroll.TrackColor = Theme.Background;
            this.hScroll.TrackColor = Theme.Background;
            this.vScroll.SmallChange = 1;
            this.hScroll.SmallChange = 8;
            this.vScroll.ValueChanged += this.OnScrollChanged;
            this.hScroll.ValueChanged += this.OnScrollChanged;
            this.Controls.Add(this.vScroll);
            this.Controls.Add(this.hScroll);
        }

        /// <summary>× で畳む要求。</summary>
        public event EventHandler CloseRequested;

        /// <summary>行クリック。ファイルが無い行でも発火する。</summary>
        public event EventHandler<ProblemActivatedEventArgs> ItemActivated;

        /// <summary>
        /// ツリーと同じ 12 DIP 双フォントを使う。所有権は移さない。
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
        /// 一覧を置き換える。フォーカスは奪わない。
        /// </summary>
        /// <param name="diagnostics">csc または合成。null は空。</param>
        public void SetItems(IList<Diagnostic> diagnostics)
        {
            this.items.Clear();
            this.displayTexts.Clear();
            this.errorCount = 0;
            this.warningCount = 0;
            this.selectedIndex = -1;
            if (diagnostics != null)
            {
                for (int i = 0; i < diagnostics.Count; i++)
                {
                    Diagnostic d = diagnostics[i];
                    if (d == null)
                    {
                        continue;
                    }

                    this.items.Add(d);
                    this.displayTexts.Add(FormatItem(d));
                    if (d.IsError)
                    {
                        this.errorCount++;
                    }
                    else
                    {
                        this.warningCount++;
                    }
                }
            }

            this.ignoreScroll = true;
            try
            {
                this.vScroll.Value = 0;
                this.hScroll.Value = 0;
            }
            finally
            {
                this.ignoreScroll = false;
            }

            this.RefreshChrome();
            this.Invalidate();
        }

        /// <summary>親の DPI 変更後にバー位置を合わせる。</summary>
        protected override void OnDpiChangedAfterParent(EventArgs e)
        {
            base.OnDpiChangedAfterParent(e);
            this.RefreshChrome();
            this.Invalidate();
        }

        /// <summary>サイズ変更でバーを置き直す。</summary>
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            this.RefreshChrome();
        }

        /// <summary>ハンドル作成後にバーを置く。</summary>
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            this.RefreshChrome();
        }

        /// <summary>ヘッダ・行・枠を描く。</summary>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            int dpi = DpiUtil.GetDpi(this.IsHandleCreated ? this.Handle : IntPtr.Zero);
            int headerH = this.HeaderHeight(dpi);
            using (SolidBrush bg = new SolidBrush(Theme.Background))
            {
                g.FillRectangle(bg, this.ClientRectangle);
            }

            Rectangle header = new Rectangle(0, 0, this.ClientSize.Width, headerH);
            using (SolidBrush headerBg = new SolidBrush(Theme.Background))
            {
                g.FillRectangle(headerBg, header);
            }

            this.DrawHeader(g, header, dpi);
            this.DrawRows(g, dpi, headerH);
            using (Pen border = new Pen(Theme.Border))
            {
                g.DrawRectangle(border, 0, 0, this.ClientSize.Width - 1, this.ClientSize.Height - 1);
                if (headerH > 0 && headerH < this.ClientSize.Height)
                {
                    g.DrawLine(border, 0, headerH - 1, this.ClientSize.Width, headerH - 1);
                }
            }
        }

        /// <summary>× または行。</summary>
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            if (this.closeBounds.Contains(e.Location))
            {
                EventHandler close = this.CloseRequested;
                if (close != null)
                {
                    close(this, EventArgs.Empty);
                }

                return;
            }

            int dpi = DpiUtil.GetDpi(this.IsHandleCreated ? this.Handle : IntPtr.Zero);
            int headerH = this.HeaderHeight(dpi);
            if (e.Y < headerH)
            {
                return;
            }

            int rowH = this.RowHeight(dpi);
            if (rowH < 1)
            {
                return;
            }

            int index = this.vScroll.Value + ((e.Y - headerH) / rowH);
            if (index < 0 || index >= this.items.Count)
            {
                return;
            }

            this.selectedIndex = index;
            this.Invalidate();
            EventHandler<ProblemActivatedEventArgs> h = this.ItemActivated;
            if (h != null)
            {
                h(this, new ProblemActivatedEventArgs(this.items[index]));
            }
        }

        /// <summary>縦ホイール。Shift は横。</summary>
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if ((Control.ModifierKeys & Keys.Shift) != 0)
            {
                if (this.hScroll.Visible)
                {
                    int notches = DpiUtil.WheelNotches(e.Delta, ref this.wheelLeftover);
                    if (notches != 0)
                    {
                        this.hScroll.Value = this.hScroll.Value - (notches * this.hScroll.SmallChange);
                    }
                }

                HandledMouseEventArgs hs = e as HandledMouseEventArgs;
                if (hs != null)
                {
                    hs.Handled = true;
                }

                return;
            }

            if (this.vScroll.Visible)
            {
                int notches = DpiUtil.WheelNotches(e.Delta, ref this.wheelLeftover);
                if (notches != 0)
                {
                    this.vScroll.Value = this.vScroll.Value - (notches * this.vScroll.SmallChange);
                }
            }

            HandledMouseEventArgs handled = e as HandledMouseEventArgs;
            if (handled != null)
            {
                handled.Handled = true;
            }
        }

        /// <summary>横ホイール。</summary>
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_MOUSEHWHEEL)
            {
                int delta = (short)((m.WParam.ToInt64() >> 16) & 0xFFFF);
                if (this.hScroll.Visible)
                {
                    int leftover = 0;
                    int notches = DpiUtil.WheelNotches(delta, ref leftover);
                    if (notches != 0)
                    {
                        this.hScroll.Value = this.hScroll.Value + (notches * this.hScroll.SmallChange);
                    }
                }

                m.Result = (IntPtr)1;
                return;
            }

            base.WndProc(ref m);
        }

        /// <summary>バーとフォント参照は破棄しない。</summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this.typographic != null)
                {
                    this.typographic.Dispose();
                    this.typographic = null;
                }
            }

            base.Dispose(disposing);
        }

        private void OnScrollChanged(object sender, EventArgs e)
        {
            if (this.ignoreScroll)
            {
                return;
            }

            this.Invalidate();
        }

        private void DrawHeader(Graphics g, Rectangle header, int dpi)
        {
            int pad = DpiUtil.ToPixels(8, dpi);
            int closeSize = DpiUtil.ToPixels(12, dpi);
            int closeInset = DpiUtil.ToPixels(4, dpi);
            int closeX = header.Right - closeInset - closeSize;
            int closeY = header.Top + ((header.Height - closeSize) / 2);
            if (closeX < header.Left)
            {
                closeX = header.Left;
            }

            this.closeBounds = new Rectangle(closeX, closeY, closeSize, closeSize);
            using (Pen xp = new Pen(Theme.Foreground, 1f))
            {
                g.DrawLine(xp, this.closeBounds.Left, this.closeBounds.Top, this.closeBounds.Right - 1, this.closeBounds.Bottom - 1);
                g.DrawLine(xp, this.closeBounds.Right - 1, this.closeBounds.Top, this.closeBounds.Left, this.closeBounds.Bottom - 1);
            }

            if (this.halfFont == null || this.fullFont == null)
            {
                return;
            }

            string title = "問題";
            string counts = string.Format("エラー {0}, 警告 {1}", this.errorCount, this.warningCount);
            int titleW = (int)Math.Ceiling(DualFontPainter.Measure(g, title, this.halfFont, this.fullFont, this.typographic));
            Rectangle titleRect = new Rectangle(header.X + pad, header.Y, titleW + pad, header.Height);
            using (SolidBrush fg = new SolidBrush(Theme.Foreground))
            {
                DualFontPainter.Draw(g, title, this.halfFont, this.fullFont, titleRect, titleRect.X, fg, this.typographic);
            }

            int countX = titleRect.Right;
            int countRight = this.closeBounds.Left - pad;
            if (countRight > countX)
            {
                Rectangle countRect = new Rectangle(countX, header.Y, countRight - countX, header.Height);
                using (SolidBrush comment = new SolidBrush(Theme.Comment))
                {
                    DualFontPainter.DrawEllipsis(g, counts, this.halfFont, this.fullFont, countRect, comment, this.typographic);
                }
            }
        }

        private void DrawRows(Graphics g, int dpi, int headerH)
        {
            if (this.halfFont == null || this.fullFont == null)
            {
                return;
            }

            int rowH = this.RowHeight(dpi);
            if (rowH < 1)
            {
                return;
            }

            int pad = DpiUtil.ToPixels(8, dpi);
            int kindW = this.MeasureKindColumn(g, dpi);
            int bar = DpiUtil.ToPixels(DpiUtil.ScrollBarThicknessDip, dpi);
            int vW = this.vScroll.Visible ? bar : 0;
            int hH = this.hScroll.Visible ? bar : 0;
            Rectangle list = new Rectangle(0, headerH, Math.Max(0, this.ClientSize.Width - vW), Math.Max(0, this.ClientSize.Height - headerH - hH));
            if (list.Width <= 0 || list.Height <= 0)
            {
                return;
            }

            int originX = list.X + pad - this.hScroll.Value;
            int first = this.vScroll.Value;
            int visible = list.Height / rowH;
            if (visible < 1)
            {
                visible = 1;
            }

            int last = first + visible;
            if (last > this.items.Count)
            {
                last = this.items.Count;
            }

            for (int i = first; i < last; i++)
            {
                int y = list.Y + ((i - first) * rowH);
                Rectangle row = new Rectangle(list.X, y, list.Width, rowH);
                if (i == this.selectedIndex)
                {
                    using (SolidBrush cur = new SolidBrush(Theme.CurrentLine))
                    {
                        g.FillRectangle(cur, row);
                    }
                }

                Diagnostic d = this.items[i];
                string kind = d.IsError ? "error" : "warning";
                Rectangle kindClip = new Rectangle(originX, y, kindW, rowH);
                using (SolidBrush kindBrush = new SolidBrush(d.IsError ? Theme.Error : Theme.Comment))
                {
                    DualFontPainter.Draw(g, kind, this.halfFont, this.fullFont, Rectangle.Intersect(kindClip, list), originX, kindBrush, this.typographic);
                }

                string rest = this.displayTexts[i];
                int restX = originX + kindW + DpiUtil.ToPixels(8, dpi);
                Rectangle restClip = new Rectangle(restX, y, Math.Max(0, list.Right - restX), rowH);
                using (SolidBrush fg = new SolidBrush(Theme.Foreground))
                {
                    DualFontPainter.Draw(g, rest, this.halfFont, this.fullFont, Rectangle.Intersect(restClip, list), restX, fg, this.typographic);
                }
            }
        }

        private int MeasureKindColumn(Graphics g, int dpi)
        {
            float w = DualFontPainter.Measure(g, "warning", this.halfFont, this.fullFont, this.typographic);
            int min = DpiUtil.ToPixels(48, dpi);
            int n = (int)Math.Ceiling(w);
            if (n < min)
            {
                return min;
            }

            return n;
        }

        private void RefreshChrome()
        {
            if (!this.IsHandleCreated)
            {
                return;
            }

            int dpi = DpiUtil.GetDpi(this.Handle);
            int headerH = this.HeaderHeight(dpi);
            int rowH = this.RowHeight(dpi);
            if (rowH < 1)
            {
                rowH = 1;
            }

            int bar = DpiUtil.ToPixels(DpiUtil.ScrollBarThicknessDip, dpi);
            int clientW = this.ClientSize.Width;
            int clientH = this.ClientSize.Height;
            int listH = Math.Max(0, clientH - headerH);
            int visibleRows = listH / rowH;
            if (visibleRows < 1)
            {
                visibleRows = 1;
            }

            int itemCount = this.items.Count;
            bool needV = DpiUtil.ScrollBarNeeded(0, Math.Max(0, itemCount - 1), visibleRows) && itemCount > visibleRows;
            int listW = clientW - (needV ? bar : 0);
            this.contentWidth = this.MeasureContentWidth(dpi);
            bool needH = this.contentWidth > listW;
            if (needH)
            {
                listH = Math.Max(0, clientH - headerH - bar);
                visibleRows = listH / rowH;
                if (visibleRows < 1)
                {
                    visibleRows = 1;
                }

                needV = itemCount > visibleRows;
                listW = clientW - (needV ? bar : 0);
                needH = this.contentWidth > listW;
            }

            this.ignoreScroll = true;
            try
            {
                this.vScroll.Minimum = 0;
                this.vScroll.Maximum = Math.Max(0, itemCount - 1);
                this.vScroll.LargeChange = visibleRows;
                this.hScroll.Minimum = 0;
                this.hScroll.Maximum = Math.Max(0, this.contentWidth - 1);
                this.hScroll.LargeChange = Math.Max(1, listW);
                this.vScroll.Visible = needV;
                this.hScroll.Visible = needH;
                if (!needV)
                {
                    this.vScroll.Value = 0;
                }

                if (!needH)
                {
                    this.hScroll.Value = 0;
                }
            }
            finally
            {
                this.ignoreScroll = false;
            }

            int vW = needV ? bar : 0;
            int hHBar = needH ? bar : 0;
            this.vScroll.Bounds = new Rectangle(this.ClientSize.Width - vW, headerH, vW, Math.Max(0, this.ClientSize.Height - headerH - hHBar));
            this.hScroll.Bounds = new Rectangle(0, this.ClientSize.Height - hHBar, Math.Max(0, this.ClientSize.Width - vW), hHBar);
            this.vScroll.BringToFront();
            this.hScroll.BringToFront();
        }

        private int MeasureContentWidth(int dpi)
        {
            if (this.halfFont == null || this.fullFont == null || !this.IsHandleCreated)
            {
                return 0;
            }

            int pad = DpiUtil.ToPixels(8, dpi);
            using (Graphics g = this.CreateGraphics())
            {
                int kindW = this.MeasureKindColumn(g, dpi);
                int gap = DpiUtil.ToPixels(8, dpi);
                int max = 0;
                for (int i = 0; i < this.displayTexts.Count; i++)
                {
                    int w = pad + kindW + gap + (int)Math.Ceiling(DualFontPainter.Measure(g, this.displayTexts[i], this.halfFont, this.fullFont, this.typographic)) + pad;
                    if (w > max)
                    {
                        max = w;
                    }
                }

                return max;
            }
        }

        private int HeaderHeight(int dpi)
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

        private int RowHeight(int dpi)
        {
            return this.HeaderHeight(dpi);
        }

        private static string FormatItem(Diagnostic d)
        {
            string code = d.Code;
            string message = d.Message;
            string loc = FormatLocation(d);
            string mid;
            if (!string.IsNullOrEmpty(code))
            {
                if (!string.IsNullOrEmpty(message))
                {
                    mid = code + ": " + message;
                }
                else
                {
                    mid = code;
                }
            }
            else
            {
                mid = (message == null) ? "" : message;
            }

            if (string.IsNullOrEmpty(loc))
            {
                return mid;
            }

            if (string.IsNullOrEmpty(mid))
            {
                return loc;
            }

            return mid + "  " + loc;
        }

        private static string FormatLocation(Diagnostic d)
        {
            if (d == null || string.IsNullOrEmpty(d.FilePath))
            {
                return "";
            }

            if (d.Line < 1)
            {
                return d.FilePath;
            }

            if (d.Column < 1)
            {
                return string.Format("{0}({1})", d.FilePath, d.Line);
            }

            return string.Format("{0}({1},{2})", d.FilePath, d.Line, d.Column);
        }
    }
}
