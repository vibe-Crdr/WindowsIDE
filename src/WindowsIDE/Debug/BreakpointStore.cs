using System;
using System.Collections.Generic;

namespace WindowsIDE.Debug
{
    /// <summary>
    /// 行ブレークのメモリ上ストア。パスは OrdinalIgnoreCase。行は 1 始まり。XML に書かない。
    /// </summary>
    public sealed class BreakpointStore
    {
        private readonly Dictionary<string, HashSet<int>> map;

        /// <summary>
        /// 空のストアを作る。
        /// </summary>
        public BreakpointStore()
        {
            this.map = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 指定パス・行をトグルする。同一行は二重にしない。
        /// </summary>
        /// <param name="path">スクリプトのフルパス。</param>
        /// <param name="line">1 始まりの行。</param>
        /// <returns>トグル後にブレークがあれば true。無効な引数や解除なら false。</returns>
        public bool Toggle(string path, int line)
        {
            if (string.IsNullOrEmpty(path) || line < 1)
            {
                return false;
            }

            HashSet<int> set;
            if (!this.map.TryGetValue(path, out set))
            {
                set = new HashSet<int>();
                this.map[path] = set;
            }

            if (set.Contains(line))
            {
                set.Remove(line);
                if (set.Count == 0)
                {
                    this.map.Remove(path);
                }

                return false;
            }

            set.Add(line);
            return true;
        }

        /// <summary>
        /// 指定パスの 1 始まり行を昇順で返す。無ければ空配列。
        /// </summary>
        /// <param name="path">スクリプトのフルパス。</param>
        /// <returns>行番号。null パスは空。</returns>
        public int[] GetLines(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return new int[0];
            }

            HashSet<int> set;
            if (!this.map.TryGetValue(path, out set) || set.Count == 0)
            {
                return new int[0];
            }

            int[] lines = new int[set.Count];
            set.CopyTo(lines);
            Array.Sort(lines);
            return lines;
        }

        /// <summary>
        /// 全エントリ。開始時に Debugger へ載せるため。
        /// </summary>
        /// <returns>パスと 1 始まり行。空でも null ではない。</returns>
        public BreakpointEntry[] GetAll()
        {
            List<BreakpointEntry> list = new List<BreakpointEntry>();
            foreach (KeyValuePair<string, HashSet<int>> kv in this.map)
            {
                foreach (int line in kv.Value)
                {
                    list.Add(new BreakpointEntry(kv.Key, line));
                }
            }

            return list.ToArray();
        }
    }

    /// <summary>
    /// ストア 1 件。パスと 1 始まり行。
    /// </summary>
    public sealed class BreakpointEntry
    {
        private string path;
        private int line;

        /// <summary>
        /// パスと行を渡す。
        /// </summary>
        /// <param name="path">スクリプトパス。</param>
        /// <param name="line">1 始まりの行。</param>
        public BreakpointEntry(string path, int line)
        {
            this.path = path;
            this.line = line;
        }

        /// <summary>スクリプトパス。</summary>
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
