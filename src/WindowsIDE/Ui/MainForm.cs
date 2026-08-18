using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using WindowsIDE.Editor;
using WindowsIDE.Languages;
using WindowsIDE.Ui.Fonts;
using WindowsIDE.Workspace;

namespace WindowsIDE.Ui
{
    /// <summary>
    /// P0 メイン枠。メニュー、ツリー、タブ、編集器、ステータス。下パネルは無い。
    /// </summary>
    public sealed class MainForm : Form
    {
        private readonly FontLoadResult fonts;
        private WorkspaceFolder workspace;
        private MenuStrip menu;
        private SplitContainer split;
        private FileTreeControl tree;
        private TabStrip tabs;
        private TextView editor;
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

            this.BuildMenu();
            this.BuildStatus();
            this.BuildBody();
            this.MainMenuStrip = this.menu;

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

            if (this.editor != null && this.editor.IsComposing)
            {
                if (keyData == (Keys.Control | Keys.Z)
                    || keyData == (Keys.Control | Keys.Y)
                    || keyData == (Keys.Control | Keys.X)
                    || keyData == (Keys.Control | Keys.V)
                    || keyData == (Keys.Control | Keys.A))
                {
                    return false;
                }
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
            this.menu = new MenuStrip();
            this.menu.Renderer = new DarkMenuRenderer();
            this.menu.BackColor = Theme.Background;
            this.menu.ForeColor = Theme.Foreground;
            this.menu.Padding = new Padding(4, 2, 0, 2);

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

            ToolStripMenuItem help = this.CreateTop("ヘルプ(&H)");
            help.DropDownItems.Add(this.CreateItem("バージョン情報(&A)", Keys.None, this.OnAbout));

            this.menu.Items.Add(file);
            this.menu.Items.Add(edit);
            this.menu.Items.Add(help);
            this.Controls.Add(this.menu);
        }

        private ToolStripMenuItem CreateTop(string text)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(text);
            item.ForeColor = Theme.Foreground;
            item.BackColor = Theme.Background;
            item.DropDown.BackColor = Theme.Background;
            item.DropDown.ForeColor = Theme.Foreground;
            return item;
        }

        private ToolStripMenuItem CreateItem(string text, Keys shortcut, EventHandler handler)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(text);
            item.ForeColor = Theme.Foreground;
            item.BackColor = Theme.Background;
            if (shortcut != Keys.None)
            {
                item.ShortcutKeys = shortcut;
            }

            item.Click += handler;
            return item;
        }

        private void BuildStatus()
        {
            this.status = new StatusStrip();
            this.status.Renderer = new DarkMenuRenderer();
            this.status.BackColor = Theme.StatusBar;
            this.status.SizingGrip = false;
            this.statusLang = new ToolStripStatusLabel("プレーン");
            this.statusPos = new ToolStripStatusLabel("1:1");
            this.statusEnc = new ToolStripStatusLabel("UTF-8 BOM");
            this.statusFont = new ToolStripStatusLabel("");
            this.statusLang.ForeColor = Theme.Foreground;
            this.statusPos.ForeColor = Theme.Foreground;
            this.statusEnc.ForeColor = Theme.Foreground;
            this.statusFont.ForeColor = Theme.Foreground;
            this.statusFont.Spring = true;
            this.status.Items.Add(this.statusLang);
            this.status.Items.Add(new ToolStripStatusLabel("  |  ") { ForeColor = Theme.Comment });
            this.status.Items.Add(this.statusPos);
            this.status.Items.Add(new ToolStripStatusLabel("  |  ") { ForeColor = Theme.Comment });
            this.status.Items.Add(this.statusEnc);
            this.status.Items.Add(new ToolStripStatusLabel("  |  ") { ForeColor = Theme.Comment });
            this.status.Items.Add(this.statusFont);
            this.Controls.Add(this.status);
        }

        private void BuildBody()
        {
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

            this.editor = new TextView();
            this.editor.Dock = DockStyle.Fill;
            this.editor.CaretMoved += this.OnEditorCaret;
            this.editor.DocumentChanged += this.OnEditorChanged;

            right.Controls.Add(this.editor);
            right.Controls.Add(this.tabs);
            this.split.Panel2.Controls.Add(right);
            this.Controls.Add(this.split);
            this.split.BringToFront();
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
            this.editor.SaveViewState();
            this.editor.Document = doc;
            this.editor.RestoreViewState();
            this.tabs.RefreshTabs();
            this.UpdateStatus();
            this.editor.Focus();
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
    }
}
