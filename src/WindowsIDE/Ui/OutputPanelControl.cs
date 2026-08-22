using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using WindowsIDE.Ui.Fonts;

namespace WindowsIDE.Ui
{
    /// <summary>
    /// 下パネルの出力。オーナー描画 + ThemedScrollBar。RichTextBox は使わない。
    /// </summary>
    public sealed class OutputPanelControl : Control
    {
        private const int WM_MOUSEHWHEEL = 0x020E;
        private const int MaxLines = 4000;
        private const int MaxMeasureChars = 4096;

        private readonly List<OutputLine> lines;
        private readonly ThemedScrollBar vScroll;
        private readonly ThemedScrollBar hScroll;
        private Font halfFont;
        private Font fullFont;
        private StringFormat typographic;
        private int contentWidth;
        private int wheelLeftover;
        private bool ignoreScroll;

        /// <summary>
        /// 空の出力を組む。
        /// </summary>
        public OutputPanelControl()
        {
            this.lines = new List<OutputLine>();
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
        /// 行を捨てる。フォーカスは奪わない。
        /// </summary>
        public void Clear()
        {
            this.lines.Clear();
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

        /// <summary>
        /// stdout または stderr の 1 行を末尾へ。4000 行を超えたら先頭を捨てる。
        /// </summary>
        /// <param name="text">1 行。null は空。</param>
        /// <param name="isStderr">stderr なら true。</param>
        public void Append(string text, bool isStderr)
        {
            this.AddLine((text == null) ? "" : text, isStderr ? 1 : 0);
        }

        /// <summary>
        /// 起動／終了などの 1 行を末尾へ（Comment 色）。
        /// </summary>
        /// <param name="text">1 行。null は空。</param>
        public void AppendStatus(string text)
        {
            this.AddLine((text == null) ? "" : text, 2);
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

        /// <summary>行と枠を描く。</summary>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            int dpi = DpiUtil.GetDpi(this.IsHandleCreated ? this.Handle : IntPtr.Zero);
            using (SolidBrush bg = new SolidBrush(Theme.Background))
            {
                g.FillRectangle(bg, this.ClientRectangle);
            }

            this.DrawRows(g, dpi);
            using (Pen border = new Pen(Theme.Border))
            {
                g.DrawRectangle(border, 0, 0, this.ClientSize.Width - 1, this.ClientSize.Height - 1);
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

        private void AddLine(string text, int kind)
        {
            OutputLine line = new OutputLine();
            line.Text = text;
            line.Kind = kind;
            this.lines.Add(line);
            while (this.lines.Count > MaxLines)
            {
                this.lines.RemoveAt(0);
            }

            this.RefreshChrome();
            this.ScrollToEnd();
            this.Invalidate();
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

            this.vScroll.Value = max;
        }

        private void OnScrollChanged(object sender, EventArgs e)
        {
            if (this.ignoreScroll)
            {
                return;
            }

            this.Invalidate();
        }

        private void DrawRows(Graphics g, int dpi)
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
            int bar = DpiUtil.ToPixels(DpiUtil.ScrollBarThicknessDip, dpi);
            int vW = this.vScroll.Visible ? bar : 0;
            int hH = this.hScroll.Visible ? bar : 0;
            Rectangle list = new Rectangle(0, 0, Math.Max(0, this.ClientSize.Width - vW), Math.Max(0, this.ClientSize.Height - hH));
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
            if (last > this.lines.Count)
            {
                last = this.lines.Count;
            }

            for (int i = first; i < last; i++)
            {
                int y = list.Y + ((i - first) * rowH);
                OutputLine line = this.lines[i];
                Color color = Theme.Foreground;
                if (line.Kind == 1)
                {
                    color = Theme.Error;
                }
                else if (line.Kind == 2)
                {
                    color = Theme.Comment;
                }

                Rectangle clip = new Rectangle(list.X, y, list.Width, rowH);
                using (SolidBrush brush = new SolidBrush(color))
                {
                    DualFontPainter.Draw(g, line.Text, this.halfFont, this.fullFont, Rectangle.Intersect(clip, list), originX, brush, this.typographic);
                }
            }
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
            int clientW = this.ClientSize.Width;
            int clientH = this.ClientSize.Height;
            int listH = Math.Max(0, clientH);
            int visibleRows = listH / rowH;
            if (visibleRows < 1)
            {
                visibleRows = 1;
            }

            int itemCount = this.lines.Count;
            bool needV = DpiUtil.ScrollBarNeeded(0, Math.Max(0, itemCount - 1), visibleRows) && itemCount > visibleRows;
            int listW = clientW - (needV ? bar : 0);
            this.contentWidth = this.MeasureContentWidth(dpi);
            bool needH = this.contentWidth > listW;
            if (needH)
            {
                listH = Math.Max(0, clientH - bar);
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
            this.vScroll.Bounds = new Rectangle(this.ClientSize.Width - vW, 0, vW, Math.Max(0, this.ClientSize.Height - hHBar));
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
                int max = 0;
                for (int i = 0; i < this.lines.Count; i++)
                {
                    string text = this.lines[i].Text;
                    if (text == null)
                    {
                        text = "";
                    }

                    if (text.Length > MaxMeasureChars)
                    {
                        text = text.Substring(0, MaxMeasureChars);
                    }

                    int w = pad + (int)Math.Ceiling(DualFontPainter.Measure(g, text, this.halfFont, this.fullFont, this.typographic)) + pad;
                    if (w > max)
                    {
                        max = w;
                    }
                }

                return max;
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

        private sealed class OutputLine
        {
            public string Text;
            public int Kind;
        }
    }
}
