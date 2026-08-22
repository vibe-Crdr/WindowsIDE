using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using WindowsIDE.Build;
using WindowsIDE.Editor;
using WindowsIDE.Languages;
using WindowsIDE.Languages.CSharp;
using WindowsIDE.Ui.Fonts;
using WindowsIDE.Workspace;

namespace WindowsIDE.Ui
{
    /// <summary>
    /// メイン枠。メニュー、ツリー、タブ、編集器、問題一覧、ステータス。起動時は下パネルを畳む。
    /// </summary>
    public sealed class MainForm : Form
    {
        private readonly FontLoadResult fonts;
        private WorkspaceFolder workspace;
        private DarkMenuRenderer chromeRenderer;
        private Font chromeHalfFont;
        private Font chromeFullFont;
        private Padding statusBasePadding;
        private MenuStrip menu;
        private SplitContainer bodySplit;
        private SplitContainer split;
        private FileTreeControl tree;
        private TabStrip tabs;
        private FindBar findBar;
        private TextView editor;
        private ProblemListControl problemList;
        private CscRunner cscRunner;
        private int buildGeneration;
        private string lastManualOutputExe;
        private bool bottomSplitterInitialized;
        private StatusStrip status;
        private ToolStripStatusLabel statusLang;
        private ToolStripStatusLabel statusPos;
        private ToolStripStatusLabel statusEnc;
        private ToolStripStatusLabel statusFont;
        private bool rebuilding;
        private bool suppressNewUntitled;
        private bool windowMetricsApplied;
        private bool leftSplitterInitialized;
        private bool activatedRebuildQueued;
        private bool activatedRebuildMouseDefer;
        private bool activatedRebuildIdleHooked;

        /// <summary>
        /// フォント読み込み結果を受け取ってシェルを組む。末尾で起動引数を開く。
        /// </summary>
        /// <param name="fonts">同梱フォントの結果。</param>
        /// <param name="startupArgs">Main から渡す起動引数。実行ファイルパスは含まない。</param>
        public MainForm(FontLoadResult fonts, string[] startupArgs)
        {
            this.fonts = fonts;
            this.Text = "WindowsIDE";
            this.StartPosition = FormStartPosition.Manual;
            this.BackColor = Theme.Background;
            this.ForeColor = Theme.Foreground;
            this.KeyPreview = true;
            this.AutoScaleMode = AutoScaleMode.None;

            this.cscRunner = new CscRunner();
            this.BuildMenu();
            this.BuildStatus();
            this.BuildBody();
            this.MainMenuStrip = this.menu;
            this.RecreateChromeFonts();

            this.ApplyEditorSettings(WorkspaceSettings.DefaultFontSize, WorkspaceSettings.DefaultTabSize);
            this.ApplyWindowMetrics();
            this.ApplyStartup(startupArgs);
            this.UpdateStatus();
        }

        /// <summary>レイアウト後、初回だけ左ペイン幅を 260 DIP 相当にする。</summary>
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (this.split == null || this.leftSplitterInitialized)
            {
                return;
            }

            int dist = DpiUtil.ToPixels(260, DpiUtil.GetDpi(this.IsHandleCreated ? this.Handle : IntPtr.Zero));
            if (this.split.Width > dist)
            {
                this.split.SplitterDistance = dist;
            }

            this.leftSplitterInitialized = true;
        }

