using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;
using WindowsIDE.Editor;
using WindowsIDE.Ui.Fonts;

namespace WindowsIDE.Ui
{
    /// <summary>
    /// 単一行の DualFont 入力。FindBar の検索・置換欄。TextView は継承しない。
    /// </summary>
    public sealed class DualFontField : Control, IImeClient
    {
        private string text;
        private int caret;
        private int selAnchor;
        private int scrollX;
        private bool selecting;
        private bool caretVisible;
        private Font halfFont;
        private Font fullFont;
        private int fontSizeDip;
        private string imeComposition;
        private int imeCursor;
        private Timer caretTimer;
        private string undoText;
        private int undoCaret;
        private int undoAnchor;
        private bool hasUndo;

        /// <summary>
        /// 空の単一行欄を組む。フォントは所有しない。
        /// </summary>
        public DualFontField()
        {
            this.text = "";
            this.imeComposition = "";
            this.undoText = "";
            this.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable | ControlStyles.ResizeRedraw | ControlStyles.EnableNotifyMessage, true);
            this.TabStop = true;
            this.BackColor = Theme.EditorBackground;
            this.ForeColor = Theme.Foreground;
            this.ImeMode = ImeMode.On;
            this.Cursor = Cursors.IBeam;
            this.caretTimer = new Timer();
            this.caretTimer.Interval = 530;
            this.caretTimer.Tick += this.OnCaretTick;
            this.caretTimer.Start();
        }

        IntPtr IImeClient.WindowHandle
        {
            get { return this.Handle; }
        }

        /// <summary>確定済みテキスト。null は空。CR/LF 以降は捨てる。</summary>
        public override string Text
        {
            get
            {
                return (this.text == null) ? "" : this.text;
            }
            set
            {
                string next = StripLineBreaks(value);
                this.text = next;
                this.caret = next.Length;
                this.selAnchor = this.caret;
                this.hasUndo = false;
                this.EnsureCaretVisible();
                this.Invalidate();
                this.OnTextChanged(EventArgs.Empty);
            }
        }

        /// <summary>未確定文字列があるとき true。START だけでは true にしない。</summary>
        public bool IsComposing
        {
            get { return !string.IsNullOrEmpty(this.imeComposition); }
        }

        /// <summary>セル高さ＋外周 1 物理 px 枠。フォント未設定なら 8+2。</summary>
        public int PreferredOuterHeight
        {
            get
            {
                if (this.halfFont == null || this.fullFont == null)
                {
                    return ImeLayout.FieldOuterHeight(8, 1);
                }

                int cell = this.halfFont.Height;
                if (this.fullFont.Height > cell)
                {
                    cell = this.fullFont.Height;
                }

                return ImeLayout.FieldOuterHeight(cell, 1);
            }
        }

        /// <summary>全文を選択する。</summary>
        public void SelectAll()
        {
            this.selAnchor = 0;
            this.caret = this.TextLength();
            this.caretVisible = true;
            this.Invalidate();
        }

        /// <summary>
        /// 半角・全角フォントを参照する。所有権は移さない。fontSizeDip は IME ToPixels 用。
        /// </summary>
        /// <param name="half">半角。</param>
        /// <param name="full">全角。</param>
        /// <param name="fontSizeDip">本文と同じ 96dpi DIP。</param>
        public void SetFonts(Font half, Font full, int fontSizeDip)
        {
            this.halfFont = half;
            this.fullFont = full;
            this.fontSizeDip = fontSizeDip;
            this.Invalidate();
        }

        void IImeClient.SetCompositionString(string text)
        {
            this.imeComposition = (text == null) ? "" : text;
        }

        void IImeClient.SetCompositionCursor(int rawCursor)
        {
            int length = (this.imeComposition == null) ? 0 : this.imeComposition.Length;
            this.imeCursor = ImeLayout.ClampCursor(rawCursor, length);
        }

        void IImeClient.ClearComposition()
        {
            this.imeComposition = "";
            this.imeCursor = 0;
        }

        void IImeClient.UpdateImeWindow()
        {
            this.UpdateImeWindow();
        }

        void IImeClient.FillQueryCharPosition(IntPtr lParam)
        {
            this.FillImeCharPosition(lParam);
        }

        void IImeClient.RequestInvalidate()
        {
            this.EnsureCaretVisible();
            this.Invalidate();
        }

