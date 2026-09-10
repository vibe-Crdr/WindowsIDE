using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace WindowsIDE.Ui
{
    /// <summary>
    /// 96dpi DIP と物理ピクセルの換算。WinForms には依存しない。
    /// </summary>
    public static class DpiUtil
    {
        /// <summary>サイズは 12 DIP。ファミリは対象による（ツリー／メニュー／ステータス／タブは双フォント、About はシステム UI）。</summary>
        public const int UiFontDip = 12;

        /// <summary>自前スクロールバーの太さ（96dpi DIP）。</summary>
        public const int ScrollBarThicknessDip = 10;

        /// <summary>行番号ガターの左右余白（96dpi DIP）。</summary>
        public const int LineNumberPadDip = 8;

        /// <summary>ガター左のブレーク印列（96dpi DIP）。</summary>
        public const int BreakMarkDip = 12;

        /// <summary>行番号ガターから本文までのインセット（96dpi DIP）。</summary>
        public const int TextInsetDip = 4;

        /// <summary>スプリッタ幅（96dpi DIP）。</summary>
        public const int SplitterWidthDip = 6;

        /// <summary>スクロールつまみの最短（96dpi DIP）。</summary>
        public const int MinScrollThumbDip = 16;

        /// <summary>StatusStrip の左右余白（96dpi DIP）。</summary>
        public const int StatusStripPadXDip = 8;

        /// <summary>StatusStrip の上下余白（96dpi DIP）。</summary>
        public const int StatusStripPadYDip = 4;

        /// <summary>MenuStrip の左右余白（96dpi DIP）。</summary>
        public const int MenuStripPadXDip = 4;

        /// <summary>MenuStrip の上下余白（96dpi DIP）。</summary>
        public const int MenuStripPadYDip = 2;

        /// <summary>
        /// ウィンドウの実 DPI。ハンドルが無ければシステムの DPI。どちらも 0 なら 96。
        /// </summary>
        /// <param name="hwnd">対象 HWND。Zero なら GetDpiForSystem。</param>
        /// <returns>DPI（96 以上が通常）。</returns>
        public static int GetDpi(IntPtr hwnd)
        {
            uint dpi = 0;
            if (hwnd != IntPtr.Zero)
            {
                dpi = Native.GetDpiForWindow(hwnd);
            }

            if (dpi == 0)
            {
                dpi = Native.GetDpiForSystem();
            }

            if (dpi == 0)
            {
                return 96;
            }

            return (int)dpi;
        }

        /// <summary>
        /// DIP を物理ピクセルへ四捨五入する。
        /// </summary>
        /// <param name="dip">96dpi 基準の DIP。負なら 0。</param>
        /// <param name="deviceDpi">画面の DPI。0 以下は 96 扱い。</param>
        /// <returns>物理ピクセル。</returns>
        public static int ToPixels(int dip, int deviceDpi)
        {
            if (dip < 0)
            {
                return 0;
            }

            if (deviceDpi <= 0)
            {
                deviceDpi = 96;
            }

            return (dip * deviceDpi + 48) / 96;
        }

        /// <summary>
        /// ホイール delta（120 が 1 ノッチ）をピクセルへ換算する。正の delta は正を返す。
        /// </summary>
        /// <param name="delta">WM_MOUSEWHEEL / HWHEEL の符号付き delta。</param>
        /// <param name="pixelsPerNotch">1 ノッチあたりのピクセル。</param>
        /// <returns>delta * pixelsPerNotch / 120（整数除算）。</returns>
        public static int WheelToPixels(int delta, int pixelsPerNotch)
        {
            return delta * pixelsPerNotch / 120;
        }

        /// <summary>
        /// スクロールバーが必要か。maximum は inclusive。page = max(largeChange, 1)。
        /// items = maximum - minimum + 1。maximum &gt; minimum かつ items &gt; page のとき true。
        /// </summary>
        /// <param name="minimum">Minimum。</param>
        /// <param name="maximum">Maximum（inclusive）。</param>
        /// <param name="largeChange">LargeChange（ページ）。</param>
        /// <returns>バーを出すとき true。</returns>
        public static bool ScrollBarNeeded(int minimum, int maximum, int largeChange)
        {
            int page = largeChange;
            if (page < 1)
            {
                page = 1;
            }

            int items = maximum - minimum + 1;
            return maximum > minimum && items > page;
        }

        /// <summary>
        /// leftover に delta を足し、120 単位のノッチ数を返す（負も可）。
        /// </summary>
        /// <param name="delta">WM_MOUSEWHEEL の符号付き delta。</param>
        /// <param name="leftover">未消費 delta。呼び出し側が保持する。</param>
        /// <returns>切り出したノッチ数。1 ノッチは 120。</returns>
        public static int WheelNotches(int delta, ref int leftover)
        {
            leftover += delta;
            int notches = leftover / 120;
            leftover -= notches * 120;
            return notches;
        }

        /// <summary>
        /// トラック長に対するつまみ長さ。WinForms 相当で page / (range + page)。
        /// </summary>
        /// <param name="trackLength">トラックの長さ（物理 px）。</param>
        /// <param name="minimum">Minimum。</param>
        /// <param name="maximum">Maximum（inclusive）。</param>
        /// <param name="largeChange">LargeChange（ページ）。</param>
        /// <param name="minThumb">つまみの下限（物理 px）。</param>
        /// <returns>つまみ長さ（物理 px）。trackLength を超えない。</returns>
        public static int ScrollThumbLength(int trackLength, int minimum, int maximum, int largeChange, int minThumb)
        {
            int range = maximum - minimum;
            if (range <= 0 || trackLength <= 0)
            {
                return trackLength;
            }

            int page = largeChange;
            if (page < 1)
            {
                page = 1;
            }

            int span = range + page;
            int thumb = (int)((long)trackLength * page / span);
            if (thumb < minThumb)
            {
                thumb = minThumb;
            }

            if (thumb > trackLength)
            {
                thumb = trackLength;
            }

            return thumb;
        }

        /// <summary>
        /// 編集行の高さ。セルメトリクスと DIP 由来の物理サイズの大きい方に余白を足す。
        /// </summary>
        /// <param name="cell">Font.Height などセル高さ（物理 px）。</param>
        /// <param name="physicalPx">fontSize DIP を物理 px にしたもの。</param>
        /// <param name="extra">既に物理 px の行間余白。</param>
        /// <returns>行の高さ（物理 px）。</returns>
        public static int EditorLineHeight(int cell, int physicalPx, int extra)
        {
            int h = cell;
            if (physicalPx > h)
            {
                h = physicalPx;
            }

            return h + extra;
        }

        /// <summary>
        /// タブバーの高さ。Font.Height と DIP 余白、下限 28 DIP。
        /// </summary>
        /// <param name="fontHeight">描画に使う Font.Height（物理 px）。</param>
        /// <param name="deviceDpi">画面の DPI。0 以下は 96 扱い。</param>
        /// <returns>タブバーの高さ（物理 px）。</returns>
        public static int TabStripHeight(int fontHeight, int deviceDpi)
        {
            int fromFont = fontHeight + ToPixels(8, deviceDpi);
            int minH = ToPixels(28, deviceDpi);
            if (fromFont > minH)
            {
                return fromFont;
            }

            return minH;
        }

        /// <summary>
        /// ツリー行の高さ。Font.Height と DIP 余白、下限 22 DIP。
        /// </summary>
        /// <param name="fontHeight">描画に使う Font.Height（物理 px）。</param>
        /// <param name="deviceDpi">画面の DPI。0 以下は 96 扱い。</param>
        /// <returns>行の高さ（物理 px）。</returns>
        public static int TreeItemHeight(int fontHeight, int deviceDpi)
        {
            int fromFont = fontHeight + ToPixels(8, deviceDpi);
            int minH = ToPixels(22, deviceDpi);
            if (fromFont > minH)
            {
                return fromFont;
            }

            return minH;
        }

        /// <summary>
        /// 本文レイヤのクリップ矩形。左は gutterWidth と textArea.Left の大きい方（textInset は足さない）。
        /// 上は textArea.Top。右と下は textArea に合わせ、負の幅・高さは 0 にする。
        /// </summary>
        /// <param name="gutterWidth">行番号ガターの幅（物理 px）。</param>
        /// <param name="textArea">編集器の描画領域。</param>
        /// <returns>本文クリップ矩形。</returns>
        public static Rectangle TextBodyClip(int gutterWidth, Rectangle textArea)
        {
            int left = gutterWidth;
            if (textArea.Left > left)
            {
                left = textArea.Left;
            }

            int top = textArea.Top;
            int width = textArea.Right - left;
            int height = textArea.Bottom - top;
            if (width < 0)
            {
                width = 0;
            }

            if (height < 0)
            {
                height = 0;
            }

            return new Rectangle(left, top, width, height);
        }

        private static class Native
        {
            [DllImport("user32.dll")]
            public static extern uint GetDpiForWindow(IntPtr hwnd);

            [DllImport("user32.dll")]
            public static extern uint GetDpiForSystem();
        }
    }
}
