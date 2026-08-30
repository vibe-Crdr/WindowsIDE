using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using WindowsIDE.Workspace;

namespace WindowsIDE.Build
{
    /// <summary>
    /// 常時 csc へ渡す開いているバッファ。Editor.Document は参照しない。
    /// </summary>
    public sealed class LiveBuffer
    {
        private string filePath;
        private string displayName;
        private string text;
        private bool isDirty;
        private bool isCSharp;

        /// <summary>ディスクパス。無題は null。</summary>
        public string FilePath
        {
            get { return this.filePath; }
            set { this.filePath = value; }
        }

        /// <summary>タブ表示名。無題の照合に使う。</summary>
        public string DisplayName
        {
            get { return this.displayName; }
            set { this.displayName = value; }
        }

        /// <summary>本文。</summary>
        public string Text
        {
            get { return this.text; }
            set { this.text = value; }
        }

        /// <summary>未保存なら true。</summary>
        public bool IsDirty
        {
            get { return this.isDirty; }
            set { this.isDirty = value; }
        }

        /// <summary>C# として live に含めるなら true。無題 Ctrl+N（Plain）は false。</summary>
        public bool IsCSharp
        {
            get { return this.isCSharp; }
            set { this.isCSharp = value; }
        }
    }

    /// <summary>
    /// 1 回の live csc のパス対応。CscPaths と LogicalPaths は平行配列。
    /// </summary>
    public sealed class LiveCompileSnapshot
    {
        private string[] cscPaths;
        private string[] logicalPaths;
        private string outputDir;
        private string rspPath;
        private string outputDll;

        /// <summary>
        /// 空のスナップショット。
        /// </summary>
        public LiveCompileSnapshot()
        {
            this.cscPaths = new string[0];
            this.logicalPaths = new string[0];
        }

        /// <summary>csc に渡すパス（TEMP またはディスク）。</summary>
        public string[] CscPaths
        {
            get { return this.cscPaths; }
            set { this.cscPaths = (value == null) ? new string[0] : value; }
        }

        /// <summary>問題一覧・波線用の論理パス。無題は null または空。</summary>
        public string[] LogicalPaths
        {
            get { return this.logicalPaths; }
            set { this.logicalPaths = (value == null) ? new string[0] : value; }
        }

        /// <summary>live 出力ディレクトリ。診断後に削除する。</summary>
        public string OutputDir
        {
            get { return this.outputDir; }
            set { this.outputDir = value; }
        }

        /// <summary>live rsp のパス。</summary>
        public string RspPath
        {
            get { return this.rspPath; }
            set { this.rspPath = value; }
        }

        /// <summary>out.dll のパス。起動しない。</summary>
        public string OutputDll
        {
            get { return this.outputDll; }
            set { this.outputDll = value; }
        }
    }

