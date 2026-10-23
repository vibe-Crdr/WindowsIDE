using System;
using WindowsIDE.Commands;

namespace WindowsIDE.Ui
{
    /// <summary>
    /// メニューに結び付いた 1 コマンド。ハンドラは Ui だけが持つ。
    /// </summary>
    public sealed class BoundCommand : CommandEntry
    {
        private readonly EventHandler handler;

        /// <summary>
        /// 表示エントリと既存メニューのハンドラを保持する。
        /// </summary>
        /// <param name="id">コマンド ID。</param>
        /// <param name="name">ニーモニックを除いた表示名。</param>
        /// <param name="shortcut">表示用ショートカット。無いときは空。</param>
        /// <param name="handler">メニュー登録時の EventHandler。null なら何もしない。</param>
        public BoundCommand(string id, string name, string shortcut, EventHandler handler)
            : base(id, name, shortcut)
        {
            this.handler = handler;
        }

        /// <summary>メニュー登録時のハンドラ。</summary>
        public EventHandler Handler
        {
            get { return this.handler; }
        }
    }
}
