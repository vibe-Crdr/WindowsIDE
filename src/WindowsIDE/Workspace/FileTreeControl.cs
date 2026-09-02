using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using WindowsIDE.Ui;
using WindowsIDE.Ui.Fonts;

namespace WindowsIDE.Workspace
{
    /// <summary>
    /// クリックまたは Enter でファイルを開くツリー。作成とリネームは行内 DualFontField。F2 と遅延ラベルクリックでリネーム、Delete で削除要求。コンテキストメニューは持たない。子は展開時に読む。
    /// </summary>
    public sealed class FileTreeControl : TreeView
    {
        private const string LazyTag = "lazy";
        private const int WM_HSCROLL = 0x0114;
        private const int WM_VSCROLL = 0x0115;
        private const int WM_MOUSEWHEEL = 0x020A;
        private const int WM_MOUSEHWHEEL = 0x020E;
        private const int SB_THUMBPOSITION = 4;

        private enum InlineSessionKind
        {
            None,
            Create,
            Rename
        }

        private static readonly object InlineCreateTag = new object();

        private string rootPath;
        private readonly Dictionary<TreeNode, Rectangle> expandMarks;
        private FontLoadResult fonts;
        private Font halfFont;
        private Font fullFont;
        private StringFormat typographic;
        private int lastDpi;
        private ThemedScrollBar vScroll;
        private ThemedScrollBar hScroll;
        private DualFontField createField;
        private TreeNode createNode;
        private bool createIsFolder;
        private string createParentDirectory;
        private InlineSessionKind inlineKind;
        private string renameOriginalName;
        private bool suppressBlurCancel;
        private bool ignoreThemedScroll;
        private bool chromeBusy;
        private bool nativeBarBusy;
        private bool rebuildBusy;
        private int wheelLeftover;
        private Timer renameClickTimer;
        private TreeNode pendingRenameNode;
        private TreeNode lastClickNode;
        private DateTime lastClickAt;

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
            this.vScroll.GotFocus += this.OnPaneFocusInvalidate;
            this.vScroll.LostFocus += this.OnPaneFocusInvalidate;
            this.hScroll.GotFocus += this.OnPaneFocusInvalidate;
            this.hScroll.LostFocus += this.OnPaneFocusInvalidate;
            this.Controls.Add(this.vScroll);
            this.Controls.Add(this.hScroll);

            this.createField = new DualFontField();
            this.createField.Visible = false;
            this.createField.TabStop = true;
            this.createField.BackColor = Theme.EditorBackground;
            this.createField.ForeColor = Theme.Foreground;
            this.createField.KeyDown += this.OnCreateFieldKeyDown;
            this.createField.GotFocus += this.OnPaneFocusInvalidate;
            this.createField.LostFocus += this.OnCreateFieldLostFocus;
            this.Controls.Add(this.createField);

            this.renameClickTimer = new Timer();
            this.renameClickTimer.Tick += this.OnRenameClickTimerTick;

            this.RecreateUiFont();
            this.BeforeExpand += this.OnBeforeExpand;
            this.AfterExpand += this.OnAfterExpandCollapse;
            this.AfterCollapse += this.OnAfterExpandCollapse;
        }

