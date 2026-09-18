using System.Windows;
using DesktopReminder.App.Themes;

namespace DesktopReminder.App.Views;

/// <summary>
/// 所有窗口的基类：把当前主题颜色注册为窗口级 DynamicResource，
/// 主题切换时全树自动刷新（深/浅色全套变化，不再出现「只变背景不变文字」）。
/// 关闭时自动退订，防止内存泄漏。
/// </summary>
public abstract class ThemeWindow : Window
{
    protected ThemeWindow()
    {
        ThemeManager.ThemeChanged += OnThemeChanged;
        Unloaded += (_, _) => ThemeManager.ThemeChanged -= OnThemeChanged;
        Loaded += (_, _) => ApplyTheme();
    }

    private void OnThemeChanged(ThemeColors colors) => ApplyTheme();

    /// <summary>把主题刷写入窗口资源字典；XAML 用 {DynamicResource ThemeXxx} 引用</summary>
    protected void ApplyTheme()
    {
        var c = ThemeManager.Current;
        Resources["ThemeBackground"] = c.Background;
        Resources["ThemeSurface"] = c.Surface;
        Resources["ThemeForeground"] = c.Foreground;
        Resources["ThemeSecondaryText"] = c.SecondaryText;
        Resources["ThemeBorder"] = c.Border;
        Resources["ThemeAccent"] = c.Accent;
        Resources["ThemeDue"] = c.Due;
        Resources["ThemeActive"] = c.Active;
        Resources["ThemeHover"] = c.Hover;
        Resources["ThemeDueSoft"] = c.DueSoft;
    }
}
