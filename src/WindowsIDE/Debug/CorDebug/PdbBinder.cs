using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace WindowsIDE.Debug
{
    /// <summary>
    /// PDB の文書+行を IL オフセットへ。WinForms は参照しない。
    /// </summary>
    internal sealed class PdbBinder : IDisposable
    {
        private ISymUnmanagedReader reader;
        private IMetaDataImport import;
        private bool disposed;

        /// <summary>
        /// EXE をメタデータとして開き、隣の PDB を読む。ICorDebug コールバックでは呼ばない。
        /// </summary>
        /// <param name="exePath">モジュールのフルパス。</param>
        /// <returns>開けたらインスタンス。失敗は null。</returns>
        public static PdbBinder TryOpenFromDisk(string exePath)
        {
            string unused;
            return TryOpenFromDisk(exePath, out unused);
        }

        /// <summary>
        /// EXE をメタデータとして開き、隣の PDB を読む。失敗理由を out する。
        /// </summary>
        /// <param name="exePath">モジュールのフルパス。</param>
        /// <param name="error">失敗理由。成功時は null。</param>
        /// <returns>開けたらインスタンス。失敗は null。</returns>
        public static PdbBinder TryOpenFromDisk(string exePath, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
            {
                error = "exe missing";
                return null;
            }

            PdbBinder pdb = TryOpenWithDispenser(exePath, CorDebugNative.ClsidCorMetaDataDispenser, out error);
            if (pdb != null)
            {
                return pdb;
            }

            string first = error;
            pdb = TryOpenWithDispenser(exePath, CorDebugNative.ClsidCorMetaDataDispenserRuntime, out error);
            if (pdb != null)
            {
                return pdb;
            }

            if (string.IsNullOrEmpty(error))
            {
                error = first;
            }
            else if (!string.IsNullOrEmpty(first))
            {
                error = first + " / " + error;
            }

            return null;
        }

        private static PdbBinder TryOpenWithDispenser(string exePath, Guid clsid, out string error)
        {
            error = null;
            Guid clsidHost = CorDebugNative.ClsidClrMetaHost;
            Guid iidHost = CorDebugNative.IidIclrMetaHost;
            object hostObj;
            int hr = CorDebugNative.CLRCreateInstance(ref clsidHost, ref iidHost, out hostObj);
            if (!CorDebugNative.Succeeded(hr) || hostObj == null)
            {
                error = "CLRCreateInstance hr=" + hr.ToString("X8");
                return null;
            }

            ICLRMetaHost host = (ICLRMetaHost)hostObj;
            Guid iidRt = CorDebugNative.IidIclrRuntimeInfo;
            object rtObj;
            hr = host.GetRuntime("v4.0.30319", ref iidRt, out rtObj);
            if (!CorDebugNative.Succeeded(hr) || rtObj == null)
            {
                error = "GetRuntime hr=" + hr.ToString("X8");
                return null;
            }

            ICLRRuntimeInfo rt = (ICLRRuntimeInfo)rtObj;
            Guid iidDisp = CorDebugNative.IidIMetaDataDispenser;
            object dispObj;
            hr = rt.GetInterface(ref clsid, ref iidDisp, out dispObj);
            if (!CorDebugNative.Succeeded(hr) || dispObj == null)
            {
                error = "GetInterface dispenser " + clsid.ToString() + " hr=" + hr.ToString("X8");
                return null;
            }

            IMetaDataDispenser dispenser = null;
            try
            {
                dispenser = (IMetaDataDispenser)dispObj;
                Guid iidImport = CorDebugNative.IidIMetaDataImport;
                object importer;
                hr = dispenser.OpenScope(exePath, CorDebugNative.MetaDataOpenRead, ref iidImport, out importer);
                if (!CorDebugNative.Succeeded(hr) || importer == null)
                {
                    error = "OpenScope hr=" + hr.ToString("X8");
                    return null;
                }

                PdbBinder pdb = TryOpen(importer, exePath);
                if (pdb == null)
                {
                    error = "GetReaderForFile failed";
                    return null;
                }

                return pdb;
            }
            catch (Exception ex)
            {
                error = ex.GetType().Name + " " + ex.Message;
                return null;
            }
            finally
            {
                Release(dispenser);
            }
        }

        /// <summary>
        /// モジュールのメタデータと隣の PDB からリーダーを開く。
        /// </summary>
        /// <param name="importer">IMetaDataImport。</param>
        /// <param name="exePath">モジュールのフルパス。</param>
        /// <returns>開けたらインスタンス。失敗は null。</returns>
        public static PdbBinder TryOpen(object importer, string exePath)
        {
            if (importer == null || string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
            {
                return null;
            }

            string search = null;
            try
            {
                search = Path.GetDirectoryName(Path.GetFullPath(exePath));
            }
            catch (Exception)
            {
                search = Path.GetDirectoryName(exePath);
            }

            Guid clsid = CorDebugNative.ClsidCorSymBinderSxS;
            Guid iid = CorDebugNative.IidISymUnmanagedBinder;
            object binderObj = null;
            ISymUnmanagedBinder binder = null;
            ISymUnmanagedReader rdr = null;
            try
            {
                int hr = CorDebugNative.CoCreateInstance(
                    ref clsid,
                    IntPtr.Zero,
                    CorDebugNative.ClsctxInprocServer,
                    ref iid,
                    out binderObj);
                if (!CorDebugNative.Succeeded(hr) || binderObj == null)
                {
                    return null;
                }

                binder = (ISymUnmanagedBinder)binderObj;
                hr = binder.GetReaderForFile(importer, exePath, search, out rdr);
                if (!CorDebugNative.Succeeded(hr) || rdr == null)
                {
                    hr = binder.GetReaderForFile(importer, exePath, null, out rdr);
                }

                if (!CorDebugNative.Succeeded(hr) || rdr == null)
                {
                    string pdbPath = Path.ChangeExtension(exePath, ".pdb");
                    hr = binder.GetReaderForFile(importer, pdbPath, search, out rdr);
                }

                if (!CorDebugNative.Succeeded(hr) || rdr == null)
                {
                    return null;
                }

                PdbBinder pdb = new PdbBinder();
                pdb.reader = rdr;
                pdb.import = importer as IMetaDataImport;
                rdr = null;
                return pdb;
            }
            catch (Exception)
            {
                return null;
            }
            finally
            {
                Release(rdr);
                Release(binder);
            }
        }

        /// <summary>
        /// ソース行に対応するメソッドトークンと IL オフセット。
        /// </summary>
        public bool TryGetIlOffset(string sourcePath, int line, out uint methodToken, out uint ilOffset)
        {
            methodToken = 0;
            ilOffset = 0;
            if (this.reader == null || string.IsNullOrEmpty(sourcePath) || line < 1)
            {
                return false;
            }

            ISymUnmanagedDocument doc = this.FindDocument(sourcePath);
            if (doc == null)
            {
                return false;
            }

            try
            {
                uint closest = (uint)line;
                int hr = doc.FindClosestLine((uint)line, out closest);
                if (CorDebugNative.Succeeded(hr) && closest > 0)
                {
                    line = (int)closest;
                }

                ISymUnmanagedMethod method;
                hr = this.reader.GetMethodFromDocumentPosition(doc, (uint)line, 1, out method);
                if (!CorDebugNative.Succeeded(hr) || method == null)
                {
                    hr = this.reader.GetMethodFromDocumentPosition(doc, (uint)line, 0, out method);
                }

                if (!CorDebugNative.Succeeded(hr) || method == null)
                {
                    return false;
                }

                try
                {
                    hr = method.GetOffset(doc, (uint)line, 1, out ilOffset);
                    if (!CorDebugNative.Succeeded(hr) || ilOffset == CorDebugNative.HiddenSequencePoint)
                    {
                        hr = method.GetOffset(doc, (uint)line, 0, out ilOffset);
                    }

                    if (!CorDebugNative.Succeeded(hr) || ilOffset == CorDebugNative.HiddenSequencePoint)
                    {
                        if (!TryFindSequencePoint(method, sourcePath, line, out ilOffset))
                        {
                            return false;
                        }
                    }

                    hr = method.GetToken(out methodToken);
                    return CorDebugNative.Succeeded(hr) && methodToken != 0;
                }
                finally
                {
                    Release(method);
                }
            }
            finally
            {
                Release(doc);
            }
        }

        /// <summary>
        /// IL オフセットからソースパスと 1 始まり行。
        /// </summary>
        public bool TryGetSource(uint methodToken, uint ilOffset, out string path, out int line)
        {
            path = null;
            line = 0;
            if (this.reader == null || methodToken == 0)
            {
                return false;
            }

            ISymUnmanagedMethod method;
            int hr = this.reader.GetMethod(methodToken, out method);
            if (!CorDebugNative.Succeeded(hr) || method == null)
            {
                return false;
            }

            try
            {
                SequencePoint[] points = ReadSequencePoints(method);
                SequencePoint best = null;
                uint bestDelta = uint.MaxValue;
                int i = 0;
                while (i < points.Length)
                {
                    SequencePoint sp = points[i];
                    i++;
                    if (sp == null || sp.Line == CorDebugNative.HiddenSequencePoint)
                    {
                        continue;
                    }

                    if (sp.Offset <= ilOffset)
                    {
                        uint delta = ilOffset - sp.Offset;
                        if (delta <= bestDelta)
                        {
                            bestDelta = delta;
                            best = sp;
                        }
                    }
                }

                if (best == null)
                {
                    return false;
                }

                path = best.Url;
                line = (int)best.Line;
                return line > 0;
            }
            finally
            {
                Release(method);
            }
        }

        /// <summary>
        /// スコープ内ローカル（スロットと名前）。
        /// </summary>
        public PdbLocal[] GetLocals(uint methodToken, uint ilOffset)
        {
            if (this.reader == null || methodToken == 0)
            {
                return new PdbLocal[0];
            }

            ISymUnmanagedMethod method;
            int hr = this.reader.GetMethod(methodToken, out method);
            if (!CorDebugNative.Succeeded(hr) || method == null)
            {
                return new PdbLocal[0];
            }

            try
            {
                ISymUnmanagedScope root;
                hr = method.GetRootScope(out root);
                if (!CorDebugNative.Succeeded(hr) || root == null)
                {
                    return new PdbLocal[0];
                }

                try
                {
                    List<PdbLocal> list = new List<PdbLocal>();
                    this.CollectLocals(root, ilOffset, list);
                    return list.ToArray();
                }
                finally
                {
                    Release(root);
                }
            }
            finally
            {
                Release(method);
            }
        }

        /// <summary>
        /// メタデータから型.メソッド名。
        /// </summary>
        public string GetMethodName(uint methodToken)
        {
            if (this.import == null || methodToken == 0)
            {
                return "";
            }

            try
            {
                uint typeTok;
                uint pch;
                uint attr;
                IntPtr sig;
                uint sigLen;
                uint rva;
                uint impl;
                StringBuilder name = new StringBuilder(260);
                int hr = this.import.GetMethodProps(
                    methodToken,
                    out typeTok,
                    name,
                    260,
                    out pch,
                    out attr,
                    out sig,
                    out sigLen,
                    out rva,
                    out impl);
                if (!CorDebugNative.Succeeded(hr))
                {
                    return "";
                }

                string method = name.ToString();
                if (typeTok == 0)
                {
                    return method;
                }

                StringBuilder typeName = new StringBuilder(260);
                uint pchType;
                uint flags;
                uint extends;
                hr = this.import.GetTypeDefProps(typeTok, typeName, 260, out pchType, out flags, out extends);
                if (!CorDebugNative.Succeeded(hr) || typeName.Length == 0)
                {
                    return method;
                }

                return typeName.ToString() + "." + method;
            }
            catch (Exception)
            {
                return "";
            }
        }

        /// <summary>COM を解放する。</summary>
        public void Dispose()
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
            Release(this.reader);
            this.reader = null;
            this.import = null;
        }

        private ISymUnmanagedDocument FindDocument(string sourcePath)
        {
            ISymUnmanagedDocument doc;
            Guid empty = Guid.Empty;
            int hr = this.reader.GetDocument(sourcePath, empty, empty, empty, out doc);
            if (CorDebugNative.Succeeded(hr) && doc != null)
            {
                return doc;
            }

            string full = NormalizePath(sourcePath);
            if (!string.IsNullOrEmpty(full) && !string.Equals(full, sourcePath, StringComparison.OrdinalIgnoreCase))
            {
                hr = this.reader.GetDocument(full, empty, empty, empty, out doc);
                if (CorDebugNative.Succeeded(hr) && doc != null)
                {
                    return doc;
                }
            }

            string wantFile = Path.GetFileName(full);
            ISymUnmanagedDocument[] docs = new ISymUnmanagedDocument[32];
            uint got = 0;
            hr = this.reader.GetDocuments(32, out got, docs);
            if (!CorDebugNative.Succeeded(hr) && hr != CorDebugNative.HrFalse)
            {
                return null;
            }

            ISymUnmanagedDocument match = null;
            uint i = 0;
            while (i < got)
            {
                ISymUnmanagedDocument d = docs[i];
                i++;
                if (d == null)
                {
                    continue;
                }

                string url = ReadDocumentUrl(d);
                bool same = PathsEqual(url, full);
                if (!same && !string.IsNullOrEmpty(wantFile))
                {
                    same = string.Equals(Path.GetFileName(url), wantFile, StringComparison.OrdinalIgnoreCase);
                }

                if (match == null && same)
                {
                    match = d;
                    continue;
                }

                Release(d);
            }

            return match;
        }

        private void CollectLocals(ISymUnmanagedScope scope, uint ilOffset, List<PdbLocal> list)
        {
            if (scope == null)
            {
                return;
            }

            uint start;
            uint end;
            scope.GetStartOffset(out start);
            scope.GetEndOffset(out end);
            bool inside = ilOffset >= start && ilOffset < end;
            if (inside)
            {
                uint count = 0;
                scope.GetLocalCount(out count);
                if (count > 0)
                {
                    ISymUnmanagedVariable[] vars = new ISymUnmanagedVariable[count];
                    uint got = 0;
                    int hr = scope.GetLocals(count, out got, vars);
                    if (CorDebugNative.Succeeded(hr) || hr == CorDebugNative.HrFalse)
                    {
                        uint i = 0;
                        while (i < got)
                        {
                            ISymUnmanagedVariable v = vars[i];
                            i++;
                            if (v == null)
                            {
                                continue;
                            }

                            try
                            {
                                uint kind;
                                v.GetAddressKind(out kind);
                                if (kind != CorDebugNative.SymAddrIlOffset)
                                {
                                    continue;
                                }

                                uint slot;
                                v.GetAddressField1(out slot);
                                string name = ReadVariableName(v);
                                if (!string.IsNullOrEmpty(name))
                                {
                                    list.Add(new PdbLocal(name, slot));
                                }
                            }
                            finally
                            {
                                Release(v);
                            }
                        }
                    }
                }
            }

            uint childCount = 0;
            scope.GetChildren(0, out childCount, new ISymUnmanagedScope[0]);
            if (childCount == 0)
            {
                return;
            }

            ISymUnmanagedScope[] children = new ISymUnmanagedScope[childCount];
            uint gotChildren = 0;
            int chr = scope.GetChildren(childCount, out gotChildren, children);
            if (!CorDebugNative.Succeeded(chr) && chr != CorDebugNative.HrFalse)
            {
                return;
            }

            uint c = 0;
            while (c < gotChildren)
            {
                ISymUnmanagedScope child = children[c];
                c++;
                try
                {
                    this.CollectLocals(child, ilOffset, list);
                }
                finally
                {
                    Release(child);
                }
            }
        }

        private static bool TryFindSequencePoint(ISymUnmanagedMethod method, string sourcePath, int line, out uint ilOffset)
        {
            ilOffset = 0;
            SequencePoint[] points = ReadSequencePoints(method);
            SequencePoint exact = null;
            SequencePoint cover = null;
            int i = 0;
            while (i < points.Length)
            {
                SequencePoint sp = points[i];
                i++;
                if (sp == null || sp.Line == CorDebugNative.HiddenSequencePoint)
                {
                    continue;
                }

                if ((int)sp.Line == line)
                {
                    exact = sp;
                    break;
                }

                if (cover == null && (int)sp.Line <= line && (int)sp.EndLine >= line)
                {
                    cover = sp;
                }
            }

            SequencePoint use = (exact != null) ? exact : cover;
            if (use == null)
            {
                return false;
            }

            ilOffset = use.Offset;
            return true;
        }

        private static SequencePoint[] ReadSequencePoints(ISymUnmanagedMethod method)
        {
            uint count = 0;
            int hr = method.GetSequencePointCount(out count);
            if (!CorDebugNative.Succeeded(hr) || count == 0)
            {
                return new SequencePoint[0];
            }

            uint[] offsets = new uint[count];
            IntPtr[] docs = new IntPtr[count];
            uint[] lines = new uint[count];
            uint[] cols = new uint[count];
            uint[] endLines = new uint[count];
            uint[] endCols = new uint[count];
            uint got = 0;
            hr = method.GetSequencePoints(count, out got, offsets, docs, lines, cols, endLines, endCols);
            if (!CorDebugNative.Succeeded(hr) && hr != CorDebugNative.HrFalse)
            {
                return new SequencePoint[0];
            }

            SequencePoint[] result = new SequencePoint[got];
            uint i = 0;
            while (i < got)
            {
                SequencePoint sp = new SequencePoint();
                sp.Offset = offsets[i];
                sp.Line = lines[i];
                sp.EndLine = endLines[i];
                ISymUnmanagedDocument doc = null;
                if (docs[i] != IntPtr.Zero)
                {
                    try
                    {
                        doc = (ISymUnmanagedDocument)Marshal.GetObjectForIUnknown(docs[i]);
                    }
                    catch (Exception)
                    {
                    }
                }

                sp.Url = ReadDocumentUrl(doc);
                result[i] = sp;
                Release(doc);
                if (docs[i] != IntPtr.Zero)
                {
                    try
                    {
                        Marshal.Release(docs[i]);
                    }
                    catch (Exception)
                    {
                    }
                }

                i++;
            }

            return result;
        }

        /// <summary>
        /// 現在 IP が属するソース行の IL ステップ範囲（半開区間）。同一行の複数 SP をすべて含める。
        /// </summary>
        /// <param name="method">ISym メソッド。</param>
        /// <param name="ilOffset">現在の IL オフセット。</param>
        /// <param name="methodIlSize">メソッド IL サイズ。0 なら末尾 SP は +1。</param>
        /// <returns>範囲。無ければ空配列。</returns>
        internal static CorDebugStepRange[] BuildStepRanges(ISymUnmanagedMethod method, uint ilOffset, uint methodIlSize)
        {
            SequencePoint[] points = ReadSequencePoints(method);
            if (points == null || points.Length == 0)
            {
                return new CorDebugStepRange[0];
            }

            uint[] offsets = new uint[points.Length];
            uint[] lines = new uint[points.Length];
            int i = 0;
            while (i < points.Length)
            {
                SequencePoint sp = points[i];
                if (sp == null)
                {
                    offsets[i] = 0;
                    lines[i] = CorDebugNative.HiddenSequencePoint;
                }
                else
                {
                    offsets[i] = sp.Offset;
                    lines[i] = sp.Line;
                }

                i++;
            }

            return BuildStepRanges(offsets, lines, ilOffset, methodIlSize);
        }

        /// <summary>
        /// シーケンスポイント配列からソース行ステップ範囲を作る。テストからも呼ぶ。
        /// </summary>
        /// <param name="offsets">各 SP の IL オフセット。</param>
        /// <param name="lines">各 SP の 1 始まり行。隠しは HiddenSequencePoint。</param>
        /// <param name="ilOffset">現在の IL オフセット。</param>
        /// <param name="methodIlSize">メソッド IL サイズ。0 なら末尾は start+1。</param>
        /// <returns>半開区間 [start, end)。無ければ空。</returns>
        internal static CorDebugStepRange[] BuildStepRanges(uint[] offsets, uint[] lines, uint ilOffset, uint methodIlSize)
        {
            if (offsets == null || lines == null || offsets.Length == 0 || offsets.Length != lines.Length)
            {
                return new CorDebugStepRange[0];
            }

            int current = -1;
            int i = 0;
            while (i < offsets.Length)
            {
                if (lines[i] != CorDebugNative.HiddenSequencePoint && offsets[i] <= ilOffset)
                {
                    current = i;
                }

                i++;
            }

            if (current < 0)
            {
                return new CorDebugStepRange[0];
            }

            uint line = lines[current];
            List<CorDebugStepRange> list = new List<CorDebugStepRange>();
            i = 0;
            while (i < offsets.Length)
            {
                if (lines[i] == CorDebugNative.HiddenSequencePoint || lines[i] != line)
                {
                    i++;
                    continue;
                }

                uint start = offsets[i];
                int next = i + 1;
                while (next < offsets.Length && lines[next] == CorDebugNative.HiddenSequencePoint)
                {
                    next++;
                }

                uint end;
                if (next < offsets.Length)
                {
                    end = offsets[next];
                }
                else if (methodIlSize > start)
                {
                    end = methodIlSize;
                }
                else
                {
                    end = start + 1;
                }

                if (end > start)
                {
                    CorDebugStepRange range = new CorDebugStepRange();
                    range.startOffset = start;
                    range.endOffset = end;
                    list.Add(range);
                }

                i = next;
            }

            return list.ToArray();
        }

        internal ISymUnmanagedMethod TryGetMethod(uint token)
        {
            if (this.reader == null || token == 0)
            {
                return null;
            }

            ISymUnmanagedMethod method;
            int hr = this.reader.GetMethod(token, out method);
            if (!CorDebugNative.Succeeded(hr))
            {
                return null;
            }

            return method;
        }

        private static string ReadDocumentUrl(ISymUnmanagedDocument doc)
        {
            if (doc == null)
            {
                return "";
            }

            uint needed = 0;
            StringBuilder probe = new StringBuilder(1);
            int hr = doc.GetURL(1, out needed, probe);
            if (!CorDebugNative.Succeeded(hr) || needed == 0)
            {
                StringBuilder small = new StringBuilder(260);
                hr = doc.GetURL(260, out needed, small);
                if (!CorDebugNative.Succeeded(hr))
                {
                    return "";
                }

                return small.ToString();
            }

            StringBuilder sb = new StringBuilder((int)needed);
            hr = doc.GetURL(needed, out needed, sb);
            if (!CorDebugNative.Succeeded(hr))
            {
                return "";
            }

            return sb.ToString();
        }

        private static string ReadVariableName(ISymUnmanagedVariable v)
        {
            uint needed = 0;
            StringBuilder probe = new StringBuilder(1);
            int hr = v.GetName(1, out needed, probe);
            StringBuilder sb;
            if (!CorDebugNative.Succeeded(hr) || needed == 0)
            {
                sb = new StringBuilder(64);
                hr = v.GetName(64, out needed, sb);
            }
            else
            {
                sb = new StringBuilder((int)needed);
                hr = v.GetName(needed, out needed, sb);
            }

            if (!CorDebugNative.Succeeded(hr))
            {
                return "";
            }

            return sb.ToString();
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return "";
            }

            try
            {
                return Path.GetFullPath(path);
            }
            catch (Exception)
            {
                return path;
            }
        }

        private static bool PathsEqual(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
            {
                return false;
            }

            return string.Equals(NormalizePath(a), NormalizePath(b), StringComparison.OrdinalIgnoreCase);
        }

        private static void Release(object com)
        {
            if (com == null)
            {
                return;
            }

            try
            {
                if (Marshal.IsComObject(com))
                {
                    Marshal.ReleaseComObject(com);
                }
            }
            catch (Exception)
            {
            }
        }

        private sealed class SequencePoint
        {
            public uint Offset;
            public uint Line;
            public uint EndLine;
            public string Url;
        }
    }

    /// <summary>
    /// PDB ローカル 1 件。
    /// </summary>
    internal sealed class PdbLocal
    {
        private string name;
        private uint slot;

        public PdbLocal(string name, uint slot)
        {
            this.name = (name == null) ? "" : name;
            this.slot = slot;
        }

        public string Name
        {
            get { return this.name; }
        }

        public uint Slot
        {
            get { return this.slot; }
        }
    }
}
