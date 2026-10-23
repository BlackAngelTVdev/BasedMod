namespace ItemAdderLauncher.UI;

internal static class Theme
{
    private static readonly Dictionary<float, Font> IconFontCache = new();

    public static readonly Color Background = Color.FromArgb(11, 16, 24);
    public static readonly Color TitleBar = Color.FromArgb(8, 8, 16);
    public static readonly Color Surface = Color.FromArgb(19, 25, 36);
    public static readonly Color SurfaceRaised = Color.FromArgb(26, 36, 51);
    public static readonly Color Input = Color.FromArgb(12, 17, 24);
    public static readonly Color Journal = Color.FromArgb(15, 19, 29);
    public static readonly Color Border = Color.FromArgb(32, 48, 72);
    public static readonly Color BorderSoft = Color.FromArgb(30, 41, 59);

    public static readonly Color Text = Color.FromArgb(241, 245, 249);
    public static readonly Color TextMuted = Color.FromArgb(148, 163, 184);
    public static readonly Color TextFaint = Color.FromArgb(106, 121, 145);

    public static readonly Color Accent = Color.FromArgb(245, 158, 11);
    public static readonly Color AccentSoft = Color.FromArgb(74, 53, 20);
    public static readonly Color Green = Color.FromArgb(74, 222, 128);
    public static readonly Color GreenSoft = Color.FromArgb(10, 42, 41);
    public static readonly Color Blue = Color.FromArgb(96, 165, 250);
    public static readonly Color BlueSoft = Color.FromArgb(20, 44, 74);
    public static readonly Color Red = Color.FromArgb(248, 113, 113);
    public static readonly Color RedSoft = Color.FromArgb(58, 26, 26);

    public static Font Ui(float size, FontStyle style = FontStyle.Regular)
    {
        return new Font("Segoe UI", size, style, GraphicsUnit.Point);
    }

    public static Font Icon(float size)
    {
        if (!IconFontCache.TryGetValue(size, out Font? font))
        {
            font = new Font("Segoe MDL2 Assets", size, FontStyle.Regular, GraphicsUnit.Point);
            IconFontCache[size] = font;
        }

        return font;
    }
}
