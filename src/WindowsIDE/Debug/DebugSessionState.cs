namespace WindowsIDE.Debug
{
    /// <summary>
    /// デバッグセッションの状態。言語に依存しない。
    /// </summary>
    public enum DebugSessionState
    {
        /// <summary>セッションなし。</summary>
        Idle = 0,

        /// <summary>実行中（停止していない）。</summary>
        Running = 1,

        /// <summary>ブレークで停止中。</summary>
        Stopped = 2
    }
}
