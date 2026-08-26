using System;
using System.Collections.Generic;
using System.Text;

namespace WindowsIDE.Terminal
{
    /// <summary>
    /// 1 セル。全角の 2 セル目は Continuation。
    /// </summary>
    public struct VtCell
    {
        /// <summary>表示文字。空は '\0'。</summary>
        public char Character;

        /// <summary>前景スロット。</summary>
        public ColorSlot Slot;

        /// <summary>全角の右半分なら true。</summary>
        public bool Continuation;
    }

    /// <summary>
    /// VT のセル格子とスクロールバック。折り返しは列数。横スクロールは持たない。
    /// </summary>
    public sealed class VtScreen
    {
        /// <summary>スクロールバック上限。</summary>
        public const int MaxScrollback = 4000;

        /// <summary>最小列。</summary>
        public const int MinColumns = 20;

        /// <summary>最小行。</summary>
        public const int MinRows = 4;

        private int columns;
        private int rows;
        private VtCell[][] screen;
        private readonly List<VtCell[]> scrollback;
        private int caretCol;
        private int caretRow;
        private bool caretVisible;
        private bool wrapPending;
        private ColorSlot currentSlot;

        /// <summary>
        /// 指定サイズの空画面を作る。最小 20×4。
        /// </summary>
        /// <param name="columns">列。</param>
        /// <param name="rows">行。</param>
        public VtScreen(int columns, int rows)
        {
            this.scrollback = new List<VtCell[]>();
            this.caretVisible = true;
            this.currentSlot = ColorSlot.Default;
            this.ApplySize(columns, rows, true);
        }

        /// <summary>列数。</summary>
        public int Columns
        {
            get { return this.columns; }
        }

        /// <summary>行数。</summary>
        public int Rows
        {
            get { return this.rows; }
        }

        /// <summary>キャレット列（0 始まり。列数と同じ値は行末）。</summary>
        public int CaretColumn
        {
            get { return this.caretCol; }
        }

        /// <summary>キャレット行（画面、0 始まり）。</summary>
        public int CaretRow
        {
            get { return this.caretRow; }
        }

        /// <summary>キャレットを描くなら true。</summary>
        public bool CaretVisible
        {
            get { return this.caretVisible; }
            set { this.caretVisible = value; }
        }

        /// <summary>以降の印字色。</summary>
        public ColorSlot CurrentSlot
        {
            get { return this.currentSlot; }
            set { this.currentSlot = value; }
        }

        /// <summary>スクロールバック行数。</summary>
        public int ScrollbackCount
        {
            get { return this.scrollback.Count; }
        }

        /// <summary>履歴全体（スクロールバック + 画面）。</summary>
        public int HistoryCount
        {
            get { return this.scrollback.Count + this.rows; }
        }

        /// <summary>
        /// 画面と履歴を空にしキャレットを原点へ。
        /// </summary>
        public void ClearAll()
        {
            this.scrollback.Clear();
            this.FillScreenEmpty();
            this.caretCol = 0;
            this.caretRow = 0;
            this.wrapPending = false;
            this.caretVisible = true;
            this.currentSlot = ColorSlot.Default;
        }

        /// <summary>
        /// 列・行を変える。最小 20×4。減った行はスクロールバックへ。
        /// </summary>
        /// <param name="newColumns">列。</param>
        /// <param name="newRows">行。</param>
        public void Resize(int newColumns, int newRows)
        {
            this.ApplySize(newColumns, newRows, false);
        }

        /// <summary>
        /// 画面セルを読む。範囲外は空。
        /// </summary>
        /// <param name="row">画面行。</param>
        /// <param name="col">列。</param>
        /// <returns>セル。</returns>
        public VtCell GetCell(int row, int col)
        {
            if (row < 0 || row >= this.rows || col < 0 || col >= this.columns)
            {
                return new VtCell();
            }

            return this.screen[row][col];
        }

