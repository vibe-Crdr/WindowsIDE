using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace WindowsIDE.Workspace
{
    /// <summary>
    /// ファイル／フォルダをごみ箱へ送る。shell32 の P/Invoke。追加 /r は不要。WinForms に依存しない。
    /// </summary>
    public static class WorkspaceRecycle
    {
        private const uint FO_DELETE = 3;
        private const ushort FOF_NOCONFIRMATION = 0x10;
        private const ushort FOF_ALLOWUNDO = 0x40;
        private const ushort FOF_NOERRORUI = 0x400;

        /// <summary>
        /// fullPath をごみ箱へ移す（FO_DELETE + FOF_ALLOWUNDO）。完全削除はしない。
        /// </summary>
        /// <param name="hwnd">親ウィンドウ。無くてよい。</param>
        /// <param name="fullPath">対象の絶対パス。</param>
        /// <param name="error">失敗理由。</param>
        /// <returns>戻り 0 かつ中止でなければ true。</returns>
        public static bool TrySendToRecycleBin(IntPtr hwnd, string fullPath, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(fullPath))
            {
                error = "パスがありません。";
                return false;
            }

            string full;
            try
            {
                full = Path.GetFullPath(fullPath);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }

            if (!File.Exists(full) && !Directory.Exists(full))
            {
                error = "ファイルまたはフォルダが見つかりません。";
                return false;
            }

            IntPtr pFrom = IntPtr.Zero;
            try
            {
                pFrom = AllocDoubleNull(full);
                SHFILEOPSTRUCT op = new SHFILEOPSTRUCT();
                op.hwnd = hwnd;
                op.wFunc = FO_DELETE;
                op.pFrom = pFrom;
                op.pTo = IntPtr.Zero;
                op.fFlags = (ushort)(FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_NOERRORUI);
                op.fAnyOperationsAborted = false;
                op.hNameMappings = IntPtr.Zero;
                op.lpszProgressTitle = IntPtr.Zero;

                int result = SHFileOperationW(ref op);
                if (result != 0)
                {
                    error = "ごみ箱へ移せませんでした。(" + result.ToString() + ")";
                    return false;
                }

                if (op.fAnyOperationsAborted)
                {
                    error = "取り消されました。";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
            finally
            {
                if (pFrom != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(pFrom);
                }
            }
        }

        private static IntPtr AllocDoubleNull(string path)
        {
            byte[] bytes = Encoding.Unicode.GetBytes(path + "\0\0");
            IntPtr ptr = Marshal.AllocHGlobal(bytes.Length);
            Marshal.Copy(bytes, 0, ptr, bytes.Length);
            return ptr;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHFILEOPSTRUCT
        {
            public IntPtr hwnd;
            public uint wFunc;
            public IntPtr pFrom;
            public IntPtr pTo;
            public ushort fFlags;
            [MarshalAs(UnmanagedType.Bool)]
            public bool fAnyOperationsAborted;
            public IntPtr hNameMappings;
            public IntPtr lpszProgressTitle;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "SHFileOperationW")]
        private static extern int SHFileOperationW(ref SHFILEOPSTRUCT fileOp);
    }
}
