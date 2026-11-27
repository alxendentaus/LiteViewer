using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace NativeLogViewer;

public partial class Form1 : Form
{
    private readonly LogView _view = new();

    // VS Code 风格：活动栏 + 编辑器标签条
    private readonly ActivityBar _activity;
    private readonly EditorTabs _tabs;
    private readonly Panel _editorHost = new() { Dock = DockStyle.Fill };

    // 极简工具栏：打开 + 查找 + 命中数
    private readonly MenuStrip _menu = new();
    private readonly ToolStrip _tool = new();
    private readonly ToolStripTextBox _search = new() { ToolTipText = "查找（F3 下一个）", Size = new Size(220, 24) };
    private readonly ToolStripLabel _hitInfo = new("0 / 0") { AutoSize = true };

    private readonly StatusStrip _status = new();
    private readonly ToolStripStatusLabel _stFile = new("DEMO.log");
    private readonly ToolStripStatusLabel _stLines = new("0 行");
    private readonly ToolStripStatusLabel _stShow = new("显示 0 行");
    private readonly ToolStripStatusLabel _stSize = new("—");
    private readonly ToolStripStatusLabel _stPos = new("行 1, 列 1");
    private readonly ToolStripStatusLabel _errTip = new() { AutoSize = true, ForeColor = Color.Firebrick };
    private readonly ToolStripStatusLabel _stRight = new("UTF-8");
    private readonly ToolStripStatusLabel _stAuthor = new("by alxendentaus") { AutoSize = true };

    // 状态（由菜单驱动，不再占用工具栏）
    private string _level = "全部";
    private string _mode = "自动";
    private string _encoding = "自动";
    private bool _caseSensitive = false;
    private bool _regexMode = false;
    private bool _follow = true;

    private string? _path;
    private long _lastLength = -1;
    private Theme _theme = Theme.Presets[0];
    private readonly FontSettings _fontSettings = new();
    private string _pythonPath = "";
    private string _compilerPath = "";
    private readonly System.Windows.Forms.Timer _tailTimer = new() { Interval = 1500 };

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LiteViewer", "settings.json");

    private const string ContextKey = @"Software\Classes\*\shell\LiteViewer";

    public Form1(string? openPath = null)
    {
        Text = "日志查看器";
        // 从 exe 自身提取自定义图标，确保窗口与任务栏显示应用图标而非系统默认图标
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
        Width = 1280; Height = 800;
        MinimumSize = new Size(880, 500);
        StartPosition = FormStartPosition.CenterScreen;

        LoadSettings();
        _view.SetTheme(_theme);
        _view.SetFontStyle(_fontSettings.Family, _fontSettings.Size, (FontStyle)_fontSettings.Style);
        _view.Dock = DockStyle.Fill;

        _activity = new ActivityBar(_theme);
        _tabs = new EditorTabs(_theme);
        BuildActivityBar();

        BuildMenu();
        BuildToolBar();
        BuildStatus();
        BuildOutputPanel();

        // 编辑区：标签条(顶) + 查找条(顶) + 输出(底) + 视图(填充)
        _editorHost.Controls.Add(_view);
        _editorHost.Controls.Add(_outputPanel);
        _editorHost.Controls.Add(_tool);
        _editorHost.Controls.Add(_tabs);

        // 主体：活动栏(左) + 编辑区(填充)
        Controls.Add(_editorHost);
        Controls.Add(_activity);
        Controls.Add(_status);
        Controls.Add(_menu);

        AllowDrop = true;
        DragEnter += (_, e) => { if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true) e.Effect = DragDropEffects.Copy; };
        DragDrop += (_, e) =>
        {
            if (e.Data?.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files && ConfirmDiscard()) LoadFile(files[0]);
        };

        WireEvents();
        ApplyTheme();

        _tailTimer.Tick += (_, _) => CheckFileUpdate();
        _tailTimer.Start();

