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
            if (!WorkspaceItemRules.TryBuildChildPath(workspaceRoot, parentDir, name, out createdPath, out error))
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
            if (!WorkspaceItemRules.TryBuildChildPath(workspaceRoot, parentDir, name, out createdPath, out error))
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

    }
}
