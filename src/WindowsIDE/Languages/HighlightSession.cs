using System;
using System.Collections.Generic;
using WindowsIDE.Editor;

namespace WindowsIDE.Languages
{
    /// <summary>
    /// 文書ごとの行開始状態。トークンはキャッシュせず、描画時に可視行だけ走査する。
    /// </summary>
    public sealed class HighlightSession
    {
        private LanguageKind language;
        private int[] startStates;
        private int validThrough;

        /// <summary>
        /// 空のセッションを Plain・1 行で作る。
        /// </summary>
        public HighlightSession()
        {
            this.language = LanguageKind.Plain;
            this.startStates = new int[1];
            this.startStates[0] = 0;
            this.validThrough = 0;
        }

        /// <summary>
        /// 言語と行数を入れ直し、開始状態を捨てる。
        /// </summary>
        /// <param name="language">判定済み言語。</param>
        /// <param name="lineCount">バッファの行数。</param>
        public void Reset(LanguageKind language, int lineCount)
        {
            this.language = language;
            if (lineCount < 1)
            {
                lineCount = 1;
            }

            this.startStates = new int[lineCount];
            this.startStates[0] = 0;
            this.validThrough = 0;
        }

        /// <summary>
        /// 指定行以降の開始状態を無効にする。Undo/Redo は 0 でよい。
        /// </summary>
        /// <param name="line">再計算を始める行。</param>
        public void InvalidateFrom(int line)
        {
            if (line < 0)
            {
                line = 0;
            }

            if (this.validThrough > line)
            {
                this.validThrough = line;
            }
        }

        /// <summary>
        /// 編集行から前方再走査する。lastModifiedLine は editLine と同じ。
        /// </summary>
        /// <param name="buffer">本文。</param>
        /// <param name="editLine">編集開始行。</param>
        public void SyncAfterEdit(TextBuffer buffer, int editLine)
        {
            this.SyncAfterEdit(buffer, editLine, editLine);
        }

        /// <summary>
        /// 編集開始行から前方再走査する。開始状態が一致しても lastModifiedLine より前では止めない。
        /// </summary>
        /// <param name="buffer">本文。</param>
        /// <param name="editLine">編集開始行。</param>
        /// <param name="lastModifiedLine">変更範囲の最終行。これより前は early-stop しない。</param>
        public void SyncAfterEdit(TextBuffer buffer, int editLine, int lastModifiedLine)
        {
            if (buffer == null)
            {
                return;
            }

            int lineCount = buffer.LineCount;
            if (lineCount < 1)
            {
                lineCount = 1;
            }

            if (editLine < 0)
            {
                editLine = 0;
            }

            if (editLine >= lineCount)
            {
                editLine = lineCount - 1;
            }

            if (lastModifiedLine < editLine)
            {
                lastModifiedLine = editLine;
            }

            if (lastModifiedLine >= lineCount)
            {
                lastModifiedLine = lineCount - 1;
            }

            int oldCount = this.startStates.Length;
            bool countChanged = oldCount != lineCount;
            int oldValid = this.validThrough;
            this.ResizeStates(lineCount);
            this.startStates[0] = 0;
            if (editLine > this.validThrough)
            {
                editLine = this.validThrough;
            }

            ILineLexer lexer = LexerRegistry.Get(this.language);
            List<Token> scratch = new List<Token>();
            int i = editLine;
            while (i < lineCount)
            {
                scratch.Clear();
                int endState;
                lexer.ScanLine(buffer.GetLine(i), this.startStates[i], scratch, out endState);
                if (i + 1 < lineCount)
                {
                    int old = this.startStates[i + 1];
                    bool comparable = !countChanged && (i + 1 <= oldValid);
                    this.startStates[i + 1] = endState;
                    if (this.validThrough < i + 1)
                    {
                        this.validThrough = i + 1;
                    }

                    if (comparable && old == endState && i >= lastModifiedLine)
                    {
                        this.validThrough = oldValid;
                        return;
                    }
                }
                else if (this.validThrough < i)
                {
                    this.validThrough = i;
                }

                i++;
            }

            this.validThrough = lineCount - 1;
        }

        /// <summary>
        /// 行の開始状態を返す。未計算なら 0。
        /// </summary>
        /// <param name="line">0 始まりの行。</param>
        /// <returns>レキサ内の状態値。</returns>
        public int GetStartState(int line)
        {
            if (line <= 0)
            {
                return 0;
            }

            if (this.startStates == null || line >= this.startStates.Length)
            {
                return 0;
            }

            if (line > this.validThrough)
            {
                return 0;
            }

            return this.startStates[line];
        }

        private void ResizeStates(int lineCount)
        {
            if (this.startStates != null && this.startStates.Length == lineCount)
            {
                return;
            }

            int[] next = new int[lineCount];
            if (this.startStates != null)
            {
                int copy = Math.Min(this.startStates.Length, lineCount);
                Array.Copy(this.startStates, next, copy);
            }

            this.startStates = next;
            if (this.validThrough >= lineCount)
            {
                this.validThrough = lineCount - 1;
            }
        }
    }
}
