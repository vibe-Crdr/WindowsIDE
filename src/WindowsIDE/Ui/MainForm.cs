using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using WindowsIDE.Build;
using WindowsIDE.Debug;
using WindowsIDE.Editor;
using WindowsIDE.Host.Cmd;
using WindowsIDE.Host.Csharp;
using WindowsIDE.Host.PowerShell;
using WindowsIDE.Languages;
using WindowsIDE.Languages.CSharp;
using WindowsIDE.Terminal;
using WindowsIDE.Ui.Fonts;
using WindowsIDE.Vba;
using WindowsIDE.Workspace;

namespace WindowsIDE.Ui
{
    /// <summary>
    /// メイン枠。メニュー、ツリー、タブ、編集器、下パネル（問題 / 出力 / ターミナル / デバッグ）、ステータス。起動時は下パネルを畳む。起動時は左ペインも畳む。
    /// </summary>
    public sealed class MainForm : Form
    {
        private readonly FontLoadResult fonts;
        private WorkspaceFolder workspace;
        private DarkMenuRenderer chromeRenderer;
        private Font chromeHalfFont;
        private Font chromeFullFont;
        private MenuStrip menu;
        private SplitContainer bodySplit;
        private SplitContainer split;
        private FileTreeControl tree;
        private FileTreeCreateBar treeCreateBar;
        private TabStrip tabs;
        private FindBar findBar;
        private TextView editor;
        private BottomPane bottomPane;
        private CscRunner cscRunner;
        private CscRunner liveCscRunner;
        private System.Windows.Forms.Timer liveTimer;
        private const int LiveDiagnoseDebounceMs = 600;
        private CsharpProcessHost csharpHost;
        private PowerShellProcessHost powershellHost;
        private CmdProcessHost cmdHost;
        private ToolStripMenuItem viewTerminalItem;
        private ToolStripMenuItem viewPowerShellItem;
        private ToolStripMenuItem viewCmdItem;
        private ToolStripMenuItem vbaNameFilenameItem;
        private ToolStripMenuItem vbaNameFolderPrefixItem;
        private int buildGeneration;
        private int liveGeneration;
        private int csharpDiagSeq;
        private Diagnostic[] csharpBucket;
        private Diagnostic[] psBucket;
        private Diagnostic[] vbaBucket;
        private Diagnostic[] lastPublishedDiagnostics;
        private int runGeneration;
        private int debugGeneration;
        private ActiveRunKind activeRunKind;
        private BreakpointStore breakpointStore;
        private PowerShellDebugger psDebugger;
        private CorDebugSession corDebug;
        private string lastManualOutputExe;
        private bool pendingLaunchAfterBuild;
        private bool pendingDebugAfterBuild;
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
        private HoverInfoControl hoverInfo;
        private bool ctrlKPending;
        private DateTime ctrlKAt;

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
            this.liveCscRunner = new CscRunner();
            this.liveTimer = new System.Windows.Forms.Timer();
            this.liveTimer.Interval = LiveDiagnoseDebounceMs;
            this.liveTimer.Tick += this.OnLiveTick;
            this.lastPublishedDiagnostics = new Diagnostic[0];
            this.csharpHost = new CsharpProcessHost();
            this.csharpHost.LineReceived += this.OnCsharpLineReceived;
            this.csharpHost.Exited += this.OnCsharpExited;
            this.csharpHost.StartFailed += this.OnCsharpStartFailed;
            this.powershellHost = new PowerShellProcessHost();
            this.powershellHost.LineReceived += this.OnPowerShellLineReceived;
            this.powershellHost.Exited += this.OnPowerShellExited;
            this.powershellHost.StartFailed += this.OnPowerShellStartFailed;
            this.cmdHost = new CmdProcessHost();
            this.cmdHost.LineReceived += this.OnCmdLineReceived;
            this.cmdHost.Exited += this.OnCmdExited;
            this.cmdHost.StartFailed += this.OnCmdStartFailed;
            this.breakpointStore = new BreakpointStore();
            this.psDebugger = new PowerShellDebugger(this.breakpointStore);
            this.psDebugger.Stopped += this.OnDebugStopped;
            this.psDebugger.ConsoleLine += this.OnDebugConsole;
            this.psDebugger.Ended += this.OnDebugEnded;
            this.corDebug = new CorDebugSession(this.breakpointStore);
            this.corDebug.Stopped += this.OnDebugStopped;
            this.corDebug.ConsoleLine += this.OnDebugConsole;
            this.corDebug.Ended += this.OnDebugEnded;
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

        /// <summary>展開済みなら初回の左ペイン幅を入れ、常に編集器へフォーカスする。</summary>
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (this.split != null && !this.split.Panel1Collapsed)
            {
                this.TryApplyInitialLeftPaneWidth();
                this.leftSplitterInitialized = true;
            }

            this.FocusEditor();
        }

