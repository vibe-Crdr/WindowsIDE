using System;
using System.Collections.Generic;
using System.Text;

namespace WindowsIDE.Editor
{
    /// <summary>
    /// バッファ内の行と列（0 始まり、UTF-16 単位）。
    /// </summary>
    public struct BufferPoint
    {
        private int line;
        private int column;

        /// <summary>
        /// 行と列から位置を作る。
        /// </summary>
        public BufferPoint(int line, int column)
        {
            this.line = line;
            this.column = column;
        }

        /// <summary>0 始まりの行。</summary>
        public int Line { get { return this.line; } set { this.line = value; } }

        /// <summary>0 始まりの列（UTF-16 単位）。</summary>
        public int Column { get { return this.column; } set { this.column = value; } }
    }

    /// <summary>
    /// 行単位のテキストバッファ。挿入・削除と全文取得を行う。WinForms に依存しない。
    /// </summary>
    public sealed class TextBuffer
    {
        private readonly List<string> lines;
        private string newLine;

        /// <summary>
        /// 空の 1 行バッファを CRLF で作る。
        /// </summary>
        public TextBuffer()
        {
            this.lines = new List<string>();
            this.lines.Add("");
            this.newLine = "\r\n";
        }

        /// <summary>行の結合に使う改行（CRLF または LF）。</summary>
        public string NewLine
        {
            get { return this.newLine; }
            set
            {
                if (value == "\n" || value == "\r\n")
                {
                    this.newLine = value;
                }
                else
                {
                    this.newLine = "\r\n";
                }
            }
        }

        /// <summary>行数。常に 1 以上。</summary>
        public int LineCount
        {
            get { return this.lines.Count; }
        }

        /// <summary>
        /// 指定行のテキストを返す（改行は含まない）。
        /// </summary>
        public string GetLine(int index)
        {
            this.ValidateLine(index);
            return this.lines[index];
        }

        /// <summary>
        /// 指定行の長さ（UTF-16 単位）を返す。
        /// </summary>
        public int GetLineLength(int index)
        {
            this.ValidateLine(index);
            return this.lines[index].Length;
        }

        /// <summary>
        /// 全文を現在の改行コードで結合して返す。
        /// </summary>
        public string GetText()
        {
            return string.Join(this.newLine, this.lines.ToArray());
        }

        /// <summary>
        /// 範囲のテキストを現在の改行コードで返す。
        /// </summary>
        public string GetText(BufferPoint start, BufferPoint end)
        {
            this.NormalizeRange(ref start, ref end);
            if (start.Line == end.Line)
            {
                string line = this.lines[start.Line];
                int a = this.ClampColumn(start.Line, start.Column);
                int b = this.ClampColumn(end.Line, end.Column);
                return line.Substring(a, b - a);
            }

            StringBuilder sb = new StringBuilder();
            string first = this.lines[start.Line];
            int sc = this.ClampColumn(start.Line, start.Column);
            sb.Append(first.Substring(sc));
            sb.Append(this.newLine);
            for (int i = start.Line + 1; i < end.Line; i++)
            {
                sb.Append(this.lines[i]);
                sb.Append(this.newLine);
            }

            string last = this.lines[end.Line];
            int ec = this.ClampColumn(end.Line, end.Column);
            sb.Append(last.Substring(0, ec));
            return sb.ToString();
        }

        /// <summary>
        /// バッファ全体を置き換える。改行は \n に正規化して行分割する。Undo は触らない。
        /// </summary>
        public void SetText(string text)
        {
            this.lines.Clear();
            if (text == null)
            {
                text = "";
            }

            string normalized = text.Replace("\r\n", "\n").Replace("\r", "\n");
            string[] parts = normalized.Split('\n');
            this.lines.AddRange(parts);
            if (this.lines.Count == 0)
            {
                this.lines.Add("");
            }
        }

        /// <summary>
        /// 指定位置に文字列を挿入する。戻り値は挿入後のキャレット位置。
        /// </summary>
        public BufferPoint Insert(int line, int column, string text)
        {
            if (text == null || text.Length == 0)
            {
                return new BufferPoint(line, column);
            }

            this.ValidateLine(line);
            column = this.ClampColumn(line, column);
            string cur = this.lines[line];
            string prefix = cur.Substring(0, column);
            string suffix = cur.Substring(column);
            string normalized = text.Replace("\r\n", "\n").Replace("\r", "\n");
            string[] parts = normalized.Split('\n');
            if (parts.Length == 1)
            {
                this.lines[line] = prefix + parts[0] + suffix;
                return new BufferPoint(line, prefix.Length + parts[0].Length);
            }

            this.lines[line] = prefix + parts[0];
            for (int i = 1; i < parts.Length - 1; i++)
            {
                this.lines.Insert(line + i, parts[i]);
            }

            int lastIndex = line + parts.Length - 1;
            this.lines.Insert(lastIndex, parts[parts.Length - 1] + suffix);
            return new BufferPoint(lastIndex, parts[parts.Length - 1].Length);
        }

        /// <summary>
        /// 範囲を削除し、削除したテキストを現在の改行コードで返す。
        /// </summary>
        public string Delete(BufferPoint start, BufferPoint end)
        {
            this.NormalizeRange(ref start, ref end);
            if (start.Line == end.Line && start.Column == end.Column)
            {
                return "";
            }

            string deleted = this.GetText(start, end);
            if (start.Line == end.Line)
            {
                string line = this.lines[start.Line];
                int a = this.ClampColumn(start.Line, start.Column);
                int b = this.ClampColumn(end.Line, end.Column);
                this.lines[start.Line] = line.Substring(0, a) + line.Substring(b);
                return deleted;
            }

            string first = this.lines[start.Line];
            string last = this.lines[end.Line];
            int sc = this.ClampColumn(start.Line, start.Column);
            int ec = this.ClampColumn(end.Line, end.Column);
            this.lines[start.Line] = first.Substring(0, sc) + last.Substring(ec);
            int removeCount = end.Line - start.Line;
            this.lines.RemoveRange(start.Line + 1, removeCount);
            return deleted;
        }

        /// <summary>
        /// 位置をバッファ範囲内に収める。
        /// </summary>
        public BufferPoint Clamp(BufferPoint point)
        {
            int line = point.Line;
            if (line < 0)
            {
                line = 0;
            }

            if (line >= this.lines.Count)
            {
                line = this.lines.Count - 1;
            }

            return new BufferPoint(line, this.ClampColumn(line, point.Column));
        }

        private int ClampColumn(int line, int column)
        {
            if (column < 0)
            {
                return 0;
            }

            int len = this.lines[line].Length;
            if (column > len)
            {
                return len;
            }

            return column;
        }

        private void ValidateLine(int index)
        {
            if (index < 0 || index >= this.lines.Count)
            {
                throw new ArgumentOutOfRangeException("index");
            }
        }

        private void NormalizeRange(ref BufferPoint start, ref BufferPoint end)
        {
            start = this.Clamp(start);
            end = this.Clamp(end);
            if (start.Line > end.Line || (start.Line == end.Line && start.Column > end.Column))
            {
                BufferPoint tmp = start;
                start = end;
                end = tmp;
            }
        }
    }
}
