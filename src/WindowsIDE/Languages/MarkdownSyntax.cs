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
        /// 先頭インデントを列数で返す。タブは 4 列相当。
        /// </summary>
        /// <param name="line">対象行。</param>
        /// <returns>インデント列数。</returns>
        public static int IndentColumns(string line)
        {
            if (line == null || line.Length == 0)
            {
                return 0;
            }

            int cols = 0;
            int i = 0;
            while (i < line.Length)
            {
                char c = line[i];
                if (c == ' ')
                {
                    cols++;
                }
                else if (c == '\t')
                {
                    cols += 4 - (cols % 4);
                }
                else
                {
                    break;
                }

                i++;
            }

            return cols;
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
        /// Setext 下線（`=` はレベル 1、`-` はレベル 2）なら true。
        /// </summary>
        /// <param name="line">対象行。</param>
        /// <param name="level">1 または 2。</param>
        /// <returns>下線なら true。</returns>
        public static bool TrySetextUnderline(string line, out int level)
        {
            level = 0;
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
            if (c != '=' && c != '-')
            {
                return false;
            }

            int n = 0;
            while (i < line.Length && line[i] == c)
            {
                n++;
                i++;
            }

            while (i < line.Length && ScanChars.IsSpace(line[i]))
            {
                i++;
            }

            if (i < line.Length || n < 1)
            {
                return false;
            }

            level = (c == '=') ? 1 : 2;
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
        /// リスト本文先頭のタスク `[ ]` / `[x]` / `[X]` なら true。
        /// </summary>
        /// <param name="line">対象行。</param>
        /// <param name="start">リスト本文開始。</param>
        /// <param name="state">Open または Closed。</param>
        /// <param name="contentStart">チェックのあとの本文開始。</param>
        /// <returns>タスクなら true。</returns>
        public static bool TryTaskMarker(string line, int start, out MarkdownTaskState state, out int contentStart)
        {
            state = MarkdownTaskState.None;
            contentStart = start;
            if (line == null || start < 0 || start + 2 >= line.Length)
            {
                return false;
            }

            if (line[start] != '[' || line[start + 2] != ']')
            {
                return false;
            }

            char mid = line[start + 1];
            if (mid == ' ')
            {
                state = MarkdownTaskState.Open;
            }
            else if (mid == 'x' || mid == 'X')
            {
                state = MarkdownTaskState.Closed;
            }
            else
            {
                return false;
            }

            int i = start + 3;
            if (i < line.Length && ScanChars.IsSpace(line[i]))
            {
                i = SkipSpaces(line, i);
            }

            contentStart = i;
            return true;
        }

        /// <summary>
        /// 行頭の 3 連以上バッククォートまたはチルダならフェンス開閉行。
        /// </summary>
        /// <param name="line">対象行。</param>
        /// <returns>フェンス行なら true。</returns>
        public static bool TryFence(string line)
        {
            char marker;
            int count;
            return TryFence(line, out marker, out count);
        }

        /// <summary>
        /// フェンス開閉行ならマーカー文字と連数を返す。
        /// </summary>
        /// <param name="line">対象行。</param>
        /// <param name="marker">`` ` `` または `~`。</param>
        /// <param name="count">連数（3 以上）。</param>
        /// <returns>フェンス行なら true。</returns>
        public static bool TryFence(string line, out char marker, out int count)
        {
            marker = '\0';
            count = 0;
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
            if (c != '`' && c != '~')
            {
                return false;
            }

            int n = 0;
            while (i < line.Length && line[i] == c)
            {
                n++;
                i++;
            }

            if (n < 3)
            {
                return false;
            }

            marker = c;
            count = n;
            return true;
        }

        /// <summary>
        /// 引用行（先頭の `>` を 1 段以上）なら true。
        /// </summary>
        /// <param name="line">対象行。</param>
        /// <param name="nestLevel">`>` の段数。</param>
        /// <param name="contentStart">引用接頭辞のあとの本文開始。</param>
        /// <returns>引用なら true。</returns>
        public static bool TryQuote(string line, out int nestLevel, out int contentStart)
        {
            nestLevel = 0;
            contentStart = 0;
            if (line == null)
            {
                return false;
            }

            int i = 0;
            int spaces = 0;
            while (i < line.Length && line[i] == ' ' && spaces < 3)
            {
                spaces++;
                i++;
            }

            if (i >= line.Length || line[i] != '>')
            {
                return false;
            }

            while (i < line.Length)
            {
                while (i < line.Length && ScanChars.IsSpace(line[i]))
                {
                    i++;
                }

                if (i >= line.Length || line[i] != '>')
                {
                    break;
                }

                nestLevel++;
                i++;
                if (i < line.Length && line[i] == ' ')
                {
                    i++;
                }
            }

            if (nestLevel < 1)
            {
                return false;
            }

            contentStart = i;
            return true;
        }

        /// <summary>
        /// テーマティックブレーク（3 連以上の `-` `*` `_`。間の空白可）なら true。
        /// </summary>
        /// <param name="line">対象行。</param>
        /// <returns>HR なら true。</returns>
        public static bool TryThematicBreak(string line)
        {
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
            if (c != '-' && c != '*' && c != '_')
            {
                return false;
            }

            int n = 0;
            while (i < line.Length)
            {
                char ch = line[i];
                if (ch == c)
                {
                    n++;
                }
                else if (!ScanChars.IsSpace(ch))
                {
                    return false;
                }

                i++;
            }

            return n >= 3;
        }

        /// <summary>
        /// インデントコード（先頭スペース/タブが 4 列相当以上）なら true。空行は含めない。
        /// </summary>
        /// <param name="line">対象行。</param>
        /// <returns>インデントコードなら true。</returns>
        public static bool TryIndentedCode(string line)
        {
            if (IsBlank(line))
            {
                return false;
            }

            return IndentColumns(line) >= 4;
        }

        /// <summary>
        /// パイプ表の 1 行をセルへ分ける。
        /// </summary>
        /// <param name="line">対象行。</param>
        /// <param name="cells">トリムしたセル。</param>
        /// <returns>パイプを含む行なら true。</returns>
        public static bool TryTableRow(string line, out string[] cells)
        {
            cells = null;
            if (line == null)
            {
                return false;
            }

            int pipe = IndexOfChar(line, '|', 0);
            if (pipe < 0)
            {
                return false;
            }

            int start = SkipLeadingSpace(line);
            int end = line.Length;
            while (end > start && ScanChars.IsSpace(line[end - 1]))
            {
                end--;
            }

            if (start < end && line[start] == '|')
            {
                start++;
            }

            if (end > start && line[end - 1] == '|')
            {
                end--;
            }

            int count = 1;
            int i = start;
            while (i < end)
            {
                if (line[i] == '|')
                {
                    count++;
                }

                i++;
            }

            cells = new string[count];
            int cell = 0;
            int cellStart = start;
            i = start;
            while (i < end)
            {
                if (line[i] == '|')
                {
                    cells[cell] = TrimRange(line, cellStart, i);
                    cell++;
                    cellStart = i + 1;
                }

                i++;
            }

            cells[cell] = TrimRange(line, cellStart, end);
            return true;
        }

        /// <summary>
        /// 表セパレータ（`|---|---` 形式。整列コロン可）なら true。
        /// </summary>
        /// <param name="line">対象行。</param>
        /// <returns>セパレータなら true。</returns>
        public static bool TryTableSep(string line)
        {
            string[] cells;
            if (!TryTableRow(line, out cells) || cells == null || cells.Length == 0)
            {
                return false;
            }

            int i = 0;
            while (i < cells.Length)
            {
                if (!IsSepCell(cells[i]))
                {
                    return false;
                }

                i++;
            }

            return true;
        }

        private static bool IsSepCell(string cell)
        {
            if (cell == null)
            {
                return false;
            }

            int i = 0;
            while (i < cell.Length && ScanChars.IsSpace(cell[i]))
            {
                i++;
            }

            if (i < cell.Length && cell[i] == ':')
            {
                i++;
            }

            int dashes = 0;
            while (i < cell.Length && cell[i] == '-')
            {
                dashes++;
                i++;
            }

            if (i < cell.Length && cell[i] == ':')
            {
                i++;
            }

            while (i < cell.Length && ScanChars.IsSpace(cell[i]))
            {
                i++;
            }

            return dashes >= 1 && i >= cell.Length;
        }

        /// <summary>
        /// 行末がハードブレーク（空白 2 つ以上）なら true。
        /// </summary>
        /// <param name="line">対象行。</param>
        /// <returns>ハードブレークなら true。</returns>
        public static bool EndsWithHardBreak(string line)
        {
            if (line == null || line.Length < 2)
            {
                return false;
            }

            return line[line.Length - 1] == ' ' && line[line.Length - 2] == ' ';
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

        private static int IndexOfChar(string text, char ch, int start)
        {
            int i = start;
            while (i < text.Length)
            {
                if (text[i] == ch)
                {
                    return i;
                }

                i++;
            }

            return -1;
        }

        private static string TrimRange(string line, int start, int end)
        {
            while (start < end && ScanChars.IsSpace(line[start]))
            {
                start++;
            }

            while (end > start && ScanChars.IsSpace(line[end - 1]))
            {
                end--;
            }

            if (end <= start)
            {
                return "";
            }

            return line.Substring(start, end - start);
        }
    }
}
