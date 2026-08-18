using System.Collections.Generic;

namespace WindowsIDE.Languages
{
    /// <summary>
    /// 1 行を走査してトークンと終了状態を返す。
    /// </summary>
    public interface ILineLexer
    {
        /// <summary>
        /// 行を走査する。tokens へ追加し、endState に次行の開始状態を出す。
        /// </summary>
        /// <param name="line">対象行。null は空行。</param>
        /// <param name="startState">この行の開始状態。</param>
        /// <param name="tokens">出力先。呼び出し側が用意する。</param>
        /// <param name="endState">次行の開始状態。</param>
        void ScanLine(string line, int startState, List<Token> tokens, out int endState);
    }

    /// <summary>
    /// レキサ共通の文字判定。ASCII 識別子と数字だけを見る。
    /// </summary>
    internal static class ScanChars
    {
        public static bool IsDigit(char c)
        {
            return c >= '0' && c <= '9';
        }

        public static bool IsHexDigit(char c)
        {
            return IsDigit(c) || (c >= 'A' && c <= 'F') || (c >= 'a' && c <= 'f');
        }

        public static bool IsIdentStart(char c)
        {
            return (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || c == '_';
        }

        public static bool IsIdentPart(char c)
        {
            return IsIdentStart(c) || IsDigit(c);
        }

        public static bool IsNumberSuffix(char c)
        {
            return c == 'U' || c == 'u' || c == 'L' || c == 'l' ||
                c == 'F' || c == 'f' || c == 'D' || c == 'd' ||
                c == 'M' || c == 'm';
        }

        public static bool IsSpace(char c)
        {
            return c == ' ' || c == '\t';
        }

        public static void Add(List<Token> tokens, int start, int length, TokenKind kind)
        {
            if (tokens == null || length <= 0)
            {
                return;
            }

            tokens.Add(new Token(start, length, kind));
        }

        public static int ReadIdent(string line, int start)
        {
            int i = start;
            if (i >= line.Length || !IsIdentStart(line[i]))
            {
                return start;
            }

            i++;
            while (i < line.Length && IsIdentPart(line[i]))
            {
                i++;
            }

            return i;
        }

        public static int ReadNumber(string line, int start)
        {
            int i = start;
            if (i >= line.Length || !IsDigit(line[i]))
            {
                return start;
            }

            if (line[i] == '0' && i + 1 < line.Length && (line[i + 1] == 'x' || line[i + 1] == 'X'))
            {
                i += 2;
                while (i < line.Length && IsHexDigit(line[i]))
                {
                    i++;
                }
            }
            else
            {
                while (i < line.Length && IsDigit(line[i]))
                {
                    i++;
                }

                if (i < line.Length && line[i] == '.' && i + 1 < line.Length && IsDigit(line[i + 1]))
                {
                    i++;
                    while (i < line.Length && IsDigit(line[i]))
                    {
                        i++;
                    }
                }
            }

            while (i < line.Length && IsNumberSuffix(line[i]))
            {
                i++;
            }

            return i;
        }
    }
}
