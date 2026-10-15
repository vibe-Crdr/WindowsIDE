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
            bool rightAlign = (flags & TextFormatFlags.Right) != 0;
            bool center = (flags & TextFormatFlags.HorizontalCenter) != 0;
            bool onDropDown = e.Item != null && e.Item.IsOnDropDown;
            float originX;
            Rectangle clip;
            if (onDropDown)
            {
                clip = this.DropDownColumnClip(e.Item, e.Graphics, rect, rightAlign, textWidth, out originX);
            }
            else
            {
                originX = rect.X;
                if (rightAlign)
                {
                    originX = rect.Right - textWidth;
                }
                else if (center)
                {
                    originX = rect.X + (rect.Width - textWidth) / 2f;
                }

                clip = rect;
                if (e.Item != null)
                {
                    Rectangle itemClient = new Rectangle(0, 0, e.Item.Width, e.Item.Height);
                    clip = ChromeTextLayout.ItemTextClip(itemClient, rect, textWidth, rightAlign, center);
                }
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

        /// <summary>
        /// ラベルとショートカットの隙間。16 DIP とタブ幅の大きい方。
        /// </summary>
        /// <param name="item">フォントを取る項目。null や Font 無しなら DIP だけ。</param>
        /// <param name="dpi">画面 DPI。</param>
        /// <returns>隙間（px）。</returns>
        internal static int ShortcutGapPx(ToolStripItem item, int dpi)
        {
            int gap = DpiUtil.ToPixels(ChromeTextLayout.DropDownLabelGapDip, dpi);
            if (item != null && item.Font != null)
            {
                int tabW = TextRenderer.MeasureText("\t", item.Font).Width;
                if (tabW > gap)
                {
                    gap = tabW;
                }
            }

            return gap;
        }

        /// <summary>
        /// ドロップダウンのラベル列またはショートカット列。高さは項目クライアント。
        /// </summary>
        /// <param name="item">描く項目。</param>
        /// <param name="g">測幅に使う Graphics。</param>
        /// <param name="textRect">TextRectangle（配置ヒント）。</param>
        /// <param name="rightAlign">ショートカット回なら true。</param>
        /// <param name="passWidth">この回の文字列幅（px）。</param>
        /// <param name="originX">描画開始 X。</param>
        /// <returns>その列だけのクリップ。</returns>
        private Rectangle DropDownColumnClip(ToolStripItem item, Graphics g, Rectangle textRect, bool rightAlign, float passWidth, out float originX)
        {
            int dpi = DpiUtil.GetDpi(IntPtr.Zero);
            if (item.Owner != null && item.Owner.IsHandleCreated)
            {
                dpi = DpiUtil.GetDpi(item.Owner.Handle);
            }

            int dummy;
            string label = UiMnemonic.Strip(item.Text, out dummy);
            float labelW = DualFontPainter.Measure(g, label, this.halfFont, this.fullFont, null);
            int shortcutPx = 0;
            ToolStripMenuItem menuItem = item as ToolStripMenuItem;
            if (menuItem != null)
            {
                string shortcut = GetShortcutDisplayText(menuItem);
                if (shortcut != null && shortcut.Length > 0)
                {
                    float shortcutW = DualFontPainter.Measure(g, shortcut, this.halfFont, this.fullFont, null);
                    shortcutPx = (int)Math.Ceiling((double)shortcutW);
                }
            }

            int gap = ShortcutGapPx(item, dpi);
            int leftFloor = DpiUtil.ToPixels(ChromeTextLayout.DropDownLeftChromeDip, dpi);
            int rightFloor = DpiUtil.ToPixels(ChromeTextLayout.DropDownRightChromeDip, dpi);
            ChromeTextLayout.MenuTextColumns cols = ChromeTextLayout.LayoutDropDownText(
                item.Width,
                textRect.X,
                textRect.Width,
                (int)Math.Ceiling((double)labelW),
                shortcutPx,
                gap,
                leftFloor,
                rightFloor);
            int height = item.Height;
            if (height < 0)
            {
                height = 0;
            }

            if (rightAlign)
            {
                int width = cols.ShortcutRight - cols.ShortcutLeft;
                if (width < 0)
                {
                    width = 0;
                }

                originX = cols.ShortcutRight - passWidth;
                return new Rectangle(cols.ShortcutLeft, 0, width, height);
            }

            int labelCol = cols.LabelRight - cols.LabelLeft;
            if (labelCol < 0)
            {
                labelCol = 0;
            }

            originX = cols.LabelLeft;
            return new Rectangle(cols.LabelLeft, 0, labelCol, height);
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
        /// 見える幅を合わせるドロップダウンを返す。自動生成フラグは DropDown getter が付ける。
        /// </summary>
        /// <returns>OwnerItem 付きの空の DualFontDropDownMenu。</returns>
        protected override ToolStripDropDown CreateDefaultDropDown()
        {
            DualFontDropDownMenu menu = new DualFontDropDownMenu();
            // 公開コンストラクタは所有者を付けない。未設定のまま自動生成だと親の Font 変更で落ちる。
            menu.OwnerItem = this;
            return menu;
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

                        int rowW = MeasureDropDownRowWidth(dual, g, half, full, dpi);
                        if (rowW > width)
                        {
                            width = rowW;
                        }
                    }
                }
                else
                {
                    int rowW = MeasureDropDownRowWidth(this, g, half, full, dpi);
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
        /// ドロップダウン1行の DualFont と左右クローム幅。GetPreferredSize と MaxItemSize は呼ばない。
        /// </summary>
        /// <param name="item">測る DualFont 項目。</param>
        /// <param name="g">計測 Graphics。</param>
        /// <param name="half">半角フォント。</param>
        /// <param name="full">全角フォント。</param>
        /// <param name="dpi">物理換算 DPI。</param>
        /// <returns>その行の幅（px）。</returns>
        internal static int MeasureDropDownRowWidth(DualFontMenuItem item, Graphics g, Font half, Font full, int dpi)
        {
            if (item == null || g == null || half == null || full == null)
            {
                return 0;
            }

            int dummy;
            string visible = UiMnemonic.Strip(item.Text, out dummy);
            float labelW = DualFontPainter.Measure(g, visible, half, full, null);
            int labelPx = (int)Math.Ceiling((double)labelW);
            int shortcutPx = 0;
            string shortcut = DarkMenuRenderer.GetShortcutDisplayText(item);
            if (shortcut != null && shortcut.Length > 0)
            {
                float shortcutW = DualFontPainter.Measure(g, shortcut, half, full, null);
                shortcutPx = (int)Math.Ceiling((double)shortcutW);
            }

            int gap = DarkMenuRenderer.ShortcutGapPx(item, dpi);
            int left = DpiUtil.ToPixels(ChromeTextLayout.DropDownLeftChromeDip, dpi);
            int right = DpiUtil.ToPixels(ChromeTextLayout.DropDownRightChromeDip, dpi);
            return ChromeTextLayout.DropDownRowWidth(labelPx, shortcutPx, gap, left, right);
        }
    }

    /// <summary>
    /// メニュー項目のドロップダウン。見える幅を兄弟 DualFont 行に合わせる。
    /// </summary>
    public sealed class DualFontDropDownMenu : ToolStripDropDownMenu
    {
        private int widthPad;
        private int layoutDepth;

        /// <summary>
        /// 空のドロップダウンを作る。自動生成フラグは DropDown getter が付ける。
        /// </summary>
        public DualFontDropDownMenu()
        {
        }

        /// <summary>
        /// 見える幅。高さは base。幅は base と兄弟 DualFont 行の大きい方に不足分を足す。フォントが無ければ base。
        /// </summary>
        protected override Size MaxItemSize
        {
            get
            {
                Size native = base.MaxItemSize;
                Font half;
                Font full;
                if (!this.TryRendererFonts(out half, out full))
                {
                    return native;
                }

                int row = this.WidestDualFontRowPx(half, full);
                int width = native.Width;
                if (row > width)
                {
                    width = row;
                }

                width += this.widthPad;
                return new Size(width, native.Height);
            }
        }

        /// <summary>
        /// フォント変更で幅の不足分を捨ててから base に渡す。
        /// </summary>
        /// <param name="e">イベント引数。</param>
        protected override void OnFontChanged(EventArgs e)
        {
            this.widthPad = 0;
            base.OnFontChanged(e);
        }

        /// <summary>
        /// base のあと、窓が行幅より狭いときだけ不足分を足して PerformLayout を最大 2 回までやり直す。
        /// </summary>
        /// <param name="e">レイアウト引数。</param>
        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (this.layoutDepth >= 2)
            {
                return;
            }

            Font half;
            Font full;
            if (!this.TryRendererFonts(out half, out full))
            {
                return;
            }

            int row = this.WidestDualFontRowPx(half, full);
            if (this.Width > 0 && this.Width < row)
            {
                this.widthPad += row - this.Width;
                this.layoutDepth++;
                try
                {
                    this.PerformLayout();
                }
                finally
                {
                    this.layoutDepth--;
                }
            }
        }

        private bool TryRendererFonts(out Font half, out Font full)
        {
            half = null;
            full = null;
            DarkMenuRenderer renderer = this.Renderer as DarkMenuRenderer;
            if (renderer == null)
            {
                return false;
            }

            half = renderer.HalfFont;
            full = renderer.FullFont;
            return half != null && full != null;
        }

        /// <summary>
        /// 兄弟 DualFont 行の最大幅。GetPreferredSize と MaxItemSize は呼ばない。
        /// </summary>
        private int WidestDualFontRowPx(Font half, Font full)
        {
            int dpi = DpiUtil.GetDpi(IntPtr.Zero);
            if (this.IsHandleCreated)
            {
                dpi = DpiUtil.GetDpi(this.Handle);
            }

            Bitmap bmp = null;
            Graphics g = DarkMenuRenderer.CreateMeasureGraphics(this, out bmp);
            try
            {
                int width = 0;
                foreach (ToolStripItem item in this.Items)
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

                    int rowW = DualFontMenuItem.MeasureDropDownRowWidth(dual, g, half, full, dpi);
                    if (rowW > width)
                    {
                        width = rowW;
                    }
                }

                return width;
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
