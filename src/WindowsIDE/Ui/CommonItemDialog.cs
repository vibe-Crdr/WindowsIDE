using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace WindowsIDE.Ui
{
    /// <summary>
    /// フォルダ選択・ファイルを開く・名前を付けて保存用の Windows Common Item Dialog。ole32 / shell32 の P/Invoke のみ。追加 /r は不要。
    /// </summary>
    public static class CommonItemDialog
    {
        private const uint ClsctxInprocServer = 1;
        private const uint FosOverwritePrompt = 0x00000002;
        private const uint FosPickFolders = 0x00000020;
        private const uint FosForceFileSystem = 0x00000040;
        private const uint FosPathMustExist = 0x00000800;
        private const uint FosFileMustExist = 0x00001000;

        private enum DialogKind
        {
            Folder,
            OpenFile,
            SaveFile
        }

        private const uint SigdnFileSysPath = 0x80058000;
        private const int HrOk = 0;
        private const int HrErrorCancelled = unchecked((int)0x800704C7);

        /// <summary>
        /// フォルダを選ぶダイアログを出す。UI スレッドのみ。
        /// </summary>
        /// <param name="owner">親ウィンドウ。null ならオーナーなし。</param>
        /// <param name="title">ダイアログ題名。</param>
        /// <param name="initialPath">初期フォルダ。作成や QI に失敗したら使わない。</param>
        /// <param name="path">選んだフルパス。キャンセル時は null。</param>
        /// <returns>選んだら true。キャンセルは false。</returns>
        public static bool TryPickFolder(IWin32Window owner, string title, string initialPath, out string path)
        {
            return TryShow(DialogKind.Folder, owner, title, initialPath, null, null, out path);
        }

        /// <summary>
        /// 既存ファイルを選ぶダイアログを出す。UI スレッドのみ。PICKFOLDERS と OVERWRITEPROMPT は付けない。
        /// </summary>
        /// <param name="owner">親ウィンドウ。null ならオーナーなし。</param>
        /// <param name="title">ダイアログ題名。</param>
        /// <param name="initialDirectory">初期フォルダ。作成や QI に失敗したら使わない。</param>
        /// <param name="filter">COMDLG 形式（名前|仕様 の | 区切り）。</param>
        /// <param name="path">選んだフルパス。キャンセル時は null。</param>
        /// <returns>選んだら true。キャンセルは false。</returns>
        public static bool TryPickOpenFile(IWin32Window owner, string title, string initialDirectory, string filter, out string path)
        {
            return TryShow(DialogKind.OpenFile, owner, title, initialDirectory, null, filter, out path);
        }

        /// <summary>
        /// 名前を付けて保存ダイアログを出す。UI スレッドのみ。SetDefaultExtension は呼ばない。
        /// </summary>
        /// <param name="owner">親ウィンドウ。null ならオーナーなし。</param>
        /// <param name="title">ダイアログ題名。</param>
        /// <param name="initialDirectory">初期フォルダ。作成や QI に失敗したら使わない。</param>
        /// <param name="fileName">初期ファイル名。</param>
        /// <param name="filter">COMDLG 形式（名前|仕様 の | 区切り）。</param>
        /// <param name="path">選んだフルパス。キャンセル時は null。</param>
        /// <returns>選んだら true。キャンセルは false。</returns>
        public static bool TryPickSaveFile(IWin32Window owner, string title, string initialDirectory, string fileName, string filter, out string path)
        {
            return TryShow(DialogKind.SaveFile, owner, title, initialDirectory, fileName, filter, out path);
        }

        private static bool TryShow(DialogKind kind, IWin32Window owner, string title, string initialDirectory, string fileName, string filter, out string path)
        {
            path = null;
            EnsureUiThread(owner);

            Guid clsid = (kind == DialogKind.SaveFile)
                ? new Guid("C0B4E2F3-BA21-4773-8DBA-335EC946EB8B")
                : new Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7");
            Guid iidDialog = new Guid("42F85136-DB7E-439C-85F1-E4075D135FC8");
            Guid iidShellItem = new Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE");

            IFileDialog dialog = null;
            IShellItem folderItem = null;
            IShellItem resultItem = null;
            IntPtr displayName = IntPtr.Zero;
            try
            {
                int hr = Native.CoCreateInstance(ref clsid, IntPtr.Zero, ClsctxInprocServer, ref iidDialog, out dialog);
                if (hr != HrOk)
                {
                    Marshal.ThrowExceptionForHR(hr);
                }

                if (dialog == null)
                {
                    throw new InvalidOperationException("Common Item Dialog を作成できませんでした。");
                }

                if (!string.IsNullOrEmpty(title))
                {
                    hr = dialog.SetTitle(title);
                    if (hr != HrOk)
                    {
                        Marshal.ThrowExceptionForHR(hr);
                    }
                }

                uint options;
                hr = dialog.GetOptions(out options);
                if (hr != HrOk)
                {
                    Marshal.ThrowExceptionForHR(hr);
                }

                if (kind == DialogKind.Folder)
                {
                    options = options | FosPickFolders | FosForceFileSystem | FosPathMustExist;
                }
                else if (kind == DialogKind.OpenFile)
                {
                    options = options | FosFileMustExist | FosForceFileSystem | FosPathMustExist;
                }
                else
                {
                    options = options | FosOverwritePrompt | FosForceFileSystem | FosPathMustExist;
                }

                hr = dialog.SetOptions(options);
                if (hr != HrOk)
                {
                    Marshal.ThrowExceptionForHR(hr);
                }

                if (!string.IsNullOrEmpty(initialDirectory))
                {
                    hr = Native.SHCreateItemFromParsingName(initialDirectory, IntPtr.Zero, ref iidShellItem, out folderItem);
                    if (hr == HrOk && folderItem != null)
                    {
                        hr = dialog.SetFolder(folderItem);
                        if (hr != HrOk)
                        {
                            Marshal.ThrowExceptionForHR(hr);
                        }
                    }
                }

                if (kind == DialogKind.SaveFile && !string.IsNullOrEmpty(fileName))
                {
                    hr = dialog.SetFileName(fileName);
                    if (hr != HrOk)
                    {
                        Marshal.ThrowExceptionForHR(hr);
                    }
                }

                if (kind != DialogKind.Folder)
                {
                    COMDLG_FILTERSPEC[] specs = ParseFilter(filter);
                    if (specs.Length > 0)
                    {
                        hr = dialog.SetFileTypes((uint)specs.Length, specs);
                        if (hr != HrOk)
                        {
                            Marshal.ThrowExceptionForHR(hr);
                        }
                    }
                }

                IntPtr hwnd = (owner == null) ? IntPtr.Zero : owner.Handle;
                hr = dialog.Show(hwnd);
                if (hr == HrErrorCancelled)
                {
                    path = null;
                    return false;
                }

                if (hr != HrOk)
                {
                    Marshal.ThrowExceptionForHR(hr);
                }

                hr = dialog.GetResult(out resultItem);
                if (hr != HrOk)
                {
                    Marshal.ThrowExceptionForHR(hr);
                }

                if (resultItem == null)
                {
                    throw new InvalidOperationException("Common Item Dialog の結果を取得できませんでした。");
                }

                hr = resultItem.GetDisplayName(SigdnFileSysPath, out displayName);
                if (hr != HrOk)
                {
                    Marshal.ThrowExceptionForHR(hr);
                }

                path = Marshal.PtrToStringUni(displayName);
                return true;
            }
            finally
            {
                if (displayName != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(displayName);
                }

                ReleaseCom(resultItem);
                ReleaseCom(folderItem);
                ReleaseCom(dialog);
            }
        }

        private static void EnsureUiThread(IWin32Window owner)
        {
            Control control = owner as Control;
            if (control != null && control.InvokeRequired)
            {
                throw new InvalidOperationException("Common Item Dialog は UI スレッドでのみ呼べます。");
            }

            if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
            {
                throw new InvalidOperationException("Common Item Dialog は UI スレッドでのみ呼べます。");
            }
        }

        private static COMDLG_FILTERSPEC[] ParseFilter(string filter)
        {
            if (string.IsNullOrEmpty(filter))
            {
                return new COMDLG_FILTERSPEC[0];
            }

            string[] parts = filter.Split('|');
            int n = parts.Length / 2;
            COMDLG_FILTERSPEC[] specs = new COMDLG_FILTERSPEC[n];
            for (int i = 0; i < n; i++)
            {
                specs[i].pszName = parts[i * 2];
                specs[i].pszSpec = parts[i * 2 + 1];
            }

            return specs;
        }

        private static void ReleaseCom(object com)
        {
            if (com != null)
            {
                Marshal.ReleaseComObject(com);
            }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct COMDLG_FILTERSPEC
        {
            [MarshalAs(UnmanagedType.LPWStr)]
            public string pszName;

            [MarshalAs(UnmanagedType.LPWStr)]
            public string pszSpec;
        }

        [ComImport]
        [Guid("42F85136-DB7E-439C-85F1-E4075D135FC8")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IFileDialog
        {
            [PreserveSig]
            int Show(IntPtr hwndParent);

            [PreserveSig]
            int SetFileTypes(uint cFileTypes, [MarshalAs(UnmanagedType.LPArray)] COMDLG_FILTERSPEC[] rgFilterSpec);

            void SetFileTypeIndex(uint iFileType);

            void GetFileTypeIndex(out uint piFileType);

            void Advise(IntPtr pfde, out uint pdwCookie);

            void Unadvise(uint dwCookie);

            [PreserveSig]
            int SetOptions(uint fos);

            [PreserveSig]
            int GetOptions(out uint pfos);

            void SetDefaultFolder([MarshalAs(UnmanagedType.Interface)] IShellItem psi);

            [PreserveSig]
            int SetFolder([MarshalAs(UnmanagedType.Interface)] IShellItem psi);

            void GetFolder([MarshalAs(UnmanagedType.Interface)] out IShellItem ppsi);

            void GetCurrentSelection([MarshalAs(UnmanagedType.Interface)] out IShellItem ppsi);

            [PreserveSig]
            int SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszName);

            void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);

            [PreserveSig]
            int SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);

            void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string pszText);

            void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string pszLabel);

            [PreserveSig]
            int GetResult([MarshalAs(UnmanagedType.Interface)] out IShellItem ppsi);

            void AddPlace([MarshalAs(UnmanagedType.Interface)] IShellItem psi, int fdap);

            void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);

            void Close(int hr);

            void SetClientGuid(ref Guid guid);

            void ClearClientData();

            void SetFilter(IntPtr pFilter);
        }

        [ComImport]
        [Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItem
        {
            void BindToHandler(IntPtr pbc, [In] ref Guid bhid, [In] ref Guid riid, out IntPtr ppv);

            void GetParent([MarshalAs(UnmanagedType.Interface)] out IShellItem ppsi);

            [PreserveSig]
            int GetDisplayName(uint sigdnName, out IntPtr ppszName);

            void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);

            void Compare([MarshalAs(UnmanagedType.Interface)] IShellItem psi, uint hint, out int piOrder);
        }

        private static class Native
        {
            [DllImport("ole32.dll", ExactSpelling = true, PreserveSig = true)]
            public static extern int CoCreateInstance(
                [In] ref Guid rclsid,
                IntPtr pUnkOuter,
                uint dwClsContext,
                [In] ref Guid riid,
                [MarshalAs(UnmanagedType.Interface)] out IFileDialog ppv);

            [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
            public static extern int SHCreateItemFromParsingName(
                string pszPath,
                IntPtr pbc,
                [In] ref Guid riid,
                [MarshalAs(UnmanagedType.Interface)] out IShellItem ppv);
        }
    }
}
