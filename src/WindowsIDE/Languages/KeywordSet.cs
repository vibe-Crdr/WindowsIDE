using System;
using System.Collections.Generic;

namespace WindowsIDE.Languages
{
    /// <summary>
    /// キーワードの照合集合。XML には出さない。
    /// </summary>
    public sealed class KeywordSet
    {
        private readonly HashSet<string> items;

        /// <summary>
        /// 単語表から集合を作る。comparer で大小比較を決める。
        /// </summary>
        /// <param name="words">キーワード。</param>
        /// <param name="comparer">Ordinal または OrdinalIgnoreCase。</param>
        public KeywordSet(IEnumerable<string> words, StringComparer comparer)
        {
            this.items = new HashSet<string>(comparer);
            if (words == null)
            {
                return;
            }

            foreach (string word in words)
            {
                if (!string.IsNullOrEmpty(word))
                {
                    this.items.Add(word);
                }
            }
        }

        /// <summary>
        /// 単語がキーワードなら true。
        /// </summary>
        /// <param name="word">照合する語。</param>
        /// <returns>含まれるとき true。</returns>
        public bool Contains(string word)
        {
            if (word == null)
            {
                return false;
            }

            return this.items.Contains(word);
        }
    }
}
