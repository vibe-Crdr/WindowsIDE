using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using WindowsIDE.Build;
using WindowsIDE.Editor;
using WindowsIDE.Workspace;

namespace WindowsIDE.Vba
{
    /// <summary>
    /// VBA ディレクトリと Excel の明示プル／プッシュ。WinForms 非依存。Excel を終了させない。
    /// </summary>
    public static class VbaSyncService
    {
        private const string TrustMessage = "VBA プロジェクト オブジェクト モデルへのアクセスを信頼する を Excel のオプションで有効にしてください。";
        private const string PickWorkbookFirst = "先にブックを選ぶ";

        /// <summary>
        /// マップのブックを設定する。初回なら vba-map.xml を作る。壊れた XML は上書きしない。
        /// </summary>
        /// <param name="workspaceRoot">ワークスペースルート。</param>
        /// <param name="workbookPath">選んだブック。</param>
        /// <returns>結果。</returns>
        public static VbaSyncResult SetWorkbook(string workspaceRoot, string workbookPath)
        {
            if (string.IsNullOrEmpty(workspaceRoot))
            {
                return VbaSyncResult.MessageBox("ワークスペースを開いてください。", true);
            }

            if (!VbaWorkbookPath.IsMacroWorkbook(workbookPath))
            {
                return VbaSyncResult.MessageBox("マクロ有効ブック（.xlsm / .xlsb）ではありません。", true);
            }

            string stored = VbaMap.StoreWorkbookPath(workspaceRoot, workbookPath);
            VbaMap map;
            string loadError;
            VbaMapLoadStatus status = VbaMap.TryLoad(workspaceRoot, out map, out loadError);
            if (status == VbaMapLoadStatus.Broken)
            {
                return VbaSyncResult.MessageBox(loadError, true);
            }

            bool created = status == VbaMapLoadStatus.Missing;
            if (created)
            {
                map = VbaMap.CreateDefault(stored);
            }
            else
            {
                map.SetWorkbookPath(stored);
            }

            string saveError;
            if (!map.TrySave(workspaceRoot, out saveError))
            {
                return VbaSyncResult.MessageBox(saveError, true);
            }

            return created ? VbaSyncResult.OkCreatedMap() : VbaSyncResult.Ok();
        }

        /// <summary>
        /// ディスクとマップだけから namingMode 変更の旧名→新名を作る。Excel は触らない。
        /// </summary>
        /// <param name="workspaceRoot">ワークスペースルート。</param>
        /// <param name="newMode">新しいモード。</param>
        /// <param name="rows">プレビュー行。</param>
        /// <returns>失敗時は MessageBox 結果。成功時は Success。</returns>
        public static VbaSyncResult PreviewNaming(string workspaceRoot, VbaNamingMode newMode, out VbaRenamePreview[] rows)
        {
            rows = new VbaRenamePreview[0];
            VbaMap map;
            VbaSyncResult fail;
            if (!TryRequireMap(workspaceRoot, false, out map, out fail))
            {
                return fail;
            }

            string vbaRoot = map.ResolveRootFullPath(workspaceRoot);
            List<VbaRenamePreview> list = new List<VbaRenamePreview>();
            if (!string.IsNullOrEmpty(vbaRoot))
            {
                string[] files = VbaDiskTree.List(vbaRoot);
                for (int i = 0; i < files.Length; i++)
                {
                    string rel = VbaDiskTree.ToRelPath(vbaRoot, files[i]);
                    if (string.IsNullOrEmpty(rel))
                    {
                        continue;
                    }

                    VbaMapComponent mapped = map.FindByRelPath(rel);
                    if (mapped != null && mapped.Kind == VbaComponentKind.Document)
                    {
                        list.Add(new VbaRenamePreview(mapped.Name, mapped.Name, true));
                        continue;
                    }

                    string oldName;
                    string newName;
                    if (mapped != null)
                    {
                        oldName = mapped.Name;
                        newName = VbaNaming.FromRelPath(rel, newMode);
                    }
                    else
                    {
                        oldName = VbaNaming.FromRelPath(rel, map.NamingMode);
                        newName = VbaNaming.FromRelPath(rel, newMode);
                    }

                    list.Add(new VbaRenamePreview(oldName, newName, false));
                }
            }

            rows = list.ToArray();
            return VbaSyncResult.Ok();
        }

