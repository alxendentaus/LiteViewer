using System.Drawing;
using System.Windows.Forms;

namespace NativeLogViewer;

/// <summary>
/// VS Code 风格左侧活动栏：竖排图标按钮，选中项左侧有蓝色指示条。
/// 图标使用 Windows 自带的 Segoe MDL2 Assets 字体，无需额外资源。
/// </summary>
internal sealed class ActivityBar : Control
{
    private sealed class Item
    {
        public string Glyph = "";
        public string Tip = "";
        public Action Click = null!;
        public bool Bottom;
    }

    private readonly List<Item> _items = new();
    private Theme _theme;
    private int _selected = -1;
    private int _hover = -1;
    private const int ItemSize = 48;
    private static readonly Font GlyphFont = new("Segoe MDL2 Assets", 20f);

    public ActivityBar(Theme theme)
    {
        _theme = theme;
        Width = 48;
        Dock = DockStyle.Left;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
        MouseMove += (_, e) => SetHover(Hit(e.Y));
        MouseLeave += (_, _) => SetHover(-1);
        MouseDown += (_, e) =>
        {
            int i = Hit(e.Y);
            if (i >= 0) { SelectedIndex = i; _items[i].Click(); }
        };
    }

    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int SelectedIndex
    {
        get => _selected;
        set { if (_selected != value) { _selected = value; Invalidate(); } }
    }

    public void SetTheme(Theme theme) { _theme = theme; Invalidate(); }

    public void AddItem(string glyph, string tip, Action click, bool bottom = false)
        => _items.Add(new Item { Glyph = glyph, Tip = tip, Click = click, Bottom = bottom });

    private int Hit(int y)
    {
        for (int i = 0; i < _items.Count; i++)
        {
            int y0 = ItemY(i);
            if (y >= y0 && y < y0 + ItemSize) return i;
        }
        return -1;
    }

    private int BottomCount() => _items.Count(i => i.Bottom);

    private int ItemY(int index)
    {
        int topStart = 4;
        int bottomStart = Math.Max(topStart, ClientSize.Height - ItemSize * BottomCount() - 8);
        int topN = 0, bottomN = 0;
        for (int i = 0; i < index; i++)
        {
            if (_items[i].Bottom) bottomN++; else topN++;
        }
        return _items[index].Bottom ? bottomStart + bottomN * ItemSize : topStart + topN * ItemSize;
    }

    private void SetHover(int i)
    {
        if (_hover == i) return;
        _hover = i;
        Cursor = i >= 0 ? Cursors.Hand : Cursors.Default;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.Clear(_theme.C(_theme.ActivityBg));
        int topStart = 4;
        int bottomStart = Math.Max(topStart, ClientSize.Height - ItemSize * BottomCount() - 8);

        for (int i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            int y = ItemY(i);
            bool active = i == _selected;
            bool hover = i == _hover;

            if (active)
            {
                using var bar = new SolidBrush(_theme.C(_theme.Accent));
                g.FillRectangle(bar, 0, y, 3, ItemSize);
            }
            else if (hover)
            {
                using var hb = new SolidBrush(_theme.C(_theme.Hover));
                g.FillRectangle(hb, 0, y, Width, ItemSize);
            }

            Color icon = active ? _theme.C(_theme.TabActiveFg) :
                hover ? _theme.C(_theme.ChromeFg) : _theme.C(_theme.ActivityFg);
            TextRenderer.DrawText(g, item.Glyph, GlyphFont,
                new Rectangle(0, y, Width, ItemSize), icon,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }

    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); SetHover(-1); }
}

/// <summary>
/// VS Code 风格编辑器标签条：显示当前文件一个标签，顶部强调线 + 修改圆点。
/// </summary>
internal sealed class EditorTabs : Control
{
    private Theme _theme;
    private string _file = "DEMO.log";
    private bool _modified;
    private const int TabWidth = 200;

    public EditorTabs(Theme theme)
    {
        _theme = theme;
        Dock = DockStyle.Top;
        Height = 36;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer, true);
    }

    public void SetTheme(Theme theme) { _theme = theme; Invalidate(); }
    public void SetFile(string name, bool modified) { _file = name; _modified = modified; Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.Clear(_theme.C(_theme.TabBg));

        int w = Math.Min(TabWidth, Math.Max(80, ClientSize.Width - 4));
        int h = ClientSize.Height;
        using (var bg = new SolidBrush(_theme.C(_theme.TabActiveBg)))
            g.FillRectangle(bg, 0, 0, w, h);
        using (var line = new SolidBrush(_theme.C(_theme.Accent)))
            g.FillRectangle(line, 0, 0, w, 2);
        using (var pen = new Pen(_theme.C(_theme.EditorBorder)))
            g.DrawLine(pen, w - 1, 0, w - 1, h);

        string text = (_modified ? "● " : "") + _file;
        var font = new Font("Segoe UI", 9f);
        TextRenderer.DrawText(g, text, font,
            new Rectangle(12, 0, w - 34, h), _theme.C(_theme.TabActiveFg),
            TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        TextRenderer.DrawText(g, "×", new Font("Segoe UI", 11f),
            new Rectangle(w - 28, 0, 22, h), _theme.C(_theme.TabInactiveFg),
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        font.Dispose();
    }
}
