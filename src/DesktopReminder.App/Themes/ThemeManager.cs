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
        SyncFluentControls(Current);
        ThemeChanged?.Invoke(Current);
    }

    /// <summary>
    /// 把当前深/浅主题同步给 WPF-UI 控件库（设置窗口等用到的 Fluent 按钮/输入框/下拉框），
    /// 强调色固定为 Fluent 蓝，不跟随系统强调色，保证与自绘部分一致。
    /// </summary>
    private static void SyncFluentControls(ThemeColors colors)
    {
        if (System.Windows.Application.Current is null)
        {
            return;
        }

        try
        {
            var theme = colors.IsDark
                ? Wpf.Ui.Appearance.ApplicationTheme.Dark
                : Wpf.Ui.Appearance.ApplicationTheme.Light;
            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(theme, Wpf.Ui.Controls.WindowBackdropType.None, false);
            Wpf.Ui.Appearance.ApplicationAccentColorManager.Apply(ThemeColors.FluentAccentBase, theme, false, false);
        }
        catch
        {
            // 控件库主题同步失败不影响自绘界面（自绘部分只读 ThemeColors）
        }
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