    /// <summary>
    /// 常時 csc のソース一覧と TEMP リマップ。Save しない。
    /// </summary>
    public static class LiveCompileUnit
    {
        /// <summary>
        /// 開いているバッファとワークスペースから live 単位を組む。0 本なら空（合成診断は出さない）。
        /// </summary>
        /// <param name="workspaceRoot">開いているフォルダ。無ければ null。</param>
        /// <param name="open">開いているタブ。null は空。</param>
        /// <param name="focusedPath">フォーカス中の FilePath。無題は null。</param>
        /// <returns>スナップショット。</returns>
        public static LiveCompileSnapshot Build(string workspaceRoot, LiveBuffer[] open, string focusedPath)
        {
            List<string> csc = new List<string>();
            List<string> logical = new List<string>();
            List<TempWrite> temps = new List<TempWrite>();
            int untitledN = 0;
            int dirtyN = 0;

            if (!string.IsNullOrEmpty(workspaceRoot) && Directory.Exists(workspaceRoot))
            {
                string[] disk = CsFileEnumerator.List(workspaceRoot);
                Dictionary<string, int> indexByPath = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                int i = 0;
                while (i < disk.Length)
                {
                    string full = NormalizeFull(disk[i]);
                    i++;
                    if (string.IsNullOrEmpty(full))
                    {
                        continue;
                    }

                    indexByPath[full] = csc.Count;
                    csc.Add(full);
                    logical.Add(full);
                }

                if (open != null)
                {
                    int b = 0;
                    while (b < open.Length)
                    {
                        LiveBuffer buf = open[b];
                        b++;
                        if (buf == null)
                        {
                            continue;
                        }

                        if (buf.IsCSharp && string.IsNullOrEmpty(buf.FilePath))
                        {
                            string name = "untitled-" + untitledN.ToString() + ".cs";
                            untitledN++;
                            temps.Add(TempWrite.Create(name, buf.Text, null));
                            continue;
                        }

                        if (string.IsNullOrEmpty(buf.FilePath) || !IsCsPath(buf.FilePath))
                        {
                            continue;
                        }

                        string path = NormalizeFull(buf.FilePath);
                        if (string.IsNullOrEmpty(path))
                        {
                            continue;
                        }

                        if (buf.IsDirty)
                        {
                            string name = "dirty-" + dirtyN.ToString() + ".cs";
                            dirtyN++;
                            int existing;
                            if (indexByPath.TryGetValue(path, out existing))
                            {
                                temps.Add(TempWrite.Replace(existing, name, buf.Text, path));
                            }
                            else
                            {
                                temps.Add(TempWrite.Create(name, buf.Text, path));
                            }
                        }
                        else if (!indexByPath.ContainsKey(path))
                        {
                            indexByPath[path] = csc.Count;
                            csc.Add(path);
                            logical.Add(path);
                        }
                    }
                }
            }
            else
            {
                if (open != null)
                {
                    int b = 0;
                    while (b < open.Length)
                    {
                        LiveBuffer buf = open[b];
                        b++;
                        if (buf == null)
                        {
                            continue;
                        }

                        if (buf.IsCSharp && string.IsNullOrEmpty(buf.FilePath))
                        {
                            string name = "untitled-" + untitledN.ToString() + ".cs";
                            untitledN++;
                            temps.Add(TempWrite.Create(name, buf.Text, null));
                        }
                    }
                }

                string focused = NormalizeFull(focusedPath);
                if (!string.IsNullOrEmpty(focused) && IsCsPath(focused) && File.Exists(focused))
                {
                    LiveBuffer match = FindOpen(open, focused);
                    if (match != null && match.IsDirty)
                    {
                        string name = "dirty-" + dirtyN.ToString() + ".cs";
                        dirtyN++;
                        temps.Add(TempWrite.Create(name, match.Text, focused));
                    }
                    else
                    {
                        csc.Add(focused);
                        logical.Add(focused);
                    }
                }
            }

            if (csc.Count == 0 && temps.Count == 0)
            {
                return new LiveCompileSnapshot();
            }

            string keyPath = workspaceRoot;
            if (string.IsNullOrEmpty(keyPath))
            {
                keyPath = focusedPath;
            }

            if (string.IsNullOrEmpty(keyPath))
            {
                keyPath = "untitled";
            }

            string outputDll = CscArgumentBuilder.GetLiveOutputDllPath(keyPath);
            string outputDir = Path.GetDirectoryName(outputDll);
            Encoding utf8Bom = new UTF8Encoding(true);
            int t = 0;
            while (t < temps.Count)
            {
                TempWrite w = temps[t];
                t++;
                string cscPath = Path.Combine(outputDir, w.FileName);
                File.WriteAllText(cscPath, w.Text == null ? "" : w.Text, utf8Bom);
                if (w.ReplaceIndex >= 0)
                {
                    csc[w.ReplaceIndex] = cscPath;
                    logical[w.ReplaceIndex] = w.LogicalPath;
                }
                else
                {
                    csc.Add(cscPath);
                    logical.Add(w.LogicalPath);
                }
            }

            string rspPath = Path.Combine(outputDir, "csc.rsp");
            CscArgumentBuilder.WriteLiveResponseFile(rspPath, outputDll, csc.ToArray());

            LiveCompileSnapshot snapshot = new LiveCompileSnapshot();
            snapshot.CscPaths = csc.ToArray();
            snapshot.LogicalPaths = logical.ToArray();
            snapshot.OutputDir = outputDir;
            snapshot.RspPath = rspPath;
            snapshot.OutputDll = outputDll;
            return snapshot;
        }

