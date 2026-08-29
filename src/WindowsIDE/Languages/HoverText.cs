using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using WindowsIDE.Editor;
using WindowsIDE.Languages.CSharp;

namespace WindowsIDE.Languages
{
    /// <summary>
    /// ホバー本文。F-DOC 枠 → 連続コメント → C# BCL XML。
    /// </summary>
    public static class HoverText
    {
        /// <summary>
        /// 識別子のホバー本文を取る。無ければ false。throw しない。
        /// </summary>
        /// <param name="language">言語。</param>
        /// <param name="buffer">現在バッファ。</param>
        /// <param name="session">行開始状態。無くてよい。</param>
        /// <param name="filePath">現在ファイル。無題は null。</param>
        /// <param name="workspaceRoot">ワークスペース根。無くてよい。</param>
        /// <param name="line">0 始まりの行。</param>
        /// <param name="column">0 始まりの列。</param>
        /// <param name="text">本文。</param>
        /// <returns>出せたら true。</returns>
        public static bool TryGet(LanguageKind language, TextBuffer buffer, HighlightSession session, string filePath, string workspaceRoot, int line, int column, out string text)
        {
            return TryGet(language, buffer, session, filePath, workspaceRoot, line, column, null, out text);
        }

        /// <summary>
        /// 開いているバッファを優先してホバー本文を取る。
        /// </summary>
        /// <param name="language">言語。</param>
        /// <param name="buffer">現在バッファ。</param>
        /// <param name="session">行開始状態。無くてよい。</param>
        /// <param name="filePath">現在ファイル。無題は null。</param>
        /// <param name="workspaceRoot">ワークスペース根。無くてよい。</param>
        /// <param name="line">0 始まりの行。</param>
        /// <param name="column">0 始まりの列。</param>
        /// <param name="openBuffers">path→本文。無くてよい。</param>
        /// <param name="text">本文。</param>
        /// <returns>出せたら true。</returns>
        public static bool TryGet(LanguageKind language, TextBuffer buffer, HighlightSession session, string filePath, string workspaceRoot, int line, int column, Dictionary<string, TextBuffer> openBuffers, out string text)
        {
            text = null;
            IdentifierHit hit;
            if (!IdentifierAtCaret.TryGet(language, buffer, session, line, column, out hit))
            {
                return false;
            }

            bool userDef = false;
            int defLine = hit.Line;
            TextBuffer defBuffer = buffer;
            HighlightSession defSession = session;
            DeclaredSymbol symbol = null;
            if (DefinitionResolver.TryResolve(language, buffer, session, filePath, workspaceRoot, line, column, openBuffers, out symbol) && symbol != null)
            {
                userDef = true;
                defLine = symbol.Line;
                if (!string.IsNullOrEmpty(symbol.FilePath) && !SamePath(symbol.FilePath, filePath))
                {
                    TextBuffer other;
                    if (TryOpenDefBuffer(symbol.FilePath, openBuffers, out other))
                    {
                        defBuffer = other;
                        defSession = new HighlightSession();
                        defSession.Reset(language, other.LineCount);
                        defSession.SyncAfterEdit(other, 0);
                    }
                }
            }
            else if (language == LanguageKind.Cmd)
            {
                userDef = true;
                defLine = hit.Line;
            }

            string comment = null;
            TryExtractComments(language, defBuffer, defSession, defLine, out comment);

            if (symbol != null)
            {
                string sig = symbol.Signature;
                if (string.IsNullOrEmpty(sig))
                {
                    sig = symbol.Name;
                }

                if (!string.IsNullOrEmpty(comment))
                {
                    text = sig + "\n\n" + comment;
                }
                else
                {
                    text = sig;
                }

                return !string.IsNullOrEmpty(text);
            }

            if (!string.IsNullOrEmpty(comment))
            {
                text = comment;
                return true;
            }

            if (!userDef && language == LanguageKind.CSharp && BclTypeCache.Contains(hit.Name))
            {
                string qualified;
                if (BclTypeCache.TryGetQualifiedName(hit.Name, out qualified))
                {
                    try
                    {
                        if (BclXmlDocs.TryGetTypeSummary(qualified, out text) && !string.IsNullOrEmpty(text))
                        {
                            return true;
                        }
                    }
                    catch (Exception)
                    {
                        text = null;
                    }
                }
            }

            return false;
        }

