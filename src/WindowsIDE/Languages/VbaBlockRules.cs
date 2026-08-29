using System;
using System.Collections.Generic;
using WindowsIDE.Editor;

namespace WindowsIDE.Languages
{
    /// <summary>
    /// VBA ブロック開始と終端の対応。For / For Each は Next。For に End を付けた終端は出さない。
    /// </summary>
    public static class VbaBlockRules
    {
        private enum BlockKind
        {
            None,
            Sub,
            Function,
            Property,
            IfBlock,
            ForBlock,
            DoBlock,
            WhileBlock,
            With,
            Select,
            Enum,
            Type
        }

        /// <summary>
        /// 行がブロック開始なら対応終端を返す。1 行 If でも If 行なら End If を返す（呼び出し側で除外）。
        /// </summary>
        /// <param name="buffer">本文。</param>
        /// <param name="session">行開始状態。</param>
        /// <param name="line">対象行。</param>
        /// <param name="closeText">終端テキスト。</param>
        /// <returns>ブロック開始のとき true。</returns>
        public static bool TryGetBlockClose(TextBuffer buffer, HighlightSession session, int line, out string closeText)
        {
            closeText = null;
            BlockKind kind = GetLineOpener(buffer, session, line);
            closeText = CloseTextOf(kind);
            return kind != BlockKind.None && closeText != null;
        }

