namespace WindowsIDE.Languages
{
    /// <summary>
    /// キャレット位置の識別子。Name は C# の先頭 @ を除く。PS 変数は非修飾名。
    /// </summary>
    public sealed class IdentifierHit
    {
        private string name;
        private int line;
        private int column;
        private int length;
        private bool isPowerShellVariable;
        private bool isPowerShellDrive;

        /// <summary>
        /// ヒットを作る。PowerShell 変数ではない。
        /// </summary>
        /// <param name="name">照合用の名前。</param>
        /// <param name="line">0 始まりの行。</param>
        /// <param name="column">スパン開始列。</param>
        /// <param name="length">スパン長。</param>
        public IdentifierHit(string name, int line, int column, int length)
            : this(name, line, column, length, false)
        {
        }

        /// <summary>
        /// ヒットを作る。
        /// </summary>
        /// <param name="name">照合用の名前。</param>
        /// <param name="line">0 始まりの行。</param>
        /// <param name="column">スパン開始列。</param>
        /// <param name="length">スパン長。</param>
        /// <param name="isPowerShellVariable">PowerShell の <c>$</c> 変数形なら true。</param>
        public IdentifierHit(string name, int line, int column, int length, bool isPowerShellVariable)
            : this(name, line, column, length, isPowerShellVariable, false)
        {
        }

        /// <summary>
        /// ヒットを作る。ドライブ修飾の変数形は解決しない。
        /// </summary>
        /// <param name="name">照合用の名前。</param>
        /// <param name="line">0 始まりの行。</param>
        /// <param name="column">スパン開始列。</param>
        /// <param name="length">スパン長。</param>
        /// <param name="isPowerShellVariable">PowerShell の <c>$</c> 変数形なら true。</param>
        /// <param name="isPowerShellDrive">スコープ以外のドライブ修飾なら true。</param>
        public IdentifierHit(string name, int line, int column, int length, bool isPowerShellVariable, bool isPowerShellDrive)
        {
            this.name = (name == null) ? "" : name;
            this.line = line;
            this.column = column;
            this.length = length;
            this.isPowerShellVariable = isPowerShellVariable;
            this.isPowerShellDrive = isPowerShellDrive;
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

        /// <summary>PowerShell の <c>$</c> 変数形。</summary>
        public bool IsPowerShellVariable
        {
            get { return this.isPowerShellVariable; }
        }

        /// <summary>スコープ以外のドライブ修飾（<c>$env:</c> 等）。</summary>
        public bool IsPowerShellDrive
        {
            get { return this.isPowerShellDrive; }
        }
    }
}
