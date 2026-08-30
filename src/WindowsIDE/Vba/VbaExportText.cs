using System;
using System.Collections.Generic;
using System.Text;

namespace WindowsIDE.Vba
{
    /// <summary>
    /// Excel Export のヘッダ処理。CodeModule 用の本文取り出しと、新規 Import 用ヘッダ合成。WinForms 非依存。
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
        /// 新規 Import 用 TEMP テキストを合成する。ディスクは書き換えない。VB_Name は excelName が勝つ。
        /// </summary>
        /// <param name="kind">Std または Class。Document は不可。</param>
        /// <param name="excelName">namingMode の Excel 側名。</param>
        /// <param name="text">ディスク文字列。null は空本文。ヘッダ付きでも可。</param>
        /// <returns>常に CRLF の Import テキスト。</returns>
        public static string EnsureForImport(VbaComponentKind kind, string excelName, string text)
        {
            if (kind != VbaComponentKind.Std && kind != VbaComponentKind.Class)
            {
                throw new ArgumentException("Std または Class のみ。", "kind");
            }

            if (string.IsNullOrEmpty(excelName))
            {
                throw new ArgumentException("excelName が空です。", "excelName");
            }

            string body = StripForCodeModule(text);
            string versionBlock;
            List<string> otherAttributes;
            CollectImportHeader(text, out versionBlock, out otherAttributes);

            StringBuilder sb = new StringBuilder();
            if (kind == VbaComponentKind.Class)
            {
                if (string.IsNullOrEmpty(versionBlock))
                {
                    sb.Append("VERSION 1.0 CLASS\r\nBEGIN\r\n  MultiUse = -1  'True\r\nEND\r\n");
                }
                else
                {
                    sb.Append(versionBlock);
                }
            }

            sb.Append("Attribute VB_Name = \"");
            sb.Append(excelName);
            sb.Append("\"\r\n");
            AppendAttributeLines(sb, otherAttributes);
            if (kind == VbaComponentKind.Class)
            {
                AppendMissingClassAttributes(sb, otherAttributes);
            }

            sb.Append(body);
            return sb.ToString();
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

        /// <summary>
        /// 先頭ヘッダから完全な VERSION/BEGIN-END と、VB_Name 以外の Attribute 行を取る。
        /// </summary>
        /// <param name="text">ディスク文字列。null 可。</param>
        /// <param name="versionBlock">完全な VERSION/BEGIN-END（末尾 CRLF）。無ければ null。</param>
        /// <param name="otherAttributes">VB_Name 以外の先頭 Attribute 行。</param>
        private static void CollectImportHeader(string text, out string versionBlock, out List<string> otherAttributes)
        {
            versionBlock = null;
            otherAttributes = new List<string>();
            if (text == null)
            {
                return;
            }

            string n = text.Replace("\r\n", "\n").Replace("\r", "\n");
            string[] lines = n.Split('\n');
            int i = 0;
            if (lines.Length > 0 && string.Equals(lines[0].Trim(), "VERSION 1.0 CLASS", StringComparison.OrdinalIgnoreCase))
            {
                if (1 < lines.Length && string.Equals(lines[1].Trim(), "BEGIN", StringComparison.OrdinalIgnoreCase))
                {
                    int j = 2;
                    while (j < lines.Length && !string.Equals(lines[j].Trim(), "END", StringComparison.OrdinalIgnoreCase))
                    {
                        j++;
                    }

                    if (j < lines.Length && string.Equals(lines[j].Trim(), "END", StringComparison.OrdinalIgnoreCase))
                    {
                        StringBuilder block = new StringBuilder();
                        for (int k = 0; k <= j; k++)
                        {
                            if (k > 0)
                            {
                                block.Append("\r\n");
                            }

                            block.Append(lines[k]);
                        }

                        block.Append("\r\n");
                        versionBlock = block.ToString();
                        i = j + 1;
                    }
                    else
                    {
                        i = lines.Length;
                    }
                }
                else
                {
                    i = 1;
                }
            }

            while (i < lines.Length && lines[i].TrimStart().StartsWith("Attribute ", StringComparison.Ordinal))
            {
                string attrName = GetAttributeName(lines[i]);
                if (attrName == null || !string.Equals(attrName, "VB_Name", StringComparison.OrdinalIgnoreCase))
                {
                    otherAttributes.Add(lines[i]);
                }

                i++;
            }
        }

        /// <summary>
        /// Attribute 行の識別子。先頭の Attribute の直後。無ければ null。
        /// </summary>
        /// <param name="line">1 行。</param>
        /// <returns>識別子。Attribute 行でなければ null。</returns>
        private static string GetAttributeName(string line)
        {
            if (line == null)
            {
                return null;
            }

            string t = line.TrimStart();
            if (!t.StartsWith("Attribute ", StringComparison.Ordinal))
            {
                return null;
            }

            int i = "Attribute ".Length;
            while (i < t.Length && (t[i] == ' ' || t[i] == '\t'))
            {
                i++;
            }

            if (i >= t.Length)
            {
                return null;
            }

            char c0 = t[i];
            if (!IsAttributeIdentChar(c0, true))
            {
                return null;
            }

            int start = i;
            i++;
            while (i < t.Length && IsAttributeIdentChar(t[i], false))
            {
                i++;
            }

            return t.Substring(start, i - start);
        }

        private static bool IsAttributeIdentChar(char c, bool first)
        {
            if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || c == '_')
            {
                return true;
            }

            if (!first && c >= '0' && c <= '9')
            {
                return true;
            }

            return false;
        }

        private static void AppendAttributeLines(StringBuilder sb, List<string> attributes)
        {
            if (attributes == null)
            {
                return;
            }

            for (int i = 0; i < attributes.Count; i++)
            {
                sb.Append(attributes[i]);
                sb.Append("\r\n");
            }
        }

        private static void AppendMissingClassAttributes(StringBuilder sb, List<string> existing)
        {
            string[] required = new string[]
            {
                "VB_GlobalNameSpace",
                "VB_Creatable",
                "VB_PredeclaredId",
                "VB_Exposed"
            };
            for (int r = 0; r < required.Length; r++)
            {
                if (!HasAttributeName(existing, required[r]))
                {
                    sb.Append("Attribute ");
                    sb.Append(required[r]);
                    sb.Append(" = False\r\n");
                }
            }
        }

        private static bool HasAttributeName(List<string> attributes, string name)
        {
            if (attributes == null)
            {
                return false;
            }

            for (int i = 0; i < attributes.Count; i++)
            {
                string n = GetAttributeName(attributes[i]);
                if (n != null && string.Equals(n, name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
