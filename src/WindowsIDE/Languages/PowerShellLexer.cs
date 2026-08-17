using System.Collections.Generic;

namespace WindowsIDE.Languages
{
    /// <summary>
    /// PowerShell の行レキサ。状態は Normal / BlockComment / HereStringDouble / HereStringSingle。
    /// </summary>
    public sealed class PowerShellLexer : ILineLexer
    {
        internal const int Normal = 0;
        internal const int BlockComment = 1;
        internal const int HereStringDouble = 2;
        internal const int HereStringSingle = 3;

        /// <summary>
        /// PowerShell の 1 行を走査する。
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

            int i = 0;
            int state = startState;
            if (state == BlockComment)
            {
                i = this.ScanBlockComment(line, 0, tokens, out state);
            }
            else if (state == HereStringDouble)
            {
                i = this.ScanHereBody(line, tokens, '"', HereStringDouble, out state);
            }
            else if (state == HereStringSingle)
            {
                i = this.ScanHereBody(line, tokens, '\'', HereStringSingle, out state);
            }

            while (i < line.Length && state == Normal)
            {
                char c = line[i];
                if (ScanChars.IsSpace(c))
                {
                    int start = i;
                    while (i < line.Length && ScanChars.IsSpace(line[i]))
                    {
                        i++;
                    }

                    ScanChars.Add(tokens, start, i - start, TokenKind.Text);
                    continue;
                }

                if (c == '<' && i + 1 < line.Length && line[i + 1] == '#')
                {
                    i = this.ScanBlockComment(line, i, tokens, out state);
                    continue;
                }

                if (c == '#')
                {
                    ScanChars.Add(tokens, i, line.Length - i, TokenKind.Comment);
                    i = line.Length;
                    continue;
                }

                if (c == '@' && i + 1 < line.Length && line[i + 1] == '"')
                {
                    ScanChars.Add(tokens, i, 2, TokenKind.String);
                    i += 2;
                    if (i < line.Length)
                    {
                        ScanChars.Add(tokens, i, line.Length - i, TokenKind.Text);
                        i = line.Length;
                    }

                    state = HereStringDouble;
                    continue;
                }

                if (c == '@' && i + 1 < line.Length && line[i + 1] == '\'')
                {
                    ScanChars.Add(tokens, i, 2, TokenKind.String);
                    i += 2;
                    if (i < line.Length)
                    {
                        ScanChars.Add(tokens, i, line.Length - i, TokenKind.Text);
                        i = line.Length;
                    }

                    state = HereStringSingle;
                    continue;
                }

                if (c == '"')
                {
                    i = this.ScanDoubleString(line, i, tokens);
                    continue;
                }

                if (c == '\'')
                {
                    i = this.ScanSingleString(line, i, tokens);
                    continue;
                }

                if (ScanChars.IsDigit(c))
                {
                    int end = ScanChars.ReadNumber(line, i);
                    ScanChars.Add(tokens, i, end - i, TokenKind.Number);
                    i = end;
                    continue;
                }

                if (ScanChars.IsIdentStart(c))
                {
                    int end = ScanChars.ReadIdent(line, i);
                    string word = line.Substring(i, end - i);
                    TokenKind kind = PowerShellKeywords.Set.Contains(word) ? TokenKind.Keyword : TokenKind.Text;
                    ScanChars.Add(tokens, i, end - i, kind);
                    i = end;
                    continue;
                }

                ScanChars.Add(tokens, i, 1, TokenKind.Text);
                i++;
            }

            if (state != Normal && i < line.Length)
            {
                TokenKind kind = (state == BlockComment) ? TokenKind.Comment : TokenKind.String;
                ScanChars.Add(tokens, i, line.Length - i, kind);
            }

            endState = state;
        }

        private int ScanHereBody(string line, List<Token> tokens, char quote, int continueState, out int state)
        {
            if (line.Length >= 2 && line[0] == quote && line[1] == '@')
            {
                ScanChars.Add(tokens, 0, 2, TokenKind.String);
                state = Normal;
                return 2;
            }

            if (line.Length > 0)
            {
                ScanChars.Add(tokens, 0, line.Length, TokenKind.String);
            }

            state = continueState;
            return line.Length;
        }

        private int ScanBlockComment(string line, int start, List<Token> tokens, out int state)
        {
            int i = start;
            if (i + 1 < line.Length && line[i] == '<' && line[i + 1] == '#')
            {
                i += 2;
            }

            while (i < line.Length)
            {
                if (line[i] == '#' && i + 1 < line.Length && line[i + 1] == '>')
                {
                    ScanChars.Add(tokens, start, (i + 2) - start, TokenKind.Comment);
                    state = Normal;
                    return i + 2;
                }

                i++;
            }

            ScanChars.Add(tokens, start, line.Length - start, TokenKind.Comment);
            state = BlockComment;
            return line.Length;
        }

        private int ScanDoubleString(string line, int start, List<Token> tokens)
        {
            int i = start + 1;
            while (i < line.Length)
            {
                if (line[i] == '`' && i + 1 < line.Length)
                {
                    i += 2;
                    continue;
                }

                if (line[i] == '"')
                {
                    ScanChars.Add(tokens, start, (i + 1) - start, TokenKind.String);
                    return i + 1;
                }

                i++;
            }

            ScanChars.Add(tokens, start, line.Length - start, TokenKind.String);
            return line.Length;
        }

        private int ScanSingleString(string line, int start, List<Token> tokens)
        {
            int i = start + 1;
            while (i < line.Length)
            {
                if (line[i] == '\'')
                {
                    if (i + 1 < line.Length && line[i + 1] == '\'')
                    {
                        i += 2;
                        continue;
                    }

                    ScanChars.Add(tokens, start, (i + 1) - start, TokenKind.String);
                    return i + 1;
                }

                i++;
            }

            ScanChars.Add(tokens, start, line.Length - start, TokenKind.String);
            return line.Length;
        }
    }
}
