using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using WindowsIDE.Ui;
using WindowsIDE.Ui.Fonts;

namespace WindowsIDE.Workspace
{
    /// <summary>
    /// クリックでファイルを開くツリー。リネーム・削除メニューは持たない。子は展開時に読む。
    /// </summary>
    public sealed class FileTreeControl : TreeView
    {
        private const string LazyTag = "lazy";
        private const int WM_HSCROLL = 0x0114;
        private const int WM_VSCROLL = 0x0115;
        private const int WM_MOUSEWHEEL = 0x020A;
        private const int WM_MOUSEHWHEEL = 0x020E;
        private const int SB_THUMBPOSITION = 4;

        private string rootPath;
        private readonly Dictionary<TreeNode, Rectangle> expandMarks;
        private FontLoadResult fonts;
        private Font halfFont;
        private Font fullFont;
        private StringFormat typographic;
        private int lastDpi;
        private ThemedScrollBar vScroll;
        private ThemedScrollBar hScroll;
        private bool ignoreThemedScroll;
        private bool chromeBusy;
        private bool rebuildBusy;
        private int wheelLeftover;

        /// <summary>
        /// オーナー描画のツリーを作る。
        /// </summary>
        public FileTreeControl()
        {
            this.expandMarks = new Dictionary<TreeNode, Rectangle>();
            this.DrawMode = TreeViewDrawMode.OwnerDrawAll;
            this.HideSelection = false;
            this.ShowLines = false;
            this.ShowPlusMinus = false;
            this.FullRowSelect = true;
            this.BorderStyle = BorderStyle.None;
            this.BackColor = Theme.Background;
            this.ForeColor = Theme.Foreground;
            this.HotTracking = false;
            this.typographic = (StringFormat)StringFormat.GenericTypographic.Clone();
            this.typographic.FormatFlags = this.typographic.FormatFlags | StringFormatFlags.MeasureTrailingSpaces | StringFormatFlags.NoWrap | StringFormatFlags.FitBlackBox;

            this.vScroll = new ThemedScrollBar(true);
            this.hScroll = new ThemedScrollBar(false);
            this.vScroll.TrackColor = Theme.Background;
            this.hScroll.TrackColor = Theme.Background;
            this.vScroll.SmallChange = 1;
            this.hScroll.SmallChange = 8;
            this.vScroll.ValueChanged += this.OnThemedScrollChanged;
            this.hScroll.ValueChanged += this.OnThemedScrollChanged;
            this.Controls.Add(this.vScroll);
            this.Controls.Add(this.hScroll);

            this.RecreateUiFont();
            this.BeforeExpand += this.OnBeforeExpand;
            this.AfterExpand += this.OnAfterExpandCollapse;
            this.AfterCollapse += this.OnAfterExpandCollapse;
        }

        /// <summary>
        /// ツリー用の 12 DIP 双フォントを適用する。本文 fontSize には連動しない。
        /// </summary>
        /// <param name="fonts">同梱フォント。null ならシステム UI 単一。</param>
        public void ApplyFonts(FontLoadResult fonts)
        {
            this.fonts = fonts;
            this.RecreateUiFont();
        }

        /// <summary>ハンドル作成後に GetDpi で UI フォントと行高を合わせる。</summary>
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.SendMessage(this.Handle, Native.TVM_SETEXTENDEDSTYLE, new IntPtr(Native.TVS_EX_DOUBLEBUFFER), new IntPtr(Native.TVS_EX_DOUBLEBUFFER));
            this.RecreateUiFont();
            this.RefreshChrome();
        }

