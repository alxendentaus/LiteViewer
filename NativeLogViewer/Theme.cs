using System.Drawing;

namespace NativeLogViewer;

/// <summary>
/// 一套完整配色。所有颜色用十六进制字符串保存，方便写进设置文件。
/// 默认主题对齐 VS Code Dark+，并提供多款经典编辑器配色。
/// </summary>
internal sealed class Theme
{
    public string Name { get; }
    public string Bg { get; }
    public string Fg { get; }
    public string GutterBg { get; }
    public string LineNo { get; }
    public string CurrentLine { get; }
    public string Border { get; }
    public string Error { get; }
    public string Warn { get; }
    public string Info { get; }
    public string Debug { get; }
    public string Trace { get; }
    public string Keyword { get; }
    public string Str { get; }
    public string Comment { get; }
    public string Number { get; }
    public string Time { get; }
    public string Tag { get; }
    public string HitBg { get; }

    // 界面配色（菜单 / 工具栏 / 状态栏 / 强调色）
    public string ChromeBg { get; }
    public string ChromeFg { get; }
    public string StatusBg { get; }
    public string StatusFg { get; }
    public string Accent { get; }
    public string Hover { get; }

    // VS Code 风格额外区域
    public string ActivityBg { get; }   // 最左侧活动栏
    public string ActivityFg { get; }   // 活动栏图标默认色
    public string TabBg { get; }        // 编辑器标签栏背景
    public string TabActiveBg { get; }  // 当前编辑器标签背景
    public string TabActiveFg { get; }  // 当前编辑器标签文字
    public string TabInactiveFg { get; }// 非当前标签文字
    public string EditorBorder { get; } // 编辑器区域分隔线

    // 自绘滚动条（对齐 VS Code 的 scrollbarSlider）
    public string ScrollThumb { get; }
    public string ScrollThumbHover { get; }
    public string ScrollThumbActive { get; }

    // 是否暗色主题：用于标题栏深色模式、菜单拾色等，不能靠索引判断
    public bool Dark { get; }

    public Theme(string name, string bg, string fg, string gutterBg, string lineNo,
        string currentLine, string border, string error, string warn, string info,
        string debug, string trace, string keyword, string str, string comment,
        string number, string time, string tag, string hitBg,
        string chromeBg, string chromeFg, string statusBg, string statusFg,
        string accent, string hover, string activityBg, string activityFg,
        string tabBg, string tabActiveBg, string tabActiveFg, string tabInactiveFg,
        string editorBorder,
        string scrollThumb = "#5A5A5A", string scrollThumbHover = "#9E9E9E", string scrollThumbActive = "#C6C6C6",
        bool dark = false)
    {
        Name = name; Bg = bg; Fg = fg; GutterBg = gutterBg; LineNo = lineNo;
        CurrentLine = currentLine; Border = border; Error = error; Warn = warn;
        Info = info; Debug = debug; Trace = trace; Keyword = keyword; Str = str;
        Comment = comment; Number = number; Time = time; Tag = tag; HitBg = hitBg;
        ChromeBg = chromeBg; ChromeFg = chromeFg; StatusBg = statusBg; StatusFg = statusFg;
        Accent = accent; Hover = hover;
        ActivityBg = activityBg; ActivityFg = activityFg;
        TabBg = tabBg; TabActiveBg = tabActiveBg; TabActiveFg = tabActiveFg;
        TabInactiveFg = tabInactiveFg; EditorBorder = editorBorder;
        ScrollThumb = scrollThumb; ScrollThumbHover = scrollThumbHover; ScrollThumbActive = scrollThumbActive;
        Dark = dark;
    }

    public Color C(string hex) => ColorTranslator.FromHtml(hex);

