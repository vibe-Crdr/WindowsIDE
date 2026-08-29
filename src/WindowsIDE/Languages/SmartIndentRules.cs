using System.Collections.Generic;
using WindowsIDE.Editor;

namespace WindowsIDE.Languages
{
    /// <summary>
    /// F-SIND の適用種別。CopyOnly は F-IND の前行空白コピーのみ。
    /// </summary>
    public enum SmartIndentKind
    {
        CopyOnly,
        Increase,
        Decrease,
        SplitPair,
        InsertBlockClose
    }

    /// <summary>
    /// Enter 時の F-SIND 計画。CloseText は InsertBlockClose のときだけ使う。
    /// </summary>
    public struct SmartIndentPlan
    {
        /// <summary>適用種別。</summary>
        public SmartIndentKind Kind;

        /// <summary>VBA 終端行のテキスト。それ以外は null。</summary>
        public string CloseText;
    }

    /// <summary>
    /// F-IND の上に載せる言語対応インデント。F-VBA-CASE は呼ばない。
    /// </summary>
    public static class SmartIndentRules
    {
        /// <summary>
        /// Enter 時点の計画を返す。cmd / Plain は CopyOnly。
        /// </summary>
        /// <param name="language">言語。</param>
        /// <param name="buffer">本文。</param>
        /// <param name="session">行開始状態。</param>
        /// <param name="line">キャレット行。</param>
        /// <param name="column">キャレット列。</param>
        /// <param name="tabSize">1 段のスペース数。</param>
        /// <returns>種別と任意の終端テキスト。</returns>
        public static SmartIndentPlan Plan(LanguageKind language, TextBuffer buffer, HighlightSession session, int line, int column, int tabSize)
        {
            SmartIndentPlan plan = new SmartIndentPlan();
            plan.Kind = SmartIndentKind.CopyOnly;
            plan.CloseText = null;
            if (buffer == null)
            {
                return plan;
            }

            if (language == LanguageKind.Cmd || language == LanguageKind.Plain)
            {
                return plan;
            }

            if (language == LanguageKind.Vba)
            {
                return PlanVba(buffer, session, line, column);
            }

            if (language == LanguageKind.CSharp || language == LanguageKind.PowerShell)
            {
                return PlanBraces(language, buffer, session, line, column);
            }

            return plan;
        }

        private static SmartIndentPlan PlanBraces(LanguageKind language, TextBuffer buffer, HighlightSession session, int line, int column)
        {
            SmartIndentPlan plan = new SmartIndentPlan();
            plan.Kind = SmartIndentKind.CopyOnly;
            if (line < 0 || line >= buffer.LineCount)
            {
                return plan;
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

            List<Token> tokens = new List<Token>();
            BraceMatch.ScanOneLine(language, buffer, session, line, tokens);

            if (column > 0 && column < text.Length && text[column - 1] == '{' && text[column] == '}')
            {
                TokenKind leftKind = BraceMatch.KindAt(tokens, column - 1);
                TokenKind rightKind = BraceMatch.KindAt(tokens, column);
                if (leftKind != TokenKind.String && leftKind != TokenKind.Comment &&
                    rightKind != TokenKind.String && rightKind != TokenKind.Comment)
                {
                    plan.Kind = SmartIndentKind.SplitPair;
                    return plan;
                }
            }

            if (LastEffectiveIsOpenBrace(tokens, text, column))
            {
                plan.Kind = SmartIndentKind.Increase;
            }

            return plan;
        }

        private static bool LastEffectiveIsOpenBrace(List<Token> tokens, string text, int column)
        {
            int lastStart = -1;
            int i = 0;
            while (i < tokens.Count)
            {
                Token token = tokens[i];
                int end = token.Start + token.Length;
                if (end > column)
                {
                    break;
                }

                if (token.Kind == TokenKind.Comment)
                {
                    i++;
                    continue;
                }

                if (IsWhitespaceToken(text, token))
                {
                    i++;
                    continue;
                }

                lastStart = token.Start;
                i++;
            }

            if (lastStart < 0 || lastStart >= text.Length)
            {
                return false;
            }

            return text[lastStart] == '{' && (lastStart + 1 == text.Length || lastStart + 1 <= column);
        }

        private static bool IsWhitespaceToken(string text, Token token)
        {
            if (token.Length <= 0 || token.Start < 0 || token.Start >= text.Length)
            {
                return false;
            }

            int i = token.Start;
            int end = token.Start + token.Length;
            if (end > text.Length)
            {
                end = text.Length;
            }

            while (i < end)
            {
                char c = text[i];
                if (c != ' ' && c != '\t')
                {
                    return false;
                }

                i++;
            }

            return true;
        }

        private static SmartIndentPlan PlanVba(TextBuffer buffer, HighlightSession session, int line, int column)
        {
            SmartIndentPlan plan = new SmartIndentPlan();
            plan.Kind = SmartIndentKind.CopyOnly;
            if (!CaretAfterLineCode(buffer, session, line, column))
            {
                return plan;
            }

            string closeText;
            if (!VbaBlockRules.TryGetBlockClose(buffer, session, line, out closeText))
            {
                return plan;
            }

            if (VbaBlockRules.IsOneLineIf(buffer, session, line) || VbaBlockRules.IsThenContinuationOnly(buffer, session, line))
            {
                return plan;
            }

            if (VbaBlockRules.HasMatchingClose(buffer, session, line))
            {
                plan.Kind = SmartIndentKind.Increase;
                return plan;
            }

            plan.Kind = SmartIndentKind.InsertBlockClose;
            plan.CloseText = closeText;
            return plan;
        }

        private static bool CaretAfterLineCode(TextBuffer buffer, HighlightSession session, int line, int column)
        {
            if (line < 0 || line >= buffer.LineCount)
            {
                return false;
            }

            string text = buffer.GetLine(line);
            List<Token> tokens = new List<Token>();
            BraceMatch.ScanOneLine(LanguageKind.Vba, buffer, session, line, tokens);
            int lastCodeEnd = 0;
            bool any = false;
            int i = 0;
            while (i < tokens.Count)
            {
                Token token = tokens[i];
                if (token.Kind == TokenKind.Comment)
                {
                    i++;
                    continue;
                }

                if (IsWhitespaceToken(text, token))
                {
                    i++;
                    continue;
                }

                any = true;
                lastCodeEnd = token.Start + token.Length;
                i++;
            }

            if (!any)
            {
                return false;
            }

            return column >= lastCodeEnd;
        }
    }
}
