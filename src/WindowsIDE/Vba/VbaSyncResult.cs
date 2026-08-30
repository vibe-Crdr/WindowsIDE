using System;
using WindowsIDE.Build;

namespace WindowsIDE.Vba
{
    /// <summary>
    /// 同期の結果。MessageBox は MainForm。WinForms 非依存。
    /// </summary>
    public sealed class VbaSyncResult
    {
        private bool success;
        private bool cancelled;
        private bool createdMap;
        private bool replaceProblems;
        private bool applyVbaCompile;
        private bool isErrorMessage;
        private string message;
        private Diagnostic[] diagnostics;
        private string[] excelOnlyNames;
        private string[] writtenPaths;

        private VbaSyncResult()
        {
            this.diagnostics = new Diagnostic[0];
            this.excelOnlyNames = new string[0];
            this.writtenPaths = new string[0];
        }

        /// <summary>操作が完了したら true。</summary>
        public bool Success
        {
            get { return this.success; }
        }

        /// <summary>ユーザーが確認で中止したら true。追加の MessageBox は出さない。</summary>
        public bool Cancelled
        {
            get { return this.cancelled; }
        }

        /// <summary>この操作で vba-map.xml を新規作成したら true。</summary>
        public bool CreatedMap
        {
            get { return this.createdMap; }
        }

        /// <summary>問題一覧をこの回の診断で置き換えるなら true。</summary>
        public bool ReplaceProblems
        {
            get { return this.replaceProblems; }
        }

        /// <summary>成功後に VBA 診断バケットへ出すなら true（手動 Compile）。</summary>
        public bool ApplyVbaCompile
        {
            get { return this.applyVbaCompile; }
        }

        /// <summary>MessageBox を Error にするなら true。Information なら false。</summary>
        public bool IsErrorMessage
        {
            get { return this.isErrorMessage; }
        }

        /// <summary>MessageBox 本文。空なら出さない。</summary>
        public string Message
        {
            get { return this.message; }
        }

        /// <summary>問題一覧へ出す診断。</summary>
        public Diagnostic[] Diagnostics
        {
            get { return this.diagnostics; }
        }

        /// <summary>Excel にだけあるモジュール名。成功後の案内用。</summary>
        public string[] ExcelOnlyNames
        {
            get { return this.excelOnlyNames; }
        }

        /// <summary>ディスクへ書いたパス。タブ再読込用。</summary>
        public string[] WrittenPaths
        {
            get { return this.writtenPaths; }
        }

        /// <summary>
        /// 成功（追加 UI なし）。
        /// </summary>
        /// <returns>結果。</returns>
        public static VbaSyncResult Ok()
        {
            VbaSyncResult r = new VbaSyncResult();
            r.success = true;
            return r;
        }

        /// <summary>
        /// マップ新規作成の成功。
        /// </summary>
        /// <returns>結果。</returns>
        public static VbaSyncResult OkCreatedMap()
        {
            VbaSyncResult r = new VbaSyncResult();
            r.success = true;
            r.createdMap = true;
            return r;
        }

        /// <summary>
        /// プル／プッシュ成功。
        /// </summary>
        /// <param name="writtenPaths">書いたパス。</param>
        /// <param name="excelOnlyNames">Excel のみの名前。</param>
        /// <returns>結果。</returns>
        public static VbaSyncResult OkSync(string[] writtenPaths, string[] excelOnlyNames)
        {
            VbaSyncResult r = new VbaSyncResult();
            r.success = true;
            r.writtenPaths = (writtenPaths == null) ? new string[0] : writtenPaths;
            r.excelOnlyNames = (excelOnlyNames == null) ? new string[0] : excelOnlyNames;
            return r;
        }

        /// <summary>
        /// プッシュ成功のうえ Compile 結果を VBA バケットへ出す。
        /// </summary>
        /// <param name="writtenPaths">書いたパス。</param>
        /// <param name="excelOnlyNames">Excel のみの名前。</param>
        /// <param name="compileDiagnostics">Compile 診断。成功クリアは空。</param>
        /// <returns>結果。</returns>
        public static VbaSyncResult OkPushThenCompile(string[] writtenPaths, string[] excelOnlyNames, Diagnostic[] compileDiagnostics)
        {
            VbaSyncResult r = OkSync(writtenPaths, excelOnlyNames);
            r.applyVbaCompile = true;
            r.diagnostics = (compileDiagnostics == null) ? new Diagnostic[0] : compileDiagnostics;
            return r;
        }

