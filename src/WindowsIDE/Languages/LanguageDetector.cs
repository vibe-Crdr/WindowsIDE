using System;
using System.IO;

namespace WindowsIDE.Languages
{
    /// <summary>
    /// パスの拡張子だけで言語を決める。中身は見ない。
    /// </summary>
    public static class LanguageDetector
    {
        /// <summary>
        /// パスから言語を返す。null・無拡張子・未知は Plain。
        /// </summary>
        /// <param name="path">ファイルパス。無題なら null。</param>
        /// <returns>判定した言語。</returns>
        public static LanguageKind FromPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return LanguageKind.Plain;
            }

            string ext = Path.GetExtension(path);
            if (string.IsNullOrEmpty(ext))
            {
                return LanguageKind.Plain;
            }

            if (ext.Equals(".cs", StringComparison.OrdinalIgnoreCase))
            {
                return LanguageKind.CSharp;
            }

            if (ext.Equals(".bas", StringComparison.OrdinalIgnoreCase) ||
                ext.Equals(".cls", StringComparison.OrdinalIgnoreCase))
            {
                return LanguageKind.Vba;
            }

            if (ext.Equals(".ps1", StringComparison.OrdinalIgnoreCase) ||
                ext.Equals(".psm1", StringComparison.OrdinalIgnoreCase) ||
                ext.Equals(".psd1", StringComparison.OrdinalIgnoreCase))
            {
                return LanguageKind.PowerShell;
            }

            if (ext.Equals(".cmd", StringComparison.OrdinalIgnoreCase) ||
                ext.Equals(".bat", StringComparison.OrdinalIgnoreCase))
            {
                return LanguageKind.Cmd;
            }

            return LanguageKind.Plain;
        }

        /// <summary>
        /// ステータスに出す言語名。
        /// </summary>
        /// <param name="kind">言語。</param>
        /// <returns>表示名。</returns>
        public static string GetDisplayName(LanguageKind kind)
        {
            switch (kind)
            {
                case LanguageKind.CSharp:
                    return "C#";
                case LanguageKind.Vba:
                    return "VBA";
                case LanguageKind.PowerShell:
                    return "PowerShell";
                case LanguageKind.Cmd:
                    return "cmd";
                default:
                    return "プレーン";
            }
        }
    }
}
