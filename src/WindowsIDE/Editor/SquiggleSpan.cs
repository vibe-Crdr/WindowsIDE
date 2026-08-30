using System;
using System.Collections.Generic;

namespace WindowsIDE.Editor
{
    /// <summary>
    /// 波線の 1 行分。行と列は 0 始まり。終了列は排他。WinForms に依存しない。
    /// </summary>
    public sealed class SquiggleSpan
    {
        private int line;
        private int startColumn;
        private int endColumn;

        private SquiggleSpan()
        {
        }

        /// <summary>0 始まりの行。</summary>
        public int Line
        {
            get { return this.line; }
        }

        /// <summary>0 始まりの開始列。</summary>
        public int StartColumn
        {
            get { return this.startColumn; }
        }

        /// <summary>0 始まりの終了列（排他）。</summary>
        public int EndColumn
        {
            get { return this.endColumn; }
        }

        /// <summary>
        /// 1 行の終了列を決める。負または未指定はトリム後末尾。空なら開始+1。
        /// </summary>
        /// <param name="lineText">その行の本文。null は空。</param>
        /// <param name="startColumn">0 始まりの開始列。</param>
        /// <param name="endColumn">0 始まりの終了列（排他）。負なら未指定。</param>
        /// <returns>0 始まりの終了列（排他）。</returns>
        public static int Resolve(string lineText, int startColumn, int endColumn)
        {
            if (lineText == null)
            {
                lineText = "";
            }

            int start = startColumn;
            if (start < 0)
            {
                start = 0;
            }

            int end;
            if (endColumn < 0)
            {
                end = lineText.TrimEnd().Length;
            }
            else
            {
                end = endColumn;
            }

            if (end <= start)
            {
                end = start + 1;
            }

            return end;
        }

        /// <summary>
        /// 1 行の波線区間を作る。
        /// </summary>
        /// <param name="line">0 始まりの行。</param>
        /// <param name="lineText">その行の本文。</param>
        /// <param name="startColumn">0 始まりの開始列。</param>
        /// <param name="endColumn">0 始まりの終了列（排他）。負なら未指定。</param>
        /// <returns>区間。</returns>
        public static SquiggleSpan FromLine(int line, string lineText, int startColumn, int endColumn)
        {
            int start = startColumn;
            if (start < 0)
            {
                start = 0;
            }

            SquiggleSpan span = new SquiggleSpan();
            span.line = line;
            span.startColumn = start;
            span.endColumn = Resolve(lineText, start, endColumn);
            return span;
        }

        /// <summary>
        /// 範囲を行ごとの区間にする。複数行は行ごと。
        /// </summary>
        /// <param name="lines">各行の本文。null は空。</param>
        /// <param name="startLine">0 始まりの開始行。</param>
        /// <param name="startColumn">0 始まりの開始列。</param>
        /// <param name="endLine">0 始まりの終了行。負または開始より前なら開始行だけ。</param>
        /// <param name="endColumn">最終行の 0 始まり終了列（排他）。負なら未指定。</param>
        /// <returns>行ごとの区間。</returns>
        public static SquiggleSpan[] FromRange(string[] lines, int startLine, int startColumn, int endLine, int endColumn)
        {
            if (lines == null || lines.Length == 0)
            {
                return new SquiggleSpan[] { FromLine(0, "", startColumn, endColumn) };
            }

            int first = startLine;
            if (first < 0)
            {
                first = 0;
            }

            if (first >= lines.Length)
            {
                first = lines.Length - 1;
            }

            int last = endLine;
            if (last < first)
            {
                last = first;
            }

            if (last >= lines.Length)
            {
                last = lines.Length - 1;
            }

            List<SquiggleSpan> list = new List<SquiggleSpan>();
            int line = first;
            while (line <= last)
            {
                string text = lines[line];
                int start;
                int end;
                if (line == first && line == last)
                {
                    start = startColumn;
                    end = endColumn;
                }
                else if (line == first)
                {
                    start = startColumn;
                    end = -1;
                }
                else if (line == last)
                {
                    start = 0;
                    end = endColumn;
                }
                else
                {
                    start = 0;
                    end = -1;
                }

                list.Add(FromLine(line, text, start, end));
                line++;
            }

            return list.ToArray();
        }
    }
}
