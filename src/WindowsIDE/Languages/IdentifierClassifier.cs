using System;
using System.Collections.Generic;

namespace WindowsIDE.Languages
{
    /// <summary>
    /// 行内の Text 識別子を Local / Instance / Method に再分類する。
    /// Keyword / String / Comment / Number は変えない。Plain と C# は何もしない（C# は束縛オーバーレイ）。
    /// </summary>
    public static class IdentifierClassifier
    {
        /// <param name="language">言語。Plain は no-op。</param>
        /// <param name="line">対象行。null は空文字。</param>
        /// <param name="tokens">ScanLine が出したリスト。要素の Kind をその場で書き換える。</param>
        public static void Apply(LanguageKind language, string line, List<Token> tokens)
        {
            if (tokens == null)
            {
                return;
            }

            if (line == null)
            {
                line = "";
            }

            if (language == LanguageKind.Plain || language == LanguageKind.CSharp)
            {
                return;
            }

            for (int i = 0; i < tokens.Count; i++)
            {
                Token token = tokens[i];
                if (token.Kind == TokenKind.Keyword ||
                    token.Kind == TokenKind.String ||
                    token.Kind == TokenKind.Comment ||
                    token.Kind == TokenKind.Number)
                {
                    continue;
                }

                if (!IsIdentifier(language, line, token))
                {
                    continue;
                }

                int prev = PreviousNonSpace(tokens, line, i);
                if (prev >= 0 && IsTypeIntro(language, line, tokens[prev]))
                {
                    continue;
                }

                if (ApplyLanguageOverride(language, line, tokens, i, prev))
                {
                    continue;
                }

                int next = NextNonSpace(tokens, line, i);
                if (next >= 0 && IsChar(line, tokens[next], '('))
                {
                    SetKind(tokens, i, TokenKind.Method);
                    continue;
                }

                if (prev >= 0 && IsChar(line, tokens[prev], '.'))
                {
                    if (next >= 0 && IsChar(line, tokens[next], '('))
                    {
                        SetKind(tokens, i, TokenKind.Method);
                    }
                    else
                    {
                        SetKind(tokens, i, TokenKind.Instance);
                    }

                    continue;
                }

                SetKind(tokens, i, TokenKind.Local);
            }
        }

        private static bool ApplyLanguageOverride(LanguageKind language, string line, List<Token> tokens, int index, int prev)
        {
            if (language == LanguageKind.Vba)
            {
                return ApplyVbaOverride(line, tokens, index, prev);
            }

            if (language == LanguageKind.PowerShell)
            {
                return ApplyPowerShellOverride(line, tokens, index, prev);
            }

            if (language == LanguageKind.Cmd)
            {
                return ApplyCmdOverride(line, tokens, index, prev);
            }

            return false;
        }

