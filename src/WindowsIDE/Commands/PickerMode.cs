namespace WindowsIDE.Commands
{
    /// <summary>
    /// コマンドポップアップのモード。クイックオープンが 0、パレットが 1。
    /// </summary>
    public enum PickerMode
    {
        /// <summary>ワークスペース内ファイル。</summary>
        QuickOpen = 0,

        /// <summary>コマンド名。</summary>
        Palette = 1
    }
}
