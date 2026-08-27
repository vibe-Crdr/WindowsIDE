using System;
using System.Collections.Generic;
using System.IO;
using WindowsIDE.Workspace;

namespace WindowsIDE.Vba
{
    /// <summary>
    /// VBA ルート配下の .bas / .cls 列挙と書き込み計画。WinForms 非依存。
    /// </summary>
    public static class VbaDiskTree
    {
        /// <summary>
        /// root 配下の .bas / .cls フルパス。\bin\ \obj\ \.git\ \.windows-ide\ は除外。.frm は列挙しない。
        /// </summary>
        /// <param name="vbaRoot">VBA ルート。</param>
        /// <returns>残したパス。</returns>
        public static string[] List(string vbaRoot)
        {
            if (string.IsNullOrEmpty(vbaRoot) || !Directory.Exists(vbaRoot))
            {
                return new string[0];
            }

            List<string> kept = new List<string>();
            AddFiles(vbaRoot, "*.bas", kept);
            AddFiles(vbaRoot, "*.cls", kept);
            return kept.ToArray();
        }

        /// <summary>
        /// root からの相対（/ 区切り）。root 外なら null。
        /// </summary>
        /// <param name="vbaRoot">VBA ルート。</param>
        /// <param name="fullPath">ファイルのフルパス。</param>
        /// <returns>相対パス。</returns>
        public static string ToRelPath(string vbaRoot, string fullPath)
        {
            if (string.IsNullOrEmpty(vbaRoot) || string.IsNullOrEmpty(fullPath))
            {
                return null;
            }

            string rootFull;
            string candFull;
            try
            {
                rootFull = Path.GetFullPath(vbaRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                candFull = Path.GetFullPath(fullPath);
            }
            catch (Exception)
            {
                return null;
            }

            if (!PathGuard.IsInsideWorkspace(rootFull, candFull))
            {
                return null;
            }

            if (candFull.Length <= rootFull.Length)
            {
                return null;
            }

            string rel = candFull.Substring(rootFull.Length + 1);
            return rel.Replace('\\', '/');
        }

        /// <summary>
        /// ワークスペース内の書き込み先を計画する。絶対・..・root 外は拒否。
        /// </summary>
        /// <param name="workspaceRoot">ワークスペースルート。</param>
        /// <param name="rootRelative">マップの root（既定 vba）。</param>
        /// <param name="relPath">root からの相対。</param>
        /// <param name="fullPath">成功時のフルパス。</param>
        /// <param name="error">失敗理由。</param>
        /// <returns>書いてよいなら true。</returns>
        public static bool TryPlanWrite(string workspaceRoot, string rootRelative, string relPath, out string fullPath, out string error)
        {
            fullPath = null;
            error = null;
            if (string.IsNullOrEmpty(workspaceRoot))
            {
                error = "ワークスペースがありません。";
                return false;
            }

            if (string.IsNullOrEmpty(rootRelative) || Path.IsPathRooted(rootRelative) || ContainsDotDot(rootRelative))
            {
                error = "VBA ルートがワークスペース内の相対パスではありません。";
                return false;
            }

            if (string.IsNullOrEmpty(relPath) || Path.IsPathRooted(relPath) || ContainsDotDot(relPath))
            {
                error = "相対パスが不正です。";
                return false;
            }

            try
            {
                string rootFull = Path.GetFullPath(Path.Combine(workspaceRoot, rootRelative.Replace('/', Path.DirectorySeparatorChar)));
                if (!PathGuard.IsInsideWorkspace(workspaceRoot, rootFull))
                {
                    error = "VBA ルートがワークスペース外です。";
                    return false;
                }

                string dest = Path.GetFullPath(Path.Combine(rootFull, relPath.Replace('/', Path.DirectorySeparatorChar)));
                if (!PathGuard.IsInsideWorkspace(workspaceRoot, dest) || !PathGuard.IsInsideWorkspace(rootFull, dest))
                {
                    error = "ワークスペース外へは書き込めません。";
                    return false;
                }

                fullPath = dest;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// 拡張子から std / class を決める。document には使わない。
        /// </summary>
        /// <param name="path">ファイルパス。</param>
        /// <returns>.cls なら Class、それ以外は Std。</returns>
        public static VbaComponentKind KindFromPath(string path)
        {
            if (!string.IsNullOrEmpty(path) && path.EndsWith(".cls", StringComparison.OrdinalIgnoreCase))
            {
                return VbaComponentKind.Class;
            }

            return VbaComponentKind.Std;
        }

        /// <summary>
        /// 種別に応じた拡張子（.bas / .cls）。
        /// </summary>
        /// <param name="kind">種別。</param>
        /// <returns>拡張子。</returns>
        public static string ExtensionFor(VbaComponentKind kind)
        {
            if (kind == VbaComponentKind.Std)
            {
                return ".bas";
            }

            return ".cls";
        }

        private static void AddFiles(string root, string pattern, List<string> kept)
        {
            string[] files;
            try
            {
                files = Directory.GetFiles(root, pattern, SearchOption.AllDirectories);
            }
            catch (Exception)
            {
                return;
            }

            for (int i = 0; i < files.Length; i++)
            {
                string path = files[i];
                if (IsExcluded(path))
                {
                    continue;
                }

                kept.Add(path);
            }
        }

        private static bool IsExcluded(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return true;
            }

            return path.IndexOf("\\obj\\", StringComparison.OrdinalIgnoreCase) >= 0
                || path.IndexOf("\\bin\\", StringComparison.OrdinalIgnoreCase) >= 0
                || path.IndexOf("\\.git\\", StringComparison.OrdinalIgnoreCase) >= 0
                || path.IndexOf("\\.windows-ide\\", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool ContainsDotDot(string path)
        {
            string n = path.Replace('\\', '/');
            string[] parts = n.Split('/');
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == "..")
                {
                    return true;
                }
            }

            return false;
        }
    }
}