        private static bool ApplyVbaOverride(string line, List<Token> tokens, int index, int prev)
        {
            if (prev < 0)
            {
                return false;
            }

            Token previous = tokens[prev];
            if (previous.Kind != TokenKind.Keyword)
            {
                return false;
            }

            string word = TokenText(line, previous);
            if (EqualsIgnoreCase(word, "Sub") ||
                EqualsIgnoreCase(word, "Function") ||
                EqualsIgnoreCase(word, "Property") ||
                EqualsIgnoreCase(word, "Call"))
            {
                SetKind(tokens, index, TokenKind.Method);
                return true;
            }

            if (EqualsIgnoreCase(word, "Get") ||
                EqualsIgnoreCase(word, "Set") ||
                EqualsIgnoreCase(word, "Let"))
            {
                int before = PreviousNonSpace(tokens, line, prev);
                if (before >= 0)
                {
                    Token owner = tokens[before];
                    if (owner.Kind == TokenKind.Keyword &&
                        EqualsIgnoreCase(TokenText(line, owner), "Property"))
                    {
                        SetKind(tokens, index, TokenKind.Method);
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool ApplyPowerShellOverride(string line, List<Token> tokens, int index, int prev)
        {
            if (prev >= 0 && IsChar(line, tokens[prev], '$'))
            {
                SetKind(tokens, index, TokenKind.Local);
                return true;
            }

            if (prev >= 0 &&
                tokens[prev].Kind == TokenKind.Keyword &&
                EqualsIgnoreCase(TokenText(line, tokens[prev]), "function"))
            {
                SetKind(tokens, index, TokenKind.Method);
                return true;
            }

            int hyphen = -1;
            int other = -1;
            if (index + 2 < tokens.Count &&
                IsChar(line, tokens[index + 1], '-') &&
                IsIdentShape(LanguageKind.PowerShell, line, tokens[index + 2]))
            {
                hyphen = index + 1;
                other = index + 2;
            }
            else if (index >= 2 &&
                IsChar(line, tokens[index - 1], '-') &&
                IsIdentShape(LanguageKind.PowerShell, line, tokens[index - 2]))
            {
                hyphen = index - 1;
                other = index - 2;
            }

            if (hyphen >= 0 && other >= 0 && !IsSpaceToken(line, tokens[hyphen]))
            {
                SetKind(tokens, index, TokenKind.Method);
                if (IsIdentShape(LanguageKind.PowerShell, line, tokens[other]))
                {
                    SetKind(tokens, other, TokenKind.Method);
                }

                return true;
            }

            return false;
        }

        private static bool ApplyCmdOverride(string line, List<Token> tokens, int index, int prev)
        {
            if (prev < 0)
            {
                return false;
            }

            if (IsChar(line, tokens[prev], '%') || IsChar(line, tokens[prev], '!'))
            {
                SetKind(tokens, index, TokenKind.Local);
                return true;
            }

            if (IsChar(line, tokens[prev], ':'))
            {
                SetKind(tokens, index, TokenKind.Method);
                return true;
            }

            return false;
        }

        private static bool IsTypeIntro(LanguageKind language, string line, Token token)
        {
            if (token.Kind != TokenKind.Keyword)
            {
                return false;
            }

            string word = TokenText(line, token);
            if (language == LanguageKind.CSharp)
            {
                return word == "class" ||
                    word == "struct" ||
                    word == "interface" ||
                    word == "enum" ||
                    word == "delegate" ||
                    word == "as" ||
                    word == "is" ||
                    word == "new";
            }

            if (language == LanguageKind.Vba)
            {
                return EqualsIgnoreCase(word, "As") ||
                    EqualsIgnoreCase(word, "New") ||
                    EqualsIgnoreCase(word, "Type") ||
                    EqualsIgnoreCase(word, "Enum") ||
                    EqualsIgnoreCase(word, "Implements");
            }

            if (language == LanguageKind.PowerShell)
            {
                return EqualsIgnoreCase(word, "class") ||
                    EqualsIgnoreCase(word, "enum");
            }

            return false;
        }

        private static bool IsIdentifier(LanguageKind language, string line, Token token)
        {
            return token.Kind == TokenKind.Text && IsIdentShape(language, line, token);
        }

        private static bool IsIdentShape(LanguageKind language, string line, Token token)
        {
            if (token.Length <= 0 || token.Start < 0 || token.Start + token.Length > line.Length)
            {
                return false;
            }

            int start = token.Start;
            int end = token.Start + token.Length;
            if (language == LanguageKind.CSharp &&
                token.Length >= 2 &&
                line[start] == '@' &&
                ScanChars.IsIdentStart(line[start + 1]))
            {
                for (int i = start + 2; i < end; i++)
                {
                    if (!ScanChars.IsIdentPart(line[i]))
                    {
                        return false;
                    }
                }

                return true;
            }

            if (!ScanChars.IsIdentStart(line[start]))
            {
                return false;
            }

            for (int i = start + 1; i < end; i++)
            {
                if (!ScanChars.IsIdentPart(line[i]))
                {
                    return false;
                }
            }

            return true;
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
            if (token.Kind != TokenKind.Text || token.Length <= 0)
            {
                return false;
            }

            if (token.Start < 0 || token.Start + token.Length > line.Length)
            {
                return false;
            }

            for (int i = 0; i < token.Length; i++)
            {
                if (!ScanChars.IsSpace(line[token.Start + i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static int PreviousNonSpace(List<Token> tokens, string line, int index)
        {
            for (int i = index - 1; i >= 0; i--)
            {
                if (!IsSpaceToken(line, tokens[i]))
                {
                    return i;
                }
            }

            return -1;
        }

        private static int NextNonSpace(List<Token> tokens, string line, int index)
        {
            for (int i = index + 1; i < tokens.Count; i++)
            {
                if (!IsSpaceToken(line, tokens[i]))
                {
                    return i;
                }
            }

            return -1;
        }

        private static string TokenText(string line, Token token)
        {
            if (token.Length <= 0 || token.Start < 0 || token.Start + token.Length > line.Length)
            {
                return "";
            }

            return line.Substring(token.Start, token.Length);
        }

        private static void SetKind(List<Token> tokens, int index, TokenKind kind)
        {
            Token t = tokens[index];
            t.Kind = kind;
            tokens[index] = t;
        }

        private static bool EqualsIgnoreCase(string a, string b)
        {
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }
    }
}
