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

            this.ScanInline(line, 0, line.Length, tokens);
            endState = Normal;
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
                    if (n == '`' || n == '*' || n == '_')
                    {
                        break;
                    }

                    i++;
                }

                ScanChars.Add(tokens, t, i - t, TokenKind.Text);
            }
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
    }
}
