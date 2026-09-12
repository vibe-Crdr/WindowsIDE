using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using WindowsIDE.Ui.Fonts;

namespace WindowsIDE.Ui
{
    /// <summary>
    /// メニューとステータスのダーク描画。テキストは DualFontPainter。
    /// </summary>
    public sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        private Font halfFont;
        private Font fullFont;

        /// <summary>
        /// ダーク用カラーテーブルで作る。
        /// </summary>
        public DarkMenuRenderer()
            : base(new DarkColorTable())
        {
            this.RoundedEdges = false;
        }

        /// <summary>
        /// 半角・全角フォントを描画と測幅に使う。所有権は移さない（Dispose しない）。
        /// </summary>
        /// <param name="half">半角。null なら既定描画に戻す。</param>
        /// <param name="full">全角。</param>
        public void SetFonts(Font half, Font full)
        {
            this.halfFont = half;
            this.fullFont = full;
        }

        internal Font HalfFont
        {
            get { return this.halfFont; }
        }

        internal Font FullFont
        {
            get { return this.fullFont; }
        }

        /// <summary>
        /// メニュー枠の立体ボーダーを出さない。
        /// </summary>
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
        }

        /// <summary>
        /// ニーモニックを除いた文字列を DualFontPainter で描く。
        /// </summary>
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            if (e == null || e.Graphics == null || this.halfFont == null || this.fullFont == null)
            {
                base.OnRenderItemText(e);
                return;
            }

            TextFormatFlags flags = e.TextFormat;
            string visible;
            int mnemonicIndex;
            if ((flags & TextFormatFlags.NoPrefix) != 0)
            {
                visible = e.Text;
                mnemonicIndex = -1;
            }
            else
            {
                visible = UiMnemonic.Strip(e.Text, out mnemonicIndex);
            }

            if ((flags & TextFormatFlags.HidePrefix) != 0)
            {
                mnemonicIndex = -1;
            }

            Rectangle rect = e.TextRectangle;
            float textWidth = DualFontPainter.Measure(e.Graphics, visible, this.halfFont, this.fullFont, null);
            float originX = rect.X;
            if ((flags & TextFormatFlags.Right) != 0)
            {
                originX = rect.Right - textWidth;
            }
            else if ((flags & TextFormatFlags.HorizontalCenter) != 0)
            {
                originX = rect.X + (rect.Width - textWidth) / 2f;
            }

            Rectangle clip = rect;
            if (e.Item != null)
            {
                Rectangle itemClient = new Rectangle(0, 0, e.Item.Width, e.Item.Height);
                bool onDropDown = e.Item.IsOnDropDown;
                bool rightAlign = (flags & TextFormatFlags.Right) != 0;
                bool center = (flags & TextFormatFlags.HorizontalCenter) != 0;
                clip = ChromeTextLayout.ItemTextClip(itemClient, rect, textWidth, onDropDown, rightAlign, center);
            }

            using (SolidBrush brush = new SolidBrush(e.TextColor))
            {
                DualFontPainter.Draw(e.Graphics, visible, this.halfFont, this.fullFont, clip, originX, brush, null);
                if (mnemonicIndex >= 0 && visible != null && mnemonicIndex < visible.Length)
                {
                    DrawMnemonicUnderline(e.Graphics, visible, mnemonicIndex, clip, originX, brush);
                }
            }
        }

        private void DrawMnemonicUnderline(Graphics g, string visible, int mnemonicIndex, Rectangle clip, float originX, Brush brush)
        {
            int glyphLen = 1;
            if (mnemonicIndex + 1 < visible.Length
                && char.IsHighSurrogate(visible[mnemonicIndex])
                && char.IsLowSurrogate(visible[mnemonicIndex + 1]))
            {
                glyphLen = 2;
            }

            string prefix = visible.Substring(0, mnemonicIndex);
            string glyph = visible.Substring(mnemonicIndex, glyphLen);
            float prefixW = DualFontPainter.Measure(g, prefix, this.halfFont, this.fullFont, null);
            float glyphW = DualFontPainter.Measure(g, glyph, this.halfFont, this.fullFont, null);
            if (glyphW <= 0f)
            {
                return;
            }

            float cell = this.halfFont.Height;
            if (this.fullFont.Height > cell)
            {
                cell = this.fullFont.Height;
            }

            float y = clip.Y + (clip.Height - cell) / 2f;
            int uy = (int)(y + cell) - 1;
            if (uy < clip.Top)
            {
                uy = clip.Top;
            }

            if (uy >= clip.Bottom || clip.Width <= 0 || clip.Height <= 0)
            {
                return;
            }

            GraphicsState state = g.Save();
            try
            {
                g.SetClip(clip);
                g.FillRectangle(brush, originX + prefixW, uy, glyphW, 1f);
            }
            finally
            {
                g.Restore(state);
            }
        }

        internal static Graphics CreateMeasureGraphics(ToolStrip owner, out Bitmap bitmap)
        {
            bitmap = null;
            Graphics g;
            if (owner != null && owner.IsHandleCreated)
            {
                g = owner.CreateGraphics();
            }
            else
            {
                bitmap = new Bitmap(1, 1);
                g = Graphics.FromImage(bitmap);
            }

            g.PageUnit = GraphicsUnit.Pixel;
            g.PageScale = 1f;
            return g;
        }

        /// <summary>
        /// 描画と同じショートカット表示。ShortcutKeyDisplayString が空なら KeysConverter。
        /// </summary>
        /// <param name="item">対象。null や非表示なら null。</param>
        /// <returns>測る文字列。足さないときは null。</returns>
        internal static string GetShortcutDisplayText(ToolStripMenuItem item)
        {
            if (item == null || !item.ShowShortcutKeys)
            {
                return null;
            }

            string text = item.ShortcutKeyDisplayString;
            if (text != null && text.Length > 0)
            {
                return text;
            }

            if (item.ShortcutKeys == Keys.None)
            {
                return null;
            }

            TypeConverter converter = TypeDescriptor.GetConverter(typeof(Keys));
            if (converter == null)
            {
                return null;
            }

            return converter.ConvertToString(item.ShortcutKeys);
        }

        private sealed class DarkColorTable : ProfessionalColorTable
        {
            public override Color MenuStripGradientBegin { get { return Theme.Background; } }
            public override Color MenuStripGradientEnd { get { return Theme.Background; } }
            public override Color MenuItemSelected { get { return Theme.Selection; } }
            public override Color MenuItemSelectedGradientBegin { get { return Theme.Selection; } }
            public override Color MenuItemSelectedGradientEnd { get { return Theme.Selection; } }
            public override Color MenuItemPressedGradientBegin { get { return Theme.CurrentLine; } }
            public override Color MenuItemPressedGradientEnd { get { return Theme.CurrentLine; } }
            public override Color MenuItemBorder { get { return Theme.Border; } }
            public override Color MenuBorder { get { return Theme.Border; } }
            public override Color ToolStripDropDownBackground { get { return Theme.Background; } }
            public override Color ImageMarginGradientBegin { get { return Theme.Background; } }
            public override Color ImageMarginGradientMiddle { get { return Theme.Background; } }
            public override Color ImageMarginGradientEnd { get { return Theme.Background; } }
            public override Color SeparatorDark { get { return Theme.Border; } }
            public override Color SeparatorLight { get { return Theme.Border; } }
            public override Color ToolStripBorder { get { return Theme.Border; } }
            public override Color StatusStripGradientBegin { get { return Theme.StatusBar; } }
            public override Color StatusStripGradientEnd { get { return Theme.StatusBar; } }
        }
    }

    /// <summary>
    /// メニュー項目。測幅を DualFontPainter（ニーモニック除去後）に合わせる。
    /// </summary>
    public sealed class DualFontMenuItem : ToolStripMenuItem
    {
        /// <summary>
        /// 表示テキストで作る。Text の <c>&amp;</c> は残す。
        /// </summary>
        /// <param name="text">項目テキスト。</param>
        public DualFontMenuItem(string text)
            : base(text)
        {
        }

        /// <summary>
        /// DualFont の希望サイズ。トップはラベル幅。ドロップダウンは WinForms 希望幅と兄弟 DualFont 行の大きい方。
        /// </summary>
        /// <param name="constrainingSize">制約サイズ。</param>
        /// <returns>希望サイズ。</returns>
        public override Size GetPreferredSize(Size constrainingSize)
        {
            DarkMenuRenderer renderer = null;
            if (this.Owner != null)
            {
                renderer = this.Owner.Renderer as DarkMenuRenderer;
            }

            Font half = (renderer == null) ? null : renderer.HalfFont;
            Font full = (renderer == null) ? null : renderer.FullFont;
            if (half == null || full == null)
            {
                return base.GetPreferredSize(constrainingSize);
            }

            if (!this.IsOnDropDown)
            {
                return GetTopLevelPreferredSize(half, full);
            }

            Size native = base.GetPreferredSize(constrainingSize);
            int cell = ChromeTextLayout.CellHeight(half.Height, full.Height);
            int height = native.Height;
            int minHeight = ChromeTextLayout.ItemContentHeight(cell) + this.Padding.Vertical;
            if (height < minHeight)
            {
                height = minHeight;
            }

            int width = native.Width;
            Bitmap bmp = null;
            Graphics g = DarkMenuRenderer.CreateMeasureGraphics(this.Owner, out bmp);
            try
            {
                int dpi = DpiUtil.GetDpi(IntPtr.Zero);
                if (this.Owner != null && this.Owner.IsHandleCreated)
                {
                    dpi = DpiUtil.GetDpi(this.Owner.Handle);
                }

                int gap = DpiUtil.ToPixels(16, dpi);
                if (this.Font != null)
                {
                    int tabW = TextRenderer.MeasureText("\t", this.Font).Width;
                    if (tabW > gap)
                    {
                        gap = tabW;
                    }
                }

                if (this.Owner != null)
                {
                    foreach (ToolStripItem item in this.Owner.Items)
                    {
                        if (item is ToolStripSeparator)
                        {
                            continue;
                        }

                        DualFontMenuItem dual = item as DualFontMenuItem;
                        if (dual == null)
                        {
                            continue;
                        }

                        int rowW = MeasureDropDownRowWidth(dual, g, half, full, dpi, gap);
                        if (rowW > width)
                        {
                            width = rowW;
                        }
                    }
                }
                else
                {
                    int rowW = MeasureDropDownRowWidth(this, g, half, full, dpi, gap);
                    if (rowW > width)
                    {
                        width = rowW;
                    }
                }
            }
            finally
            {
                g.Dispose();
                if (bmp != null)
                {
                    bmp.Dispose();
                }
            }

            if (width < 1)
            {
                width = 1;
            }

            if (height < 1)
            {
                height = 1;
            }

            return new Size(width, height);
        }

        /// <summary>
        /// トップレベル項目の DualFont ラベル幅（兄弟 max しない）。
        /// </summary>
        /// <param name="half">半角フォント。</param>
        /// <param name="full">全角フォント。</param>
        /// <returns>希望サイズ。</returns>
        private Size GetTopLevelPreferredSize(Font half, Font full)
        {
            int dummy;
            string visible = UiMnemonic.Strip(this.Text, out dummy);
            Bitmap bmp = null;
            Graphics g = DarkMenuRenderer.CreateMeasureGraphics(this.Owner, out bmp);
            try
            {
                float textW = DualFontPainter.Measure(g, visible, half, full, null);
                int cell = ChromeTextLayout.CellHeight(half.Height, full.Height);
                int width = (int)Math.Ceiling((double)textW) + this.Padding.Horizontal;
                int height = ChromeTextLayout.ItemContentHeight(cell) + this.Padding.Vertical;
                if (width < 1)
                {
                    width = 1;
                }

                if (height < 1)
                {
                    height = 1;
                }

                return new Size(width, height);
            }
            finally
            {
                g.Dispose();
                if (bmp != null)
                {
                    bmp.Dispose();
                }
            }
        }

        /// <summary>
        /// ドロップダウン1行の DualFont+chrome 幅。GetPreferredSize は呼ばない（兄弟走査の再帰禁止）。
        /// </summary>
        /// <param name="item">測る DualFont 項目。</param>
        /// <param name="g">計測 Graphics。</param>
        /// <param name="half">半角フォント。</param>
        /// <param name="full">全角フォント。</param>
        /// <param name="dpi">物理換算 DPI。</param>
        /// <param name="gap">ラベルとショートカットの隙間（px）。</param>
        /// <returns>その行の幅（px）。</returns>
        private static int MeasureDropDownRowWidth(DualFontMenuItem item, Graphics g, Font half, Font full, int dpi, int gap)
        {
            if (item == null || g == null)
            {
                return 0;
            }

            int dummy;
            string visible = UiMnemonic.Strip(item.Text, out dummy);
            float labelW = DualFontPainter.Measure(g, visible, half, full, null);
            int width = DpiUtil.ToPixels(24, dpi) + (int)Math.Ceiling((double)labelW) + gap;
            string shortcut = DarkMenuRenderer.GetShortcutDisplayText(item);
            if (shortcut != null && shortcut.Length > 0)
            {
                float shortcutW = DualFontPainter.Measure(g, shortcut, half, full, null);
                width += (int)Math.Ceiling((double)shortcutW);
            }

            width += DpiUtil.ToPixels(10, dpi) + DpiUtil.ToPixels(8, dpi);
            return width;
        }
    }

    /// <summary>
    /// ステータスラベル。測幅を DualFontPainter に合わせる。
    /// </summary>
    public sealed class DualFontStatusLabel : ToolStripStatusLabel
    {
        /// <summary>
        /// 表示テキストで作る。
        /// </summary>
        /// <param name="text">ラベルテキスト。</param>
        public DualFontStatusLabel(string text)
            : base(text)
        {
        }

        /// <summary>
        /// 項目の既定 Margin は空。余白は StatusStrip の Padding。
        /// </summary>
        protected override Padding DefaultMargin
        {
            get { return Padding.Empty; }
        }

        /// <summary>
        /// DualFontPainter で測った幅を返す。フォントが無ければ既定。
        /// </summary>
        /// <param name="constrainingSize">制約サイズ。</param>
        /// <returns>希望サイズ。</returns>
        public override Size GetPreferredSize(Size constrainingSize)
        {
            DarkMenuRenderer renderer = null;
            if (this.Owner != null)
            {
                renderer = this.Owner.Renderer as DarkMenuRenderer;
            }

            Font half = (renderer == null) ? null : renderer.HalfFont;
            Font full = (renderer == null) ? null : renderer.FullFont;
            if (half == null || full == null)
            {
                return base.GetPreferredSize(constrainingSize);
            }

            int dummy;
            string visible = UiMnemonic.Strip(this.Text, out dummy);
            Bitmap bmp = null;
            Graphics g = DarkMenuRenderer.CreateMeasureGraphics(this.Owner, out bmp);
            try
            {
                float textW = DualFontPainter.Measure(g, visible, half, full, null);
                int cell = ChromeTextLayout.CellHeight(half.Height, full.Height);
                int width = (int)Math.Ceiling((double)textW) + this.Padding.Horizontal;
                int height = ChromeTextLayout.ItemContentHeight(cell) + this.Padding.Vertical;
                if (width < 1)
                {
                    width = 1;
                }

                if (height < 1)
                {
                    height = 1;
                }

                return new Size(width, height);
            }
            finally
            {
                g.Dispose();
                if (bmp != null)
                {
                    bmp.Dispose();
                }
            }
        }
    }
}
