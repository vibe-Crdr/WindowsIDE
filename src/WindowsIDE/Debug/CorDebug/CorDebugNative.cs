using System;
using System.Runtime.InteropServices;
using System.Text;

namespace WindowsIDE.Debug
{
    /// <summary>
    /// ICorDebug / ICLRMetaHost / ISymUnmanaged の P/Invoke と [ComImport]。製品 /r は増やさない。
    /// </summary>
    internal static class CorDebugNative
    {
        internal const uint ClsctxInprocServer = 1;
        internal const uint CoinitMultithreaded = 0;
        internal const uint CreateNoWindow = 0x08000000;
        internal const uint DebugOnlyThisProcess = 0x00000002;
        internal const uint StartfUseStdHandles = 0x00000100;
        internal const uint StartfUseShowWindow = 0x00000001;
        internal const uint HandleFlagInherit = 0x00000001;
        internal const int HrOk = 0;
        internal const int HrFalse = 1;
        internal const uint HiddenSequencePoint = 0x00FEEFEE;
        internal const uint IlOffsetNoMapping = 0xFFFFFFFF;
        internal const uint IlOffsetProlog = 0xFFFFFFFE;
        internal const uint IlOffsetEpilog = 0xFFFFFFFD;
        internal const int ElementTypeBoolean = 0x02;
        internal const int ElementTypeChar = 0x03;
        internal const int ElementTypeI1 = 0x04;
        internal const int ElementTypeU1 = 0x05;
        internal const int ElementTypeI2 = 0x06;
        internal const int ElementTypeU2 = 0x07;
        internal const int ElementTypeI4 = 0x08;
        internal const int ElementTypeU4 = 0x09;
        internal const int ElementTypeI8 = 0x0A;
        internal const int ElementTypeU8 = 0x0B;
        internal const int ElementTypeR4 = 0x0C;
        internal const int ElementTypeR8 = 0x0D;
        internal const int ElementTypeString = 0x0E;
        internal const int ElementTypeI = 0x18;
        internal const int ElementTypeU = 0x19;
        internal const int ElementTypeClass = 0x12;
        internal const int ElementTypeObject = 0x1C;
        internal const int ElementTypeSzArray = 0x1D;
        internal const int ElementTypeValueType = 0x11;
        internal const uint SymAddrIlOffset = 1;
        internal const int StackFrameLimit = 32;
        internal const int StringPreviewChars = 256;

        internal static readonly Guid ClsidClrMetaHost = new Guid("9280188D-0E8E-4867-B30C-7FA83884E8DE");
        internal static readonly Guid IidIclrMetaHost = new Guid("D332DB9E-B9B3-4125-8207-A14884F53216");
        internal static readonly Guid IidIclrRuntimeInfo = new Guid("BD39D1D2-BA2F-486a-89B0-B4B0CB466891");
        internal static readonly Guid ClsidClrDebuggingLegacy = new Guid("DF8395B5-A4BA-450B-A77C-A9A47762C520");
        internal static readonly Guid IidICorDebug = new Guid("3D6F5F61-7538-11D3-8D5B-00104B35E7EF");
        internal static readonly Guid ClsidCorSymBinderSxS = new Guid("0A29FF9E-7F9C-4437-8B11-F424491E3931");
        internal static readonly Guid IidISymUnmanagedBinder = new Guid("AA544D42-28CB-11d3-BD22-0000F80849BD");
        internal static readonly Guid IidIMetaDataImport = new Guid("7DAC8207-D3AE-4c75-9B67-92801A497D44");
        internal static readonly Guid ClsidCorMetaDataDispenser = new Guid("E5CB7A31-7512-11d2-89CE-0080C792E5D8");
        internal static readonly Guid ClsidCorMetaDataDispenserRuntime = new Guid("1EC2DE53-75CC-11d2-9775-00A0C9B4D50C");
        internal static readonly Guid IidIMetaDataDispenser = new Guid("809C652E-7396-11D2-9771-00A0C9B4D50C");
        internal const uint MetaDataOpenRead = 0;

