using System;
using System.Collections.Generic;
using System.IO;
using WindowsIDE.Editor;
using WindowsIDE.Languages.CSharp;
using WindowsIDE.Workspace;

namespace WindowsIDE.Languages
{
    /// <summary>
    /// ワークスペース .cs / .bas / .cls の位置付き宣言キャッシュ。stamp は WorkspaceTypeNames と同型。
    /// </summary>
    public static class WorkspaceSymbols
    {
        private static string cachedCsRoot;
        private static List<DeclaredSymbol> cachedCs;
        private static int cachedCsCount;
        private static long cachedCsStamp;
        private static string cachedVbaRoot;
        private static List<DeclaredSymbol> cachedVba;
        private static int cachedVbaCount;
        private static long cachedVbaStamp;
        private static readonly object Gate = new object();

        /// <summary>
        /// キャッシュを捨てる。保存やフォルダ再読込のあと次の Get で集め直す。
        /// </summary>
        public static void Invalidate()
        {
            lock (Gate)
            {
                cachedCsRoot = null;
                cachedCs = null;
                cachedCsCount = -1;
                cachedCsStamp = 0;
                cachedVbaRoot = null;
                cachedVba = null;
                cachedVbaCount = -1;
                cachedVbaStamp = 0;
            }
        }

        /// <summary>
        /// ワークスペース .cs の型・メソッド・プロパティ。ローカルは含めない。
        /// </summary>
        /// <param name="root">ワークスペースルート。</param>
        /// <returns>出現順。root が空なら空。</returns>
        public static List<DeclaredSymbol> GetCsharp(string root)
        {
            return GetCsharp(root, null);
        }

        /// <summary>
        /// ワークスペース .cs の宣言。開いているバッファがあればそのパスを上書きする。
        /// </summary>
        /// <param name="root">ワークスペースルート。</param>
        /// <param name="openBuffers">path→本文。無くてよい。</param>
        /// <returns>出現順。</returns>
        public static List<DeclaredSymbol> GetCsharp(string root, Dictionary<string, TextBuffer> openBuffers)
        {
            List<DeclaredSymbol> disk = GetCsharpDisk(root);
            return OverlayCsharp(disk, openBuffers);
        }

        /// <summary>
        /// ワークスペース .bas / .cls の Sub / Function / Property。Excel 名は見ない。
        /// </summary>
        /// <param name="root">ワークスペースルート。</param>
        /// <returns>出現順。root が空なら空。</returns>
        public static List<DeclaredSymbol> GetVba(string root)
        {
            return GetVba(root, null);
        }

        /// <summary>
        /// ワークスペース VBA 宣言。開いているバッファがあればそのパスを上書きする。
        /// </summary>
        /// <param name="root">ワークスペースルート。</param>
        /// <param name="openBuffers">path→本文。無くてよい。</param>
        /// <returns>出現順。</returns>
        public static List<DeclaredSymbol> GetVba(string root, Dictionary<string, TextBuffer> openBuffers)
        {
            List<DeclaredSymbol> disk = GetVbaDisk(root);
            return OverlayVba(disk, openBuffers);
        }

        private static List<DeclaredSymbol> GetCsharpDisk(string root)
        {
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
            {
                return new List<DeclaredSymbol>();
            }

            string full = Path.GetFullPath(root);
            int count;
            long stamp;
            string[] files = CsFileEnumerator.List(full, out count, out stamp);
            lock (Gate)
            {
                if (cachedCs != null &&
                    string.Equals(cachedCsRoot, full, StringComparison.OrdinalIgnoreCase) &&
                    cachedCsCount == count &&
                    cachedCsStamp == stamp)
                {
                    return cachedCs;
                }

                List<DeclaredSymbol> list = new List<DeclaredSymbol>();
                CollectCsharpFiles(files, list);
                cachedCsRoot = full;
                cachedCs = list;
                cachedCsCount = count;
                cachedCsStamp = stamp;
                return list;
            }
        }

