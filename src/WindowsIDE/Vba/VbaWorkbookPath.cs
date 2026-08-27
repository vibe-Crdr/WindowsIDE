using System;
using System.IO;

namespace WindowsIDE.Vba
{
    /// <summary>
    /// マクロ有効ブックの拡張子判定。.xlsx / .xls / その他は拒否。
    /// </summary>
    public static class VbaWorkbookPath
    {
        /// <summary>
        /// .xlsm または .xlsb なら true。
        /// </summary>
        /// <param name="path">調べるパス。</param>
        /// <returns>マクロ有効ブックなら true。</returns>
        public static bool IsMacroWorkbook(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            string ext = Path.GetExtension(path);
            return string.Equals(ext, ".xlsm", StringComparison.OrdinalIgnoreCase)
                || string.Equals(ext, ".xlsb", StringComparison.OrdinalIgnoreCase);
        }
    }
}
