namespace WindowsIDE.Vba
{
    /// <summary>
    /// VBA の Excel 側コンポーネント名。先頭英字、残り [A-Za-z0-9_]、長さ 1–31。切り詰めない。
    /// </summary>
    public static class VbaIdentifier
    {
        /// <summary>
        /// 識別子として妥当なら true。空・先頭数字/記号・32 文字以上は false。
        /// </summary>
        /// <param name="name">調べる名前。</param>
        /// <returns>妥当なら true。</returns>
        public static bool IsValid(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            if (name.Length < 1 || name.Length > 31)
            {
                return false;
            }

            char first = name[0];
            if (!IsAsciiLetter(first))
            {
                return false;
            }

            for (int i = 1; i < name.Length; i++)
            {
                char c = name[i];
                if (!IsAsciiLetter(c) && !IsDigit(c) && c != '_')
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsAsciiLetter(char c)
        {
            return (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');
        }

        private static bool IsDigit(char c)
        {
            return c >= '0' && c <= '9';
        }
    }
}
