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
    public required Color HoverColor { get; init; }

    public Brush Background => new SolidColorBrush(BackgroundColor);
    public Brush Surface => new SolidColorBrush(SurfaceColor);
    public Brush Foreground => new SolidColorBrush(ForegroundColor);
    public Brush SecondaryText => new SolidColorBrush(SecondaryTextColor);
    public Brush Border => new SolidColorBrush(BorderColor);
    public Brush Accent => new SolidColorBrush(AccentColor);
    public Brush Due => new SolidColorBrush(DueColor);
    public Brush Active => new SolidColorBrush(ActiveColor);
    public Brush Hover => new SolidColorBrush(HoverColor);

    public static ThemeColors Light { get; } = new()
    {
        BackgroundColor = Color.FromRgb(0xF5, 0xF6, 0xF8),
        SurfaceColor = Color.FromRgb(0xFF, 0xFF, 0xFF),
        ForegroundColor = Color.FromRgb(0x1F, 0x29, 0x37),
        SecondaryTextColor = Color.FromRgb(0x64, 0x6B, 0x78),
        BorderColor = Color.FromRgb(0xE5, 0xE7, 0xEB),
        AccentColor = Color.FromRgb(0x0E, 0x93, 0xB5),
        DueColor = Color.FromRgb(0xDC, 0x26, 0x26),
        ActiveColor = Color.FromRgb(0x16, 0xA3, 0x4A),
        HoverColor = Color.FromArgb(0x0F, 0x00, 0x00, 0x00),
    };

    public static ThemeColors Dark { get; } = new()
    {
        BackgroundColor = Color.FromRgb(0x20, 0x21, 0x24),
        SurfaceColor = Color.FromRgb(0x2B, 0x2C, 0x30),
        ForegroundColor = Color.FromRgb(0xF0, 0xF1, 0xF3),
        SecondaryTextColor = Color.FromRgb(0x9A, 0xA1, 0xAB),
        BorderColor = Color.FromRgb(0x3E, 0x40, 0x46),
        AccentColor = Color.FromRgb(0x3B, 0xD6, 0xF2),
        DueColor = Color.FromRgb(0xFF, 0x6B, 0x6B),
        ActiveColor = Color.FromRgb(0x4A, 0xDE, 0x80),
        HoverColor = Color.FromArgb(0x1F, 0xFF, 0xFF, 0xFF),
    };
}