        if (!string.IsNullOrEmpty(openPath) && File.Exists(openPath)) LoadFile(openPath);
        else LoadDemo();
        UpdateTitle();
    }

    private void BuildActivityBar()
    {
        // Segoe MDL2 Assets 图标字符
        _activity.AddItem("\uE8B7", "打开文件 (Ctrl+O)", () => OpenFile());          // Folder
        _activity.AddItem("\uE721", "查找 (Ctrl+F)", () => { _search.Focus(); _search.SelectAll(); }); // Zoom
        _activity.AddItem("\uE768", "运行 (F5)", () => RunCode());                   // Play
        _activity.AddItem("\uE706", "切换主题", () => CycleTheme());                 // Brightness
        _activity.AddItem("\uE713", "运行工具设置", () => ShowRunnerSettings(), bottom: true); // Settings
        _activity.SelectedIndex = 0;
    }

    private static int ThemeIndex(Theme t)
    {
        for (int i = 0; i < Theme.Presets.Count; i++) if (ReferenceEquals(Theme.Presets[i], t)) return i;
        return 0;
    }

    private void CycleTheme() => SetTheme(Theme.Presets[(ThemeIndex(_theme) + 1) % Theme.Presets.Count]);

    private void UpdateTitle()
    {
        string name = _path != null ? Path.GetFileName(_path) : "DEMO.log";
        Text = (_view.Modified ? "*" : "") + name + " - 日志查看器";
        _tabs.SetFile(name, _view.Modified);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_runProcess != null && !_runProcess.HasExited)
        {
            try { _runProcess.Kill(entireProcessTree: true); } catch { }
        }
        e.Cancel = !ConfirmDiscard();
        base.OnFormClosing(e);
    }

    private bool ConfirmDiscard()
    {
        if (!_view.Modified) return true;
        var r = MessageBox.Show("当前内容已修改，是否保存？\n\n是 = 保存后继续\n否 = 放弃修改\n取消 = 返回编辑",
            "未保存的更改", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
        if (r == DialogResult.Cancel) return false;
        if (r == DialogResult.Yes) { Save(false); if (_view.Modified) return false; }
        return true;
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.S)) { Save(false); return true; }
        if (keyData == (Keys.Control | Keys.Shift | Keys.S)) { Save(true); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void Save(bool saveAs)
    {
        string? target = _path;
        if (target == null || saveAs)
        {
            using var dlg = new SaveFileDialog
            {
                Title = "保存文件",
                Filter = "日志文件|*.log|文本文件|*.txt|所有文件|*.*",
                FileName = _path != null ? Path.GetFileName(_path) : "日志.log",
                OverwritePrompt = true
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;
            target = dlg.FileName;
        }
        try
        {
            File.WriteAllText(target, string.Join("\r\n", _view.GetLines()), GetEncoding());
            _path = target;
            _lastLength = new FileInfo(target).Length;
            _view.ClearModifiedFlag();
            _stFile.Text = Path.GetFileName(target);
            _stSize.Text = FormatSize(_lastLength);
            _errTip.Text = "已保存 " + DateTime.Now.ToString("HH:mm:ss");
            UpdateTitle();
        }
        catch (Exception ex) { _errTip.Text = "保存失败: " + ex.Message; }
    }

    // ---------- 菜单 ----------
    private void BuildMenu()
    {
        _menu.Renderer = new ThemeRenderer(_theme);

        // 文件
        var mFile = new ToolStripMenuItem("文件(_F)");
        mFile.DropDownItems.Add(NewMenu("打开(_O)...", Keys.Control | Keys.O, (_, _) => OpenFile()));
        mFile.DropDownItems.Add(NewMenu("保存(_S)", Keys.Control | Keys.S, (_, _) => Save(false)));
        mFile.DropDownItems.Add(NewMenu("另存为(_A)...", Keys.Control | Keys.Shift | Keys.S, (_, _) => Save(true)));
        mFile.DropDownItems.Add(new ToolStripSeparator());
        mFile.DropDownItems.Add(NewMenu("重新加载(_R)", Keys.Control | Keys.R, (_, _) => Reload()));
        mFile.DropDownItems.Add(new ToolStripSeparator());
        mFile.DropDownItems.Add(NewMenu("退出(_X)", Keys.Alt | Keys.F4, (_, _) => Close()));

        // 编辑
        var mEdit = new ToolStripMenuItem("编辑(_E)");
        mEdit.DropDownOpening += (_, _) => UpdateEditMenu();
        _miUndo = NewMenu("撤销(_U)", Keys.Control | Keys.Z, (_, _) => _view.Undo());
        _miRedo = NewMenu("重做(_R)", Keys.Control | Keys.Y, (_, _) => _view.Redo());
        _miCut = NewMenu("剪切(_T)", Keys.Control | Keys.X, (_, _) => _view.Cut());
        _miCopy = NewMenu("复制(_C)", Keys.Control | Keys.C, (_, _) => _view.Copy());
        _miPaste = NewMenu("粘贴(_P)", Keys.Control | Keys.V, (_, _) => _view.Paste());
        mEdit.DropDownItems.Add(_miUndo);
        mEdit.DropDownItems.Add(_miRedo);
        mEdit.DropDownItems.Add(new ToolStripSeparator());
        mEdit.DropDownItems.Add(_miCut);
        mEdit.DropDownItems.Add(_miCopy);
        mEdit.DropDownItems.Add(_miPaste);
        mEdit.DropDownItems.Add(NewMenu("全选(_A)", Keys.Control | Keys.A, (_, _) => _view.SelectAll()));
        mEdit.DropDownItems.Add(new ToolStripSeparator());
        mEdit.DropDownItems.Add(NewMenu("查找(_F)", Keys.Control | Keys.F, (_, _) => { _search.Focus(); _search.SelectAll(); }));
        mEdit.DropDownItems.Add(NewMenu("查找下一个", Keys.F3, (_, _) => Goto(1)));
        mEdit.DropDownItems.Add(NewMenu("查找上一个", Keys.Shift | Keys.F3, (_, _) => Goto(-1)));
        mEdit.DropDownItems.Add(new ToolStripSeparator());
        mEdit.DropDownItems.Add(CheckMenu("区分大小写", _caseSensitive, v => { _caseSensitive = v; ApplyFilter(); SaveSettings(); }));
        mEdit.DropDownItems.Add(CheckMenu("使用正则表达式", _regexMode, v => { _regexMode = v; ApplyFilter(); SaveSettings(); }));

        // 查看
        var mView = new ToolStripMenuItem("查看(_V)");
        _levelMenu = BuildRadioGroup(new[] { "全部", "ERROR", "WARN", "INFO", "DEBUG", "TRACE" }, _level, v => { _level = v; ApplyFilter(); SaveSettings(); });
        mView.DropDownItems.Add(new ToolStripMenuItem("日志级别") { DropDownItems = { } });
        var mLevel = (ToolStripMenuItem)mView.DropDownItems[mView.DropDownItems.Count - 1];
        foreach (var item in _levelMenu) mLevel.DropDownItems.Add(item);

        _modeMenu = BuildRadioGroup(new[] { "自动", "日志", "代码" }, _mode, v => { _mode = v; ApplyFilter(); SaveSettings(); });
        var mMode = new ToolStripMenuItem("显示模式");
        foreach (var item in _modeMenu) mMode.DropDownItems.Add(item);
        mView.DropDownItems.Add(mMode);

        _encMenu = BuildRadioGroup(new[] { "自动", "UTF-8", "GBK" }, _encoding, v =>
        {
            _encoding = v; SaveSettings();
            if (_path != null) LoadFile(_path);
        });
        var mEnc = new ToolStripMenuItem("文件编码");
        foreach (var item in _encMenu) mEnc.DropDownItems.Add(item);
        mView.DropDownItems.Add(mEnc);

        mView.DropDownItems.Add(new ToolStripSeparator());
        mView.DropDownItems.Add(NewMenu("字体(_F)...", Keys.None, (_, _) => ChooseFont()));
        mView.DropDownItems.Add(NewMenu("恢复默认字体", Keys.None, (_, _) => RestoreDefaultFont()));
        mView.DropDownItems.Add(CheckMenu("自动跟随最新日志", _follow, v => { _follow = v; if (v) _view.ScrollToBottom(); SaveSettings(); }));

        // 主题
        var mTheme = new ToolStripMenuItem("主题(_T)");
        _themeMenu.Clear();
        foreach (var t in Theme.Presets)
        {
            var item = new ToolStripMenuItem(t.Name) { CheckOnClick = true, Checked = t.Name == _theme.Name };
            item.Click += (_, _) =>
            {
                foreach (var sib in _themeMenu) sib.Checked = false;
                item.Checked = true;
                SetTheme(t);
            };
            _themeMenu.Add(item);
            mTheme.DropDownItems.Add(item);
        }

        // 工具
        var mTools = new ToolStripMenuItem("工具(_S)");
        _installItem = new ToolStripMenuItem("安装到右键菜单") { CheckOnClick = false };
        _installItem.Click += (_, _) => ToggleContextMenu();
        mTools.DropDownItems.Add(_installItem);
        mTools.DropDownItems.Add(NewMenu("取消右键菜单", Keys.None, (_, _) => UninstallContextMenu()));
        mTools.DropDownItems.Add(new ToolStripSeparator());
        mTools.DropDownItems.Add(NewMenu("关于 / 署名", Keys.None, (_, _) =>
        {
            using var about = new AboutForm(_theme);
            about.ShowDialog(this);
        }));
        UpdateInstallMenu();

        _menu.Items.AddRange(new ToolStripItem[] { mFile, mEdit, mView, mTheme, BuildRunMenu(), mTools });
    }

    private ToolStripMenuItem _installItem = new();
    private ToolStripMenuItem _miUndo = new(), _miRedo = new(), _miCut = new(), _miCopy = new(), _miPaste = new();
    private List<ToolStripMenuItem> _levelMenu = new();
    private List<ToolStripMenuItem> _modeMenu = new();
    private List<ToolStripMenuItem> _encMenu = new();
    private readonly List<ToolStripMenuItem> _themeMenu = new();

    private static ToolStripMenuItem NewMenu(string text, Keys shortcut, EventHandler onClick)
    {
        var item = new ToolStripMenuItem(text) { ShortcutKeys = shortcut, ShowShortcutKeys = shortcut != Keys.None };
        item.Click += onClick;
        return item;
    }

    private static ToolStripMenuItem CheckMenu(string text, bool checkedState, Action<bool> onToggle)
    {
        var item = new ToolStripMenuItem(text) { CheckOnClick = true, Checked = checkedState };
        item.CheckedChanged += (_, _) => onToggle(item.Checked);
        return item;
    }

    private static List<ToolStripMenuItem> BuildRadioGroup(string[] values, string selected, Action<string> onSelect)
    {
        var list = new List<ToolStripMenuItem>();
        foreach (var v in values)
        {
            var item = new ToolStripMenuItem(v) { CheckOnClick = true, Checked = v == selected };
            item.Click += (_, _) =>
            {
                foreach (var sib in list) sib.Checked = false;
                item.Checked = true;
                onSelect(v);
            };
            list.Add(item);
        }
        return list;
    }

    // ---------- 极简工具栏 ----------
    private void BuildToolBar()
    {
        _tool.Renderer = new ThemeRenderer(_theme);
        _tool.GripStyle = ToolStripGripStyle.Hidden;
        _tool.Padding = new Padding(6, 2, 6, 2);

        var btnPrev = new ToolStripButton("↑") { ToolTipText = "上一个匹配 (Shift+F3)" };
        btnPrev.Click += (_, _) => Goto(-1);
        var btnNext = new ToolStripButton("↓") { ToolTipText = "下一个匹配 (F3)" };
        btnNext.Click += (_, _) => Goto(1);

        _tool.Items.AddRange(new ToolStripItem[]
        {
            new ToolStripLabel("查找:"), _search, btnPrev, btnNext, _hitInfo,
            new ToolStripStatusLabel { Spring = true },
            new ToolStripLabel("Ctrl+F 查找 · F5 运行 · Ctrl+S 保存")
        });
    }

    private void BuildStatus()
    {
        _status.Renderer = new ThemeRenderer(_theme);
        _status.Items.AddRange(new ToolStripItem[]
        {
            _stFile, new ToolStripSeparator(), _stLines, new ToolStripSeparator(),
            _stShow, new ToolStripSeparator(), _stSize, new ToolStripSeparator(), _stPos,
            new ToolStripSeparator(), _runState,
            new ToolStripStatusLabel { Spring = true }, _errTip, _stRight,
            new ToolStripSeparator(), _stAuthor
        });
        _stAuthor.IsLink = true;
        _stAuthor.ToolTipText = "作者署名 · 点击查看 / 访问开源仓库";
        _stAuthor.Click += (_, _) =>
        {
            using var about = new AboutForm(_theme);
            about.ShowDialog(this);
        };
        _status.Dock = DockStyle.Bottom;
        _status.SizingGrip = false;
    }

    private void WireEvents()
    {
        _search.TextChanged += (_, _) => ApplyFilter();
        _search.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Goto((ModifierKeys & Keys.Shift) != 0 ? -1 : 1); }
            if (e.KeyCode == Keys.Escape) _search.Text = "";
        };

        _view.ModifiedChanged += UpdateTitle;
        _view.ContentChanged += UpdateCounts;
        _view.HistoryChanged += UpdateEditMenu;
        _view.SaveRequested += () => Save(false);
        _view.CaretMoved += (line, col) => _stPos.Text = $"行 {line}, 列 {col}";
    }

    private void UpdateCounts()
    {
        // 编辑后同步主数据，避免切换过滤/搜索时丢失修改
        _lines.Clear();
        _lines.AddRange(_view.GetLines());
        _hitInfo.Text = _view.MatchInfo;
        _stLines.Text = _view.SourceCount.ToString("N0") + " 行";
        _stShow.Text = "显示 " + _view.ViewCount.ToString("N0") + " 行";
    }

    private void UpdateEditMenu()
    {
        _miUndo.Enabled = _view.CanUndo;
        _miRedo.Enabled = _view.CanRedo;
        bool sel = _view.HasSelection;
        _miCut.Enabled = sel;
        _miCopy.Enabled = sel;
        try { _miPaste.Enabled = Clipboard.ContainsText(); } catch { _miPaste.Enabled = false; }
    }

    private void Goto(int step)
    {
        _view.GotoMatch(step);
        _hitInfo.Text = _view.MatchInfo;
    }

    // ---------- 右键菜单 ----------
    private void UpdateInstallMenu()
    {
        bool installed = IsContextMenuInstalled();
        _installItem.Text = installed ? "✓ 已安装右键菜单（点击卸载）" : "安装到右键菜单";
    }

    private static bool IsContextMenuInstalled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(ContextKey);
        return key != null;
    }

    private void ToggleContextMenu()
    {
        if (IsContextMenuInstalled()) { UninstallContextMenu(); return; }
        InstallContextMenu();
    }

    private void InstallContextMenu()
    {
        try
        {
            string exe = Application.ExecutablePath;
            using var key = Registry.CurrentUser.CreateSubKey(ContextKey);
            key.SetValue("", "用日志查看器打开");
            key.SetValue("Icon", exe);
            using var cmd = key.CreateSubKey("command");
            cmd.SetValue("", $"\"{exe}\" \"%1\"");
            UpdateInstallMenu();
            MessageBox.Show("已安装到右键菜单。\n现在在任意文件上点右键，选择「用日志查看器打开」即可。",
                "安装成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("安装失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void UninstallContextMenu()
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(ContextKey, throwOnMissingSubKey: false);
            UpdateInstallMenu();
            MessageBox.Show("已从右键菜单移除。", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("卸载失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // ---------- 字体 ----------
    private void ChooseFont()
    {
        using var dlg = new FontDialog
        {
            Font = _fontSettings.CreateFont(),
            ShowEffects = false,
            ShowColor = false
        };
        if (dlg.ShowDialog() != DialogResult.OK) return;
        _fontSettings.Family = dlg.Font.Name;
        _fontSettings.Size = dlg.Font.Size;
        _fontSettings.Style = (int)dlg.Font.Style;
        _view.SetFontStyle(_fontSettings.Family, _fontSettings.Size, (FontStyle)_fontSettings.Style);
        SaveSettings();
    }

    private void RestoreDefaultFont()
    {
        _fontSettings.Family = FontSettings.DefaultFamily;
        _fontSettings.Size = FontSettings.DefaultSize;
        _fontSettings.Style = (int)FontStyle.Regular;
        _view.SetFontStyle(_fontSettings.Family, _fontSettings.Size, FontStyle.Regular);
        SaveSettings();
    }

    // ---------- 主题与设置 ----------
    private void SetTheme(Theme theme)
    {
        _theme = theme;
        ApplyTheme();
        SaveSettings();
    }

    private void ApplyTheme()
    {
        BackColor = _theme.C(_theme.Bg);
        ForeColor = _theme.C(_theme.Fg);
        _view.SetTheme(_theme);
        _errTip.ForeColor = _theme.C(_theme.Error);
        _stAuthor.LinkColor = _theme.C(_theme.Accent);

        // 切换主题时重建三个条的渲染器
        _menu.Renderer = new ThemeRenderer(_theme);
        _tool.Renderer = new ThemeRenderer(_theme);
        _status.Renderer = new ThemeRenderer(_theme);
        _search.BackColor = _theme.C(_theme.Bg);
        _search.ForeColor = _theme.C(_theme.Fg);
        _activity.SetTheme(_theme);
        _tabs.SetTheme(_theme);
        _editorHost.BackColor = _theme.C(_theme.Bg);
        ApplyRunTheme();

        // 让标题栏也跟随主题（Windows 10/11）：用活动栏色，接近 VS Code 标题栏
        try
        {
            Color c = _theme.C(_theme.ActivityBg);
            int color = c.R | (c.G << 8) | (c.B << 16); // COLORREF 是 BGR
            DwmSetWindowAttribute(Handle, 35, ref color, sizeof(int)); // DWMWA_CAPTION_COLOR
            DwmSetWindowAttribute(Handle, 34, ref color, sizeof(int)); // DWMWA_BORDER_COLOR
            int dark = IsDarkTheme() ? 1 : 0;
            DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int));  // DWMWA_USE_IMMERSIVE_DARK_MODE
        }
        catch { /* 旧系统不支持就保持默认标题栏 */ }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    private bool IsDarkTheme() => _theme.Dark;

    private void LoadSettings()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return;
            using var doc = JsonDocument.Parse(File.ReadAllText(SettingsPath));
            var root = doc.RootElement;
            string S(string name, string fallback) =>
                root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : fallback;
            bool B(string name, bool fallback) =>
                root.TryGetProperty(name, out var v) && (v.ValueKind == JsonValueKind.True || v.ValueKind == JsonValueKind.False)
                    ? v.GetBoolean() : fallback;
            float F(string name, float fallback) =>
                root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetSingle(out var f)
                    ? f : fallback;
            int N(string name, int fallback) =>
                root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i)
                    ? i : fallback;

            _theme = Theme.Find(S("theme", _theme.Name));
            _level = S("level", _level);
            _mode = S("mode", _mode);
            _encoding = S("encoding", _encoding);
            _caseSensitive = B("case", _caseSensitive);
            _regexMode = B("regex", _regexMode);
            _follow = B("follow", _follow);
            _fontSettings.Family = S("fontFamily", _fontSettings.Family);
            _fontSettings.Size = F("fontSize", _fontSettings.Size);
            _fontSettings.Style = N("fontStyle", _fontSettings.Style);
            _pythonPath = S("pythonPath", _pythonPath);
            _compilerPath = S("compilerPath", _compilerPath);
            string lang = S("runLanguage", _runLanguage);
            _runLanguage = lang is "Python" or "C++" ? lang : "自动";
        }
        catch { /* 设置损坏就用默认值 */ }
    }

    private void SaveSettings()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            var data = new
            {
                theme = _theme.Name, level = _level, mode = _mode, encoding = _encoding,
                @case = _caseSensitive, regex = _regexMode, follow = _follow,
                fontFamily = _fontSettings.Family, fontSize = _fontSettings.Size, fontStyle = _fontSettings.Style,
                pythonPath = _pythonPath, compilerPath = _compilerPath, runLanguage = _runLanguage
            };
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(data));
        }
        catch { /* 写不进设置也无所谓 */ }
    }

    private string ResolveMode()
    {
        if (_mode == "日志") return "log";
        if (_mode == "代码") return "code";
        string sample = string.Join('\n', _lines.Take(80));
        return Regex.Matches(sample, @"\b(FATAL|ERROR|WARN|WARNING|INFO|DEBUG|TRACE)\b").Count >= 3 ? "log" : "code";
    }

    private readonly List<string> _lines = new();

    private void ApplyFilter()
    {
        Regex? re = null;
        string q = _search.Text ?? "";
        _errTip.Text = "";
        if (!string.IsNullOrEmpty(q))
        {
            string pattern = _regexMode ? q : Regex.Escape(q);
            try { re = new Regex(pattern, _caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase); }
            catch (Exception ex) { _errTip.Text = "正则无效: " + ex.Message; return; }
        }

        _view.SetSource(_lines);
        _view.TagText = _level;
        _view.ApplyFilter(re, ResolveMode());
        _hitInfo.Text = _view.MatchInfo;
        _stLines.Text = _lines.Count.ToString("N0") + " 行";
        _stShow.Text = "显示 " + _view.ViewCount.ToString("N0") + " 行";
    }

    private void OpenFile()
    {
        using var dlg = new OpenFileDialog
        {
            Title = "打开日志或代码文件",
            Filter = "日志/代码文件|*.log;*.txt;*.out;*.json;*.xml;*.yml;*.yaml;*.cs;*.js;*.ts;*.py;*.java;*.c;*.cpp;*.h;*.css;*.html;*.md|所有文件|*.*"
        };
        if (dlg.ShowDialog() == DialogResult.OK && ConfirmDiscard()) LoadFile(dlg.FileName);
    }

    private Encoding GetEncoding() => _encoding == "GBK"
        ? Encoding.GetEncoding("GBK", EncoderFallback.ReplacementFallback, DecoderFallback.ReplacementFallback)
        : new UTF8Encoding(false, false);

    // 去掉 UTF-8 BOM
    private static byte[] StripBom(byte[] data) =>
        data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF ? data[3..] : data;

    private static bool IsCont(byte b) => b >= 0x80 && b <= 0xBF;

    // 混合编码智能解码：整段能严格按 UTF-8 解码就直接用；否则逐字节判断，
    // 3/4 字节 UTF-8 序列按 UTF-8 解，其余非 ASCII 字节按 GBK 解。
    // 这样同一个文件里 UTF-8 中文与 GBK 中文混排也能全部正确显示。
    private static string DecodeMixed(byte[] data)
    {
        data = StripBom(data);
        // 快速路径：整段合法 UTF-8
        try { return new UTF8Encoding(false, true).GetString(data); }
        catch (DecoderFallbackException) { }

        var utf8 = new UTF8Encoding(false, false);
        var gbk = Encoding.GetEncoding("GBK", EncoderFallback.ReplacementFallback, DecoderFallback.ReplacementFallback);
        var sb = new StringBuilder(data.Length);
        int i = 0;
        while (i < data.Length)
        {
            byte b = data[i];
            int seq = 0;
            // 4 字节 UTF-8: F0- F4 + 3 个续字节（含首续字节范围限制）
            if (b >= 0xF0 && b <= 0xF4 && i + 3 < data.Length && IsCont(data[i + 1]) && IsCont(data[i + 2]) && IsCont(data[i + 3])
                && !(b == 0xF0 && data[i + 1] < 0x90) && !(b == 0xF4 && data[i + 1] > 0x8F)) seq = 4;
            // 3 字节 UTF-8: E0-EF + 2 个续字节（含首续字节范围限制）
            else if (b >= 0xE0 && b <= 0xEF && i + 2 < data.Length && IsCont(data[i + 1]) && IsCont(data[i + 2])
                && !(b == 0xE0 && data[i + 1] < 0xA0) && !(b == 0xED && data[i + 1] > 0x9F)) seq = 3;

            if (seq > 0) { sb.Append(utf8.GetString(data, i, seq)); i += seq; continue; }

            // 其余非 ASCII 字节按 GBK 取 1~2 字节解码
            if (b < 0x80) { sb.Append((char)b); i++; continue; }
            int n = (b >= 0x81 && b <= 0xFE && i + 1 < data.Length && data[i + 1] >= 0x40 && data[i + 1] <= 0xFE && data[i + 1] != 0x7F) ? 2 : 1;
            sb.Append(gbk.GetString(data, i, n));
            i += n;
        }
        return sb.ToString();
    }

    private void LoadFile(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (info.Length > 200L * 1024 * 1024 &&
                MessageBox.Show($"文件 {info.Length / 1048576} MB，可能占用较多内存。仍要打开吗？", "大文件提示",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var ms = new MemoryStream();
            fs.CopyTo(ms);
            byte[] raw = ms.ToArray();
            string text = _encoding == "自动"
                ? DecodeMixed(raw)
                : GetEncoding().GetString(StripBom(raw));
            // NUL 字节会让 GDI 绘制中止该行后续字符，替换为空格
            text = text.Replace('\0', ' ');
            _lines.Clear();
            _lines.AddRange(text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None));
            _path = path;
            _lastLength = info.Length;
            _stFile.Text = info.Name;
            _stSize.Text = FormatSize(info.Length);
            ApplyFilter();
            _view.ClearHistory();
            UpdateTitle();
        }
        catch (Exception ex) { _errTip.Text = "打开失败: " + ex.Message; }
    }

    private void Reload()
    {
        if (_path != null && ConfirmDiscard()) LoadFile(_path);
        else if (_path == null) _errTip.Text = "演示数据无需重载";
    }

    private void CheckFileUpdate()
    {
        if (!_follow || _path == null || _view.Modified) return;
        try { if (new FileInfo(_path).Length != _lastLength) LoadFile(_path); }
        catch { /* 文件可能被临时占用，忽略 */ }
    }

    private static string FormatSize(long n) => n < 1024 ? n + " B"
        : n < 1048576 ? (n / 1024.0).ToString("F1") + " KB"
        : (n / 1048576.0).ToString("F2") + " MB";

    private void LoadDemo()
    {
        string[] levels = { "INFO", "INFO", "DEBUG", "INFO", "WARN", "INFO", "DEBUG", "ERROR", "INFO", "TRACE" };
        string[] msgs =
        {
            "Server started on port 8080", "Accepted connection from 10.0.0.{0}",
            "Cache hit ratio: 0.{0}", "Processing request id=req_{0}",
            "Database query finished in {0}ms", "User session refreshed",
            "Retrying upstream call (attempt {0})", "Config reloaded successfully",
            "Disk usage at {0}%", "Health check passed"
        };
        DateTime baseTime = DateTime.Now.AddMinutes(-5);
        for (int i = 0; i < 8000; i++)
        {
            string lv = levels[i % levels.Length];
            string ts = baseTime.AddMilliseconds(i * 40).ToString("yyyy-MM-dd HH:mm:ss.fff");
            string msg = string.Format(msgs[i % msgs.Length], i % 997 + 1);
            _lines.Add($"{ts} [{lv}] [main] {msg}");
        }
        ApplyFilter();
        _view.ClearHistory();
    }
}
