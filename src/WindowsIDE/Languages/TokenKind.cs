namespace WindowsIDE.Languages
{
    /// <summary>
    /// 字句の種類。色は TextView が Theme に対応付ける。
    /// Local / Instance / Method / Type を含み、Function は持たない。
    /// </summary>
    public enum TokenKind
    {
        Text,
        Keyword,
        String,
        Comment,
        Number,
        Local,
        Instance,
        Method,
        Type
    }
}
