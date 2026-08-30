using System;
using System.Collections.Generic;
using System.Management.Automation.Language;

namespace WindowsIDE.Languages
{
    /// <summary>
    /// Parser.ParseInput の 1 件。行・列は 1 始まり。Build.Diagnostic は作らない。
    /// </summary>
    public sealed class PowerShellParseError
    {
        private int startLine;
        private int startColumn;
        private int endLine;
        private int endColumn;
        private string message;

        /// <summary>
        /// 位置付きで作る。
        /// </summary>
        /// <param name="startLine">1 始まりの開始行。</param>
        /// <param name="startColumn">1 始まりの開始列。</param>
        /// <param name="endLine">1 始まりの終了行。</param>
        /// <param name="endColumn">1 始まりの終了列（含む）。</param>
        /// <param name="message">本文。</param>
        public PowerShellParseError(int startLine, int startColumn, int endLine, int endColumn, string message)
        {
            this.startLine = startLine;
            this.startColumn = startColumn;
            this.endLine = endLine;
            this.endColumn = endColumn;
            this.message = (message == null) ? "" : message;
        }

        /// <summary>1 始まりの開始行。</summary>
        public int StartLine
        {
            get { return this.startLine; }
        }

        /// <summary>1 始まりの開始列。</summary>
        public int StartColumn
        {
            get { return this.startColumn; }
        }

        /// <summary>1 始まりの終了行。</summary>
        public int EndLine
        {
            get { return this.endLine; }
        }

        /// <summary>1 始まりの終了列（含む）。</summary>
        public int EndColumn
        {
            get { return this.endColumn; }
        }

        /// <summary>本文。</summary>
        public string Message
        {
            get { return this.message; }
        }
    }

    /// <summary>
    /// SMA の Parser.ParseInput から構文エラー位置を取る。Runspace は開かない。
    /// </summary>
    public static class PowerShellParseErrors
    {
        /// <summary>
        /// 本文の ParseInput errors を返す。Classify は errors を捨てたまま。
        /// </summary>
        /// <param name="text">スクリプト。null は空。</param>
        /// <returns>エラー配列。</returns>
        public static PowerShellParseError[] Collect(string text)
        {
            System.Management.Automation.Language.Token[] tokens;
            ParseError[] errors;
            try
            {
                Parser.ParseInput(text == null ? "" : text, out tokens, out errors);
            }
            catch (Exception)
            {
                return new PowerShellParseError[0];
            }

            if (errors == null || errors.Length == 0)
            {
                return new PowerShellParseError[0];
            }

            List<PowerShellParseError> list = new List<PowerShellParseError>();
            int i = 0;
            while (i < errors.Length)
            {
                ParseError err = errors[i];
                i++;
                if (err == null)
                {
                    continue;
                }

                int startLine = 1;
                int startColumn = 1;
                int endLine = 1;
                int endColumn = 1;
                IScriptExtent extent = err.Extent;
                if (extent != null)
                {
                    startLine = extent.StartLineNumber;
                    startColumn = extent.StartColumnNumber;
                    endLine = extent.EndLineNumber;
                    // Extent の EndColumnNumber は 1 始まりの排他。含む列へ直す。
                    endColumn = extent.EndColumnNumber - 1;
                    if (endColumn < startColumn && endLine == startLine)
                    {
                        endColumn = startColumn;
                    }

                    if (endLine < startLine)
                    {
                        endLine = startLine;
                    }
                }

                list.Add(new PowerShellParseError(startLine, startColumn, endLine, endColumn, err.Message));
            }

            return list.ToArray();
        }
    }
}