        /// <summary>最初のハンドルで起動サイズを実 DPI に合わせ、キャプション色を付ける。</summary>
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            NativeCaption.Apply(this.Handle);
            this.ApplyWindowMetrics();
            this.RecreateChromeFonts();
        }

        /// <summary>フォルダ再読込（フォーカス復帰）。クリックより後に遅延する。</summary>
        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            if (this.workspace != null && this.tree != null)
            {
                this.QueueActivatedRebuild();
            }
        }

        /// <summary>破棄後に遅延 Rebuild が走らないよう Idle を外す。</summary>
        protected override void OnHandleDestroyed(EventArgs e)
        {
            this.UnhookActivatedRebuildIdle();
            base.OnHandleDestroyed(e);
        }

        private void QueueActivatedRebuild()
        {
            if (this.IsDisposed || !this.IsHandleCreated || this.activatedRebuildQueued)
            {
                return;
            }

            this.activatedRebuildQueued = true;
            this.BeginInvoke(new MethodInvoker(this.OnActivatedRebuild));
        }

        private void OnActivatedRebuild()
        {
            this.activatedRebuildQueued = false;
            if (this.IsDisposed || !this.IsHandleCreated || this.workspace == null || this.tree == null)
            {
                this.activatedRebuildMouseDefer = false;
                this.UnhookActivatedRebuildIdle();
                return;
            }

            if ((Control.MouseButtons & MouseButtons.Left) != 0)
            {
                if (!this.activatedRebuildMouseDefer)
                {
                    this.activatedRebuildMouseDefer = true;
                    this.QueueActivatedRebuild();
                    return;
                }

                this.HookActivatedRebuildIdle();
                return;
            }

            this.activatedRebuildMouseDefer = false;
            this.UnhookActivatedRebuildIdle();
            this.tree.Rebuild();
            this.RefreshWorkspaceTypes();
        }

        private void HookActivatedRebuildIdle()
        {
            if (this.activatedRebuildIdleHooked)
            {
                return;
            }

            this.activatedRebuildIdleHooked = true;
            Application.Idle += this.OnActivatedRebuildIdle;
        }

        private void UnhookActivatedRebuildIdle()
        {
            if (!this.activatedRebuildIdleHooked)
            {
                return;
            }

            this.activatedRebuildIdleHooked = false;
            Application.Idle -= this.OnActivatedRebuildIdle;
        }

        private void OnActivatedRebuildIdle(object sender, EventArgs e)
        {
            if ((Control.MouseButtons & MouseButtons.Left) != 0)
            {
                return;
            }

            this.UnhookActivatedRebuildIdle();
            this.activatedRebuildMouseDefer = false;
            if (this.IsDisposed || !this.IsHandleCreated || this.workspace == null || this.tree == null)
            {
                return;
            }

            this.tree.Rebuild();
            this.RefreshWorkspaceTypes();
        }

        /// <summary>DPI 変更は base のみ。本文フォント再生成は TextView の Handle/AfterParent に任せる。</summary>
        protected override void OnDpiChanged(DpiChangedEventArgs e)
        {
            base.OnDpiChanged(e);
        }

        /// <summary>Ctrl+Tab など。</summary>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.Tab))
            {
                this.tabs.SelectNext();
                return true;
            }

            if (keyData == (Keys.Control | Keys.Shift | Keys.Tab))
            {
                this.tabs.SelectPrevious();
                return true;
            }

            if (keyData == (Keys.Control | Keys.O))
            {
                this.OpenFolder();
                return true;
            }

            if (keyData == (Keys.Control | Keys.N))
            {
                this.NewUntitled();
                return true;
            }

            if (keyData == (Keys.Control | Keys.S))
            {
                this.SaveCurrent(false);
                return true;
            }

            if (keyData == (Keys.Control | Keys.Shift | Keys.S))
            {
                this.SaveCurrent(true);
                return true;
            }

            if (keyData == (Keys.Control | Keys.W))
            {
                this.CloseCurrentTab();
                return true;
            }

            if ((this.editor != null && this.editor.IsComposing) || (this.findBar != null && this.findBar.IsComposing))
            {
                if (keyData == (Keys.Control | Keys.Z)
                    || keyData == (Keys.Control | Keys.Y)
                    || keyData == (Keys.Control | Keys.X)
                    || keyData == (Keys.Control | Keys.V)
                    || keyData == (Keys.Control | Keys.A)
                    || keyData == (Keys.Control | Keys.F)
                    || keyData == (Keys.Control | Keys.H)
                    || keyData == Keys.F3
                    || keyData == (Keys.Shift | Keys.F3)
                    || keyData == (Keys.Control | Keys.Shift | Keys.B))
                {
                    return false;
                }
            }

            if (this.findBar != null && this.findBar.ContainsFocus)
            {
                if (keyData == (Keys.Control | Keys.Z)
                    || keyData == (Keys.Control | Keys.Y)
                    || keyData == (Keys.Control | Keys.X)
                    || keyData == (Keys.Control | Keys.C)
                    || keyData == (Keys.Control | Keys.V)
                    || keyData == (Keys.Control | Keys.A))
                {
                    return false;
                }
            }

            if (keyData == (Keys.Control | Keys.F))
            {
                this.ExecuteFind();
                return true;
            }

            if (keyData == (Keys.Control | Keys.H))
            {
                this.ExecuteReplace();
                return true;
            }

            if (keyData == Keys.F3)
            {
                this.ExecuteFindNext();
                return true;
            }

            if (keyData == (Keys.Shift | Keys.F3))
            {
                this.ExecuteFindPrevious();
                return true;
            }

            if (keyData == Keys.Escape && this.findBar != null && this.findBar.Visible)
            {
                if ((this.editor != null && this.editor.IsComposing) || this.findBar.IsComposing)
                {
                    return false;
                }

                this.CloseFindBar();
                return true;
            }

            if (keyData == (Keys.Control | Keys.Shift | Keys.B))
            {
                this.StartManualBuild();
                return true;
            }

            if (keyData == (Keys.Control | Keys.Z))
            {
                this.editor.Undo();
                return true;
            }

            if (keyData == (Keys.Control | Keys.Y))
            {
                this.editor.Redo();
                return true;
            }

            if (keyData == (Keys.Control | Keys.C))
            {
                this.editor.Copy();
                return true;
            }

            if (keyData == (Keys.Control | Keys.X))
            {
                this.editor.Cut();
                return true;
            }

            if (keyData == (Keys.Control | Keys.V))
            {
                this.editor.Paste();
                return true;
            }

            if (keyData == (Keys.Control | Keys.A))
            {
                this.editor.SelectAll();
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void BuildMenu()
        {
            this.chromeRenderer = new DarkMenuRenderer();
            this.menu = new MenuStrip();
            this.menu.Renderer = this.chromeRenderer;
            this.menu.BackColor = Theme.Background;
            this.menu.ForeColor = Theme.Foreground;
            this.menu.Padding = new Padding(4, 2, 0, 2);
            this.menu.DpiChangedAfterParent += this.OnChromeDpiChangedAfterParent;

            ToolStripMenuItem file = this.CreateTop("ファイル(&F)");
            file.DropDownItems.Add(this.CreateItem("フォルダを開く(&O)", Keys.Control | Keys.O, this.OnOpenFolder));
            file.DropDownItems.Add(this.CreateItem("新規(&N)", Keys.Control | Keys.N, this.OnNew));
            file.DropDownItems.Add(new ToolStripSeparator());
            file.DropDownItems.Add(this.CreateItem("保存(&S)", Keys.Control | Keys.S, this.OnSave));
            file.DropDownItems.Add(this.CreateItem("名前を付けて保存(&A)", Keys.Control | Keys.Shift | Keys.S, this.OnSaveAs));
            file.DropDownItems.Add(this.CreateItem("すべて保存(&L)", Keys.None, this.OnSaveAll));
            file.DropDownItems.Add(new ToolStripSeparator());
            file.DropDownItems.Add(this.CreateItem("タブを閉じる(&C)", Keys.Control | Keys.W, this.OnCloseTab));
            file.DropDownItems.Add(this.CreateItem("終了(&X)", Keys.None, this.OnExit));

            ToolStripMenuItem edit = this.CreateTop("編集(&E)");
            edit.DropDownItems.Add(this.CreateItem("元に戻す(&U)", Keys.Control | Keys.Z, this.OnUndo));
            edit.DropDownItems.Add(this.CreateItem("やり直し(&R)", Keys.Control | Keys.Y, this.OnRedo));
            edit.DropDownItems.Add(new ToolStripSeparator());
            edit.DropDownItems.Add(this.CreateItem("切り取り(&T)", Keys.Control | Keys.X, this.OnCut));
            edit.DropDownItems.Add(this.CreateItem("コピー(&C)", Keys.Control | Keys.C, this.OnCopy));
            edit.DropDownItems.Add(this.CreateItem("貼り付け(&P)", Keys.Control | Keys.V, this.OnPaste));
            edit.DropDownItems.Add(this.CreateItem("すべて選択(&A)", Keys.Control | Keys.A, this.OnSelectAll));
            edit.DropDownItems.Add(new ToolStripSeparator());
            edit.DropDownItems.Add(this.CreateItem("検索(&F)", Keys.Control | Keys.F, this.OnFind));
            edit.DropDownItems.Add(this.CreateItem("置換(&H)", Keys.Control | Keys.H, this.OnReplace));
            edit.DropDownItems.Add(this.CreateFindNavItem("次を検索(&N)", Keys.F3, "F3", this.OnFindNext));
            edit.DropDownItems.Add(this.CreateFindNavItem("前を検索(&B)", Keys.Shift | Keys.F3, "Shift+F3", this.OnFindPrevious));

            ToolStripMenuItem build = this.CreateTop("ビルド(&B)");
            build.DropDownItems.Add(this.CreateBuildItem("ビルド(&B)", this.OnBuild));

            ToolStripMenuItem help = this.CreateTop("ヘルプ(&H)");
            help.DropDownItems.Add(this.CreateItem("バージョン情報(&A)", Keys.None, this.OnAbout));

            this.menu.Items.Add(file);
            this.menu.Items.Add(edit);
            this.menu.Items.Add(build);
            this.menu.Items.Add(help);
            this.Controls.Add(this.menu);
        }

        private ToolStripMenuItem CreateTop(string text)
        {
            DualFontMenuItem item = new DualFontMenuItem(text);
            item.ForeColor = Theme.Foreground;
            item.BackColor = Theme.Background;
            item.DropDown.BackColor = Theme.Background;
            item.DropDown.ForeColor = Theme.Foreground;
            item.DropDown.Renderer = this.chromeRenderer;
            return item;
        }

        private ToolStripMenuItem CreateItem(string text, Keys shortcut, EventHandler handler)
        {
            DualFontMenuItem item = new DualFontMenuItem(text);
            item.ForeColor = Theme.Foreground;
            item.BackColor = Theme.Background;
            if (shortcut != Keys.None)
            {
                item.ShortcutKeys = shortcut;
            }

            item.Click += handler;
            return item;
        }

        private ToolStripMenuItem CreateFindNavItem(string text, Keys shortcut, string display, EventHandler handler)
        {
            DualFontMenuItem item = new DualFontMenuItem(text);
            item.ForeColor = Theme.Foreground;
            item.BackColor = Theme.Background;
            try
            {
                item.ShortcutKeys = shortcut;
            }
            catch (InvalidEnumArgumentException)
            {
                item.ShortcutKeyDisplayString = display;
            }

            if (item.ShortcutKeys == Keys.None)
            {
                item.ShortcutKeyDisplayString = display;
            }

            item.Click += handler;
            return item;
        }

        private ToolStripMenuItem CreateBuildItem(string text, EventHandler handler)
        {
            DualFontMenuItem item = new DualFontMenuItem(text);
            item.ForeColor = Theme.Foreground;
            item.BackColor = Theme.Background;
            item.ShortcutKeyDisplayString = "Ctrl+Shift+B";
            item.Click += handler;
            return item;
        }

        private void BuildStatus()
        {
            this.status = new StatusStrip();
            this.status.Renderer = this.chromeRenderer;
            this.status.BackColor = Theme.StatusBar;
            this.status.SizingGrip = false;
            this.statusBasePadding = this.status.Padding;
            this.status.DpiChangedAfterParent += this.OnChromeDpiChangedAfterParent;
            this.statusLang = this.CreateStatusLabel("プレーン", Theme.Foreground);
            this.statusPos = this.CreateStatusLabel("1:1", Theme.Foreground);
            this.statusEnc = this.CreateStatusLabel("UTF-8 BOM", Theme.Foreground);
            this.statusFont = this.CreateStatusLabel("", Theme.Foreground);
            this.statusFont.Spring = true;
            this.status.Items.Add(this.statusLang);
            this.status.Items.Add(this.CreateStatusLabel("  |  ", Theme.Comment));
            this.status.Items.Add(this.statusPos);
            this.status.Items.Add(this.CreateStatusLabel("  |  ", Theme.Comment));
            this.status.Items.Add(this.statusEnc);
            this.status.Items.Add(this.CreateStatusLabel("  |  ", Theme.Comment));
            this.status.Items.Add(this.statusFont);
            this.Controls.Add(this.status);
        }

        private ToolStripStatusLabel CreateStatusLabel(string text, Color color)
        {
            DualFontStatusLabel label = new DualFontStatusLabel(text);
            label.ForeColor = color;
            return label;
        }

        /// <summary>
        /// メニューとステータス用の 12 DIP 双フォントを作り直す。本文 fontSize には連動しない。
        /// FindBar のラベル／ボタンも同じ 12 DIP。検索欄の本文サイズは ApplyEditorSettings。
        /// </summary>
        private void RecreateChromeFonts()
        {
            if (this.fonts == null || this.chromeRenderer == null || this.menu == null || this.status == null)
            {
                return;
            }

            int dpi = DpiUtil.GetDpi(this.IsHandleCreated ? this.Handle : IntPtr.Zero);
            int px = DpiUtil.ToPixels(DpiUtil.UiFontDip, dpi);
            Font newHalf = this.fonts.CreateHalfWidth(px);
            Font newFull = this.fonts.CreateFullWidth(px);
            Font oldHalf = this.chromeHalfFont;
            Font oldFull = this.chromeFullFont;
            this.chromeHalfFont = newHalf;
            this.chromeFullFont = newFull;
            this.chromeRenderer.SetFonts(newHalf, newFull);
            this.menu.Font = newHalf;
            this.status.Font = newHalf;
            if (this.findBar != null)
            {
                this.findBar.SetFonts(newHalf, newFull);
            }

            if (this.problemList != null)
            {
                this.problemList.SetFonts(newHalf, newFull);
            }

            int extraTop = 0;
            int extraBottom = 0;
            if (newFull.Height > newHalf.Height)
            {
                int d = newFull.Height - newHalf.Height;
                extraTop = d / 2;
                extraBottom = d - extraTop;
            }

            this.menu.Padding = new Padding(4, 2 + extraTop, 0, 2 + extraBottom);
            Padding sp = this.statusBasePadding;
            this.status.Padding = new Padding(sp.Left, sp.Top + extraTop, sp.Right, sp.Bottom + extraBottom);

            if (oldHalf != null)
            {
                oldHalf.Dispose();
                if (object.ReferenceEquals(oldFull, oldHalf))
                {
                    oldFull = null;
                }
            }

            if (oldFull != null)
            {
                oldFull.Dispose();
            }

            this.menu.Invalidate();
            this.status.Invalidate();
            this.menu.PerformLayout();
            this.status.PerformLayout();
            if (this.findBar != null)
            {
                this.findBar.Invalidate();
                this.findBar.PerformLayout();
            }

            if (this.problemList != null)
            {
                this.problemList.Invalidate();
            }
        }

        private void OnChromeDpiChangedAfterParent(object sender, EventArgs e)
        {
            this.RecreateChromeFonts();
        }

        private void BuildBody()
        {
            this.bodySplit = new SplitContainer();
            this.bodySplit.Dock = DockStyle.Fill;
            this.bodySplit.Orientation = Orientation.Horizontal;
            this.bodySplit.BackColor = Theme.LineNumber;
            this.bodySplit.Panel1.BackColor = Theme.Background;
            this.bodySplit.Panel2.BackColor = Theme.Background;
            this.bodySplit.Panel2Collapsed = true;
            this.bodySplit.FixedPanel = FixedPanel.Panel2;

            this.split = new SplitContainer();
            this.split.Dock = DockStyle.Fill;
            this.split.BackColor = Theme.LineNumber;
            this.split.Panel1.BackColor = Theme.Background;
            this.split.Panel2.BackColor = Theme.EditorBackground;

            this.tree = new FileTreeControl();
            this.tree.Dock = DockStyle.Fill;
            this.tree.ApplyFonts(this.fonts);
            this.tree.FileOpenRequested += this.OnTreeOpen;
            this.split.Panel1.Controls.Add(this.tree);

            Panel right = new Panel();
            right.Dock = DockStyle.Fill;
            right.BackColor = Theme.EditorBackground;

            this.tabs = new TabStrip();
            this.tabs.Dock = DockStyle.Top;
            this.tabs.SelectedIndexChanged += this.OnTabChanged;
            this.tabs.TabCloseRequested += this.OnTabClose;
            this.tabs.FocusEditorRequested += this.OnTabFocusEditor;

            Panel editorColumn = new Panel();
            editorColumn.Dock = DockStyle.Fill;
            editorColumn.BackColor = Theme.EditorBackground;

            this.findBar = new FindBar();
            this.findBar.Dock = DockStyle.Top;
            this.findBar.Visible = false;
            this.findBar.QueryChanged += this.OnFindQueryChanged;
            this.findBar.FindNextRequested += this.OnFindNextRequested;
            this.findBar.FindPreviousRequested += this.OnFindPreviousRequested;
            this.findBar.ReplaceRequested += this.OnReplaceRequested;
            this.findBar.ReplaceAllRequested += this.OnReplaceAllRequested;
            this.findBar.CloseRequested += this.OnFindCloseRequested;

            this.editor = new TextView();
            this.editor.Dock = DockStyle.Fill;
            this.editor.CaretMoved += this.OnEditorCaret;
            this.editor.DocumentChanged += this.OnEditorChanged;

            editorColumn.Controls.Add(this.editor);
            editorColumn.Controls.Add(this.findBar);
            right.Controls.Add(editorColumn);
            right.Controls.Add(this.tabs);
            this.split.Panel2.Controls.Add(right);

            this.problemList = new ProblemListControl();
            this.problemList.Dock = DockStyle.Fill;
            this.problemList.CloseRequested += this.OnProblemListClose;
            this.problemList.ItemActivated += this.OnProblemActivated;

            this.bodySplit.Panel1.Controls.Add(this.split);
            this.bodySplit.Panel2.Controls.Add(this.problemList);
            this.Controls.Add(this.bodySplit);
            this.bodySplit.BringToFront();
        }

        /// <summary>
        /// 起動時の外枠・最小サイズ・左ペイン下限を DIP で決める。最初のハンドル作成時だけ確定する。
        /// </summary>
        private void ApplyWindowMetrics()
        {
            if (this.windowMetricsApplied)
            {
                return;
            }

            int dpi = DpiUtil.GetDpi(this.IsHandleCreated ? this.Handle : IntPtr.Zero);
            Rectangle wa;
            if (this.IsHandleCreated)
            {
                wa = Screen.FromHandle(this.Handle).WorkingArea;
            }
            else
            {
                wa = Screen.PrimaryScreen.WorkingArea;
            }

            int minW = DpiUtil.ToPixels(960, dpi);
            int minH = DpiUtil.ToPixels(600, dpi);
            if (minW > wa.Width)
            {
                minW = wa.Width;
            }

            if (minH > wa.Height)
            {
                minH = wa.Height;
            }

            int width = DpiUtil.ToPixels(1280, dpi);
            int height = DpiUtil.ToPixels(800, dpi);
            int maxW = wa.Width * 9 / 10;
            int maxH = wa.Height * 9 / 10;
            if (height > maxH)
            {
                height = maxH;
            }

            if (width > maxW)
            {
                width = maxW;
            }

            this.MinimumSize = new Size(minW, minH);
            this.Size = new Size(width, height);
            if (this.windowMetricsApplied)
            {
                return;
            }

            if (this.split != null)
            {
                int splitterW = DpiUtil.ToPixels(DpiUtil.SplitterWidthDip, dpi);
                if (splitterW < 1)
                {
                    splitterW = 1;
                }

                this.split.SplitterWidth = splitterW;
                int panel1Min = DpiUtil.ToPixels(160, dpi);
                int panel2Min = DpiUtil.ToPixels(320, dpi);
                int need = panel1Min + panel2Min + this.split.SplitterWidth;
                if (this.split.Width == 0 || this.split.Width >= need)
                {
                    this.split.Panel1MinSize = panel1Min;
                    this.split.Panel2MinSize = panel2Min;
                }
            }

            if (this.bodySplit != null)
            {
                int splitterH = DpiUtil.ToPixels(DpiUtil.SplitterWidthDip, dpi);
                if (splitterH < 1)
                {
                    splitterH = 1;
                }

                this.bodySplit.SplitterWidth = splitterH;
                int topMin = DpiUtil.ToPixels(240, dpi);
                int botMin = DpiUtil.ToPixels(80, dpi);
                int needH = topMin + botMin + this.bodySplit.SplitterWidth;
                if (this.bodySplit.Height == 0 || this.bodySplit.Height >= needH)
                {
                    this.bodySplit.Panel1MinSize = topMin;
                    this.bodySplit.Panel2MinSize = botMin;
                }
            }

            this.StartPosition = FormStartPosition.Manual;
            int x = wa.Left + (wa.Width - this.Width) / 2;
            int y = wa.Top + (wa.Height - this.Height) / 2;
            if (x < wa.Left)
            {
                x = wa.Left;
            }

            if (y < wa.Top)
            {
                y = wa.Top;
            }

            this.Location = new Point(x, y);
            if (this.IsHandleCreated)
            {
                this.windowMetricsApplied = true;
            }
        }

        private void ApplyEditorSettings(int fontSize, int tabSize)
        {
            this.editor.ApplyFonts(this.fonts, fontSize, tabSize);
            if (this.findBar != null)
            {
                this.findBar.SetEditorInputFont(this.fonts, fontSize);
            }
            if (this.fonts != null && this.fonts.UsedFallback)
            {
                this.statusFont.ForeColor = Theme.Error;
                this.statusFont.Text = this.fonts.ErrorMessage;
            }
            else if (this.fonts != null)
            {
                this.statusFont.ForeColor = Theme.Foreground;
                this.statusFont.Text = this.fonts.HalfWidthFamilyName + " / " + this.fonts.FullWidthFamilyName;
            }
        }

        private void NewUntitled()
        {
            Document doc = Document.CreateUntitled();
            this.tabs.AddTab(doc);
            this.AttachDocument(doc);
        }

        private void AttachDocument(Document doc)
        {
            this.ApplyWorkspaceToDocument(doc);
            this.editor.SaveViewState();
            this.editor.Document = doc;
            this.editor.RestoreViewState();
            this.tabs.RefreshTabs();
            this.UpdateStatus();
            this.editor.Focus();
            this.RefreshFindCount();
        }

        private void ApplyWorkspaceToDocument(Document doc)
        {
            if (doc == null || doc.HighlightSession == null)
            {
                return;
            }

            if (this.workspace != null)
            {
                doc.HighlightSession.WorkspaceRoot = this.workspace.RootPath;
                doc.HighlightSession.InvalidateFrom(0);
                doc.HighlightSession.SyncAfterEdit(doc.Buffer, 0);
            }
        }

        private void RefreshWorkspaceTypes()
        {
            WorkspaceTypeNames.Invalidate();
            if (this.tabs == null)
            {
                return;
            }

            for (int i = 0; i < this.tabs.Tabs.Count; i++)
            {
                this.ApplyWorkspaceToDocument(this.tabs.Tabs[i]);
            }
        }

        private void RefreshWorkspaceTypesAfterCsSave(Document doc)
        {
            if (doc == null || string.IsNullOrEmpty(doc.FilePath))
            {
                return;
            }

            if (!string.Equals(Path.GetExtension(doc.FilePath), ".cs", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            this.RefreshWorkspaceTypes();
        }

        /// <summary>
        /// 起動引数を開く。ファイルが 1 件以上成功したら無題は作らない。失敗は Warning 1 枚。
        /// </summary>
        /// <param name="startupArgs">Main の引数。</param>
        private void ApplyStartup(string[] startupArgs)
        {
            StartupPlan plan = StartupArgs.Parse(startupArgs);
            StringBuilder warnings = new StringBuilder();
            this.AppendStartupFailures(warnings, plan);

            if (!string.IsNullOrEmpty(plan.WorkspaceRoot))
            {
                string bindError;
                if (!this.TryBindWorkspace(plan.WorkspaceRoot, out bindError))
                {
                    this.AppendWarning(warnings, plan.WorkspaceRoot, bindError);
                }
            }

            int opened = 0;
            for (int i = 0; i < plan.Files.Length; i++)
            {
                string openError;
                if (this.TryOpenFile(plan.Files[i], false, out openError))
                {
                    opened++;
                }
                else
                {
                    this.AppendWarning(warnings, plan.Files[i], openError);
                }
            }

            if (opened == 0)
            {
                this.NewUntitled();
            }

            if (warnings.Length > 0)
            {
                MessageBox.Show(this, warnings.ToString(), "WindowsIDE", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void AppendStartupFailures(StringBuilder warnings, StartupPlan plan)
        {
            if (plan == null || plan.Failures == null)
            {
                return;
            }

            for (int i = 0; i < plan.Failures.Length; i++)
            {
                StartupFailure item = plan.Failures[i];
                string path = (item == null) ? null : item.Path;
                string reason = (item == null) ? null : item.Reason;
                this.AppendWarning(warnings, path, reason);
            }
        }

        private void AppendWarning(StringBuilder warnings, string path, string reason)
        {
            if (warnings == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(path) && string.IsNullOrEmpty(reason))
            {
                return;
            }

            if (warnings.Length > 0)
            {
                warnings.AppendLine();
            }

            if (string.IsNullOrEmpty(path))
            {
                warnings.Append(reason);
            }
            else if (string.IsNullOrEmpty(reason))
            {
                warnings.Append(path);
            }
            else
            {
                warnings.Append(path);
                warnings.Append(": ");
                warnings.Append(reason);
            }
        }

        /// <summary>
        /// フォルダをワークスペースとして開き、ツリーに束ねる。
        /// </summary>
        /// <param name="path">フォルダパス。</param>
        /// <param name="error">失敗理由。</param>
        /// <returns>成功なら true。</returns>
        private bool TryBindWorkspace(string path, out string error)
        {
            error = null;
            try
            {
                this.workspace = WorkspaceFolder.Open(path);
                this.tree.BindWorkspace(this.workspace.RootPath);
                this.ApplyEditorSettings(this.workspace.Settings.FontSize, this.workspace.Settings.TabSize);
                this.Text = "WindowsIDE - " + this.workspace.RootPath;
                WorkspaceTypeNames.Invalidate();
                if (this.editor != null)
                {
                    this.ApplyWorkspaceToDocument(this.editor.Document);
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// 既存ファイルをタブで開く。既に開いていればそのタブを表示する。requireInsideWorkspace なら Contains（PathGuard）で拒否する。
        /// </summary>
        /// <param name="path">開くパス。</param>
        /// <param name="requireInsideWorkspace">true ならワークスペース外を拒否する。</param>
        /// <param name="error">失敗理由。拒否は null。</param>
        /// <returns>開いた、または既存タブを表示したら true。</returns>
        private bool TryOpenFile(string path, bool requireInsideWorkspace, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(path))
            {
                error = "空のパスです。";
                return false;
            }

            if (requireInsideWorkspace && this.workspace != null && !this.workspace.Contains(path))
            {
                return false;
            }

            for (int i = 0; i < this.tabs.Tabs.Count; i++)
            {
                if (this.tabs.Tabs[i].FilePath != null
                    && string.Equals(this.tabs.Tabs[i].FilePath, path, StringComparison.OrdinalIgnoreCase))
                {
                    this.tabs.SelectedIndex = i;
                    this.AttachDocument(this.tabs.Tabs[i]);
                    return true;
                }
            }

            try
            {
                Document doc = Document.Open(path);
                this.tabs.AddTab(doc);
                this.AttachDocument(doc);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private void OpenFolder()
        {
            string initial = null;
            if (this.workspace != null)
            {
                initial = this.workspace.RootPath;
            }

            string path;
            try
            {
                if (!CommonItemDialog.TryPickFolder(this, "ワークスペースにするフォルダを選ぶ", initial, out path))
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "WindowsIDE", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string error;
            if (!this.TryBindWorkspace(path, out error))
            {
                MessageBox.Show(this, error, "WindowsIDE", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnTreeOpen(object sender, EventArgs e)
        {
            string path = this.tree.SelectedFilePath;
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            string error;
            if (!this.TryOpenFile(path, true, out error) && !string.IsNullOrEmpty(error))
            {
                MessageBox.Show(this, error, "WindowsIDE", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void OnTabChanged(object sender, EventArgs e)
        {
            if (this.rebuilding)
            {
                return;
            }

            Document doc = this.tabs.SelectedDocument;
            this.AttachDocument(doc);
        }

        private void OnTabFocusEditor(object sender, EventArgs e)
        {
            if (this.editor != null)
            {
                this.editor.Focus();
            }
        }

        private void OnTabClose(object sender, TabCloseEventArgs e)
        {
            this.CloseTabAt(e.Index);
        }

        private void CloseCurrentTab()
        {
            if (this.tabs.SelectedIndex >= 0)
            {
                this.CloseTabAt(this.tabs.SelectedIndex);
            }
        }

        private bool CloseTabAt(int index)
        {
            if (index < 0 || index >= this.tabs.Tabs.Count)
            {
                return true;
            }

            Document doc = this.tabs.Tabs[index];
            if (doc.IsDirty)
            {
                DialogResult r = MessageBox.Show(this, doc.DisplayName + " を保存しますか?", "WindowsIDE", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (r == DialogResult.Cancel)
                {
                    return false;
                }

                if (r == DialogResult.Yes)
                {
                    this.tabs.SelectedIndex = index;
                    if (!this.SaveCurrent(false))
                    {
                        return false;
                    }
                }
            }

            this.rebuilding = true;
            this.tabs.RemoveAt(index);
            this.rebuilding = false;
            if (this.tabs.Tabs.Count == 0)
            {
                if (!this.suppressNewUntitled)
                {
                    this.NewUntitled();
                }
            }
            else
            {
                this.AttachDocument(this.tabs.SelectedDocument);
            }

            return true;
        }

        private bool SaveCurrent(bool saveAs)
        {
            Document doc = this.tabs.SelectedDocument;
            if (doc == null)
            {
                return false;
            }

            if (saveAs || string.IsNullOrEmpty(doc.FilePath))
            {
                string initialDir = null;
                if (this.workspace != null)
                {
                    initialDir = this.workspace.RootPath;
                }

                string path;
                try
                {
                    if (!CommonItemDialog.TryPickSaveFile(
                        this,
                        "名前を付けて保存",
                        initialDir,
                        doc.DisplayName,
                        "すべてのファイル (*.*)|*.*|C# (*.cs)|*.cs",
                        out path))
                    {
                        return false;
                    }

                    doc.SaveAs(path);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, "WindowsIDE", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }
            }
            else
            {
                try
                {
                    doc.Save();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, "WindowsIDE", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }
            }

            this.EnsureWorkspaceXml();
            this.RefreshWorkspaceTypesAfterCsSave(doc);
            this.tabs.RefreshTabs();
            this.UpdateStatus();
            return true;
        }

        private void SaveAll()
        {
            int current = this.tabs.SelectedIndex;
            for (int i = 0; i < this.tabs.Tabs.Count; i++)
            {
                this.tabs.SelectedIndex = i;
                if (this.tabs.Tabs[i].IsDirty)
                {
                    if (!this.SaveCurrent(false))
                    {
                        return;
                    }
                }
            }

            if (current >= 0)
            {
                this.tabs.SelectedIndex = current;
            }
        }

        private void EnsureWorkspaceXml()
        {
            if (this.workspace == null)
            {
                return;
            }

            string path = WorkspaceSettings.GetFilePath(this.workspace.RootPath);
            if (!File.Exists(path))
            {
                this.workspace.Settings.Save(this.workspace.RootPath);
            }
        }

        private void UpdateStatus()
        {
            Document doc = (this.editor == null) ? null : this.editor.Document;
            if (doc == null)
            {
                this.statusLang.Text = LanguageDetector.GetDisplayName(LanguageKind.Plain);
                this.statusPos.Text = "1:1";
                this.statusEnc.Text = "";
                return;
            }

            this.statusLang.Text = LanguageDetector.GetDisplayName(doc.Language);
            this.statusPos.Text = (doc.CaretLine + 1).ToString() + ":" + (doc.CaretColumn + 1).ToString();
            this.statusEnc.Text = doc.EncodingInfo.GetDisplayName();
            this.tabs.RefreshTabs();
        }

        private void OnOpenFolder(object sender, EventArgs e) { this.OpenFolder(); }
        private void OnNew(object sender, EventArgs e) { this.NewUntitled(); }
        private void OnSave(object sender, EventArgs e) { this.SaveCurrent(false); }
        private void OnSaveAs(object sender, EventArgs e) { this.SaveCurrent(true); }
        private void OnSaveAll(object sender, EventArgs e) { this.SaveAll(); }
        private void OnCloseTab(object sender, EventArgs e) { this.CloseCurrentTab(); }
        private void OnExit(object sender, EventArgs e) { this.Close(); }
        private void OnUndo(object sender, EventArgs e) { this.editor.Undo(); }
        private void OnRedo(object sender, EventArgs e) { this.editor.Redo(); }
        private void OnCut(object sender, EventArgs e) { this.editor.Cut(); }
        private void OnCopy(object sender, EventArgs e) { this.editor.Copy(); }
        private void OnPaste(object sender, EventArgs e) { this.editor.Paste(); }
        private void OnSelectAll(object sender, EventArgs e) { this.editor.SelectAll(); }
        private void OnFind(object sender, EventArgs e) { this.ExecuteFind(); }
        private void OnReplace(object sender, EventArgs e) { this.ExecuteReplace(); }
        private void OnFindNext(object sender, EventArgs e) { this.ExecuteFindNext(); }
        private void OnFindPrevious(object sender, EventArgs e) { this.ExecuteFindPrevious(); }
        private void OnFindQueryChanged(object sender, EventArgs e) { this.RefreshFindCount(); }
        private void OnFindNextRequested(object sender, EventArgs e) { this.ExecuteFindNext(); }
        private void OnFindPreviousRequested(object sender, EventArgs e) { this.ExecuteFindPrevious(); }
        private void OnReplaceRequested(object sender, EventArgs e) { this.ExecuteReplaceOne(); }
        private void OnReplaceAllRequested(object sender, EventArgs e) { this.ExecuteReplaceAll(); }
        private void OnFindCloseRequested(object sender, EventArgs e) { this.CloseFindBar(); }
        private void OnBuild(object sender, EventArgs e) { this.StartManualBuild(); }

        private void StartManualBuild()
        {
            string workspaceRoot = (this.workspace == null) ? null : this.workspace.RootPath;
            string focused = null;
            if (this.editor != null && this.editor.Document != null)
            {
                focused = this.editor.Document.FilePath;
            }

            string[] sources = CompileUnit.Resolve(workspaceRoot, focused);
            if (sources == null || sources.Length == 0)
            {
                this.ShowSynthetic("C# ソースがありません。");
                return;
            }

            string saveError;
            if (!this.TrySaveDirtySources(sources, out saveError))
            {
                this.ShowSynthetic(saveError);
                return;
            }

            if (!FrameworkCsc.CompilerExists())
            {
                this.ShowSynthetic("csc.exe が見つかりません。");
                return;
            }

            string keyPath = workspaceRoot;
            if (string.IsNullOrEmpty(keyPath))
            {
                keyPath = sources[0];
            }

            string outputExe;
            string rspPath;
            try
            {
                outputExe = CscArgumentBuilder.GetOutputExePath(keyPath);
                rspPath = Path.Combine(Path.GetDirectoryName(outputExe), "csc.rsp");
                CscArgumentBuilder.WriteResponseFile(rspPath, outputExe, sources);
            }
            catch (Exception ex)
            {
                this.ShowSynthetic("応答ファイルの作成に失敗した: " + ex.Message);
                return;
            }

            this.buildGeneration++;
            int gen = this.buildGeneration;
            if (this.cscRunner != null)
            {
                this.cscRunner.Kill();
            }

            ManualBuildRequest req = new ManualBuildRequest();
            req.Generation = gen;
            req.RspPath = rspPath;
            req.OutputExe = outputExe;
            Thread thread = new Thread(this.BuildWorkerProc);
            thread.IsBackground = true;
            thread.Start(req);
        }

        private void BuildWorkerProc(object state)
        {
            ManualBuildRequest req = state as ManualBuildRequest;
            if (req == null || this.cscRunner == null)
            {
                return;
            }

            CscRunResult result = this.cscRunner.Run(req.RspPath, req.Generation);
            if (this.IsDisposed || !this.IsHandleCreated)
            {
                return;
            }

            this.BeginInvoke(new MethodInvoker(delegate
            {
                this.OnBuildFinished(req, result);
            }));
        }

        private void OnBuildFinished(ManualBuildRequest req, CscRunResult result)
        {
            if (this.IsDisposed || req == null || result == null)
            {
                return;
            }

            if (req.Generation != this.buildGeneration || result.Generation != this.buildGeneration)
            {
                return;
            }

            if (!string.IsNullOrEmpty(result.StartError))
            {
                this.ShowSynthetic(result.StartError);
                return;
            }

            string combined = result.CombinedOutput();
            Diagnostic[] parsed = DiagnosticParser.ApplyExitCode(DiagnosticParser.Parse(combined), result.ExitCode, combined);
            if (result.ExitCode == 0)
            {
                this.lastManualOutputExe = req.OutputExe;
            }

            this.ShowBuildDiagnostics(parsed);
        }

        private bool TrySaveDirtySources(string[] sources, out string error)
        {
            error = null;
            if (sources == null || this.tabs == null)
            {
                return true;
            }

            HashSet<string> set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < sources.Length; i++)
            {
                if (string.IsNullOrEmpty(sources[i]))
                {
                    continue;
                }

                try
                {
                    set.Add(Path.GetFullPath(sources[i]));
                }
                catch (Exception)
                {
                }
            }

            bool savedCs = false;
            for (int i = 0; i < this.tabs.Tabs.Count; i++)
            {
                Document doc = this.tabs.Tabs[i];
                if (doc == null || string.IsNullOrEmpty(doc.FilePath) || !doc.IsDirty)
                {
                    continue;
                }

                string full;
                try
                {
                    full = Path.GetFullPath(doc.FilePath);
                }
                catch (Exception)
                {
                    continue;
                }

                if (!set.Contains(full))
                {
                    continue;
                }

                try
                {
                    if (!doc.Save())
                    {
                        error = "保存に失敗した: " + doc.FilePath;
                        return false;
                    }

                    if (string.Equals(Path.GetExtension(doc.FilePath), ".cs", StringComparison.OrdinalIgnoreCase))
                    {
                        savedCs = true;
                    }
                }
                catch (Exception ex)
                {
                    error = "保存に失敗した: " + ex.Message;
                    return false;
                }
            }

            if (savedCs)
            {
                this.RefreshWorkspaceTypes();
            }

            this.tabs.RefreshTabs();
            this.UpdateStatus();
            return true;
        }

        private void ShowSynthetic(string message)
        {
            this.ShowBuildDiagnostics(new Diagnostic[] { Diagnostic.CreateSynthetic(message) });
        }

        private void ShowBuildDiagnostics(Diagnostic[] list)
        {
            this.ShowProblemPanel();
            if (this.problemList != null)
            {
                this.problemList.SetItems(list);
            }
        }

        private void ShowProblemPanel()
        {
            if (this.bodySplit == null)
            {
                return;
            }

            this.bodySplit.Panel2Collapsed = false;
            if (this.bottomSplitterInitialized)
            {
                return;
            }

            int dpi = DpiUtil.GetDpi(this.IsHandleCreated ? this.Handle : IntPtr.Zero);
            int bottom = DpiUtil.ToPixels(180, dpi);
            int splitter = this.bodySplit.SplitterWidth;
            int min1 = this.bodySplit.Panel1MinSize;
            int avail = this.bodySplit.Height;
            int distance = avail - bottom - splitter;
            if (distance < min1)
            {
                distance = min1;
            }

            if (distance > 0 && avail > distance + splitter)
            {
                this.bodySplit.SplitterDistance = distance;
            }

            this.bottomSplitterInitialized = true;
        }

        private void OnProblemListClose(object sender, EventArgs e)
        {
            if (this.bodySplit != null)
            {
                this.bodySplit.Panel2Collapsed = true;
            }
        }

        private void OnProblemActivated(object sender, ProblemActivatedEventArgs e)
        {
            if (e == null || e.Diagnostic == null || string.IsNullOrEmpty(e.Diagnostic.FilePath))
            {
                return;
            }

            if (!File.Exists(e.Diagnostic.FilePath))
            {
                return;
            }

            string openError;
            if (!this.TryOpenFile(e.Diagnostic.FilePath, false, out openError))
            {
                return;
            }

            if (this.editor == null || this.editor.Document == null || this.editor.Document.Buffer == null)
            {
                return;
            }

            if (e.Diagnostic.Line < 1)
            {
                this.editor.Focus();
                return;
            }

            int line0 = e.Diagnostic.Line - 1;
            int col0 = 0;
            if (e.Diagnostic.Column >= 1)
            {
                col0 = e.Diagnostic.Column - 1;
            }

            BufferPoint start = this.editor.Document.Buffer.Clamp(new BufferPoint(line0, col0));
            int endCol = this.editor.Document.Buffer.GetLineLength(start.Line);
            this.editor.SelectRange(start, new BufferPoint(start.Line, endCol));
            this.editor.Focus();
        }

        private void ExecuteFind()
        {
            if (this.findBar == null || this.editor == null)
            {
                return;
            }

            if (this.editor.IsComposing)
            {
                return;
            }

            if (this.findBar.Visible && this.findBar.FindBoxContainsFocus)
            {
                this.findBar.SelectFindBoxAll();
                return;
            }

            string seed = this.TryGetFindSeed();
            if (seed != null && seed.Length > 0)
            {
                this.findBar.Query = FindRules.NormalizeQuery(seed);
            }

            this.findBar.ShowFindRow();
            this.findBar.FocusFindBox();
            this.findBar.SelectFindBoxAll();
            this.RefreshFindCount();
        }

        private void ExecuteReplace()
        {
            if (this.findBar == null || this.editor == null)
            {
                return;
            }

            if (this.editor.IsComposing)
            {
                return;
            }

            this.findBar.ShowReplaceRow();
            this.findBar.FocusReplaceBox();
            this.RefreshFindCount();
        }

        private void ExecuteFindNext()
        {
            if (this.findBar == null || this.editor == null)
            {
                return;
            }

            if (this.editor.IsComposing)
            {
                return;
            }

            string query = FindRules.NormalizeQuery(this.findBar.Query);
            if (query.Length == 0)
            {
                this.ExecuteFind();
                return;
            }

            bool keepEditorFocus = !this.findBar.ContainsFocus;
            if (!this.findBar.Visible)
            {
                this.findBar.ShowPreservingReplaceRow();
                keepEditorFocus = true;
            }

            this.editor.FindNext(query, this.findBar.IgnoreCase, true);
            if (keepEditorFocus)
            {
                this.editor.Focus();
            }

            this.RefreshFindCount();
        }

        private void ExecuteFindPrevious()
        {
            if (this.findBar == null || this.editor == null)
            {
                return;
            }

            if (this.editor.IsComposing)
            {
                return;
            }

            string query = FindRules.NormalizeQuery(this.findBar.Query);
            if (query.Length == 0)
            {
                this.ExecuteFind();
                return;
            }

            bool keepEditorFocus = !this.findBar.ContainsFocus;
            if (!this.findBar.Visible)
            {
                this.findBar.ShowPreservingReplaceRow();
                keepEditorFocus = true;
            }

            this.editor.FindPrevious(query, this.findBar.IgnoreCase, true);
            if (keepEditorFocus)
            {
                this.editor.Focus();
            }

            this.RefreshFindCount();
        }

        private void ExecuteReplaceOne()
        {
            if (this.findBar == null || this.editor == null || this.editor.Document == null)
            {
                return;
            }

            if (this.editor.IsComposing)
            {
                return;
            }

            string query = FindRules.NormalizeQuery(this.findBar.Query);
            if (query.Length == 0)
            {
                return;
            }

            string replacement = this.findBar.Replacement;
            if (replacement == null)
            {
                replacement = "";
            }

            if (this.SelectionMatchesQuery(query, this.findBar.IgnoreCase))
            {
                this.editor.ReplaceSelection(replacement);
            }

            this.editor.FindNext(query, this.findBar.IgnoreCase, true);
            this.RefreshFindCount();
        }

        private void ExecuteReplaceAll()
        {
            if (this.findBar == null || this.editor == null)
            {
                return;
            }

            if (this.editor.IsComposing)
            {
                return;
            }

            string query = FindRules.NormalizeQuery(this.findBar.Query);
            string replacement = this.findBar.Replacement;
            this.editor.ReplaceAll(query, replacement, this.findBar.IgnoreCase);
            this.RefreshFindCount();
        }

        private void CloseFindBar()
        {
            if (this.findBar != null)
            {
                this.findBar.HideBar();
            }

            if (this.editor != null)
            {
                this.editor.Focus();
            }
        }

        private void RefreshFindCount()
        {
            if (this.findBar == null || !this.findBar.Visible)
            {
                return;
            }

            string query = FindRules.NormalizeQuery(this.findBar.Query);
            if (query.Length == 0)
            {
                this.findBar.SetMatchCount(0, true);
                return;
            }

            if (this.editor == null || this.editor.Document == null)
            {
                this.findBar.SetMatchCount(0, false);
                return;
            }

            int n = FindRules.Count(this.editor.Document.Buffer, query, this.findBar.IgnoreCase);
            this.findBar.SetMatchCount(n, false);
        }

        private string TryGetFindSeed()
        {
            if (this.editor == null || this.editor.Document == null || !this.editor.Document.HasSelection())
            {
                return null;
            }

            BufferPoint a;
            BufferPoint b;
            this.editor.Document.GetSelection(out a, out b);
            if (a.Line != b.Line)
            {
                return null;
            }

            return this.editor.Document.Buffer.GetText(a, b);
        }

        private bool SelectionMatchesQuery(string query, bool ignoreCase)
        {
            if (this.editor == null || this.editor.Document == null || !this.editor.Document.HasSelection())
            {
                return false;
            }

            BufferPoint a;
            BufferPoint b;
            this.editor.Document.GetSelection(out a, out b);
            string selected = this.editor.Document.Buffer.GetText(a, b);
            StringComparison comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            return string.Equals(selected, query, comparison);
        }

        private void OnAbout(object sender, EventArgs e)
        {
            using (AboutForm about = new AboutForm())
            {
                about.ShowDialog(this);
            }
        }

        private void OnEditorCaret(object sender, EventArgs e)
        {
            this.UpdateStatus();
        }

        private void OnEditorChanged(object sender, EventArgs e)
        {
            this.tabs.RefreshTabs();
            this.UpdateStatus();
            this.RefreshFindCount();
        }

        /// <summary>クロム用 Font を破棄する。Renderer は所有しない。</summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this.cscRunner != null)
                {
                    this.cscRunner.Kill();
                }

                if (this.menu != null)
                {
                    this.menu.DpiChangedAfterParent -= this.OnChromeDpiChangedAfterParent;
                }

                if (this.status != null)
                {
                    this.status.DpiChangedAfterParent -= this.OnChromeDpiChangedAfterParent;
                }

                if (this.chromeRenderer != null)
                {
                    this.chromeRenderer.SetFonts(null, null);
                }
            }

            base.Dispose(disposing);

            if (disposing)
            {
                if (this.chromeHalfFont != null)
                {
                    this.chromeHalfFont.Dispose();
                    if (object.ReferenceEquals(this.chromeFullFont, this.chromeHalfFont))
                    {
                        this.chromeFullFont = null;
                    }

                    this.chromeHalfFont = null;
                }

                if (this.chromeFullFont != null)
                {
                    this.chromeFullFont.Dispose();
                    this.chromeFullFont = null;
                }
            }
        }

        /// <summary>未保存があれば確認する。</summary>
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            this.suppressNewUntitled = true;
            while (this.tabs.Tabs.Count > 0)
            {
                if (!this.CloseTabAt(0))
                {
                    this.suppressNewUntitled = false;
                    e.Cancel = true;
                    return;
                }
            }
        }

        private sealed class ManualBuildRequest
        {
            public int Generation;
            public string RspPath;
            public string OutputExe;
        }
    }
}
