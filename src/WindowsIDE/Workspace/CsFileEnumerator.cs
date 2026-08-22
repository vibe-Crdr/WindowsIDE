using System;
using System.Collections.Generic;
using System.IO;

namespace WindowsIDE.Workspace
{
    /// <summary>
    /// ワークスペース配下の .cs を列挙する。パスに \bin\ \obj\ \.git\ を含むものを除外する。
    /// </summary>
    public static class CsFileEnumerator
    {
        /// <summary>
        /// ルート配下の .cs フルパス。root が空、または存在しなければ空配列。
        /// </summary>
        /// <param name="root">ワークスペースルート。</param>
        /// <returns>残したパス。</returns>
        public static string[] List(string root)
        {
            int count;
            long stamp;
            return List(root, out count, out stamp);
        }

        /// <summary>
        /// ルート配下の .cs を列挙し、件数と最新 UTC ticks も返す（ハイライトキャッシュ用）。
        /// </summary>
        /// <param name="root">ワークスペースルート。</param>
        /// <param name="count">残したファイル数。</param>
        /// <param name="stamp">最新 LastWriteTimeUtc.Ticks。無ければ 0。</param>
        /// <returns>残したパス。</returns>
        public static string[] List(string root, out int count, out long stamp)
        {
            count = 0;
            stamp = 0;
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
            {
                return new string[0];
            }

            string[] files;
            try
            {
                files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
            }
            catch (Exception)
            {
                return new string[0];
            }

            List<string> kept = new List<string>();
            for (int i = 0; i < files.Length; i++)
            {
                string path = files[i];
                if (path.IndexOf("\\obj\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    path.IndexOf("\\bin\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    path.IndexOf("\\.git\\", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                kept.Add(path);
                count++;
                try
                {
                    long ticks = File.GetLastWriteTimeUtc(path).Ticks;
                    if (ticks > stamp)
                    {
                        stamp = ticks;
                    }
                }
                catch (Exception)
                {
                }
            }

            return kept.ToArray();
        }
    }
}
