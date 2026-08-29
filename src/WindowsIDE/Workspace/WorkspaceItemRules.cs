using System;
using System.IO;

namespace WindowsIDE.Workspace
{
    /// <summary>
    /// ワークスペース内のファイル／フォルダ名とパス境界の規則。WinForms に依存しない。
    /// </summary>
    public static class WorkspaceItemRules
    {
        /// <summary>Windows の予約デバイス名。</summary>
        public static readonly string[] ReservedNames = new string[]
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

        /// <summary>
        /// 1 要素のファイル／フォルダ名として妥当なら true。
        /// </summary>
        /// <param name="name">調べる名前。</param>
        /// <param name="error">失敗理由。</param>
        /// <returns>妥当なら true。</returns>
        public static bool TryValidateItemName(string name, out string error)
        {
            error = null;
            if (name == null)
            {
                name = "";
            }

            name = name.Trim();
            if (name.Length == 0)
            {
                error = "名前が空です。";
                return false;
            }

            if (!IsSingleFileName(name) || HasInvalidFileNameChars(name) || name == "." || name == ".."
                || name.EndsWith(".") || char.IsWhiteSpace(name[name.Length - 1]))
            {
                error = "名前が不正です。";
                return false;
            }

            if (IsReservedName(name))
            {
                error = "予約された名前は使えません。";
                return false;
            }

            return true;
        }

        /// <summary>
        /// 親ディレクトリ配下の新規パスを組む。衝突とワークスペース外は失敗する。
        /// </summary>
        /// <param name="workspaceRoot">ワークスペース根。</param>
        /// <param name="parentDir">親ディレクトリ。</param>
        /// <param name="name">1 要素の名前。</param>
        /// <param name="createdPath">成功時の絶対パス。</param>
        /// <param name="error">失敗理由。</param>
        /// <returns>組めたら true。</returns>
        public static bool TryBuildChildPath(string workspaceRoot, string parentDir, string name, out string createdPath, out string error)
        {
            createdPath = null;
            error = null;
            if (string.IsNullOrEmpty(workspaceRoot) || string.IsNullOrEmpty(parentDir))
            {
                error = "作成先がありません。";
                return false;
            }

            if (!TryValidateItemName(name, out error))
            {
                return false;
            }

            name = name.Trim();
            string full;
            try
            {
                full = Path.GetFullPath(Path.Combine(parentDir, name));
            }
            catch (Exception)
            {
                error = "名前が不正です。";
                return false;
            }

            if (!PathGuard.IsInsideWorkspace(workspaceRoot, full))
            {
                error = "ワークスペースの外には作成できません。";
                return false;
            }

            if (File.Exists(full) || Directory.Exists(full))
            {
                error = "同じ名前のファイルまたはフォルダがあります。";
                return false;
            }

            createdPath = full;
            return true;
        }

        /// <summary>
        /// 親を変えず 1 要素だけ差し替えたリネーム先を組む。根と衝突とワークスペース外は失敗する。
        /// </summary>
        /// <param name="workspaceRoot">ワークスペース根。</param>
        /// <param name="existingFull">現在の絶対パス。</param>
        /// <param name="newName">新しい 1 要素名。</param>
        /// <param name="newFull">成功時の絶対パス。</param>
        /// <param name="error">失敗理由。</param>
        /// <returns>組めたら true。</returns>
        public static bool TryBuildRenamedPath(string workspaceRoot, string existingFull, string newName, out string newFull, out string error)
        {
            newFull = null;
            error = null;
            if (string.IsNullOrEmpty(workspaceRoot) || string.IsNullOrEmpty(existingFull))
            {
                error = "リネーム元がありません。";
                return false;
            }

            string existing;
            try
            {
                existing = Path.GetFullPath(existingFull);
            }
            catch (Exception)
            {
                error = "名前が不正です。";
                return false;
            }

            if (IsWorkspaceRoot(workspaceRoot, existing))
            {
                error = "ワークスペースの根はリネームできません。";
                return false;
            }

            if (!PathGuard.IsInsideWorkspace(workspaceRoot, existing))
            {
                error = "ワークスペースの外にはリネームできません。";
                return false;
            }

            if (!TryValidateItemName(newName, out error))
            {
                return false;
            }

            newName = newName.Trim();
            string parent = Path.GetDirectoryName(existing);
            if (string.IsNullOrEmpty(parent))
            {
                error = "親がありません。";
                return false;
            }

            string full;
            try
            {
                full = Path.GetFullPath(Path.Combine(parent, newName));
            }
            catch (Exception)
            {
                error = "名前が不正です。";
                return false;
            }

            if (!PathGuard.IsInsideWorkspace(workspaceRoot, full))
            {
                error = "ワークスペースの外にはリネームできません。";
                return false;
            }

            bool collision = File.Exists(full) || Directory.Exists(full);
            if (collision && !string.Equals(full, existing, StringComparison.OrdinalIgnoreCase))
            {
                error = "同じ名前のファイルまたはフォルダがあります。";
                return false;
            }

            newFull = full;
            return true;
        }

