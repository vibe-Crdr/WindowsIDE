using System;

namespace WindowsIDE.Debug
{
    /// <summary>
    /// 停止時のローカル 1 件。値は展開しない。
    /// </summary>
    public sealed class DebugVariable
    {
        private string name;
        private string text;

        /// <summary>
        /// 名前と表示文字列を渡す。
        /// </summary>
        /// <param name="name">変数名（$ なし）。</param>
        /// <param name="text">ToString 結果。null は空。</param>
        public DebugVariable(string name, string text)
        {
            this.name = (name == null) ? "" : name;
            this.text = (text == null) ? "" : text;
        }

        /// <summary>変数名。</summary>
        public string Name
        {
            get { return this.name; }
        }

        /// <summary>表示文字列。</summary>
        public string Text
        {
            get { return this.text; }
        }
    }

    /// <summary>
    /// DebuggerStop で停止したとき。
    /// </summary>
    public sealed class DebugStoppedEventArgs : EventArgs
    {
        private int generation;
        private string path;
        private int line;
        private DebugVariable[] variables;
        private DebugStackFrame[] frames;

        /// <summary>
        /// 世代・パス・1 始まり行・ローカルを渡す。Frames は空。
        /// </summary>
        /// <param name="generation">Start に渡した世代。</param>
        /// <param name="path">停止スクリプト。無ければ null。</param>
        /// <param name="line">1 始まりの行。無ければ 0。</param>
        /// <param name="variables">ローカル。null は空。</param>
        public DebugStoppedEventArgs(int generation, string path, int line, DebugVariable[] variables)
            : this(generation, path, line, variables, null)
        {
        }

        /// <summary>
        /// 世代・パス・1 始まり行・ローカル・スタックを渡す。
        /// </summary>
        /// <param name="generation">Start に渡した世代。</param>
        /// <param name="path">停止位置のパス。無ければ null。</param>
        /// <param name="line">1 始まりの行。無ければ 0。</param>
        /// <param name="variables">ローカル。null は空。</param>
        /// <param name="frames">コールスタック。null は空。</param>
        public DebugStoppedEventArgs(int generation, string path, int line, DebugVariable[] variables, DebugStackFrame[] frames)
        {
            this.generation = generation;
            this.path = path;
            this.line = line;
            if (variables == null)
            {
                this.variables = new DebugVariable[0];
            }
            else
            {
                this.variables = variables;
            }

            if (frames == null)
            {
                this.frames = new DebugStackFrame[0];
            }
            else
            {
                this.frames = frames;
            }
        }

        /// <summary>Start に渡した世代。</summary>
        public int Generation
        {
            get { return this.generation; }
        }

        /// <summary>停止スクリプト。</summary>
        public string Path
        {
            get { return this.path; }
        }

        /// <summary>1 始まりの行。</summary>
        public int Line
        {
            get { return this.line; }
        }

        /// <summary>ローカル。</summary>
        public DebugVariable[] Variables
        {
            get { return this.variables; }
        }

        /// <summary>コールスタック。無ければ空。</summary>
        public DebugStackFrame[] Frames
        {
            get { return this.frames; }
        }
    }

    /// <summary>
    /// デバッグコンソール 1 行。kind は 0=stdout、1=stderr、2=起動終了警告。
    /// </summary>
    public sealed class DebugConsoleEventArgs : EventArgs
    {
        private int generation;
        private int kind;
        private string line;

        /// <summary>
        /// 世代と種別と本文を渡す。
        /// </summary>
        /// <param name="generation">Start に渡した世代。</param>
        /// <param name="kind">0 stdout / 1 stderr / 2 Comment。</param>
        /// <param name="line">1 行。null は空。</param>
        public DebugConsoleEventArgs(int generation, int kind, string line)
        {
            this.generation = generation;
            this.kind = kind;
            this.line = (line == null) ? "" : line;
        }

        /// <summary>Start に渡した世代。</summary>
        public int Generation
        {
            get { return this.generation; }
        }

        /// <summary>0 stdout / 1 stderr / 2 Comment。</summary>
        public int Kind
        {
            get { return this.kind; }
        }

        /// <summary>1 行。</summary>
        public string Line
        {
            get { return this.line; }
        }
    }

    /// <summary>
    /// セッション終了。
    /// </summary>
    public sealed class DebugEndedEventArgs : EventArgs
    {
        private int generation;

        /// <summary>
        /// 世代を渡す。
        /// </summary>
        /// <param name="generation">Start に渡した世代。</param>
        public DebugEndedEventArgs(int generation)
        {
            this.generation = generation;
        }

        /// <summary>Start に渡した世代。</summary>
        public int Generation
        {
            get { return this.generation; }
        }
    }
}
