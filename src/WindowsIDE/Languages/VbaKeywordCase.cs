using System.Collections.Generic;
using WindowsIDE.Editor;

namespace WindowsIDE.Languages
{
    /// <summary>
    /// VBA キーワードを表の大文字小文字へ直す。インデントは触らない。
    /// </summary>
    public static class VbaKeywordCase
    {
        /// <summary>
        /// 区切り入力の直前トークンが表のキーワードなら canonical を返す。
        /// </summary>
        /// <param name="language">言語。VBA 以外は false。</param>
        /// <param name="buffer">本文。</param>
        /// <param name="session">行開始状態。</param>
        /// <param name="line">キャレット行。</param>
        /// <param name="column">キャレット列。</param>
        /// <param name="typed">入力しようとしている区切り。</param>
        /// <param name="startColumn">置換開始列。</param>
        /// <param name="canonical">表の綴り。</param>
        /// <returns>置換するとき true。</returns>
        public static bool TryCanonicalBeforeSeparator(LanguageKind language, TextBuffer buffer, HighlightSession session, int line, int column, char typed, out int startColumn, out string canonical)
        {
            startColumn = 0;
            canonical = null;
            if (language != LanguageKind.Vba || buffer == null)
            {
                return false;
            }

            if (!IsSeparator(typed))
            {
                return false;
            }

            if (line < 0 || line >= buffer.LineCount)
            {
                return false;
            }

            if (column <= 0)
            {
                return false;
            }

            string text = buffer.GetLine(line);
            if (column > text.Length)
            {
                column = text.Length;
            }

            List<Token> tokens = new List<Token>();
            BraceMatch.ScanOneLine(language, buffer, session, line, tokens);
            int pos = column - 1;
            Token hit = new Token();
            bool found = false;
            int i = 0;
            while (i < tokens.Count)
            {
                Token token = tokens[i];
                if (pos >= token.Start && pos < token.Start + token.Length)
                {
                    hit = token;
                    found = true;
                    break;
                }

                i++;
            }

            if (!found)
            {
                return false;
            }

            if (hit.Kind == TokenKind.String)
            {
                return false;
            }

            int len = hit.Length;
            if (hit.Start + len > text.Length)
            {
                len = text.Length - hit.Start;
            }

            if (len <= 0)
            {
                return false;
            }

            string word = text.Substring(hit.Start, len);
            if (hit.Kind == TokenKind.Comment)
            {
                if (!TryRemHead(word, out canonical))
                {
                    return false;
                }

                startColumn = hit.Start;
                return !string.Equals(word.Substring(0, canonical.Length), canonical, System.StringComparison.Ordinal);
            }

            if (hit.Kind != TokenKind.Keyword)
            {
                return false;
            }

            if (!VbaKeywords.TryCanonical(word, out canonical))
            {
                return false;
            }

            if (string.Equals(word, canonical, System.StringComparison.Ordinal))
            {
                return false;
            }

            startColumn = hit.Start;
            return true;
        }

        private static bool TryRemHead(string comment, out string canonical)
        {
            canonical = null;
            if (comment == null || comment.Length < 3)
            {
                return false;
            }

            string head = comment.Substring(0, 3);
            if (!head.Equals("Rem", System.StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (comment.Length > 3 && ScanChars.IsIdentPart(comment[3]))
            {
                return false;
            }

            canonical = "Rem";
            return true;
        }

        private static bool IsSeparator(char c)
        {
            if (c == ' ' || c == '\t' || c == '\r' || c == '\n')
            {
                return true;
            }

            switch (c)
            {
                case '(':
                case ')':
                case '[':
                case ']':
                case '{':
                case '}':
                case ',':
                case '.':
                case ':':
                case ';':
                case '+':
                case '-':
                case '*':
                case '/':
                case '\\':
                case '^':
                case '&':
                case '=':
                case '<':
                case '>':
                case '!':
                case '#':
                case '\'':
                case '"':
                    return true;
                default:
                    return false;
            }
        }
    }
}