        /// <summary>
        /// マップの namingMode だけ更新する。Excel はリネームしない。
        /// </summary>
        /// <param name="workspaceRoot">ワークスペースルート。</param>
        /// <param name="mode">新しいモード。</param>
        /// <returns>結果。</returns>
        public static VbaSyncResult SetNamingMode(string workspaceRoot, VbaNamingMode mode)
        {
            VbaMap map;
            VbaSyncResult fail;
            if (!TryRequireMap(workspaceRoot, false, out map, out fail))
            {
                return fail;
            }

            map.SetNamingMode(mode);
            string error;
            if (!map.TrySave(workspaceRoot, out error))
            {
                return VbaSyncResult.MessageBox(error, true);
            }

            return VbaSyncResult.Ok();
        }

        /// <summary>
        /// Excel を正としてディスクへ書き、マップを更新する。Workbook.Save は確認時だけ。Excel を終了させない。
        /// </summary>
        /// <param name="workspaceRoot">ワークスペースルート。</param>
        /// <param name="confirm">未保存確認。</param>
        /// <returns>結果。</returns>
        public static VbaSyncResult Pull(string workspaceRoot, VbaSyncConfirm confirm)
        {
            VbaMap map;
            VbaSyncResult fail;
            if (!TryRequireMap(workspaceRoot, true, out map, out fail))
            {
                return fail;
            }

            string vbaRoot = map.ResolveRootFullPath(workspaceRoot);
            if (string.IsNullOrEmpty(vbaRoot))
            {
                return VbaSyncResult.MessageBox("VBA ルートがワークスペース内ではありません。", true);
            }

            string workbook = map.ResolveWorkbookFullPath(workspaceRoot);
            ExcelSession session = null;
            string tempDir = null;
            try
            {
                VbaSyncResult connect = ConnectExcel(workbook, out session);
                if (connect != null)
                {
                    return connect;
                }

                List<ExcelComp> comps = EnumerateComponents(session.Components);
                List<string> dests = new List<string>();
                List<PullPlan> plans = new List<PullPlan>();
                List<Diagnostic> problems = new List<Diagnostic>();
                for (int i = 0; i < comps.Count; i++)
                {
                    ExcelComp c = comps[i];
                    VbaComponentKind kind = KindFromType(c.Type);
                    string ext = VbaDiskTree.ExtensionFor(kind);
                    VbaMapComponent existing = map.FindByName(c.Name);
                    string rel;
                    if (existing != null && !string.IsNullOrEmpty(existing.RelPath))
                    {
                        string existingFull;
                        string planErr;
                        if (VbaDiskTree.TryPlanWrite(workspaceRoot, map.RootRelative, existing.RelPath, out existingFull, out planErr)
                            && File.Exists(existingFull))
                        {
                            rel = existing.RelPath;
                        }
                        else
                        {
                            rel = c.Name + ext;
                        }
                    }
                    else
                    {
                        rel = c.Name + ext;
                    }

                    string dest;
                    string destErr;
                    if (!VbaDiskTree.TryPlanWrite(workspaceRoot, map.RootRelative, rel, out dest, out destErr))
                    {
                        problems.Add(Diagnostic.CreateSynthetic(c.Name, destErr));
                        continue;
                    }

                    dests.Add(dest);
                    PullPlan plan = new PullPlan();
                    plan.Comp = c;
                    plan.Kind = kind;
                    plan.RelPath = rel;
                    plan.DestPath = dest;
                    plans.Add(plan);
                }

                if (problems.Count > 0)
                {
                    return VbaSyncResult.Problems(problems.ToArray());
                }

                if (confirm != null && !confirm.ConfirmDirty(dests.ToArray()))
                {
                    return VbaSyncResult.Cancel();
                }

                VbaSyncResult saved = ConfirmAndSaveWorkbook(session, confirm);
                if (saved != null)
                {
                    return saved;
                }

                tempDir = CreateTempDir();
                BeginScreenUpdatingOff(session);
                List<string> written = new List<string>();
                List<VbaMapComponent> next = new List<VbaMapComponent>();
                for (int i = 0; i < plans.Count; i++)
                {
                    PullPlan plan = plans[i];
                    string tempFile = Path.Combine(tempDir, plan.Comp.Name + VbaDiskTree.ExtensionFor(plan.Kind));
                    try
                    {
                        ComInvoker.Call(plan.Comp.Component, "Export", tempFile);
                        byte[] raw = File.ReadAllBytes(tempFile);
                        string text;
                        FileEncodingInfo decoded;
                        string decErr;
                        if (!FileEncoding.TryDecode(raw, out text, out decoded, out decErr))
                        {
                            problems.Add(Diagnostic.CreateSynthetic(plan.DestPath, decErr));
                            continue;
                        }

                        try
                        {
                            FileEncoding.GetBytesToSave(text, map.GetEncodingInfo(), plan.DestPath);
                        }
                        catch (EncoderFallbackException)
                        {
                            problems.Add(Diagnostic.CreateSynthetic(plan.DestPath, "CP932 で表せない文字があります。"));
                            continue;
                        }

                        plan.Text = text;
                        next.Add(new VbaMapComponent(plan.Comp.Name, plan.RelPath, plan.Kind));
                    }
                    catch (Exception ex)
                    {
                        problems.Add(Diagnostic.CreateSynthetic(plan.DestPath, ex.Message));
                    }
                }

                if (problems.Count > 0)
                {
                    return VbaSyncResult.Problems(problems.ToArray());
                }

                for (int i = 0; i < plans.Count; i++)
                {
                    PullPlan plan = plans[i];
                    string writeErr;
                    if (!TryWriteEncoded(plan.DestPath, plan.Text, map.GetEncodingInfo(), out writeErr))
                    {
                        problems.Add(Diagnostic.CreateSynthetic(plan.DestPath, writeErr));
                        continue;
                    }

                    written.Add(plan.DestPath);
                }

                if (problems.Count > 0)
                {
                    return VbaSyncResult.Problems(problems.ToArray());
                }

                map.SetComponents(next.ToArray());
                string saveMapErr;
                if (!map.TrySave(workspaceRoot, out saveMapErr))
                {
                    return VbaSyncResult.MessageBox(saveMapErr, true);
                }

                return VbaSyncResult.OkSync(written.ToArray(), new string[0]);
            }
            catch (COMException ex)
            {
                return VbaSyncResult.MessageBox(ex.Message, true);
            }
            catch (InvalidOperationException ex)
            {
                return VbaSyncResult.MessageBox(ex.Message, true);
            }
            catch (Exception ex)
            {
                return VbaSyncResult.MessageBox(ex.Message, true);
            }
            finally
            {
                RestoreScreenUpdating(session);
                DeleteTempBestEffort(tempDir);
            }
        }

