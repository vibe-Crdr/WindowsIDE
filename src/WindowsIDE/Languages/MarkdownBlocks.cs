using System.Collections.Generic;
using WindowsIDE.Editor;

namespace WindowsIDE.Languages
{
    /// <summary>
    /// プレビュー用ブロックの種類。
    /// </summary>
    public enum MarkdownBlockKind
    {
        Heading,
        Paragraph,
        ListItem,
        Fence,
        Quote,
        HorizontalRule,
        IndentedCode,
        Table
    }

    /// <summary>
    /// リスト項目のタスク状態。
    /// </summary>
    public enum MarkdownTaskState
    {
        None,
        Open,
        Closed
    }

    /// <summary>
    /// プレビュー用インラインの種類。
    /// </summary>
    public enum MarkdownInlineKind
    {
        Text,
        Emphasis,
        Strong,
        Code,
        Link,
        Image,
        Break
    }

    /// <summary>
    /// プレビュー 1 断片。Kind と本文。書式フラグは重ねてよい。
    /// </summary>
    public sealed class MarkdownInline
    {
        private MarkdownInlineKind kind;
        private string text;
        private string destination;
        private bool strong;
        private bool emphasis;
        private bool strike;
        private bool code;

        /// <summary>
        /// 種類と本文から断片を作る。
        /// </summary>
        /// <param name="kind">インライン種類。</param>
        /// <param name="text">本文。null は空。</param>
        public MarkdownInline(MarkdownInlineKind kind, string text)
            : this(kind, text, null, kind == MarkdownInlineKind.Strong, kind == MarkdownInlineKind.Emphasis, false, kind == MarkdownInlineKind.Code)
        {
        }

        /// <summary>
        /// 種類・本文・リンク先・書式フラグから断片を作る。
        /// </summary>
        /// <param name="kind">インライン種類。</param>
        /// <param name="text">本文。null は空。</param>
        /// <param name="destination">リンクまたは画像の先。null は空。</param>
        /// <param name="strong">太字。</param>
        /// <param name="emphasis">斜体。</param>
        /// <param name="strike">取り消し線。</param>
        /// <param name="code">インラインコード。</param>
        public MarkdownInline(MarkdownInlineKind kind, string text, string destination, bool strong, bool emphasis, bool strike, bool code)
        {
            this.kind = kind;
            this.text = (text == null) ? "" : text;
            this.destination = (destination == null) ? "" : destination;
            this.strong = strong;
            this.emphasis = emphasis;
            this.strike = strike;
            this.code = code;
        }

        /// <summary>インライン種類。</summary>
        public MarkdownInlineKind Kind
        {
            get { return this.kind; }
        }

        /// <summary>本文。画像は alt。Break は空。</summary>
        public string Text
        {
            get { return this.text; }
        }

        /// <summary>リンクまたは画像の先。無ければ空。</summary>
        public string Destination
        {
            get { return this.destination; }
        }

        /// <summary>太字なら true。</summary>
        public bool Strong
        {
            get { return this.strong; }
        }

        /// <summary>斜体なら true。</summary>
        public bool Emphasis
        {
            get { return this.emphasis; }
        }

        /// <summary>取り消し線なら true。</summary>
        public bool Strike
        {
            get { return this.strike; }
        }

        /// <summary>インラインコードなら true。</summary>
        public bool Code
        {
            get { return this.code; }
        }
    }

    /// <summary>
    /// 表の 1 行。セルはインライン配列。
    /// </summary>
    public sealed class MarkdownTableRow
    {
        private List<List<MarkdownInline>> cells;

        /// <summary>
        /// セル列から行を作る。
        /// </summary>
        /// <param name="cells">セル。null は空行。</param>
        public MarkdownTableRow(List<List<MarkdownInline>> cells)
        {
            this.cells = (cells == null) ? new List<List<MarkdownInline>>() : cells;
        }

