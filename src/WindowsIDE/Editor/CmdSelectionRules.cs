using System;

namespace WindowsIDE.Editor
{
    /// <summary>
    /// cmd 選択行実行の抽出規則。WinForms に依存しない。
    /// </summary>
    public static class CmdSelectionRules
    {
        /// <summary>
        /// 実行する本文を取り出す。選択なしは現在行。同一行は文字どおり。複数行は BlockLastLine までを CRLF で結合する。
        /// </summary>
        /// <param name="buffer">対象バッファ。</param>
        /// <param name="caretLine">選択が無いときの行。</param>
        /// <param name="start">選択開始。</param>
        /// <param name="end">選択終了。</param>
        /// <returns>実行本文。buffer が無ければ空。</returns>
        public static string Extract(TextBuffer buffer, int caretLine, BufferPoint start, BufferPoint end)
        {
            if (buffer == null)
            {
                return "";
            }

            NormalizeRange(ref start, ref end);
            if (start.Line == end.Line && start.Column == end.Column)
            {
                if (caretLine < 0 || caretLine >= buffer.LineCount)
                {
                    return "";
                }

                return buffer.GetLine(caretLine);
            }

            if (start.Line == end.Line)
            {
                return buffer.GetText(start, end);
            }

            int last = IndentRules.BlockLastLine(start, end);
            BufferPoint adjEnd = end;
            if (last != end.Line)
            {
                if (last < 0)
                {
                    last = 0;
                }

                if (last >= buffer.LineCount)
                {
                    last = buffer.LineCount - 1;
                }

                adjEnd = new BufferPoint(last, buffer.GetLineLength(last));
            }

            return ToCrLf(buffer.GetText(start, adjEnd));
        }

        /// <summary>
        /// 空、または空白・タブ・CR・LF だけのとき true。
        /// </summary>
        /// <param name="text">判定する本文。null は空と同じ。</param>
        /// <returns>実行できる文字が無ければ true。</returns>
        public static bool IsBlank(string text)
        {
            if (text == null || text.Length == 0)
            {
                return true;
            }

            int i = 0;
            while (i < text.Length)
            {
                char c = text[i];
                if (c != ' ' && c != '\t' && c != '\r' && c != '\n')
                {
                    return false;
                }

                i++;
            }

            return true;
        }

        private static void NormalizeRange(ref BufferPoint start, ref BufferPoint end)
        {
            if (end.Line < start.Line || (end.Line == start.Line && end.Column < start.Column))
            {
                BufferPoint swap = start;
                start = end;
                end = swap;
            }
        }

        private static string ToCrLf(string text)
        {
            if (text == null || text.Length == 0)
            {
                return (text == null) ? "" : text;
            }

            return text.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n");
        }
    }
}
