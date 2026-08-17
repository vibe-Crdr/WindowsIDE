using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace WindowsIDE.Ui
{
    /// <summary>
    /// DWM でキャプション・枠・文字色をシェル色に合わせる。WinForms には依存しない。
    /// </summary>
    public static class NativeCaption
    {
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_BORDER_COLOR = 34;
        private const int DWMWA_CAPTION_COLOR = 35;
        private const int DWMWA_TEXT_COLOR = 36;

        /// <summary>
        /// Color を COLORREF（0x00BBGGRR）へ変換する。
        /// </summary>
        /// <param name="c">元の色。</param>
        /// <returns>DWM に渡す COLORREF。</returns>
        public static int ToColorRef(Color c)
        {
            return c.R | (c.G << 8) | (c.B << 16);
        }

        /// <summary>
        /// ImmersiveDarkMode のあと CAPTION / TEXT / BORDER を適用する。失敗は無視して続行する。
        /// </summary>
        /// <param name="hwnd">対象 HWND。Zero なら何もしない。</param>
        public static void Apply(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            int dark = 1;
            Native.DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, 4);

            int caption = ToColorRef(Theme.Background);
            Native.DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref caption, 4);

            int text = ToColorRef(Theme.Foreground);
            Native.DwmSetWindowAttribute(hwnd, DWMWA_TEXT_COLOR, ref text, 4);

            int border = ToColorRef(Theme.Border);
            Native.DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref border, 4);
        }

        private static class Native
        {
            [DllImport("dwmapi.dll")]
            public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
        }
    }
}
