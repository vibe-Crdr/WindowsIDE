using System.Collections.Generic;

namespace WindowsIDE.Languages
{
    /// <summary>
    /// Markdown の行レキサ。状態は Normal / Fenced。識別子オーバーレイはしない。
    /// </summary>
    public sealed class MarkdownLexer : ILineLexer
    {
        internal const int Normal = 0;
        internal const int Fenced = 1;

        /// <summary>
        /// Markdown の 1 行を走査する。
        /// </summary>
        /// <param name="line">対象行。</param>
        /// <param name="startState">行開始状態。</param>
        /// <param name="tokens">出力先。</param>
        /// <param name="endState">次行の開始状態。</param>
        public void ScanLine(string line, int startState, List<Token> tokens, out int endState)
        {
            if (line == null)
            {
                line = "";
            }

            if (startState == Fenced)
            {
                ScanChars.Add(tokens, 0, line.Length, TokenKind.String);
                if (MarkdownSyntax.TryFence(line))
                {
                    endState = Normal;
                }
                else
                {
                    endState = Fenced;
                }

                return;
            }

            if (MarkdownSyntax.TryFence(line))
            {
                ScanChars.Add(tokens, 0, line.Length, TokenKind.String);
                endState = Fenced;
                return;
            }

            int quoteNest;
            int quoteContent;
            if (MarkdownSyntax.TryQuote(line, out quoteNest, out quoteContent))
            {
                if (quoteContent > 0)
                {
                    ScanChars.Add(tokens, 0, quoteContent, TokenKind.Keyword);
                }

                this.ScanInline(line, quoteContent, line.Length, tokens);
                endState = Normal;
                return;
            }

            int level;
            int hashStart;
            int contentStart;
            if (MarkdownSyntax.TryHeading(line, out level, out hashStart, out contentStart))
            {
                if (hashStart > 0)
                {
                    ScanChars.Add(tokens, 0, hashStart, TokenKind.Text);
                }

                ScanChars.Add(tokens, hashStart, line.Length - hashStart, TokenKind.Keyword);
                endState = Normal;
                return;
            }

            int markerStart;
            int markerLength;
            if (MarkdownSyntax.TryListItem(line, out markerStart, out markerLength, out contentStart))
            {
                if (markerStart > 0)
                {
                    ScanChars.Add(tokens, 0, markerStart, TokenKind.Text);
                }

                ScanChars.Add(tokens, markerStart, markerLength, TokenKind.Keyword);
                int afterMarker = markerStart + markerLength;
                this.ScanInline(line, afterMarker, line.Length, tokens);
                endState = Normal;
                return;
            }

            int setextLevel;
            if (MarkdownSyntax.TrySetextUnderline(line, out setextLevel))
            {
                ScanChars.Add(tokens, 0, line.Length, TokenKind.Keyword);
                endState = Normal;
                return;
            }

            if (MarkdownSyntax.TryThematicBreak(line))
            {
                ScanChars.Add(tokens, 0, line.Length, TokenKind.Keyword);
                endState = Normal;
                return;
            }

            string[] tableCells;
            if (MarkdownSyntax.TryTableRow(line, out tableCells))
            {
                this.ScanTable(line, tokens);
                endState = Normal;
                return;
            }

            if (MarkdownSyntax.TryIndentedCode(line))
            {
                ScanChars.Add(tokens, 0, line.Length, TokenKind.String);
                endState = Normal;
                return;
            }

            this.ScanInline(line, 0, line.Length, tokens);
            endState = Normal;
        }

        private void ScanTable(string line, List<Token> tokens)
        {
            int i = 0;
            int run = 0;
            while (i < line.Length)
            {
                if (line[i] == '|')
                {
                    if (i > run)
                    {
                        this.ScanInline(line, run, i, tokens);
                    }

                    ScanChars.Add(tokens, i, 1, TokenKind.Keyword);
                    i++;
                    run = i;
                    continue;
                }

                i++;
            }

            if (i > run)
            {
                this.ScanInline(line, run, i, tokens);
            }
        }

