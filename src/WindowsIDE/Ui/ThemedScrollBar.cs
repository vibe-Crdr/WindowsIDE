using System;
using System.Drawing;
using System.Windows.Forms;

namespace WindowsIDE.Ui
{
    /// <summary>
    /// 矢印無しの自前スクロールバー。ScrollBar は継承しない。
    /// </summary>
    public sealed class ThemedScrollBar : Control
    {
        private int minimum;
        private int maximum;
        private int largeChange;
        private int smallChange;
        private int value;
        private Color trackColor;
        private readonly bool vertical;
        private bool hover;
        private bool pressed;
        private bool dragging;
        private int dragOffset;
        private int wheelLeftover;

        /// <summary>
        /// 向きを決めて作る。
        /// </summary>
        /// <param name="vertical">縦向きなら true。</param>
        public ThemedScrollBar(bool vertical)
        {
            this.vertical = vertical;
            this.maximum = 100;
            this.largeChange = 10;
            this.smallChange = 1;
            this.trackColor = Theme.Background;
            this.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            this.TabStop = false;
            this.Cursor = Cursors.Default;
            this.BackColor = Theme.Background;
            int bar = DpiUtil.ToPixels(DpiUtil.ScrollBarThicknessDip, DpiUtil.GetDpi(IntPtr.Zero));
            if (bar < 1)
            {
                bar = DpiUtil.ScrollBarThicknessDip;
            }

            if (this.vertical)
            {
                this.Width = bar;
            }
            else
            {
                this.Height = bar;
            }
        }

        /// <summary>縦向きなら true。</summary>
        public bool Vertical
        {
            get { return this.vertical; }
        }

        /// <summary>最小値。</summary>
        public int Minimum
        {
            get { return this.minimum; }
            set
            {
                this.minimum = value;
                this.ClampValue();
                this.Invalidate();
            }
        }

        /// <summary>最大値（inclusive）。</summary>
        public int Maximum
        {
            get { return this.maximum; }
            set
            {
                this.maximum = value;
                this.ClampValue();
                this.Invalidate();
            }
        }

        /// <summary>ページ量。トラッククリックで動く量。</summary>
        public int LargeChange
        {
            get { return this.largeChange; }
            set
            {
                this.largeChange = (value < 1) ? 1 : value;
                this.ClampValue();
                this.Invalidate();
            }
        }

        /// <summary>矢印相当・ホイール 1 ノッチの量。</summary>
        public int SmallChange
        {
            get { return this.smallChange; }
            set
            {
                this.smallChange = (value < 1) ? 1 : value;
            }
        }

        /// <summary>現在位置。有効最大は Maximum - LargeChange + 1。</summary>
        public int Value
        {
            get { return this.value; }
            set { this.SetValue(value); }
        }

        /// <summary>トラック色。未設定時は Theme.Background。</summary>
        public Color TrackColor
        {
            get { return this.trackColor; }
            set
            {
                this.trackColor = value;
                this.BackColor = value;
                this.Invalidate();
            }
        }

        /// <summary>Value が変わったとき。</summary>
        public event EventHandler ValueChanged;

        /// <summary>描画。矢印は無い。</summary>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            using (SolidBrush track = new SolidBrush(this.trackColor.IsEmpty ? Theme.Background : this.trackColor))
            {
                g.FillRectangle(track, this.ClientRectangle);
            }

            Rectangle thumb = this.GetThumbRect();
            if (thumb.Width <= 0 || thumb.Height <= 0)
            {
                return;
            }

            Color fill = Theme.LineNumber;
            if (this.pressed)
            {
                fill = Theme.Selection;
            }
            else if (this.hover)
            {
                fill = Theme.Comment;
            }

            using (SolidBrush b = new SolidBrush(fill))
            {
                g.FillRectangle(b, thumb);
            }
        }

        /// <summary>つまみドラッグまたはトラックの LargeChange。</summary>
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            Rectangle thumb = this.GetThumbRect();
            if (thumb.Contains(e.Location))
            {
                this.dragging = true;
                this.pressed = true;
                this.dragOffset = this.vertical ? (e.Y - thumb.Y) : (e.X - thumb.X);
                this.Capture = true;
                this.Invalidate();
                return;
            }

