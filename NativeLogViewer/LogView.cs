using System.Globalization;
using System.Text.RegularExpressions;

namespace NativeLogViewer;

/// <summary>
/// 自绘日志视图：虚拟滚动，只绘制可见行，因此大文件也不会卡。
/// 支持完整文本编辑（光标、选区、输入、撤销重做、复制粘贴），不依赖第三方控件。
/// </summary>
internal sealed class LogView : Control
{
    private int _lineHeight = 20;
    private int _gutterWidth = 62;
    private const int TextPad = 8;
    private int LineHeight => _lineHeight;
    private int GutterWidth => _gutterWidth;
    private static readonly TextFormatFlags Tf =
        TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix;

    private readonly ThemedScrollBar _scroll = new(vertical: true);
    private readonly ThemedScrollBar _hScroll = new(vertical: false);
    private readonly List<string> _src = new();
    private readonly List<int> _view = new();      // 可见行在 _src 中的下标
    private readonly List<int> _matches = new();   // 命中行在 _view 中的下标
    private int _matchPtr = -1;
    private int _current = -1;

    // 编辑状态：光标与锚点都使用 _view 坐标（可见行下标，字符列）
    private int _caretLine = -1, _caretCol = 0;
    private int _anchorLine = -1, _anchorCol = 0;
    private bool _selecting;
    private bool _focused, _caretOn = true;
    private readonly System.Windows.Forms.Timer _blink = new() { Interval = 500 };

    // 撤销/重做：记录被修改行的前后内容
    private sealed class EditAction
    {
        public int Start;
        public List<string> Before = new();
        public List<string> After = new();
    }
    private readonly List<EditAction> _undo = new();
    private readonly List<EditAction> _redo = new();

    private Font _font = new(FontSettings.DefaultFamily, FontSettings.DefaultSize);
    private Font _fontBold = new(FontSettings.DefaultFamily, FontSettings.DefaultSize, FontStyle.Bold);
    private Regex? _searchRe;
    private string _mode = "log";
    private Theme _theme = Theme.Presets[0];

    // 主题色（全部来自当前主题）
    private Color Bg => _theme.C(_theme.Bg);
    private Color Fg => _theme.C(_theme.Fg);
    private Color GutterBg => _theme.C(_theme.GutterBg);
    private Color LnFg => _theme.C(_theme.LineNo);
    private Color CurBg => _theme.C(_theme.CurrentLine);
    private Color Err => _theme.C(_theme.Error);
    private Color Warn => _theme.C(_theme.Warn);
    private Color Info => _theme.C(_theme.Info);
    private Color Debug => _theme.C(_theme.Debug);
    private Color Trace => _theme.C(_theme.Trace);
    private Color Keyword => _theme.C(_theme.Keyword);
    private Color Str => _theme.C(_theme.Str);
    private Color Com => _theme.C(_theme.Comment);
    private Color Num => _theme.C(_theme.Number);
    private Color Time => _theme.C(_theme.Time);
    private Color TagColor => _theme.C(_theme.Tag);
    private Color HitBg => _theme.C(_theme.HitBg);
    private Color Border => _theme.C(_theme.Border);
    private Color Accent => _theme.C(_theme.Accent);

    private static readonly Regex LogRe = new(
        @"\b(FATAL|ERROR|EROR|WARN|WARNING|INFO|DEBUG|TRACE)\b|(\d{4}[-/]\d{2}[-/]\d{2}[ T]\d{2}:\d{2}:\d{2}(?:[.,]\d+)?|\b\d{2}:\d{2}:\d{2}(?:[.,]\d+)?\b)|(\[[^\]\n]{1,40}\])",
        RegexOptions.Compiled);
    private static readonly Regex CodeRe = new(
        @"(\/\/[^\n]*|#[^\n]*|\/\*[\s\S]*?\*\/)|(""(?:\\.|[^""\\])*""|'(?:\\.|[^'\\])*'|`(?:\\.|[^`\\])*`)|(\b(?:function|const|let|var|if|else|for|while|do|return|class|def|import|from|export|public|private|protected|void|int|string|bool|true|false|null|None|async|await|try|catch|finally|throw|new|this|super|package|include|using|struct|interface|enum|switch|case|break|continue|yield|lambda|pass|static|final)\b)|(\b\d+(?:\.\d+)?\b)",
        RegexOptions.Compiled);