        private void ScanInline(string line, int start, int end, List<Token> tokens)
        {
            int i = start;
            while (i < end)
            {
                char c = line[i];
                if (c == '`')
                {
                    int close = IndexOfChar(line, '`', i + 1, end);
                    if (close >= 0)
                    {
                        ScanChars.Add(tokens, i, close - i + 1, TokenKind.String);
                        i = close + 1;
                        continue;
                    }
                }
                else if (c == '!' && i + 1 < end && line[i + 1] == '[')
                {
                    int consumed;
                    if (this.TryScanLink(line, i + 1, end, tokens, i, out consumed))
                    {
                        i = consumed;
                        continue;
                    }
                }
                else if (c == '[')
                {
                    int consumed;
                    if (this.TryScanLink(line, i, end, tokens, i, out consumed))
                    {
                        i = consumed;
                        continue;
                    }
                }
                else if (c == '<' && (StartsAt(line, i + 1, end, "http://") || StartsAt(line, i + 1, end, "https://")))
                {
                    int close = IndexOfChar(line, '>', i + 1, end);
                    if (close > i + 1)
                    {
                        ScanChars.Add(tokens, i, close - i + 1, TokenKind.Local);
                        i = close + 1;
                        continue;
                    }
                }
                else if (c == '~' && i + 1 < end && line[i + 1] == '~')
                {
                    int close = IndexOfTwo(line, '~', i + 2, end);
                    if (close >= 0)
                    {
                        ScanChars.Add(tokens, i, 2, TokenKind.Keyword);
                        if (close > i + 2)
                        {
                            ScanChars.Add(tokens, i + 2, close - (i + 2), TokenKind.Text);
                        }

                        ScanChars.Add(tokens, close, 2, TokenKind.Keyword);
                        i = close + 2;
                        continue;
                    }
                }
                else if ((c == '*' || c == '_') && i + 2 < end && line[i + 1] == c && line[i + 2] == c)
                {
                    int close = IndexOfThree(line, c, i + 3, end);
                    if (close >= 0)
                    {
                        ScanChars.Add(tokens, i, 3, TokenKind.Keyword);
                        if (close > i + 3)
                        {
                            ScanChars.Add(tokens, i + 3, close - (i + 3), TokenKind.Text);
                        }

                        ScanChars.Add(tokens, close, 3, TokenKind.Keyword);
                        i = close + 3;
                        continue;
                    }
                }
                else if ((c == '*' || c == '_') && i + 1 < end && line[i + 1] == c)
                {
                    int close = IndexOfTwo(line, c, i + 2, end);
                    if (close >= 0)
                    {
                        ScanChars.Add(tokens, i, 2, TokenKind.Keyword);
                        if (close > i + 2)
                        {
                            ScanChars.Add(tokens, i + 2, close - (i + 2), TokenKind.Text);
                        }

                        ScanChars.Add(tokens, close, 2, TokenKind.Keyword);
                        i = close + 2;
                        continue;
                    }
                }
                else if (c == '*' || c == '_')
                {
                    int close = IndexOfChar(line, c, i + 1, end);
                    if (close >= 0)
                    {
                        ScanChars.Add(tokens, i, 1, TokenKind.Keyword);
                        if (close > i + 1)
                        {
                            ScanChars.Add(tokens, i + 1, close - (i + 1), TokenKind.Text);
                        }

                        ScanChars.Add(tokens, close, 1, TokenKind.Keyword);
                        i = close + 1;
                        continue;
                    }
                }

                int t = i;
                i++;
                while (i < end)
                {
                    char n = line[i];
                    if (n == '`' || n == '*' || n == '_' || n == '~' || n == '[' || n == '!' || n == '<')
                    {
                        break;
                    }

                    i++;
                }

                ScanChars.Add(tokens, t, i - t, TokenKind.Text);
            }
        }

        private bool TryScanLink(string line, int openBracket, int end, List<Token> tokens, int bangStart, out int consumed)
        {
            consumed = openBracket;
            int closeBracket = IndexOfChar(line, ']', openBracket + 1, end);
            if (closeBracket < 0 || closeBracket + 1 >= end || line[closeBracket + 1] != '(')
            {
                return false;
            }

            int closeParen = IndexOfChar(line, ')', closeBracket + 2, end);
            if (closeParen < 0)
            {
                return false;
            }

            if (bangStart < openBracket)
            {
                ScanChars.Add(tokens, bangStart, openBracket - bangStart, TokenKind.Keyword);
            }

            ScanChars.Add(tokens, openBracket, 1, TokenKind.Keyword);
            if (closeBracket > openBracket + 1)
            {
                ScanChars.Add(tokens, openBracket + 1, closeBracket - (openBracket + 1), TokenKind.Local);
            }

            ScanChars.Add(tokens, closeBracket, 2, TokenKind.Keyword);
            if (closeParen > closeBracket + 2)
            {
                ScanChars.Add(tokens, closeBracket + 2, closeParen - (closeBracket + 2), TokenKind.String);
            }

            ScanChars.Add(tokens, closeParen, 1, TokenKind.Keyword);
            consumed = closeParen + 1;
            return true;
        }

        private static bool StartsAt(string line, int start, int end, string token)
        {
            if (start + token.Length > end)
            {
                return false;
            }

            int i = 0;
            while (i < token.Length)
            {
                char a = line[start + i];
                char b = token[i];
                if (a >= 'A' && a <= 'Z')
                {
                    a = (char)(a - 'A' + 'a');
                }

                if (a != b)
                {
                    return false;
                }

                i++;
            }

            return true;
        }

        private static int IndexOfChar(string line, char ch, int start, int end)
        {
            int i = start;
            while (i < end)
            {
                if (line[i] == ch)
                {
                    return i;
                }

                i++;
            }

            return -1;
        }

        private static int IndexOfTwo(string line, char ch, int start, int end)
        {
            int i = start;
            while (i + 1 < end)
            {
                if (line[i] == ch && line[i + 1] == ch)
                {
                    return i;
                }

                i++;
            }

            return -1;
        }

        private static int IndexOfThree(string line, char ch, int start, int end)
        {
            int i = start;
            while (i + 2 < end)
            {
                if (line[i] == ch && line[i + 1] == ch && line[i + 2] == ch)
                {
                    return i;
                }

                i++;
            }

            return -1;
        }
    }
}
