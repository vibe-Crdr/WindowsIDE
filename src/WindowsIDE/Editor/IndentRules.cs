namespace WindowsIDE.Editor
{
    /// <summary>
    /// 言語非依存の F-IND 規則。先頭空白のコピーと複数行インデント。WinForms に依存しない。
    /// </summary>
    public static class IndentRules
    {
        /// <summary>
        /// 行頭の連続するスペースとタブだけを返す。U+3000 は含めない。null は空文字。
        /// </summary>
        /// <param name="line">対象行。null なら空。</param>
        /// <returns>先頭空白。タブは展開しない。</returns>
        public static string LeadingWhitespace(string line)
        {
            if (line == null)
            {
                line = "";
            }

            int n = 0;
            while (n < line.Length)
            {
                char c = line[n];
                if (c != ' ' && c != '\t')
                {
                    break;
                }

                n++;
            }

            if (n == 0)
            {
                return "";
            }

            return line.Substring(0, n);
        }

        /// <summary>
        /// 複数行にまたがる選択なら true。同一行は false。
        /// </summary>
        /// <param name="start">選択開始（開始≦終了）。</param>
        /// <param name="end">選択終了。</param>
        /// <returns>start.Line と end.Line が異なるとき true。</returns>
        public static bool IsBlockIndentSelection(BufferPoint start, BufferPoint end)
        {
            return start.Line != end.Line;
        }

        /// <summary>
        /// ブロックインデントの最終行。終端が列 0 かつ開始より下ならその行を除外する。
        /// </summary>
        /// <param name="start">選択開始（開始≦終了）。</param>
        /// <param name="end">選択終了。</param>
        /// <returns>対象の最終行インデックス。</returns>
        public static int BlockLastLine(BufferPoint start, BufferPoint end)
        {
            if (end.Column == 0 && end.Line > start.Line)
            {
                return end.Line - 1;
            }

            return end.Line;
        }

        /// <summary>
        /// 行頭に tabSize 個のスペースを足した行を返す。空行も含む。tabSize が 1 未満なら 1。
        /// </summary>
        /// <param name="line">対象行。null なら空。</param>
        /// <param name="tabSize">足すスペース数。</param>
        /// <returns>インデント後の行。</returns>
        public static string IndentLine(string line, int tabSize)
        {
            if (line == null)
            {
                line = "";
            }

            return new string(' ', NormalizeTabSize(tabSize)) + line;
        }

        /// <summary>
        /// 行頭を 1 段削る。先頭がタブなら 1 文字、それ以外は最大 tabSize 個のスペース。無い行は変えない。
        /// </summary>
        /// <param name="line">対象行。null なら空。</param>
        /// <param name="tabSize">削るスペース上限。1 未満なら 1。</param>
        /// <param name="removed">削った文字数。</param>
        /// <returns>削ったあとの行。</returns>
        public static string UnindentLine(string line, int tabSize, out int removed)
        {
            if (line == null)
            {
                line = "";
            }

            tabSize = NormalizeTabSize(tabSize);
            if (line.Length == 0)
            {
                removed = 0;
                return line;
            }

            if (line[0] == '\t')
            {
                removed = 1;
                return line.Substring(1);
            }

            int n = 0;
            while (n < tabSize && n < line.Length && line[n] == ' ')
            {
                n++;
            }

            removed = n;
            if (n == 0)
            {
                return line;
            }

            return line.Substring(n);
        }

        /// <summary>
        /// インデント後の列。列 0 は 0 のまま、否则 +tabSize。tabSize が 1 未満なら 1。
        /// </summary>
        /// <param name="column">補正前の列。</param>
        /// <param name="tabSize">足したスペース数。</param>
        /// <returns>補正後の列（0 以上）。</returns>
        public static int AdjustColumnAfterIndent(int column, int tabSize)
        {
            if (column <= 0)
            {
                return 0;
            }

            return column + NormalizeTabSize(tabSize);
        }

        /// <summary>
        /// アンインデント後の列。max(0, column - removed)。
        /// </summary>
        /// <param name="column">補正前の列。</param>
        /// <param name="removed">その行で削った文字数。</param>
        /// <returns>補正後の列（0 以上）。</returns>
        public static int AdjustColumnAfterUnindent(int column, int removed)
        {
            if (removed < 0)
            {
                removed = 0;
            }

            int next = column - removed;
            if (next < 0)
            {
                return 0;
            }

            return next;
        }

        private static int NormalizeTabSize(int tabSize)
        {
            if (tabSize < 1)
            {
                return 1;
            }

            return tabSize;
        }
    }
}