        private static bool TryOpenDefBuffer(string path, Dictionary<string, TextBuffer> openBuffers, out TextBuffer buffer)
        {
            buffer = null;
            if (openBuffers != null)
            {
                foreach (KeyValuePair<string, TextBuffer> pair in openBuffers)
                {
                    if (pair.Value == null || string.IsNullOrEmpty(pair.Key))
                    {
                        continue;
                    }

                    if (SamePath(pair.Key, path))
                    {
                        buffer = pair.Value;
                        return true;
                    }
                }
            }

            return WorkspaceSymbols.TryReadBuffer(path, out buffer);
        }

        private static bool TryExtractComments(LanguageKind language, TextBuffer buffer, HighlightSession session, int definitionLine, out string text)
        {
            text = null;
            if (buffer == null || definitionLine <= 0 || definitionLine > buffer.LineCount)
            {
                return false;
            }

            List<string> stripped = new List<string>();
            int line = definitionLine - 1;
            while (line >= 0)
            {
                string rest;
                if (!DocCommentRules.TryStripComment(language, buffer.GetLine(line), out rest))
                {
                    break;
                }

                if (IsBlankLine(buffer.GetLine(line)))
                {
                    break;
                }

                stripped.Insert(0, rest);
                line--;
            }

            if (stripped.Count == 0)
            {
                return false;
            }

            string[] frame;
            if (DocCommentRules.TryGetFrame(language, buffer, session, definitionLine, out frame) && frame != null)
            {
                text = JoinLines(frame);
                return !string.IsNullOrEmpty(text);
            }

            if (language == LanguageKind.CSharp && AllTripleSlash(buffer, definitionLine, stripped.Count))
            {
                string xml;
                if (TrySummaryFromSlashes(stripped, out xml))
                {
                    text = xml;
                    return true;
                }
            }

            text = JoinLines(stripped.ToArray());
            return !string.IsNullOrEmpty(text);
        }

        private static bool AllTripleSlash(TextBuffer buffer, int definitionLine, int count)
        {
            int start = definitionLine - count;
            for (int i = 0; i < count; i++)
            {
                string line = buffer.GetLine(start + i).TrimStart(' ', '\t');
                if (!line.StartsWith("///", StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TrySummaryFromSlashes(List<string> stripped, out string text)
        {
            text = null;
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < stripped.Count; i++)
            {
                sb.Append(stripped[i]);
                sb.Append('\n');
            }

            try
            {
                XmlDocument doc = new XmlDocument();
                doc.XmlResolver = null;
                doc.LoadXml("<root>" + sb.ToString() + "</root>");
                XmlNodeList nodes = doc.GetElementsByTagName("summary");
                if (nodes == null || nodes.Count == 0)
                {
                    return false;
                }

                string raw = nodes[0].InnerText;
                if (string.IsNullOrEmpty(raw))
                {
                    return false;
                }

                text = raw.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
                return text.Length > 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string JoinLines(string[] lines)
        {
            if (lines == null || lines.Length == 0)
            {
                return "";
            }

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < lines.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append('\n');
                }

                sb.Append(lines[i]);
            }

            return sb.ToString();
        }

        private static bool IsBlankLine(string line)
        {
            if (line == null)
            {
                return true;
            }

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c != ' ' && c != '\t')
                {
                    return false;
                }
            }

            return true;
        }

        private static bool SamePath(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
            {
                return false;
            }

            try
            {
                return string.Equals(System.IO.Path.GetFullPath(a), System.IO.Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
