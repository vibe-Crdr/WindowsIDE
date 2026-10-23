namespace WindowsIDE.Commands
{
    /// <summary>
    /// 選択できない空表示。通常の行があるときは null。
    /// </summary>
    public static class PickerHint
    {
        /// <summary>ワークスペースが無い。</summary>
        public const string NoWorkspace = "ワークスペースがありません";

        /// <summary>走査結果が空でクエリも空。</summary>
        public const string NoFiles = "ファイルがありません";

        /// <summary>クエリまたはパレットに該当が無い。</summary>
        public const string NoMatch = "該当なし";

        /// <summary>
        /// クイックオープンの空表示。ワークスペース無しを先に返す。
        /// </summary>
        /// <param name="hasWorkspace">ワークスペースがあるとき true。</param>
        /// <param name="snapshotCount">走査件数。未走査は 0。</param>
        /// <param name="matchCount">絞り込み件数。</param>
        /// <param name="query">入力。前後空白は空とみなす。</param>
        /// <returns>空表示。行があるときは null。</returns>
        public static string QuickOpen(bool hasWorkspace, int snapshotCount, int matchCount, string query)
        {
            if (!hasWorkspace)
            {
                return NoWorkspace;
            }

            if (matchCount > 0)
            {
                return null;
            }

            string trimmed = (query == null) ? "" : query.Trim();
            if (snapshotCount <= 0 && trimmed.Length == 0)
            {
                return NoFiles;
            }

            return NoMatch;
        }

        /// <summary>
        /// パレットの空表示。
        /// </summary>
        /// <param name="matchCount">絞り込み件数。</param>
        /// <returns>0 件なら「該当なし」。行があるときは null。</returns>
        public static string Palette(int matchCount)
        {
            if (matchCount > 0)
            {
                return null;
            }

            return NoMatch;
        }
    }
}
