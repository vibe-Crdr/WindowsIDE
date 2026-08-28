using System;
using System.Drawing;
using System.Windows.Forms;
using WindowsIDE.Ui.Fonts;
using WindowsIDE.Workspace;

namespace WindowsIDE.Ui
{
    /// <summary>
    /// タブ直下のファイル内検索・置換バー。下パネルではない。Visible=false のとき占有しない。
    /// </summary>
    public sealed class FindBar : Control
    {
        private readonly DualFontField findBox;
        private readonly DualFontField replaceBox;
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
        private FontLoadResult fontSource;
        private Font inputHalf;
        private Font inputFull;
        private int editorFontDip;
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

        /// <summary>検索または置換欄が IME 変換中のとき true。</summary>
        public bool IsComposing
        {
            get { return this.findBox.IsComposing || this.replaceBox.IsComposing; }
        }

        /// <summary>
        /// ラベル・件数・ボタン用の 12 DIP 双フォントを受け取る。所有権は移さない。検索欄の本文サイズには使わない。
        /// </summary>
        /// <param name="half">半角。</param>
        /// <param name="full">全角。</param>
        public void SetFonts(Font half, Font full)
        {
            this.halfFont = half;
            this.fullFont = full;

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
        /// 検索・置換欄を本文と同じ半角ファミリ・fontSize DIP にする。フォントは FindBar が所有する。
        /// </summary>
        /// <param name="fonts">同梱フォント。null なら何もしない。</param>
        /// <param name="dipSize">本文と同じ 96dpi DIP。0 以下なら DefaultFontSize。</param>
        public void SetEditorInputFont(FontLoadResult fonts, int dipSize)
        {
            if (fonts == null)
            {
                return;
            }

            this.fontSource = fonts;
            if (dipSize > 0)
            {
                this.editorFontDip = dipSize;
            }
            else
            {
                this.editorFontDip = WorkspaceSettings.DefaultFontSize;
            }

            this.RecreateInputFont();
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
                this.replaceBox.ForeColor = Theme.Foreground;
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

                this.replaceBox.ForeColor = Theme.Foreground;
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

        /// <summary>ハンドル作成後に本文 DIP を物理 px へ換算し直す。</summary>
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            this.RecreateInputFont();
            if (this.Visible)
            {
                this.ApplyBarHeight();
            }
        }

        /// <summary>DPI に合わせて入力フォントとバー高さを付け直す。</summary>
        protected override void OnDpiChangedAfterParent(EventArgs e)
        {
            base.OnDpiChangedAfterParent(e);
            this.RecreateInputFont();
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

        /// <summary>検索行と置換行を、入力欄の PreferredOuterHeight に合わせて並べる。</summary>
        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            int dpi = DpiUtil.GetDpi(this.IsHandleCreated ? this.Handle : IntPtr.Zero);
            int pad = DpiUtil.ToPixels(6, dpi);
            int gap = DpiUtil.ToPixels(4, dpi);
            int findH = this.RowHeightPx(dpi);
            int boxH = this.InputBoxHeight();
            int y0 = (findH - boxH) / 2;
            if (y0 < 0)
            {
                y0 = 0;
            }

            Graphics g = this.CreateGraphics();
            try
            {
                int x = pad;
                int labelW = this.MeasureMark(g, this.searchLabel, 36);
                this.searchLabel.Bounds = new Rectangle(x, y0, labelW, boxH);
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

                this.findBox.Bounds = new Rectangle(x, y0, boxW, boxH);
                x += boxW + gap;
                this.countLabel.Bounds = new Rectangle(x, y0, countW, boxH);
                x += countW + gap;
                this.caseButton.Bounds = new Rectangle(x, y0, caseW, boxH);
                x += caseW + gap;
                this.nextButton.Bounds = new Rectangle(x, y0, nextW, boxH);
                x += nextW + gap;
                this.prevButton.Bounds = new Rectangle(x, y0, prevW, boxH);
                x += prevW + gap;
                this.closeButton.Bounds = new Rectangle(x, y0, closeW, boxH);

                if (this.replaceRowVisible)
                {
                    int y1 = findH + y0;
                    int rx = pad;
                    int rLabelW = this.MeasureMark(g, this.replaceLabel, 36);
                    this.replaceLabel.Bounds = new Rectangle(rx, y1, rLabelW, boxH);
                    rx += rLabelW + gap;
                    int replW = this.MeasureMark(g, this.replaceButton, 36);
                    int allW = this.MeasureMark(g, this.replaceAllButton, 44);
                    int rRight = replW + gap + allW + pad;
                    int rBoxW = this.ClientSize.Width - rx - rRight;
                    if (rBoxW < minBox)
                    {
                        rBoxW = minBox;
                    }

                    this.replaceBox.Bounds = new Rectangle(rx, y1, rBoxW, boxH);
                    rx += rBoxW + gap;
                    this.replaceButton.Bounds = new Rectangle(rx, y1, replW, boxH);
                    rx += replW + gap;
                    this.replaceAllButton.Bounds = new Rectangle(rx, y1, allW, boxH);
                }
            }
            finally
            {
                g.Dispose();
            }
        }

        private int InputBoxHeight()
        {
            int h = this.findBox.PreferredOuterHeight;
            int rh = this.replaceBox.PreferredOuterHeight;
            if (rh > h)
            {
                h = rh;
            }

            return h;
        }

        private int RowHeightPx(int dpi)
        {
            int vpad = DpiUtil.ToPixels(4, dpi);
            return this.InputBoxHeight() + vpad * 2;
        }

        private void ApplyBarHeight()
        {
            int dpi = DpiUtil.GetDpi(this.IsHandleCreated ? this.Handle : IntPtr.Zero);
            int findH = this.RowHeightPx(dpi);
            this.Height = this.replaceRowVisible ? (findH * 2) : findH;
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

        private void RecreateInputFont()
        {
            if (this.fontSource == null || this.editorFontDip <= 0)
            {
                return;
            }

            int dpi = DpiUtil.GetDpi(this.IsHandleCreated ? this.Handle : IntPtr.Zero);
            int px = DpiUtil.ToPixels(this.editorFontDip, dpi);
            Font newHalf = this.fontSource.CreateHalfWidth(px);
            Font newFull = this.fontSource.CreateFullWidth(px);
            Font oldHalf = this.inputHalf;
            Font oldFull = this.inputFull;
            this.inputHalf = newHalf;
            this.inputFull = newFull;
            this.findBox.SetFonts(newHalf, newFull, this.editorFontDip);
            this.replaceBox.SetFonts(newHalf, newFull, this.editorFontDip);
            if (oldHalf != null)
            {
                oldHalf.Dispose();
            }

            if (oldFull != null)
            {
                oldFull.Dispose();
            }

            this.ApplyBarHeight();
            this.PerformLayout();
        }

        private DualFontField CreateBox()
        {
            DualFontField box = new DualFontField();
            box.BackColor = Theme.EditorBackground;
            box.ForeColor = Theme.Foreground;
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

            if (this.findBox.IsComposing)
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

            if (this.replaceBox.IsComposing)
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

        /// <summary>所有する半角・全角入力フォントを破棄する。フィールド Font には割り当てない。</summary>
        /// <param name="disposing">マネージドも捨てるなら true。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this.inputHalf != null)
                {
                    this.inputHalf.Dispose();
                    this.inputHalf = null;
                }

                if (this.inputFull != null)
                {
                    this.inputFull.Dispose();
                    this.inputFull = null;
                }
            }

            base.Dispose(disposing);
        }
    }
}
