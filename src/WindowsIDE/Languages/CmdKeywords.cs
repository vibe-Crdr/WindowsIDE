using System;

namespace WindowsIDE.Languages
{
    /// <summary>
    /// cmd の内部コマンド中心のキーワード。Rem はスキャナが行コメントにする。
    /// </summary>
    public static class CmdKeywords
    {
        /// <summary>照合に使う語。OrdinalIgnoreCase。</summary>
        public static readonly string[] Words = new string[]
        {
            "assoc",
            "break",
            "call",
            "cd",
            "chdir",
            "cls",
            "color",
            "copy",
            "date",
            "defined",
            "del",
            "dir",
            "echo",
            "else",
            "endlocal",
            "erase",
            "errorlevel",
            "exist",
            "exit",
            "for",
            "ftype",
            "goto",
            "if",
            "md",
            "mkdir",
            "mklink",
            "move",
            "not",
            "path",
            "pause",
            "popd",
            "prompt",
            "pushd",
            "rd",
            "rem",
            "ren",
            "rename",
            "rmdir",
            "set",
            "setlocal",
            "shift",
            "start",
            "time",
            "title",
            "type",
            "ver",
            "verify",
            "vol"
        };

        /// <summary>OrdinalIgnoreCase のキーワード集合。</summary>
        public static readonly KeywordSet Set = new KeywordSet(Words, StringComparer.OrdinalIgnoreCase);
    }
}
