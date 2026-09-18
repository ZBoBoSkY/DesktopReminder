using System.Windows.Media;

namespace DesktopReminder.App.Themes;

/// <summary>
/// 一套主题的全部颜色（冻结需求 R5：背景/文字/边框/强调/状态色全套同步变化）。
/// 所有界面颜色只从这里取，禁止在 XAML 里硬编码颜色。
/// </summary>
public sealed class ThemeColors
{
    public required Color BackgroundColor { get; init; }
    public required Color SurfaceColor { get; init; }
    public required Color ForegroundColor { get; init; }
    public required Color SecondaryTextColor { get; init; }
    public required Color BorderColor { get; init; }
    public required Color AccentColor { get; init; }
    public required Color DueColor { get; init; }
    public required Color ActiveColor { get; init; }

    public Brush Background => new SolidColorBrush(BackgroundColor);
    public Brush Surface => new SolidColorBrush(SurfaceColor);
    public Brush Foreground => new SolidColorBrush(ForegroundColor);
    public Brush SecondaryText => new SolidColorBrush(SecondaryTextColor);
    public Brush Border => new SolidColorBrush(BorderColor);
    public Brush Accent => new SolidColorBrush(AccentColor);
    public Brush Due => new SolidColorBrush(DueColor);
    public Brush Active => new SolidColorBrush(ActiveColor);

    public static ThemeColors Light { get; } = new()
    {
        BackgroundColor = Color.FromRgb(0xF7, 0xF8, 0xFA),
        SurfaceColor = Color.FromRgb(0xFF, 0xFF, 0xFF),
        ForegroundColor = Color.FromRgb(0x1F, 0x29, 0x37),
        SecondaryTextColor = Color.FromRgb(0x6B, 0x72, 0x80),
        BorderColor = Color.FromRgb(0xE5, 0xE7, 0xEB),
        AccentColor = Color.FromRgb(0x08, 0x91, 0xB2),
        DueColor = Color.FromRgb(0xDC, 0x26, 0x26),
        ActiveColor = Color.FromRgb(0x16, 0xA3, 0x4A),
    };

    public static ThemeColors Dark { get; } = new()
    {
        BackgroundColor = Color.FromRgb(0x1E, 0x1E, 0x1E),
        SurfaceColor = Color.FromRgb(0x2D, 0x2D, 0x30),
        ForegroundColor = Color.FromRgb(0xF0, 0xF0, 0xF0),
        SecondaryTextColor = Color.FromRgb(0x9D, 0xA5, 0xAF),
        BorderColor = Color.FromRgb(0x3F, 0x3F, 0x46),
        AccentColor = Color.FromRgb(0x22, 0xD3, 0xEE),
        DueColor = Color.FromRgb(0xFF, 0x6B, 0x6B),
        ActiveColor = Color.FromRgb(0x4A, 0xDE, 0x80),
    };
}
