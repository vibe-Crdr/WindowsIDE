using System.Drawing;
using System.Windows.Forms;

namespace WindowsIDE.Ui
{
    /// <summary>
    /// メニューとステータスのダーク描画。
    /// </summary>
    public sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        /// <summary>
        /// ダーク用カラーテーブルで作る。
        /// </summary>
        public DarkMenuRenderer()
            : base(new DarkColorTable())
        {
            this.RoundedEdges = false;
        }

        /// <summary>
        /// メニュー枠の立体ボーダーを出さない。
        /// </summary>
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
        }

        private sealed class DarkColorTable : ProfessionalColorTable
        {
            public override Color MenuStripGradientBegin { get { return Theme.Background; } }
            public override Color MenuStripGradientEnd { get { return Theme.Background; } }
            public override Color MenuItemSelected { get { return Theme.Selection; } }
            public override Color MenuItemSelectedGradientBegin { get { return Theme.Selection; } }
            public override Color MenuItemSelectedGradientEnd { get { return Theme.Selection; } }
            public override Color MenuItemPressedGradientBegin { get { return Theme.CurrentLine; } }
            public override Color MenuItemPressedGradientEnd { get { return Theme.CurrentLine; } }
            public override Color MenuItemBorder { get { return Theme.Border; } }
            public override Color MenuBorder { get { return Theme.Border; } }
            public override Color ToolStripDropDownBackground { get { return Theme.Background; } }
            public override Color ImageMarginGradientBegin { get { return Theme.Background; } }
            public override Color ImageMarginGradientMiddle { get { return Theme.Background; } }
            public override Color ImageMarginGradientEnd { get { return Theme.Background; } }
            public override Color SeparatorDark { get { return Theme.Border; } }
            public override Color SeparatorLight { get { return Theme.Border; } }
            public override Color ToolStripBorder { get { return Theme.Border; } }
            public override Color StatusStripGradientBegin { get { return Theme.StatusBar; } }
            public override Color StatusStripGradientEnd { get { return Theme.StatusBar; } }
        }
    }
}
