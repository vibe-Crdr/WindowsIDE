using System.Collections.Generic;
using System.Text;
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
        Fence
    }

    /// <summary>
    /// プレビュー用インラインの種類。
    /// </summary>
    public enum MarkdownInlineKind
    {
        Text,
        Emphasis,
        Strong,
        Code
    }

    /// <summary>
    /// プレビュー 1 断片。Kind と本文。
    /// </summary>
    public sealed class MarkdownInline
    {
        private MarkdownInlineKind kind;
        private string text;

        /// <summary>
        /// 種類と本文から断片を作る。
        /// </summary>
        /// <param name="kind">インライン種類。</param>
        /// <param name="text">本文。null は空。</param>
        public MarkdownInline(MarkdownInlineKind kind, string text)
        {
            this.kind = kind;
            this.text = (text == null) ? "" : text;
        }

        /// <summary>インライン種類。</summary>
        public MarkdownInlineKind Kind
        {
            get { return this.kind; }
        }

        /// <summary>本文。</summary>
        public string Text
        {
            get { return this.text; }
        }
    }

    /// <summary>
    /// プレビュー 1 ブロック。見出し・段落・リスト・フェンス。
    /// </summary>
    public sealed class MarkdownBlock
    {
        private MarkdownBlockKind kind;
        private int headingLevel;
        private string listMarker;
        private List<MarkdownInline> inlines;
        private string[] fenceLines;

        /// <summary>
        /// ブロックを作る。
        /// </summary>
        /// <param name="kind">ブロック種類。</param>
        /// <param name="headingLevel">見出しレベル。見出し以外は 0。</param>
        /// <param name="listMarker">リストマーカー。リスト以外は空。</param>
        /// <param name="inlines">インライン列。フェンスは空でよい。</param>
        /// <param name="fenceLines">フェンス中身。フェンス以外は空配列。</param>
        public MarkdownBlock(MarkdownBlockKind kind, int headingLevel, string listMarker, List<MarkdownInline> inlines, string[] fenceLines)
        {
            this.kind = kind;
            this.headingLevel = headingLevel;
            this.listMarker = (listMarker == null) ? "" : listMarker;
            this.inlines = (inlines == null) ? new List<MarkdownInline>() : inlines;
            this.fenceLines = (fenceLines == null) ? new string[0] : fenceLines;
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

        /// <summary>フェンス中身の生行。開始・終了行は含まない。</summary>
        public string[] FenceLines
        {
            get { return this.fenceLines; }
        }
    }

    /// <summary>
    /// TextBuffer からプレビュー用ブロック列を作る。WinForms は参照しない。
    /// </summary>
    public static class MarkdownBlocks
    {
        /// <summary>
        /// バッファを見出し・段落・リスト・フェンスへ分ける。
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
            List<string> fenceAcc = null;
            List<string> paraAcc = null;
            int i = 0;
            while (i < buffer.LineCount)
            {
                string line = buffer.GetLine(i);
                if (inFence)
                {
                    if (MarkdownSyntax.TryFence(line))
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
                    FlushParagraph(result, paraAcc);
                    paraAcc = null;
                    i++;
                    continue;
                }

                if (MarkdownSyntax.TryFence(line))
                {
                    FlushParagraph(result, paraAcc);
                    paraAcc = null;
                    inFence = true;
                    fenceAcc = new List<string>();
                    i++;
                    continue;
                }

                int level;
                int hashStart;
                int contentStart;
                if (MarkdownSyntax.TryHeading(line, out level, out hashStart, out contentStart))
                {
                    FlushParagraph(result, paraAcc);
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
                    FlushParagraph(result, paraAcc);
                    paraAcc = null;
                    string marker = line.Substring(markerStart, markerLength);
                    string body = (contentStart < line.Length) ? line.Substring(contentStart) : "";
                    result.Add(new MarkdownBlock(MarkdownBlockKind.ListItem, 0, marker, ParseInlines(body), new string[0]));
                    i++;
                    continue;
                }

                if (paraAcc == null)
                {
                    paraAcc = new List<string>();
                }

                paraAcc.Add(TrimSpaces(line));
                i++;
            }

            if (inFence)
            {
                result.Add(MakeFence(fenceAcc));
            }
            else
            {
                FlushParagraph(result, paraAcc);
            }

            return result;
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

        private static void FlushParagraph(List<MarkdownBlock> result, List<string> paraAcc)
        {
            if (paraAcc == null || paraAcc.Count == 0)
            {
                return;
            }

            string joined = JoinParts(paraAcc);
            if (joined.Length == 0)
            {
                return;
            }

            result.Add(new MarkdownBlock(MarkdownBlockKind.Paragraph, 0, "", ParseInlines(joined), new string[0]));
        }

        private static string JoinParts(List<string> parts)
        {
            StringBuilder sb = new StringBuilder();
            int i = 0;
            while (i < parts.Count)
            {
                string part = parts[i];
                if (part != null && part.Length > 0)
                {
                    if (sb.Length > 0)
                    {
                        sb.Append(' ');
                    }

                    sb.Append(part);
                }

                i++;
            }

            return sb.ToString();
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
    }
}
