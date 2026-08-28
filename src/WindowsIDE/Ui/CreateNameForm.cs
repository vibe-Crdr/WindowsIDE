using System;
using System.Drawing;
using System.Windows.Forms;
using WindowsIDE.Ui.Fonts;

namespace WindowsIDE.Ui
{
    /// <summary>
    /// ファイル／フォルダ名を DualFont で入力するモーダル。システム Button は使わない。
    /// </summary>
    public sealed class CreateNameForm : Form
    {
        private readonly FontLoadResult fonts;
        private readonly Font chromeHalf;
        private readonly Font chromeFull;
        private readonly DualFontField field;
        private readonly ChromeMark okMark;
        private readonly ChromeMark cancelMark;
        private Font ownedHalf;
        private Font ownedFull;
        private string enteredName;

        /// <summary>
        /// 名前ダイアログを組む。既定名はファイル untitled.txt、フォルダ NewFolder。
        /// </summary>
        /// <param name="isFolder">フォルダ作成なら true。</param>
        /// <param name="fonts">同梱フォント。null なら chrome フォントだけ使う。</param>
        /// <param name="chromeHalf">12 DIP 半角。所有権は移さない。</param>
        /// <param name="chromeFull">12 DIP 全角。所有権は移さない。</param>
        /// <param name="initialName">再表示用の名前。空なら既定名。</param>
        public CreateNameForm(bool isFolder, FontLoadResult fonts, Font chromeHalf, Font chromeFull, string initialName)
        {
            this.fonts = fonts;
            this.chromeHalf = chromeHalf;
            this.chromeFull = chromeFull;
            this.Text = isFolder ? "フォルダを作成" : "ファイルを作成";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.ShowInTaskbar = false;
            this.AutoScaleMode = AutoScaleMode.None;
            this.KeyPreview = true;
            this.BackColor = Theme.Background;
            this.ForeColor = Theme.Foreground;

            this.field = new DualFontField();
            this.field.BackColor = Theme.EditorBackground;
            this.field.ForeColor = Theme.Foreground;
            this.okMark = new ChromeMark("作成", true, false);
            this.cancelMark = new ChromeMark("キャンセル", true, false);
            this.okMark.Click += this.OnOkClick;
            this.cancelMark.Click += this.OnCancelClick;
            this.Controls.Add(this.field);
            this.Controls.Add(this.okMark);
            this.Controls.Add(this.cancelMark);

            string seed = initialName;
            if (seed != null)
            {
                seed = seed.Trim();
            }

            if (string.IsNullOrEmpty(seed))
            {
                seed = isFolder ? "NewFolder" : "untitled.txt";
            }

            this.field.Text = seed;
            this.ApplyDialogFonts();
            this.ApplyLayout();
        }

        /// <summary>OK 時の Trim 済み名前。キャンセル時は null。</summary>
        public string EnteredName
        {
            get { return this.enteredName; }
        }

        /// <summary>表示時に名前を全選択してフォーカスする。</summary>
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            this.field.SelectAll();
            this.field.Focus();
        }

