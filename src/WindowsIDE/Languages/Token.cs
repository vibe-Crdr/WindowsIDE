namespace WindowsIDE.Languages
{
    /// <summary>
    /// 1 行内の字句。Start / Length は UTF-16 単位。
    /// </summary>
    public struct Token
    {
        /// <summary>行内の開始位置（UTF-16）。</summary>
        public int Start;

        /// <summary>長さ（UTF-16）。</summary>
        public int Length;

        /// <summary>字句の種類。</summary>
        public TokenKind Kind;

        /// <summary>
        /// 位置と種類からトークンを作る。
        /// </summary>
        /// <param name="start">行内の開始位置。</param>
        /// <param name="length">長さ。</param>
        /// <param name="kind">種類。</param>
        public Token(int start, int length, TokenKind kind)
        {
            this.Start = start;
            this.Length = length;
            this.Kind = kind;
        }
    }
}
