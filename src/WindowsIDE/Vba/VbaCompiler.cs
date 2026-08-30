using System;
using System.IO;
using WindowsIDE.Build;
using WindowsIDE.Editor;

namespace WindowsIDE.Vba
{
    /// <summary>
    /// 手動 VBA Compile。CommandBar Id 578。セッションは object。WinForms に依存しない。
    /// </summary>
    public static class VbaCompiler
    {
        private const int CompileControlId = 578;
        private const string FailMessage = "VBA のコンパイルに失敗した";
        private const string UnavailableMessage = "VBA のコンパイル診断を取得できませんでした。";

        /// <summary>
        /// プッシュ成功後の同じ Excel セッションで Compile する。Quit しない。レキサで埋めない。
        /// </summary>
        /// <param name="excelApp">Excel.Application。object。</param>
        /// <param name="map">ディスクパスへのマップ。</param>
        /// <param name="workspaceRoot">ワークスペース根。</param>
        /// <returns>成功なら空配列。失敗は問題一覧用診断。</returns>
        public static Diagnostic[] Compile(object excelApp, VbaMap map, string workspaceRoot)
        {
            if (excelApp == null)
            {
                return new Diagnostic[] { Diagnostic.CreateSynthetic(UnavailableMessage) };
            }

            try
            {
                object vbe = ComInvoker.GetProperty(excelApp, "VBE");
                if (vbe == null)
                {
                    return new Diagnostic[] { Diagnostic.CreateSynthetic(UnavailableMessage) };
                }

                object commandBars = ComInvoker.GetProperty(vbe, "CommandBars");
                if (commandBars == null)
                {
                    return new Diagnostic[] { Diagnostic.CreateSynthetic(UnavailableMessage) };
                }

                object compileControl = ComInvoker.Call(commandBars, "FindControl", Type.Missing, CompileControlId);
                if (compileControl == null)
                {
                    return new Diagnostic[] { Diagnostic.CreateSynthetic(UnavailableMessage) };
                }

                if (!IsEnabled(compileControl))
                {
                    return new Diagnostic[0];
                }

                ComInvoker.Call(compileControl, "Execute");
                if (!IsEnabled(compileControl))
                {
                    return new Diagnostic[0];
                }

                return BuildFailure(vbe, map, workspaceRoot);
            }
            catch (Exception)
            {
                return new Diagnostic[] { Diagnostic.CreateSynthetic(UnavailableMessage) };
            }
        }

        private static bool IsEnabled(object control)
        {
            object value = ComInvoker.GetProperty(control, "Enabled");
            if (value == null)
            {
                return false;
            }

            try
            {
                return Convert.ToBoolean(value);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static Diagnostic[] BuildFailure(object vbe, VbaMap map, string workspaceRoot)
        {
            int startLine = 0;
            int startColumn = 0;
            int endLine = 0;
            int endColumn = 0;
            string filePath = null;
            try
            {
                object pane = ComInvoker.GetProperty(vbe, "ActiveCodePane");
                if (pane != null)
                {
                    object[] sel = new object[] { 0, 0, 0, 0 };
                    ComInvoker.CallByRef(pane, "GetSelection", sel);
                    startLine = ToInt(sel[0]);
                    startColumn = ToInt(sel[1]);
                    endLine = ToInt(sel[2]);
                    endColumn = ToInt(sel[3]);
                    object codeModule = ComInvoker.GetProperty(pane, "CodeModule");
                    if (codeModule != null)
                    {
                        object parent = ComInvoker.GetProperty(codeModule, "Parent");
                        if (parent != null)
                        {
                            string name = Convert.ToString(ComInvoker.GetProperty(parent, "Name"));
                            filePath = MapToDisk(map, workspaceRoot, name);
                        }
                    }
                }
            }
            catch (Exception)
            {
            }

            if (!string.IsNullOrEmpty(filePath) && (startLine > 0 || endLine > 0))
            {
                try
                {
                    byte[] bytes = File.ReadAllBytes(filePath);
                    string diskText;
                    FileEncodingInfo info;
                    string err;
                    if (FileEncoding.TryDecode(bytes, out diskText, out info, out err))
                    {
                        startLine = VbaExportText.ToDiskLine(startLine, diskText);
                        endLine = VbaExportText.ToDiskLine(endLine, diskText);
                    }
                }
                catch (Exception)
                {
                    // 未変換。Compile の外側 catch に逃さない。
                }
            }

            Diagnostic d = Diagnostic.FromCompiler(filePath, startLine, startColumn, true, null, FailMessage, endLine, endColumn);
            return new Diagnostic[] { d };
        }

        private static string MapToDisk(VbaMap map, string workspaceRoot, string excelName)
        {
            if (map == null || string.IsNullOrEmpty(excelName))
            {
                return null;
            }

            VbaMapComponent found = map.FindByName(excelName);
            if (found == null || string.IsNullOrEmpty(found.RelPath))
            {
                return null;
            }

            string root = map.ResolveRootFullPath(workspaceRoot);
            if (string.IsNullOrEmpty(root))
            {
                return null;
            }

            string rel = found.RelPath.Replace('/', Path.DirectorySeparatorChar);
            try
            {
                return Path.GetFullPath(Path.Combine(root, rel));
            }
            catch (Exception)
            {
                return Path.Combine(root, rel);
            }
        }

        private static int ToInt(object value)
        {
            if (value == null)
            {
                return 0;
            }

            try
            {
                return Convert.ToInt32(value);
            }
            catch (Exception)
            {
                return 0;
            }
        }
    }
}
