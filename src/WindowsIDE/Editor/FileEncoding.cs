using System;
using System.Text;

namespace WindowsIDE.Editor
{
    /// <summary>
    /// 開いたファイルのエンコーディングと改行。WinForms に依存しない。
    /// </summary>
    public sealed class FileEncodingInfo
    {
        private int codePage;
        private bool hasBom;
        private bool utf16BigEndian;
        private string newLine;

        /// <summary>
        /// 既定の新規ファイル用（UTF-8 BOM + CRLF）。
        /// </summary>
        public FileEncodingInfo()
        {
            this.codePage = 65001;
            this.hasBom = true;
            this.utf16BigEndian = false;
            this.newLine = "\r\n";
        }

        /// <summary>
        /// 検出結果から作る。
        /// </summary>
        /// <param name="codePage">65001=UTF-8、932=CP932、1200=UTF-16。</param>
        /// <param name="hasBom">BOM 付きなら true。</param>
        /// <param name="utf16BigEndian">UTF-16 BE なら true。</param>
        /// <param name="newLine">CRLF または LF。</param>
        public FileEncodingInfo(int codePage, bool hasBom, bool utf16BigEndian, string newLine)
        {
            this.codePage = codePage;
            this.hasBom = hasBom;
            this.utf16BigEndian = utf16BigEndian;
            this.newLine = (newLine == "\n") ? "\n" : "\r\n";
        }

        /// <summary>コードページ。UTF-8 は 65001、CP932 は 932、UTF-16 は 1200。</summary>
        public int CodePage { get { return this.codePage; } }

        /// <summary>BOM を書くなら true。</summary>
        public bool HasBom { get { return this.hasBom; } set { this.hasBom = value; } }

        /// <summary>UTF-16 のビッグエンディアンなら true。</summary>
        public bool Utf16BigEndian { get { return this.utf16BigEndian; } }

        /// <summary>改行（CRLF または LF）。</summary>
        public string NewLine { get { return this.newLine; } }

        /// <summary>UTF-8 なら true。</summary>
        public bool IsUtf8
        {
            get { return this.codePage == 65001; }
        }

        /// <summary>
        /// ステータス表示用の短い名前を返す。
        /// </summary>
        public string GetDisplayName()
        {
            if (this.codePage == 65001)
            {
                return this.hasBom ? "UTF-8 BOM" : "UTF-8";
            }

            if (this.codePage == 1200)
            {
                return this.utf16BigEndian ? "UTF-16 BE" : "UTF-16 LE";
            }

            if (this.codePage == 932)
            {
                return "CP932";
            }

            return "cp" + this.codePage.ToString();
        }

        /// <summary>
        /// 書き込み用 Encoding を返す。
        /// </summary>
        public Encoding CreateEncoding()
        {
            if (this.codePage == 65001)
            {
                return new UTF8Encoding(this.hasBom);
            }

            if (this.codePage == 1200)
            {
                return this.utf16BigEndian ? new UnicodeEncoding(true, this.hasBom) : new UnicodeEncoding(false, this.hasBom);
            }

            if (this.codePage == 932)
            {
                return Encoding.GetEncoding(932, EncoderFallback.ExceptionFallback, DecoderFallback.ReplacementFallback);
            }

            return Encoding.GetEncoding(this.codePage);
        }
    }

    /// <summary>
    /// 開く・保存時のエンコーディング判定。WinForms に依存しない。
    /// </summary>
    public static class FileEncoding
    {
        /// <summary>
        /// バイト列をテキストとして読む。バイナリ疑いは false。
        /// </summary>
        /// <param name="data">ファイルバイト。</param>
        /// <param name="text">成功時の文字列。</param>
        /// <param name="info">検出したエンコーディング。</param>
        /// <param name="error">失敗時の理由。</param>
        /// <returns>開けたなら true。</returns>
        public static bool TryDecode(byte[] data, out string text, out FileEncodingInfo info, out string error)
        {
            text = null;
            info = null;
            error = null;
            if (data == null)
            {
                data = new byte[0];
            }

            if (HasBom(data, 0xEF, 0xBB, 0xBF))
            {
                Encoding utf8 = new UTF8Encoding(true, false);
                text = utf8.GetString(data, 3, data.Length - 3);
                info = new FileEncodingInfo(65001, true, false, DetectNewLine(text));
                return true;
            }

            if (data.Length >= 2 && data[0] == 0xFF && data[1] == 0xFE)
            {
                Encoding le = new UnicodeEncoding(false, true);
                text = le.GetString(data, 2, data.Length - 2);
                info = new FileEncodingInfo(1200, true, false, DetectNewLine(text));
                return true;
            }

            if (data.Length >= 2 && data[0] == 0xFE && data[1] == 0xFF)
            {
                Encoding be = new UnicodeEncoding(true, true);
                text = be.GetString(data, 2, data.Length - 2);
                info = new FileEncodingInfo(1200, true, true, DetectNewLine(text));
                return true;
            }

            if (LooksBinary(data))
            {
                error = "バイナリファイルの可能性があるため開けません。";
                return false;
            }

            if (IsValidUtf8(data))
            {
                Encoding utf8 = new UTF8Encoding(false, false);
                text = utf8.GetString(data);
                info = new FileEncodingInfo(65001, false, false, DetectNewLine(text));
                return true;
            }

            Encoding cp932 = Encoding.GetEncoding(932);
            text = cp932.GetString(data);
            info = new FileEncodingInfo(932, false, false, DetectNewLine(text));
            return true;
        }

