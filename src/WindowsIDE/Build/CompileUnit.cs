using System;
using System.IO;
using WindowsIDE.Workspace;

namespace WindowsIDE.Build
{
    /// <summary>
    /// 手動ビルドのソース一覧。P14（F-LIVE の無題一時ファイル）とは別。
    /// </summary>
    public static class CompileUnit
    {
        /// <summary>
        /// ワークスペースがあれば根の全 .cs。無ければ focusedPath がディスク上の .cs のとき 1 本。無題・非 .cs は 0 本。
        /// </summary>
        /// <param name="workspaceRoot">開いているフォルダ。無ければ null。</param>
        /// <param name="focusedPath">フォーカス中タブの FilePath。無題は null。</param>
        /// <returns>csc に渡すフルパス。</returns>
        public static string[] Resolve(string workspaceRoot, string focusedPath)
        {
            if (!string.IsNullOrEmpty(workspaceRoot) && Directory.Exists(workspaceRoot))
            {
                return CsFileEnumerator.List(workspaceRoot);
            }

            if (string.IsNullOrEmpty(focusedPath))
            {
                return new string[0];
            }

            string full;
            try
            {
                full = Path.GetFullPath(focusedPath);
            }
            catch (Exception)
            {
                return new string[0];
            }

            if (!File.Exists(full))
            {
                return new string[0];
            }

            if (!string.Equals(Path.GetExtension(full), ".cs", StringComparison.OrdinalIgnoreCase))
            {
                return new string[0];
            }

            return new string[] { full };
        }
    }
}
