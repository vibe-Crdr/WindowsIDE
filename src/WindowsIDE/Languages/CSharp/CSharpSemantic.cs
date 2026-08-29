using System;
using System.Collections.Generic;
using System.Text;
using WindowsIDE.Editor;

namespace WindowsIDE.Languages.CSharp
{
    /// <summary>
    /// ハイライト用の C# 5 再帰下降。コード生成はしない。宣言収集も同じ下降。
    /// </summary>
    public sealed class CSharpSemantic
    {
        private List<CSharpTok> tokens;
        private int pos;
        private HashSet<string> types;
        private int fileTypeCount;
        private HashSet<string> methods;
        private HashSet<string> fields;
        private Scope scope;
        private List<ClassifySpan>[] overlay;
        private bool muteMarks;
        private int lineCount;
        private List<DeclaredSymbol> collected;
        private string collectPath;
        private bool includeLocals;
        private List<string> typeStack;

        private sealed class CSharpTok
        {
            public int Line;
            public int Start;
            public int Length;
            public TokenKind Kind;
            public string Text;
        }

        private sealed class Scope
        {
            public Scope Parent;
            public Dictionary<string, bool> Locals;

            public Scope(Scope parent)
            {
                this.Parent = parent;
                this.Locals = new Dictionary<string, bool>(StringComparer.Ordinal);
            }

            public bool Contains(string name)
            {
                Scope current = this;
                while (current != null)
                {
                    if (current.Locals.ContainsKey(name))
                    {
                        return true;
                    }

                    current = current.Parent;
                }

                return false;
            }

            public void Add(string name)
            {
                if (!string.IsNullOrEmpty(name) && !this.Locals.ContainsKey(name))
                {
                    this.Locals.Add(name, true);
                }
            }
        }

        /// <summary>
        /// バッファを束縛し、行ごとの識別子スパンを overlay に書く。
        /// </summary>
        /// <param name="buffer">本文。</param>
        /// <param name="workspaceRoot">ワークスペース。無くてよい。</param>
        /// <param name="overlay">行数分のリスト。呼び出し側が用意する。</param>
        public static void Classify(TextBuffer buffer, string workspaceRoot, List<ClassifySpan>[] overlay)
        {
            CSharpSemantic semantic = new CSharpSemantic();
            semantic.Run(buffer, workspaceRoot, overlay);
        }

        /// <summary>
        /// 同じ再帰下降から位置付き宣言を集める。ハイライトの Classify は変えない。
        /// </summary>
        /// <param name="buffer">本文。</param>
        /// <param name="filePath">ディスクパス。無題は null。</param>
        /// <param name="includeLocals">フィールドとローカルと仮引数も含める。</param>
        /// <returns>出現順の宣言。</returns>
        public static List<DeclaredSymbol> Collect(TextBuffer buffer, string filePath, bool includeLocals)
        {
            CSharpSemantic semantic = new CSharpSemantic();
            semantic.collected = new List<DeclaredSymbol>();
            semantic.collectPath = filePath;
            semantic.includeLocals = includeLocals;
            int lines = 1;
            if (buffer != null && buffer.LineCount > 0)
            {
                lines = buffer.LineCount;
            }

            List<ClassifySpan>[] overlay = NewOverlay(lines);
            semantic.Run(buffer, null, overlay);
            return semantic.collected;
        }

        /// <summary>
        /// テスト用。1 ファイル文字列を束縛し、指定位置の Kind を返す。
        /// </summary>
        /// <param name="text">ソース。</param>
        /// <param name="line">0 始まりの行。</param>
        /// <param name="index">行内インデックス。</param>
        /// <returns>識別子 Kind。無ければ Text。</returns>
        public static TokenKind KindAt(string text, int line, int index)
        {
            TextBuffer buffer = new TextBuffer();
            buffer.SetText(text == null ? "" : text);
            List<ClassifySpan>[] overlay = NewOverlay(buffer.LineCount);
            Classify(buffer, null, overlay);
            if (line < 0 || line >= overlay.Length)
            {
                return TokenKind.Text;
            }

            List<ClassifySpan> spans = overlay[line];
            for (int i = 0; i < spans.Count; i++)
            {
                ClassifySpan span = spans[i];
                if (index >= span.Start && index < span.Start + span.Length)
                {
                    return span.Kind;
                }
            }

            return TokenKind.Text;
        }

        private static List<ClassifySpan>[] NewOverlay(int lineCount)
        {
            if (lineCount < 1)
            {
                lineCount = 1;
            }

            List<ClassifySpan>[] overlay = new List<ClassifySpan>[lineCount];
            for (int i = 0; i < overlay.Length; i++)
            {
                overlay[i] = new List<ClassifySpan>();
            }

            return overlay;
        }

        private void Run(TextBuffer buffer, string workspaceRoot, List<ClassifySpan>[] overlay)
        {
            if (buffer == null || overlay == null)
            {
                return;
            }

            this.overlay = overlay;
            this.lineCount = buffer.LineCount;
            this.tokens = Tokenize(buffer);
            this.types = new HashSet<string>(StringComparer.Ordinal);
            this.fileTypeCount = 0;
            this.methods = new HashSet<string>(StringComparer.Ordinal);
            this.fields = new HashSet<string>(StringComparer.Ordinal);
            this.scope = new Scope(null);
            this.typeStack = new List<string>();
            AddKnownTypes(workspaceRoot);
            CollectDeclaredTypes();
            this.pos = 0;
            if (this.fileTypeCount == 0)
            {
                this.ParseStatementList();
            }
            else
            {
                this.ParseCompilationUnit();
            }
        }

        private void AddKnownTypes(string workspaceRoot)
        {
            HashSet<string> bcl = BclTypeCache.Names();
            foreach (string name in bcl)
            {
                this.types.Add(name);
            }

            HashSet<string> workspace = WorkspaceTypeNames.Get(workspaceRoot);
            foreach (string name in workspace)
            {
                this.types.Add(name);
            }
        }