        /// <summary>
        /// Then より後ろにコメント以外のトークンがある 1 行 If なら true。
        /// </summary>
        /// <param name="buffer">本文。</param>
        /// <param name="session">行開始状態。</param>
        /// <param name="line">対象行。</param>
        /// <returns>1 行 If のとき true。</returns>
        public static bool IsOneLineIf(TextBuffer buffer, HighlightSession session, int line)
        {
            List<Tok> toks = LineTokens(buffer, session, line);
            int thenIndex;
            if (!TryFindThen(toks, out thenIndex))
            {
                return false;
            }

            int i = thenIndex + 1;
            while (i < toks.Count)
            {
                Tok tok = toks[i];
                if (tok.Kind == TokenKind.Comment || tok.IsWhitespace)
                {
                    i++;
                    continue;
                }

                if (tok.Word == "_")
                {
                    i++;
                    continue;
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Then のあとが継続 `_` と空白・コメントだけなら true。
        /// </summary>
        /// <param name="buffer">本文。</param>
        /// <param name="session">行開始状態。</param>
        /// <param name="line">対象行。</param>
        /// <returns>継続行のとき true。</returns>
        public static bool IsThenContinuationOnly(TextBuffer buffer, HighlightSession session, int line)
        {
            List<Tok> toks = LineTokens(buffer, session, line);
            int thenIndex;
            if (!TryFindThen(toks, out thenIndex))
            {
                return false;
            }

            bool sawCont = false;
            int i = thenIndex + 1;
            while (i < toks.Count)
            {
                Tok tok = toks[i];
                if (tok.Kind == TokenKind.Comment || tok.IsWhitespace)
                {
                    i++;
                    continue;
                }

                if (tok.Word == "_")
                {
                    sawCont = true;
                    i++;
                    continue;
                }

                return false;
            }

            return sawCont;
        }

        /// <summary>
        /// 対象行のブロック開始に、同じ先頭空白の対応終端が既にあるなら true。
        /// 浅い終端は外側用。内側開始はそれを自分の対にしない。
        /// </summary>
        /// <param name="buffer">本文。</param>
        /// <param name="session">行開始状態。</param>
        /// <param name="line">対象行。</param>
        /// <returns>既に終端があるとき true。</returns>
        public static bool HasMatchingClose(TextBuffer buffer, HighlightSession session, int line)
        {
            BlockKind want = GetLineOpener(buffer, session, line);
            if (want == BlockKind.None || buffer == null || line < 0 || line >= buffer.LineCount)
            {
                return false;
            }

            string prefix = IndentRules.LeadingWhitespace(buffer.GetLine(line));
            List<BlockEvent> events = CollectEvents(buffer, session);
            int i = 0;
            while (i < events.Count)
            {
                BlockEvent ev = events[i];
                if (ev.IsClose && ev.Kind == want && ev.Line > line && ev.Line < buffer.LineCount)
                {
                    string closePrefix = IndentRules.LeadingWhitespace(buffer.GetLine(ev.Line));
                    if (closePrefix == prefix)
                    {
                        return true;
                    }
                }

                i++;
            }

            return false;
        }

        private static BlockKind GetLineOpener(TextBuffer buffer, HighlightSession session, int line)
        {
            List<Tok> toks = LineTokens(buffer, session, line);
            int i = SkipWs(toks, 0);
            i = SkipLabelsAndModifiers(toks, i);
            i = SkipWs(toks, i);
            if (i >= toks.Count)
            {
                return BlockKind.None;
            }

            Tok tok = toks[i];
            if (!tok.IsKeyword)
            {
                return BlockKind.None;
            }

            if (IsIf(tok.Word))
            {
                int thenIndex;
                if (!TryFindThen(toks, out thenIndex))
                {
                    return BlockKind.None;
                }

                return BlockKind.IfBlock;
            }

            return OpenerKind(tok.Word);
        }

        private static int SkipWs(List<Tok> toks, int i)
        {
            while (i < toks.Count && toks[i].IsWhitespace)
            {
                i++;
            }

            return i;
        }

        private static int SkipLabelsAndModifiers(List<Tok> toks, int i)
        {
            i = SkipWs(toks, i);
            while (i + 1 < toks.Count && !toks[i].IsKeyword && !toks[i].IsWhitespace && toks[i + 1].Word == ":")
            {
                i += 2;
                while (i < toks.Count && toks[i].IsWhitespace)
                {
                    i++;
                }
            }

            while (i < toks.Count && toks[i].IsKeyword && IsModifier(toks[i].Word))
            {
                i++;
                while (i < toks.Count && toks[i].IsWhitespace)
                {
                    i++;
                }
            }

            return i;
        }

        private static bool TryFindThen(List<Tok> toks, out int thenIndex)
        {
            thenIndex = -1;
            int i = 0;
            while (i < toks.Count)
            {
                if (toks[i].IsKeyword && IsThen(toks[i].Word))
                {
                    thenIndex = i;
                    return true;
                }

                i++;
            }

            return false;
        }

        private static List<BlockEvent> CollectEvents(TextBuffer buffer, HighlightSession session)
        {
            List<BlockEvent> events = new List<BlockEvent>();
            if (buffer == null)
            {
                return events;
            }

            List<Tok> all = new List<Tok>();
            int line = 0;
            while (line < buffer.LineCount)
            {
                List<Tok> row = LineTokens(buffer, session, line);
                int t = 0;
                while (t < row.Count)
                {
                    all.Add(row[t]);
                    t++;
                }

                line++;
            }

            int i = 0;
            while (i < all.Count)
            {
                Tok tok = all[i];
                if (tok.IsWhitespace)
                {
                    i++;
                    continue;
                }

                if (tok.Kind == TokenKind.Comment)
                {
                    i++;
                    continue;
                }

                if (!tok.IsKeyword)
                {
                    i++;
                    continue;
                }

                if (IsEnd(tok.Word) && NextKeyword(all, i + 1).IsKeyword)
                {
                    Tok next = NextKeyword(all, i + 1);
                    BlockKind closed = KindFromEndWord(next.Word);
                    if (closed != BlockKind.None)
                    {
                        events.Add(OpenOrClose(tok.Line, closed, true));
                        i = IndexOfTok(all, next) + 1;
                        continue;
                    }
                }

                if (IsExit(tok.Word) && NextKeyword(all, i + 1).IsKeyword)
                {
                    i = IndexOfTok(all, NextKeyword(all, i + 1)) + 1;
                    continue;
                }

                if (IsElseIf(tok.Word) || IsElse(tok.Word) || IsCase(tok.Word))
                {
                    i++;
                    continue;
                }

                if (IsWhile(tok.Word) && PreviousKeyword(all, i).IsKeyword && IsDo(PreviousKeyword(all, i).Word))
                {
                    i++;
                    continue;
                }

                if (IsIf(tok.Word))
                {
                    if (IsHashBefore(all, i))
                    {
                        i++;
                        continue;
                    }

                    if (IsOneLineIf(buffer, session, tok.Line) || IsThenContinuationOnly(buffer, session, tok.Line))
                    {
                        i++;
                        continue;
                    }

                    int thenIndexDummy;
                    List<Tok> lineToks = LineTokens(buffer, session, tok.Line);
                    if (!TryFindThen(lineToks, out thenIndexDummy))
                    {
                        i++;
                        continue;
                    }

                    events.Add(OpenOrClose(tok.Line, BlockKind.IfBlock, false));
                    i++;
                    continue;
                }

                BlockKind opener = OpenerKind(tok.Word);
                if (opener != BlockKind.None && opener != BlockKind.IfBlock)
                {
                    events.Add(OpenOrClose(tok.Line, opener, false));
                    i++;
                    continue;
                }

                if (IsNext(tok.Word))
                {
                    events.Add(OpenOrClose(tok.Line, BlockKind.ForBlock, true));
                    i++;
                    continue;
                }

                if (IsLoop(tok.Word))
                {
                    events.Add(OpenOrClose(tok.Line, BlockKind.DoBlock, true));
                    i++;
                    continue;
                }

                if (IsWend(tok.Word))
                {
                    events.Add(OpenOrClose(tok.Line, BlockKind.WhileBlock, true));
                    i++;
                    continue;
                }

                i++;
            }

            return events;
        }

        private static bool[] MatchEvents(List<BlockEvent> events)
        {
            bool[] matched = new bool[events.Count];
            List<int> stack = new List<int>();
            int i = 0;
            while (i < events.Count)
            {
                BlockEvent ev = events[i];
                if (!ev.IsClose)
                {
                    stack.Add(i);
                }
                else
                {
                    int found = -1;
                    int s = stack.Count - 1;
                    while (s >= 0)
                    {
                        if (events[stack[s]].Kind == ev.Kind)
                        {
                            found = s;
                            break;
                        }

                        s--;
                    }

                    if (found >= 0)
                    {
                        int open = stack[found];
                        int drop = stack.Count - 1;
                        while (drop > found)
                        {
                            stack.RemoveAt(drop);
                            drop--;
                        }

                        stack.RemoveAt(found);
                        matched[open] = true;
                        matched[i] = true;
                    }
                }

                i++;
            }

            return matched;
        }

        private static BlockEvent OpenOrClose(int line, BlockKind kind, bool isClose)
        {
            BlockEvent ev = new BlockEvent();
            ev.Line = line;
            ev.Kind = kind;
            ev.IsClose = isClose;
            return ev;
        }

        private static Tok NextKeyword(List<Tok> all, int start)
        {
            Tok empty = new Tok();
            int i = start;
            while (i < all.Count)
            {
                if (all[i].IsWhitespace)
                {
                    i++;
                    continue;
                }

                return all[i];
            }

            return empty;
        }

        private static Tok PreviousKeyword(List<Tok> all, int index)
        {
            Tok empty = new Tok();
            int i = index - 1;
            while (i >= 0)
            {
                if (all[i].IsWhitespace)
                {
                    i--;
                    continue;
                }

                return all[i];
            }

            return empty;
        }

        private static int IndexOfTok(List<Tok> all, Tok tok)
        {
            int i = 0;
            while (i < all.Count)
            {
                if (all[i].Line == tok.Line && all[i].Start == tok.Start)
                {
                    return i;
                }

                i++;
            }

            return all.Count;
        }

        private static bool IsHashBefore(List<Tok> all, int index)
        {
            Tok prev = PreviousKeyword(all, index);
            return prev.Word == "#";
        }

        private static List<Tok> LineTokens(TextBuffer buffer, HighlightSession session, int line)
        {
            List<Tok> result = new List<Tok>();
            if (buffer == null || line < 0 || line >= buffer.LineCount)
            {
                return result;
            }

            string text = buffer.GetLine(line);
            List<Token> tokens = new List<Token>();
            BraceMatch.ScanOneLine(LanguageKind.Vba, buffer, session, line, tokens);
            int i = 0;
            while (i < tokens.Count)
            {
                Token token = tokens[i];
                Tok tok = new Tok();
                tok.Line = line;
                tok.Start = token.Start;
                tok.Kind = token.Kind;
                int len = token.Length;
                if (token.Start + len > text.Length)
                {
                    len = text.Length - token.Start;
                }

                if (token.Start >= 0 && len > 0)
                {
                    tok.Word = text.Substring(token.Start, len);
                }
                else
                {
                    tok.Word = "";
                }

                tok.IsKeyword = token.Kind == TokenKind.Keyword;
                tok.IsWhitespace = IsWs(tok.Word);
                result.Add(tok);
                i++;
            }

            return result;
        }

        private static bool IsWs(string word)
        {
            if (string.IsNullOrEmpty(word))
            {
                return true;
            }

            int i = 0;
            while (i < word.Length)
            {
                char c = word[i];
                if (c != ' ' && c != '\t')
                {
                    return false;
                }

                i++;
            }

            return true;
        }

        private static BlockKind OpenerKind(string word)
        {
            if (IsSub(word))
            {
                return BlockKind.Sub;
            }

            if (IsFunction(word))
            {
                return BlockKind.Function;
            }

            if (IsProperty(word))
            {
                return BlockKind.Property;
            }

            if (IsIf(word))
            {
                return BlockKind.IfBlock;
            }

            if (IsFor(word))
            {
                return BlockKind.ForBlock;
            }

            if (IsDo(word))
            {
                return BlockKind.DoBlock;
            }

            if (IsWhile(word))
            {
                return BlockKind.WhileBlock;
            }

            if (IsWith(word))
            {
                return BlockKind.With;
            }

            if (IsSelect(word))
            {
                return BlockKind.Select;
            }

            if (IsEnum(word))
            {
                return BlockKind.Enum;
            }

            if (IsType(word))
            {
                return BlockKind.Type;
            }

            return BlockKind.None;
        }

        private static BlockKind KindFromEndWord(string word)
        {
            if (IsSub(word))
            {
                return BlockKind.Sub;
            }

            if (IsFunction(word))
            {
                return BlockKind.Function;
            }

            if (IsProperty(word))
            {
                return BlockKind.Property;
            }

            if (IsIf(word))
            {
                return BlockKind.IfBlock;
            }

            if (IsWith(word))
            {
                return BlockKind.With;
            }

            if (IsSelect(word))
            {
                return BlockKind.Select;
            }

            if (IsEnum(word))
            {
                return BlockKind.Enum;
            }

            if (IsType(word))
            {
                return BlockKind.Type;
            }

            return BlockKind.None;
        }

        private static string CloseTextOf(BlockKind kind)
        {
            switch (kind)
            {
                case BlockKind.Sub:
                    return "End Sub";
                case BlockKind.Function:
                    return "End Function";
                case BlockKind.Property:
                    return "End Property";
                case BlockKind.IfBlock:
                    return "End If";
                case BlockKind.ForBlock:
                    return "Next";
                case BlockKind.DoBlock:
                    return "Loop";
                case BlockKind.WhileBlock:
                    return "Wend";
                case BlockKind.With:
                    return "End With";
                case BlockKind.Select:
                    return "End Select";
                case BlockKind.Enum:
                    return "End Enum";
                case BlockKind.Type:
                    return "End Type";
                default:
                    return null;
            }
        }

        private static bool IsModifier(string word)
        {
            return Eq(word, "Public") || Eq(word, "Private") || Eq(word, "Friend") || Eq(word, "Global") || Eq(word, "Static");
        }

        private static bool IsSub(string word) { return Eq(word, "Sub"); }
        private static bool IsFunction(string word) { return Eq(word, "Function"); }
        private static bool IsProperty(string word) { return Eq(word, "Property"); }
        private static bool IsIf(string word) { return Eq(word, "If"); }
        private static bool IsThen(string word) { return Eq(word, "Then"); }
        private static bool IsFor(string word) { return Eq(word, "For"); }
        private static bool IsDo(string word) { return Eq(word, "Do"); }
        private static bool IsWhile(string word) { return Eq(word, "While"); }
        private static bool IsWith(string word) { return Eq(word, "With"); }
        private static bool IsSelect(string word) { return Eq(word, "Select"); }
        private static bool IsEnum(string word) { return Eq(word, "Enum"); }
        private static bool IsType(string word) { return Eq(word, "Type"); }
        private static bool IsEnd(string word) { return Eq(word, "End"); }
        private static bool IsExit(string word) { return Eq(word, "Exit"); }
        private static bool IsNext(string word) { return Eq(word, "Next"); }
        private static bool IsLoop(string word) { return Eq(word, "Loop"); }
        private static bool IsWend(string word) { return Eq(word, "Wend"); }
        private static bool IsElseIf(string word) { return Eq(word, "ElseIf"); }
        private static bool IsElse(string word) { return Eq(word, "Else"); }
        private static bool IsCase(string word) { return Eq(word, "Case"); }

        private static bool Eq(string a, string b)
        {
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }

        private struct Tok
        {
            public int Line;
            public int Start;
            public string Word;
            public TokenKind Kind;
            public bool IsKeyword;
            public bool IsWhitespace;
        }

        private struct BlockEvent
        {
            public int Line;
            public BlockKind Kind;
            public bool IsClose;
        }
    }
}
