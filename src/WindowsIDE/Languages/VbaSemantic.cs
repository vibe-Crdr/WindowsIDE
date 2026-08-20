using System;
using System.Collections.Generic;
using System.IO;
using WindowsIDE.Editor;

namespace WindowsIDE.Languages
{
    /// <summary>
    /// VBA の型名と文頭呼び出しを Type / Method にする。Excel COM は起動しない。
    /// </summary>
    public static class VbaSemantic
    {
        private static readonly string[] ExcelTypes = new string[]
        {
            "Application",
            "Workbook",
            "Workbooks",
            "Worksheet",
            "Worksheets",
            "Range",
            "Cells",
            "Chart",
            "Shape"
        };

        /// <summary>
        /// 宣言と As 型、文頭呼び出しを overlay へ書く。
        /// </summary>
        /// <param name="buffer">本文。</param>
        /// <param name="workspaceRoot">.cls 名を集めるルート。</param>
        /// <param name="overlay">行ごとのスパン。</param>
        public static void Classify(TextBuffer buffer, string workspaceRoot, List<ClassifySpan>[] overlay)
        {
            if (buffer == null || overlay == null)
            {
                return;
            }

            HashSet<string> types = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < ExcelTypes.Length; i++)
            {
                types.Add(ExcelTypes[i]);
            }

            AddClsNames(workspaceRoot, types);
            VbaLexer lexer = new VbaLexer();
            List<Token> tokens = new List<Token>();
            int state = 0;
            for (int line = 0; line < buffer.LineCount; line++)
            {
                string text = buffer.GetLine(line);
                tokens.Clear();
                int endState;
                lexer.ScanLine(text, state, tokens, out endState);
                state = endState;
                CollectTypes(text, tokens, types);
            }

            state = 0;
            for (int line = 0; line < buffer.LineCount; line++)
            {
                string text = buffer.GetLine(line);
                tokens.Clear();
                int endState;
                lexer.ScanLine(text, state, tokens, out endState);
                state = endState;
                ApplyLine(text, tokens, types, overlay[line]);
            }
        }

        private static void AddClsNames(string root, HashSet<string> types)
        {
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
            {
                return;
            }

            string[] files;
            try
            {
                files = Directory.GetFiles(root, "*.cls", SearchOption.AllDirectories);
            }
            catch (Exception)
            {
                return;
            }

            for (int i = 0; i < files.Length; i++)
            {
                string name = Path.GetFileNameWithoutExtension(files[i]);
                if (!string.IsNullOrEmpty(name))
                {
                    types.Add(name);
                }
            }
        }

        private static void CollectTypes(string line, List<Token> tokens, HashSet<string> types)
        {
            for (int i = 0; i < tokens.Count; i++)
            {
                Token token = tokens[i];
                if (token.Kind != TokenKind.Keyword)
                {
                    continue;
                }

                string word = line.Substring(token.Start, token.Length);
                if (!word.Equals("Type", StringComparison.OrdinalIgnoreCase) &&
                    !word.Equals("Enum", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (i > 0)
                {
                    Token prev = PreviousNonSpace(tokens, line, i);
                    if (prev.Length > 0)
                    {
                        string prevWord = line.Substring(prev.Start, prev.Length);
                        if (prevWord.Equals("End", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }
                    }
                }

                int next = NextNonSpace(tokens, line, i);
                if (next < 0)
                {
                    continue;
                }

                Token ident = tokens[next];
                if (ident.Kind == TokenKind.Keyword || ident.Kind == TokenKind.String || ident.Kind == TokenKind.Comment)
                {
                    continue;
                }

                types.Add(line.Substring(ident.Start, ident.Length));
            }
        }

        private static void ApplyLine(string line, List<Token> tokens, HashSet<string> types, List<ClassifySpan> dest)
        {
            bool firstIdent = true;
            for (int i = 0; i < tokens.Count; i++)
            {
                Token token = tokens[i];
                if (token.Kind == TokenKind.Keyword || token.Kind == TokenKind.String ||
                    token.Kind == TokenKind.Comment || token.Kind == TokenKind.Number)
                {
                    if (token.Kind == TokenKind.Keyword)
                    {
                        firstIdent = false;
                    }

                    continue;
                }

                if (IsSpace(line, token))
                {
                    continue;
                }

                if (!IsIdent(line, token))
                {
                    firstIdent = false;
                    continue;
                }

                string name = line.Substring(token.Start, token.Length);
                int prev = PreviousNonSpaceIndex(tokens, line, i);
                int next = NextNonSpace(tokens, line, i);
                if (prev >= 0 && tokens[prev].Kind == TokenKind.Keyword)
                {
                    string kw = line.Substring(tokens[prev].Start, tokens[prev].Length);
                    if (kw.Equals("As", StringComparison.OrdinalIgnoreCase) ||
                        kw.Equals("New", StringComparison.OrdinalIgnoreCase) ||
                        kw.Equals("Implements", StringComparison.OrdinalIgnoreCase))
                    {
                        dest.Add(new ClassifySpan(token.Start, token.Length, TokenKind.Type));
                        firstIdent = false;
                        continue;
                    }
                }

                if (types.Contains(name))
                {
                    dest.Add(new ClassifySpan(token.Start, token.Length, TokenKind.Type));
                    firstIdent = false;
                    continue;
                }

                if (firstIdent && next >= 0)
                {
                    Token ntok = tokens[next];
                    if (ntok.Kind == TokenKind.String || IsIdent(line, ntok))
                    {
                        dest.Add(new ClassifySpan(token.Start, token.Length, TokenKind.Method));
                    }
                }

                firstIdent = false;
            }
        }

        private static Token PreviousNonSpace(List<Token> tokens, string line, int index)
        {
            int i = PreviousNonSpaceIndex(tokens, line, index);
            if (i < 0)
            {
                return new Token(0, 0, TokenKind.Text);
            }

            return tokens[i];
        }

        private static int PreviousNonSpaceIndex(List<Token> tokens, string line, int index)
        {
            for (int i = index - 1; i >= 0; i--)
            {
                if (!IsSpace(line, tokens[i]))
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
                if (!IsSpace(line, tokens[i]))
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool IsSpace(string line, Token token)
        {
            if (token.Kind != TokenKind.Text)
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

            return token.Length > 0;
        }

        private static bool IsIdent(string line, Token token)
        {
            if (token.Length <= 0 || token.Start < 0 || token.Start + token.Length > line.Length)
            {
                return false;
            }

            if (!ScanChars.IsIdentStart(line[token.Start]))
            {
                return false;
            }

            for (int i = 1; i < token.Length; i++)
            {
                if (!ScanChars.IsIdentPart(line[token.Start + i]))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
