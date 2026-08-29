using System.Collections.Generic;
using WindowsIDE.Editor;

namespace WindowsIDE.Languages
{
    /// <summary>
    /// 対括弧照合の結果。対が無ければ PairLine は -1。
    /// </summary>
    public struct BraceMatchResult
    {
        /// <summary>隣接する括弧が見つかった。</summary>
        public bool Found;

        /// <summary>対の種類が一致した。</summary>
        public bool Matched;

        /// <summary>アンカーの行。</summary>
        public int AnchorLine;

        /// <summary>アンカーの列。</summary>
        public int AnchorColumn;

        /// <summary>対の行。無ければ -1。</summary>
        public int PairLine;

        /// <summary>対の列。</summary>
        public int PairColumn;
    }

    /// <summary>
    /// キャレット隣接の対括弧を、行レキサの括弧トークンをスタック照合して探す。
    /// </summary>
    public static class BraceMatch
    {
        /// <summary>
        /// キャレット位置の隣接括弧と対を返す。cmd / Plain は Found=false。
        /// </summary>
        /// <param name="language">言語。</param>
        /// <param name="buffer">本文。</param>
        /// <param name="session">行開始状態。null なら 0 から走査する。</param>
        /// <param name="line">キャレット行。</param>
        /// <param name="column">キャレット列。</param>
        /// <returns>アンカー・対・一致フラグ。</returns>
        public static BraceMatchResult Find(LanguageKind language, TextBuffer buffer, HighlightSession session, int line, int column)
        {
            BraceMatchResult empty = new BraceMatchResult();
            empty.PairLine = -1;
            if (buffer == null || !HasAnyBrace(language))
            {
                return empty;
            }

            int anchorLine;
            int anchorColumn;
            if (!TryGetAnchor(language, buffer, session, line, column, out anchorLine, out anchorColumn))
            {
                return empty;
            }

            List<BraceSite> sites = CollectSites(language, buffer, session, '\0', -1, -1);
            int anchorIndex = IndexOf(sites, anchorLine, anchorColumn);
            if (anchorIndex < 0)
            {
                return empty;
            }

            int[] pairs;
            bool[] matched;
            MatchSites(language, sites, out pairs, out matched);

            BraceMatchResult result = new BraceMatchResult();
            result.Found = true;
            result.AnchorLine = anchorLine;
            result.AnchorColumn = anchorColumn;
            result.Matched = matched[anchorIndex];
            if (pairs[anchorIndex] >= 0)
            {
                BraceSite pair = sites[pairs[anchorIndex]];
                result.PairLine = pair.Line;
                result.PairColumn = pair.Column;
            }
            else
            {
                result.PairLine = -1;
                result.PairColumn = 0;
            }

            return result;
        }

        /// <summary>
        /// opener だけを (line, column) に入れたと仮定し、既存の閉じと組めるなら true。
        /// </summary>
        /// <param name="language">言語。</param>
        /// <param name="buffer">本文。</param>
        /// <param name="session">行開始状態。</param>
        /// <param name="line">挿入行。</param>
        /// <param name="column">挿入列。</param>
        /// <param name="opener">開き括弧。</param>
        /// <returns>仮想 opener が対と一致するとき true。</returns>
        public static bool WouldMatchInsertedOpener(LanguageKind language, TextBuffer buffer, HighlightSession session, int line, int column, char opener)
        {
            if (buffer == null || !BracePairs.IsOpener(language, opener))
            {
                return false;
            }

            List<BraceSite> sites = CollectSites(language, buffer, session, opener, line, column);
            int virtualIndex = IndexOf(sites, line, column);
            if (virtualIndex < 0)
            {
                return false;
            }

            int[] pairs;
            bool[] matched;
            MatchSites(language, sites, out pairs, out matched);
            return matched[virtualIndex];
        }

        /// <summary>
        /// 指定列のトークン種類。列がトークン外なら Text。
        /// </summary>
        /// <param name="language">言語。</param>
        /// <param name="buffer">本文。</param>
        /// <param name="session">行開始状態。</param>
        /// <param name="line">行。</param>
        /// <param name="column">列。</param>
        /// <returns>TokenKind。</returns>
        public static TokenKind TokenKindAt(LanguageKind language, TextBuffer buffer, HighlightSession session, int line, int column)
        {
            List<Token> tokens = new List<Token>();
            ScanOneLine(language, buffer, session, line, tokens);
            return KindAt(tokens, column);
        }

        /// <summary>
        /// 1 行を走査して tokens を埋める。
        /// </summary>
        /// <param name="language">言語。</param>
        /// <param name="buffer">本文。</param>
        /// <param name="session">行開始状態。</param>
        /// <param name="line">行。</param>
        /// <param name="tokens">出力。先に Clear する。</param>
        public static void ScanOneLine(LanguageKind language, TextBuffer buffer, HighlightSession session, int line, List<Token> tokens)
        {
            if (tokens == null)
            {
                return;
            }

            tokens.Clear();
            if (buffer == null || line < 0 || line >= buffer.LineCount)
            {
                return;
            }

            ILineLexer lexer = LexerRegistry.Get(language);
            int start = 0;
            if (session != null)
            {
                start = session.GetStartState(line);
            }

            int endState;
            lexer.ScanLine(buffer.GetLine(line), start, tokens, out endState);
        }

