using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace NativeLogViewer;

/// <summary>
/// 简易运行面板：一键运行当前 Python / C++ 代码，捕获 stdout/stderr 显示到底部输出窗口。
/// 不做断点单步，只是「运行看输出」。
/// </summary>
public partial class Form1
{
    private Panel? _outputPanel;
    private RichTextBox? _output;
    private ThemedScrollBar? _outputScroll;
    private bool _outputSyncing;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint { public int X; public int Y; }

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, ref NativePoint lParam);

    private const int EM_GETSCROLLPOS = 0x04DD;
    private const int EM_SETSCROLLPOS = 0x04DE;
    private readonly ToolStripStatusLabel _runState = new("运行: 就绪");
    private Process? _runProcess;
    private string _runLanguage = "自动";
    private readonly List<ToolStripMenuItem> _runLangMenu = new();
    private ToolStripMenuItem? _miRun, _miStop, _miClear;

    private static readonly string RunTempDir = Path.Combine(Path.GetTempPath(), "LiteViewerRun");

    // ---------- 构建 UI ----------
    private ToolStripMenuItem BuildRunMenu()
    {
        var mRun = new ToolStripMenuItem("运行(_R)");

        _miRun = NewMenu("运行(F5)", Keys.F5, (_, _) => RunCode());
        _miStop = NewMenu("停止", Keys.None, (_, _) => StopRun());
        _miClear = NewMenu("清空输出", Keys.None, (_, _) => ClearOutput());
        mRun.DropDownItems.Add(_miRun);
        mRun.DropDownItems.Add(_miStop);
        mRun.DropDownItems.Add(_miClear);
        mRun.DropDownItems.Add(new ToolStripSeparator());

        foreach (var v in new[] { "自动", "Python", "C++" })
        {
            var item = new ToolStripMenuItem(v) { CheckOnClick = true, Checked = v == _runLanguage };
            item.Click += (_, _) =>
            {
                foreach (var sib in _runLangMenu) sib.Checked = false;
                item.Checked = true;
                _runLanguage = v;
                SaveSettings();
            };
            _runLangMenu.Add(item);
            mRun.DropDownItems.Add(item);
        }
        mRun.DropDownItems.Add(new ToolStripSeparator());
        mRun.DropDownItems.Add(NewMenu("运行工具设置...", Keys.None, (_, _) => ShowRunnerSettings()));

        return mRun;
    }

    private void BuildOutputPanel()
    {
        _outputPanel = new Panel { Dock = DockStyle.Bottom, Height = 190, Visible = false };

        var bar = new ToolStrip
        {
            Dock = DockStyle.Top,
            GripStyle = ToolStripGripStyle.Hidden,
            Padding = new Padding(6, 1, 6, 1),
            Renderer = new ThemeRenderer(_theme)
        };
        bar.Items.Add(new ToolStripLabel("输出"));
        bar.Items.Add(new ToolStripButton("运行 F5") { DisplayStyle = ToolStripItemDisplayStyle.Text });
        bar.Items[1].Click += (_, _) => RunCode();
        bar.Items.Add(new ToolStripButton("停止") { DisplayStyle = ToolStripItemDisplayStyle.Text });
        bar.Items[2].Click += (_, _) => StopRun();
        bar.Items.Add(new ToolStripButton("清空") { DisplayStyle = ToolStripItemDisplayStyle.Text });
        bar.Items[3].Click += (_, _) => ClearOutput();
        bar.Items.Add(new ToolStripButton("关闭") { DisplayStyle = ToolStripItemDisplayStyle.Text, Alignment = ToolStripItemAlignment.Right });
        bar.Items[4].Click += (_, _) => HideOutput();
        _outputBar = bar;

        _output = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            WordWrap = false,
            ScrollBars = RichTextBoxScrollBars.None,
            BorderStyle = BorderStyle.None,
            Font = new Font(FontSettings.DefaultFamily, 10f),
            BackColor = _theme.C(_theme.Bg),
            ForeColor = _theme.C(_theme.Fg),
            Padding = new Padding(6, 4, 6, 4)
        };
        _outputScroll = new ThemedScrollBar(vertical: true);
        _outputScroll.SetTheme(_theme);
        _outputScroll.ValueChanged += (_, _) =>
        {
            if (_outputSyncing || _output == null || !_output.IsHandleCreated) return;
            _outputSyncing = true;
            try
            {
                var pt = new NativePoint { X = 0, Y = _outputScroll.Value };
                SendMessage(_output.Handle, EM_SETSCROLLPOS, IntPtr.Zero, ref pt);
            }
            finally { _outputSyncing = false; }
        };
        _output.VScroll += (_, _) => SyncOutputScrollFromRich();
        _output.TextChanged += (_, _) => { if (!_outputSyncing) UpdateOutputScrollRange(); };
        _output.Resize += (_, _) => UpdateOutputScrollRange();

        _outputPanel.Controls.Add(_output);
        _outputPanel.Controls.Add(_outputScroll);
        _outputPanel.Controls.Add(bar);
    }

    // 依据 RichTextBox 内容高度/可视高度更新自绘滚动条范围
    private void UpdateOutputScrollRange()
    {
        if (_output == null || _outputScroll == null || !_output.IsHandleCreated) return;
        var pt = new NativePoint();
        SendMessage(_output.Handle, EM_GETSCROLLPOS, IntPtr.Zero, ref pt);
        int cur = pt.Y;
        // 临时移到底部读取最大滚动量，再还原
        var bottom = new NativePoint { X = 0, Y = int.MaxValue };
        SendMessage(_output.Handle, EM_SETSCROLLPOS, IntPtr.Zero, ref bottom);
        var atBottom = new NativePoint();
        SendMessage(_output.Handle, EM_GETSCROLLPOS, IntPtr.Zero, ref atBottom);
        int maxY = atBottom.Y;
        var restore = new NativePoint { X = 0, Y = cur };
        SendMessage(_output.Handle, EM_SETSCROLLPOS, IntPtr.Zero, ref restore);

        _outputSyncing = true;
        try
        {
            _outputScroll.Maximum = Math.Max(0, maxY);
            _outputScroll.LargeChange = Math.Max(1, _output.ClientSize.Height);
            _outputScroll.SmallChange = Math.Max(1, _output.Font.Height * 3);
            _outputScroll.Value = Math.Clamp(cur, 0, _outputScroll.MaxValue);
        }
        finally { _outputSyncing = false; }
    }

    // RichTextBox 自身滚动（键盘/滚轮）时反向同步自绘滚动条
    private void SyncOutputScrollFromRich()
    {
        if (_output == null || _outputScroll == null || _outputSyncing || !_output.IsHandleCreated) return;
        var pt = new NativePoint();
        SendMessage(_output.Handle, EM_GETSCROLLPOS, IntPtr.Zero, ref pt);
        _outputSyncing = true;
        try { _outputScroll.Value = Math.Clamp(pt.Y, 0, _outputScroll.MaxValue); }
        finally { _outputSyncing = false; }
    }

    private ToolStrip _outputBar = new();

    private void ApplyRunTheme()
    {
        if (_output == null) return;
        _output.BackColor = _theme.C(_theme.Bg);
        _output.ForeColor = _theme.C(_theme.Fg);
        _outputScroll?.SetTheme(_theme);
        _outputBar.Renderer = new ThemeRenderer(_theme);
        _runState.ForeColor = (_runProcess != null && !_runProcess.HasExited)
            ? _theme.C(_theme.Info) : _theme.C(_theme.LineNo);
    }

    // ---------- 运行主流程 ----------
    private void ShowOutput()
    {
        if (_outputPanel == null) return;
        _outputPanel.Visible = true;
        _outputPanel.BringToFront();
    }

    private void HideOutput()
    {
        if (_outputPanel != null) _outputPanel.Visible = false;
    }

    private void ClearOutput()
    {
        if (_output != null) _output.Clear();
    }

    private void AppendLine(string text, bool error = false)
    {
        if (_output == null) return;
        void Do()
        {
            if (_output.IsDisposed) return;
            _output.SelectionStart = _output.TextLength;
            _output.SelectionLength = 0;
            _output.SelectionColor = error ? _theme.C(_theme.Error) : _theme.C(_theme.Fg);
            _output.AppendText(text + "\n");
            _output.SelectionColor = _theme.C(_theme.Fg);
            _output.ScrollToCaret();
        }
        // 统一投递到 UI 消息队列：事件回调（线程池）与 await 后续（UI 线程）
        // 混用同步/异步写会导致输出乱序甚至丢失，全部走 BeginInvoke 保证 FIFO。
        try { _output.BeginInvoke(Do); } catch { }
    }

    private async void RunCode()
    {
        if (_runProcess != null && !_runProcess.HasExited) StopRun();

        string? lang = ResolveRunLanguage();
        if (lang == null) return;

        string? source = PrepareSource(lang, out string workDir);
        if (source == null) return;

        ShowOutput();
        ClearOutput();
        if (lang == "Python")
            await RunPythonAsync(source, workDir);
        else
            await RunCppAsync(source, workDir);
    }

    private string? ResolveRunLanguage()
    {
        if (_runLanguage != "自动") return _runLanguage;
        string ext = _path != null ? Path.GetExtension(_path).ToLowerInvariant() : "";
        if (ext == ".py") return "Python";
        if (ext is ".c" or ".cc" or ".cpp" or ".cxx" or ".h" or ".hpp") return "C++";
        var r = MessageBox.Show(
            "无法根据文件扩展名判断语言。\n\n是 = 按 Python 运行\n否 = 按 C++ 运行\n取消 = 不运行",
            "选择运行语言", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
        return r == DialogResult.Yes ? "Python" : r == DialogResult.No ? "C++" : null;
    }

    // 已保存且未修改则直接运行磁盘文件；否则写临时文件，避免改动原文件
    private string? PrepareSource(string lang, out string workDir)
    {
        workDir = Path.GetTempPath();
        if (_path != null && !_view.Modified && File.Exists(_path))
        {
            workDir = Path.GetDirectoryName(_path) ?? Path.GetTempPath();
            return _path;
        }

        try
        {
            Directory.CreateDirectory(RunTempDir);
            string ext = lang == "Python" ? ".py" : ".cpp";
            string name = (_path != null ? Path.GetFileNameWithoutExtension(_path) : "run") + ext;
            string temp = Path.Combine(RunTempDir, name);
            File.WriteAllText(temp, string.Join("\n", _view.GetLines()), new UTF8Encoding(false));
            workDir = RunTempDir;
            return temp;
        }
        catch (Exception ex)
        {
            AppendLine("准备源文件失败: " + ex.Message, true);
            return null;
        }
    }

    private async Task RunPythonAsync(string source, string workDir)
    {
        string? exe = PythonPath();
        if (string.IsNullOrWhiteSpace(exe) || !File.Exists(exe))
        {
            AppendLine("未找到 Python 解释器。请在「运行 → 运行工具设置...」中指定 python.exe 路径。", true);
            return;
        }
        var psi = new ProcessStartInfo(exe)
        {
            WorkingDirectory = workDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        psi.ArgumentList.Add(source);
        psi.Environment["PYTHONIOENCODING"] = "utf-8";
        await RunProcessAsync(psi, "Python");
    }

    private async Task RunCppAsync(string source, string workDir)
    {
        string? compiler = CompilerPath();
        if (string.IsNullOrWhiteSpace(compiler) || !File.Exists(compiler))
        {
            AppendLine("未找到 C++ 编译器。请安装 MinGW-w64 / LLVM 并在「运行 → 运行工具设置...」中指定 g++.exe 路径。", true);
            return;
        }

        Directory.CreateDirectory(RunTempDir);
        string exePath = Path.Combine(RunTempDir, "liteviewer_" + Guid.NewGuid().ToString("N") + ".exe");
        string name = Path.GetFileName(compiler).ToLowerInvariant();
        var compile = new ProcessStartInfo(compiler)
        {
            WorkingDirectory = RunTempDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        bool isMsvc = name is "cl.exe";
        if (isMsvc)
        {
            compile.ArgumentList.Add("/nologo");
            compile.ArgumentList.Add("/EHsc");
            compile.ArgumentList.Add("/Od");
            compile.ArgumentList.Add("/Zi");
            compile.ArgumentList.Add("/Fe:" + exePath);
            compile.ArgumentList.Add(source);
        }
        else
        {
            compile.ArgumentList.Add("-std=c++17");
            compile.ArgumentList.Add("-O0");
            compile.ArgumentList.Add("-g");
            // 静态链接运行库：MinGW/LLVM 编译出的 exe 否则依赖 libstdc++-6.dll /
            // libgcc_s_seh-1.dll / libwinpthread-1.dll，脱离编译器目录就报 0xC0000135。
            compile.ArgumentList.Add("-static-libgcc");
            compile.ArgumentList.Add("-static-libstdc++");
            compile.ArgumentList.Add("-static");
            compile.ArgumentList.Add(source);
            compile.ArgumentList.Add("-o");
            compile.ArgumentList.Add(exePath);
        }

        AppendLine("--- 编译中 ---");
        var (ok, _) = await RunProcessAsync(compile, "编译", updateState: false);
        if (!ok || !File.Exists(exePath))
        {
            AppendLine("编译失败，未运行程序。", true);
            return;
        }

        var run = new ProcessStartInfo(exePath)
        {
            WorkingDirectory = workDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        // 双保险：把编译器目录临时加入 PATH。静态链接已解决大多数情况，
        // 但个别编译器（如 clang 的运行时）仍可能需要，加上不影响其他环境。
        string? compilerDir = Path.GetDirectoryName(compiler);
        if (!string.IsNullOrEmpty(compilerDir))
        {
            string oldPath = Environment.GetEnvironmentVariable("PATH") ?? "";
            Environment.SetEnvironmentVariable("PATH", compilerDir + Path.PathSeparator + oldPath);
            try { await RunProcessAsync(run, "程序"); }
            finally { Environment.SetEnvironmentVariable("PATH", oldPath); }
        }
        else
        {
            await RunProcessAsync(run, "程序");
        }
    }

    private async Task<(bool ok, int code)> RunProcessAsync(ProcessStartInfo psi, string label, bool updateState = true)
    {
        var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
        proc.OutputDataReceived += (_, e) => { if (e.Data != null) AppendLine(e.Data); };
        proc.ErrorDataReceived += (_, e) => { if (e.Data != null) AppendLine(e.Data, true); };

        _runProcess = proc;
        if (updateState) SetRunState("运行中", true);
        var sw = Stopwatch.StartNew();
        try
        {
            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            await proc.WaitForExitAsync();
            proc.WaitForExit(); // 确保异步输出冲刷完
            sw.Stop();
            AppendLine($"--- {label}结束，退出码 {proc.ExitCode}，耗时 {sw.ElapsedMilliseconds} ms ---");
            return (proc.ExitCode == 0, proc.ExitCode);
        }
        catch (Exception ex)
        {
            sw.Stop();
            AppendLine($"启动{label}失败: " + ex.Message, true);
            return (false, -1);
        }
        finally
        {
            _runProcess = null;
            if (updateState) SetRunState("就绪", false);
        }
    }

    private void StopRun()
    {
        var p = _runProcess;
        if (p == null || p.HasExited) return;
        try { p.Kill(entireProcessTree: true); AppendLine("--- 已停止 ---", true); }
        catch (Exception ex) { AppendLine("停止失败: " + ex.Message, true); }
    }

    private void SetRunState(string text, bool running)
    {
        _runState.Text = "运行: " + text;
        _runState.ForeColor = running ? _theme.C(_theme.Info) : _theme.C(_theme.LineNo);
    }

    // ---------- 工具路径 ----------
    private string PythonPath()
    {
        if (!string.IsNullOrWhiteSpace(_pythonPath) && File.Exists(_pythonPath)) return _pythonPath;
        return FindOnPath("python.exe", "python3.exe", "py.exe") ?? "";
    }

    private string CompilerPath()
    {
        if (!string.IsNullOrWhiteSpace(_compilerPath) && File.Exists(_compilerPath)) return _compilerPath;
        return FindOnPath("g++.exe", "clang++.exe", "cl.exe") ?? "";
    }

    private static string? FindOnPath(params string[] names)
    {
        string? path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path)) return null;
        foreach (string dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (string name in names)
            {
                try
                {
                    string full = Path.Combine(dir.Trim('"'), name);
                    if (File.Exists(full)) return full;
                }
                catch { }
            }
        }
        return null;
    }

    private void ShowRunnerSettings()
    {
        using var dlg = new Form
        {
            Text = "运行工具设置",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(520, 180),
            MaximizeBox = false, MinimizeBox = false, ShowInTaskbar = false
        };
        var lblPy = new Label { Text = "Python 解释器:", Location = new Point(16, 20), AutoSize = true };
        var tbPy = new TextBox { Text = _pythonPath, Location = new Point(120, 16), Size = new Size(330, 24) };
        var btnPy = new Button { Text = "浏览...", Location = new Point(456, 15), Size = new Size(50, 26) };
        btnPy.Click += (_, _) =>
        {
            using var f = new OpenFileDialog { Filter = "python.exe|python.exe|所有文件|*.*", FileName = tbPy.Text };
            if (f.ShowDialog() == DialogResult.OK) tbPy.Text = f.FileName;
        };

        var lblCpp = new Label { Text = "C++ 编译器:", Location = new Point(16, 60), AutoSize = true };
        var tbCpp = new TextBox { Text = _compilerPath, Location = new Point(120, 56), Size = new Size(330, 24) };
        var btnCpp = new Button { Text = "浏览...", Location = new Point(456, 55), Size = new Size(50, 26) };
        btnCpp.Click += (_, _) =>
        {
            using var f = new OpenFileDialog { Filter = "编译器|g++.exe;clang++.exe;cl.exe|所有文件|*.*", FileName = tbCpp.Text };
            if (f.ShowDialog() == DialogResult.OK) tbCpp.Text = f.FileName;
        };

        var hint = new Label
        {
            Text = "留空则自动从 PATH 探测。C++ 推荐 MinGW-w64 的 g++.exe。",
            Location = new Point(16, 96), AutoSize = true, ForeColor = Color.Gray
        };
        var ok = new Button { Text = "确定", DialogResult = DialogResult.OK, Location = new Point(348, 138), Size = new Size(76, 28) };
        var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Location = new Point(432, 138), Size = new Size(76, 28) };
        dlg.Controls.AddRange(new Control[] { lblPy, tbPy, btnPy, lblCpp, tbCpp, btnCpp, hint, ok, cancel });
        dlg.AcceptButton = ok; dlg.CancelButton = cancel;
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        _pythonPath = tbPy.Text.Trim();
        _compilerPath = tbCpp.Text.Trim();
        SaveSettings();
    }
}
