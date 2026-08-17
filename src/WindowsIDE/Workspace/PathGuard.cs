using System;
using System.IO;

namespace WindowsIDE.Workspace
{
    /// <summary>
    /// ワークスペース配下かどうかを判定する。WinForms に依存しない。
    /// </summary>
    public static class PathGuard
    {
        /// <summary>
        /// candidate が workspaceRoot 自身またはその子孫なら true。相対の .. は解決してから見る。
        /// </summary>
        /// <param name="workspaceRoot">ワークスペースのルート。</param>
        /// <param name="candidate">調べるパス。</param>
        /// <returns>内部なら true。</returns>
        public static bool IsInsideWorkspace(string workspaceRoot, string candidate)
        {
            if (string.IsNullOrEmpty(workspaceRoot) || string.IsNullOrEmpty(candidate))
            {
                return false;
            }

            string rootFull;
            string candFull;
            try
            {
                rootFull = Path.GetFullPath(workspaceRoot);
                candFull = Path.GetFullPath(candidate);
            }
            catch (Exception)
            {
                return false;
            }

            string rootNorm = TrimSlash(rootFull);
            string candNorm = TrimSlash(candFull);
            if (string.Equals(candNorm, rootNorm, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string rootPrefix = rootNorm + Path.DirectorySeparatorChar;
            return candNorm.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase);
        }

        private static string TrimSlash(string path)
        {
            return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }
}