        private static List<CSharpTok> Tokenize(TextBuffer buffer)
        {
            List<CSharpTok> list = new List<CSharpTok>();
            CSharpLexer lexer = new CSharpLexer();
            List<Token> lineTokens = new List<Token>();
            int state = 0;
            for (int line = 0; line < buffer.LineCount; line++)
            {
                string text = buffer.GetLine(line);
                lineTokens.Clear();
                int endState;
                lexer.ScanLine(text, state, lineTokens, out endState);
                state = endState;
                for (int i = 0; i < lineTokens.Count; i++)
                {
                    Token token = lineTokens[i];
                    if (token.Length <= 0)
                    {
                        continue;
                    }

                    if (IsWhitespace(text, token))
                    {
                        continue;
                    }

                    CSharpTok tok = new CSharpTok();
                    tok.Line = line;
                    tok.Start = token.Start;
                    tok.Length = token.Length;
                    tok.Kind = token.Kind;
                    tok.Text = text.Substring(token.Start, token.Length);
                    list.Add(tok);
                }
            }

            return list;
        }

        private static bool IsWhitespace(string line, Token token)
        {
            if (token.Kind != TokenKind.Text)
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

        private void CollectDeclaredTypes()
        {
            for (int i = 0; i < this.tokens.Count; i++)
            {
                CSharpTok tok = this.tokens[i];
                if (tok.Kind != TokenKind.Keyword)
                {
                    continue;
                }

                if (tok.Text != "class" && tok.Text != "struct" && tok.Text != "interface" &&
                    tok.Text != "enum" && tok.Text != "delegate")
                {
                    continue;
                }

                int n = i + 1;
                while (n < this.tokens.Count && this.tokens[n].Kind == TokenKind.Comment)
                {
                    n++;
                }

                if (n < this.tokens.Count && IsIdentToken(this.tokens[n]))
                {
                    this.types.Add(this.tokens[n].Text);
                    this.fileTypeCount++;
                }
            }
        }

        private bool IsIdentToken(CSharpTok tok)
        {
            if (tok == null || tok.Kind == TokenKind.Keyword || tok.Kind == TokenKind.String ||
                tok.Kind == TokenKind.Comment || tok.Kind == TokenKind.Number)
            {
                return false;
            }

            string text = tok.Text;
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            int i = 0;
            if (text[0] == '@' && text.Length > 1)
            {
                i = 1;
            }

            if (!ScanChars.IsIdentStart(text[i]))
            {
                return false;
            }

            i++;
            while (i < text.Length)
            {
                if (!ScanChars.IsIdentPart(text[i]))
                {
                    return false;
                }

                i++;
            }

            return true;
        }

        private CSharpTok Peek()
        {
            this.SkipComments();
            if (this.pos >= this.tokens.Count)
            {
                return null;
            }

            return this.tokens[this.pos];
        }

        private CSharpTok PeekRaw()
        {
            if (this.pos >= this.tokens.Count)
            {
                return null;
            }

            return this.tokens[this.pos];
        }

        private void SkipComments()
        {
            while (this.pos < this.tokens.Count && this.tokens[this.pos].Kind == TokenKind.Comment)
            {
                this.pos++;
            }
        }

        private CSharpTok Take()
        {
            this.SkipComments();
            if (this.pos >= this.tokens.Count)
            {
                return null;
            }

            CSharpTok tok = this.tokens[this.pos];
            this.pos++;
            return tok;
        }

        private bool MatchKeyword(string word)
        {
            CSharpTok tok = this.Peek();
            if (tok != null && tok.Kind == TokenKind.Keyword && tok.Text == word)
            {
                this.Take();
                return true;
            }

            return false;
        }

        private bool MatchIdentWord(string word)
        {
            CSharpTok tok = this.Peek();
            if (tok != null && this.IsIdentToken(tok) && tok.Text == word)
            {
                this.Take();
                return true;
            }

            return false;
        }

        private bool MatchAccessor(string word)
        {
            return this.MatchKeyword(word) || this.MatchIdentWord(word);
        }

        private void PushScope()
        {
            this.scope = new Scope(this.scope);
        }

        private void PopScope()
        {
            if (this.scope != null && this.scope.Parent != null)
            {
                this.scope = this.scope.Parent;
            }
        }

        private bool MatchChar(char c)
        {
            CSharpTok tok = this.Peek();
            if (tok != null && tok.Kind == TokenKind.Text && tok.Length == 1 && tok.Text[0] == c)
            {
                this.Take();
                return true;
            }

            return false;
        }

        private bool PeekChar(char c)
        {
            CSharpTok tok = this.Peek();
            return tok != null && tok.Kind == TokenKind.Text && tok.Length == 1 && tok.Text[0] == c;
        }

        private bool PeekKeyword(string word)
        {
            CSharpTok tok = this.Peek();
            return tok != null && tok.Kind == TokenKind.Keyword && tok.Text == word;
        }

        private bool PeekIdent()
        {
            CSharpTok tok = this.Peek();
            return tok != null && this.IsIdentToken(tok);
        }

        private void RecoverStatement()
        {
            int guard = 0;
            while (this.Peek() != null && guard < 10000)
            {
                guard++;
                if (this.MatchChar(';') || this.PeekChar('{') || this.PeekChar('}'))
                {
                    return;
                }

                this.Take();
            }
        }

        private void Mark(CSharpTok tok, TokenKind kind)
        {
            if (this.muteMarks || tok == null || tok.Line < 0 || tok.Line >= this.overlay.Length)
            {
                return;
            }

            this.overlay[tok.Line].Add(new ClassifySpan(tok.Start, tok.Length, kind));
        }

        private void AddDecl(CSharpTok tok, SymbolKind kind, string containing, string signature)
        {
            if (this.collected == null || tok == null)
            {
                return;
            }

            if (!this.includeLocals && (kind == SymbolKind.Local || kind == SymbolKind.Field))
            {
                return;
            }

            string name = this.IdentName(tok);
            if (string.IsNullOrEmpty(name))
            {
                return;
            }

            this.collected.Add(new DeclaredSymbol(
                name,
                kind,
                LanguageKind.CSharp,
                this.collectPath,
                tok.Line,
                tok.Start,
                tok.Length,
                containing,
                signature));
        }

        private string IdentName(CSharpTok tok)
        {
            if (tok == null || string.IsNullOrEmpty(tok.Text))
            {
                return "";
            }

            if (tok.Text.Length >= 2 && tok.Text[0] == '@')
            {
                return tok.Text.Substring(1);
            }

            return tok.Text;
        }

        private string CurrentType()
        {
            if (this.typeStack == null || this.typeStack.Count == 0)
            {
                return null;
            }

            return this.typeStack[this.typeStack.Count - 1];
        }

        private void PushType(string name)
        {
            if (this.typeStack == null)
            {
                this.typeStack = new List<string>();
            }

            this.typeStack.Add(name == null ? "" : name);
        }

        private void PopType()
        {
            if (this.typeStack != null && this.typeStack.Count > 0)
            {
                this.typeStack.RemoveAt(this.typeStack.Count - 1);
            }
        }

        private string CompactSlice(int from, int toExclusive)
        {
            if (this.tokens == null || from < 0)
            {
                return "";
            }

            if (toExclusive > this.tokens.Count)
            {
                toExclusive = this.tokens.Count;
            }

            StringBuilder sb = new StringBuilder();
            CSharpTok prev = null;
            int i = from;
            while (i < toExclusive)
            {
                CSharpTok tok = this.tokens[i];
                i++;
                if (tok == null || tok.Kind == TokenKind.Comment)
                {
                    continue;
                }

                if (sb.Length > 0 && this.NeedsSpace(prev, tok))
                {
                    sb.Append(' ');
                }

                sb.Append(tok.Text);
                prev = tok;
            }

            return sb.ToString();
        }

        private bool NeedsSpace(CSharpTok left, CSharpTok right)
        {
            if (left == null || right == null)
            {
                return false;
            }

            if (this.IsSingle(right, '(') || this.IsSingle(right, '[') || this.IsSingle(right, '<') ||
                this.IsSingle(right, ')') || this.IsSingle(right, ']') || this.IsSingle(right, '>') ||
                this.IsSingle(right, ',') || this.IsSingle(right, '.') || this.IsSingle(right, ';') ||
                this.IsSingle(right, '?') || this.IsSingle(right, '*'))
            {
                return false;
            }

            if (this.IsSingle(left, '(') || this.IsSingle(left, '[') || this.IsSingle(left, '<') ||
                this.IsSingle(left, '.') || this.IsSingle(left, '~'))
            {
                return false;
            }

            return true;
        }

        private bool IsSingle(CSharpTok tok, char c)
        {
            return tok != null && tok.Text != null && tok.Text.Length == 1 && tok.Text[0] == c;
        }

        private void ParseStatementList()
        {
            int guard = 0;
            while (this.Peek() != null && guard < 200000)
            {
                guard++;
                if (this.PeekChar('}'))
                {
                    this.Take();
                    continue;
                }

                if (this.LooksLikeMethod())
                {
                    this.ParseLocalMethod();
                    continue;
                }

                this.ParseStatement();
            }
        }

        private bool LooksLikeMethod()
        {
            int saved = this.pos;
            this.muteMarks = true;
            this.SkipModifiers();
            bool ok = false;
            if (this.PeekBuiltinType() || this.PeekIdent())
            {
                this.ParseType();
                if (this.PeekIdent())
                {
                    this.Take();
                    this.SkipGenerics();
                    ok = this.PeekChar('(');
                }
            }

            this.pos = saved;
            this.muteMarks = false;
            return ok;
        }

        private void ParseLocalMethod()
        {
            this.SkipModifiers();
            int typeStart = this.pos;
            this.ParseType();
            if (this.PeekIdent())
            {
                CSharpTok name = this.Take();
                this.methods.Add(this.IdentName(name));
                this.Mark(name, TokenKind.Method);
                this.SkipGenerics();
                if (this.MatchChar('('))
                {
                    this.ParseParameterListAndBody(name, SymbolKind.Method, typeStart);
                    return;
                }
            }

            this.ParseMethodRest();
        }

        private void ParseCompilationUnit()
        {
            int guard = 0;
            while (this.Peek() != null && guard < 200000)
            {
                guard++;
                this.SkipAttributes();
                this.SkipModifiers();
                if (this.MatchKeyword("namespace"))
                {
                    this.ParseNamespace();
                    continue;
                }

                if (this.MatchKeyword("using"))
                {
                    this.ParseUsing();
                    continue;
                }

                if (this.PeekTypeIntro())
                {
                    this.ParseTypeDecl();
                    continue;
                }

                if (this.PeekChar('}'))
                {
                    this.Take();
                    continue;
                }

                this.ParseMemberOrStatement();
            }
        }

        private void ParseNamespace()
        {
            this.ParseDottedName(false);
            if (this.MatchChar(';'))
            {
                return;
            }

            if (this.MatchChar('{'))
            {
                this.ParseCompilationUnitUntilBrace();
            }
        }

        private void ParseCompilationUnitUntilBrace()
        {
            int guard = 0;
            while (this.Peek() != null && !this.PeekChar('}') && guard < 200000)
            {
                guard++;
                this.SkipAttributes();
                this.SkipModifiers();
                if (this.MatchKeyword("namespace"))
                {
                    this.ParseNamespace();
                    continue;
                }

                if (this.MatchKeyword("using"))
                {
                    this.ParseUsing();
                    continue;
                }

                if (this.PeekTypeIntro())
                {
                    this.ParseTypeDecl();
                    continue;
                }

                this.ParseMemberOrStatement();
            }

            this.MatchChar('}');
        }

        private void ParseUsing()
        {
            this.MatchKeyword("static");
            this.ParseDottedName(false);
            if (this.MatchChar('='))
            {
                this.ParseType();
            }

            this.MatchChar(';');
        }

        private bool PeekTypeIntro()
        {
            return this.PeekKeyword("class") || this.PeekKeyword("struct") ||
                this.PeekKeyword("interface") || this.PeekKeyword("enum") ||
                this.PeekKeyword("delegate");
        }

        private void ParseTypeDecl()
        {
            bool isDelegate = this.PeekKeyword("delegate");
            bool isEnum = this.PeekKeyword("enum");
            CSharpTok intro = this.Peek();
            string introWord = (intro == null) ? "type" : intro.Text;
            this.Take();
            CSharpTok name = null;
            if (this.PeekIdent())
            {
                name = this.Take();
                string ident = this.IdentName(name);
                this.types.Add(ident);
                this.Mark(name, TokenKind.Type);
                this.AddDecl(name, SymbolKind.Type, this.CurrentType(), introWord + " " + ident);
                this.PushType(ident);
            }

            try
            {
                this.SkipGenerics();
                if (this.MatchChar(':'))
                {
                    this.ParseBaseList();
                }

                this.SkipWhereClauses();
                if (isDelegate)
                {
                    this.PushScope();
                    this.MatchChar('(');
                    this.ParseParameterList();
                    this.MatchChar(')');
                    this.PopScope();
                    this.MatchChar(';');
                    return;
                }

                if (!this.MatchChar('{'))
                {
                    this.RecoverStatement();
                    return;
                }

                if (isEnum)
                {
                    this.ParseEnumBody();
                    this.MatchChar('}');
                    return;
                }

                HashSet<string> savedMethods = this.methods;
                HashSet<string> savedFields = this.fields;
                this.methods = new HashSet<string>(StringComparer.Ordinal);
                this.fields = new HashSet<string>(StringComparer.Ordinal);
                while (this.Peek() != null && !this.PeekChar('}'))
                {
                    this.ParseMember();
                }

                this.MatchChar('}');
                this.methods = savedMethods;
                this.fields = savedFields;
            }
            finally
            {
                if (name != null)
                {
                    this.PopType();
                }
            }
        }

        private void ParseEnumBody()
        {
            while (this.Peek() != null && !this.PeekChar('}'))
            {
                this.SkipAttributes();
                if (this.PeekIdent())
                {
                    CSharpTok member = this.Take();
                    this.Mark(member, TokenKind.Instance);
                    this.AddDecl(member, SymbolKind.Field, this.CurrentType(), this.IdentName(member));
                }

                if (this.MatchChar('='))
                {
                    this.ParseExpression();
                }

                if (!this.MatchChar(','))
                {
                    break;
                }
            }
        }

        private void ParseMember()
        {
            this.SkipAttributes();
            this.SkipModifiers();
            if (this.PeekTypeIntro())
            {
                this.ParseTypeDecl();
                return;
            }

            if (this.PeekChar('}'))
            {
                return;
            }

            if (this.MatchChar('~'))
            {
                int sigStart = this.pos - 1;
                if (this.PeekIdent())
                {
                    CSharpTok dtor = this.Take();
                    this.Mark(dtor, TokenKind.Method);
                    this.SkipGenerics();
                    if (this.MatchChar('('))
                    {
                        this.ParseParameterListAndBody(dtor, SymbolKind.Method, sigStart);
                        return;
                    }
                }

                this.ParseMethodRest();
                return;
            }

            if (this.MatchKeyword("event"))
            {
                this.ParseType();
                if (this.PeekIdent())
                {
                    CSharpTok name = this.Take();
                    this.fields.Add(this.IdentName(name));
                    this.Mark(name, TokenKind.Instance);
                    this.AddDecl(name, SymbolKind.Field, this.CurrentType(), "event " + this.IdentName(name));
                }

                if (this.MatchChar('{'))
                {
                    this.ParseAccessors();
                }
                else
                {
                    this.MatchChar(';');
                }

                return;
            }

            int start = this.pos;
            CSharpTok ctorPeek = this.Peek();
            this.ParseType();
            if (this.MatchChar('('))
            {
                if (ctorPeek != null)
                {
                    string ctorName = this.IdentName(ctorPeek);
                    this.methods.Add(ctorName);
                    this.Mark(ctorPeek, TokenKind.Method);
                }

                this.ParseParameterListAndBody(ctorPeek, SymbolKind.Constructor, start);
                return;
            }

            if (!this.PeekIdent())
            {
                if (this.MatchChar('{'))
                {
                    this.SkipBraces();
                }
                else
                {
                    this.RecoverMember();
                }

                return;
            }

            CSharpTok ident = this.Take();
            this.SkipGenerics();
            if (this.MatchChar('('))
            {
                this.methods.Add(this.IdentName(ident));
                this.Mark(ident, TokenKind.Method);
                this.ParseParameterListAndBody(ident, SymbolKind.Method, start);
                return;
            }

            if (this.MatchChar('{'))
            {
                this.fields.Add(this.IdentName(ident));
                this.Mark(ident, TokenKind.Instance);
                this.AddDecl(ident, SymbolKind.Property, this.CurrentType(), this.CompactSlice(start, this.pos - 1));
                this.ParseAccessors();
                return;
            }

            if (this.MatchChar('['))
            {
                this.ParseExpression();
                this.MatchChar(']');
                if (this.MatchChar('{'))
                {
                    this.Mark(ident, TokenKind.Instance);
                    this.AddDecl(ident, SymbolKind.Property, this.CurrentType(), this.IdentName(ident));
                    this.ParseAccessors();
                    return;
                }
            }

            this.fields.Add(this.IdentName(ident));
            this.Mark(ident, TokenKind.Instance);
            this.AddDecl(ident, SymbolKind.Field, this.CurrentType(), this.CompactSlice(start, this.pos));
            if (this.MatchChar('='))
            {
                this.ParseExpression();
            }

            while (this.MatchChar(','))
            {
                if (this.PeekIdent())
                {
                    CSharpTok more = this.Take();
                    this.fields.Add(this.IdentName(more));
                    this.Mark(more, TokenKind.Instance);
                    this.AddDecl(more, SymbolKind.Field, this.CurrentType(), this.IdentName(more));
                    if (this.MatchChar('='))
                    {
                        this.ParseExpression();
                    }
                }
            }

            this.MatchChar(';');
            if (start == this.pos)
            {
                this.RecoverMember();
            }
        }

        private void ParseMethodRest()
        {
            this.MatchChar('(');
            this.ParseParameterListAndBody();
        }

        private void ParseParameterListAndBody()
        {
            this.ParseParameterListAndBody(null, SymbolKind.Method, -1);
        }

        private void ParseParameterListAndBody(CSharpTok name, SymbolKind kind, int sigStart)
        {
            this.PushScope();
            this.ParseParameterList();
            this.MatchChar(')');
            if (name != null && sigStart >= 0)
            {
                string sig = this.CompactSlice(sigStart, this.pos);
                if (kind == SymbolKind.Constructor && !string.IsNullOrEmpty(this.CurrentType()) &&
                    sig.IndexOf('.') < 0)
                {
                    sig = this.CurrentType() + "." + sig;
                }

                this.AddDecl(name, kind, this.CurrentType(), sig);
            }

            this.SkipWhereClauses();
            this.ParseMethodBodyOrSemi();
            this.PopScope();
        }

        private void ParseMethodBodyOrSemi()
        {
            if (this.MatchChar(';'))
            {
                return;
            }

            if (this.MatchKeyword("where"))
            {
                this.SkipWhereClauses();
            }

            if (this.MatchChar('{'))
            {
                this.ParseBlockContents();
                this.MatchChar('}');
            }
        }

        private void ParseAccessors()
        {
            while (this.Peek() != null && !this.PeekChar('}'))
            {
                this.SkipAttributes();
                this.SkipModifiers();
                if (this.MatchAccessor("get") || this.MatchAccessor("set") || this.MatchAccessor("add") || this.MatchAccessor("remove"))
                {
                    if (this.MatchChar('{'))
                    {
                        this.ParseBlockContents();
                        this.MatchChar('}');
                    }
                    else
                    {
                        this.MatchChar(';');
                    }

                    continue;
                }

                if (this.PeekChar('}'))
                {
                    break;
                }

                this.Take();
            }

            this.MatchChar('}');
        }

        private void ParseParameterList()
        {
            while (this.Peek() != null && !this.PeekChar(')'))
            {
                this.SkipAttributes();
                this.MatchKeyword("this");
                this.MatchKeyword("params");
                this.MatchKeyword("ref");
                this.MatchKeyword("out");
                this.MatchKeyword("in");
                this.ParseType();
                if (this.PeekIdent())
                {
                    CSharpTok name = this.Take();
                    this.scope.Add(this.IdentName(name));
                    this.Mark(name, TokenKind.Local);
                    this.AddDecl(name, SymbolKind.Local, this.CurrentType(), this.IdentName(name));
                }

                if (this.MatchChar('='))
                {
                    this.ParseExpression();
                }

                if (!this.MatchChar(','))
                {
                    break;
                }
            }
        }

        private void ParseBlockContents()
        {
            Scope saved = this.scope;
            this.scope = new Scope(saved);
            int guard = 0;
            while (this.Peek() != null && !this.PeekChar('}') && guard < 200000)
            {
                guard++;
                this.ParseStatement();
            }

            this.scope = saved;
        }

        private void ParseStatement()
        {
            this.SkipAttributes();
            if (this.MatchChar(';'))
            {
                return;
            }

            if (this.MatchChar('{'))
            {
                this.ParseBlockContents();
                this.MatchChar('}');
                return;
            }

            if (this.MatchKeyword("if"))
            {
                this.ParseParenExpr();
                this.ParseStatement();
                if (this.MatchKeyword("else"))
                {
                    this.ParseStatement();
                }

                return;
            }

            if (this.MatchKeyword("while") || this.MatchKeyword("switch") || this.MatchKeyword("lock") ||
                this.MatchKeyword("using") || this.MatchKeyword("fixed"))
            {
                this.ParseParenExprOrDecl();
                this.ParseStatement();
                return;
            }

            if (this.MatchKeyword("for"))
            {
                this.MatchChar('(');
                if (!this.PeekChar(';'))
                {
                    if (this.LooksLikeDeclaration())
                    {
                        this.ParseDeclaration(false);
                    }
                    else
                    {
                        this.ParseExpression();
                    }
                }

                this.MatchChar(';');
                if (!this.PeekChar(';'))
                {
                    this.ParseExpression();
                }

                this.MatchChar(';');
                if (!this.PeekChar(')'))
                {
                    this.ParseExpression();
                }

                this.MatchChar(')');
                this.ParseStatement();
                return;
            }

            if (this.MatchKeyword("foreach"))
            {
                this.PushScope();
                this.MatchChar('(');
                this.ParseType();
                if (this.PeekIdent())
                {
                    CSharpTok name = this.Take();
                    this.scope.Add(this.IdentName(name));
                    this.Mark(name, TokenKind.Local);
                    this.AddDecl(name, SymbolKind.Local, this.CurrentType(), this.IdentName(name));
                }

                this.MatchKeyword("in");
                this.ParseExpression();
                this.MatchChar(')');
                this.ParseStatement();
                this.PopScope();
                return;
            }

            if (this.MatchKeyword("do"))
            {
                this.ParseStatement();
                this.MatchKeyword("while");
                this.ParseParenExpr();
                this.MatchChar(';');
                return;
            }

            if (this.MatchKeyword("try"))
            {
                if (this.MatchChar('{'))
                {
                    this.ParseBlockContents();
                    this.MatchChar('}');
                }

                while (this.MatchKeyword("catch"))
                {
                    this.PushScope();
                    if (this.MatchChar('('))
                    {
                        this.ParseType();
                        if (this.PeekIdent())
                        {
                            CSharpTok name = this.Take();
                            this.scope.Add(this.IdentName(name));
                            this.Mark(name, TokenKind.Local);
                            this.AddDecl(name, SymbolKind.Local, this.CurrentType(), this.IdentName(name));
                        }

                        this.MatchChar(')');
                    }

                    if (this.MatchChar('{'))
                    {
                        this.ParseBlockContents();
                        this.MatchChar('}');
                    }

                    this.PopScope();
                }

                if (this.MatchKeyword("finally"))
                {
                    if (this.MatchChar('{'))
                    {
                        this.ParseBlockContents();
                        this.MatchChar('}');
                    }
                }

                return;
            }

            if (this.MatchKeyword("return") || this.MatchKeyword("throw") || this.MatchKeyword("yield"))
            {
                this.MatchKeyword("return");
                this.MatchKeyword("break");
                if (!this.PeekChar(';'))
                {
                    this.ParseExpression();
                }

                this.MatchChar(';');
                return;
            }

            if (this.MatchKeyword("break") || this.MatchKeyword("continue"))
            {
                this.MatchChar(';');
                return;
            }

            if (this.MatchKeyword("goto"))
            {
                if (this.PeekIdent())
                {
                    this.Mark(this.Take(), TokenKind.Local);
                }

                this.MatchChar(';');
                return;
            }

            if (this.MatchKeyword("case"))
            {
                this.ParseExpression();
                this.MatchChar(':');
                return;
            }

            if (this.MatchKeyword("default"))
            {
                this.MatchChar(':');
                return;
            }

            if (this.PeekIdent())
            {
                int saved = this.pos;
                CSharpTok ident = this.Take();
                if (this.MatchChar(':'))
                {
                    return;
                }

                this.pos = saved;
            }

            if (this.LooksLikeDeclaration())
            {
                this.ParseDeclaration(true);
                return;
            }

            this.ParseExpression();
            this.MatchChar(';');
        }

        private void ParseMemberOrStatement()
        {
            if (this.PeekTypeIntro())
            {
                this.ParseTypeDecl();
                return;
            }

            this.ParseMember();
        }

        private void ParseDeclaration(bool needSemi)
        {
            this.MatchKeyword("const");
            this.ParseType();
            this.ParseDeclarators();
            if (needSemi)
            {
                this.MatchChar(';');
            }
        }

        private void ParseDeclarators()
        {
            while (true)
            {
                if (this.PeekIdent())
                {
                    CSharpTok name = this.Take();
                    this.scope.Add(this.IdentName(name));
                    this.Mark(name, TokenKind.Local);
                    this.AddDecl(name, SymbolKind.Local, this.CurrentType(), this.IdentName(name));
                }

                if (this.MatchChar('='))
                {
                    this.ParseExpression();
                }

                if (!this.MatchChar(','))
                {
                    break;
                }
            }
        }

        private bool LooksLikeDeclaration()
        {
            int saved = this.pos;
            this.muteMarks = true;
            bool result = this.TryLooksLikeDeclaration();
            this.pos = saved;
            this.muteMarks = false;
            return result;
        }

        private bool TryLooksLikeDeclaration()
        {
            this.MatchKeyword("const");
            this.MatchKeyword("var");
            if (this.PeekBuiltinType())
            {
                this.Take();
            }
            else if (this.PeekIdent())
            {
                this.ParseType();
            }
            else
            {
                return false;
            }

            this.SkipPointerArrayNullable();
            if (!this.PeekIdent())
            {
                return false;
            }

            int after = this.pos;
            this.Take();
            bool paren = this.PeekChar('(');
            this.pos = after;
            return !paren;
        }

        private bool PeekBuiltinType()
        {
            CSharpTok tok = this.Peek();
            if (tok == null || tok.Kind != TokenKind.Keyword)
            {
                return false;
            }

            string w = tok.Text;
            return w == "bool" || w == "byte" || w == "char" || w == "decimal" || w == "double" ||
                w == "float" || w == "int" || w == "long" || w == "object" || w == "sbyte" ||
                w == "short" || w == "string" || w == "uint" || w == "ulong" || w == "ushort" ||
                w == "void" || w == "var" || w == "dynamic";
        }

        private void ParseType()
        {
            this.MatchKeyword("ref");
            this.MatchKeyword("out");
            this.MatchKeyword("in");
            if (this.PeekBuiltinType())
            {
                this.Take();
            }
            else
            {
                this.ParseDottedName(true);
            }

            this.SkipGenerics();
            this.SkipPointerArrayNullable();
        }

        private void ParseDottedName(bool typePosition)
        {
            if (!this.PeekIdent() && !this.PeekKeyword("global"))
            {
                return;
            }

            if (this.MatchKeyword("global"))
            {
                this.MatchChar(':');
                this.MatchChar(':');
            }

            if (this.PeekIdent())
            {
                CSharpTok ident = this.Take();
                this.ClassifyName(ident, typePosition, this.PeekChar('.'), this.PeekChar('('));
            }

            while (this.MatchChar('.'))
            {
                if (this.PeekIdent())
                {
                    CSharpTok ident = this.Take();
                    bool paren = this.PeekChar('(');
                    if (typePosition)
                    {
                        this.Mark(ident, TokenKind.Type);
                    }
                    else if (paren)
                    {
                        this.Mark(ident, TokenKind.Method);
                    }
                    else if (this.PeekChar('.'))
                    {
                        if (this.IsTypeName(ident.Text))
                        {
                            this.Mark(ident, TokenKind.Type);
                        }
                        else
                        {
                            this.Mark(ident, TokenKind.Instance);
                        }
                    }
                    else
                    {
                        this.Mark(ident, TokenKind.Instance);
                    }
                }

                this.SkipGenerics();
            }

            this.SkipGenerics();
        }

        private void ClassifyName(CSharpTok ident, bool typePosition, bool followedByDot, bool followedByParen)
        {
            string name = ident.Text;
            if (name.Length > 0 && name[0] == '@')
            {
                name = name.Substring(1);
            }

            if (typePosition)
            {
                this.Mark(ident, TokenKind.Type);
                return;
            }

            if (followedByDot && this.IsTypeName(name))
            {
                this.Mark(ident, TokenKind.Type);
                return;
            }

            if (this.scope.Contains(name))
            {
                this.Mark(ident, TokenKind.Local);
                return;
            }

            if (followedByParen)
            {
                if (this.methods.Contains(name) || !this.fields.Contains(name))
                {
                    this.Mark(ident, TokenKind.Method);
                    return;
                }
            }

            if (this.fields.Contains(name))
            {
                this.Mark(ident, TokenKind.Instance);
                return;
            }

            if (this.IsTypeName(name))
            {
                this.Mark(ident, TokenKind.Type);
                return;
            }

            if (followedByParen)
            {
                this.Mark(ident, TokenKind.Method);
                return;
            }

            this.Mark(ident, TokenKind.Local);
        }

        private bool IsTypeName(string name)
        {
            return this.types.Contains(name) || BclTypeCache.Contains(name);
        }

        private void ParseExpression()
        {
            this.ParseAssign();
        }

        private void ParseAssign()
        {
            this.ParseOr();
            CSharpTok tok = this.Peek();
            if (tok != null && tok.Kind == TokenKind.Text && tok.Length == 1)
            {
                char c = tok.Text[0];
                if (c == '=' || c == '+' || c == '-' || c == '*' || c == '/' || c == '%' ||
                    c == '&' || c == '|' || c == '^' || c == '<' || c == '>')
                {
                    this.Take();
                    if (c != '=' && this.MatchChar('='))
                    {
                    }

                    this.ParseAssign();
                }
            }
        }

        private void ParseOr()
        {
            this.ParseAnd();
            while (true)
            {
                if (this.PeekChar('|'))
                {
                    this.Take();
                    this.MatchChar('|');
                    this.ParseAnd();
                    continue;
                }

                break;
            }
        }

        private void ParseAnd()
        {
            this.ParseEquality();
            while (this.PeekChar('&'))
            {
                this.Take();
                this.MatchChar('&');
                this.ParseEquality();
            }
        }

        private void ParseEquality()
        {
            this.ParseRel();
            while (true)
            {
                if (this.PeekChar('=') || this.PeekChar('!'))
                {
                    int saved = this.pos;
                    this.Take();
                    if (this.MatchChar('='))
                    {
                        this.ParseRel();
                        continue;
                    }

                    this.pos = saved;
                }

                break;
            }
        }

        private void ParseRel()
        {
            this.ParseShift();
            while (this.PeekChar('<') || this.PeekChar('>') || this.PeekKeyword("is") || this.PeekKeyword("as"))
            {
                if (this.PeekKeyword("is") || this.PeekKeyword("as"))
                {
                    this.Take();
                    this.ParseType();
                    continue;
                }

                this.Take();
                this.MatchChar('=');
                this.ParseShift();
            }
        }

        private void ParseShift()
        {
            this.ParseAdd();
            while (this.PeekChar('<') || this.PeekChar('>'))
            {
                int saved = this.pos;
                this.Take();
                if ((this.tokens[saved].Text == "<" && this.MatchChar('<')) ||
                    (this.tokens[saved].Text == ">" && this.MatchChar('>')))
                {
                    this.ParseAdd();
                    continue;
                }

                this.pos = saved;
                break;
            }
        }

        private void ParseAdd()
        {
            this.ParseMul();
            while (this.PeekChar('+') || this.PeekChar('-'))
            {
                this.Take();
                this.ParseMul();
            }
        }

        private void ParseMul()
        {
            this.ParseUnary();
            while (this.PeekChar('*') || this.PeekChar('/') || this.PeekChar('%'))
            {
                this.Take();
                this.ParseUnary();
            }
        }

        private void ParseUnary()
        {
            if (this.PeekChar('+') || this.PeekChar('-') || this.PeekChar('!') || this.PeekChar('~') ||
                this.PeekChar('&') || this.PeekChar('*'))
            {
                this.Take();
                this.ParseUnary();
                return;
            }

            if (this.MatchKeyword("new"))
            {
                this.ParseType();
                if (this.MatchChar('('))
                {
                    this.ParseArgList();
                    this.MatchChar(')');
                }

                if (this.MatchChar('{'))
                {
                    this.ParseInitializer();
                    this.MatchChar('}');
                }

                if (this.MatchChar('['))
                {
                    this.ParseArgList();
                    this.MatchChar(']');
                    this.SkipPointerArrayNullable();
                    if (this.MatchChar('{'))
                    {
                        this.ParseInitializer();
                        this.MatchChar('}');
                    }
                }

                this.ParsePostfix();
                return;
            }

            if (this.MatchKeyword("typeof") || this.MatchKeyword("sizeof") || this.MatchKeyword("default"))
            {
                if (this.MatchChar('('))
                {
                    this.ParseType();
                    this.MatchChar(')');
                }

                this.ParsePostfix();
                return;
            }

            if (this.MatchChar('('))
            {
                int saved = this.pos;
                this.muteMarks = true;
                bool cast = this.LooksLikeDeclaration() || this.PeekBuiltinType() || (this.PeekIdent() && this.IsTypeName(this.Peek().Text));
                this.muteMarks = false;
                if (cast)
                {
                    this.ParseType();
                    if (this.MatchChar(')'))
                    {
                        this.ParseUnary();
                        return;
                    }
                }

                this.pos = saved;
                this.ParseExpression();
                this.MatchChar(')');
                this.ParsePostfix();
                return;
            }

            this.ParsePrimary();
            this.ParsePostfix();
        }

        private void ParsePrimary()
        {
            if (this.MatchKeyword("this") || this.MatchKeyword("base") || this.MatchKeyword("true") ||
                this.MatchKeyword("false") || this.MatchKeyword("null"))
            {
                return;
            }

            CSharpTok tok = this.Peek();
            if (tok == null)
            {
                return;
            }

            if (tok.Kind == TokenKind.Number || tok.Kind == TokenKind.String)
            {
                this.Take();
                return;
            }

            if (this.PeekIdent())
            {
                bool paren = false;
                bool dot = false;
                if (this.pos + 1 < this.tokens.Count)
                {
                    int look = this.pos + 1;
                    while (look < this.tokens.Count && this.tokens[look].Kind == TokenKind.Comment)
                    {
                        look++;
                    }

                    if (look < this.tokens.Count)
                    {
                        CSharpTok next = this.tokens[look];
                        paren = next.Kind == TokenKind.Text && next.Length == 1 && next.Text[0] == '(';
                        dot = next.Kind == TokenKind.Text && next.Length == 1 && next.Text[0] == '.';
                    }
                }

                CSharpTok ident = this.Take();
                this.ClassifyName(ident, false, dot, paren);
                this.SkipGenerics();
                return;
            }

            this.Take();
        }

        private void ParsePostfix()
        {
            while (true)
            {
                if (this.MatchChar('.'))
                {
                    if (this.PeekIdent())
                    {
                        CSharpTok ident = this.Take();
                        bool paren = this.PeekChar('(');
                        if (paren)
                        {
                            this.Mark(ident, TokenKind.Method);
                        }
                        else
                        {
                            this.Mark(ident, TokenKind.Instance);
                        }

                        this.SkipGenerics();
                    }

                    continue;
                }

                if (this.MatchChar('('))
                {
                    this.ParseArgList();
                    this.MatchChar(')');
                    continue;
                }

                if (this.MatchChar('['))
                {
                    this.ParseArgList();
                    this.MatchChar(']');
                    continue;
                }

                if (this.MatchChar('+'))
                {
                    this.MatchChar('+');
                    continue;
                }

                if (this.MatchChar('-'))
                {
                    this.MatchChar('-');
                    continue;
                }

                break;
            }
        }

        private void ParseArgList()
        {
            if (this.PeekChar(')'))
            {
                return;
            }

            while (this.Peek() != null)
            {
                this.MatchKeyword("ref");
                this.MatchKeyword("out");
                this.ParseExpression();
                if (!this.MatchChar(','))
                {
                    break;
                }
            }
        }

        private void ParseInitializer()
        {
            int guard = 0;
            while (this.Peek() != null && !this.PeekChar('}') && guard < 10000)
            {
                guard++;
                this.ParseExpression();
                if (!this.MatchChar(','))
                {
                    break;
                }
            }
        }

        private void ParseParenExpr()
        {
            this.MatchChar('(');
            this.ParseExpression();
            this.MatchChar(')');
        }

        private void ParseParenExprOrDecl()
        {
            this.MatchChar('(');
            if (this.LooksLikeDeclaration())
            {
                this.ParseDeclaration(false);
            }
            else
            {
                this.ParseExpression();
            }

            this.MatchChar(')');
        }

        private void ParseBaseList()
        {
            this.ParseType();
            while (this.MatchChar(','))
            {
                this.ParseType();
            }
        }

        private void SkipGenerics()
        {
            if (!this.PeekChar('<'))
            {
                return;
            }

            int saved = this.pos;
            this.Take();
            int depth = 1;
            int guard = 0;
            while (this.Peek() != null && depth > 0 && guard < 1000)
            {
                guard++;
                if (this.PeekIdent() || this.PeekBuiltinType())
                {
                    if (this.PeekIdent())
                    {
                        CSharpTok ident = this.Take();
                        this.Mark(ident, TokenKind.Type);
                    }
                    else
                    {
                        this.Take();
                    }

                    continue;
                }

                if (this.MatchChar('<'))
                {
                    depth++;
                    continue;
                }

                if (this.MatchChar('>'))
                {
                    depth--;
                    continue;
                }

                if (this.MatchChar(','))
                {
                    continue;
                }

                if (this.PeekChar('{') || this.PeekChar(';'))
                {
                    this.pos = saved;
                    return;
                }

                this.Take();
            }
        }

        private void SkipPointerArrayNullable()
        {
            while (this.MatchChar('*') || this.MatchChar('?'))
            {
            }

            while (this.MatchChar('['))
            {
                while (this.Peek() != null && !this.PeekChar(']'))
                {
                    if (this.PeekChar(','))
                    {
                        this.Take();
                        continue;
                    }

                    this.ParseExpression();
                    if (!this.MatchChar(','))
                    {
                        break;
                    }
                }

                this.MatchChar(']');
            }
        }

        private void SkipWhereClauses()
        {
            while (this.MatchKeyword("where"))
            {
                if (this.PeekIdent())
                {
                    this.Mark(this.Take(), TokenKind.Type);
                }

                this.MatchChar(':');
                this.ParseType();
                while (this.MatchChar(','))
                {
                    this.ParseType();
                }
            }
        }

        private void SkipAttributes()
        {
            while (this.MatchChar('['))
            {
                int depth = 1;
                while (this.Peek() != null && depth > 0)
                {
                    if (this.MatchChar('['))
                    {
                        depth++;
                        continue;
                    }

                    if (this.MatchChar(']'))
                    {
                        depth--;
                        continue;
                    }

                    if (this.PeekIdent())
                    {
                        this.Mark(this.Take(), TokenKind.Type);
                        continue;
                    }

                    this.Take();
                }
            }
        }

        private void SkipModifiers()
        {
            while (true)
            {
                CSharpTok tok = this.Peek();
                if (tok == null || tok.Kind != TokenKind.Keyword)
                {
                    return;
                }

                string w = tok.Text;
                if (w == "public" || w == "private" || w == "protected" || w == "internal" ||
                    w == "static" || w == "abstract" || w == "sealed" || w == "override" ||
                    w == "virtual" || w == "extern" || w == "unsafe" || w == "partial" ||
                    w == "readonly" || w == "volatile" || w == "async" || w == "new" ||
                    w == "const")
                {
                    this.Take();
                    continue;
                }

                return;
            }
        }

        private void SkipBraces()
        {
            int depth = 1;
            while (this.Peek() != null && depth > 0)
            {
                if (this.MatchChar('{'))
                {
                    depth++;
                    continue;
                }

                if (this.MatchChar('}'))
                {
                    depth--;
                    continue;
                }

                this.Take();
            }
        }

        private void RecoverMember()
        {
            int guard = 0;
            while (this.Peek() != null && guard < 10000)
            {
                guard++;
                if (this.PeekChar('}'))
                {
                    return;
                }

                if (this.PeekTypeIntro() || this.LooksLikeMethod())
                {
                    return;
                }

                if (this.MatchChar(';'))
                {
                    return;
                }

                this.Take();
            }
        }
    }
}