        [DllImport("mscoree.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        internal static extern int CLRCreateInstance(
            [In] ref Guid clsid,
            [In] ref Guid iid,
            [MarshalAs(UnmanagedType.Interface)] out object ppInterface);

        [DllImport("ole32.dll", ExactSpelling = true, PreserveSig = true)]
        internal static extern int CoInitializeEx(IntPtr pvReserved, uint dwCoInit);

        [DllImport("ole32.dll", ExactSpelling = true)]
        internal static extern void CoUninitialize();

        [DllImport("ole32.dll", ExactSpelling = true, PreserveSig = true)]
        internal static extern int CoCreateInstance(
            [In] ref Guid rclsid,
            IntPtr pUnkOuter,
            uint dwClsContext,
            [In] ref Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out object ppv);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CreatePipe(
            out IntPtr hReadPipe,
            out IntPtr hWritePipe,
            ref SecurityAttributes lpPipeAttributes,
            uint nSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetHandleInformation(IntPtr hObject, uint dwMask, uint dwFlags);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CloseHandle(IntPtr hObject);

        internal static bool Succeeded(int hr)
        {
            return hr >= 0;
        }

        internal static string ReadWideName(GetNameCallback getter)
        {
            if (getter == null)
            {
                return "";
            }

            uint needed = 0;
            int hr = getter(0, out needed, null);
            if (!Succeeded(hr) || needed == 0)
            {
                StringBuilder small = new StringBuilder(260);
                hr = getter(260, out needed, small);
                if (!Succeeded(hr))
                {
                    return "";
                }

                return small.ToString();
            }

            StringBuilder sb = new StringBuilder((int)needed);
            hr = getter(needed, out needed, sb);
            if (!Succeeded(hr))
            {
                return "";
            }

            return sb.ToString();
        }

        internal delegate int GetNameCallback(uint cchName, out uint pcchName, StringBuilder szName);
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SecurityAttributes
    {
        public int nLength;
        public IntPtr lpSecurityDescriptor;
        public int bInheritHandle;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8, CharSet = CharSet.Unicode)]
    internal struct CorDebugStartupInfo
    {
        public int cb;
        public IntPtr lpReserved;
        public IntPtr lpDesktop;
        public IntPtr lpTitle;
        public int dwX;
        public int dwY;
        public int dwXSize;
        public int dwYSize;
        public int dwXCountChars;
        public int dwYCountChars;
        public int dwFillAttribute;
        public int dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct CorDebugProcessInformation
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public int dwProcessId;
        public int dwThreadId;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct CorDebugIlToNativeMap
    {
        public uint ilOffset;
        public uint nativeStartOffset;
        public uint nativeEndOffset;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct CorDebugStepRange
    {
        public uint startOffset;
        public uint endOffset;
    }

    [ComImport]
    [Guid("D332DB9E-B9B3-4125-8207-A14884F53216")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ICLRMetaHost
    {
        [PreserveSig]
        int GetRuntime(
            [MarshalAs(UnmanagedType.LPWStr)] string pwzVersion,
            [In] ref Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out object ppRuntime);

        [PreserveSig]
        int GetVersionFromFile(
            [MarshalAs(UnmanagedType.LPWStr)] string pwzFilePath,
            [MarshalAs(UnmanagedType.LPWStr)] StringBuilder pwzBuffer,
            ref uint pcchBuffer);

        [PreserveSig]
        int EnumerateInstalledRuntimes([MarshalAs(UnmanagedType.Interface)] out object ppEnumerator);

        [PreserveSig]
        int EnumerateLoadedRuntimes(IntPtr hndProcess, [MarshalAs(UnmanagedType.Interface)] out object ppEnumerator);

        [PreserveSig]
        int RequestRuntimeLoadedNotification(IntPtr pCallbackFunction);

        [PreserveSig]
        int QueryLegacyV2RuntimeBinding([In] ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object ppUnk);

        [PreserveSig]
        int ExitProcess(int iExitCode);
    }

    [ComImport]
    [Guid("BD39D1D2-BA2F-486a-89B0-B4B0CB466891")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ICLRRuntimeInfo
    {
        [PreserveSig]
        int GetVersionString([MarshalAs(UnmanagedType.LPWStr)] StringBuilder pwzBuffer, ref uint pcchBuffer);

        [PreserveSig]
        int GetRuntimeDirectory([MarshalAs(UnmanagedType.LPWStr)] StringBuilder pwzBuffer, ref uint pcchBuffer);

        [PreserveSig]
        int IsLoaded(IntPtr hndProcess, out int pbLoaded);

        [PreserveSig]
        int LoadErrorString(uint iResourceID, [MarshalAs(UnmanagedType.LPWStr)] StringBuilder pwzBuffer, ref uint pcchBuffer, int iLocaleID);

        [PreserveSig]
        int LoadLibrary([MarshalAs(UnmanagedType.LPWStr)] string pwzDllName, out IntPtr phndModule);

        [PreserveSig]
        int GetProcAddress([MarshalAs(UnmanagedType.LPStr)] string pszProcName, out IntPtr ppProc);

        [PreserveSig]
        int GetInterface(
            [In] ref Guid rclsid,
            [In] ref Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out object ppUnk);

        [PreserveSig]
        int IsLoadable(out int pbLoadable);

        [PreserveSig]
        int SetDefaultStartupFlags(uint dwStartupFlags, [MarshalAs(UnmanagedType.LPWStr)] string pwzHostConfigFile);

        [PreserveSig]
        int GetDefaultStartupFlags(out uint pdwStartupFlags, [MarshalAs(UnmanagedType.LPWStr)] StringBuilder pwzHostConfigFile, ref uint pcchHostConfigFile);

        [PreserveSig]
        int BindAsLegacyV2Runtime();

        [PreserveSig]
        int IsStarted(out int pbStarted, out uint pdwStartupFlags);
    }

    [ComImport]
    [Guid("3D6F5F61-7538-11D3-8D5B-00104B35E7EF")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ICorDebug
    {
        [PreserveSig]
        int Initialize();

        [PreserveSig]
        int Terminate();

        [PreserveSig]
        int SetManagedHandler([MarshalAs(UnmanagedType.Interface)] ICorDebugManagedCallback pCallback);

        [PreserveSig]
        int SetUnmanagedHandler(IntPtr pCallback);

        [PreserveSig]
        int CreateProcess(
            [MarshalAs(UnmanagedType.LPWStr)] string lpApplicationName,
            [MarshalAs(UnmanagedType.LPWStr)] StringBuilder lpCommandLine,
            IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes,
            int bInheritHandles,
            uint dwCreationFlags,
            IntPtr lpEnvironment,
            [MarshalAs(UnmanagedType.LPWStr)] string lpCurrentDirectory,
            ref CorDebugStartupInfo lpStartupInfo,
            ref CorDebugProcessInformation lpProcessInformation,
            int debuggingFlags,
            [MarshalAs(UnmanagedType.Interface)] out ICorDebugProcess ppProcess);

        [PreserveSig]
        int DebugActiveProcess(uint id, int win32Attach, [MarshalAs(UnmanagedType.Interface)] out ICorDebugProcess ppProcess);

        [PreserveSig]
        int EnumerateProcesses([MarshalAs(UnmanagedType.Interface)] out object ppProcess);

        [PreserveSig]
        int GetProcess(uint dwProcessId, [MarshalAs(UnmanagedType.Interface)] out ICorDebugProcess ppProcess);

        [PreserveSig]
        int CanLaunchOrAttach(uint dwProcessId, int win32DebuggingEnabled);
    }

    [ComImport]
    [Guid("3D6F5F62-7538-11D3-8D5B-00104B35E7EF")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ICorDebugController
    {
        [PreserveSig]
        int Stop(uint dwTimeoutIgnored);

        [PreserveSig]
        int Continue(int fIsOutOfBand);

        [PreserveSig]
        int IsRunning(out int pbRunning);

        [PreserveSig]
        int HasQueuedCallbacks([MarshalAs(UnmanagedType.Interface)] ICorDebugThread pThread, out int pbQueued);

        [PreserveSig]
        int EnumerateThreads([MarshalAs(UnmanagedType.Interface)] out object ppThreads);

        [PreserveSig]
        int SetAllThreadsDebugState(int state, [MarshalAs(UnmanagedType.Interface)] ICorDebugThread pExceptThisThread);

        [PreserveSig]
        int Detach();

        [PreserveSig]
        int Terminate(uint exitCode);

        [PreserveSig]
        int CanCommitChanges(uint cSnapshots, IntPtr pSnapshots, out IntPtr pError);

        [PreserveSig]
        int CommitChanges(uint cSnapshots, IntPtr pSnapshots, out IntPtr pError);
    }

    [ComImport]
    [Guid("3D6F5F63-7538-11D3-8D5B-00104B35E7EF")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ICorDebugAppDomain
    {
        [PreserveSig]
        int Stop(uint dwTimeoutIgnored);

        [PreserveSig]
        int Continue(int fIsOutOfBand);

        [PreserveSig]
        int IsRunning(out int pbRunning);

        [PreserveSig]
        int HasQueuedCallbacks([MarshalAs(UnmanagedType.Interface)] ICorDebugThread pThread, out int pbQueued);

        [PreserveSig]
        int EnumerateThreads([MarshalAs(UnmanagedType.Interface)] out object ppThreads);

        [PreserveSig]
        int SetAllThreadsDebugState(int state, [MarshalAs(UnmanagedType.Interface)] ICorDebugThread pExceptThisThread);

        [PreserveSig]
        int Detach();

        [PreserveSig]
        int Terminate(uint exitCode);

        [PreserveSig]
        int CanCommitChanges(uint cSnapshots, IntPtr pSnapshots, out IntPtr pError);

        [PreserveSig]
        int CommitChanges(uint cSnapshots, IntPtr pSnapshots, out IntPtr pError);

        [PreserveSig]
        int GetProcess([MarshalAs(UnmanagedType.Interface)] out ICorDebugProcess ppProcess);

        [PreserveSig]
        int EnumerateAssemblies([MarshalAs(UnmanagedType.Interface)] out object ppAssemblies);

        [PreserveSig]
        int GetModuleFromMetaDataInterface([MarshalAs(UnmanagedType.IUnknown)] object pIMetaData, [MarshalAs(UnmanagedType.Interface)] out ICorDebugModule ppModule);

        [PreserveSig]
        int EnumerateBreakpoints([MarshalAs(UnmanagedType.Interface)] out object ppBreakpoints);

        [PreserveSig]
        int EnumerateSteppers([MarshalAs(UnmanagedType.Interface)] out object ppSteppers);

        [PreserveSig]
        int IsAttached(out int pbAttached);

        [PreserveSig]
        int GetName(uint cchName, out uint pcchName, [MarshalAs(UnmanagedType.LPWStr)] StringBuilder szName);

        [PreserveSig]
        int GetObject([MarshalAs(UnmanagedType.Interface)] out object ppObject);

        [PreserveSig]
        int Attach();

        [PreserveSig]
        int GetID(out uint pId);
    }

    [ComImport]
    [Guid("3D6F5F64-7538-11D3-8D5B-00104B35E7EF")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ICorDebugProcess
    {
        [PreserveSig]
        int Stop(uint dwTimeoutIgnored);

        [PreserveSig]
        int Continue(int fIsOutOfBand);

        [PreserveSig]
        int IsRunning(out int pbRunning);

        [PreserveSig]
        int HasQueuedCallbacks([MarshalAs(UnmanagedType.Interface)] ICorDebugThread pThread, out int pbQueued);

        [PreserveSig]
        int EnumerateThreads([MarshalAs(UnmanagedType.Interface)] out object ppThreads);

        [PreserveSig]
        int SetAllThreadsDebugState(int state, [MarshalAs(UnmanagedType.Interface)] ICorDebugThread pExceptThisThread);

        [PreserveSig]
        int Detach();

        [PreserveSig]
        int Terminate(uint exitCode);

        [PreserveSig]
        int CanCommitChanges(uint cSnapshots, IntPtr pSnapshots, out IntPtr pError);

        [PreserveSig]
        int CommitChanges(uint cSnapshots, IntPtr pSnapshots, out IntPtr pError);

        [PreserveSig]
        int GetID(out uint pdwProcessId);

        [PreserveSig]
        int GetHandle(out IntPtr phProcessHandle);

        [PreserveSig]
        int GetThread(uint dwThreadId, [MarshalAs(UnmanagedType.Interface)] out ICorDebugThread ppThread);

        [PreserveSig]
        int EnumerateObjects([MarshalAs(UnmanagedType.Interface)] out object ppObjects);

        [PreserveSig]
        int IsTransitionStub(ulong address, out int pbTransitionStub);

        [PreserveSig]
        int IsOSSuspended(uint threadID, out int pbSuspended);

        [PreserveSig]
        int GetThreadContext(uint threadID, uint contextSize, IntPtr context);

        [PreserveSig]
        int SetThreadContext(uint threadID, uint contextSize, IntPtr context);

        [PreserveSig]
        int ReadMemory(ulong address, uint size, IntPtr buffer, out IntPtr read);

        [PreserveSig]
        int WriteMemory(ulong address, uint size, IntPtr buffer, out IntPtr written);

        [PreserveSig]
        int ClearCurrentException(uint threadID);

        [PreserveSig]
        int EnableLogMessages(int fOnOff);

        [PreserveSig]
        int ModifyLogSwitch([MarshalAs(UnmanagedType.LPWStr)] string pLogSwitchName, int lLevel);

        [PreserveSig]
        int EnumerateAppDomains([MarshalAs(UnmanagedType.Interface)] out object ppAppDomains);

        [PreserveSig]
        int GetObject([MarshalAs(UnmanagedType.Interface)] out object ppObject);

        [PreserveSig]
        int ThreadForFiberCookie(uint fiberCookie, [MarshalAs(UnmanagedType.Interface)] out ICorDebugThread ppThread);

        [PreserveSig]
        int GetHelperThreadID(out uint pThreadID);
    }

    [ComImport]
    [Guid("938C6D66-7FB6-4F69-B389-425B8987329B")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ICorDebugThread
    {
        [PreserveSig]
        int GetProcess([MarshalAs(UnmanagedType.Interface)] out ICorDebugProcess ppProcess);

        [PreserveSig]
        int GetID(out uint pdwThreadId);

        [PreserveSig]
        int GetHandle(out IntPtr phThreadHandle);

        [PreserveSig]
        int GetAppDomain([MarshalAs(UnmanagedType.Interface)] out ICorDebugAppDomain ppAppDomain);

        [PreserveSig]
        int SetDebugState(int state);

        [PreserveSig]
        int GetDebugState(out int pState);

        [PreserveSig]
        int GetUserState(out int pState);

        [PreserveSig]
        int GetCurrentException([MarshalAs(UnmanagedType.Interface)] out ICorDebugValue ppExceptionObject);

        [PreserveSig]
        int ClearCurrentException();

        [PreserveSig]
        int CreateStepper([MarshalAs(UnmanagedType.Interface)] out ICorDebugStepper ppStepper);

        [PreserveSig]
        int EnumerateChains([MarshalAs(UnmanagedType.Interface)] out ICorDebugChainEnum ppChains);

        [PreserveSig]
        int GetActiveChain([MarshalAs(UnmanagedType.Interface)] out ICorDebugChain ppChain);

        [PreserveSig]
        int GetActiveFrame([MarshalAs(UnmanagedType.Interface)] out ICorDebugFrame ppFrame);

        [PreserveSig]
        int GetRegisterSet([MarshalAs(UnmanagedType.Interface)] out object ppRegisters);

        [PreserveSig]
        int CreateEval([MarshalAs(UnmanagedType.Interface)] out object ppEval);

        [PreserveSig]
        int GetObject([MarshalAs(UnmanagedType.Interface)] out ICorDebugValue ppObject);
    }

    [ComImport]
    [Guid("CC7BCAEE-8A68-11d2-983C-0000F808342D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ICorDebugChain
    {
        [PreserveSig]
        int GetThread([MarshalAs(UnmanagedType.Interface)] out ICorDebugThread ppThread);

        [PreserveSig]
        int GetStackRange(out ulong pStart, out ulong pEnd);

        [PreserveSig]
        int GetContext([MarshalAs(UnmanagedType.Interface)] out object ppContext);

        [PreserveSig]
        int GetCaller([MarshalAs(UnmanagedType.Interface)] out ICorDebugChain ppChain);

        [PreserveSig]
        int GetCallee([MarshalAs(UnmanagedType.Interface)] out ICorDebugChain ppChain);

        [PreserveSig]
        int GetPrevious([MarshalAs(UnmanagedType.Interface)] out ICorDebugChain ppChain);

        [PreserveSig]
        int GetNext([MarshalAs(UnmanagedType.Interface)] out ICorDebugChain ppChain);

        [PreserveSig]
        int IsManaged(out int pManaged);

        [PreserveSig]
        int EnumerateFrames([MarshalAs(UnmanagedType.Interface)] out ICorDebugFrameEnum ppFrames);

        [PreserveSig]
        int GetActiveFrame([MarshalAs(UnmanagedType.Interface)] out ICorDebugFrame ppFrame);

        [PreserveSig]
        int GetRegisterSet([MarshalAs(UnmanagedType.Interface)] out object ppRegisters);

        [PreserveSig]
        int GetReason(out int pReason);
    }

    [ComImport]
    [Guid("CC7BCAEF-8A68-11d2-983C-0000F808342D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ICorDebugFrame
    {
        [PreserveSig]
        int GetChain([MarshalAs(UnmanagedType.Interface)] out ICorDebugChain ppChain);

        [PreserveSig]
        int GetCode([MarshalAs(UnmanagedType.Interface)] out ICorDebugCode ppCode);

        [PreserveSig]
        int GetFunction([MarshalAs(UnmanagedType.Interface)] out ICorDebugFunction ppFunction);

        [PreserveSig]
        int GetFunctionToken(out uint pToken);

        [PreserveSig]
        int GetStackRange(out ulong pStart, out ulong pEnd);

        [PreserveSig]
        int GetCaller([MarshalAs(UnmanagedType.Interface)] out ICorDebugFrame ppFrame);

        [PreserveSig]
        int GetCallee([MarshalAs(UnmanagedType.Interface)] out ICorDebugFrame ppFrame);

        [PreserveSig]
        int CreateStepper([MarshalAs(UnmanagedType.Interface)] out ICorDebugStepper ppStepper);
    }

    [ComImport]
    [Guid("03E26311-4F76-11d3-88C6-006097945418")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ICorDebugILFrame
    {
        [PreserveSig]
        int GetChain([MarshalAs(UnmanagedType.Interface)] out ICorDebugChain ppChain);

        [PreserveSig]
        int GetCode([MarshalAs(UnmanagedType.Interface)] out ICorDebugCode ppCode);

        [PreserveSig]
        int GetFunction([MarshalAs(UnmanagedType.Interface)] out ICorDebugFunction ppFunction);

        [PreserveSig]
        int GetFunctionToken(out uint pToken);

        [PreserveSig]
        int GetStackRange(out ulong pStart, out ulong pEnd);

        [PreserveSig]
        int GetCaller([MarshalAs(UnmanagedType.Interface)] out ICorDebugFrame ppFrame);

        [PreserveSig]
        int GetCallee([MarshalAs(UnmanagedType.Interface)] out ICorDebugFrame ppFrame);

        [PreserveSig]
        int CreateStepper([MarshalAs(UnmanagedType.Interface)] out ICorDebugStepper ppStepper);

        [PreserveSig]
        int GetIP(out uint pnOffset, out int pMappingResult);

        [PreserveSig]
        int SetIP(uint nOffset);

        [PreserveSig]
        int EnumerateLocalVariables([MarshalAs(UnmanagedType.Interface)] out object ppValueEnum);

        [PreserveSig]
        int GetLocalVariable(uint dwIndex, [MarshalAs(UnmanagedType.Interface)] out ICorDebugValue ppValue);

        [PreserveSig]
        int EnumerateArguments([MarshalAs(UnmanagedType.Interface)] out object ppValueEnum);

        [PreserveSig]
        int GetArgument(uint dwIndex, [MarshalAs(UnmanagedType.Interface)] out ICorDebugValue ppValue);

        [PreserveSig]
        int GetStackDepth(out uint pDepth);

        [PreserveSig]
        int GetStackValue(uint dwIndex, [MarshalAs(UnmanagedType.Interface)] out ICorDebugValue ppValue);

        [PreserveSig]
        int CanSetIP(uint nOffset);
    }

    [ComImport]
    [Guid("DBA2D8C1-E5C5-4069-8C13-10A7C6ABF43D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ICorDebugModule
    {
        [PreserveSig]
        int GetProcess([MarshalAs(UnmanagedType.Interface)] out ICorDebugProcess ppProcess);

        [PreserveSig]
        int GetBaseAddress(out ulong pAddress);

        [PreserveSig]
        int GetAssembly([MarshalAs(UnmanagedType.Interface)] out object ppAssembly);

        [PreserveSig]
        int GetName(uint cchName, out uint pcchName, [MarshalAs(UnmanagedType.LPWStr)] StringBuilder szName);

        [PreserveSig]
        int EnableJITDebugging(int bTrackJITInfo, int bAllowJitOpts);

        [PreserveSig]
        int EnableClassLoadCallbacks(int bClassLoadCallbacks);

        [PreserveSig]
        int GetFunctionFromToken(uint methodDef, [MarshalAs(UnmanagedType.Interface)] out ICorDebugFunction ppFunction);

        [PreserveSig]
        int GetFunctionFromRVA(ulong rva, [MarshalAs(UnmanagedType.Interface)] out ICorDebugFunction ppFunction);

        [PreserveSig]
        int GetClassFromToken(uint typeDef, [MarshalAs(UnmanagedType.Interface)] out ICorDebugClass ppClass);

        [PreserveSig]
        int CreateBreakpoint([MarshalAs(UnmanagedType.Interface)] out object ppBreakpoint);

        [PreserveSig]
        int GetEditAndContinueSnapshot([MarshalAs(UnmanagedType.Interface)] out object ppEditAndContinueSnapshot);

        [PreserveSig]
        int GetMetaDataInterface([In] ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object ppObj);

        [PreserveSig]
        int GetToken(out uint pToken);

        [PreserveSig]
        int IsDynamic(out int pDynamic);

        [PreserveSig]
        int GetGlobalVariableValue(uint fieldDef, [MarshalAs(UnmanagedType.Interface)] out ICorDebugValue ppValue);

        [PreserveSig]
        int GetSize(out uint pcBytes);

        [PreserveSig]
        int IsInMemory(out int pInMemory);
    }

    [ComImport]
    [Guid("CC7BCAF3-8A68-11d2-983C-0000F808342D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ICorDebugFunction
    {
        [PreserveSig]
        int GetModule([MarshalAs(UnmanagedType.Interface)] out ICorDebugModule ppModule);

        [PreserveSig]
        int GetClass([MarshalAs(UnmanagedType.Interface)] out ICorDebugClass ppClass);

        [PreserveSig]
        int GetToken(out uint pMethodDef);

        [PreserveSig]
        int GetILCode([MarshalAs(UnmanagedType.Interface)] out ICorDebugCode ppCode);

        [PreserveSig]
        int GetNativeCode([MarshalAs(UnmanagedType.Interface)] out ICorDebugCode ppCode);

        [PreserveSig]
        int CreateBreakpoint([MarshalAs(UnmanagedType.Interface)] out ICorDebugFunctionBreakpoint ppBreakpoint);

        [PreserveSig]
        int GetLocalVarSigToken(out uint pmdSig);

        [PreserveSig]
        int GetCurrentVersionNumber(out uint pnCurrentVersion);
    }

    [ComImport]
    [Guid("CC7BCAF4-8A68-11d2-983C-0000F808342D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ICorDebugCode
    {
        [PreserveSig]
        int IsIL(out int pbIL);

        [PreserveSig]
        int GetFunction([MarshalAs(UnmanagedType.Interface)] out ICorDebugFunction ppFunction);

        [PreserveSig]
        int GetAddress(out ulong pStart);

        [PreserveSig]
        int GetSize(out uint pcBytes);

        [PreserveSig]
        int CreateBreakpoint(uint offset, [MarshalAs(UnmanagedType.Interface)] out ICorDebugFunctionBreakpoint ppBreakpoint);

        [PreserveSig]
        int GetCode(uint startOffset, uint endOffset, uint cBufferAlloc, IntPtr buffer, out uint pcBufferSize);

        [PreserveSig]
        int GetVersionNumber(out uint nVersion);

        [PreserveSig]
        int GetILToNativeMapping(uint cMap, out uint pcMap, [In, Out] CorDebugIlToNativeMap[] map);

        [PreserveSig]
        int GetEnCRemapSequencePoints(uint cMap, out uint pcMap, [In, Out] uint[] offsets);
    }

    [ComImport]
    [Guid("CC7BCAE8-8A68-11d2-983C-0000F808342D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ICorDebugBreakpoint
    {
        [PreserveSig]
        int Activate(int bActive);

        [PreserveSig]
        int IsActive(out int pbActive);
    }

    [ComImport]
    [Guid("CC7BCAE9-8A68-11d2-983C-0000F808342D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ICorDebugFunctionBreakpoint
    {
        [PreserveSig]
        int Activate(int bActive);

        [PreserveSig]
        int IsActive(out int pbActive);

        [PreserveSig]
        int GetFunction([MarshalAs(UnmanagedType.Interface)] out ICorDebugFunction ppFunction);

        [PreserveSig]
        int GetOffset(out uint pnOffset);
    }

    [ComImport]
    [Guid("CC7BCAEC-8A68-11d2-983C-0000F808342D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ICorDebugStepper
    {
        [PreserveSig]
        int IsActive(out int pbActive);

        [PreserveSig]
        int Deactivate();

        [PreserveSig]
        int SetInterceptMask(int mask);

        [PreserveSig]
        int SetUnmappedStopMask(int mask);

        [PreserveSig]
        int Step(int bStepIn);

        [PreserveSig]
        int StepRange(int bStepIn, [In] CorDebugStepRange[] ranges, uint cRangeCount);

        [PreserveSig]
        int StepOut();

        [PreserveSig]
        int SetRangeIL(int bIL);
    }

    [ComImport]
    [Guid("CC7BCAF5-8A68-11d2-983C-0000F808342D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ICorDebugClass
    {
        [PreserveSig]
        int GetModule([MarshalAs(UnmanagedType.Interface)] out ICorDebugModule pModule);

        [PreserveSig]
        int GetToken(out uint pTypeDef);

        [PreserveSig]
        int GetStaticFieldValue(uint fieldDef, [MarshalAs(UnmanagedType.Interface)] ICorDebugFrame pFrame, [MarshalAs(UnmanagedType.Interface)] out ICorDebugValue ppValue);
    }

    [ComImport]
    [Guid("D613F0BB-ACE1-4c19-BD72-E4C08D5DA7F5")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ICorDebugType
    {
        [PreserveSig]
        int GetType(out int ty);

        [PreserveSig]
        int GetClass([MarshalAs(UnmanagedType.Interface)] out ICorDebugClass ppClass);
    }

    [ComImport]
    [Guid("CC7BCAF7-8A68-11d2-983C-0000F808342D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ICorDebugValue
    {
        [PreserveSig]
        int GetType(out int pType);

        [PreserveSig]
        int GetSize(out uint pSize);

        [PreserveSig]
        int GetAddress(out ulong pAddress);

        [PreserveSig]
        int CreateBreakpoint([MarshalAs(UnmanagedType.Interface)] out object ppBreakpoint);
    }

    [ComImport]
    [Guid("5E0B54E7-D88A-4626-9420-A691E0A78B49")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ICorDebugValue2
    {
        [PreserveSig]
        int GetExactType([MarshalAs(UnmanagedType.Interface)] out ICorDebugType ppType);
    }

    [ComImport]
    [Guid("CC7BCAF8-8A68-11d2-983C-0000F808342D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ICorDebugGenericValue
    {
        [PreserveSig]
        int GetType(out int pType);

        [PreserveSig]
        int GetSize(out uint pSize);

        [PreserveSig]
        int GetAddress(out ulong pAddress);

        [PreserveSig]
        int CreateBreakpoint([MarshalAs(UnmanagedType.Interface)] out object ppBreakpoint);

        [PreserveSig]
        int GetValue(IntPtr pTo);

        [PreserveSig]
        int SetValue(IntPtr pFrom);
    }

    [ComImport]
    [Guid("CC7BCAF9-8A68-11d2-983C-0000F808342D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ICorDebugReferenceValue
    {
        [PreserveSig]
        int GetType(out int pType);

        [PreserveSig]
        int GetSize(out uint pSize);

        [PreserveSig]
        int GetAddress(out ulong pAddress);

        [PreserveSig]
        int CreateBreakpoint([MarshalAs(UnmanagedType.Interface)] out object ppBreakpoint);

        [PreserveSig]
        int IsNull(out int pbNull);

        [PreserveSig]
        int GetValue(out ulong pValue);

        [PreserveSig]
        int SetValue(ulong value);

        [PreserveSig]
        int Dereference([MarshalAs(UnmanagedType.Interface)] out ICorDebugValue ppValue);

        [PreserveSig]
        int DereferenceStrong([MarshalAs(UnmanagedType.Interface)] out ICorDebugValue ppValue);
    }

    [ComImport]
    [Guid("CC7BCAFA-8A68-11d2-983C-0000F808342D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ICorDebugHeapValue
    {
        [PreserveSig]
        int GetType(out int pType);

        [PreserveSig]
        int GetSize(out uint pSize);

        [PreserveSig]
        int GetAddress(out ulong pAddress);

        [PreserveSig]
        int CreateBreakpoint([MarshalAs(UnmanagedType.Interface)] out object ppBreakpoint);

        [PreserveSig]
        int IsValid(out int pbValid);

        [PreserveSig]
        int CreateRelocBreakpoint([MarshalAs(UnmanagedType.Interface)] out object ppBreakpoint);
    }

    [ComImport]
    [Guid("CC7BCAFC-8A68-11d2-983C-0000F808342D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ICorDebugBoxValue
    {
        [PreserveSig]
        int GetType(out int pType);

        [PreserveSig]
        int GetSize(out uint pSize);

        [PreserveSig]
        int GetAddress(out ulong pAddress);

        [PreserveSig]
        int CreateBreakpoint([MarshalAs(UnmanagedType.Interface)] out object ppBreakpoint);

        [PreserveSig]
        int IsValid(out int pbValid);

        [PreserveSig]
        int CreateRelocBreakpoint([MarshalAs(UnmanagedType.Interface)] out object ppBreakpoint);

        [PreserveSig]
        int GetObject([MarshalAs(UnmanagedType.Interface)] out ICorDebugObjectValue ppObject);
    }

    [ComImport]
    [Guid("18AD3D6E-B7D2-11d2-BD04-0000F80849BD")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ICorDebugObjectValue
    {
        [PreserveSig]
        int GetType(out int pType);

        [PreserveSig]
        int GetSize(out uint pSize);

        [PreserveSig]
        int GetAddress(out ulong pAddress);

        [PreserveSig]
        int CreateBreakpoint([MarshalAs(UnmanagedType.Interface)] out object ppBreakpoint);

        [PreserveSig]
        int GetClass([MarshalAs(UnmanagedType.Interface)] out ICorDebugClass ppClass);

        [PreserveSig]
        int GetFieldValue([MarshalAs(UnmanagedType.Interface)] ICorDebugClass pClass, uint fieldDef, [MarshalAs(UnmanagedType.Interface)] out ICorDebugValue ppValue);

        [PreserveSig]
        int GetVirtualMethod(uint memberRef, [MarshalAs(UnmanagedType.Interface)] out ICorDebugFunction ppFunction);

        [PreserveSig]
        int GetContext([MarshalAs(UnmanagedType.Interface)] out object ppContext);

        [PreserveSig]
        int IsValueClass(out int pbIsValueClass);

        [PreserveSig]
        int GetManagedCopy([MarshalAs(UnmanagedType.IUnknown)] out object ppObject);

        [PreserveSig]
        int SetFromManagedCopy([MarshalAs(UnmanagedType.IUnknown)] object pObject);
    }

    [ComImport]
    [Guid("CC7BCAFD-8A68-11d2-983C-0000F808342D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ICorDebugStringValue
    {
        [PreserveSig]
        int GetType(out int pType);

        [PreserveSig]
        int GetSize(out uint pSize);

        [PreserveSig]
        int GetAddress(out ulong pAddress);

        [PreserveSig]
        int CreateBreakpoint([MarshalAs(UnmanagedType.Interface)] out object ppBreakpoint);

        [PreserveSig]
        int IsValid(out int pbValid);

        [PreserveSig]
        int CreateRelocBreakpoint([MarshalAs(UnmanagedType.Interface)] out object ppBreakpoint);

        [PreserveSig]
        int GetLength(out uint pcchString);

        [PreserveSig]
        int GetString(uint cchString, out uint pcchString, [MarshalAs(UnmanagedType.LPWStr)] StringBuilder szString);
    }

    [ComImport]
    [Guid("CC7BCB01-8A68-11d2-983C-0000F808342D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ICorDebugEnum
    {
        [PreserveSig]
        int Skip(uint celt);

        [PreserveSig]
        int Reset();

        [PreserveSig]
        int Clone([MarshalAs(UnmanagedType.Interface)] out ICorDebugEnum ppEnum);

        [PreserveSig]
        int GetCount(out uint pcelt);
    }

    [ComImport]
    [Guid("CC7BCB07-8A68-11d2-983C-0000F808342D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ICorDebugFrameEnum
    {
        [PreserveSig]
        int Skip(uint celt);

        [PreserveSig]
        int Reset();

        [PreserveSig]
        int Clone([MarshalAs(UnmanagedType.Interface)] out ICorDebugEnum ppEnum);

        [PreserveSig]
        int GetCount(out uint pcelt);

        [PreserveSig]
        int Next(uint celt, [Out, MarshalAs(UnmanagedType.LPArray)] ICorDebugFrame[] frames, out uint pceltFetched);
    }

    [ComImport]
    [Guid("CC7BCB08-8A68-11d2-983C-0000F808342D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ICorDebugChainEnum
    {
        [PreserveSig]
        int Skip(uint celt);

        [PreserveSig]
        int Reset();

        [PreserveSig]
        int Clone([MarshalAs(UnmanagedType.Interface)] out ICorDebugEnum ppEnum);

        [PreserveSig]
        int GetCount(out uint pcelt);

        [PreserveSig]
        int Next(uint celt, [Out, MarshalAs(UnmanagedType.LPArray)] ICorDebugChain[] chains, out uint pceltFetched);
    }

    [ComImport]
    [Guid("DF59507C-D47A-459E-BCE2-6427EAC8FD06")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ICorDebugAssembly
    {
    }

    [ComImport]
    [Guid("CC7BCAF6-8A68-11d2-983C-0000F808342D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ICorDebugEval
    {
    }

    [ComImport]
    [Guid("CC726F2F-1DB7-459b-B0EC-05F01D841B42")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ICorDebugMDA
    {
    }

    [ComImport]
    [Guid("0000000C-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ICorDebugStream
    {
    }

    [ComImport]
    [Guid("809C652E-7396-11D2-9771-00A0C9B4D50C")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMetaDataDispenser
    {
        [PreserveSig]
        int DefineScope(
            [In] ref Guid rclsid,
            uint dwCreateFlags,
            [In] ref Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out object ppIUnk);

        [PreserveSig]
        int OpenScope(
            [MarshalAs(UnmanagedType.LPWStr)] string szScope,
            uint dwOpenFlags,
            [In] ref Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out object ppIUnk);

        [PreserveSig]
        int OpenScopeOnMemory(
            IntPtr pData,
            uint cbData,
            uint dwOpenFlags,
            [In] ref Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out object ppIUnk);
    }

    [ComImport]
    [Guid("7DAC8207-D3AE-4c75-9B67-92801A497D44")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMetaDataImport
    {
        [PreserveSig]
        int CloseEnum(IntPtr hEnum);

        [PreserveSig]
        int CountEnum(IntPtr hEnum, out uint pulCount);

        [PreserveSig]
        int ResetEnum(IntPtr hEnum, uint ulPos);

        [PreserveSig]
        int EnumTypeDefs(ref IntPtr phEnum, [Out] uint[] rTypeDefs, uint cMax, out uint pcTypeDefs);

        [PreserveSig]
        int EnumInterfaceImpls(ref IntPtr phEnum, uint td, [Out] uint[] rImpls, uint cMax, out uint pcImpls);

        [PreserveSig]
        int EnumTypeRefs(ref IntPtr phEnum, [Out] uint[] rTypeRefs, uint cMax, out uint pcTypeRefs);

        [PreserveSig]
        int FindTypeDefByName([MarshalAs(UnmanagedType.LPWStr)] string szTypeDef, uint tkEnclosingClass, out uint ptd);

        [PreserveSig]
        int GetScopeProps([MarshalAs(UnmanagedType.LPWStr)] StringBuilder szName, uint cchName, out uint pchName, out Guid pmvid);

        [PreserveSig]
        int GetModuleFromScope(out uint pmd);

        [PreserveSig]
        int GetTypeDefProps(uint td, [MarshalAs(UnmanagedType.LPWStr)] StringBuilder szTypeDef, uint cchTypeDef, out uint pchTypeDef, out uint pdwTypeDefFlags, out uint ptkExtends);

        [PreserveSig]
        int GetInterfaceImplProps(uint iiImpl, out uint pClass, out uint ptkIface);

        [PreserveSig]
        int GetTypeRefProps(uint tr, out uint ptkResolutionScope, [MarshalAs(UnmanagedType.LPWStr)] StringBuilder szName, uint cchName, out uint pchName);

        [PreserveSig]
        int ResolveTypeRef(uint tr, [In] ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object ppIScope, out uint ptd);

        [PreserveSig]
        int EnumMembers(ref IntPtr phEnum, uint cl, [Out] uint[] rMembers, uint cMax, out uint pcTokens);

        [PreserveSig]
        int EnumMembersWithName(ref IntPtr phEnum, uint cl, [MarshalAs(UnmanagedType.LPWStr)] string szName, [Out] uint[] rMembers, uint cMax, out uint pcTokens);

        [PreserveSig]
        int EnumMethods(ref IntPtr phEnum, uint cl, [Out] uint[] rMethods, uint cMax, out uint pcTokens);

        [PreserveSig]
        int EnumMethodsWithName(ref IntPtr phEnum, uint cl, [MarshalAs(UnmanagedType.LPWStr)] string szName, [Out] uint[] rMethods, uint cMax, out uint pcTokens);

        [PreserveSig]
        int EnumFields(ref IntPtr phEnum, uint cl, [Out] uint[] rFields, uint cMax, out uint pcTokens);

        [PreserveSig]
        int EnumFieldsWithName(ref IntPtr phEnum, uint cl, [MarshalAs(UnmanagedType.LPWStr)] string szName, [Out] uint[] rFields, uint cMax, out uint pcTokens);

        [PreserveSig]
        int EnumParams(ref IntPtr phEnum, uint mb, [Out] uint[] rParams, uint cMax, out uint pcTokens);

        [PreserveSig]
        int EnumMemberRefs(ref IntPtr phEnum, uint tkParent, [Out] uint[] rMemberRefs, uint cMax, out uint pcTokens);

        [PreserveSig]
        int EnumMethodImpls(ref IntPtr phEnum, uint td, [Out] uint[] rMethodBody, [Out] uint[] rMethodDecl, uint cMax, out uint pcTokens);

        [PreserveSig]
        int EnumPermissionSets(ref IntPtr phEnum, uint tk, uint dwActions, [Out] uint[] rPermission, uint cMax, out uint pcTokens);

        [PreserveSig]
        int FindMember(uint td, [MarshalAs(UnmanagedType.LPWStr)] string szName, IntPtr pvSigBlob, uint cbSigBlob, out uint pmb);

        [PreserveSig]
        int FindMethod(uint td, [MarshalAs(UnmanagedType.LPWStr)] string szName, IntPtr pvSigBlob, uint cbSigBlob, out uint pmb);

        [PreserveSig]
        int FindField(uint td, [MarshalAs(UnmanagedType.LPWStr)] string szName, IntPtr pvSigBlob, uint cbSigBlob, out uint pmb);

        [PreserveSig]
        int FindMemberRef(uint td, [MarshalAs(UnmanagedType.LPWStr)] string szName, IntPtr pvSigBlob, uint cbSigBlob, out uint pmr);

        [PreserveSig]
        int GetMethodProps(uint mb, out uint pClass, [MarshalAs(UnmanagedType.LPWStr)] StringBuilder szMethod, uint cchMethod, out uint pchMethod, out uint pdwAttr, out IntPtr ppvSigBlob, out uint pcbSigBlob, out uint pulCodeRVA, out uint pdwImplFlags);
    }

    [ComImport]
    [Guid("AA544D42-28CB-11d3-BD22-0000F80849BD")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ISymUnmanagedBinder
    {
        [PreserveSig]
        int GetReaderForFile(
            [MarshalAs(UnmanagedType.IUnknown)] object importer,
            [MarshalAs(UnmanagedType.LPWStr)] string fileName,
            [MarshalAs(UnmanagedType.LPWStr)] string searchPath,
            [MarshalAs(UnmanagedType.Interface)] out ISymUnmanagedReader pRetVal);

        [PreserveSig]
        int GetReaderFromStream(
            [MarshalAs(UnmanagedType.IUnknown)] object importer,
            [MarshalAs(UnmanagedType.Interface)] object pstream,
            [MarshalAs(UnmanagedType.Interface)] out ISymUnmanagedReader pRetVal);
    }

    [ComImport]
    [Guid("B4CE6286-2A6B-3712-A3B7-1EE1DAD467B5")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ISymUnmanagedReader
    {
        [PreserveSig]
        int GetDocument(
            [MarshalAs(UnmanagedType.LPWStr)] string url,
            Guid language,
            Guid languageVendor,
            Guid documentType,
            [MarshalAs(UnmanagedType.Interface)] out ISymUnmanagedDocument pRetVal);

        [PreserveSig]
        int GetDocuments(uint cDocs, out uint pcDocs, [Out, MarshalAs(UnmanagedType.LPArray)] ISymUnmanagedDocument[] pDocs);

        [PreserveSig]
        int GetUserEntryPoint(out uint pToken);

        [PreserveSig]
        int GetMethod(uint token, [MarshalAs(UnmanagedType.Interface)] out ISymUnmanagedMethod pRetVal);

        [PreserveSig]
        int GetMethodByVersion(uint token, int version, [MarshalAs(UnmanagedType.Interface)] out ISymUnmanagedMethod pRetVal);

        [PreserveSig]
        int GetVariables(uint parent, uint cVars, out uint pcVars, [Out, MarshalAs(UnmanagedType.LPArray)] ISymUnmanagedVariable[] pVars);

        [PreserveSig]
        int GetGlobalVariables(uint cVars, out uint pcVars, [Out, MarshalAs(UnmanagedType.LPArray)] ISymUnmanagedVariable[] pVars);

        [PreserveSig]
        int GetMethodFromDocumentPosition(
            [MarshalAs(UnmanagedType.Interface)] ISymUnmanagedDocument document,
            uint line,
            uint column,
            [MarshalAs(UnmanagedType.Interface)] out ISymUnmanagedMethod pRetVal);

        [PreserveSig]
        int GetSymAttribute(uint parent, [MarshalAs(UnmanagedType.LPWStr)] string name, uint cBuffer, out uint pcBuffer, IntPtr buffer);

        [PreserveSig]
        int GetNamespaces(uint cNameSpaces, out uint pcNameSpaces, IntPtr namespaces);

        [PreserveSig]
        int Initialize(
            [MarshalAs(UnmanagedType.IUnknown)] object importer,
            [MarshalAs(UnmanagedType.LPWStr)] string filename,
            [MarshalAs(UnmanagedType.LPWStr)] string searchPath,
            IntPtr pIStream);

        [PreserveSig]
        int UpdateSymbolStore([MarshalAs(UnmanagedType.LPWStr)] string filename, IntPtr pIStream);

        [PreserveSig]
        int ReplaceSymbolStore([MarshalAs(UnmanagedType.LPWStr)] string filename, IntPtr pIStream);

        [PreserveSig]
        int GetSymbolStoreFileName(uint cchName, out uint pcchName, [MarshalAs(UnmanagedType.LPWStr)] StringBuilder szName);

        [PreserveSig]
        int GetMethodsFromDocumentPosition(
            [MarshalAs(UnmanagedType.Interface)] ISymUnmanagedDocument document,
            uint line,
            uint column,
            uint cMethod,
            out uint pcMethod,
            [Out, MarshalAs(UnmanagedType.LPArray)] ISymUnmanagedMethod[] pRetVal);

        [PreserveSig]
        int GetDocumentVersion([MarshalAs(UnmanagedType.Interface)] ISymUnmanagedDocument pDoc, out int version, out int pbCurrent);

        [PreserveSig]
        int GetMethodVersion([MarshalAs(UnmanagedType.Interface)] ISymUnmanagedMethod pMethod, out int version);
    }

    [ComImport]
    [Guid("40DE4037-7C81-3E1E-B022-AE1ABFF2CA08")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ISymUnmanagedDocument
    {
        [PreserveSig]
        int GetURL(uint cchUrl, out uint pcchUrl, [MarshalAs(UnmanagedType.LPWStr)] StringBuilder szUrl);

        [PreserveSig]
        int GetDocumentType(out Guid pRetVal);

        [PreserveSig]
        int GetLanguage(out Guid pRetVal);

        [PreserveSig]
        int GetLanguageVendor(out Guid pRetVal);

        [PreserveSig]
        int GetCheckSumAlgorithmId(out Guid pRetVal);

        [PreserveSig]
        int GetCheckSum(uint cData, out uint pcData, IntPtr data);

        [PreserveSig]
        int FindClosestLine(uint line, out uint pRetVal);

        [PreserveSig]
        int HasEmbeddedSource(out int pRetVal);

        [PreserveSig]
        int GetSourceLength(out uint pRetVal);

        [PreserveSig]
        int GetSourceRange(uint startLine, uint startColumn, uint endLine, uint endColumn, uint cSourceBytes, out uint pcSourceBytes, IntPtr source);
    }

    [ComImport]
    [Guid("B62B923C-B500-3158-A543-24F307A8B7E1")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ISymUnmanagedMethod
    {
        [PreserveSig]
        int GetToken(out uint pToken);

        [PreserveSig]
        int GetSequencePointCount(out uint pRetVal);

        [PreserveSig]
        int GetRootScope([MarshalAs(UnmanagedType.Interface)] out ISymUnmanagedScope pRetVal);

        [PreserveSig]
        int GetScopeFromOffset(uint offset, [MarshalAs(UnmanagedType.Interface)] out ISymUnmanagedScope pRetVal);

        [PreserveSig]
        int GetOffset(
            [MarshalAs(UnmanagedType.Interface)] ISymUnmanagedDocument document,
            uint line,
            uint column,
            out uint pRetVal);

        [PreserveSig]
        int GetRanges(
            [MarshalAs(UnmanagedType.Interface)] ISymUnmanagedDocument document,
            uint line,
            uint column,
            uint cRanges,
            out uint pcRanges,
            [Out] uint[] ranges);

        [PreserveSig]
        int GetParameters(uint cParams, out uint pcParams, [Out, MarshalAs(UnmanagedType.LPArray)] ISymUnmanagedVariable[] paramsArray);

        [PreserveSig]
        int GetNamespace([MarshalAs(UnmanagedType.Interface)] out object pRetVal);

        [PreserveSig]
        int GetSourceStartEnd(
            [In, MarshalAs(UnmanagedType.LPArray)] ISymUnmanagedDocument[] docs,
            [In, Out] uint[] lines,
            [In, Out] uint[] columns,
            out int pRetVal);

        [PreserveSig]
        int GetSequencePoints(
            uint cPoints,
            out uint pcPoints,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] uint[] offsets,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] IntPtr[] documents,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] uint[] lines,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] uint[] columns,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] uint[] endLines,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] uint[] endColumns);
    }

    [ComImport]
    [Guid("68005D0F-B8E0-3B01-84D5-A11A94154942")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ISymUnmanagedScope
    {
        [PreserveSig]
        int GetMethod([MarshalAs(UnmanagedType.Interface)] out ISymUnmanagedMethod pRetVal);

        [PreserveSig]
        int GetParent([MarshalAs(UnmanagedType.Interface)] out ISymUnmanagedScope pRetVal);

        [PreserveSig]
        int GetChildren(uint cChildren, out uint pcChildren, [Out, MarshalAs(UnmanagedType.LPArray)] ISymUnmanagedScope[] children);

        [PreserveSig]
        int GetStartOffset(out uint pRetVal);

        [PreserveSig]
        int GetEndOffset(out uint pRetVal);

        [PreserveSig]
        int GetLocalCount(out uint pRetVal);

        [PreserveSig]
        int GetLocals(uint cLocals, out uint pcLocals, [Out, MarshalAs(UnmanagedType.LPArray)] ISymUnmanagedVariable[] locals);

        [PreserveSig]
        int GetNamespaces(uint cNameSpaces, out uint pcNameSpaces, IntPtr namespaces);
    }

    [ComImport]
    [Guid("9F60EEBE-2D9A-3F7C-BF58-80BC991C60BB")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ISymUnmanagedVariable
    {
        [PreserveSig]
        int GetName(uint cchName, out uint pcchName, [MarshalAs(UnmanagedType.LPWStr)] StringBuilder szName);

        [PreserveSig]
        int GetAttributes(out uint pRetVal);

        [PreserveSig]
        int GetSignature(uint cSig, out uint pcSig, IntPtr sig);

        [PreserveSig]
        int GetAddressKind(out uint pRetVal);

        [PreserveSig]
        int GetAddressField1(out uint pRetVal);

        [PreserveSig]
        int GetAddressField2(out uint pRetVal);

        [PreserveSig]
        int GetAddressField3(out uint pRetVal);

        [PreserveSig]
        int GetStartOffset(out uint pRetVal);

        [PreserveSig]
        int GetEndOffset(out uint pRetVal);
    }
}
