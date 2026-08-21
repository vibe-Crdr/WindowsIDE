using System.Text;

namespace WindowsIDE.Ui.Fonts
{
    /// <summary>
    /// WinForms と同じニーモニック規則で表示用文字列を作る。WinForms には依存しない。
    /// </summary>
    public static class UiMnemonic
    {
        /// <summary>
        /// <c>&amp;&amp;</c> を表示 <c>&amp;</c> にし、最初の単独 <c>&amp;X</c> の X をニーモニックにする。他の <c>&amp;</c> は落とす。
        /// </summary>
        /// <param name="text">元のテキスト。null や空は空文字。</param>
        /// <param name="mnemonicIndex">stripped 側のニーモニック位置。無ければ -1。</param>
        /// <returns>表示用文字列。null や空は ""。</returns>
        public static string Strip(string text, out int mnemonicIndex)
        {
            mnemonicIndex = -1;
            if (text == null || text.Length == 0)
            {
                return "";
            }

            StringBuilder sb = new StringBuilder(text.Length);
            int i = 0;
            while (i < text.Length)
            {
                if (text[i] != '&')
                {
                    sb.Append(text[i]);
                    i++;
                    continue;
                }

                if (i + 1 >= text.Length)
                {
                    break;
                }

                if (text[i + 1] == '&')
                {
                    sb.Append('&');
                    i += 2;
                    continue;
                }

                if (mnemonicIndex < 0)
                {
                    mnemonicIndex = sb.Length;
                }

                i++;
            }

            return sb.ToString();
        }
    }
}