    public int SourceCount => _src.Count;
    public int ViewCount => _view.Count;
    public int MatchCount => _matches.Count;
    public string MatchInfo => _matches.Count == 0 ? "0 / 0" : $"{_matchPtr + 1} / {_matches.Count}";
    public bool Modified { get; private set; }
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public int CaretLineNumber => _caretLine >= 0 && _caretLine < _view.Count ? _view[_caretLine] + 1 : 0;
    public int CaretColumn => _caretCol + 1;

    public event Action? ModifiedChanged;
    public event Action? ContentChanged;
    public event Action? HistoryChanged;
    public event Action? SaveRequested;
    public event Action<int, int>? CaretMoved;

    private float TextX => GutterWidth + TextPad;
    // 水平滚动条占据了 ClientSize 底部，计算可见行数时要扣掉它
    private int TextViewportHeight => Math.Max(LineHeight, ClientSize.Height - _hScroll.Height);
    private int VisibleLines => Math.Max(1, TextViewportHeight / LineHeight);

    // WinForms ScrollBar 的实际可达最大值是 Maximum - LargeChange + 1
    private int MaxVScroll => Math.Max(0, _scroll.Maximum - _scroll.LargeChange + 1);
    private int MaxHScroll => Math.Max(0, _hScroll.Maximum - _hScroll.LargeChange + 1);

