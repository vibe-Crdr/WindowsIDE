using System;
using System.Collections.Generic;

namespace WindowsIDE.Commands
{
    /// <summary>
    /// コマンド表示名の部分一致。登録順を保つ。ショートカットは見ない。
    /// </summary>
    public static class CommandQuery
    {
        /// <summary>
        /// 表示名に query が含まれる元配列の添字。空クエリは登録順の先頭。
        /// </summary>
        /// <param name="entries">登録順。null なら長さ 0。</param>
        /// <param name="query">null または前後空白だけなら空クエリ。</param>
        /// <param name="maxResults">上限。1 未満なら長さ 0。</param>
        /// <returns>元配列の添字。該当なしは長さ 0。</returns>
        public static int[] Match(CommandEntry[] entries, string query, int maxResults)
        {
            if (entries == null || maxResults < 1)
            {
                return new int[0];
            }

            string needle = (query == null) ? "" : query.Trim();
            List<int> hits = new List<int>();
            for (int i = 0; i < entries.Length; i++)
            {
                if (hits.Count >= maxResults)
                {
                    break;
                }

                CommandEntry entry = entries[i];
                if (entry == null)
                {
                    continue;
                }

                if (needle.Length == 0)
                {
                    hits.Add(i);
                    continue;
                }

                string name = (entry.Name == null) ? "" : entry.Name;
                if (name.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    hits.Add(i);
                }
            }

            return hits.ToArray();
        }
    }
}
