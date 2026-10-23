namespace WindowsIDE.Commands
{
    /// <summary>
    /// メニュー順のコマンド ID。値は安定したドット名。ハンドラは持たない。
    /// </summary>
    public static class CommandIds
    {
        /// <summary>フォルダを開く。</summary>
        public const string FileOpenFolder = "file.openFolder";

        /// <summary>新規。</summary>
        public const string FileNew = "file.new";

        /// <summary>ファイルを作成。</summary>
        public const string FileCreateFile = "file.createFile";

        /// <summary>フォルダを作成。</summary>
        public const string FileCreateFolder = "file.createFolder";

        /// <summary>保存。</summary>
        public const string FileSave = "file.save";

        /// <summary>名前を付けて保存。</summary>
        public const string FileSaveAs = "file.saveAs";

        /// <summary>すべて保存。</summary>
        public const string FileSaveAll = "file.saveAll";

        /// <summary>タブを閉じる。</summary>
        public const string FileCloseTab = "file.closeTab";

        /// <summary>終了。</summary>
        public const string FileExit = "file.exit";

        /// <summary>元に戻す。</summary>
        public const string EditUndo = "edit.undo";

        /// <summary>やり直し。</summary>
        public const string EditRedo = "edit.redo";

        /// <summary>切り取り。</summary>
        public const string EditCut = "edit.cut";

        /// <summary>コピー。</summary>
        public const string EditCopy = "edit.copy";

        /// <summary>貼り付け。</summary>
        public const string EditPaste = "edit.paste";

        /// <summary>すべて選択。</summary>
        public const string EditSelectAll = "edit.selectAll";

        /// <summary>検索。</summary>
        public const string EditFind = "edit.find";

        /// <summary>置換。</summary>
        public const string EditReplace = "edit.replace";

        /// <summary>次を検索。</summary>
        public const string EditFindNext = "edit.findNext";

        /// <summary>前を検索。</summary>
        public const string EditFindPrevious = "edit.findPrevious";

        /// <summary>定義へ移動。</summary>
        public const string EditGotoDefinition = "edit.gotoDefinition";

        /// <summary>クイック インフォ。</summary>
        public const string EditQuickInfo = "edit.quickInfo";

        /// <summary>枠コメントを挿入。</summary>
        public const string EditInsertDocFrame = "edit.insertDocFrame";

        /// <summary>ビルド。</summary>
        public const string BuildBuild = "build.build";

        /// <summary>開始/続行。</summary>
        public const string RunStartContinue = "run.startContinue";

        /// <summary>停止。</summary>
        public const string RunStop = "run.stop";

        /// <summary>ステップ オーバー。</summary>
        public const string RunStepOver = "run.stepOver";

        /// <summary>ステップ イン。</summary>
        public const string RunStepInto = "run.stepInto";

        /// <summary>デバッグなしで実行。</summary>
        public const string RunWithoutDebug = "run.withoutDebug";

        /// <summary>選択行を実行。</summary>
        public const string RunSelection = "run.selection";

        /// <summary>ブックを選ぶ。</summary>
        public const string VbaPickWorkbook = "vba.pickWorkbook";

        /// <summary>プル。</summary>
        public const string VbaPull = "vba.pull";

        /// <summary>プッシュ。</summary>
        public const string VbaPush = "vba.push";

        /// <summary>コンパイル。</summary>
        public const string VbaCompile = "vba.compile";

        /// <summary>クイックオープン。</summary>
        public const string ViewQuickOpen = "view.quickOpen";

        /// <summary>コマンドパレット。</summary>
        public const string ViewCommandPalette = "view.commandPalette";

        /// <summary>エクスプローラー。</summary>
        public const string ViewExplorer = "view.explorer";

        /// <summary>編集器。</summary>
        public const string ViewEditor = "view.editor";

        /// <summary>プレビュー。</summary>
        public const string ViewPreview = "view.preview";

        /// <summary>ターミナル。</summary>
        public const string ViewTerminal = "view.terminal";

        /// <summary>デバッグ。</summary>
        public const string ViewDebug = "view.debug";

        /// <summary>バージョン情報。</summary>
        public const string HelpAbout = "help.about";
    }
}