    public LogView()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.Selectable, true);
        Controls.Add(_scroll);
        Controls.Add(_hScroll);
        _scroll.ValueChanged += (_, _) => Invalidate();
        _hScroll.ValueChanged += (_, _) => Invalidate();
        MouseWheel += (_, e) =>
        {
            int delta = e.Delta > 0 ? -3 : 3;
            _scroll.Value = Math.Clamp(_scroll.Value + delta, 0, MaxVScroll);
        };
        MouseDown += OnMouseDown;
        MouseMove += OnMouseMove;
        MouseUp += (_, _) => _selecting = false;
        MouseDoubleClick += (_, e) => { if (e.X >= GutterWidth) { PlaceCaret(e.X, e.Y, false); SelectWord(); } };
        GotFocus += (_, _) => { _focused = true; _caretOn = true; _blink.Start(); Invalidate(); };
        LostFocus += (_, _) => { _focused = false; _blink.Stop(); Invalidate(); };
        _blink.Tick += (_, _) => { _caretOn = !_caretOn; Invalidate(); };
    }

    // ---------- 公共接口 ----------
    public void SetTheme(Theme theme)
    {
        _theme = theme;
        _scroll.SetTheme(theme);
        _hScroll.SetTheme(theme);
        Invalidate();
    }
    public void SetMode(string mode) { _mode = mode; Invalidate(); }

    public void SetFontStyle(string family, float size, FontStyle style)
    {
        var old = _font;
        var oldBold = _fontBold;
        try
        {
            _font = new Font(family, size, style);
            _fontBold = new Font(family, size, style | FontStyle.Bold);
        }
        catch
        {
            _font = old; _fontBold = oldBold;
            return;
        }
        old.Dispose();
        oldBold.Dispose();

        // 行高取字体实际高度，留上下留白
        _lineHeight = Math.Max(16, TextRenderer.MeasureText("Ag", _font, Size.Empty, Tf).Height + 6);
        UpdateGutterWidth();
        UpdateScrollRange();
        Invalidate();
    }

    private void UpdateGutterWidth()
    {
        int charW = TextRenderer.MeasureText("0", _font, Size.Empty, Tf).Width;
        int digits = Math.Max(2, Math.Max(_src.Count, _view.Count).ToString(CultureInfo.InvariantCulture).Length);
        _gutterWidth = Math.Max(46, digits * charW + 22);
    }

    public void SetSource(List<string> lines)
    {
        _src.Clear();
        _src.AddRange(lines);
        RebuildView();
    }

    public void ClearHistory()
    {
        _undo.Clear(); _redo.Clear();
        if (Modified) { Modified = false; ModifiedChanged?.Invoke(); }
        HistoryChanged?.Invoke();
    }

    public void ApplyFilter(Regex? searchRe, string mode)
    {
        _searchRe = searchRe;
        _mode = mode;
        _matchPtr = -1;
        _current = -1;
        RebuildView();
        _matchPtr = _matches.Count > 0 ? 0 : -1;
        if (_matches.Count > 0) { _caretLine = _matches[0]; _caretCol = 0; _anchorLine = -1; _current = _caretLine; }
        Invalidate();
        NotifyCaret();
    }

    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string TagText { get; set; } = "全部";

    private void RebuildView(int preferSrcLine = -1, int preferCol = -1)
    {
        _view.Clear();
        _matches.Clear();
        UpdateGutterWidth();
        string level = TagText;
        for (int i = 0; i < _src.Count; i++)
        {
            string line = _src[i];
            if (level != "全部" && line.IndexOf(level, StringComparison.OrdinalIgnoreCase) < 0) continue;
            _view.Add(i);
            if (_searchRe != null && _searchRe.IsMatch(line)) _matches.Add(_view.Count - 1);
        }
        if (_matchPtr >= _matches.Count) _matchPtr = _matches.Count > 0 ? 0 : -1;

        if (preferSrcLine >= 0)
        {
            int vi = _view.IndexOf(preferSrcLine);
            if (vi < 0)
            {
                vi = 0;
                int best = int.MaxValue;
                for (int i = 0; i < _view.Count; i++)
                {
                    int d = Math.Abs(_view[i] - preferSrcLine);
                    if (d < best) { best = d; vi = i; }
                }
            }
            _caretLine = vi;
            int len = _view.Count > 0 ? _src[_view[vi]].Length : 0;
            _caretCol = preferCol < 0 ? len : Math.Clamp(preferCol, 0, len);
            _anchorLine = -1;
        }
        ClampCaret();
        _current = _caretLine;
        UpdateScrollRange();
        EnsureCaretVisible();
        Invalidate();
        NotifyCaret();
        ContentChanged?.Invoke();
    }

    private void ClampCaret()
    {
        if (_view.Count == 0) { _caretLine = -1; _caretCol = 0; _anchorLine = -1; return; }
        if (_caretLine < 0) _caretLine = 0;
        if (_caretLine >= _view.Count) _caretLine = _view.Count - 1;
        _caretCol = Math.Clamp(_caretCol, 0, _src[_view[_caretLine]].Length);
        if (_anchorLine >= _view.Count) _anchorLine = -1;
        if (_anchorLine >= 0) _anchorCol = Math.Clamp(_anchorCol, 0, _src[_view[_anchorLine]].Length);
        _current = _caretLine;
    }

    private void NotifyCaret() => CaretMoved?.Invoke(CaretLineNumber, CaretColumn);

    private void UpdateScrollRange()
    {
        int visible = VisibleLines;
        // Maximum 设为行索引上界，LargeChange 设为一屏行数，
        // 这样 WinForms 实际可达值 Maximum-LargeChange+1 正好是最后一行起始位置。
        _scroll.Maximum = Math.Max(0, _view.Count - 1);
        _scroll.LargeChange = Math.Max(1, visible);
        if (_scroll.Value > MaxVScroll) _scroll.Value = MaxVScroll;

        UpdateHorizontalRange();
    }

    // 水平滚动范围由可见行中最宽的一行决定（等宽字体，按字符数估算）
    private void UpdateHorizontalRange()
    {
        int textArea = Math.Max(1, ClientSize.Width - _scroll.Width - GutterWidth - TextPad * 2);
        int maxCols = 0;
        for (int i = 0; i < _view.Count; i++)
        {
            int len = _src[_view[i]].Length;
            if (len > maxCols) maxCols = len;
        }
        // 用字符宽度估算：每字符约 TextRenderer 测得的等宽宽度
        int charW = TextRenderer.MeasureText("0", _font).Width;
        int contentW = maxCols * charW + TextPad * 2;
        // 同样留出 LargeChange 的滑道长度，避免横向最后一段拖不到。
        _hScroll.Maximum = Math.Max(0, contentW - 1);
        _hScroll.LargeChange = Math.Max(1, textArea);
        _hScroll.SmallChange = charW * 4;
        if (_hScroll.Value > MaxHScroll) _hScroll.Value = MaxHScroll;
    }

    public void ScrollToBottom()
    {
        UpdateScrollRange();
        _scroll.Value = MaxVScroll;
        Invalidate();
    }

    public void GotoMatch(int step)
    {
        if (_matches.Count == 0) return;
        _matchPtr = (_matchPtr + step + _matches.Count) % _matches.Count;
        _caretLine = _matches[_matchPtr];
        _caretCol = 0;
        _anchorLine = -1;
        _current = _caretLine;
        EnsureCaretVisible();
        Invalidate();
        NotifyCaret();
    }

    private void EnsureCaretVisible()
    {
        if (_caretLine < 0 || _view.Count == 0) return;
        int visible = VisibleLines;
        int targetV;
        if (_caretLine < _scroll.Value) targetV = _caretLine;
        else if (_caretLine >= _scroll.Value + visible) targetV = _caretLine - visible + 1;
        else targetV = _scroll.Value;
        _scroll.Value = Math.Clamp(targetV, 0, MaxVScroll);

        // 水平跟随：光标超出可视宽度时自动横向滚动
        int viewW = Math.Max(1, ClientSize.Width - _scroll.Width - GutterWidth - TextPad * 2);
        float caretX = MeasurePrefix(_src[_view[_caretLine]], _caretCol);
        if (caretX < _hScroll.Value) _hScroll.Value = Math.Clamp((int)caretX - TextPad, 0, MaxHScroll);
        else if (caretX > _hScroll.Value + viewW) _hScroll.Value = Math.Clamp((int)caretX - viewW + TextPad, 0, MaxHScroll);
    }

    protected override void OnResize(EventArgs e) { base.OnResize(e); UpdateScrollRange(); Invalidate(); }

    // ---------- 绘制 ----------
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        g.Clear(Bg);
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        int w = ClientSize.Width - _scroll.Width;
        int textH = TextViewportHeight;
        using var gutterBrush = new SolidBrush(GutterBg);
        g.FillRectangle(gutterBrush, 0, 0, GutterWidth, textH);
        using var borderPen = new Pen(Border);
        g.DrawLine(borderPen, GutterWidth - 1, 0, GutterWidth - 1, textH);
        using var selBrush = new SolidBrush(Color.FromArgb(64, Accent));

        int start = _scroll.Value;
        int visible = VisibleLines;
        int end = Math.Min(_view.Count, start + visible);
        bool hasSel = HasSelection;
        var (sLine, sCol) = hasSel ? SelStart : (0, 0);
        var (eLine, eCol) = hasSel ? SelEnd : (0, 0);
        int hOff = _hScroll.Value;
        float tx = TextX - hOff;   // 文本绘制起点（已扣除水平滚动）

        // 第一遍：行号 + 当前行底色/强调条（不裁剪、不受水平滚动影响）
        for (int i = start; i < end; i++)
        {
            int y = (i - start) * LineHeight;
            if (i == _current)
            {
                using var curBrush = new SolidBrush(CurBg);
                g.FillRectangle(curBrush, 0, y, w, LineHeight);
                using var barBrush = new SolidBrush(Accent);
                g.FillRectangle(barBrush, 0, y, 3, LineHeight);
            }
            using var lnBrush = new SolidBrush(i == _current ? Accent : LnFg);
            Font lnFont = i == _current ? _fontBold : _font;
            g.DrawString((_view[i] + 1).ToString(CultureInfo.InvariantCulture), lnFont, lnBrush,
                new RectangleF(0, y, GutterWidth - 8, LineHeight),
                new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center });
        }

        // 第二遍：文本区裁剪后绘制正文，超出宽度的部分被裁掉，可横向滚动查看
        var clipState = g.Save();
        g.SetClip(new Rectangle(GutterWidth, 0, Math.Max(0, w - GutterWidth), textH));
        for (int i = start; i < end; i++)
        {
            int y = (i - start) * LineHeight;
            string text = _src[_view[i]];

            // 选区背景（支持跨行）
            if (hasSel && i >= sLine && i <= eLine)
            {
                int c1 = i == sLine ? sCol : 0;
                int c2 = i == eLine ? eCol : text.Length;
                if (c2 > c1)
                {
                    float x1 = tx + MeasurePrefix(text, c1);
                    float x2 = tx + MeasurePrefix(text, c2);
                    g.FillRectangle(selBrush, x1, y, Math.Max(1, x2 - x1), LineHeight);
                }
            }

            // 搜索命中背景（圆角高亮）
            if (_searchRe != null)
            {
                foreach (Match m in _searchRe.Matches(text))
                {
                    if (m.Length == 0) continue;
                    float x1 = tx + MeasurePrefix(text, m.Index);
                    float x2 = tx + MeasurePrefix(text, Math.Min(m.Index + m.Length, text.Length));
                    if (x1 > w) break;
                    using var hitBrush = new SolidBrush(HitBg);
                    g.FillRectangle(hitBrush, x1, y + 2, Math.Min(x2, w) - x1, LineHeight - 4);
                }
            }

            DrawTokens(g, text, tx, y);

            // 文本光标
            if (_focused && _caretOn && i == _caretLine)
            {
                float cx = tx + MeasurePrefix(text, _caretCol);
                using var caretPen = new Pen(Fg, 1.5f);
                g.DrawLine(caretPen, cx, y + 2, cx, y + LineHeight - 2);
            }
        }
        g.Restore(clipState);
    }

    private float MeasurePrefix(string text, int col)
    {
        if (col <= 0) return 0;
        string s = col >= text.Length ? text : text[..col];
        return TextRenderer.MeasureText(s, _font, Size.Empty, Tf).Width;
    }

    private void DrawTokens(Graphics g, string text, float x, int y)
    {
        Regex re = _mode == "code" ? CodeRe : LogRe;
        int last = 0;
        foreach (Match m in re.Matches(text))
        {
            if (m.Index > last) Draw(g, text[last..m.Index], ref x, y, Fg);
            Color color = Fg;
            if (_mode == "code")
            {
                color = m.Groups[1].Success ? Com : m.Groups[2].Success ? Str :
                        m.Groups[3].Success ? Keyword : Num;
            }
            else
            {
                if (m.Groups[1].Success)
                {
                    string lv = m.Groups[1].Value.ToUpperInvariant()
                        .Replace("WARNING", "WARN").Replace("EROR", "ERROR");
                    color = lv switch
                    {
                        "FATAL" or "ERROR" => Err,
                        "WARN" => Warn,
                        "INFO" => Info,
                        "DEBUG" => Debug,
                        _ => Trace
                    };
                }
                else color = m.Groups[2].Success ? Time : TagColor;
            }
            Draw(g, m.Value, ref x, y, color);
            last = m.Index + m.Length;
        }
        if (last < text.Length) Draw(g, text[last..], ref x, y, Fg);
    }

    private void Draw(Graphics g, string s, ref float x, int y, Color color)
    {
        if (s.Length == 0) return;
        TextRenderer.DrawText(g, s, _font, new Point((int)x, y), color, Tf);
        x += TextRenderer.MeasureText(s, _font, Size.Empty, Tf).Width;
    }

    // ---------- 鼠标：定位、拖选、选词 ----------
    private void OnMouseDown(object? sender, MouseEventArgs e)
    {
        Cursor = e.X >= GutterWidth ? Cursors.IBeam : Cursors.Default;
        Focus();
        if (_view.Count == 0) return;
        int line = Math.Clamp(_scroll.Value + e.Y / LineHeight, 0, _view.Count - 1);
        if (e.X < GutterWidth)
        {
            _anchorLine = line; _anchorCol = 0;
            _caretLine = line; _caretCol = _src[_view[line]].Length;
        }
        else
        {
            PlaceCaret(e.X, e.Y, (ModifierKeys & Keys.Shift) != 0);
        }
        _selecting = true;
        Capture = true;
        EnsureCaretVisible();
        Invalidate();
        NotifyCaret();
    }

    private void OnMouseMove(object? sender, MouseEventArgs e)
    {
        // 文本区显示 I 形输入光标（和记事本一致），行号区保持箭头
        Cursor = e.X >= GutterWidth ? Cursors.IBeam : Cursors.Default;
        if (!_selecting) return;
        PlaceCaret(e.X, e.Y, true);
        Invalidate();
        NotifyCaret();
    }

    private void PlaceCaret(int x, int y, bool extend)
    {
        if (_view.Count == 0) return;
        int line = Math.Clamp(_scroll.Value + y / LineHeight, 0, _view.Count - 1);
        string text = _src[_view[line]];
        float px = Math.Max(0, x - TextX + _hScroll.Value);
        // 二分定位，长行也不会卡；中文按实际宽度处理
        int lo = 0, hi = text.Length;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) / 2;
            if (MeasurePrefix(text, mid) <= px) lo = mid; else hi = mid - 1;
        }
        int col = lo;
        if (!extend) { _anchorLine = line; _anchorCol = col; }
        _caretLine = line; _caretCol = col;
        _current = line;
    }

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';

    private void SelectWord()
    {
        if (_caretLine < 0) return;
        string text = _src[_view[_caretLine]];
        int a = Math.Min(_caretCol, text.Length), b = a;
        if (a < text.Length && IsWordChar(text[a]))
        {
            while (a > 0 && IsWordChar(text[a - 1])) a--;
            while (b < text.Length && IsWordChar(text[b])) b++;
        }
        else
        {
            while (a > 0 && !char.IsWhiteSpace(text[a - 1])) a--;
            while (b < text.Length && !char.IsWhiteSpace(text[b])) b++;
        }
        if (b > a) { _anchorLine = _caretLine; _anchorCol = a; _caretCol = b; Invalidate(); }
    }

    // ---------- 选区 ----------
    public bool HasSelection => _anchorLine >= 0 && (_anchorLine != _caretLine || _anchorCol != _caretCol);

    private (int line, int col) SelStart =>
        _anchorLine < _caretLine || (_anchorLine == _caretLine && _anchorCol <= _caretCol)
            ? (_anchorLine, _anchorCol) : (_caretLine, _caretCol);

    private (int line, int col) SelEnd =>
        _anchorLine < _caretLine || (_anchorLine == _caretLine && _anchorCol <= _caretCol)
            ? (_caretLine, _caretCol) : (_anchorLine, _anchorCol);

    private string GetSelectedText()
    {
        if (!HasSelection) return "";
        var (sl, sc) = SelStart;
        var (el, ec) = SelEnd;
        if (sl == el) return _src[_view[sl]][sc..ec];
        var sb = new System.Text.StringBuilder();
        sb.Append(_src[_view[sl]][sc..]);
        for (int i = sl + 1; i <= el; i++) sb.Append("\r\n").Append(i == el ? _src[_view[i]][..ec] : _src[_view[i]]);
        return sb.ToString();
    }

    public void SelectAll()
    {
        if (_view.Count == 0) return;
        _anchorLine = 0; _anchorCol = 0;
        _caretLine = _view.Count - 1; _caretCol = _src[_view[^1]].Length;
        _current = _caretLine;
        EnsureCaretVisible(); Invalidate(); NotifyCaret();
    }

    // ---------- 键盘 ----------
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        Keys mod = keyData & Keys.Modifiers;
        Keys key = keyData & Keys.KeyCode;
        if (mod == Keys.Control)
        {
            switch (key)
            {
                case Keys.A: SelectAll(); return true;
                case Keys.C: Copy(); return true;
                case Keys.X: Cut(); return true;
                case Keys.V: Paste(); return true;
                case Keys.Z: Undo(); return true;
                case Keys.Y: Redo(); return true;
                case Keys.S: SaveRequested?.Invoke(); return true;
                case Keys.Home: MoveCaret(_view.Count > 0 ? 0 : -1, 0, false); return true;
                case Keys.End: MoveCaret(_view.Count - 1, int.MaxValue, false); return true;
                case Keys.Left: WordMove(-1, false); return true;
                case Keys.Right: WordMove(1, false); return true;
            }
        }
        if (mod == (Keys.Control | Keys.Shift) && key == Keys.Z) { Redo(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        bool shift = (e.Modifiers & Keys.Shift) != 0;
        switch (e.KeyCode)
        {
            case Keys.Up: MoveCaret(_caretLine - 1, _caretCol, shift); break;
            case Keys.Down: MoveCaret(_caretLine + 1, _caretCol, shift); break;
            case Keys.Left: MoveCaret(_caretLine, _caretCol - 1, shift); break;
            case Keys.Right: MoveCaret(_caretLine, _caretCol + 1, shift); break;
            case Keys.Home: MoveCaret(_caretLine, 0, shift); break;
            case Keys.End: MoveCaret(_caretLine, int.MaxValue, shift); break;
            case Keys.PageUp: MoveCaret(_caretLine - Math.Max(1, ClientSize.Height / LineHeight - 1), _caretCol, shift); break;
            case Keys.PageDown: MoveCaret(_caretLine + Math.Max(1, ClientSize.Height / LineHeight - 1), _caretCol, shift); break;
            case Keys.Back:
                if (HasSelection) DeleteSelection();
                else if (_caretCol > 0) BackspaceChar();
                else if (_caretLine > 0) MergeWithPrevious();
                break;
            case Keys.Delete:
                if (HasSelection) DeleteSelection();
                else if (_caretLine >= 0 && _caretCol < _src[_view[_caretLine]].Length) DeleteChar();
                else if (_caretLine >= 0 && _caretLine < _view.Count - 1) MergeWithNext();
                break;
            case Keys.Enter:
                InsertText("\n"); break;
            case Keys.Tab:
                InsertText("\t"); break;
            case Keys.Escape:
                _anchorLine = -1; Invalidate(); break;
            default:
                return;
        }
        e.SuppressKeyPress = true;
        e.Handled = true;
    }

    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        base.OnKeyPress(e);
        if (e.Handled || char.IsControl(e.KeyChar)) return;
        InsertText(e.KeyChar.ToString());
        e.Handled = true;
    }

    private void MoveCaret(int line, int col, bool extend)
    {
        if (_view.Count == 0) return;
        line = Math.Clamp(line, 0, _view.Count - 1);
        col = Math.Clamp(col, 0, _src[_view[line]].Length);
        if (!extend) { _anchorLine = line; _anchorCol = col; }
        _caretLine = line; _caretCol = col;
        _current = line;
        EnsureCaretVisible(); Invalidate(); NotifyCaret();
    }

    private void WordMove(int dir, bool extend)
    {
        if (_caretLine < 0) return;
        string text = _src[_view[_caretLine]];
        int col = _caretCol;
        if (dir < 0)
        {
            if (col == 0) { MoveCaret(_caretLine - 1, int.MaxValue, extend); return; }
            col--;
            while (col > 0 && !IsWordChar(text[col])) col--;
            while (col > 0 && IsWordChar(text[col - 1])) col--;
        }
        else
        {
            if (col >= text.Length) { MoveCaret(_caretLine + 1, 0, extend); return; }
            while (col < text.Length && IsWordChar(text[col])) col++;
            while (col < text.Length && !IsWordChar(text[col])) col++;
        }
        MoveCaret(_caretLine, col, extend);
    }

    // ---------- 编辑操作 ----------
    private void Record(int start, IEnumerable<string> before, IEnumerable<string> after)
    {
        _undo.Add(new EditAction { Start = start, Before = before.ToList(), After = after.ToList() });
        _redo.Clear();
        HistoryChanged?.Invoke();
    }

    private void MarkModified()
    {
        if (!Modified) { Modified = true; ModifiedChanged?.Invoke(); }
        ContentChanged?.Invoke();
    }

    public void ClearModifiedFlag()
    {
        if (Modified) { Modified = false; ModifiedChanged?.Invoke(); }
    }

    private void InsertText(string text)
    {
        if (_caretLine < 0) return;
        if (HasSelection) DeleteSelection();
        if (_caretLine < 0) return;
        text = text.Replace("\r\n", "\n").Replace('\r', '\n');
        int srcLine = _view[_caretLine];
        string cur = _src[srcLine];
        int col = Math.Clamp(_caretCol, 0, cur.Length);
        var parts = text.Split('\n');

        if (parts.Length == 1)
        {
            string before = cur;
            _src[srcLine] = cur.Insert(col, text);
            Record(srcLine, new[] { before }, new[] { _src[srcLine] });
            _caretCol = col + text.Length;
            ClampCaret(); Invalidate(); NotifyCaret();
        }
        else
        {
            var before = new[] { cur };
            var after = new List<string> { cur[..col] + parts[0] };
            for (int i = 1; i < parts.Length - 1; i++) after.Add(parts[i]);
            after.Add(parts[^1] + cur[col..]);
            _src.RemoveRange(srcLine, 1);
            _src.InsertRange(srcLine, after);
            Record(srcLine, before, after);
            RebuildView(srcLine + parts.Length - 1, parts[^1].Length);
        }
        MarkModified();
    }

    private void BackspaceChar()
    {
        int srcLine = _view[_caretLine];
        string cur = _src[srcLine];
        if (_caretCol <= 0 || cur.Length == 0) return;
        string before = cur;
        _src[srcLine] = cur.Remove(_caretCol - 1, 1);
        Record(srcLine, new[] { before }, new[] { _src[srcLine] });
        _caretCol--;
        ClampCaret(); Invalidate(); NotifyCaret(); MarkModified();
    }

    private void DeleteChar()
    {
        int srcLine = _view[_caretLine];
        string cur = _src[srcLine];
        if (_caretCol >= cur.Length) return;
        string before = cur;
        _src[srcLine] = cur.Remove(_caretCol, 1);
        Record(srcLine, new[] { before }, new[] { _src[srcLine] });
        ClampCaret(); Invalidate(); NotifyCaret(); MarkModified();
    }

    private void MergeWithPrevious()
    {
        if (_caretLine <= 0) return;
        int prev = _view[_caretLine - 1], curSrc = _view[_caretLine];
        var before = new[] { _src[prev], _src[curSrc] };
        var after = new[] { _src[prev] + _src[curSrc] };
        int col = _src[prev].Length;
        _src.RemoveRange(prev, 2);
        _src.InsertRange(prev, after);
        Record(prev, before, after);
        RebuildView(prev, col);
        MarkModified();
    }

    private void MergeWithNext()
    {
        if (_caretLine >= _view.Count - 1) return;
        int curSrc = _view[_caretLine], next = _view[_caretLine + 1];
        var before = new[] { _src[curSrc], _src[next] };
        var after = new[] { _src[curSrc] + _src[next] };
        int col = _src[curSrc].Length;
        _src.RemoveRange(curSrc, 2);
        _src.InsertRange(curSrc, after);
        Record(curSrc, before, after);
        RebuildView(curSrc, col);
        MarkModified();
    }

    private void DeleteSelection()
    {
        if (!HasSelection) return;
        var (sl, sc) = SelStart;
        var (el, ec) = SelEnd;
        int sSrc = _view[sl], eSrc = _view[el];
        if (sSrc == eSrc)
        {
            string before = _src[sSrc];
            _src[sSrc] = before.Remove(sc, ec - sc);
            Record(sSrc, new[] { before }, new[] { _src[sSrc] });
            _caretLine = sl; _caretCol = sc; _anchorLine = -1;
            ClampCaret(); Invalidate(); NotifyCaret();
        }
        else
        {
            var before = _src.GetRange(sSrc, eSrc - sSrc + 1);
            var after = new[] { _src[sSrc][..sc] + _src[eSrc][ec..] };
            _src.RemoveRange(sSrc, eSrc - sSrc + 1);
            _src.InsertRange(sSrc, after);
            Record(sSrc, before, after);
            _anchorLine = -1;
            RebuildView(sSrc, sc);
        }
        MarkModified();
    }

    public void Undo()
    {
        if (_undo.Count == 0) return;
        var a = _undo[^1]; _undo.RemoveAt(_undo.Count - 1);
        _src.RemoveRange(a.Start, a.After.Count);
        _src.InsertRange(a.Start, a.Before);
        _redo.Add(new EditAction { Start = a.Start, Before = a.After, After = a.Before });
        _anchorLine = -1;
        RebuildView(a.Start, Math.Min(a.Before[0].Length, a.Before[0].Length));
        HistoryChanged?.Invoke(); MarkModified();
    }

    public void Redo()
    {
        if (_redo.Count == 0) return;
        var a = _redo[^1]; _redo.RemoveAt(_redo.Count - 1);
        _src.RemoveRange(a.Start, a.Before.Count);
        _src.InsertRange(a.Start, a.After);
        _undo.Add(new EditAction { Start = a.Start, Before = a.Before, After = a.After });
        _anchorLine = -1;
        RebuildView(a.Start, a.After[0].Length);
        HistoryChanged?.Invoke(); MarkModified();
    }

    public void Copy()
    {
        if (!HasSelection) return;
        try { Clipboard.SetText(GetSelectedText()); } catch { /* 剪贴板被占用 */ }
    }

    public void Cut()
    {
        if (!HasSelection) return;
        Copy();
        DeleteSelection();
    }

    public void Paste()
    {
        try
        {
            if (Clipboard.ContainsText()) InsertText(Clipboard.GetText());
        }
        catch { /* 剪贴板被占用 */ }
    }

    public string[] GetLines() => _src.ToArray();

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _font.Dispose(); _fontBold.Dispose(); _scroll.Dispose(); _blink.Dispose(); }
        base.Dispose(disposing);
    }
}