        /// <summary>
        /// ディスクを Excel へ送る。部分適用しない。プッシュ後に Workbook.Save しない。Excel を終了させない。
        /// </summary>
        /// <param name="workspaceRoot">ワークスペースルート。</param>
        /// <param name="confirm">未保存確認。</param>
        /// <returns>結果。</returns>
        public static VbaSyncResult Push(string workspaceRoot, VbaSyncConfirm confirm)
        {
            return PushCore(workspaceRoot, confirm, false);
        }

        /// <summary>
        /// プッシュ成功と同じセッション解放前に Compile する。失敗・Cancel・問題一覧置換なら Push の結果のまま。
        /// </summary>
        /// <param name="workspaceRoot">ワークスペースルート。</param>
        /// <param name="confirm">未保存確認。</param>
        /// <returns>結果。</returns>
        public static VbaSyncResult PushThenCompile(string workspaceRoot, VbaSyncConfirm confirm)
        {
            return PushCore(workspaceRoot, confirm, true);
        }

        private static VbaSyncResult PushCore(string workspaceRoot, VbaSyncConfirm confirm, bool compileAfter)
        {
            VbaMap map;
            VbaSyncResult fail;
            if (!TryRequireMap(workspaceRoot, true, out map, out fail))
            {
                return fail;
            }

            string vbaRoot = map.ResolveRootFullPath(workspaceRoot);
            if (string.IsNullOrEmpty(vbaRoot))
            {
                return VbaSyncResult.MessageBox("VBA ルートがワークスペース内ではありません。", true);
            }

            string[] files = VbaDiskTree.List(vbaRoot);
            List<PushItem> items = new List<PushItem>();
            List<string> names = new List<string>();
            List<string> paths = new List<string>();
            List<Diagnostic> problems = new List<Diagnostic>();
            Dictionary<string, bool> matchedRel = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < files.Length; i++)
            {
                string rel = VbaDiskTree.ToRelPath(vbaRoot, files[i]);
                if (string.IsNullOrEmpty(rel))
                {
                    problems.Add(Diagnostic.CreateSynthetic(files[i], "相対パスを取れません。"));
                    continue;
                }

                VbaMapComponent mapped = map.FindByRelPath(rel);
                PushItem item = new PushItem();
                item.FullPath = files[i];
                item.RelPath = rel;
                if (mapped != null)
                {
                    item.Kind = mapped.Kind;
                    item.OldExcelName = mapped.Name;
                    item.ExcelName = VbaNaming.ToExcelName(rel, map.NamingMode, mapped.Kind, mapped.Name);
                    item.IsNew = false;
                    matchedRel[rel] = true;
                }
                else
                {
                    item.Kind = VbaDiskTree.KindFromPath(files[i]);
                    item.OldExcelName = null;
                    item.ExcelName = VbaNaming.FromRelPath(rel, map.NamingMode);
                    item.IsNew = true;
                }

                if (item.Kind != VbaComponentKind.Document && !VbaIdentifier.IsValid(item.ExcelName))
                {
                    string msg = (item.ExcelName != null && item.ExcelName.Length > 31)
                        ? "VBA 識別子が 31 文字を超えています。"
                        : "VBA 識別子として不正です: " + item.ExcelName;
                    problems.Add(Diagnostic.CreateSynthetic(item.FullPath, msg));
                }

                items.Add(item);
                names.Add(item.ExcelName);
                paths.Add(item.FullPath);

                string encErr;
                if (!TryEncodeForSync(item.FullPath, map.GetEncodingInfo(), out encErr))
                {
                    problems.Add(Diagnostic.CreateSynthetic(item.FullPath, encErr));
                }
            }

