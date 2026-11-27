using System.Drawing;
using System.Windows.Forms;

namespace NativeLogViewer;

/// <summary>
/// 让 MenuStrip / ToolStrip / StatusStrip 跟随当前主题配色，风格对齐 VS Code：
/// 扁平无渐变、细分隔线、选中项使用蓝色高亮。
/// </summary>
internal sealed class ThemeRenderer : ToolStripProfessionalRenderer
{
    private readonly Theme _t;

    public ThemeRenderer(Theme theme) : base(new ThemeColorTable(theme)) { _t = theme; }

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        Color bg = e.ToolStrip is StatusStrip ? _t.C(_t.StatusBg) : _t.C(_t.ChromeBg);
        using var brush = new SolidBrush(bg);
        e.Graphics.FillRectangle(brush, e.AffectedBounds);
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        using var pen = new Pen(_t.C(_t.EditorBorder));
        if (e.ToolStrip is StatusStrip)
            e.Graphics.DrawLine(pen, e.ConnectedArea.Left, e.ConnectedArea.Top, e.ConnectedArea.Right, e.ConnectedArea.Top);
        else if (e.ToolStrip is MenuStrip)
            e.Graphics.DrawLine(pen, 0, e.ToolStrip.Height - 1, e.ToolStrip.Width, e.ToolStrip.Height - 1);
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.ToolStrip is StatusStrip ? _t.C(_t.StatusFg) : _t.C(_t.ChromeFg);
        base.OnRenderItemText(e);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        using var pen = new Pen(_t.C(_t.EditorBorder));
        if (e.Vertical)
            e.Graphics.DrawLine(pen, e.Item.Width / 2, 3, e.Item.Width / 2, e.Item.Height - 3);
        else
            e.Graphics.DrawLine(pen, 3, e.Item.Height / 2, e.Item.Width - 3, e.Item.Height / 2);
    }

    protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e)
    {
        if (e.Item.Selected || e.Item.Pressed)
        {
            Color c = e.Item.Pressed ? _t.C(_t.Accent) : _t.C(_t.Hover);
            using var brush = new SolidBrush(c);
            e.Graphics.FillRectangle(brush, 0, 0, e.Item.Width, e.Item.Height);
        }
        else
        {
            base.OnRenderButtonBackground(e);
        }
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if (e.Item.Selected || e.Item.Pressed)
        {
            using var brush = new SolidBrush(_t.C(_t.Hover));
            e.Graphics.FillRectangle(brush, new Rectangle(0, 0, e.Item.Width, e.Item.Height));
        }
        // 不绘制默认渐变背景，保持扁平
    }

    private sealed class ThemeColorTable : ProfessionalColorTable
    {
        private readonly Theme _t;
        public ThemeColorTable(Theme t) { _t = t; }
        private Color C(string hex) => ColorTranslator.FromHtml(hex);

        public override Color MenuStripGradientBegin => C(_t.ChromeBg);
        public override Color MenuStripGradientEnd => C(_t.ChromeBg);
        public override Color ToolStripGradientBegin => C(_t.ChromeBg);
        public override Color ToolStripGradientMiddle => C(_t.ChromeBg);
        public override Color ToolStripGradientEnd => C(_t.ChromeBg);
        public override Color StatusStripGradientBegin => C(_t.StatusBg);
        public override Color StatusStripGradientEnd => C(_t.StatusBg);
        public override Color ToolStripPanelGradientBegin => C(_t.ChromeBg);
        public override Color ToolStripPanelGradientEnd => C(_t.ChromeBg);
        public override Color MenuBorder => C(_t.EditorBorder);
        public override Color MenuItemBorder => C(_t.Accent);
        public override Color MenuItemSelected => C(_t.Hover);
        public override Color MenuItemSelectedGradientBegin => C(_t.Hover);
        public override Color MenuItemSelectedGradientEnd => C(_t.Hover);
        public override Color MenuItemPressedGradientBegin => C(_t.ChromeBg);
        public override Color MenuItemPressedGradientEnd => C(_t.ChromeBg);
        public override Color ButtonSelectedGradientBegin => C(_t.Hover);
        public override Color ButtonSelectedGradientMiddle => C(_t.Hover);
        public override Color ButtonSelectedGradientEnd => C(_t.Hover);
        public override Color ButtonSelectedBorder => C(_t.Hover);
        public override Color ButtonPressedGradientBegin => C(_t.Accent);
        public override Color ButtonPressedGradientMiddle => C(_t.Accent);
        public override Color ButtonPressedGradientEnd => C(_t.Accent);
        public override Color ButtonCheckedGradientBegin => C(_t.Hover);
        public override Color ButtonCheckedGradientMiddle => C(_t.Hover);
        public override Color ButtonCheckedGradientEnd => C(_t.Hover);
        public override Color CheckBackground => C(_t.Bg);
        public override Color CheckSelectedBackground => C(_t.Hover);
        public override Color CheckPressedBackground => C(_t.Accent);
        public override Color GripDark => C(_t.EditorBorder);
        public override Color GripLight => C(_t.ChromeBg);
        public override Color SeparatorDark => C(_t.EditorBorder);
        public override Color SeparatorLight => C(_t.ChromeBg);
        public override Color ImageMarginGradientBegin => C(_t.ChromeBg);
        public override Color ImageMarginGradientMiddle => C(_t.ChromeBg);
        public override Color ImageMarginGradientEnd => C(_t.ChromeBg);
        public override Color OverflowButtonGradientBegin => C(_t.ChromeBg);
        public override Color OverflowButtonGradientMiddle => C(_t.ChromeBg);
        public override Color OverflowButtonGradientEnd => C(_t.ChromeBg);
    }
}
