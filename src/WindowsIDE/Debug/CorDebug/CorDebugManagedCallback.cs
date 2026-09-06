using System;
using System.Runtime.InteropServices;

namespace WindowsIDE.Debug
{
    /// <summary>
    /// ICorDebug のマネージドコールバック。WinForms は参照しない。
    /// </summary>
    internal interface ICorDebugCallbackSink
    {
        void HandleBreakpoint(ICorDebugAppDomain appDomain, ICorDebugThread thread);

        void HandleStepComplete(ICorDebugAppDomain appDomain, ICorDebugThread thread);

        void HandleUnhandledException(ICorDebugAppDomain appDomain, ICorDebugThread thread);

        void HandleLoadModule(ICorDebugAppDomain appDomain, ICorDebugModule module);

        void HandleCreateProcess(ICorDebugProcess process);

        void HandleExitProcess(int generation);

        bool IsCallbackAlive { get; }
    }

    [ComImport]
    [Guid("3D6F5F60-7538-11D3-8D5B-00104B35E7EF")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ICorDebugManagedCallback
    {
        [PreserveSig]
        int Breakpoint(
            [MarshalAs(UnmanagedType.Interface)] ICorDebugAppDomain pAppDomain,
            [MarshalAs(UnmanagedType.Interface)] ICorDebugThread pThread,
            [MarshalAs(UnmanagedType.Interface)] ICorDebugBreakpoint pBreakpoint);

        [PreserveSig]
        int StepComplete(
            [MarshalAs(UnmanagedType.Interface)] ICorDebugAppDomain pAppDomain,
            [MarshalAs(UnmanagedType.Interface)] ICorDebugThread pThread,
            [MarshalAs(UnmanagedType.Interface)] ICorDebugStepper pStepper,
            int reason);

        [PreserveSig]
        int Break(
            [MarshalAs(UnmanagedType.Interface)] ICorDebugAppDomain pAppDomain,
            [MarshalAs(UnmanagedType.Interface)] ICorDebugThread thread);

        [PreserveSig]
        int Exception(
            [MarshalAs(UnmanagedType.Interface)] ICorDebugAppDomain pAppDomain,
            [MarshalAs(UnmanagedType.Interface)] ICorDebugThread pThread,
            int unhandled);

        [PreserveSig]
        int EvalComplete(
            [MarshalAs(UnmanagedType.Interface)] ICorDebugAppDomain pAppDomain,
            [MarshalAs(UnmanagedType.Interface)] ICorDebugThread pThread,
            [MarshalAs(UnmanagedType.Interface)] ICorDebugEval pEval);

        [PreserveSig]
        int EvalException(
            [MarshalAs(UnmanagedType.Interface)] ICorDebugAppDomain pAppDomain,
            [MarshalAs(UnmanagedType.Interface)] ICorDebugThread pThread,
            [MarshalAs(UnmanagedType.Interface)] ICorDebugEval pEval);

        [PreserveSig]
        int CreateProcess([MarshalAs(UnmanagedType.Interface)] ICorDebugProcess pProcess);

        [PreserveSig]
        int ExitProcess([MarshalAs(UnmanagedType.Interface)] ICorDebugProcess pProcess);

        [PreserveSig]
        int CreateThread(
            [MarshalAs(UnmanagedType.Interface)] ICorDebugAppDomain pAppDomain,
            [MarshalAs(UnmanagedType.Interface)] ICorDebugThread thread);

        [PreserveSig]
        int ExitThread(
            [MarshalAs(UnmanagedType.Interface)] ICorDebugAppDomain pAppDomain,
            [MarshalAs(UnmanagedType.Interface)] ICorDebugThread thread);

        [PreserveSig]
        int LoadModule(
            [MarshalAs(UnmanagedType.Interface)] ICorDebugAppDomain pAppDomain,
            [MarshalAs(UnmanagedType.Interface)] ICorDebugModule pModule);

        [PreserveSig]
        int UnloadModule(
            [MarshalAs(UnmanagedType.Interface)] ICorDebugAppDomain pAppDomain,
            [MarshalAs(UnmanagedType.Interface)] ICorDebugModule pModule);

        [PreserveSig]
        int LoadClass(
            [MarshalAs(UnmanagedType.Interface)] ICorDebugAppDomain pAppDomain,
            [MarshalAs(UnmanagedType.Interface)] ICorDebugClass c);

        [PreserveSig]
        int UnloadClass(
            [MarshalAs(UnmanagedType.Interface)] ICorDebugAppDomain pAppDomain,
            [MarshalAs(UnmanagedType.Interface)] ICorDebugClass c);

        [PreserveSig]
        int DebuggerError(
            [MarshalAs(UnmanagedType.Interface)] ICorDebugProcess pProcess,
            int errorHR,
            uint errorCode);

        [PreserveSig]
        int LogMessage(
            [MarshalAs(UnmanagedType.Interface)] ICorDebugAppDomain pAppDomain,
            [MarshalAs(UnmanagedType.Interface)] ICorDebugThread pThread,
            int lLevel,
            [MarshalAs(UnmanagedType.LPWStr)] string pLogSwitchName,
            [MarshalAs(UnmanagedType.LPWStr)] string pMessage);

        [PreserveSig]
        int LogSwitch(
            [MarshalAs(UnmanagedType.Interface)] ICorDebugAppDomain pAppDomain,
            [MarshalAs(UnmanagedType.Interface)] ICorDebugThread pThread,
            int lLevel,
            uint ulReason,
            [MarshalAs(UnmanagedType.LPWStr)] string pLogSwitchName,
            [MarshalAs(UnmanagedType.LPWStr)] string pParentName);

        [PreserveSig]
        int CreateAppDomain(
            [MarshalAs(UnmanagedType.Interface)] ICorDebugProcess pProcess,
            [MarshalAs(UnmanagedType.Interface)] ICorDebugAppDomain pAppDomain);

        [PreserveSig]
        int ExitAppDomain(
            [MarshalAs(UnmanagedType.Interface)] ICorDebugProcess pProcess,
            [MarshalAs(UnmanagedType.Interface)] ICorDebugAppDomain pAppDomain);

        [PreserveSig]
        int LoadAssembly(
            [MarshalAs(UnmanagedType.Interface)] ICorDebugAppDomain pAppDomain,
            [MarshalAs(UnmanagedType.Interface)] ICorDebugAssembly pAssembly);

        [PreserveSig]
        int UnloadAssembly(
            [MarshalAs(UnmanagedType.Interface)] ICorDebugAppDomain pAppDomain,
            [MarshalAs(UnmanagedType.Interface)] ICorDebugAssembly pAssembly);

        [PreserveSig]
        int ControlCTrap([MarshalAs(UnmanagedType.Interface)] ICorDebugProcess pProcess);

        [PreserveSig]
        int NameChange(
            [MarshalAs(UnmanagedType.Interface)] ICorDebugAppDomain pAppDomain,
            [MarshalAs(UnmanagedType.Interface)] ICorDebugThread pThread);

        [PreserveSig]
        int UpdateModuleSymbols(
            [MarshalAs(UnmanagedType.Interface)] ICorDebugAppDomain pAppDomain,
            [MarshalAs(UnmanagedType.Interface)] ICorDebugModule pModule,
            [MarshalAs(UnmanagedType.Interface)] ICorDebugStream pSymbolStream);

        [PreserveSig]
        int EditAndContinueRemap(
            [MarshalAs(UnmanagedType.Interface)] ICorDebugAppDomain pAppDomain,
            [MarshalAs(UnmanagedType.Interface)] ICorDebugThread pThread,
            [MarshalAs(UnmanagedType.Interface)] ICorDebugFunction pFunction,
            int fAccurate);

        [PreserveSig]
        int BreakpointSetError(
            [MarshalAs(UnmanagedType.Interface)] ICorDebugAppDomain pAppDomain,
            [MarshalAs(UnmanagedType.Interface)] ICorDebugThread pThread,
            [MarshalAs(UnmanagedType.Interface)] ICorDebugBreakpoint pBreakpoint,
            uint dwError);
    }

    [ComImport]
    [Guid("250E5EEA-DB5C-4C76-B6F3-8C46F12E3203")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ICorDebugManagedCallback2
    {
        [PreserveSig]
        int FunctionRemapOpportunity(
            [MarshalAs(UnmanagedType.Interface)] ICorDebugAppDomain pAppDomain,
            [MarshalAs(UnmanagedType.Interface)] ICorDebugThread pThread,
            [MarshalAs(UnmanagedType.Interface)] ICorDebugFunction pOldFunction,
            [MarshalAs(UnmanagedType.Interface)] ICorDebugFunction pNewFunction,
            uint oldILOffset);

        [PreserveSig]
        int CreateConnection(
            [MarshalAs(UnmanagedType.Interface)] ICorDebugProcess pProcess,
            uint dwConnectionId,
            [MarshalAs(UnmanagedType.LPWStr)] string pConnName);

        [PreserveSig]
        int ChangeConnection(
            [MarshalAs(UnmanagedType.Interface)] ICorDebugProcess pProcess,
            uint dwConnectionId);

        [PreserveSig]
        int DestroyConnection(
            [MarshalAs(UnmanagedType.Interface)] ICorDebugProcess pProcess,
            uint dwConnectionId);

        [PreserveSig]
        int Exception(
            [MarshalAs(UnmanagedType.Interface)] ICorDebugAppDomain pAppDomain,
            [MarshalAs(UnmanagedType.Interface)] ICorDebugThread pThread,
            [MarshalAs(UnmanagedType.Interface)] ICorDebugFrame pFrame,
            uint nOffset,
            int dwEventType,
            uint dwFlags);

        [PreserveSig]
        int ExceptionUnwind(
            [MarshalAs(UnmanagedType.Interface)] ICorDebugAppDomain pAppDomain,
            [MarshalAs(UnmanagedType.Interface)] ICorDebugThread pThread,
            int dwEventType,
            uint dwFlags);

        [PreserveSig]
        int FunctionRemapComplete(
            [MarshalAs(UnmanagedType.Interface)] ICorDebugAppDomain pAppDomain,
            [MarshalAs(UnmanagedType.Interface)] ICorDebugThread pThread,
            [MarshalAs(UnmanagedType.Interface)] ICorDebugFunction pFunction);

        [PreserveSig]
        int MDANotification(
            [MarshalAs(UnmanagedType.Interface)] ICorDebugController pController,
            [MarshalAs(UnmanagedType.Interface)] ICorDebugThread pThread,
            [MarshalAs(UnmanagedType.Interface)] ICorDebugMDA pMDA);
    }

    /// <summary>
    /// ICorDebugManagedCallback と Callback2。コールバックから COM を UI に渡さない。
    /// </summary>
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    internal sealed class CorDebugManagedCallback : ICorDebugManagedCallback, ICorDebugManagedCallback2
    {
        private ICorDebugCallbackSink sink;
        private int generation;

        /// <summary>
        /// セッションへイベントを渡す。
        /// </summary>
        /// <param name="sink">セッション。null なら無視して Continue。</param>
        /// <param name="generation">このワーカーの世代。ExitProcess の待ちを取り違えない。</param>
        public CorDebugManagedCallback(ICorDebugCallbackSink sink, int generation)
        {
            this.sink = sink;
            this.generation = generation;
        }

        public int Breakpoint(ICorDebugAppDomain pAppDomain, ICorDebugThread pThread, ICorDebugBreakpoint pBreakpoint)
        {
            try
            {
                if (this.IsAlive())
                {
                    this.sink.HandleBreakpoint(pAppDomain, pThread);
                    return CorDebugNative.HrOk;
                }
            }
            catch (Exception)
            {
            }

            ContinueApp(pAppDomain);
            return CorDebugNative.HrOk;
        }

        public int StepComplete(ICorDebugAppDomain pAppDomain, ICorDebugThread pThread, ICorDebugStepper pStepper, int reason)
        {
            try
            {
                if (this.IsAlive())
                {
                    this.sink.HandleStepComplete(pAppDomain, pThread);
                    return CorDebugNative.HrOk;
                }
            }
            catch (Exception)
            {
            }

            ContinueApp(pAppDomain);
            return CorDebugNative.HrOk;
        }

        public int Break(ICorDebugAppDomain pAppDomain, ICorDebugThread thread)
        {
            ContinueApp(pAppDomain);
            return CorDebugNative.HrOk;
        }

        public int Exception(ICorDebugAppDomain pAppDomain, ICorDebugThread pThread, int unhandled)
        {
            try
            {
                if (unhandled != 0 && this.IsAlive())
                {
                    this.sink.HandleUnhandledException(pAppDomain, pThread);
                    return CorDebugNative.HrOk;
                }
            }
            catch (Exception)
            {
            }

            ContinueApp(pAppDomain);
            return CorDebugNative.HrOk;
        }

        public int EvalComplete(ICorDebugAppDomain pAppDomain, ICorDebugThread pThread, ICorDebugEval pEval)
        {
            ContinueApp(pAppDomain);
            return CorDebugNative.HrOk;
        }

        public int EvalException(ICorDebugAppDomain pAppDomain, ICorDebugThread pThread, ICorDebugEval pEval)
        {
            ContinueApp(pAppDomain);
            return CorDebugNative.HrOk;
        }

        public int CreateProcess(ICorDebugProcess pProcess)
        {
            try
            {
                if (this.IsAlive())
                {
                    this.sink.HandleCreateProcess(pProcess);
                }
            }
            catch (Exception)
            {
            }

            ContinueProcess(pProcess);
            return CorDebugNative.HrOk;
        }

        public int ExitProcess(ICorDebugProcess pProcess)
        {
            try
            {
                if (this.sink != null)
                {
                    this.sink.HandleExitProcess(this.generation);
                }
            }
            catch (Exception)
            {
            }

            return CorDebugNative.HrOk;
        }

        public int CreateThread(ICorDebugAppDomain pAppDomain, ICorDebugThread thread)
        {
            ContinueApp(pAppDomain);
            return CorDebugNative.HrOk;
        }

        public int ExitThread(ICorDebugAppDomain pAppDomain, ICorDebugThread thread)
        {
            ContinueApp(pAppDomain);
            return CorDebugNative.HrOk;
        }

        public int LoadModule(ICorDebugAppDomain pAppDomain, ICorDebugModule pModule)
        {
            try
            {
                if (this.IsAlive())
                {
                    this.sink.HandleLoadModule(pAppDomain, pModule);
                }
            }
            catch (Exception)
            {
            }

            ContinueApp(pAppDomain);
            return CorDebugNative.HrOk;
        }

        public int UnloadModule(ICorDebugAppDomain pAppDomain, ICorDebugModule pModule)
        {
            ContinueApp(pAppDomain);
            return CorDebugNative.HrOk;
        }

        public int LoadClass(ICorDebugAppDomain pAppDomain, ICorDebugClass c)
        {
            ContinueApp(pAppDomain);
            return CorDebugNative.HrOk;
        }

        public int UnloadClass(ICorDebugAppDomain pAppDomain, ICorDebugClass c)
        {
            ContinueApp(pAppDomain);
            return CorDebugNative.HrOk;
        }

        public int DebuggerError(ICorDebugProcess pProcess, int errorHR, uint errorCode)
        {
            ContinueProcess(pProcess);
            return CorDebugNative.HrOk;
        }

        public int LogMessage(ICorDebugAppDomain pAppDomain, ICorDebugThread pThread, int lLevel, string pLogSwitchName, string pMessage)
        {
            ContinueApp(pAppDomain);
            return CorDebugNative.HrOk;
        }

        public int LogSwitch(ICorDebugAppDomain pAppDomain, ICorDebugThread pThread, int lLevel, uint ulReason, string pLogSwitchName, string pParentName)
        {
            ContinueApp(pAppDomain);
            return CorDebugNative.HrOk;
        }

        public int CreateAppDomain(ICorDebugProcess pProcess, ICorDebugAppDomain pAppDomain)
        {
            try
            {
                if (pAppDomain != null)
                {
                    pAppDomain.Attach();
                }
            }
            catch (Exception)
            {
            }

            ContinueApp(pAppDomain);
            return CorDebugNative.HrOk;
        }

        public int ExitAppDomain(ICorDebugProcess pProcess, ICorDebugAppDomain pAppDomain)
        {
            ContinueProcess(pProcess);
            return CorDebugNative.HrOk;
        }

        public int LoadAssembly(ICorDebugAppDomain pAppDomain, ICorDebugAssembly pAssembly)
        {
            ContinueApp(pAppDomain);
            return CorDebugNative.HrOk;
        }

        public int UnloadAssembly(ICorDebugAppDomain pAppDomain, ICorDebugAssembly pAssembly)
        {
            ContinueApp(pAppDomain);
            return CorDebugNative.HrOk;
        }

        public int ControlCTrap(ICorDebugProcess pProcess)
        {
            ContinueProcess(pProcess);
            return CorDebugNative.HrOk;
        }

        public int NameChange(ICorDebugAppDomain pAppDomain, ICorDebugThread pThread)
        {
            ContinueApp(pAppDomain);
            return CorDebugNative.HrOk;
        }

        public int UpdateModuleSymbols(ICorDebugAppDomain pAppDomain, ICorDebugModule pModule, ICorDebugStream pSymbolStream)
        {
            ContinueApp(pAppDomain);
            return CorDebugNative.HrOk;
        }

        public int EditAndContinueRemap(ICorDebugAppDomain pAppDomain, ICorDebugThread pThread, ICorDebugFunction pFunction, int fAccurate)
        {
            ContinueApp(pAppDomain);
            return CorDebugNative.HrOk;
        }

        public int BreakpointSetError(ICorDebugAppDomain pAppDomain, ICorDebugThread pThread, ICorDebugBreakpoint pBreakpoint, uint dwError)
        {
            ContinueApp(pAppDomain);
            return CorDebugNative.HrOk;
        }

        public int FunctionRemapOpportunity(ICorDebugAppDomain pAppDomain, ICorDebugThread pThread, ICorDebugFunction pOldFunction, ICorDebugFunction pNewFunction, uint oldILOffset)
        {
            ContinueApp(pAppDomain);
            return CorDebugNative.HrOk;
        }

        public int CreateConnection(ICorDebugProcess pProcess, uint dwConnectionId, string pConnName)
        {
            ContinueProcess(pProcess);
            return CorDebugNative.HrOk;
        }

        public int ChangeConnection(ICorDebugProcess pProcess, uint dwConnectionId)
        {
            ContinueProcess(pProcess);
            return CorDebugNative.HrOk;
        }

        public int DestroyConnection(ICorDebugProcess pProcess, uint dwConnectionId)
        {
            ContinueProcess(pProcess);
            return CorDebugNative.HrOk;
        }

        int ICorDebugManagedCallback2.Exception(ICorDebugAppDomain pAppDomain, ICorDebugThread pThread, ICorDebugFrame pFrame, uint nOffset, int dwEventType, uint dwFlags)
        {
            ContinueApp(pAppDomain);
            return CorDebugNative.HrOk;
        }

        public int ExceptionUnwind(ICorDebugAppDomain pAppDomain, ICorDebugThread pThread, int dwEventType, uint dwFlags)
        {
            ContinueApp(pAppDomain);
            return CorDebugNative.HrOk;
        }

        public int FunctionRemapComplete(ICorDebugAppDomain pAppDomain, ICorDebugThread pThread, ICorDebugFunction pFunction)
        {
            ContinueApp(pAppDomain);
            return CorDebugNative.HrOk;
        }

        public int MDANotification(ICorDebugController pController, ICorDebugThread pThread, ICorDebugMDA pMDA)
        {
            try
            {
                if (pController != null)
                {
                    pController.Continue(0);
                }
            }
            catch (Exception)
            {
            }

            return CorDebugNative.HrOk;
        }

        private bool IsAlive()
        {
            return this.sink != null && this.sink.IsCallbackAlive;
        }

        private static void ContinueApp(ICorDebugAppDomain app)
        {
            if (app == null)
            {
                return;
            }

            try
            {
                app.Continue(0);
            }
            catch (Exception)
            {
            }
        }

        private static void ContinueProcess(ICorDebugProcess process)
        {
            if (process == null)
            {
                return;
            }

            try
            {
                process.Continue(0);
            }
            catch (Exception)
            {
            }
        }
    }
}