            VbaNameCollision[] collisions = VbaNaming.FindCollisions(names.ToArray(), paths.ToArray());
            for (int i = 0; i < collisions.Length; i++)
            {
                VbaNameCollision c = collisions[i];
                string msg = "Excel 名が衝突します: " + c.ExcelName;
                problems.Add(Diagnostic.CreateSynthetic(c.PathA, msg));
                problems.Add(Diagnostic.CreateSynthetic(c.PathB, msg));
            }

            if (problems.Count > 0)
            {
                return VbaSyncResult.Problems(problems.ToArray());
            }

            List<string> dirtyPaths = new List<string>();
            for (int i = 0; i < items.Count; i++)
            {
                dirtyPaths.Add(items[i].FullPath);
            }

            if (confirm != null && !confirm.ConfirmDirty(dirtyPaths.ToArray()))
            {
                return VbaSyncResult.Cancel();
            }

            string workbook = map.ResolveWorkbookFullPath(workspaceRoot);
            ExcelSession session = null;
            string tempDir = null;
            List<PushApplied> applied = new List<PushApplied>();
            try
            {
                VbaSyncResult connect = ConnectExcel(workbook, out session);
                if (connect != null)
                {
                    return connect;
                }

                List<ExcelComp> excelComps = EnumerateComponents(session.Components);
                Dictionary<string, ExcelComp> excelByName = new Dictionary<string, ExcelComp>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < excelComps.Count; i++)
                {
                    if (!excelByName.ContainsKey(excelComps[i].Name))
                    {
                        excelByName.Add(excelComps[i].Name, excelComps[i]);
                    }
                }

                for (int i = 0; i < items.Count; i++)
                {
                    PushItem item = items[i];
                    if (!item.IsNew)
                    {
                        continue;
                    }

                    if (excelByName.ContainsKey(item.ExcelName))
                    {
                        problems.Add(Diagnostic.CreateSynthetic(item.FullPath, "Excel に同名のモジュールがある（マップが切れているため紐付けない）: " + item.ExcelName));
                    }
                }

                if (problems.Count > 0)
                {
                    return VbaSyncResult.Problems(problems.ToArray());
                }

                VbaSyncResult saved = ConfirmAndSaveWorkbook(session, confirm);
                if (saved != null)
                {
                    return saved;
                }

                tempDir = CreateTempDir();
                BeginScreenUpdatingOff(session);
                List<VbaMapComponent> next = new List<VbaMapComponent>();
                Dictionary<string, bool> pushedNames = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

