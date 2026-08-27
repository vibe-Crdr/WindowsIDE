namespace WindowsIDE.Vba
{
    /// <summary>
    /// VBComponent.Type のうち同期対象。1=std、2=class、100=document。UserForm(3) とその他は対象外。
    /// </summary>
    public enum VbaComponentKind
    {
        /// <summary>標準モジュール（.bas）。</summary>
        Std = 1,

        /// <summary>クラス モジュール（.cls）。</summary>
        Class = 2,

        /// <summary>ドキュメント モジュール（ThisWorkbook / シート）。Name 変更・Remove・Import しない。</summary>
        Document = 100
    }
}