        /// <summary>セル列。各セルはインライン配列。</summary>
        public List<List<MarkdownInline>> Cells
        {
            get { return this.cells; }
        }
    }

    /// <summary>
    /// プレビュー 1 ブロック。見出し・段落・リスト・フェンス・引用・HR・表。
    /// </summary>
    public sealed class MarkdownBlock
    {
        private MarkdownBlockKind kind;
        private int headingLevel;
        private string listMarker;
        private List<MarkdownInline> inlines;
        private string[] fenceLines;
        private int indentLevel;
        private MarkdownTaskState taskState;
        private int nestLevel;
        private List<MarkdownTableRow> tableRows;

        /// <summary>
        /// ブロックを作る。
        /// </summary>
        /// <param name="kind">ブロック種類。</param>
        /// <param name="headingLevel">見出しレベル。見出し以外は 0。</param>
        /// <param name="listMarker">リストマーカー。リスト以外は空。</param>
        /// <param name="inlines">インライン列。フェンスは空でよい。</param>
        /// <param name="fenceLines">フェンス中身。フェンス以外は空配列。</param>
        public MarkdownBlock(MarkdownBlockKind kind, int headingLevel, string listMarker, List<MarkdownInline> inlines, string[] fenceLines)
            : this(kind, headingLevel, listMarker, inlines, fenceLines, 0, MarkdownTaskState.None, 0, null)
        {
        }

        /// <summary>
        /// 第2スライス用のブロックを作る。
        /// </summary>
        /// <param name="kind">ブロック種類。</param>
        /// <param name="headingLevel">見出しレベル。見出し以外は 0。</param>
        /// <param name="listMarker">リストマーカー。リスト以外は空。</param>
        /// <param name="inlines">インライン列。</param>
        /// <param name="fenceLines">フェンスまたはインデントコードの生行。</param>
        /// <param name="indentLevel">リスト段。</param>
        /// <param name="taskState">タスク状態。</param>
        /// <param name="nestLevel">引用の段。</param>
        /// <param name="tableRows">表の行。先頭がヘッダ。表以外は空。</param>
        public MarkdownBlock(
            MarkdownBlockKind kind,
            int headingLevel,
            string listMarker,
            List<MarkdownInline> inlines,
            string[] fenceLines,
            int indentLevel,
            MarkdownTaskState taskState,
            int nestLevel,
            List<MarkdownTableRow> tableRows)
        {
            this.kind = kind;
            this.headingLevel = headingLevel;
            this.listMarker = (listMarker == null) ? "" : listMarker;
            this.inlines = (inlines == null) ? new List<MarkdownInline>() : inlines;
            this.fenceLines = (fenceLines == null) ? new string[0] : fenceLines;
            this.indentLevel = indentLevel;
            this.taskState = taskState;
            this.nestLevel = nestLevel;
            this.tableRows = (tableRows == null) ? new List<MarkdownTableRow>() : tableRows;
        }

        /// <summary>ブロック種類。</summary>
        public MarkdownBlockKind Kind
        {
            get { return this.kind; }
        }

        /// <summary>見出しレベル（1〜6）。見出し以外は 0。</summary>
        public int HeadingLevel
        {
            get { return this.headingLevel; }
        }

        /// <summary>リストマーカー（`-` や `1.`）。リスト以外は空。</summary>
        public string ListMarker
        {
            get { return this.listMarker; }
        }

        /// <summary>インライン列。</summary>
        public List<MarkdownInline> Inlines
        {
            get { return this.inlines; }
        }

        /// <summary>フェンスまたはインデントコードの生行。開始・終了行は含まない。</summary>
        public string[] FenceLines
        {
            get { return this.fenceLines; }
        }

        /// <summary>リストの段。0 始まり。</summary>
        public int IndentLevel
        {
            get { return this.indentLevel; }
        }