                for (int i = 0; i < items.Count; i++)
                {
                    PushItem item = items[i];
                    string text;
                    string readErr;
                    if (!TryReadText(item.FullPath, out text, out readErr))
                    {
                        problems.Add(Diagnostic.CreateSynthetic(item.FullPath, readErr));
                        return FailPushApply(problems, session.Components, applied);
                    }

                    try
                    {
                        if (item.Kind == VbaComponentKind.Document)
                        {
                            ExcelComp docComp;
                            if (!excelByName.TryGetValue(item.OldExcelName, out docComp))
                            {
                                problems.Add(Diagnostic.CreateSynthetic(item.FullPath, "document モジュールが見つかりません: " + item.OldExcelName));
                                return FailPushApply(problems, session.Components, applied);
                            }

                            string snapshot;
                            string replaceErr;
                            if (!TryReplaceCodeModule(docComp.Component, text, out snapshot, out replaceErr))
                            {
                                problems.Add(Diagnostic.CreateSynthetic(item.FullPath, replaceErr));
                                return FailPushApply(problems, session.Components, applied);
                            }

                            RecordReplace(applied, docComp.Component, snapshot, null);
                        }
                        else if (!item.IsNew)
                        {
                            ExcelComp existing;
                            if (excelByName.TryGetValue(item.OldExcelName, out existing))
                            {
                                string snapshot;
                                string replaceErr;
                                if (!TryReplaceCodeModule(existing.Component, text, out snapshot, out replaceErr))
                                {
                                    problems.Add(Diagnostic.CreateSynthetic(item.FullPath, replaceErr));
                                    return FailPushApply(problems, session.Components, applied);
                                }

                                bool renamed = !string.Equals(item.ExcelName, item.OldExcelName, StringComparison.OrdinalIgnoreCase);
                                if (renamed)
                                {
                                    try
                                    {
                                        ComInvoker.SetProperty(existing.Component, "Name", item.ExcelName);
                                    }
                                    catch (Exception ex)
                                    {
                                        RestoreCodeModuleBestEffort(existing.Component, snapshot);
                                        RestoreNameBestEffort(existing.Component, item.OldExcelName);
                                        problems.Add(Diagnostic.CreateSynthetic(item.FullPath, ex.Message));
                                        return FailPushApply(problems, session.Components, applied);
                                    }
                                }

                                RecordReplace(applied, existing.Component, snapshot, renamed ? item.OldExcelName : null);
                            }
                            else
                            {
                                if (!TryApplyImportNew(session.Components, tempDir, item, text, map.GetEncodingInfo(), applied, problems))
                                {
                                    return FailPushApply(problems, session.Components, applied);
                                }
                            }
                        }
                        else
                        {
                            if (!TryApplyImportNew(session.Components, tempDir, item, text, map.GetEncodingInfo(), applied, problems))
                            {
                                return FailPushApply(problems, session.Components, applied);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        problems.Add(Diagnostic.CreateSynthetic(item.FullPath, ex.Message));
                        return FailPushApply(problems, session.Components, applied);
                    }

                    next.Add(new VbaMapComponent(item.ExcelName, item.RelPath, item.Kind));
                    pushedNames[item.ExcelName] = true;
                }

                if (problems.Count > 0)
                {
                    return FailPushApply(problems, session.Components, applied);
                }

                List<string> excelOnly = new List<string>();
                for (int i = 0; i < excelComps.Count; i++)
                {
                    if (!pushedNames.ContainsKey(excelComps[i].Name))
                    {
                        excelOnly.Add(excelComps[i].Name);
                    }
                }

                for (int i = 0; i < map.Components.Length; i++)
                {
                    VbaMapComponent leftover = map.Components[i];
                    if (matchedRel.ContainsKey(leftover.RelPath))
                    {
                        continue;
                    }

                    bool already = false;
                    for (int j = 0; j < next.Count; j++)
                    {
                        if (string.Equals(next[j].Name, leftover.Name, StringComparison.OrdinalIgnoreCase))
                        {
                            already = true;
                            break;
                        }
                    }

                    if (!already)
                    {
                        next.Add(leftover);
                    }
                }

                map.SetComponents(next.ToArray());
                string saveMapErr;
                if (!map.TrySave(workspaceRoot, out saveMapErr))
                {
                    RollbackPush(session.Components, applied);
                    return VbaSyncResult.MessageBox(saveMapErr, true);
                }

                if (compileAfter)
                {
                    Diagnostic[] compileDiags = VbaCompiler.Compile(session.App, map, workspaceRoot);
                    return VbaSyncResult.OkPushThenCompile(new string[0], excelOnly.ToArray(), compileDiags);
                }

                return VbaSyncResult.OkSync(new string[0], excelOnly.ToArray());
            }
            catch (COMException ex)
            {
                RollbackPush(session == null ? null : session.Components, applied);
                return VbaSyncResult.MessageBox(ex.Message, true);
            }
            catch (InvalidOperationException ex)
            {
                RollbackPush(session == null ? null : session.Components, applied);
                return VbaSyncResult.MessageBox(ex.Message, true);
            }
            catch (Exception ex)
            {
                RollbackPush(session == null ? null : session.Components, applied);
                return VbaSyncResult.MessageBox(ex.Message, true);
            }
            finally
            {
                RestoreScreenUpdating(session);
                DeleteTempBestEffort(tempDir);
            }
        }

