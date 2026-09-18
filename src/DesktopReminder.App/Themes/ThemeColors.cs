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
    /// Apple 风格系统色（浅/深两套，来自 iOS/macOS 系统调色板）：
    /// - 卡片使用 94% 半透明材质，桌面透出一点点 → 玻璃感（苹果「材质」原则）
    /// - Accent 用系统蓝，状态色用系统绿/红，色相有据可依
    /// </summary>
    public static ThemeColors Light { get; } = new()
    {
        BackgroundColor = Color.FromRgb(0xF2, 0xF2, 0xF7), // iOS 系统灰
        SurfaceColor = Color.FromArgb(0xF0, 0xFF, 0xFF, 0xFF), // 94% 白（玻璃卡片）
        ForegroundColor = Color.FromRgb(0x1C, 0x1C, 0x1E), // iOS 主文本
        SecondaryTextColor = Color.FromRgb(0x8E, 0x8E, 0x93), // iOS 次要文本
        BorderColor = Color.FromRgb(0xE5, 0xE5, 0xEA), // iOS 分隔线
        AccentColor = Color.FromRgb(0x58, 0x56, 0xD6), // 深靛蓝（iOS Indigo，方案 A）
        DueColor = Color.FromRgb(0xFF, 0x3B, 0x30), // Apple 红
        ActiveColor = Color.FromRgb(0x34, 0xC7, 0x59), // Apple 绿
        HoverColor = Color.FromArgb(0x12, 0x00, 0x00, 0x00),
        DueSoftColor = Color.FromArgb(0x14, 0xFF, 0x3B, 0x30), // 红色 8% 底（过期行/提示条）
    };

    public static ThemeColors Dark { get; } = new()
    {
        BackgroundColor = Color.FromRgb(0x1C, 0x1C, 0x1E), // iOS 深色系统灰
        SurfaceColor = Color.FromArgb(0xF0, 0x2C, 0x2C, 0x2E), // 94% 深灰（玻璃卡片）
        ForegroundColor = Color.FromRgb(0xFF, 0xFF, 0xFF), // iOS 深色主文本
        SecondaryTextColor = Color.FromRgb(0x98, 0x98, 0x9F), // iOS 深色次要文本
        BorderColor = Color.FromRgb(0x38, 0x38, 0x3A), // iOS 深色分隔线
        AccentColor = Color.FromRgb(0x5E, 0x5C, 0xE6), // 深靛蓝（iOS Dark Indigo，方案 A）
        DueColor = Color.FromRgb(0xFF, 0x45, 0x3A), // Apple 深色红
        ActiveColor = Color.FromRgb(0x30, 0xD1, 0x58), // Apple 深色绿
        HoverColor = Color.FromArgb(0x1F, 0xFF, 0xFF, 0xFF),
        DueSoftColor = Color.FromArgb(0x26, 0xFF, 0x45, 0x3A), // 红色 15% 底（过期行/提示条）
    };
}
