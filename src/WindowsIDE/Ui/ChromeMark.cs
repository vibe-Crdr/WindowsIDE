using System;
using System.Drawing;
using System.Windows.Forms;
using WindowsIDE.Ui.Fonts;

namespace WindowsIDE.Ui
{
    /// <summary>
    /// FindBar と作成バーのラベル／ボタン。DualFontPainter で中央描画する。
    /// </summary>
    public sealed class ChromeMark : Control
    {
        private readonly bool clickable;
        private readonly bool toggle;
        private bool toggled;
        private bool hot;
        private string caption;
        private Font halfFont;
        private Font fullFont;

        /// <summary>
        /// キャプション付きのクロム部品を作る。
        /// </summary>
        /// <param name="caption">表示文字列。null は空。</param>
        /// <param name="clickable">true なら枠とホバー、MouseDown で Click。</param>
        /// <param name="toggle">true なら押下で Selection 背景を切り替える。</param>
        public ChromeMark(string caption, bool clickable, bool toggle)
        {
            this.caption = (caption == null) ? "" : caption;
            this.clickable = clickable;
            this.toggle = toggle;
            this.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            // MouseDown で OnClick する（押した瞬間に反応する既存 UX）。
            // StandardClick を切らないと MouseUp で Click がもう一度上がり、次／前が 2 ヒット飛ぶ。
            this.SetStyle(ControlStyles.StandardClick, false);
            this.TabStop = false;
            this.BackColor = Theme.Background;
        }

        /// <summary>表示文字列。null は空。</summary>
        public string Caption
        {
            get { return this.caption; }
            set
            {
                this.caption = (value == null) ? "" : value;
                this.Invalidate();
            }
        }

        /// <summary>トグルがオンなら true。</summary>
        public bool Toggled
        {
            get { return this.toggled; }
        }

        /// <summary>
        /// 半角・全角フォントを参照する。所有権は移さない。
        /// </summary>
        /// <param name="half">半角。</param>
        /// <param name="full">全角。</param>
        public void SetFonts(Font half, Font full)
        {
            this.halfFont = half;
            this.fullFont = full;
            this.Invalidate();
        }

        /// <summary>clickable のときホバーを付ける。</summary>
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

        /// <summary>トグルは Selection、ホバーは CurrentLine、clickable なら Border 枠。文字は DualFont 中央。</summary>
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
                clip.Inflate(-2, -1);
                string text = this.caption;
                if (text == null)
                {
                    text = "";
                }

                float width = DualFontPainter.Measure(e.Graphics, text, this.halfFont, this.fullFont, null);
                if (width > clip.Width && clip.Width > 0)
                {
                    text = DualFontPainter.FitEllipsis(e.Graphics, text, this.halfFont, this.fullFont, clip.Width, null);
                    width = DualFontPainter.Measure(e.Graphics, text, this.halfFont, this.fullFont, null);
                }

                float originX = clip.X + (clip.Width - width) / 2f;
                DualFontPainter.Draw(e.Graphics, text, this.halfFont, this.fullFont, clip, originX, fg, null);
            }
        }
    }
}
