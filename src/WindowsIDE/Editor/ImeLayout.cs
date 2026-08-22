namespace WindowsIDE.Editor
{
    /// <summary>
    /// IME 未確定キャレットのクライアント座標と、変換フォント lfHeight の符号。WinForms / imm32 には依存しない。
    /// </summary>
    public static class ImeLayout
    {
        /// <summary>
        /// 変換中カーソルを 0 以上 length 以下に収める。
        /// </summary>
        /// <param name="cursor">IME が返すカーソル位置。</param>
        /// <param name="length">未確定文字列の長さ。負なら 0 扱い。</param>
        /// <returns>クランプしたインデックス。</returns>
        public static int ClampCursor(int cursor, int length)
        {
            if (length < 0)
            {
                length = 0;
            }

            if (cursor < 0)
            {
                return 0;
            }

            if (cursor > length)
            {
                return length;
            }

            return cursor;
        }

        /// <summary>
        /// 本文上のクライアント X。
        /// </summary>
        /// <param name="gutterWidth">行番号ガター幅。</param>
        /// <param name="textInset">ガター右の本文余白。</param>
        /// <param name="columnX">キャレット列の本文 X（スクロール前）。</param>
        /// <param name="hScroll">横スクロール量。</param>
        /// <param name="prefixWidth">未確定の先頭からカーソルまでの幅。</param>
        /// <returns>クライアント X。</returns>
        public static int ClientX(int gutterWidth, int textInset, float columnX, int hScroll, float prefixWidth)
        {
            return gutterWidth + textInset + (int)(columnX - hScroll + prefixWidth);
        }

        /// <summary>
        /// 本文上のクライアント Y。
        /// </summary>
        /// <param name="caretLine">キャレット行。</param>
        /// <param name="firstVisible">先頭可視行。</param>
        /// <param name="lineHeight">行高。</param>
        /// <returns>クライアント Y。</returns>
        public static int ClientY(int caretLine, int firstVisible, int lineHeight)
        {
            return (caretLine - firstVisible) * lineHeight;
        }

        /// <summary>
        /// 変換フォントの高さ。physicalPx が 1 未満なら 1 にし、符号を反転して返す。
        /// </summary>
        /// <param name="physicalPx">本文サイズの物理ピクセル。</param>
        /// <returns>負の高さ（セル高さ。アセント基準）。</returns>
        public static int CompositionFontHeight(int physicalPx)
        {
            if (physicalPx < 1)
            {
                physicalPx = 1;
            }

            return -physicalPx;
        }

        /// <summary>
        /// 単一行欄のクライアント X。ガター無し・列 X は 0。既存 ClientX は変えない。
        /// </summary>
        /// <param name="contentLeft">内側余白を含む左端。</param>
        /// <param name="scrollX">横スクロール量。</param>
        /// <param name="prefixWidth">確定接頭辞＋未確定カーソルまでの幅。</param>
        /// <returns>クライアント X。</returns>
        public static int FieldClientX(int contentLeft, int scrollX, float prefixWidth)
        {
            return ClientX(0, contentLeft, 0f, scrollX, prefixWidth);
        }

        /// <summary>
        /// 単一行欄の外周高さ。cell が 1 未満なら 1、borderPx が負なら 0。
        /// </summary>
        /// <param name="cellHeight">半角・全角 Font.Height の大きい方。</param>
        /// <param name="borderPx">片側の枠（物理 px）。</param>
        /// <returns>cell + borderPx * 2。</returns>
        public static int FieldOuterHeight(int cellHeight, int borderPx)
        {
            if (cellHeight < 1)
            {
                cellHeight = 1;
            }

            if (borderPx < 0)
            {
                borderPx = 0;
            }

            return cellHeight + borderPx * 2;
        }
    }
}
