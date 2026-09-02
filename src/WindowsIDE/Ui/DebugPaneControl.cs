using System;
using System.Drawing;
using System.Windows.Forms;
using WindowsIDE.Debug;
using WindowsIDE.Ui.Fonts;

namespace WindowsIDE.Ui
{
    /// <summary>
    /// デバッグタブの実体。上ローカル、下コンソール。縦 SplitContainer はセッション内（XML に書かない）。
    /// </summary>
    public sealed class DebugPaneControl : Control
    {
        private readonly SplitContainer split;
        private readonly OutputPanelControl locals;
        private readonly OutputPanelControl console;
        private bool splitterInitialized;

        /// <summary>
        /// 空のローカルとコンソールを組む。
        /// </summary>
        public DebugPaneControl()
        {
            this.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            this.TabStop = false;
            this.BackColor = Theme.Background;
            this.ForeColor = Theme.Foreground;

            this.split = new SplitContainer();
            this.split.Dock = DockStyle.Fill;
            this.split.Orientation = Orientation.Horizontal;
            this.split.BackColor = Theme.LineNumber;
            this.split.Panel1.BackColor = Theme.Background;
            this.split.Panel2.BackColor = Theme.Background;

            this.locals = new OutputPanelControl();
            this.locals.Dock = DockStyle.Fill;
            this.console = new OutputPanelControl();
            this.console.Dock = DockStyle.Fill;
            this.split.Panel1.Controls.Add(this.locals);
            this.split.Panel2.Controls.Add(this.console);
            this.Controls.Add(this.split);
        }

        /// <summary>コンソール（stdout / stderr / 起動終了）。</summary>
        public OutputPanelControl ConsolePanel
        {
            get { return this.console; }
        }

        /// <summary>
        /// ツリーと同じ 12 DIP 双フォントを使う。所有権は移さない。
        /// </summary>
        /// <param name="half">半角。</param>
        /// <param name="full">全角。</param>
        public void SetFonts(Font half, Font full)
        {
            this.locals.SetFonts(half, full);
            this.console.SetFonts(half, full);
        }

        /// <summary>
        /// ローカル一覧を置き換える。フォーカスは奪わない。
        /// </summary>
        /// <param name="variables">停止時の変数。null は空。</param>
        public void SetLocals(DebugVariable[] variables)
        {
            this.locals.Clear();
            if (variables == null)
            {
                return;
            }

            int i = 0;
            while (i < variables.Length)
            {
                DebugVariable v = variables[i];
                i++;
                if (v == null)
                {
                    continue;
                }

                this.locals.Append(v.Name + "  " + v.Text, false);
            }
        }

        /// <summary>ローカルを空にする。</summary>
        public void ClearLocals()
        {
            this.locals.Clear();
        }

        /// <summary>親の DPI 変更後にスプリッタ幅を合わせる。</summary>
        protected override void OnDpiChangedAfterParent(EventArgs e)
        {
            base.OnDpiChangedAfterParent(e);
            this.ApplySplitterChrome();
        }

        /// <summary>ハンドル作成後にスプリッタを置く。</summary>
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            this.ApplySplitterChrome();
            this.TryInitSplitter();
        }

        /// <summary>サイズ変更で初回距離を入れる。</summary>
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            this.TryInitSplitter();
        }

        private void ApplySplitterChrome()
        {
            int dpi = DpiUtil.GetDpi(this.IsHandleCreated ? this.Handle : IntPtr.Zero);
            int w = DpiUtil.ToPixels(DpiUtil.SplitterWidthDip, dpi);
            if (w < 1)
            {
                w = 1;
            }

            this.split.SplitterWidth = w;
            int min = DpiUtil.ToPixels(40, dpi);
            if (min < 1)
            {
                min = 1;
            }

            this.split.Panel1MinSize = min;
            this.split.Panel2MinSize = min;
        }

        private void TryInitSplitter()
        {
            if (this.splitterInitialized || !this.IsHandleCreated)
            {
                return;
            }

            int avail = this.split.Height;
            if (avail <= this.split.SplitterWidth)
            {
                return;
            }

            int dist = avail / 3;
            int min1 = this.split.Panel1MinSize;
            int min2 = this.split.Panel2MinSize;
            int max = avail - this.split.SplitterWidth - min2;
            if (dist < min1)
            {
                dist = min1;
            }

            if (dist > max)
            {
                dist = max;
            }

            if (dist > 0)
            {
                this.split.SplitterDistance = dist;
                this.splitterInitialized = true;
            }
        }
    }
}
