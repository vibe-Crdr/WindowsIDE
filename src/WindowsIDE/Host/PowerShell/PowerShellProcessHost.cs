using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace WindowsIDE.Host.PowerShell
{
    /// <summary>
    /// ユーザー .ps1 の 1 行。stdout か stderr か。
    /// </summary>
    public sealed class PowerShellLineReceivedEventArgs : EventArgs
    {
        private int generation;
        private string text;
        private bool isStderr;

        /// <summary>
        /// 世代と本文と種別を渡す。
        /// </summary>
        /// <param name="generation">Start に渡した世代。</param>
        /// <param name="text">1 行。改行は含まない。</param>
        /// <param name="isStderr">stderr なら true。</param>
        public PowerShellLineReceivedEventArgs(int generation, string text, bool isStderr)
        {
            this.generation = generation;
            this.text = (text == null) ? "" : text;
            this.isStderr = isStderr;
        }

        /// <summary>Start に渡した世代。</summary>
        public int Generation
        {
            get { return this.generation; }
        }

        /// <summary>1 行。</summary>
        public string Text
        {
            get { return this.text; }
        }

        /// <summary>stderr なら true。</summary>
        public bool IsStderr
        {
            get { return this.isStderr; }
        }
    }

    /// <summary>
    /// ユーザー .ps1 の終了。
    /// </summary>
    public sealed class PowerShellProcessExitedEventArgs : EventArgs
    {
        private int generation;
        private int exitCode;

        /// <summary>
        /// 世代と終了コードを渡す。
        /// </summary>
        /// <param name="generation">Start に渡した世代。</param>
        /// <param name="exitCode">プロセスの ExitCode。取れなければ -1。</param>
        public PowerShellProcessExitedEventArgs(int generation, int exitCode)
        {
            this.generation = generation;
            this.exitCode = exitCode;
        }

        /// <summary>Start に渡した世代。</summary>
        public int Generation
        {
            get { return this.generation; }
        }

        /// <summary>終了コード。</summary>
        public int ExitCode
        {
            get { return this.exitCode; }
        }
    }

    /// <summary>
    /// ユーザー .ps1 の起動失敗。
    /// </summary>
    public sealed class PowerShellStartFailedEventArgs : EventArgs
    {
        private int generation;
        private string message;

        /// <summary>
        /// 世代と理由を渡す。
        /// </summary>
        /// <param name="generation">Start に渡した世代。</param>
        /// <param name="message">理由。</param>
        public PowerShellStartFailedEventArgs(int generation, string message)
        {
            this.generation = generation;
            this.message = (message == null) ? "" : message;
        }

        /// <summary>Start に渡した世代。</summary>
        public int Generation
        {
            get { return this.generation; }
        }

        /// <summary>理由。</summary>
        public string Message
        {
            get { return this.message; }
        }
    }

    /// <summary>
    /// ディスク上の .ps1 を Windows PowerShell 5.1 の子プロセスで 1 本だけ起動する。WinForms 非依存。
    /// 保持している Process 参照だけ Kill する。プロセス名検索はしない。
    /// </summary>
    public sealed class PowerShellProcessHost
    {
        private readonly object gate;
        private Process process;
        private string startError;

        /// <summary>
        /// 空のホストを作る。
        /// </summary>
        public PowerShellProcessHost()
        {
            this.gate = new object();
        }

        /// <summary>直前の Start の失敗理由。成功時は null。</summary>
        public string StartError
        {
            get { return this.startError; }
        }

        /// <summary>保持プロセスがまだ生きていれば true。</summary>
        public bool IsRunning
        {
            get
            {
                lock (this.gate)
                {
                    Process p = this.process;
                    if (p == null)
                    {
                        return false;
                    }

                    try
                    {
                        return !p.HasExited;
                    }
                    catch (InvalidOperationException)
                    {
                        return false;
                    }
                }
            }
        }

        /// <summary>1 行。呼び出しスレッドは UI ではない。</summary>
        public event EventHandler<PowerShellLineReceivedEventArgs> LineReceived;

        /// <summary>終了。呼び出しスレッドは UI ではない。</summary>
        public event EventHandler<PowerShellProcessExitedEventArgs> Exited;

        /// <summary>起動失敗。</summary>
        public event EventHandler<PowerShellStartFailedEventArgs> StartFailed;

        /// <summary>
        /// 既存プロセスを Kill してから scriptPath を -File で起動する。stdin は直後に閉じる。
        /// 呼び出しスレッドで WaitForExit しない。
        /// </summary>
        /// <param name="scriptPath">ディスク上の .ps1。</param>
        /// <param name="workingDirectory">作業ディレクトリ。空ならスクリプトのディレクトリ。</param>
        /// <param name="generation">古い完了を捨てるための世代。</param>
        public void Start(string scriptPath, string workingDirectory, int generation)
        {
            this.startError = null;
            this.Kill();
            if (string.IsNullOrEmpty(scriptPath) || !File.Exists(scriptPath))
            {
                this.startError = "実行ファイルがありません。";
                this.RaiseStartFailed(generation, this.startError);
                return;
            }

            string work = workingDirectory;
            if (string.IsNullOrEmpty(work))
            {
                work = Path.GetDirectoryName(scriptPath);
            }

            ProcessStartInfo info = new ProcessStartInfo();
            info.FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe");
            info.Arguments = "-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"" + scriptPath + "\"";
            info.UseShellExecute = false;
            info.CreateNoWindow = true;
            info.RedirectStandardOutput = true;
            info.RedirectStandardError = true;
            info.RedirectStandardInput = true;
            info.StandardOutputEncoding = Encoding.Default;
            info.StandardErrorEncoding = Encoding.Default;
            if (!string.IsNullOrEmpty(work))
            {
                info.WorkingDirectory = work;
            }

            Process p = new Process();
            p.StartInfo = info;
            StreamEnd stdoutEnd = new StreamEnd();
            StreamEnd stderrEnd = new StreamEnd();
            int gen = generation;
            p.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
            {
                this.OnOutput(gen, e, false, stdoutEnd);
            };
            p.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
            {
                this.OnOutput(gen, e, true, stderrEnd);
            };

            lock (this.gate)
            {
                this.process = p;
            }

            try
            {
                if (!p.Start())
                {
                    this.startError = "プロセスを起動できませんでした。";
                    this.ClearProcess(p);
                    this.RaiseStartFailed(generation, this.startError);
                    return;
                }

                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                p.StandardInput.Close();
                Thread waiter = new Thread(delegate()
                {
                    this.WaitAndRaise(p, gen, stdoutEnd, stderrEnd);
                });
                waiter.IsBackground = true;
                waiter.Start();
            }
            catch (Exception ex)
            {
                this.startError = ex.Message;
                this.ClearProcess(p);
                this.RaiseStartFailed(generation, this.startError);
            }
        }

        /// <summary>
        /// このホストが保持するプロセスだけを終了する。プロセス名検索はしない。
        /// 呼び出しスレッドで WaitForExit しない。
        /// </summary>
        public void Kill()
        {
            Process p;
            lock (this.gate)
            {
                p = this.process;
            }

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

        /// <summary>
        /// 保持している Process 参照の終了を待つ。ワーカー専用。UI スレッドから呼ばない。
        /// プロセス名検索はしない。
        /// </summary>
        /// <param name="milliseconds">待ち上限。負なら 0。</param>
        /// <returns>終了していれば true。タイムアウトなら false。</returns>
        public bool WaitUntilExited(int milliseconds)
        {
            Process p;
            lock (this.gate)
            {
                p = this.process;
            }

            if (p == null)
            {
                return true;
            }

            int wait = milliseconds;
            if (wait < 0)
            {
                wait = 0;
            }

            try
            {
                if (p.HasExited)
                {
                    return true;
                }

                return p.WaitForExit(wait);
            }
            catch (InvalidOperationException)
            {
                return true;
            }
            catch (Win32Exception)
            {
                return false;
            }
        }

        private void OnOutput(int generation, DataReceivedEventArgs e, bool isStderr, StreamEnd end)
        {
            if (e == null || e.Data == null)
            {
                if (end != null)
                {
                    end.Set();
                }

                return;
            }

            this.RaiseLine(generation, e.Data, isStderr);
        }

        private void WaitAndRaise(Process p, int generation, StreamEnd stdoutEnd, StreamEnd stderrEnd)
        {
            int code = -1;
            try
            {
                if (p != null)
                {
                    p.WaitForExit();
                }

                if (stdoutEnd != null)
                {
                    stdoutEnd.Wait(10000);
                }

                if (stderrEnd != null)
                {
                    stderrEnd.Wait(10000);
                }

                try
                {
                    if (p != null)
                    {
                        code = p.ExitCode;
                    }
                }
                catch (InvalidOperationException)
                {
                }

                this.RaiseExited(generation, code);
            }
            catch (Exception)
            {
            }
            finally
            {
                this.ClearProcess(p);
                if (stdoutEnd != null)
                {
                    stdoutEnd.Dispose();
                }

                if (stderrEnd != null)
                {
                    stderrEnd.Dispose();
                }
            }
        }

        private void ClearProcess(Process p)
        {
            lock (this.gate)
            {
                if (object.ReferenceEquals(this.process, p))
                {
                    this.process = null;
                }
            }

            if (p == null)
            {
                return;
            }

            try
            {
                p.Dispose();
            }
            catch (Exception)
            {
            }
        }

        private void RaiseLine(int generation, string text, bool isStderr)
        {
            EventHandler<PowerShellLineReceivedEventArgs> h = this.LineReceived;
            if (h != null)
            {
                h(this, new PowerShellLineReceivedEventArgs(generation, text, isStderr));
            }
        }

        private void RaiseExited(int generation, int exitCode)
        {
            EventHandler<PowerShellProcessExitedEventArgs> h = this.Exited;
            if (h != null)
            {
                h(this, new PowerShellProcessExitedEventArgs(generation, exitCode));
            }
        }

        private void RaiseStartFailed(int generation, string message)
        {
            EventHandler<PowerShellStartFailedEventArgs> h = this.StartFailed;
            if (h != null)
            {
                h(this, new PowerShellStartFailedEventArgs(generation, message));
            }
        }

        private sealed class StreamEnd
        {
            private readonly ManualResetEvent done;

            public StreamEnd()
            {
                this.done = new ManualResetEvent(false);
            }

            public void Set()
            {
                this.done.Set();
            }

            public void Wait(int milliseconds)
            {
                this.done.WaitOne(milliseconds);
            }

            public void Dispose()
            {
                this.done.Close();
            }
        }
    }
}