        /// <summary>最初のハンドルで起動サイズを実 DPI に合わせ、キャプション色を付ける。</summary>
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            NativeCaption.Apply(this.Handle);
            this.ApplyWindowMetrics();
            this.RecreateChromeFonts();
        }

        private const int WM_ACTIVATEAPP = 0x001C;

        /// <summary>他プロセスから戻ったときだけフォルダ再読込。自前ホバーでは走らない。</summary>
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_ACTIVATEAPP && m.WParam != IntPtr.Zero)
            {
                if (this.workspace != null && this.tree != null)
                {
                    this.QueueActivatedRebuild();
                }
            }

            base.WndProc(ref m);
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
            if (this.tree.IsInlineCreateActive)
            {
                this.RefreshWorkspaceTypes();
                this.BeginInvoke(new MethodInvoker(this.RestoreInlineCreateFocus));
                return;
            }

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

            if (this.tree.IsInlineCreateActive)
            {
                this.RefreshWorkspaceTypes();
                this.BeginInvoke(new MethodInvoker(this.RestoreInlineCreateFocus));
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

            if ((this.editor != null && this.editor.IsComposing) || (this.findBar != null && this.findBar.IsComposing) || (this.bottomPane != null && this.bottomPane.IsTerminalComposing) || (this.tree != null && this.tree.IsInlineCreateComposing))
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
                    || keyData == (Keys.Control | Keys.Shift | Keys.B)
                    || keyData == (Keys.Control | Keys.F5)
                    || keyData == Keys.F5
                    || keyData == (Keys.Shift | Keys.F5)
                    || keyData == Keys.F8
                    || keyData == Keys.F9
                    || keyData == Keys.F10
                    || keyData == Keys.F11
                    || keyData == (Keys.Control | Keys.Oemtilde)
                    || keyData == (Keys.Control | Keys.Alt | Keys.P)
                    || keyData == (Keys.Control | Keys.Alt | Keys.H)
                    || keyData == Keys.F12
                    || keyData == (Keys.Control | Keys.Alt | Keys.D)
                    || keyData == (Keys.Control | Keys.K)
                    || keyData == (Keys.Control | Keys.I)
                    || keyData == (Keys.Control | Keys.Shift | Keys.E)
                    || keyData == (Keys.Control | Keys.D1)
                    || keyData == (Keys.Control | Keys.Alt | Keys.N)
                    || keyData == (Keys.Control | Keys.Shift | Keys.N))
                {
                    return false;
                }
            }

            if ((this.findBar != null && this.findBar.ContainsFocus) || (this.tree != null && this.tree.IsInlineCreateActive))
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

            if (keyData == Keys.Escape && this.tree != null && this.tree.IsInlineCreateActive)
            {
                if (this.tree.IsInlineCreateComposing)
                {
                    return false;
                }

                this.tree.CancelInlineCreate();
                return true;
            }

            if (keyData == Keys.Escape && this.IsHoverVisible())
            {
                this.HideHover();
                return true;
            }

            if (keyData == Keys.Escape && this.findBar != null && this.findBar.Visible)
            {
                if ((this.editor != null && this.editor.IsComposing) || this.findBar.IsComposing || (this.bottomPane != null && this.bottomPane.IsTerminalComposing) || (this.tree != null && this.tree.IsInlineCreateComposing))
                {
                    return false;
                }

                this.CloseFindBar();
                return true;
            }

            if (keyData == (Keys.Control | Keys.Oemtilde))
            {
                this.OnToggleTerminal();
                return true;
            }

            if (keyData == (Keys.Control | Keys.Shift | Keys.E))
            {
                this.OnFocusExplorer(this, EventArgs.Empty);
                return true;
            }

            if (keyData == (Keys.Control | Keys.D1))
            {
                this.OnFocusEditor(this, EventArgs.Empty);
                return true;
            }

            if (keyData == (Keys.Control | Keys.Alt | Keys.N))
            {
                this.RunTreeCreate(false);
                return true;
            }

            if (keyData == (Keys.Control | Keys.Shift | Keys.N))
            {
                this.RunTreeCreate(true);
                return true;
            }

            if (keyData == Keys.Escape && this.bottomPane != null && this.bottomPane.IsTerminalFocused)
            {
                if (this.bottomPane.IsTerminalComposing)
                {
                    return false;
                }

                this.bottomPane.Terminal.SendToPty(new byte[] { 0x1B });
                return true;
            }

            if (keyData == (Keys.Control | Keys.Shift | Keys.B))
            {
                this.OnBuild(this, EventArgs.Empty);
                return true;
            }

            if (keyData == (Keys.Control | Keys.F5))
            {
                this.OnRun(this, EventArgs.Empty);
                return true;
            }

            if (keyData == Keys.F8)
            {
                this.OnRunSelection(this, EventArgs.Empty);
                return true;
            }

            if (keyData == Keys.F5)
            {
                this.OnDebugStartContinue(this, EventArgs.Empty);
                return true;
            }

            if (keyData == (Keys.Shift | Keys.F5))
            {
                this.OnDebugStop(this, EventArgs.Empty);
                return true;
            }

            if (keyData == Keys.F9)
            {
                this.OnDebugToggleBreakpoint(this, EventArgs.Empty);
                return true;
            }

            if (keyData == Keys.F10)
            {
                this.OnDebugStepOver(this, EventArgs.Empty);
                return true;
            }

            if (keyData == Keys.F11)
            {
                this.OnDebugStepInto(this, EventArgs.Empty);
                return true;
            }

            if (keyData == (Keys.Control | Keys.Alt | Keys.P))
            {
                this.OnVbaPull(this, EventArgs.Empty);
                return true;
            }

            if (keyData == (Keys.Control | Keys.Alt | Keys.H))
            {
                this.OnVbaPush(this, EventArgs.Empty);
                return true;
            }

            if (this.bottomPane != null && this.bottomPane.IsTerminalFocused)
            {
                if (keyData == (Keys.Control | Keys.Z)
                    || keyData == (Keys.Control | Keys.Y)
                    || keyData == (Keys.Control | Keys.X)
                    || keyData == (Keys.Control | Keys.C)
                    || keyData == (Keys.Control | Keys.V)
                    || keyData == (Keys.Control | Keys.A)
                    || keyData == Keys.F12
                    || keyData == (Keys.Control | Keys.K)
                    || keyData == (Keys.Control | Keys.I)
                    || keyData == (Keys.Control | Keys.Alt | Keys.D))
                {
                    return false;
                }
            }

            if (this.ctrlKPending && keyData != (Keys.Control | Keys.K) && keyData != (Keys.Control | Keys.I))
            {
                this.ctrlKPending = false;
            }

            if (keyData == Keys.F12)
            {
                this.OnGotoDefinition(this, EventArgs.Empty);
                return true;
            }

            if (keyData == (Keys.Control | Keys.Alt | Keys.D))
            {
                this.OnInsertDocFrame(this, EventArgs.Empty);
                return true;
            }

            if (keyData == (Keys.Control | Keys.K))
            {
                this.ctrlKPending = true;
                this.ctrlKAt = DateTime.UtcNow;
                return true;
            }

            if (keyData == (Keys.Control | Keys.I))
            {
                if (this.ctrlKPending && (DateTime.UtcNow - this.ctrlKAt).TotalMilliseconds <= 1000)
                {
                    this.ctrlKPending = false;
                    this.OnQuickInfo(this, EventArgs.Empty);
                    return true;
                }

                this.ctrlKPending = false;
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
            file.DropDownItems.Add(this.CreateDisplayCommand("ファイルを作成(&I)", "Ctrl+Alt+N", this.OnCreateWorkspaceFile));
            file.DropDownItems.Add(this.CreateDisplayCommand("フォルダを作成(&D)", "Ctrl+Shift+N", this.OnCreateWorkspaceFolder));
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
            edit.DropDownItems.Add(new ToolStripSeparator());
            edit.DropDownItems.Add(this.CreateDisplayCommand("定義へ移動(&G)", "F12", this.OnGotoDefinition));
            edit.DropDownItems.Add(this.CreateDisplayCommand("クイック インフォ(&I)", "Ctrl+K Ctrl+I", this.OnQuickInfo));
            edit.DropDownItems.Add(this.CreateDisplayCommand("枠コメントを挿入(&D)", "Ctrl+Alt+D", this.OnInsertDocFrame));

            ToolStripMenuItem build = this.CreateTop("ビルド(&B)");
            build.DropDownItems.Add(this.CreateBuildItem("ビルド(&B)", this.OnBuild));

            ToolStripMenuItem run = this.CreateTop("実行(&R)");
            run.DropDownItems.Add(this.CreateDisplayCommand("開始/続行(&C)", "F5", this.OnDebugStartContinue));
            run.DropDownItems.Add(this.CreateDisplayCommand("停止(&S)", "Shift+F5", this.OnDebugStop));
            run.DropDownItems.Add(this.CreateDisplayCommand("ステップ オーバー(&O)", "F10", this.OnDebugStepOver));
            run.DropDownItems.Add(this.CreateDisplayCommand("ステップ イン(&I)", "F11", this.OnDebugStepInto));
            run.DropDownItems.Add(new ToolStripSeparator());
            run.DropDownItems.Add(this.CreateRunItem("デバッグなしで実行(&N)", this.OnRun));
            run.DropDownItems.Add(this.CreateRunSelectionItem("選択行を実行(&L)", this.OnRunSelection));

            ToolStripMenuItem vba = this.CreateTop("VBA(&A)");
            vba.DropDownOpening += this.OnVbaMenuOpening;
            vba.DropDownItems.Add(this.CreateItem("ブックを選ぶ(&B)", Keys.None, this.OnVbaPickWorkbook));
            vba.DropDownItems.Add(this.CreateVbaPullItem("プル(&P)", this.OnVbaPull));
            vba.DropDownItems.Add(this.CreateVbaPushItem("プッシュ(&H)", this.OnVbaPush));
            vba.DropDownItems.Add(this.CreateDisplayCommand("コンパイル(&C)", "", this.OnVbaCompile));
            vba.DropDownItems.Add(new ToolStripSeparator());
            ToolStripMenuItem naming = this.CreateTop("名前の付け方(&N)");
            this.vbaNameFilenameItem = this.CreateShellCheckItem("filename", this.OnVbaNamingFilename);
            this.vbaNameFolderPrefixItem = this.CreateShellCheckItem("folder_prefix", this.OnVbaNamingFolderPrefix);
            naming.DropDownItems.Add(this.vbaNameFilenameItem);
            naming.DropDownItems.Add(this.vbaNameFolderPrefixItem);
            vba.DropDownItems.Add(naming);

            ToolStripMenuItem view = this.CreateTop("表示(&V)");
            view.DropDownItems.Add(this.CreateDisplayCommand("エクスプローラー(&E)", "Ctrl+Shift+E", this.OnFocusExplorer));
            view.DropDownItems.Add(this.CreateDisplayCommand("編集器(&D)", "Ctrl+1", this.OnFocusEditor));
            view.DropDownItems.Add(new ToolStripSeparator());
            this.viewTerminalItem = this.CreateTerminalViewItem("ターミナル(&T)", this.OnViewTerminal);
            view.DropDownItems.Add(this.viewTerminalItem);
            view.DropDownItems.Add(this.CreateDisplayCommand("デバッグ(&G)", "", this.OnViewDebug));
            view.DropDownItems.Add(new ToolStripSeparator());
            this.viewPowerShellItem = this.CreateShellCheckItem("PowerShell 5.1", this.OnViewPowerShell);
            this.viewPowerShellItem.Checked = true;
            this.viewCmdItem = this.CreateShellCheckItem("コマンド プロンプト", this.OnViewCmd);
            view.DropDownItems.Add(this.viewPowerShellItem);
            view.DropDownItems.Add(this.viewCmdItem);

            ToolStripMenuItem help = this.CreateTop("ヘルプ(&H)");
            help.DropDownItems.Add(this.CreateItem("バージョン情報(&A)", Keys.None, this.OnAbout));

            this.menu.Items.Add(file);
            this.menu.Items.Add(edit);
            this.menu.Items.Add(build);
            this.menu.Items.Add(run);
            this.menu.Items.Add(vba);
            this.menu.Items.Add(view);
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

        private ToolStripMenuItem CreateDisplayCommand(string text, string display, EventHandler handler)
        {
            DualFontMenuItem item = new DualFontMenuItem(text);
            item.ForeColor = Theme.Foreground;
            item.BackColor = Theme.Background;
            item.ShortcutKeyDisplayString = display;
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

        private ToolStripMenuItem CreateVbaPullItem(string text, EventHandler handler)
        {
            DualFontMenuItem item = new DualFontMenuItem(text);
            item.ForeColor = Theme.Foreground;
            item.BackColor = Theme.Background;
            item.ShortcutKeyDisplayString = "Ctrl+Alt+P";
            item.Click += handler;
            return item;
        }

        private ToolStripMenuItem CreateVbaPushItem(string text, EventHandler handler)
        {
            DualFontMenuItem item = new DualFontMenuItem(text);
            item.ForeColor = Theme.Foreground;
            item.BackColor = Theme.Background;
            item.ShortcutKeyDisplayString = "Ctrl+Alt+H";
            item.Click += handler;
            return item;
        }

        private ToolStripMenuItem CreateRunItem(string text, EventHandler handler)
        {
            DualFontMenuItem item = new DualFontMenuItem(text);
            item.ForeColor = Theme.Foreground;
            item.BackColor = Theme.Background;
            item.ShortcutKeyDisplayString = "Ctrl+F5";
            item.Click += handler;
            return item;
        }

        private ToolStripMenuItem CreateRunSelectionItem(string text, EventHandler handler)
        {
            DualFontMenuItem item = new DualFontMenuItem(text);
            item.ForeColor = Theme.Foreground;
            item.BackColor = Theme.Background;
            item.ShortcutKeyDisplayString = "F8";
            item.Click += handler;
            return item;
        }

        private ToolStripMenuItem CreateTerminalViewItem(string text, EventHandler handler)
        {
            DualFontMenuItem item = new DualFontMenuItem(text);
            item.ForeColor = Theme.Foreground;
            item.BackColor = Theme.Background;
            item.ShortcutKeyDisplayString = "Ctrl+`";
            item.Click += handler;
            return item;
        }

        private ToolStripMenuItem CreateShellCheckItem(string text, EventHandler handler)
        {
            DualFontMenuItem item = new DualFontMenuItem(text);
            item.ForeColor = Theme.Foreground;
            item.BackColor = Theme.Background;
            item.Click += handler;
            return item;
        }

        private void BuildStatus()
        {
            this.status = new StatusStrip();
            this.status.Renderer = this.chromeRenderer;
            this.status.BackColor = Theme.StatusBar;
            this.status.SizingGrip = false;
            this.status.DpiChangedAfterParent += this.OnChromeDpiChangedAfterParent;
            this.statusLang = this.CreateStatusLabel(LanguageDetector.GetDisplayName(LanguageKind.Plain), Theme.Foreground);
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
        /// FindBar のラベル／ボタンと作成バーも同じ 12 DIP。検索欄の本文サイズは ApplyEditorSettings。
        /// タブ題名も同じ 12 DIP。本文 fontSize 非連動。
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

            if (this.treeCreateBar != null)
            {
                this.treeCreateBar.SetFonts(newHalf, newFull);
            }

            if (this.bottomPane != null)
            {
                this.bottomPane.SetFonts(newHalf, newFull);
            }

            if (this.tabs != null)
            {
                this.tabs.SetFonts(newHalf, newFull);
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
            int padX = DpiUtil.ToPixels(DpiUtil.StatusStripPadXDip, dpi);
            int padY = DpiUtil.ToPixels(DpiUtil.StatusStripPadYDip, dpi);
            this.status.Padding = new Padding(padX, padY + extraTop, padX, padY + extraBottom);

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

            if (this.treeCreateBar != null)
            {
                this.treeCreateBar.Invalidate();
                this.treeCreateBar.PerformLayout();
            }

            if (this.bottomPane != null)
            {
                this.bottomPane.Invalidate();
            }

            if (this.tabs != null)
            {
                this.tabs.Invalidate();
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
            this.split.Panel1Collapsed = true;

            this.tree = new FileTreeControl();
            this.tree.Dock = DockStyle.Fill;
            this.tree.ApplyFonts(this.fonts);
            this.tree.FileOpenRequested += this.OnTreeOpen;
            this.tree.InlineCreateCommit += this.OnTreeInlineCreateCommit;
            this.tree.InlineRenameCommit += this.OnTreeInlineRenameCommit;
            this.tree.DeleteRequested += this.OnTreeDeleteRequested;
            this.treeCreateBar = new FileTreeCreateBar();
            this.treeCreateBar.Dock = DockStyle.Top;
            this.treeCreateBar.FileCreateRequested += this.OnTreeCreateFile;
            this.treeCreateBar.FolderCreateRequested += this.OnTreeCreateFolder;
            this.split.Panel1.Controls.Add(this.tree);
            this.split.Panel1.Controls.Add(this.treeCreateBar);

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
            this.editor.HoverIdle += this.OnEditorHoverIdle;
            this.editor.HoverCancel += this.OnEditorHoverCancel;
            this.editor.BreakpointToggleRequested += this.OnEditorBreakpointToggle;

            editorColumn.Controls.Add(this.editor);
            editorColumn.Controls.Add(this.findBar);
            right.Controls.Add(editorColumn);
            right.Controls.Add(this.tabs);
            this.split.Panel2.Controls.Add(right);

            this.bottomPane = new BottomPane();
            this.bottomPane.Dock = DockStyle.Fill;
            this.bottomPane.CloseRequested += this.OnBottomPaneClose;
            this.bottomPane.TerminalSelected += this.OnBottomPaneTerminalSelected;
            this.bottomPane.ItemActivated += this.OnProblemActivated;

            this.bodySplit.Panel1.Controls.Add(this.split);
            this.bodySplit.Panel2.Controls.Add(this.bottomPane);
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
            this.ApplyEditorSquiggles(this.lastPublishedDiagnostics);
            this.RefreshBreakpointMarks();
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
            WorkspaceSymbols.Invalidate();
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

            if (!string.Equals(Path.GetExtension(doc.FilePath), ".cs", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(Path.GetExtension(doc.FilePath), ".bas", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(Path.GetExtension(doc.FilePath), ".cls", StringComparison.OrdinalIgnoreCase))
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
                WorkspaceSymbols.Invalidate();
                if (this.bottomPane != null)
                {
                    this.bottomPane.Terminal.SetWorkspaceRoot(this.workspace.RootPath);
                }
                if (this.editor != null)
                {
                    this.ApplyWorkspaceToDocument(this.editor.Document);
                }
                this.EnsureLeftPaneVisible();
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

        /// <summary>ファイルメニュー／ショートカットからワークスペース内ファイルの行内作成を始める。</summary>
        private void OnCreateWorkspaceFile(object sender, EventArgs e)
        {
            this.RunTreeCreate(false);
        }

        /// <summary>ファイルメニュー／ショートカットからワークスペース内フォルダの行内作成を始める。</summary>
        private void OnCreateWorkspaceFolder(object sender, EventArgs e)
        {
            this.RunTreeCreate(true);
        }

        private void OnTreeCreateFile(object sender, EventArgs e)
        {
            this.BeginInvoke(new MethodInvoker(delegate { this.RunTreeCreate(false); }));
        }

        private void OnTreeCreateFolder(object sender, EventArgs e)
        {
            this.BeginInvoke(new MethodInvoker(delegate { this.RunTreeCreate(true); }));
        }

        /// <summary>
        /// 作成バーまたはショートカットからファイルまたはフォルダの行内作成を始める。
        /// </summary>
        /// <param name="isFolder">フォルダなら true。</param>
        private void RunTreeCreate(bool isFolder)
        {
            if (this.workspace == null)
            {
                MessageBox.Show(this, "ワークスペースを開いてください。", "WindowsIDE", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (this.tree != null && this.tree.IsInlineCreateActive)
            {
                this.tree.CancelInlineCreate();
            }

            string selected = null;
            if (this.tree != null && this.tree.SelectedNode != null)
            {
                selected = this.tree.SelectedNode.Tag as string;
            }

            string parent = WorkspaceCreateRules.ResolveCreateDirectory(this.workspace.RootPath, selected);
            if (parent == null)
            {
                MessageBox.Show(this, "作成先を決定できません。", "WindowsIDE", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (this.tree == null || !this.tree.BeginInlineCreate(isFolder, parent))
            {
                MessageBox.Show(this, "作成先を決定できません。", "WindowsIDE", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>
        /// 行内作成の確定。規則は WorkspaceCreateRules。失敗時は欄を残して MessageBox。
        /// </summary>
        private void OnTreeInlineCreateCommit(object sender, TreeCreateCommitEventArgs e)
        {
            if (e == null || this.workspace == null || this.tree == null)
            {
                return;
            }

            string created;
            string error;
            bool ok;
            if (e.IsFolder)
            {
                ok = WorkspaceCreateRules.TryCreateDirectory(this.workspace.RootPath, e.ParentDirectory, e.Name, out created, out error);
            }
            else
            {
                ok = WorkspaceCreateRules.TryCreateFile(this.workspace.RootPath, e.ParentDirectory, e.Name, out created, out error);
            }

            if (!ok)
            {
                this.tree.SetInlineBlurCancelSuppressed(true);
                try
                {
                    MessageBox.Show(this, error, "WindowsIDE", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                finally
                {
                    this.tree.SetInlineBlurCancelSuppressed(false);
                }

                this.tree.FocusInlineCreate();
                return;
            }

            this.tree.CancelInlineCreate();
            this.tree.Rebuild();
            this.tree.RevealAndSelect(created);
            if (!e.IsFolder)
            {
                string openError;
                if (!this.TryOpenFile(created, true, out openError) && !string.IsNullOrEmpty(openError))
                {
                    MessageBox.Show(this, openError, "WindowsIDE", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private void RestoreInlineCreateFocus()
        {
            if (this.IsDisposed || this.tree == null || !this.tree.IsInlineCreateActive)
            {
                return;
            }

            this.tree.FocusInlineCreate();
        }

        /// <summary>
        /// ツリーへフォーカスする。ワークスペース未 Bind では何もしない。インライン中は欄へ。作成はキャンセルしない。
        /// </summary>
        private void OnFocusExplorer(object sender, EventArgs e)
        {
            if (this.workspace == null || this.tree == null)
            {
                return;
            }

            this.EnsureLeftPaneVisible();
            this.tree.Focus();
            if (this.tree.IsInlineCreateActive)
            {
                this.tree.FocusInlineCreate();
            }
        }

        /// <summary>
        /// 編集器へフォーカスする。ワークスペース無しでも可。左ペイン・下パネルは畳まない。FindBar は閉じない。
        /// </summary>
        private void OnFocusEditor(object sender, EventArgs e)
        {
            this.FocusEditor();
        }

        /// <summary>
        /// 行内リネームの確定。規則は WorkspaceItemRules。失敗時は欄を残して MessageBox。
        /// </summary>
        private void OnTreeInlineRenameCommit(object sender, TreeRenameCommitEventArgs e)
        {
            if (e == null || this.workspace == null || this.tree == null || string.IsNullOrEmpty(e.OldPath))
            {
                return;
            }

            string newFull;
            string error;
            if (!WorkspaceItemRules.TryBuildRenamedPath(this.workspace.RootPath, e.OldPath, e.NewName, out newFull, out error))
            {
                this.ShowInlineTreeError(error);
                return;
            }

            bool wasDirectory = Directory.Exists(e.OldPath);
            try
            {
                if (wasDirectory)
                {
                    Directory.Move(e.OldPath, newFull);
                }
                else
                {
                    File.Move(e.OldPath, newFull);
                }
            }
            catch (IOException ex)
            {
                this.ShowInlineTreeError(ex.Message);
                return;
            }
            catch (UnauthorizedAccessException ex)
            {
                this.ShowInlineTreeError(ex.Message);
                return;
            }

            this.tree.CancelInlineCreate();
            this.RetargetOpenDocuments(e.OldPath, newFull, wasDirectory);
            this.tabs.RefreshTabs();
            this.UpdateStatus();
            this.RefreshWorkspaceTypes();
            this.ApplyVbaMapAfterDiskChange(e.OldPath, newFull, wasDirectory);
            this.tree.Rebuild();
            this.tree.RevealAndSelect(newFull);
        }

        /// <summary>
        /// ツリーの Delete。根は無音。確認のうえごみ箱。Cancel ならディスクを触らない。
        /// </summary>
        private void OnTreeDeleteRequested(object sender, EventArgs e)
        {
            if (this.tree == null || this.workspace == null)
            {
                return;
            }

            TreeNode node = this.tree.SelectedNode;
            if (node == null)
            {
                return;
            }

            string path = node.Tag as string;
            if (string.IsNullOrEmpty(path) || WorkspaceItemRules.IsWorkspaceRoot(this.workspace.RootPath, path))
            {
                return;
            }

            if (!PathGuard.IsInsideWorkspace(this.workspace.RootPath, path))
            {
                return;
            }

            if (!File.Exists(path) && !Directory.Exists(path))
            {
                return;
            }

            bool wasDirectory = Directory.Exists(path);
            string name = Path.GetFileName(path);
            if (string.IsNullOrEmpty(name))
            {
                name = path;
            }

            string prompt;
            if (wasDirectory)
            {
                prompt = string.Format("フォルダ '{0}' とその中身をごみ箱に移しますか?", name);
            }
            else
            {
                prompt = string.Format("'{0}' をごみ箱に移しますか?", name);
            }

            DialogResult confirm = MessageBox.Show(this, prompt, "WindowsIDE", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
            if (confirm != DialogResult.Yes)
            {
                return;
            }

            if (!this.TryPromptDirtyTabsUnder(path))
            {
                return;
            }

            string error;
            if (!WorkspaceRecycle.TrySendToRecycleBin(this.Handle, path, out error))
            {
                MessageBox.Show(this, error, "WindowsIDE", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            this.RemoveTabsUnder(path);
            this.ApplyVbaMapAfterDiskChange(path, null, wasDirectory);
            this.tree.Rebuild();
            string parent = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(parent))
            {
                this.tree.RevealAndSelect(parent);
            }

            this.RefreshWorkspaceTypes();
            this.UpdateStatus();
        }

        private void ShowInlineTreeError(string error)
        {
            this.tree.SetInlineBlurCancelSuppressed(true);
            try
            {
                MessageBox.Show(this, error, "WindowsIDE", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                this.tree.SetInlineBlurCancelSuppressed(false);
            }

            this.tree.FocusInlineCreate();
        }

        private void RetargetOpenDocuments(string oldPath, string newPath, bool wasDirectory)
        {
            if (this.tabs == null || string.IsNullOrEmpty(oldPath) || string.IsNullOrEmpty(newPath))
            {
                return;
            }

            for (int i = 0; i < this.tabs.Tabs.Count; i++)
            {
                Document doc = this.tabs.Tabs[i];
                if (doc == null || string.IsNullOrEmpty(doc.FilePath))
                {
                    continue;
                }

                if (wasDirectory)
                {
                    if (WorkspaceItemRules.IsSameOrUnder(oldPath, doc.FilePath))
                    {
                        string next = WorkspaceItemRules.ReplacePathPrefix(oldPath, newPath, doc.FilePath);
                        doc.Retarget(next);
                    }
                }
                else if (string.Equals(doc.FilePath, oldPath, StringComparison.OrdinalIgnoreCase))
                {
                    doc.Retarget(newPath);
                }
            }
        }

        private bool TryPromptDirtyTabsUnder(string path)
        {
            if (this.tabs == null)
            {
                return true;
            }

            for (int i = 0; i < this.tabs.Tabs.Count; i++)
            {
                Document doc = this.tabs.Tabs[i];
                if (doc == null || string.IsNullOrEmpty(doc.FilePath) || !doc.IsDirty)
                {
                    continue;
                }

                if (!WorkspaceItemRules.IsSameOrUnder(path, doc.FilePath))
                {
                    continue;
                }

                DialogResult r = MessageBox.Show(this, doc.DisplayName + " を保存しますか?", "WindowsIDE", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (r == DialogResult.Cancel)
                {
                    return false;
                }

                if (r == DialogResult.Yes)
                {
                    this.tabs.SelectedIndex = i;
                    if (!this.SaveCurrent(false))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private void RemoveTabsUnder(string path)
        {
            if (this.tabs == null)
            {
                return;
            }

            this.suppressNewUntitled = true;
            this.rebuilding = true;
            try
            {
                int i = this.tabs.Tabs.Count - 1;
                while (i >= 0)
                {
                    Document doc = this.tabs.Tabs[i];
                    if (doc != null && !string.IsNullOrEmpty(doc.FilePath)
                        && WorkspaceItemRules.IsSameOrUnder(path, doc.FilePath))
                    {
                        this.tabs.RemoveAt(i);
                    }

                    i--;
                }
            }
            finally
            {
                this.rebuilding = false;
                this.suppressNewUntitled = false;
            }

            if (this.tabs.Tabs.Count == 0)
            {
                this.NewUntitled();
            }
            else
            {
                this.AttachDocument(this.tabs.SelectedDocument);
            }
        }

        private void ApplyVbaMapAfterDiskChange(string oldFull, string newFull, bool wasDirectory)
        {
            if (this.workspace == null || string.IsNullOrEmpty(oldFull))
            {
                return;
            }

            VbaMap map;
            string loadError;
            VbaMapLoadStatus status = VbaMap.TryLoad(this.workspace.RootPath, out map, out loadError);
            if (status == VbaMapLoadStatus.Missing || status == VbaMapLoadStatus.Broken || map == null)
            {
                return;
            }

            if (!map.TryApplyDiskChange(this.workspace.RootPath, oldFull, newFull, wasDirectory))
            {
                return;
            }

            string saveError;
            if (!map.TrySave(this.workspace.RootPath, out saveError))
            {
                string message = string.IsNullOrEmpty(saveError) ? "vba-map.xml を保存できませんでした。" : saveError;
                MessageBox.Show(this, message, "WindowsIDE", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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

        private void OnVbaMenuOpening(object sender, EventArgs e)
        {
            bool filename = false;
            bool folder = false;
            if (this.workspace != null)
            {
                VbaMap map;
                string error;
                if (VbaMap.TryLoad(this.workspace.RootPath, out map, out error) == VbaMapLoadStatus.Ok && map != null)
                {
                    filename = map.NamingMode == VbaNamingMode.Filename;
                    folder = map.NamingMode == VbaNamingMode.FolderPrefix;
                }
            }

            if (this.vbaNameFilenameItem != null)
            {
                this.vbaNameFilenameItem.Checked = filename;
            }

            if (this.vbaNameFolderPrefixItem != null)
            {
                this.vbaNameFolderPrefixItem.Checked = folder;
            }
        }

        private void OnVbaPickWorkbook(object sender, EventArgs e)
        {
            if (this.workspace == null)
            {
                MessageBox.Show(this, "ワークスペースを開いてください。", "WindowsIDE", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string initial = this.workspace.RootPath;
            string path;
            try
            {
                if (!CommonItemDialog.TryPickOpenFile(
                    this,
                    "Excel マクロ有効ブックを選ぶ",
                    initial,
                    "Excel マクロ有効ブック (*.xlsm;*.xlsb)|*.xlsm;*.xlsb",
                    out path))
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "WindowsIDE", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (!VbaWorkbookPath.IsMacroWorkbook(path))
            {
                MessageBox.Show(this, "マクロ有効ブック（.xlsm / .xlsb）ではありません。", "WindowsIDE", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            this.ApplyVbaSyncResult(VbaSyncService.SetWorkbook(this.workspace.RootPath, path), false);
        }

        private void OnVbaPull(object sender, EventArgs e)
        {
            this.RunVbaSync(delegate(VbaSyncConfirm confirm)
            {
                return VbaSyncService.Pull(this.workspace.RootPath, confirm);
            }, true);
        }

        private void OnVbaPush(object sender, EventArgs e)
        {
            this.RunVbaSync(delegate(VbaSyncConfirm confirm)
            {
                return VbaSyncService.Push(this.workspace.RootPath, confirm);
            }, false);
        }

        private void OnVbaCompile(object sender, EventArgs e)
        {
            this.RunVbaSync(delegate(VbaSyncConfirm confirm)
            {
                return VbaSyncService.PushThenCompile(this.workspace.RootPath, confirm);
            }, false);
        }

        private void OnVbaNamingFilename(object sender, EventArgs e)
        {
            this.ChangeVbaNamingMode(VbaNamingMode.Filename);
        }

        private void OnVbaNamingFolderPrefix(object sender, EventArgs e)
        {
            this.ChangeVbaNamingMode(VbaNamingMode.FolderPrefix);
        }

        private void ChangeVbaNamingMode(VbaNamingMode mode)
        {
            if (this.workspace == null)
            {
                MessageBox.Show(this, "ワークスペースを開いてください。", "WindowsIDE", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            VbaRenamePreview[] rows;
            VbaSyncResult preview = VbaSyncService.PreviewNaming(this.workspace.RootPath, mode, out rows);
            if (!preview.Success)
            {
                this.ApplyVbaSyncResult(preview, false);
                return;
            }

            StringBuilder sb = new StringBuilder();
            int shown = (rows.Length < 20) ? rows.Length : 20;
            for (int i = 0; i < shown; i++)
            {
                sb.Append(rows[i].OldName);
                sb.Append(" → ");
                sb.Append(rows[i].NewName);
                if (rows[i].IsDocument)
                {
                    sb.Append("（document）");
                }

                sb.AppendLine();
            }

            if (rows.Length > 20)
            {
                sb.Append("他 ");
                sb.Append((rows.Length - 20).ToString());
                sb.AppendLine(" 件");
            }

            sb.Append("名前の付け方だけを更新します。Excel 側はまだリネームしません。");
            DialogResult answer = MessageBox.Show(this, sb.ToString(), "WindowsIDE", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (answer != DialogResult.Yes)
            {
                return;
            }

            this.ApplyVbaSyncResult(VbaSyncService.SetNamingMode(this.workspace.RootPath, mode), false);
        }

        private void RunVbaSync(Func<VbaSyncConfirm, VbaSyncResult> action, bool reloadWritten)
        {
            if (this.workspace == null)
            {
                MessageBox.Show(this, "ワークスペースを開いてください。", "WindowsIDE", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            VbaSyncConfirm confirm = new VbaSyncConfirm(this.ConfirmVbaDirty, this.ConfirmVbaExcelUnsaved);
            this.UseWaitCursor = true;
            VbaSyncResult result;
            try
            {
                result = action(confirm);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "WindowsIDE", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            finally
            {
                this.UseWaitCursor = false;
            }

            this.ApplyVbaSyncResult(result, reloadWritten);
        }

        private bool ConfirmVbaDirty(string[] paths)
        {
            bool wait = this.UseWaitCursor;
            this.UseWaitCursor = false;
            try
            {
                List<Document> dirty = new List<Document>();
                if (this.tabs != null && paths != null)
                {
                    for (int i = 0; i < this.tabs.Tabs.Count; i++)
                    {
                        Document doc = this.tabs.Tabs[i];
                        if (doc == null || !doc.IsDirty || string.IsNullOrEmpty(doc.FilePath))
                        {
                            continue;
                        }

                        for (int j = 0; j < paths.Length; j++)
                        {
                            if (paths[j] != null && string.Equals(doc.FilePath, paths[j], StringComparison.OrdinalIgnoreCase))
                            {
                                dirty.Add(doc);
                                break;
                            }
                        }
                    }
                }

                if (dirty.Count == 0)
                {
                    return true;
                }

                DialogResult r = MessageBox.Show(
                    this,
                    "対象ファイルに未保存のタブがあります。保存して続行しますか?",
                    "WindowsIDE",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                if (r != DialogResult.Yes)
                {
                    return false;
                }

                for (int i = 0; i < dirty.Count; i++)
                {
                    try
                    {
                        if (!dirty[i].Save())
                        {
                            return false;
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, ex.Message, "WindowsIDE", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return false;
                    }
                }

                if (this.tabs != null)
                {
                    this.tabs.RefreshTabs();
                }

                return true;
            }
            finally
            {
                this.UseWaitCursor = wait;
            }
        }

        private bool ConfirmVbaExcelUnsaved()
        {
            bool wait = this.UseWaitCursor;
            this.UseWaitCursor = false;
            try
            {
                DialogResult r = MessageBox.Show(
                    this,
                    "Excel のブックが未保存です。保存して続行しますか?",
                    "WindowsIDE",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                return r == DialogResult.Yes;
            }
            finally
            {
                this.UseWaitCursor = wait;
            }
        }

        private void ApplyVbaSyncResult(VbaSyncResult result, bool reloadWritten)
        {
            if (result == null || result.Cancelled)
            {
                return;
            }

            if (result.ReplaceProblems)
            {
                this.ShowBuildDiagnostics(result.Diagnostics);
                return;
            }

            if (!string.IsNullOrEmpty(result.Message))
            {
                MessageBox.Show(
                    this,
                    result.Message,
                    "WindowsIDE",
                    MessageBoxButtons.OK,
                    result.IsErrorMessage ? MessageBoxIcon.Error : MessageBoxIcon.Warning);
                if (!result.Success)
                {
                    return;
                }
            }

            if (!result.Success)
            {
                return;
            }

            if (result.ApplyVbaCompile)
            {
                this.ApplyVbaDiagnostics(result.Diagnostics);
            }

            if (result.CreatedMap)
            {
                MessageBox.Show(
                    this,
                    "Excel 上の VBA は平坦です。フォルダは IDE のディスク専用です。",
                    "WindowsIDE",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }

            if (reloadWritten && this.tree != null && this.workspace != null)
            {
                this.tree.Rebuild();
            }

            if (reloadWritten && result.WrittenPaths != null && this.tabs != null)
            {
                for (int i = 0; i < this.tabs.Tabs.Count; i++)
                {
                    Document doc = this.tabs.Tabs[i];
                    if (doc == null || string.IsNullOrEmpty(doc.FilePath) || doc.IsDirty)
                    {
                        continue;
                    }

                    for (int j = 0; j < result.WrittenPaths.Length; j++)
                    {
                        if (result.WrittenPaths[j] != null
                            && string.Equals(doc.FilePath, result.WrittenPaths[j], StringComparison.OrdinalIgnoreCase))
                        {
                            doc.ReloadFromDisk();
                            break;
                        }
                    }
                }

                if (this.editor != null && this.editor.Document != null)
                {
                    Document current = this.editor.Document;
                    this.editor.Document = current;
                }

                this.tabs.RefreshTabs();
                this.UpdateStatus();
            }

            if (result.ExcelOnlyNames != null && result.ExcelOnlyNames.Length > 0)
            {
                StringBuilder names = new StringBuilder();
                names.AppendLine("Excel にだけあるモジュール（削除していません）:");
                for (int i = 0; i < result.ExcelOnlyNames.Length; i++)
                {
                    names.AppendLine(result.ExcelOnlyNames[i]);
                }

                MessageBox.Show(this, names.ToString(), "WindowsIDE", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

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
        private void OnBuild(object sender, EventArgs e)
        {
            if (this.IsCsDebugging())
            {
                this.ShowSynthetic("C# のデバッグ中はビルドできない。");
                return;
            }

            this.pendingLaunchAfterBuild = false;
            this.pendingDebugAfterBuild = false;
            if (this.csharpHost != null)
            {
                this.csharpHost.Kill();
            }

            if (this.powershellHost != null)
            {
                this.powershellHost.Kill();
            }

            if (this.cmdHost != null)
            {
                this.cmdHost.Kill();
            }

            this.StartManualBuild();
        }

        private void OnRun(object sender, EventArgs e)
        {
            if (this.IsDebugging())
            {
                this.ShowSynthetic("デバッグ中は実行できない。");
                return;
            }

            string path = null;
            if (this.editor != null && this.editor.Document != null)
            {
                path = this.editor.Document.FilePath;
            }

            if (string.IsNullOrEmpty(path))
            {
                this.ShowSynthetic("無題は実行できない。");
                return;
            }

            string ext = Path.GetExtension(path);
            if (string.Equals(ext, ".ps1", StringComparison.OrdinalIgnoreCase))
            {
                this.pendingLaunchAfterBuild = false;
                this.buildGeneration++;
                if (this.cscRunner != null)
                {
                    this.cscRunner.Kill();
                }

                this.InvalidateLiveCsc();

                if (this.csharpHost != null)
                {
                    this.csharpHost.Kill();
                }

                if (this.cmdHost != null)
                {
                    this.cmdHost.Kill();
                }

                this.StartPs(path);
                return;
            }

            if (string.Equals(ext, ".psm1", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ext, ".psd1", StringComparison.OrdinalIgnoreCase))
            {
                this.ShowSynthetic(".psm1 / .psd1 は実行対象外です。");
                return;
            }

            if (string.Equals(ext, ".cmd", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ext, ".bat", StringComparison.OrdinalIgnoreCase))
            {
                this.pendingLaunchAfterBuild = false;
                this.buildGeneration++;
                if (this.cscRunner != null)
                {
                    this.cscRunner.Kill();
                }

                this.InvalidateLiveCsc();

                if (this.csharpHost != null)
                {
                    this.csharpHost.Kill();
                }

                if (this.powershellHost != null)
                {
                    this.powershellHost.Kill();
                }

                this.StartCmd(path);
                return;
            }

            if (string.Equals(ext, ".cs", StringComparison.OrdinalIgnoreCase))
            {
                this.pendingLaunchAfterBuild = true;
                this.pendingDebugAfterBuild = false;
                if (this.powershellHost != null)
                {
                    this.powershellHost.Kill();
                }

                if (this.cmdHost != null)
                {
                    this.cmdHost.Kill();
                }

                this.StartManualBuild();
                return;
            }

            this.ShowSynthetic("このファイルは実行できません。");
        }

        private void OnRunSelection(object sender, EventArgs e)
        {
            if (this.IsDebugging())
            {
                this.ShowSynthetic("デバッグ中は実行できない。");
                return;
            }

            string path = null;
            if (this.editor != null && this.editor.Document != null)
            {
                path = this.editor.Document.FilePath;
            }

            if (string.IsNullOrEmpty(path))
            {
                this.ShowSynthetic("無題は実行できない。");
                return;
            }

            string ext = Path.GetExtension(path);
            if (!string.Equals(ext, ".cmd", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(ext, ".bat", StringComparison.OrdinalIgnoreCase))
            {
                this.ShowSynthetic("選択行の実行は .cmd / .bat だけです。");
                return;
            }

            Document doc = this.editor.Document;
            BufferPoint selStart;
            BufferPoint selEnd;
            doc.GetSelection(out selStart, out selEnd);
            string scriptText = CmdSelectionRules.Extract(doc.Buffer, doc.CaretLine, selStart, selEnd);
            if (CmdSelectionRules.IsBlank(scriptText))
            {
                this.ShowSynthetic("実行する行がありません。");
                return;
            }

            string cmdExe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
            if (string.IsNullOrEmpty(cmdExe) || !File.Exists(cmdExe))
            {
                this.ShowSynthetic("cmd.exe が見つかりません。");
                return;
            }

            string full;
            try
            {
                full = Path.GetFullPath(path);
            }
            catch (Exception)
            {
                this.ShowSynthetic("このファイルは実行できません。");
                return;
            }

            string scriptDir = Path.GetDirectoryName(full);
            this.pendingLaunchAfterBuild = false;
            this.buildGeneration++;
            if (this.cscRunner != null)
            {
                this.cscRunner.Kill();
            }

            this.InvalidateLiveCsc();

            if (this.csharpHost != null)
            {
                this.csharpHost.Kill();
            }

            if (this.powershellHost != null)
            {
                this.powershellHost.Kill();
            }

            this.EnsureBottomPaneVisible();
            if (this.bottomPane == null || this.cmdHost == null)
            {
                return;
            }

            this.bottomPane.Output.Clear();
            this.bottomPane.ShowOutput();
            if (this.csharpHost != null)
            {
                this.csharpHost.Kill();
            }

            if (this.powershellHost != null)
            {
                this.powershellHost.Kill();
            }

            this.runGeneration++;
            this.activeRunKind = ActiveRunKind.Cmd;
            this.cmdHost.StartSelection(scriptText, scriptDir, this.runGeneration);
            if (string.IsNullOrEmpty(this.cmdHost.StartError))
            {
                this.bottomPane.Output.AppendStatus("起動: 選択行 (" + full + ")");
            }
        }

        private void StartPs(string path)
        {
            string psExe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe");
            if (string.IsNullOrEmpty(psExe) || !File.Exists(psExe))
            {
                this.ShowSynthetic("powershell.exe が見つかりません。");
                return;
            }

            string full;
            try
            {
                full = Path.GetFullPath(path);
            }
            catch (Exception)
            {
                this.ShowSynthetic("このファイルは実行できません。");
                return;
            }

            if (this.editor != null && this.editor.Document != null && this.editor.Document.IsDirty)
            {
                try
                {
                    if (!this.editor.Document.Save())
                    {
                        this.ShowSynthetic("保存に失敗した: " + this.editor.Document.FilePath);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    this.ShowSynthetic("保存に失敗した: " + ex.Message);
                    return;
                }

                if (this.tabs != null)
                {
                    this.tabs.RefreshTabs();
                }

                this.UpdateStatus();
            }

            this.EnsureBottomPaneVisible();
            if (this.bottomPane == null || this.powershellHost == null)
            {
                return;
            }

            this.bottomPane.Output.Clear();
            this.bottomPane.ShowOutput();
            if (this.csharpHost != null)
            {
                this.csharpHost.Kill();
            }

            if (this.cmdHost != null)
            {
                this.cmdHost.Kill();
            }

            this.runGeneration++;
            this.activeRunKind = ActiveRunKind.PowerShell;
            string scriptDir = Path.GetDirectoryName(full);
            this.powershellHost.Start(full, scriptDir, this.runGeneration);
            if (string.IsNullOrEmpty(this.powershellHost.StartError))
            {
                this.bottomPane.Output.AppendStatus("起動: " + full);
            }
        }

        private void StartCmd(string path)
        {
            string cmdExe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
            if (string.IsNullOrEmpty(cmdExe) || !File.Exists(cmdExe))
            {
                this.ShowSynthetic("cmd.exe が見つかりません。");
                return;
            }

            string full;
            try
            {
                full = Path.GetFullPath(path);
            }
            catch (Exception)
            {
                this.ShowSynthetic("このファイルは実行できません。");
                return;
            }

            if (this.editor != null && this.editor.Document != null && this.editor.Document.IsDirty)
            {
                try
                {
                    if (!this.editor.Document.Save())
                    {
                        this.ShowSynthetic("保存に失敗した: " + this.editor.Document.FilePath);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    this.ShowSynthetic("保存に失敗した: " + ex.Message);
                    return;
                }

                if (this.tabs != null)
                {
                    this.tabs.RefreshTabs();
                }

                this.UpdateStatus();
            }

            this.EnsureBottomPaneVisible();
            if (this.bottomPane == null || this.cmdHost == null)
            {
                return;
            }

            this.bottomPane.Output.Clear();
            this.bottomPane.ShowOutput();
            if (this.csharpHost != null)
            {
                this.csharpHost.Kill();
            }

            if (this.powershellHost != null)
            {
                this.powershellHost.Kill();
            }

            this.runGeneration++;
            this.activeRunKind = ActiveRunKind.Cmd;
            string scriptDir = Path.GetDirectoryName(full);
            this.cmdHost.Start(full, scriptDir, this.runGeneration);
            if (string.IsNullOrEmpty(this.cmdHost.StartError))
            {
                this.bottomPane.Output.AppendStatus("起動: " + full);
            }
        }

        private bool StartManualBuild()
        {
            this.buildGeneration++;
            if (this.csharpHost != null)
            {
                this.csharpHost.Kill();
            }

            if (this.powershellHost != null)
            {
                this.powershellHost.Kill();
            }

            if (this.cmdHost != null)
            {
                this.cmdHost.Kill();
            }

            if (this.cscRunner != null)
            {
                this.cscRunner.Kill();
            }

            this.InvalidateLiveCsc();

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
                return false;
            }

            string saveError;
            if (!this.TrySaveDirtySources(sources, out saveError))
            {
                this.ShowSynthetic(saveError);
                return false;
            }

            if (!FrameworkCsc.CompilerExists())
            {
                this.ShowSynthetic("csc.exe が見つかりません。");
                return false;
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
                return false;
            }

            int gen = this.buildGeneration;
            ManualBuildRequest req = new ManualBuildRequest();
            req.Generation = gen;
            req.RspPath = rspPath;
            req.OutputExe = outputExe;
            req.WorkingDirectory = ResolveWorkingDirectory(workspaceRoot, sources, outputExe);
            Thread thread = new Thread(this.BuildWorkerProc);
            thread.IsBackground = true;
            thread.Start(req);
            return true;
        }

        private void BuildWorkerProc(object state)
        {
            ManualBuildRequest req = state as ManualBuildRequest;
            if (req == null || this.cscRunner == null)
            {
                return;
            }

            // 世代が違う＝.ps1 / .cmd 実行などでこの csc は無効。古いワーカーが新しいホストを Kill / 待ってはいけない。
            if (!this.IsCurrentBuild(req.Generation))
            {
                return;
            }

            if (this.csharpHost != null)
            {
                this.csharpHost.Kill();
                this.csharpHost.WaitUntilExited(5000);
            }

            if (!this.IsCurrentBuild(req.Generation))
            {
                return;
            }

            if (this.powershellHost != null)
            {
                if (!this.IsCurrentBuild(req.Generation))
                {
                    return;
                }

                this.powershellHost.Kill();
                this.powershellHost.WaitUntilExited(5000);
            }

            if (!this.IsCurrentBuild(req.Generation))
            {
                return;
            }

            if (this.cmdHost != null)
            {
                if (!this.IsCurrentBuild(req.Generation))
                {
                    return;
                }

                this.cmdHost.Kill();
                this.cmdHost.WaitUntilExited(5000);
            }

            if (!this.IsCurrentBuild(req.Generation))
            {
                return;
            }

            CscRunResult result = this.cscRunner.Run(req.RspPath, req.Generation);
            if (this.IsDisposed || !this.IsHandleCreated || !this.IsCurrentBuild(req.Generation))
            {
                return;
            }

            this.BeginInvoke(new MethodInvoker(delegate
            {
                this.OnBuildFinished(req, result);
            }));
        }

        /// <summary>
        /// このワーカーの世代が、いま有効な手動ビルドと一致するか。違えばホストも csc も触らない。
        /// </summary>
        /// <param name="generation">StartManualBuild が渡した世代。</param>
        /// <returns>現行なら true。</returns>
        private bool IsCurrentBuild(int generation)
        {
            return generation == this.buildGeneration;
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
                this.pendingDebugAfterBuild = false;
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
            if (this.pendingDebugAfterBuild)
            {
                this.pendingDebugAfterBuild = false;
                if (result.ExitCode != 0)
                {
                    return;
                }

                if (this.corDebug == null)
                {
                    return;
                }

                string pdbPath = null;
                if (!string.IsNullOrEmpty(req.OutputExe))
                {
                    pdbPath = Path.ChangeExtension(req.OutputExe, ".pdb");
                }

                if (string.IsNullOrEmpty(req.OutputExe) || !File.Exists(req.OutputExe) || string.IsNullOrEmpty(pdbPath) || !File.Exists(pdbPath))
                {
                    this.ShowSynthetic("デバッグ用の exe または pdb がありません。");
                    return;
                }

                this.ShowDebugPanel();
                if (this.bottomPane != null)
                {
                    this.bottomPane.DebugPane.ConsolePanel.AppendStatus("起動: " + req.OutputExe);
                }

                this.corDebug.Start(req.OutputExe, req.WorkingDirectory, this.debugGeneration);
                return;
            }

            if (!this.pendingLaunchAfterBuild || result.ExitCode != 0)
            {
                return;
            }

            this.EnsureBottomPaneVisible();
            if (this.bottomPane == null)
            {
                return;
            }

            this.bottomPane.Output.Clear();
            this.bottomPane.ShowOutput();
            if (string.IsNullOrEmpty(req.OutputExe) || !File.Exists(req.OutputExe))
            {
                this.bottomPane.Output.AppendStatus("実行ファイルがありません。");
                return;
            }

            if (this.powershellHost != null)
            {
                this.powershellHost.Kill();
            }

            if (this.cmdHost != null)
            {
                this.cmdHost.Kill();
            }

            this.runGeneration++;
            this.activeRunKind = ActiveRunKind.CSharp;
            if (this.csharpHost == null)
            {
                return;
            }

            this.csharpHost.Start(req.OutputExe, req.WorkingDirectory, this.runGeneration);
            if (string.IsNullOrEmpty(this.csharpHost.StartError))
            {
                this.bottomPane.Output.AppendStatus("起動: " + req.OutputExe);
            }
        }

        private static string ResolveWorkingDirectory(string workspaceRoot, string[] sources, string outputExe)
        {
            if (!string.IsNullOrEmpty(workspaceRoot))
            {
                return workspaceRoot;
            }

            if (sources != null && sources.Length > 0 && !string.IsNullOrEmpty(sources[0]))
            {
                try
                {
                    string dir = Path.GetDirectoryName(Path.GetFullPath(sources[0]));
                    if (!string.IsNullOrEmpty(dir))
                    {
                        return dir;
                    }
                }
                catch (Exception)
                {
                }
            }

            if (!string.IsNullOrEmpty(outputExe))
            {
                try
                {
                    string dir = Path.GetDirectoryName(outputExe);
                    if (!string.IsNullOrEmpty(dir))
                    {
                        return dir;
                    }
                }
                catch (Exception)
                {
                }
            }

            return Environment.CurrentDirectory;
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
            this.csharpBucket = null;
            this.psBucket = null;
            this.vbaBucket = null;
            this.EnsureBottomPaneVisible();
            this.PublishProblemList(list);
        }

        /// <summary>
        /// C# 常時診断だけを置き換える。hostOwns を外す。
        /// </summary>
        /// <param name="list">csc 診断。null は空。</param>
        private void ApplyCSharpDiagnostics(Diagnostic[] list)
        {
            this.csharpBucket = list;
            this.PublishBuckets();
        }

        /// <summary>
        /// PowerShell ParseInput 診断だけを置き換える。
        /// </summary>
        /// <param name="list">構文エラー。null は空。</param>
        private void ApplyPowerShellDiagnostics(Diagnostic[] list)
        {
            this.psBucket = list;
            this.PublishBuckets();
        }

        /// <summary>
        /// VBA Compile 診断だけを置き換える。成功クリアは空配列。
        /// </summary>
        /// <param name="list">Compile 診断。null は空。</param>
        private void ApplyVbaDiagnostics(Diagnostic[] list)
        {
            this.vbaBucket = list;
            this.PublishBuckets();
        }

        private void PublishBuckets()
        {
            Diagnostic[] list = ConcatBuckets(this.csharpBucket, this.psBucket, this.vbaBucket);
            if (list.Length > 0)
            {
                this.EnsureBottomPaneVisible();
            }

            this.PublishProblemList(list);
        }

        private void PublishProblemList(Diagnostic[] list)
        {
            if (list == null)
            {
                list = new Diagnostic[0];
            }

            this.lastPublishedDiagnostics = list;
            if (this.bottomPane != null)
            {
                this.bottomPane.SetProblemItems(list);
            }

            this.ApplyEditorSquiggles(list);
        }

        private void ApplyEditorSquiggles(Diagnostic[] list)
        {
            if (this.editor == null)
            {
                return;
            }

            Document doc = this.editor.Document;
            if (doc == null)
            {
                this.editor.SetErrorDiagnostics(new Diagnostic[0]);
                return;
            }

            if (list == null)
            {
                list = this.lastPublishedDiagnostics;
            }

            if (list == null)
            {
                this.editor.SetErrorDiagnostics(new Diagnostic[0]);
                return;
            }

            List<Diagnostic> matched = new List<Diagnostic>();
            int i = 0;
            while (i < list.Length)
            {
                Diagnostic d = list[i];
                i++;
                if (d == null || !d.IsError)
                {
                    continue;
                }

                if (DiagnosticMatchesDocument(d, doc))
                {
                    matched.Add(d);
                }
            }

            this.editor.SetErrorDiagnostics(matched.ToArray());
        }

        private static bool DiagnosticMatchesDocument(Diagnostic d, Document doc)
        {
            if (d == null || doc == null)
            {
                return false;
            }

            if (string.IsNullOrEmpty(d.FilePath))
            {
                if (!string.IsNullOrEmpty(doc.FilePath))
                {
                    return false;
                }

                if (string.IsNullOrEmpty(d.FilePath) && !string.IsNullOrEmpty(doc.DisplayName))
                {
                    return true;
                }

                return true;
            }

            if (string.IsNullOrEmpty(doc.FilePath))
            {
                return string.Equals(d.FilePath, doc.DisplayName, StringComparison.OrdinalIgnoreCase);
            }

            return string.Equals(d.FilePath, doc.FilePath, StringComparison.OrdinalIgnoreCase);
        }

        private static Diagnostic[] ConcatBuckets(Diagnostic[] a, Diagnostic[] b, Diagnostic[] c)
        {
            int na = (a == null) ? 0 : a.Length;
            int nb = (b == null) ? 0 : b.Length;
            int nc = (c == null) ? 0 : c.Length;
            Diagnostic[] list = new Diagnostic[na + nb + nc];
            int o = 0;
            int i;
            if (a != null)
            {
                i = 0;
                while (i < a.Length)
                {
                    list[o] = a[i];
                    o++;
                    i++;
                }
            }

            if (b != null)
            {
                i = 0;
                while (i < b.Length)
                {
                    list[o] = b[i];
                    o++;
                    i++;
                }
            }

            if (c != null)
            {
                i = 0;
                while (i < c.Length)
                {
                    list[o] = c[i];
                    o++;
                    i++;
                }
            }

            return list;
        }

        private void InvalidateLiveCsc()
        {
            this.liveGeneration++;
            this.csharpDiagSeq++;
            if (this.liveCscRunner != null)
            {
                this.liveCscRunner.Kill();
            }
        }

        /// <summary>
        /// 左ペインを展開し、未設定なら初期幅 260 DIP を入れる。フォーカスは移さない。
        /// </summary>
        private void EnsureLeftPaneVisible()
        {
            if (this.split == null)
            {
                return;
            }

            this.split.Panel1Collapsed = false;
            this.TryApplyInitialLeftPaneWidth();
        }

        /// <summary>
        /// 左ペインが展開済みで幅が足りるとき、初回だけ 260 DIP を入れる。
        /// </summary>
        private void TryApplyInitialLeftPaneWidth()
        {
            if (this.split == null || this.split.Panel1Collapsed || this.leftSplitterInitialized)
            {
                return;
            }

            int dpi = DpiUtil.GetDpi(this.IsHandleCreated ? this.Handle : IntPtr.Zero);
            int dist = DpiUtil.ToPixels(260, dpi);
            if (this.split.Width > dist)
            {
                this.split.SplitterDistance = dist;
                this.leftSplitterInitialized = true;
            }
        }

        /// <summary>
        /// 起動時・Ctrl+1・表示メニューのフォーカスを編集器へ移す。
        /// </summary>
        private void FocusEditor()
        {
            if (this.editor == null)
            {
                return;
            }

            this.ActiveControl = this.editor;
            this.editor.Focus();
        }

        private void EnsureBottomPaneVisible()
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

        private void OnBottomPaneClose(object sender, EventArgs e)
        {
            if (this.bodySplit != null)
            {
                this.bodySplit.Panel2Collapsed = true;
            }
        }

        private void OnBottomPaneTerminalSelected(object sender, EventArgs e)
        {
            this.ShowTerminalPanel();
        }

        private void OnToggleTerminal()
        {
            if (this.bodySplit != null && !this.bodySplit.Panel2Collapsed && this.bottomPane != null && this.bottomPane.IsTerminalFocused)
            {
                this.bodySplit.Panel2Collapsed = true;
                if (this.editor != null)
                {
                    this.editor.Focus();
                }

                return;
            }

            this.ShowTerminalPanel();
        }

        private void OnViewTerminal(object sender, EventArgs e)
        {
            this.ShowTerminalPanel();
        }

        private void OnViewPowerShell(object sender, EventArgs e)
        {
            this.SwitchTerminalShell(ShellKind.PowerShell51);
        }

        private void OnViewCmd(object sender, EventArgs e)
        {
            this.SwitchTerminalShell(ShellKind.Cmd);
        }

        private void ShowTerminalPanel()
        {
            this.EnsureBottomPaneVisible();
            if (this.bottomPane == null)
            {
                return;
            }

            string root = (this.workspace == null) ? null : this.workspace.RootPath;
            this.bottomPane.Terminal.SetWorkspaceRoot(root);
            this.bottomPane.ShowTerminal();
            this.bottomPane.Terminal.StartIfNeeded();
        }

        private void SwitchTerminalShell(ShellKind kind)
        {
            this.EnsureBottomPaneVisible();
            if (this.bottomPane == null)
            {
                return;
            }

            string root = (this.workspace == null) ? null : this.workspace.RootPath;
            this.bottomPane.Terminal.SetWorkspaceRoot(root);
            this.bottomPane.ShowTerminal();
            if (this.bottomPane.Terminal.ShellKind == kind)
            {
                this.bottomPane.Terminal.StartIfNeeded();
            }
            else
            {
                this.bottomPane.Terminal.SwitchShell(kind);
            }

            if (this.viewPowerShellItem != null)
            {
                this.viewPowerShellItem.Checked = (kind == ShellKind.PowerShell51);
            }

            if (this.viewCmdItem != null)
            {
                this.viewCmdItem.Checked = (kind == ShellKind.Cmd);
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

        private void OnCsharpLineReceived(object sender, CsharpLineReceivedEventArgs e)
        {
            if (e == null)
            {
                return;
            }

            if (this.InvokeRequired)
            {
                this.BeginInvoke(new MethodInvoker(delegate
                {
                    this.OnCsharpLineReceived(sender, e);
                }));
                return;
            }

            if (this.IsDisposed || e.Generation != this.runGeneration || this.activeRunKind != ActiveRunKind.CSharp || this.bottomPane == null)
            {
                return;
            }

            this.bottomPane.Output.Append(e.Text, e.IsStderr);
        }

        private void OnCsharpExited(object sender, CsharpProcessExitedEventArgs e)
        {
            if (e == null)
            {
                return;
            }

            if (this.InvokeRequired)
            {
                this.BeginInvoke(new MethodInvoker(delegate
                {
                    this.OnCsharpExited(sender, e);
                }));
                return;
            }

            if (this.IsDisposed || e.Generation != this.runGeneration || this.activeRunKind != ActiveRunKind.CSharp || this.bottomPane == null)
            {
                return;
            }

            this.bottomPane.Output.AppendStatus("終了コード: " + e.ExitCode.ToString());
        }

        private void OnCsharpStartFailed(object sender, CsharpStartFailedEventArgs e)
        {
            if (e == null)
            {
                return;
            }

            if (this.InvokeRequired)
            {
                this.BeginInvoke(new MethodInvoker(delegate
                {
                    this.OnCsharpStartFailed(sender, e);
                }));
                return;
            }

            if (this.IsDisposed || e.Generation != this.runGeneration || this.activeRunKind != ActiveRunKind.CSharp || this.bottomPane == null)
            {
                return;
            }

            this.EnsureBottomPaneVisible();
            this.bottomPane.ShowOutput();
            this.bottomPane.Output.Append(e.Message, true);
        }

        private void OnPowerShellLineReceived(object sender, PowerShellLineReceivedEventArgs e)
        {
            if (e == null)
            {
                return;
            }

            if (this.InvokeRequired)
            {
                this.BeginInvoke(new MethodInvoker(delegate
                {
                    this.OnPowerShellLineReceived(sender, e);
                }));
                return;
            }

            if (this.IsDisposed || e.Generation != this.runGeneration || this.activeRunKind != ActiveRunKind.PowerShell || this.bottomPane == null)
            {
                return;
            }

            this.bottomPane.Output.Append(e.Text, e.IsStderr);
        }

        private void OnPowerShellExited(object sender, PowerShellProcessExitedEventArgs e)
        {
            if (e == null)
            {
                return;
            }

            if (this.InvokeRequired)
            {
                this.BeginInvoke(new MethodInvoker(delegate
                {
                    this.OnPowerShellExited(sender, e);
                }));
                return;
            }

            if (this.IsDisposed || e.Generation != this.runGeneration || this.activeRunKind != ActiveRunKind.PowerShell || this.bottomPane == null)
            {
                return;
            }

            this.bottomPane.Output.AppendStatus("終了コード: " + e.ExitCode.ToString());
        }

        private void OnPowerShellStartFailed(object sender, PowerShellStartFailedEventArgs e)
        {
            if (e == null)
            {
                return;
            }

            if (this.InvokeRequired)
            {
                this.BeginInvoke(new MethodInvoker(delegate
                {
                    this.OnPowerShellStartFailed(sender, e);
                }));
                return;
            }

            if (this.IsDisposed || e.Generation != this.runGeneration || this.activeRunKind != ActiveRunKind.PowerShell || this.bottomPane == null)
            {
                return;
            }

            this.EnsureBottomPaneVisible();
            this.bottomPane.ShowOutput();
            this.bottomPane.Output.Append(e.Message, true);
        }

        private void OnCmdLineReceived(object sender, CmdLineReceivedEventArgs e)
        {
            if (e == null)
            {
                return;
            }

            if (this.InvokeRequired)
            {
                this.BeginInvoke(new MethodInvoker(delegate
                {
                    this.OnCmdLineReceived(sender, e);
                }));
                return;
            }

            if (this.IsDisposed || e.Generation != this.runGeneration || this.activeRunKind != ActiveRunKind.Cmd || this.bottomPane == null)
            {
                return;
            }

            this.bottomPane.Output.Append(e.Text, e.IsStderr);
        }

        private void OnCmdExited(object sender, CmdProcessExitedEventArgs e)
        {
            if (e == null)
            {
                return;
            }

            if (this.InvokeRequired)
            {
                this.BeginInvoke(new MethodInvoker(delegate
                {
                    this.OnCmdExited(sender, e);
                }));
                return;
            }

            if (this.IsDisposed || e.Generation != this.runGeneration || this.activeRunKind != ActiveRunKind.Cmd || this.bottomPane == null)
            {
                return;
            }

            this.bottomPane.Output.AppendStatus("終了コード: " + e.ExitCode.ToString());
        }

        private void OnCmdStartFailed(object sender, CmdStartFailedEventArgs e)
        {
            if (e == null)
            {
                return;
            }

            if (this.InvokeRequired)
            {
                this.BeginInvoke(new MethodInvoker(delegate
                {
                    this.OnCmdStartFailed(sender, e);
                }));
                return;
            }

            if (this.IsDisposed || e.Generation != this.runGeneration || this.activeRunKind != ActiveRunKind.Cmd || this.bottomPane == null)
            {
                return;
            }

            this.EnsureBottomPaneVisible();
            this.bottomPane.ShowOutput();
            this.bottomPane.Output.Append(e.Message, true);
        }

        private void OnGotoDefinition(object sender, EventArgs e)
        {
            this.HideHover();
            if (this.editor == null || this.editor.Document == null || this.editor.Document.Buffer == null)
            {
                return;
            }

            Document doc = this.editor.Document;
            string root = (this.workspace == null) ? null : this.workspace.RootPath;
            DeclaredSymbol symbol;
            if (!DefinitionResolver.TryResolve(doc.Language, doc.Buffer, doc.HighlightSession, doc.FilePath, root, doc.CaretLine, doc.CaretColumn, this.CollectOpenBuffers(), out symbol) || symbol == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(symbol.FilePath))
            {
                bool same = false;
                if (!string.IsNullOrEmpty(doc.FilePath))
                {
                    try
                    {
                        same = string.Equals(Path.GetFullPath(symbol.FilePath), Path.GetFullPath(doc.FilePath), StringComparison.OrdinalIgnoreCase);
                    }
                    catch (Exception)
                    {
                        same = string.Equals(symbol.FilePath, doc.FilePath, StringComparison.OrdinalIgnoreCase);
                    }
                }

                if (!same)
                {
                    string openError;
                    if (!this.TryOpenFile(symbol.FilePath, false, out openError))
                    {
                        return;
                    }
                }
            }

            if (this.editor == null)
            {
                return;
            }

            BufferPoint start = new BufferPoint(symbol.Line, symbol.Column);
            BufferPoint end = new BufferPoint(symbol.Line, symbol.Column + symbol.Length);
            this.editor.SelectRange(start, end);
            this.editor.Focus();
        }

        private void OnQuickInfo(object sender, EventArgs e)
        {
            if (this.editor == null || this.editor.Document == null)
            {
                return;
            }

            IdentifierHit hit;
            Point below;
            Point above;
            int minX;
            if (!this.editor.TryGetHoverAnchorAtCaret(out hit, out below, out above, out minX))
            {
                this.HideHover();
                return;
            }

            this.ShowHoverAt(hit.Line, hit.Column, below, above, minX);
        }

        private void OnInsertDocFrame(object sender, EventArgs e)
        {
            this.HideHover();
            if (this.editor == null)
            {
                return;
            }

            this.editor.InsertDocFrame();
            this.editor.Focus();
        }

        private void OnEditorHoverIdle(object sender, HoverIdleEventArgs e)
        {
            if (e == null || e.Hit == null)
            {
                this.HideHover();
                return;
            }

            this.ShowHoverAt(e.Hit.Line, e.Hit.Column, e.BelowScreen, e.AboveScreen, e.MinScreenX);
        }

        private void OnEditorHoverCancel(object sender, EventArgs e)
        {
            if (this.hoverInfo != null && this.hoverInfo.IsMouseOverPopup)
            {
                return;
            }

            this.HideHover();
        }

        private void ShowHoverAt(int line, int column, Point below, Point above, int minX)
        {
            if (this.editor == null || this.editor.Document == null)
            {
                return;
            }

            Document doc = this.editor.Document;
            string root = (this.workspace == null) ? null : this.workspace.RootPath;
            string text;
            if (!HoverText.TryGet(doc.Language, doc.Buffer, doc.HighlightSession, doc.FilePath, root, line, column, this.CollectOpenBuffers(), out text) || string.IsNullOrEmpty(text))
            {
                this.HideHover();
                return;
            }

            if (this.hoverInfo == null)
            {
                this.hoverInfo = new HoverInfoControl(this.fonts);
                this.hoverInfo.Owner = this;
            }

            this.hoverInfo.ShowText(text, doc.Language, below, above, minX);
        }

        private Dictionary<string, TextBuffer> CollectOpenBuffers()
        {
            Dictionary<string, TextBuffer> map = new Dictionary<string, TextBuffer>(StringComparer.OrdinalIgnoreCase);
            if (this.tabs == null)
            {
                return map;
            }

            int i = 0;
            while (i < this.tabs.Tabs.Count)
            {
                Document d = this.tabs.Tabs[i];
                i++;
                if (d == null || string.IsNullOrEmpty(d.FilePath) || d.Buffer == null)
                {
                    continue;
                }

                map[d.FilePath] = d.Buffer;
            }

            return map;
        }

        private bool IsHoverVisible()
        {
            return this.hoverInfo != null && this.hoverInfo.Visible;
        }

        private void HideHover()
        {
            if (this.hoverInfo != null)
            {
                this.hoverInfo.Hide();
            }
        }

        private void OnEditorCaret(object sender, EventArgs e)
        {
            this.HideHover();
            this.UpdateStatus();
        }

        private bool IsDebugging()
        {
            return this.IsPsDebugging() || this.IsCsDebugging();
        }

        private bool IsPsDebugging()
        {
            return this.psDebugger != null && this.psDebugger.State != DebugSessionState.Idle;
        }

        private bool IsCsDebugging()
        {
            if (this.pendingDebugAfterBuild)
            {
                return true;
            }

            return this.corDebug != null && this.corDebug.State != DebugSessionState.Idle;
        }

        private static bool IsBreakpointPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            string ext = Path.GetExtension(path);
            if (string.Equals(ext, ".ps1", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return string.Equals(ext, ".cs", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPs1Path(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            return string.Equals(Path.GetExtension(path), ".ps1", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsCsPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            return string.Equals(Path.GetExtension(path), ".cs", StringComparison.OrdinalIgnoreCase);
        }

        private void RefreshBreakpointMarks()
        {
            if (this.editor == null)
            {
                return;
            }

            string path = null;
            if (this.editor.Document != null)
            {
                path = this.editor.Document.FilePath;
            }

            int[] lines1 = (this.breakpointStore == null) ? new int[0] : this.breakpointStore.GetLines(path);
            int[] lines0 = new int[lines1.Length];
            int i = 0;
            while (i < lines1.Length)
            {
                lines0[i] = lines1[i] - 1;
                i++;
            }

            this.editor.SetBreakpointLines(lines0);
        }

        private void OnViewDebug(object sender, EventArgs e)
        {
            this.ShowDebugPanel();
        }

        private void ShowDebugPanel()
        {
            this.EnsureBottomPaneVisible();
            if (this.bottomPane == null)
            {
                return;
            }

            this.bottomPane.ShowDebug();
        }

        private void OnEditorBreakpointToggle(object sender, GutterBreakpointEventArgs e)
        {
            if (e == null || this.editor == null || this.editor.Document == null)
            {
                return;
            }

            string path = this.editor.Document.FilePath;
            if (!IsBreakpointPath(path))
            {
                return;
            }

            this.breakpointStore.Toggle(path, e.Line + 1);
            this.RefreshBreakpointMarks();
        }

        private void OnDebugToggleBreakpoint(object sender, EventArgs e)
        {
            if (this.editor == null || this.editor.Document == null)
            {
                return;
            }

            string path = this.editor.Document.FilePath;
            if (!IsBreakpointPath(path))
            {
                return;
            }

            int line1 = this.editor.Document.CaretLine + 1;
            this.breakpointStore.Toggle(path, line1);
            this.RefreshBreakpointMarks();
        }

        private void OnDebugStartContinue(object sender, EventArgs e)
        {
            DebugSessionState ps = (this.psDebugger == null) ? DebugSessionState.Idle : this.psDebugger.State;
            DebugSessionState cs = (this.corDebug == null) ? DebugSessionState.Idle : this.corDebug.State;
            if (ps == DebugSessionState.Running || cs == DebugSessionState.Running || this.pendingDebugAfterBuild)
            {
                return;
            }

            if (ps == DebugSessionState.Stopped)
            {
                this.psDebugger.Continue();
                return;
            }

            if (cs == DebugSessionState.Stopped)
            {
                this.corDebug.Continue();
                return;
            }

            string path = null;
            if (this.editor != null && this.editor.Document != null)
            {
                path = this.editor.Document.FilePath;
            }

            if (string.IsNullOrEmpty(path))
            {
                this.ShowSynthetic("無題はデバッグできない。");
                return;
            }

            if (IsPs1Path(path))
            {
                this.StartPsDebug(path);
                return;
            }

            if (IsCsPath(path))
            {
                this.StartCsDebug(path);
                return;
            }

            this.ShowSynthetic("C# のデバッグはディスク上の .cs、PowerShell はディスク上の .ps1 だけです。");
        }

        private void OnDebugStop(object sender, EventArgs e)
        {
            if (this.psDebugger != null && this.psDebugger.State != DebugSessionState.Idle)
            {
                this.psDebugger.Stop();
            }

            if (this.corDebug != null && this.corDebug.State != DebugSessionState.Idle)
            {
                this.corDebug.Stop();
            }

            if (this.pendingDebugAfterBuild)
            {
                this.pendingDebugAfterBuild = false;
                this.buildGeneration++;
                if (this.cscRunner != null)
                {
                    this.cscRunner.Kill();
                }
            }

            if (this.liveTimer != null && !this.IsDebugging())
            {
                this.liveTimer.Interval = LiveDiagnoseDebounceMs;
                this.liveTimer.Stop();
                this.liveTimer.Start();
            }
        }

        private void OnDebugStepOver(object sender, EventArgs e)
        {
            if (this.psDebugger != null && this.psDebugger.State == DebugSessionState.Stopped)
            {
                this.psDebugger.StepOver();
                return;
            }

            if (this.corDebug != null && this.corDebug.State == DebugSessionState.Stopped)
            {
                this.corDebug.StepOver();
            }
        }

        private void OnDebugStepInto(object sender, EventArgs e)
        {
            if (this.psDebugger != null && this.psDebugger.State == DebugSessionState.Stopped)
            {
                this.psDebugger.StepInto();
                return;
            }

            if (this.corDebug != null && this.corDebug.State == DebugSessionState.Stopped)
            {
                this.corDebug.StepInto();
            }
        }

        private void StartPsDebug(string path)
        {
            string full;
            try
            {
                full = Path.GetFullPath(path);
            }
            catch (Exception)
            {
                this.ShowSynthetic("PowerShell のデバッグはディスク上の .ps1 だけです。");
                return;
            }

            if (this.editor != null && this.editor.Document != null && this.editor.Document.IsDirty)
            {
                try
                {
                    if (!this.editor.Document.Save())
                    {
                        this.ShowSynthetic("保存に失敗した: " + this.editor.Document.FilePath);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    this.ShowSynthetic("保存に失敗した: " + ex.Message);
                    return;
                }

                if (this.tabs != null)
                {
                    this.tabs.RefreshTabs();
                }

                this.UpdateStatus();
            }

            this.pendingLaunchAfterBuild = false;
            this.pendingDebugAfterBuild = false;
            this.buildGeneration++;
            if (this.cscRunner != null)
            {
                this.cscRunner.Kill();
            }

            this.InvalidateLiveCsc();
            if (this.liveTimer != null)
            {
                this.liveTimer.Stop();
            }

            if (this.csharpHost != null)
            {
                this.csharpHost.Kill();
            }

            if (this.powershellHost != null)
            {
                this.powershellHost.Kill();
            }

            if (this.cmdHost != null)
            {
                this.cmdHost.Kill();
            }

            if (this.corDebug != null)
            {
                this.corDebug.Stop();
            }

            this.ShowDebugPanel();
            if (this.bottomPane != null)
            {
                this.bottomPane.DebugPane.ClearLocals();
                this.bottomPane.DebugPane.ConsolePanel.Clear();
                this.bottomPane.DebugPane.ConsolePanel.AppendStatus("起動: " + full);
            }

            this.debugGeneration++;
            string scriptDir = Path.GetDirectoryName(full);
            this.psDebugger.Start(full, scriptDir, this.debugGeneration);
        }

        private void StartCsDebug(string path)
        {
            string full;
            try
            {
                full = Path.GetFullPath(path);
            }
            catch (Exception)
            {
                this.ShowSynthetic("C# のデバッグはディスク上の .cs、PowerShell はディスク上の .ps1 だけです。");
                return;
            }

            if (!File.Exists(full))
            {
                this.ShowSynthetic("C# のデバッグはディスク上の .cs、PowerShell はディスク上の .ps1 だけです。");
                return;
            }

            if (this.editor != null && this.editor.Document != null && this.editor.Document.IsDirty)
            {
                try
                {
                    if (!this.editor.Document.Save())
                    {
                        this.ShowSynthetic("保存に失敗した: " + this.editor.Document.FilePath);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    this.ShowSynthetic("保存に失敗した: " + ex.Message);
                    return;
                }

                if (this.tabs != null)
                {
                    this.tabs.RefreshTabs();
                }

                this.UpdateStatus();
            }

            this.pendingLaunchAfterBuild = false;
            this.pendingDebugAfterBuild = true;
            if (this.cscRunner != null)
            {
                this.cscRunner.Kill();
            }

            this.InvalidateLiveCsc();
            if (this.liveTimer != null)
            {
                this.liveTimer.Stop();
            }

            if (this.csharpHost != null)
            {
                this.csharpHost.Kill();
            }

            if (this.powershellHost != null)
            {
                this.powershellHost.Kill();
            }

            if (this.cmdHost != null)
            {
                this.cmdHost.Kill();
            }

            if (this.psDebugger != null)
            {
                this.psDebugger.Stop();
            }

            this.ShowDebugPanel();
            if (this.bottomPane != null)
            {
                this.bottomPane.DebugPane.ClearLocals();
                this.bottomPane.DebugPane.ConsolePanel.Clear();
            }

            this.debugGeneration++;
            if (!this.StartManualBuild())
            {
                this.pendingDebugAfterBuild = false;
                if (this.liveTimer != null)
                {
                    this.liveTimer.Interval = LiveDiagnoseDebounceMs;
                    this.liveTimer.Stop();
                    this.liveTimer.Start();
                }
            }
        }

        private void OnDebugStopped(object sender, DebugStoppedEventArgs e)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new EventHandler<DebugStoppedEventArgs>(this.OnDebugStopped), sender, e);
                return;
            }

            if (this.IsDisposed || e == null || e.Generation != this.debugGeneration)
            {
                return;
            }

            if (this.bottomPane != null)
            {
                this.bottomPane.DebugPane.SetStoppedInfo(e.Frames, e.Variables);
            }

            string stopPath = e.Path;
            if (!string.IsNullOrEmpty(stopPath) && File.Exists(stopPath))
            {
                string openError;
                if (this.TryOpenFile(stopPath, false, out openError))
                {
                    this.GoToDebugLine(e.Line);
                }
            }
            else if (this.bottomPane != null)
            {
                string missing = string.IsNullOrEmpty(stopPath) ? "(不明)" : stopPath;
                this.bottomPane.DebugPane.ConsolePanel.AppendStatus("停止位置のファイルを開けない: " + missing);
            }
        }

        private void GoToDebugLine(int line1)
        {
            if (this.editor == null || this.editor.Document == null || this.editor.Document.Buffer == null)
            {
                return;
            }

            if (line1 < 1)
            {
                return;
            }

            int line0 = line1 - 1;
            BufferPoint p = new BufferPoint(line0, 0);
            this.editor.SelectRange(p, p);
        }

        private void OnDebugConsole(object sender, DebugConsoleEventArgs e)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new EventHandler<DebugConsoleEventArgs>(this.OnDebugConsole), sender, e);
                return;
            }

            if (this.IsDisposed || e == null || e.Generation != this.debugGeneration || this.bottomPane == null)
            {
                return;
            }

            if (e.Kind == 1)
            {
                this.bottomPane.DebugPane.ConsolePanel.Append(e.Line, true);
            }
            else if (e.Kind == 2)
            {
                this.bottomPane.DebugPane.ConsolePanel.AppendStatus(e.Line);
            }
            else
            {
                this.bottomPane.DebugPane.ConsolePanel.Append(e.Line, false);
            }
        }

        private void OnDebugEnded(object sender, DebugEndedEventArgs e)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new EventHandler<DebugEndedEventArgs>(this.OnDebugEnded), sender, e);
                return;
            }

            if (this.IsDisposed || e == null || e.Generation != this.debugGeneration)
            {
                return;
            }

            if (this.bottomPane != null)
            {
                this.bottomPane.DebugPane.ClearLocals();
                this.bottomPane.DebugPane.ConsolePanel.AppendStatus("終了");
            }

            if (this.liveTimer != null && !this.IsDebugging())
            {
                this.liveTimer.Interval = LiveDiagnoseDebounceMs;
                this.liveTimer.Stop();
                this.liveTimer.Start();
            }
        }

        private void OnEditorChanged(object sender, EventArgs e)
        {
            this.tabs.RefreshTabs();
            this.UpdateStatus();
            this.RefreshFindCount();
            if (this.editor != null && this.editor.IsComposing)
            {
                if (this.liveTimer != null)
                {
                    this.liveTimer.Stop();
                }

                return;
            }

            if (this.IsDebugging())
            {
                if (this.liveTimer != null)
                {
                    this.liveTimer.Stop();
                }

                return;
            }

            if (this.liveTimer == null)
            {
                return;
            }

            this.liveTimer.Interval = LiveDiagnoseDebounceMs;
            this.liveTimer.Stop();
            this.liveTimer.Start();
        }

        private void OnLiveTick(object sender, EventArgs e)
        {
            if (this.IsDebugging())
            {
                if (this.liveTimer != null)
                {
                    this.liveTimer.Stop();
                }

                return;
            }

            if (this.editor != null && this.editor.IsComposing)
            {
                if (this.liveTimer != null)
                {
                    this.liveTimer.Stop();
                    this.liveTimer.Start();
                }

                return;
            }

            if (this.liveTimer != null)
            {
                this.liveTimer.Stop();
            }

            LanguageKind lang = LanguageKind.Plain;
            if (this.editor != null && this.editor.Document != null)
            {
                lang = this.editor.Document.Language;
            }

            if (lang == LanguageKind.CSharp)
            {
                this.StartLiveCsc();
                return;
            }

            if (lang == LanguageKind.PowerShell)
            {
                this.ApplyPowerShellDiagnostics(this.CollectOpenPowerShellDiagnostics());
                return;
            }
        }

        private void StartLiveCsc()
        {
            LiveBuffer[] bufs = this.CollectLiveBuffers();
            string workspaceRoot = (this.workspace == null) ? null : this.workspace.RootPath;
            string focused = null;
            if (this.editor != null && this.editor.Document != null)
            {
                focused = this.editor.Document.FilePath;
            }

            LiveCompileSnapshot snapshot;
            try
            {
                snapshot = LiveCompileUnit.Build(workspaceRoot, bufs, focused);
            }
            catch (Exception)
            {
                return;
            }

            if (snapshot == null || snapshot.CscPaths == null || snapshot.CscPaths.Length == 0)
            {
                return;
            }

            this.buildGeneration++;
            this.liveGeneration++;
            this.csharpDiagSeq++;
            int seq = this.csharpDiagSeq;
            int liveGen = this.liveGeneration;
            if (this.cscRunner != null)
            {
                this.cscRunner.Kill();
            }

            if (this.liveCscRunner != null)
            {
                this.liveCscRunner.Kill();
            }

            LiveBuildRequest req = new LiveBuildRequest();
            req.LiveGeneration = liveGen;
            req.CSharpDiagSeq = seq;
            req.Snapshot = snapshot;
            req.RspPath = snapshot.RspPath;
            Thread thread = new Thread(this.LiveCscWorkerProc);
            thread.IsBackground = true;
            thread.Start(req);
        }

        private void LiveCscWorkerProc(object state)
        {
            LiveBuildRequest req = state as LiveBuildRequest;
            if (req == null || this.liveCscRunner == null)
            {
                return;
            }

            CscRunResult result = this.liveCscRunner.Run(req.RspPath, req.LiveGeneration);
            if (this.IsDisposed || !this.IsHandleCreated)
            {
                if (req.Snapshot != null)
                {
                    LiveCompileUnit.TryDeleteDirectory(req.Snapshot.OutputDir);
                }

                return;
            }

            this.BeginInvoke(new MethodInvoker(delegate
            {
                this.OnLiveCscFinished(req, result);
            }));
        }

        private void OnLiveCscFinished(LiveBuildRequest req, CscRunResult result)
        {
            if (this.IsDisposed || req == null)
            {
                return;
            }

            if (req.CSharpDiagSeq != this.csharpDiagSeq || req.LiveGeneration != this.liveGeneration)
            {
                if (req.Snapshot != null)
                {
                    LiveCompileUnit.TryDeleteDirectory(req.Snapshot.OutputDir);
                }

                return;
            }

            Diagnostic[] parsed;
            if (result == null || !string.IsNullOrEmpty(result.StartError))
            {
                string msg = (result == null || string.IsNullOrEmpty(result.StartError)) ? "csc.exe を起動できませんでした。" : result.StartError;
                parsed = new Diagnostic[] { Diagnostic.CreateSynthetic(msg) };
            }
            else
            {
                string combined = result.CombinedOutput();
                parsed = DiagnosticParser.ApplyExitCode(DiagnosticParser.Parse(combined), result.ExitCode, combined);
            }

            Diagnostic[] remapped = new Diagnostic[(parsed == null) ? 0 : parsed.Length];
            int i = 0;
            while (parsed != null && i < parsed.Length)
            {
                remapped[i] = LiveCompileUnit.Remap(parsed[i], req.Snapshot);
                i++;
            }

            this.ApplyCSharpDiagnostics(remapped);
            if (req.Snapshot != null)
            {
                LiveCompileUnit.TryDeleteDirectory(req.Snapshot.OutputDir);
            }
        }

        private LiveBuffer[] CollectLiveBuffers()
        {
            if (this.tabs == null)
            {
                return new LiveBuffer[0];
            }

            List<LiveBuffer> list = new List<LiveBuffer>();
            int i = 0;
            while (i < this.tabs.Tabs.Count)
            {
                Document d = this.tabs.Tabs[i];
                i++;
                if (d == null)
                {
                    continue;
                }

                LiveBuffer b = new LiveBuffer();
                b.FilePath = d.FilePath;
                b.DisplayName = d.DisplayName;
                b.Text = (d.Buffer == null) ? "" : d.Buffer.GetText();
                b.IsDirty = d.IsDirty;
                b.IsCSharp = d.Language == LanguageKind.CSharp;
                list.Add(b);
            }

            return list.ToArray();
        }

        private Diagnostic[] CollectOpenPowerShellDiagnostics()
        {
            List<Diagnostic> list = new List<Diagnostic>();
            if (this.tabs == null)
            {
                return list.ToArray();
            }

            int i = 0;
            while (i < this.tabs.Tabs.Count)
            {
                Document d = this.tabs.Tabs[i];
                i++;
                if (d == null || d.Language != LanguageKind.PowerShell || d.Buffer == null)
                {
                    continue;
                }

                PowerShellParseError[] errs = PowerShellParseErrors.Collect(d.Buffer.GetText());
                int e = 0;
                while (e < errs.Length)
                {
                    PowerShellParseError err = errs[e];
                    e++;
                    if (err == null)
                    {
                        continue;
                    }

                    list.Add(Diagnostic.FromCompiler(d.FilePath, err.StartLine, err.StartColumn, true, null, err.Message, err.EndLine, err.EndColumn));
                }
            }

            return list.ToArray();
        }

        /// <summary>クロム用 Font を破棄する。Renderer は所有しない。</summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this.liveTimer != null)
                {
                    this.liveTimer.Stop();
                    this.liveTimer.Tick -= this.OnLiveTick;
                    this.liveTimer.Dispose();
                    this.liveTimer = null;
                }

                if (this.liveCscRunner != null)
                {
                    this.liveCscRunner.Kill();
                }

                if (this.cscRunner != null)
                {
                    this.cscRunner.Kill();
                }

                if (this.csharpHost != null)
                {
                    this.csharpHost.LineReceived -= this.OnCsharpLineReceived;
                    this.csharpHost.Exited -= this.OnCsharpExited;
                    this.csharpHost.StartFailed -= this.OnCsharpStartFailed;
                    this.csharpHost.Kill();
                }

                if (this.powershellHost != null)
                {
                    this.powershellHost.LineReceived -= this.OnPowerShellLineReceived;
                    this.powershellHost.Exited -= this.OnPowerShellExited;
                    this.powershellHost.StartFailed -= this.OnPowerShellStartFailed;
                    this.powershellHost.Kill();
                }

                if (this.cmdHost != null)
                {
                    this.cmdHost.LineReceived -= this.OnCmdLineReceived;
                    this.cmdHost.Exited -= this.OnCmdExited;
                    this.cmdHost.StartFailed -= this.OnCmdStartFailed;
                    this.cmdHost.Kill();
                }

                if (this.psDebugger != null)
                {
                    this.psDebugger.Stopped -= this.OnDebugStopped;
                    this.psDebugger.ConsoleLine -= this.OnDebugConsole;
                    this.psDebugger.Ended -= this.OnDebugEnded;
                    this.psDebugger.Dispose();
                    this.psDebugger = null;
                }

                if (this.corDebug != null)
                {
                    this.corDebug.Stopped -= this.OnDebugStopped;
                    this.corDebug.ConsoleLine -= this.OnDebugConsole;
                    this.corDebug.Ended -= this.OnDebugEnded;
                    this.corDebug.Dispose();
                    this.corDebug = null;
                }

                if (this.bottomPane != null)
                {
                    this.bottomPane.Terminal.CloseSession();
                }

                if (this.hoverInfo != null)
                {
                    this.hoverInfo.Dispose();
                    this.hoverInfo = null;
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

                if (this.tabs != null)
                {
                    this.tabs.SetFonts(null, null);
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

        private enum ActiveRunKind
        {
            None = 0,
            CSharp = 1,
            PowerShell = 2,
            Cmd = 3
        }

        private sealed class ManualBuildRequest
        {
            public int Generation;
            public string RspPath;
            public string OutputExe;
            public string WorkingDirectory;
        }

        private sealed class LiveBuildRequest
        {
            public int LiveGeneration;
            public int CSharpDiagSeq;
            public string RspPath;
            public LiveCompileSnapshot Snapshot;
        }
    }
}
