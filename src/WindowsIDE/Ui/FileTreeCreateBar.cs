using System;
using System.Drawing;
using System.Windows.Forms;
using WindowsIDE.Ui.Fonts;

namespace WindowsIDE.Ui
{
    /// <summary>
    /// 左ペイン上端のファイル／フォルダ作成バー。ツリーの Controls には入れない。本文 fontSize 非連動。
    /// </summary>
    public sealed class FileTreeCreateBar : Control
    {
        private readonly ChromeMark fileMark;
        private readonly ChromeMark folderMark;
        private readonly ToolTip toolTip;
        private Font halfFont;
        private Font fullFont;

        /// <summary>
        /// 「ファイル」「フォルダ」ボタン付きのバーを組む。
        /// </summary>
        public FileTreeCreateBar()
        {
            this.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            this.TabStop = false;
            this.BackColor = Theme.Background;
            this.ForeColor = Theme.Foreground;

            this.fileMark = new ChromeMark("ファイル", true, false);
            this.folderMark = new ChromeMark("フォルダ", true, false);
            this.fileMark.Click += this.OnFileClick;
            this.folderMark.Click += this.OnFolderClick;
            this.Controls.Add(this.fileMark);
            this.Controls.Add(this.folderMark);

            this.toolTip = new ToolTip();
            this.toolTip.SetToolTip(this.fileMark, "ファイルを作成");
            this.toolTip.SetToolTip(this.folderMark, "フォルダを作成");
            this.ApplyBarHeight();
        }

        /// <summary>ファイル作成ボタン。</summary>
        public event EventHandler FileCreateRequested;

        /// <summary>フォルダ作成ボタン。</summary>
        public event EventHandler FolderCreateRequested;

        /// <summary>
        /// 12 DIP 双フォントを参照する。所有権は移さない。本文 fontSize には使わない。
        /// </summary>
        /// <param name="half">半角。</param>
        /// <param name="full">全角。</param>
        public void SetFonts(Font half, Font full)
        {
            this.halfFont = half;
            this.fullFont = full;
            this.fileMark.SetFonts(half, full);
            this.folderMark.SetFonts(half, full);
            this.ApplyBarHeight();
            this.PerformLayout();
            this.Invalidate();
        }

        /// <summary>ハンドル作成後に高さを付け直す。</summary>
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            this.ApplyBarHeight();
        }

        /// <summary>DPI 変更後に高さを付け直す。</summary>
        protected override void OnDpiChangedAfterParent(EventArgs e)
        {
            base.OnDpiChangedAfterParent(e);
            this.ApplyBarHeight();
            this.PerformLayout();
        }

        /// <summary>下端に 1 物理 px の Theme.Border を描く。</summary>
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

        /// <summary>左 6 DIP、間隔 4 DIP でボタンを並べる。</summary>
        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            int dpi = DpiUtil.GetDpi(this.IsHandleCreated ? this.Handle : IntPtr.Zero);
            int pad = DpiUtil.ToPixels(6, dpi);
            int gap = DpiUtil.ToPixels(4, dpi);
            int vpad = DpiUtil.ToPixels(4, dpi);
            int markH = this.FontHeightPx();
            if (markH < 1)
            {
                markH = 1;
            }

            Graphics g = this.CreateGraphics();
            try
            {
                int fileW = this.MeasureMark(g, this.fileMark, 36);
                int folderW = this.MeasureMark(g, this.folderMark, 36);
                int x = pad;
                this.fileMark.Bounds = new Rectangle(x, vpad, fileW, markH);
                x += fileW + gap;
                this.folderMark.Bounds = new Rectangle(x, vpad, folderW, markH);
            }
            finally
            {
                g.Dispose();
            }
        }

        /// <summary>ツールチップを破棄する。</summary>
        /// <param name="disposing">マネージドも捨てるなら true。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this.toolTip != null)
                {
                    this.toolTip.Dispose();
                }
            }

            base.Dispose(disposing);
        }

        private void ApplyBarHeight()
        {
            int dpi = DpiUtil.GetDpi(this.IsHandleCreated ? this.Handle : IntPtr.Zero);
            int vpad = DpiUtil.ToPixels(4, dpi);
            this.Height = this.FontHeightPx() + vpad * 2;
            if (this.Parent != null)
            {
                this.Parent.PerformLayout();
            }
        }

        private int FontHeightPx()
        {
            int h = 8;
            if (this.halfFont != null)
            {
                h = this.halfFont.Height;
            }

            if (this.fullFont != null && this.fullFont.Height > h)
            {
                h = this.fullFont.Height;
            }

            return h;
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

        private void OnFileClick(object sender, EventArgs e)
        {
            this.Raise(this.FileCreateRequested);
        }

        private void OnFolderClick(object sender, EventArgs e)
        {
            this.Raise(this.FolderCreateRequested);
        }

        private void Raise(EventHandler handler)
        {
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }
    }
}
