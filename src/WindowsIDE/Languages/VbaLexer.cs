using System.Collections.Generic;

namespace WindowsIDE.Languages
{
    /// <summary>
    /// VBA の行レキサ。状態は常に Normal。
    /// </summary>
    public sealed class VbaLexer : ILineLexer
    {
        /// <summary>
        /// VBA の 1 行を走査する。endState は 0。
        /// </summary>
        /// <param name="line">対象行。</param>
        /// <param name="startState">無視する。</param>
        /// <param name="tokens">出力先。</param>
        /// <param name="endState">常に 0。</param>
        public void ScanLine(string line, int startState, List<Token> tokens, out int endState)
        {
            endState = 0;
            if (line == null)
            {
                line = "";
            }

            int i = 0;
            while (i < line.Length)
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

                if (c == '\'')
                {
                    ScanChars.Add(tokens, i, line.Length - i, TokenKind.Comment);
                    i = line.Length;
                    continue;
                }

                if (this.IsRemStart(line, i))
                {
                    ScanChars.Add(tokens, i, line.Length - i, TokenKind.Comment);
                    i = line.Length;
                    continue;
                }

                if (c == '"')
                {
                    i = this.ScanString(line, i, tokens);
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
                    TokenKind kind = VbaKeywords.Set.Contains(word) ? TokenKind.Keyword : TokenKind.Text;
                    ScanChars.Add(tokens, i, end - i, kind);
                    i = end;
                    continue;
                }

                ScanChars.Add(tokens, i, 1, TokenKind.Text);
                i++;
            }

            IdentifierClassifier.Apply(LanguageKind.Vba, line, tokens);
        }

        private bool IsRemStart(string line, int index)
        {
            if (index > 0 && !ScanChars.IsSpace(line[index - 1]))
            {
                return false;
            }

            if (index + 3 > line.Length)
            {
                return false;
            }

            string word = line.Substring(index, 3);
            if (!word.Equals("Rem", System.StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (index + 3 < line.Length && ScanChars.IsIdentPart(line[index + 3]))
            {
                return false;
            }

            return true;
        }

        private int ScanString(string line, int start, List<Token> tokens)
        {
            int i = start + 1;
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
                    return i + 1;
                }

                i++;
            }

            ScanChars.Add(tokens, start, line.Length - start, TokenKind.String);
            return line.Length;
        }
    }
}
