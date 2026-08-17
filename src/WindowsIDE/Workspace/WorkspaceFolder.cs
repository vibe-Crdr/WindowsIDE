using System.IO;

namespace WindowsIDE.Workspace
{
    /// <summary>
    /// 開いているフォルダと設定。
    /// </summary>
    public sealed class WorkspaceFolder
    {
        private string rootPath;
        private WorkspaceSettings settings;

        private WorkspaceFolder(string rootPath, WorkspaceSettings settings)
        {
            this.rootPath = rootPath;
            this.settings = settings;
        }

        /// <summary>正規化したルートパス。</summary>
        public string RootPath { get { return this.rootPath; } }

        /// <summary>fontSize / tabSize。</summary>
        public WorkspaceSettings Settings { get { return this.settings; } }

        /// <summary>
        /// フォルダを開く。workspace.xml が無ければ既定値。XML は作らない。
        /// </summary>
        /// <param name="path">フォルダパス。</param>
        /// <returns>ワークスペース。</returns>
        public static WorkspaceFolder Open(string path)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            {
                throw new DirectoryNotFoundException(path);
            }

            string full = Path.GetFullPath(path);
            WorkspaceSettings settings = WorkspaceSettings.Load(full);
            return new WorkspaceFolder(full, settings);
        }

        /// <summary>
        /// パスがこのワークスペース内なら true。
        /// </summary>
        public bool Contains(string path)
        {
            return PathGuard.IsInsideWorkspace(this.rootPath, path);
        }
    }
}
