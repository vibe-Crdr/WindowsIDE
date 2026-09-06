namespace WindowsIDE.Debug
{
    /// <summary>
    /// 停止時のコールスタック 1 件。行は 1 始まり。
    /// </summary>
    public sealed class DebugStackFrame
    {
        private string method;
        private string path;
        private int line;

        /// <summary>
        /// メソッド名・パス・行を渡す。
        /// </summary>
        /// <param name="method">メソッド名。null は空。</param>
        /// <param name="path">ソースパス。無ければ null。</param>
        /// <param name="line">1 始まりの行。無ければ 0。</param>
        public DebugStackFrame(string method, string path, int line)
        {
            this.method = (method == null) ? "" : method;
            this.path = path;
            this.line = line;
        }

        /// <summary>メソッド名。</summary>
        public string Method
        {
            get { return this.method; }
        }

        /// <summary>ソースパス。</summary>
        public string Path
        {
            get { return this.path; }
        }

        /// <summary>1 始まりの行。</summary>
        public int Line
        {
            get { return this.line; }
        }
    }
}