        /// <summary>
        /// SysTreeView32 に TVS_NOHSCROLL を付ける。Scrollable は変えない。
        /// </summary>
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.Style |= Native.TVS_NOHSCROLL;
                return cp;
            }
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
            this.SuppressNativeBars();
            Native.SendMessage(this.Handle, Native.TVM_SETEXTENDEDSTYLE, new IntPtr(Native.TVS_EX_DOUBLEBUFFER), new IntPtr(Native.TVS_EX_DOUBLEBUFFER));
            this.RecreateUiFont();
            this.RefreshChrome();
            this.SuppressNativeBars();
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

        /// <summary>行内作成の確定。Name は Trim 済み非空。ディスクにはまだ触れない。</summary>
        public event EventHandler<TreeCreateCommitEventArgs> InlineCreateCommit;

        /// <summary>行内リネームの確定。NewName は Trim 済み。ディスクにはまだ触れない。</summary>
        public event EventHandler<TreeRenameCommitEventArgs> InlineRenameCommit;

        /// <summary>ツリーフォーカス時の Delete。SelectedNode.Tag を読む。</summary>
        public event EventHandler DeleteRequested;

        /// <summary>最後に開く要求を出したファイル。フォルダなら null。</summary>
        public string SelectedFilePath { get; private set; }

        /// <summary>行内作成またはリネームの欄が出ているとき true。</summary>
        public bool IsInlineCreateActive
        {
            get
            {
                return this.createNode != null && this.createField != null && this.createField.Visible;
            }
        }

        /// <summary>行内作成／リネーム欄が IME 未確定のとき true。</summary>
        public bool IsInlineCreateComposing
        {
            get
            {
                return this.IsInlineCreateActive && this.createField.IsComposing;
            }
        }

        /// <summary>
        /// ワークスペースを結び付けて根から読む。
        /// </summary>
        public void BindWorkspace(string path)
        {
            this.CancelInlineCreate();
            this.rootPath = path;
            this.Rebuild();
        }

        /// <summary>
        /// フォーカス復帰用に再構築する。展開・選択・スクロールは復元する。作成中なら破棄する。
        /// </summary>
        public void Rebuild()
        {
            this.CancelInlineCreate();
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

        /// <summary>
        /// 指定親の下で行内作成を始める。既存セッションは破棄する。失敗したら false。
        /// </summary>
        /// <param name="isFolder">フォルダ作成なら true。</param>
        /// <param name="parentDirectory">作成先ディレクトリ。</param>
        /// <returns>欄を出せたら true。</returns>
        public bool BeginInlineCreate(bool isFolder, string parentDirectory)
        {
            this.CancelInlineCreate();
            if (string.IsNullOrEmpty(parentDirectory) || this.createField == null)
            {
                return false;
            }

            string parentFull;
            try
            {
                parentFull = Path.GetFullPath(parentDirectory);
            }
            catch (Exception)
            {
                return false;
            }

            if (!Directory.Exists(parentFull) || !PathGuard.IsInsideWorkspace(this.rootPath, parentFull))
            {
                return false;
            }

            TreeNode parentNode = this.FindNodeByPath(this.Nodes, parentFull);
            if (parentNode == null)
            {
                return false;
            }

            this.EnsureChildren(parentNode);
            parentNode.Expand();

            int index = 0;
            if (!isFolder)
            {
                for (int i = 0; i < parentNode.Nodes.Count; i++)
                {
                    string tag = parentNode.Nodes[i].Tag as string;
                    if (!string.IsNullOrEmpty(tag) && Directory.Exists(tag))
                    {
                        index = i + 1;
                    }
                }
            }

            TreeNode placeholder = new TreeNode("");
            placeholder.Tag = InlineCreateTag;
            parentNode.Nodes.Insert(index, placeholder);

            this.createNode = placeholder;
            this.createIsFolder = isFolder;
            this.createParentDirectory = parentFull;
            this.inlineKind = InlineSessionKind.Create;
            this.renameOriginalName = null;
            this.createField.SetFonts(this.halfFont, this.fullFont, DpiUtil.UiFontDip);
            this.createField.Text = "";
            this.createField.BorderColor = Theme.Selection;
            this.createField.Visible = true;
            placeholder.EnsureVisible();
            this.RefreshChrome();
            this.createField.Focus();
            return true;
        }

        /// <summary>
        /// 指定ノードの行内リネームを始める。根・番兵・lazy・ワークスペース外は false。既存セッションは破棄する。
        /// </summary>
        /// <param name="node">対象ノード。</param>
        /// <returns>欄を出せたら true。</returns>
        public bool BeginInlineRename(TreeNode node)
        {
            this.CancelInlineCreate();
            this.StopRenameClickTimer();
            if (node == null || this.createField == null || this.IsCreatePlaceholder(node) || object.Equals(node.Tag, LazyTag))
            {
                return false;
            }

            string path = node.Tag as string;
            if (string.IsNullOrEmpty(path) || !PathGuard.IsInsideWorkspace(this.rootPath, path)
                || WorkspaceItemRules.IsWorkspaceRoot(this.rootPath, path))
            {
                return false;
            }

            if (!File.Exists(path) && !Directory.Exists(path))
            {
                return false;
            }

            this.createNode = node;
            this.createIsFolder = false;
            this.createParentDirectory = null;
            this.inlineKind = InlineSessionKind.Rename;
            this.renameOriginalName = node.Text;
            this.SelectedNode = node;
            this.createField.SetFonts(this.halfFont, this.fullFont, DpiUtil.UiFontDip);
            this.createField.Text = node.Text;
            this.createField.SelectAll();
            this.createField.BorderColor = Theme.Selection;
            this.createField.Visible = true;
            node.EnsureVisible();
            this.RefreshChrome();
            this.createField.Focus();
            this.createField.SelectAll();
            this.Invalidate();
            return true;
        }

        /// <summary>
        /// 行内作成の番兵を外し、リネーム欄も隠す。ディスクには触れない。
        /// </summary>
        public void CancelInlineCreate()
        {
            this.StopRenameClickTimer();
            InlineSessionKind kind = this.inlineKind;
            TreeNode node = this.createNode;
            TreeNode parent = (node == null) ? null : node.Parent;
            this.createNode = null;
            this.createIsFolder = false;
            this.createParentDirectory = null;
            this.inlineKind = InlineSessionKind.None;
            this.renameOriginalName = null;
            if (this.createField != null)
            {
                this.createField.Visible = false;
                this.createField.Text = "";
                this.createField.BorderColor = Theme.Border;
            }

            if (kind == InlineSessionKind.Create && node != null)
            {
                if (node.Parent != null)
                {
                    node.Parent.Nodes.Remove(node);
                }
                else if (this.Nodes.Contains(node))
                {
                    this.Nodes.Remove(node);
                }

                if (parent != null)
                {
                    this.SelectedNode = parent;
                }

                this.RefreshChrome();
            }
            else if (kind == InlineSessionKind.Rename)
            {
                this.RefreshChrome();
                this.Invalidate();
            }
        }

        /// <summary>
        /// MessageBox 中など、欄の LostFocus でキャンセルしない。
        /// </summary>
        /// <param name="suppressed">true なら LostFocus キャンセルを止める。</param>
        public void SetInlineBlurCancelSuppressed(bool suppressed)
        {
            this.suppressBlurCancel = suppressed;
        }

        /// <summary>
        /// 行内作成／リネーム欄へフォーカスする。セッションが無ければ false。
        /// </summary>
        /// <returns>欄へ移せたら true。</returns>
        public bool FocusInlineCreate()
        {
            if (!this.IsInlineCreateActive)
            {
                return false;
            }

            return this.createField.Focus();
        }

        /// <summary>
        /// 祖先を EnsureChildren + Expand してから選択する。FileOpenRequested は上げない。Rebuild 中は何もしない。
        /// </summary>
        /// <param name="path">対象パス。</param>
        public void RevealAndSelect(string path)
        {
            if (this.rebuildBusy || string.IsNullOrEmpty(path))
            {
                return;
            }

            string full;
            try
            {
                full = Path.GetFullPath(path);
            }
            catch (Exception)
            {
                return;
            }

            if (!PathGuard.IsInsideWorkspace(this.rootPath, full))
            {
                return;
            }

            List<string> ancestors = new List<string>();
            string current;
            try
            {
                current = Path.GetDirectoryName(full);
            }
            catch (Exception)
            {
                return;
            }

            string rootFull = null;
            try
            {
                if (!string.IsNullOrEmpty(this.rootPath))
                {
                    rootFull = Path.GetFullPath(this.rootPath);
                }
            }
            catch (Exception)
            {
                rootFull = this.rootPath;
            }

            while (!string.IsNullOrEmpty(current))
            {
                string currentFull;
                try
                {
                    currentFull = Path.GetFullPath(current);
                }
                catch (Exception)
                {
                    break;
                }

                if (!PathGuard.IsInsideWorkspace(this.rootPath, currentFull))
                {
                    break;
                }

                ancestors.Add(currentFull);
                if (rootFull != null && string.Equals(currentFull, rootFull, StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                string parent = Path.GetDirectoryName(currentFull);
                if (string.IsNullOrEmpty(parent) || string.Equals(parent, currentFull, StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                current = parent;
            }

            ancestors.Reverse();
            for (int i = 0; i < ancestors.Count; i++)
            {
                TreeNode node = this.FindNodeByPath(this.Nodes, ancestors[i]);
                if (node != null)
                {
                    this.EnsureChildren(node);
                    node.Expand();
                }
            }

            TreeNode target = this.FindNodeByPath(this.Nodes, full);
            if (target == null)
            {
                return;
            }

            this.SelectedNode = target;
            target.EnsureVisible();
            this.RefreshChrome();
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
        /// ツリー選択行の塗り。ペイン内にフォーカスがあれば Selection、なければ CurrentLine。
        /// </summary>
        /// <param name="paneContainsFocus">FileTreeControl.ContainsFocus 相当。</param>
        /// <returns>選択行に使う Theme 色。プレースホルダには使わない。</returns>
        public static Color TreeSelectionBack(bool paneContainsFocus)
        {
            return paneContainsFocus ? Theme.Selection : Theme.CurrentLine;
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

            bool placeholder = this.IsCreatePlaceholder(e.Node);
            bool selected = !placeholder && (e.State & TreeNodeStates.Selected) != 0;
            Color back = selected ? TreeSelectionBack(this.ContainsFocus) : Theme.Background;
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

            if (placeholder)
            {
                int icon = DpiUtil.ToPixels(16, dpi);
                int iconX = indent + (e.Node.Level * step);
                int iconY = bounds.Y + (rowH - icon) / 2;
                this.DrawCreatePlaceholderIcon(e.Graphics, iconX, iconY, dpi, this.createIsFolder);
                this.expandMarks.Remove(e.Node);
                return;
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
            if (this.IsRenameTarget(e.Node))
            {
                return;
            }

            using (SolidBrush fg = new SolidBrush(Theme.Foreground))
            {
                DualFontPainter.Draw(e.Graphics, e.Node.Text, this.halfFont, this.fullFont, clip, x, fg, this.typographic);
            }
        }

        /// <summary>ツリー本体がフォーカスを得たとき選択行を Selection で描き直す。</summary>
        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            this.Invalidate();
        }

        /// <summary>ツリー本体がフォーカスを失ったとき選択行を CurrentLine で描き直す。</summary>
        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            this.Invalidate();
        }

        /// <summary>子を含むペインへ入ったとき選択行を描き直す。</summary>
        protected override void OnEnter(EventArgs e)
        {
            base.OnEnter(e);
            this.Invalidate();
        }

        /// <summary>子を含むペインから出たとき選択行を描き直す。</summary>
        protected override void OnLeave(EventArgs e)
        {
            base.OnLeave(e);
            this.Invalidate();
        }

        private void OnPaneFocusInvalidate(object sender, EventArgs e)
        {
            this.Invalidate();
        }

        /// <summary>HWHEEL は横。NCCALCSIZE 前とスクロール後にネイティブバーを隠す。VSCROLL 系は nPos 取得（RefreshChrome）のあと Suppress。</summary>
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

            if (m.Msg == Native.WM_NCCALCSIZE)
            {
                this.SuppressNativeBars();
                base.WndProc(ref m);
                return;
            }

            base.WndProc(ref m);
            if (m.Msg == WM_VSCROLL || m.Msg == WM_HSCROLL || m.Msg == WM_MOUSEWHEEL)
            {
                this.RefreshChrome();
            }

            if (m.Msg == WM_VSCROLL || m.Msg == WM_HSCROLL || m.Msg == WM_MOUSEWHEEL
                || m.Msg == Native.WM_SIZE || m.Msg == Native.WM_WINDOWPOSCHANGED)
            {
                this.SuppressNativeBars();
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
        /// 選択済みラベルの遅延クリックでリネーム。行内中に番兵／リネーム対象以外をクリックしたら先にキャンセルする。
        /// </summary>
        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (this.rebuildBusy)
            {
                return;
            }

            if (this.IsInlineCreateActive)
            {
                TreeNode inlineHit = this.HitNode(e.X, e.Y);
                if (this.IsCreatePlaceholder(inlineHit) || this.IsRenameTarget(inlineHit))
                {
                    if (this.createField != null)
                    {
                        this.createField.Focus();
                    }

                    return;
                }

                this.CancelInlineCreate();
            }

            if (e.Button != MouseButtons.Left || Control.ModifierKeys != Keys.None)
            {
                this.StopRenameClickTimer();
                if (e.Button != MouseButtons.Left)
                {
                    base.OnMouseDown(e);
                    return;
                }

                TreeNode other = this.HitNode(e.X, e.Y);
                base.OnMouseDown(e);
                if (other == null)
                {
                    this.SelectedFilePath = null;
                    return;
                }

                string otherPath = other.Tag as string;
                if (!string.IsNullOrEmpty(otherPath) && Directory.Exists(otherPath))
                {
                    this.SelectedFilePath = null;
                    Rectangle otherMark;
                    if (this.expandMarks.TryGetValue(other, out otherMark) && otherMark.Contains(e.Location))
                    {
                        if (other.IsExpanded)
                        {
                            other.Collapse();
                        }
                        else
                        {
                            other.Expand();
                        }
                    }

                    return;
                }

                this.TryRequestFileOpen(other);
                return;
            }

            TreeNode node = this.HitNode(e.X, e.Y);
            if (node != null)
            {
                string folderPath = node.Tag as string;
                Rectangle mark;
                if (!string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath)
                    && this.expandMarks.TryGetValue(node, out mark) && mark.Contains(e.Location))
                {
                    this.StopRenameClickTimer();
                    this.RememberClick(node);
                    base.OnMouseDown(e);
                    this.SelectedFilePath = null;
                    if (node.IsExpanded)
                    {
                        node.Collapse();
                    }
                    else
                    {
                        node.Expand();
                    }

                    return;
                }
            }

            if (node != null && this.IsPendingRenameClick(node))
            {
                this.StopRenameClickTimer();
                this.RememberClick(node);
                base.OnMouseDown(e);
                this.TryRequestFileOpen(node);
                return;
            }

            bool wasSelected = node != null && this.SelectedNode == node;
            bool labelHit = node != null && this.HitLabel(node, e.Location);
            bool canRename = node != null && this.CanBeginRename(node);
            if (wasSelected && labelHit && canRename && !this.IsQuickRepeat(node))
            {
                this.StartRenameClickTimer(node);
                this.RememberClick(node);
                base.OnMouseDown(e);
                return;
            }

            this.StopRenameClickTimer();
            this.RememberClick(node);
            base.OnMouseDown(e);
            if (node == null)
            {
                this.SelectedFilePath = null;
                return;
            }

            string path = node.Tag as string;
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            {
                this.SelectedFilePath = null;
                return;
            }

            this.TryRequestFileOpen(node);
        }

        /// <summary>
        /// 選択中ファイルは Enter で開く。F2 でリネーム。Delete で削除要求。フォルダと修飾付き Enter は base に渡す。
        /// </summary>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (this.rebuildBusy)
            {
                base.OnKeyDown(e);
                return;
            }

            if (this.IsInlineCreateActive)
            {
                if (e.KeyData == Keys.F2 || e.KeyData == Keys.Delete)
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    return;
                }
            }

            if (e.KeyData == Keys.F2)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                this.BeginInlineRename(this.SelectedNode);
                return;
            }

            if (e.KeyData == Keys.Delete)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                EventHandler del = this.DeleteRequested;
                if (del != null)
                {
                    del(this, EventArgs.Empty);
                }

                return;
            }

            if (e.KeyData == Keys.Enter)
            {
                if (this.TryRequestFileOpen(this.SelectedNode))
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    return;
                }
            }

            base.OnKeyDown(e);
        }

        /// <summary>
        /// ファイルノードなら選択して開く要求を出す。フォルダ・lazy・ワークスペース外は false。
        /// </summary>
        /// <param name="node">対象ノード。null 可。</param>
        /// <returns>開く要求を出したら true。</returns>
        private bool TryRequestFileOpen(TreeNode node)
        {
            if (node == null || object.Equals(node.Tag, LazyTag) || this.IsCreatePlaceholder(node))
            {
                this.SelectedFilePath = null;
                return false;
            }

            string path = node.Tag as string;
            if (string.IsNullOrEmpty(path)
                || Directory.Exists(path)
                || !File.Exists(path)
                || !PathGuard.IsInsideWorkspace(this.rootPath, path))
            {
                this.SelectedFilePath = null;
                return false;
            }

            this.SelectedNode = node;
            this.SelectedFilePath = path;
            EventHandler h = this.FileOpenRequested;
            if (h != null)
            {
                h(this, EventArgs.Empty);
            }

            return true;
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
            if (this.createField != null)
            {
                this.createField.SetFonts(this.halfFont, this.fullFont, DpiUtil.UiFontDip);
            }
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
                this.SuppressNativeBars();

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
                this.LayoutInlineCreateField();
                if (!needV)
                {
                    this.SetNativeScroll(Native.SB_VERT, 0);
                }

                this.SetNativeScroll(Native.SB_HORZ, 0);

                this.SuppressNativeBars();
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
            Native.SetScrollInfo(this.Handle, nBar, ref si, false);

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

            this.SuppressNativeBars();
            this.Invalidate();
        }

        private void SuppressNativeBars()
        {
            if (this.nativeBarBusy || !this.IsHandleCreated)
            {
                return;
            }

            this.nativeBarBusy = true;
            try
            {
                Native.ShowScrollBar(this.Handle, Native.SB_BOTH, false);
                long style = Native.GetWindowLongPtr(this.Handle, Native.GWL_STYLE).ToInt64();
                long next = style & ~((long)Native.WS_VSCROLL | (long)Native.WS_HSCROLL);
                if (next != style)
                {
                    Native.SetWindowLongPtr(this.Handle, Native.GWL_STYLE, new IntPtr(next));
                }
            }
            finally
            {
                this.nativeBarBusy = false;
            }
        }

        /// <summary>所有している 12 DIP Pixel フォントを破棄する。作成欄とスクロールの購読も外す。</summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.StopRenameClickTimer();
                if (this.renameClickTimer != null)
                {
                    this.renameClickTimer.Tick -= this.OnRenameClickTimerTick;
                    this.renameClickTimer.Dispose();
                    this.renameClickTimer = null;
                }

                if (this.createField != null)
                {
                    this.createField.KeyDown -= this.OnCreateFieldKeyDown;
                    this.createField.GotFocus -= this.OnPaneFocusInvalidate;
                    this.createField.LostFocus -= this.OnCreateFieldLostFocus;
                }

                if (this.vScroll != null)
                {
                    this.vScroll.GotFocus -= this.OnPaneFocusInvalidate;
                    this.vScroll.LostFocus -= this.OnPaneFocusInvalidate;
                }

                if (this.hScroll != null)
                {
                    this.hScroll.GotFocus -= this.OnPaneFocusInvalidate;
                    this.hScroll.LostFocus -= this.OnPaneFocusInvalidate;
                }

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

        private bool IsCreatePlaceholder(TreeNode node)
        {
            return node != null && object.ReferenceEquals(node.Tag, InlineCreateTag);
        }

        private bool IsRenameTarget(TreeNode node)
        {
            return this.inlineKind == InlineSessionKind.Rename && node != null && object.ReferenceEquals(node, this.createNode);
        }

        private bool CanBeginRename(TreeNode node)
        {
            if (node == null || this.IsCreatePlaceholder(node) || object.Equals(node.Tag, LazyTag))
            {
                return false;
            }

            string path = node.Tag as string;
            if (string.IsNullOrEmpty(path) || !PathGuard.IsInsideWorkspace(this.rootPath, path)
                || WorkspaceItemRules.IsWorkspaceRoot(this.rootPath, path))
            {
                return false;
            }

            return File.Exists(path) || Directory.Exists(path);
        }

        private bool HitLabel(TreeNode node, Point location)
        {
            if (node == null || this.halfFont == null || this.fullFont == null)
            {
                return false;
            }

            Rectangle row = node.Bounds;
            int rowH = this.ItemHeight;
            if (rowH < 1)
            {
                rowH = row.Height;
            }

            if (location.Y < row.Y || location.Y >= row.Y + rowH)
            {
                return false;
            }

            int dpi = DpiUtil.GetDpi(this.IsHandleCreated ? this.Handle : IntPtr.Zero);
            int indent = DpiUtil.ToPixels(4, dpi);
            int step = DpiUtil.ToPixels(16, dpi);
            int markW = DpiUtil.ToPixels(14, dpi);
            int scrollX = 0;
            if (this.hScroll != null)
            {
                scrollX = this.hScroll.Value;
            }

            int x = indent + (node.Level * step) - scrollX;
            string path = node.Tag as string;
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            {
                x += markW;
            }

            int width;
            using (Graphics g = this.CreateGraphics())
            {
                width = (int)Math.Ceiling(DualFontPainter.Measure(g, node.Text, this.halfFont, this.fullFont, this.typographic));
            }

            if (width < 1)
            {
                return false;
            }

            return location.X >= x && location.X < x + width;
        }

        private void RememberClick(TreeNode node)
        {
            this.lastClickNode = node;
            this.lastClickAt = DateTime.UtcNow;
        }

        private bool IsQuickRepeat(TreeNode node)
        {
            if (node == null || this.lastClickNode != node)
            {
                return false;
            }

            int limit = SystemInformation.DoubleClickTime;
            if (limit < 1)
            {
                limit = 500;
            }

            return (DateTime.UtcNow - this.lastClickAt).TotalMilliseconds <= limit;
        }

        private bool IsPendingRenameClick(TreeNode node)
        {
            return node != null && this.renameClickTimer != null && this.renameClickTimer.Enabled && this.pendingRenameNode == node;
        }

        private void StartRenameClickTimer(TreeNode node)
        {
            this.StopRenameClickTimer();
            if (node == null || this.renameClickTimer == null)
            {
                return;
            }

            int interval = SystemInformation.DoubleClickTime;
            if (interval < 1)
            {
                interval = 500;
            }

            this.pendingRenameNode = node;
            this.renameClickTimer.Interval = interval;
            this.renameClickTimer.Start();
        }

        private void StopRenameClickTimer()
        {
            if (this.renameClickTimer != null)
            {
                this.renameClickTimer.Stop();
            }

            this.pendingRenameNode = null;
        }

        private void OnRenameClickTimerTick(object sender, EventArgs e)
        {
            TreeNode node = this.pendingRenameNode;
            this.StopRenameClickTimer();
            if (node == null || node.TreeView != this)
            {
                return;
            }

            this.BeginInlineRename(node);
        }

        private void OnCreateFieldKeyDown(object sender, KeyEventArgs e)
        {
            if (e == null || !this.IsInlineCreateActive)
            {
                return;
            }

            if (e.KeyCode == Keys.Escape)
            {
                if (this.createField.IsComposing)
                {
                    return;
                }

                e.Handled = true;
                e.SuppressKeyPress = true;
                this.CancelInlineCreate();
                return;
            }

            if (e.KeyCode != Keys.Enter)
            {
                return;
            }

            if (this.createField.IsComposing)
            {
                return;
            }

            e.Handled = true;
            e.SuppressKeyPress = true;
            string name = this.createField.Text;
            if (name == null)
            {
                name = "";
            }

            name = name.Trim();
            if (this.inlineKind == InlineSessionKind.Rename)
            {
                string current = this.renameOriginalName;
                if (current == null)
                {
                    current = "";
                }

                if (name.Length == 0 || string.Equals(name, current, StringComparison.OrdinalIgnoreCase))
                {
                    this.CancelInlineCreate();
                    return;
                }

                string oldPath = (this.createNode == null) ? null : (this.createNode.Tag as string);
                EventHandler<TreeRenameCommitEventArgs> rh = this.InlineRenameCommit;
                if (rh != null && !string.IsNullOrEmpty(oldPath))
                {
                    rh(this, new TreeRenameCommitEventArgs(oldPath, name));
                }

                return;
            }

            if (name.Length == 0)
            {
                this.CancelInlineCreate();
                return;
            }

            EventHandler<TreeCreateCommitEventArgs> h = this.InlineCreateCommit;
            if (h != null)
            {
                h(this, new TreeCreateCommitEventArgs(this.createIsFolder, this.createParentDirectory, name));
            }
        }

        private void OnCreateFieldLostFocus(object sender, EventArgs e)
        {
            this.Invalidate();
            if (!this.IsInlineCreateActive || this.suppressBlurCancel)
            {
                return;
            }

            Form form = this.FindForm();
            if (form != null && !form.ContainsFocus)
            {
                return;
            }

            this.BeginInvoke(new MethodInvoker(this.CancelInlineCreateIfBlurred));
        }

        private void CancelInlineCreateIfBlurred()
        {
            if (this.IsDisposed || !this.IsInlineCreateActive || this.suppressBlurCancel)
            {
                return;
            }

            if (this.createField != null && this.createField.Focused)
            {
                return;
            }

            Form form = this.FindForm();
            if (form != null && !form.ContainsFocus)
            {
                return;
            }

            this.CancelInlineCreate();
        }

        private void LayoutInlineCreateField()
        {
            if (!this.IsInlineCreateActive || this.createNode == null || this.createField == null)
            {
                return;
            }

            int dpi = DpiUtil.GetDpi(this.IsHandleCreated ? this.Handle : IntPtr.Zero);
            int indent = DpiUtil.ToPixels(4, dpi);
            int step = DpiUtil.ToPixels(16, dpi);
            int icon = DpiUtil.ToPixels(16, dpi);
            int gap = DpiUtil.ToPixels(2, dpi);
            int markW = DpiUtil.ToPixels(14, dpi);
            int rightPad = DpiUtil.ToPixels(4, dpi);
            Rectangle row = this.createNode.Bounds;
            int rowH = this.ItemHeight;
            if (rowH < 1)
            {
                rowH = row.Height;
            }

            int scrollX = 0;
            if (this.hScroll != null)
            {
                scrollX = this.hScroll.Value;
            }

            int left;
            if (this.inlineKind == InlineSessionKind.Rename)
            {
                left = indent + (this.createNode.Level * step) - scrollX;
                string path = this.createNode.Tag as string;
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                {
                    left += markW;
                }
            }
            else
            {
                left = indent + (this.createNode.Level * step) + icon + gap;
            }
            int right = this.ClientSize.Width - rightPad;
            if (this.vScroll != null && this.vScroll.Visible)
            {
                right = this.vScroll.Left - rightPad;
            }

            int height = this.createField.PreferredOuterHeight;
            int y = row.Y + (rowH - height) / 2;
            int bottomLimit = this.ClientSize.Height;
            if (this.hScroll != null && this.hScroll.Visible)
            {
                bottomLimit = this.hScroll.Top;
            }

            if (y + height > bottomLimit)
            {
                y = bottomLimit - height;
            }

            if (y < 0)
            {
                y = 0;
            }

            int minW = DpiUtil.ToPixels(24, dpi);
            int width = right - left;
            if (width < minW)
            {
                left = right - minW;
                if (left < 0)
                {
                    left = 0;
                }

                width = right - left;
            }

            if (width < 1)
            {
                width = 1;
            }

            this.createField.Bounds = new Rectangle(left, y, width, height);
        }

        private void DrawCreatePlaceholderIcon(Graphics g, int x, int y, int dpi, bool folder)
        {
            if (g == null)
            {
                return;
            }

            SmoothingMode previous = g.SmoothingMode;
            try
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                float width = (float)dpi / 96f;
                using (Pen pen = new Pen(Theme.Foreground, width))
                {
                    pen.LineJoin = LineJoin.Miter;
                    pen.StartCap = LineCap.Flat;
                    pen.EndCap = LineCap.Flat;
                    float originX = (float)x;
                    float originY = (float)y;
                    if (folder)
                    {
                        DrawCreateFolderIcon(g, pen, originX, originY, dpi);
                    }
                    else
                    {
                        DrawCreateFileIcon(g, pen, originX, originY, dpi);
                    }
                }
            }
            finally
            {
                g.SmoothingMode = previous;
            }
        }

        private static void DrawCreateFileIcon(Graphics g, Pen pen, float originX, float originY, int dpi)
        {
            using (GraphicsPath outline = new GraphicsPath())
            {
                outline.AddLines(new PointF[]
                {
                    CreateIconPt(3f, 2f, originX, originY, dpi),
                    CreateIconPt(9.2f, 2f, originX, originY, dpi),
                    CreateIconPt(12f, 4.8f, originX, originY, dpi),
                    CreateIconPt(12f, 13f, originX, originY, dpi),
                    CreateIconPt(3f, 13f, originX, originY, dpi)
                });
                outline.CloseFigure();
                g.DrawPath(pen, outline);
            }

            using (GraphicsPath fold = new GraphicsPath())
            {
                fold.AddLines(new PointF[]
                {
                    CreateIconPt(9.2f, 2f, originX, originY, dpi),
                    CreateIconPt(9.2f, 4.8f, originX, originY, dpi),
                    CreateIconPt(12f, 4.8f, originX, originY, dpi)
                });
                g.DrawPath(pen, fold);
            }
        }

        private static void DrawCreateFolderIcon(Graphics g, Pen pen, float originX, float originY, int dpi)
        {
            using (GraphicsPath outline = new GraphicsPath())
            {
                outline.AddLines(new PointF[]
                {
                    CreateIconPt(2f, 4.5f, originX, originY, dpi),
                    CreateIconPt(2f, 3.2f, originX, originY, dpi),
                    CreateIconPt(6.4f, 3.2f, originX, originY, dpi),
                    CreateIconPt(7.3f, 4.5f, originX, originY, dpi),
                    CreateIconPt(14f, 4.5f, originX, originY, dpi),
                    CreateIconPt(14f, 13f, originX, originY, dpi),
                    CreateIconPt(2f, 13f, originX, originY, dpi)
                });
                outline.CloseFigure();
                g.DrawPath(pen, outline);
            }
        }

        private static PointF CreateIconPt(float xDip, float yDip, float originX, float originY, int dpi)
        {
            return new PointF(originX + (xDip * (float)dpi / 96f), originY + (yDip * (float)dpi / 96f));
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
            public const int WM_NCCALCSIZE = 0x0083;
            public const int WM_SIZE = 0x0005;
            public const int WM_WINDOWPOSCHANGED = 0x0047;
            public const int GWL_STYLE = -16;
            public const int WS_HSCROLL = 0x00100000;
            public const int WS_VSCROLL = 0x00200000;
            public const int TVS_NOHSCROLL = 0x8000;

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

            [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
            public static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

            [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
            public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

            public static bool TryGetScroll(IntPtr hwnd, int nBar, out SCROLLINFO info)
            {
                info = new SCROLLINFO();
                info.cbSize = (uint)Marshal.SizeOf(typeof(SCROLLINFO));
                info.fMask = SIF_ALL;
                return GetScrollInfo(hwnd, nBar, ref info);
            }
        }
    }

    /// <summary>
    /// ツリー内インライン作成の確定。Name は Trim 済み非空。
    /// </summary>
    public sealed class TreeCreateCommitEventArgs : EventArgs
    {
        /// <summary>
        /// 確定内容を保持する。
        /// </summary>
        /// <param name="isFolder">フォルダ作成なら true。</param>
        /// <param name="parentDirectory">作成先ディレクトリ。</param>
        /// <param name="name">Trim 済み非空の名前。</param>
        public TreeCreateCommitEventArgs(bool isFolder, string parentDirectory, string name)
        {
            this.IsFolder = isFolder;
            this.ParentDirectory = parentDirectory;
            this.Name = name;
        }

        /// <summary>フォルダ作成なら true。</summary>
        public bool IsFolder { get; private set; }

        /// <summary>作成先ディレクトリ。</summary>
        public string ParentDirectory { get; private set; }

        /// <summary>Trim 済み名前。</summary>
        public string Name { get; private set; }
    }

    /// <summary>
    /// ツリー内インラインリネームの確定。NewName は Trim 済み非空。
    /// </summary>
    public sealed class TreeRenameCommitEventArgs : EventArgs
    {
        /// <summary>
        /// 確定内容を保持する。
        /// </summary>
        /// <param name="oldPath">リネーム前の絶対パス。</param>
        /// <param name="newName">Trim 済み新しい名前（1 要素）。</param>
        public TreeRenameCommitEventArgs(string oldPath, string newName)
        {
            this.OldPath = oldPath;
            this.NewName = newName;
        }

        /// <summary>リネーム前の絶対パス。</summary>
        public string OldPath { get; private set; }

        /// <summary>Trim 済み新しい名前。</summary>
        public string NewName { get; private set; }
    }
}
