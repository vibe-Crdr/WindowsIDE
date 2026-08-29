using System.Collections.Generic;
using WindowsIDE.Editor;

namespace WindowsIDE.Languages.CSharp
{
    /// <summary>
    /// 現在バッファの C# 宣言。正は CSharpSemantic.Collect。
    /// </summary>
    public static class CSharpSymbols
    {
        /// <summary>
        /// バッファから宣言を集める。WS 走査では includeLocals を false にする。
        /// </summary>
        /// <param name="buffer">本文。</param>
        /// <param name="filePath">ディスクパス。無題は null。</param>
        /// <param name="includeLocals">フィールドとローカルと仮引数も含める。</param>
        /// <returns>出現順の宣言。</returns>
        public static List<DeclaredSymbol> Collect(TextBuffer buffer, string filePath, bool includeLocals)
        {
            return CSharpSemantic.Collect(buffer, filePath, includeLocals);
        }
    }
}
