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
    public required Color DueSoftColor { get; init; }

    public Brush Background => new SolidColorBrush(BackgroundColor);
    public Brush Surface => new SolidColorBrush(SurfaceColor);
    public Brush Foreground => new SolidColorBrush(ForegroundColor);
    public Brush SecondaryText => new SolidColorBrush(SecondaryTextColor);
    public Brush Border => new SolidColorBrush(BorderColor);
    public Brush Accent => new SolidColorBrush(AccentColor);
    public Brush Due => new SolidColorBrush(DueColor);
    public Brush Active => new SolidColorBrush(ActiveColor);
    public Brush Hover => new SolidColorBrush(HoverColor);
    public Brush DueSoft => new SolidColorBrush(DueSoftColor);

    /// <summary>
    /// Windows 11 Fluent 2 配色（取自 WinUI 3 默认主题资源）：
    /// - 浅色：Mica 浅灰底 #F3F3F3 + 近白卡片，描边 6% 黑
    /// - 深色：#202020 底 + #2C2C2C 卡片，描边 8% 白
    /// - 强调色为 Windows 默认蓝（浅色 AccentDark1 #005FB8 / 深色 AccentLight2 #60CDFF）
    /// - 状态色：成功绿 / 危险红沿用系统 Success / Critical 色
    /// </summary>
    public static ThemeColors Light { get; } = new()
    {
        BackgroundColor = Color.FromRgb(0xF3, 0xF3, 0xF3), // Mica 浅灰底
        SurfaceColor = Color.FromArgb(0xF7, 0xFB, 0xFB, 0xFB), // 卡片（97% 近白）
        ForegroundColor = Color.FromArgb(0xE4, 0x00, 0x00, 0x00), // TextFillColorPrimary
        SecondaryTextColor = Color.FromArgb(0x9E, 0x00, 0x00, 0x00), // TextFillColorSecondary
        BorderColor = Color.FromArgb(0x0F, 0x00, 0x00, 0x00), // CardStroke
        AccentColor = Color.FromRgb(0x00, 0x5F, 0xB8), // Fluent 蓝
        DueColor = Color.FromRgb(0xC4, 0x2B, 0x1C), // SystemFillColorCritical
        ActiveColor = Color.FromRgb(0x0F, 0x7B, 0x0F), // SystemFillColorSuccess
        HoverColor = Color.FromArgb(0x09, 0x00, 0x00, 0x00), // SubtleFillSecondary
        DueSoftColor = Color.FromRgb(0xFD, 0xE7, 0xE9), // CriticalBackground
    };

    public static ThemeColors Dark { get; } = new()
    {
        BackgroundColor = Color.FromRgb(0x20, 0x20, 0x20), // Mica 深色底
        SurfaceColor = Color.FromArgb(0xF7, 0x2C, 0x2C, 0x2C), // 卡片
        ForegroundColor = Color.FromRgb(0xFF, 0xFF, 0xFF), // TextFillColorPrimary
        SecondaryTextColor = Color.FromArgb(0xC5, 0xFF, 0xFF, 0xFF), // TextFillColorSecondary
        BorderColor = Color.FromArgb(0x15, 0xFF, 0xFF, 0xFF), // CardStroke
        AccentColor = Color.FromRgb(0x60, 0xCD, 0xFF), // Fluent 蓝（深色版）
        DueColor = Color.FromRgb(0xFF, 0x99, 0xA4), // SystemFillColorCritical
        ActiveColor = Color.FromRgb(0x6C, 0xCB, 0x5F), // SystemFillColorSuccess
        HoverColor = Color.FromArgb(0x0F, 0xFF, 0xFF, 0xFF), // SubtleFillSecondary
        DueSoftColor = Color.FromRgb(0x44, 0x27, 0x26), // CriticalBackground
    };

    /// <summary>喂给 WPF-UI 的强调色基色（Windows 默认蓝 #0078D4），它会按深浅主题自动派生明暗档</summary>
    public static Color FluentAccentBase { get; } = Color.FromRgb(0x00, 0x78, 0xD4);

    /// <summary>是否为深色主题</summary>
    public bool IsDark => ReferenceEquals(this, Dark);
}
