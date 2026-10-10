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
    /// 深靛蓝主题配色（用户选定蓝色系）：
    /// - 深色：近黑深灰背景 + 稍浅的卡片表面，纯靠深浅分层，无明显描边
    /// - 浅色：奶油暖白背景 + 纯白卡片
    /// - 强调色为深靛蓝，完成用柔和绿，到期用柔和红
    /// </summary>
    public static ThemeColors Light { get; } = new()
    {
        BackgroundColor = Color.FromRgb(0xF4, 0xEF, 0xEF), // 奶油暖白
        SurfaceColor = Color.FromArgb(0xF6, 0xFF, 0xFF, 0xFF), // 96% 白卡片
        ForegroundColor = Color.FromRgb(0x1B, 0x1B, 0x1B), // 近黑主文本
        SecondaryTextColor = Color.FromRgb(0x8E, 0x8E, 0x93), // 次要文本
        BorderColor = Color.FromArgb(0x18, 0x00, 0x00, 0x00), // 极淡描边
        AccentColor = Color.FromRgb(0x58, 0x56, 0xD6), // 深靛蓝（浅色版）
        DueColor = Color.FromRgb(0xE5, 0x55, 0x6B), // 柔和红
        ActiveColor = Color.FromRgb(0x66, 0xBB, 0x6A), // 柔和绿
        HoverColor = Color.FromArgb(0x12, 0x00, 0x00, 0x00),
        DueSoftColor = Color.FromArgb(0x14, 0xE5, 0x55, 0x6B), // 红色 8% 底
    };

    public static ThemeColors Dark { get; } = new()
    {
        BackgroundColor = Color.FromRgb(0x1B, 0x1B, 0x1B), // 近黑深灰
        SurfaceColor = Color.FromArgb(0xF6, 0x2A, 0x2A, 0x2A), // 深灰卡片表面
        ForegroundColor = Color.FromRgb(0xFF, 0xFF, 0xFF), // 白主文本
        SecondaryTextColor = Color.FromRgb(0x9E, 0x9E, 0x9E), // 次要文本
        BorderColor = Color.FromArgb(0x12, 0xFF, 0xFF, 0xFF), // 极淡描边
        AccentColor = Color.FromRgb(0x5E, 0x5C, 0xE6), // 深靛蓝（深色版）
        DueColor = Color.FromRgb(0xFF, 0x6B, 0x81), // 柔和红
        ActiveColor = Color.FromRgb(0x6F, 0xC8, 0x73), // 柔和绿
        HoverColor = Color.FromArgb(0x1F, 0xFF, 0xFF, 0xFF),
        DueSoftColor = Color.FromArgb(0x26, 0xFF, 0x6B, 0x81), // 红色 15% 底
    };
}