        private static bool TryRequireMap(string workspaceRoot, bool needWorkbook, out VbaMap map, out VbaSyncResult fail)
        {
            map = null;
            fail = null;
            if (string.IsNullOrEmpty(workspaceRoot))
            {
                fail = VbaSyncResult.MessageBox("ワークスペースを開いてください。", true);
                return false;
            }

            string error;
            VbaMapLoadStatus status = VbaMap.TryLoad(workspaceRoot, out map, out error);
            if (status == VbaMapLoadStatus.Broken)
            {
                fail = VbaSyncResult.MessageBox(error, true);
                return false;
            }

            if (status == VbaMapLoadStatus.Missing)
            {
                fail = VbaSyncResult.MessageBox(PickWorkbookFirst, true);
                return false;
            }

            if (needWorkbook)
            {
                string wb = map.ResolveWorkbookFullPath(workspaceRoot);
                if (string.IsNullOrEmpty(wb))
                {
                    fail = VbaSyncResult.MessageBox(PickWorkbookFirst, true);
                    return false;
                }

                if (!VbaWorkbookPath.IsMacroWorkbook(wb))
                {
                    fail = VbaSyncResult.MessageBox("マクロ有効ブック（.xlsm / .xlsb）ではありません。", true);
                    return false;
                }
            }

            return true;
        }

        private static VbaSyncResult ConnectExcel(string workbookPath, out ExcelSession session)
        {
            session = new ExcelSession();
            string excelError;
            session.App = AcquireExcel(out excelError);
            if (session.App == null)
            {
                return VbaSyncResult.MessageBox(excelError, true);
            }

            object workbooks = ComInvoker.GetProperty(session.App, "Workbooks");
            session.Workbook = FindWorkbook(workbooks, workbookPath);
            if (session.Workbook == null)
            {
                session.Workbook = ComInvoker.Call(workbooks, "Open", workbookPath);
            }

            if (session.Workbook == null)
            {
                return VbaSyncResult.MessageBox("ブックを開けません。", true);
            }

            try
            {
                session.VbProject = ComInvoker.GetProperty(session.Workbook, "VBProject");
            }
            catch (COMException)
            {
                return VbaSyncResult.MessageBox(TrustMessage, true);
            }
            catch (Exception)
            {
                return VbaSyncResult.MessageBox(TrustMessage, true);
            }

            if (session.VbProject == null)
            {
                return VbaSyncResult.MessageBox(TrustMessage, true);
            }

            session.Components = ComInvoker.GetProperty(session.VbProject, "VBComponents");
            return null;
        }

        private static object AcquireExcel(out string error)
        {
            error = null;
            try
            {
                return Marshal.GetActiveObject("Excel.Application");
            }
            catch (COMException)
            {
            }
            catch (InvalidComObjectException)
            {
            }

            Type t = Type.GetTypeFromProgID("Excel.Application");
            if (t == null)
            {
                error = "Excel が見つかりません。";
                return null;
            }

            try
            {
                object app = Activator.CreateInstance(t);
                ComInvoker.SetProperty(app, "Visible", true);
                return app;
            }
            catch (Exception ex)
            {
                error = "Excel を起動できません: " + ex.Message;
                return null;
            }
        }

        private static object FindWorkbook(object workbooks, string fullPath)
        {
            int count = Convert.ToInt32(ComInvoker.GetProperty(workbooks, "Count"));
            string want = Path.GetFullPath(fullPath);
            for (int i = 1; i <= count; i++)
            {
                object wb = ComInvoker.GetProperty(workbooks, "Item", new object[] { i });
                object fullObj = ComInvoker.GetProperty(wb, "FullName");
                string name = (fullObj == null) ? "" : Convert.ToString(fullObj);
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                try
                {
                    if (string.Equals(Path.GetFullPath(name), want, StringComparison.OrdinalIgnoreCase))
                    {
                        return wb;
                    }
                }
                catch (Exception)
                {
                }
            }

            return null;
        }

        private static VbaSyncResult ConfirmAndSaveWorkbook(ExcelSession session, VbaSyncConfirm confirm)
        {
            bool saved = true;
            try
            {
                object v = ComInvoker.GetProperty(session.Workbook, "Saved");
                if (v is bool)
                {
                    saved = (bool)v;
                }
                else if (v != null)
                {
                    saved = Convert.ToBoolean(v);
                }
            }
            catch (Exception)
            {
                saved = true;
            }

            if (saved)
            {
                return null;
            }

            if (confirm == null || !confirm.ConfirmExcelUnsaved())
            {
                return VbaSyncResult.Cancel();
            }

            object oldAlerts = null;
            bool haveAlerts = false;
            try
            {
                oldAlerts = ComInvoker.GetProperty(session.App, "DisplayAlerts");
                haveAlerts = true;
                ComInvoker.SetProperty(session.App, "DisplayAlerts", false);
                ComInvoker.Call(session.Workbook, "Save");
            }
            catch (Exception ex)
            {
                return VbaSyncResult.MessageBox(ex.Message, true);
            }
            finally
            {
                if (haveAlerts)
                {
                    try
                    {
                        ComInvoker.SetProperty(session.App, "DisplayAlerts", oldAlerts);
                    }
                    catch (Exception)
                    {
                    }
                }
            }

            return null;
        }

