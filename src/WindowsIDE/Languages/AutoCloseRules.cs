using WindowsIDE.Editor;

namespace WindowsIDE.Languages
{
    /// <summary>
    /// C# / PowerShell の対括弧自動閉じ可否と閉じ上書きスキップ（P17）。既対は同じ indent のスタック対。VBA / cmd / Plain は入れない。
    /// </summary>
    public static class AutoCloseRules
    {
        /// <summary>
        /// typedOpener の直後に closer を入れるべきなら true。同じ indent の既対では入れない。
        /// </summary>
        /// <param name="language">言語。</param>
        /// <param name="buffer">本文。</param>
        /// <param name="session">行開始状態。</param>
        /// <param name="line">キャレット行。</param>
        /// <param name="column">キャレット列。</param>
        /// <param name="typedOpener">入力した開き括弧。</param>
        /// <param name="closer">入れる閉じ括弧。</param>
        /// <returns>閉じを入れるとき true。</returns>
        public static bool ShouldInsertCloser(LanguageKind language, TextBuffer buffer, HighlightSession session, int line, int column, char typedOpener, out char closer)
        {
            closer = '\0';
            if (buffer == null)
            {
                return false;
            }

            if (language != LanguageKind.CSharp && language != LanguageKind.PowerShell)
            {
                return false;
            }

            if (!BracePairs.TryGetCloser(language, typedOpener, out closer))
            {
                closer = '\0';
                return false;
            }

            if (line < 0 || line >= buffer.LineCount)
            {
                return false;
            }

            string text = buffer.GetLine(line);
            if (column < 0)
            {
                column = 0;
            }

            if (column > text.Length)
            {
                column = text.Length;
            }

            TokenKind kind = BraceMatch.TokenKindAt(language, buffer, session, line, column);
            if (kind == TokenKind.String || kind == TokenKind.Comment)
            {
                return false;
            }

            if (column < text.Length && text[column] == closer)
            {
                return false;
            }

            if (BraceMatch.WouldMatchInsertedOpener(language, buffer, session, line, column, typedOpener))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 入力した閉じ括弧がキャレット直後と同じなら挿入せずスキップすべきとき true。
        /// </summary>
        /// <param name="language">言語。</param>
        /// <param name="buffer">本文。</param>
        /// <param name="session">行開始状態。</param>
        /// <param name="line">キャレット行。</param>
        /// <param name="column">キャレット列（直後の閉じの位置）。</param>
        /// <param name="typed">入力した閉じ括弧。</param>
        /// <returns>挿入せずキャレットだけ進めるとき true。</returns>
        public static bool ShouldSkipTypedCloser(LanguageKind language, TextBuffer buffer, HighlightSession session, int line, int column, char typed)
        {
            if (buffer == null)
            {
                return false;
            }

            if (language != LanguageKind.CSharp && language != LanguageKind.PowerShell)
            {
                return false;
            }

            if (!BracePairs.IsCloser(language, typed))
            {
                return false;
            }

            if (line < 0 || line >= buffer.LineCount)
            {
                return false;
            }

            string text = buffer.GetLine(line);
            if (column < 0)
            {
                column = 0;
            }

            if (column > text.Length)
            {
                column = text.Length;
            }

            TokenKind kind = BraceMatch.TokenKindAt(language, buffer, session, line, column);
            if (kind == TokenKind.String || kind == TokenKind.Comment)
            {
                return false;
            }

            if (column >= text.Length)
            {
                return false;
            }

            return text[column] == typed;
        }
    }
}
