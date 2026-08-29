namespace WindowsIDE.Languages
{
    /// <summary>
    /// キャレット位置の識別子。Name は C# の先頭 @ を除く。
    /// </summary>
    public sealed class IdentifierHit
    {
        private string name;
        private int line;
        private int column;
        private int length;

        /// <summary>
        /// ヒットを作る。
        /// </summary>
        /// <param name="name">照合用の名前。</param>
        /// <param name="line">0 始まりの行。</param>
        /// <param name="column">スパン開始列。</param>
        /// <param name="length">スパン長。</param>
        public IdentifierHit(string name, int line, int column, int length)
        {
            this.name = (name == null) ? "" : name;
            this.line = line;
            this.column = column;
            this.length = length;
        }

        /// <summary>照合用の名前。</summary>
        public string Name
        {
            get { return this.name; }
        }

        /// <summary>0 始まりの行。</summary>
        public int Line
        {
            get { return this.line; }
        }

        /// <summary>スパン開始列（0 始まり）。</summary>
        public int Column
        {
            get { return this.column; }
        }

        /// <summary>スパン長。</summary>
        public int Length
        {
            get { return this.length; }
        }
    }
}