        private static void BeginScreenUpdatingOff(ExcelSession session)
        {
            if (session == null || session.App == null)
            {
                return;
            }

            try
            {
                session.OldScreenUpdating = ComInvoker.GetProperty(session.App, "ScreenUpdating");
                session.HaveScreenUpdating = true;
                ComInvoker.SetProperty(session.App, "ScreenUpdating", false);
            }
            catch (Exception)
            {
                session.HaveScreenUpdating = false;
            }
        }

        private static void RestoreScreenUpdating(ExcelSession session)
        {
            if (session == null || session.App == null || !session.HaveScreenUpdating)
            {
                return;
            }

            try
            {
                ComInvoker.SetProperty(session.App, "ScreenUpdating", session.OldScreenUpdating);
            }
            catch (Exception)
            {
            }
        }

        private static List<ExcelComp> EnumerateComponents(object components)
        {
            List<ExcelComp> list = new List<ExcelComp>();
            int count = Convert.ToInt32(ComInvoker.GetProperty(components, "Count"));
            for (int i = 1; i <= count; i++)
            {
                object comp = ComInvoker.GetProperty(components, "Item", new object[] { i });
                int type = Convert.ToInt32(ComInvoker.GetProperty(comp, "Type"));
                if (type != 1 && type != 2 && type != 100)
                {
                    continue;
                }

                ExcelComp item = new ExcelComp();
                item.Component = comp;
                item.Type = type;
                item.Name = Convert.ToString(ComInvoker.GetProperty(comp, "Name"));
                list.Add(item);
            }

            return list;
        }

        private static VbaComponentKind KindFromType(int type)
        {
            if (type == 2)
            {
                return VbaComponentKind.Class;
            }

            if (type == 100)
            {
                return VbaComponentKind.Document;
            }

            return VbaComponentKind.Std;
        }

        private static VbaSyncResult FailPushApply(List<Diagnostic> problems, object components, List<PushApplied> applied)
        {
            RollbackPush(components, applied);
            return VbaSyncResult.Problems(problems.ToArray());
        }

        private static void RecordReplace(List<PushApplied> applied, object component, string snapshot, string oldName)
        {
            PushApplied rec = new PushApplied();
            rec.Component = component;
            rec.Snapshot = snapshot;
            rec.OldName = oldName;
            rec.Imported = false;
            applied.Add(rec);
        }

        private static bool TryApplyImportNew(object components, string tempDir, PushItem item, string text, FileEncodingInfo encoding, List<PushApplied> applied, List<Diagnostic> problems)
        {
            object imported;
            string importErr;
            if (!TryImportNew(components, tempDir, item, text, encoding, out imported, out importErr))
            {
                if (imported != null)
                {
                    PushApplied rec = new PushApplied();
                    rec.Component = imported;
                    rec.Imported = true;
                    applied.Add(rec);
                }

                problems.Add(Diagnostic.CreateSynthetic(item.FullPath, importErr));
                return false;
            }

            PushApplied ok = new PushApplied();
            ok.Component = imported;
            ok.Imported = true;
            applied.Add(ok);
            return true;
        }

        private static void RollbackPush(object components, List<PushApplied> applied)
        {
            if (applied == null)
            {
                return;
            }

            for (int i = applied.Count - 1; i >= 0; i--)
            {
                PushApplied one = applied[i];
                try
                {
                    if (one.Imported)
                    {
                        if (components != null && one.Component != null)
                        {
                            ComInvoker.Call(components, "Remove", one.Component);
                        }
                    }
                    else
                    {
                        if (one.Component != null && !string.IsNullOrEmpty(one.OldName))
                        {
                            ComInvoker.SetProperty(one.Component, "Name", one.OldName);
                        }

                        if (one.Component != null && one.Snapshot != null)
                        {
                            RestoreCodeModule(one.Component, one.Snapshot);
                        }
                    }
                }
                catch (Exception)
                {
                }
            }
        }

        private static void RestoreNameBestEffort(object comp, string oldName)
        {
            try
            {
                if (comp != null && !string.IsNullOrEmpty(oldName))
                {
                    ComInvoker.SetProperty(comp, "Name", oldName);
                }
            }
            catch (Exception)
            {
            }
        }

        private static void RestoreCodeModuleBestEffort(object comp, string snapshot)
        {
            try
            {
                RestoreCodeModule(comp, snapshot);
            }
            catch (Exception)
            {
            }
        }

        private static void RestoreCodeModule(object comp, string snapshot)
        {
            object code = ComInvoker.GetProperty(comp, "CodeModule");
            DeleteAllLines(code);
            ComInvoker.Call(code, "AddFromString", snapshot == null ? "" : snapshot);
        }