        /// <summary>タスク状態。リスト以外は None。</summary>
        public MarkdownTaskState TaskState
        {
            get { return this.taskState; }
        }

        /// <summary>引用の段。0 は引用外。</summary>
        public int NestLevel
        {
            get { return this.nestLevel; }
        }

        /// <summary>表の行。先頭がヘッダ。</summary>
        public List<MarkdownTableRow> TableRows
        {
            get { return this.tableRows; }
        }
    }

    /// <summary>
    /// TextBuffer からプレビュー用ブロック列を作る。WinForms は参照しない。
    /// </summary>
    public static class MarkdownBlocks
    {
        /// <summary>
        /// バッファを見出し・段落・リスト・フェンス等へ分ける。
        /// </summary>
        /// <param name="buffer">本文。null は空列。</param>
        /// <returns>ブロック列。</returns>
        public static List<MarkdownBlock> Parse(TextBuffer buffer)
        {
            List<MarkdownBlock> result = new List<MarkdownBlock>();
            if (buffer == null)
            {
                return result;
            }

            bool inFence = false;
            char fenceMarker = '`';
            List<string> fenceAcc = null;
            List<string> paraAcc = null;
            int i = 0;
            while (i < buffer.LineCount)
            {
                string line = buffer.GetLine(i);
                if (inFence)
                {
                    char closeMarker;
                    int closeCount;
                    if (MarkdownSyntax.TryFence(line, out closeMarker, out closeCount) && closeMarker == fenceMarker)
                    {
                        result.Add(MakeFence(fenceAcc));
                        fenceAcc = null;
                        inFence = false;
                    }
                    else
                    {
                        fenceAcc.Add(line);
                    }

                    i++;
                    continue;
                }

                if (MarkdownSyntax.IsBlank(line))
                {
                    FlushParagraph(result, paraAcc, 0);
                    paraAcc = null;
                    i++;
                    continue;
                }

                char openMarker;
                int openCount;
                if (MarkdownSyntax.TryFence(line, out openMarker, out openCount))
                {
                    FlushParagraph(result, paraAcc, 0);
                    paraAcc = null;
                    inFence = true;
                    fenceMarker = openMarker;
                    fenceAcc = new List<string>();
                    i++;
                    continue;
                }

                int quoteNest;
                int quoteContent;
                if (MarkdownSyntax.TryQuote(line, out quoteNest, out quoteContent))
                {
                    FlushParagraph(result, paraAcc, 0);
                    paraAcc = null;
                    string inner = (quoteContent < line.Length) ? line.Substring(quoteContent) : "";
                    result.Add(ParseQuoted(inner, quoteNest));
                    i++;
                    continue;
                }

                int level;
                int hashStart;
                int contentStart;
                if (MarkdownSyntax.TryHeading(line, out level, out hashStart, out contentStart))
                {
                    FlushParagraph(result, paraAcc, 0);
                    paraAcc = null;
                    string body = (contentStart < line.Length) ? line.Substring(contentStart) : "";
                    result.Add(new MarkdownBlock(MarkdownBlockKind.Heading, level, "", ParseInlines(body), new string[0]));
                    i++;
                    continue;
                }

                int markerStart;
                int markerLength;
                if (MarkdownSyntax.TryListItem(line, out markerStart, out markerLength, out contentStart))
                {
                    FlushParagraph(result, paraAcc, 0);
                    paraAcc = null;
                    string marker = line.Substring(markerStart, markerLength);
                    int indentLevel = MarkdownSyntax.IndentColumns(line) / 2;
                    MarkdownTaskState task = MarkdownTaskState.None;
                    int bodyStart = contentStart;
                    MarkdownTaskState parsedTask;
                    int taskBody;
                    if (MarkdownSyntax.TryTaskMarker(line, contentStart, out parsedTask, out taskBody))
                    {
                        task = parsedTask;
                        bodyStart = taskBody;
                    }

                    string body = (bodyStart < line.Length) ? line.Substring(bodyStart) : "";
                    result.Add(new MarkdownBlock(
                        MarkdownBlockKind.ListItem,
                        0,
                        marker,
                        ParseInlines(body),
                        new string[0],
                        indentLevel,
                        task,
                        0,
                        null));
                    i++;
                    continue;
                }

                if (i + 1 < buffer.LineCount)
                {
                    string[] rowCells;
                    if (MarkdownSyntax.TryTableRow(line, out rowCells) && MarkdownSyntax.TryTableSep(buffer.GetLine(i + 1)))
                    {
                        FlushParagraph(result, paraAcc, 0);
                        paraAcc = null;
                        i = ParseTable(buffer, i, result);
                        continue;
                    }
                }

                if (paraAcc == null && MarkdownSyntax.TryThematicBreak(line))
                {
                    result.Add(new MarkdownBlock(MarkdownBlockKind.HorizontalRule, 0, "", new List<MarkdownInline>(), new string[0]));
                    i++;
                    continue;
                }

                if (paraAcc == null && MarkdownSyntax.TryIndentedCode(line))
                {
                    i = ParseIndentedCode(buffer, i, result);
                    continue;
                }

                if (i + 1 < buffer.LineCount)
                {
                    int setextLevel;
                    if (!MarkdownSyntax.IsBlank(line) && MarkdownSyntax.TrySetextUnderline(buffer.GetLine(i + 1), out setextLevel))
                    {
                        FlushParagraph(result, paraAcc, 0);
                        paraAcc = null;
                        result.Add(new MarkdownBlock(
                            MarkdownBlockKind.Heading,
                            setextLevel,
                            "",
                            ParseInlines(TrimSpaces(line)),
                            new string[0]));
                        i += 2;
                        continue;
                    }
                }

                if (paraAcc == null)
                {
                    paraAcc = new List<string>();
                }

                paraAcc.Add(line);
                i++;
            }

            if (inFence)
            {
                result.Add(MakeFence(fenceAcc));
            }
            else
            {
                FlushParagraph(result, paraAcc, 0);
            }

            return result;
        }

