using System.Collections.Generic;

namespace WindowsIDE.Terminal
{
    /// <summary>
    /// バイト／文字列を VtScreen へ。未知 CSI は最終バイトまで読んで捨て、ESC は印字しない。
    /// </summary>
    public sealed class VtParser
    {
        private const int Ground = 0;
        private const int Escape = 1;
        private const int Csi = 2;
        private const int Osc = 3;
        private const int OscEsc = 4;

        private readonly VtScreen screen;
        private int state;
        private bool csiPrivate;
        private readonly List<int> csiParams;
        private int csiCurrent;
        private bool csiHasDigit;

        /// <summary>
        /// 画面へ書き込むパーサを作る。
        /// </summary>
        /// <param name="screen">対象。null 不可。</param>
        public VtParser(VtScreen screen)
        {
            this.screen = screen;
            this.csiParams = new List<int>();
            this.Reset();
        }

        /// <summary>
        /// 状態を Ground に戻す。画面は消さない。
        /// </summary>
        public void Reset()
        {
            this.state = Ground;
            this.csiPrivate = false;
            this.csiParams.Clear();
            this.csiCurrent = 0;
            this.csiHasDigit = false;
        }

        /// <summary>
        /// UTF-16 文字列を食わせる。null は無視。
        /// </summary>
        /// <param name="text">VT を含むテキスト。</param>
        public void Feed(string text)
        {
            if (text == null || text.Length == 0)
            {
                return;
            }

            int i = 0;
            while (i < text.Length)
            {
                char ch = text[i];
                this.Consume(ch);
                i++;
            }
        }

        private void Consume(char ch)
        {
            int code = ch;
            if (this.state == Osc)
            {
                if (code == 0x07)
                {
                    this.state = Ground;
                    return;
                }

                if (code == 0x1B)
                {
                    this.state = OscEsc;
                    return;
                }

                return;
            }

            if (this.state == OscEsc)
            {
                if (ch == '\\')
                {
                    this.state = Ground;
                    return;
                }

                this.state = Osc;
                if (code == 0x07)
                {
                    this.state = Ground;
                }

                return;
            }

            if (this.state == Escape)
            {
                this.OnEscape(ch);
                return;
            }

            if (this.state == Csi)
            {
                this.OnCsi(ch);
                return;
            }

            if (code == 0x1B)
            {
                this.state = Escape;
                return;
            }

            if (code == 0x00 || code == 0x07)
            {
                return;
            }

            if (code == 0x08)
            {
                this.screen.Backspace();
                return;
            }

            if (code == 0x09)
            {
                this.screen.Tab();
                return;
            }

            if (code == 0x0A)
            {
                this.screen.LineFeed();
                return;
            }

            if (code == 0x0D)
            {
                this.screen.CarriageReturn();
                return;
            }

            if (code < 32 || code == 0x7F)
            {
                return;
            }

            this.screen.PutChar(ch);
        }

        private void OnEscape(char ch)
        {
            if (ch == '[')
            {
                this.state = Csi;
                this.csiPrivate = false;
                this.csiParams.Clear();
                this.csiCurrent = 0;
                this.csiHasDigit = false;
                return;
            }

            if (ch == ']')
            {
                this.state = Osc;
                return;
            }

            if (ch == '7')
            {
                this.screen.SaveCaret();
                this.state = Ground;
                return;
            }

            if (ch == '8')
            {
                this.screen.RestoreCaret();
                this.state = Ground;
                return;
            }

            this.state = Ground;
        }

        private void OnCsi(char ch)
        {
            if (ch == '?' && this.csiParams.Count == 0 && !this.csiHasDigit)
            {
                this.csiPrivate = true;
                return;
            }

            if (ch >= '0' && ch <= '9')
            {
                this.csiHasDigit = true;
                this.csiCurrent = (this.csiCurrent * 10) + (ch - '0');
                if (this.csiCurrent > 9999)
                {
                    this.csiCurrent = 9999;
                }

                return;
            }

            if (ch == ';')
            {
                this.csiParams.Add(this.csiHasDigit ? this.csiCurrent : 0);
                this.csiCurrent = 0;
                this.csiHasDigit = false;
                return;
            }

            if (ch >= 0x20 && ch < 0x40)
            {
                return;
            }

            this.csiParams.Add(this.csiHasDigit ? this.csiCurrent : 0);
            this.csiHasDigit = false;
            this.csiCurrent = 0;
            this.state = Ground;
            if (ch < 0x40 || ch > 0x7E)
            {
                return;
            }

            this.DispatchCsi(ch);
        }

