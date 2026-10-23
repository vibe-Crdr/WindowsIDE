using System;
using System.Collections.Generic;

namespace WindowsIDE.Workspace
{
    /// <summary>
    /// ファイル名を先、相対パスだけを後に並べる部分一致。フルパスは見ない。
    /// </summary>
    public static class QuickOpenQuery
    {
        /// <summary>
        /// クエリに合う元配列の添字。空クエリは相対パス順の先頭。
        /// </summary>
        /// <param name="files">スナップショット。null なら長さ 0。</param>
        /// <param name="query">/ は \ にする。前後空白は捨て、中の空白は残す。</param>
        /// <param name="maxResults">上限。1 未満なら長さ 0。</param>
        /// <returns>段の中は相対パス順。該当なしは長さ 0。</returns>
        public static int[] Match(QuickOpenFile[] files, string query, int maxResults)
        {
            if (files == null || maxResults < 1)
            {
                return new int[0];
            }

            int[] order = OrderByRelative(files);
            string needle = Normalize(query);
            if (needle.Length == 0)
            {
                return Take(order, maxResults);
            }

            List<int> byName = new List<int>();
            List<int> byPath = new List<int>();
            for (int n = 0; n < order.Length; n++)
            {
                int index = order[n];
                QuickOpenFile file = files[index];
                if (file == null)
                {
                    continue;
                }

                string name = (file.FileName == null) ? "" : file.FileName;
                string relative = (file.RelativePath == null) ? "" : file.RelativePath;
                if (name.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    byName.Add(index);
                }
                else if (relative.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    byPath.Add(index);
                }
            }

            List<int> merged = new List<int>(byName.Count + byPath.Count);
            merged.AddRange(byName);
            merged.AddRange(byPath);
            return TakeList(merged, maxResults);
        }

        private static string Normalize(string query)
        {
            if (query == null || query.Length == 0)
            {
                return "";
            }

            return query.Replace('/', '\\').Trim();
        }

        private static int[] OrderByRelative(QuickOpenFile[] files)
        {
            List<int> indexes = new List<int>();
            for (int i = 0; i < files.Length; i++)
            {
                if (files[i] != null)
                {
                    indexes.Add(i);
                }
            }

            int[] order = indexes.ToArray();
            Array.Sort(order, delegate(int left, int right)
            {
                string a = RelativeOf(files, left);
                string b = RelativeOf(files, right);
                return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
            });
            return order;
        }

        private static string RelativeOf(QuickOpenFile[] files, int index)
        {
            QuickOpenFile file = files[index];
            if (file == null || file.RelativePath == null)
            {
                return "";
            }

            return file.RelativePath;
        }

        private static int[] Take(int[] order, int maxResults)
        {
            int count = order.Length;
            if (count > maxResults)
            {
                count = maxResults;
            }

            int[] taken = new int[count];
            for (int i = 0; i < count; i++)
            {
                taken[i] = order[i];
            }

            return taken;
        }

        private static int[] TakeList(List<int> source, int maxResults)
        {
            int count = source.Count;
            if (count > maxResults)
            {
                count = maxResults;
            }

            int[] taken = new int[count];
            for (int i = 0; i < count; i++)
            {
                taken[i] = source[i];
            }

            return taken;
        }
    }
}