        private static MarkdownBlock ParseQuoted(string inner, int nestLevel)
        {
            int level;
            int hashStart;
            int contentStart;
            if (MarkdownSyntax.TryHeading(inner, out level, out hashStart, out contentStart))
            {
                string body = (contentStart < inner.Length) ? inner.Substring(contentStart) : "";
                return new MarkdownBlock(
                    MarkdownBlockKind.Heading,
                    level,
                    "",
                    ParseInlines(body),
                    new string[0],
                    0,
                    MarkdownTaskState.None,
                    nestLevel,
                    null);
            }

            if (MarkdownSyntax.TryThematicBreak(inner))
            {
                return new MarkdownBlock(
                    MarkdownBlockKind.HorizontalRule,
                    0,
                    "",
                    new List<MarkdownInline>(),
                    new string[0],
                    0,
                    MarkdownTaskState.None,
                    nestLevel,
                    null);
            }

            int markerStart;
            int markerLength;
            if (MarkdownSyntax.TryListItem(inner, out markerStart, out markerLength, out contentStart))
            {
                string marker = inner.Substring(markerStart, markerLength);
                MarkdownTaskState task = MarkdownTaskState.None;
                int bodyStart = contentStart;
                MarkdownTaskState parsedTask;
                int taskBody;
                if (MarkdownSyntax.TryTaskMarker(inner, contentStart, out parsedTask, out taskBody))
                {
                    task = parsedTask;
                    bodyStart = taskBody;
                }

                string body = (bodyStart < inner.Length) ? inner.Substring(bodyStart) : "";
                return new MarkdownBlock(
                    MarkdownBlockKind.ListItem,
                    0,
                    marker,
                    ParseInlines(body),
                    new string[0],
                    0,
                    task,
                    nestLevel,
                    null);
            }

            return new MarkdownBlock(
                MarkdownBlockKind.Quote,
                0,
                "",
                ParseInlines(TrimSpaces(inner)),
                new string[0],
                0,
                MarkdownTaskState.None,
                nestLevel,
                null);
        }

