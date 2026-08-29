namespace WindowsIDE.Languages
{
    /// <summary>
    /// ユーザーソース上の宣言。行・列は 0 始まり。無題の FilePath は null。
    /// </summary>
    public sealed class DeclaredSymbol
    {
        private string name;
        private SymbolKind kind;
        private LanguageKind language;
        private string filePath;
        private int line;
        private int column;
        private int length;
        private string containingType;
        private string signature;

        /// <summary>
        /// 宣言を作る。所属型とシグネチャは空。
        /// </summary>
        /// <param name="name">照合用の名前。C# の先頭 @ は除く。</param>
        /// <param name="kind">種類。</param>
        /// <param name="language">言語。</param>
        /// <param name="filePath">ディスクパス。無題は null。</param>
        /// <param name="line">0 始まりの行。</param>
        /// <param name="column">0 始まりの列。</param>
        /// <param name="length">識別子スパンの長さ。</param>
        public DeclaredSymbol(string name, SymbolKind kind, LanguageKind language, string filePath, int line, int column, int length)
            : this(name, kind, language, filePath, line, column, length, null, null)
        {
        }

        /// <summary>
        /// 所属型と表示用シグネチャ付きの宣言を作る。
        /// </summary>
        /// <param name="name">照合用の名前。C# の先頭 @ は除く。</param>
        /// <param name="kind">種類。</param>
        /// <param name="language">言語。</param>
        /// <param name="filePath">ディスクパス。無題は null。</param>
        /// <param name="line">0 始まりの行。</param>
        /// <param name="column">0 始まりの列。</param>
        /// <param name="length">識別子スパンの長さ。</param>
        /// <param name="containingType">所属型。無ければ null。</param>
        /// <param name="signature">ホバー用シグネチャ。無ければ null。</param>
        public DeclaredSymbol(string name, SymbolKind kind, LanguageKind language, string filePath, int line, int column, int length, string containingType, string signature)
        {
            this.name = (name == null) ? "" : name;
            this.kind = kind;
            this.language = language;
            this.filePath = filePath;
            this.line = line;
            this.column = column;
            this.length = length;
            this.containingType = containingType;
            this.signature = signature;
        }

        /// <summary>照合用の名前。</summary>
        public string Name
        {
            get { return this.name; }
        }

        /// <summary>種類。</summary>
        public SymbolKind Kind
        {
            get { return this.kind; }
        }

        /// <summary>言語。</summary>
        public LanguageKind Language
        {
            get { return this.language; }
        }

        /// <summary>ディスクパス。無題は null。</summary>
        public string FilePath
        {
            get { return this.filePath; }
        }

        /// <summary>0 始まりの行。</summary>
        public int Line
        {
            get { return this.line; }
        }

        /// <summary>0 始まりの列。</summary>
        public int Column
        {
            get { return this.column; }
        }

        /// <summary>識別子スパンの長さ。</summary>
        public int Length
        {
            get { return this.length; }
        }

        /// <summary>所属型。トップレベルは null。</summary>
        public string ContainingType
        {
            get { return this.containingType; }
        }

        /// <summary>ホバー用シグネチャ。無ければ null。</summary>
        public string Signature
        {
            get { return this.signature; }
        }
    }
}
