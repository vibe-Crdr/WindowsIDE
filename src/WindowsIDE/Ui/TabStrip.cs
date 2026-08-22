using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using WindowsIDE.Editor;
using WindowsIDE.Ui.Fonts;

namespace WindowsIDE.Ui
{
    /// <summary>
    /// タブ閉じる要求。
    /// </summary>
    public sealed class TabCloseEventArgs : EventArgs
    {
        private int index;

        /// <summary>
        /// 閉じるタブのインデックスを渡す。
        /// </summary>
        public TabCloseEventArgs(int index)
        {
            this.index = index;
        }

        /// <summary>閉じる対象。</summary>
        public int Index { get { return this.index; } }
    }

    /// <summary>
    /// 自前タブバー。WinForms の TabControl は使わない。
    /// </summary>
    public sealed class TabStrip : Control
    {
        private readonly List<Document> tabs;
        private int selectedIndex;
        private readonly List<Rectangle> tabBounds;
        private readonly List<Rectangle> closeBounds;
        private Font borrowedHalf;
        private Font borrowedFull;
        private Font fallbackHalf;
        private Font fallbackFull;

        /// <summary>
        /// 空のタブバーを作る。
        /// </summary>
        public TabStrip()
        {
            this.tabs = new List<Document>();
            this.tabBounds = new List<Rectangle>();
            this.closeBounds = new List<Rectangle>();
            this.selectedIndex = -1;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            this.SetStyle(ControlStyles.Selectable, false);
            this.TabStop = false;
            this.EnsureFallbackFonts();
            this.ApplyBarHeight();
            this.BackColor = Theme.Background;
            this.ForeColor = Theme.Foreground;
        }

        /// <summary>ハンドル作成後に GetDpi で高さを合わせる。</summary>
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            this.ApplyBarHeight();
        }

