using System;
using System.Collections.Generic;

namespace WindowsIDE.Languages
{
    /// <summary>
    /// VBA の言語キーワード。Rem は表に置くが、スキャナが行コメントにする。
    /// </summary>
    public static class VbaKeywords
    {
        /// <summary>照合に使う語。OrdinalIgnoreCase。</summary>
        public static readonly string[] Words = new string[]
        {
            "AddressOf",
            "Alias",
            "And",
            "As",
            "Boolean",
            "ByRef",
            "Byte",
            "ByVal",
            "Call",
            "Case",
            "Const",
            "Currency",
            "Date",
            "Declare",
            "Dim",
            "Do",
            "Double",
            "Each",
            "Else",
            "ElseIf",
            "End",
            "Enum",
            "Eqv",
            "Event",
            "Exit",
            "False",
            "For",
            "Friend",
            "Function",
            "Get",
            "Global",
            "GoSub",
            "GoTo",
            "If",
            "Imp",
            "Implements",
            "In",
            "Integer",
            "Is",
            "Let",
            "Lib",
            "Like",
            "Long",
            "LongLong",
            "LongPtr",
            "Loop",
            "LSet",
            "Me",
            "Mod",
            "New",
            "Next",
            "Not",
            "Nothing",
            "Object",
            "On",
            "Option",
            "Optional",
            "Or",
            "ParamArray",
            "Preserve",
            "Private",
            "Property",
            "PtrSafe",
            "Public",
            "RaiseEvent",
            "ReDim",
            "Rem",
            "Resume",
            "RSet",
            "Select",
            "Set",
            "Single",
            "Static",
            "Step",
            "Stop",
            "String",
            "Sub",
            "Then",
            "To",
            "True",
            "Type",
            "TypeOf",
            "Until",
            "Variant",
            "Wend",
            "While",
            "With",
            "WithEvents",
            "Xor"
        };

        /// <summary>OrdinalIgnoreCase のキーワード集合。</summary>
        public static readonly KeywordSet Set = new KeywordSet(Words, StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, string> CanonicalMap = BuildCanonicalMap();

        /// <summary>
        /// Words の綴りを OrdinalIgnoreCase で返す。
        /// </summary>
        /// <param name="word">照合する語。</param>
        /// <param name="canonical">表の綴り。</param>
        /// <returns>表にあるとき true。</returns>
        public static bool TryCanonical(string word, out string canonical)
        {
            canonical = null;
            if (word == null)
            {
                return false;
            }

            return CanonicalMap.TryGetValue(word, out canonical);
        }

        private static Dictionary<string, string> BuildCanonicalMap()
        {
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int i = 0;
            while (i < Words.Length)
            {
                string item = Words[i];
                if (!string.IsNullOrEmpty(item) && !map.ContainsKey(item))
                {
                    map.Add(item, item);
                }

                i++;
            }

            return map;
        }
    }
}
