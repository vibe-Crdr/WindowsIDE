using System;

namespace WindowsIDE.Ui
{
    /// <summary>
    /// タブバーの幅と横オフセット計算。WinForms は参照しない。
    /// </summary>
    public static class TabStripLayout
    {
        /// <summary>先頭タブの左端（物理 px）。DIP 化しない。</summary>
        public const int StartX = 4;

        /// <summary>タブ間ギャップ（物理 px）。DIP 化しない。</summary>
        public const int TabGap = 2;

        /// <summary>ホイール 1 ノッチあたりの DIP。換算は DpiUtil.ToPixels。</summary>
        public const int WheelStepDip = 32;

        /// <summary>
        /// タブ幅を決める。textWidth+pad+closeSlot と minW の大きい方。
        /// </summary>
        /// <param name="textWidth">題名幅（px）。</param>
        /// <param name="pad">題名左余白（px）。</param>
        /// <param name="closeSlot">閉じる印とその左右余白（px）。</param>
        /// <param name="minW">下限幅（px）。</param>
        /// <returns>タブ幅（px）。</returns>
        public static int TabWidth(int textWidth, int pad, int closeSlot, int minW)
        {
            int w = textWidth + pad + closeSlot;
            if (w < minW)
            {
                return minW;
            }

            return w;
        }

        /// <summary>
        /// 全タブのレイアウト幅。末尾ギャップは含めない。空または null は 0。
        /// </summary>
        /// <param name="widths">各タブ幅。null 可。</param>
        /// <param name="startX">先頭左端。</param>
        /// <param name="gap">タブ間ギャップ。</param>
        /// <returns>内容幅（px）。</returns>
        public static int ContentWidth(int[] widths, int startX, int gap)
        {
            if (widths == null || widths.Length == 0)
            {
                return 0;
            }

            int total = startX;
            for (int i = 0; i < widths.Length; i++)
            {
                total += widths[i];
                if (i < widths.Length - 1)
                {
                    total += gap;
                }
            }

            return total;
        }

        /// <summary>
        /// 指定タブのレイアウト左端。
        /// </summary>
        /// <param name="widths">各タブ幅。null なら startX。</param>
        /// <param name="index">タブ番号。</param>
        /// <param name="startX">先頭左端。</param>
        /// <param name="gap">タブ間ギャップ。</param>
        /// <returns>左端（px）。</returns>
        public static int TabLeft(int[] widths, int index, int startX, int gap)
        {
            int x = startX;
            if (widths == null)
            {
                return x;
            }

            int n = index;
            if (n < 0)
            {
                return x;
            }

            if (n > widths.Length)
            {
                n = widths.Length;
            }

            for (int i = 0; i < n; i++)
            {
                x += widths[i] + gap;
            }

            return x;
        }

        /// <summary>
        /// 横オフセットを [0, contentWidth-viewportWidth] に収める。溢れ無しまたは viewportWidth&lt;=0 は 0。
        /// </summary>
        /// <param name="offset">現在のオフセット。</param>
        /// <param name="contentWidth">内容幅。</param>
        /// <param name="viewportWidth">可視幅。</param>
        /// <returns>収めたオフセット。</returns>
        public static int ClampOffset(int offset, int contentWidth, int viewportWidth)
        {
            if (viewportWidth <= 0 || contentWidth <= viewportWidth)
            {
                return 0;
            }

            int max = contentWidth - viewportWidth;
            if (offset < 0)
            {
                return 0;
            }

            if (offset > max)
            {
                return max;
            }

            return offset;
        }

        /// <summary>
        /// 選択タブが可視になるようオフセットを寄せる。最後に ClampOffset する。
        /// タブ幅がビュー以上なら tabLeft にピン。左はみ出しは tabLeft、右は tabRight-viewport。完全可視なら offset 不変。viewportWidth&lt;=0 は Clamp 経由で 0。
        /// </summary>
        /// <param name="offset">現在のオフセット。</param>
        /// <param name="tabLeft">タブ左端（レイアウト座標）。</param>
        /// <param name="tabRight">タブ右端（レイアウト座標）。</param>
        /// <param name="contentWidth">内容幅。</param>
        /// <param name="viewportWidth">可視幅。</param>
        /// <returns>寄せたオフセット。</returns>
        public static int EnsureVisible(int offset, int tabLeft, int tabRight, int contentWidth, int viewportWidth)
        {
            int next = offset;
            int tabWidth = tabRight - tabLeft;
            if (viewportWidth > 0 && tabWidth >= viewportWidth)
            {
                next = tabLeft;
            }
            else if (tabLeft < offset)
            {
                next = tabLeft;
            }
            else if (tabRight > offset + viewportWidth)
            {
                next = tabRight - viewportWidth;
            }

            return ClampOffset(next, contentWidth, viewportWidth);
        }
    }
}
