using System.Collections.Generic;

namespace WindowsIDE.Languages
{
    /// <summary>
    /// 不明言語。行全体を Text にする。状態は常に Normal。
    /// </summary>
    public sealed class PlainLexer : ILineLexer
    {
        /// <summary>
        /// 行全体を Text として出す。endState は 0。
        /// </summary>
        /// <param name="line">対象行。</param>
        /// <param name="startState">無視する。</param>
        /// <param name="tokens">出力先。</param>
        /// <param name="endState">常に 0。</param>
        public void ScanLine(string line, int startState, List<Token> tokens, out int endState)
        {
            endState = 0;
            if (line == null)
            {
                return;
            }

            ScanChars.Add(tokens, 0, line.Length, TokenKind.Text);
        }
    }
}