        /// <summary>
        /// 保存バイト列を作る。開いた Encoding と BOM を維持する。例外: .cs かつ UTF-8 は必ず BOM。
        /// </summary>
        /// <param name="text">本文。</param>
        /// <param name="info">開いたときの情報。null なら新規扱い。</param>
        /// <param name="filePath">保存先。拡張子判定に使う。</param>
        /// <returns>書き込むバイト。</returns>
        public static byte[] GetBytesToSave(string text, FileEncodingInfo info, string filePath)
        {
            if (info == null)
            {
                info = new FileEncodingInfo();
            }

            if (text == null)
            {
                text = "";
            }

            bool forceBom = info.IsUtf8 && HasCsExtension(filePath);
            bool bom = forceBom ? true : info.HasBom;
            FileEncodingInfo write = new FileEncodingInfo(info.CodePage, bom, info.Utf16BigEndian, info.NewLine);
            string normalized = NormalizeNewLines(text, write.NewLine);
            Encoding enc = write.CreateEncoding();
            byte[] preamble = bom ? enc.GetPreamble() : new byte[0];
            byte[] body = enc.GetBytes(normalized);
            if (preamble.Length == 0)
            {
                return body;
            }

            byte[] all = new byte[preamble.Length + body.Length];
            Buffer.BlockCopy(preamble, 0, all, 0, preamble.Length);
            Buffer.BlockCopy(body, 0, all, preamble.Length, body.Length);
            return all;
        }

        /// <summary>
        /// 新規空ファイルのバイト。`.bas`/`.cls` は CP932 BOM なし CRLF、それ以外は UTF-8 BOM + CRLF。
        /// </summary>
        /// <param name="filePath">作成先。拡張子判定に使う。</param>
        /// <returns>書き込むバイト。</returns>
        public static byte[] GetBytesForNewEmptyFile(string filePath)
        {
            if (!string.IsNullOrEmpty(filePath)
                && (filePath.EndsWith(".bas", StringComparison.OrdinalIgnoreCase)
                    || filePath.EndsWith(".cls", StringComparison.OrdinalIgnoreCase)))
            {
                return GetBytesToSave("", new FileEncodingInfo(932, false, false, "\r\n"), filePath);
            }

            return GetBytesToSave("", new FileEncodingInfo(), filePath);
        }

        /// <summary>
        /// 本文の改行を CRLF か LF かに揃える。
        /// </summary>
        public static string NormalizeNewLines(string text, string newLine)
        {
            if (text == null)
            {
                return "";
            }

            string n = text.Replace("\r\n", "\n").Replace("\r", "\n");
            if (newLine == "\n")
            {
                return n;
            }

            return n.Replace("\n", "\r\n");
        }

        /// <summary>
        /// 本文から改行種を決める。CRLF が一つでもあれば CRLF。LF のみなら LF。無ければ CRLF。
        /// </summary>
        public static string DetectNewLine(string text)
        {
            if (text == null)
            {
                return "\r\n";
            }

            if (text.IndexOf("\r\n") >= 0)
            {
                return "\r\n";
            }

            if (text.IndexOf('\n') >= 0)
            {
                return "\n";
            }

            return "\r\n";
        }

        /// <summary>
        /// UTF-16 BOM 無しで NUL が多い、または制御文字が多い場合はバイナリとみなす。
        /// </summary>
        public static bool LooksBinary(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                return false;
            }

            int n = data.Length < 8192 ? data.Length : 8192;
            int control = 0;
            for (int i = 0; i < n; i++)
            {
                byte b = data[i];
                if (b == 0)
                {
                    return true;
                }

                if (b < 7 || (b > 13 && b < 32 && b != 27))
                {
                    control++;
                }
            }

            return control > (n / 8);
        }

        private static bool HasBom(byte[] data, byte a, byte b, byte c)
        {
            return data.Length >= 3 && data[0] == a && data[1] == b && data[2] == c;
        }

        private static bool IsValidUtf8(byte[] data)
        {
            try
            {
                Encoding enc = new UTF8Encoding(false, true);
                enc.GetString(data);
                return true;
            }
            catch (DecoderFallbackException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private static bool HasCsExtension(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return false;
            }

            return filePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);
        }
    }
}