        /// <summary>
        /// 履歴行のセル。absRow 0 が最古。
        /// </summary>
        /// <param name="absRow">履歴行。</param>
        /// <param name="col">列。</param>
        /// <returns>セル。</returns>
        public VtCell GetHistoryCell(int absRow, int col)
        {
            if (col < 0 || col >= this.columns)
            {
                return new VtCell();
            }

            if (absRow < 0)
            {
                return new VtCell();
            }

            if (absRow < this.scrollback.Count)
            {
                VtCell[] line = this.scrollback[absRow];
                if (col >= line.Length)
                {
                    return new VtCell();
                }

                return line[col];
            }

            int screenRow = absRow - this.scrollback.Count;
            return this.GetCell(screenRow, col);
        }

        /// <summary>
        /// 履歴 1 行を文字列にする。継続セルは出さない。
        /// </summary>
        /// <param name="absRow">履歴行。</param>
        /// <returns>行テキスト。末尾空白は残す。</returns>
        public string GetHistoryText(int absRow)
        {
            StringBuilder sb = new StringBuilder();
            int col = 0;
            while (col < this.columns)
            {
                VtCell cell = this.GetHistoryCell(absRow, col);
                if (cell.Continuation)
                {
                    col++;
                    continue;
                }

                if (cell.Character == '\0')
                {
                    sb.Append(' ');
                    col++;
                    continue;
                }

                sb.Append(cell.Character);
                int w = CellWidth.Of(cell.Character);
                if (w < 1)
                {
                    w = 1;
                }

                col += w;
            }

            return sb.ToString();
        }

        /// <summary>
        /// 選択範囲をコピー用テキストにする。行末は CRLF。
        /// </summary>
        /// <param name="aRow">端点の履歴行。</param>
        /// <param name="aCol">端点の列。</param>
        /// <param name="bRow">もう一端の履歴行。</param>
        /// <param name="bCol">もう一端の列。</param>
        /// <returns>テキスト。空選択は ""。</returns>
        public string GetSelectedText(int aRow, int aCol, int bRow, int bCol)
        {
            int r0 = aRow;
            int c0 = aCol;
            int r1 = bRow;
            int c1 = bCol;
            if (r1 < r0 || (r1 == r0 && c1 < c0))
            {
                r0 = bRow;
                c0 = bCol;
                r1 = aRow;
                c1 = aCol;
            }

            if (r0 == r1 && c0 == c1)
            {
                return "";
            }

            StringBuilder sb = new StringBuilder();
            int row = r0;
            while (row <= r1)
            {
                int startCol = (row == r0) ? c0 : 0;
                int endCol = (row == r1) ? c1 : this.columns;
                if (startCol < 0)
                {
                    startCol = 0;
                }

                if (endCol > this.columns)
                {
                    endCol = this.columns;
                }

                int col = startCol;
                while (col < endCol)
                {
                    VtCell cell = this.GetHistoryCell(row, col);
                    if (cell.Continuation)
                    {
                        col++;
                        continue;
                    }

                    if (cell.Character == '\0')
                    {
                        sb.Append(' ');
                        col++;
                    }
                    else
                    {
                        sb.Append(cell.Character);
                        int w = CellWidth.Of(cell.Character);
                        if (w < 1)
                        {
                            w = 1;
                        }

                        col += w;
                    }
                }

                if (row < r1)
                {
                    sb.Append("\r\n");
                }

                row++;
            }

            return sb.ToString();
        }

        /// <summary>CR。列を 0 にし、折り返し待ちを消す。</summary>
        public void CarriageReturn()
        {
            this.caretCol = 0;
            this.wrapPending = false;
        }

        /// <summary>LF。次行へ。最下行ならスクロール。</summary>
        public void LineFeed()
        {
            this.wrapPending = false;
            if (this.caretRow + 1 < this.rows)
            {
                this.caretRow++;
                return;
            }

            this.ScrollUp();
        }

