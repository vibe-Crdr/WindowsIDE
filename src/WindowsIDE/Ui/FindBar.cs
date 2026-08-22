using System;
using System.Drawing;
using System.Windows.Forms;
using WindowsIDE.Ui.Fonts;

namespace WindowsIDE.Ui
{
    /// <summary>
    /// タブ直下のファイル内検索・置換バー。下パネルではない。Visible=false のとき占有しない。
    /// </summary>
    public sealed class FindBar : Control
    {
        private const int FindRowHeightDip = 32;
        private const int ReplaceBarHeightDip = 60;

        private readonly TextBox findBox;
        private readonly TextBox replaceBox;
        private readonly ChromeMark searchLabel;
        private readonly ChromeMark replaceLabel;
        private readonly ChromeMark countLabel;
        private readonly ChromeMark caseButton;
        private readonly ChromeMark nextButton;
        private readonly ChromeMark prevButton;
        private readonly ChromeMark replaceButton;
        private readonly ChromeMark replaceAllButton;
        private readonly ChromeMark closeButton;
        private Font halfFont;
        private Font fullFont;
        private bool replaceRowVisible;
        private bool suppressQueryEvent;

        /// <summary>
        /// 非表示の検索バーを組む。
        /// </summary>
        public FindBar()
        {
            this.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            this.TabStop = false;
            this.BackColor = Theme.Background;
            this.ForeColor = Theme.Foreground;
            this.Visible = false;

            this.searchLabel = this.CreateMark("検索", false, false);
            this.countLabel = this.CreateMark("", false, false);
            this.caseButton = this.CreateMark("Aa", true, true);
            this.nextButton = this.CreateMark("次", true, false);
            this.prevButton = this.CreateMark("前", true, false);
            this.closeButton = this.CreateMark("×", true, false);
            this.replaceLabel = this.CreateMark("置換", false, false);
            this.replaceButton = this.CreateMark("置換", true, false);
            this.replaceAllButton = this.CreateMark("すべて", true, false);

            this.findBox = this.CreateBox();
            this.replaceBox = this.CreateBox();
            this.findBox.TabIndex = 0;
            this.replaceBox.TabIndex = 1;

            this.caseButton.Click += this.OnCaseClick;
            this.nextButton.Click += this.OnNextClick;
            this.prevButton.Click += this.OnPrevClick;
            this.replaceButton.Click += this.OnReplaceClick;
            this.replaceAllButton.Click += this.OnReplaceAllClick;
            this.closeButton.Click += this.OnCloseClick;
            this.findBox.TextChanged += this.OnFindTextChanged;
            this.replaceBox.TextChanged += this.OnReplaceTextChanged;
            this.findBox.KeyDown += this.OnFindBoxKeyDown;
            this.replaceBox.KeyDown += this.OnReplaceBoxKeyDown;

            this.Controls.Add(this.searchLabel);
            this.Controls.Add(this.findBox);
            this.Controls.Add(this.countLabel);
            this.Controls.Add(this.caseButton);
            this.Controls.Add(this.nextButton);
            this.Controls.Add(this.prevButton);
            this.Controls.Add(this.closeButton);
            this.Controls.Add(this.replaceLabel);
            this.Controls.Add(this.replaceBox);
            this.Controls.Add(this.replaceButton);
            this.Controls.Add(this.replaceAllButton);
            this.SetReplaceRowVisible(false);
            this.ApplyBarHeight();
        }

        /// <summary>検索語が変わったとき（大小トグルも含む）。</summary>
        public event EventHandler QueryChanged;

        /// <summary>次を検索。</summary>
        public event EventHandler FindNextRequested;

        /// <summary>前を検索。</summary>
        public event EventHandler FindPreviousRequested;

        /// <summary>1 件置換。</summary>
        public event EventHandler ReplaceRequested;

        /// <summary>すべて置換。</summary>
        public event EventHandler ReplaceAllRequested;

        /// <summary>バーを閉じる。</summary>
        public event EventHandler CloseRequested;

        /// <summary>検索欄のテキスト。</summary>
        public string Query
        {
            get { return this.findBox.Text; }
            set
            {
                this.suppressQueryEvent = true;
                this.findBox.Text = (value == null) ? "" : value;
                this.suppressQueryEvent = false;
            }
        }

        /// <summary>置換欄のテキスト。null は空。</summary>
        public string Replacement
        {
            get { return this.replaceBox.Text; }
            set { this.replaceBox.Text = (value == null) ? "" : value; }
        }

        /// <summary>大小を無視するなら true。Aa 非押下が既定。</summary>
        public bool IgnoreCase
        {
            get { return !this.caseButton.Toggled; }
        }

