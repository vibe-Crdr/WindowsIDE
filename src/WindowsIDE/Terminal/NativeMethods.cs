using System;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace WindowsIDE.Terminal
{
    /// <summary>
    /// kernel32 の ConPTY / パイプ / プロセスと、user32 の SPI。追加の /r は不要。
    /// </summary>
    public static class NativeMethods
    {
        /// <summary>CreateProcess に STARTUPINFOEX を付ける。</summary>
        public const uint ExtendedStartupinfoPresent = 0x00080000;

        /// <summary>CreateProcess に Unicode 環境ブロックを付ける。</summary>
        public const uint CreateUnicodeEnvironment = 0x00000400;

        /// <summary>SPI_GETSCREENREADER。対話 ConsoleHost がこれだけを見て PSReadLine を切る。SM(70) は使わない。</summary>
        public const uint SpiGetScreenReader = 0x0046;

        /// <summary>SPI_SETSCREENREADER。uiParam が BOOL。pvParam は NULL。fWinIni は 0（INI も SENDCHANGE も無し）。</summary>
        public const uint SpiSetScreenReader = 0x0047;

        /// <summary>親が持つパイプ端の継承を落とす。</summary>
        public const uint HandleFlagInherit = 0x00000001;

        /// <summary>PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE。</summary>
        public static readonly IntPtr ProcThreadAttributePseudoconsole = new IntPtr(0x00020016);

        /// <summary>WaitForSingleObject 無限待ち。UI からは使わない。</summary>
        public const uint Infinite = 0xFFFFFFFF;

        /// <summary>WAIT_OBJECT_0。</summary>
        public const uint WaitObject0 = 0;

        /// <summary>x64 の sizeof(STARTUPINFOEX)。cb にこれを書く。</summary>
        public const int StartupInfoExSize = 112;

        /// <summary>STARTUPINFOEX 内の lpAttributeList オフセット（x64）。</summary>
        public const int StartupInfoExAttributeOffset = 104;

        /// <summary>
        /// ConPTY のサイズ。X/Y は short。
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct Coord
        {
            /// <summary>列。</summary>
            public short X;

            /// <summary>行。</summary>
            public short Y;
        }

        /// <summary>
        /// CreateProcessW のプロセス情報。
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct ProcessInformation
        {
            /// <summary>プロセスハンドル。</summary>
            public IntPtr hProcess;

            /// <summary>スレッドハンドル。</summary>
            public IntPtr hThread;

            /// <summary>プロセス ID。</summary>
            public int dwProcessId;

            /// <summary>スレッド ID。</summary>
            public int dwThreadId;
        }

        /// <summary>
        /// STARTUPINFO。STARTUPINFOEX の先頭。
        /// </summary>
        [StructLayout(LayoutKind.Sequential, Pack = 8, CharSet = CharSet.Unicode)]
        public struct StartupInfo
        {
            /// <summary>構造体サイズ。</summary>
            public int cb;

            /// <summary>予約。</summary>
            public IntPtr lpReserved;

            /// <summary>デスクトップ。</summary>
            public IntPtr lpDesktop;

            /// <summary>タイトル。</summary>
            public IntPtr lpTitle;

            /// <summary>X。</summary>
            public int dwX;

            /// <summary>Y。</summary>
            public int dwY;

            /// <summary>幅。</summary>
            public int dwXSize;

            /// <summary>高さ。</summary>
            public int dwYSize;

            /// <summary>列数。</summary>
            public int dwXCountChars;

            /// <summary>行数。</summary>
            public int dwYCountChars;

            /// <summary>塗り属性。</summary>
            public int dwFillAttribute;

            /// <summary>フラグ。ConPTY では STARTF_USESTDHANDLES を付けない（0 のまま。付けると子がリダイレクト扱いになる）。</summary>
            public int dwFlags;

            /// <summary>ShowWindow。</summary>
            public short wShowWindow;

            /// <summary>予約 2 の長さ。</summary>
            public short cbReserved2;

            /// <summary>予約 2。</summary>
            public IntPtr lpReserved2;

            /// <summary>標準入力。</summary>
            public IntPtr hStdInput;

            /// <summary>標準出力。</summary>
            public IntPtr hStdOutput;

            /// <summary>標準エラー。</summary>
            public IntPtr hStdError;
        }

        /// <summary>
        /// STARTUPINFO + 属性リスト。
        /// </summary>
        [StructLayout(LayoutKind.Sequential, Pack = 8, CharSet = CharSet.Unicode)]
        public struct StartupInfoEx
        {
            /// <summary>先頭の STARTUPINFO。</summary>
            public StartupInfo StartupInfo;

            /// <summary>属性リスト。</summary>
            public IntPtr lpAttributeList;
        }

        /// <summary>
        /// CreatePipe のセキュリティ。
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct SecurityAttributes
        {
            /// <summary>サイズ。</summary>
            public int nLength;

            /// <summary>記述子。</summary>
            public IntPtr lpSecurityDescriptor;

            /// <summary>継承するなら 1。</summary>
            public int bInheritHandle;
        }

        /// <summary>疑似コンソールを作る。成功は HRESULT 0 以上。</summary>
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern int CreatePseudoConsole(Coord size, IntPtr hInput, IntPtr hOutput, uint dwFlags, out IntPtr phPC);

        /// <summary>疑似コンソールのサイズを変える。</summary>
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern int ResizePseudoConsole(IntPtr hPC, Coord size);

        /// <summary>疑似コンソールを閉じる。</summary>
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern void ClosePseudoConsole(IntPtr hPC);

        /// <summary>属性リストの必要サイズを取る／初期化する。</summary>
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool InitializeProcThreadAttributeList(IntPtr lpAttributeList, int dwAttributeCount, int dwFlags, ref IntPtr lpSize);

        /// <summary>属性を 1 件書く。</summary>
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UpdateProcThreadAttribute(IntPtr lpAttributeList, uint dwFlags, IntPtr attribute, IntPtr lpValue, IntPtr cbSize, IntPtr lpPreviousValue, IntPtr lpReturnSize);

        /// <summary>属性リストを破棄する。続けて FreeHGlobal する。</summary>
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern void DeleteProcThreadAttributeList(IntPtr lpAttributeList);

        /// <summary>プロセスを起動する。lpCommandLine は書き換えられる。</summary>
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CreateProcessW(
            string lpApplicationName,
            StringBuilder lpCommandLine,
            IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes,
            [MarshalAs(UnmanagedType.Bool)] bool bInheritHandles,
            uint dwCreationFlags,
            IntPtr lpEnvironment,
            string lpCurrentDirectory,
            ref StartupInfoEx lpStartupInfo,
            out ProcessInformation lpProcessInformation);

        /// <summary>匿名パイプを作る。</summary>
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CreatePipe(out IntPtr hReadPipe, out IntPtr hWritePipe, ref SecurityAttributes lpPipeAttributes, uint nSize);

        /// <summary>継承フラグを変える。</summary>
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetHandleInformation(IntPtr hObject, uint dwMask, uint dwFlags);

        /// <summary>パイプから読む。UI スレッドでは呼ばない。</summary>
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ReadFile(IntPtr hFile, byte[] lpBuffer, uint nNumberOfBytesToRead, out uint lpNumberOfBytesRead, IntPtr lpOverlapped);

        /// <summary>パイプへ書く。</summary>
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool WriteFile(IntPtr hFile, byte[] lpBuffer, uint nNumberOfBytesToWrite, out uint lpNumberOfBytesWritten, IntPtr lpOverlapped);

        /// <summary>保持しているプロセスだけを終了する。</summary>
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

        /// <summary>ハンドルを閉じる。</summary>
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CloseHandle(IntPtr hObject);

        /// <summary>終了コード。ワーカー専用。</summary>
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetExitCodeProcess(IntPtr hProcess, out uint lpExitCode);

        /// <summary>プロセス終了待ち。UI スレッドでは呼ばない。</summary>
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

        /// <summary>親の Unicode 環境ブロック。呼び出し側は FreeEnvironmentStringsW と組で解放する。</summary>
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr GetEnvironmentStringsW();

        /// <summary>GetEnvironmentStringsW が返したブロックを解放する。</summary>
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool FreeEnvironmentStringsW(IntPtr lpszEnvironmentBlock);

        /// <summary>SPI_GETSCREENREADER 用。pvParam は BOOL（4 バイト）への参照。</summary>
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref int pvParam, uint fWinIni);

        /// <summary>SPI_SETSCREENREADER 用。pvParam は NULL（IntPtr.Zero）。</summary>
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);

        /// <summary>
        /// ハンドルが有効なら CloseHandle し Zero にする。
        /// </summary>
        /// <param name="handle">対象。</param>
        public static void SafeClose(ref IntPtr handle)
        {
            if (handle == IntPtr.Zero)
            {
                return;
            }

            IntPtr h = handle;
            handle = IntPtr.Zero;
            try
            {
                CloseHandle(h);
            }
            catch
            {
            }
        }

        /// <summary>
        /// HKCU Blind Access の On が非 0 なら true。欠け・読めない・0 / "0" は false（Off）。書込はしない。
        /// </summary>
        /// <returns>スクリーンリーダー利用がレジストリ上 On なら true。</returns>
        public static bool IsBlindAccessOn()
        {
            RegistryKey key = null;
            try
            {
                key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Accessibility\Blind Access", false);
                if (key == null)
                {
                    return false;
                }

                object raw = key.GetValue("On");
                if (raw == null)
                {
                    return false;
                }

                string text = raw.ToString();
                if (string.IsNullOrEmpty(text) || text == "0")
                {
                    return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                if (key != null)
                {
                    try
                    {
                        key.Close();
                    }
                    catch
                    {
                    }
                }
            }
        }

        /// <summary>
        /// レジストリが Off かつ SPI_GETSCREENREADER が非 0 のときだけ、張り付きフラグをライブ解除する。
        /// GET 失敗は触らない。SET 失敗でも呼び出し元は Start を続行してよい。製品は値を戻さない。SM(70) は見ない。
        /// </summary>
        public static void ClearStuckScreenReaderFlag()
        {
            if (IsBlindAccessOn())
            {
                return;
            }

            int current = 0;
            if (!SystemParametersInfo(SpiGetScreenReader, 0, ref current, 0))
            {
                return;
            }

            if (current == 0)
            {
                return;
            }

            SystemParametersInfo(SpiSetScreenReader, 0, IntPtr.Zero, 0);
        }

        /// <summary>
        /// 親の Unicode 環境をコピーし、名前が TERM（無視大小）のエントリだけ落とす。
        /// COLORTERM / TERM_PROGRAM / 先頭が '=' の隠しエントリは残す。失敗時は IntPtr.Zero（継承にフォールバック）。
        /// </summary>
        /// <returns>AllocHGlobal したブロック。失敗時は Zero。呼び出し側が FreeHGlobal する。</returns>
        public static IntPtr AllocUnicodeEnvironmentWithoutTerm()
        {
            IntPtr source = IntPtr.Zero;
            try
            {
                source = GetEnvironmentStringsW();
                if (source == IntPtr.Zero)
                {
                    return IntPtr.Zero;
                }

                int destChars = 0;
                IntPtr walk = source;
                while (true)
                {
                    int n = CountEnvironmentEntryChars(walk);
                    if (n == 0)
                    {
                        break;
                    }

                    if (!IsTermEnvironmentEntry(walk, n))
                    {
                        destChars += n + 1;
                    }

                    walk = new IntPtr(walk.ToInt64() + (n + 1) * 2);
                }

                destChars++;
                if (destChars < 2)
                {
                    destChars = 2;
                }

                IntPtr dest = Marshal.AllocHGlobal(destChars * 2);
                try
                {
                    IntPtr write = dest;
                    walk = source;
                    while (true)
                    {
                        int n = CountEnvironmentEntryChars(walk);
                        if (n == 0)
                        {
                            break;
                        }

                        int bytes = (n + 1) * 2;
                        if (!IsTermEnvironmentEntry(walk, n))
                        {
                            byte[] chunk = new byte[bytes];
                            Marshal.Copy(walk, chunk, 0, bytes);
                            Marshal.Copy(chunk, 0, write, bytes);
                            write = new IntPtr(write.ToInt64() + bytes);
                        }

                        walk = new IntPtr(walk.ToInt64() + bytes);
                    }

                    Marshal.WriteInt16(write, 0);
                    if (write == dest)
                    {
                        Marshal.WriteInt16(dest, 2, 0);
                    }

                    return dest;
                }
                catch
                {
                    try
                    {
                        Marshal.FreeHGlobal(dest);
                    }
                    catch
                    {
                    }

                    return IntPtr.Zero;
                }
            }
            catch
            {
                return IntPtr.Zero;
            }
            finally
            {
                if (source != IntPtr.Zero)
                {
                    try
                    {
                        FreeEnvironmentStringsW(source);
                    }
                    catch
                    {
                    }
                }
            }
        }

        /// <summary>1 エントリの文字数（終端 null を含まない）。</summary>
        /// <param name="p">エントリ先頭。</param>
        /// <returns>文字数。</returns>
        private static int CountEnvironmentEntryChars(IntPtr p)
        {
            int n = 0;
            while (Marshal.ReadInt16(p, n * 2) != 0)
            {
                n++;
            }

            return n;
        }

        /// <summary>
        /// 先頭の '=' より前の名前が TERM なら true。eq が 0 の隠しエントリ（=C: など）は false。
        /// </summary>
        /// <param name="p">エントリ先頭。</param>
        /// <param name="charCount">終端 null を含まない長さ。</param>
        /// <returns>落とすべき TERM なら true。</returns>
        private static bool IsTermEnvironmentEntry(IntPtr p, int charCount)
        {
            int eq = -1;
            int i;
            for (i = 0; i < charCount; i++)
            {
                if (Marshal.ReadInt16(p, i * 2) == (short)'=')
                {
                    eq = i;
                    break;
                }
            }

            if (eq <= 0)
            {
                return false;
            }

            if (eq != 4)
            {
                return false;
            }

            char[] nameChars = new char[4];
            nameChars[0] = (char)Marshal.ReadInt16(p, 0);
            nameChars[1] = (char)Marshal.ReadInt16(p, 2);
            nameChars[2] = (char)Marshal.ReadInt16(p, 4);
            nameChars[3] = (char)Marshal.ReadInt16(p, 6);
            return string.Equals(new string(nameChars), "TERM", StringComparison.OrdinalIgnoreCase);
        }
    }
}
