using System;
using System.Drawing;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using WindowsIDE.Editor;

namespace WindowsIDE.Ui
{
    /// <summary>
    /// imm32 の変換状態をオーナー描画クライアントへ渡す。
    /// </summary>
    public interface IImeClient
    {
        /// <summary>IME 操作対象の HWND。</summary>
        IntPtr WindowHandle { get; }

        /// <summary>GCS_COMPSTR のときだけ未確定文字列を入れる。</summary>
        /// <param name="text">未確定。null は空。</param>
        void SetCompositionString(string text);

        /// <summary>GCS_CURSORPOS。クライアントが ClampCursor する。</summary>
        /// <param name="rawCursor">ImmGetCompositionStringW の戻り値。</param>
        void SetCompositionCursor(int rawCursor);

        /// <summary>RESULTSTR または END で未確定を空にする。挿入はしない。</summary>
        void ClearComposition();

        /// <summary>変換窓と候補除外矩形を更新する。</summary>
        void UpdateImeWindow();

        /// <summary>IMR_QUERYCHARPOSITION の IMECHARPOSITION を埋める。</summary>
        /// <param name="lParam">構造体ポインタ。</param>
        void FillQueryCharPosition(IntPtr lParam);

        /// <summary>再描画を要求する。</summary>
        void RequestInvalidate();
    }

    /// <summary>
    /// NativeIme.Dispatch が WndProc に返す続き方。
    /// </summary>
    public enum ImeDispatchKind
    {
        /// <summary>処理済み。base.WndProc は呼ばない。</summary>
        Consumed,

        /// <summary>状態を更新済み。続けて base.WndProc を呼ぶ。</summary>
        ContinueBase,

        /// <summary>LParam を直した。base のあと UpdateImeWindow する。</summary>
        ContinueBaseThenUpdate,

        /// <summary>IME 対象外。base.WndProc のみ。</summary>
        None
    }

    /// <summary>
    /// imm32 の共通呼び出しと WM_IME_* の振り分け。システム変換窓は出さない。
    /// </summary>
    public static class NativeIme
    {
        private const int WM_IME_STARTCOMPOSITION = 0x010D;
        private const int WM_IME_ENDCOMPOSITION = 0x010E;
        private const int WM_IME_COMPOSITION = 0x010F;
        private const int WM_IME_SETCONTEXT = 0x0281;
        private const int WM_IME_REQUEST = 0x0288;
        private const int IMR_QUERYCHARPOSITION = 6;
        private const int GCS_COMPSTR = 0x0008;
        private const int GCS_CURSORPOS = 0x0080;
        private const int GCS_RESULTSTR = 0x0800;
        private const int CFS_POINT = 2;
        private const int CFS_EXCLUDE = 0x0080;

        private static string compositionFace;
        private static bool compositionFaceResolved;

        [DllImport("imm32.dll")]
        private static extern IntPtr ImmGetContext(IntPtr hWnd);

        [DllImport("imm32.dll")]
        private static extern bool ImmReleaseContext(IntPtr hWnd, IntPtr hIMC);

        [DllImport("imm32.dll", CharSet = CharSet.Unicode)]
        private static extern int ImmGetCompositionStringW(IntPtr hIMC, int dwIndex, byte[] lpBuf, int dwBufLen);

        [DllImport("imm32.dll")]
        private static extern bool ImmSetCompositionWindow(IntPtr hIMC, ref COMPOSITIONFORM lpCompForm);

        [DllImport("imm32.dll")]
        private static extern bool ImmSetCandidateWindow(IntPtr hIMC, ref CANDIDATEFORM lpCandidate);

