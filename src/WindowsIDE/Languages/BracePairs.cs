using System.Collections.Generic;

namespace WindowsIDE.Languages
{
    /// <summary>
    /// 言語ごとの対括弧。C# / PowerShell は ()[]{}、VBA は ()、cmd / Plain は空。
    /// </summary>
    public static class BracePairs
    {
        private static readonly Dictionary<char, char> CsharpAndPowerShell = BuildPairs("()[]{}");
        private static readonly Dictionary<char, char> Vba = BuildPairs("()");
        private static readonly Dictionary<char, char> Empty = new Dictionary<char, char>();

        /// <summary>
        /// opener に対応する closer を返す。
        /// </summary>
        /// <param name="language">言語。</param>
        /// <param name="opener">開き括弧。</param>
        /// <param name="closer">閉じ括弧。</param>
        /// <returns>対応があるとき true。</returns>
        public static bool TryGetCloser(LanguageKind language, char opener, out char closer)
        {
            return Table(language).TryGetValue(opener, out closer);
        }

        /// <summary>
        /// opener として扱う文字なら true。
        /// </summary>
        /// <param name="language">言語。</param>
        /// <param name="ch">文字。</param>
        /// <returns>開き括弧のとき true。</returns>
        public static bool IsOpener(LanguageKind language, char ch)
        {
            return Table(language).ContainsKey(ch);
        }

        /// <summary>
        /// closer として扱う文字なら true。
        /// </summary>
        /// <param name="language">言語。</param>
        /// <param name="ch">文字。</param>
        /// <returns>閉じ括弧のとき true。</returns>
        public static bool IsCloser(LanguageKind language, char ch)
        {
            Dictionary<char, char> table = Table(language);
            foreach (KeyValuePair<char, char> pair in table)
            {
                if (pair.Value == ch)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 開きまたは閉じの括弧なら true。
        /// </summary>
        /// <param name="language">言語。</param>
        /// <param name="ch">文字。</param>
        /// <returns>対括弧の対象なら true。</returns>
        public static bool IsBrace(LanguageKind language, char ch)
        {
            return IsOpener(language, ch) || IsCloser(language, ch);
        }

        internal static bool CloserMatches(LanguageKind language, char opener, char closer)
        {
            char expected;
            if (!TryGetCloser(language, opener, out expected))
            {
                return false;
            }

            return expected == closer;
        }

        private static Dictionary<char, char> Table(LanguageKind language)
        {
            switch (language)
            {
                case LanguageKind.CSharp:
                case LanguageKind.PowerShell:
                    return CsharpAndPowerShell;
                case LanguageKind.Vba:
                    return Vba;
                default:
                    return Empty;
            }
        }

        private static Dictionary<char, char> BuildPairs(string text)
        {
            Dictionary<char, char> map = new Dictionary<char, char>();
            if (text == null)
            {
                return map;
            }

            int i = 0;
            while (i + 1 < text.Length)
            {
                map[text[i]] = text[i + 1];
                i += 2;
            }

            return map;
        }
    }
}
