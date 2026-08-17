using System;
using System.Runtime.InteropServices;

namespace WindowsIDE.Ui.Fonts
{
    /// <summary>
    /// プロセス限定のフォント登録用 P/Invoke。
    /// </summary>
    public static class NativeFonts
    {
        /// <summary>AddFontResourceEx のプロセス限定フラグ。</summary>
        public const int FR_PRIVATE = 0x10;

        /// <summary>
        /// メモリ上のフォントをプロセスに登録する。失敗時は IntPtr.Zero。
        /// </summary>
        [DllImport("gdi32.dll")]
        public static extern IntPtr AddFontMemResourceEx(IntPtr pbFont, uint cbFont, IntPtr pdv, [In] ref uint pcFonts);

        /// <summary>
        /// メモリフォント登録を外す。
        /// </summary>
        [DllImport("gdi32.dll")]
        public static extern bool RemoveFontMemResourceEx(IntPtr fh);

        /// <summary>
        /// ファイルからプロセス限定でフォントを登録する。戻り値は追加されたフォント数。
        /// </summary>
        [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
        public static extern int AddFontResourceEx(string lpszFilename, uint fl, IntPtr pdv);

        /// <summary>
        /// AddFontResourceEx で登録したフォントを外す。
        /// </summary>
        [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
        public static extern bool RemoveFontResourceEx(string lpFileName, uint fl, IntPtr pdv);
    }
}
