using System.Collections.Generic;
using WindowsIDE.Editor;
using WindowsIDE.Languages.CSharp;

namespace WindowsIDE.Languages
{
    /// <summary>
    /// 言語ごとの識別子オーバーレイを overlay に書く。
    /// </summary>
    public static class SemanticClassifier
    {
        /// <summary>
        /// バッファを分類する。Plain / cmd は C# 束縛を走らせない。
        /// </summary>
        /// <param name="language">言語。</param>
        /// <param name="buffer">本文。</param>
        /// <param name="workspaceRoot">ワークスペース。無くてよい。</param>
        /// <param name="overlay">行ごとのスパン。呼び出し側が行数分用意する。</param>
        public static void Classify(LanguageKind language, TextBuffer buffer, string workspaceRoot, List<ClassifySpan>[] overlay)
        {
            if (buffer == null || overlay == null)
            {
                return;
            }

            if (language == LanguageKind.CSharp)
            {
                CSharpSemantic.Classify(buffer, workspaceRoot, overlay);
                return;
            }

            if (language == LanguageKind.PowerShell)
            {
                PowerShellSemantic.Classify(buffer, overlay);
                return;
            }

            if (language == LanguageKind.Vba)
            {
                VbaSemantic.Classify(buffer, workspaceRoot, overlay);
            }
        }
    }
}