        private void DispatchCsi(char finalByte)
        {
            if (this.csiPrivate)
            {
                if (finalByte == 'h' || finalByte == 'l')
                {
                    int p = this.Param(0, 0);
                    if (p == 25)
                    {
                        this.screen.CaretVisible = (finalByte == 'h');
                    }
                }

                return;
            }

            if (finalByte == 'A')
            {
                this.screen.MoveUp(this.Param(0, 1));
                return;
            }

            if (finalByte == 'B')
            {
                this.screen.MoveDown(this.Param(0, 1));
                return;
            }

            if (finalByte == 'C')
            {
                this.screen.MoveForward(this.Param(0, 1));
                return;
            }

            if (finalByte == 'D')
            {
                this.screen.MoveBack(this.Param(0, 1));
                return;
            }

            if (finalByte == 'H' || finalByte == 'f')
            {
                this.screen.SetCursor(this.Param(0, 1), this.Param(1, 1));
                return;
            }

            if (finalByte == 'G')
            {
                this.screen.SetColumn(this.Param(0, 1));
                return;
            }

            if (finalByte == 'K')
            {
                this.screen.EraseLine(this.Param(0, 0));
                return;
            }

            if (finalByte == 'J')
            {
                this.screen.EraseDisplay(this.Param(0, 0));
                return;
            }

            if (finalByte == 'm')
            {
                this.ApplySgr();
            }
        }

        private int Param(int index, int defaultValue)
        {
            if (index < 0 || index >= this.csiParams.Count)
            {
                return defaultValue;
            }

            int v = this.csiParams[index];
            if (v == 0)
            {
                return defaultValue;
            }

            return v;
        }

        private void ApplySgr()
        {
            if (this.csiParams.Count == 0)
            {
                this.screen.CurrentSlot = ColorSlot.Default;
                return;
            }

            int i = 0;
            while (i < this.csiParams.Count)
            {
                int n = this.csiParams[i];
                if (n == 0)
                {
                    this.screen.CurrentSlot = ColorSlot.Default;
                    i++;
                    continue;
                }

                if (n == 1)
                {
                    i++;
                    continue;
                }

                if (n == 30 || n == 90)
                {
                    this.screen.CurrentSlot = ColorSlot.Comment;
                    i++;
                    continue;
                }

                if (n == 31 || n == 91)
                {
                    this.screen.CurrentSlot = ColorSlot.Error;
                    i++;
                    continue;
                }

                if (n == 32 || n == 92)
                {
                    this.screen.CurrentSlot = ColorSlot.String;
                    i++;
                    continue;
                }

                if (n == 33 || n == 93)
                {
                    this.screen.CurrentSlot = ColorSlot.Number;
                    i++;
                    continue;
                }

                if (n == 34 || n == 94)
                {
                    this.screen.CurrentSlot = ColorSlot.Local;
                    i++;
                    continue;
                }

                if (n == 35 || n == 95)
                {
                    this.screen.CurrentSlot = ColorSlot.Keyword;
                    i++;
                    continue;
                }

                if (n == 36 || n == 96)
                {
                    this.screen.CurrentSlot = ColorSlot.Type;
                    i++;
                    continue;
                }

                if (n == 37 || n == 97 || n == 39)
                {
                    this.screen.CurrentSlot = ColorSlot.Foreground;
                    i++;
                    continue;
                }

                if (n == 38 || n == 48)
                {
                    i++;
                    if (i >= this.csiParams.Count)
                    {
                        break;
                    }

                    int mode = this.csiParams[i];
                    i++;
                    if (mode == 5)
                    {
                        i++;
                    }
                    else if (mode == 2)
                    {
                        i += 3;
                    }

                    continue;
                }

                if ((n >= 40 && n <= 47) || n == 49 || (n >= 100 && n <= 107))
                {
                    i++;
                    continue;
                }

                i++;
            }
        }
    }
}
