using System.Drawing;

namespace WindowsIDE.Ui
{
    /// <summary>
    /// ダークテーマの色。docs/ui.md の初期パレット。
    /// </summary>
    public static class Theme
    {
        public static readonly Color Background = Color.FromArgb(0x1a, 0x1b, 0x26);
        public static readonly Color EditorBackground = Color.FromArgb(0x16, 0x16, 0x1e);
        public static readonly Color Foreground = Color.FromArgb(0xc0, 0xca, 0xf5);
        public static readonly Color Comment = Color.FromArgb(0x56, 0x5f, 0x89);
        public static readonly Color LineNumber = Color.FromArgb(0x3b, 0x42, 0x61);
        public static readonly Color CurrentLine = Color.FromArgb(0x29, 0x2e, 0x42);
        public static readonly Color Border = Color.FromArgb(0x1f, 0x23, 0x35);
        public static readonly Color Selection = Color.FromArgb(0x3d, 0x59, 0xa1);
        public static readonly Color Error = Color.FromArgb(0xf7, 0x76, 0x8e);
        public static readonly Color Keyword = Color.FromArgb(0xbb, 0x9a, 0xf7);
        public static readonly Color StringLiteral = Color.FromArgb(0x9e, 0xce, 0x6a);
        public static readonly Color Number = Color.FromArgb(0xff, 0x9e, 0x64);
        public static readonly Color StatusBar = Color.FromArgb(0x16, 0x16, 0x1e);
    }
}
