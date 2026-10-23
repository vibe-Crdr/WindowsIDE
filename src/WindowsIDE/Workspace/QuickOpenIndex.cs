using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace WindowsIDE.Workspace
{
    /// <summary>
    /// クイックオープン用のファイルスナップショット。UI スレッドで一度だけ呼ぶ。
    /// </summary>
    public static class QuickOpenIndex
    {
        /// <summary>走査の既定上限。</summary>
        public const int DefaultMax = 20000;

        /// <summary>
        /// ルート配下のファイル。bin / obj / .git には降りない。拡張子では絞らない。
        /// </summary>
        /// <param name="root">ワークスペースルート。無い、またはディレクトリでなければ長さ 0。</param>
        /// <param name="maxFiles">採用上限。1 未満なら長さ 0。</param>
        /// <returns>その時点までの採用分を相対パス順に並べた配列。</returns>
        public static QuickOpenFile[] List(string root, int maxFiles)
        {
            if (string.IsNullOrEmpty(root) || maxFiles < 1 || !Directory.Exists(root))
            {
                return new QuickOpenFile[0];
            }

            string rootFull;
            try
            {
                rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch (Exception)
            {
                return new QuickOpenFile[0];
            }

            if (IsSkippedDirectoryName(Path.GetFileName(rootFull)))
            {
                return new QuickOpenFile[0];
            }

            List<QuickOpenFile> sink = new List<QuickOpenFile>();
            Dictionary<string, bool> visited = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            Walk(rootFull, rootFull, maxFiles, sink, visited);
            sink.Sort(CompareRelative);
            return sink.ToArray();
        }

        private static void Walk(string dir, string rootFull, int maxFiles, List<QuickOpenFile> sink, Dictionary<string, bool> visited)
        {
            string identity = DirectoryIdentity(dir);
            if (identity.Length == 0 || visited.ContainsKey(identity))
            {
                return;
            }

            visited[identity] = true;
            if (sink.Count >= maxFiles)
            {
                return;
            }

            string[] files = TryGetFiles(dir);
            if (files != null)
            {
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < files.Length; i++)
                {
                    if (sink.Count >= maxFiles)
                    {
                        return;
                    }

                    Adopt(files[i], rootFull, sink);
                }
            }

            if (sink.Count >= maxFiles)
            {
                return;
            }

            string[] dirs = TryGetDirectories(dir);
            if (dirs == null)
            {
                return;
            }

            Array.Sort(dirs, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < dirs.Length; i++)
            {
                if (sink.Count >= maxFiles)
                {
                    return;
                }

                string child = dirs[i];
                if (!PathGuard.IsInsideWorkspace(rootFull, child))
                {
                    continue;
                }

                string childName = Path.GetFileName(child.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (IsSkippedDirectoryName(childName))
                {
                    continue;
                }

                if (ContainsSkippedSegment(child))
                {
                    continue;
                }

                if (!CanDescend(child, rootFull, visited))
                {
                    continue;
                }

                Walk(child, rootFull, maxFiles, sink, visited);
            }
        }

        private static void Adopt(string path, string rootFull, List<QuickOpenFile> sink)
        {
            if (string.IsNullOrEmpty(path) || !PathGuard.IsInsideWorkspace(rootFull, path))
            {
                return;
            }

            string full;
            try
            {
                full = Path.GetFullPath(path);
            }
            catch (Exception)
            {
                return;
            }

            if (ContainsSkippedSegment(full))
            {
                return;
            }

            string relative = ToRelative(rootFull, full);
            if (relative.Length == 0)
            {
                return;
            }

            sink.Add(new QuickOpenFile(full, relative, Path.GetFileName(full)));
        }

        /// <summary>
        /// 解決先がワークスペース外、または既に歩いたディレクトリなら降りない。解決できないリパースポイントも降りない。
        /// </summary>
        private static bool CanDescend(string child, string rootFull, Dictionary<string, bool> visited)
        {
            string identity = DirectoryIdentity(child);
            if (identity.Length == 0)
            {
                return false;
            }

            if (!PathGuard.IsInsideWorkspace(rootFull, identity))
            {
                return false;
            }

            return !visited.ContainsKey(identity);
        }

        /// <summary>
        /// ディレクトリの解決先。最終パスを優先する。解決できないリパースポイントは空。通常ディレクトリだけ GetFullPath に戻す。
        /// </summary>
        private static string DirectoryIdentity(string path)
        {
            string final = TryFinalPath(path);
            if (final.Length > 0)
            {
                return final;
            }

            bool reparse;
            if (!TryIsReparsePoint(path, out reparse) || reparse)
            {
                return "";
            }

            try
            {
                return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch (Exception)
            {
                return "";
            }
        }

        private static bool TryIsReparsePoint(string path, out bool reparse)
        {
            reparse = false;
            try
            {
                DirectoryInfo info = new DirectoryInfo(path);
                reparse = (info.Attributes & FileAttributes.ReparsePoint) != 0;
                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static string TryFinalPath(string path)
        {
            IntPtr handle = Native.CreateFile(path, 0, Native.FileShareReadWriteDelete, IntPtr.Zero, Native.OpenExisting, Native.FileFlagBackupSemantics, IntPtr.Zero);
            if (handle == Native.InvalidHandle)
            {
                return "";
            }

            try
            {
                StringBuilder buffer = new StringBuilder(520);
                int length = Native.GetFinalPathNameByHandle(handle, buffer, buffer.Capacity, 0);
                if (length <= 0)
                {
                    return "";
                }

                if (length >= buffer.Capacity)
                {
                    buffer = new StringBuilder(length);
                    length = Native.GetFinalPathNameByHandle(handle, buffer, buffer.Capacity, 0);
                    if (length <= 0 || length >= buffer.Capacity)
                    {
                        return "";
                    }
                }

                return NormalizeFinal(buffer.ToString(0, length));
            }
            finally
            {
                Native.CloseHandle(handle);
            }
        }

        private static string NormalizeFinal(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return "";
            }

            if (path.StartsWith(@"\\?\", StringComparison.Ordinal))
            {
                if (path.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
                {
                    path = @"\\" + path.Substring(8);
                }
                else
                {
                    path = path.Substring(4);
                }
            }

            try
            {
                path = Path.GetFullPath(path);
            }
            catch (Exception)
            {
                return "";
            }

            return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static class Native
        {
            internal static readonly IntPtr InvalidHandle = new IntPtr(-1);
            internal const int FileShareReadWriteDelete = 7;
            internal const int OpenExisting = 3;
            internal const int FileFlagBackupSemantics = 0x02000000;

            [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            internal static extern IntPtr CreateFile(string lpFileName, int dwDesiredAccess, int dwShareMode, IntPtr lpSecurityAttributes, int dwCreationDisposition, int dwFlagsAndAttributes, IntPtr hTemplateFile);

            [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            internal static extern int GetFinalPathNameByHandle(IntPtr hFile, StringBuilder lpszFilePath, int cchFilePath, int dwFlags);

            [DllImport("kernel32.dll", SetLastError = true)]
            internal static extern bool CloseHandle(IntPtr hObject);
        }

        private static string[] TryGetFiles(string dir)
        {
            try
            {
                return Directory.GetFiles(dir);
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        private static string[] TryGetDirectories(string dir)
        {
            try
            {
                return Directory.GetDirectories(dir);
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        private static bool IsSkippedDirectoryName(string name)
        {
            return string.Equals(name, "bin", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "obj", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, ".git", StringComparison.OrdinalIgnoreCase);
        }

        private static bool ContainsSkippedSegment(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath))
            {
                return false;
            }

            if (fullPath.IndexOf("\\bin\\", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            if (fullPath.IndexOf("\\obj\\", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            return fullPath.IndexOf("\\.git\\", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string ToRelative(string rootFull, string fullPath)
        {
            string prefix = rootFull + "\\";
            if (!fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetFileName(fullPath);
            }

            return fullPath.Substring(prefix.Length).Replace('/', '\\');
        }

        private static int CompareRelative(QuickOpenFile left, QuickOpenFile right)
        {
            string a = (left == null || left.RelativePath == null) ? "" : left.RelativePath;
            string b = (right == null || right.RelativePath == null) ? "" : right.RelativePath;
            return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
        }
    }
}
