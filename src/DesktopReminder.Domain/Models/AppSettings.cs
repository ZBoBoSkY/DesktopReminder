namespace DesktopReminder.Domain.Models;

/// <summary>
/// 应用配置（全部可持久化，设置即时生效）。
/// 数值默认值即出厂默认，全部可在设置界面修改。
/// </summary>
public sealed class AppSettings
{
    /// <summary>清晰 → 半透明的无操作超时（默认 5 分钟）</summary>
    public TimeSpan GlassTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>半透明 → 图标的无操作超时（默认 30 秒）</summary>
    public TimeSpan IconTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>半透明状态的不透明度（0.1~1.0，默认 0.35）</summary>
    public double GlassOpacity { get; set; } = 0.35;

    /// <summary>图标状态周期性提醒间隔（默认 5 分钟）</summary>
    public TimeSpan RemindInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>新建任务的默认时长（分钟，默认 5）</summary>
    public int DefaultTaskMinutes { get; set; } = 5;

    /// <summary>主题模式（默认跟随系统）</summary>
    public ThemeMode Theme { get; set; } = ThemeMode.FollowSystem;

    /// <summary>声音提醒开关（默认开）</summary>
    public bool SoundEnabled { get; set; } = true;

    /// <summary>开机自启（默认关）</summary>
    public bool AutoStart { get; set; } = false;

    /// <summary>主窗口位置与尺寸记忆（null = 未设定，使用默认布局）</summary>
    public double? WindowX { get; set; }
    public double? WindowY { get; set; }
    public double? WindowWidth { get; set; }
    public double? WindowHeight { get; set; }

    /// <summary>图标位置记忆</summary>
    public double? IconX { get; set; }
    public double? IconY { get; set; }
}