        /// <summary>左右・Home/End は入力キー。Tab は入れない（フォーカス移動）。</summary>
        protected override bool IsInputKey(Keys keyData)
        {
            Keys code = keyData & Keys.KeyCode;
            if (code == Keys.Left || code == Keys.Right || code == Keys.Home || code == Keys.End
                || code == Keys.Up || code == Keys.Down)
            {
                return true;
            }

            return base.IsInputKey(keyData);
        }

        /// <summary>Ctrl+C/X/V/A は欄が処理。Ctrl+Y は何もしない。変換中は Undo しない。</summary>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.C))
            {
                this.Copy();
                return true;
            }

            if (this.IsComposing)
            {
                if (keyData == (Keys.Control | Keys.X)
                    || keyData == (Keys.Control | Keys.V)
                    || keyData == (Keys.Control | Keys.A)
                    || keyData == (Keys.Control | Keys.Z)
                    || keyData == (Keys.Control | Keys.Y))
                {
                    return false;
                }
            }

            if (keyData == (Keys.Control | Keys.X))
            {
                if (!this.IsComposing)
                {
                    this.Cut();
                }

                return true;
            }

            if (keyData == (Keys.Control | Keys.V))
            {
                if (!this.IsComposing)
                {
                    this.Paste();
                }

                return true;
            }

            if (keyData == (Keys.Control | Keys.A))
            {
                if (!this.IsComposing)
                {
                    this.SelectAll();
                }

                return true;
            }

            if (keyData == (Keys.Control | Keys.Z))
            {
                if (!this.IsComposing)
                {
                    this.UndoSwap();
                }

                return true;
            }

            if (keyData == (Keys.Control | Keys.Y))
            {
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        /// <summary>確定文字の挿入。変換中と制御文字は入れない。改行は捨てる。</summary>
        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            base.OnKeyPress(e);
            if (this.IsComposing)
            {
                return;
            }

            if (e.KeyChar == '\r' || e.KeyChar == '\n' || e.KeyChar == '\t' || e.KeyChar == 8 || e.KeyChar < 32)
            {
                return;
            }

            this.InsertText(e.KeyChar.ToString());
            e.Handled = true;
        }