    public static readonly IReadOnlyList<Theme> Presets = new[]
    {
        // 1. VS Code Dark+（默认）
        new Theme("VS Code 暗色", "#1E1E1E", "#D4D4D4", "#1E1E1E", "#858585",
            "#2A2D2E", "#2B2B2B", "#F14C4C", "#CCA700", "#4EC9B0", "#9CDCFE", "#C586C0",
            "#569CD6", "#CE9178", "#6A9955", "#B5CEA8", "#C586C0", "#4EC9B0", "#613214",
            "#252526", "#CCCCCC", "#007ACC", "#FFFFFF", "#007ACC", "#094771",
            "#333333", "#858585", "#252526", "#1E1E1E", "#FFFFFF", "#969696", "#2B2B2B",
            "#5A5A5A", "#9E9E9E", "#C6C6C6", dark: true),

        // 2. VS Code Light+
        new Theme("VS Code 亮色", "#FFFFFF", "#000000", "#FFFFFF", "#237893",
            "#F5F5F5", "#E5E5E5", "#E51400", "#BF8803", "#008000", "#0451A5", "#AF00DB",
            "#0000FF", "#A31515", "#008000", "#098658", "#AF00DB", "#267F99", "#F0E68C",
            "#F3F3F3", "#333333", "#007ACC", "#FFFFFF", "#007ACC", "#CDE6F7",
            "#2C2C2C", "#9D9D9D", "#F3F3F3", "#FFFFFF", "#000000", "#616161", "#E5E5E5",
            "#C1C1C1", "#A6A6A6", "#858585", dark: false),

        // 3. 豆沙绿护眼
        new Theme("护眼（豆沙绿）", "#CCE8CF", "#1E3323", "#C7E3CB", "#6D8A72",
            "#BEDFC3", "#A8CCAE", "#B00020", "#9A6B00", "#1E6B32", "#2A6F97", "#6A2E8A",
            "#0000CC", "#8B2E2E", "#1E6B32", "#1E6B32", "#6A2E8A", "#0B6E5E", "#FFE08A",
            "#C5E2C9", "#1E3323", "#2E7D4F", "#FFFFFF", "#2E7D4F", "#A8D5B5",
            "#B8D8BC", "#6D8A72", "#C5E2C9", "#CCE8CF", "#1E3323", "#5A7A60", "#A8CCAE",
            "#8FB996", "#6E9C78", "#4F7E5A", dark: false),

        // 4. 午夜蓝
        new Theme("午夜蓝", "#151E2C", "#D6E2F0", "#151E2C", "#7089A6",
            "#1E2C40", "#2A3850", "#FF6B6B", "#E8B339", "#7EE787", "#79C0FF", "#D2A8FF",
            "#79C0FF", "#E6B796", "#7EE787", "#A5D6FF", "#D2A8FF", "#56D4C0", "#806318",
            "#1B2838", "#D6E2F0", "#3B82F6", "#FFFFFF", "#58A6FF", "#26374D",
            "#111B29", "#7089A6", "#1B2838", "#151E2C", "#FFFFFF", "#8FA3BC", "#2A3850",
            "#41536B", "#6C84A6", "#A0B8D8", dark: true),

        // 5. One Dark（Atom / VS Code 热门配色）
        new Theme("One Dark", "#282C34", "#ABB2BF", "#282C34", "#636D83",
            "#2C323C", "#3E4451", "#E06C75", "#E5C07B", "#98C379", "#61AFEF", "#C678DD",
            "#C678DD", "#98C379", "#5C6370", "#D19A66", "#61AFEF", "#56B6C2", "#3A3F4B",
            "#21252B", "#ABB2BF", "#21252B", "#ABB2BF", "#61AFEF", "#3E4451",
            "#21252B", "#9DA5B4", "#21252B", "#282C34", "#FFFFFF", "#7F848E", "#3E4451",
            "#4B5263", "#6B7280", "#9198A8", dark: true),

        // 6. Dracula（紫粉系）
        new Theme("Dracula", "#282A36", "#F8F8F2", "#282A36", "#6272A4",
            "#44475A", "#44475A", "#FF5555", "#F1FA8C", "#50FA7B", "#8BE9FD", "#BD93F9",
            "#FF79C6", "#F1FA8C", "#6272A4", "#BD93F9", "#8BE9FD", "#50FA7B", "#44475A",
            "#21222C", "#F8F8F2", "#6272A4", "#FFFFFF", "#BD93F9", "#44475A",
            "#21222C", "#6272A4", "#21222C", "#282A36", "#F8F8F2", "#6272A4", "#44475A",
            "#44475A", "#6272A4", "#8BE9FD", dark: true),

        // 7. Nord（北欧冷色）
        new Theme("Nord", "#2E3440", "#D8DEE9", "#2E3440", "#4C566A",
            "#3B4252", "#434C5E", "#BF616A", "#EBCB8B", "#A3BE8C", "#81A1C1", "#B48EAD",
            "#B48EAD", "#A3BE8C", "#616E88", "#B48EAD", "#81A1C1", "#88C0D0", "#434C5E",
            "#242932", "#D8DEE9", "#5E81AC", "#FFFFFF", "#88C0D0", "#434C5E",
            "#242932", "#616E88", "#242932", "#2E3440", "#ECEFF4", "#616E88", "#434C5E",
            "#4C566A", "#5E81AC", "#81A1C1", dark: true),

        // 8. Monokai（经典）
        new Theme("Monokai", "#272822", "#F8F8F2", "#272822", "#75715E",
            "#3E3D32", "#49483E", "#F92672", "#E6DB74", "#A6E22E", "#66D9EF", "#AE81FF",
            "#F92672", "#E6DB74", "#75715E", "#AE81FF", "#66D9EF", "#A6E22E", "#49483E",
            "#1E1F1C", "#F8F8F2", "#A6E22E", "#272822", "#A6E22E", "#49483E",
            "#1E1F1C", "#75715E", "#1E1F1C", "#272822", "#F8F8F2", "#75715E", "#49483E",
            "#49483E", "#75715E", "#A6E22E", dark: true),

        // 9. Solarized Dark
        new Theme("Solarized 暗色", "#002B36", "#839496", "#002B36", "#586E75",
            "#073642", "#073642", "#DC322F", "#B58900", "#859900", "#268BD2", "#6C71C4",
            "#859900", "#2AA198", "#586E75", "#D33682", "#268BD2", "#CB4B16", "#073642",
            "#00212B", "#93A1A1", "#073642", "#93A1A1", "#268BD2", "#073642",
            "#00212B", "#586E75", "#00212B", "#002B36", "#EEE8D5", "#586E75", "#073642",
            "#073642", "#586E75", "#839496", dark: true),

        // 10. Solarized Light
        new Theme("Solarized 亮色", "#FDF6E3", "#657B83", "#FDF6E3", "#93A1A1",
            "#EEE8D5", "#D3C6AA", "#DC322F", "#B58900", "#859900", "#268BD2", "#6C71C4",
            "#859900", "#2AA198", "#93A1A1", "#D33682", "#268BD2", "#CB4B16", "#EEE8D5",
            "#EEE8D5", "#586E75", "#268BD2", "#FFFFFF", "#268BD2", "#E0DCCC",
            "#DDD6C3", "#93A1A1", "#EEE8D5", "#FDF6E3", "#073642", "#93A1A1", "#D3C6AA",
            "#D3C6AA", "#93A1A1", "#657B83", dark: false),

        // 11. GitHub Dark
        new Theme("GitHub 暗色", "#0D1117", "#C9D1D9", "#0D1117", "#484F58",
            "#161B22", "#30363D", "#F85149", "#D29922", "#3FB950", "#58A6FF", "#BC8CFF",
            "#FF7B72", "#A5D6FF", "#8B949E", "#79C0FF", "#58A6FF", "#7EE787", "#1F3255",
            "#010409", "#C9D1D9", "#238636", "#FFFFFF", "#58A6FF", "#21262D",
            "#010409", "#6E7681", "#010409", "#0D1117", "#F0F6FC", "#8B949E", "#30363D",
            "#30363D", "#484F58", "#6E7681", dark: true),

        // 12. 羊皮纸（暖色护眼）
        new Theme("羊皮纸（暖色）", "#F5F0E1", "#3B3529", "#EFE9D8", "#9A8E76",
            "#EAE3CF", "#D8CFB8", "#B22222", "#9A6B00", "#2E7D32", "#1565C0", "#6A1B9A",
            "#6A1B9A", "#8B2E2E", "#9A8E76", "#1565C0", "#1565C0", "#2E7D32", "#E8DCC0",
            "#EAE3CF", "#4A4232", "#8D6E63", "#FFFFFF", "#8D6E63", "#DDD5BE",
            "#E0D8C2", "#9A8E76", "#EAE3CF", "#F5F0E1", "#3B3529", "#9A8E76", "#D8CFB8",
            "#D8CFB8", "#9A8E76", "#6B6151", dark: false)
    };

    // 兼容旧设置里保存的主题名
    public static Theme Find(string name) => name switch
    {
        "深色" or "VS Code 暗色" => Presets[0],
        "记事本（浅色）" or "VS Code 亮色" => Presets[1],
        _ => Presets.FirstOrDefault(t => t.Name == name) ?? Presets[0]
    };
}
