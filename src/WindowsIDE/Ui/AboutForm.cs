using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace WindowsIDE.Ui
{
    /// <summary>
    /// 製品名とフォント OFL ライセンスを表示する。
    /// </summary>
    public sealed class AboutForm : Form
    {
        private readonly Label title;
        private readonly Label note;
        private readonly TextBox box;
        private readonly Button close;
        private readonly ThemedScrollBar vScroll;
        private Font uiFont;
        private int lastDpi;
        private bool ignoreAboutScroll;
        private int wheelLeftover;
        private BoxNative boxNative;

        /// <summary>
        /// About ダイアログを作る。
        /// </summary>
        public AboutForm()
        {
            this.Text = "WindowsIDE について";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.AutoScaleMode = AutoScaleMode.None;
            this.BackColor = Theme.Background;
            this.ForeColor = Theme.Foreground;
            this.ShowInTaskbar = false;

            this.title = new Label();
            this.title.Text = "WindowsIDE";
            this.title.ForeColor = Theme.Foreground;
            this.title.BackColor = Theme.Background;
            this.title.AutoSize = true;
            this.Controls.Add(this.title);

            this.note = new Label();
            this.note.Text = "フォントは SIL Open Font License 1.1 です。OS にはインストールしません。";
            this.note.ForeColor = Theme.Comment;
            this.note.BackColor = Theme.Background;
            this.note.AutoSize = true;
            this.Controls.Add(this.note);

            this.box = new TextBox();
            this.box.Multiline = true;
            this.box.ReadOnly = true;
            this.box.WordWrap = true;
            this.box.ScrollBars = ScrollBars.Vertical;
            this.box.BorderStyle = BorderStyle.FixedSingle;
            this.box.BackColor = Theme.EditorBackground;
            this.box.ForeColor = Theme.Foreground;
            this.box.Text = LoadLicenses();
            this.box.Select(0, 0);
            this.box.MouseWheel += this.OnBoxMouseWheel;
            this.box.HandleCreated += this.OnBoxHandleCreated;
            this.Controls.Add(this.box);

            this.vScroll = new ThemedScrollBar(true);
            this.vScroll.TrackColor = Theme.EditorBackground;
            this.vScroll.SmallChange = 1;
            this.vScroll.ValueChanged += this.OnAboutScrollChanged;
            this.Controls.Add(this.vScroll);

            this.close = new Button();
            this.close.Text = "閉じる";
            this.close.DialogResult = DialogResult.OK;
            this.close.FlatStyle = FlatStyle.Flat;
            this.close.FlatAppearance.BorderColor = Theme.Border;
            this.close.BackColor = Theme.CurrentLine;
            this.close.ForeColor = Theme.Foreground;
            this.Controls.Add(this.close);
            this.close.TabIndex = 0;
            this.box.TabIndex = 1;
            this.AcceptButton = this.close;
            this.CancelButton = this.close;
            this.ActiveControl = this.close;

            this.ApplyAboutLayout();
        }

        /// <summary>表示時にライセンス欄の選択を外し、閉じるへフォーカスする。</summary>
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            this.ActiveControl = this.close;
            this.close.Focus();
            this.box.Select(0, 0);
        }

        /// <summary>ハンドル作成後に GetDpi で再配置し、キャプション色を付ける。</summary>
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            NativeCaption.Apply(this.Handle);
            this.ApplyAboutLayout();
        }

        /// <summary>親の DPI 変更後に 12 DIP Pixel とレイアウトを作り直す。</summary>
        protected override void OnDpiChangedAfterParent(EventArgs e)
        {
            base.OnDpiChangedAfterParent(e);
            this.ApplyAboutLayout();
        }

        /// <summary>
        /// ClientSize とアンカーを DIP から決める。テキストボックスはボタン帯の上に置く。
        /// </summary>
        private void ApplyAboutLayout()
        {
            int dpi = DpiUtil.GetDpi(this.IsHandleCreated ? this.Handle : IntPtr.Zero);
            if (dpi != this.lastDpi || this.uiFont == null)
            {
                this.lastDpi = dpi;
                this.RecreateUiFont();
            }
            int pad = DpiUtil.ToPixels(12, dpi);
            int band = DpiUtil.ToPixels(48, dpi);
            int gap = DpiUtil.ToPixels(8, dpi);

            int clientW = DpiUtil.ToPixels(720, dpi);
            int clientH = DpiUtil.ToPixels(540, dpi);
            Rectangle wa;
            if (this.IsHandleCreated)
            {
                wa = Screen.FromHandle(this.Handle).WorkingArea;
            }
            else
            {
                wa = Screen.PrimaryScreen.WorkingArea;
            }

            int maxW = wa.Width * 9 / 10;
            int maxH = wa.Height * 9 / 10;
            if (clientH > maxH)
            {
                clientH = maxH;
            }

            if (clientW > maxW)
            {
                clientW = maxW;
            }

            this.Padding = new Padding(pad);
            this.ClientSize = new Size(clientW, clientH);

            int innerWidth = this.ClientSize.Width - pad - pad;
            if (innerWidth < 1)
            {
                innerWidth = 1;
            }

            this.title.AutoSize = true;
            this.title.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            this.title.Location = new Point(pad, pad);

            this.note.AutoSize = true;
            this.note.MaximumSize = new Size(innerWidth, 0);
            this.note.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            this.note.Location = new Point(pad, this.title.Bottom + gap);

            int btnH = DpiUtil.ToPixels(32, dpi);
            int fromFont = this.close.Font.Height + DpiUtil.ToPixels(8, dpi);
            if (fromFont > btnH)
            {
                btnH = fromFont;
            }

            int btnW = DpiUtil.ToPixels(100, dpi);
            int textW = TextRenderer.MeasureText(this.close.Text, this.close.Font).Width;
            int fromText = textW + DpiUtil.ToPixels(16, dpi);
            if (fromText > btnW)
            {
                btnW = fromText;
            }

            this.close.Size = new Size(btnW, btnH);
            this.close.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            int closeX = this.ClientSize.Width - pad - btnW;
            int closeY = this.ClientSize.Height - pad - btnH;
            if (closeX < pad)
            {
                closeX = pad;
            }

            if (closeY < pad)
            {
                closeY = pad;
            }

            this.close.Location = new Point(closeX, closeY);

            int boxTop = this.note.Bottom + gap;
            int boxBottom = this.ClientSize.Height - band;
            if (boxBottom > this.close.Top)
            {
                boxBottom = this.close.Top;
            }

            int boxHeight = boxBottom - boxTop;
            if (boxHeight < 1)
            {
                boxHeight = 1;
            }

            this.box.Location = new Point(pad, boxTop);
            this.box.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
            this.box.Size = new Size(innerWidth, boxHeight);
            this.ApplyAboutScroll(pad, boxTop, innerWidth, boxHeight, dpi);
        }

        private void RecreateUiFont()
        {
            int dpi = DpiUtil.GetDpi(this.IsHandleCreated ? this.Handle : IntPtr.Zero);
            this.lastDpi = dpi;
            int px = DpiUtil.ToPixels(DpiUtil.UiFontDip, dpi);
            FontFamily family = SystemFonts.MessageBoxFont.FontFamily;
            Font created = new Font(family, px, FontStyle.Regular, GraphicsUnit.Pixel);
            this.title.Font = created;
            this.note.Font = created;
            this.box.Font = created;
            this.close.Font = created;
            if (this.uiFont != null)
            {
                this.uiFont.Dispose();
            }

            this.uiFont = created;
        }

        private void OnBoxHandleCreated(object sender, EventArgs e)
        {
            this.AttachBoxNative();
            this.HideNativeBars();
            this.ApplyAboutLayout();
        }

        private void AttachBoxNative()
        {
            if (!this.box.IsHandleCreated)
            {
                return;
            }

            if (this.boxNative != null)
            {
                this.boxNative.ReleaseHandle();
                this.boxNative = null;
            }

            this.boxNative = new BoxNative(this);
            this.boxNative.AssignHandle(this.box.Handle);
        }

        private void HideNativeBars()
        {
            if (!this.box.IsHandleCreated)
            {
                return;
            }

            Native.ShowScrollBar(this.box.Handle, Native.SB_BOTH, false);
        }

        private void ApplyAboutScroll(int pad, int boxTop, int innerWidth, int boxHeight, int dpi)
        {
            int bar = DpiUtil.ToPixels(DpiUtil.ScrollBarThicknessDip, dpi);
            bool needV = false;
            int nPos = 0;
            if (this.box.IsHandleCreated)
            {
                Native.SCROLLINFO info;
                Native.TryGetScroll(this.box.Handle, Native.SB_VERT, out info);
                nPos = info.nPos;
                this.HideNativeBars();
                this.box.Width = innerWidth;
                needV = this.AboutBarNeeded();
                if (needV)
                {
                    this.box.Width = Math.Max(1, innerWidth - bar);
                    needV = this.AboutBarNeeded();
                    if (!needV)
                    {
                        this.box.Width = innerWidth;
                    }
                }
            }

            int barW = needV ? bar : 0;
            this.vScroll.Visible = needV;
            this.vScroll.Bounds = new Rectangle(pad + this.box.Width, boxTop, barW, boxHeight);
            this.vScroll.BringToFront();
            if (this.box.IsHandleCreated)
            {
                this.SyncAboutBarFromEdit();
                if (needV)
                {
                    this.ignoreAboutScroll = true;
                    try
                    {
                        this.vScroll.Value = nPos;
                    }
                    finally
                    {
                        this.ignoreAboutScroll = false;
                    }
                }
                else
                {
                    this.SetEditScroll(0);
                }

                this.HideNativeBars();
            }
        }

        private bool AboutBarNeeded()
        {
            if (!this.box.IsHandleCreated)
            {
                return false;
            }

            int lines = Native.GetEditLineCount(this.box.Handle);
            int visible = this.GetVisibleEditLines();
            return DpiUtil.ScrollBarNeeded(0, Math.Max(0, lines - 1), visible);
        }

        private int GetVisibleEditLines()
        {
            int lineH = (this.box.Font != null) ? this.box.Font.Height : 1;
            if (lineH < 1)
            {
                lineH = 1;
            }

            int n = this.box.ClientSize.Height / lineH;
            if (n < 1)
            {
                n = 1;
            }

            return n;
        }

        private void SyncAboutBarFromEdit()
        {
            if (this.ignoreAboutScroll || !this.box.IsHandleCreated)
            {
                return;
            }

            Native.SCROLLINFO info;
            bool has = Native.TryGetScroll(this.box.Handle, Native.SB_VERT, out info);
            this.HideNativeBars();
            int lines = Native.GetEditLineCount(this.box.Handle);
            int visible = this.GetVisibleEditLines();
            this.ignoreAboutScroll = true;
            try
            {
                this.vScroll.Minimum = 0;
                this.vScroll.Maximum = Math.Max(0, lines - 1);
                this.vScroll.LargeChange = visible;
                if (this.vScroll.Visible && has)
                {
                    this.vScroll.Value = info.nPos;
                }
                else if (!this.vScroll.Visible)
                {
                    this.vScroll.Value = 0;
                }
            }
            finally
            {
                this.ignoreAboutScroll = false;
            }
        }

        private void OnAboutScrollChanged(object sender, EventArgs e)
        {
            if (this.ignoreAboutScroll || !this.box.IsHandleCreated)
            {
                return;
            }

            this.SetEditScroll(this.vScroll.Value);
        }

        private void SetEditScroll(int pos)
        {
            if (!this.box.IsHandleCreated)
            {
                return;
            }

            Native.SCROLLINFO si = new Native.SCROLLINFO();
            si.cbSize = (uint)Marshal.SizeOf(typeof(Native.SCROLLINFO));
            si.fMask = Native.SIF_POS;
            si.nPos = pos;
            Native.SetScrollInfo(this.box.Handle, Native.SB_VERT, ref si, true);
            int wParam = Native.SB_THUMBPOSITION | (pos << 16);
            this.ignoreAboutScroll = true;
            try
            {
                Native.SendMessage(this.box.Handle, Native.WM_VSCROLL, new IntPtr(wParam), IntPtr.Zero);
            }
            finally
            {
                this.ignoreAboutScroll = false;
            }

            this.HideNativeBars();
        }

        private void OnBoxMouseWheel(object sender, MouseEventArgs e)
        {
            int notches = DpiUtil.WheelNotches(e.Delta, ref this.wheelLeftover);
            if (notches != 0 && this.vScroll.Visible)
            {
                this.vScroll.Value = this.vScroll.Value + (-notches * this.vScroll.SmallChange);
            }

            HandledMouseEventArgs he = e as HandledMouseEventArgs;
            if (he != null)
            {
                he.Handled = true;
            }
        }

        /// <summary>所有している 12 DIP Pixel フォントを破棄する。</summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this.boxNative != null)
                {
                    this.boxNative.ReleaseHandle();
                    this.boxNative = null;
                }

                if (this.uiFont != null)
                {
                    this.uiFont.Dispose();
                    this.uiFont = null;
                }
            }

            base.Dispose(disposing);
        }

        private static string LoadLicenses()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== Cascadia Mono ===");
            sb.AppendLine(ReadResource("WindowsIDE.Fonts.CascadiaLicense"));
            sb.AppendLine();
            sb.AppendLine("=== Source Han Sans JP ===");
            sb.AppendLine(ReadResource("WindowsIDE.Fonts.SourceHanSansLicense"));
            return sb.ToString();
        }

        private static string ReadResource(string name)
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            Stream stream = asm.GetManifestResourceStream(name);
            if (stream == null)
            {
                return "(ライセンスを埋め込みリソースから読めませんでした: " + name + ")";
            }

            using (stream)
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }

        private void OnBoxNativeVScroll()
        {
            this.SyncAboutBarFromEdit();
        }

        private sealed class BoxNative : NativeWindow
        {
            private readonly AboutForm owner;

            public BoxNative(AboutForm owner)
            {
                this.owner = owner;
            }

            protected override void WndProc(ref Message m)
            {
                base.WndProc(ref m);
                if (m.Msg == Native.WM_VSCROLL)
                {
                    this.owner.OnBoxNativeVScroll();
                }
            }
        }

        private static class Native
        {
            public const int SB_HORZ = 0;
            public const int SB_VERT = 1;
            public const int SB_BOTH = 3;
            public const int SB_THUMBPOSITION = 4;
            public const int WM_VSCROLL = 0x0115;
            public const int EM_GETLINECOUNT = 0x00BA;
            public const uint SIF_POS = 0x0004;
            public const uint SIF_ALL = 0x17;

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

            public static int GetEditLineCount(IntPtr hwnd)
            {
                return SendMessage(hwnd, EM_GETLINECOUNT, IntPtr.Zero, IntPtr.Zero).ToInt32();
            }
        }
    }
}