        /// <summary>BS。左へ動くだけ。内容は消さない。</summary>
        public void Backspace()
        {
            this.wrapPending = false;
            if (this.caretCol > 0)
            {
                this.caretCol--;
            }
        }

        /// <summary>HT。8 桁タブ。</summary>
        public void Tab()
        {
            this.EnsureWrap();
            int next = ((this.caretCol / 8) + 1) * 8;
            if (next >= this.columns)
            {
                this.caretCol = this.columns;
                this.wrapPending = true;
                return;
            }

            this.caretCol = next;
        }

        /// <summary>
        /// 1 文字を置く。幅は CellWidth。列を超えたら折り返す。
        /// </summary>
        /// <param name="ch">印字文字。</param>
        public void PutChar(char ch)
        {
            int w = CellWidth.Of(ch);
            if (w <= 0)
            {
                return;
            }

            this.EnsureWrap();
            if (this.caretCol + w > this.columns)
            {
                this.CarriageReturn();
                this.LineFeed();
            }

            if (this.caretCol >= this.columns)
            {
                this.CarriageReturn();
                this.LineFeed();
            }

            int col = this.caretCol;
            if (col < 0)
            {
                col = 0;
            }

            if (col >= this.columns)
            {
                return;
            }

            this.ClearWideAt(this.caretRow, col);
            this.screen[this.caretRow][col].Character = ch;
            this.screen[this.caretRow][col].Slot = this.currentSlot;
            this.screen[this.caretRow][col].Continuation = false;
            if (w == 2 && col + 1 < this.columns)
            {
                this.ClearWideAt(this.caretRow, col + 1);
                this.screen[this.caretRow][col + 1].Character = '\0';
                this.screen[this.caretRow][col + 1].Slot = this.currentSlot;
                this.screen[this.caretRow][col + 1].Continuation = true;
            }

            this.caretCol = col + w;
            if (this.caretCol >= this.columns)
            {
                this.caretCol = this.columns;
                this.wrapPending = true;
            }
        }

        /// <summary>
        /// CUP。1 始まり。欠けた値は 1。
        /// </summary>
        /// <param name="row1">行（1 始まり）。</param>
        /// <param name="col1">列（1 始まり）。</param>
        public void SetCursor(int row1, int col1)
        {
            this.wrapPending = false;
            int r = row1 - 1;
            int c = col1 - 1;
            if (r < 0)
            {
                r = 0;
            }

            if (r >= this.rows)
            {
                r = this.rows - 1;
            }

            if (c < 0)
            {
                c = 0;
            }

            if (c >= this.columns)
            {
                c = this.columns - 1;
            }

            this.caretRow = r;
            this.caretCol = c;
        }

        /// <summary>CUU。</summary>
        /// <param name="n">量。1 未満は 1。</param>
        public void MoveUp(int n)
        {
            this.wrapPending = false;
            if (n < 1)
            {
                n = 1;
            }

            this.caretRow -= n;
            if (this.caretRow < 0)
            {
                this.caretRow = 0;
            }
        }

        /// <summary>CUD。</summary>
        /// <param name="n">量。1 未満は 1。</param>
        public void MoveDown(int n)
        {
            this.wrapPending = false;
            if (n < 1)
            {
                n = 1;
            }

            this.caretRow += n;
            if (this.caretRow >= this.rows)
            {
                this.caretRow = this.rows - 1;
            }
        }

        /// <summary>CUF。</summary>
        /// <param name="n">量。1 未満は 1。</param>
        public void MoveForward(int n)
        {
            this.wrapPending = false;
            if (n < 1)
            {
                n = 1;
            }

            this.caretCol += n;
            if (this.caretCol >= this.columns)
            {
                this.caretCol = this.columns - 1;
            }
        }

        /// <summary>CUB。</summary>
        /// <param name="n">量。1 未満は 1。</param>
        public void MoveBack(int n)
        {
            this.wrapPending = false;
            if (n < 1)
            {
                n = 1;
            }

            this.caretCol -= n;
            if (this.caretCol < 0)
            {
                this.caretCol = 0;
            }
        }

