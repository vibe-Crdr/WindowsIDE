using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Reflection;
using System.Threading;
using Microsoft.PowerShell;

namespace WindowsIDE.Debug
{
    /// <summary>
    /// ディスク上の .ps1 を同一プロセスの Runspace + SMA Debugger でデバッグする。
    /// UI スレッドでは Wait / Invoke しない。Host.PowerShell とは別。
    /// GAC SMA 3.0 に公開 SetLineBreakpoint が無いため、内部 LineBreakpoint と SetBreakpoints を使う。
    /// </summary>
    public sealed class PowerShellDebugger : IDisposable
    {
        private readonly object gate;
        private readonly BreakpointStore breakpoints;
        private readonly ManualResetEvent resumeReady;
        private DebugSessionState state;
        private int generation;
        private string scriptPath;
        private string workingDirectory;
        private DebuggerResumeAction pendingAction;
        private Runspace runspace;
        private PowerShell powershell;
        private Debugger debugger;
        private bool disposed;
        private bool stopRequested;

        /// <summary>
        /// ストアを受け取り空のセッションにする。
        /// </summary>
        /// <param name="breakpoints">行 BP。null なら空ストア。</param>
        public PowerShellDebugger(BreakpointStore breakpoints)
        {
            this.gate = new object();
            this.breakpoints = (breakpoints == null) ? new BreakpointStore() : breakpoints;
            this.resumeReady = new ManualResetEvent(false);
            this.state = DebugSessionState.Idle;
            this.pendingAction = DebuggerResumeAction.Continue;
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

        /// <summary>停止。パイプラインスレッドから。UI は BeginInvoke すること。</summary>
        public event EventHandler<DebugStoppedEventArgs> Stopped;

        /// <summary>デバッグコンソール。UI は BeginInvoke すること。</summary>
        public event EventHandler<DebugConsoleEventArgs> ConsoleLine;

        /// <summary>終了。世代付き。</summary>
        public event EventHandler<DebugEndedEventArgs> Ended;

        /// <summary>
        /// ワーカーで Runspace を開きディスクパスを Invoke する。UI で Invoke しない。
        /// </summary>
        /// <param name="path">ディスク上の .ps1。</param>
        /// <param name="cwd">作業ディレクトリ。空ならスクリプトのディレクトリ。</param>
        /// <param name="generation">古い完了を捨てるための世代。</param>
        public void Start(string path, string cwd, int generation)
        {
            this.RequestStop();
            string full = path;
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

            lock (this.gate)
            {
                this.generation = generation;
                this.scriptPath = full;
                this.workingDirectory = work;
                this.stopRequested = false;
                this.state = DebugSessionState.Running;
                this.pendingAction = DebuggerResumeAction.Continue;
                this.resumeReady.Reset();
            }

            Thread thread = new Thread(this.Worker);
            thread.IsBackground = true;
            thread.Name = "WindowsIDE.Debug";
            thread.Start(generation);
        }

        /// <summary>Stopped なら続行。Idle / Running は no-op。</summary>
        public void Continue()
        {
            this.Resume(DebuggerResumeAction.Continue);
        }

        /// <summary>Stopped ならステップ オーバー。</summary>
        public void StepOver()
        {
            this.Resume(DebuggerResumeAction.StepOver);
        }

        /// <summary>Stopped ならステップ イン。</summary>
        public void StepInto()
        {
            this.Resume(DebuggerResumeAction.StepInto);
        }

        /// <summary>
        /// 停止する。UI で Wait しない。Idle は no-op。
        /// </summary>
        public void Stop()
        {
            this.RequestStop();
        }

        /// <summary>Runspace を閉じる。UI で Wait しない。</summary>
        public void Dispose()
        {
            lock (this.gate)
            {
                this.disposed = true;
            }

            this.RequestStop();
            try
            {
                this.resumeReady.Set();
            }
            catch (ObjectDisposedException)
            {
            }

            ThreadPool.QueueUserWorkItem(delegate
            {
                this.CloseEngine();
            });

            try
            {
                this.resumeReady.Close();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private void Resume(DebuggerResumeAction action)
        {
            Debugger dbg;
            lock (this.gate)
            {
                if (this.state != DebugSessionState.Stopped)
                {
                    return;
                }

                this.pendingAction = action;
                dbg = this.debugger;
                this.state = DebugSessionState.Running;
            }

            if (dbg != null)
            {
                try
                {
                    dbg.SetDebuggerAction(action);
                }
                catch (Exception)
                {
                }
            }

            try
            {
                this.resumeReady.Set();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private void RequestStop()
        {
            Debugger dbg;
            PowerShell ps;
            DebugSessionState st;
            lock (this.gate)
            {
                st = this.state;
                if (st == DebugSessionState.Idle)
                {
                    return;
                }

                this.pendingAction = DebuggerResumeAction.Stop;
                this.stopRequested = true;
                dbg = this.debugger;
                ps = this.powershell;
            }

            if (dbg != null)
            {
                try
                {
                    dbg.SetDebuggerAction(DebuggerResumeAction.Stop);
                }
                catch (Exception)
                {
                }
            }

            try
            {
                this.resumeReady.Set();
            }
            catch (ObjectDisposedException)
            {
            }

            if (ps != null)
            {
                try
                {
                    ps.BeginStop(null, null);
                }
                catch (Exception)
                {
                }
            }

            if (st == DebugSessionState.Running)
            {
                ThreadPool.QueueUserWorkItem(delegate
                {
                    this.CloseEngine();
                });
            }
        }

        private void Worker(object state)
        {
            int gen = (int)state;
            Runspace rs = null;
            PowerShell ps = null;
            Debugger dbg = null;
            try
            {
                string path;
                string work;
                lock (this.gate)
                {
                    path = this.scriptPath;
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

                InitialSessionState iss = InitialSessionState.CreateDefault();
                iss.ExecutionPolicy = ExecutionPolicy.Bypass;
                rs = RunspaceFactory.CreateRunspace(iss);
                lock (this.gate)
                {
                    if (this.disposed || this.generation != gen || this.stopRequested)
                    {
                        rs.Dispose();
                        rs = null;
                        return;
                    }

                    this.runspace = rs;
                }

                rs.Open();
                if (!string.IsNullOrEmpty(work))
                {
                    try
                    {
                        rs.SessionStateProxy.Path.SetLocation(work);
                    }
                    catch (Exception)
                    {
                    }
                }

                dbg = rs.Debugger;
                dbg.SetDebugMode(DebugModes.LocalScript);
                dbg.DebuggerStop += this.OnDebuggerStop;
                this.ApplyBreakpoints(dbg, path);
                lock (this.gate)
                {
                    if (this.disposed || this.generation != gen || this.stopRequested)
                    {
                        return;
                    }

                    this.debugger = dbg;
                }

                ps = PowerShell.Create();
                ps.Runspace = rs;
                lock (this.gate)
                {
                    if (this.disposed || this.generation != gen || this.stopRequested)
                    {
                        return;
                    }

                    this.powershell = ps;
                }

                this.HookStreams(ps, gen);
                ps.AddCommand(path);
                lock (this.gate)
                {
                    if (this.disposed || this.generation != gen || this.stopRequested)
                    {
                        return;
                    }
                }

                PSDataCollection<PSObject> output = new PSDataCollection<PSObject>();
                output.DataAdded += delegate(object sender, DataAddedEventArgs e)
                {
                    this.OnOutputAdded(sender, e, gen);
                };
                IAsyncResult ar = ps.BeginInvoke<PSObject, PSObject>(null, output);
                try
                {
                    ps.EndInvoke(ar);
                }
                catch (PipelineStoppedException)
                {
                }
                catch (Exception ex)
                {
                    this.RaiseConsole(gen, 1, ex.Message);
                }
            }
            catch (Exception ex)
            {
                this.RaiseConsole(gen, 1, ex.Message);
            }
            finally
            {
                this.ReleaseEngine(gen, dbg, ps, rs);
                lock (this.gate)
                {
                    if (this.generation == gen)
                    {
                        this.state = DebugSessionState.Idle;
                    }
                }

                this.RaiseEnded(gen);
            }
        }

        private void ApplyBreakpoints(Debugger dbg, string startPath)
        {
            if (dbg == null)
            {
                return;
            }

            List<Breakpoint> list = new List<Breakpoint>();
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

                string bpPath = entry.Path;
                try
                {
                    bpPath = Path.GetFullPath(entry.Path);
                }
                catch (Exception)
                {
                }

                if (!string.IsNullOrEmpty(startPath) && string.Equals(bpPath, startPath, StringComparison.OrdinalIgnoreCase))
                {
                    bpPath = startPath;
                }

                LineBreakpoint lineBp = CreateLineBreakpoint(bpPath, entry.Line);
                if (lineBp != null)
                {
                    list.Add(lineBp);
                }
            }

            if (list.Count == 0)
            {
                return;
            }

            try
            {
                dbg.SetBreakpoints(list);
            }
            catch (Exception)
            {
            }
        }

        private static LineBreakpoint CreateLineBreakpoint(string path, int line)
        {
            ConstructorInfo ctor = typeof(LineBreakpoint).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new Type[] { typeof(string), typeof(int), typeof(ScriptBlock) },
                null);
            if (ctor == null)
            {
                return null;
            }

            try
            {
                return (LineBreakpoint)ctor.Invoke(new object[] { path, line, null });
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void OnDebuggerStop(object sender, DebuggerStopEventArgs e)
        {
            int gen;
            bool stale;
            lock (this.gate)
            {
                gen = this.generation;
                stale = this.disposed;
            }

            if (e == null)
            {
                return;
            }

            if (stale)
            {
                e.ResumeAction = DebuggerResumeAction.Stop;
                return;
            }

            string script = null;
            int line1 = 0;
            if (e.InvocationInfo != null)
            {
                script = e.InvocationInfo.ScriptName;
                line1 = e.InvocationInfo.ScriptLineNumber;
            }

            Debugger dbg = sender as Debugger;
            DebugVariable[] vars = this.CollectVariables(dbg);
            lock (this.gate)
            {
                if (this.generation != gen || this.disposed)
                {
                    e.ResumeAction = DebuggerResumeAction.Stop;
                    return;
                }

                this.state = DebugSessionState.Stopped;
                this.pendingAction = DebuggerResumeAction.Continue;
            }

            try
            {
                this.resumeReady.Reset();
            }
            catch (ObjectDisposedException)
            {
                e.ResumeAction = DebuggerResumeAction.Stop;
                return;
            }

            this.RaiseStopped(gen, script, line1, vars);
            try
            {
                this.resumeReady.WaitOne();
            }
            catch (ObjectDisposedException)
            {
                e.ResumeAction = DebuggerResumeAction.Stop;
                return;
            }

            DebuggerResumeAction action;
            lock (this.gate)
            {
                action = this.pendingAction;
                if (this.generation != gen || this.disposed)
                {
                    action = DebuggerResumeAction.Stop;
                }
                else if (action != DebuggerResumeAction.Stop)
                {
                    this.state = DebugSessionState.Running;
                }
            }

            e.ResumeAction = action;
        }

        private DebugVariable[] CollectVariables(Debugger dbg)
        {
            if (dbg == null)
            {
                return new DebugVariable[0];
            }

            try
            {
                PSCommand cmd = new PSCommand();
                cmd.AddCommand("Get-Variable");
                PSDataCollection<PSObject> output = new PSDataCollection<PSObject>();
                dbg.ProcessCommand(cmd, output);
                List<DebugVariable> list = new List<DebugVariable>();
                int i = 0;
                while (i < output.Count)
                {
                    PSObject o = output[i];
                    i++;
                    string name = null;
                    object value = null;
                    if (o != null)
                    {
                        PSVariable pv = o.BaseObject as PSVariable;
                        if (pv != null)
                        {
                            name = pv.Name;
                            value = pv.Value;
                        }
                        else
                        {
                            if (o.Properties["Name"] != null && o.Properties["Name"].Value != null)
                            {
                                name = o.Properties["Name"].Value.ToString();
                            }

                            if (o.Properties["Value"] != null)
                            {
                                value = o.Properties["Value"].Value;
                            }
                        }
                    }

                    if (string.IsNullOrEmpty(name))
                    {
                        continue;
                    }

                    list.Add(new DebugVariable(name, FormatValue(value)));
                }

                return list.ToArray();
            }
            catch (Exception)
            {
                return new DebugVariable[0];
            }
        }

        private static string FormatValue(object value)
        {
            if (value == null)
            {
                return "$null";
            }

            string text;
            try
            {
                text = value.ToString();
            }
            catch (Exception)
            {
                return "";
            }

            if (text == null)
            {
                return "";
            }

            if (text.Length > 256)
            {
                return text.Substring(0, 256);
            }

            return text;
        }

        private void HookStreams(PowerShell ps, int gen)
        {
            ps.Streams.Error.DataAdded += delegate(object sender, DataAddedEventArgs e)
            {
                PSDataCollection<ErrorRecord> col = sender as PSDataCollection<ErrorRecord>;
                if (col == null || e.Index < 0 || e.Index >= col.Count)
                {
                    return;
                }

                ErrorRecord rec = col[e.Index];
                string text = (rec == null) ? "" : rec.ToString();
                this.RaiseConsole(gen, 1, text);
            };
            ps.Streams.Warning.DataAdded += delegate(object sender, DataAddedEventArgs e)
            {
                this.RaiseStreamMessage(sender as PSDataCollection<WarningRecord>, e.Index, gen, 2);
            };
            ps.Streams.Verbose.DataAdded += delegate(object sender, DataAddedEventArgs e)
            {
                this.RaiseStreamMessage(sender as PSDataCollection<VerboseRecord>, e.Index, gen, 2);
            };
            ps.Streams.Debug.DataAdded += delegate(object sender, DataAddedEventArgs e)
            {
                this.RaiseStreamMessage(sender as PSDataCollection<DebugRecord>, e.Index, gen, 2);
            };
            ps.Streams.Information.DataAdded += delegate(object sender, DataAddedEventArgs e)
            {
                PSDataCollection<InformationRecord> col = sender as PSDataCollection<InformationRecord>;
                if (col == null || e.Index < 0 || e.Index >= col.Count)
                {
                    return;
                }

                InformationRecord rec = col[e.Index];
                if (rec == null || rec.MessageData == null)
                {
                    return;
                }

                this.RaiseConsole(gen, 0, rec.MessageData.ToString());
            };
        }

        private void RaiseStreamMessage<T>(PSDataCollection<T> col, int index, int gen, int kind) where T : InformationalRecord
        {
            if (col == null || index < 0 || index >= col.Count)
            {
                return;
            }

            InformationalRecord rec = col[index];
            string text = (rec == null) ? "" : rec.Message;
            this.RaiseConsole(gen, kind, text);
        }

        private void OnOutputAdded(object sender, DataAddedEventArgs e, int gen)
        {
            PSDataCollection<PSObject> col = sender as PSDataCollection<PSObject>;
            if (col == null || e.Index < 0 || e.Index >= col.Count)
            {
                return;
            }

            PSObject o = col[e.Index];
            string text = (o == null) ? "" : o.ToString();
            this.RaiseConsole(gen, 0, text);
        }

        private void CloseEngine()
        {
            Debugger dbg;
            PowerShell ps;
            Runspace rs;
            lock (this.gate)
            {
                dbg = this.debugger;
                this.debugger = null;
                ps = this.powershell;
                this.powershell = null;
                rs = this.runspace;
                this.runspace = null;
            }

            this.DisposeEngine(dbg, ps, rs);
        }

        private void ReleaseEngine(int gen, Debugger dbg, PowerShell ps, Runspace rs)
        {
            lock (this.gate)
            {
                if (object.ReferenceEquals(this.debugger, dbg))
                {
                    this.debugger = null;
                }

                if (object.ReferenceEquals(this.powershell, ps))
                {
                    this.powershell = null;
                }

                if (object.ReferenceEquals(this.runspace, rs))
                {
                    this.runspace = null;
                }
            }

            this.DisposeEngine(dbg, ps, rs);
        }

        private void DisposeEngine(Debugger dbg, PowerShell ps, Runspace rs)
        {
            if (dbg != null)
            {
                try
                {
                    dbg.DebuggerStop -= this.OnDebuggerStop;
                }
                catch (Exception)
                {
                }
            }

            if (ps != null)
            {
                try
                {
                    ps.Dispose();
                }
                catch (Exception)
                {
                }
            }

            if (rs != null)
            {
                try
                {
                    rs.Close();
                }
                catch (Exception)
                {
                }

                try
                {
                    rs.Dispose();
                }
                catch (Exception)
                {
                }
            }
        }

        private void RaiseStopped(int generation, string path, int line, DebugVariable[] variables)
        {
            EventHandler<DebugStoppedEventArgs> h = this.Stopped;
            if (h != null)
            {
                h(this, new DebugStoppedEventArgs(generation, path, line, variables));
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
    }
}
