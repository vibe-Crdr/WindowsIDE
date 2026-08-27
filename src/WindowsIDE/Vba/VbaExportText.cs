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
    }
}
