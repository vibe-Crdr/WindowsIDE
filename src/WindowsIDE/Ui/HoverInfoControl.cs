using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using WindowsIDE.Languages;
using WindowsIDE.Ui.Fonts;

namespace WindowsIDE.Ui
{
    /// <summary>
    /// F-HOV のオーナー描画ポップアップ。フォーカスを奪わず、折り返しと ThemedScrollBar を持つ。
    /// </summary>
    public sealed class HoverInfoControl : Form
    {
        /// <summary>マウス静止から表示までの待ち（ミリ秒）。</summary>
        public const int ShowDelayMs = 400;

        private const int MaxWidthDip = 480;
        private const int MaxHeightDip = 240;
        private const int PadDip = 6;
        private const int SW_SHOWNOACTIVATE = 4;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        private Font halfFont;
        private Font fullFont;
        private StringFormat typographic;
        private string[] lines;
        private int lineHeight;
        private int pad;
        private bool mouseInside;
        private LanguageKind language;
        private readonly ThemedScrollBar vScroll;
        private int contentHeight;
        private bool ignoreScroll;

        /// <summary>
        /// 枠なしのホバー窓を作る。フォントは 12 DIP DualFont。
        /// </summary>
        /// <param name="fonts">同梱フォント。null なら FontLoader.LastResult。</param>
        public HoverInfoControl(FontLoadResult fonts)
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.Manual;
            this.ControlBox = false;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.TopMost = true;
            this.BackColor = Theme.Background;
            this.ForeColor = Theme.Foreground;
            this.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            this.typographic = (StringFormat)StringFormat.GenericTypographic.Clone();
            this.typographic.FormatFlags = this.typographic.FormatFlags | StringFormatFlags.MeasureTrailingSpaces | StringFormatFlags.NoWrap | StringFormatFlags.FitBlackBox;
            this.lines = new string[0];
            this.language = LanguageKind.Plain;
            this.vScroll = new ThemedScrollBar(true);
            this.vScroll.TrackColor = Theme.Background;
            this.vScroll.SmallChange = 1;
            this.vScroll.ValueChanged += this.OnScrollChanged;
            this.Controls.Add(this.vScroll);
            FontLoadResult load = fonts;
            if (load == null)
            {
                load = FontLoader.LastResult;
            }

            this.RecreateFonts(load);
            this.MouseEnter += this.OnPopupMouseEnter;
            this.MouseLeave += this.OnPopupMouseLeave;
        }

        /// <summary>ポップアップ上にマウスがある。</summary>
        public bool IsMouseOverPopup
        {
            get
            {
                if (!this.Visible)
                {
                    return false;
                }

                Point pt = Control.MousePosition;
                return this.Bounds.Contains(pt) || this.mouseInside;
            }
        }

