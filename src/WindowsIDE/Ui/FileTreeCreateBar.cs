using System;
using System.Drawing;
using System.Windows.Forms;

namespace WindowsIDE.Ui
{
    /// <summary>
    /// 左ペイン上端のファイル／フォルダ作成バー。ツリーの Controls には入れない。本文 fontSize 非連動。
    /// </summary>
    public sealed class FileTreeCreateBar : Control
    {
        private readonly CreateBarGlyph fileGlyph;
        private readonly CreateBarGlyph folderGlyph;
        private readonly ToolTip toolTip;

        /// <summary>
        /// 線画アイコン2つのバーを組む。
        /// </summary>
        public FileTreeCreateBar()
        {
            this.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            this.TabStop = false;
            this.BackColor = Theme.Background;
            this.ForeColor = Theme.Foreground;

            this.fileGlyph = new CreateBarGlyph(CreateBarGlyphKind.File);
            this.folderGlyph = new CreateBarGlyph(CreateBarGlyphKind.Folder);
            this.fileGlyph.Click += this.OnFileClick;
            this.folderGlyph.Click += this.OnFolderClick;
            this.Controls.Add(this.fileGlyph);
            this.Controls.Add(this.folderGlyph);

            this.toolTip = new ToolTip();
            this.toolTip.SetToolTip(this.fileGlyph, "ファイルを作成");
            this.toolTip.SetToolTip(this.folderGlyph, "フォルダを作成");
            this.ApplyBarHeight();
        }

        /// <summary>ファイル作成ボタン。</summary>
        public event EventHandler FileCreateRequested;

        /// <summary>フォルダ作成ボタン。</summary>
        public event EventHandler FolderCreateRequested;

        /// <summary>
        /// MainForm が呼ぶ互換入口。フォントは高さに使わない。バー高さと配置を付け直す。
        /// </summary>
        /// <param name="half">半角。未使用。</param>
        /// <param name="full">全角。未使用。</param>
        public void SetFonts(Font half, Font full)
        {
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

        /// <summary>左 6 DIP、間隔 4 DIP、ヒット 22 DIP 正方形で線画を並べる。</summary>
        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            int dpi = DpiUtil.GetDpi(this.IsHandleCreated ? this.Handle : IntPtr.Zero);
            int pad = DpiUtil.ToPixels(6, dpi);
            int gap = DpiUtil.ToPixels(4, dpi);
            int vpad = DpiUtil.ToPixels(4, dpi);
            int hit = DpiUtil.ToPixels(22, dpi);
            if (hit < 1)
            {
                hit = 1;
            }

            int x = pad;
            this.fileGlyph.Bounds = new Rectangle(x, vpad, hit, hit);
            x += hit + gap;
            this.folderGlyph.Bounds = new Rectangle(x, vpad, hit, hit);
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
            this.Height = DpiUtil.ToPixels(22, dpi) + DpiUtil.ToPixels(4, dpi) * 2;
            if (this.Parent != null)
            {
                this.Parent.PerformLayout();
            }
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
