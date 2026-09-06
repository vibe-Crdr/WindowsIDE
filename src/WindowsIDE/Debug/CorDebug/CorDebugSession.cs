using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace WindowsIDE.Debug
{
    /// <summary>
    /// ディスク上のユーザー EXE を ICorDebug.CreateProcess で別プロセスデバッグする。
    /// UI では Initialize / CreateProcess / Wait しない。Host.Csharp とは別。
    /// </summary>
    public sealed class CorDebugSession : IDisposable, ICorDebugCallbackSink
    {
        private readonly object gate;
        private readonly BreakpointStore breakpoints;
        private readonly List<object> keepAlive;
        private readonly AutoResetEvent commandPulse;
        private ManualResetEvent sessionEnded;
        private int waitGeneration;
        private int pendingCommand;
        private int pendingCommandGen;
        private DebugSessionState state;
        private int generation;
        private string exePath;
        private string workingDirectory;
        private bool disposed;
        private bool stopRequested;
        private ICorDebug corDebug;
        private ICorDebugProcess process;
        private ICorDebugThread lastThread;
        private ICorDebugStepper lastStepper;
        private PdbBinder pdb;
        private CorDebugManagedCallback callback;
        private PendingLineBp[] pendingLineBps;
        private IntPtr stdoutRead;
        private IntPtr stderrRead;
        private const int CmdContinue = 1;
        private const int CmdStepOver = 2;
        private const int CmdStepInto = 3;
        private const int CmdStop = 4;

        /// <summary>
        /// ストアを受け取り空のセッションにする。
        /// </summary>
        /// <param name="breakpoints">行 BP。null なら空ストア。</param>
        public CorDebugSession(BreakpointStore breakpoints)
        {
            this.gate = new object();
            this.breakpoints = (breakpoints == null) ? new BreakpointStore() : breakpoints;
            this.keepAlive = new List<object>();
            this.commandPulse = new AutoResetEvent(false);
            this.state = DebugSessionState.Idle;
            this.stdoutRead = IntPtr.Zero;
            this.stderrRead = IntPtr.Zero;
        }

        /// <summary>現在の状態。</summary>
        public DebugSessionState State
        {
            get
            {
                lock (this.gate)
                {
                    return this.state;
                }
            }
        }

        /// <summary>停止。デバッガスレッドから。UI は BeginInvoke すること。</summary>
        public event EventHandler<DebugStoppedEventArgs> Stopped;

        /// <summary>デバッグコンソール。UI は BeginInvoke すること。</summary>
        public event EventHandler<DebugConsoleEventArgs> ConsoleLine;

        /// <summary>終了。世代付き。</summary>
        public event EventHandler<DebugEndedEventArgs> Ended;

        bool ICorDebugCallbackSink.IsCallbackAlive
        {
            get
            {
                lock (this.gate)
                {
                    return !this.disposed && !this.stopRequested;
                }
            }
        }

        /// <summary>
        /// MTA ワーカーで ICorDebug.CreateProcess する。UI では呼ばない。
        /// </summary>
        /// <param name="exePath">TEMP 上の out.exe。</param>
        /// <param name="cwd">作業ディレクトリ。空なら exe のディレクトリ。</param>
        /// <param name="generation">古い完了を捨てるための世代。</param>
        public void Start(string exePath, string cwd, int generation)
        {
            this.RequestStop();
            string full = exePath;
            string work = cwd;
            try
            {
                if (!string.IsNullOrEmpty(full))
                {
                    full = Path.GetFullPath(full);
                }

                if (string.IsNullOrEmpty(work) && !string.IsNullOrEmpty(full))
                {
                    work = Path.GetDirectoryName(full);
                }
            }
            catch (Exception)
            {
            }

            ManualResetEvent ended = new ManualResetEvent(false);
            lock (this.gate)
            {
                this.generation = generation;
                this.exePath = full;
                this.workingDirectory = work;
                this.stopRequested = false;
                this.state = DebugSessionState.Running;
                this.sessionEnded = ended;
                this.waitGeneration = generation;
            }

            Thread thread = new Thread(new ThreadStart(delegate
            {
                this.Worker(generation, ended);
            }));
            thread.IsBackground = true;
            thread.Name = "WindowsIDE.CorDebug";
            thread.SetApartmentState(ApartmentState.MTA);
            thread.Start();
        }

        /// <summary>Stopped なら続行。Idle / Running は no-op。</summary>
        public void Continue()
        {
            this.QueueCommand(CmdContinue);
        }

        /// <summary>Stopped ならステップ オーバー。</summary>
        public void StepOver()
        {
            this.QueueCommand(CmdStepOver);
        }

        /// <summary>Stopped ならステップ イン。</summary>
        public void StepInto()
        {
            this.QueueCommand(CmdStepInto);
        }

        /// <summary>
        /// 停止する。UI で Wait しない。Idle は no-op。保持プロセスだけ Terminate。
        /// </summary>
        public void Stop()
        {
            this.RequestStop();
        }

        /// <summary>デバッガを閉じる。UI で Wait しない。</summary>
        public void Dispose()
        {
            lock (this.gate)
            {
                this.disposed = true;
            }

            this.RequestStop();
            ManualResetEvent ended;
            lock (this.gate)
            {
                ended = this.sessionEnded;
            }

            if (ended != null)
            {
                try
                {
                    ended.Set();
                }
                catch (ObjectDisposedException)
                {
                }
            }

            try
            {
                this.commandPulse.Set();
            }
            catch (ObjectDisposedException)
            {
            }

            try
            {
                this.commandPulse.Close();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        void ICorDebugCallbackSink.HandleBreakpoint(ICorDebugAppDomain appDomain, ICorDebugThread thread)
        {
            this.StopAt(thread);
        }

        void ICorDebugCallbackSink.HandleStepComplete(ICorDebugAppDomain appDomain, ICorDebugThread thread)
        {
            this.StopAt(thread);
        }

        void ICorDebugCallbackSink.HandleUnhandledException(ICorDebugAppDomain appDomain, ICorDebugThread thread)
        {
            this.StopAt(thread);
        }

        void ICorDebugCallbackSink.HandleLoadModule(ICorDebugAppDomain appDomain, ICorDebugModule module)
        {
            if (module == null)
            {
                return;
            }

            string name = ReadModuleName(module);
            string exe;
            int gen;
            lock (this.gate)
            {
                exe = this.exePath;
                gen = this.generation;
            }

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(exe))
            {
                return;
            }

            if (!string.Equals(NormalizePath(name), NormalizePath(exe), StringComparison.OrdinalIgnoreCase))
            {
                string left = Path.GetFileName(name);
                string right = Path.GetFileName(exe);
                if (string.IsNullOrEmpty(left) || !string.Equals(left, right, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            try
            {
                module.EnableJITDebugging(1, 0);
            }
            catch (Exception)
            {
            }

            PendingLineBp[] pending;
            lock (this.gate)
            {
                pending = this.pendingLineBps;
            }

            if (pending == null || pending.Length == 0)
            {
                return;
            }

            this.BindPreparedBreakpoints(module, pending);
        }

        void ICorDebugCallbackSink.HandleCreateProcess(ICorDebugProcess process)
        {
            lock (this.gate)
            {
                if (process != null)
                {
                    this.process = process;
                }
            }
        }

        void ICorDebugCallbackSink.HandleExitProcess(int generation)
        {
            ManualResetEvent ended = null;
            lock (this.gate)
            {
                if (this.waitGeneration == generation)
                {
                    ended = this.sessionEnded;
                }
            }

            if (ended == null)
            {
                return;
            }

            try
            {
                ended.Set();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private void Worker(int gen, ManualResetEvent ended)
        {
            int coInit = -1;
            ICorDebug dbg = null;
            try
            {
                coInit = CorDebugNative.CoInitializeEx(IntPtr.Zero, CorDebugNative.CoinitMultithreaded);
                string path;
                string work;
                lock (this.gate)
                {
                    path = this.exePath;
                    work = this.workingDirectory;
                    if (this.stopRequested || this.disposed || this.generation != gen)
                    {
                        return;
                    }
                }

                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                {
                    this.RaiseConsole(gen, 1, "実行ファイルがありません。");
                    return;
                }

                PdbBinder diskPdb = PdbBinder.TryOpenFromDisk(path);
                lock (this.gate)
                {
                    if (this.pdb != null)
                    {
                        this.pdb.Dispose();
                    }

                    this.pdb = diskPdb;
                }

                if (diskPdb == null)
                {
                    this.RaiseConsole(gen, 2, "PDB を開けませんでした。");
                }
                else
                {
                    this.PrepareCsharpBreakpoints(diskPdb, gen);
                }

                dbg = this.CreateDebugger();
                if (dbg == null)
                {
                    this.RaiseConsole(gen, 1, "CLR デバッガを初期化できませんでした。");
                    return;
                }

                lock (this.gate)
                {
                    if (this.disposed || this.generation != gen || this.stopRequested)
                    {
                        return;
                    }

                    this.corDebug = dbg;
                    this.callback = new CorDebugManagedCallback(this, gen);
                }

                int hr = dbg.SetManagedHandler(this.callback);
                if (!CorDebugNative.Succeeded(hr))
                {
                    this.RaiseConsole(gen, 1, "デバッグ ハンドラを設定できませんでした。");
                    return;
                }

                try
                {
                    dbg.SetUnmanagedHandler(IntPtr.Zero);
                }
                catch (Exception)
                {
                }

                IntPtr stdinChild = IntPtr.Zero;
                IntPtr stdinParent = IntPtr.Zero;
                IntPtr stdoutChild = IntPtr.Zero;
                IntPtr stderrChild = IntPtr.Zero;
                if (!this.CreateStdPipes(out stdinChild, out stdinParent, out stdoutChild, out stderrChild))
                {
                    this.RaiseConsole(gen, 1, "標準出力パイプを作れませんでした。");
                    return;
                }

                CorDebugStartupInfo si = new CorDebugStartupInfo();
                si.cb = Marshal.SizeOf(typeof(CorDebugStartupInfo));
                si.dwFlags = (int)(CorDebugNative.StartfUseStdHandles | CorDebugNative.StartfUseShowWindow);
                si.wShowWindow = 0;
                si.hStdInput = stdinChild;
                si.hStdOutput = stdoutChild;
                si.hStdError = stderrChild;

                StringBuilder cmd = new StringBuilder();
                cmd.Append('"');
                cmd.Append(path);
                cmd.Append('"');

                uint flags = CorDebugNative.CreateNoWindow;
                CorDebugProcessInformation pi = new CorDebugProcessInformation();
                ICorDebugProcess proc;
                hr = dbg.CreateProcess(
                    path,
                    cmd,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    1,
                    flags,
                    IntPtr.Zero,
                    string.IsNullOrEmpty(work) ? null : work,
                    ref si,
                    ref pi,
                    0,
                    out proc);

                CloseIfSet(stdinChild);
                CloseIfSet(stdoutChild);
                CloseIfSet(stderrChild);
                CloseIfSet(stdinParent);
                if (pi.hThread != IntPtr.Zero)
                {
                    CorDebugNative.CloseHandle(pi.hThread);
                }

                if (pi.hProcess != IntPtr.Zero)
                {
                    CorDebugNative.CloseHandle(pi.hProcess);
                }

                if (!CorDebugNative.Succeeded(hr) || proc == null)
                {
                    this.ClosePipeReads();
                    this.RaiseConsole(gen, 1, "デバッグ プロセスを起動できませんでした。");
                    return;
                }

                lock (this.gate)
                {
                    this.process = proc;
                    if (this.stopRequested || this.disposed || this.generation != gen)
                    {
                        try
                        {
                            proc.Terminate(1);
                        }
                        catch (Exception)
                        {
                        }
                    }
                }

                this.StartPipeReader(this.stdoutRead, 0, gen);
                this.StartPipeReader(this.stderrRead, 1, gen);
                this.stdoutRead = IntPtr.Zero;
                this.stderrRead = IntPtr.Zero;

                this.PumpUntilExit(ended, proc, gen);
            }
            catch (Exception ex)
            {
                this.RaiseConsole(gen, 1, ex.Message);
            }
            finally
            {
                this.CleanupEngine(dbg);
                if (coInit == 0 || coInit == CorDebugNative.HrFalse)
                {
                    try
                    {
                        CorDebugNative.CoUninitialize();
                    }
                    catch (Exception)
                    {
                    }
                }

                lock (this.gate)
                {
                    if (this.generation == gen)
                    {
                        this.state = DebugSessionState.Idle;
                    }

                    if (this.sessionEnded == ended)
                    {
                        this.sessionEnded = null;
                    }
                }

                this.RaiseEnded(gen);
                if (ended != null)
                {
                    try
                    {
                        ended.Close();
                    }
                    catch (ObjectDisposedException)
                    {
                    }
                }
            }
        }

        private void PumpUntilExit(ManualResetEvent ended, ICorDebugProcess proc, int workerGen)
        {
            if (ended == null)
            {
                return;
            }

            WaitHandle[] waits = new WaitHandle[] { ended, this.commandPulse };
            while (true)
            {
                int which;
                try
                {
                    which = WaitHandle.WaitAny(waits);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (AbandonedMutexException)
                {
                    break;
                }

                if (which == 0)
                {
                    break;
                }

                this.RunPendingCommand(ended, proc, workerGen);
            }
        }

        private void QueueCommand(int command)
        {
            lock (this.gate)
            {
                if (this.state != DebugSessionState.Stopped || this.disposed)
                {
                    return;
                }

                this.state = DebugSessionState.Running;
                this.pendingCommand = command;
                this.pendingCommandGen = this.generation;
            }

            try
            {
                this.commandPulse.Set();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private void RunPendingCommand(ManualResetEvent ended, ICorDebugProcess proc, int workerGen)
        {
            int cmd;
            lock (this.gate)
            {
                if (this.pendingCommandGen != workerGen)
                {
                    return;
                }

                cmd = this.pendingCommand;
                this.pendingCommand = 0;
            }

            if (cmd == CmdStop)
            {
                if (proc != null)
                {
                    try
                    {
                        proc.Terminate(1);
                    }
                    catch (Exception)
                    {
                    }

                    this.SafeContinue(proc);
                }

                if (ended != null)
                {
                    try
                    {
                        ended.Set();
                    }
                    catch (ObjectDisposedException)
                    {
                    }
                }

                return;
            }

            if (cmd == CmdContinue)
            {
                this.SafeContinue(proc);
                return;
            }

            if (cmd == CmdStepOver)
            {
                this.ResumeStep(false, proc);
                return;
            }

            if (cmd == CmdStepInto)
            {
                this.ResumeStep(true, proc);
            }
        }

        private ICorDebug CreateDebugger()
        {
            Guid clsid = CorDebugNative.ClsidClrMetaHost;
            Guid iidHost = CorDebugNative.IidIclrMetaHost;
            object hostObj;
            int hr = CorDebugNative.CLRCreateInstance(ref clsid, ref iidHost, out hostObj);
            if (!CorDebugNative.Succeeded(hr) || hostObj == null)
            {
                return null;
            }

            ICLRMetaHost host = (ICLRMetaHost)hostObj;
            Guid iidRt = CorDebugNative.IidIclrRuntimeInfo;
            object rtObj;
            hr = host.GetRuntime("v4.0.30319", ref iidRt, out rtObj);
            if (!CorDebugNative.Succeeded(hr) || rtObj == null)
            {
                return null;
            }

            ICLRRuntimeInfo rt = (ICLRRuntimeInfo)rtObj;
            Guid clsidDbg = CorDebugNative.ClsidClrDebuggingLegacy;
            Guid iidDbg = CorDebugNative.IidICorDebug;
            object dbgObj;
            hr = rt.GetInterface(ref clsidDbg, ref iidDbg, out dbgObj);
            if (!CorDebugNative.Succeeded(hr) || dbgObj == null)
            {
                return null;
            }

            ICorDebug dbg = (ICorDebug)dbgObj;
            hr = dbg.Initialize();
            if (!CorDebugNative.Succeeded(hr))
            {
                return null;
            }

            return dbg;
        }

        private bool CreateStdPipes(out IntPtr stdinChild, out IntPtr stdinParent, out IntPtr stdoutChild, out IntPtr stderrChild)
        {
            stdinChild = IntPtr.Zero;
            stdinParent = IntPtr.Zero;
            stdoutChild = IntPtr.Zero;
            stderrChild = IntPtr.Zero;
            IntPtr stdoutParent = IntPtr.Zero;
            IntPtr stderrParent = IntPtr.Zero;
            SecurityAttributes sa = new SecurityAttributes();
            sa.nLength = Marshal.SizeOf(typeof(SecurityAttributes));
            sa.bInheritHandle = 1;
            if (!CorDebugNative.CreatePipe(out stdinChild, out stdinParent, ref sa, 0))
            {
                return false;
            }

            CorDebugNative.SetHandleInformation(stdinParent, CorDebugNative.HandleFlagInherit, 0);
            if (!CorDebugNative.CreatePipe(out stdoutParent, out stdoutChild, ref sa, 0))
            {
                CloseIfSet(stdinChild);
                CloseIfSet(stdinParent);
                stdinChild = IntPtr.Zero;
                stdinParent = IntPtr.Zero;
                return false;
            }

            CorDebugNative.SetHandleInformation(stdoutParent, CorDebugNative.HandleFlagInherit, 0);
            if (!CorDebugNative.CreatePipe(out stderrParent, out stderrChild, ref sa, 0))
            {
                CloseIfSet(stdinChild);
                CloseIfSet(stdinParent);
                CloseIfSet(stdoutParent);
                CloseIfSet(stdoutChild);
                stdinChild = IntPtr.Zero;
                stdinParent = IntPtr.Zero;
                stdoutChild = IntPtr.Zero;
                return false;
            }

            CorDebugNative.SetHandleInformation(stderrParent, CorDebugNative.HandleFlagInherit, 0);
            lock (this.gate)
            {
                this.stdoutRead = stdoutParent;
                this.stderrRead = stderrParent;
            }

            return true;
        }

        private void StartPipeReader(IntPtr handle, int kind, int gen)
        {
            if (handle == IntPtr.Zero)
            {
                return;
            }

            Thread thread = new Thread(new ThreadStart(delegate
            {
                try
                {
                    using (FileStream fs = new FileStream(new SafeFileHandle(handle, true), FileAccess.Read))
                    {
                        using (StreamReader reader = new StreamReader(fs, Encoding.Default))
                        {
                            string line;
                            while ((line = reader.ReadLine()) != null)
                            {
                                this.RaiseConsole(gen, kind, line);
                            }
                        }
                    }
                }
                catch (Exception)
                {
                }
            }));
            thread.IsBackground = true;
            thread.Name = (kind == 1) ? "WindowsIDE.CorDebug.err" : "WindowsIDE.CorDebug.out";
            thread.Start();
        }

        private void PrepareCsharpBreakpoints(PdbBinder binder, int gen)
        {
            List<PendingLineBp> list = new List<PendingLineBp>();
            if (binder == null)
            {
                lock (this.gate)
                {
                    this.pendingLineBps = new PendingLineBp[0];
                }

                return;
            }

            BreakpointEntry[] entries = this.breakpoints.GetAll();
            int i = 0;
            while (i < entries.Length)
            {
                BreakpointEntry entry = entries[i];
                i++;
                if (entry == null || string.IsNullOrEmpty(entry.Path) || entry.Line < 1)
                {
                    continue;
                }

                if (!string.Equals(Path.GetExtension(entry.Path), ".cs", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                uint token;
                uint il;
                if (!binder.TryGetIlOffset(entry.Path, entry.Line, out token, out il))
                {
                    continue;
                }

                PendingLineBp item = new PendingLineBp();
                item.Token = token;
                item.IlOffset = il;
                list.Add(item);
            }

            lock (this.gate)
            {
                this.pendingLineBps = list.ToArray();
            }
        }

        private void BindPreparedBreakpoints(ICorDebugModule module, PendingLineBp[] pending)
        {
            if (module == null || pending == null)
            {
                return;
            }

            int i = 0;
            while (i < pending.Length)
            {
                PendingLineBp item = pending[i];
                i++;
                if (item == null || item.Token == 0)
                {
                    continue;
                }

                ICorDebugFunction fn;
                int hr = module.GetFunctionFromToken(item.Token, out fn);
                if (!CorDebugNative.Succeeded(hr) || fn == null)
                {
                    continue;
                }

                try
                {
                    this.CreateLineBreakpoint(fn, item.IlOffset);
                }
                finally
                {
                    ReleaseCom(fn);
                }
            }
        }

        private void CreateLineBreakpoint(ICorDebugFunction fn, uint ilOffset)
        {
            ICorDebugFunctionBreakpoint bp = null;
            ICorDebugCode ilCode = null;
            int hr = fn.GetILCode(out ilCode);
            if (CorDebugNative.Succeeded(hr) && ilCode != null)
            {
                try
                {
                    hr = ilCode.CreateBreakpoint(ilOffset, out bp);
                }
                finally
                {
                    ReleaseCom(ilCode);
                }
            }

            if (bp == null)
            {
                ICorDebugCode native = null;
                hr = fn.GetNativeCode(out native);
                if (CorDebugNative.Succeeded(hr) && native != null)
                {
                    try
                    {
                        uint nativeOff;
                        if (TryMapIlToNative(native, ilOffset, out nativeOff))
                        {
                            hr = native.CreateBreakpoint(nativeOff, out bp);
                        }
                    }
                    finally
                    {
                        ReleaseCom(native);
                    }
                }
            }

            if (bp == null)
            {
                return;
            }

            try
            {
                bp.Activate(1);
                lock (this.gate)
                {
                    this.keepAlive.Add(bp);
                    bp = null;
                }
            }
            finally
            {
                ReleaseCom(bp);
            }
        }

        private static bool TryMapIlToNative(ICorDebugCode native, uint ilOffset, out uint nativeOffset)
        {
            nativeOffset = 0;
            uint needed = 0;
            CorDebugIlToNativeMap[] probe = new CorDebugIlToNativeMap[0];
            int hr = native.GetILToNativeMapping(0, out needed, probe);
            if (!CorDebugNative.Succeeded(hr) && needed == 0)
            {
                return false;
            }

            if (needed == 0)
            {
                return false;
            }

            CorDebugIlToNativeMap[] map = new CorDebugIlToNativeMap[needed];
            uint got = 0;
            hr = native.GetILToNativeMapping(needed, out got, map);
            if (!CorDebugNative.Succeeded(hr) && hr != CorDebugNative.HrFalse)
            {
                return false;
            }

            int best = -1;
            uint i = 0;
            while (i < got)
            {
                uint il = map[i].ilOffset;
                i++;
                if (il == CorDebugNative.IlOffsetNoMapping || il == CorDebugNative.IlOffsetProlog || il == CorDebugNative.IlOffsetEpilog)
                {
                    continue;
                }

                if (il == ilOffset)
                {
                    nativeOffset = map[i - 1].nativeStartOffset;
                    return true;
                }

                if (il <= ilOffset)
                {
                    if (best < 0 || map[best].ilOffset < il)
                    {
                        best = (int)(i - 1);
                    }
                }
            }

            if (best < 0)
            {
                return false;
            }

            nativeOffset = map[best].nativeStartOffset;
            return true;
        }

        private void StopAt(ICorDebugThread thread)
        {
            int gen;
            lock (this.gate)
            {
                if (this.disposed || this.stopRequested)
                {
                    ICorDebugProcess p = this.process;
                    this.SafeContinue(p);
                    return;
                }

                gen = this.generation;
                this.state = DebugSessionState.Stopped;
                this.lastThread = thread;
            }

            string path = null;
            int line = 0;
            DebugVariable[] vars = new DebugVariable[0];
            DebugStackFrame[] frames = new DebugStackFrame[0];
            try
            {
                this.CollectStopped(thread, out path, out line, out vars, out frames);
            }
            catch (Exception)
            {
            }

            this.RaiseStopped(gen, path, line, vars, frames);
        }

        private void CollectStopped(ICorDebugThread thread, out string path, out int line, out DebugVariable[] vars, out DebugStackFrame[] frames)
        {
            path = null;
            line = 0;
            vars = new DebugVariable[0];
            frames = new DebugStackFrame[0];
            if (thread == null)
            {
                return;
            }

            ICorDebugFrame frame;
            int hr = thread.GetActiveFrame(out frame);
            ICorDebugILFrame ilFrame = null;
            if (CorDebugNative.Succeeded(hr) && frame != null)
            {
                ilFrame = frame as ICorDebugILFrame;
                if (ilFrame == null)
                {
                    try
                    {
                        ilFrame = (ICorDebugILFrame)frame;
                    }
                    catch (InvalidCastException)
                    {
                    }
                }
            }

            uint token = 0;
            uint ip = 0;
            PdbBinder binder;
            lock (this.gate)
            {
                binder = this.pdb;
            }

            if (ilFrame != null)
            {
                int map;
                ilFrame.GetIP(out ip, out map);
                ICorDebugFunction fn;
                hr = ilFrame.GetFunction(out fn);
                if (CorDebugNative.Succeeded(hr) && fn != null)
                {
                    fn.GetToken(out token);
                    ReleaseCom(fn);
                }

                if (binder != null && token != 0)
                {
                    binder.TryGetSource(token, ip, out path, out line);
                    vars = this.ReadLocals(ilFrame, binder, token, ip);
                }
            }

            frames = this.ReadStack(thread, binder);
            if (string.IsNullOrEmpty(path) && frames.Length > 0)
            {
                path = frames[0].Path;
                if (line < 1)
                {
                    line = frames[0].Line;
                }
            }
        }

        private DebugVariable[] ReadLocals(ICorDebugILFrame ilFrame, PdbBinder binder, uint token, uint ip)
        {
            PdbLocal[] pdbLocals = binder.GetLocals(token, ip);
            List<DebugVariable> list = new List<DebugVariable>();
            int i = 0;
            while (i < pdbLocals.Length)
            {
                PdbLocal loc = pdbLocals[i];
                i++;
                if (loc == null || string.IsNullOrEmpty(loc.Name))
                {
                    continue;
                }

                ICorDebugValue value;
                int hr = ilFrame.GetLocalVariable(loc.Slot, out value);
                string text = "null";
                if (CorDebugNative.Succeeded(hr) && value != null)
                {
                    try
                    {
                        text = FormatValue(value, binder);
                    }
                    finally
                    {
                        ReleaseCom(value);
                    }
                }

                list.Add(new DebugVariable(loc.Name, text));
            }

            return list.ToArray();
        }

        private DebugStackFrame[] ReadStack(ICorDebugThread thread, PdbBinder binder)
        {
            List<DebugStackFrame> list = new List<DebugStackFrame>();
            ICorDebugChainEnum chains;
            int hr = thread.EnumerateChains(out chains);
            if (!CorDebugNative.Succeeded(hr) || chains == null)
            {
                return new DebugStackFrame[0];
            }

            try
            {
                ICorDebugChain[] one = new ICorDebugChain[1];
                uint got = 0;
                while (list.Count < CorDebugNative.StackFrameLimit)
                {
                    hr = chains.Next(1, one, out got);
                    if ((!CorDebugNative.Succeeded(hr) && hr != CorDebugNative.HrFalse) || got == 0)
                    {
                        break;
                    }

                    ICorDebugChain chain = one[0];
                    try
                    {
                        int managed;
                        chain.IsManaged(out managed);
                        if (managed == 0)
                        {
                            continue;
                        }

                        this.ReadChainFrames(chain, binder, list);
                    }
                    finally
                    {
                        ReleaseCom(chain);
                    }
                }
            }
            finally
            {
                ReleaseCom(chains);
            }

            return list.ToArray();
        }

        private void ReadChainFrames(ICorDebugChain chain, PdbBinder binder, List<DebugStackFrame> list)
        {
            ICorDebugFrameEnum frames;
            int hr = chain.EnumerateFrames(out frames);
            if (!CorDebugNative.Succeeded(hr) || frames == null)
            {
                return;
            }

            try
            {
                ICorDebugFrame[] one = new ICorDebugFrame[1];
                uint got = 0;
                while (list.Count < CorDebugNative.StackFrameLimit)
                {
                    hr = frames.Next(1, one, out got);
                    if ((!CorDebugNative.Succeeded(hr) && hr != CorDebugNative.HrFalse) || got == 0)
                    {
                        break;
                    }

                    ICorDebugFrame frame = one[0];
                    try
                    {
                        ICorDebugFunction fn;
                        hr = frame.GetFunction(out fn);
                        if (!CorDebugNative.Succeeded(hr) || fn == null)
                        {
                            continue;
                        }

                        uint token;
                        fn.GetToken(out token);
                        ReleaseCom(fn);
                        string method = (binder == null) ? "" : binder.GetMethodName(token);
                        string path = null;
                        int line = 0;
                        ICorDebugILFrame ilFrame = frame as ICorDebugILFrame;
                        if (ilFrame == null)
                        {
                            try
                            {
                                ilFrame = (ICorDebugILFrame)frame;
                            }
                            catch (InvalidCastException)
                            {
                            }
                        }

                        if (ilFrame != null && binder != null)
                        {
                            uint ip;
                            int map;
                            ilFrame.GetIP(out ip, out map);
                            binder.TryGetSource(token, ip, out path, out line);
                        }

                        list.Add(new DebugStackFrame(method, path, line));
                    }
                    finally
                    {
                        ReleaseCom(frame);
                    }
                }
            }
            finally
            {
                ReleaseCom(frames);
            }
        }

        private static string FormatValue(ICorDebugValue value, PdbBinder binder)
        {
            if (value == null)
            {
                return "null";
            }

            int et = 0;
            value.GetType(out et);
            ICorDebugReferenceValue rv = TryCast<ICorDebugReferenceValue>(value);
            if (rv != null)
            {
                int isNull;
                rv.IsNull(out isNull);
                if (isNull != 0)
                {
                    return "null";
                }

                ICorDebugValue inner;
                int hr = rv.Dereference(out inner);
                if (CorDebugNative.Succeeded(hr) && inner != null)
                {
                    try
                    {
                        return FormatDeref(inner, binder, et);
                    }
                    finally
                    {
                        ReleaseCom(inner);
                    }
                }
            }

            string prim = TryReadPrimitive(value, et);
            if (prim != null)
            {
                return prim;
            }

            return TypeName(value, binder, et);
        }

        private static string FormatDeref(ICorDebugValue inner, PdbBinder binder, int outerType)
        {
            ICorDebugStringValue sv = TryCast<ICorDebugStringValue>(inner);
            if (sv != null)
            {
                return ReadString(sv);
            }

            ICorDebugBoxValue box = TryCast<ICorDebugBoxValue>(inner);
            if (box != null)
            {
                ICorDebugObjectValue boxed;
                int hr = box.GetObject(out boxed);
                if (CorDebugNative.Succeeded(hr) && boxed != null)
                {
                    try
                    {
                        int bt = 0;
                        boxed.GetType(out bt);
                        string prim = TryReadPrimitive(boxed, bt);
                        if (prim != null)
                        {
                            return prim;
                        }
                    }
                    finally
                    {
                        ReleaseCom(boxed);
                    }
                }
            }

            int et = 0;
            inner.GetType(out et);
            if (et == CorDebugNative.ElementTypeString || outerType == CorDebugNative.ElementTypeString)
            {
                sv = TryCast<ICorDebugStringValue>(inner);
                if (sv != null)
                {
                    return ReadString(sv);
                }
            }

            string p = TryReadPrimitive(inner, et);
            if (p != null)
            {
                return p;
            }

            return TypeName(inner, binder, et);
        }

        private static string TryReadPrimitive(object value, int et)
        {
            ICorDebugGenericValue gv = TryCast<ICorDebugGenericValue>(value);
            if (gv == null)
            {
                return null;
            }

            uint size = 0;
            gv.GetSize(out size);
            if (size == 0 || size > 16)
            {
                if (et != CorDebugNative.ElementTypeBoolean && et != CorDebugNative.ElementTypeChar)
                {
                    return null;
                }
            }

            IntPtr buf = Marshal.AllocHGlobal(16);
            try
            {
                int hr = gv.GetValue(buf);
                if (!CorDebugNative.Succeeded(hr))
                {
                    return null;
                }

                switch (et)
                {
                    case CorDebugNative.ElementTypeBoolean:
                        return (Marshal.ReadByte(buf) != 0) ? "True" : "False";
                    case CorDebugNative.ElementTypeI1:
                        return ((sbyte)Marshal.ReadByte(buf)).ToString();
                    case CorDebugNative.ElementTypeU1:
                        return Marshal.ReadByte(buf).ToString();
                    case CorDebugNative.ElementTypeI2:
                        return Marshal.ReadInt16(buf).ToString();
                    case CorDebugNative.ElementTypeU2:
                    case CorDebugNative.ElementTypeChar:
                        return Marshal.ReadInt16(buf).ToString();
                    case CorDebugNative.ElementTypeI4:
                    case CorDebugNative.ElementTypeI:
                        return Marshal.ReadInt32(buf).ToString();
                    case CorDebugNative.ElementTypeU4:
                    case CorDebugNative.ElementTypeU:
                        return ((uint)Marshal.ReadInt32(buf)).ToString();
                    case CorDebugNative.ElementTypeI8:
                        return Marshal.ReadInt64(buf).ToString();
                    case CorDebugNative.ElementTypeU8:
                        return ((ulong)Marshal.ReadInt64(buf)).ToString();
                    case CorDebugNative.ElementTypeR4:
                        float[] f = new float[1];
                        Marshal.Copy(buf, f, 0, 1);
                        return f[0].ToString();
                    case CorDebugNative.ElementTypeR8:
                        double[] d = new double[1];
                        Marshal.Copy(buf, d, 0, 1);
                        return d[0].ToString();
                    default:
                        return null;
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buf);
            }
        }

        private static string ReadString(ICorDebugStringValue sv)
        {
            uint len = 0;
            sv.GetLength(out len);
            uint take = len;
            if (take > CorDebugNative.StringPreviewChars)
            {
                take = CorDebugNative.StringPreviewChars;
            }

            StringBuilder sb = new StringBuilder((int)take + 1);
            uint got = 0;
            int hr = sv.GetString(take + 1, out got, sb);
            if (!CorDebugNative.Succeeded(hr))
            {
                return "";
            }

            string text = sb.ToString();
            if (text.Length > CorDebugNative.StringPreviewChars)
            {
                return text.Substring(0, CorDebugNative.StringPreviewChars);
            }

            return text;
        }

        private static string TypeName(ICorDebugValue value, PdbBinder binder, int et)
        {
            ICorDebugValue2 v2 = TryCast<ICorDebugValue2>(value);
            if (v2 != null)
            {
                ICorDebugType t;
                int hr = v2.GetExactType(out t);
                if (CorDebugNative.Succeeded(hr) && t != null)
                {
                    try
                    {
                        ICorDebugClass cls;
                        hr = t.GetClass(out cls);
                        if (CorDebugNative.Succeeded(hr) && cls != null)
                        {
                            try
                            {
                                string n = ReadTypeName(cls, binder);
                                if (!string.IsNullOrEmpty(n))
                                {
                                    return n;
                                }
                            }
                            finally
                            {
                                ReleaseCom(cls);
                            }
                        }
                    }
                    finally
                    {
                        ReleaseCom(t);
                    }
                }
            }

            return ElementTypeName(et);
        }

        private static string ReadTypeName(ICorDebugClass cls, PdbBinder binder)
        {
            if (cls == null)
            {
                return "";
            }

            uint tok;
            cls.GetToken(out tok);
            if (binder == null || tok == 0)
            {
                return "";
            }

            ICorDebugModule mod;
            int hr = cls.GetModule(out mod);
            if (!CorDebugNative.Succeeded(hr) || mod == null)
            {
                return "";
            }

            try
            {
                Guid iid = CorDebugNative.IidIMetaDataImport;
                object importer;
                hr = mod.GetMetaDataInterface(ref iid, out importer);
                IMetaDataImport import = importer as IMetaDataImport;
                if (!CorDebugNative.Succeeded(hr) || import == null)
                {
                    return "";
                }

                StringBuilder sb = new StringBuilder(260);
                uint pch;
                uint flags;
                uint extends;
                hr = import.GetTypeDefProps(tok, sb, 260, out pch, out flags, out extends);
                if (!CorDebugNative.Succeeded(hr))
                {
                    return "";
                }

                return sb.ToString();
            }
            finally
            {
                ReleaseCom(mod);
            }
        }

        private static string ElementTypeName(int et)
        {
            switch (et)
            {
                case CorDebugNative.ElementTypeString:
                    return "String";
                case CorDebugNative.ElementTypeClass:
                    return "Object";
                case CorDebugNative.ElementTypeObject:
                    return "Object";
                case CorDebugNative.ElementTypeSzArray:
                    return "Array";
                case CorDebugNative.ElementTypeValueType:
                    return "ValueType";
                default:
                    return "Object";
            }
        }

        private static T TryCast<T>(object value) where T : class
        {
            if (value == null)
            {
                return null;
            }

            T t = value as T;
            if (t != null)
            {
                return t;
            }

            try
            {
                return (T)value;
            }
            catch (InvalidCastException)
            {
                return null;
            }
            catch (COMException)
            {
                return null;
            }
        }

        private void ResumeStep(bool stepIn, ICorDebugProcess proc)
        {
            ICorDebugThread thread;
            PdbBinder binder;
            lock (this.gate)
            {
                thread = this.lastThread;
                binder = this.pdb;
                this.lastThread = null;
            }

            if (thread == null)
            {
                this.SafeContinue(proc);
                return;
            }

            try
            {
                ICorDebugStepper old;
                lock (this.gate)
                {
                    old = this.lastStepper;
                    this.lastStepper = null;
                }

                if (old != null)
                {
                    try
                    {
                        old.Deactivate();
                    }
                    catch (Exception)
                    {
                    }

                    ReleaseCom(old);
                }

                ICorDebugStepper stepper;
                int hr = thread.CreateStepper(out stepper);
                if (!CorDebugNative.Succeeded(hr) || stepper == null)
                {
                    this.SafeContinue(proc);
                    return;
                }

                lock (this.gate)
                {
                    this.lastStepper = stepper;
                }

                stepper.SetInterceptMask(0);
                stepper.SetUnmappedStopMask(0);
                stepper.SetRangeIL(1);
                bool ranged = this.TryStepRange(thread, stepper, binder, stepIn);
                if (!ranged)
                {
                    stepper.Step(stepIn ? 1 : 0);
                }
            }
            catch (Exception)
            {
            }

            this.SafeContinue(proc);
        }

        private bool TryStepRange(ICorDebugThread thread, ICorDebugStepper stepper, PdbBinder binder, bool stepIn)
        {
            if (binder == null)
            {
                return false;
            }

            ICorDebugFrame frame;
            int hr = thread.GetActiveFrame(out frame);
            if (!CorDebugNative.Succeeded(hr) || frame == null)
            {
                return false;
            }

            try
            {
                ICorDebugILFrame ilFrame = TryCast<ICorDebugILFrame>(frame);
                if (ilFrame == null)
                {
                    return false;
                }

                uint ip;
                int map;
                ilFrame.GetIP(out ip, out map);
                uint token;
                ilFrame.GetFunctionToken(out token);
                ISymUnmanagedMethod method = binder.TryGetMethod(token);
                if (method == null)
                {
                    return false;
                }

                try
                {
                    CorDebugStepRange range;
                    if (!PdbBinder.TryCurrentRange(method, ip, out range))
                    {
                        return false;
                    }

                    CorDebugStepRange[] ranges = new CorDebugStepRange[1];
                    ranges[0] = range;
                    hr = stepper.StepRange(stepIn ? 1 : 0, ranges, 1);
                    return CorDebugNative.Succeeded(hr);
                }
                finally
                {
                    ReleaseCom(method);
                }
            }
            finally
            {
                ReleaseCom(frame);
            }
        }

        private void RequestStop()
        {
            ICorDebugProcess proc;
            lock (this.gate)
            {
                if (this.state == DebugSessionState.Idle)
                {
                    return;
                }

                this.stopRequested = true;
                this.pendingCommand = CmdStop;
                this.pendingCommandGen = this.generation;
                proc = this.process;
            }

            try
            {
                this.commandPulse.Set();
            }
            catch (ObjectDisposedException)
            {
            }

            if (proc != null)
            {
                try
                {
                    proc.Terminate(1);
                }
                catch (Exception)
                {
                }

                this.SafeContinue(proc);
            }
        }

        private void CleanupEngine(ICorDebug dbg)
        {
            this.ClosePipeReads();
            ICorDebugProcess proc;
            PdbBinder binder;
            ICorDebug cor;
            List<object> alive;
            lock (this.gate)
            {
                proc = this.process;
                this.process = null;
                binder = this.pdb;
                this.pdb = null;
                cor = this.corDebug;
                this.corDebug = null;
                this.callback = null;
                this.lastThread = null;
                alive = new List<object>(this.keepAlive);
                this.keepAlive.Clear();
                ICorDebugStepper step = this.lastStepper;
                this.lastStepper = null;
                if (step != null)
                {
                    alive.Add(step);
                }
            }

            int a = 0;
            while (a < alive.Count)
            {
                ReleaseCom(alive[a]);
                a++;
            }

            if (binder != null)
            {
                binder.Dispose();
            }

            ReleaseCom(proc);
            ICorDebug use = (dbg != null) ? dbg : cor;
            if (use != null)
            {
                try
                {
                    use.Terminate();
                }
                catch (Exception)
                {
                }

                ReleaseCom(use);
            }
        }

        private void ClosePipeReads()
        {
            IntPtr outH;
            IntPtr errH;
            lock (this.gate)
            {
                outH = this.stdoutRead;
                errH = this.stderrRead;
                this.stdoutRead = IntPtr.Zero;
                this.stderrRead = IntPtr.Zero;
            }

            CloseIfSet(outH);
            CloseIfSet(errH);
        }

        private void SafeContinue(ICorDebugProcess proc)
        {
            if (proc == null)
            {
                return;
            }

            try
            {
                proc.Continue(0);
            }
            catch (Exception)
            {
            }
        }

        private void RaiseStopped(int generation, string path, int line, DebugVariable[] variables, DebugStackFrame[] frames)
        {
            EventHandler<DebugStoppedEventArgs> h = this.Stopped;
            if (h != null)
            {
                h(this, new DebugStoppedEventArgs(generation, path, line, variables, frames));
            }
        }

        private void RaiseConsole(int generation, int kind, string line)
        {
            EventHandler<DebugConsoleEventArgs> h = this.ConsoleLine;
            if (h != null)
            {
                h(this, new DebugConsoleEventArgs(generation, kind, line));
            }
        }

        private void RaiseEnded(int generation)
        {
            EventHandler<DebugEndedEventArgs> h = this.Ended;
            if (h != null)
            {
                h(this, new DebugEndedEventArgs(generation));
            }
        }

        private static string ReadModuleName(ICorDebugModule module)
        {
            uint needed = 0;
            int hr = module.GetName(0, out needed, null);
            StringBuilder sb;
            if (!CorDebugNative.Succeeded(hr) || needed == 0)
            {
                sb = new StringBuilder(260);
                hr = module.GetName(260, out needed, sb);
            }
            else
            {
                sb = new StringBuilder((int)needed);
                hr = module.GetName(needed, out needed, sb);
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

        private static void CloseIfSet(IntPtr handle)
        {
            if (handle == IntPtr.Zero)
            {
                return;
            }

            try
            {
                CorDebugNative.CloseHandle(handle);
            }
            catch (Exception)
            {
            }
        }

        private static void ReleaseCom(object com)
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

        private sealed class PendingLineBp
        {
            public uint Token;
            public uint IlOffset;
        }
    }
}
