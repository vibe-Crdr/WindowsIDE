using System;
using System.Collections.Generic;
using System.IO;

namespace WindowsIDE
{
    /// <summary>
    /// 起動引数 1 件の失敗。パスと理由。
    /// </summary>
    public sealed class StartupFailure
    {
        private readonly string path;
        private readonly string reason;

        /// <summary>
        /// 失敗したパスと理由を保持する。
        /// </summary>
        /// <param name="path">引数またはフルパス。空のこともある。</param>
        /// <param name="reason">失敗理由。</param>
        public StartupFailure(string path, string reason)
        {
            this.path = path;
            this.reason = reason;
        }

        /// <summary>失敗した引数またはフルパス。</summary>
        public string Path { get { return this.path; } }

        /// <summary>失敗理由。</summary>
        public string Reason { get { return this.reason; } }
    }

    /// <summary>
    /// 起動引数の解釈結果。親フォルダはワークスペースにしない。
    /// </summary>
    public sealed class StartupPlan
    {
        private readonly string workspaceRoot;
        private readonly string[] files;
        private readonly StartupFailure[] failures;

        /// <summary>
        /// 解析結果を保持する。
        /// </summary>
        /// <param name="workspaceRoot">ディレクトリ引数の最後。無ければ null。</param>
        /// <param name="files">存在するファイルのフルパス。</param>
        /// <param name="failures">空・解決失敗・欠落。</param>
        public StartupPlan(string workspaceRoot, string[] files, StartupFailure[] failures)
        {
            this.workspaceRoot = workspaceRoot;
            this.files = (files == null) ? new string[0] : files;
            this.failures = (failures == null) ? new StartupFailure[0] : failures;
        }

        /// <summary>ディレクトリ引数の最後。無ければ null。ファイルの親は入れない。</summary>
        public string WorkspaceRoot { get { return this.workspaceRoot; } }

        /// <summary>存在するファイルのフルパス。OrdinalIgnoreCase で重複無し。</summary>
        public string[] Files { get { return this.files; } }

        /// <summary>開けない引数。</summary>
        public StartupFailure[] Failures { get { return this.failures; } }
    }

    /// <summary>
    /// Explorer などから渡された起動引数を解釈する。WinForms に依存しない。
    /// </summary>
    public static class StartupArgs
    {
        /// <summary>
        /// 引数をワークスペース・ファイル・失敗に分ける。ディレクトリは後勝ち。親フォルダは Bind しない。
        /// </summary>
        /// <param name="args">Main の引数。実行ファイルパスは含まない。</param>
        /// <returns>解釈結果。</returns>
        public static StartupPlan Parse(string[] args)
        {
            List<string> files = new List<string>();
            List<StartupFailure> failures = new List<StartupFailure>();
            string workspaceRoot = null;

            if (args == null || args.Length == 0)
            {
                return new StartupPlan(null, files.ToArray(), failures.ToArray());
            }

            for (int i = 0; i < args.Length; i++)
            {
                string raw = args[i];
                if (string.IsNullOrEmpty(raw))
                {
                    failures.Add(new StartupFailure(raw, "空のパスです。"));
                    continue;
                }

                string full;
                try
                {
                    full = Path.GetFullPath(raw);
                }
                catch (Exception ex)
                {
                    failures.Add(new StartupFailure(raw, ex.Message));
                    continue;
                }

                if (Directory.Exists(full))
                {
                    workspaceRoot = full;
                    continue;
                }

                if (File.Exists(full))
                {
                    if (!ContainsPath(files, full))
                    {
                        files.Add(full);
                    }

                    continue;
                }

                failures.Add(new StartupFailure(full, "ファイルまたはフォルダが見つかりません。"));
            }

            return new StartupPlan(workspaceRoot, files.ToArray(), failures.ToArray());
        }

        private static bool ContainsPath(List<string> files, string full)
        {
            for (int i = 0; i < files.Count; i++)
            {
                if (string.Equals(files[i], full, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
