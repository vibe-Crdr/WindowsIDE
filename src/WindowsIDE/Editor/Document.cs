using System;
using System.IO;
using WindowsIDE.Languages;

namespace WindowsIDE.Editor
{
    /// <summary>
    /// 開いている文書。バッファ、Undo、エンコーディング、キャレット、スクロール、ハイライト状態を持つ。
    /// </summary>
    public sealed class Document
    {
        private static int untitledSerial = 1;

        private string filePath;
        private string untitledName;
        private TextBuffer buffer;
        private UndoStack undo;
        private FileEncodingInfo encodingInfo;
        private HighlightSession highlightSession;
        private bool isDirty;
        private int caretLine;
        private int caretColumn;
        private int anchorLine;
        private int anchorColumn;
        private int scrollX;
        private int scrollY;

        private Document()
        {
            this.buffer = new TextBuffer();
            this.undo = new UndoStack();
            this.encodingInfo = new FileEncodingInfo();
            this.highlightSession = new HighlightSession();
            this.isDirty = false;
            this.caretLine = 0;
            this.caretColumn = 0;
            this.anchorLine = 0;
            this.anchorColumn = 0;
            this.scrollX = 0;
            this.scrollY = 0;
        }

        /// <summary>ディスク上のパス。無題なら null。</summary>
        public string FilePath { get { return this.filePath; } }

        /// <summary>タブに出す名前。</summary>
        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrEmpty(this.filePath))
                {
                    return Path.GetFileName(this.filePath);
                }

                return this.untitledName;
            }
        }

        /// <summary>本文。</summary>
        public TextBuffer Buffer { get { return this.buffer; } }

        /// <summary>Undo 履歴。</summary>
        public UndoStack Undo { get { return this.undo; } }

        /// <summary>開いたときのエンコーディング。</summary>
        public FileEncodingInfo EncodingInfo { get { return this.encodingInfo; } }

        /// <summary>パスの拡張子から都度判定する言語。</summary>
        public LanguageKind Language
        {
            get { return LanguageDetector.FromPath(this.filePath); }
        }

        /// <summary>行開始状態。タブ切替では捨てない。</summary>
        public HighlightSession HighlightSession
        {
            get { return this.highlightSession; }
        }

        /// <summary>未保存なら true。</summary>
        public bool IsDirty { get { return this.isDirty; } set { this.isDirty = value; } }

        /// <summary>キャレット行。</summary>
        public int CaretLine { get { return this.caretLine; } set { this.caretLine = value; } }

        /// <summary>キャレット列。</summary>
        public int CaretColumn { get { return this.caretColumn; } set { this.caretColumn = value; } }

        /// <summary>選択アンカー行。</summary>
        public int AnchorLine { get { return this.anchorLine; } set { this.anchorLine = value; } }

        /// <summary>選択アンカー列。</summary>
        public int AnchorColumn { get { return this.anchorColumn; } set { this.anchorColumn = value; } }

        /// <summary>横スクロール（ピクセル）。</summary>
        public int ScrollX { get { return this.scrollX; } set { this.scrollX = value; } }

        /// <summary>縦スクロール（行）。</summary>
        public int ScrollY { get { return this.scrollY; } set { this.scrollY = value; } }

        /// <summary>
        /// スクロールバーからの位置を記録する。ignoreProgrammatic なら何もしない。
        /// タブ切替の Maximum 縮小クランプでは true を渡し、短い文書の ScrollY を壊さない。
        /// </summary>
        /// <param name="scrollY">縦位置（行）。</param>
        /// <param name="scrollX">横位置（ピクセル）。</param>
        /// <param name="ignoreProgrammatic">プログラム更新中なら true。</param>
        public void ApplyBarScroll(int scrollY, int scrollX, bool ignoreProgrammatic)
        {
            if (ignoreProgrammatic)
            {
                return;
            }

            this.scrollY = scrollY;
            this.scrollX = scrollX;
        }

        /// <summary>
        /// 無題ドキュメントを UTF-8 BOM + CRLF で作る。
        /// </summary>
        public static Document CreateUntitled()
        {
            Document doc = new Document();
            doc.untitledName = "無題-" + untitledSerial.ToString();
            untitledSerial++;
            doc.buffer.NewLine = "\r\n";
            doc.ResetHighlight();
            return doc;
        }

        /// <summary>
        /// ファイルを開く。バイナリ疑いは例外。
        /// </summary>
        /// <param name="path">絶対パス。</param>
        /// <returns>開いた文書。</returns>
        public static Document Open(string path)
        {
            byte[] data = File.ReadAllBytes(path);
            string text;
            FileEncodingInfo info;
            string error;
            if (!FileEncoding.TryDecode(data, out text, out info, out error))
            {
                throw new InvalidOperationException(error);
            }

            Document doc = new Document();
            doc.filePath = Path.GetFullPath(path);
            doc.encodingInfo = info;
            doc.buffer.NewLine = info.NewLine;
            doc.buffer.SetText(text);
            doc.undo.Clear();
            doc.isDirty = false;
            doc.ResetHighlight();
            return doc;
        }

        /// <summary>
        /// 現在のパスへ保存する。無題なら false。
        /// </summary>
        public bool Save()
        {
            if (string.IsNullOrEmpty(this.filePath))
            {
                return false;
            }

            this.WriteTo(this.filePath);
            return true;
        }

        /// <summary>
        /// 名前を付けて保存する。パスを更新する。
        /// </summary>
        /// <param name="path">保存先。</param>
        public void SaveAs(string path)
        {
            LanguageKind before = this.Language;
            this.WriteTo(path);
            this.filePath = Path.GetFullPath(path);
            this.untitledName = null;
            if (before != this.Language)
            {
                this.ResetHighlight();
            }
        }

        /// <summary>
        /// 選択があるなら true。
        /// </summary>
        public bool HasSelection()
        {
            return this.caretLine != this.anchorLine || this.caretColumn != this.anchorColumn;
        }

        /// <summary>
        /// 選択範囲を開始≦終了で返す。
        /// </summary>
        public void GetSelection(out BufferPoint start, out BufferPoint end)
        {
            BufferPoint a = new BufferPoint(this.anchorLine, this.anchorColumn);
            BufferPoint b = new BufferPoint(this.caretLine, this.caretColumn);
            if (a.Line < b.Line || (a.Line == b.Line && a.Column <= b.Column))
            {
                start = a;
                end = b;
            }
            else
            {
                start = b;
                end = a;
            }

            start = this.buffer.Clamp(start);
            end = this.buffer.Clamp(end);
        }

        /// <summary>
        /// キャレットとアンカーを同じ位置にする。
        /// </summary>
        public void CollapseSelection()
        {
            this.anchorLine = this.caretLine;
            this.anchorColumn = this.caretColumn;
        }

        /// <summary>
        /// 変更を記録してダーティにする。
        /// </summary>
        public void MarkDirty()
        {
            this.isDirty = true;
        }

        private void ResetHighlight()
        {
            this.highlightSession.Reset(this.Language, this.buffer.LineCount);
            this.highlightSession.SyncAfterEdit(this.buffer, 0);
        }

        private void WriteTo(string path)
        {
            string text = this.buffer.GetText();
            byte[] bytes = FileEncoding.GetBytesToSave(text, this.encodingInfo, path);
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllBytes(path, bytes);
            if (this.encodingInfo.IsUtf8 && path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                this.encodingInfo.HasBom = true;
            }

            this.isDirty = false;
        }
    }
}
