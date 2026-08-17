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
        private bool usedFallback;
        private string errorMessage;

        internal FontLoadResult(FontFamily halfFamily, FontFamily fullFamily, FontFamily boldFamily, bool usedFallback, string errorMessage)
        {
            this.halfFamily = halfFamily;
            this.fullFamily = fullFamily;
            this.boldFamily = boldFamily;
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
        private static PrivateFontCollection privateFonts;
        private static List<GCHandle> pins;
        private static List<IntPtr> memHandles;
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
        /// <param name="sourceHan">源ノ角 Regular の OTF。</param>
        /// <returns>読み込み結果。</returns>
        public static FontLoadResult LoadFromBytes(byte[] cascadiaRegular, byte[] cascadiaBold, byte[] sourceHan)
        {
            EnsureState();
            string[] halfNames = new string[] { "Cascadia Mono" };
            string[] fullNames = new string[] { "Source Han Sans JP", "源ノ角ゴシック JP" };

            FontFamily half;
            FontFamily bold;
            FontFamily full;
            bool halfOk = TryLoadFamily(cascadiaRegular, halfNames, ".ttf", out half);
            TryLoadFamily(cascadiaBold, halfNames, ".ttf", out bold);
            bool fullOk = TryLoadFamily(sourceHan, fullNames, ".otf", out full);

            bool fallback = false;
            string error = null;
            if (!halfOk)
            {
                fallback = true;
                half = TryOsFamily(new string[] { "Consolas", "Consolas" });
            }

            if (!fullOk)
            {
                fallback = true;
                full = TryOsFamily(new string[] { "Yu Gothic", "Yu Gothic UI", "MS Gothic" });
            }

            if (fallback)
            {
                error = "同梱フォントの読み込みに失敗したため、Consolas / Yu Gothic に退避しています。";
            }

            lastResult = new FontLoadResult(half, full, bold, fallback, error);
            return lastResult;
        }

        /// <summary>直近の Load 結果。未実行なら null。</summary>
        public static FontLoadResult LastResult
        {
            get { return lastResult; }
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

            if (memHandles == null)
            {
                memHandles = new List<IntPtr>();
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

        private static bool TryLoadFamily(byte[] data, string[] familyNames, string extension, out FontFamily family)
        {
            family = null;
            if (data == null || data.Length < 16)
            {
                return false;
            }

            if (TryAddMemoryFont(data, familyNames, out family))
            {
                return true;
            }

            if (TryAddFontMemResource(data, familyNames, out family))
            {
                return true;
            }

            if (TryAddTempFile(data, familyNames, extension, out family))
            {
                return true;
            }

            return false;
        }

        private static bool TryAddMemoryFont(byte[] data, string[] familyNames, out FontFamily family)
        {
            family = null;
            GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            try
            {
                privateFonts.AddMemoryFont(handle.AddrOfPinnedObject(), data.Length);
                family = FindPrivateFamily(familyNames);
                if (family != null)
                {
                    pins.Add(handle);
                    return true;
                }
            }
            catch (Exception)
            {
            }

            if (handle.IsAllocated)
            {
                handle.Free();
            }

            return false;
        }

        private static bool TryAddFontMemResource(byte[] data, string[] familyNames, out FontFamily family)
        {
            family = null;
            GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            try
            {
                uint count = 1;
                IntPtr added = NativeFonts.AddFontMemResourceEx(handle.AddrOfPinnedObject(), (uint)data.Length, IntPtr.Zero, ref count);
                if (added == IntPtr.Zero)
                {
                    handle.Free();
                    return false;
                }

                pins.Add(handle);
                memHandles.Add(added);
                family = TryOsFamily(familyNames);
                return family != null;
            }
            catch (Exception)
            {
                if (handle.IsAllocated)
                {
                    handle.Free();
                }

                return false;
            }
        }

        private static bool TryAddTempFile(byte[] data, string[] familyNames, string extension, out FontFamily family)
        {
            family = null;
            string dir = Path.Combine(Path.GetTempPath(), "WindowsIDE-fonts");
            try
            {
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, Guid.NewGuid().ToString("N") + extension);
                File.WriteAllBytes(path, data);
                int added = NativeFonts.AddFontResourceEx(path, (uint)NativeFonts.FR_PRIVATE, IntPtr.Zero);
                if (added <= 0)
                {
                    try
                    {
                        File.Delete(path);
                    }
                    catch (IOException)
                    {
                    }

                    return false;
                }

                tempFiles.Add(path);
                family = TryOsFamily(familyNames);
                return family != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static FontFamily FindPrivateFamily(string[] familyNames)
        {
            FontFamily[] families = privateFonts.Families;
            for (int i = 0; i < families.Length; i++)
            {
                for (int n = 0; n < familyNames.Length; n++)
                {
                    if (string.Equals(families[i].Name, familyNames[n], StringComparison.OrdinalIgnoreCase))
                    {
                        return families[i];
                    }
                }
            }

            return null;
        }

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

            try
            {
                return FontFamily.GenericMonospace;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }
    }
}
