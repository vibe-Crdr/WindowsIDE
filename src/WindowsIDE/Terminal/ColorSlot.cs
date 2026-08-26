namespace WindowsIDE.Terminal
{
    /// <summary>
    /// VT の前景色スロット。Theme には依存しない。UI 側で既存色へ写す。
    /// </summary>
    public enum ColorSlot
    {
        /// <summary>SGR 0 / 既定。</summary>
        Default = 0,

        /// <summary>SGR 37 / 97。</summary>
        Foreground = 1,

        /// <summary>SGR 31 / 91。</summary>
        Error = 2,

        /// <summary>SGR 32 / 92。</summary>
        String = 3,

        /// <summary>SGR 33 / 93。</summary>
        Number = 4,

        /// <summary>SGR 34 / 94。</summary>
        Local = 5,

        /// <summary>SGR 35 / 95。</summary>
        Keyword = 6,

        /// <summary>SGR 36 / 96。</summary>
        Type = 7,

        /// <summary>SGR 30 / 90。</summary>
        Comment = 8
    }
}