        [DllImport("imm32.dll", CharSet = CharSet.Unicode)]
        private static extern bool ImmSetCompositionFontW(IntPtr hIMC, ref LOGFONT lplf);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct COMPOSITIONFORM
        {
            public int dwStyle;
            public POINT ptCurrentPos;
            public RECT rcArea;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct CANDIDATEFORM
        {
            public int dwIndex;
            public int dwStyle;
            public POINT ptCurrentPos;
            public RECT rcArea;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IMECHARPOSITION
        {
            public int dwSize;
            public int dwCharPos;
            public POINT pt;
            public int cLineHeight;
            public RECT rcDocument;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct LOGFONT
        {
            public int lfHeight;
            public int lfWidth;
            public int lfEscapement;
            public int lfOrientation;
            public int lfWeight;
            public byte lfItalic;
            public byte lfUnderline;
            public byte lfStrikeOut;
            public byte lfCharSet;
            public byte lfOutPrecision;
            public byte lfClipPrecision;
            public byte lfQuality;
            public byte lfPitchAndFamily;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string lfFaceName;
        }

        /// <summary>
        /// WM_IME_* をクライアントへ振り分ける。START は base / DefWndProc に渡さない（システム変換窓の既定箱を出さない）。
        /// </summary>
        /// <param name="m">ウィンドウメッセージ。</param>
        /// <param name="client">描画側。null なら None。</param>
        /// <returns>WndProc の続き方。</returns>
        public static ImeDispatchKind Dispatch(ref Message m, IImeClient client)
        {
            if (client == null)
            {
                return ImeDispatchKind.None;
            }

            if (m.Msg == WM_IME_STARTCOMPOSITION)
            {
                client.UpdateImeWindow();
                m.Result = IntPtr.Zero;
                return ImeDispatchKind.Consumed;
            }

            if (m.Msg == WM_IME_REQUEST && m.WParam.ToInt32() == IMR_QUERYCHARPOSITION)
            {
                if (m.LParam != IntPtr.Zero)
                {
                    client.FillQueryCharPosition(m.LParam);
                }

                m.Result = (IntPtr)1;
                return ImeDispatchKind.Consumed;
            }

            if (m.Msg == WM_IME_SETCONTEXT)
            {
                long lp = m.LParam.ToInt64();
                lp = lp & ~0x80000000L;
                m.LParam = (IntPtr)lp;
                return ImeDispatchKind.ContinueBaseThenUpdate;
            }

            if (m.Msg == WM_IME_COMPOSITION)
            {
                int flag = m.LParam.ToInt32();
                if ((flag & GCS_COMPSTR) != 0)
                {
                    client.SetCompositionString(GetCompositionString(client.WindowHandle, GCS_COMPSTR));
                }

                if ((flag & GCS_CURSORPOS) != 0)
                {
                    client.SetCompositionCursor(GetCursorPos(client.WindowHandle));
                }

                if ((flag & GCS_RESULTSTR) != 0)
                {
                    client.ClearComposition();
                }

                client.UpdateImeWindow();
                client.RequestInvalidate();
                return ImeDispatchKind.ContinueBase;
            }

            if (m.Msg == WM_IME_ENDCOMPOSITION)
            {
                client.ClearComposition();
                client.RequestInvalidate();
                return ImeDispatchKind.ContinueBase;
            }

            return ImeDispatchKind.None;
        }

        /// <summary>
        /// 変換フォントを本文の物理サイズと IME 可視のシステム顔にする。失敗は無視する。
        /// </summary>
        /// <param name="hwnd">対象 HWND。</param>
        /// <param name="physicalPx">本文サイズの物理ピクセル。</param>
        public static void ApplyCompositionFont(IntPtr hwnd, int physicalPx)
        {
            IntPtr himc = ImmGetContext(hwnd);
            if (himc == IntPtr.Zero)
            {
                return;
            }

            try
            {
                LOGFONT lf = new LOGFONT();
                lf.lfHeight = ImeLayout.CompositionFontHeight(physicalPx);
                lf.lfWeight = 400;
                lf.lfCharSet = 128;
                lf.lfFaceName = ResolveCompositionFace();
                ImmSetCompositionFontW(himc, ref lf);
            }
            catch
            {
            }
            finally
            {
                ImmReleaseContext(hwnd, himc);
            }
        }

        /// <summary>
        /// 変換文字列を読む。失敗や空は ""。
        /// </summary>
        /// <param name="hwnd">対象 HWND。</param>
        /// <param name="flag">GCS_*。</param>
        /// <returns>文字列。</returns>
        public static string GetCompositionString(IntPtr hwnd, int flag)
        {
            IntPtr himc = ImmGetContext(hwnd);
            if (himc == IntPtr.Zero)
            {
                return "";
            }

            try
            {
                int len = ImmGetCompositionStringW(himc, flag, null, 0);
                if (len <= 0)
                {
                    return "";
                }

                byte[] buf = new byte[len];
                ImmGetCompositionStringW(himc, flag, buf, len);
                return Encoding.Unicode.GetString(buf).TrimEnd('\0');
            }
            finally
            {
                ImmReleaseContext(hwnd, himc);
            }
        }

        /// <summary>
        /// GCS_CURSORPOS は ImmGetCompositionStringW の戻り値（バッファではない）。
        /// </summary>
        /// <param name="hwnd">対象 HWND。</param>
        /// <returns>カーソル指数。失敗は 0。</returns>
        public static int GetCursorPos(IntPtr hwnd)
        {
            IntPtr himc = ImmGetContext(hwnd);
            if (himc == IntPtr.Zero)
            {
                return 0;
            }

            try
            {
                int index = ImmGetCompositionStringW(himc, GCS_CURSORPOS, null, 0);
                if (index < 0)
                {
                    return 0;
                }

                return index;
            }
            finally
            {
                ImmReleaseContext(hwnd, himc);
            }
        }

        /// <summary>
        /// 変換窓の位置をクライアント座標で指定する。
        /// </summary>
        /// <param name="hwnd">対象 HWND。</param>
        /// <param name="x">クライアント X。</param>
        /// <param name="y">クライアント Y。</param>
        public static void MoveCompositionWindow(IntPtr hwnd, int x, int y)
        {
            IntPtr himc = ImmGetContext(hwnd);
            if (himc == IntPtr.Zero)
            {
                return;
            }

            try
            {
                COMPOSITIONFORM form = new COMPOSITIONFORM();
                form.dwStyle = CFS_POINT;
                form.ptCurrentPos.x = x;
                form.ptCurrentPos.y = y;
                ImmSetCompositionWindow(himc, ref form);
            }
            finally
            {
                ImmReleaseContext(hwnd, himc);
            }
        }

        /// <summary>
        /// 候補リストが被らないよう除外矩形を入れる。
        /// </summary>
        /// <param name="hwnd">対象 HWND。</param>
        /// <param name="clientExclude">クライアント座標の除外。</param>
        public static void SetCandidateExclude(IntPtr hwnd, Rectangle clientExclude)
        {
            IntPtr himc = ImmGetContext(hwnd);
            if (himc == IntPtr.Zero)
            {
                return;
            }

            try
            {
                CANDIDATEFORM form = new CANDIDATEFORM();
                form.dwIndex = 0;
                form.dwStyle = CFS_EXCLUDE;
                form.ptCurrentPos.x = clientExclude.Left;
                form.ptCurrentPos.y = clientExclude.Top;
                form.rcArea.left = clientExclude.Left;
                form.rcArea.top = clientExclude.Top;
                form.rcArea.right = clientExclude.Right;
                form.rcArea.bottom = clientExclude.Bottom;
                ImmSetCandidateWindow(himc, ref form);
            }
            finally
            {
                ImmReleaseContext(hwnd, himc);
            }
        }

        /// <summary>
        /// IMECHARPOSITION.dwCharPos を読む。
        /// </summary>
        /// <param name="lParam">構造体ポインタ。</param>
        /// <returns>要求された文字位置。</returns>
        public static int ReadCharPos(IntPtr lParam)
        {
            IMECHARPOSITION pos = (IMECHARPOSITION)Marshal.PtrToStructure(lParam, typeof(IMECHARPOSITION));
            return pos.dwCharPos;
        }

        /// <summary>
        /// IMECHARPOSITION を画面座標と文書矩形で埋める。
        /// </summary>
        /// <param name="lParam">構造体ポインタ。</param>
        /// <param name="charPos">文字位置。</param>
        /// <param name="screenPt">その文字の画面座標。</param>
        /// <param name="lineHeight">cLineHeight。</param>
        /// <param name="docLeft">rcDocument 左。</param>
        /// <param name="docTop">rcDocument 上。</param>
        /// <param name="docRight">rcDocument 右。</param>
        /// <param name="docBottom">rcDocument 下。</param>
        public static void WriteCharPosition(IntPtr lParam, int charPos, Point screenPt, int lineHeight, int docLeft, int docTop, int docRight, int docBottom)
        {
            IMECHARPOSITION pos = (IMECHARPOSITION)Marshal.PtrToStructure(lParam, typeof(IMECHARPOSITION));
            pos.dwCharPos = charPos;
            pos.pt.x = screenPt.X;
            pos.pt.y = screenPt.Y;
            pos.cLineHeight = lineHeight;
            pos.rcDocument.left = docLeft;
            pos.rcDocument.top = docTop;
            pos.rcDocument.right = docRight;
            pos.rcDocument.bottom = docBottom;
            Marshal.StructureToPtr(pos, lParam, false);
        }

        private static string ResolveCompositionFace()
        {
            if (compositionFaceResolved)
            {
                return compositionFace;
            }

            compositionFaceResolved = true;
            compositionFace = "MS Gothic";
            try
            {
                using (InstalledFontCollection installed = new InstalledFontCollection())
                {
                    if (HasInstalledFamily(installed, "Yu Gothic"))
                    {
                        compositionFace = "Yu Gothic";
                    }
                    else if (HasInstalledFamily(installed, "Yu Gothic UI"))
                    {
                        compositionFace = "Yu Gothic UI";
                    }
                    else if (HasInstalledFamily(installed, "MS Gothic"))
                    {
                        compositionFace = "MS Gothic";
                    }
                }
            }
            catch
            {
                compositionFace = "MS Gothic";
            }

            return compositionFace;
        }

        private static bool HasInstalledFamily(InstalledFontCollection installed, string name)
        {
            FontFamily[] families = installed.Families;
            if (families == null)
            {
                return false;
            }

            int i;
            for (i = 0; i < families.Length; i++)
            {
                if (string.Equals(families[i].Name, name, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