        /// <summary>CHA。1 始まり。</summary>
        /// <param name="col1">列。</param>
        public void SetColumn(int col1)
        {
            this.wrapPending = false;
            int c = col1 - 1;
            if (c < 0)
            {
                c = 0;
            }

            if (c >= this.columns)
            {
                c = this.columns - 1;
            }

            this.caretCol = c;
        }

        /// <summary>
        /// EL。0 行末まで、1 行頭まで、2 行全体。
        /// </summary>
        /// <param name="mode">モード。</param>
        public void EraseLine(int mode)
        {
            int row = this.caretRow;
            if (row < 0 || row >= this.rows)
            {
                return;
            }

            int from = 0;
            int to = this.columns;
            if (mode == 0)
            {
                from = this.caretCol;
                if (from < 0)
                {
                    from = 0;
                }
            }
            else if (mode == 1)
            {
                to = this.caretCol + 1;
                if (to > this.columns)
                {
                    to = this.columns;
                }
            }

            int col;
            for (col = from; col < to; col++)
            {
                this.screen[row][col] = new VtCell();
            }
        }

        /// <summary>
        /// ED。0 画面末まで、1 画面頭まで、2 画面全体（スクロールバックは残す）。
        /// </summary>
        /// <param name="mode">モード。</param>
        public void EraseDisplay(int mode)
        {
            if (mode == 2)
            {
                this.FillScreenEmpty();
                return;
            }

            if (mode == 0)
            {
                this.EraseLine(0);
                int r;
                for (r = this.caretRow + 1; r < this.rows; r++)
                {
                    this.ClearRow(r);
                }

                return;
            }

            if (mode == 1)
            {
                int r;
                for (r = 0; r < this.caretRow; r++)
                {
                    this.ClearRow(r);
                }

                this.EraseLine(1);
            }
        }

        /// <summary>
        /// プレーン 1 行を画面末へ（終了メッセージや起動失敗）。折り返して書く。
        /// </summary>
        /// <param name="text">本文。null は空。</param>
        /// <param name="slot">色。</param>
        public void WritePlainLine(string text, ColorSlot slot)
        {
            ColorSlot prev = this.currentSlot;
            this.currentSlot = slot;
            this.wrapPending = false;
            this.caretCol = 0;
            if (this.caretRow < this.rows - 1)
            {
                this.caretRow++;
            }
            else
            {
                this.ScrollUp();
            }

            if (text != null)
            {
                int i = 0;
                while (i < text.Length)
                {
                    int units;
                    int w = CellWidth.Of(text, i, out units);
                    if (w <= 0)
                    {
                        i += (units < 1) ? 1 : units;
                        continue;
                    }

                    this.PutChar(text[i]);
                    i += units;
                }
            }

            this.currentSlot = prev;
            this.CarriageReturn();
            this.LineFeed();
        }

        private int savedColumn;
        private int savedRow;
        private bool hasSavedCaret;

        /// <summary>ESC 7。キャレット位置を覚える。</summary>
        public void SaveCaret()
        {
            this.savedColumn = this.caretCol;
            this.savedRow = this.caretRow;
            this.hasSavedCaret = true;
        }

        /// <summary>ESC 8。保存が無ければ何もしない。</summary>
        public void RestoreCaret()
        {
            if (!this.hasSavedCaret)
            {
                return;
            }

            this.wrapPending = false;
            this.caretCol = this.savedColumn;
            this.caretRow = this.savedRow;
            if (this.caretRow >= this.rows)
            {
                this.caretRow = this.rows - 1;
            }

            if (this.caretCol > this.columns)
            {
                this.caretCol = this.columns;
            }
        }

        private void EnsureWrap()
        {
            if (!this.wrapPending)
            {
                return;
            }

            this.wrapPending = false;
            this.caretCol = 0;
            this.LineFeed();
        }

