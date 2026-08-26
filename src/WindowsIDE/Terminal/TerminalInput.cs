namespace WindowsIDE.Terminal
{
    /// <summary>
    /// キー分類の結果種別。WinForms 型は使わない。
    /// </summary>
    public enum TerminalInputKind
    {
        /// <summary>何もしない。</summary>
        None = 0,

        /// <summary>パネルの表示／フォーカス切替。</summary>
        Toggle = 1,

        /// <summary>選択をコピー。</summary>
        Copy = 2,

        /// <summary>クリップボードを PTY へ。</summary>
        Paste = 3,

        /// <summary>PTY へバイト列を送る。</summary>
        Send = 4
    }

    /// <summary>
    /// キー分類の結果。
    /// </summary>
    public sealed class TerminalInputResult
    {
        private TerminalInputKind kind;
        private byte[] payload;

        /// <summary>
        /// 種別と任意の送信用バイトを持つ。
        /// </summary>
        /// <param name="kind">動作。</param>
        /// <param name="payload">Send のとき。他は null 可。</param>
        public TerminalInputResult(TerminalInputKind kind, byte[] payload)
        {
            this.kind = kind;
            this.payload = payload;
        }

        /// <summary>動作。</summary>
        public TerminalInputKind Kind
        {
            get { return this.kind; }
        }

        /// <summary>Send のバイト。無ければ null。</summary>
        public byte[] Payload
        {
            get { return this.payload; }
        }
    }

    /// <summary>
    /// ターミナルのキー分類。Keys の数値を int で受ける。WinForms は参照しない。
    /// </summary>
    public static class TerminalInput
    {
        /// <summary>Keys.Oemtilde。</summary>
        public const int KeyOemtilde = 0xC0;

        /// <summary>Keys.Control。</summary>
        public const int ModifierControl = 0x20000;

        /// <summary>Keys.Shift。</summary>
        public const int ModifierShift = 0x10000;

        /// <summary>Keys.C。</summary>
        public const int KeyC = 67;

        /// <summary>Keys.V。</summary>
        public const int KeyV = 86;

        /// <summary>Keys.A。</summary>
        public const int KeyA = 65;

        /// <summary>Keys.Z。</summary>
        public const int KeyZ = 90;

        /// <summary>Keys.X。</summary>
        public const int KeyX = 88;

        /// <summary>Keys.Y。</summary>
        public const int KeyY = 89;

        /// <summary>Keys.Enter / Return。</summary>
        public const int KeyEnter = 13;

        /// <summary>Keys.Tab。</summary>
        public const int KeyTab = 9;

        /// <summary>Keys.Back。</summary>
        public const int KeyBack = 8;

        /// <summary>Keys.Escape。</summary>
        public const int KeyEscape = 27;

        /// <summary>Keys.Left。</summary>
        public const int KeyLeft = 37;

        /// <summary>Keys.Up。</summary>
        public const int KeyUp = 38;

        /// <summary>Keys.Right。</summary>
        public const int KeyRight = 39;

        /// <summary>Keys.Down。</summary>
        public const int KeyDown = 40;

        private static readonly TerminalInputResult NoneResult = new TerminalInputResult(TerminalInputKind.None, null);
        private static readonly TerminalInputResult ToggleResult = new TerminalInputResult(TerminalInputKind.Toggle, null);
        private static readonly TerminalInputResult CopyResult = new TerminalInputResult(TerminalInputKind.Copy, null);
        private static readonly TerminalInputResult PasteResult = new TerminalInputResult(TerminalInputKind.Paste, null);

        /// <summary>
        /// キーを分類する。変換中は Toggle しない。
        /// </summary>
        /// <param name="keyData">Keys の数値（修飾込み）。</param>
        /// <param name="hasSelection">選択があれば true。</param>
        /// <param name="isComposing">IME 変換中なら true。</param>
        /// <returns>動作。</returns>
        public static TerminalInputResult Classify(int keyData, bool hasSelection, bool isComposing)
        {
            int code = keyData & 0xFFFF;
            int mods = keyData & ~0xFFFF;
            bool control = (mods & ModifierControl) != 0;
            bool shift = (mods & ModifierShift) != 0;
            bool onlyControl = control && !shift && (mods & ~ModifierControl) == 0;
            bool noMod = mods == 0;

            if (onlyControl && code == KeyOemtilde)
            {
                if (isComposing)
                {
                    return NoneResult;
                }

                return ToggleResult;
            }

            if (isComposing)
            {
                return NoneResult;
            }

            if (onlyControl && code == KeyC)
            {
                if (hasSelection)
                {
                    return CopyResult;
                }

                return new TerminalInputResult(TerminalInputKind.Send, new byte[] { 0x03 });
            }

            if (onlyControl && code == KeyV)
            {
                return PasteResult;
            }

            if (onlyControl && code == KeyA)
            {
                return new TerminalInputResult(TerminalInputKind.Send, new byte[] { 0x01 });
            }

            if (onlyControl && code == KeyZ)
            {
                return new TerminalInputResult(TerminalInputKind.Send, new byte[] { 0x1A });
            }

            if (onlyControl && code == KeyX)
            {
                return new TerminalInputResult(TerminalInputKind.Send, new byte[] { 0x18 });
            }

            if (onlyControl && code == KeyY)
            {
                return new TerminalInputResult(TerminalInputKind.Send, new byte[] { 0x19 });
            }

            if (noMod && code == KeyEnter)
            {
                return new TerminalInputResult(TerminalInputKind.Send, new byte[] { 0x0D });
            }

            if (noMod && code == KeyTab)
            {
                return new TerminalInputResult(TerminalInputKind.Send, new byte[] { 0x09 });
            }

            if (noMod && code == KeyBack)
            {
                return new TerminalInputResult(TerminalInputKind.Send, new byte[] { 0x08 });
            }

            if (noMod && code == KeyEscape)
            {
                return new TerminalInputResult(TerminalInputKind.Send, new byte[] { 0x1B });
            }

            if (noMod && code == KeyUp)
            {
                return new TerminalInputResult(TerminalInputKind.Send, new byte[] { 0x1B, 0x5B, 0x41 });
            }

            if (noMod && code == KeyDown)
            {
                return new TerminalInputResult(TerminalInputKind.Send, new byte[] { 0x1B, 0x5B, 0x42 });
            }

            if (noMod && code == KeyRight)
            {
                return new TerminalInputResult(TerminalInputKind.Send, new byte[] { 0x1B, 0x5B, 0x43 });
            }

            if (noMod && code == KeyLeft)
            {
                return new TerminalInputResult(TerminalInputKind.Send, new byte[] { 0x1B, 0x5B, 0x44 });
            }

            return NoneResult;
        }
    }
}
