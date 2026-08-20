namespace WindowsIDE.Languages
{
    /// <summary>
    /// 1 行内の識別子分類。Start / Length は UTF-16。
    /// </summary>
    public struct ClassifySpan
    {
        /// <summary>行内開始位置。</summary>
        public int Start;

        /// <summary>長さ。</summary>
        public int Length;

        /// <summary>束縛または走査で決めた種類。</summary>
        public TokenKind Kind;

        /// <summary>
        /// 位置と種類からスパンを作る。
        /// </summary>
        /// <param name="start">開始。</param>
        /// <param name="length">長さ。</param>
        /// <param name="kind">種類。</param>
        public ClassifySpan(int start, int length, TokenKind kind)
        {
            this.Start = start;
            this.Length = length;
            this.Kind = kind;
        }
    }
}
