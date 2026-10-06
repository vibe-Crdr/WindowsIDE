using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace WindowsIDE.Ui.Fonts
{
    /// <summary>
    /// 埋め込みフォントの読み込み結果。
    /// </summary>
    public sealed class FontLoadResult
    {
        private FontFamily halfFamily;
        private FontFamily fullFamily;
        private FontFamily boldFamily;
        private bool usedFallback;
        private string errorMessage;
        private string halfWidthFamilyName;
        private string fullWidthFamilyName;

        internal FontLoadResult(FontFamily halfFamily, FontFamily fullFamily, FontFamily boldFamily, bool usedFallback, string errorMessage, string halfWidthFamilyName, string fullWidthFamilyName)
        {
            this.halfFamily = halfFamily;
            this.fullFamily = fullFamily;
            this.boldFamily = boldFamily;
            this.usedFallback = usedFallback;
            this.errorMessage = errorMessage;
            this.halfWidthFamilyName = string.IsNullOrEmpty(halfWidthFamilyName) ? "Consolas" : halfWidthFamilyName;
            this.fullWidthFamilyName = string.IsNullOrEmpty(fullWidthFamilyName) ? "Yu Gothic" : fullWidthFamilyName;
        }

        /// <summary>OS フォントへ退避したなら true。</summary>
        public bool UsedFallback { get { return this.usedFallback; } }

        /// <summary>失敗時のステータス文言。成功時は null。</summary>
        public string ErrorMessage { get { return this.errorMessage; } }

        /// <summary>半角ファミリ名。name テーブルの値で、GDI+ の FontFamily.Name ではない。</summary>
        public string HalfWidthFamilyName
        {
            get { return this.halfWidthFamilyName; }
        }

        /// <summary>全角ファミリ名。name テーブルの値で、GDI+ の FontFamily.Name ではない。</summary>
        public string FullWidthFamilyName
        {
            get { return this.fullWidthFamilyName; }
        }

        /// <summary>
        /// 指定ピクセルサイズの半角 Regular フォントを返す。
        /// </summary>
        public Font CreateHalfWidth(float pixelSize)
        {
            return CreateFont(this.halfFamily, pixelSize, FontStyle.Regular);
        }

        /// <summary>
        /// 指定ピクセルサイズの全角 Regular フォントを返す。
        /// </summary>
        public Font CreateFullWidth(float pixelSize)
        {
            return CreateFont(this.fullFamily, pixelSize, FontStyle.Regular);
        }

        /// <summary>
        /// 同梱 Bold。P0 の TextView では使わない。
        /// </summary>
        public Font CreateHalfWidthBold(float pixelSize)
        {
            FontFamily fam = (this.boldFamily != null) ? this.boldFamily : this.halfFamily;
            return CreateFont(fam, pixelSize, FontStyle.Bold);
        }

        private static Font CreateFont(FontFamily family, float pixelSize, FontStyle style)
        {
            if (pixelSize < 6f)
            {
                pixelSize = 6f;
            }

            if (family != null)
            {
                try
                {
                    return new Font(family, pixelSize, style, GraphicsUnit.Pixel);
                }
                catch (ArgumentException)
                {
                    return new Font(family, pixelSize, FontStyle.Regular, GraphicsUnit.Pixel);
                }
            }

            return new Font("Consolas", pixelSize, FontStyle.Regular, GraphicsUnit.Pixel);
        }
    }

    /// <summary>
    /// EXE 埋め込みフォントをプロセス内登録する。
    /// </summary>
    public static class FontLoader
    {
        private const int RouteFail = 0;
        private const int RouteMemory = 1;
        private const int RouteFile = 2;
        private const int ProbeSize = 96;
        private const float ProbeFontPx = 48f;
        private const int ProbeMinDiff = 30;
        private const int ProbeMinInk = 20;

        private static List<PrivateFontCollection> collections;
        private static List<GCHandle> pins;
        private static List<string> tempFiles;
        private static FontLoadResult lastResult;

        /// <summary>
        /// 埋め込みリソースから読む。失敗時は Consolas / Yu Gothic / MS Gothic。
        /// </summary>
        /// <returns>読み込み結果。</returns>
        public static FontLoadResult Load()
        {
            byte[] regular = ReadResource("WindowsIDE.Fonts.CascadiaMonoRegular");
            byte[] bold = ReadResource("WindowsIDE.Fonts.CascadiaMonoBold");
            byte[] sourceHan = ReadResource("WindowsIDE.Fonts.SourceHanSansJpRegular");
            return LoadFromBytes(regular, bold, sourceHan);
        }

        /// <summary>
        /// テスト用。null や不正バイトで失敗経路（UsedFallback）を検証できる。
        /// </summary>
        /// <param name="cascadiaRegular">Cascadia Mono Regular の TTF。</param>
        /// <param name="cascadiaBold">Cascadia Mono Bold の TTF。</param>
        /// <param name="sourceHan">源ノ角 Regular の静的 TTF。</param>
        /// <returns>読み込み結果。</returns>
        public static FontLoadResult LoadFromBytes(byte[] cascadiaRegular, byte[] cascadiaBold, byte[] sourceHan)
        {
            return LoadFromBytes(cascadiaRegular, cascadiaBold, sourceHan, false);
        }

        /// <summary>
        /// テスト用。skipMemoryRoute でメモリ経路を飛ばし、一時ファイル経路を検証できる。
        /// </summary>
        /// <param name="cascadiaRegular">Cascadia Mono Regular の TTF。</param>
        /// <param name="cascadiaBold">Cascadia Mono Bold の TTF。</param>
        /// <param name="sourceHan">源ノ角 Regular の静的 TTF。</param>
        /// <param name="skipMemoryRoute">true なら AddMemoryFont を試さない（テスト用シーム）。</param>
        /// <returns>読み込み結果。</returns>
        public static FontLoadResult LoadFromBytes(byte[] cascadiaRegular, byte[] cascadiaBold, byte[] sourceHan, bool skipMemoryRoute)
        {
            Cleanup();
            EnsureState();
            SweepStaleTempFiles();
            string[] halfNames = new string[] { "Cascadia Mono" };
            string[] fullNames = new string[] { "源ノ角ゴシック JP", "Source Han Sans JP" };

            FontFamily half;
            FontFamily bold;
            FontFamily full;
            string halfName;
            string boldName;
            string fullName;
            int halfRoute = TryLoadFamily(cascadiaRegular, halfNames, ".ttf", skipMemoryRoute, false, out half, out halfName);
            TryLoadFamily(cascadiaBold, halfNames, ".ttf", skipMemoryRoute, false, out bold, out boldName);
            int fullRoute = TryLoadFamily(sourceHan, fullNames, ".ttf", skipMemoryRoute, true, out full, out fullName);

            bool fallback = false;
            if (halfRoute == RouteFail)
            {
                fallback = true;
                string accepted;
                half = TryOsFamily(new string[] { "Consolas" }, out accepted);
                if (half == null)
                {
                    half = FontFamily.GenericMonospace;
                    halfName = "GenericMonospace";
                }
                else
                {
                    halfName = accepted;
                }
            }

            if (fullRoute == RouteFail)
            {
                fallback = true;
                string accepted;
                full = TryOsFamily(new string[] { "Yu Gothic", "Yu Gothic UI", "MS Gothic" }, out accepted);
                if (full == null)
                {
                    full = FontFamily.GenericMonospace;
                    fullName = "GenericMonospace";
                }
                else
                {
                    fullName = accepted;
                }
            }

            string error = null;
            if (halfRoute == RouteFail && fullRoute == RouteFail)
            {
                error = "同梱フォントの読み込みに失敗したため、" + halfName + " / " + fullName + " に退避しています。";
            }
            else if (halfRoute == RouteFail)
            {
                error = "同梱フォントの読み込みに失敗したため、半角を " + halfName + " に退避しています。";
            }
            else if (fullRoute == RouteFail)
            {
                error = "同梱フォントの読み込みに失敗したため、全角を " + fullName + " に退避しています。";
            }

            lastResult = new FontLoadResult(half, full, bold, fallback, error, halfName, fullName);
            Log("font load: half=" + halfName + " (" + RouteLabel(halfRoute) + "), bold=" + boldName + ", full=" + fullName + " (" + RouteLabel(fullRoute) + ")");
            return lastResult;
        }

        /// <summary>直近の Load 結果。未実行なら null。</summary>
        public static FontLoadResult LastResult
        {
            get { return lastResult; }
        }

        /// <summary>
        /// 登録したフォントと一時ファイルを解放する。終了時に Program.Main の finally から呼ぶ。例外は外へ出さない。
        /// </summary>
        public static void Cleanup()
        {
            try
            {
                if (collections != null)
                {
                    for (int i = 0; i < collections.Count; i++)
                    {
                        try
                        {
                            if (collections[i] != null)
                            {
                                collections[i].Dispose();
                            }
                        }
                        catch (Exception)
                        {
                        }
                    }
                }

                if (pins != null)
                {
                    for (int i = 0; i < pins.Count; i++)
                    {
                        try
                        {
                            if (pins[i].IsAllocated)
                            {
                                pins[i].Free();
                            }
                        }
                        catch (Exception)
                        {
                        }
                    }
                }

                if (tempFiles != null)
                {
                    for (int i = 0; i < tempFiles.Count; i++)
                    {
                        try
                        {
                            File.Delete(tempFiles[i]);
                        }
                        catch (Exception)
                        {
                        }
                    }
                }

                try
                {
                    string dir = TempFontDir();
                    if (Directory.Exists(dir))
                    {
                        Directory.Delete(dir, false);
                    }
                }
                catch (Exception)
                {
                }
            }
            catch (Exception)
            {
            }
            finally
            {
                collections = null;
                pins = null;
                tempFiles = null;
                lastResult = null;
            }
        }

        private static void EnsureState()
        {
            if (collections == null)
            {
                collections = new List<PrivateFontCollection>();
            }

            if (pins == null)
            {
                pins = new List<GCHandle>();
            }

            if (tempFiles == null)
            {
                tempFiles = new List<string>();
            }
        }

        private static byte[] ReadResource(string name)
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            using (Stream stream = asm.GetManifestResourceStream(name))
            {
                if (stream == null)
                {
                    return null;
                }

                MemoryStream ms = new MemoryStream();
                byte[] buffer = new byte[4096];
                int n;
                while ((n = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, n);
                }

                return ms.ToArray();
            }
        }

        private static string TempFontDir()
        {
            return Path.Combine(Path.GetTempPath(), "WindowsIDE", "fonts");
        }

        /// <summary>
        /// 前回実行が残した一時フォントを消す。ロック中（他インスタンス稼働中）のファイルはスキップされる。
        /// </summary>
        private static void SweepStaleTempFiles()
        {
            string dir;
            try
            {
                dir = TempFontDir();
                if (!Directory.Exists(dir))
                {
                    return;
                }
            }
            catch (Exception)
            {
                return;
            }

            string[] patterns = new string[] { "*.ttf", "*.otf" };
            for (int p = 0; p < patterns.Length; p++)
            {
                string[] files;
                try
                {
                    files = Directory.GetFiles(dir, patterns[p]);
                }
                catch (Exception)
                {
                    continue;
                }

                for (int i = 0; i < files.Length; i++)
                {
                    try
                    {
                        File.Delete(files[i]);
                    }
                    catch (Exception)
                    {
                    }
                }
            }
        }

        /// <summary>
        /// 1 ファイルを専用 PrivateFontCollection で、メモリ → 一時ファイルの順に試す。
        /// 戻り値は採用経路（失敗は RouteFail）。
        /// </summary>
        private static int TryLoadFamily(byte[] data, string[] acceptableNames, string extension, bool skipMemoryRoute, bool requireCjkOutline, out FontFamily family, out string displayName)
        {
            family = null;
            displayName = null;
            if (data == null || data.Length < 16)
            {
                return RouteFail;
            }

            string parsed = ReadPreferredFamilyName(data);
            if (!skipMemoryRoute && TryAddMemoryFont(data, acceptableNames, parsed, requireCjkOutline, out family))
            {
                displayName = DisplayName(parsed, family, acceptableNames);
                return RouteMemory;
            }

            if (TryAddFontFile(data, acceptableNames, parsed, extension, requireCjkOutline, out family))
            {
                displayName = DisplayName(parsed, family, acceptableNames);
                return RouteFile;
            }

            return RouteFail;
        }

        private static bool TryAddMemoryFont(byte[] data, string[] acceptableNames, string parsedName, bool requireCjkOutline, out FontFamily family)
        {
            family = null;
            PrivateFontCollection fonts = new PrivateFontCollection();
            GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            bool keep = false;
            try
            {
                int before = fonts.Families.Length;
                fonts.AddMemoryFont(handle.AddrOfPinnedObject(), data.Length);
                if (!TryAcceptFamily(fonts, before, acceptableNames, parsedName, requireCjkOutline, out family))
                {
                    Log("AddMemoryFont: rejected parsed=" + parsedName);
                }
                else
                {
                    // 個別フォントを外す API は無い。不採用のコレクションは捨て、採用分のピンは Cleanup まで残す。
                    pins.Add(handle);
                    collections.Add(fonts);
                    keep = true;
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log("AddMemoryFont failed: " + ex.GetType().Name + ": " + ex.Message);
            }

            if (!keep)
            {
                try
                {
                    fonts.Dispose();
                }
                catch (Exception)
                {
                }

                if (handle.IsAllocated)
                {
                    handle.Free();
                }
            }

            return false;
        }

        /// <summary>
        /// 一時ファイルへ書き出して PrivateFontCollection.AddFontFile で読む。失敗時はその場でファイルを消す。
        /// </summary>
        private static bool TryAddFontFile(byte[] data, string[] acceptableNames, string parsedName, string extension, bool requireCjkOutline, out FontFamily family)
        {
            family = null;
            string path = null;
            PrivateFontCollection fonts = new PrivateFontCollection();
            bool keep = false;
            try
            {
                string dir = TempFontDir();
                Directory.CreateDirectory(dir);
                path = Path.Combine(dir, Guid.NewGuid().ToString("N") + extension);
                File.WriteAllBytes(path, data);
                int before = fonts.Families.Length;
                fonts.AddFontFile(path);
                if (!TryAcceptFamily(fonts, before, acceptableNames, parsedName, requireCjkOutline, out family))
                {
                    Log("AddFontFile: rejected (" + extension + ") parsed=" + parsedName);
                }
                else
                {
                    tempFiles.Add(path);
                    collections.Add(fonts);
                    keep = true;
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log("AddFontFile failed: " + ex.GetType().Name + ": " + ex.Message);
            }

            if (!keep)
            {
                try
                {
                    fonts.Dispose();
                }
                catch (Exception)
                {
                }

                if (path != null)
                {
                    try
                    {
                        File.Delete(path);
                    }
                    catch (Exception)
                    {
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// このコレクションへ今足したファミリだけを見る。名前は name テーブルを正にし、
        /// GDI+ が Cascadia Mono と返す全角顔は捨てる。全角は Yu Gothic UI と同じ「あ」も捨てる。
        /// </summary>
        private static bool TryAcceptFamily(PrivateFontCollection fonts, int beforeCount, string[] acceptableNames, string parsedName, bool requireCjkOutline, out FontFamily family)
        {
            family = null;
            FontFamily[] families;
            try
            {
                families = fonts.Families;
            }
            catch (Exception ex)
            {
                Log("Families failed: " + ex.GetType().Name + ": " + ex.Message);
                return false;
            }

            if (families == null || families.Length <= beforeCount)
            {
                return false;
            }

            family = families[families.Length - 1];
            string gdiName = null;
            try
            {
                gdiName = family.Name;
            }
            catch (ArgumentException)
            {
            }

            if (string.Equals(gdiName, "Cascadia Mono", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(parsedName, "Cascadia Mono", StringComparison.OrdinalIgnoreCase))
            {
                Log("rejected Cascadia alias gdi=" + gdiName + " parsed=" + parsedName);
                family = null;
                return false;
            }

            if (!NameIn(gdiName, acceptableNames) && !NameIn(parsedName, acceptableNames))
            {
                Log("rejected name gdi=" + gdiName + " parsed=" + parsedName);
                family = null;
                return false;
            }

            if (requireCjkOutline && !HasDistinctCjkOutline(family))
            {
                Log("rejected UI-font outline gdi=" + gdiName + " parsed=" + parsedName);
                family = null;
                return false;
            }

            return true;
        }

        private static bool NameIn(string name, string[] acceptableNames)
        {
            if (string.IsNullOrEmpty(name) || acceptableNames == null)
            {
                return false;
            }

            for (int i = 0; i < acceptableNames.Length; i++)
            {
                if (string.Equals(name, acceptableNames[i], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string DisplayName(string parsed, FontFamily family, string[] acceptableNames)
        {
            if (!string.IsNullOrEmpty(parsed))
            {
                return parsed;
            }

            try
            {
                if (family != null && !string.IsNullOrEmpty(family.Name))
                {
                    return family.Name;
                }
            }
            catch (ArgumentException)
            {
            }

            if (acceptableNames != null && acceptableNames.Length > 0)
            {
                return acceptableNames[0];
            }

            return null;
        }

        /// <summary>
        /// GDI+ DrawString が CFF を描けないとき全角は Yu Gothic UI になる。
        /// その顔は同梱成功にしない。Yu Gothic UI が無い環境では検査しない。
        /// </summary>
        private static bool HasDistinctCjkOutline(FontFamily family)
        {
            Font ui = null;
            try
            {
                ui = new Font("Yu Gothic UI", ProbeFontPx, FontStyle.Regular, GraphicsUnit.Pixel);
            }
            catch (ArgumentException)
            {
                return true;
            }

            Font mine = null;
            try
            {
                mine = new Font(family, ProbeFontPx, FontStyle.Regular, GraphicsUnit.Pixel);
                int diff;
                int ink;
                if (!CompareGlyph(mine, ui, "あ", out diff, out ink))
                {
                    return false;
                }

                Log("cjk outline diff=" + diff.ToString() + " ink=" + ink.ToString());
                return ink >= ProbeMinInk && diff >= ProbeMinDiff;
            }
            catch (Exception ex)
            {
                Log("cjk outline probe failed: " + ex.GetType().Name + ": " + ex.Message);
                return false;
            }
            finally
            {
                if (mine != null)
                {
                    mine.Dispose();
                }

                ui.Dispose();
            }
        }

        private static bool CompareGlyph(Font mine, Font other, string text, out int diff, out int ink)
        {
            diff = 0;
            ink = 0;
            using (Bitmap left = DrawGlyph(mine, text))
            using (Bitmap right = DrawGlyph(other, text))
            {
                byte[] a = CopyPixels(left);
                byte[] b = CopyPixels(right);
                if (a == null || b == null)
                {
                    return false;
                }

                int stride = ProbeSize * 4;
                for (int y = 0; y < ProbeSize; y++)
                {
                    int row = y * stride;
                    for (int x = 0; x < ProbeSize; x++)
                    {
                        int i = row + (x * 4);
                        if (a[i] != 0 || a[i + 1] != 0 || a[i + 2] != 0)
                        {
                            ink++;
                        }

                        if (a[i] != b[i] || a[i + 1] != b[i + 1] || a[i + 2] != b[i + 2])
                        {
                            diff++;
                        }
                    }
                }
            }

            return true;
        }

        private static Bitmap DrawGlyph(Font font, string text)
        {
            Bitmap bmp = new Bitmap(ProbeSize, ProbeSize, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Black);
                g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
                g.DrawString(text, font, Brushes.White, 4f, 4f);
            }

            return bmp;
        }

        private static byte[] CopyPixels(Bitmap bmp)
        {
            BitmapData bits = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                int stride = bits.Stride;
                if (stride < 0)
                {
                    stride = -stride;
                }

                int bytes = stride * bmp.Height;
                byte[] raw = new byte[bytes];
                Marshal.Copy(bits.Scan0, raw, 0, bytes);
                if (stride == ProbeSize * 4)
                {
                    return raw;
                }

                byte[] packed = new byte[ProbeSize * ProbeSize * 4];
                for (int y = 0; y < ProbeSize; y++)
                {
                    Buffer.BlockCopy(raw, y * stride, packed, y * ProbeSize * 4, ProbeSize * 4);
                }

                return packed;
            }
            finally
            {
                bmp.UnlockBits(bits);
            }
        }

        /// <summary>
        /// name テーブルのファミリー名。日本語 (0x411) を優先し、無ければ英語 (0x409)。
        /// </summary>
        private static string ReadPreferredFamilyName(byte[] data)
        {
            try
            {
                int nameOffset = FindTable(data, "name");
                if (nameOffset < 0 || nameOffset + 6 > data.Length)
                {
                    return null;
                }

                int count = ReadU16(data, nameOffset + 2);
                int stringOffset = ReadU16(data, nameOffset + 4);
                if (count < 0 || count > 256)
                {
                    return null;
                }

                string english = null;
                string any = null;
                for (int i = 0; i < count; i++)
                {
                    int rec = nameOffset + 6 + (i * 12);
                    if (rec + 12 > data.Length)
                    {
                        break;
                    }

                    int platform = ReadU16(data, rec);
                    int encoding = ReadU16(data, rec + 2);
                    int language = ReadU16(data, rec + 4);
                    int nameId = ReadU16(data, rec + 6);
                    int length = ReadU16(data, rec + 8);
                    int offset = ReadU16(data, rec + 10);
                    if (platform != 3 || encoding != 1 || nameId != 1 || length < 2 || (length % 2) != 0)
                    {
                        continue;
                    }

                    int start = nameOffset + stringOffset + offset;
                    if (start < 0 || start + length > data.Length)
                    {
                        continue;
                    }

                    string value = Encoding.BigEndianUnicode.GetString(data, start, length);
                    if (string.IsNullOrEmpty(value))
                    {
                        continue;
                    }

                    if (language == 0x411)
                    {
                        return value;
                    }

                    if (language == 0x409 && english == null)
                    {
                        english = value;
                    }

                    if (any == null)
                    {
                        any = value;
                    }
                }

                if (english != null)
                {
                    return english;
                }

                return any;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static int FindTable(byte[] data, string tag)
        {
            if (data == null || data.Length < 12 || tag == null || tag.Length != 4)
            {
                return -1;
            }

            int count = ReadU16(data, 4);
            if (count < 1 || count > 80)
            {
                return -1;
            }

            if (12 + (count * 16) > data.Length)
            {
                return -1;
            }

            for (int i = 0; i < count; i++)
            {
                int rec = 12 + (i * 16);
                if (data[rec] == (byte)tag[0] && data[rec + 1] == (byte)tag[1] && data[rec + 2] == (byte)tag[2] && data[rec + 3] == (byte)tag[3])
                {
                    int offset = ReadU32(data, rec + 8);
                    if (offset < 0 || offset >= data.Length)
                    {
                        return -1;
                    }

                    return offset;
                }
            }

            return -1;
        }

        private static int ReadU16(byte[] data, int offset)
        {
            return (data[offset] << 8) | data[offset + 1];
        }

        private static int ReadU32(byte[] data, int offset)
        {
            return (int)(((uint)data[offset] << 24) | ((uint)data[offset + 1] << 16) | ((uint)data[offset + 2] << 8) | data[offset + 3]);
        }

        /// <summary>
        /// OS のインストール済みファミリを候補順に試す。全滅なら null（GenericMonospace は成功扱いしない）。
        /// accepted は要求した名前。FontFamily.Name は使わない。
        /// </summary>
        private static FontFamily TryOsFamily(string[] names, out string accepted)
        {
            accepted = null;
            for (int i = 0; i < names.Length; i++)
            {
                try
                {
                    FontFamily fam = new FontFamily(names[i]);
                    accepted = names[i];
                    return fam;
                }
                catch (ArgumentException)
                {
                }
            }

            return null;
        }

        private static string RouteLabel(int route)
        {
            if (route == RouteMemory)
            {
                return "memory";
            }

            if (route == RouteFile)
            {
                return "file";
            }

            return "os-fallback";
        }

        /// <summary>
        /// %TEMP%\WindowsIDE.log へ 1 行追記する。ログ失敗でフォント読込を壊さないよう例外は握りつぶす。
        /// </summary>
        private static void Log(string message)
        {
            try
            {
                string path = Path.Combine(Path.GetTempPath(), "WindowsIDE.log");
                string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + message + Environment.NewLine;
                File.AppendAllText(path, line);
            }
            catch (Exception)
            {
            }
        }
    }
}