        /// <summary>検索欄がフォーカスを持っているとき true。</summary>
        public bool FindBoxContainsFocus
        {
            get { return this.findBox.Focused || this.findBox.ContainsFocus; }
        }

        /// <summary>
        /// 12 DIP 双フォントを受け取る。所有権は移さない。
        /// </summary>
        /// <param name="half">半角。TextBox にも使う。</param>
        /// <param name="full">全角。</param>
        public void SetFonts(Font half, Font full)
        {
            this.halfFont = half;
            this.fullFont = full;
            if (half != null)
            {
                this.findBox.Font = half;
                this.replaceBox.Font = half;
            }

            this.searchLabel.SetFonts(half, full);
            this.replaceLabel.SetFonts(half, full);
            this.countLabel.SetFonts(half, full);
            this.caseButton.SetFonts(half, full);
            this.nextButton.SetFonts(half, full);
            this.prevButton.SetFonts(half, full);
            this.replaceButton.SetFonts(half, full);
            this.replaceAllButton.SetFonts(half, full);
            this.closeButton.SetFonts(half, full);
            this.PerformLayout();
            this.Invalidate();
        }

        /// <summary>
        /// ヒット件数を出す。queryEmpty なら件数を空にし、検索欄と件数は前景色。
        /// 0 件かつクエリ非空なら検索欄と件数を既存 Error にする。
        /// </summary>
        /// <param name="count">ヒット数。</param>
        /// <param name="queryEmpty">クエリが空なら true。</param>
        public void SetMatchCount(int count, bool queryEmpty)
        {
            if (queryEmpty)
            {
                this.countLabel.Caption = "";
                this.countLabel.ForeColor = Theme.Foreground;
                this.findBox.ForeColor = Theme.Foreground;
            }
            else
            {
                this.countLabel.Caption = count.ToString();
                if (count == 0)
                {
                    this.countLabel.ForeColor = Theme.Error;
                    this.findBox.ForeColor = Theme.Error;
                }
                else
                {
                    this.countLabel.ForeColor = Theme.Foreground;
                    this.findBox.ForeColor = Theme.Foreground;
                }
            }

            this.countLabel.Invalidate();
        }

        /// <summary>検索行だけ出して置換行は隠す。検索欄へフォーカスは呼び出し側。</summary>
        public void ShowFindRow()
        {
            this.replaceRowVisible = false;
            this.SetReplaceRowVisible(false);
            this.ApplyBarHeight();
            this.Visible = true;
            if (this.Parent != null)
            {
                this.Parent.PerformLayout();
            }
        }

        /// <summary>検索行と置換行を出す。</summary>
        public void ShowReplaceRow()
        {
            this.replaceRowVisible = true;
            this.SetReplaceRowVisible(true);
            this.ApplyBarHeight();
            this.Visible = true;
            if (this.Parent != null)
            {
                this.Parent.PerformLayout();
            }
        }

        /// <summary>バーを出す。置換行の開閉は維持する。</summary>
        public void ShowPreservingReplaceRow()
        {
            this.SetReplaceRowVisible(this.replaceRowVisible);
            this.ApplyBarHeight();
            this.Visible = true;
            if (this.Parent != null)
            {
                this.Parent.PerformLayout();
            }
        }

        /// <summary>バーを隠す。クエリと置換行フラグは残す。</summary>
        public void HideBar()
        {
            this.Visible = false;
        }

        /// <summary>検索欄へフォーカスする。</summary>
        public void FocusFindBox()
        {
            this.findBox.Focus();
        }

        /// <summary>置換欄へフォーカスする。</summary>
        public void FocusReplaceBox()
        {
            this.replaceBox.Focus();
        }

        /// <summary>検索欄を全選択する。</summary>
        public void SelectFindBoxAll()
        {
            this.findBox.SelectAll();
        }

        /// <summary>DPI に合わせてバー高さを付け直す。</summary>
        protected override void OnDpiChangedAfterParent(EventArgs e)
        {
            base.OnDpiChangedAfterParent(e);
            if (this.Visible)
            {
                this.ApplyBarHeight();
            }

            this.PerformLayout();
        }

        /// <summary>下端に細い枠を描く。</summary>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (e == null || e.Graphics == null)
            {
                return;
            }

            using (Pen p = new Pen(Theme.Border))
            {
                int y = this.ClientSize.Height - 1;
                e.Graphics.DrawLine(p, 0, y, this.ClientSize.Width, y);
            }
        }