        /// <summary>
        /// ユーザー中止。問題一覧は触らない。
        /// </summary>
        /// <returns>結果。</returns>
        public static VbaSyncResult Cancel()
        {
            VbaSyncResult r = new VbaSyncResult();
            r.cancelled = true;
            return r;
        }

        /// <summary>
        /// MessageBox 用。問題一覧は触らない。
        /// </summary>
        /// <param name="message">本文。</param>
        /// <param name="isError">Error アイコンなら true。</param>
        /// <returns>結果。</returns>
        public static VbaSyncResult MessageBox(string message, bool isError)
        {
            VbaSyncResult r = new VbaSyncResult();
            r.message = (message == null) ? "" : message;
            r.isErrorMessage = isError;
            return r;
        }

        /// <summary>
        /// 問題一覧を置き換える失敗。
        /// </summary>
        /// <param name="diagnostics">診断。</param>
        /// <returns>結果。</returns>
        public static VbaSyncResult Problems(Diagnostic[] diagnostics)
        {
            VbaSyncResult r = new VbaSyncResult();
            r.replaceProblems = true;
            r.diagnostics = (diagnostics == null) ? new Diagnostic[0] : diagnostics;
            return r;
        }
    }

    /// <summary>
    /// 同期中の確認。WinForms 非依存。false で全体中止。
    /// </summary>
    public sealed class VbaSyncConfirm
    {
        private Func<string[], bool> confirmDirty;
        private Func<bool> confirmExcelUnsaved;

        /// <summary>
        /// コールバックを渡す。null なら確認なしで続行。
        /// </summary>
        /// <param name="confirmDirty">上書き／読み取り対象の未保存。true で保存済みとして続行。</param>
        /// <param name="confirmExcelUnsaved">Excel 未保存。true で Workbook.Save して続行。</param>
        public VbaSyncConfirm(Func<string[], bool> confirmDirty, Func<bool> confirmExcelUnsaved)
        {
            this.confirmDirty = confirmDirty;
            this.confirmExcelUnsaved = confirmExcelUnsaved;
        }

        /// <summary>
        /// 対象パスに未保存があれば確認する。
        /// </summary>
        /// <param name="paths">対象フルパス。</param>
        /// <returns>続行なら true。</returns>
        public bool ConfirmDirty(string[] paths)
        {
            if (this.confirmDirty == null)
            {
                return true;
            }

            return this.confirmDirty(paths);
        }

        /// <summary>
        /// Excel 未保存の確認。
        /// </summary>
        /// <returns>保存して続行なら true。</returns>
        public bool ConfirmExcelUnsaved()
        {
            if (this.confirmExcelUnsaved == null)
            {
                return true;
            }

            return this.confirmExcelUnsaved();
        }
    }

    /// <summary>
    /// namingMode プレビューの 1 行。document は旧名＝新名。
    /// </summary>
    public sealed class VbaRenamePreview
    {
        private string oldName;
        private string newName;
        private bool document;

        /// <summary>
        /// 旧名と新名。
        /// </summary>
        /// <param name="oldName">現在の Excel 名。</param>
        /// <param name="newName">新しい Excel 名。</param>
        /// <param name="document">document なら true。</param>
        public VbaRenamePreview(string oldName, string newName, bool document)
        {
            this.oldName = (oldName == null) ? "" : oldName;
            this.newName = (newName == null) ? "" : newName;
            this.document = document;
        }

        /// <summary>現在の名前。</summary>
        public string OldName
        {
            get { return this.oldName; }
        }

        /// <summary>新しい名前。</summary>
        public string NewName
        {
            get { return this.newName; }
        }

        /// <summary>document なら true（変更なし）。</summary>
        public bool IsDocument
        {
            get { return this.document; }
        }
    }
}
