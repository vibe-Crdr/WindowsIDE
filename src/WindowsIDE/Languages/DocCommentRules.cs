using System;
using System.Collections.Generic;
using System.Text;
using WindowsIDE.Editor;

namespace WindowsIDE.Languages
{
    /// <summary>
    /// F-DOC 枠の挿入計画。
    /// </summary>
    public sealed class DocInsertPlan
    {
        private bool isDuplicate;
        private int insertLine;
        private string text;
        private int caretColumn;

        /// <summary>
        /// 計画を作る。
        /// </summary>
        /// <param name="isDuplicate">直前に既存枠がある。</param>
        /// <param name="insertLine">挿入する行（定義行）。</param>
        /// <param name="text">枠本文（末尾改行込み）。</param>
        /// <param name="caretColumn">summary のコロン直後。</param>
        public DocInsertPlan(bool isDuplicate, int insertLine, string text, int caretColumn)
        {
            this.isDuplicate = isDuplicate;
            this.insertLine = insertLine;
            this.text = (text == null) ? "" : text;
            this.caretColumn = caretColumn;
        }

        /// <summary>直前に既存枠がある。</summary>
        public bool IsDuplicate
        {
            get { return this.isDuplicate; }
        }

        /// <summary>挿入する行（定義行のインデックス）。</summary>
        public int InsertLine
        {
            get { return this.insertLine; }
        }

        /// <summary>枠本文。末尾はバッファの NewLine。</summary>
        public string Text
        {
            get { return this.text; }
        }

        /// <summary>summary のコロン直後の列。</summary>
        public int CaretColumn
        {
            get { return this.caretColumn; }
        }
    }

    /// <summary>
    /// 枠コメントの検出と生成。C# は // のみ。/// は生成しない。
    /// </summary>
    public static class DocCommentRules
    {
        private const string Dash24 = "------------------------";
        private const string LabelSummary = "summary :";
        private const string LabelArgs = "args    :";
        private const string LabelReturns = "returns :";

        /// <summary>
        /// 挿入計画を作る。Plain / 無題 / 宣言なし / 空 cmd は false。
        /// </summary>
        /// <param name="language">言語。</param>
        /// <param name="buffer">本文。</param>
        /// <param name="session">行開始状態。無くてよい。</param>
        /// <param name="filePath">ディスクパス。無題は null。</param>
        /// <param name="caretLine">キャレット行。</param>
        /// <param name="caretColumn">キャレット列。未使用でも受け取る。</param>
        /// <param name="plan">計画。</param>
        /// <returns>挿入または重複検出なら true。</returns>
        public static bool TryBuildInsert(LanguageKind language, TextBuffer buffer, HighlightSession session, string filePath, int caretLine, int caretColumn, out DocInsertPlan plan)
        {
            plan = null;
            if (buffer == null || string.IsNullOrEmpty(filePath))
            {
                return false;
            }

            if (language == LanguageKind.Plain)
            {
                return false;
            }

            int defLine;
            if (!TryFindDefinitionLine(language, buffer, session, filePath, caretLine, out defLine))
            {
                return false;
            }

            string indent = IndentRules.LeadingWhitespace(buffer.GetLine(defLine));
            string prefix = CommentPrefix(language);
            if (prefix == null)
            {
                return false;
            }

            if (HasFrameBefore(language, buffer, session, defLine, indent))
            {
                plan = new DocInsertPlan(true, defLine, "", indent.Length + prefix.Length + LabelSummary.Length);
                return true;
            }

            string args;
            string returns;
            FillSignature(language, buffer.GetLine(defLine), out args, out returns);
            string nl = buffer.NewLine;
            StringBuilder sb = new StringBuilder();
            sb.Append(indent);
            sb.Append(prefix);
            sb.Append(Dash24);
            sb.Append(nl);
            sb.Append(indent);
            sb.Append(prefix);
            sb.Append(LabelSummary);
            sb.Append(nl);
            sb.Append(indent);
            sb.Append(prefix);
            sb.Append(LabelArgs);
            if (!string.IsNullOrEmpty(args))
            {
                sb.Append(" ");
                sb.Append(args);
            }

            sb.Append(nl);
            sb.Append(indent);
            sb.Append(prefix);
            sb.Append(LabelReturns);
            if (!string.IsNullOrEmpty(returns))
            {
                sb.Append(" ");
                sb.Append(returns);
            }

            sb.Append(nl);
            sb.Append(indent);
            sb.Append(prefix);
            sb.Append(Dash24);
            sb.Append(nl);
            int caretCol = indent.Length + prefix.Length + LabelSummary.Length;
            plan = new DocInsertPlan(false, defLine, sb.ToString(), caretCol);
            return true;
        }

