using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace WindowsIDE.Terminal
{
    /// <summary>
    /// PTY 出力。呼び出しスレッドは UI ではない。
    /// </summary>
    public sealed class TerminalOutputEventArgs : EventArgs
    {
        private int generation;
        private string text;

        /// <summary>
        /// 世代と UTF-8 復号した断片を渡す。
        /// </summary>
        /// <param name="generation">Start の世代。</param>
        /// <param name="text">断片。null は空。</param>
        public TerminalOutputEventArgs(int generation, string text)
        {
            this.generation = generation;
            this.text = (text == null) ? "" : text;
        }

        /// <summary>Start の世代。</summary>
        public int Generation
        {
            get { return this.generation; }
        }

        /// <summary>断片。</summary>
        public string Text
        {
            get { return this.text; }
        }
    }

    /// <summary>
    /// PTY 子プロセスの終了。
    /// </summary>
    public sealed class TerminalExitedEventArgs : EventArgs
    {
        private int generation;
        private int exitCode;

        /// <summary>
        /// 世代と終了コードを渡す。
        /// </summary>
        /// <param name="generation">Start の世代。</param>
        /// <param name="exitCode">取れなければ -1。</param>
        public TerminalExitedEventArgs(int generation, int exitCode)
        {
            this.generation = generation;
            this.exitCode = exitCode;
        }

        /// <summary>Start の世代。</summary>
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
    /// PTY 起動失敗。
    /// </summary>
    public sealed class TerminalStartFailedEventArgs : EventArgs
    {
        private int generation;
        private string message;

        /// <summary>
        /// 世代と理由を渡す。
        /// </summary>
        /// <param name="generation">Start の世代。</param>
        /// <param name="message">理由。</param>
        public TerminalStartFailedEventArgs(int generation, string message)
        {
            this.generation = generation;
            this.message = (message == null) ? "" : message;
        }

        /// <summary>Start の世代。</summary>
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
    /// ConPTY 1 セッション。起動・書き込み・バックグラウンド読み・Kill・Dispose。WinForms 非依存。
    /// </summary>
    public sealed class PseudoConsoleSession : IDisposable
    {
        private readonly object gate;
        private int generation;
        private bool closed;
        private bool running;
        private string startError;
        private IntPtr hPC;
        private IntPtr hProcess;
        private IntPtr ptyWrite;
        private IntPtr ptyRead;
        private IntPtr attrList;

        /// <summary>
        /// 空のセッションを作る。
        /// </summary>
        public PseudoConsoleSession()
        {
            this.gate = new object();
        }

        /// <summary>直前の Start の失敗理由。成功時は null。</summary>
        public string StartError
        {
            get { return this.startError; }
        }

        /// <summary>保持プロセスが生きていれば true。</summary>
        public bool IsRunning
        {
            get
            {
                lock (this.gate)
                {
                    return this.running && this.hProcess != IntPtr.Zero && !this.closed;
                }
            }
        }

        /// <summary>現在の世代。</summary>
        public int Generation
        {
            get
            {
                lock (this.gate)
                {
                    return this.generation;
                }
            }
        }

        /// <summary>出力断片。呼び出しスレッドは UI ではない。</summary>
        public event EventHandler<TerminalOutputEventArgs> OutputReceived;

        /// <summary>終了。呼び出しスレッドは UI ではない。</summary>
        public event EventHandler<TerminalExitedEventArgs> Exited;

        /// <summary>起動失敗。</summary>
        public event EventHandler<TerminalStartFailedEventArgs> StartFailed;

        /// <summary>
        /// 製品の対話シェルを起動する。既存セッションは Kill する（待たない）。
        /// </summary>
        /// <param name="kind">シェル。</param>
        /// <param name="workspaceRoot">作業ディレクトリの根。無ければプロファイル。</param>
        /// <param name="columns">PTY 列。最小 20。</param>
        /// <param name="rows">PTY 行。最小 4。</param>
        /// <param name="generation">古い完了を捨てる世代。0 以下なら内部で増やす。</param>
        public void Start(ShellKind kind, string workspaceRoot, int columns, int rows, int generation)
        {
            string exe = ShellPaths.GetExecutable(kind);
            string args = ShellPaths.GetArguments(kind);
            string cwd = ShellPaths.GetWorkingDirectory(workspaceRoot);
            this.Start(exe, args, cwd, columns, rows, generation);
        }

        /// <summary>
        /// 指定 exe と引数で ConPTY 付きプロセスを起動する。UI で待たない。
        /// </summary>
        /// <param name="exePath">フルパス。</param>
        /// <param name="arguments">引数。null は無し。</param>
        /// <param name="workingDirectory">作業ディレクトリ。</param>
        /// <param name="columns">列。</param>
        /// <param name="rows">行。</param>
        /// <param name="generation">世代。0 以下なら内部で増やす。</param>
        public void Start(string exePath, string arguments, string workingDirectory, int columns, int rows, int generation)
        {
            this.startError = null;
            this.Kill();
            int gen;
            lock (this.gate)
            {
                if (generation > 0)
                {
                    this.generation = generation;
                }
                else
                {
                    this.generation++;
                    if (this.generation < 1)
                    {
                        this.generation = 1;
                    }
                }

                gen = this.generation;
                this.closed = false;
                this.running = false;
            }

            if (!ShellPaths.IsAllowedExecutable(exePath))
            {
                this.FailStart(gen, "実行ファイルが許可されていません。");
                return;
            }

            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
            {
                this.FailStart(gen, "実行ファイルがありません。");
                return;
            }

            int cols = columns;
            int rws = rows;
            if (cols < VtScreen.MinColumns)
            {
                cols = VtScreen.MinColumns;
            }

            if (rws < VtScreen.MinRows)
            {
                rws = VtScreen.MinRows;
            }

            string cwd = workingDirectory;
            if (string.IsNullOrEmpty(cwd) || !Directory.Exists(cwd))
            {
                cwd = ShellPaths.GetWorkingDirectory(null);
            }

            IntPtr inRead = IntPtr.Zero;
            IntPtr inWrite = IntPtr.Zero;
            IntPtr outRead = IntPtr.Zero;
            IntPtr outWrite = IntPtr.Zero;
            IntPtr pc = IntPtr.Zero;
            IntPtr list = IntPtr.Zero;
            IntPtr environment = IntPtr.Zero;
            NativeMethods.ProcessInformation pi = new NativeMethods.ProcessInformation();
            bool processCreated = false;
            try
            {
                NativeMethods.SecurityAttributes sa = new NativeMethods.SecurityAttributes();
                sa.nLength = Marshal.SizeOf(typeof(NativeMethods.SecurityAttributes));
                sa.bInheritHandle = 1;
                if (!NativeMethods.CreatePipe(out inRead, out inWrite, ref sa, 0))
                {
                    this.FailStart(gen, "パイプを作れませんでした。");
                    return;
                }

                if (!NativeMethods.CreatePipe(out outRead, out outWrite, ref sa, 0))
                {
                    this.FailStart(gen, "パイプを作れませんでした。");
                    return;
                }

                NativeMethods.SetHandleInformation(inWrite, NativeMethods.HandleFlagInherit, 0);
                NativeMethods.SetHandleInformation(outRead, NativeMethods.HandleFlagInherit, 0);

                NativeMethods.Coord size = new NativeMethods.Coord();
                size.X = (short)cols;
                size.Y = (short)rws;
                int hr = NativeMethods.CreatePseudoConsole(size, inRead, outWrite, 0, out pc);
                if (hr < 0 || pc == IntPtr.Zero)
                {
                    this.FailStart(gen, "疑似コンソールを作れませんでした。");
                    return;
                }

                NativeMethods.SafeClose(ref inRead);
                NativeMethods.SafeClose(ref outWrite);

                IntPtr attrSize = IntPtr.Zero;
                NativeMethods.InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref attrSize);
                if (attrSize == IntPtr.Zero)
                {
                    this.FailStart(gen, "プロセス属性を初期化できませんでした。");
                    return;
                }

                list = Marshal.AllocHGlobal(attrSize);
                if (!NativeMethods.InitializeProcThreadAttributeList(list, 1, 0, ref attrSize))
                {
                    this.FailStart(gen, "プロセス属性を初期化できませんでした。");
                    return;
                }

                if (!NativeMethods.UpdateProcThreadAttribute(
                    list,
                    0,
                    NativeMethods.ProcThreadAttributePseudoconsole,
                    pc,
                    new IntPtr(IntPtr.Size),
                    IntPtr.Zero,
                    IntPtr.Zero))
                {
                    this.FailStart(gen, "疑似コンソール属性を設定できませんでした。");
                    return;
                }

                lock (this.gate)
                {
                    this.hPC = pc;
                    this.ptyWrite = inWrite;
                    this.ptyRead = outRead;
                    this.attrList = list;
                    this.closed = false;
                }

                inWrite = IntPtr.Zero;
                outRead = IntPtr.Zero;
                IntPtr attrForProcess = list;
                list = IntPtr.Zero;
                pc = IntPtr.Zero;

                Thread reader = new Thread(delegate()
                {
                    this.ReadLoop(gen);
                });
                reader.IsBackground = true;
                reader.Name = "WindowsIDE.Terminal.Read";
                reader.Start();

                NativeMethods.StartupInfoEx siex = new NativeMethods.StartupInfoEx();
                siex.StartupInfo.cb = Marshal.SizeOf(typeof(NativeMethods.StartupInfoEx));
                // dwFlags は 0 のまま。STARTF_USESTDHANDLES を付けると子がリダイレクト扱いになる。
                // レジストリ Blind Access Off のときだけ張り付き SPI を解除し、PTY 子へ TERM は渡さない。
                siex.lpAttributeList = attrForProcess;

                StringBuilder cmdLine = new StringBuilder(exePath.Length + 256);
                cmdLine.Append('"');
                cmdLine.Append(exePath);
                cmdLine.Append('"');
                if (!string.IsNullOrEmpty(arguments))
                {
                    cmdLine.Append(' ');
                    cmdLine.Append(arguments);
                }

                uint flags = NativeMethods.ExtendedStartupinfoPresent | NativeMethods.CreateUnicodeEnvironment;
                NativeMethods.ClearStuckScreenReaderFlag();
                environment = NativeMethods.AllocUnicodeEnvironmentWithoutTerm();
                if (!NativeMethods.CreateProcessW(
                    null,
                    cmdLine,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    false,
                    flags,
                    environment,
                    cwd,
                    ref siex,
                    out pi))
                {
                    this.FailStart(gen, "プロセスを起動できませんでした。");
                    return;
                }

                processCreated = true;
                lock (this.gate)
                {
                    this.hProcess = pi.hProcess;
                    pi.hProcess = IntPtr.Zero;
                    this.running = true;
                    this.closed = false;
                }

                Thread waiter = new Thread(delegate()
                {
                    this.WaitLoop(gen);
                });
                waiter.IsBackground = true;
                waiter.Name = "WindowsIDE.Terminal.Wait";
                waiter.Start();
            }
            catch (Exception ex)
            {
                this.startError = ex.Message;
                this.FailStart(gen, this.startError);
            }
            finally
            {
                if (environment != IntPtr.Zero)
                {
                    try
                    {
                        Marshal.FreeHGlobal(environment);
                    }
                    catch
                    {
                    }

                    environment = IntPtr.Zero;
                }

                NativeMethods.SafeClose(ref inRead);
                NativeMethods.SafeClose(ref inWrite);
                NativeMethods.SafeClose(ref outRead);
                NativeMethods.SafeClose(ref outWrite);
                if (pi.hThread != IntPtr.Zero)
                {
                    NativeMethods.SafeClose(ref pi.hThread);
                }

                if (pi.hProcess != IntPtr.Zero && !processCreated)
                {
                    NativeMethods.SafeClose(ref pi.hProcess);
                }

                if (list != IntPtr.Zero)
                {
                    try
                    {
                        NativeMethods.DeleteProcThreadAttributeList(list);
                    }
                    catch
                    {
                    }

                    Marshal.FreeHGlobal(list);
                }

                if (pc != IntPtr.Zero)
                {
                    try
                    {
                        NativeMethods.ClosePseudoConsole(pc);
                    }
                    catch
                    {
                    }
                }
            }
        }

        /// <summary>
        /// UTF-8 バイトを PTY へ書く。Enter は 0x0D。
        /// </summary>
        /// <param name="data">バイト。null や空は無視。</param>
        public void Write(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                return;
            }

            IntPtr h;
            lock (this.gate)
            {
                if (this.closed || this.ptyWrite == IntPtr.Zero)
                {
                    return;
                }

                h = this.ptyWrite;
            }

            uint written;
            NativeMethods.WriteFile(h, data, (uint)data.Length, out written, IntPtr.Zero);
        }

        /// <summary>
        /// 文字列を UTF-8 で書く。BOM は付けない。
        /// </summary>
        /// <param name="text">本文。null は無視。</param>
        public void WriteText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            this.Write(Encoding.UTF8.GetBytes(text));
        }

        /// <summary>
        /// PTY をリサイズする。世代が現在のときだけ。UI で待たない。
        /// </summary>
        /// <param name="columns">列。</param>
        /// <param name="rows">行。</param>
        /// <param name="generation">要求世代。現在と違えば無視。</param>
        public void Resize(int columns, int rows, int generation)
        {
            IntPtr pc;
            lock (this.gate)
            {
                if (this.closed || this.hPC == IntPtr.Zero || generation != this.generation)
                {
                    return;
                }

                pc = this.hPC;
            }

            int cols = columns;
            int rws = rows;
            if (cols < VtScreen.MinColumns)
            {
                cols = VtScreen.MinColumns;
            }

            if (rws < VtScreen.MinRows)
            {
                rws = VtScreen.MinRows;
            }

            NativeMethods.Coord size = new NativeMethods.Coord();
            size.X = (short)cols;
            size.Y = (short)rws;
            NativeMethods.ResizePseudoConsole(pc, size);
        }

        /// <summary>
        /// 保持するプロセスと HPCON / パイプだけを閉じる。プロセス名検索はしない。UI で待たない。
        /// </summary>
        public void Kill()
        {
            IntPtr process;
            IntPtr pc;
            IntPtr write;
            IntPtr read;
            IntPtr list;
            lock (this.gate)
            {
                this.closed = true;
                this.running = false;
                process = this.hProcess;
                pc = this.hPC;
                write = this.ptyWrite;
                read = this.ptyRead;
                list = this.attrList;
                this.hPC = IntPtr.Zero;
                this.ptyWrite = IntPtr.Zero;
                this.ptyRead = IntPtr.Zero;
                this.attrList = IntPtr.Zero;
            }

            if (process != IntPtr.Zero)
            {
                try
                {
                    NativeMethods.TerminateProcess(process, 1);
                }
                catch
                {
                }
            }

            if (pc != IntPtr.Zero)
            {
                try
                {
                    NativeMethods.ClosePseudoConsole(pc);
                }
                catch
                {
                }
            }

            NativeMethods.SafeClose(ref write);
            NativeMethods.SafeClose(ref read);
            if (list != IntPtr.Zero)
            {
                try
                {
                    NativeMethods.DeleteProcThreadAttributeList(list);
                }
                catch
                {
                }

                try
                {
                    Marshal.FreeHGlobal(list);
                }
                catch
                {
                }
            }
        }

        /// <summary>
        /// 保持プロセスの終了を待つ。ワーカー／テスト専用。UI スレッドから呼ばない。
        /// </summary>
        /// <param name="milliseconds">待ち上限。負なら 0。</param>
        /// <returns>終了していれば true。</returns>
        public bool WaitUntilExited(int milliseconds)
        {
            IntPtr process;
            lock (this.gate)
            {
                process = this.hProcess;
                if (process == IntPtr.Zero && !this.running)
                {
                    return true;
                }
            }

            if (process == IntPtr.Zero)
            {
                return true;
            }

            int wait = milliseconds;
            if (wait < 0)
            {
                wait = 0;
            }

            uint rc = NativeMethods.WaitForSingleObject(process, (uint)wait);
            return rc == NativeMethods.WaitObject0;
        }

        /// <summary>Kill して破棄する。UI で待たない。</summary>
        public void Dispose()
        {
            this.Kill();
        }

        private void ReadLoop(int generation)
        {
            byte[] buffer = new byte[4096];
            char[] chars = new char[4096];
            Decoder decoder = Encoding.UTF8.GetDecoder();
            while (true)
            {
                IntPtr h;
                lock (this.gate)
                {
                    if (this.closed || generation != this.generation)
                    {
                        return;
                    }

                    h = this.ptyRead;
                    if (h == IntPtr.Zero)
                    {
                        return;
                    }
                }

                uint read;
                bool ok;
                try
                {
                    ok = NativeMethods.ReadFile(h, buffer, (uint)buffer.Length, out read, IntPtr.Zero);
                }
                catch
                {
                    return;
                }

                if (!ok || read == 0)
                {
                    return;
                }

                int n = decoder.GetChars(buffer, 0, (int)read, chars, 0);
                if (n > 0)
                {
                    this.RaiseOutput(generation, new string(chars, 0, n));
                }
            }
        }

        private void WaitLoop(int generation)
        {
            IntPtr process;
            lock (this.gate)
            {
                process = this.hProcess;
            }

            int code = -1;
            if (process != IntPtr.Zero)
            {
                NativeMethods.WaitForSingleObject(process, NativeMethods.Infinite);
                uint exit;
                if (NativeMethods.GetExitCodeProcess(process, out exit))
                {
                    code = (int)exit;
                }
            }

            IntPtr pc = IntPtr.Zero;
            lock (this.gate)
            {
                if (generation == this.generation)
                {
                    this.running = false;
                    pc = this.hPC;
                    this.hPC = IntPtr.Zero;
                }

                if (this.hProcess == process)
                {
                    this.hProcess = IntPtr.Zero;
                }
            }

            if (pc != IntPtr.Zero)
            {
                try
                {
                    NativeMethods.ClosePseudoConsole(pc);
                }
                catch
                {
                }
            }

            NativeMethods.SafeClose(ref process);
            this.RaiseExited(generation, code);
        }

        private void FailStart(int generation, string message)
        {
            this.startError = message;
            this.Kill();
            this.RaiseStartFailed(generation, message);
        }

        private void RaiseOutput(int generation, string text)
        {
            EventHandler<TerminalOutputEventArgs> h = this.OutputReceived;
            if (h != null)
            {
                h(this, new TerminalOutputEventArgs(generation, text));
            }
        }

        private void RaiseExited(int generation, int exitCode)
        {
            EventHandler<TerminalExitedEventArgs> h = this.Exited;
            if (h != null)
            {
                h(this, new TerminalExitedEventArgs(generation, exitCode));
            }
        }

        private void RaiseStartFailed(int generation, string message)
        {
            EventHandler<TerminalStartFailedEventArgs> h = this.StartFailed;
            if (h != null)
            {
                h(this, new TerminalStartFailedEventArgs(generation, message));
            }
        }
    }
}
