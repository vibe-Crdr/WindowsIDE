namespace WindowsIDE.Terminal
{
    /// <summary>
    /// 端末セル幅。半角 1、全角 2。Ui.Fonts には依存しない。
    /// </summary>
    public static class CellWidth
    {
        /// <summary>
        /// 1 文字のセル幅。ASCII 印字と半角カナは 1、それ以外は 2。制御は 0。
        /// </summary>
        /// <param name="c">対象文字。</param>
        /// <returns>0 / 1 / 2。</returns>
        public static int Of(char c)
        {
            if (c < ' ')
            {
                return 0;
            }

            if (c == '\u007F')
            {
                return 0;
            }

            if (c >= '\u0020' && c <= '\u007E')
            {
                return 1;
            }

            if (c >= '\uFF61' && c <= '\uFF9F')
            {
                return 1;
            }

            return 2;
        }

        /// <summary>
        /// UTF-16 位置のセル幅。サロゲートは 2 セル。
        /// </summary>
        /// <param name="text">対象。null は 0。</param>
        /// <param name="index">UTF-16 インデックス。</param>
        /// <param name="codeUnitCount">消費する UTF-16 単位。</param>
        /// <returns>0 / 1 / 2。</returns>
        public static int Of(string text, int index, out int codeUnitCount)
        {
            codeUnitCount = 1;
            if (text == null || index < 0 || index >= text.Length)
            {
                return 0;
            }

            char c = text[index];
            if (char.IsHighSurrogate(c))
            {
                if (index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
                {
                    codeUnitCount = 2;
                }

                return 2;
            }

            if (char.IsLowSurrogate(c))
            {
                return 2;
            }

            return Of(c);
        }
    }
}
