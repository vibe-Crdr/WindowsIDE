using System;
using System.Drawing;

namespace WindowsIDE.Ui
{
    /// <summary>
    /// メニューとステータスの DualFont セル高さと文字クリップ。WinForms は参照しない。
    /// </summary>
    public static class ChromeTextLayout
    {
        /// <summary>項目内容の上下インク余白（物理 px）。DIP 化しない。</summary>
        public const int InkPadPx = 2;

        /// <summary>未レイアウト時のドロップダウン左クローム（DIP）。</summary>
        public const int DropDownLeftChromeDip = 33;

        /// <summary>未レイアウト時のドロップダウン右クローム（DIP）。</summary>
        public const int DropDownRightChromeDip = 27;

        /// <summary>レイアウト済み右クロームを底の代わりに使う許容（px）。</summary>
        public const int DropDownRightChromeSlackPx = 8;

        /// <summary>ラベルとショートカットの隙間の底（DIP）。</summary>
        public const int DropDownLabelGapDip = 16;

        /// <summary>
        /// ドロップダウンのラベル列とショートカット列。幅は px。
        /// </summary>
        public struct MenuTextColumns
        {
            /// <summary>列分けに使った項目幅。未レイアウトなら合成幅。</summary>
            public int ItemWidth;

            /// <summary>ラベル列の左。</summary>
            public int LabelLeft;

            /// <summary>ラベル列の右。ショートカットがあるときはその左より小さい。</summary>
            public int LabelRight;

            /// <summary>ショートカット列の左。ショートカットが無ければ内容右端。</summary>
            public int ShortcutLeft;

            /// <summary>ショートカット列の右（共有右端）。</summary>
            public int ShortcutRight;
        }

        /// <summary>
        /// 半角・全角セルの大きい方。どちらも 1 未満なら 1。
        /// </summary>
        /// <param name="halfH">半角 Font.Height（px）。</param>
        /// <param name="fullH">全角 Font.Height（px）。</param>
        /// <returns>セル高さ（px）。</returns>
        public static int CellHeight(int halfH, int fullH)
        {
            int cell = halfH;
            if (fullH > cell)
            {
                cell = fullH;
            }

            if (cell < 1)
            {
                return 1;
            }

            return cell;
        }

        /// <summary>
        /// DualFont セルにインク余白を足した項目内容高さ。
        /// </summary>
        /// <param name="cell">セル高さ（px）。</param>
        /// <returns>内容高さ（px）。</returns>
        public static int ItemContentHeight(int cell)
        {
            return cell + InkPadPx;
        }

        /// <summary>
        /// ストリップ希望高さ。内容高さに上下 Padding（DIP→px）を足す。
        /// </summary>
        /// <param name="cell">セル高さ（px）。</param>
        /// <param name="padYDip">上下各辺の Padding（DIP）。</param>
        /// <param name="dpi">画面 DPI。</param>
        /// <returns>希望高さ（px）。</returns>
        public static int StripPreferredHeight(int cell, int padYDip, int dpi)
        {
            int pad = DpiUtil.ToPixels(padYDip, dpi);
            return ItemContentHeight(cell) + (2 * pad);
        }

        /// <summary>
        /// DualFont 描画クリップ。TextRectangle は左右の配置ヒント。高さは項目クライアント。
        /// </summary>
        /// <param name="item">項目クライアント矩形。</param>
        /// <param name="textRect">ToolStrip の TextRectangle（配置ヒント）。</param>
        /// <param name="measuredW">DualFont 測幅（px）。</param>
        /// <param name="rightAlign">右揃え。</param>
        /// <param name="center">中央揃え（rightAlign が優先）。</param>
        /// <returns>クリップ矩形。幅または高さが負なら Empty。</returns>
        public static Rectangle ItemTextClip(Rectangle item, Rectangle textRect, float measuredW, bool rightAlign, bool center)
        {
            Rectangle r = Rectangle.Intersect(textRect, item);
            r.Y = item.Y;
            r.Height = item.Height;
            int need = (int)Math.Ceiling((double)measuredW);

            if (rightAlign)
            {
                int right = textRect.Right;
                if (right > item.Right)
                {
                    right = item.Right;
                }

                int x = right - need;
                if (x < item.Left)
                {
                    x = item.Left;
                }

                r.X = x;
                r.Width = right - x;
            }
            else if (center)
            {
                int mid = r.X + (r.Width / 2);
                if (r.Width <= 0)
                {
                    mid = textRect.X + (textRect.Width / 2);
                }

                int left = mid - (need / 2);
                int right = left + need;
                if (left < item.Left)
                {
                    left = item.Left;
                }

                if (right > item.Right)
                {
                    right = item.Right;
                }

                r.X = left;
                r.Width = right - left;
            }
            else
            {
                int x = item.Left;
                if (textRect.X > x)
                {
                    x = textRect.X;
                }

                int right = x + need;
                if (right > item.Right)
                {
                    right = item.Right;
                }

                r.X = x;
                r.Width = right - x;
            }

            if (r.Width < 0 || r.Height < 0)
            {
                return Rectangle.Empty;
            }

            return r;
        }