        private static List<DeclaredSymbol> GetVbaDisk(string root)
        {
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
            {
                return new List<DeclaredSymbol>();
            }

            string full = Path.GetFullPath(root);
            int count;
            long stamp;
            string[] files = VbaFileEnumerator.List(full, out count, out stamp);
            lock (Gate)
            {
                if (cachedVba != null &&
                    string.Equals(cachedVbaRoot, full, StringComparison.OrdinalIgnoreCase) &&
                    cachedVbaCount == count &&
                    cachedVbaStamp == stamp)
                {
                    return cachedVba;
                }

                List<DeclaredSymbol> list = new List<DeclaredSymbol>();
                CollectVbaFiles(files, list);
                cachedVbaRoot = full;
                cachedVba = list;
                cachedVbaCount = count;
                cachedVbaStamp = stamp;
                return list;
            }
        }

        private static List<DeclaredSymbol> OverlayCsharp(List<DeclaredSymbol> disk, Dictionary<string, TextBuffer> openBuffers)
        {
            return Overlay(disk, openBuffers, true);
        }

        private static List<DeclaredSymbol> OverlayVba(List<DeclaredSymbol> disk, Dictionary<string, TextBuffer> openBuffers)
        {
            return Overlay(disk, openBuffers, false);
        }

        private static List<DeclaredSymbol> Overlay(List<DeclaredSymbol> disk, Dictionary<string, TextBuffer> openBuffers, bool csharp)
        {
            if (openBuffers == null || openBuffers.Count == 0)
            {
                return (disk == null) ? new List<DeclaredSymbol>() : disk;
            }

            List<DeclaredSymbol> result = new List<DeclaredSymbol>();
            if (disk != null)
            {
                for (int i = 0; i < disk.Count; i++)
                {
                    DeclaredSymbol item = disk[i];
                    if (item == null)
                    {
                        continue;
                    }

                    if (HasOpenPath(openBuffers, item.FilePath, csharp))
                    {
                        continue;
                    }

                    result.Add(item);
                }
            }

            foreach (KeyValuePair<string, TextBuffer> pair in openBuffers)
            {
                if (pair.Value == null || string.IsNullOrEmpty(pair.Key) || !MatchesOverlay(pair.Key, csharp))
                {
                    continue;
                }

                if (csharp)
                {
                    result.AddRange(CSharpSymbols.Collect(pair.Value, pair.Key, false));
                }
                else
                {
                    result.AddRange(CollectVba(pair.Value, pair.Key));
                }
            }

            return result;
        }

