using System;
using System.Collections.Generic;
using System.Drawing;
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

        private readonly ThemedScrollBar vScroll;
        private readonly List<PreviewSpan> spans;
        private List<MarkdownBlock> blocks;
        private FontLoadResult fonts;
        private Font halfFont;
        private Font fullFont;
        private StringFormat typographic;
        private int fontSize;
        private int contentHeight;
        private int layoutWidth;
        private bool layoutDirty;
        private bool ignoreScroll;
        private int wheelLeftover;

        /// <summary>
        /// 空のプレビューを組む。フォントは ApplyFonts で渡す。
        /// </summary>
        public MarkdownPreviewControl()
        {
            this.blocks = new List<MarkdownBlock>();
            this.spans = new List<PreviewSpan>();
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
        /// 本文と同じ DIP DualFont を使う。Bold は作らない。
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
            int i = 0;
            while (i < this.spans.Count)
            {
                PreviewSpan span = this.spans[i];
                int y = span.Y - scrollY;
                int lineH = this.LineHeight();
                if (y + lineH >= 0 && y < viewH)
                {
                    Rectangle clip = new Rectangle(span.X, y, Math.Max(1, this.ClientSize.Width - span.X), lineH);
                    using (SolidBrush brush = new SolidBrush(span.Color))
                    {
                        DualFontPainter.Draw(g, span.Text, this.halfFont, this.fullFont, clip, span.X, brush, this.typographic);
                    }
                }

                i++;
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

        /// <summary>所有フォントだけ破棄する。</summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.DisposeFonts();
                if (this.typographic != null)
                {
                    this.typographic.Dispose();
                    this.typographic = null;
                }
            }

            base.Dispose(disposing);
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
            this.DisposeFonts();
            this.halfFont = newHalf;
            this.fullFont = newFull;
        }

        private void DisposeFonts()
        {
            if (this.halfFont != null)
            {
                this.halfFont.Dispose();
                this.halfFont = null;
            }

            if (this.fullFont != null)
            {
                this.fullFont.Dispose();
                this.fullFont = null;
            }
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
                    }

                    int x = pad;
                    this.AppendInlines(g, block.Inlines, pad, wrapRight, ref x, ref y, lineH, Theme.Keyword);
                    y += lineH;
                }
                else if (block.Kind == MarkdownBlockKind.ListItem)
                {
                    string marker = this.ListPrefix(block.ListMarker);
                    int markerW = (int)Math.Ceiling(DualFontPainter.Measure(g, marker, this.halfFont, this.fullFont, this.typographic));
                    this.spans.Add(new PreviewSpan(pad, y, marker, Theme.Keyword));
                    int left = pad + markerW;
                    int x = left;
                    this.AppendInlines(g, block.Inlines, left, wrapRight, ref x, ref y, lineH, Theme.Foreground);
                    y += lineH;
                }
                else if (block.Kind == MarkdownBlockKind.Fence)
                {
                    int left = pad + fenceIndent;
                    string[] lines = block.FenceLines;
                    int f = 0;
                    while (f < lines.Length)
                    {
                        int x = left;
                        this.AppendText(g, (lines[f] == null) ? "" : lines[f], left, wrapRight, ref x, ref y, lineH, Theme.StringLiteral);
                        y += lineH;
                        f++;
                    }

                    if (lines.Length == 0)
                    {
                        y += lineH;
                    }
                }
                else
                {
                    int x = pad;
                    this.AppendInlines(g, block.Inlines, pad, wrapRight, ref x, ref y, lineH, Theme.Foreground);
                    y += lineH;
                }

                i++;
            }

            this.contentHeight = y + pad;
        }

        private string ListPrefix(string marker)
        {
            if (marker != null && marker.Length > 0 && marker[0] >= '0' && marker[0] <= '9')
            {
                return marker + " ";
            }

            return "• ";
        }

        private void AppendInlines(Graphics g, List<MarkdownInline> inlines, int left, int wrapRight, ref int x, ref int y, int lineH, Color defaultColor)
        {
            if (inlines == null)
            {
                return;
            }

            int i = 0;
            while (i < inlines.Count)
            {
                MarkdownInline inline = inlines[i];
                Color color = defaultColor;
                if (inline.Kind == MarkdownInlineKind.Emphasis || inline.Kind == MarkdownInlineKind.Strong)
                {
                    color = Theme.Keyword;
                }
                else if (inline.Kind == MarkdownInlineKind.Code)
                {
                    color = Theme.StringLiteral;
                }

                this.AppendText(g, inline.Text, left, wrapRight, ref x, ref y, lineH, color);
                i++;
            }
        }

        private void AppendText(Graphics g, string text, int left, int wrapRight, ref int x, ref int y, int lineH, Color color)
        {
            if (text == null || text.Length == 0)
            {
                return;
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

                int fit = this.FitCount(g, text, pos, remain);
                if (fit < 1)
                {
                    if (x > left)
                    {
                        x = left;
                        y += lineH;
                        remain = wrapRight - x;
                        fit = this.FitCount(g, text, pos, remain);
                    }

                    if (fit < 1)
                    {
                        fit = 1;
                    }
                }

                if (pos + fit < text.Length)
                {
                    int lastSpace = -1;
                    int k = 0;
                    while (k < fit)
                    {
                        char ch = text[pos + k];
                        if (ch == ' ' || ch == '\t')
                        {
                            lastSpace = k;
                        }

                        k++;
                    }

                    if (lastSpace >= 1)
                    {
                        fit = lastSpace + 1;
                    }
                }

                string piece = text.Substring(pos, fit).TrimEnd(' ', '\t');
                if (piece.Length > 0)
                {
                    this.spans.Add(new PreviewSpan(x, y, piece, color));
                    x += (int)Math.Ceiling(DualFontPainter.Measure(g, piece, this.halfFont, this.fullFont, this.typographic));
                }

                pos += fit;
                while (pos < text.Length && (text[pos] == ' ' || text[pos] == '\t'))
                {
                    pos++;
                }

                if (pos < text.Length)
                {
                    x = left;
                    y += lineH;
                }
            }
        }

        private int FitCount(Graphics g, string text, int start, int maxWidth)
        {
            int remaining = text.Length - start;
            if (remaining <= 0 || maxWidth <= 0)
            {
                return 0;
            }

            if (DualFontPainter.Measure(g, text.Substring(start, 1), this.halfFont, this.fullFont, this.typographic) > maxWidth)
            {
                return 0;
            }

            int lo = 1;
            int hi = remaining;
            int fit = 1;
            while (lo <= hi)
            {
                int mid = lo + ((hi - lo) / 2);
                if (DualFontPainter.Measure(g, text.Substring(start, mid), this.halfFont, this.fullFont, this.typographic) <= maxWidth)
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

        private sealed class PreviewSpan
        {
            public int X;
            public int Y;
            public string Text;
            public Color Color;

            public PreviewSpan(int x, int y, string text, Color color)
            {
                this.X = x;
                this.Y = y;
                this.Text = text;
                this.Color = color;
            }
        }
    }
}
