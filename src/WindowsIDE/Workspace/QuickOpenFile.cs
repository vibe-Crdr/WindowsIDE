namespace WindowsIDE.Workspace
{
    /// <summary>
    /// クイックオープンの 1 ファイル。ディレクトリは入れない。
    /// </summary>
    public sealed class QuickOpenFile
    {
        private readonly string fullPath;
        private readonly string relativePath;
        private readonly string fileName;

        /// <summary>
        /// フルパス、相対パス、ファイル名を保持する。null は空文字にする。
        /// </summary>
        /// <param name="fullPath">フルパス。</param>
        /// <param name="relativePath">ルートからの相対。区切りは \。</param>
        /// <param name="fileName">ファイル名。</param>
        public QuickOpenFile(string fullPath, string relativePath, string fileName)
        {
            this.fullPath = (fullPath == null) ? "" : fullPath;
            this.relativePath = (relativePath == null) ? "" : relativePath;
            this.fileName = (fileName == null) ? "" : fileName;
        }

        /// <summary>フルパス。絞り込みには使わない。</summary>
        public string FullPath
        {
            get { return this.fullPath; }
        }

        /// <summary>ルートからの相対パス。区切りは \。</summary>
        public string RelativePath
        {
            get { return this.relativePath; }
        }

        /// <summary>ファイル名。</summary>
        public string FileName
        {
            get { return this.fileName; }
        }
    }
}
