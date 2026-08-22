using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace WindowsIDE.Build
{
    /// <summary>
    /// 指定 csc.exe の 1 回分の結果。ユーザー EXE は含まない。
    /// </summary>
    public sealed class CscRunResult
    {
        private int generation;
        private int exitCode;
        private string standardOutput;
        private string standardError;
        private string startError;
        private bool started;

        /// <summary>呼び出し側が付けた世代。</summary>
        public int Generation
        {
            get { return this.generation; }
            set { this.generation = value; }
        }

        /// <summary>プロセスが起動できたか。</summary>
        public bool Started
        {
            get { return this.started; }
            set { this.started = value; }
        }

        /// <summary>csc の終了コード。未起動は -1。</summary>
        public int ExitCode
        {
            get { return this.exitCode; }
            set { this.exitCode = value; }
        }

        /// <summary>標準出力。</summary>
        public string StandardOutput
        {
            get { return this.standardOutput; }
            set { this.standardOutput = value; }
        }

        /// <summary>標準エラー。</summary>
        public string StandardError
        {
            get { return this.standardError; }
            set { this.standardError = value; }
        }

        /// <summary>起動失敗の理由。成功時は null。</summary>
        public string StartError
        {
            get { return this.startError; }
            set { this.startError = value; }
        }

        /// <summary>
        /// stdout と stderr を改行でつなぐ。
        /// </summary>
        public string CombinedOutput()
        {
            string a = this.standardOutput;
            string b = this.standardError;
            if (string.IsNullOrEmpty(a))
            {
                return (b == null) ? "" : b;
            }

            if (string.IsNullOrEmpty(b))
            {
                return a;
            }

            return a + "\n" + b;
        }
    }

    /// <summary>
    /// 指定 Framework csc.exe を別プロセスで走らせる。このインスタンスが持っている Process だけ Kill する。
    /// </summary>
    public sealed class CscRunner
    {
        private readonly object gate;
        private readonly object runLock;
        private Process process;

        /// <summary>
        /// 空のランナーを作る。
        /// </summary>
        public CscRunner()
        {
            this.gate = new object();
            this.runLock = new object();
        }

        /// <summary>
        /// このランナーが保持する csc だけを終了する。プロセス名検索はしない。
        /// </summary>
        public void Kill()
        {
            Process p;
            lock (this.gate)
            {
                p = this.process;
            }

            KillProcess(p);
        }

        /// <summary>
        /// 指定 csc を同期実行する。呼び出し元スレッドで待つ。UI スレッドから呼ばない。
        /// </summary>
        /// <param name="rspPath">UTF-8 BOM の rsp。コマンドラインは /noconfig @rsp。</param>
        /// <param name="generation">古い完了を捨てるための世代。</param>
        /// <returns>出力と終了コード。</returns>
        public CscRunResult Run(string rspPath, int generation)
        {
            lock (this.runLock)
            {
                return this.RunCore(rspPath, generation);
            }
        }

        private CscRunResult RunCore(string rspPath, int generation)
        {
            CscRunResult result = new CscRunResult();
            result.Generation = generation;
            result.ExitCode = -1;
            result.StandardOutput = "";
            result.StandardError = "";
            if (string.IsNullOrEmpty(rspPath))
            {
                result.StartError = "応答ファイルが空です。";
                return result;
            }

            ProcessStartInfo info = new ProcessStartInfo();
            info.FileName = FrameworkCsc.CompilerPath;
            info.Arguments = "/noconfig @\"" + rspPath + "\"";
            info.UseShellExecute = false;
            info.CreateNoWindow = true;
            info.RedirectStandardOutput = true;
            info.RedirectStandardError = true;
            info.RedirectStandardInput = true;
            info.StandardOutputEncoding = Encoding.UTF8;
            info.StandardErrorEncoding = Encoding.UTF8;

            Process p = new Process();
            p.StartInfo = info;
            LineBucket stdout = new LineBucket();
            LineBucket stderr = new LineBucket();
            p.OutputDataReceived += stdout.OnData;
            p.ErrorDataReceived += stderr.OnData;
            lock (this.gate)
            {
                KillProcess(this.process);
                this.process = p;
            }

            try
            {
                if (!p.Start())
                {
                    result.StartError = "csc.exe を起動できませんでした。";
                    return result;
                }

                result.Started = true;
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                p.StandardInput.Close();
                p.WaitForExit();
                stdout.WaitEnd();
                stderr.WaitEnd();
                result.ExitCode = p.ExitCode;
                result.StandardOutput = stdout.GetText();
                result.StandardError = stderr.GetText();
                return result;
            }
            catch (Exception ex)
            {
                result.StartError = ex.Message;
                return result;
            }
            finally
            {
                try
                {
                    p.OutputDataReceived -= stdout.OnData;
                    p.ErrorDataReceived -= stderr.OnData;
                }
                catch (Exception)
                {
                }

                stdout.Dispose();
                stderr.Dispose();
                lock (this.gate)
                {
                    if (object.ReferenceEquals(this.process, p))
                    {
                        this.process = null;
                    }
                }

                try
                {
                    p.Dispose();
                }
                catch (Exception)
                {
                }
            }
        }

        private static void KillProcess(Process p)
        {
            if (p == null)
            {
                return;
            }

            try
            {
                if (!p.HasExited)
                {
                    p.Kill();
                }
            }
            catch (InvalidOperationException)
            {
            }
            catch (Win32Exception)
            {
            }
        }

        private sealed class LineBucket
        {
            private readonly StringBuilder text;
            private readonly ManualResetEvent done;

            public LineBucket()
            {
                this.text = new StringBuilder();
                this.done = new ManualResetEvent(false);
            }

            public void OnData(object sender, DataReceivedEventArgs e)
            {
                if (e == null || e.Data == null)
                {
                    this.done.Set();
                    return;
                }

                lock (this.text)
                {
                    if (this.text.Length > 0)
                    {
                        this.text.Append('\n');
                    }

                    this.text.Append(e.Data);
                }
            }

            public void WaitEnd()
            {
                this.done.WaitOne(60000);
            }

            public string GetText()
            {
                lock (this.text)
                {
                    return this.text.ToString();
                }
            }

            public void Dispose()
            {
                this.done.Close();
            }
        }
    }
}