        /// <summary>
        /// 定義行直前の F-DOC 枠なら true。通常コメントは枠とみなさない。
        /// </summary>
        /// <param name="language">言語。</param>
        /// <param name="buffer">本文。</param>
        /// <param name="session">行開始状態。</param>
        /// <param name="definitionLine">定義行。</param>
        /// <param name="lines">枠の行（プレフィックス除去後）。</param>
        /// <returns>枠なら true。</returns>
        public static bool TryGetFrame(LanguageKind language, TextBuffer buffer, HighlightSession session, int definitionLine, out string[] lines)
        {
            lines = null;
            if (buffer == null || definitionLine < 5)
            {
                return false;
            }

            string indent = IndentRules.LeadingWhitespace(buffer.GetLine(definitionLine));
            if (!HasFrameBefore(language, buffer, session, definitionLine, indent))
            {
                return false;
            }

            string[] raw = new string[5];
            for (int i = 0; i < 5; i++)
            {
                string stripped;
                if (!TryStripComment(language, buffer.GetLine(definitionLine - 5 + i), out stripped))
                {
                    return false;
                }

                raw[i] = stripped;
            }

            lines = raw;
            return true;
        }

        private static bool TryFindDefinitionLine(LanguageKind language, TextBuffer buffer, HighlightSession session, string filePath, int caretLine, out int defLine)
        {
            defLine = -1;
            if (caretLine < 0 || caretLine >= buffer.LineCount)
            {
                return false;
            }

            if (language == LanguageKind.Cmd)
            {
                if (IsBlankOrCommentOnly(language, buffer, session, caretLine))
                {
                    return false;
                }

                defLine = caretLine;
                return true;
            }

            List<DeclaredSymbol> list = DefinitionResolver.ListCurrent(language, buffer, filePath);
            if (IsDeclarationAt(list, caretLine))
            {
                defLine = caretLine;
                return true;
            }

            int best = -1;
            for (int i = 0; i < list.Count; i++)
            {
                DeclaredSymbol item = list[i];
                if (item == null)
                {
                    continue;
                }

                if (item.Kind != SymbolKind.Type && item.Kind != SymbolKind.Method && item.Kind != SymbolKind.Property)
                {
                    continue;
                }

                if (item.Line < caretLine && item.Line > best)
                {
                    best = item.Line;
                }
            }

            if (best < 0)
            {
                return false;
            }

            defLine = best;
            return true;
        }

