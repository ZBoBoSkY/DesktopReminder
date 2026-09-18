using System.Windows.Media;
using DesktopReminder.Domain.Models;

namespace DesktopReminder.App.Themes;

/// <summary>
/// 主题管理器（唯一主题源，冻结需求 R5.3）。
/// 所有窗口订阅 ThemeChanged 并递归刷新自身；禁止在 XAML 硬编码颜色。
/// 跟随系统：读注册表 AppsUseLightTheme，并订阅系统主题变化事件。
/// </summary>
public static class ThemeManager
{
    private static ThemeMode _mode = ThemeMode.FollowSystem;
    private static bool _followSystem = true;

    /// <summary>当前主题颜色（全应用唯一来源）</summary>
    public static ThemeColors Current { get; private set; } = ThemeColors.Light;

    /// <summary>主题变化事件：窗口/控件订阅后立即全套刷新</summary>
    public static event Action<ThemeColors>? ThemeChanged;

    public static void Initialize(AppSettings settings)
    {
        _mode = settings.Theme;
        _followSystem = _mode == ThemeMode.FollowSystem;
        Apply();
    }

    public static void SetMode(ThemeMode mode)
    {
        _mode = mode;
        _followSystem = mode == ThemeMode.FollowSystem;
        Apply();
    }

    /// <summary>系统主题变化时调用（由 App 订阅 SystemEvents）</summary>
    public static void OnSystemThemeChanged()
    {
        if (_followSystem)
        {
            Apply();
        }
    }

    private static void Apply()
    {
        Current = _followSystem ? (IsSystemDark ? ThemeColors.Dark : ThemeColors.Light)
                                : (_mode == ThemeMode.Dark ? ThemeColors.Dark : ThemeColors.Light);
        ThemeChanged?.Invoke(Current);
    }

    /// <summary>Windows 是否处于深色应用主题（注册表 AppsUseLightTheme=0）</summary>
    public static bool IsSystemDark
    {
        get
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                var value = key?.GetValue("AppsUseLightTheme");
                return value is int i && i == 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
