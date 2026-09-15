using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

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
        private FontFamily italicFamily;
        private FontFamily boldItalicFamily;
        private FontFamily fullBoldFamily;
        private bool usedFallback;
        private string errorMessage;

        internal FontLoadResult(
            FontFamily halfFamily,
            FontFamily fullFamily,
            FontFamily boldFamily,
            FontFamily italicFamily,
            FontFamily boldItalicFamily,
            FontFamily fullBoldFamily,
            bool usedFallback,
            string errorMessage)
        {
            this.halfFamily = halfFamily;
            this.fullFamily = fullFamily;
            this.boldFamily = boldFamily;
            this.italicFamily = italicFamily;
            this.boldItalicFamily = boldItalicFamily;
            this.fullBoldFamily = fullBoldFamily;
            this.usedFallback = usedFallback;
            this.errorMessage = errorMessage;
        }

        /// <summary>OS フォントへ退避したなら true。</summary>
        public bool UsedFallback { get { return this.usedFallback; } }

        /// <summary>失敗時のステータス文言。成功時は null。</summary>
        public string ErrorMessage { get { return this.errorMessage; } }

        /// <summary>半角ファミリ名。</summary>
        public string HalfWidthFamilyName
        {
            get { return (this.halfFamily == null) ? "Consolas" : this.halfFamily.Name; }
        }

        /// <summary>全角ファミリ名。</summary>
        public string FullWidthFamilyName
        {
            get { return (this.fullFamily == null) ? "Yu Gothic" : this.fullFamily.Name; }
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
        /// 同梱 Bold。TextView では使わない。Markdown プレビューが使う。
        /// </summary>
        /// <param name="pixelSize">ピクセルサイズ。</param>
        /// <returns>半角 Bold フォント。</returns>
        public Font CreateHalfWidthBold(float pixelSize)
        {
            FontFamily fam = (this.boldFamily != null) ? this.boldFamily : this.halfFamily;
            return CreateFont(fam, pixelSize, FontStyle.Bold);
        }

        /// <summary>
        /// 同梱 Italic。Markdown プレビューが使う。
        /// </summary>
        /// <param name="pixelSize">ピクセルサイズ。</param>
        /// <returns>半角 Italic フォント。</returns>
        public Font CreateHalfWidthItalic(float pixelSize)
        {
            FontFamily fam = (this.italicFamily != null) ? this.italicFamily : this.halfFamily;
            return CreateFont(fam, pixelSize, FontStyle.Italic);
        }

        /// <summary>
        /// 同梱 BoldItalic。Markdown プレビューが使う。
        /// </summary>
        /// <param name="pixelSize">ピクセルサイズ。</param>
        /// <returns>半角 BoldItalic フォント。</returns>
        public Font CreateHalfWidthBoldItalic(float pixelSize)
        {
            FontFamily fam = this.boldItalicFamily;
            if (fam == null)
            {
                fam = (this.boldFamily != null) ? this.boldFamily : this.halfFamily;
            }

            return CreateFont(fam, pixelSize, FontStyle.Bold | FontStyle.Italic);
        }

        /// <summary>
        /// 同梱源ノ角 Bold。Markdown プレビューが使う。
        /// </summary>
        /// <param name="pixelSize">ピクセルサイズ。</param>
        /// <returns>全角 Bold フォント。</returns>
        public Font CreateFullWidthBold(float pixelSize)
        {
            FontFamily fam = (this.fullBoldFamily != null) ? this.fullBoldFamily : this.fullFamily;
            return CreateFont(fam, pixelSize, FontStyle.Bold);
        }

        /// <summary>
        /// 全角 Italic。源ノ角 Italic ファイルは無く、FontStyle 合成（失敗時 Regular）。
        /// </summary>
        /// <param name="pixelSize">ピクセルサイズ。</param>
        /// <returns>全角 Italic または Regular。</returns>
        public Font CreateFullWidthItalic(float pixelSize)
        {
            return CreateFont(this.fullFamily, pixelSize, FontStyle.Italic);
        }

        /// <summary>
        /// 全角 BoldItalic。Bold 実ファイルに Italic 合成（失敗時 Bold / Regular）。
        /// </summary>
        /// <param name="pixelSize">ピクセルサイズ。</param>
        /// <returns>全角 BoldItalic または退避フォント。</returns>
        public Font CreateFullWidthBoldItalic(float pixelSize)
        {
            FontFamily fam = (this.fullBoldFamily != null) ? this.fullBoldFamily : this.fullFamily;
            return CreateFont(fam, pixelSize, FontStyle.Bold | FontStyle.Italic);
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
                }

                if ((style & FontStyle.Bold) != 0 && (style & FontStyle.Italic) != 0)
                {
                    try
                    {
                        return new Font(family, pixelSize, FontStyle.Bold, GraphicsUnit.Pixel);
                    }
                    catch (ArgumentException)
                    {
                    }
                }

                try
                {
                    return new Font(family, pixelSize, FontStyle.Regular, GraphicsUnit.Pixel);
                }
                catch (ArgumentException)
                {
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

        private static PrivateFontCollection privateFonts;
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
            byte[] italic = ReadResource("WindowsIDE.Fonts.CascadiaMonoItalic");
            byte[] boldItalic = ReadResource("WindowsIDE.Fonts.CascadiaMonoBoldItalic");
            byte[] sourceHan = ReadResource("WindowsIDE.Fonts.SourceHanSansJpRegular");
            byte[] sourceHanBold = ReadResource("WindowsIDE.Fonts.SourceHanSansJpBold");
            return LoadFromBytes(regular, bold, italic, boldItalic, sourceHan, sourceHanBold);
        }

        /// <summary>
        /// テスト用。3 引数互換。null や不正バイトで失敗経路（UsedFallback）を検証できる。
        /// </summary>
        /// <param name="cascadiaRegular">Cascadia Mono Regular の TTF。</param>
        /// <param name="cascadiaBold">Cascadia Mono Bold の TTF。</param>
        /// <param name="sourceHan">源ノ角 Regular の OTF。</param>
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
        /// <param name="sourceHan">源ノ角 Regular の OTF。</param>
        /// <param name="skipMemoryRoute">true なら AddMemoryFont を試さない（テスト用シーム）。</param>
        /// <returns>読み込み結果。</returns>
        public static FontLoadResult LoadFromBytes(byte[] cascadiaRegular, byte[] cascadiaBold, byte[] sourceHan, bool skipMemoryRoute)
        {
            return LoadFromBytes(cascadiaRegular, cascadiaBold, null, null, sourceHan, null, skipMemoryRoute);
        }

        /// <summary>
        /// 6 ファイル経路。Italic / BoldItalic / 源ノ角 Bold を含めて読む。
        /// </summary>
        /// <param name="cascadiaRegular">Cascadia Mono Regular。</param>
        /// <param name="cascadiaBold">Cascadia Mono Bold。</param>
        /// <param name="cascadiaItalic">Cascadia Mono Italic。null なら合成。</param>
        /// <param name="cascadiaBoldItalic">Cascadia Mono BoldItalic。null なら合成。</param>
        /// <param name="sourceHan">源ノ角 Regular。</param>
        /// <param name="sourceHanBold">源ノ角 Bold。null なら合成。</param>
        /// <returns>読み込み結果。</returns>
        public static FontLoadResult LoadFromBytes(
            byte[] cascadiaRegular,
            byte[] cascadiaBold,
            byte[] cascadiaItalic,
            byte[] cascadiaBoldItalic,
            byte[] sourceHan,
            byte[] sourceHanBold)
        {
            return LoadFromBytes(cascadiaRegular, cascadiaBold, cascadiaItalic, cascadiaBoldItalic, sourceHan, sourceHanBold, false);
        }

        /// <summary>
        /// 6 ファイル経路。skipMemoryRoute で一時ファイル経路を検証できる。
        /// </summary>
        /// <param name="cascadiaRegular">Cascadia Mono Regular。</param>
        /// <param name="cascadiaBold">Cascadia Mono Bold。</param>
        /// <param name="cascadiaItalic">Cascadia Mono Italic。</param>
        /// <param name="cascadiaBoldItalic">Cascadia Mono BoldItalic。</param>
        /// <param name="sourceHan">源ノ角 Regular。</param>
        /// <param name="sourceHanBold">源ノ角 Bold。</param>
        /// <param name="skipMemoryRoute">true なら AddMemoryFont を試さない。</param>
        /// <returns>読み込み結果。</returns>
        public static FontLoadResult LoadFromBytes(
            byte[] cascadiaRegular,
            byte[] cascadiaBold,
            byte[] cascadiaItalic,
            byte[] cascadiaBoldItalic,
            byte[] sourceHan,
            byte[] sourceHanBold,
            bool skipMemoryRoute)
        {
            EnsureState();
            SweepStaleTempFiles();

            FontFamily half;
            FontFamily bold;
            FontFamily italic;
            FontFamily boldItalic;
            FontFamily full;
            FontFamily fullBold;
            int halfRoute = TryLoadFamily(cascadiaRegular, ".ttf", skipMemoryRoute, out half);
            TryLoadFamily(cascadiaBold, ".ttf", skipMemoryRoute, out bold);
            TryLoadFamily(cascadiaItalic, ".ttf", skipMemoryRoute, out italic);
            TryLoadFamily(cascadiaBoldItalic, ".ttf", skipMemoryRoute, out boldItalic);
            int fullRoute = TryLoadFamily(sourceHan, ".otf", skipMemoryRoute, out full);
            TryLoadFamily(sourceHanBold, ".otf", skipMemoryRoute, out fullBold);

            bool fallback = false;
            if (halfRoute == RouteFail)
            {
                fallback = true;
                half = TryOsFamily(new string[] { "Consolas" });
                if (half == null)
                {
                    half = FontFamily.GenericMonospace;
                }
            }

            if (fullRoute == RouteFail)
            {
                fallback = true;
                full = TryOsFamily(new string[] { "Yu Gothic", "Yu Gothic UI", "MS Gothic" });
                if (full == null)
                {
                    full = FontFamily.GenericMonospace;
                }
            }

            string error = null;
            if (halfRoute == RouteFail && fullRoute == RouteFail)
            {
                error = "同梱フォントの読み込みに失敗したため、" + half.Name + " / " + full.Name + " に退避しています。";
            }
            else if (halfRoute == RouteFail)
            {
                error = "同梱フォントの読み込みに失敗したため、半角を " + half.Name + " に退避しています。";
            }
            else if (fullRoute == RouteFail)
            {
                error = "同梱フォントの読み込みに失敗したため、全角を " + full.Name + " に退避しています。";
            }

            lastResult = new FontLoadResult(half, full, bold, italic, boldItalic, fullBold, fallback, error);
            Log("font load: half=" + half.Name + " (" + RouteLabel(halfRoute) + "), full=" + full.Name + " (" + RouteLabel(fullRoute) + ")");
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
                if (privateFonts != null)
                {
                    try
                    {
                        privateFonts.Dispose();
                    }
                    catch (Exception)
                    {
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
                privateFonts = null;
                pins = null;
                tempFiles = null;
                lastResult = null;
            }
        }

        private static void EnsureState()
        {
            if (privateFonts == null)
            {
                privateFonts = new PrivateFontCollection();
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
        /// 1 ファミリを メモリ → 一時ファイル の順で試す。戻り値は採用経路（失敗は RouteFail）。
        /// FontFamily は追加直後の参照を保持する（名前検索で Regular と取り違えない）。
        /// </summary>
        private static int TryLoadFamily(byte[] data, string extension, bool skipMemoryRoute, out FontFamily family)
        {
            family = null;
            if (data == null || data.Length < 16)
            {
                return RouteFail;
            }

            if (!skipMemoryRoute && TryAddMemoryFont(data, out family))
            {
                return RouteMemory;
            }

            if (TryAddFontFile(data, extension, out family))
            {
                return RouteFile;
            }

            return RouteFail;
        }

        private static bool TryAddMemoryFont(byte[] data, out FontFamily family)
        {
            family = null;
            GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            bool keep = false;
            try
            {
                int before = privateFonts.Families.Length;
                privateFonts.AddMemoryFont(handle.AddrOfPinnedObject(), data.Length);
                family = CaptureAddedFamily(before);
                // AddMemoryFont が例外なく戻った場合、名不一致でもコレクションはバッファを参照し続ける。
                // 個別フォントをコレクションから外す API は無いため、GC 移動/回収後の Families 列挙が
                // 無効メモリに触れないよう、ピンはプロセス寿命まで保持する（Cleanup まで解放しない）。
                pins.Add(handle);
                keep = true;
                if (family != null)
                {
                    return true;
                }

                Log("AddMemoryFont: family not found");
            }
            catch (Exception ex)
            {
                Log("AddMemoryFont failed: " + ex.GetType().Name + ": " + ex.Message);
            }

            if (!keep && handle.IsAllocated)
            {
                handle.Free();
            }

            return false;
        }

        /// <summary>
        /// 一時ファイルへ書き出して PrivateFontCollection.AddFontFile で読む。失敗時はその場でファイルを消す。
        /// </summary>
        private static bool TryAddFontFile(byte[] data, string extension, out FontFamily family)
        {
            family = null;
            string path = null;
            try
            {
                string dir = TempFontDir();
                Directory.CreateDirectory(dir);
                path = Path.Combine(dir, Guid.NewGuid().ToString("N") + extension);
                File.WriteAllBytes(path, data);
                int before = privateFonts.Families.Length;
                privateFonts.AddFontFile(path);
                family = CaptureAddedFamily(before);
                if (family != null)
                {
                    tempFiles.Add(path);
                    return true;
                }

                Log("AddFontFile: family not found (" + extension + ")");
            }
            catch (Exception ex)
            {
                Log("AddFontFile failed: " + ex.GetType().Name + ": " + ex.Message);
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

            return false;
        }

        /// <summary>
        /// 追加直後の FontFamily 参照を返す。件数増なら末尾、同一名マージなら既存末尾。
        /// </summary>
        private static FontFamily CaptureAddedFamily(int countBefore)
        {
            FontFamily[] families = privateFonts.Families;
            if (families == null || families.Length == 0)
            {
                return null;
            }

            if (families.Length > countBefore)
            {
                return families[families.Length - 1];
            }

            return families[families.Length - 1];
        }

        /// <summary>
        /// OS のインストール済みファミリを候補順に試す。全滅なら null（GenericMonospace は成功扱いしない）。
        /// </summary>
        private static FontFamily TryOsFamily(string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                try
                {
                    FontFamily fam = new FontFamily(names[i]);
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