        /// <summary>フォント変更後に描画フォント基準で高さを合わせる。</summary>
        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            this.ApplyBarHeight();
        }

        /// <summary>親の DPI 変更後にバー高さを合わせる。フォント再生成は MainForm.RecreateChromeFonts。</summary>
        protected override void OnDpiChangedAfterParent(EventArgs e)
        {
            base.OnDpiChangedAfterParent(e);
            this.ApplyBarHeight();
        }

        /// <summary>選択変更。</summary>
        public event EventHandler SelectedIndexChanged;

        /// <summary>選択中タブやバー空白のクリック後、編集器へフォーカスを戻す。</summary>
        public event EventHandler FocusEditorRequested;

        /// <summary>閉じるボタン。</summary>
        public event EventHandler<TabCloseEventArgs> TabCloseRequested;

        /// <summary>開いている文書。</summary>
        public IList<Document> Tabs { get { return this.tabs; } }

        /// <summary>選択中のインデックス。無ければ -1。</summary>
        public int SelectedIndex
        {
            get { return this.selectedIndex; }
            set
            {
                if (value < -1 || value >= this.tabs.Count)
                {
                    return;
                }

                if (this.selectedIndex != value)
                {
                    this.selectedIndex = value;
                    this.Invalidate();
                    EventHandler h = this.SelectedIndexChanged;
                    if (h != null)
                    {
                        h(this, EventArgs.Empty);
                    }
                }
            }
        }

        /// <summary>選択中の文書。無ければ null。</summary>
        public Document SelectedDocument
        {
            get
            {
                if (this.selectedIndex < 0 || this.selectedIndex >= this.tabs.Count)
                {
                    return null;
                }

                return this.tabs[this.selectedIndex];
            }
        }

        /// <summary>
        /// タブ題名用の 12 DIP 双フォントを使う。所有権は移さない。本文 fontSize には連動しない。
        /// </summary>
        /// <param name="half">半角。</param>
        /// <param name="full">全角。</param>
        public void SetFonts(Font half, Font full)
        {
            this.DisposeFallbackFonts();
            this.borrowedHalf = half;
            this.borrowedFull = full;
            this.ApplyBarHeight();
            this.Invalidate();
        }

        /// <summary>
        /// タブを追加して選択する。
        /// </summary>
        public void AddTab(Document document)
        {
            if (document == null)
            {
                return;
            }

            this.tabs.Add(document);
            this.SelectedIndex = this.tabs.Count - 1;
            this.Invalidate();
        }

        /// <summary>
        /// 指定タブを除く。
        /// </summary>
        public void RemoveAt(int index)
        {
            if (index < 0 || index >= this.tabs.Count)
            {
                return;
            }

            this.tabs.RemoveAt(index);
            if (this.tabs.Count == 0)
            {
                this.selectedIndex = -1;
            }
            else if (this.selectedIndex >= this.tabs.Count)
            {
                this.selectedIndex = this.tabs.Count - 1;
            }
            else if (this.selectedIndex > index)
            {
                this.selectedIndex--;
            }

            this.Invalidate();
            EventHandler h = this.SelectedIndexChanged;
            if (h != null)
            {
                h(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// 文書のタブを探して選択する。無ければ追加する。
        /// </summary>
        public void ShowDocument(Document document)
        {
            for (int i = 0; i < this.tabs.Count; i++)
            {
                if (object.ReferenceEquals(this.tabs[i], document))
                {
                    this.SelectedIndex = i;
                    return;
                }

                if (document.FilePath != null && this.tabs[i].FilePath != null
                    && string.Equals(this.tabs[i].FilePath, document.FilePath, StringComparison.OrdinalIgnoreCase))
                {
                    this.SelectedIndex = i;
                    return;
                }
            }

            this.AddTab(document);
        }

        /// <summary>
        /// 次のタブへ。
        /// </summary>
        public void SelectNext()
        {
            if (this.tabs.Count == 0)
            {
                return;
            }

            int next = this.selectedIndex + 1;
            if (next >= this.tabs.Count)
            {
                next = 0;
            }

            this.SelectedIndex = next;
        }

        /// <summary>
        /// 前のタブへ。
        /// </summary>
        public void SelectPrevious()
        {
            if (this.tabs.Count == 0)
            {
                return;
            }

            int prev = this.selectedIndex - 1;
            if (prev < 0)
            {
                prev = this.tabs.Count - 1;
            }

            this.SelectedIndex = prev;
        }

        /// <summary>
        /// 未保存印などの再描画。
        /// </summary>
        public void RefreshTabs()
        {
            this.Invalidate();
        }

        /// <summary>
        /// タブを描画する。
        /// </summary>
        protected override void OnPaint(PaintEventArgs e)
        {
            int dpi = DpiUtil.GetDpi(this.IsHandleCreated ? this.Handle : IntPtr.Zero);
            Font half;
            Font full;
            this.GetPaintFonts(out half, out full);

            Graphics g = e.Graphics;
            g.Clear(Theme.Background);
            using (Pen border = new Pen(Theme.Border))
            {
                g.DrawLine(border, 0, this.Height - 1, this.Width, this.Height - 1);
            }

            this.tabBounds.Clear();
            this.closeBounds.Clear();
            int pad = DpiUtil.ToPixels(8, dpi);
            int closeGap = DpiUtil.ToPixels(6, dpi);
            // 閉じる印の一辺は 8 DIP。線幅は 2。余白は題名側と右端が各 6 DIP。
            int closeSize = DpiUtil.ToPixels(8, dpi);
            int closeSlot = closeGap + closeSize + closeGap;
            int minW = DpiUtil.ToPixels(72, dpi);
            int x = 4;
            for (int i = 0; i < this.tabs.Count; i++)
            {
                Document doc = this.tabs[i];
                string title = doc.DisplayName;
                if (doc.IsDirty)
                {
                    title = title + " *";
                }

                int textW = (int)Math.Ceiling(DualFontPainter.Measure(g, title, half, full, null));
                int w = textW + pad + closeSlot;
                if (w < minW)
                {
                    w = minW;
                }

                Rectangle tab = new Rectangle(x, 0, w, this.Height - 1);
                this.tabBounds.Add(tab);
                int closeX = tab.Right - closeGap - closeSize;
                int closeY = tab.Top + ((tab.Height - closeSize) / 2);
                if (closeX < tab.Left)
                {
                    closeX = tab.Left;
                }

                if (closeY < 0)
                {
                    closeY = 0;
                }

                if (closeY + closeSize > tab.Bottom)
                {
                    closeY = tab.Bottom - closeSize;
                    if (closeY < 0)
                    {
                        closeY = 0;
                    }
                }

                Rectangle close = new Rectangle(closeX, closeY, closeSize, closeSize);
                this.closeBounds.Add(close);

                Color back = (i == this.selectedIndex) ? Theme.EditorBackground : Theme.Background;
                using (SolidBrush b = new SolidBrush(back))
                {
                    g.FillRectangle(b, tab);
                }

                using (Pen p = new Pen(Theme.Border))
                {
                    g.DrawRectangle(p, tab);
                }

                Rectangle titleRect = new Rectangle(tab.X + pad, tab.Y, Math.Max(0, tab.Width - pad - closeSlot), tab.Height);
                using (SolidBrush fg = new SolidBrush(Theme.Foreground))
                {
                    DualFontPainter.DrawEllipsis(g, title, half, full, titleRect, fg, null);
                }

                using (Pen xp = new Pen(Theme.Foreground, 2f))
                {
                    g.DrawLine(xp, close.Left, close.Top, close.Right - 1, close.Bottom - 1);
                    g.DrawLine(xp, close.Right - 1, close.Top, close.Left, close.Bottom - 1);
                }

                x += w + 2;
            }
        }

        /// <summary>
        /// 借用が揃っていればそれを使う。どちらか欠けていれば所有の 12 DIP MessageBoxFont を半角・全角に使う。
        /// </summary>
        private void GetPaintFonts(out Font half, out Font full)
        {
            if (this.borrowedHalf != null && this.borrowedFull != null)
            {
                half = this.borrowedHalf;
                full = this.borrowedFull;
                return;
            }

            this.EnsureFallbackFonts();
            half = this.fallbackHalf;
            full = this.fallbackFull;
        }

        /// <summary>
        /// FileTree の fonts==null と同じ 12 DIP MessageBox 退避。半角・全角は別インスタンス。
        /// </summary>
        private void EnsureFallbackFonts()
        {
            if (this.fallbackHalf != null && this.fallbackFull != null)
            {
                return;
            }

            this.DisposeFallbackFonts();
            int dpi = DpiUtil.GetDpi(this.IsHandleCreated ? this.Handle : IntPtr.Zero);
            int px = DpiUtil.ToPixels(DpiUtil.UiFontDip, dpi);
            FontFamily family = SystemFonts.MessageBoxFont.FontFamily;
            this.fallbackHalf = new Font(family, px, FontStyle.Regular, GraphicsUnit.Pixel);
            this.fallbackFull = new Font(family, px, FontStyle.Regular, GraphicsUnit.Pixel);
        }

        private void DisposeFallbackFonts()
        {
            if (this.fallbackHalf != null)
            {
                this.fallbackHalf.Dispose();
                if (object.ReferenceEquals(this.fallbackFull, this.fallbackHalf))
                {
                    this.fallbackFull = null;
                }

                this.fallbackHalf = null;
            }

            if (this.fallbackFull != null)
            {
                this.fallbackFull.Dispose();
                this.fallbackFull = null;
            }
        }

        private void ApplyBarHeight()
        {
            int dpi = DpiUtil.GetDpi(this.IsHandleCreated ? this.Handle : IntPtr.Zero);
            Font half = null;
            Font full = null;
            if (this.borrowedHalf != null && this.borrowedFull != null)
            {
                half = this.borrowedHalf;
                full = this.borrowedFull;
            }
            else
            {
                half = this.fallbackHalf;
                full = this.fallbackFull;
            }

            int fontHeight;
            if (half != null && full != null)
            {
                fontHeight = half.Height;
                if (full.Height > fontHeight)
                {
                    fontHeight = full.Height;
                }
            }
            else if (half != null)
            {
                fontHeight = half.Height;
            }
            else if (full != null)
            {
                fontHeight = full.Height;
            }
            else
            {
                fontHeight = this.Font.Height;
            }

            this.Height = DpiUtil.TabStripHeight(fontHeight, dpi);
        }

        /// <summary>借用フォントは破棄しない。所有している退避フォントだけ破棄する。</summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.DisposeFallbackFonts();
                this.borrowedHalf = null;
                this.borrowedFull = null;
            }

            base.Dispose(disposing);
        }

        /// <summary>
        /// クリックで選択または閉じる。
        /// </summary>
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            try
            {
                for (int i = 0; i < this.closeBounds.Count; i++)
                {
                    if (this.closeBounds[i].Contains(e.Location))
                    {
                        EventHandler<TabCloseEventArgs> close = this.TabCloseRequested;
                        if (close != null)
                        {
                            close(this, new TabCloseEventArgs(i));
                        }

                        return;
                    }
                }

                for (int i = 0; i < this.tabBounds.Count; i++)
                {
                    if (this.tabBounds[i].Contains(e.Location))
                    {
                        this.SelectedIndex = i;
                        return;
                    }
                }
            }
            finally
            {
                EventHandler focus = this.FocusEditorRequested;
                if (focus != null)
                {
                    focus(this, EventArgs.Empty);
                }
            }
        }
    }
}
