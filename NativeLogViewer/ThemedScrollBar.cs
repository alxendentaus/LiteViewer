using System.Drawing.Drawing2D;

namespace NativeLogViewer;

/// <summary>
/// 自绘主题化滚动条，外观跟随 Theme，替代 WinForms 原生滚动条
/// （原生滚动条在暗色主题下仍是系统浅色，风格不统一）。
/// 保留 ScrollBar 的属性语义：Value 可达上限 = Maximum - LargeChange + 1，
/// 因此调用方现有的滚动范围计算无需改动。
/// </summary>
internal sealed class ThemedScrollBar : Control
{
    private readonly bool _vertical;
    private Theme _theme = Theme.Presets[0];
    private int _minimum;
    private int _maximum = 100;
    private int _largeChange = 10;
    private int _smallChange = 1;
    private int _value;

    private bool _hover;
    private bool _dragging;
    private int _dragStartPos;
    private int _dragStartValue;

    public event EventHandler? ValueChanged;

    public ThemedScrollBar(bool vertical)
    {
        _vertical = vertical;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint | ControlStyles.Selectable, true);
        if (vertical) { Width = 16; Dock = DockStyle.Right; }
        else { Height = 16; Dock = DockStyle.Bottom; }
    }

    // ---------- 与原生 ScrollBar 一致的属性语义 ----------
    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int Minimum
    {
        get => _minimum;
        set { _minimum = value; Clamp(); Invalidate(); }
    }

    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int Maximum
    {
        get => _maximum;
        set { _maximum = value; Clamp(); Invalidate(); }
    }

    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int LargeChange
    {
        get => _largeChange;
        set { _largeChange = Math.Max(1, value); Clamp(); Invalidate(); }
    }

    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int SmallChange
    {
        get => _smallChange;
        set { _smallChange = Math.Max(1, value); Invalidate(); }
    }

    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int Value
    {
        get => _value;
        set
        {
            int v = Math.Clamp(value, _minimum, MaxValue);
            if (_value == v) return;
            _value = v;
            ValueChanged?.Invoke(this, EventArgs.Empty);
            Invalidate();
        }
    }

    public int MaxValue => Math.Max(_minimum, _maximum - _largeChange + 1);

    private void Clamp()
    {
        if (_maximum < _minimum) _maximum = _minimum;
        _value = Math.Clamp(_value, _minimum, MaxValue);
    }

    public void SetTheme(Theme theme) { _theme = theme; Invalidate(); }

    // ---------- 几何 ----------
    private int Extent => _vertical ? ClientSize.Height : ClientSize.Width;
    private int Thickness => Math.Max(1, _vertical ? ClientSize.Width : ClientSize.Height);
    private const int TrackPad = 2;
    private int TrackLength => Math.Max(1, Extent - TrackPad * 2);

    private int ThumbSize
    {
        get
        {
            int span = (_maximum - _minimum) + _largeChange;
            if (span <= 0) return TrackLength;
            int size = (int)Math.Round(TrackLength * (double)_largeChange / span);
            return Math.Clamp(size, Math.Min(28, TrackLength), TrackLength);
        }
    }

    private int ThumbPos
    {
        get
        {
            int movable = TrackLength - ThumbSize;
            if (movable <= 0 || MaxValue <= _minimum) return TrackPad;
            return TrackPad + (int)Math.Round((_value - _minimum) / (double)(MaxValue - _minimum) * movable);
        }
    }

    private bool NeedScroll => MaxValue > _minimum;

    // ---------- 绘制 ----------
    private Color TrackBg => _theme.C(_theme.Bg);
    private Color ThumbNormal => _theme.C(_theme.ScrollThumb);
    private Color ThumbHover => _theme.C(_theme.ScrollThumbHover);
    private Color ThumbActive => _theme.C(_theme.ScrollThumbActive);

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.Clear(TrackBg);
        if (!NeedScroll) return;

        int ts = ThumbSize, tp = ThumbPos, t = Thickness;
        // 拇指宽度固定约 10px、居中，轨道不额外绘制——贴近 VS Code 的细滚动条
        int inset = Math.Max(1, (t - 10) / 2);
        int cross = Math.Max(4, t - inset * 2);
        Rectangle r = _vertical
            ? new Rectangle(inset, tp, cross, ts)
            : new Rectangle(tp, inset, ts, cross);

        Color c = _dragging ? ThumbActive : (_hover ? ThumbHover : ThumbNormal);
        using var brush = new SolidBrush(c);
        using var path = RoundRect(r, Math.Min(5, cross / 2));
        g.FillPath(brush, path);
    }

    private static GraphicsPath RoundRect(Rectangle r, int radius)
    {
        var p = new GraphicsPath();
        int d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        if (d <= 0) { p.AddRectangle(r); return p; }
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    // ---------- 鼠标交互 ----------
    private int MouseCoord(MouseEventArgs e) => _vertical ? e.Y : e.X;

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left || !NeedScroll) return;
        int pos = MouseCoord(e);
        int tp = ThumbPos, ts = ThumbSize;
        if (pos >= tp && pos < tp + ts)
        {
            _dragging = true;
            _dragStartPos = pos;
            _dragStartValue = _value;
        }
        else
        {
            // 点击轨道：按 LargeChange 翻页
            Value = _value + (pos < tp ? -_largeChange : _largeChange);
        }
        Capture = true;
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int pos = MouseCoord(e);
        if (_dragging)
        {
            int movable = TrackLength - ThumbSize;
            if (movable > 0 && MaxValue > _minimum)
            {
                int delta = pos - _dragStartPos;
                int valueDelta = (int)Math.Round(delta / (double)movable * (MaxValue - _minimum));
                Value = _dragStartValue + valueDelta;
            }
        }
        else
        {
            bool h = NeedScroll && pos >= ThumbPos && pos < ThumbPos + ThumbSize;
            if (h != _hover) { _hover = h; Invalidate(); }
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (_dragging) { _dragging = false; Capture = false; Invalidate(); }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hover && !_dragging) { _hover = false; Invalidate(); }
    }
}