        private static bool HasOpenPath(Dictionary<string, TextBuffer> openBuffers, string path, bool csharp)
        {
            if (string.IsNullOrEmpty(path) || !MatchesOverlay(path, csharp))
            {
                return false;
            }

            foreach (KeyValuePair<string, TextBuffer> pair in openBuffers)
            {
                if (string.IsNullOrEmpty(pair.Key))
                {
                    continue;
                }

                try
                {
                    if (string.Equals(Path.GetFullPath(pair.Key), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                catch (Exception)
                {
                    if (string.Equals(pair.Key, path, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool MatchesOverlay(string path, bool csharp)
        {
            string ext = Path.GetExtension(path);
            if (csharp)
            {
                return string.Equals(ext, ".cs", StringComparison.OrdinalIgnoreCase);
            }

            return string.Equals(ext, ".bas", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ext, ".cls", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 1 バッファの VBA Sub / Function / Property。
        /// </summary>
        /// <param name="buffer">本文。</param>
        /// <param name="filePath">ディスクパス。無題は null。</param>
        /// <returns>出現順。</returns>
        public static List<DeclaredSymbol> CollectVba(TextBuffer buffer, string filePath)
        {
            List<DeclaredSymbol> result = new List<DeclaredSymbol>();
            if (buffer == null)
            {
                return result;
            }

            VbaLexer lexer = new VbaLexer();
            List<Token> tokens = new List<Token>();
            for (int line = 0; line < buffer.LineCount; line++)
            {
                string text = buffer.GetLine(line);
                tokens.Clear();
                int endState;
                lexer.ScanLine(text, 0, tokens, out endState);
                AddVbaFromLine(result, text, tokens, line, filePath);
            }

            return result;
        }

        /// <summary>
        /// ディスクバイトを読んでバッファにする。失敗は false。
        /// </summary>
        /// <param name="path">ファイル。</param>
        /// <param name="buffer">成功時の本文。</param>
        /// <returns>読めたら true。</returns>
        public static bool TryReadBuffer(string path, out TextBuffer buffer)
        {
            buffer = null;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return false;
            }

            byte[] data;
            try
            {
                data = File.ReadAllBytes(path);
            }
            catch (Exception)
            {
                return false;
            }

            string text;
            FileEncodingInfo info;
            string error;
            if (!FileEncoding.TryDecode(data, out text, out info, out error))
            {
                return false;
            }

            buffer = new TextBuffer();
            buffer.SetText(text);
            if (info != null)
            {
                buffer.NewLine = info.NewLine;
            }

            return true;
        }

        private static void CollectCsharpFiles(string[] files, List<DeclaredSymbol> list)
        {
            if (files == null)
            {
                return;
            }

            for (int i = 0; i < files.Length; i++)
            {
                TextBuffer buffer;
                if (!TryReadBuffer(files[i], out buffer))
                {
                    continue;
                }

                List<DeclaredSymbol> found = CSharpSymbols.Collect(buffer, files[i], false);
                list.AddRange(found);
            }
        }

        private static void CollectVbaFiles(string[] files, List<DeclaredSymbol> list)
        {
            if (files == null)
            {
                return;
            }

            for (int i = 0; i < files.Length; i++)
            {
                TextBuffer buffer;
                if (!TryReadBuffer(files[i], out buffer))
                {
                    continue;
                }

                List<DeclaredSymbol> found = CollectVba(buffer, files[i]);
                list.AddRange(found);
            }
        }

        private static void AddVbaFromLine(List<DeclaredSymbol> result, string line, List<Token> tokens, int lineIndex, string filePath)
        {
            for (int i = 0; i < tokens.Count; i++)
            {
                Token token = tokens[i];
                if (token.Kind != TokenKind.Keyword)
                {
                    continue;
                }

                string word = TokenText(line, token);
                if (!EqualsIgnoreCase(word, "Sub") &&
                    !EqualsIgnoreCase(word, "Function") &&
                    !EqualsIgnoreCase(word, "Property"))
                {
                    continue;
                }

                int n = NextNonSpace(tokens, line, i);
                if (n < 0)
                {
                    continue;
                }

                Token next = tokens[n];
                if (next.Kind == TokenKind.Keyword)
                {
                    string second = TokenText(line, next);
                    if (EqualsIgnoreCase(second, "Get") ||
                        EqualsIgnoreCase(second, "Set") ||
                        EqualsIgnoreCase(second, "Let"))
                    {
                        n = NextNonSpace(tokens, line, n);
                        if (n < 0)
                        {
                            continue;
                        }

                        next = tokens[n];
                    }
                }

                if (!IdentifierClassifier.LooksLikeIdentifier(LanguageKind.Vba, line, next))
                {
                    continue;
                }

                if (next.Kind == TokenKind.Keyword)
                {
                    continue;
                }

                string name = TokenText(line, next);
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                result.Add(new DeclaredSymbol(name, SymbolKind.Method, LanguageKind.Vba, filePath, lineIndex, next.Start, next.Length, null, word + " " + name));
            }
        }

        private static int NextNonSpace(List<Token> tokens, string line, int index)
        {
            for (int i = index + 1; i < tokens.Count; i++)
            {
                if (!IsSpaceToken(line, tokens[i]))
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool IsSpaceToken(string line, Token token)
        {
            if (token.Length <= 0 || token.Start < 0 || token.Start + token.Length > line.Length)
            {
                return false;
            }

            for (int i = 0; i < token.Length; i++)
            {
                char c = line[token.Start + i];
                if (c != ' ' && c != '\t')
                {
                    return false;
                }
            }

            return true;
        }

        private static string TokenText(string line, Token token)
        {
            if (token.Length <= 0 || token.Start < 0 || token.Start + token.Length > line.Length)
            {
                return "";
            }

            return line.Substring(token.Start, token.Length);
        }

        private static bool EqualsIgnoreCase(string a, string b)
        {
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }
    }
}