        /// <summary>フォント変更後に所有 UI フォント基準で行高を合わせる。</summary>
        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            this.ApplyItemHeight();
        }

        /// <summary>親の DPI 変更後に 12 DIP Pixel フォントを作り直す。</summary>
        protected override void OnDpiChangedAfterParent(EventArgs e)
        {
            base.OnDpiChangedAfterParent(e);
            this.RecreateUiFont();
            this.RefreshChrome();
        }

        /// <summary>ネイティブバーを隠し、自前バーを右下へ置く。</summary>
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            this.RefreshChrome();
        }

        /// <summary>ファイルを開く要求。SelectedPath が対象。</summary>
        public event EventHandler FileOpenRequested;

        /// <summary>クリックされたファイルのフルパス。フォルダなら null。</summary>
        public string SelectedFilePath { get; private set; }

        /// <summary>
        /// ワークスペースを結び付けて根から読む。
        /// </summary>
        public void BindWorkspace(string path)
        {
            this.rootPath = path;
            this.Rebuild();
        }

        /// <summary>
        /// フォーカス復帰用に再構築する。展開・選択・スクロールは復元する。
        /// </summary>
        public void Rebuild()
        {
            this.rebuildBusy = true;
            try
            {
                this.RebuildCore();
            }
            finally
            {
                this.rebuildBusy = false;
            }
        }

        private void RebuildCore()
        {
            bool sameRoot = false;
            List<string> expanded = new List<string>();
            string selectedPath = null;
            int vPos = 0;
            int hPos = 0;
            if (this.Nodes.Count > 0)
            {
                string previousRoot = this.Nodes[0].Tag as string;
                sameRoot = previousRoot != null
                    && !string.IsNullOrEmpty(this.rootPath)
                    && string.Equals(previousRoot, this.rootPath, StringComparison.OrdinalIgnoreCase);
                if (sameRoot)
                {
                    this.CollectExpanded(this.Nodes, expanded);
                    if (this.SelectedNode != null)
                    {
                        selectedPath = this.SelectedNode.Tag as string;
                    }

                    if (this.vScroll != null)
                    {
                        vPos = this.vScroll.Value;
                    }

                    if (this.hScroll != null)
                    {
                        hPos = this.hScroll.Value;
                    }
                }
            }

            this.BeginUpdate();
            try
            {
                this.expandMarks.Clear();
                this.Nodes.Clear();
                this.SelectedFilePath = null;
                if (string.IsNullOrEmpty(this.rootPath) || !Directory.Exists(this.rootPath))
                {
                    return;
                }

                TreeNode root = new TreeNode(Path.GetFileName(this.rootPath));
                if (root.Text.Length == 0)
                {
                    root.Text = this.rootPath;
                }

                root.Tag = this.rootPath;
                this.Nodes.Add(root);
                this.LoadChildren(root, this.rootPath);
                if (!sameRoot)
                {
                    root.Expand();
                }
                else
                {
                    expanded.Sort(CompareExpandPath);
                    for (int i = 0; i < expanded.Count; i++)
                    {
                        TreeNode node = this.FindNodeByPath(this.Nodes, expanded[i]);
                        if (node != null)
                        {
                            node.Expand();
                        }
                    }

                    if (!string.IsNullOrEmpty(selectedPath))
                    {
                        TreeNode selected = this.FindNodeByPath(this.Nodes, selectedPath);
                        if (selected != null)
                        {
                            this.SelectedNode = selected;
                        }
                    }
                }
            }
            finally
            {
                this.EndUpdate();
                this.RefreshChrome();
                if (sameRoot && this.IsHandleCreated)
                {
                    if (this.vScroll != null && this.vScroll.Visible)
                    {
                        this.vScroll.Value = vPos;
                    }

                    if (this.hScroll != null && this.hScroll.Visible)
                    {
                        this.hScroll.Value = hPos;
                    }
                }
            }
        }

        /// <summary>
        /// ノードをダーク描画する。縦バー領域は塗らない。
        /// </summary>
        protected override void OnDrawNode(DrawTreeNodeEventArgs e)
        {
            int dpi = DpiUtil.GetDpi(this.IsHandleCreated ? this.Handle : IntPtr.Zero);
            if (dpi != this.lastDpi || this.halfFont == null || this.fullFont == null)
            {
                this.lastDpi = dpi;
                this.RecreateUiFont();
            }

            Rectangle bounds = e.Bounds;
            int vBar = 0;
            int hBar = 0;
            if (this.vScroll != null && this.vScroll.Visible)
            {
                vBar = this.vScroll.Width;
            }

            if (this.hScroll != null && this.hScroll.Visible)
            {
                hBar = this.hScroll.Height;
            }

            bool selected = (e.State & TreeNodeStates.Selected) != 0;
            Color back = selected ? Theme.Selection : Theme.Background;
            int rowW = this.ClientSize.Width - vBar;
            if (rowW < 0)
            {
                rowW = 0;
            }

            int rowH = bounds.Height;
            if (hBar > 0 && bounds.Bottom > this.ClientSize.Height - hBar)
            {
                rowH = this.ClientSize.Height - hBar - bounds.Y;
                if (rowH < 0)
                {
                    rowH = 0;
                }
            }

            using (SolidBrush b = new SolidBrush(back))
            {
                e.Graphics.FillRectangle(b, 0, bounds.Y, rowW, rowH);
            }

            if (this.halfFont == null || this.fullFont == null || rowW <= 0 || rowH <= 0)
            {
                return;
            }

            int indent = DpiUtil.ToPixels(4, dpi);
            int step = DpiUtil.ToPixels(16, dpi);
            int markW = DpiUtil.ToPixels(14, dpi);
            int scrollX = 0;
            if (this.hScroll != null)
            {
                scrollX = this.hScroll.Value;
            }

            int x = indent + (e.Node.Level * step) - scrollX;
            bool folder = Directory.Exists(e.Node.Tag as string);
            if (folder)
            {
                string mark = e.Node.IsExpanded ? "-" : "+";
                Rectangle markRect = new Rectangle(x, bounds.Y, markW, rowH);
                this.expandMarks[e.Node] = markRect;
                using (SolidBrush comment = new SolidBrush(Theme.Comment))
                {
                    float markY = bounds.Y + (rowH - this.halfFont.Height) / 2f;
                    e.Graphics.DrawString(mark, this.halfFont, comment, markRect.X, markY + DualFontPainter.BaselineOffset(this.halfFont, this.halfFont, this.fullFont), this.typographic);
                }

                x += markW;
            }
            else
            {
                this.expandMarks.Remove(e.Node);
            }

            Rectangle clip = new Rectangle(0, bounds.Y, rowW, rowH);
            using (SolidBrush fg = new SolidBrush(Theme.Foreground))
            {
                DualFontPainter.Draw(e.Graphics, e.Node.Text, this.halfFont, this.fullFont, clip, x, fg, this.typographic);
            }
        }

        /// <summary>HWHEEL は横、縦ホイール後は自前バーを同期する。</summary>
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_MOUSEHWHEEL)
            {
                long wp = m.WParam.ToInt64();
                short delta = (short)((wp >> 16) & 0xFFFF);
                this.ApplyHorizontalDelta(delta);
                m.Result = (IntPtr)1;
                return;
            }

            base.WndProc(ref m);
            if (m.Msg == WM_VSCROLL || m.Msg == WM_HSCROLL || m.Msg == WM_MOUSEWHEEL)
            {
                this.RefreshChrome();
            }
        }

        /// <summary>Shift 付きホイールは横。それ以外はノッチで縦。base は呼ばない。</summary>
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if ((Control.ModifierKeys & Keys.Shift) != 0)
            {
                this.ApplyHorizontalDelta(e.Delta);
                HandledMouseEventArgs he = e as HandledMouseEventArgs;
                if (he != null)
                {
                    he.Handled = true;
                }

                return;
            }

            int notches = DpiUtil.WheelNotches(e.Delta, ref this.wheelLeftover);
            if (notches != 0 && this.vScroll != null)
            {
                this.vScroll.Value = this.vScroll.Value + (-notches * this.vScroll.SmallChange);
            }

            HandledMouseEventArgs handled = e as HandledMouseEventArgs;
            if (handled != null)
            {
                handled.Handled = true;
            }

            this.Invalidate();
        }

        /// <summary>
        /// 左クリックでファイルを開く。OwnerDraw のため GetNodeAt と行の Y で当てる。
        /// </summary>
        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (this.rebuildBusy)
            {
                return;
            }

            if (e.Button != MouseButtons.Left)
            {
                base.OnMouseDown(e);
                return;
            }

            base.OnMouseDown(e);
            TreeNode node = this.HitNode(e.X, e.Y);
            if (node == null)
            {
                this.SelectedFilePath = null;
                return;
            }

            string path = node.Tag as string;
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            {
                this.SelectedFilePath = null;
                Rectangle mark;
                if (this.expandMarks.TryGetValue(node, out mark) && mark.Contains(e.Location))
                {
                    if (node.IsExpanded)
                    {
                        node.Collapse();
                    }
                    else
                    {
                        node.Expand();
                    }
                }

                return;
            }

            if (string.IsNullOrEmpty(path) || !File.Exists(path) || !PathGuard.IsInsideWorkspace(this.rootPath, path))
            {
                this.SelectedFilePath = null;
                return;
            }

            this.SelectedNode = node;
            this.SelectedFilePath = path;
            EventHandler h = this.FileOpenRequested;
            if (h != null)
            {
                h(this, EventArgs.Empty);
            }
        }

        private TreeNode HitNode(int x, int y)
        {
            TreeNode node = this.GetNodeAt(x, y);
            if (node != null && !object.Equals(node.Tag, LazyTag))
            {
                return node;
            }

            return this.HitVisibleNodeByY(this.Nodes, y);
        }

        private TreeNode HitVisibleNodeByY(TreeNodeCollection nodes, int y)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                TreeNode node = nodes[i];
                if (object.Equals(node.Tag, LazyTag))
                {
                    continue;
                }

                Rectangle bounds = node.Bounds;
                if (y >= bounds.Y && y < bounds.Y + bounds.Height)
                {
                    return node;
                }

                if (node.IsExpanded)
                {
                    TreeNode child = this.HitVisibleNodeByY(node.Nodes, y);
                    if (child != null)
                    {
                        return child;
                    }
                }
            }

            return null;
        }

        private void OnBeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            this.EnsureChildren(e.Node);
        }

        private void OnAfterExpandCollapse(object sender, TreeViewEventArgs e)
        {
            this.RefreshChrome();
        }

        private void EnsureChildren(TreeNode node)
        {
            if (node.Nodes.Count == 1 && object.Equals(node.Nodes[0].Tag, LazyTag))
            {
                node.Nodes.Clear();
                string path = node.Tag as string;
                this.LoadChildren(node, path);
            }
        }

        private void LoadChildren(TreeNode parent, string path)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            {
                return;
            }

            try
            {
                string[] dirs = Directory.GetDirectories(path);
                Array.Sort(dirs, StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < dirs.Length; i++)
                {
                    string full = dirs[i];
                    if (!PathGuard.IsInsideWorkspace(this.rootPath, full))
                    {
                        continue;
                    }

                    DirectoryInfo info = new DirectoryInfo(full);
                    if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        string target = Path.GetFullPath(full);
                        if (!PathGuard.IsInsideWorkspace(this.rootPath, target))
                        {
                            continue;
                        }
                    }

                    TreeNode child = new TreeNode(info.Name);
                    child.Tag = full;
                    child.Nodes.Add(new TreeNode("...") { Tag = LazyTag });
                    parent.Nodes.Add(child);
                }

                string[] files = Directory.GetFiles(path);
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < files.Length; i++)
                {
                    string full = files[i];
                    if (!PathGuard.IsInsideWorkspace(this.rootPath, full))
                    {
                        continue;
                    }

                    TreeNode child = new TreeNode(Path.GetFileName(full));
                    child.Tag = full;
                    parent.Nodes.Add(child);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private void RecreateUiFont()
        {
            int dpi = DpiUtil.GetDpi(this.IsHandleCreated ? this.Handle : IntPtr.Zero);
            this.lastDpi = dpi;
            if (this.halfFont != null)
            {
                this.halfFont.Dispose();
                if (object.ReferenceEquals(this.fullFont, this.halfFont))
                {
                    this.fullFont = null;
                }

                this.halfFont = null;
            }

            if (this.fullFont != null)
            {
                this.fullFont.Dispose();
                this.fullFont = null;
            }

            int px = DpiUtil.ToPixels(DpiUtil.UiFontDip, dpi);
            if (this.fonts != null)
            {
                this.halfFont = this.fonts.CreateHalfWidth(px);
                this.fullFont = this.fonts.CreateFullWidth(px);
            }
            else
            {
                FontFamily family = SystemFonts.MessageBoxFont.FontFamily;
                this.halfFont = new Font(family, px, FontStyle.Regular, GraphicsUnit.Pixel);
                this.fullFont = new Font(family, px, FontStyle.Regular, GraphicsUnit.Pixel);
            }

            this.ApplyItemHeight();
        }

        private void ApplyItemHeight()
        {
            int dpi = DpiUtil.GetDpi(this.IsHandleCreated ? this.Handle : IntPtr.Zero);
            int fontHeight = 12;
            if (this.halfFont != null)
            {
                fontHeight = this.halfFont.Height;
            }

            if (this.fullFont != null && this.fullFont.Height > fontHeight)
            {
                fontHeight = this.fullFont.Height;
            }

            this.ItemHeight = DpiUtil.TreeItemHeight(fontHeight, dpi);
        }

        private void RefreshChrome()
        {
            if (this.chromeBusy || !this.IsHandleCreated || this.vScroll == null || this.hScroll == null)
            {
                return;
            }

            this.chromeBusy = true;
            try
            {
                Native.SCROLLINFO vInfo;
                bool hasV = Native.TryGetScroll(this.Handle, Native.SB_VERT, out vInfo);
                Native.ShowScrollBar(this.Handle, Native.SB_BOTH, false);

                int dpi = DpiUtil.GetDpi(this.Handle);
                int bar = DpiUtil.ToPixels(DpiUtil.ScrollBarThicknessDip, dpi);
                int visibleCount = this.CountVisibleNodes(this.Nodes);
                int maxLabel = this.MeasureVisibleLabelMaxWidth(dpi);
                int clientW = this.ClientSize.Width;
                int clientH = this.ClientSize.Height;
                int itemH = this.ItemHeight;
                if (itemH < 1)
                {
                    itemH = 1;
                }

                int viewportH = clientH;
                int visibleRows = viewportH / itemH;
                if (visibleRows < 1)
                {
                    visibleRows = 1;
                }

                bool needV = visibleCount > visibleRows;
                int viewportW = clientW - (needV ? bar : 0);
                bool needH = maxLabel > viewportW;
                if (needH)
                {
                    viewportH = clientH - bar;
                    visibleRows = viewportH / itemH;
                    if (visibleRows < 1)
                    {
                        visibleRows = 1;
                    }

                    needV = visibleCount > visibleRows;
                    viewportW = clientW - (needV ? bar : 0);
                    needH = maxLabel > viewportW;
                }

                this.ignoreThemedScroll = true;
                try
                {
                    this.vScroll.Minimum = 0;
                    this.vScroll.Maximum = Math.Max(0, visibleCount - 1);
                    this.vScroll.LargeChange = visibleRows;
                    this.hScroll.Minimum = 0;
                    this.hScroll.Maximum = Math.Max(0, maxLabel - 1);
                    this.hScroll.LargeChange = Math.Max(1, viewportW);
                    this.vScroll.Visible = needV;
                    this.hScroll.Visible = needH;
                    if (needV)
                    {
                        if (hasV)
                        {
                            this.vScroll.Value = vInfo.nPos;
                        }
                    }
                    else
                    {
                        this.vScroll.Value = 0;
                    }

                    if (!needH)
                    {
                        this.hScroll.Value = 0;
                    }
                }
                finally
                {
                    this.ignoreThemedScroll = false;
                }

                int vW = needV ? bar : 0;
                int hH = needH ? bar : 0;
                this.vScroll.Bounds = new Rectangle(this.ClientSize.Width - vW, 0, vW, Math.Max(0, this.ClientSize.Height - hH));
                this.hScroll.Bounds = new Rectangle(0, this.ClientSize.Height - hH, Math.Max(0, this.ClientSize.Width - vW), hH);
                this.vScroll.BringToFront();
                this.hScroll.BringToFront();
                if (!needV)
                {
                    this.SetNativeScroll(Native.SB_VERT, 0);
                }

                this.SetNativeScroll(Native.SB_HORZ, 0);

                Native.ShowScrollBar(this.Handle, Native.SB_BOTH, false);
            }
            finally
            {
                this.chromeBusy = false;
            }
        }

        private int CountVisibleNodes(TreeNodeCollection nodes)
        {
            int n = 0;
            for (int i = 0; i < nodes.Count; i++)
            {
                TreeNode node = nodes[i];
                if (object.Equals(node.Tag, LazyTag))
                {
                    continue;
                }

                n++;
                if (node.IsExpanded)
                {
                    n += this.CountVisibleNodes(node.Nodes);
                }
            }

            return n;
        }

        private int MeasureVisibleLabelMaxWidth(int dpi)
        {
            if (this.halfFont == null || this.fullFont == null || !this.IsHandleCreated)
            {
                return 0;
            }

            using (Graphics g = this.CreateGraphics())
            {
                return this.MeasureVisibleLabelMaxWidthCore(g, this.Nodes, dpi);
            }
        }

        private int MeasureVisibleLabelMaxWidthCore(Graphics g, TreeNodeCollection nodes, int dpi)
        {
            int max = 0;
            int indent = DpiUtil.ToPixels(4, dpi);
            int step = DpiUtil.ToPixels(16, dpi);
            int markW = DpiUtil.ToPixels(14, dpi);
            for (int i = 0; i < nodes.Count; i++)
            {
                TreeNode node = nodes[i];
                if (object.Equals(node.Tag, LazyTag))
                {
                    continue;
                }

                int w = indent + (node.Level * step);
                string path = node.Tag as string;
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                {
                    w += markW;
                }

                w += (int)Math.Ceiling(DualFontPainter.Measure(g, node.Text, this.halfFont, this.fullFont, this.typographic));
                w += DpiUtil.ToPixels(DpiUtil.LineNumberPadDip, dpi);
                if (w > max)
                {
                    max = w;
                }

                if (node.IsExpanded)
                {
                    int child = this.MeasureVisibleLabelMaxWidthCore(g, node.Nodes, dpi);
                    if (child > max)
                    {
                        max = child;
                    }
                }
            }

            return max;
        }

        private void CollectExpanded(TreeNodeCollection nodes, List<string> expanded)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                TreeNode node = nodes[i];
                if (object.Equals(node.Tag, LazyTag))
                {
                    continue;
                }

                if (node.IsExpanded)
                {
                    string path = node.Tag as string;
                    if (!string.IsNullOrEmpty(path))
                    {
                        expanded.Add(path);
                    }

                    this.CollectExpanded(node.Nodes, expanded);
                }
            }
        }

        private TreeNode FindNodeByPath(TreeNodeCollection nodes, string path)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                TreeNode node = nodes[i];
                string tag = node.Tag as string;
                if (tag != null && string.Equals(tag, path, StringComparison.OrdinalIgnoreCase))
                {
                    return node;
                }

                TreeNode child = this.FindNodeByPath(node.Nodes, path);
                if (child != null)
                {
                    return child;
                }
            }

            return null;
        }

        private static int CompareExpandPath(string a, string b)
        {
            int da = PathDepth(a);
            int db = PathDepth(b);
            if (da != db)
            {
                return da.CompareTo(db);
            }

            return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
        }

        private static int PathDepth(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return 0;
            }

            int n = 0;
            for (int i = 0; i < path.Length; i++)
            {
                char c = path[i];
                if (c == '\\' || c == '/')
                {
                    n++;
                }
            }

            return n;
        }

        private void OnThemedScrollChanged(object sender, EventArgs e)
        {
            if (this.ignoreThemedScroll || !this.IsHandleCreated)
            {
                return;
            }

            ThemedScrollBar bar = sender as ThemedScrollBar;
            if (bar == null)
            {
                return;
            }

            if (object.ReferenceEquals(bar, this.vScroll))
            {
                this.SetNativeScroll(Native.SB_VERT, bar.Value);
                return;
            }

            this.Invalidate();
        }

        private void ApplyHorizontalDelta(int delta)
        {
            if (this.hScroll == null || !this.hScroll.Visible)
            {
                return;
            }

            int dpi = DpiUtil.GetDpi(this.IsHandleCreated ? this.Handle : IntPtr.Zero);
            int dx = DpiUtil.WheelToPixels(delta, DpiUtil.ToPixels(32, dpi));
            this.hScroll.Value = this.hScroll.Value + dx;
            this.Invalidate();
        }

        private void SetNativeScroll(int nBar, int pos)
        {
            Native.SCROLLINFO si = new Native.SCROLLINFO();
            si.cbSize = (uint)Marshal.SizeOf(typeof(Native.SCROLLINFO));
            si.fMask = Native.SIF_POS;
            si.nPos = pos;
            Native.SetScrollInfo(this.Handle, nBar, ref si, true);

            int msg = (nBar == Native.SB_VERT) ? WM_VSCROLL : WM_HSCROLL;
            int wParam = SB_THUMBPOSITION | (pos << 16);
            this.ignoreThemedScroll = true;
            try
            {
                Native.SendMessage(this.Handle, msg, new IntPtr(wParam), IntPtr.Zero);
            }
            finally
            {
                this.ignoreThemedScroll = false;
            }

            Native.ShowScrollBar(this.Handle, Native.SB_BOTH, false);
            this.Invalidate();
        }

        /// <summary>所有している 12 DIP Pixel フォントを破棄する。</summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this.halfFont != null)
                {
                    this.halfFont.Dispose();
                    if (object.ReferenceEquals(this.fullFont, this.halfFont))
                    {
                        this.fullFont = null;
                    }

                    this.halfFont = null;
                }

                if (this.fullFont != null)
                {
                    this.fullFont.Dispose();
                    this.fullFont = null;
                }

                if (this.typographic != null)
                {
                    this.typographic.Dispose();
                    this.typographic = null;
                }
            }

            base.Dispose(disposing);
        }

        private static class Native
        {
            public const int SB_HORZ = 0;
            public const int SB_VERT = 1;
            public const int SB_BOTH = 3;
            public const uint SIF_POS = 0x0004;
            public const uint SIF_ALL = 0x17;
            public const int TVM_SETEXTENDEDSTYLE = 0x1100 + 44;
            public const int TVS_EX_DOUBLEBUFFER = 0x0004;

            [StructLayout(LayoutKind.Sequential)]
            public struct SCROLLINFO
            {
                public uint cbSize;
                public uint fMask;
                public int nMin;
                public int nMax;
                public uint nPage;
                public int nPos;
                public int nTrackPos;
            }

            [DllImport("user32.dll")]
            public static extern bool ShowScrollBar(IntPtr hWnd, int wBar, bool bShow);

            [DllImport("user32.dll")]
            public static extern bool GetScrollInfo(IntPtr hwnd, int nBar, ref SCROLLINFO lpsi);

            [DllImport("user32.dll")]
            public static extern int SetScrollInfo(IntPtr hwnd, int nBar, ref SCROLLINFO lpsi, bool redraw);

            [DllImport("user32.dll")]
            public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

            public static bool TryGetScroll(IntPtr hwnd, int nBar, out SCROLLINFO info)
            {
                info = new SCROLLINFO();
                info.cbSize = (uint)Marshal.SizeOf(typeof(SCROLLINFO));
                info.fMask = SIF_ALL;
                return GetScrollInfo(hwnd, nBar, ref info);
            }
        }
    }
}