        /// <summary>検索行と置換行を DIP 定数で並べる。</summary>
        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            int dpi = DpiUtil.GetDpi(this.IsHandleCreated ? this.Handle : IntPtr.Zero);
            int pad = DpiUtil.ToPixels(6, dpi);
            int gap = DpiUtil.ToPixels(4, dpi);
            int findH = DpiUtil.ToPixels(FindRowHeightDip, dpi);
            int inner = findH - DpiUtil.ToPixels(8, dpi);
            if (inner < 16)
            {
                inner = 16;
            }

            Graphics g = this.CreateGraphics();
            try
            {
                int y0 = (findH - inner) / 2;
                int x = pad;
                int labelW = this.MeasureMark(g, this.searchLabel, 36);
                this.searchLabel.Bounds = new Rectangle(x, y0, labelW, inner);
                x += labelW + gap;

                int countW = this.MeasureMark(g, this.countLabel, 28);
                if (countW < DpiUtil.ToPixels(28, dpi))
                {
                    countW = DpiUtil.ToPixels(28, dpi);
                }

                int caseW = this.MeasureMark(g, this.caseButton, 28);
                int nextW = this.MeasureMark(g, this.nextButton, 32);
                int prevW = this.MeasureMark(g, this.prevButton, 32);
                int closeW = this.MeasureMark(g, this.closeButton, 22);
                int right = countW + gap + caseW + gap + nextW + gap + prevW + gap + closeW + pad;
                int boxW = this.ClientSize.Width - x - right;
                int minBox = DpiUtil.ToPixels(80, dpi);
                if (boxW < minBox)
                {
                    boxW = minBox;
                }

                this.findBox.Bounds = new Rectangle(x, y0, boxW, inner);
                x += boxW + gap;
                this.countLabel.Bounds = new Rectangle(x, y0, countW, inner);
                x += countW + gap;
                this.caseButton.Bounds = new Rectangle(x, y0, caseW, inner);
                x += caseW + gap;
                this.nextButton.Bounds = new Rectangle(x, y0, nextW, inner);
                x += nextW + gap;
                this.prevButton.Bounds = new Rectangle(x, y0, prevW, inner);
                x += prevW + gap;
                this.closeButton.Bounds = new Rectangle(x, y0, closeW, inner);

                if (this.replaceRowVisible)
                {
                    int y1 = findH + (findH - inner) / 2;
                    if (y1 + inner > this.ClientSize.Height)
                    {
                        y1 = this.ClientSize.Height - inner;
                        if (y1 < findH)
                        {
                            y1 = findH;
                        }
                    }

                    int rx = pad;
                    int rLabelW = this.MeasureMark(g, this.replaceLabel, 36);
                    this.replaceLabel.Bounds = new Rectangle(rx, y1, rLabelW, inner);
                    rx += rLabelW + gap;
                    int replW = this.MeasureMark(g, this.replaceButton, 36);
                    int allW = this.MeasureMark(g, this.replaceAllButton, 44);
                    int rRight = replW + gap + allW + pad;
                    int rBoxW = this.ClientSize.Width - rx - rRight;
                    if (rBoxW < minBox)
                    {
                        rBoxW = minBox;
                    }

                    this.replaceBox.Bounds = new Rectangle(rx, y1, rBoxW, inner);
                    rx += rBoxW + gap;
                    this.replaceButton.Bounds = new Rectangle(rx, y1, replW, inner);
                    rx += replW + gap;
                    this.replaceAllButton.Bounds = new Rectangle(rx, y1, allW, inner);
                }
            }
            finally
            {
                g.Dispose();
            }
        }

        private void ApplyBarHeight()
        {
            int dpi = DpiUtil.GetDpi(this.IsHandleCreated ? this.Handle : IntPtr.Zero);
            int dip = this.replaceRowVisible ? ReplaceBarHeightDip : FindRowHeightDip;
            this.Height = DpiUtil.ToPixels(dip, dpi);
            if (this.Parent != null)
            {
                this.Parent.PerformLayout();
            }
        }

        private void SetReplaceRowVisible(bool visible)
        {
            this.replaceLabel.Visible = visible;
            this.replaceBox.Visible = visible;
            this.replaceButton.Visible = visible;
            this.replaceAllButton.Visible = visible;
        }

        private TextBox CreateBox()
        {
            TextBox box = new TextBox();
            box.BorderStyle = BorderStyle.FixedSingle;
            box.BackColor = Theme.EditorBackground;
            box.ForeColor = Theme.Foreground;
            box.AcceptsReturn = false;
            box.AcceptsTab = false;
            return box;
        }

        private ChromeMark CreateMark(string caption, bool clickable, bool toggle)
        {
            ChromeMark mark = new ChromeMark(caption, clickable, toggle);
            return mark;
        }

        private int MeasureMark(Graphics g, ChromeMark mark, int minDip)
        {
            int dpi = DpiUtil.GetDpi(this.IsHandleCreated ? this.Handle : IntPtr.Zero);
            int min = DpiUtil.ToPixels(minDip, dpi);
            int pad = DpiUtil.ToPixels(10, dpi);
            if (g == null || this.halfFont == null || this.fullFont == null)
            {
                return min;
            }

            int w = (int)Math.Ceiling((double)DualFontPainter.Measure(g, mark.Caption, this.halfFont, this.fullFont, null)) + pad;
            if (w < min)
            {
                return min;
            }

            return w;
        }

        private void OnCaseClick(object sender, EventArgs e)
        {
            this.RaiseQueryChanged();
        }

        private void OnNextClick(object sender, EventArgs e)
        {
            this.Raise(this.FindNextRequested);
        }

        private void OnPrevClick(object sender, EventArgs e)
        {
            this.Raise(this.FindPreviousRequested);
        }

        private void OnReplaceClick(object sender, EventArgs e)
        {
            this.Raise(this.ReplaceRequested);
        }

        private void OnReplaceAllClick(object sender, EventArgs e)
        {
            this.Raise(this.ReplaceAllRequested);
        }

        private void OnCloseClick(object sender, EventArgs e)
        {
            this.Raise(this.CloseRequested);
        }

        private void OnFindTextChanged(object sender, EventArgs e)
        {
            if (this.suppressQueryEvent)
            {
                return;
            }

            this.RaiseQueryChanged();
        }

        private void OnReplaceTextChanged(object sender, EventArgs e)
        {
        }

        private void OnFindBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
            {
                return;
            }

            e.Handled = true;
            e.SuppressKeyPress = true;
            if (e.Shift)
            {
                this.Raise(this.FindPreviousRequested);
            }
            else
            {
                this.Raise(this.FindNextRequested);
            }
        }

        private void OnReplaceBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
            {
                return;
            }

            e.Handled = true;
            e.SuppressKeyPress = true;
            if (e.Shift)
            {
                this.Raise(this.FindPreviousRequested);
            }
            else
            {
                this.Raise(this.ReplaceRequested);
            }
        }

        private void RaiseQueryChanged()
        {
            this.Raise(this.QueryChanged);
        }

        private void Raise(EventHandler handler)
        {
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private sealed class ChromeMark : Control
        {
            private readonly bool clickable;
            private readonly bool toggle;
            private bool toggled;
            private bool hot;
            private string caption;
            private Font halfFont;
            private Font fullFont;

            public ChromeMark(string caption, bool clickable, bool toggle)
            {
                this.caption = (caption == null) ? "" : caption;
                this.clickable = clickable;
                this.toggle = toggle;
                this.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
                this.TabStop = false;
                this.BackColor = Theme.Background;
            }

            public string Caption
            {
                get { return this.caption; }
                set
                {
                    this.caption = (value == null) ? "" : value;
                    this.Invalidate();
                }
            }

            public bool Toggled
            {
                get { return this.toggled; }
            }

            public void SetFonts(Font half, Font full)
            {
                this.halfFont = half;
                this.fullFont = full;
                this.Invalidate();
            }

            protected override void OnMouseEnter(EventArgs e)
            {
                base.OnMouseEnter(e);
                if (!this.clickable)
                {
                    return;
                }

                this.hot = true;
                this.Invalidate();
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                base.OnMouseLeave(e);
                this.hot = false;
                this.Invalidate();
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e);
                if (!this.clickable || e.Button != MouseButtons.Left)
                {
                    return;
                }

                if (this.toggle)
                {
                    this.toggled = !this.toggled;
                }

                this.OnClick(EventArgs.Empty);
                this.Invalidate();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                if (e == null || e.Graphics == null)
                {
                    return;
                }

                Color bg = Theme.Background;
                if (this.clickable && this.toggle && this.toggled)
                {
                    bg = Theme.Selection;
                }
                else if (this.clickable && this.hot)
                {
                    bg = Theme.CurrentLine;
                }

                using (SolidBrush fill = new SolidBrush(bg))
                {
                    e.Graphics.FillRectangle(fill, this.ClientRectangle);
                }

                if (this.clickable)
                {
                    using (Pen border = new Pen(Theme.Border))
                    {
                        e.Graphics.DrawRectangle(border, 0, 0, this.Width - 1, this.Height - 1);
                    }
                }

                if (this.halfFont == null || this.fullFont == null)
                {
                    return;
                }

                using (SolidBrush fg = new SolidBrush(this.ForeColor))
                {
                    Rectangle clip = this.ClientRectangle;
                    clip.Inflate(-2, 0);
                    DualFontPainter.DrawEllipsis(e.Graphics, this.caption, this.halfFont, this.fullFont, clip, fg, null);
                }
            }
        }
    }
}