        private static int ParseTable(TextBuffer buffer, int start, List<MarkdownBlock> result)
        {
            List<MarkdownTableRow> rows = new List<MarkdownTableRow>();
            rows.Add(ParseTableRow(buffer.GetLine(start)));
            int i = start + 2;
            while (i < buffer.LineCount)
            {
                string line = buffer.GetLine(i);
                if (MarkdownSyntax.IsBlank(line))
                {
                    break;
                }

                string[] cells;
                if (!MarkdownSyntax.TryTableRow(line, out cells))
                {
                    break;
                }

                rows.Add(ParseTableRow(line));
                i++;
            }

            result.Add(new MarkdownBlock(
                MarkdownBlockKind.Table,
                0,
                "",
                new List<MarkdownInline>(),
                new string[0],
                0,
                MarkdownTaskState.None,
                0,
                rows));
            return i;
        }

        private static MarkdownTableRow ParseTableRow(string line)
        {
            string[] cells;
            List<List<MarkdownInline>> parsed = new List<List<MarkdownInline>>();
            if (!MarkdownSyntax.TryTableRow(line, out cells) || cells == null)
            {
                return new MarkdownTableRow(parsed);
            }

            int i = 0;
            while (i < cells.Length)
            {
                parsed.Add(ParseInlines(cells[i]));
                i++;
            }

            return new MarkdownTableRow(parsed);
        }

        private static int ParseIndentedCode(TextBuffer buffer, int start, List<MarkdownBlock> result)
        {
            List<string> lines = new List<string>();
            int i = start;
            while (i < buffer.LineCount)
            {
                string line = buffer.GetLine(i);
                if (MarkdownSyntax.IsBlank(line))
                {
                    if (i + 1 < buffer.LineCount && MarkdownSyntax.TryIndentedCode(buffer.GetLine(i + 1)))
                    {
                        lines.Add("");
                        i++;
                        continue;
                    }

                    break;
                }

                if (!MarkdownSyntax.TryIndentedCode(line))
                {
                    break;
                }

                lines.Add(StripIndent(line, 4));
                i++;
            }

            result.Add(new MarkdownBlock(MarkdownBlockKind.IndentedCode, 0, "", new List<MarkdownInline>(), lines.ToArray()));
            return i;
        }

        private static string StripIndent(string line, int columns)
        {
            if (line == null)
            {
                return "";
            }

            int cols = 0;
            int i = 0;
            while (i < line.Length && cols < columns)
            {
                char c = line[i];
                if (c == ' ')
                {
                    cols++;
                    i++;
                }
                else if (c == '\t')
                {
                    cols += 4 - (cols % 4);
                    i++;
                }
                else
                {
                    break;
                }
            }

            return line.Substring(i);
        }

        private static MarkdownBlock MakeFence(List<string> lines)
        {
            string[] arr;
            if (lines == null || lines.Count == 0)
            {
                arr = new string[0];
            }
            else
            {
                arr = lines.ToArray();
            }

            return new MarkdownBlock(MarkdownBlockKind.Fence, 0, "", new List<MarkdownInline>(), arr);
        }

