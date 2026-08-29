using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WindowsIDE.Ui
{
    /// <summary>
    /// 作成バー線画の種類。
    /// </summary>
    public enum CreateBarGlyphKind
    {
        /// <summary>紙とドッグイヤーと右下のプラス。</summary>
        File,

        /// <summary>左上タブのフォルダと右下のプラス。</summary>
        Folder
    }

    /// <summary>
    /// 作成バーの線画アイコン。紙またはフォルダと右下のプラス。枠なし、塗りなし。
    /// </summary>
    public sealed class CreateBarGlyph : Control
    {
        private readonly CreateBarGlyphKind kind;
        private bool hot;

        /// <summary>
        /// 指定種類の線画ボタンを作る。
        /// </summary>
        /// <param name="kind">File は紙、Folder はフォルダ。</param>
        public CreateBarGlyph(CreateBarGlyphKind kind)
        {
            this.kind = kind;
            this.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            this.SetStyle(ControlStyles.StandardClick, false);
            this.TabStop = false;
            this.BackColor = Theme.Background;
            if (kind == CreateBarGlyphKind.File)
            {
                this.AccessibleName = "ファイルを作成";
            }
            else
            {
                this.AccessibleName = "フォルダを作成";
            }
        }

        /// <summary>ホバーを付ける。</summary>
        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            this.hot = true;
            this.Invalidate();
        }

        /// <summary>ホバーを外す。</summary>
        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            this.hot = false;
            this.Invalidate();
        }

        /// <summary>左ボタン押下で Click。StandardClick は切ってあるので MouseUp では上げない。</summary>
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            this.OnClick(EventArgs.Empty);
        }

        /// <summary>ホバーは CurrentLine。線は Foreground。枠と塗りはなし。斜線のため OnPaint 中だけ AntiAlias。</summary>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (e == null || e.Graphics == null)
            {
                return;
            }

            Graphics g = e.Graphics;
            Color bg = Theme.Background;
            if (this.hot)
            {
                bg = Theme.CurrentLine;
            }

            using (SolidBrush fill = new SolidBrush(bg))
            {
                g.FillRectangle(fill, this.ClientRectangle);
            }

            SmoothingMode previous = g.SmoothingMode;
            try
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                int dpi = DpiUtil.GetDpi(this.IsHandleCreated ? this.Handle : IntPtr.Zero);
                float width = (float)dpi / 96f;
                using (Pen pen = new Pen(Theme.Foreground, width))
                {
                    pen.LineJoin = LineJoin.Miter;
                    pen.StartCap = LineCap.Flat;
                    pen.EndCap = LineCap.Flat;
                    float originX = DipPx(3f, dpi);
                    float originY = DipPx(3f, dpi);
                    if (this.kind == CreateBarGlyphKind.File)
                    {
                        DrawFile(g, pen, originX, originY, dpi);
                    }
                    else
                    {
                        DrawFolder(g, pen, originX, originY, dpi);
                    }
                }
            }
            finally
            {
                g.SmoothingMode = previous;
            }
        }

        private static void DrawFile(Graphics g, Pen pen, float originX, float originY, int dpi)
        {
            using (GraphicsPath outline = new GraphicsPath())
            {
                outline.AddLines(new PointF[]
                {
                    Pt(3f, 2f, originX, originY, dpi),
                    Pt(9.2f, 2f, originX, originY, dpi),
                    Pt(12f, 4.8f, originX, originY, dpi),
                    Pt(12f, 13f, originX, originY, dpi),
                    Pt(3f, 13f, originX, originY, dpi)
                });
                outline.CloseFigure();
                g.DrawPath(pen, outline);
            }

            using (GraphicsPath fold = new GraphicsPath())
            {
                fold.AddLines(new PointF[]
                {
                    Pt(9.2f, 2f, originX, originY, dpi),
                    Pt(9.2f, 4.8f, originX, originY, dpi),
                    Pt(12f, 4.8f, originX, originY, dpi)
                });
                g.DrawPath(pen, fold);
            }

            g.DrawLine(pen, Pt(10.5f, 12.2f, originX, originY, dpi), Pt(14.5f, 12.2f, originX, originY, dpi));
            g.DrawLine(pen, Pt(12.5f, 10.2f, originX, originY, dpi), Pt(12.5f, 14.2f, originX, originY, dpi));
        }

        private static void DrawFolder(Graphics g, Pen pen, float originX, float originY, int dpi)
        {
            using (GraphicsPath outline = new GraphicsPath())
            {
                outline.AddLines(new PointF[]
                {
                    Pt(2f, 4.5f, originX, originY, dpi),
                    Pt(2f, 3.2f, originX, originY, dpi),
                    Pt(6.4f, 3.2f, originX, originY, dpi),
                    Pt(7.3f, 4.5f, originX, originY, dpi),
                    Pt(14f, 4.5f, originX, originY, dpi),
                    Pt(14f, 13f, originX, originY, dpi),
                    Pt(2f, 13f, originX, originY, dpi)
                });
                outline.CloseFigure();
                g.DrawPath(pen, outline);
            }

            g.DrawLine(pen, Pt(10.5f, 12f, originX, originY, dpi), Pt(14.5f, 12f, originX, originY, dpi));
            g.DrawLine(pen, Pt(12.5f, 10f, originX, originY, dpi), Pt(12.5f, 14f, originX, originY, dpi));
        }

        private static float DipPx(float dip, int dpi)
        {
            return dip * (float)dpi / 96f;
        }

        private static PointF Pt(float xDip, float yDip, float originX, float originY, int dpi)
        {
            return new PointF(originX + DipPx(xDip, dpi), originY + DipPx(yDip, dpi));
        }
    }
}