        private static bool HasAnyBrace(LanguageKind language)
        {
            return language == LanguageKind.CSharp || language == LanguageKind.PowerShell || language == LanguageKind.Vba;
        }

        private static bool TryGetAnchor(LanguageKind language, TextBuffer buffer, HighlightSession session, int line, int column, out int anchorLine, out int anchorColumn)
        {
            anchorLine = line;
            anchorColumn = column;
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

            List<Token> tokens = new List<Token>();
            ScanOneLine(language, buffer, session, line, tokens);

            bool leftOk = false;
            int leftCol = column - 1;
            if (leftCol >= 0 && leftCol < text.Length && BracePairs.IsBrace(language, text[leftCol]))
            {
                TokenKind kind = KindAt(tokens, leftCol);
                if (kind != TokenKind.String && kind != TokenKind.Comment)
                {
                    leftOk = true;
                }
            }

            bool rightOk = false;
            int rightCol = column;
            if (rightCol >= 0 && rightCol < text.Length && BracePairs.IsBrace(language, text[rightCol]))
            {
                TokenKind kind = KindAt(tokens, rightCol);
                if (kind != TokenKind.String && kind != TokenKind.Comment)
                {
                    rightOk = true;
                }
            }

            if (leftOk)
            {
                anchorColumn = leftCol;
                return true;
            }

            if (rightOk)
            {
                anchorColumn = rightCol;
                return true;
            }

            return false;
        }

        private static List<BraceSite> CollectSites(LanguageKind language, TextBuffer buffer, HighlightSession session, char virtualOpener, int virtualLine, int virtualColumn)
        {
            List<BraceSite> sites = new List<BraceSite>();
            ILineLexer lexer = LexerRegistry.Get(language);
            List<Token> tokens = new List<Token>();
            int state = 0;
            if (session != null)
            {
                state = session.GetStartState(0);
            }

            int lineCount = buffer.LineCount;
            int line = 0;
            while (line < lineCount)
            {
                tokens.Clear();
                int endState;
                string text = buffer.GetLine(line);
                lexer.ScanLine(text, state, tokens, out endState);
                state = endState;

                bool insertedVirtual = false;
                int t = 0;
                while (t < tokens.Count)
                {
                    Token token = tokens[t];
                    if (virtualOpener != '\0' && virtualLine == line && !insertedVirtual && token.Start >= virtualColumn)
                    {
                        AddSite(sites, language, virtualLine, virtualColumn, virtualOpener, TokenKind.Text);
                        insertedVirtual = true;
                    }

                    if (token.Length == 1 && token.Kind != TokenKind.String && token.Kind != TokenKind.Comment)
                    {
                        int col = token.Start;
                        if (virtualOpener != '\0' && virtualLine == line && col >= virtualColumn)
                        {
                            col++;
                        }

                        AddSite(sites, language, line, col, text[token.Start], token.Kind);
                    }

                    t++;
                }

                if (virtualOpener != '\0' && virtualLine == line && !insertedVirtual)
                {
                    AddSite(sites, language, virtualLine, virtualColumn, virtualOpener, TokenKind.Text);
                }

                line++;
            }

            return sites;
        }

        private static void AddSite(List<BraceSite> sites, LanguageKind language, int line, int column, char ch, TokenKind kind)
        {
            if (kind == TokenKind.String || kind == TokenKind.Comment)
            {
                return;
            }

            if (!BracePairs.IsBrace(language, ch))
            {
                return;
            }

            BraceSite site = new BraceSite();
            site.Line = line;
            site.Column = column;
            site.Ch = ch;
            site.IsOpener = BracePairs.IsOpener(language, ch);
            sites.Add(site);
        }

        private static void MatchSites(LanguageKind language, List<BraceSite> sites, out int[] pairs, out bool[] matched)
        {
            int n = sites.Count;
            pairs = new int[n];
            matched = new bool[n];
            int i = 0;
            while (i < n)
            {
                pairs[i] = -1;
                i++;
            }

            Stack<int> stack = new Stack<int>();
            i = 0;
            while (i < n)
            {
                BraceSite site = sites[i];
                if (site.IsOpener)
                {
                    stack.Push(i);
                }
                else if (stack.Count == 0)
                {
                    matched[i] = false;
                }
                else
                {
                    int open = stack.Pop();
                    pairs[i] = open;
                    pairs[open] = i;
                    bool ok = BracePairs.CloserMatches(language, sites[open].Ch, site.Ch);
                    matched[i] = ok;
                    matched[open] = ok;
                }

                i++;
            }
        }

        private static int IndexOf(List<BraceSite> sites, int line, int column)
        {
            int i = 0;
            while (i < sites.Count)
            {
                BraceSite site = sites[i];
                if (site.Line == line && site.Column == column)
                {
                    return i;
                }

                i++;
            }

            return -1;
        }

        internal static TokenKind KindAt(List<Token> tokens, int column)
        {
            if (tokens == null)
            {
                return TokenKind.Text;
            }

            int i = 0;
            while (i < tokens.Count)
            {
                Token token = tokens[i];
                if (column >= token.Start && column < token.Start + token.Length)
                {
                    return token.Kind;
                }

                i++;
            }

            return TokenKind.Text;
        }

        private struct BraceSite
        {
            public int Line;
            public int Column;
            public char Ch;
            public bool IsOpener;
        }
    }
}
