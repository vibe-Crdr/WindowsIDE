using System.IO;

namespace WindowsIDE.Build
{
    /// <summary>
    /// ユーザー手動 csc の固定パスと Framework 6 DLL。製品 winexe / SMA とは別。
    /// </summary>
    public static class FrameworkCsc
    {
        /// <summary>指定 Framework64 の csc.exe。これ以外は使わない。</summary>
        public const string CompilerPath = @"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe";

        /// <summary>コンパイラと同じフォルダ。</summary>
        public const string FrameworkDirectory = @"C:\Windows\Microsoft.NET\Framework64\v4.0.30319";

        private static readonly string[] ReferenceFileNames = new string[]
        {
            "mscorlib.dll",
            "System.dll",
            "System.Core.dll",
            "System.Drawing.dll",
            "System.Windows.Forms.dll",
            "System.Xml.dll"
        };

        /// <summary>
        /// 指定 csc.exe がディスク上にあれば true。
        /// </summary>
        public static bool CompilerExists()
        {
            return File.Exists(CompilerPath);
        }

        /// <summary>
        /// ユーザー csc の /r フルパス（6 DLL）。SMA は含まない。
        /// </summary>
        /// <returns>Framework64 上の DLL パス。</returns>
        public static string[] GetReferencePaths()
        {
            string[] paths = new string[ReferenceFileNames.Length];
            for (int i = 0; i < ReferenceFileNames.Length; i++)
            {
                paths[i] = Path.Combine(FrameworkDirectory, ReferenceFileNames[i]);
            }

            return paths;
        }
    }
}
