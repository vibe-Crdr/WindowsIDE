using System.Collections.Generic;
using WindowsIDE.Editor;

namespace WindowsIDE.Languages
{
    /// <summary>
    /// キャレット位置の識別子ヒット。BraceMatch の左 1 文字優先とは別。
    /// </summary>
    public static class IdentifierAtCaret
    {
        /// <summary>
        /// 行トークンから識別子を取る。文字列・コメント・Keyword・Number は false。
        /// </summary>
        /// <param name="language">言語。</param>
        /// <param name="buffer">本文。</param>
        /// <param name="session">行開始状態。無くてよい。</param>
        /// <param name="line">0 始まりの行。</param>
        /// <param name="column">0 始まりの列。</param>
        /// <param name="hit">ヒット。</param>
        /// <returns>識別子なら true。</returns>
        public static bool TryGet(LanguageKind language, TextBuffer buffer, HighlightSession session, int line, int column, out IdentifierHit hit)
        {
            hit = null;
            if (buffer == null || line < 0 || line >= buffer.LineCount)
            {
                return false;
            }

            string text = buffer.GetLine(line);
            if (column < 0)
            {
                column = 0;
            }

            if (column > text.Length)
            {
                column = text.Length;
            }

            List<Token> tokens = new List<Token>();
            BraceMatch.ScanOneLine(language, buffer, session, line, tokens);
            TokenKind at = KindAt(tokens, column);
            if (at == TokenKind.String || at == TokenKind.Comment)
            {
                return false;
            }

            int index = IndexCovering(tokens, column);
            if (index < 0)
            {
                if (column > 0)
                {
                    index = IndexCovering(tokens, column - 1);
                }

                if (index < 0)
                {
                    index = IndexAtOrAfter(tokens, column);
                }
            }

            if (index < 0)
            {
                return false;
            }

            Token token = tokens[index];
            if (token.Kind == TokenKind.Keyword || token.Kind == TokenKind.Number)
            {
                return false;
            }

            if (token.Kind == TokenKind.String || token.Kind == TokenKind.Comment)
            {
                return false;
            }

            if (!IdentifierClassifier.LooksLikeIdentifier(language, text, token))
            {
                return false;
            }

            int start;
            int length;
            string name;
            ExpandName(language, text, tokens, index, out start, out length, out name);
            if (string.IsNullOrEmpty(name) || length <= 0)
            {
                return false;
            }

            hit = new IdentifierHit(name, line, start, length);
            return true;
        }

        private static void ExpandName(LanguageKind language, string line, List<Token> tokens, int index, out int start, out int length, out string name)
        {
            Token token = tokens[index];
            start = token.Start;
            length = token.Length;
            int last = index;
            if (language == LanguageKind.PowerShell)
            {
                int first = index;
                while (first >= 2 &&
                    IsChar(line, tokens[first - 1], '-') &&
                    !IsSpaceToken(line, tokens[first - 1]) &&
                    IdentifierClassifier.LooksLikeIdentifier(language, line, tokens[first - 2]))
                {
                    first = first - 2;
                }

                last = index;
                while (last + 2 < tokens.Count &&
                    IsChar(line, tokens[last + 1], '-') &&
                    !IsSpaceToken(line, tokens[last + 1]) &&
                    IdentifierClassifier.LooksLikeIdentifier(language, line, tokens[last + 2]))
                {
                    last = last + 2;
                }

                start = tokens[first].Start;
                Token endTok = tokens[last];
                length = endTok.Start + endTok.Length - start;
            }

            if (start < 0 || length <= 0 || start + length > line.Length)
            {
                name = "";
                return;
            }

            name = line.Substring(start, length);
            if (language == LanguageKind.CSharp && name.Length >= 2 && name[0] == '@')
            {
                name = name.Substring(1);
            }
        }

        private static int IndexCovering(List<Token> tokens, int column)
        {
            for (int i = 0; i < tokens.Count; i++)
            {
                Token token = tokens[i];
                if (token.Start <= column && column < token.Start + token.Length)
                {
                    return i;
                }
            }

            return -1;
        }

        private static int IndexAtOrAfter(List<Token> tokens, int column)
        {
            for (int i = 0; i < tokens.Count; i++)
            {
                Token token = tokens[i];
                if (token.Start >= column)
                {
                    return i;
                }
            }

            return -1;
        }

        private static TokenKind KindAt(List<Token> tokens, int column)
        {
            int index = IndexCovering(tokens, column);
            if (index < 0)
            {
                return TokenKind.Text;
            }

            return tokens[index].Kind;
        }

        private static bool IsChar(string line, Token token, char c)
        {
            return token.Length == 1 &&
                token.Start >= 0 &&
                token.Start < line.Length &&
                line[token.Start] == c;
        }

        private static bool IsSpaceToken(string line, Token token)
        {
            if (token.Length <= 0 || token.Start < 0 || token.Start + token.Length > line.Length)
            {
                return false;
            }

            for (int i = 0; i < token.Length; i++)
            {
                char ch = line[token.Start + i];
                if (ch != ' ' && ch != '\t')
                {
                    return false;
                }
            }

            return true;
        }
    }
}
