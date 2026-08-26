namespace WindowsIDE.Terminal
{
    /// <summary>
    /// 統合ターミナルのシェル種別。既定は PowerShell 5.1。
    /// </summary>
    public enum ShellKind
    {
        /// <summary>System32 の powershell.exe 5.1。</summary>
        PowerShell51 = 0,

        /// <summary>System32 の cmd.exe。</summary>
        Cmd = 1
    }
}
