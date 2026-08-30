using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation.Language;
using WindowsIDE.Editor;
using WindowsIDE.Languages.CSharp;

namespace WindowsIDE.Languages
{
    /// <summary>
    /// F12 の定義解決。BCL へは入らない。複数ヒットは先頭。ピッカーなし。
    /// </summary>
    public static class DefinitionResolver
    {
        /// <summary>
        /// キャレット位置の識別子をユーザー宣言へ解決する。失敗は false。
        /// </summary>
        /// <param name="language">言語。</param>
        /// <param name="buffer">現在バッファ。</param>
        /// <param name="session">行開始状態。無くてよい。</param>
        /// <param name="filePath">現在ファイル。無題は null。</param>
        /// <param name="workspaceRoot">ワークスペース根。無くてよい。</param>
        /// <param name="line">0 始まりの行。</param>
        /// <param name="column">0 始まりの列。</param>
        /// <param name="symbol">解決結果。</param>
        /// <returns>解決できたら true。</returns>
        public static bool TryResolve(LanguageKind language, TextBuffer buffer, HighlightSession session, string filePath, string workspaceRoot, int line, int column, out DeclaredSymbol symbol)
        {
            return TryResolve(language, buffer, session, filePath, workspaceRoot, line, column, null, out symbol);
        }

