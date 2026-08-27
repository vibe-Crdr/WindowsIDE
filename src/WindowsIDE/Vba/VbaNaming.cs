using System;
using System.Collections.Generic;
using System.IO;

namespace WindowsIDE.Vba
{
    /// <summary>
    /// Excel 側名の付け方。正は vba-map の namingMode。
    /// </summary>
    public enum VbaNamingMode
    {
        /// <summary>ファイル名から拡張子を除く。既定。</summary>
        Filename,

        /// <summary>root からの相対フォルダを _ でつなぎ、最後にファイル名。</summary>
        FolderPrefix
    }

    /// <summary>
    /// 同じ Excel 名になる 2 パス。
    /// </summary>
    public sealed class VbaNameCollision
    {
        private string excelName;
        private string pathA;
        private string pathB;

        /// <summary>
        /// 衝突した Excel 名と両方のパスを覚える。
        /// </summary>
        /// <param name="excelName">衝突した名前。</param>
        /// <param name="pathA">一方のパス。</param>
        /// <param name="pathB">もう一方のパス。</param>
        public VbaNameCollision(string excelName, string pathA, string pathB)
        {
            this.excelName = excelName;
            this.pathA = pathA;
            this.pathB = pathB;
        }

        /// <summary>衝突した Excel 名。</summary>
        public string ExcelName
        {
            get { return this.excelName; }
        }

        /// <summary>一方のパス。</summary>
        public string PathA
        {
            get { return this.pathA; }
        }

        /// <summary>もう一方のパス。</summary>
        public string PathB
        {
            get { return this.pathB; }
        }
    }

    /// <summary>
    /// ディスク相対パスから Excel 名を決める。document はモードを無視して Excel 名を使う。
    /// </summary>
    public static class VbaNaming
    {
        /// <summary>
        /// XML 値からモードを読む。不明なら filename。
        /// </summary>
        /// <param name="text">filename / folder_prefix。</param>
        /// <returns>モード。</returns>
        public static VbaNamingMode ParseMode(string text)
        {
            if (text != null && string.Equals(text.Trim(), "folder_prefix", StringComparison.OrdinalIgnoreCase))
            {
                return VbaNamingMode.FolderPrefix;
            }

            return VbaNamingMode.Filename;
        }

        /// <summary>
        /// マップへ書く値。
        /// </summary>
        /// <param name="mode">モード。</param>
        /// <returns>filename または folder_prefix。</returns>
        public static string ToXml(VbaNamingMode mode)
        {
            return (mode == VbaNamingMode.FolderPrefix) ? "folder_prefix" : "filename";
        }

        /// <summary>
        /// root からの相対パス（区切り / または \）から Excel 名を作る。document は documentName。
        /// </summary>
        /// <param name="relPath">root からの相対。</param>
        /// <param name="mode">filename / folder_prefix。</param>
        /// <param name="kind">コンポーネント種別。</param>
        /// <param name="documentName">document のとき使う Excel 名。</param>
        /// <returns>Excel 側名。作れなければ空文字。</returns>
        public static string ToExcelName(string relPath, VbaNamingMode mode, VbaComponentKind kind, string documentName)
        {
            if (kind == VbaComponentKind.Document)
            {
                return (documentName == null) ? "" : documentName;
            }

            return FromRelPath(relPath, mode);
        }

        /// <summary>
        /// std / class の Excel 名。フォルダは folder_prefix のときだけ _ でつなぐ。
        /// </summary>
        /// <param name="relPath">root からの相対。</param>
        /// <param name="mode">モード。</param>
        /// <returns>Excel 名。</returns>
        public static string FromRelPath(string relPath, VbaNamingMode mode)
        {
            if (string.IsNullOrEmpty(relPath))
            {
                return "";
            }

            string normalized = relPath.Replace('\\', '/').Trim('/');
            string file = Path.GetFileNameWithoutExtension(normalized.Replace('/', Path.DirectorySeparatorChar));
            if (string.IsNullOrEmpty(file))
            {
                return "";
            }

            if (mode != VbaNamingMode.FolderPrefix)
            {
                return file;
            }

            int slash = normalized.LastIndexOf('/');
            if (slash < 0)
            {
                return file;
            }

            string dir = normalized.Substring(0, slash);
            string prefix = dir.Replace('/', '_');
            if (string.IsNullOrEmpty(prefix))
            {
                return file;
            }

            return prefix + "_" + file;
        }

        /// <summary>
        /// 同じ Excel 名（大文字小文字無視）になる組を返す。衝突が無ければ空配列。
        /// </summary>
        /// <param name="excelNames">各ファイルの Excel 名。paths と同じ長さ。</param>
        /// <param name="paths">診断に出すパス。</param>
        /// <returns>衝突組。</returns>
        public static VbaNameCollision[] FindCollisions(string[] excelNames, string[] paths)
        {
            if (excelNames == null || paths == null || excelNames.Length != paths.Length)
            {
                return new VbaNameCollision[0];
            }

            List<VbaNameCollision> list = new List<VbaNameCollision>();
            for (int i = 0; i < excelNames.Length; i++)
            {
                string a = excelNames[i];
                if (string.IsNullOrEmpty(a))
                {
                    continue;
                }

                for (int j = i + 1; j < excelNames.Length; j++)
                {
                    string b = excelNames[j];
                    if (string.IsNullOrEmpty(b))
                    {
                        continue;
                    }

                    if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase))
                    {
                        list.Add(new VbaNameCollision(a, paths[i], paths[j]));
                    }
                }
            }

            return list.ToArray();
        }
    }
}