        private static bool IsDeclarationAt(List<DeclaredSymbol> list, int line)
        {
            if (list == null)
            {
                return false;
            }

            for (int i = 0; i < list.Count; i++)
            {
                DeclaredSymbol item = list[i];
                if (item == null)
                {
                    continue;
                }

                if (item.Line != line)
                {
                    continue;
                }

                if (item.Kind == SymbolKind.Type || item.Kind == SymbolKind.Method || item.Kind == SymbolKind.Property)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasFrameBefore(LanguageKind language, TextBuffer buffer, HighlightSession session, int defLine, string indent)
        {
            if (defLine < 5)
            {
                return false;
            }

            string dash;
            string summary;
            string args;
            string returns;
            string dash2;
            if (!TryStripComment(language, buffer.GetLine(defLine - 5), out dash) ||
                !TryStripComment(language, buffer.GetLine(defLine - 4), out summary) ||
                !TryStripComment(language, buffer.GetLine(defLine - 3), out args) ||
                !TryStripComment(language, buffer.GetLine(defLine - 2), out returns) ||
                !TryStripComment(language, buffer.GetLine(defLine - 1), out dash2))
            {
                return false;
            }

            if (dash != Dash24 || dash2 != Dash24)
            {
                return false;
            }

            if (!StartsWithOrdinal(summary, "summary") || !StartsWithOrdinal(args, "args") || !StartsWithOrdinal(returns, "returns"))
            {
                return false;
            }

            if (summary.IndexOf(':') < 0 || args.IndexOf(':') < 0 || returns.IndexOf(':') < 0)
            {
                return false;
            }

            return true;
        }

        private static bool StartsWithOrdinal(string text, string prefix)
        {
            if (text == null || prefix == null || text.Length < prefix.Length)
            {
                return false;
            }

            return string.Compare(text, 0, prefix, 0, prefix.Length, StringComparison.Ordinal) == 0;
        }

        private static string CommentPrefix(LanguageKind language)
        {
            if (language == LanguageKind.CSharp)
            {
                return "//";
            }

            if (language == LanguageKind.Vba)
            {
                return "'";
            }

            if (language == LanguageKind.PowerShell)
            {
                return "#";
            }

            if (language == LanguageKind.Cmd)
            {
                return "rem ";
            }

            return null;
        }

        /// <summary>
        /// コメント行ならプレフィックスを除いた本文を返す。
        /// </summary>
        /// <param name="language">言語。</param>
        /// <param name="line">行。</param>
        /// <param name="rest">プレフィックス除去後。</param>
        /// <returns>コメント行なら true。</returns>
        public static bool TryStripComment(LanguageKind language, string line, out string rest)
        {
            rest = null;
            if (line == null)
            {
                line = "";
            }

            string trimmed = line.TrimStart(' ', '\t');
            if (language == LanguageKind.CSharp)
            {
                if (trimmed.StartsWith("///", StringComparison.Ordinal))
                {
                    rest = trimmed.Substring(3).TrimStart(' ', '\t');
                    return true;
                }

                if (trimmed.StartsWith("//", StringComparison.Ordinal))
                {
                    rest = trimmed.Substring(2);
                    return true;
                }

                return false;
            }

            if (language == LanguageKind.Vba)
            {
                if (trimmed.StartsWith("'", StringComparison.Ordinal))
                {
                    rest = trimmed.Substring(1);
                    return true;
                }

                if (trimmed.Length >= 3 && trimmed.StartsWith("Rem", StringComparison.OrdinalIgnoreCase))
                {
                    if (trimmed.Length == 3 || trimmed[3] == ' ' || trimmed[3] == '\t')
                    {
                        rest = (trimmed.Length > 4) ? trimmed.Substring(4) : "";
                        return true;
                    }
                }

                return false;
            }

            if (language == LanguageKind.PowerShell)
            {
                if (trimmed.StartsWith("#", StringComparison.Ordinal))
                {
                    rest = trimmed.Substring(1);
                    return true;
                }

                return false;
            }

            if (language == LanguageKind.Cmd)
            {
                if (trimmed.StartsWith("::", StringComparison.Ordinal))
                {
                    rest = trimmed.Substring(2);
                    return true;
                }

                if (trimmed.Length >= 3 && trimmed.StartsWith("rem", StringComparison.OrdinalIgnoreCase))
                {
                    if (trimmed.Length == 3 || trimmed[3] == ' ' || trimmed[3] == '\t')
                    {
                        rest = (trimmed.Length > 4) ? trimmed.Substring(4) : ((trimmed.Length > 3) ? trimmed.Substring(3).TrimStart(' ', '\t') : "");
                        return true;
                    }
                }

                return false;
            }

            return false;
        }

        private static bool IsBlankOrCommentOnly(LanguageKind language, TextBuffer buffer, HighlightSession session, int line)
        {
            string text = buffer.GetLine(line);
            if (string.IsNullOrEmpty(text.Trim()))
            {
                return true;
            }

            List<Token> tokens = new List<Token>();
            BraceMatch.ScanOneLine(language, buffer, session, line, tokens);
            bool any = false;
            for (int i = 0; i < tokens.Count; i++)
            {
                Token token = tokens[i];
                if (IsSpace(text, token))
                {
                    continue;
                }

                any = true;
                if (token.Kind != TokenKind.Comment)
                {
                    return false;
                }
            }

            return any;
        }

        private static bool IsSpace(string line, Token token)
        {
            if (token.Length <= 0 || token.Start < 0 || token.Start + token.Length > line.Length)
            {
                return false;
            }

            for (int i = 0; i < token.Length; i++)
            {
                char c = line[token.Start + i];
                if (c != ' ' && c != '\t')
                {
                    return false;
                }
            }

            return true;
        }

        private static void FillSignature(LanguageKind language, string line, out string args, out string returns)
        {
            args = "";
            returns = "";
            if (string.IsNullOrEmpty(line))
            {
                return;
            }

            if (language == LanguageKind.Cmd)
            {
                return;
            }

            int open = line.IndexOf('(');
            int close = line.LastIndexOf(')');
            if (open >= 0 && close > open)
            {
                args = ParamNames(line.Substring(open + 1, close - open - 1));
            }

            if (language == LanguageKind.CSharp)
            {
                returns = CsharpReturn(line, open);
                return;
            }

            if (language == LanguageKind.Vba)
            {
                if (ContainsWordIgnoreCase(line, "Sub"))
                {
                    returns = "";
                    return;
                }

                int asAt = LastAs(line);
                if (asAt >= 0)
                {
                    returns = line.Substring(asAt).Trim();
                }

                return;
            }
        }

        private static string CsharpReturn(string line, int open)
        {
            string head = line;
            if (open >= 0)
            {
                head = line.Substring(0, open);
            }

            string[] parts = head.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            string lastType = "";
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i];
                if (p == "public" || p == "private" || p == "protected" || p == "internal" ||
                    p == "static" || p == "abstract" || p == "sealed" || p == "override" ||
                    p == "virtual" || p == "extern" || p == "unsafe" || p == "partial" ||
                    p == "readonly" || p == "async" || p == "new" || p == "const")
                {
                    continue;
                }

                if (p == "class" || p == "struct" || p == "interface" || p == "enum" || p == "delegate")
                {
                    return "";
                }

                lastType = p;
            }

            if (parts.Length >= 2)
            {
                lastType = parts[parts.Length - 2];
            }

            if (lastType == "void" || lastType == "Sub")
            {
                return "";
            }

            return lastType;
        }

