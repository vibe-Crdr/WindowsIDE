using System;
using System.Collections.Generic;
using System.Management.Automation.Language;
using WindowsIDE.Editor;

namespace WindowsIDE.Languages
{
    /// <summary>
    /// PowerShell ハイライト用。SMA の Parser.ParseInput だけを使う。
    /// </summary>
    public static class PowerShellSemantic
    {
        /// <summary>
        /// AST から関数・変数・型・コマンドを overlay へ書く。
        /// </summary>
        /// <param name="buffer">本文。</param>
        /// <param name="overlay">行ごとのスパン。</param>
        public static void Classify(TextBuffer buffer, List<ClassifySpan>[] overlay)
        {
            if (buffer == null || overlay == null)
            {
                return;
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
                return;
            }

            if (ast == null)
            {
                return;
            }

            IEnumerable<Ast> nodes;
            try
            {
                nodes = ast.FindAll(delegate(Ast item) { return item != null; }, true);
            }
            catch (Exception)
            {
                return;
            }

            foreach (Ast node in nodes)
            {
                FunctionDefinitionAst fn = node as FunctionDefinitionAst;
                if (fn != null && fn.Extent != null && !string.IsNullOrEmpty(fn.Name))
                {
                    AddNamed(buffer, overlay, fn.Extent, fn.Name, TokenKind.Method);
                    continue;
                }

                VariableExpressionAst variable = node as VariableExpressionAst;
                if (variable != null && variable.Extent != null)
                {
                    AddVariable(buffer, overlay, variable);
                    continue;
                }

                TypeConstraintAst constraint = node as TypeConstraintAst;
                if (constraint != null && constraint.Extent != null)
                {
                    AddTypeExtent(buffer, overlay, constraint.Extent);
                    continue;
                }

                TypeExpressionAst typeExpr = node as TypeExpressionAst;
                if (typeExpr != null && typeExpr.Extent != null)
                {
                    AddTypeExtent(buffer, overlay, typeExpr.Extent);
                    continue;
                }

                InvokeMemberExpressionAst invoke = node as InvokeMemberExpressionAst;
                if (invoke != null && invoke.Member != null && invoke.Member.Extent != null)
                {
                    Add(buffer, overlay, invoke.Member.Extent, TokenKind.Method);
                    continue;
                }

                MemberExpressionAst member = node as MemberExpressionAst;
                if (member != null && member.Member != null && member.Member.Extent != null)
                {
                    Add(buffer, overlay, member.Member.Extent, TokenKind.Instance);
                    continue;
                }

                CommandAst command = node as CommandAst;
                if (command != null && command.CommandElements != null && command.CommandElements.Count > 0)
                {
                    string name = command.GetCommandName();
                    if (!string.IsNullOrEmpty(name))
                    {
                        Add(buffer, overlay, command.CommandElements[0].Extent, TokenKind.Method);
                    }
                }
            }
        }

        private static void AddVariable(TextBuffer buffer, List<ClassifySpan>[] overlay, VariableExpressionAst variable)
        {
            IScriptExtent extent = variable.Extent;
            if (extent == null)
            {
                return;
            }

            string value = extent.Text;
            int extra = 0;
            if (!string.IsNullOrEmpty(value) && value[0] == '$')
            {
                extra = 1;
            }

            int start = ColumnToIndex(buffer, extent.StartLineNumber - 1, extent.StartColumnNumber - 1) + extra;
            int length = extent.EndOffset - extent.StartOffset - extra;
            if (length < 0)
            {
                length = 0;
            }

            AddAt(overlay, extent.StartLineNumber - 1, start, length, TokenKind.Local);
        }

        private static void AddTypeExtent(TextBuffer buffer, List<ClassifySpan>[] overlay, IScriptExtent extent)
        {
            string value = extent.Text;
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            int line = extent.StartLineNumber - 1;
            int col = extent.StartColumnNumber - 1;
            int index = ColumnToIndex(buffer, line, col);
            int start = 0;
            int length = value.Length;
            if (value[0] == '[' && value[value.Length - 1] == ']' && value.Length >= 2)
            {
                start = 1;
                length = value.Length - 2;
            }

            int lastDot = value.LastIndexOf('.');
            if (lastDot >= start)
            {
                int skip = lastDot - start + 1;
                start += skip;
                length -= skip;
            }

            AddAt(overlay, line, index + start, length, TokenKind.Type);
        }

        private static void AddNamed(TextBuffer buffer, List<ClassifySpan>[] overlay, IScriptExtent extent, string name, TokenKind kind)
        {
            if (extent == null || string.IsNullOrEmpty(name))
            {
                return;
            }

            string text = extent.Text;
            int at = 0;
            if (!string.IsNullOrEmpty(text))
            {
                at = text.IndexOf(name, StringComparison.OrdinalIgnoreCase);
                if (at < 0)
                {
                    at = 0;
                }
            }

            int line = extent.StartLineNumber - 1;
            int col = extent.StartColumnNumber - 1 + at;
            int start = ColumnToIndex(buffer, line, col);
            AddAt(overlay, line, start, name.Length, kind);
        }

        private static void Add(TextBuffer buffer, List<ClassifySpan>[] overlay, IScriptExtent extent, TokenKind kind)
        {
            if (extent == null)
            {
                return;
            }

            int line = extent.StartLineNumber - 1;
            int col = extent.StartColumnNumber - 1;
            int start = ColumnToIndex(buffer, line, col);
            int length = extent.EndOffset - extent.StartOffset;
            if (length < 0)
            {
                length = extent.Text == null ? 0 : extent.Text.Length;
            }

            AddAt(overlay, line, start, length, kind);
        }

        private static void AddAt(List<ClassifySpan>[] overlay, int line, int start, int length, TokenKind kind)
        {
            if (overlay == null || line < 0 || line >= overlay.Length || length <= 0 || start < 0)
            {
                return;
            }

            overlay[line].Add(new ClassifySpan(start, length, kind));
        }

        private static int ColumnToIndex(TextBuffer buffer, int line, int column)
        {
            if (buffer == null || line < 0 || line >= buffer.LineCount)
            {
                return column;
            }

            string text = buffer.GetLine(line);
            if (column < 0)
            {
                return 0;
            }

            if (column > text.Length)
            {
                return text.Length;
            }

            return column;
        }
    }
}
