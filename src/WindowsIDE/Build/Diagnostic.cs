namespace WindowsIDE.Build
{
    /// <summary>
    /// csc または IDE 合成の 1 件。行・列は csc と同じ 1 始まり。無いときは 0。Code が CSxxxx なのは csc のみ。
    /// </summary>
    public sealed class Diagnostic
    {
        private string filePath;
        private int line;
        private int column;
        private bool isError;
        private string code;
        private string message;

        /// <summary>対象ファイル。無ければ null。</summary>
        public string FilePath
        {
            get { return this.filePath; }
        }

        /// <summary>1 始まりの行。無ければ 0。</summary>
        public int Line
        {
            get { return this.line; }
        }

        /// <summary>1 始まりの列。無ければ 0。</summary>
        public int Column
        {
            get { return this.column; }
        }

        /// <summary>error / fatal / 合成なら true。warning なら false。</summary>
        public bool IsError
        {
            get { return this.isError; }
        }

        /// <summary>csc の CSxxxx。合成は null。</summary>
        public string Code
        {
            get { return this.code; }
        }

        /// <summary>本文。</summary>
        public string Message
        {
            get { return this.message; }
        }

        /// <summary>
        /// csc の error または warning を作る。
        /// </summary>
        /// <param name="filePath">フルパス。無ければ null。</param>
        /// <param name="line">1 始まり。無ければ 0。</param>
        /// <param name="column">1 始まり。無ければ 0。</param>
        /// <param name="isError">error なら true。</param>
        /// <param name="code">CSxxxx。不明なら null。</param>
        /// <param name="message">本文。</param>
        /// <returns>診断。</returns>
        public static Diagnostic FromCompiler(string filePath, int line, int column, bool isError, string code, string message)
        {
            Diagnostic d = new Diagnostic();
            d.filePath = filePath;
            d.line = line;
            d.column = column;
            d.isError = isError;
            d.code = code;
            d.message = (message == null) ? "" : message;
            return d;
        }

        /// <summary>
        /// IDE 側の 1 件。Code は CSxxxx にしない。
        /// </summary>
        /// <param name="message">表示する文。</param>
        /// <returns>error 扱いの合成診断。</returns>
        public static Diagnostic CreateSynthetic(string message)
        {
            return CreateSynthetic(null, message);
        }

        /// <summary>
        /// ファイル付きの IDE 合成。Code は CSxxxx にしない。
        /// </summary>
        /// <param name="filePath">対象パス。無ければ null。</param>
        /// <param name="message">表示する文。</param>
        /// <returns>error 扱いの合成診断。</returns>
        public static Diagnostic CreateSynthetic(string filePath, string message)
        {
            Diagnostic d = new Diagnostic();
            d.filePath = filePath;
            d.line = 0;
            d.column = 0;
            d.isError = true;
            d.code = null;
            d.message = (message == null) ? "" : message;
            return d;
        }
    }
}