        private static string ParamNames(string inside)
        {
            if (string.IsNullOrEmpty(inside) || inside.Trim().Length == 0)
            {
                return "";
            }

            string[] parts = inside.Split(',');
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < parts.Length; i++)
            {
                string name = LastIdent(parts[i]);
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                if (sb.Length > 0)
                {
                    sb.Append(", ");
                }

                sb.Append(name);
            }

            return sb.ToString();
        }

        private static string LastIdent(string segment)
        {
            if (segment == null)
            {
                return "";
            }

            string[] parts = segment.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = parts.Length - 1; i >= 0; i--)
            {
                string p = parts[i];
                if (p == "this" || p == "ref" || p == "out" || p == "params" || p == "ByVal" || p == "ByRef" || p == "Optional" || p == "As")
                {
                    continue;
                }

                if (p.Length > 0 && p[0] == '$')
                {
                    return p;
                }

                if (ScanChars.IsIdentStart(p[0]) || p[0] == '@')
                {
                    if (p.Length >= 2 && p[0] == '@')
                    {
                        return p.Substring(1);
                    }

                    return p;
                }
            }

            return "";
        }

        private static int LastAs(string line)
        {
            int best = -1;
            int i = 0;
            while (i + 2 < line.Length)
            {
                if ((line[i] == 'A' || line[i] == 'a') &&
                    (line[i + 1] == 'S' || line[i + 1] == 's') &&
                    (i == 0 || line[i - 1] == ' ' || line[i - 1] == '\t') &&
                    (i + 2 >= line.Length || line[i + 2] == ' ' || line[i + 2] == '\t'))
                {
                    best = i + 2;
                }

                i++;
            }

            if (best < 0)
            {
                return -1;
            }

            return best;
        }

        private static bool ContainsWordIgnoreCase(string line, string word)
        {
            if (line == null || word == null)
            {
                return false;
            }

            return line.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