        private static void FlushParagraph(List<MarkdownBlock> result, List<string> paraAcc, int nestLevel)
        {
            if (paraAcc == null || paraAcc.Count == 0)
            {
                return;
            }

            List<MarkdownInline> inlines = new List<MarkdownInline>();
            int i = 0;
            while (i < paraAcc.Count)
            {
                string raw = paraAcc[i];
                if (i > 0)
                {
                    if (MarkdownSyntax.EndsWithHardBreak(paraAcc[i - 1]))
                    {
                        inlines.Add(new MarkdownInline(MarkdownInlineKind.Break, ""));
                    }
                    else
                    {
                        inlines.Add(new MarkdownInline(MarkdownInlineKind.Text, " "));
                    }
                }

                List<MarkdownInline> part = ParseInlines(TrimSpaces(raw));
                int p = 0;
                while (p < part.Count)
                {
                    inlines.Add(part[p]);
                    p++;
                }

                i++;
            }

            if (inlines.Count == 0)
            {
                return;
            }

            result.Add(new MarkdownBlock(
                MarkdownBlockKind.Paragraph,
                0,
                "",
                inlines,
                new string[0],
                0,
                MarkdownTaskState.None,
                nestLevel,
                null));
        }

        private static string TrimSpaces(string line)
        {
            if (line == null || line.Length == 0)
            {
                return "";
            }

            int start = 0;
            while (start < line.Length && ScanChars.IsSpace(line[start]))
            {
                start++;
            }

            int end = line.Length;
            while (end > start && ScanChars.IsSpace(line[end - 1]))
            {
                end--;
            }

            if (start == 0 && end == line.Length)
            {
                return line;
            }

            return line.Substring(start, end - start);
        }

        private static List<MarkdownInline> ParseInlines(string text)
        {
            List<MarkdownInline> result = new List<MarkdownInline>();
            if (text == null || text.Length == 0)
            {
                return result;
            }

            int i = 0;
            int textStart = 0;
            while (i < text.Length)
            {
                char c = text[i];
                if (c == '`')
                {
                    int close = IndexOfChar(text, '`', i + 1);
                    if (close >= 0)
                    {
                        FlushText(result, text, textStart, i);
                        result.Add(new MarkdownInline(MarkdownInlineKind.Code, text.Substring(i + 1, close - (i + 1))));
                        i = close + 1;
                        textStart = i;
                        continue;
                    }
                }
                else if (c == '!' && i + 1 < text.Length && text[i + 1] == '[')
                {
                    string label;
                    string dest;
                    int end;
                    if (TryParseLink(text, i + 1, out label, out dest, out end))
                    {
                        FlushText(result, text, textStart, i);
                        result.Add(new MarkdownInline(MarkdownInlineKind.Image, label, dest, false, false, false, false));
                        i = end;
                        textStart = i;
                        continue;
                    }
                }
                else if (c == '[')
                {
                    string label;
                    string dest;
                    int end;
                    if (TryParseLink(text, i, out label, out dest, out end))
                    {
                        FlushText(result, text, textStart, i);
                        result.Add(new MarkdownInline(MarkdownInlineKind.Link, label, dest, false, false, false, false));
                        i = end;
                        textStart = i;
                        continue;
                    }
                }
                else if (c == '<' && IsAutolinkStart(text, i))
                {
                    int close = IndexOfChar(text, '>', i + 1);
                    if (close > i + 1)
                    {
                        string url = text.Substring(i + 1, close - (i + 1));
                        FlushText(result, text, textStart, i);
                        result.Add(new MarkdownInline(MarkdownInlineKind.Link, url, url, false, false, false, false));
                        i = close + 1;
                        textStart = i;
                        continue;
                    }
                }
                else if (c == '~' && i + 1 < text.Length && text[i + 1] == '~')
                {
                    int close = IndexOfTwo(text, '~', i + 2);
                    if (close >= 0)
                    {
                        FlushText(result, text, textStart, i);
                        result.Add(new MarkdownInline(
                            MarkdownInlineKind.Text,
                            text.Substring(i + 2, close - (i + 2)),
                            null,
                            false,
                            false,
                            true,
                            false));
                        i = close + 2;
                        textStart = i;
                        continue;
                    }
                }
                else if ((c == '*' || c == '_') && i + 2 < text.Length && text[i + 1] == c && text[i + 2] == c)
                {
                    int close = IndexOfThree(text, c, i + 3);
                    if (close >= 0)
                    {
                        FlushText(result, text, textStart, i);
                        result.Add(new MarkdownInline(
                            MarkdownInlineKind.Strong,
                            text.Substring(i + 3, close - (i + 3)),
                            null,
                            true,
                            true,
                            false,
                            false));
                        i = close + 3;
                        textStart = i;
                        continue;
                    }
                }
                else if ((c == '*' || c == '_') && i + 1 < text.Length && text[i + 1] == c)
                {
                    int close = IndexOfTwo(text, c, i + 2);
                    if (close >= 0)
                    {
                        FlushText(result, text, textStart, i);
                        result.Add(new MarkdownInline(MarkdownInlineKind.Strong, text.Substring(i + 2, close - (i + 2))));
                        i = close + 2;
                        textStart = i;
                        continue;
                    }
                }
                else if (c == '*' || c == '_')
                {
                    int close = IndexOfChar(text, c, i + 1);
                    if (close >= 0)
                    {
                        FlushText(result, text, textStart, i);
                        result.Add(new MarkdownInline(MarkdownInlineKind.Emphasis, text.Substring(i + 1, close - (i + 1))));
                        i = close + 1;
                        textStart = i;
                        continue;
                    }
                }

                i++;
            }

            FlushText(result, text, textStart, text.Length);
            return result;
        }

