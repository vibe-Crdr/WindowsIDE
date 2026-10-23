namespace WindowsIDE.Commands
{
    /// <summary>
    /// パレットに出す 1 件。ID、表示名、ショートカット文字列。ハンドラは持たない。
    /// </summary>
    public class CommandEntry
    {
        private readonly string id;
        private readonly string name;
        private readonly string shortcut;

        /// <summary>
        /// 表示用の 1 件を保持する。null は空文字にする。
        /// </summary>
        /// <param name="id">コマンド ID。</param>
        /// <param name="name">ニーモニックを除いた表示名。</param>
        /// <param name="shortcut">表示用ショートカット。無いときは空。</param>
        public CommandEntry(string id, string name, string shortcut)
        {
            this.id = (id == null) ? "" : id;
            this.name = (name == null) ? "" : name;
            this.shortcut = (shortcut == null) ? "" : shortcut;
        }

        /// <summary>コマンド ID。</summary>
        public string Id
        {
            get { return this.id; }
        }

        /// <summary>表示名。絞り込みはここだけを見る。</summary>
        public string Name
        {
            get { return this.name; }
        }

        /// <summary>ショートカット表示。一致には使わない。</summary>
        public string Shortcut
        {
            get { return this.shortcut; }
        }
    }
}
