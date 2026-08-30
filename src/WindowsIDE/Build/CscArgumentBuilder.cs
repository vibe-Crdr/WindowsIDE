using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace WindowsIDE.Build
{
    /// <summary>
    /// 手動ビルド用 rsp を書く。/noconfig は rsp に入れない（コマンドライン専用）。
    /// </summary>
    public static class CscArgumentBuilder
    {
        /// <summary>
        /// ワークスペース根または単一ファイルのフルパスから TEMP 上の out.exe パスを返す。ディレクトリは作る。
        /// </summary>
        /// <param name="keyPath">OrdinalIgnoreCase でハッシュするフルパス。</param>
        /// <returns>out.exe のフルパス。</returns>
        public static string GetOutputExePath(string keyPath)
        {
            string key = ShortHash(keyPath);
            string dir = Path.Combine(Path.GetTempPath(), "WindowsIDE", "build", "manual", key);
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "out.exe");
        }

        /// <summary>
        /// ワークスペース根または単一ファイルのフルパスから TEMP 上の live out.dll パスを返す。ディレクトリは作る。
        /// </summary>
        /// <param name="keyPath">OrdinalIgnoreCase でハッシュするフルパス。</param>
        /// <returns>out.dll のフルパス。</returns>
        public static string GetLiveOutputDllPath(string keyPath)
        {
            string key = ShortHash(keyPath);
            string dir = Path.Combine(Path.GetTempPath(), "WindowsIDE", "build", "live", key);
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "out.dll");
        }

        /// <summary>
        /// UTF-8 BOM の rsp を書く。中身に /noconfig は含めない。
        /// </summary>
        /// <param name="rspPath">書き先。</param>
        /// <param name="outputExe">/out のフルパス。</param>
        /// <param name="sources">ソースのフルパス。</param>
        public static void WriteResponseFile(string rspPath, string outputExe, string[] sources)
        {
            if (string.IsNullOrEmpty(rspPath))
            {
                throw new ArgumentException("rspPath");
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("/nostdlib");
            sb.AppendLine("/platform:x64");
            sb.AppendLine("/target:exe");
            sb.AppendLine("/debug+");
            sb.AppendLine("/utf8output");
            string[] refs = FrameworkCsc.GetReferencePaths();
            for (int i = 0; i < refs.Length; i++)
            {
                sb.Append("/r:");
                sb.AppendLine(Quote(refs[i]));
            }

            sb.Append("/out:");
            sb.AppendLine(Quote(outputExe));
            if (sources != null)
            {
                for (int i = 0; i < sources.Length; i++)
                {
                    if (string.IsNullOrEmpty(sources[i]))
                    {
                        continue;
                    }

                    sb.AppendLine(Quote(sources[i]));
                }
            }

            string dir = Path.GetDirectoryName(rspPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            Encoding utf8Bom = new UTF8Encoding(true);
            File.WriteAllText(rspPath, sb.ToString(), utf8Bom);
        }

        /// <summary>
        /// 常時診断用 UTF-8 BOM の rsp を書く。/target:library。debug スイッチは書かない。/noconfig は含めない。
        /// </summary>
        /// <param name="rspPath">書き先。</param>
        /// <param name="outputDll">/out のフルパス。</param>
        /// <param name="sources">ソースのフルパス。</param>
        public static void WriteLiveResponseFile(string rspPath, string outputDll, string[] sources)
        {
            if (string.IsNullOrEmpty(rspPath))
            {
                throw new ArgumentException("rspPath");
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("/nostdlib");
            sb.AppendLine("/platform:x64");
            sb.AppendLine("/target:library");
            sb.AppendLine("/utf8output");
            string[] refs = FrameworkCsc.GetReferencePaths();
            for (int i = 0; i < refs.Length; i++)
            {
                sb.Append("/r:");
                sb.AppendLine(Quote(refs[i]));
            }

            sb.Append("/out:");
            sb.AppendLine(Quote(outputDll));
            if (sources != null)
            {
                for (int i = 0; i < sources.Length; i++)
                {
                    if (string.IsNullOrEmpty(sources[i]))
                    {
                        continue;
                    }

                    sb.AppendLine(Quote(sources[i]));
                }
            }

            string dir = Path.GetDirectoryName(rspPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            Encoding utf8Bom = new UTF8Encoding(true);
            File.WriteAllText(rspPath, sb.ToString(), utf8Bom);
        }

        /// <summary>
        /// パスを OrdinalIgnoreCase 相当（大文字化）して SHA1 の先頭 16 桁にする。
        /// </summary>
        /// <param name="keyPath">ワークスペース根またはファイル。</param>
        /// <returns>16 桁 hex。</returns>
        public static string ShortHash(string keyPath)
        {
            string text = (keyPath == null) ? "" : keyPath.ToUpperInvariant();
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            using (SHA1 sha = SHA1.Create())
            {
                byte[] hash = sha.ComputeHash(bytes);
                return ToHex(hash, 8);
            }
        }

        private static string Quote(string path)
        {
            if (path == null)
            {
                path = "";
            }

            return "\"" + path.Replace("\"", "\"\"") + "\"";
        }

        private static string ToHex(byte[] data, int take)
        {
            if (data == null || take < 1)
            {
                return "";
            }

            if (take > data.Length)
            {
                take = data.Length;
            }

            char[] hex = new char[take * 2];
            for (int i = 0; i < take; i++)
            {
                int b = data[i];
                hex[i * 2] = Nibble(b >> 4);
                hex[i * 2 + 1] = Nibble(b & 0xF);
            }

            return new string(hex);
        }

        private static char Nibble(int n)
        {
            n = n & 0xF;
            if (n < 10)
            {
                return (char)('0' + n);
            }

            return (char)('a' + (n - 10));
        }
    }
}
