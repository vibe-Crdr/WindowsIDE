namespace WindowsIDE.Commands
{
    /// <summary>
    /// ポップアップ選択の移動。端で止め、周回しない。
    /// </summary>
    public static class PickerNavigation
    {
        /// <summary>
        /// クエリ変更後の選択。0 件は -1、それ以外は 0。
        /// </summary>
        /// <param name="count">選択できる行数。</param>
        /// <returns>新しい選択。0 件は -1。</returns>
        public static int OnQueryChanged(int count)
        {
            if (count <= 0)
            {
                return -1;
            }

            return 0;
        }

        /// <summary>
        /// 選択を delta だけ動かす。0 件は -1。範囲外の現在位置は端へ寄せ、そこから動かさない。
        /// </summary>
        /// <param name="current">現在の選択。</param>
        /// <param name="count">選択できる行数。</param>
        /// <param name="delta">上は -1、下は +1。</param>
        /// <returns>新しい選択。</returns>
        public static int Move(int current, int count, int delta)
        {
            if (count <= 0)
            {
                return -1;
            }

            if (current < 0)
            {
                return 0;
            }

            if (current >= count)
            {
                return count - 1;
            }

            int next = current + delta;
            if (next < 0)
            {
                return 0;
            }

            if (next >= count)
            {
                return count - 1;
            }

            return next;
        }
    }
}
