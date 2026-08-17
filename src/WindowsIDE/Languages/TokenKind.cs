namespace WindowsIDE.Languages
{
    /// <summary>
    /// 字句の種類。色は TextView が Theme に対応付ける。
    /// </summary>
    public enum TokenKind
    {
        Text,
        Keyword,
        String,
        Comment,
        Number
    }
}
