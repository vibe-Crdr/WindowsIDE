using System;
using System.IO;

namespace WindowsIDE.Languages
{
    /// <summary>
    /// Markdown 画像パスの正規化。http と親越えを拒否する。WinForms / Image は参照しない。
    /// </summary>
    public static class MarkdownLocalPath
    {
        /// <summary>
        /// 相対パスが基準ディレクトリ自身または配下に収まるなら絶対パスを返す。
        /// </summary>
        /// <param name="documentDirectory">`.md` のディレクトリ。null や空は拒否。</param>
        /// <param name="destination">リンク先。相対パスのみ。</param>
        /// <param name="fullPath">正規化した絶対パス。</param>
        /// <returns>配下に収まるなら true。</returns>
        public static bool TryNormalize(string documentDirectory, string destination, out string fullPath)
        {
            fullPath = null;
            if (string.IsNullOrEmpty(documentDirectory) || string.IsNullOrEmpty(destination))
            {
                return false;
            }

            if (HasRemoteScheme(destination))
            {
                return false;
            }

            string dest = destination.Trim();
            if (dest.Length == 0)
            {
                return false;
            }

            try
            {
                if (Path.IsPathRooted(dest))
                {
                    return false;
                }

                string root = TrimSlash(Path.GetFullPath(documentDirectory));
                string combined = TrimSlash(Path.GetFullPath(Path.Combine(root, dest)));
                if (string.Equals(root, combined, StringComparison.OrdinalIgnoreCase))
                {
                    fullPath = combined;
                    return true;
                }

                string prefix = root + Path.DirectorySeparatorChar;
                if (!combined.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                fullPath = combined;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// http(s) などスキーム付きなら true。
        /// </summary>
        /// <param name="destination">リンク先。</param>
        /// <returns>リモートなら true。</returns>
        public static bool HasRemoteScheme(string destination)
        {
            if (string.IsNullOrEmpty(destination))
            {
                return false;
            }

            int i = 0;
            while (i + 2 < destination.Length)
            {
                if (destination[i] == ':' && destination[i + 1] == '/' && destination[i + 2] == '/')
                {
                    return true;
                }

                i++;
            }

            return false;
        }

        private static string TrimSlash(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return path;
            }

            char last = path[path.Length - 1];
            if (last == Path.DirectorySeparatorChar || last == Path.AltDirectorySeparatorChar)
            {
                return path.Substring(0, path.Length - 1);
            }

            return path;
        }
    }
}