        private static bool TryReplaceCodeModule(object comp, string text, out string snapshot, out string error)
        {
            snapshot = "";
            error = null;
            object code;
            try
            {
                code = ComInvoker.GetProperty(comp, "CodeModule");
                int count = Convert.ToInt32(ComInvoker.GetProperty(code, "CountOfLines"));
                if (count > 0)
                {
                    object lines = ComInvoker.GetProperty(code, "Lines", new object[] { 1, count });
                    snapshot = Convert.ToString(lines);
                    if (snapshot == null)
                    {
                        snapshot = "";
                    }
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }

            string stripped = VbaExportText.StripForCodeModule(text);
            try
            {
                DeleteAllLines(code);
                ComInvoker.Call(code, "AddFromString", stripped);
                return true;
            }
            catch (Exception ex)
            {
                try
                {
                    DeleteAllLines(code);
                    ComInvoker.Call(code, "AddFromString", snapshot);
                }
                catch (Exception)
                {
                }

                error = ex.Message;
                return false;
            }
        }

        private static void DeleteAllLines(object codeModule)
        {
            int lines = Convert.ToInt32(ComInvoker.GetProperty(codeModule, "CountOfLines"));
            if (lines > 0)
            {
                ComInvoker.Call(codeModule, "DeleteLines", 1, lines);
            }
        }

        private static bool TryImportNew(object components, string tempDir, PushItem item, string text, FileEncodingInfo encoding, out object imported, out string error)
        {
            imported = null;
            error = null;
            string ext = VbaDiskTree.ExtensionFor(item.Kind);
            string tempFile = Path.Combine(tempDir, item.ExcelName + ext);
            if (!TryWriteEncoded(tempFile, text, encoding, out error))
            {
                return false;
            }

            ComInvoker.Call(components, "Import", tempFile);
            try
            {
                imported = ComInvoker.GetProperty(components, "Item", new object[] { item.ExcelName });
                string current = Convert.ToString(ComInvoker.GetProperty(imported, "Name"));
                if (!string.Equals(current, item.ExcelName, StringComparison.OrdinalIgnoreCase))
                {
                    ComInvoker.SetProperty(imported, "Name", item.ExcelName);
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool TryReadText(string path, out string text, out string error)
        {
            text = null;
            error = null;
            try
            {
                byte[] data = File.ReadAllBytes(path);
                FileEncodingInfo info;
                return FileEncoding.TryDecode(data, out text, out info, out error);
            }
            catch (IOException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool TryEncodeForSync(string path, FileEncodingInfo encoding, out string error)
        {
            string text;
            if (!TryReadText(path, out text, out error))
            {
                return false;
            }

            try
            {
                FileEncoding.GetBytesToSave(text, encoding, path);
                return true;
            }
            catch (EncoderFallbackException)
            {
                error = "CP932 で表せない文字があります。";
                return false;
            }
        }

        private static bool TryWriteEncoded(string path, string text, FileEncodingInfo encoding, out string error)
        {
            error = null;
            try
            {
                byte[] bytes = FileEncoding.GetBytesToSave(text, encoding, path);
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.WriteAllBytes(path, bytes);
                return true;
            }
            catch (EncoderFallbackException)
            {
                error = "CP932 で表せない文字があります。";
                return false;
            }
            catch (IOException ex)
            {
                error = ex.Message;
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static string CreateTempDir()
        {
            string dir = Path.Combine(Path.GetTempPath(), "WindowsIDE", "vba", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static void DeleteTempBestEffort(string dir)
        {
            if (string.IsNullOrEmpty(dir))
            {
                return;
            }

            try
            {
                Directory.Delete(dir, true);
            }
            catch (Exception)
            {
            }
        }

        private sealed class ExcelSession
        {
            public object App;
            public object Workbook;
            public object VbProject;
            public object Components;
            public object OldScreenUpdating;
            public bool HaveScreenUpdating;
        }

        private sealed class ExcelComp
        {
            public object Component;
            public string Name;
            public int Type;
        }

        private sealed class PullPlan
        {
            public ExcelComp Comp;
            public VbaComponentKind Kind;
            public string RelPath;
            public string DestPath;
            public string Text;
        }

        private sealed class PushItem
        {
            public string FullPath;
            public string RelPath;
            public string ExcelName;
            public string OldExcelName;
            public VbaComponentKind Kind;
            public bool IsNew;
        }

        private sealed class PushApplied
        {
            public object Component;
            public string Snapshot;
            public string OldName;
            public bool Imported;
        }
    }
}