        /// <summary>
        /// csc が出した TEMP パスを論理パスへ戻す。一致しなければ live 配下なら空にする。
        /// </summary>
        /// <param name="diagnostic">csc の 1 件。</param>
        /// <param name="snapshot">この回のスナップショット。</param>
        /// <returns>リマップした診断。</returns>
        public static Diagnostic Remap(Diagnostic diagnostic, LiveCompileSnapshot snapshot)
        {
            if (diagnostic == null)
            {
                return diagnostic;
            }

            if (snapshot == null || snapshot.CscPaths == null || snapshot.LogicalPaths == null)
            {
                return diagnostic;
            }

            string path = diagnostic.FilePath;
            if (string.IsNullOrEmpty(path))
            {
                return diagnostic;
            }

            string full = NormalizeFull(path);
            if (string.IsNullOrEmpty(full))
            {
                full = path;
            }

            int i = 0;
            int n = snapshot.CscPaths.Length;
            if (snapshot.LogicalPaths.Length < n)
            {
                n = snapshot.LogicalPaths.Length;
            }

            while (i < n)
            {
                string cscPath = NormalizeFull(snapshot.CscPaths[i]);
                if (!string.IsNullOrEmpty(cscPath) && string.Equals(cscPath, full, StringComparison.OrdinalIgnoreCase))
                {
                    return diagnostic.WithFilePath(snapshot.LogicalPaths[i]);
                }

                i++;
            }

            if (!string.IsNullOrEmpty(snapshot.OutputDir))
            {
                string dir = NormalizeFull(snapshot.OutputDir);
                if (!string.IsNullOrEmpty(dir) && full.StartsWith(dir, StringComparison.OrdinalIgnoreCase))
                {
                    return diagnostic.WithFilePath(null);
                }
            }

            return diagnostic;
        }

        /// <summary>
        /// live ディレクトリをベストエフォートで消す。
        /// </summary>
        /// <param name="dir">消すディレクトリ。</param>
        /// <returns>消せたら true。</returns>
        public static bool TryDeleteDirectory(string dir)
        {
            if (string.IsNullOrEmpty(dir))
            {
                return false;
            }

            try
            {
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, true);
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static LiveBuffer FindOpen(LiveBuffer[] open, string fullPath)
        {
            if (open == null || string.IsNullOrEmpty(fullPath))
            {
                return null;
            }

            int i = 0;
            while (i < open.Length)
            {
                LiveBuffer buf = open[i];
                i++;
                if (buf == null || string.IsNullOrEmpty(buf.FilePath))
                {
                    continue;
                }

                string p = NormalizeFull(buf.FilePath);
                if (string.Equals(p, fullPath, StringComparison.OrdinalIgnoreCase))
                {
                    return buf;
                }
            }

            return null;
        }

        private static bool IsCsPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            return string.Equals(Path.GetExtension(path), ".cs", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeFull(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            try
            {
                return Path.GetFullPath(path);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private sealed class TempWrite
        {
            public string FileName;
            public string Text;
            public string LogicalPath;
            public int ReplaceIndex;

            public static TempWrite Create(string fileName, string text, string logicalPath)
            {
                TempWrite w = new TempWrite();
                w.FileName = fileName;
                w.Text = text;
                w.LogicalPath = logicalPath;
                w.ReplaceIndex = -1;
                return w;
            }

            public static TempWrite Replace(int index, string fileName, string text, string logicalPath)
            {
                TempWrite w = Create(fileName, text, logicalPath);
                w.ReplaceIndex = index;
                return w;
            }
        }
    }
}
