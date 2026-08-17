using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using WindowsIDE.Editor;

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
        private const TextFormatFlags LabelTextFlags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.PreserveGraphicsClipping;

        private readonly List<Document> tabs;
        private int selectedIndex;
        private readonly List<Rectangle> tabBounds;
        private readonly List<Rectangle> closeBounds;
        private Font uiFont;
        private int lastDpi;

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
            this.RecreateUiFont();
            this.BackColor = Theme.Background;
            this.ForeColor = Theme.Foreground;
        }

        /// <summary>ハンドル作成後に GetDpi で UI フォントと高さを合わせる。</summary>
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            this.RecreateUiFont();
        }

        /// <summary>フォント変更後に所有 UI フォント基準で高さを合わせる。</summary>
        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            this.ApplyBarHeight();
        }

        /// <summary>親の DPI 変更後に 12 DIP Pixel フォントを作り直す。</summary>
        protected override void OnDpiChangedAfterParent(EventArgs e)
        {
            base.OnDpiChangedAfterParent(e);
            this.RecreateUiFont();
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
            if (dpi != this.lastDpi || this.uiFont == null)
            {
                this.lastDpi = dpi;
                this.RecreateUiFont();
            }

            Graphics g = e.Graphics;
            g.Clear(Theme.Background);
            using (Pen border = new Pen(Theme.Border))
            {
                g.DrawLine(border, 0, this.Height - 1, this.Width, this.Height - 1);
            }

            this.tabBounds.Clear();
            this.closeBounds.Clear();
            Font font = (this.uiFont != null) ? this.uiFont : this.Font;
            int pad = DpiUtil.ToPixels(8, dpi);
            int closeSize = DpiUtil.ToPixels(12, dpi);
            int closeInset = DpiUtil.ToPixels(2, dpi);
            int closeSlot = closeSize + closeInset;
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

                Size sz = TextRenderer.MeasureText(g, title, font, new Size(int.MaxValue, int.MaxValue), LabelTextFlags);
                int w = sz.Width + pad + closeSlot;
                if (w < minW)
                {
                    w = minW;
                }

                Rectangle tab = new Rectangle(x, 0, w, this.Height - 1);
                this.tabBounds.Add(tab);
                int closeX = tab.Right - closeInset - closeSize;
                int closeY = tab.Top + closeInset;
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
                TextRenderer.DrawText(g, title, font, titleRect, Theme.Foreground, LabelTextFlags);
                using (Pen xp = new Pen(Theme.Comment))
                {
                    g.DrawLine(xp, close.Left, close.Top, close.Right - 1, close.Bottom - 1);
                    g.DrawLine(xp, close.Right - 1, close.Top, close.Left, close.Bottom - 1);
                }

                x += w + 2;
            }
        }

        private void RecreateUiFont()
        {
            int dpi = DpiUtil.GetDpi(this.IsHandleCreated ? this.Handle : IntPtr.Zero);
            this.lastDpi = dpi;
            if (this.uiFont != null)
            {
                this.uiFont.Dispose();
                this.uiFont = null;
            }

            int px = DpiUtil.ToPixels(DpiUtil.UiFontDip, dpi);
            FontFamily family = SystemFonts.MessageBoxFont.FontFamily;
            this.uiFont = new Font(family, px, FontStyle.Regular, GraphicsUnit.Pixel);
            this.ApplyBarHeight();
        }

        private void ApplyBarHeight()
        {
            int dpi = DpiUtil.GetDpi(this.IsHandleCreated ? this.Handle : IntPtr.Zero);
            int fontHeight = (this.uiFont != null) ? this.uiFont.Height : this.Font.Height;
            this.Height = DpiUtil.TabStripHeight(fontHeight, dpi);
        }

        /// <summary>所有している 12 DIP Pixel フォントを破棄する。</summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing && this.uiFont != null)
            {
                this.uiFont.Dispose();
                this.uiFont = null;
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