        /// <summary>フォーカスを奪わない。</summary>
        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        /// <summary>WS_EX_NOACTIVATE と WS_EX_TOOLWINDOW。</summary>
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
                return cp;
            }
        }

        /// <summary>
        /// 本文を載せ、識別子の下（収まらなければ上）に出す。ガターには出さない。
        /// </summary>
        /// <param name="text">本文。null や空なら隠す。</param>
        /// <param name="kind">シグネチャ行の字句色用。</param>
        /// <param name="belowScreen">識別子下端のスクリーン座標。</param>
        /// <param name="aboveScreen">識別子上端のスクリーン座標。</param>
        /// <param name="minScreenX">これより左（ガター）には出さない。</param>
        public void ShowText(string text, LanguageKind kind, Point belowScreen, Point aboveScreen, int minScreenX)
        {
            if (string.IsNullOrEmpty(text))
            {
                this.Hide();
                return;
            }

            this.language = kind;
            this.RecreateFonts(FontLoader.LastResult);
            this.MeasureWrapAndSize(text);
            Rectangle work = Screen.FromPoint(belowScreen).WorkingArea;
            int x = belowScreen.X;
            if (x < minScreenX)
            {
                x = minScreenX;
            }

            int y = belowScreen.Y;
            if (y + this.Height > work.Bottom)
            {
                y = aboveScreen.Y - this.Height;
            }

            if (y < work.Top)
            {
                y = work.Top;
            }

            if (x + this.Width > work.Right)
            {
                x = work.Right - this.Width;
            }

            if (x < work.Left)
            {
                x = work.Left;
            }

            this.Location = new Point(x, y);
            this.ShowInactive();
            this.Invalidate();
        }

        /// <summary>隠す。</summary>
        public new void Hide()
        {
            this.mouseInside = false;
            base.Hide();
        }

        /// <summary>親の DPI 変更後に 12 DIP を保つ。</summary>
        protected override void OnDpiChanged(DpiChangedEventArgs e)
        {
            base.OnDpiChanged(e);
            this.RecreateFonts(FontLoader.LastResult);
            this.Invalidate();
        }

        /// <summary>背景・枠・本文を描く。省略記号は付けない。</summary>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(Theme.Background);
            using (Pen border = new Pen(Theme.Border))
            {
                g.DrawRectangle(border, 0, 0, this.Width - 1, this.Height - 1);
            }

            if (this.halfFont == null || this.fullFont == null || this.lines == null)
            {
                return;
            }

            int bar = this.vScroll.Visible ? this.vScroll.Width : 0;
            int innerW = this.Width - this.pad * 2 - bar;
            int innerH = this.Height - this.pad * 2;
            int scroll = this.vScroll.Visible ? this.vScroll.Value : 0;
            int y = this.pad - scroll;
            bool signature = true;
            int i = 0;
            while (i < this.lines.Length)
            {
                string line = this.lines[i];
                if (signature && i > 0 && line.Length == 0)
                {
                    signature = false;
                }

                if (y + this.lineHeight > this.pad && y < this.pad + innerH)
                {
                    Rectangle clip = new Rectangle(this.pad, y, innerW, this.lineHeight);
                    if (y < this.pad)
                    {
                        int cut = this.pad - y;
                        clip = new Rectangle(this.pad, this.pad, innerW, this.lineHeight - cut);
                    }

                    if (clip.Height > 0)
                    {
                        if (signature && line.Length > 0)
                        {
                            this.DrawSignatureLine(g, line, clip, this.pad, y);
                        }
                        else
                        {
                            using (SolidBrush fg = new SolidBrush(Theme.Foreground))
                            {
                                DualFontPainter.Draw(g, line, this.halfFont, this.fullFont, clip, this.pad, fg, this.typographic);
                            }
                        }
                    }
                }

                y += this.lineHeight;
                i++;
            }
        }

        /// <summary>ホイールで本文を動かす。</summary>
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (this.vScroll.Visible)
            {
                this.vScroll.Value = this.vScroll.Value - (DpiUtil.WheelNotches(e.Delta, ref this.wheelLeftover) * this.vScroll.SmallChange);
            }

            HandledMouseEventArgs he = e as HandledMouseEventArgs;
            if (he != null)
            {
                he.Handled = true;
            }
        }

        /// <summary>所有 Font を破棄する。</summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this.typographic != null)
                {
                    this.typographic.Dispose();
                    this.typographic = null;
                }

                this.DisposeFonts();
            }

            base.Dispose(disposing);
        }

        private int wheelLeftover;

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private void ShowInactive()
        {
            if (!this.IsHandleCreated)
            {
                this.CreateHandle();
            }

            ShowWindow(this.Handle, SW_SHOWNOACTIVATE);
            this.Visible = true;
        }

        private void OnScrollChanged(object sender, EventArgs e)
        {
            if (this.ignoreScroll)
            {
                return;
            }

            this.Invalidate();
        }

        private void RecreateFonts(FontLoadResult fonts)
        {
            if (fonts == null)
            {
                return;
            }

            int dpi = DpiUtil.GetDpi(this.IsHandleCreated ? this.Handle : IntPtr.Zero);
            int px = DpiUtil.ToPixels(DpiUtil.UiFontDip, dpi);
            Font newHalf = fonts.CreateHalfWidth(px);
            Font newFull = fonts.CreateFullWidth(px);
            this.DisposeFonts();
            this.halfFont = newHalf;
            this.fullFont = newFull;
            this.lineHeight = newHalf.Height;
            if (newFull.Height > this.lineHeight)
            {
                this.lineHeight = newFull.Height;
            }

            this.pad = DpiUtil.ToPixels(PadDip, dpi);
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

        private void MeasureWrapAndSize(string text)
        {
            int dpi = DpiUtil.GetDpi(this.IsHandleCreated ? this.Handle : IntPtr.Zero);
            int maxW = DpiUtil.ToPixels(MaxWidthDip, dpi);
            int maxH = DpiUtil.ToPixels(MaxHeightDip, dpi);
            this.pad = DpiUtil.ToPixels(PadDip, dpi);
            int bar = DpiUtil.ToPixels(DpiUtil.ScrollBarThicknessDip, dpi);
            this.ignoreScroll = true;
            try
            {
                this.vScroll.Value = 0;
            }
            finally
            {
                this.ignoreScroll = false;
            }

            float wrapW = maxW - this.pad * 2;
            this.lines = this.WrapText(text, wrapW);
            this.contentHeight = this.pad * 2 + this.lineHeight * this.lines.Length;
            bool needBar = this.contentHeight > maxH;
            if (needBar)
            {
                wrapW = maxW - this.pad * 2 - bar;
                this.lines = this.WrapText(text, wrapW);
                this.contentHeight = this.pad * 2 + this.lineHeight * this.lines.Length;
            }

            float contentW = 1f;
            if (this.halfFont != null && this.fullFont != null)
            {
                using (Graphics g = this.CreateGraphics())
                {
                    int i = 0;
                    while (i < this.lines.Length)
                    {
                        float w = DualFontPainter.Measure(g, this.lines[i], this.halfFont, this.fullFont, this.typographic);
                        if (w > contentW)
                        {
                            contentW = w;
                        }

                        i++;
                    }
                }
            }

            int width = this.pad * 2 + (int)Math.Ceiling(contentW) + (needBar ? bar : 0);
            if (width < this.pad * 2 + 8)
            {
                width = this.pad * 2 + 8;
            }

            if (width > maxW)
            {
                width = maxW;
            }

            int height = this.contentHeight;
            if (height > maxH)
            {
                height = maxH;
            }

            if (height < this.pad * 2 + this.lineHeight)
            {
                height = this.pad * 2 + this.lineHeight;
            }

            this.Size = new Size(width, height);
            this.LayoutScroll(needBar, bar, height);
        }

        private string[] WrapText(string text, float wrapW)
        {
            if (this.halfFont == null || this.fullFont == null)
            {
                return text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            }

            using (Graphics g = this.CreateGraphics())
            {
                return DualFontPainter.Wrap(g, text, this.halfFont, this.fullFont, wrapW, this.typographic);
            }
        }

        private void LayoutScroll(bool needBar, int bar, int height)
        {
            if (!needBar)
            {
                this.vScroll.Visible = false;
                return;
            }

            this.vScroll.Visible = true;
            this.vScroll.Bounds = new Rectangle(this.Width - bar - 1, 1, bar, height - 2);
            this.vScroll.Minimum = 0;
            int innerH = height - this.pad * 2;
            int max = this.contentHeight - this.pad * 2;
            if (max < innerH)
            {
                max = innerH;
            }

            this.vScroll.Maximum = max;
            this.vScroll.LargeChange = innerH;
            this.vScroll.SmallChange = this.lineHeight;
        }

        private void DrawSignatureLine(Graphics g, string line, Rectangle clip, int originX, int y)
        {
            ILineLexer lexer = LexerRegistry.Get(this.language);
            List<Token> tokens = new List<Token>();
            int endState;
            lexer.ScanLine(line, 0, tokens, out endState);
            float x = originX;
            int t = 0;
            while (t < tokens.Count)
            {
                Token token = tokens[t];
                t++;
                if (token.Length <= 0 || token.Start < 0 || token.Start + token.Length > line.Length)
                {
                    continue;
                }

                string piece = line.Substring(token.Start, token.Length);
                float w = DualFontPainter.Measure(g, piece, this.halfFont, this.fullFont, this.typographic);
                using (SolidBrush brush = new SolidBrush(ColorFor(token.Kind)))
                {
                    DualFontPainter.Draw(g, piece, this.halfFont, this.fullFont, clip, x, brush, this.typographic);
                }

                x += w;
            }
        }

        private static Color ColorFor(TokenKind kind)
        {
            if (kind == TokenKind.Keyword)
            {
                return Theme.Keyword;
            }

            if (kind == TokenKind.Type)
            {
                return Theme.Type;
            }

            if (kind == TokenKind.Method)
            {
                return Theme.Method;
            }

            if (kind == TokenKind.Local)
            {
                return Theme.Local;
            }

            if (kind == TokenKind.Instance)
            {
                return Theme.Instance;
            }

            if (kind == TokenKind.Comment)
            {
                return Theme.Comment;
            }

            if (kind == TokenKind.String)
            {
                return Theme.StringLiteral;
            }

            if (kind == TokenKind.Number)
            {
                return Theme.Number;
            }

            return Theme.Foreground;
        }

        private void OnPopupMouseEnter(object sender, EventArgs e)
        {
            this.mouseInside = true;
        }

        private void OnPopupMouseLeave(object sender, EventArgs e)
        {
            this.mouseInside = false;
        }
    }
}
