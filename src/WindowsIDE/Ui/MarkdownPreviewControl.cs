using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using WindowsIDE.Languages;
using WindowsIDE.Ui.Fonts;
using WindowsIDE.Workspace;

namespace WindowsIDE.Ui
{
    /// <summary>
    /// Markdown プレビュー。オーナー描画サブセット。HTML 表示コントロールは使わない。
    /// </summary>
    public sealed class MarkdownPreviewControl : Control
    {
        private const int PadDip = 8;
        private const int FenceIndentDip = 8;
        private const int BlockGapDip = 4;
        private const int QuoteIndentDip = 12;
        private const int QuoteBarDip = 3;
        private const int ListIndentDip = 16;
        private const int CodePadDip = 6;
        private const int ImageMaxHeightDip = 240;
        private const int TaskBoxDip = 12;

        private readonly ThemedScrollBar vScroll;
        private readonly List<PreviewSpan> spans;
        private readonly Dictionary<string, CachedImage> imageCache;
        private List<MarkdownBlock> blocks;
        private FontLoadResult fonts;
        private Font halfFont;
        private Font fullFont;
        private Font halfBold;
        private Font fullBold;
        private Font halfItalic;
        private Font fullItalic;
        private Font halfBoldItalic;
        private Font fullBoldItalic;
        private StringFormat typographic;
        private int fontSize;
        private int contentHeight;
        private int layoutWidth;
        private bool layoutDirty;
        private bool ignoreScroll;
        private int wheelLeftover;
        private string documentDirectory;

        /// <summary>
        /// 空のプレビューを組む。フォントは ApplyFonts で渡す。
        /// </summary>
        public MarkdownPreviewControl()
        {
            this.blocks = new List<MarkdownBlock>();
            this.spans = new List<PreviewSpan>();
            this.imageCache = new Dictionary<string, CachedImage>(StringComparer.OrdinalIgnoreCase);
            this.fontSize = WorkspaceSettings.DefaultFontSize;
            this.typographic = (StringFormat)StringFormat.GenericTypographic.Clone();
            this.typographic.FormatFlags = this.typographic.FormatFlags | StringFormatFlags.MeasureTrailingSpaces | StringFormatFlags.NoWrap | StringFormatFlags.FitBlackBox;
            this.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            this.TabStop = false;
            this.BackColor = Theme.EditorBackground;
            this.ForeColor = Theme.Foreground;
            this.vScroll = new ThemedScrollBar(true);
            this.vScroll.TrackColor = Theme.EditorBackground;
            this.vScroll.SmallChange = 1;
            this.vScroll.ValueChanged += this.OnScrollChanged;
            this.Controls.Add(this.vScroll);
            this.layoutDirty = true;
        }

        /// <summary>
        /// 本文と同じ DIP DualFont を使い、プレビュー用に Bold / Italic も作る。
        /// </summary>
        /// <param name="loadResult">同梱フォント。所有権は移さない。</param>
        /// <param name="dipSize">editor/@fontSize DIP。</param>
        public void ApplyFonts(FontLoadResult loadResult, int dipSize)
        {
            this.fonts = loadResult;
            this.fontSize = (dipSize > 0) ? dipSize : WorkspaceSettings.DefaultFontSize;
            this.RecreateFonts();
            this.layoutDirty = true;
            this.Invalidate();
        }

        /// <summary>
        /// ディスク上 `.md` のディレクトリ。相対画像の基準。無題は null。
        /// </summary>
        /// <param name="directory">ディレクトリ。null は画像を描かない。</param>
        public void SetDocumentDirectory(string directory)
        {
            if (!string.Equals(this.documentDirectory, directory, StringComparison.OrdinalIgnoreCase))
            {
                this.DisposeImages();
            }

            this.documentDirectory = directory;
            this.layoutDirty = true;
            this.Invalidate();
        }

        /// <summary>
        /// キャッシュしたブロックを描く。null は空。
        /// </summary>
        /// <param name="value">MarkdownBlocks.Parse の結果。</param>
        public void SetBlocks(List<MarkdownBlock> value)
        {
            this.blocks = (value == null) ? new List<MarkdownBlock>() : value;
            this.layoutDirty = true;
            this.RefreshChrome();
            this.Invalidate();
        }

        /// <summary>ハンドル作成後にバーを置く。</summary>
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            this.RecreateFonts();
            this.layoutDirty = true;
            this.RefreshChrome();
        }

        /// <summary>親の DPI 変更後に DIP を保ったまま物理フォントを作り直す。</summary>
        protected override void OnDpiChangedAfterParent(EventArgs e)
        {
            base.OnDpiChangedAfterParent(e);
            this.RecreateFonts();
            this.layoutDirty = true;
            this.RefreshChrome();
            this.Invalidate();
        }