        /// <summary>
        /// ドロップダウンのラベル列とショートカット列。幅が負なら 0。未レイアウト（項目幅 0 以下）はクローム底から幅を合成する。
        /// </summary>
        /// <param name="itemWidth">項目幅（px）。0 以下は未レイアウト。</param>
        /// <param name="textRectX">TextRectangle の X（px）。</param>
        /// <param name="textRectWidth">TextRectangle の幅（px）。</param>
        /// <param name="labelWidthPx">ニーモニック除去後のラベル幅（px）。</param>
        /// <param name="shortcutWidthPx">ショートカット幅（px）。0 なら列を分けない。</param>
        /// <param name="gapPx">ラベルとショートカットの隙間（px）。</param>
        /// <param name="leftFloorPx">未レイアウト時の左クローム（px）。</param>
        /// <param name="rightFloorPx">未レイアウト時の右クローム（px）。</param>
        /// <returns>ラベル列とショートカット列。</returns>
        public static MenuTextColumns LayoutDropDownText(int itemWidth, int textRectX, int textRectWidth, int labelWidthPx, int shortcutWidthPx, int gapPx, int leftFloorPx, int rightFloorPx)
        {
            textRectWidth = NonNegative(textRectWidth);
            labelWidthPx = NonNegative(labelWidthPx);
            shortcutWidthPx = NonNegative(shortcutWidthPx);
            gapPx = NonNegative(gapPx);
            leftFloorPx = NonNegative(leftFloorPx);
            rightFloorPx = NonNegative(rightFloorPx);
            if (itemWidth <= 0)
            {
                itemWidth = DropDownRowWidth(labelWidthPx, shortcutWidthPx, gapPx, leftFloorPx, rightFloorPx);
            }

            int leftChrome;
            int rightChrome;
            bool laidOut = itemWidth > 0 && textRectWidth > 0 && textRectX >= 0;
            if (laidOut)
            {
                leftChrome = textRectX;
                int rightCandidate = itemWidth - (textRectX + textRectWidth);
                if (rightCandidate < 0)
                {
                    rightCandidate = 0;
                }

                if (rightCandidate <= rightFloorPx + DropDownRightChromeSlackPx)
                {
                    rightChrome = rightCandidate;
                }
                else
                {
                    rightChrome = rightFloorPx;
                }
            }
            else
            {
                leftChrome = leftFloorPx;
                rightChrome = rightFloorPx;
            }

            int contentRight = itemWidth - rightChrome;
            if (contentRight < leftChrome)
            {
                contentRight = leftChrome;
            }

            MenuTextColumns cols = new MenuTextColumns();
            cols.ItemWidth = itemWidth;
            cols.LabelLeft = leftChrome;
            if (shortcutWidthPx > 0)
            {
                cols.ShortcutRight = contentRight;
                int shortcutLeft = contentRight - shortcutWidthPx;
                if (shortcutLeft < leftChrome)
                {
                    shortcutLeft = leftChrome;
                }

                cols.ShortcutLeft = shortcutLeft;
                int gapUse = gapPx;
                if (gapUse < 1)
                {
                    gapUse = 1;
                }

                int labelRight = shortcutLeft - gapUse;
                if (labelRight < leftChrome)
                {
                    labelRight = leftChrome;
                }

                cols.LabelRight = labelRight;
            }
            else
            {
                cols.ShortcutLeft = contentRight;
                cols.ShortcutRight = contentRight;
                cols.LabelRight = contentRight;
            }

            return cols;
        }

        /// <summary>
        /// ドロップダウン 1 行の幅。ショートカットが無ければギャップを足さない。
        /// </summary>
        /// <param name="labelWidthPx">ラベル幅（px）。負なら 0。</param>
        /// <param name="shortcutWidthPx">ショートカット幅（px）。0 以下ならギャップも足さない。</param>
        /// <param name="gapPx">隙間（px）。負なら 0。</param>
        /// <param name="leftChromePx">左クローム（px）。</param>
        /// <param name="rightChromePx">右クローム（px）。</param>
        /// <returns>行幅（px）。</returns>
        public static int DropDownRowWidth(int labelWidthPx, int shortcutWidthPx, int gapPx, int leftChromePx, int rightChromePx)
        {
            int width = NonNegative(leftChromePx) + NonNegative(labelWidthPx) + NonNegative(rightChromePx);
            if (shortcutWidthPx > 0)
            {
                width += NonNegative(gapPx) + shortcutWidthPx;
            }

            return width;
        }

        private static int NonNegative(int value)
        {
            if (value < 0)
            {
                return 0;
            }

            return value;
        }
    }
}