        /// <summary>
        /// 開いているバッファをディスクより優先して解決する。
        /// </summary>
        /// <param name="language">言語。</param>
        /// <param name="buffer">現在バッファ。</param>
        /// <param name="session">行開始状態。無くてよい。</param>
        /// <param name="filePath">現在ファイル。無題は null。</param>
        /// <param name="workspaceRoot">ワークスペース根。無くてよい。</param>
        /// <param name="line">0 始まりの行。</param>
        /// <param name="column">0 始まりの列。</param>
        /// <param name="openBuffers">開いている path→本文。無くてよい。</param>
        /// <param name="symbol">解決結果。</param>
        /// <returns>解決できたら true。</returns>
        public static bool TryResolve(LanguageKind language, TextBuffer buffer, HighlightSession session, string filePath, string workspaceRoot, int line, int column, Dictionary<string, TextBuffer> openBuffers, out DeclaredSymbol symbol)
        {
            symbol = null;
            IdentifierHit hit;
            if (!IdentifierAtCaret.TryGet(language, buffer, session, line, column, out hit))
            {
                return false;
            }

            if (language == LanguageKind.Cmd || language == LanguageKind.Plain)
            {
                return false;
            }

            if (language == LanguageKind.PowerShell)
            {
                List<DeclaredSymbol> psList = ListCurrent(language, buffer, filePath);
                if (hit.IsPowerShellVariable)
                {
                    if (hit.IsPowerShellDrive)
                    {
                        return false;
                    }

                    return TryPickCore(psList, hit.Name, language, false, false, null, null, new SymbolKind[] { SymbolKind.Local }, out symbol);
                }

                return TryPickCore(psList, hit.Name, language, false, false, null, null, new SymbolKind[] { SymbolKind.Method }, out symbol);
            }

            bool paren = FollowedByParen(buffer, hit);
            bool afterNew = PrecededByNew(buffer, hit);
            string receiver = DotReceiver(buffer, hit);
            List<DeclaredSymbol> current = ListCurrent(language, buffer, filePath);
            List<DeclaredSymbol> workspace = null;
            if (language == LanguageKind.CSharp)
            {
                workspace = WorkspaceSymbols.GetCsharp(workspaceRoot, openBuffers);
            }
            else if (language == LanguageKind.Vba)
            {
                workspace = WorkspaceSymbols.GetVba(workspaceRoot, openBuffers);
            }

            if (!string.IsNullOrEmpty(receiver))
            {
                if (TryPickCore(current, hit.Name, language, paren, afterNew, receiver, null, out symbol))
                {
                    return true;
                }

                if (workspace != null &&
                    TryPickCore(workspace, hit.Name, language, paren, afterNew, receiver, filePath, out symbol))
                {
                    return true;
                }
            }

            if (TryPickCore(current, hit.Name, language, paren, afterNew, null, null, out symbol))
            {
                return true;
            }

            if (workspace != null &&
                TryPickCore(workspace, hit.Name, language, paren, afterNew, null, filePath, out symbol))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// 現在バッファの宣言。C# はローカル込み。PS は function と変数。
        /// </summary>
        /// <param name="language">言語。</param>
        /// <param name="buffer">本文。</param>
        /// <param name="filePath">ディスクパス。無題は null。</param>
        /// <returns>出現順。</returns>
        public static List<DeclaredSymbol> ListCurrent(LanguageKind language, TextBuffer buffer, string filePath)
        {
            if (language == LanguageKind.CSharp)
            {
                return CSharpSymbols.Collect(buffer, filePath, true);
            }

            if (language == LanguageKind.Vba)
            {
                return WorkspaceSymbols.CollectVba(buffer, filePath);
            }

            if (language == LanguageKind.PowerShell)
            {
                return ListPowerShell(buffer, filePath);
            }

            return new List<DeclaredSymbol>();
        }

        /// <summary>
        /// Parser.ParseInput の function と変数宣言。ドットソースは見ない。Runspace は開かない。
        /// </summary>
        /// <param name="buffer">本文。</param>
        /// <param name="filePath">ディスクパス。無題は null。</param>
        /// <returns>出現順の宣言。</returns>
        public static List<DeclaredSymbol> ListPowerShell(TextBuffer buffer, string filePath)
        {
            List<DeclaredSymbol> result = new List<DeclaredSymbol>();
            if (buffer == null)
            {
                return result;
            }

            string text = buffer.GetText();
            System.Management.Automation.Language.Token[] tokens;
            ParseError[] errors;
            ScriptBlockAst ast;
            try
            {
                ast = Parser.ParseInput(text == null ? "" : text, out tokens, out errors);
            }
            catch (Exception)
            {
                return result;
            }

            if (ast == null)
            {
                return result;
            }

            IEnumerable<Ast> nodes;
            try
            {
                nodes = ast.FindAll(delegate(Ast item)
                {
                    return item is FunctionDefinitionAst ||
                        item is ParameterAst ||
                        item is AssignmentStatementAst ||
                        item is ForEachStatementAst;
                }, true);
            }
            catch (Exception)
            {
                return result;
            }

            foreach (Ast node in nodes)
            {
                FunctionDefinitionAst fn = node as FunctionDefinitionAst;
                if (fn != null)
                {
                    AddPowerShellFunction(fn, filePath, result);
                    continue;
                }

                ParameterAst paramAst = node as ParameterAst;
                if (paramAst != null)
                {
                    string name;
                    int line;
                    int col;
                    int length;
                    if (!TryDescribePowerShellVar(paramAst.Name, out name, out line, out col, out length))
                    {
                        continue;
                    }

                    result.Add(new DeclaredSymbol(name, SymbolKind.Local, LanguageKind.PowerShell, filePath, line, col, length, null, "param $" + name));
                    continue;
                }

                AssignmentStatementAst assign = node as AssignmentStatementAst;
                if (assign != null)
                {
                    string sig = null;
                    if (assign.Left != null && assign.Left.Extent != null)
                    {
                        sig = assign.Left.Extent.Text;
                    }

                    AddPowerShellAssignment(assign.Left, filePath, sig, result);
                    continue;
                }

                ForEachStatementAst loop = node as ForEachStatementAst;
                if (loop != null)
                {
                    string name;
                    int line;
                    int col;
                    int length;
                    if (!TryDescribePowerShellVar(loop.Variable, out name, out line, out col, out length))
                    {
                        continue;
                    }

                    result.Add(new DeclaredSymbol(name, SymbolKind.Local, LanguageKind.PowerShell, filePath, line, col, length, null, "$" + name));
                }
            }

            return result;
        }

        private static void AddPowerShellFunction(FunctionDefinitionAst fn, string filePath, List<DeclaredSymbol> result)
        {
            if (fn == null || fn.Extent == null || string.IsNullOrEmpty(fn.Name) || result == null)
            {
                return;
            }

            int line = fn.Extent.StartLineNumber - 1;
            int col = fn.Extent.StartColumnNumber - 1;
            string extentText = fn.Extent.Text;
            if (!string.IsNullOrEmpty(extentText))
            {
                int at = extentText.IndexOf(fn.Name, StringComparison.OrdinalIgnoreCase);
                if (at >= 0)
                {
                    col = col + at;
                }
            }

            if (line < 0)
            {
                line = 0;
            }

            if (col < 0)
            {
                col = 0;
            }

            result.Add(new DeclaredSymbol(fn.Name, SymbolKind.Method, LanguageKind.PowerShell, filePath, line, col, fn.Name.Length, null, "function " + fn.Name));
        }

        private static void AddPowerShellAssignment(ExpressionAst left, string filePath, string signature, List<DeclaredSymbol> result)
        {
            if (left == null || result == null)
            {
                return;
            }

            ConvertExpressionAst conv = left as ConvertExpressionAst;
            if (conv != null)
            {
                AddPowerShellAssignment(conv.Child, filePath, signature, result);
                return;
            }

            AttributedExpressionAst attr = left as AttributedExpressionAst;
            if (attr != null)
            {
                AddPowerShellAssignment(attr.Child, filePath, signature, result);
                return;
            }

            ArrayLiteralAst arr = left as ArrayLiteralAst;
            if (arr != null)
            {
                if (arr.Elements == null)
                {
                    return;
                }

                int e = 0;
                while (e < arr.Elements.Count)
                {
                    AddPowerShellAssignment(arr.Elements[e], filePath, signature, result);
                    e++;
                }

                return;
            }

            if (left is MemberExpressionAst || left is IndexExpressionAst)
            {
                return;
            }

            VariableExpressionAst ve = left as VariableExpressionAst;
            if (ve == null)
            {
                return;
            }

            string name;
            int line;
            int col;
            int length;
            if (!TryDescribePowerShellVar(ve, out name, out line, out col, out length))
            {
                return;
            }

            string sig = signature;
            if (string.IsNullOrEmpty(sig))
            {
                sig = "$" + name;
            }

            result.Add(new DeclaredSymbol(name, SymbolKind.Local, LanguageKind.PowerShell, filePath, line, col, length, null, sig));
        }

        private static bool TryDescribePowerShellVar(VariableExpressionAst ve, out string name, out int line, out int column, out int length)
        {
            name = null;
            line = 0;
            column = 0;
            length = 0;
            if (ve == null || ve.VariablePath == null || ve.Extent == null || ve.Splatted)
            {
                return false;
            }

            if (ve.VariablePath.IsDriveQualified && !IsPowerShellScopeName(ve.VariablePath.DriveName))
            {
                return false;
            }

            string user = ve.VariablePath.UserPath;
            if (string.IsNullOrEmpty(user))
            {
                return false;
            }

            if (ve.VariablePath.IsUnqualified)
            {
                name = user;
            }
            else
            {
                int colon = user.LastIndexOf(':');
                if (colon >= 0 && colon + 1 < user.Length)
                {
                    name = user.Substring(colon + 1);
                }
                else
                {
                    name = user;
                }
            }

            if (string.IsNullOrEmpty(name) || IsPowerShellConstantName(name))
            {
                return false;
            }

            line = ve.Extent.StartLineNumber - 1;
            column = ve.Extent.StartColumnNumber - 1;
            string extentText = ve.Extent.Text;
            if (!string.IsNullOrEmpty(extentText))
            {
                int at = extentText.LastIndexOf(name, StringComparison.OrdinalIgnoreCase);
                if (at >= 0)
                {
                    column = column + at;
                }
            }

            if (line < 0)
            {
                line = 0;
            }

            if (column < 0)
            {
                column = 0;
            }

            length = name.Length;
            return length > 0;
        }

        private static bool IsPowerShellScopeName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            return string.Equals(name, "local", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "script", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "global", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "private", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPowerShellConstantName(string name)
        {
            return string.Equals(name, "null", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "true", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "false", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryPickCore(List<DeclaredSymbol> list, string name, LanguageKind language, bool paren, bool afterNew, string receiverType, string skipPath, out DeclaredSymbol symbol)
        {
            return TryPickCore(list, name, language, paren, afterNew, receiverType, skipPath, null, out symbol);
        }

        private static bool TryPickCore(List<DeclaredSymbol> list, string name, LanguageKind language, bool paren, bool afterNew, string receiverType, string skipPath, SymbolKind[] kindOrder, out DeclaredSymbol symbol)
        {
            symbol = null;
            if (list == null || string.IsNullOrEmpty(name))
            {
                return false;
            }

            SymbolKind[] order;
            if (kindOrder != null && kindOrder.Length > 0)
            {
                order = kindOrder;
            }
            else if (afterNew && paren)
            {
                order = new SymbolKind[] { SymbolKind.Constructor, SymbolKind.Type, SymbolKind.Method, SymbolKind.Property, SymbolKind.Field, SymbolKind.Local };
            }
            else if (paren)
            {
                order = new SymbolKind[] { SymbolKind.Method, SymbolKind.Constructor, SymbolKind.Property, SymbolKind.Type, SymbolKind.Field, SymbolKind.Local };
            }
            else
            {
                order = new SymbolKind[] { SymbolKind.Type, SymbolKind.Method, SymbolKind.Constructor, SymbolKind.Property, SymbolKind.Field, SymbolKind.Local };
            }

            for (int k = 0; k < order.Length; k++)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    DeclaredSymbol item = list[i];
                    if (item == null || item.Kind != order[k])
                    {
                        continue;
                    }

                    if (SamePath(item.FilePath, skipPath))
                    {
                        continue;
                    }

                    if (!NamesEqual(language, item.Name, name))
                    {
                        continue;
                    }

                    if (!string.IsNullOrEmpty(receiverType))
                    {
                        if (item.Kind == SymbolKind.Type)
                        {
                            continue;
                        }

                        if (!NamesEqual(language, item.ContainingType, receiverType))
                        {
                            continue;
                        }
                    }

                    symbol = item;
                    return true;
                }
            }

            return false;
        }

        private static bool FollowedByParen(TextBuffer buffer, IdentifierHit hit)
        {
            if (buffer == null || hit == null || hit.Line < 0 || hit.Line >= buffer.LineCount)
            {
                return false;
            }

            string line = buffer.GetLine(hit.Line);
            int i = SkipSpaces(line, hit.Column + hit.Length);
            i = SkipGenericArgs(line, i);
            i = SkipSpaces(line, i);
            if (i >= line.Length)
            {
                return false;
            }

            return line[i] == '(';
        }

        private static int SkipSpaces(string line, int i)
        {
            while (i < line.Length && (line[i] == ' ' || line[i] == '\t'))
            {
                i++;
            }

            return i;
        }

        /// <summary>
        /// 識別子直後の `&lt;...&gt;` を飛ばす。比較式なら位置を戻す。
        /// </summary>
        /// <param name="line">1 行。</param>
        /// <param name="i">開始列。</param>
        /// <returns>閉じたあとの列。失敗なら i。</returns>
        private static int SkipGenericArgs(string line, int i)
        {
            if (line == null || i >= line.Length || line[i] != '<')
            {
                return i;
            }

            int saved = i;
            i++;
            int depth = 1;
            while (i < line.Length && depth > 0)
            {
                char c = line[i];
                if (c == '<')
                {
                    depth++;
                    i++;
                    continue;
                }

                if (c == '>')
                {
                    depth--;
                    i++;
                    continue;
                }

                if (c == ' ' || c == '\t' || c == ',' || c == '.' || c == '?' || ScanChars.IsIdentPart(c))
                {
                    i++;
                    continue;
                }

                return saved;
            }

            if (depth != 0)
            {
                return saved;
            }

            return i;
        }

        private static bool PrecededByNew(TextBuffer buffer, IdentifierHit hit)
        {
            if (buffer == null || hit == null || hit.Line < 0 || hit.Line >= buffer.LineCount)
            {
                return false;
            }

            string line = buffer.GetLine(hit.Line);
            int i = hit.Column - 1;
            while (i >= 0 && (line[i] == ' ' || line[i] == '\t'))
            {
                i--;
            }

            if (i < 2)
            {
                return false;
            }

            if (line[i] != 'w' || line[i - 1] != 'e' || line[i - 2] != 'n')
            {
                return false;
            }

            int before = i - 3;
            if (before >= 0 && ScanChars.IsIdentPart(line[before]))
            {
                return false;
            }

            return true;
        }

        private static string DotReceiver(TextBuffer buffer, IdentifierHit hit)
        {
            if (buffer == null || hit == null || hit.Line < 0 || hit.Line >= buffer.LineCount)
            {
                return null;
            }

            string line = buffer.GetLine(hit.Line);
            int i = hit.Column - 1;
            while (i >= 0 && (line[i] == ' ' || line[i] == '\t'))
            {
                i--;
            }

            if (i < 0 || line[i] != '.')
            {
                return null;
            }

            i--;
            while (i >= 0 && (line[i] == ' ' || line[i] == '\t'))
            {
                i--;
            }

            int end = i + 1;
            while (i >= 0 && ScanChars.IsIdentPart(line[i]))
            {
                i--;
            }

            if (i >= 0 && line[i] == '@')
            {
                i--;
            }

            int start = i + 1;
            if (start >= end)
            {
                return null;
            }

            string name = line.Substring(start, end - start);
            if (name.Length >= 2 && name[0] == '@')
            {
                name = name.Substring(1);
            }

            if (name == "this" || name == "base")
            {
                return null;
            }

            return name;
        }

        private static bool NamesEqual(LanguageKind language, string a, string b)
        {
            if (language == LanguageKind.CSharp)
            {
                return string.Equals(a, b, StringComparison.Ordinal);
            }

            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }

        private static bool SamePath(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
            {
                return false;
            }

            try
            {
                return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
