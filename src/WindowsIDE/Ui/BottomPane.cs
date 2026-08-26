using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using WindowsIDE.Build;
using WindowsIDE.Ui.Fonts;

namespace WindowsIDE.Ui
{
    /// <summary>
    /// 下パネル。タブは問題 / 出力 / ターミナル。ヘッダ（タブチップ + 件数 + ×）を持つ。
    /// </summary>
    public sealed class BottomPane : Control
    {
        private readonly ProblemListControl problems;
        private readonly OutputPanelControl output;
        private readonly TerminalControl terminal;
        private Font halfFont;
        private Font fullFont;
        private StringFormat typographic;
        private int selectedTab;
        private Rectangle problemsChip;
        private Rectangle outputChip;
        private Rectangle terminalChip;
        private Rectangle closeBounds;

        /// <summary>
        /// 問題と出力を組む。初期選択は問題。
        /// </summary>
        public BottomPane()
        {
            this.selectedTab = 0;
            this.typographic = (StringFormat)StringFormat.GenericTypographic.Clone();
            this.typographic.FormatFlags = this.typographic.FormatFlags | StringFormatFlags.MeasureTrailingSpaces | StringFormatFlags.NoWrap | StringFormatFlags.FitBlackBox;
            this.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            this.TabStop = false;
            this.BackColor = Theme.Background;
            this.ForeColor = Theme.Foreground;

            this.problems = new ProblemListControl();
            this.output = new OutputPanelControl();
            this.terminal = new TerminalControl();
            this.problems.Visible = true;
            this.output.Visible = false;
            this.terminal.Visible = false;
            this.Controls.Add(this.problems);
            this.Controls.Add(this.output);
            this.Controls.Add(this.terminal);
        }

        /// <summary>× で畳む要求。</summary>
        public event EventHandler CloseRequested;

        /// <summary>ヘッダのターミナルチップを選んだ。PTY 起動は親が行う。</summary>
        public event EventHandler TerminalSelected;

        /// <summary>問題一覧の行クリック。</summary>
        public event EventHandler<ProblemActivatedEventArgs> ItemActivated
        {
            add { this.problems.ItemActivated += value; }
            remove { this.problems.ItemActivated -= value; }
        }

        /// <summary>問題一覧。</summary>
        public ProblemListControl Problems
        {
            get { return this.problems; }
        }

        /// <summary>出力。</summary>
        public OutputPanelControl Output
        {
            get { return this.output; }
        }

        /// <summary>統合ターミナル。</summary>
        public TerminalControl Terminal
        {
            get { return this.terminal; }
        }

        /// <summary>ターミナルがフォーカスを持っていれば true。</summary>
        public bool IsTerminalFocused
        {
            get { return this.terminal != null && this.terminal.ContainsFocus; }
        }

        /// <summary>ターミナルが IME 変換中なら true。</summary>
        public bool IsTerminalComposing
        {
            get { return this.terminal != null && this.terminal.IsComposing; }
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
            this.problems.SetFonts(half, full);
            this.output.SetFonts(half, full);
            this.terminal.SetFonts(half, full);
            this.LayoutChildren();
            this.Invalidate();
        }

        /// <summary>
        /// 問題一覧を置き換えて問題タブを出す。フォーカスは奪わない。
        /// </summary>
        /// <param name="diagnostics">csc または合成。null は空。</param>
        public void SetProblemItems(IList<Diagnostic> diagnostics)
        {
            this.problems.SetItems(diagnostics);
            this.ShowProblems();
        }

        /// <summary>問題タブを出す。フォーカスは奪わない。</summary>
        public void ShowProblems()
        {
            this.selectedTab = 0;
            this.problems.Visible = true;
            this.output.Visible = false;
            this.terminal.Visible = false;
            this.LayoutChildren();
            this.Invalidate();
        }

        /// <summary>出力タブを出す。フォーカスは奪わない。</summary>
        public void ShowOutput()
        {
            this.selectedTab = 1;
            this.problems.Visible = false;
            this.output.Visible = true;
            this.terminal.Visible = false;
            this.LayoutChildren();
            this.Invalidate();
        }

        /// <summary>ターミナルタブを出し、ターミナルへフォーカスする。</summary>
        public void ShowTerminal()
        {
            this.selectedTab = 2;
            this.problems.Visible = false;
            this.output.Visible = false;
            this.terminal.Visible = true;
            this.LayoutChildren();
            this.Invalidate();
            this.terminal.Focus();
        }

        /// <summary>親の DPI 変更後に配置を合わせる。</summary>
        protected override void OnDpiChangedAfterParent(EventArgs e)
        {
            base.OnDpiChangedAfterParent(e);
            this.LayoutChildren();
            this.Invalidate();
        }

