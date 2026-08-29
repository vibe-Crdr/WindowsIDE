using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace WindowsIDE.Ui.Fonts
{
    /// <summary>
    /// 半角・全角フォントをグリフ切替して測る・描く。WinForms には依存しない。
    /// </summary>
    public static class DualFontPainter
    {
        /// <summary>
        /// 文字列幅を半角・全角ランごとに合計する。
        /// </summary>
        /// <param name="g">計測に使う Graphics。</param>
        /// <param name="text">対象。null や空は 0。</param>
        /// <param name="half">半角フォント。</param>
        /// <param name="full">全角フォント。</param>
        /// <param name="format">測り方。null ならタイポグラフィ既定。</param>
        /// <returns>幅（px）。</returns>
        public static float Measure(Graphics g, string text, Font half, Font full, StringFormat format)
        {
            if (g == null || half == null || full == null || text == null || text.Length == 0)
            {
                return 0f;
            }

            StringFormat fmt = format;
            bool own = false;
            if (fmt == null)
            {
                fmt = CreateFormat();
                own = true;
            }

            try
            {
                float width = 0f;
                int i = 0;
                while (i < text.Length)
                {
                    int runLen;
                    Font font = TakeRun(text, i, half, full, out runLen);
                    string run = text.Substring(i, runLen);
                    width += g.MeasureString(run, font, PointF.Empty, fmt).Width;
                    i += runLen;
                }

                return width;
            }
            finally
            {
                if (own)
                {
                    fmt.Dispose();
                }
            }
        }

        /// <summary>
        /// maxWidth に収まるよう末尾を半角 "..." で切る。空や null は ""。
        /// </summary>
        /// <param name="g">計測に使う Graphics。</param>
        /// <param name="text">元の文字列。</param>
        /// <param name="half">半角フォント。</param>
        /// <param name="full">全角フォント。</param>
        /// <param name="maxWidth">最大幅。</param>
        /// <param name="format">測り方。null ならタイポグラフィ既定。</param>
        /// <returns>収まる文字列。収まらなければ "..."。</returns>
        public static string FitEllipsis(Graphics g, string text, Font half, Font full, float maxWidth, StringFormat format)
        {
            if (text == null || text.Length == 0)
            {
                return "";
            }

            if (g == null || half == null || full == null)
            {
                return text;
            }

            if (Measure(g, text, half, full, format) <= maxWidth)
            {
                return text;
            }

            int len = text.Length;
            while (len > 0)
            {
                string candidate = text.Substring(0, len) + "...";
                if (Measure(g, candidate, half, full, format) <= maxWidth)
                {
                    return candidate;
                }

                len--;
            }

            return "...";
        }

        /// <summary>
        /// maxWidth に収まるよう空白優先で折り返す。元の改行は残す。省略記号は付けない。
        /// </summary>
        /// <param name="g">計測に使う Graphics。</param>
        /// <param name="text">対象。null は空。</param>
        /// <param name="half">半角フォント。</param>
        /// <param name="full">全角フォント。</param>
        /// <param name="maxWidth">最大幅。</param>
        /// <param name="format">測り方。null ならタイポグラフィ既定。</param>
        /// <returns>折り返し後の行。</returns>
        public static string[] Wrap(Graphics g, string text, Font half, Font full, float maxWidth, StringFormat format)
        {
            if (text == null || text.Length == 0)
            {
                return new string[0];
            }

            string[] paras = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            List<string> result = new List<string>();
            for (int p = 0; p < paras.Length; p++)
            {
                WrapParagraph(g, paras[p], half, full, maxWidth, format, result);
            }

            return result.ToArray();
        }

        private static void WrapParagraph(Graphics g, string line, Font half, Font full, float maxWidth, StringFormat format, List<string> result)
        {
            if (line == null)
            {
                result.Add("");
                return;
            }

            if (line.Length == 0 || g == null || half == null || full == null || maxWidth <= 1f)
            {
                result.Add(line);
                return;
            }

            if (Measure(g, line, half, full, format) <= maxWidth)
            {
                result.Add(line);
                return;
            }

            int start = 0;
            while (start < line.Length)
            {
                int lo = 1;
                int hi = line.Length - start;
                int fit = 1;
                while (lo <= hi)
                {
                    int mid = lo + ((hi - lo) / 2);
                    string slice = line.Substring(start, mid);
                    if (Measure(g, slice, half, full, format) <= maxWidth)
                    {
                        fit = mid;
                        lo = mid + 1;
                    }
                    else
                    {
                        hi = mid - 1;
                    }
                }

                int breakAt = fit;
                if (start + fit < line.Length)
                {
                    int lastSpace = -1;
                    int i = 0;
                    while (i < fit)
                    {
                        char c = line[start + i];
                        if (c == ' ' || c == '\t')
                        {
                            lastSpace = i;
                        }

                        i++;
                    }

                    if (lastSpace >= 1)
                    {
                        breakAt = lastSpace + 1;
                    }
                }

                string part = line.Substring(start, breakAt).TrimEnd(' ', '\t');
                result.Add(part);
                start += breakAt;
                while (start < line.Length && (line[start] == ' ' || line[start] == '\t'))
                {
                    start++;
                }
            }
        }

        /// <summary>
        /// clip にクリップし、originX から双フォントで描く。溢れは切る（省略記号は付けない）。
        /// </summary>
        /// <param name="g">描画先。</param>
        /// <param name="text">対象。null や空は何もしない。</param>
        /// <param name="half">半角フォント。</param>
        /// <param name="full">全角フォント。</param>
        /// <param name="clip">クリップ矩形。</param>
        /// <param name="originX">描画開始 X。clip より左でもよい（横スクロール）。</param>
        /// <param name="brush">文字色。</param>
        /// <param name="format">測り方。null ならタイポグラフィ既定。</param>
        public static void Draw(Graphics g, string text, Font half, Font full, Rectangle clip, float originX, Brush brush, StringFormat format)
        {
            if (g == null || half == null || full == null || brush == null || text == null || text.Length == 0)
            {
                return;
            }

            if (clip.Width <= 0 || clip.Height <= 0)
            {
                return;
            }

            StringFormat fmt = format;
            bool own = false;
            if (fmt == null)
            {
                fmt = CreateFormat();
                own = true;
            }

            GraphicsState state = g.Save();
            try
            {
                g.SetClip(clip);
                float cell = half.Height;
                if (full.Height > cell)
                {
                    cell = full.Height;
                }

                float x = originX;
                float y = clip.Y + (clip.Height - cell) / 2f;
                int i = 0;
                while (i < text.Length)
                {
                    int runLen;
                    Font font = TakeRun(text, i, half, full, out runLen);
                    string run = text.Substring(i, runLen);
                    float w = g.MeasureString(run, font, PointF.Empty, fmt).Width;
                    if (x + w >= clip.Left && x <= clip.Right)
                    {
                        g.DrawString(run, font, brush, x, y + BaselineOffset(font, half, full), fmt);
                    }

                    x += w;
                    i += runLen;
                    if (x > clip.Right)
                    {
                        break;
                    }
                }
            }
            finally
            {
                g.Restore(state);
                if (own)
                {
                    fmt.Dispose();
                }
            }
        }

        /// <summary>
        /// bounds にクリップし、溢れは FitEllipsis して双フォントで描く。
        /// </summary>
        /// <param name="g">描画先。</param>
        /// <param name="text">対象。</param>
        /// <param name="half">半角フォント。</param>
        /// <param name="full">全角フォント。</param>
        /// <param name="bounds">クリップと配置。</param>
        /// <param name="brush">文字色。</param>
        /// <param name="format">測り方。null ならタイポグラフィ既定。</param>
        public static void DrawEllipsis(Graphics g, string text, Font half, Font full, Rectangle bounds, Brush brush, StringFormat format)
        {
            if (g == null || half == null || full == null || brush == null || bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            string fitted = FitEllipsis(g, text, half, full, bounds.Width, format);
            Draw(g, fitted, half, full, bounds, bounds.X, brush, format);
        }

        /// <summary>
        /// 半角・全角の最大 ascent に合わせるベースライン補正（TextView と同じ式）。
        /// </summary>
        /// <param name="font">これから描くフォント。</param>
        /// <param name="half">半角フォント。</param>
        /// <param name="full">全角フォント。</param>
        /// <returns>DrawString の Y に足すオフセット。</returns>
        public static float BaselineOffset(Font font, Font half, Font full)
        {
            if (font == null || half == null || full == null)
            {
                return 0f;
            }

            float fontAscent = Ascent(font);
            float halfAscent = Ascent(half);
            float fullAscent = Ascent(full);
            float max = halfAscent;
            if (fullAscent > max)
            {
                max = fullAscent;
            }

            return max - fontAscent + 1f;
        }

        private static float Ascent(Font font)
        {
            return font.Size * font.FontFamily.GetCellAscent(font.Style) / font.FontFamily.GetEmHeight(font.Style);
        }

        private static Font TakeRun(string text, int index, Font half, Font full, out int runLen)
        {
            int count;
            bool useHalf = GlyphClassifier.UseHalfWidthFont(text, index, out count);
            int i = index + count;
            while (i < text.Length)
            {
                int n;
                bool nextHalf = GlyphClassifier.UseHalfWidthFont(text, i, out n);
                if (nextHalf != useHalf)
                {
                    break;
                }

                i += n;
            }

            runLen = i - index;
            return useHalf ? half : full;
        }

        private static StringFormat CreateFormat()
        {
            StringFormat fmt = (StringFormat)StringFormat.GenericTypographic.Clone();
            fmt.FormatFlags = fmt.FormatFlags | StringFormatFlags.MeasureTrailingSpaces | StringFormatFlags.NoWrap | StringFormatFlags.FitBlackBox;
            return fmt;
        }
    }
}