        /// <summary>
        /// path がワークスペース根自身なら true。
        /// </summary>
        /// <param name="workspaceRoot">ワークスペース根。</param>
        /// <param name="path">調べるパス。</param>
        /// <returns>根なら true。</returns>
        public static bool IsWorkspaceRoot(string workspaceRoot, string path)
        {
            if (string.IsNullOrEmpty(workspaceRoot) || string.IsNullOrEmpty(path))
            {
                return false;
            }

            string rootFull;
            string pathFull;
            try
            {
                rootFull = TrimSlash(Path.GetFullPath(workspaceRoot));
                pathFull = TrimSlash(Path.GetFullPath(path));
            }
            catch (Exception)
            {
                return false;
            }

            return string.Equals(rootFull, pathFull, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// candidate が ancestor 自身またはその配下なら true。ディレクトリ境界付き（C:\ws\a は C:\ws\ab を含まない）。
        /// </summary>
        /// <param name="ancestor">祖先パス。</param>
        /// <param name="candidate">調べるパス。</param>
        /// <returns>自身または配下なら true。</returns>
        public static bool IsSameOrUnder(string ancestor, string candidate)
        {
            if (string.IsNullOrEmpty(ancestor) || string.IsNullOrEmpty(candidate))
            {
                return false;
            }

            string ancFull;
            string candFull;
            try
            {
                ancFull = TrimSlash(Path.GetFullPath(ancestor));
                candFull = TrimSlash(Path.GetFullPath(candidate));
            }
            catch (Exception)
            {
                return false;
            }

            if (string.Equals(ancFull, candFull, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string prefix = ancFull + Path.DirectorySeparatorChar;
            return candFull.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// path が oldRoot 自身または配下なら newRoot 付きに差し替える。境界付き。該当しなければ path の絶対パス。
        /// </summary>
        /// <param name="oldRoot">元の根。</param>
        /// <param name="newRoot">新しい根。</param>
        /// <param name="path">対象パス。</param>
        /// <returns>差し替え後の絶対パス。</returns>
        public static string ReplacePathPrefix(string oldRoot, string newRoot, string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return path;
            }

            string pathFull;
            try
            {
                pathFull = Path.GetFullPath(path);
            }
            catch (Exception)
            {
                return path;
            }

            if (!IsSameOrUnder(oldRoot, pathFull))
            {
                return pathFull;
            }

            string oldFull;
            string newFull;
            try
            {
                oldFull = TrimSlash(Path.GetFullPath(oldRoot));
                newFull = TrimSlash(Path.GetFullPath(newRoot));
            }
            catch (Exception)
            {
                return pathFull;
            }

            string pathNorm = TrimSlash(pathFull);
            if (string.Equals(pathNorm, oldFull, StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetFullPath(newFull);
            }

            string relative = pathNorm.Substring(oldFull.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return Path.GetFullPath(Path.Combine(newFull, relative));
        }

        /// <summary>
        /// 区切りを含まない 1 要素の名前なら true。
        /// </summary>
        /// <param name="name">調べる名前。</param>
        /// <returns>1 要素なら true。</returns>
        public static bool IsSingleFileName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            if (name.IndexOf('\\') >= 0 || name.IndexOf('/') >= 0)
            {
                return false;
            }

            string fileName = Path.GetFileName(name);
            return string.Equals(fileName, name, StringComparison.Ordinal);
        }

        /// <summary>
        /// ファイル名に使えない文字を含むなら true。
        /// </summary>
        /// <param name="name">調べる名前。</param>
        /// <returns>不正文字があれば true。</returns>
        public static bool HasInvalidFileNameChars(string name)
        {
            if (name == null)
            {
                return true;
            }

            char[] invalid = Path.GetInvalidFileNameChars();
            for (int i = 0; i < invalid.Length; i++)
            {
                if (name.IndexOf(invalid[i]) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 予約デバイス名（拡張子付きを含む）なら true。
        /// </summary>
        /// <param name="name">調べる名前。</param>
        /// <returns>予約名なら true。</returns>
        public static bool IsReservedName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            if (MatchesReserved(name))
            {
                return true;
            }

            string noExt = Path.GetFileNameWithoutExtension(name);
            if (string.IsNullOrEmpty(noExt))
            {
                return false;
            }

            return MatchesReserved(noExt);
        }

        /// <summary>
        /// 予約表と大小無視で一致するなら true。
        /// </summary>
        /// <param name="name">調べる名前。</param>
        /// <returns>一致すれば true。</returns>
        public static bool MatchesReserved(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            for (int i = 0; i < ReservedNames.Length; i++)
            {
                if (string.Equals(name, ReservedNames[i], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string TrimSlash(string path)
        {
            return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }
}