        /// <summary>Backspace/Delete/矢印。Enter は挿入せず FindBar の KeyDown へ。Up/Down は何もしない。</summary>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (this.IsComposing)
            {
                if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Back || e.KeyCode == Keys.Delete
                    || e.KeyCode == Keys.Left || e.KeyCode == Keys.Right || e.KeyCode == Keys.Up || e.KeyCode == Keys.Down
                    || e.KeyCode == Keys.Home || e.KeyCode == Keys.End)
                {
                    return;
                }
            }

            if (e.KeyCode == Keys.Back)
            {
                e.Handled = true;
                this.Backspace();
                return;
            }

            if (e.KeyCode == Keys.Delete)
            {
                e.Handled = true;
                this.DeleteForward();
                return;
            }

            if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Right || e.KeyCode == Keys.Home || e.KeyCode == Keys.End)
            {
                e.Handled = true;
                this.MoveCaret(e.KeyCode, e.Shift);
                return;
            }

            if (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down)
            {
                e.Handled = true;
            }
        }

        /// <summary>クリックでキャレット。Shift なしなら選択解除。</summary>
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            this.Focus();
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            int col = this.HitTest(e.X);
            this.caret = col;
            if ((Control.ModifierKeys & Keys.Shift) == 0)
            {
                this.selAnchor = this.caret;
            }

            this.selecting = true;
            this.Capture = true;
            this.caretVisible = true;
            this.EnsureCaretVisible();
            this.Invalidate();
        }

        /// <summary>ドラッグ選択。</summary>
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!this.selecting)
            {
                return;
            }

            this.caret = this.HitTest(e.X);
            this.EnsureCaretVisible();
            this.Invalidate();
        }

        /// <summary>ドラッグ終了。</summary>
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            this.selecting = false;
            this.Capture = false;
        }

        /// <summary>Shift+ホイールで横スクロール。</summary>
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if ((Control.ModifierKeys & Keys.Shift) != 0)
            {
                int dpi = DpiUtil.GetDpi(this.IsHandleCreated ? this.Handle : IntPtr.Zero);
                int dx = DpiUtil.WheelToPixels(e.Delta, DpiUtil.ToPixels(32, dpi));
                this.AddScrollX(dx);
                return;
            }

            base.OnMouseWheel(e);
        }

        /// <summary>幅が変わったらキャレットを可視に収める。</summary>
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            this.EnsureCaretVisible();
        }

        /// <summary>フォーカスでキャレットを出す。</summary>
        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            this.caretVisible = true;
            this.Invalidate();
        }

        /// <summary>フォーカス喪失でキャレットを消す。</summary>
        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            this.selecting = false;
            this.caretVisible = false;
            this.Invalidate();
        }

        /// <summary>IME と横ホイール。</summary>
        protected override void WndProc(ref Message m)
        {
            const int WM_MOUSEHWHEEL = 0x020E;
            if (m.Msg == WM_MOUSEHWHEEL)
            {
                long wp = m.WParam.ToInt64();
                short delta = (short)((wp >> 16) & 0xFFFF);
                int dpi = DpiUtil.GetDpi(this.IsHandleCreated ? this.Handle : IntPtr.Zero);
                int dx = DpiUtil.WheelToPixels(delta, DpiUtil.ToPixels(32, dpi));
                this.AddScrollX(dx);
                m.Result = (IntPtr)1;
                return;
            }

            ImeDispatchKind kind = NativeIme.Dispatch(ref m, this);
            if (kind == ImeDispatchKind.Consumed)
            {
                return;
            }

            base.WndProc(ref m);
            if (kind == ImeDispatchKind.ContinueBaseThenUpdate)
            {
                this.UpdateImeWindow();
            }
        }

        /// <summary>外周 1px 枠と DualFont 本文。省略記号は付けない。</summary>
        protected override void OnPaint(PaintEventArgs e)
        {
            if (e == null || e.Graphics == null)
            {
                return;
            }

            Graphics g = e.Graphics;
            g.Clear(this.BackColor);
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            using (Pen border = new Pen(Theme.Border))
            {
                g.DrawRectangle(border, 0, 0, this.Width - 1, this.Height - 1);
            }

            if (this.halfFont == null || this.fullFont == null)
            {
                return;
            }

            int cell = this.CellHeight();
            Rectangle textClip = this.TextClipRect(cell);
            if (textClip.Width <= 0 || textClip.Height <= 0)
            {
                return;
            }

            float originX = this.ContentLeft() - this.scrollX;
            int selA = this.selAnchor;
            int selB = this.caret;
            if (selA > selB)
            {
                int t = selA;
                selA = selB;
                selB = t;
            }

            GraphicsState state = g.Save();
            try
            {
                g.SetClip(textClip);
                if (selA != selB)
                {
                    float x0 = originX + DualFontPainter.Measure(g, this.text.Substring(0, selA), this.halfFont, this.fullFont, null);
                    float x1 = originX + DualFontPainter.Measure(g, this.text.Substring(0, selB), this.halfFont, this.fullFont, null);
                    using (SolidBrush sel = new SolidBrush(Theme.Selection))
                    {
                        g.FillRectangle(sel, x0, textClip.Y, x1 - x0, textClip.Height);
                    }
                }

                using (SolidBrush fg = new SolidBrush(this.ForeColor))
                {
                    DualFontPainter.Draw(g, this.text, this.halfFont, this.fullFont, textClip, originX, fg, null);
                }

                float prefixWidth = 0f;
                float caretOrigin = originX + DualFontPainter.Measure(g, this.text.Substring(0, this.caret), this.halfFont, this.fullFont, null);
                if (!string.IsNullOrEmpty(this.imeComposition))
                {
                    float imeWidth = DualFontPainter.Measure(g, this.imeComposition, this.halfFont, this.fullFont, null);
                    int cursor = ImeLayout.ClampCursor(this.imeCursor, this.imeComposition.Length);
                    if (cursor > 0)
                    {
                        prefixWidth = DualFontPainter.Measure(g, this.imeComposition.Substring(0, cursor), this.halfFont, this.fullFont, null);
                    }

                    using (SolidBrush imeBg = new SolidBrush(Theme.Selection))
                    {
                        g.FillRectangle(imeBg, caretOrigin, textClip.Y, imeWidth, textClip.Height);
                    }

                    using (SolidBrush fg = new SolidBrush(this.ForeColor))
                    {
                        DualFontPainter.Draw(g, this.imeComposition, this.halfFont, this.fullFont, textClip, caretOrigin, fg, null);
                    }

                    using (Pen underline = new Pen(Theme.Foreground))
                    {
                        g.DrawLine(underline, caretOrigin, textClip.Y + textClip.Height - 2, caretOrigin + imeWidth, textClip.Y + textClip.Height - 2);
                    }

                    this.caretVisible = true;
                }

                if (this.Focused && this.caretVisible)
                {
                    int x = ImeLayout.FieldClientX(this.ContentLeft(), this.scrollX, prefixWidth + (caretOrigin - originX));
                    if (x >= textClip.Left && x < textClip.Right)
                    {
                        using (SolidBrush caretBrush = new SolidBrush(Theme.Foreground))
                        {
                            g.FillRectangle(caretBrush, x, textClip.Y + 1, 1, textClip.Height - 2);
                        }
                    }
                }
            }
            finally
            {
                g.Restore(state);
            }
        }

        /// <summary>タイマーを破棄する。フォントは所有しない。</summary>
        /// <param name="disposing">マネージドも捨てるなら true。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && this.caretTimer != null)
            {
                this.caretTimer.Dispose();
                this.caretTimer = null;
            }

            base.Dispose(disposing);
        }

        private void Copy()
        {
            string selected = this.SelectedText();
            if (selected.Length == 0)
            {
                return;
            }

            Clipboard.SetText(selected);
        }

        private void Cut()
        {
            if (!this.HasSelection())
            {
                return;
            }

            this.Copy();
            this.SnapshotUndo();
            this.DeleteSelectionCore();
            this.AfterUserEdit();
        }

        private void Paste()
        {
            if (!Clipboard.ContainsText())
            {
                return;
            }

            this.InsertText(Clipboard.GetText());
        }

        private void UndoSwap()
        {
            if (!this.hasUndo)
            {
                return;
            }

            string t = this.text;
            int c = this.caret;
            int a = this.selAnchor;
            this.text = this.undoText;
            this.caret = this.undoCaret;
            this.selAnchor = this.undoAnchor;
            this.undoText = t;
            this.undoCaret = c;
            this.undoAnchor = a;
            this.EnsureCaretVisible();
            this.Invalidate();
            this.OnTextChanged(EventArgs.Empty);
        }

        private void InsertText(string s)
        {
            s = StripLineBreaks(s);
            if (s.Length == 0 && !this.HasSelection())
            {
                return;
            }

            this.SnapshotUndo();
            this.DeleteSelectionCore();
            this.text = this.text.Insert(this.caret, s);
            this.caret += s.Length;
            this.selAnchor = this.caret;
            this.AfterUserEdit();
        }

        private void Backspace()
        {
            if (this.HasSelection())
            {
                this.SnapshotUndo();
                this.DeleteSelectionCore();
                this.AfterUserEdit();
                return;
            }

            if (this.caret <= 0)
            {
                return;
            }

            this.SnapshotUndo();
            this.text = this.text.Remove(this.caret - 1, 1);
            this.caret--;
            this.selAnchor = this.caret;
            this.AfterUserEdit();
        }

        private void DeleteForward()
        {
            if (this.HasSelection())
            {
                this.SnapshotUndo();
                this.DeleteSelectionCore();
                this.AfterUserEdit();
                return;
            }

            if (this.caret >= this.TextLength())
            {
                return;
            }

            this.SnapshotUndo();
            this.text = this.text.Remove(this.caret, 1);
            this.selAnchor = this.caret;
            this.AfterUserEdit();
        }

        private void DeleteSelectionCore()
        {
            int a = this.selAnchor;
            int b = this.caret;
            if (a > b)
            {
                int t = a;
                a = b;
                b = t;
            }

            if (a == b)
            {
                return;
            }

            this.text = this.text.Remove(a, b - a);
            this.caret = a;
            this.selAnchor = a;
        }

        private void MoveCaret(Keys key, bool shift)
        {
            int next = this.caret;
            if (key == Keys.Left)
            {
                next--;
            }
            else if (key == Keys.Right)
            {
                next++;
            }
            else if (key == Keys.Home)
            {
                next = 0;
            }
            else if (key == Keys.End)
            {
                next = this.TextLength();
            }
            else
            {
                return;
            }

            if (next < 0)
            {
                next = 0;
            }

            int len = this.TextLength();
            if (next > len)
            {
                next = len;
            }

            this.caret = next;
            if (!shift)
            {
                this.selAnchor = this.caret;
            }

            this.caretVisible = true;
            this.EnsureCaretVisible();
            this.Invalidate();
        }

        private void SnapshotUndo()
        {
            this.undoText = this.text;
            this.undoCaret = this.caret;
            this.undoAnchor = this.selAnchor;
            this.hasUndo = true;
        }

        private void AfterUserEdit()
        {
            this.caretVisible = true;
            this.EnsureCaretVisible();
            this.Invalidate();
            this.OnTextChanged(EventArgs.Empty);
        }

        private void EnsureCaretVisible()
        {
            if (this.halfFont == null || this.fullFont == null || !this.IsHandleCreated)
            {
                if (this.scrollX < 0)
                {
                    this.scrollX = 0;
                }

                return;
            }

            float origin = 0f;
            float x = 0f;
            using (Graphics g = this.CreateGraphics())
            {
                origin = DualFontPainter.Measure(g, this.text.Substring(0, this.caret), this.halfFont, this.fullFont, null);
                x = origin;
                if (this.IsComposing)
                {
                    float total = DualFontPainter.Measure(g, this.imeComposition, this.halfFont, this.fullFont, null);
                    int cursor = ImeLayout.ClampCursor(this.imeCursor, this.imeComposition.Length);
                    float prefix = 0f;
                    if (cursor > 0)
                    {
                        prefix = DualFontPainter.Measure(g, this.imeComposition.Substring(0, cursor), this.halfFont, this.fullFont, null);
                    }

                    if (total < prefix)
                    {
                        total = prefix;
                    }

                    x = origin + total;
                }
            }

            int view = this.TextViewportWidth();
            if (origin < this.scrollX)
            {
                this.scrollX = (int)origin;
            }
            else if (x > this.scrollX + view)
            {
                this.scrollX = (int)x - view + 8;
            }

            if (this.scrollX < 0)
            {
                this.scrollX = 0;
            }

            if (this.IsComposing)
            {
                this.UpdateImeWindow();
            }
        }

        private int HitTest(int clickX)
        {
            int len = this.TextLength();
            if (len == 0 || this.halfFont == null || this.fullFont == null || !this.IsHandleCreated)
            {
                return 0;
            }

            float originX = this.ContentLeft() - this.scrollX;
            using (Graphics g = this.CreateGraphics())
            {
                int best = 0;
                float bestDist = Math.Abs((float)clickX - originX);
                int i;
                for (i = 1; i <= len; i++)
                {
                    float w = DualFontPainter.Measure(g, this.text.Substring(0, i), this.halfFont, this.fullFont, null);
                    float dist = Math.Abs((float)clickX - (originX + w));
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        best = i;
                    }
                }

                return best;
            }
        }

        private void AddScrollX(int dx)
        {
            this.scrollX += dx;
            if (this.scrollX < 0)
            {
                this.scrollX = 0;
            }

            this.Invalidate();
            if (this.IsComposing)
            {
                this.UpdateImeWindow();
            }
        }

        private void UpdateImeWindow()
        {
            if (!this.IsHandleCreated)
            {
                return;
            }

            int physicalPx = DpiUtil.ToPixels(this.fontSizeDip, DpiUtil.GetDpi(this.Handle));
            NativeIme.ApplyCompositionFont(this.Handle, physicalPx);
            int contentLeft = this.ContentLeft();
            int cell = this.CellHeight();
            Rectangle textClip = this.TextClipRect(cell);
            float prefixWidth = 0f;
            float totalWidth = 0f;
            float confirmed = 0f;
            if (this.halfFont != null && this.fullFont != null)
            {
                using (Graphics g = this.CreateGraphics())
                {
                    confirmed = DualFontPainter.Measure(g, this.text.Substring(0, this.caret), this.halfFont, this.fullFont, null);
                    if (!string.IsNullOrEmpty(this.imeComposition))
                    {
                        int cursor = ImeLayout.ClampCursor(this.imeCursor, this.imeComposition.Length);
                        if (cursor > 0)
                        {
                            prefixWidth = DualFontPainter.Measure(g, this.imeComposition.Substring(0, cursor), this.halfFont, this.fullFont, null);
                        }

                        totalWidth = DualFontPainter.Measure(g, this.imeComposition, this.halfFont, this.fullFont, null);
                    }
                }
            }

            int x = ImeLayout.FieldClientX(contentLeft, this.scrollX, confirmed + prefixWidth);
            int y = textClip.Y;
            NativeIme.MoveCompositionWindow(this.Handle, x, y);
            int excludeX = ImeLayout.FieldClientX(contentLeft, this.scrollX, confirmed);
            int excludeW = (int)totalWidth;
            if (excludeW < 1)
            {
                excludeW = 1;
            }

            NativeIme.SetCandidateExclude(this.Handle, new Rectangle(excludeX, y, excludeW, cell));
        }

        private void FillImeCharPosition(IntPtr lParam)
        {
            int length = (this.imeComposition == null) ? 0 : this.imeComposition.Length;
            int charPos = ImeLayout.ClampCursor(NativeIme.ReadCharPos(lParam), length);
            int contentLeft = this.ContentLeft();
            int cell = this.CellHeight();
            Rectangle textClip = this.TextClipRect(cell);
            float confirmed = 0f;
            float prefixWidth = 0f;
            if (this.halfFont != null && this.fullFont != null)
            {
                using (Graphics g = this.CreateGraphics())
                {
                    confirmed = DualFontPainter.Measure(g, this.text.Substring(0, this.caret), this.halfFont, this.fullFont, null);
                    if (charPos > 0 && !string.IsNullOrEmpty(this.imeComposition))
                    {
                        prefixWidth = DualFontPainter.Measure(g, this.imeComposition.Substring(0, charPos), this.halfFont, this.fullFont, null);
                    }
                }
            }

            int cx = ImeLayout.FieldClientX(contentLeft, this.scrollX, confirmed + prefixWidth);
            int cy = textClip.Y;
            Point screen = this.PointToScreen(new Point(cx, cy));
            Point docTl = this.PointToScreen(new Point(0, 0));
            Point docBr = this.PointToScreen(new Point(this.ClientSize.Width, this.ClientSize.Height));
            NativeIme.WriteCharPosition(lParam, charPos, screen, cell, docTl.X, docTl.Y, docBr.X, docBr.Y);
        }

        private void OnCaretTick(object sender, EventArgs e)
        {
            if (!this.Focused)
            {
                return;
            }

            if (this.IsComposing)
            {
                this.caretVisible = true;
                this.Invalidate();
                return;
            }

            this.caretVisible = !this.caretVisible;
            this.Invalidate();
        }

        private int ContentLeft()
        {
            int dpi = DpiUtil.GetDpi(this.IsHandleCreated ? this.Handle : IntPtr.Zero);
            return 1 + DpiUtil.ToPixels(DpiUtil.TextInsetDip, dpi);
        }

        private int CellHeight()
        {
            int cell = 0;
            if (this.halfFont != null)
            {
                cell = this.halfFont.Height;
            }

            if (this.fullFont != null && this.fullFont.Height > cell)
            {
                cell = this.fullFont.Height;
            }

            if (cell < 1)
            {
                cell = 8;
            }

            return cell;
        }

        private Rectangle TextClipRect(int cell)
        {
            int innerTop = 1;
            int innerLeft = 1;
            int innerWidth = this.ClientSize.Width - 2;
            int innerHeight = this.ClientSize.Height - 2;
            if (innerWidth < 0)
            {
                innerWidth = 0;
            }

            if (innerHeight < 1)
            {
                innerHeight = 1;
            }

            int top = innerTop + (innerHeight - cell) / 2;
            if (top < innerTop)
            {
                top = innerTop;
            }

            int h = cell;
            if (h > innerHeight)
            {
                h = innerHeight;
            }

            return new Rectangle(innerLeft, top, innerWidth, h);
        }

        private int TextViewportWidth()
        {
            int dpi = DpiUtil.GetDpi(this.IsHandleCreated ? this.Handle : IntPtr.Zero);
            int right = 1 + DpiUtil.ToPixels(DpiUtil.TextInsetDip, dpi);
            int view = this.ClientSize.Width - this.ContentLeft() - right;
            if (view < 1)
            {
                return 1;
            }

            return view;
        }

        private int TextLength()
        {
            return (this.text == null) ? 0 : this.text.Length;
        }

        private bool HasSelection()
        {
            return this.caret != this.selAnchor;
        }

        private string SelectedText()
        {
            int a = this.selAnchor;
            int b = this.caret;
            if (a > b)
            {
                int t = a;
                a = b;
                b = t;
            }

            if (a == b || this.text == null)
            {
                return "";
            }

            return this.text.Substring(a, b - a);
        }

        private static string StripLineBreaks(string value)
        {
            if (value == null)
            {
                return "";
            }

            int n = value.Length;
            int i = 0;
            while (i < n)
            {
                char c = value[i];
                if (c == '\r' || c == '\n')
                {
                    n = i;
                    break;
                }

                i++;
            }

            if (n == value.Length)
            {
                return value;
            }

            return value.Substring(0, n);
        }
    }
}
