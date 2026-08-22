using System;

namespace WindowsIDE.Editor
{
    /// <summary>
    /// ファイル内検索の 1 ヒット。開始と終端（終端は排他）。BufferPoint はフィールドで持つ。
    /// </summary>
    public struct FindMatch
    {
        /// <summary>ヒット開始。</summary>
        public BufferPoint Start;

        /// <summary>ヒット終端（この位置は含まない）。</summary>
        public BufferPoint End;
    }

    /// <summary>
    /// 言語非依存の F-FIND 規則。リテラルのみ。WinForms に依存しない。
    /// </summary>
    public static class FindRules
    {
        /// <summary>
        /// クエリを単一行にする。null は空。最初の CR または LF より前だけを残す。
        /// </summary>
        /// <param name="query">入力。null 可。</param>
        /// <returns>改行を含まないクエリ。</returns>
        public static string NormalizeQuery(string query)
        {
            if (query == null)
            {
                return "";
            }

            int n = query.Length;
            int i = 0;
            while (i < n)
            {
                char c = query[i];
                if (c == '\r' || c == '\n')
                {
                    n = i;
                    break;
                }

                i++;
            }

            if (n == query.Length)
            {
                return query;
            }

            return query.Substring(0, n);
        }

        /// <summary>
        /// fromInclusive 以降（その点を含む）の次ヒット。wrap ならファイル先頭へ戻る。0 件ではラップしない。
        /// </summary>
        /// <param name="buffer">対象バッファ。</param>
        /// <param name="query">検索語。正規化される。</param>
        /// <param name="ignoreCase">true なら OrdinalIgnoreCase。</param>
        /// <param name="fromInclusive">この位置以降を探す。</param>
        /// <param name="wrap">末尾で無ければ先頭から続ける。</param>
        /// <param name="match">ヒット。無ければ default。</param>
        /// <returns>ヒットがあれば true。</returns>
        public static bool TryFindNext(TextBuffer buffer, string query, bool ignoreCase, BufferPoint fromInclusive, bool wrap, out FindMatch match)
        {
            match = new FindMatch();
            query = NormalizeQuery(query);
            if (buffer == null || query.Length == 0)
            {
                return false;
            }

            fromInclusive = buffer.Clamp(fromInclusive);
            StringComparison comparison = Comparison(ignoreCase);
            if (TryFindForward(buffer, query, comparison, fromInclusive, out match))
            {
                return true;
            }

            if (!wrap)
            {
                return false;
            }

            return TryFindForward(buffer, query, comparison, new BufferPoint(0, 0), out match);
        }

        /// <summary>
        /// fromInclusive より前（開始がこの点未満）の前ヒット。wrap ならファイル末尾から探す。0 件ではラップしない。
        /// </summary>
        /// <param name="buffer">対象バッファ。</param>
        /// <param name="query">検索語。正規化される。</param>
        /// <param name="ignoreCase">true なら OrdinalIgnoreCase。</param>
        /// <param name="fromInclusive">この位置より前を探す。</param>
        /// <param name="wrap">先頭で無ければ末尾から続ける。</param>
        /// <param name="match">ヒット。無ければ default。</param>
        /// <returns>ヒットがあれば true。</returns>
        public static bool TryFindPrevious(TextBuffer buffer, string query, bool ignoreCase, BufferPoint fromInclusive, bool wrap, out FindMatch match)
        {
            match = new FindMatch();
            query = NormalizeQuery(query);
            if (buffer == null || query.Length == 0)
            {
                return false;
            }

            fromInclusive = buffer.Clamp(fromInclusive);
            StringComparison comparison = Comparison(ignoreCase);
            if (TryFindBackward(buffer, query, comparison, fromInclusive, out match))
            {
                return true;
            }

            if (!wrap)
            {
                return false;
            }

            int last = buffer.LineCount - 1;
            BufferPoint eof = new BufferPoint(last, buffer.GetLineLength(last));
            return TryFindBackward(buffer, query, comparison, eof, out match);
        }

        /// <summary>
        /// 重ならないリテラルヒット数。空クエリは 0。行をまたがない。
        /// </summary>
        /// <param name="buffer">対象バッファ。</param>
        /// <param name="query">検索語。正規化される。</param>
        /// <param name="ignoreCase">true なら OrdinalIgnoreCase。</param>
        /// <returns>ヒット数。</returns>
        public static int Count(TextBuffer buffer, string query, bool ignoreCase)
        {
            query = NormalizeQuery(query);
            if (buffer == null || query.Length == 0)
            {
                return 0;
            }

            StringComparison comparison = Comparison(ignoreCase);
            int n = 0;
            int line = 0;
            while (line < buffer.LineCount)
            {
                string text = buffer.GetLine(line);
                int col = 0;
                while (col < text.Length)
                {
                    int at = text.IndexOf(query, col, comparison);
                    if (at < 0)
                    {
                        break;
                    }

                    n++;
                    col = at + query.Length;
                }

                line++;
            }

            return n;
        }

        private static StringComparison Comparison(bool ignoreCase)
        {
            if (ignoreCase)
            {
                return StringComparison.OrdinalIgnoreCase;
            }

            return StringComparison.Ordinal;
        }

        private static bool TryFindForward(TextBuffer buffer, string query, StringComparison comparison, BufferPoint fromInclusive, out FindMatch match)
        {
            match = new FindMatch();
            int line = fromInclusive.Line;
            while (line < buffer.LineCount)
            {
                string text = buffer.GetLine(line);
                int startCol = 0;
                if (line == fromInclusive.Line)
                {
                    startCol = fromInclusive.Column;
                }

                if (startCol < 0)
                {
                    startCol = 0;
                }

                if (startCol > text.Length)
                {
                    startCol = text.Length;
                }

                int at = text.IndexOf(query, startCol, comparison);
                if (at >= 0)
                {
                    match.Start = new BufferPoint(line, at);
                    match.End = new BufferPoint(line, at + query.Length);
                    return true;
                }

                line++;
            }

            return false;
        }

        private static bool TryFindBackward(TextBuffer buffer, string query, StringComparison comparison, BufferPoint fromInclusive, out FindMatch match)
        {
            match = new FindMatch();
            int line = fromInclusive.Line;
            while (line >= 0)
            {
                string text = buffer.GetLine(line);
                int startIndex;
                if (line == fromInclusive.Line)
                {
                    startIndex = fromInclusive.Column - 1;
                }
                else
                {
                    startIndex = text.Length;
                }

                if (startIndex >= 0 && text.Length > 0)
                {
                    if (startIndex > text.Length)
                    {
                        startIndex = text.Length;
                    }

                    int at = text.LastIndexOf(query, startIndex, comparison);
                    if (at >= 0)
                    {
                        match.Start = new BufferPoint(line, at);
                        match.End = new BufferPoint(line, at + query.Length);
                        return true;
                    }
                }

                line--;
            }

            return false;
        }
    }
}
