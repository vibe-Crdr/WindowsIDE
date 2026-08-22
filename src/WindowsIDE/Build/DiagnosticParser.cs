using System;
using System.Collections.Generic;

namespace WindowsIDE.Build
{
    /// <summary>
    /// csc の stdout/stderr を診断にする。Regex は使わず IndexOf / Substring / int.TryParse のみ。
    /// </summary>
    public static class DiagnosticParser
    {
        private const string LocError = "): error ";
        private const string LocWarning = "): warning ";
        private const string LocFatal = "): fatal error ";
        private const string FileError = "error CS";
        private const string FileFatal = "fatal error CS";
        private const string FileWarning = "warning CS";
        private const string CscError = "CSC : error CS";
        private const string CscWarning = "CSC : warning CS";

        /// <summary>
        /// バナーと空行を捨て、error / warning を返す。位置付きを先に見る。
        /// </summary>
        /// <param name="text">csc の出力。null は空。</param>
        /// <returns>診断配列。</returns>
        public static Diagnostic[] Parse(string text)
        {
            List<Diagnostic> list = new List<Diagnostic>();
            if (string.IsNullOrEmpty(text))
            {
                return new Diagnostic[0];
            }

            string normalized = text.Replace("\r\n", "\n").Replace("\r", "\n");
            string[] lines = normalized.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                Diagnostic d = ParseLine(lines[i]);
                if (d != null)
                {
                    list.Add(d);
                }
            }

            return list.ToArray();
        }

        /// <summary>
        /// csc が非ゼロで終わったのに診断が 0 件なら、成功表示にしない合成 1 件を足す。
        /// parsed が既にあればそのまま。exit 0 の空は空のまま（偽の成功診断は足さない）。
        /// </summary>
        /// <param name="parsed">Parse の結果。null は空配列。</param>
        /// <param name="exitCode">csc の終了コード。</param>
        /// <param name="combinedOutput">stdout と stderr。要約に使う。</param>
        /// <returns>表示する診断。</returns>
        public static Diagnostic[] ApplyExitCode(Diagnostic[] parsed, int exitCode, string combinedOutput)
        {
            if (parsed == null)
            {
                parsed = new Diagnostic[0];
            }

            if (parsed.Length > 0)
            {
                return parsed;
            }

            if (exitCode == 0)
            {
                return parsed;
            }

            string message = string.Format("csc が失敗した (exit {0})。", exitCode);
            string extra = FirstNonEmptyLine(combinedOutput);
            if (!string.IsNullOrEmpty(extra))
            {
                message = message + " " + extra;
            }

            return new Diagnostic[] { Diagnostic.CreateSynthetic(message) };
        }

        private static string FirstNonEmptyLine(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "";
            }

            string normalized = text.Replace("\r\n", "\n").Replace("\r", "\n");
            string[] lines = normalized.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length > 0)
                {
                    if (line.Length > 200)
                    {
                        return line.Substring(0, 200);
                    }

                    return line;
                }
            }

            return "";
        }

        private static Diagnostic ParseLine(string line)
        {
            if (string.IsNullOrEmpty(line))
            {
                return null;
            }

            string trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                return null;
            }

            Diagnostic located = ParseLocated(trimmed);
            if (located != null)
            {
                return located;
            }

            return ParseFileless(trimmed);
        }

        private static Diagnostic ParseLocated(string line)
        {
            bool isError = true;
            int kindAt = line.IndexOf(LocError, StringComparison.Ordinal);
            int kindLen = LocError.Length;
            if (kindAt < 0)
            {
                kindAt = line.IndexOf(LocFatal, StringComparison.Ordinal);
                kindLen = LocFatal.Length;
            }

            if (kindAt < 0)
            {
                kindAt = line.IndexOf(LocWarning, StringComparison.Ordinal);
                kindLen = LocWarning.Length;
                isError = false;
            }

            if (kindAt < 0)
            {
                return null;
            }

            int open = line.LastIndexOf('(', kindAt);
            if (open < 0)
            {
                return null;
            }

            string path = line.Substring(0, open).Trim();
            if (path.Length == 0)
            {
                path = null;
            }

            string coords = line.Substring(open + 1, kindAt - (open + 1));
            int lineNo;
            int colNo;
            ParseCoords(coords, out lineNo, out colNo);
            string rest = line.Substring(kindAt + kindLen);
            string code;
            string message;
            SplitCodeMessage(rest, out code, out message);
            return Diagnostic.FromCompiler(path, lineNo, colNo, isError, code, message);
        }

        private static Diagnostic ParseFileless(string line)
        {
            bool isError = true;
            int csAt = -1;
            int p = line.IndexOf(FileFatal, StringComparison.Ordinal);
            if (p >= 0)
            {
                csAt = p + "fatal error ".Length;
            }
            else
            {
                p = line.IndexOf(CscError, StringComparison.Ordinal);
                if (p >= 0)
                {
                    csAt = p + "CSC : error ".Length;
                }
                else
                {
                    p = line.IndexOf(CscWarning, StringComparison.Ordinal);
                    if (p >= 0)
                    {
                        isError = false;
                        csAt = p + "CSC : warning ".Length;
                    }
                    else
                    {
                        p = line.IndexOf(FileError, StringComparison.Ordinal);
                        if (p >= 0)
                        {
                            csAt = p + "error ".Length;
                        }
                        else
                        {
                            p = line.IndexOf(FileWarning, StringComparison.Ordinal);
                            if (p >= 0)
                            {
                                isError = false;
                                csAt = p + "warning ".Length;
                            }
                        }
                    }
                }
            }

            if (csAt < 0)
            {
                return null;
            }

            string rest = line.Substring(csAt);
            string code;
            string message;
            SplitCodeMessage(rest, out code, out message);
            return Diagnostic.FromCompiler(null, 0, 0, isError, code, message);
        }

        private static void ParseCoords(string coords, out int lineNo, out int colNo)
        {
            lineNo = 0;
            colNo = 0;
            if (string.IsNullOrEmpty(coords))
            {
                return;
            }

            string text = coords.Trim();
            int comma = text.IndexOf(',');
            if (comma < 0)
            {
                int n;
                if (int.TryParse(text, out n) && n > 0)
                {
                    lineNo = n;
                }

                return;
            }

            int lineVal;
            if (int.TryParse(text.Substring(0, comma).Trim(), out lineVal) && lineVal > 0)
            {
                lineNo = lineVal;
            }

            int colVal;
            if (int.TryParse(text.Substring(comma + 1).Trim(), out colVal) && colVal > 0)
            {
                colNo = colVal;
            }
        }

        private static void SplitCodeMessage(string rest, out string code, out string message)
        {
            code = null;
            message = "";
            if (string.IsNullOrEmpty(rest))
            {
                return;
            }

            string text = rest.Trim();
            int colon = text.IndexOf(':');
            if (colon < 0)
            {
                message = text;
                return;
            }

            string maybeCode = text.Substring(0, colon).Trim();
            if (LooksLikeCsCode(maybeCode))
            {
                code = maybeCode;
                message = text.Substring(colon + 1).Trim();
            }
            else
            {
                message = text;
            }
        }

        private static bool LooksLikeCsCode(string text)
        {
            if (string.IsNullOrEmpty(text) || text.Length < 3)
            {
                return false;
            }

            if (text[0] != 'C' || text[1] != 'S')
            {
                return false;
            }

            if (text.Length - 2 < 1)
            {
                return false;
            }

            for (int i = 2; i < text.Length; i++)
            {
                char c = text[i];
                if (c < '0' || c > '9')
                {
                    return false;
                }
            }

            return true;
        }
    }
}
