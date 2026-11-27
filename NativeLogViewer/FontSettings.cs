namespace NativeLogViewer;

/// <summary>日志区字体设置，默认 Consolas 12 常规。</summary>
internal sealed class FontSettings
{
    public const string DefaultFamily = "Consolas";
    public const float DefaultSize = 12f;

    public string Family { get; set; } = DefaultFamily;
    public float Size { get; set; } = DefaultSize;
    public int Style { get; set; } = (int)FontStyle.Regular;

    public Font CreateFont()
    {
        try { return new Font(Family, Size, (FontStyle)Style); }
        catch { return new Font(DefaultFamily, DefaultSize); }
    }
}
