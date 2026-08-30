using System;
using System.Text;

namespace WindowsIDE.Vba
{
    /// <summary>
    /// Excel Export ファイルから CodeModule 用の本文を取り出す。WinForms 非依存。
    /// </summary>
    public static class VbaExportText
    {
        /// <summary>
        /// VERSION / BEGIN-END / 直後の連続 Attribute 行を除いた本文を返す。本文中の Attribute は残す。改行は CRLF。null は空文字。
        /// </summary>
        /// <param name="text">Export 生テキスト、または既に本文だけ。</param>
        /// <returns>AddFromString 用の本文。</returns>
        public static string StripForCodeModule(string text)
        {
            if (text == null)
            {
                return "";
            }

            string n = text.Replace("\r\n", "\n").Replace("\r", "\n");
            string[] lines = n.Split('\n');
            int i = IndexAfterExportHeader(lines);

            StringBuilder sb = new StringBuilder();
            bool first = true;
            for (; i < lines.Length; i++)
            {
                if (!first)
                {
                    sb.Append("\r\n");
                }

                first = false;
                sb.Append(lines[i]);
            }

            return sb.ToString();
        }

        /// <summary>
        /// StripForCodeModule が除く先頭ヘッダ行数。本文中の Attribute は数えない。null は 0。
        /// </summary>
        /// <param name="text">ディスク上の VBA テキスト。null 可。</param>
        /// <returns>除いた先頭行数。</returns>
        public static int CountCodeModuleHeaderLines(string text)
        {
            if (text == null)
            {
                return 0;
            }

            string n = text.Replace("\r\n", "\n").Replace("\r", "\n");
            string[] lines = n.Split('\n');
            return IndexAfterExportHeader(lines);
        }

        /// <summary>
        /// CodeModule 行をディスク行へ写す。0 以下はそのまま。それ以外はヘッダ行数を足す。
        /// </summary>
        /// <param name="codeModuleLine">GetSelection の行（1 始まり）。</param>
        /// <param name="diskText">ディスク上の VBA テキスト。null 可。</param>
        /// <returns>ディスク行。変換しないときは codeModuleLine。</returns>
        public static int ToDiskLine(int codeModuleLine, string diskText)
        {
            if (codeModuleLine <= 0)
            {
                return codeModuleLine;
            }

            return codeModuleLine + CountCodeModuleHeaderLines(diskText);
        }

        /// <summary>
        /// VERSION / BEGIN-END / 直後の連続 Attribute を飛ばした本文開始インデックス。本文中の Attribute は対象外。
        /// </summary>
        /// <param name="lines">改行正規化後の行。</param>
        /// <returns>本文先頭のインデックス（除いた行数）。</returns>
        private static int IndexAfterExportHeader(string[] lines)
        {
            int i = 0;
            if (lines.Length > 0 && string.Equals(lines[0].Trim(), "VERSION 1.0 CLASS", StringComparison.OrdinalIgnoreCase))
            {
                i = 1;
                if (i < lines.Length && string.Equals(lines[i].Trim(), "BEGIN", StringComparison.OrdinalIgnoreCase))
                {
                    i++;
                    while (i < lines.Length && !string.Equals(lines[i].Trim(), "END", StringComparison.OrdinalIgnoreCase))
                    {
                        i++;
                    }

                    if (i < lines.Length && string.Equals(lines[i].Trim(), "END", StringComparison.OrdinalIgnoreCase))
                    {
                        i++;
                    }
                }
            }

            while (i < lines.Length && lines[i].TrimStart().StartsWith("Attribute ", StringComparison.Ordinal))
            {
                i++;
            }

            return i;
        }
    }
}
