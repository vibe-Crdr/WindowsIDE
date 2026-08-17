namespace WindowsIDE.Ui.Fonts
{
    /// <summary>
    /// グリフを半角フォント（Cascadia Mono）か全角フォント（源ノ角）かに分ける。
    /// </summary>
    public static class GlyphClassifier
    {
        /// <summary>
        /// 指定位置の文字が半角フォントなら true。サロゲートは全角側。
        /// </summary>
        /// <param name="text">対象文字列。</param>
        /// <param name="index">UTF-16 インデックス。</param>
        /// <param name="codeUnitCount">このグリフが消費する UTF-16 単位数。</param>
        /// <returns>Cascadia Mono を使うなら true。</returns>
        public static bool UseHalfWidthFont(string text, int index, out int codeUnitCount)
        {
            codeUnitCount = 1;
            if (text == null || index < 0 || index >= text.Length)
            {
                return true;
            }

            char c = text[index];
            if (char.IsHighSurrogate(c))
            {
                if (index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
                {
                    codeUnitCount = 2;
                }
                return false;
            }

            if (char.IsLowSurrogate(c))
            {
                return false;
            }

            if (c >= '\u0020' && c <= '\u007E')
            {
                return true;
            }

            if (c >= '\uFF61' && c <= '\uFF9F')
            {
                return true;
            }

            return false;
        }
    }
}