            int coord = this.vertical ? e.Y : e.X;
            int thumbStart = this.vertical ? thumb.Y : thumb.X;
            if (coord < thumbStart)
            {
                this.SetValue(this.value - this.largeChange);
            }
            else
            {
                this.SetValue(this.value + this.largeChange);
            }
        }

        /// <summary>ドラッグ中はつまみ位置から Value を決める。</summary>
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (this.dragging)
            {
                this.UpdateDrag(this.vertical ? e.Y : e.X);
                return;
            }

            bool nowHover = this.GetThumbRect().Contains(e.Location);
            if (nowHover != this.hover)
            {
                this.hover = nowHover;
                this.Invalidate();
            }
        }

        /// <summary>ドラッグ終了。</summary>
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            this.dragging = false;
            this.pressed = false;
            this.Capture = false;
            this.Invalidate();
        }

        /// <summary>ホバー解除。</summary>
        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (!this.dragging && this.hover)
            {
                this.hover = false;
                this.Invalidate();
            }
        }

        /// <summary>ホイールで SmallChange。正の delta は Value を減らす。高精度 delta は 120 単位で 1 ノッチ。</summary>
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            int notches = DpiUtil.WheelNotches(e.Delta, ref this.wheelLeftover);
            if (notches != 0)
            {
                this.SetValue(this.value - (notches * this.smallChange));
            }

            HandledMouseEventArgs he = e as HandledMouseEventArgs;
            if (he != null)
            {
                he.Handled = true;
            }
        }

        private void UpdateDrag(int mouseCoord)
        {
            int trackLen = this.vertical ? this.Height : this.Width;
            int thumbLen = this.GetThumbLength(trackLen);
            int travel = trackLen - thumbLen;
            if (travel <= 0)
            {
                return;
            }

            int pos = mouseCoord - this.dragOffset;
            if (pos < 0)
            {
                pos = 0;
            }

            if (pos > travel)
            {
                pos = travel;
            }

            int range = this.GetMaxValue() - this.minimum;
            int next = this.minimum;
            if (range > 0)
            {
                next = this.minimum + (int)((long)pos * range / travel);
            }

            this.SetValue(next);
        }

        private Rectangle GetThumbRect()
        {
            int trackLen = this.vertical ? this.Height : this.Width;
            int thickness = this.vertical ? this.Width : this.Height;
            if (trackLen <= 0 || thickness <= 0)
            {
                return Rectangle.Empty;
            }

            int thumbLen = this.GetThumbLength(trackLen);
            int travel = trackLen - thumbLen;
            int range = this.GetMaxValue() - this.minimum;
            int pos = 0;
            if (range > 0 && travel > 0)
            {
                pos = (int)((long)(this.value - this.minimum) * travel / range);
            }

            if (this.vertical)
            {
                return new Rectangle(0, pos, thickness, thumbLen);
            }

            return new Rectangle(pos, 0, thumbLen, thickness);
        }

        private int GetThumbLength(int trackLen)
        {
            int dpi = DpiUtil.GetDpi(this.IsHandleCreated ? this.Handle : IntPtr.Zero);
            int minThumb = DpiUtil.ToPixels(DpiUtil.MinScrollThumbDip, dpi);
            return DpiUtil.ScrollThumbLength(trackLen, this.minimum, this.maximum, this.largeChange, minThumb);
        }

        private int GetMaxValue()
        {
            if (this.largeChange > this.maximum)
            {
                return this.minimum;
            }

            int maxValue = this.maximum - this.largeChange + 1;
            if (maxValue < this.minimum)
            {
                return this.minimum;
            }

            return maxValue;
        }

        private void ClampValue()
        {
            this.SetValue(this.value);
        }

        private void SetValue(int next)
        {
            int maxValue = this.GetMaxValue();
            if (next < this.minimum)
            {
                next = this.minimum;
            }

            if (next > maxValue)
            {
                next = maxValue;
            }

            if (next == this.value)
            {
                return;
            }

            this.value = next;
            this.Invalidate();
            EventHandler h = this.ValueChanged;
            if (h != null)
            {
                h(this, EventArgs.Empty);
            }
        }
    }
}