        private void ScrollUp()
        {
            this.scrollback.Add(CloneRow(this.screen[0]));
            while (this.scrollback.Count > MaxScrollback)
            {
                this.scrollback.RemoveAt(0);
            }

            int r;
            for (r = 0; r < this.rows - 1; r++)
            {
                this.screen[r] = this.screen[r + 1];
            }

            this.screen[this.rows - 1] = this.NewRow();
        }

        private void ApplySize(int newColumns, int newRows, bool forceEmpty)
        {
            if (newColumns < MinColumns)
            {
                newColumns = MinColumns;
            }

            if (newRows < MinRows)
            {
                newRows = MinRows;
            }

            if (!forceEmpty && this.screen != null && newColumns == this.columns && newRows == this.rows)
            {
                return;
            }

            VtCell[][] old = this.screen;
            int oldRows = this.rows;
            int oldCols = this.columns;
            this.columns = newColumns;
            this.rows = newRows;
            this.screen = new VtCell[newRows][];
            int r;
            for (r = 0; r < newRows; r++)
            {
                this.screen[r] = this.NewRow();
            }

            if (old != null && !forceEmpty)
            {
                if (newRows < oldRows)
                {
                    int extra = oldRows - newRows;
                    int i;
                    for (i = 0; i < extra; i++)
                    {
                        this.scrollback.Add(CloneRowResized(old[i], newColumns));
                    }

                    while (this.scrollback.Count > MaxScrollback)
                    {
                        this.scrollback.RemoveAt(0);
                    }

                    for (r = 0; r < newRows; r++)
                    {
                        this.CopyRow(old[extra + r], this.screen[r], oldCols, newColumns);
                    }
                }
                else
                {
                    for (r = 0; r < oldRows && r < newRows; r++)
                    {
                        this.CopyRow(old[r], this.screen[r], oldCols, newColumns);
                    }
                }
            }

            if (this.caretRow >= this.rows)
            {
                this.caretRow = this.rows - 1;
            }

            if (this.caretCol > this.columns)
            {
                this.caretCol = this.columns;
            }

            this.wrapPending = false;
        }

        private void FillScreenEmpty()
        {
            int r;
            for (r = 0; r < this.rows; r++)
            {
                this.screen[r] = this.NewRow();
            }
        }

        private void ClearRow(int row)
        {
            this.screen[row] = this.NewRow();
        }

        private VtCell[] NewRow()
        {
            return new VtCell[this.columns];
        }

        private static VtCell[] CloneRow(VtCell[] src)
        {
            VtCell[] dst = new VtCell[src.Length];
            Array.Copy(src, dst, src.Length);
            return dst;
        }

        private static VtCell[] CloneRowResized(VtCell[] src, int cols)
        {
            VtCell[] dst = new VtCell[cols];
            int n = src.Length;
            if (n > cols)
            {
                n = cols;
            }

            Array.Copy(src, dst, n);
            return dst;
        }

        private void CopyRow(VtCell[] src, VtCell[] dst, int oldCols, int newCols)
        {
            int n = oldCols;
            if (n > newCols)
            {
                n = newCols;
            }

            Array.Copy(src, dst, n);
        }

        private void ClearWideAt(int row, int col)
        {
            if (row < 0 || row >= this.rows || col < 0 || col >= this.columns)
            {
                return;
            }

            if (this.screen[row][col].Continuation && col > 0)
            {
                this.screen[row][col - 1] = new VtCell();
                this.screen[row][col] = new VtCell();
                return;
            }

            if (!this.screen[row][col].Continuation)
            {
                int w = CellWidth.Of(this.screen[row][col].Character);
                this.screen[row][col] = new VtCell();
                if (w == 2 && col + 1 < this.columns)
                {
                    this.screen[row][col + 1] = new VtCell();
                }
            }
        }
    }
}
