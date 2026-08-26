using System;
using System.IO;

namespace WindowsIDE.Terminal
{
    /// <summary>
    /// 対話シェルの exe フルパスと引数。SpecialFolder.System だけを使い、検索パスは見ない。
    /// </summary>
    public static class ShellPaths
    {
        /// <summary>
        /// 種別の exe フルパス。存在確認はしない。
        /// </summary>
        /// <param name="kind">シェル。</param>
        /// <returns>フルパス。</returns>
        public static string GetExecutable(ShellKind kind)
        {
            string system = Environment.GetFolderPath(Environment.SpecialFolder.System);
            if (kind == ShellKind.Cmd)
            {
                return Path.Combine(system, "cmd.exe");
            }

            return Path.Combine(system, "WindowsPowerShell", "v1.0", "powershell.exe");
        }

        /// <summary>
        /// 製品の対話引数。PowerShell はロゴ無し・プロファイル無し・実行ポリシー Bypass。cmd は /d のみ。
        /// </summary>
        /// <param name="kind">シェル。</param>
        /// <returns>引数文字列。</returns>
        public static string GetArguments(ShellKind kind)
        {
            if (kind == ShellKind.Cmd)
            {
                return "/d";
            }

            return "-NoLogo -NoProfile -ExecutionPolicy Bypass";
        }

        /// <summary>
        /// 作業ディレクトリ。ワークスペース根があればそれ、無ければユーザープロファイル。TEMP には落とさない。
        /// </summary>
        /// <param name="workspaceRoot">ワークスペース根。null や不存在はプロファイル。</param>
        /// <returns>存在するディレクトリ。</returns>
        public static string GetWorkingDirectory(string workspaceRoot)
        {
            if (!string.IsNullOrEmpty(workspaceRoot))
            {
                try
                {
                    if (Directory.Exists(workspaceRoot))
                    {
                        return Path.GetFullPath(workspaceRoot);
                    }
                }
                catch (ArgumentException)
                {
                }
                catch (NotSupportedException)
                {
                }
                catch (IOException)
                {
                }
            }

            string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(profile))
            {
                return profile;
            }

            return Environment.GetFolderPath(Environment.SpecialFolder.System);
        }

        /// <summary>
        /// SpecialFolder.System 直下の既知シェル exe だけを許可する。それ以外は拒否。
        /// </summary>
        /// <param name="exePath">検査するパス。</param>
        /// <returns>許可なら true。</returns>
        public static bool IsAllowedExecutable(string exePath)
        {
            if (string.IsNullOrEmpty(exePath))
            {
                return false;
            }

            string full;
            try
            {
                full = Path.GetFullPath(exePath);
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
            catch (IOException)
            {
                return false;
            }

            if (string.Equals(full, GetExecutable(ShellKind.PowerShell51), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(full, GetExecutable(ShellKind.Cmd), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }
    }
}