        /// <summary>キャプション色を付け、12 DIP をハンドル DPI で付け直す。</summary>
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            NativeCaption.Apply(this.Handle);
            this.ApplyDialogFonts();
            this.ApplyLayout();
        }

        /// <summary>DPI 変更後に 12 DIP フォントと配置を付け直す。</summary>
        protected override void OnDpiChangedAfterParent(EventArgs e)
        {
            base.OnDpiChangedAfterParent(e);
            this.ApplyDialogFonts();
            this.ApplyLayout();
        }

        /// <summary>Enter は作成（変換中は無視）、Esc はキャンセル。AcceptButton は使わない。</summary>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e == null)
            {
                return;
            }

            if (e.KeyCode == Keys.Escape)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                this.DialogResult = DialogResult.Cancel;
                this.Close();
                return;
            }

            if (e.KeyCode == Keys.Enter)
            {
                if (this.field.IsComposing)
                {
                    return;
                }

                e.Handled = true;
                e.SuppressKeyPress = true;
                this.TryAccept();
            }
        }

        /// <summary>所有フォントを破棄する。chrome フォントは破棄しない。</summary>
        /// <param name="disposing">マネージドも捨てるなら true。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.DisposeOwnedFonts();
            }

            base.Dispose(disposing);
        }

        private void OnOkClick(object sender, EventArgs e)
        {
            this.TryAccept();
        }

        private void OnCancelClick(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        private void TryAccept()
        {
            if (this.DialogResult == DialogResult.OK)
            {
                return;
            }

            string name = this.field.Text;
            if (name == null)
            {
                name = "";
            }

            name = name.Trim();
            if (name.Length == 0)
            {
                MessageBox.Show(this, "名前を入力してください。", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.field.Focus();
                return;
            }

            this.enteredName = name;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void ApplyDialogFonts()
        {
            int dpi = DpiUtil.GetDpi(this.IsHandleCreated ? this.Handle : IntPtr.Zero);
            int px = DpiUtil.ToPixels(DpiUtil.UiFontDip, dpi);
            Font half = this.chromeHalf;
            Font full = this.chromeFull;
            Font createdHalf = null;
            Font createdFull = null;
            if (this.fonts != null)
            {
                createdHalf = this.fonts.CreateHalfWidth(px);
                createdFull = this.fonts.CreateFullWidth(px);
                half = createdHalf;
                full = createdFull;
            }

            this.field.SetFonts(half, full, DpiUtil.UiFontDip);
            this.okMark.SetFonts(half, full);
            this.cancelMark.SetFonts(half, full);
            this.DisposeOwnedFonts();
            this.ownedHalf = createdHalf;
            this.ownedFull = createdFull;
        }

        private void DisposeOwnedFonts()
        {
            if (this.ownedHalf != null)
            {
                this.ownedHalf.Dispose();
                if (object.ReferenceEquals(this.ownedFull, this.ownedHalf))
                {
                    this.ownedFull = null;
                }

                this.ownedHalf = null;
            }

            if (this.ownedFull != null)
            {
                this.ownedFull.Dispose();
                this.ownedFull = null;
            }
        }

        private void ApplyLayout()
        {
            int dpi = DpiUtil.GetDpi(this.IsHandleCreated ? this.Handle : IntPtr.Zero);
            int pad = DpiUtil.ToPixels(12, dpi);
            int gap = DpiUtil.ToPixels(4, dpi);
            int width = DpiUtil.ToPixels(360, dpi);
            int fieldH = this.field.PreferredOuterHeight;
            int btnH = fieldH;
            int minBtn = DpiUtil.ToPixels(24, dpi);
            if (btnH < minBtn)
            {
                btnH = minBtn;
            }

            Graphics g = this.CreateGraphics();
            int okW;
            int cancelW;
            try
            {
                okW = this.MeasureMark(g, this.okMark, 36);
                cancelW = this.MeasureMark(g, this.cancelMark, 36);
            }
            finally
            {
                g.Dispose();
            }

            int height = pad + fieldH + gap + btnH + pad;
            this.ClientSize = new Size(width, height);
            this.field.Bounds = new Rectangle(pad, pad, width - pad * 2, fieldH);
            int btnY = pad + fieldH + gap;
            this.okMark.Bounds = new Rectangle(pad, btnY, okW, btnH);
            this.cancelMark.Bounds = new Rectangle(pad + okW + gap, btnY, cancelW, btnH);
        }

        private int MeasureMark(Graphics g, ChromeMark mark, int minDip)
        {
            int dpi = DpiUtil.GetDpi(this.IsHandleCreated ? this.Handle : IntPtr.Zero);
            int min = DpiUtil.ToPixels(minDip, dpi);
            int pad = DpiUtil.ToPixels(10, dpi);
            Font half = this.ownedHalf;
            Font full = this.ownedFull;
            if (half == null)
            {
                half = this.chromeHalf;
            }

            if (full == null)
            {
                full = this.chromeFull;
            }
            if (g == null || half == null || full == null)
            {
                return min;
            }

            int w = (int)Math.Ceiling((double)DualFontPainter.Measure(g, mark.Caption, half, full, null)) + pad;
            if (w < min)
            {
                return min;
            }

            return w;
        }
    }
}