        /// <summary>サイズ変更で子を置き直す。</summary>
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            this.LayoutChildren();
        }

        /// <summary>ハンドル作成後に子を置く。</summary>
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            this.LayoutChildren();
        }

        /// <summary>ヘッダ（タブチップ・件数・×）を描く。</summary>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            int dpi = DpiUtil.GetDpi(this.IsHandleCreated ? this.Handle : IntPtr.Zero);
            int headerH = this.HeaderHeight(dpi);
            using (SolidBrush bg = new SolidBrush(Theme.Background))
            {
                g.FillRectangle(bg, new Rectangle(0, 0, this.ClientSize.Width, headerH));
            }

            this.DrawHeader(g, new Rectangle(0, 0, this.ClientSize.Width, headerH), dpi);
            using (Pen border = new Pen(Theme.Border))
            {
                g.DrawRectangle(border, 0, 0, this.ClientSize.Width - 1, this.ClientSize.Height - 1);
                if (headerH > 0 && headerH < this.ClientSize.Height)
                {
                    g.DrawLine(border, 0, headerH - 1, this.ClientSize.Width, headerH - 1);
                }
            }
        }

        /// <summary>タブチップまたは ×。</summary>
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

            if (this.problemsChip.Contains(e.Location))
            {
                this.ShowProblems();
                return;
            }

            if (this.outputChip.Contains(e.Location))
            {
                this.ShowOutput();
                return;
            }

            if (this.terminalChip.Contains(e.Location))
            {
                this.ShowTerminal();
                EventHandler selected = this.TerminalSelected;
                if (selected != null)
                {
                    selected(this, EventArgs.Empty);
                }
            }
        }

        /// <summary>借用フォントは破棄しない。</summary>
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
                this.problemsChip = Rectangle.Empty;
                this.outputChip = Rectangle.Empty;
                this.terminalChip = Rectangle.Empty;
                return;
            }

            int chipPad = DpiUtil.ToPixels(8, dpi);
            int x = header.X + DpiUtil.ToPixels(4, dpi);
            this.problemsChip = this.DrawChip(g, header, dpi, x, "問題", this.selectedTab == 0);
            x = this.problemsChip.Right + DpiUtil.ToPixels(2, dpi);
            this.outputChip = this.DrawChip(g, header, dpi, x, "出力", this.selectedTab == 1);
            x = this.outputChip.Right + DpiUtil.ToPixels(2, dpi);
            this.terminalChip = this.DrawChip(g, header, dpi, x, "ターミナル", this.selectedTab == 2);

            string counts = string.Format("エラー {0}, 警告 {1}", this.problems.ErrorCount, this.problems.WarningCount);
            int countX = this.terminalChip.Right + chipPad;
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

        private Rectangle DrawChip(Graphics g, Rectangle header, int dpi, int x, string title, bool selected)
        {
            int pad = DpiUtil.ToPixels(8, dpi);
            int textW = (int)Math.Ceiling(DualFontPainter.Measure(g, title, this.halfFont, this.fullFont, this.typographic));
            int w = textW + pad + pad;
            int minW = DpiUtil.ToPixels(48, dpi);
            if (w < minW)
            {
                w = minW;
            }

            int chipH = header.Height - DpiUtil.ToPixels(4, dpi);
            if (chipH < 1)
            {
                chipH = header.Height;
            }

            int y = header.Y + ((header.Height - chipH) / 2);
            Rectangle chip = new Rectangle(x, y, w, chipH);
            Color back = selected ? Theme.EditorBackground : Theme.Background;
            using (SolidBrush b = new SolidBrush(back))
            {
                g.FillRectangle(b, chip);
            }

            using (Pen p = new Pen(Theme.Border))
            {
                g.DrawRectangle(p, chip.X, chip.Y, chip.Width - 1, chip.Height - 1);
            }

            using (SolidBrush fg = new SolidBrush(Theme.Foreground))
            {
                DualFontPainter.DrawEllipsis(g, title, this.halfFont, this.fullFont, chip, fg, this.typographic);
            }

            return chip;
        }

        private void LayoutChildren()
        {
            if (!this.IsHandleCreated)
            {
                return;
            }

            int dpi = DpiUtil.GetDpi(this.Handle);
            int headerH = this.HeaderHeight(dpi);
            Rectangle content = new Rectangle(0, headerH, this.ClientSize.Width, Math.Max(0, this.ClientSize.Height - headerH));
            this.problems.Bounds = content;
            this.output.Bounds = content;
            this.terminal.Bounds = content;
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

            return DpiUtil.TabStripHeight(fontH, dpi);
        }
    }
}
