using System;
using System.Collections.Generic;
using System.IO;
using WindowsIDE.Editor;

namespace WindowsIDE.Languages.CSharp
{
    /// <summary>
    /// ワークスペース配下の .cs から class/struct/interface/enum/delegate 名を集める。
    /// </summary>
    public static class WorkspaceTypeNames
    {
        private static string cachedRoot;
        private static HashSet<string> cachedNames;
        private static int cachedFileCount;
        private static long cachedStamp;
        private static readonly object Gate = new object();

        /// <summary>
        /// ルート配下の型単純名。root が空なら空集合。同一ルートでもファイル数／時刻が変われば再収集する。
        /// </summary>
        /// <param name="root">ワークスペースルート。</param>
        /// <returns>Ordinal の集合。</returns>
        public static HashSet<string> Get(string root)
        {
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
            {
                return new HashSet<string>(StringComparer.Ordinal);
            }

            string full = Path.GetFullPath(root);
            int count;
            long stamp;
            string[] files = ListCsFiles(full, out count, out stamp);
            lock (Gate)
            {
                if (cachedNames != null &&
                    string.Equals(cachedRoot, full, StringComparison.OrdinalIgnoreCase) &&
                    cachedFileCount == count &&
                    cachedStamp == stamp)
                {
                    return cachedNames;
                }

                HashSet<string> set = new HashSet<string>(StringComparer.Ordinal);
                Collect(files, set);
                cachedRoot = full;
                cachedNames = set;
                cachedFileCount = count;
                cachedStamp = stamp;
                return set;
            }
        }

        /// <summary>
        /// キャッシュを捨てる。保存やフォルダ再読込のあと次の Get で集め直す。
        /// </summary>
        public static void Invalidate()
        {
            lock (Gate)
            {
                cachedRoot = null;
                cachedNames = null;
                cachedFileCount = -1;
                cachedStamp = 0;
            }
        }

        private static string[] ListCsFiles(string root, out int count, out long stamp)
        {
            count = 0;
            stamp = 0;
            string[] files;
            try
            {
                files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
            }
            catch (Exception)
            {
                return new string[0];
            }

            List<string> kept = new List<string>();
            for (int i = 0; i < files.Length; i++)
            {
                string path = files[i];
                if (path.IndexOf("\\obj\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    path.IndexOf("\\bin\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    path.IndexOf("\\.git\\", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                kept.Add(path);
                count++;
                try
                {
                    long ticks = File.GetLastWriteTimeUtc(path).Ticks;
                    if (ticks > stamp)
                    {
                        stamp = ticks;
                    }
                }
                catch (Exception)
                {
                }
            }

            return kept.ToArray();
        }

        private static void Collect(string[] files, HashSet<string> set)
        {
            if (files == null)
            {
                return;
            }

            CSharpLexer lexer = new CSharpLexer();
            List<Token> tokens = new List<Token>();
            for (int f = 0; f < files.Length; f++)
            {
                string path = files[f];
                string text;
                try
                {
                    text = File.ReadAllText(path);
                }
                catch (Exception)
                {
                    continue;
                }

                TextBuffer buffer = new TextBuffer();
                buffer.SetText(text);
                int state = 0;
                for (int line = 0; line < buffer.LineCount; line++)
                {
                    tokens.Clear();
                    int endState;
                    lexer.ScanLine(buffer.GetLine(line), state, tokens, out endState);
                    state = endState;
                    AddTypesFromLine(buffer.GetLine(line), tokens, set);
                }
            }
        }

        private static void AddTypesFromLine(string line, List<Token> tokens, HashSet<string> set)
        {
            for (int i = 0; i < tokens.Count; i++)
            {
                Token token = tokens[i];
                if (token.Kind != TokenKind.Keyword)
                {
                    continue;
                }

                string word = line.Substring(token.Start, token.Length);
                if (word != "class" && word != "struct" && word != "interface" &&
                    word != "enum" && word != "delegate")
                {
                    continue;
                }

                int next = NextIdent(line, tokens, i + 1);
                if (next < 0)
                {
                    continue;
                }

                Token ident = tokens[next];
                if (ident.Kind != TokenKind.Text)
                {
                    continue;
                }

                set.Add(line.Substring(ident.Start, ident.Length));
            }
        }

        private static int NextIdent(string line, List<Token> tokens, int start)
        {
            for (int i = start; i < tokens.Count; i++)
            {
                Token token = tokens[i];
                if (token.Kind == TokenKind.Comment || token.Kind == TokenKind.String)
                {
                    continue;
                }

                if (token.Kind == TokenKind.Text && IsAllSpace(line, token))
                {
                    continue;
                }

                return i;
            }

            return -1;
        }

        private static bool IsAllSpace(string line, Token token)
        {
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
    }
}