        private static bool IsAutolinkStart(string text, int i)
        {
            if (text == null || i + 7 >= text.Length || text[i] != '<')
            {
                return false;
            }

            return StartsAt(text, i + 1, "http://") || StartsAt(text, i + 1, "https://");
        }

        private static bool StartsAt(string text, int start, string token)
        {
            if (start + token.Length > text.Length)
            {
                return false;
            }

            int i = 0;
            while (i < token.Length)
            {
                char a = text[start + i];
                char b = token[i];
                if (a >= 'A' && a <= 'Z')
                {
                    a = (char)(a - 'A' + 'a');
                }

                if (a != b)
                {
                    return false;
                }

                i++;
            }

            return true;
        }

        private static bool TryParseLink(string text, int openBracket, out string label, out string dest, out int end)
        {
            label = "";
            dest = "";
            end = openBracket;
            if (text == null || openBracket >= text.Length || text[openBracket] != '[')
            {
                return false;
            }

            int closeBracket = IndexOfChar(text, ']', openBracket + 1);
            if (closeBracket < 0 || closeBracket + 1 >= text.Length || text[closeBracket + 1] != '(')
            {
                return false;
            }

            int closeParen = IndexOfChar(text, ')', closeBracket + 2);
            if (closeParen < 0)
            {
                return false;
            }

            label = text.Substring(openBracket + 1, closeBracket - (openBracket + 1));
            dest = text.Substring(closeBracket + 2, closeParen - (closeBracket + 2));
            end = closeParen + 1;
            return true;
        }

        private static void FlushText(List<MarkdownInline> result, string text, int start, int end)
        {
            if (end <= start)
            {
                return;
            }

            result.Add(new MarkdownInline(MarkdownInlineKind.Text, text.Substring(start, end - start)));
        }

        private static int IndexOfChar(string text, char ch, int start)
        {
            int i = start;
            while (i < text.Length)
            {
                if (text[i] == ch)
                {
                    return i;
                }

                i++;
            }

            return -1;
        }

        private static int IndexOfTwo(string text, char ch, int start)
        {
            int i = start;
            while (i + 1 < text.Length)
            {
                if (text[i] == ch && text[i + 1] == ch)
                {
                    return i;
                }

                i++;
            }

            return -1;
        }

        private static int IndexOfThree(string text, char ch, int start)
        {
            int i = start;
            while (i + 2 < text.Length)
            {
                if (text[i] == ch && text[i + 1] == ch && text[i + 2] == ch)
                {
                    return i;
                }

                i++;
            }

            return -1;
        }
    }
}
