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
        /// <param name="onDropDown">ドロップダウンなら true（左右は textRect のまま）。</param>
        /// <param name="rightAlign">右揃え。</param>
        /// <param name="center">中央揃え（rightAlign が優先）。</param>
        /// <returns>クリップ矩形。幅または高さが負なら Empty。</returns>
        public static Rectangle ItemTextClip(Rectangle item, Rectangle textRect, float measuredW, bool onDropDown, bool rightAlign, bool center)
        {
            Rectangle r = Rectangle.Intersect(textRect, item);
            r.Y = item.Y;
            r.Height = item.Height;
            int need = (int)Math.Ceiling((double)measuredW);

            if (onDropDown)
            {
                int x = item.Left;
                if (textRect.X > x)
                {
                    x = textRect.X;
                }

                int right = item.Right;
                if (textRect.Right < right)
                {
                    right = textRect.Right;
                }

                r.X = x;
                int width = right - x;
                if (width < 0)
                {
                    width = 0;
                }

                r.Width = width;
            }
            else if (rightAlign)
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
    }
}
