using System;
using System.IO;
using WindowsIDE.Editor;

namespace WindowsIDE.Workspace
{
    /// <summary>
    /// ワークスペース内へのファイル／フォルダ新規作成の規則。WinForms に依存しない。
    /// </summary>
    public static class WorkspaceCreateRules
    {
        private static readonly string[] ReservedNames = new string[]
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

        /// <summary>
        /// 作成先ディレクトリを決める。選択がフォルダならそれ、ファイルなら親、空／不正なら根。外なら根（根も外なら null）。
        /// </summary>
        /// <param name="workspaceRoot">ワークスペース根。</param>
        /// <param name="selectedPath">ツリー選択パス。null 可。</param>
        /// <returns>作成先の絶対パス。決められなければ null。</returns>
        public static string ResolveCreateDirectory(string workspaceRoot, string selectedPath)
        {
            if (string.IsNullOrEmpty(workspaceRoot))
            {
                return null;
            }

            string rootFull;
            try
            {
                rootFull = Path.GetFullPath(workspaceRoot);
            }
            catch (Exception)
            {
                return null;
            }

            if (!PathGuard.IsInsideWorkspace(rootFull, rootFull))
            {
                return null;
            }

            string candidate = rootFull;
            if (!string.IsNullOrEmpty(selectedPath))
            {
                try
                {
                    string selectedFull = Path.GetFullPath(selectedPath);
                    if (PathGuard.IsInsideWorkspace(rootFull, selectedFull))
                    {
                        if (Directory.Exists(selectedFull))
                        {
                            candidate = selectedFull;
                        }
                        else if (File.Exists(selectedFull))
                        {
                            string parent = Path.GetDirectoryName(selectedFull);
                            if (!string.IsNullOrEmpty(parent))
                            {
                                candidate = parent;
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    candidate = rootFull;
                }
            }

            string result;
            try
            {
                result = Path.GetFullPath(candidate);
            }
            catch (Exception)
            {
                return null;
            }

            if (!PathGuard.IsInsideWorkspace(rootFull, result))
            {
                result = rootFull;
            }

            if (!PathGuard.IsInsideWorkspace(rootFull, result))
            {
                return null;
            }

            return result;
        }

        /// <summary>
        /// 空ファイルを CreateNew で作る。同名は上書きしない。
        /// </summary>
        /// <param name="workspaceRoot">ワークスペース根。</param>
        /// <param name="parentDir">作成先ディレクトリ。</param>
        /// <param name="name">ファイル名（1 要素）。</param>
        /// <param name="createdPath">成功時の絶対パス。</param>
        /// <param name="error">失敗理由。</param>
        /// <returns>作れたら true。</returns>
        public static bool TryCreateFile(string workspaceRoot, string parentDir, string name, out string createdPath, out string error)
        {
            if (!TryBuildNewPath(workspaceRoot, parentDir, name, out createdPath, out error))
            {
                return false;
            }

            try
            {
                byte[] bytes = FileEncoding.GetBytesForNewEmptyFile(createdPath);
                if (bytes == null)
                {
                    bytes = new byte[0];
                }

                FileStream stream = new FileStream(createdPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                try
                {
                    if (bytes.Length > 0)
                    {
                        stream.Write(bytes, 0, bytes.Length);
                    }
                }
                finally
                {
                    stream.Dispose();
                }

                return true;
            }
            catch (IOException ex)
            {
                createdPath = null;
                error = ex.Message;
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                createdPath = null;
                error = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// フォルダを作る。既存なら失敗する。
        /// </summary>
        /// <param name="workspaceRoot">ワークスペース根。</param>
        /// <param name="parentDir">作成先ディレクトリ。</param>
        /// <param name="name">フォルダ名（1 要素）。</param>
        /// <param name="createdPath">成功時の絶対パス。</param>
        /// <param name="error">失敗理由。</param>
        /// <returns>作れたら true。</returns>
        public static bool TryCreateDirectory(string workspaceRoot, string parentDir, string name, out string createdPath, out string error)
        {
            if (!TryBuildNewPath(workspaceRoot, parentDir, name, out createdPath, out error))
            {
                return false;
            }

            try
            {
                if (Directory.Exists(createdPath) || File.Exists(createdPath))
                {
                    error = "同じ名前のファイルまたはフォルダがあります。";
                    createdPath = null;
                    return false;
                }

                Directory.CreateDirectory(createdPath);
                if (!Directory.Exists(createdPath))
                {
                    error = "フォルダを作成できません。";
                    createdPath = null;
                    return false;
                }

                return true;
            }
            catch (IOException ex)
            {
                createdPath = null;
                error = ex.Message;
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                createdPath = null;
                error = ex.Message;
                return false;
            }
        }

        private static bool TryBuildNewPath(string workspaceRoot, string parentDir, string name, out string createdPath, out string error)
        {
            createdPath = null;
            error = null;
            if (string.IsNullOrEmpty(workspaceRoot) || string.IsNullOrEmpty(parentDir))
            {
                error = "作成先がありません。";
                return false;
            }

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

        private static bool IsSingleFileName(string name)
        {
            if (name.IndexOf('\\') >= 0 || name.IndexOf('/') >= 0)
            {
                return false;
            }

            string fileName = Path.GetFileName(name);
            return string.Equals(fileName, name, StringComparison.Ordinal);
        }

        private static bool HasInvalidFileNameChars(string name)
        {
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

        private static bool IsReservedName(string name)
        {
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

        private static bool MatchesReserved(string name)
        {
            for (int i = 0; i < ReservedNames.Length; i++)
            {
                if (string.Equals(name, ReservedNames[i], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
