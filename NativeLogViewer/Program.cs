using System.Text;

namespace NativeLogViewer;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        // 记录未处理异常到文件，方便排查
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            try { File.WriteAllText(Path.Combine(Path.GetTempPath(), "liteviewer-crash.txt"), e.ExceptionObject.ToString()); }
            catch { }
        };
        Application.ThreadException += (_, e) =>
        {
            try { File.WriteAllText(Path.Combine(Path.GetTempPath(), "liteviewer-crash.txt"), e.Exception.ToString()); }
            catch { }
        };

        string? openPath = args.Length > 0 && File.Exists(args[0]) ? args[0] : null;
        Application.Run(new Form1(openPath));
    }
}