        /// <summary>サイズ変更で折り返しをやり直す。</summary>
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            this.layoutDirty = true;
            this.RefreshChrome();
        }

        /// <summary>ブロックを折り返して描く。</summary>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            using (SolidBrush bg = new SolidBrush(Theme.EditorBackground))
            {
                g.FillRectangle(bg, this.ClientRectangle);
            }

            if (this.halfFont == null || this.fullFont == null)
            {
                return;
            }

            this.EnsureLayout(g);
            int scrollY = this.vScroll.Visible ? this.vScroll.Value : 0;
            int viewH = this.ClientSize.Height;
            int pass = 0;
            while (pass < 2)
            {
                int i = 0;
                while (i < this.spans.Count)
                {
                    PreviewSpan span = this.spans[i];
                    bool deco = span.Op == PreviewOp.Fill || span.Op == PreviewOp.Border || span.Op == PreviewOp.Line;
                    if ((pass == 0 && !deco) || (pass == 1 && deco))
                    {
                        i++;
                        continue;
                    }

                    int y = span.Y - scrollY;
                    int h = (span.H > 0) ? span.H : this.LineHeight();
                    if (y + h >= 0 && y < viewH)
                    {
                        this.PaintSpan(g, span, y, h);
                    }

                    i++;
                }

                pass++;
            }
        }

        /// <summary>縦ホイール。内容が収まるときは動かない。</summary>
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (this.vScroll.Visible)
            {
                int notches = DpiUtil.WheelNotches(e.Delta, ref this.wheelLeftover);
                if (notches != 0)
                {
                    this.vScroll.Value = this.vScroll.Value - (notches * this.vScroll.SmallChange);
                }
            }

            HandledMouseEventArgs handled = e as HandledMouseEventArgs;
            if (handled != null)
            {
                handled.Handled = true;
            }
        }

        /// <summary>所有フォントと画像キャッシュを破棄する。</summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.DisposeFonts();
                this.DisposeImages();
                if (this.typographic != null)
                {
                    this.typographic.Dispose();
                    this.typographic = null;
                }
            }

            base.Dispose(disposing);
        }

        private void PaintSpan(Graphics g, PreviewSpan span, int y, int h)
        {
            if (span.Op == PreviewOp.Fill)
            {
                using (SolidBrush brush = new SolidBrush(span.FillColor))
                {
                    g.FillRectangle(brush, span.X, y, Math.Max(1, span.W), Math.Max(1, h));
                }

                return;
            }

            if (span.Op == PreviewOp.Border)
            {
                using (Pen pen = new Pen(span.Color))
                {
                    g.DrawRectangle(pen, span.X, y, Math.Max(1, span.W), Math.Max(1, h));
                }

                return;
            }

            if (span.Op == PreviewOp.Line)
            {
                using (Pen pen = new Pen(span.Color))
                {
                    g.DrawLine(pen, span.X, y, span.X + Math.Max(1, span.W), y);
                }

                return;
            }

            if (span.Op == PreviewOp.Image && span.PictureImage != null)
            {
                g.DrawImage(span.PictureImage, span.X, y, Math.Max(1, span.W), Math.Max(1, h));
                return;
            }

            if (span.Op == PreviewOp.Check)
            {
                int box = Math.Max(8, Math.Min(h - 2, span.W));
                int by = y + Math.Max(0, (h - box) / 2);
                using (Pen pen = new Pen(Theme.Border))
                {
                    g.DrawRectangle(pen, span.X, by, box, box);
                }

                if (span.CheckClosed)
                {
                    using (Pen pen = new Pen(Theme.Foreground, 1.5f))
                    {
                        int x1 = span.X + 2;
                        int y1 = by + (box / 2);
                        int x2 = span.X + (box / 2) - 1;
                        int y2 = by + box - 3;
                        int x3 = span.X + box - 2;
                        int y3 = by + 2;
                        g.DrawLine(pen, x1, y1, x2, y2);
                        g.DrawLine(pen, x2, y2, x3, y3);
                    }
                }

                return;
            }

            Font half = (span.Half != null) ? span.Half : this.halfFont;
            Font full = (span.Full != null) ? span.Full : this.fullFont;
            int clipW = span.W;
            if (clipW <= 0)
            {
                clipW = this.ClientSize.Width - span.X;
            }

            Rectangle clip = new Rectangle(span.X, y, Math.Max(1, clipW), h);
            using (SolidBrush brush = new SolidBrush(span.Color))
            {
                DualFontPainter.Draw(g, span.Text, half, full, clip, span.X, brush, this.typographic);
            }

            if (span.Text != null && span.Text.Length > 0 && (span.Underline || span.Strike))
            {
                int w = span.W;
                if (w <= 0)
                {
                    w = (int)Math.Ceiling(DualFontPainter.Measure(g, span.Text, half, full, this.typographic));
                }

                using (Pen pen = new Pen(span.Color))
                {
                    if (span.Underline)
                    {
                        int uy = y + h - 2;
                        g.DrawLine(pen, span.X, uy, span.X + w, uy);
                    }

                    if (span.Strike)
                    {
                        int sy = y + (h / 2);
                        g.DrawLine(pen, span.X, sy, span.X + w, sy);
                    }
                }
            }
        }

        private void OnScrollChanged(object sender, EventArgs e)
        {
            if (this.ignoreScroll)
            {
                return;
            }

            this.Invalidate();
        }

        private void RecreateFonts()
        {
            if (this.fonts == null)
            {
                return;
            }

            int dpi = DpiUtil.GetDpi(this.IsHandleCreated ? this.Handle : IntPtr.Zero);
            int physicalPx = DpiUtil.ToPixels(this.fontSize, dpi);
            Font newHalf = this.fonts.CreateHalfWidth(physicalPx);
            Font newFull = this.fonts.CreateFullWidth(physicalPx);
            Font newHalfBold = this.fonts.CreateHalfWidthBold(physicalPx);
            Font newFullBold = this.fonts.CreateFullWidthBold(physicalPx);
            Font newHalfItalic = this.fonts.CreateHalfWidthItalic(physicalPx);
            Font newFullItalic = this.fonts.CreateFullWidthItalic(physicalPx);
            Font newHalfBoldItalic = this.fonts.CreateHalfWidthBoldItalic(physicalPx);
            Font newFullBoldItalic = this.fonts.CreateFullWidthBoldItalic(physicalPx);
            this.DisposeFonts();
            this.halfFont = newHalf;
            this.fullFont = newFull;
            this.halfBold = newHalfBold;
            this.fullBold = newFullBold;
            this.halfItalic = newHalfItalic;
            this.fullItalic = newFullItalic;
            this.halfBoldItalic = newHalfBoldItalic;
            this.fullBoldItalic = newFullBoldItalic;
        }

        private void DisposeFonts()
        {
            DisposeFont(ref this.halfFont);
            DisposeFont(ref this.fullFont);
            DisposeFont(ref this.halfBold);
            DisposeFont(ref this.fullBold);
            DisposeFont(ref this.halfItalic);
            DisposeFont(ref this.fullItalic);
            DisposeFont(ref this.halfBoldItalic);
            DisposeFont(ref this.fullBoldItalic);
        }

        private static void DisposeFont(ref Font font)
        {
            if (font != null)
            {
                font.Dispose();
                font = null;
            }
        }

        private void DisposeImages()
        {
            foreach (KeyValuePair<string, CachedImage> pair in this.imageCache)
            {
                if (pair.Value != null)
                {
                    pair.Value.Dispose();
                }
            }

            this.imageCache.Clear();
        }

        private int LineHeight()
        {
            int h = 1;
            if (this.halfFont != null)
            {
                h = this.halfFont.Height;
            }

            if (this.fullFont != null && this.fullFont.Height > h)
            {
                h = this.fullFont.Height;
            }

            return h;
        }

        private void RefreshChrome()
        {
            if (!this.IsHandleCreated || this.halfFont == null || this.fullFont == null)
            {
                return;
            }

            using (Graphics g = this.CreateGraphics())
            {
                this.EnsureLayout(g);
            }

            int dpi = DpiUtil.GetDpi(this.Handle);
            int bar = DpiUtil.ToPixels(DpiUtil.ScrollBarThicknessDip, dpi);
            int clientW = this.ClientSize.Width;
            int clientH = this.ClientSize.Height;
            int viewH = Math.Max(1, clientH);
            bool needV = this.contentHeight > viewH;
            int listW = clientW - (needV ? bar : 0);
            if (listW != this.layoutWidth)
            {
                this.layoutDirty = true;
                using (Graphics g = this.CreateGraphics())
                {
                    this.EnsureLayout(g);
                }

                needV = this.contentHeight > viewH;
                listW = clientW - (needV ? bar : 0);
            }

            this.ignoreScroll = true;
            try
            {
                this.vScroll.Minimum = 0;
                this.vScroll.Maximum = Math.Max(0, this.contentHeight - 1);
                this.vScroll.LargeChange = Math.Max(1, viewH);
                this.vScroll.SmallChange = Math.Max(1, this.LineHeight());
                this.vScroll.Visible = needV;
                if (!needV)
                {
                    this.vScroll.Value = 0;
                }
            }
            finally
            {
                this.ignoreScroll = false;
            }

            int vW = needV ? bar : 0;
            this.vScroll.Bounds = new Rectangle(this.ClientSize.Width - vW, 0, vW, Math.Max(0, this.ClientSize.Height));
        }

        private void EnsureLayout(Graphics g)
        {
            int dpi = DpiUtil.GetDpi(this.IsHandleCreated ? this.Handle : IntPtr.Zero);
            int bar = DpiUtil.ToPixels(DpiUtil.ScrollBarThicknessDip, dpi);
            int viewH = Math.Max(1, this.ClientSize.Height);
            bool guessV = this.contentHeight > viewH;
            int wrapW = this.ClientSize.Width - (guessV ? bar : 0);
            if (wrapW < 1)
            {
                wrapW = 1;
            }

            if (!this.layoutDirty && wrapW == this.layoutWidth)
            {
                return;
            }

            this.LayoutBlocks(g, wrapW);
            bool needV = this.contentHeight > viewH;
            int finalW = this.ClientSize.Width - (needV ? bar : 0);
            if (finalW < 1)
            {
                finalW = 1;
            }

            if (finalW != wrapW)
            {
                this.LayoutBlocks(g, finalW);
            }
        }

        private void LayoutBlocks(Graphics g, int width)
        {
            this.spans.Clear();
            this.layoutWidth = width;
            this.layoutDirty = false;
            if (this.halfFont == null || this.fullFont == null || g == null)
            {
                this.contentHeight = 0;
                return;
            }

            int dpi = DpiUtil.GetDpi(this.IsHandleCreated ? this.Handle : IntPtr.Zero);
            int pad = DpiUtil.ToPixels(PadDip, dpi);
            int fenceIndent = DpiUtil.ToPixels(FenceIndentDip, dpi);
            int gap = DpiUtil.ToPixels(BlockGapDip, dpi);
            int quoteIndent = DpiUtil.ToPixels(QuoteIndentDip, dpi);
            int quoteBar = DpiUtil.ToPixels(QuoteBarDip, dpi);
            int listIndent = DpiUtil.ToPixels(ListIndentDip, dpi);
            int codePad = DpiUtil.ToPixels(CodePadDip, dpi);
            int taskBox = DpiUtil.ToPixels(TaskBoxDip, dpi);
            int lineH = this.LineHeight();
            int y = pad;
            int wrapRight = width - pad;
            if (wrapRight <= pad)
            {
                wrapRight = pad + 1;
            }

            int i = 0;
            while (i < this.blocks.Count)
            {
                MarkdownBlock block = this.blocks[i];
                if (i > 0)
                {
                    y += gap;
                }

                int nest = block.NestLevel;
                int quotePad = (nest > 0) ? (pad + (nest * quoteIndent)) : pad;
                int yStart = y;
                if (block.Kind == MarkdownBlockKind.Heading)
                {
                    int extra = DpiUtil.ToPixels((7 - block.HeadingLevel) * 4, dpi);
                    if (extra < DpiUtil.ToPixels(4, dpi))
                    {
                        extra = DpiUtil.ToPixels(4, dpi);
                    }

                    if (i > 0)
                    {
                        y += extra;
                        yStart = y;
                    }

                    int x = quotePad;
                    this.AppendInlines(g, block.Inlines, quotePad, wrapRight, ref x, ref y, lineH, Theme.Keyword, true);
                    y += lineH;
                }
                else if (block.Kind == MarkdownBlockKind.ListItem)
                {
                    int left = quotePad + (block.IndentLevel * listIndent);
                    if (block.TaskState != MarkdownTaskState.None)
                    {
                        this.spans.Add(PreviewSpan.CheckBox(left, y, taskBox, lineH, block.TaskState == MarkdownTaskState.Closed));
                        left += taskBox + DpiUtil.ToPixels(6, dpi);
                    }
                    else
                    {
                        string marker = this.ListPrefix(block.ListMarker);
                        int markerW = (int)Math.Ceiling(DualFontPainter.Measure(g, marker, this.halfFont, this.fullFont, this.typographic));
                        this.spans.Add(PreviewSpan.TextRun(left, y, markerW, lineH, marker, Theme.Keyword, this.halfFont, this.fullFont, false, false));
                        left += markerW;
                    }

                    int x = left;
                    this.AppendInlines(g, block.Inlines, left, wrapRight, ref x, ref y, lineH, Theme.Foreground, false);
                    y += lineH;
                }
                else if (block.Kind == MarkdownBlockKind.Fence || block.Kind == MarkdownBlockKind.IndentedCode)
                {
                    int boxLeft = quotePad;
                    int boxRight = wrapRight;
                    int innerLeft = boxLeft + fenceIndent;
                    int innerRight = boxRight - codePad;
                    if (innerRight <= innerLeft)
                    {
                        innerRight = innerLeft + 1;
                    }

                    int codeTop = y;
                    string[] lines = block.FenceLines;
                    int f = 0;
                    while (f < lines.Length)
                    {
                        int x = innerLeft;
                        this.AppendText(
                            g,
                            (lines[f] == null) ? "" : lines[f],
                            innerLeft,
                            innerRight,
                            ref x,
                            ref y,
                            lineH,
                            Theme.StringLiteral,
                            this.halfFont,
                            this.fullFont,
                            false,
                            false,
                            true);
                        y += lineH;
                        f++;
                    }

                    if (lines.Length == 0)
                    {
                        y += lineH;
                    }

                    int boxH = y - codeTop;
                    if (boxH < lineH)
                    {
                        boxH = lineH;
                    }

                    this.spans.Add(PreviewSpan.Fill(boxLeft, codeTop, Math.Max(1, boxRight - boxLeft), boxH, Theme.CurrentLine));
                    this.spans.Add(PreviewSpan.Border(boxLeft, codeTop, Math.Max(1, boxRight - boxLeft), boxH, Theme.Border));
                }
                else if (block.Kind == MarkdownBlockKind.HorizontalRule)
                {
                    int mid = y + (lineH / 2);
                    this.spans.Add(PreviewSpan.Rule(quotePad, mid, Math.Max(1, wrapRight - quotePad), Theme.Border));
                    y += lineH;
                }
                else if (block.Kind == MarkdownBlockKind.Table)
                {
                    y = this.LayoutTable(g, block, quotePad, wrapRight, y, lineH, dpi);
                }
                else
                {
                    int x = quotePad;
                    this.AppendInlines(g, block.Inlines, quotePad, wrapRight, ref x, ref y, lineH, Theme.Foreground, false);
                    y += lineH;
                }

                if (nest > 0)
                {
                    int barH = y - yStart;
                    if (barH < lineH)
                    {
                        barH = lineH;
                    }

                    int n = 0;
                    while (n < nest)
                    {
                        int barX = pad + (n * quoteIndent);
                        this.spans.Add(PreviewSpan.Fill(barX, yStart, Math.Max(1, quoteBar), barH, Theme.Selection));
                        n++;
                    }
                }

                i++;
            }

            this.contentHeight = y + pad;
        }

        private int LayoutTable(Graphics g, MarkdownBlock block, int left, int wrapRight, int y, int lineH, int dpi)
        {
            List<MarkdownTableRow> rows = block.TableRows;
            if (rows == null || rows.Count == 0)
            {
                return y + lineH;
            }

            int cols = 1;
            int r = 0;
            while (r < rows.Count)
            {
                MarkdownTableRow row = rows[r];
                if (row != null && row.Cells != null && row.Cells.Count > cols)
                {
                    cols = row.Cells.Count;
                }

                r++;
            }

            int cellPad = DpiUtil.ToPixels(4, dpi);
            int[] widths = new int[cols];
            r = 0;
            while (r < rows.Count)
            {
                MarkdownTableRow row = rows[r];
                int c = 0;
                while (c < cols)
                {
                    bool header = r == 0;
                    int w = this.MeasureCell(g, this.CellInlines(row, c), header) + (cellPad * 2);
                    if (w > widths[c])
                    {
                        widths[c] = w;
                    }

                    c++;
                }

                r++;
            }

            int total = 0;
            int c2 = 0;
            while (c2 < cols)
            {
                if (widths[c2] < DpiUtil.ToPixels(24, dpi))
                {
                    widths[c2] = DpiUtil.ToPixels(24, dpi);
                }

                total += widths[c2];
                c2++;
            }

            int avail = wrapRight - left;
            if (avail < 1)
            {
                avail = 1;
            }

            if (total > avail)
            {
                int c3 = 0;
                while (c3 < cols)
                {
                    widths[c3] = Math.Max(1, (widths[c3] * avail) / total);
                    c3++;
                }
            }

            r = 0;
            while (r < rows.Count)
            {
                MarkdownTableRow row = rows[r];
                int x = left;
                int c = 0;
                while (c < cols)
                {
                    int cw = widths[c];
                    this.spans.Add(PreviewSpan.Border(x, y, cw, lineH, Theme.Border));
                    bool header = r == 0;
                    int tx = x + cellPad;
                    int cellRight = x + cw - cellPad;
                    if (cellRight <= tx)
                    {
                        cellRight = tx + 1;
                    }

                    this.AppendCellInlines(g, this.CellInlines(row, c), tx, cellRight, y, lineH, header);
                    x += cw;
                    c++;
                }

                y += lineH;
                r++;
            }

            return y;
        }

        private List<MarkdownInline> CellInlines(MarkdownTableRow row, int index)
        {
            if (row == null || row.Cells == null || index < 0 || index >= row.Cells.Count)
            {
                return new List<MarkdownInline>();
            }

            List<MarkdownInline> inlines = row.Cells[index];
            return (inlines == null) ? new List<MarkdownInline>() : inlines;
        }

        private int MeasureCell(Graphics g, List<MarkdownInline> inlines, bool header)
        {
            if (inlines == null || inlines.Count == 0)
            {
                return 0;
            }

            int width = 0;
            int i = 0;
            while (i < inlines.Count)
            {
                MarkdownInline inline = inlines[i];
                if (inline != null && inline.Text != null && inline.Text.Length > 0 && inline.Kind != MarkdownInlineKind.Break && inline.Kind != MarkdownInlineKind.Image)
                {
                    Font half;
                    Font full;
                    Color color;
                    bool underline;
                    bool strike;
                    bool code;
                    this.StyleInline(inline, header, out half, out full, out color, out underline, out strike, out code);
                    width += (int)Math.Ceiling(DualFontPainter.Measure(g, inline.Text, half, full, this.typographic));
                }

                i++;
            }

            return width;
        }

        private void AppendCellInlines(Graphics g, List<MarkdownInline> inlines, int left, int right, int y, int lineH, bool header)
        {
            if (inlines == null)
            {
                return;
            }

            int x = left;
            int i = 0;
            while (i < inlines.Count)
            {
                MarkdownInline inline = inlines[i];
                if (inline != null && inline.Text != null && inline.Text.Length > 0 && inline.Kind != MarkdownInlineKind.Break && inline.Kind != MarkdownInlineKind.Image)
                {
                    Font half;
                    Font full;
                    Color color;
                    bool underline;
                    bool strike;
                    bool code;
                    this.StyleInline(inline, header, out half, out full, out color, out underline, out strike, out code);
                    this.AppendClippedText(g, inline.Text, ref x, y, right, lineH, color, half, full, underline, strike, code);
                }

                i++;
            }
        }

        private void AppendClippedText(
            Graphics g,
            string text,
            ref int x,
            int y,
            int right,
            int lineH,
            Color color,
            Font half,
            Font full,
            bool underline,
            bool strike,
            bool codeBand)
        {
            if (text == null || text.Length == 0)
            {
                return;
            }

            int remain = right - x;
            if (remain < 1)
            {
                return;
            }

            if (half == null)
            {
                half = this.halfFont;
            }

            if (full == null)
            {
                full = this.fullFont;
            }

            string fitted = DualFontPainter.FitEllipsis(g, text, half, full, remain, this.typographic);
            if (fitted == null || fitted.Length == 0)
            {
                return;
            }

            int w = (int)Math.Ceiling(DualFontPainter.Measure(g, fitted, half, full, this.typographic));
            if (w > remain)
            {
                w = remain;
            }

            if (codeBand)
            {
                this.spans.Add(PreviewSpan.Fill(x, y, w, lineH, Theme.CurrentLine));
            }

            this.spans.Add(PreviewSpan.TextRun(x, y, w, lineH, fitted, color, half, full, underline, strike));
            x += w;
        }

        private void StyleInline(MarkdownInline inline, bool heading, out Font half, out Font full, out Color color, out bool underline, out bool strike, out bool code)
        {
            code = inline.Code || inline.Kind == MarkdownInlineKind.Code;
            bool strong = heading || inline.Strong || inline.Kind == MarkdownInlineKind.Strong;
            bool em = inline.Emphasis || inline.Kind == MarkdownInlineKind.Emphasis;
            this.PickFonts(strong && !code, em && !code, code, heading && em && !code, out half, out full);
            color = Theme.Foreground;
            if (code)
            {
                color = Theme.StringLiteral;
            }
            else if (inline.Kind == MarkdownInlineKind.Link)
            {
                color = Theme.Local;
            }
            else if (heading)
            {
                color = Theme.Keyword;
            }

            underline = inline.Kind == MarkdownInlineKind.Link;
            strike = inline.Strike;
        }

        private string ListPrefix(string marker)
        {
            if (marker != null && marker.Length > 0 && marker[0] >= '0' && marker[0] <= '9')
            {
                return marker + " ";
            }

            return "• ";
        }

        private void AppendInlines(Graphics g, List<MarkdownInline> inlines, int left, int wrapRight, ref int x, ref int y, int lineH, Color defaultColor, bool heading)
        {
            if (inlines == null)
            {
                return;
            }

            int dpi = DpiUtil.GetDpi(this.IsHandleCreated ? this.Handle : IntPtr.Zero);
            int i = 0;
            while (i < inlines.Count)
            {
                MarkdownInline inline = inlines[i];
                if (inline.Kind == MarkdownInlineKind.Break)
                {
                    x = left;
                    y += lineH;
                    i++;
                    continue;
                }

                if (inline.Kind == MarkdownInlineKind.Image)
                {
                    this.AppendImage(g, inline, left, wrapRight, ref x, ref y, lineH, dpi);
                    i++;
                    continue;
                }

                bool code = inline.Code || inline.Kind == MarkdownInlineKind.Code;
                bool strong = heading || inline.Strong || inline.Kind == MarkdownInlineKind.Strong;
                bool em = inline.Emphasis || inline.Kind == MarkdownInlineKind.Emphasis;
                Font half;
                Font full;
                this.PickFonts(strong && !code, em && !code, code, heading && em && !code, out half, out full);
                Color color = defaultColor;
                if (code)
                {
                    color = Theme.StringLiteral;
                }
                else if (inline.Kind == MarkdownInlineKind.Link)
                {
                    color = Theme.Local;
                }
                else if (!heading)
                {
                    color = Theme.Foreground;
                }

                bool underline = inline.Kind == MarkdownInlineKind.Link;
                bool strike = inline.Strike;
                this.AppendText(g, inline.Text, left, wrapRight, ref x, ref y, lineH, color, half, full, underline, strike, code);
                i++;
            }
        }

        private void PickFonts(bool bold, bool italic, bool code, bool boldItalic, out Font half, out Font full)
        {
            if (code)
            {
                half = this.halfFont;
                full = this.fullFont;
                return;
            }

            if (boldItalic || (bold && italic))
            {
                half = this.halfBoldItalic;
                full = this.fullBoldItalic;
                return;
            }

            if (bold)
            {
                half = this.halfBold;
                full = this.fullBold;
                return;
            }

            if (italic)
            {
                half = this.halfItalic;
                full = this.fullItalic;
                return;
            }

            half = this.halfFont;
            full = this.fullFont;
        }

        private void AppendImage(Graphics g, MarkdownInline inline, int left, int wrapRight, ref int x, ref int y, int lineH, int dpi)
        {
            string alt = (inline.Text == null || inline.Text.Length == 0) ? "画像" : inline.Text;
            Image picture = this.TryGetImage(inline.Destination);
            if (picture == null)
            {
                this.AppendText(g, "[画像: " + alt + "]", left, wrapRight, ref x, ref y, lineH, Theme.Comment, this.halfFont, this.fullFont, false, false, false);
                return;
            }

            int maxH = DpiUtil.ToPixels(ImageMaxHeightDip, dpi);
            int maxW = wrapRight - left;
            if (maxW < 8)
            {
                maxW = 8;
            }

            int iw = picture.Width;
            int ih = picture.Height;
            if (iw < 1)
            {
                iw = 1;
            }

            if (ih < 1)
            {
                ih = 1;
            }

            float scale = 1f;
            if (iw > maxW)
            {
                scale = (float)maxW / (float)iw;
            }

            if ((int)(ih * scale) > maxH)
            {
                scale = (float)maxH / (float)ih;
            }

            int dw = Math.Max(1, (int)(iw * scale));
            int dh = Math.Max(1, (int)(ih * scale));
            if (x > left && x + dw > wrapRight)
            {
                x = left;
                y += lineH;
            }

            this.spans.Add(PreviewSpan.Picture(x, y, dw, dh, picture));
            y += dh;
            x = left;
        }

        private Image TryGetImage(string destination)
        {
            string full;
            if (!MarkdownLocalPath.TryNormalize(this.documentDirectory, destination, out full))
            {
                return null;
            }

            if (!File.Exists(full))
            {
                return null;
            }

            DateTime mtime;
            try
            {
                mtime = File.GetLastWriteTimeUtc(full);
            }
            catch (Exception)
            {
                return null;
            }

            string key = full + "|" + mtime.Ticks.ToString();
            CachedImage cached;
            if (this.imageCache.TryGetValue(key, out cached) && cached != null && cached.Picture != null)
            {
                return cached.Picture;
            }

            MemoryStream stream = null;
            try
            {
                byte[] bytes = File.ReadAllBytes(full);
                stream = new MemoryStream(bytes);
                Image loaded = Image.FromStream(stream);
                this.EvictStaleImages(full, key);
                this.imageCache[key] = new CachedImage(loaded, stream);
                return loaded;
            }
            catch (Exception)
            {
                if (stream != null)
                {
                    stream.Dispose();
                }

                return null;
            }
        }

        private void EvictStaleImages(string fullPath, string keepKey)
        {
            if (fullPath == null || keepKey == null)
            {
                return;
            }

            string prefix = fullPath + "|";
            List<string> drop = new List<string>();
            foreach (KeyValuePair<string, CachedImage> pair in this.imageCache)
            {
                if (pair.Key != keepKey && pair.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    drop.Add(pair.Key);
                }
            }

            int i = 0;
            while (i < drop.Count)
            {
                CachedImage stale;
                if (this.imageCache.TryGetValue(drop[i], out stale))
                {
                    this.imageCache.Remove(drop[i]);
                    if (stale != null)
                    {
                        stale.Dispose();
                    }
                }

                i++;
            }
        }

        private void AppendText(
            Graphics g,
            string text,
            int left,
            int wrapRight,
            ref int x,
            ref int y,
            int lineH,
            Color color,
            Font half,
            Font full,
            bool underline,
            bool strike,
            bool codeBand)
        {
            if (text == null || text.Length == 0)
            {
                return;
            }

            if (half == null)
            {
                half = this.halfFont;
            }

            if (full == null)
            {
                full = this.fullFont;
            }

            int pos = 0;
            while (pos < text.Length)
            {
                int remain = wrapRight - x;
                if (remain < 8 && x > left)
                {
                    x = left;
                    y += lineH;
                    remain = wrapRight - x;
                }

                int fit = this.FitCount(g, text, pos, remain, half, full);
                if (fit < 1)
                {
                    if (x > left)
                    {
                        x = left;
                        y += lineH;
                        remain = wrapRight - x;
                        fit = this.FitCount(g, text, pos, remain, half, full);
                    }

                    if (fit < 1)
                    {
                        fit = 1;
                    }
                }

                MarkdownPreviewSlice slice = MarkdownPreviewText.Take(text, pos, fit);
                if (slice.DrawText.Length > 0)
                {
                    int w = (int)Math.Ceiling(DualFontPainter.Measure(g, slice.DrawText, half, full, this.typographic));
                    if (codeBand)
                    {
                        this.spans.Add(PreviewSpan.Fill(x, y, w, lineH, Theme.CurrentLine));
                    }

                    this.spans.Add(PreviewSpan.TextRun(x, y, w, lineH, slice.DrawText, color, half, full, underline, strike));
                    x += w;
                }

                if (slice.Consumed < 1)
                {
                    break;
                }

                pos += slice.Consumed;
                if (slice.BreakLine && pos < text.Length)
                {
                    x = left;
                    y += lineH;
                }
            }
        }

        private int FitCount(Graphics g, string text, int start, int maxWidth, Font half, Font full)
        {
            int remaining = text.Length - start;
            if (remaining <= 0 || maxWidth <= 0)
            {
                return 0;
            }

            if (DualFontPainter.Measure(g, text.Substring(start, 1), half, full, this.typographic) > maxWidth)
            {
                return 0;
            }

            int lo = 1;
            int hi = remaining;
            int fit = 1;
            while (lo <= hi)
            {
                int mid = lo + ((hi - lo) / 2);
                if (DualFontPainter.Measure(g, text.Substring(start, mid), half, full, this.typographic) <= maxWidth)
                {
                    fit = mid;
                    lo = mid + 1;
                }
                else
                {
                    hi = mid - 1;
                }
            }

            return fit;
        }

        private enum PreviewOp
        {
            Text,
            Fill,
            Border,
            Line,
            Image,
            Check
        }

        private sealed class CachedImage
        {
            public Image Picture;
            public MemoryStream Stream;

            public CachedImage(Image picture, MemoryStream stream)
            {
                this.Picture = picture;
                this.Stream = stream;
            }

            public void Dispose()
            {
                if (this.Picture != null)
                {
                    this.Picture.Dispose();
                    this.Picture = null;
                }

                if (this.Stream != null)
                {
                    this.Stream.Dispose();
                    this.Stream = null;
                }
            }
        }

        private sealed class PreviewSpan
        {
            public PreviewOp Op;
            public int X;
            public int Y;
            public int W;
            public int H;
            public string Text;
            public Color Color;
            public Color FillColor;
            public Font Half;
            public Font Full;
            public bool Underline;
            public bool Strike;
            public Image PictureImage;
            public bool CheckClosed;

            public static PreviewSpan TextRun(int x, int y, int w, int h, string text, Color color, Font half, Font full, bool underline, bool strike)
            {
                PreviewSpan span = new PreviewSpan();
                span.Op = PreviewOp.Text;
                span.X = x;
                span.Y = y;
                span.W = w;
                span.H = h;
                span.Text = text;
                span.Color = color;
                span.Half = half;
                span.Full = full;
                span.Underline = underline;
                span.Strike = strike;
                return span;
            }

            public static PreviewSpan Fill(int x, int y, int w, int h, Color fill)
            {
                PreviewSpan span = new PreviewSpan();
                span.Op = PreviewOp.Fill;
                span.X = x;
                span.Y = y;
                span.W = w;
                span.H = h;
                span.FillColor = fill;
                return span;
            }

            public static PreviewSpan Border(int x, int y, int w, int h, Color color)
            {
                PreviewSpan span = new PreviewSpan();
                span.Op = PreviewOp.Border;
                span.X = x;
                span.Y = y;
                span.W = w;
                span.H = h;
                span.Color = color;
                return span;
            }

            public static PreviewSpan Rule(int x, int y, int w, Color color)
            {
                PreviewSpan span = new PreviewSpan();
                span.Op = PreviewOp.Line;
                span.X = x;
                span.Y = y;
                span.W = w;
                span.H = 1;
                span.Color = color;
                return span;
            }

            public static PreviewSpan Picture(int x, int y, int w, int h, Image picture)
            {
                PreviewSpan span = new PreviewSpan();
                span.Op = PreviewOp.Image;
                span.X = x;
                span.Y = y;
                span.W = w;
                span.H = h;
                span.PictureImage = picture;
                return span;
            }

            public static PreviewSpan CheckBox(int x, int y, int w, int h, bool closed)
            {
                PreviewSpan span = new PreviewSpan();
                span.Op = PreviewOp.Check;
                span.X = x;
                span.Y = y;
                span.W = w;
                span.H = h;
                span.CheckClosed = closed;
                return span;
            }
        }
    }

    /// <summary>
    /// プレビュー 1 行分の切り出し。描く文字列、消費文字数、行を折るかを持つ。
    /// </summary>
    internal sealed class MarkdownPreviewSlice
    {
        private readonly string drawText;
        private readonly int consumed;
        private readonly bool breakLine;

        /// <summary>
        /// 切り出し結果を作る。
        /// </summary>
        /// <param name="drawText">描く文字列。null は空。</param>
        /// <param name="consumed">消費する文字数。</param>
        /// <param name="breakLine">行を折るなら true。</param>
        public MarkdownPreviewSlice(string drawText, int consumed, bool breakLine)
        {
            this.drawText = (drawText == null) ? "" : drawText;
            this.consumed = consumed;
            this.breakLine = breakLine;
        }

        /// <summary>描く文字列。空なら幅を進めない。</summary>
        public string DrawText
        {
            get { return this.drawText; }
        }

        /// <summary>このスライスが消費する文字数。</summary>
        public int Consumed
        {
            get { return this.consumed; }
        }

        /// <summary>消費のあと行を折るなら true。</summary>
        public bool BreakLine
        {
            get { return this.breakLine; }
        }
    }

    /// <summary>
    /// プレビュー本文の折り返し切り出し。末尾まで届く空白は残し、続きに非空白がある折り返し空白は描かない。
    /// </summary>
    internal static class MarkdownPreviewText
    {
        /// <summary>
        /// 残り幅に入る文字数から、描く文字列と消費文字数を決める。末尾まで届く空白は残し、続きに非空白がある折り返し空白は描かない。
        /// </summary>
        /// <param name="text">対象文字列。</param>
        /// <param name="pos">開始位置。</param>
        /// <param name="fit">今の行の残り幅に入る文字数。</param>
        /// <returns>描く文字列、消費文字数、行を折るか。</returns>
        public static MarkdownPreviewSlice Take(string text, int pos, int fit)
        {
            if (text == null || pos < 0 || pos >= text.Length || fit < 1)
            {
                return new MarkdownPreviewSlice("", 0, false);
            }

            int remaining = text.Length - pos;
            if (fit > remaining)
            {
                fit = remaining;
            }

            if (pos + fit >= text.Length)
            {
                return new MarkdownPreviewSlice(text.Substring(pos, fit), fit, false);
            }

            int lastSpace = -1;
            int k = 0;
            while (k < fit)
            {
                if (IsBreakSpace(text[pos + k]))
                {
                    lastSpace = k;
                }

                k++;
            }

            if (lastSpace >= 1)
            {
                int after = pos + lastSpace + 1;
                while (after < text.Length && IsBreakSpace(text[after]))
                {
                    after++;
                }

                if (after < text.Length)
                {
                    string raw = text.Substring(pos, lastSpace + 1);
                    return new MarkdownPreviewSlice(raw.TrimEnd(' ', '\t'), after - pos, true);
                }

                return new MarkdownPreviewSlice(text.Substring(pos, fit), fit, true);
            }

            if (RestIsBreakSpace(text, pos + fit))
            {
                return new MarkdownPreviewSlice(text.Substring(pos, fit), fit, true);
            }

            int next = pos + fit;
            while (next < text.Length && IsBreakSpace(text[next]))
            {
                next++;
            }

            return new MarkdownPreviewSlice(text.Substring(pos, fit).TrimEnd(' ', '\t'), next - pos, true);
        }

        private static bool IsBreakSpace(char ch)
        {
            return ch == ' ' || ch == '\t';
        }

        private static bool RestIsBreakSpace(string text, int index)
        {
            int i = index;
            while (i < text.Length)
            {
                if (!IsBreakSpace(text[i]))
                {
                    return false;
                }

                i++;
            }

            return true;
        }
    }
}
