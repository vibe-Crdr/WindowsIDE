using System;

namespace WindowsIDE.Languages
{
    /// <summary>
    /// PowerShell の言語キーワード。コマンドレットは含めない。
    /// </summary>
    public static class PowerShellKeywords
    {
        /// <summary>照合に使う語。OrdinalIgnoreCase。</summary>
        public static readonly string[] Words = new string[]
        {
            "begin",
            "break",
            "catch",
            "class",
            "configuration",
            "continue",
            "data",
            "define",
            "do",
            "dynamicparam",
            "else",
            "elseif",
            "end",
            "enum",
            "exit",
            "filter",
            "finally",
            "for",
            "foreach",
            "from",
            "function",
            "hidden",
            "if",
            "in",
            "inlinescript",
            "parallel",
            "param",
            "process",
            "return",
            "sequence",
            "static",
            "switch",
            "throw",
            "trap",
            "try",
            "until",
            "using",
            "var",
            "while",
            "workflow"
        };

        /// <summary>OrdinalIgnoreCase のキーワード集合。</summary>
        public static readonly KeywordSet Set = new KeywordSet(Words, StringComparer.OrdinalIgnoreCase);
    }
}
