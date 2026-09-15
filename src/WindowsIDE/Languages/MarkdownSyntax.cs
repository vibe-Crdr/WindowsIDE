namespace WindowsIDE.Languages
{
    /// <summary>
    /// Markdown の行頭分類。レキサとブロック列が共有する。文字走査のみ。
    /// </summary>
    internal static class MarkdownSyntax
    {
        /// <summary>
        /// 先頭のスペースとタブを飛ばした位置を返す。
        /// </summary>
        /// <param name="line">対象行。null は空。</param>
        /// <returns>最初の非空白位置。全部空白なら Length。</returns>
        public static int SkipLeadingSpace(string line)
        {
            if (line == null)
            {
                return 0;
            }

            int i = 0;
            while (i < line.Length && ScanChars.IsSpace(line[i]))
            {
                i++;
            }

            return i;
        }

        /// <summary>
        /// 空行または空白だけの行なら true。
        /// </summary>
        /// <param name="line">対象行。</param>
        /// <returns>空白のみなら true。</returns>
        public static bool IsBlank(string line)
        {
            if (line == null || line.Length == 0)
            {
                return true;
            }

            return SkipLeadingSpace(line) >= line.Length;
        }

        /// <summary>
        /// ATX 見出し（`#`〜`######` のあと空白）なら true。
        /// </summary>
        /// <param name="line">対象行。</param>
        /// <param name="level">1〜6。</param>
        /// <param name="hashStart">最初の `#` の位置。</param>
        /// <param name="contentStart">マーカー直後空白のあとの本文開始。</param>
        /// <returns>見出しなら true。</returns>
        public static bool TryHeading(string line, out int level, out int hashStart, out int contentStart)
        {
            level = 0;
            hashStart = 0;
            contentStart = 0;
            if (line == null)
            {
                return false;
            }

            int i = SkipLeadingSpace(line);
            if (i >= line.Length || line[i] != '#')
            {
                return false;
            }

            hashStart = i;
            while (i < line.Length && line[i] == '#')
            {
                i++;
            }

            int n = i - hashStart;
            if (n < 1 || n > 6)
            {
                return false;
            }

            if (i >= line.Length || !ScanChars.IsSpace(line[i]))
            {
                return false;
            }

            i++;
            while (i < line.Length && ScanChars.IsSpace(line[i]))
            {
                i++;
            }

            level = n;
            contentStart = i;
            return true;
        }

        /// <summary>
        /// リスト項目（`-` `*` `+` または数字+`.` のあと空白）なら true。
        /// </summary>
        /// <param name="line">対象行。</param>
        /// <param name="markerStart">マーカー開始。</param>
        /// <param name="markerLength">マーカー長（数字リストは `.` まで）。</param>
        /// <param name="contentStart">マーカー直後空白のあとの本文開始。</param>
        /// <returns>リストなら true。</returns>
        public static bool TryListItem(string line, out int markerStart, out int markerLength, out int contentStart)
        {
            markerStart = 0;
            markerLength = 0;
            contentStart = 0;
            if (line == null)
            {
                return false;
            }

            int i = SkipLeadingSpace(line);
            if (i >= line.Length)
            {
                return false;
            }

            char c = line[i];
            if (c == '-' || c == '*' || c == '+')
            {
                if (i + 1 >= line.Length || !ScanChars.IsSpace(line[i + 1]))
                {
                    return false;
                }

                markerStart = i;
                markerLength = 1;
                contentStart = SkipSpaces(line, i + 2);
                return true;
            }

            if (!ScanChars.IsDigit(c))
            {
                return false;
            }

            int j = i;
            while (j < line.Length && ScanChars.IsDigit(line[j]))
            {
                j++;
            }

            if (j >= line.Length || line[j] != '.')
            {
                return false;
            }

            if (j + 1 >= line.Length || !ScanChars.IsSpace(line[j + 1]))
            {
                return false;
            }

            markerStart = i;
            markerLength = j - i + 1;
            contentStart = SkipSpaces(line, j + 2);
            return true;
        }

        /// <summary>
        /// 行頭の 3 連以上バッククォートならフェンス開閉行。
        /// </summary>
        /// <param name="line">対象行。</param>
        /// <returns>フェンス行なら true。</returns>
        public static bool TryFence(string line)
        {
            if (line == null)
            {
                return false;
            }

            int i = SkipLeadingSpace(line);
            if (i >= line.Length || line[i] != '`')
            {
                return false;
            }

            int n = 0;
            while (i < line.Length && line[i] == '`')
            {
                n++;
                i++;
            }

            return n >= 3;
        }

        private static int SkipSpaces(string line, int start)
        {
            int i = start;
            while (i < line.Length && ScanChars.IsSpace(line[i]))
            {
                i++;
            }

            return i;
        }
    }
}
