using System.Collections.Generic;

namespace WindowsIDE.Languages
{
    /// <summary>
    /// C# の行レキサ。状態は Normal / BlockComment / VerbatimString。
    /// </summary>
    public sealed class CSharpLexer : ILineLexer
    {
        internal const int Normal = 0;
        internal const int BlockComment = 1;
        internal const int VerbatimString = 2;

        private static readonly KeywordSet Directives = new KeywordSet(
            new string[]
            {
                "if",
                "else",
                "elif",
                "endif",
                "define",
                "undef",
                "warning",
                "error",
                "line",
                "region",
                "endregion",
                "pragma",
                "nullable"
            },
            System.StringComparer.Ordinal);

        /// <summary>
        /// C# の 1 行を走査する。
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
            else if (state == VerbatimString)
            {
                i = this.ScanVerbatim(line, 0, tokens, out state);
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

                if (c == '/' && i + 1 < line.Length && line[i + 1] == '/')
                {
                    ScanChars.Add(tokens, i, line.Length - i, TokenKind.Comment);
                    i = line.Length;
                    continue;
                }

                if (c == '/' && i + 1 < line.Length && line[i + 1] == '*')
                {
                    i = this.ScanBlockComment(line, i, tokens, out state);
                    continue;
                }

                if (c == '@' && i + 1 < line.Length && line[i + 1] == '"')
                {
                    i = this.ScanVerbatim(line, i, tokens, out state);
                    continue;
                }

                if (c == '@' && i + 1 < line.Length && ScanChars.IsIdentStart(line[i + 1]))
                {
                    int end = ScanChars.ReadIdent(line, i + 1);
                    ScanChars.Add(tokens, i, end - i, TokenKind.Text);
                    i = end;
                    continue;
                }

                if (c == '"')
                {
                    i = this.ScanRegularString(line, i, tokens);
                    continue;
                }

                if (c == '\'')
                {
                    i = this.ScanChar(line, i, tokens);
                    continue;
                }

                if (c == '#' && this.IsDirectivePosition(line, i))
                {
                    i = this.ScanDirective(line, i, tokens);
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
                    TokenKind kind = CSharpKeywords.Set.Contains(word) ? TokenKind.Keyword : TokenKind.Text;
                    ScanChars.Add(tokens, i, end - i, kind);
                    i = end;
                    continue;
                }

                ScanChars.Add(tokens, i, 1, TokenKind.Text);
                i++;
            }

            if (state != Normal && i < line.Length)
            {
                ScanChars.Add(tokens, i, line.Length - i, state == BlockComment ? TokenKind.Comment : TokenKind.String);
            }

            endState = state;
        }

        private bool IsDirectivePosition(string line, int hashIndex)
        {
            for (int i = 0; i < hashIndex; i++)
            {
                if (!ScanChars.IsSpace(line[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private int ScanDirective(string line, int hashIndex, List<Token> tokens)
        {
            ScanChars.Add(tokens, hashIndex, 1, TokenKind.Text);
            int i = hashIndex + 1;
            while (i < line.Length && ScanChars.IsSpace(line[i]))
            {
                i++;
            }

            if (i > hashIndex + 1)
            {
                ScanChars.Add(tokens, hashIndex + 1, i - (hashIndex + 1), TokenKind.Text);
            }

            if (i < line.Length && ScanChars.IsIdentStart(line[i]))
            {
                int end = ScanChars.ReadIdent(line, i);
                string word = line.Substring(i, end - i);
                TokenKind kind = Directives.Contains(word) ? TokenKind.Keyword : TokenKind.Text;
                ScanChars.Add(tokens, i, end - i, kind);
                i = end;
            }

            while (i < line.Length)
            {
                if (line[i] == '/' && i + 1 < line.Length && line[i + 1] == '/')
                {
                    ScanChars.Add(tokens, i, line.Length - i, TokenKind.Comment);
                    return line.Length;
                }

                int start = i;
                while (i < line.Length && !(line[i] == '/' && i + 1 < line.Length && line[i + 1] == '/'))
                {
                    i++;
                }

                ScanChars.Add(tokens, start, i - start, TokenKind.Text);
            }

            return i;
        }

        private int ScanBlockComment(string line, int start, List<Token> tokens, out int state)
        {
            int i = start;
            if (i + 1 < line.Length && line[i] == '/' && line[i + 1] == '*')
            {
                i += 2;
            }

            while (i < line.Length)
            {
                if (line[i] == '*' && i + 1 < line.Length && line[i + 1] == '/')
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

        private int ScanVerbatim(string line, int start, List<Token> tokens, out int state)
        {
            int i = start;
            if (i + 1 < line.Length && line[i] == '@' && line[i + 1] == '"')
            {
                i += 2;
            }

            while (i < line.Length)
            {
                if (line[i] == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        i += 2;
                        continue;
                    }

                    ScanChars.Add(tokens, start, (i + 1) - start, TokenKind.String);
                    state = Normal;
                    return i + 1;
                }

                i++;
            }

            ScanChars.Add(tokens, start, line.Length - start, TokenKind.String);
            state = VerbatimString;
            return line.Length;
        }

        private int ScanRegularString(string line, int start, List<Token> tokens)
        {
            int i = start + 1;
            while (i < line.Length)
            {
                if (line[i] == '\\' && i + 1 < line.Length)
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

        private int ScanChar(string line, int start, List<Token> tokens)
        {
            int i = start + 1;
            while (i < line.Length)
            {
                if (line[i] == '\\' && i + 1 < line.Length)
                {
                    i += 2;
                    continue;
                }

                if (line[i] == '\'')
                {
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
