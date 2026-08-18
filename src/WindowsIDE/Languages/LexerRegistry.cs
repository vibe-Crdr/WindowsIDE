namespace WindowsIDE.Languages
{
    /// <summary>
    /// 言語ごとの行レキサを返す。インスタンスは使い回す。
    /// </summary>
    public static class LexerRegistry
    {
        private static readonly ILineLexer Plain = new PlainLexer();
        private static readonly ILineLexer CSharp = new CSharpLexer();
        private static readonly ILineLexer Vba = new VbaLexer();
        private static readonly ILineLexer PowerShell = new PowerShellLexer();
        private static readonly ILineLexer Cmd = new CmdLexer();

        /// <summary>
        /// 言語に対応するレキサを返す。未知は Plain。
        /// </summary>
        /// <param name="kind">言語。</param>
        /// <returns>行レキサ。</returns>
        public static ILineLexer Get(LanguageKind kind)
        {
            switch (kind)
            {
                case LanguageKind.CSharp:
                    return CSharp;
                case LanguageKind.Vba:
                    return Vba;
                case LanguageKind.PowerShell:
                    return PowerShell;
                case LanguageKind.Cmd:
                    return Cmd;
                default:
                    return Plain;
            }
        }
    }
}
