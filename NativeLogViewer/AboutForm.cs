using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace NativeLogViewer;

/// <summary>
/// 关于 / 署名窗口。展示作者署名、开源仓库链接与版本信息，配色跟随当前主题。
/// </summary>
internal sealed class AboutForm : Form
{
    private const string Author = "alxendentaus";
    private const string RepoUrl = "https://github.com/alxendentaus";
    private const string RepoDisplay = "github.com/alxendentaus";

    public AboutForm(Theme theme)
    {
        Color bg = theme.C(theme.Bg);
        Color fg = theme.C(theme.Fg);
        Color muted = theme.C(theme.LineNo);
        Color accent = theme.C(theme.Accent);

        Text = "关于 LiteViewer";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(430, 250);
        BackColor = bg;
        ForeColor = fg;
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

        // 应用名
        var title = new Label
        {
            Text = "LiteViewer  ·  轻量日志查看器",
            Font = new Font("Segoe UI", 16f, FontStyle.Bold),
            ForeColor = fg,
            AutoSize = true,
            Location = new Point(28, 26)
        };
        Controls.Add(title);

        var version = typeof(AboutForm).Assembly.GetName().Version;
        string ver = version == null ? "1.1.0" : $"{version.Major}.{version.Minor}.{version.Build}";
        var sub = new Label
        {
            Text = $"版本 {ver}   ·   自绘虚拟滚动，支持大文件 / 多主题 / 代码运行",
            Font = new Font("Segoe UI", 9f),
            ForeColor = muted,
            AutoSize = true,
            Location = new Point(30, 64)
        };
        Controls.Add(sub);

        // 分隔线
        var line = new Panel
        {
            Location = new Point(28, 92),
            Size = new Size(374, 1),
            BackColor = theme.C(theme.Border)
        };
        Controls.Add(line);

        // 署名
        var authorLabel = new Label
        {
            Text = "作者 / 署名",
            Font = new Font("Segoe UI", 9f),
            ForeColor = muted,
            AutoSize = true,
            Location = new Point(30, 112)
        };
        Controls.Add(authorLabel);

        var authorName = new Label
        {
            Text = Author,
            Font = new Font("Consolas", 15f, FontStyle.Bold),
            ForeColor = accent,
            AutoSize = true,
            Location = new Point(30, 132),
            Cursor = Cursors.Hand
        };
        authorName.Click += (_, _) => OpenUrl();
        Controls.Add(authorName);

        // 仓库链接
        var repoLabel = new Label
        {
            Text = "开源仓库",
            Font = new Font("Segoe UI", 9f),
            ForeColor = muted,
            AutoSize = true,
            Location = new Point(30, 168)
        };
        Controls.Add(repoLabel);

        var link = new LinkLabel
        {
            Text = RepoDisplay,
            AutoSize = true,
            Location = new Point(30, 188),
            Font = new Font("Segoe UI", 10f),
            LinkColor = accent,
            ActiveLinkColor = theme.C(theme.Error),
            VisitedLinkColor = accent
        };
        link.LinkClicked += (_, _) => OpenUrl();
        Controls.Add(link);

        // 关闭按钮
        var btn = new Button
        {
            Text = "关闭",
            DialogResult = DialogResult.OK,
            Size = new Size(90, 30),
            Location = new Point(312, 196),
            BackColor = theme.C(theme.ChromeBg),
            ForeColor = fg,
            FlatStyle = FlatStyle.Flat
        };
        btn.FlatAppearance.BorderColor = theme.C(theme.Border);
        Controls.Add(btn);

        AcceptButton = btn;
        CancelButton = btn;
        KeyPreview = true;
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) Close(); };

        // 标题栏跟随主题（Windows 10/11）
        Load += (_, _) =>
        {
            try
            {
                Color cap = theme.C(theme.ActivityBg);
                int color = cap.R | (cap.G << 8) | (cap.B << 16); // COLORREF = BGR
                DwmSetWindowAttribute(Handle, 35, ref color, sizeof(int));       // DWMWA_CAPTION_COLOR
                int dark = theme.Dark ? 1 : 0;
                DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int));        // DWMWA_USE_IMMERSIVE_DARK_MODE
            }
            catch { /* 旧系统不支持则保持默认标题栏 */ }
        };
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    private static void OpenUrl()
    {
        try
        {
            Process.Start(new ProcessStartInfo(RepoUrl) { UseShellExecute = true });
        }
        catch
        {
            Clipboard.SetText(RepoUrl);
            MessageBox.Show("已复制仓库地址到剪贴板：" + RepoUrl, "打开链接", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
