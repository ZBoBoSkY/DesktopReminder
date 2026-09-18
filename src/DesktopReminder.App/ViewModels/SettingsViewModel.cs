using System.Windows.Input;
using DesktopReminder.Domain.Models;

namespace DesktopReminder.App.ViewModels;

/// <summary>
/// 设置对话框 ViewModel（冻结需求 R1.10/R2.10 + 后续确认项：主题/声音/开机自启）。
/// 修改即时保存到配置仓储；主题切换立即生效并通知全局。
/// </summary>
public sealed class SettingsViewModel : ObservableObject
{
    private readonly AppSettings _settings;
    private readonly Action<AppSettings> _onSave;
    private int _glassMinutes;
    private int _iconSeconds;
    private int _opacityPercent;
    private int _remindIntervalMinutes;
    private int _defaultMinutes;
    private int _themeIndex;
    private bool _soundEnabled;
    private bool _autoStart;

    public SettingsViewModel(AppSettings settings, Action<AppSettings> onSave)
    {
        _settings = settings;
        _onSave = onSave;

        _glassMinutes = (int)Math.Round(settings.GlassTimeout.TotalMinutes);
        _iconSeconds = (int)Math.Round(settings.IconTimeout.TotalSeconds);
        _opacityPercent = (int)Math.Round(settings.GlassOpacity * 100);
        _remindIntervalMinutes = (int)Math.Round(settings.RemindInterval.TotalMinutes);
        _defaultMinutes = settings.DefaultTaskMinutes;
        _themeIndex = settings.Theme switch
        {
            ThemeMode.Light => 0,
            ThemeMode.Dark => 1,
            _ => 2, // FollowSystem
        };
        _soundEnabled = settings.SoundEnabled;
        _autoStart = settings.AutoStart;

        SaveCommand = new RelayCommand(_ => SaveAndClose?.Invoke());
        ApplyCommand = new RelayCommand(_ => Save());
        CancelCommand = new RelayCommand(_ => Cancel?.Invoke());
    }

    public static IReadOnlyList<string> ThemeOptions { get; } = new[] { "浅色", "深色", "跟随系统" };

    public int GlassMinutes { get => _glassMinutes; set => SetProperty(ref _glassMinutes, value); }
    public int IconSeconds { get => _iconSeconds; set => SetProperty(ref _iconSeconds, value); }
    public int OpacityPercent { get => _opacityPercent; set => SetProperty(ref _opacityPercent, value); }
    public int RemindIntervalMinutes { get => _remindIntervalMinutes; set => SetProperty(ref _remindIntervalMinutes, value); }
    public int DefaultMinutes { get => _defaultMinutes; set => SetProperty(ref _defaultMinutes, value); }
    public int ThemeIndex { get => _themeIndex; set => SetProperty(ref _themeIndex, value); }
    public bool SoundEnabled { get => _soundEnabled; set => SetProperty(ref _soundEnabled, value); }
    public bool AutoStart { get => _autoStart; set => SetProperty(ref _autoStart, value); }

    public RelayCommand SaveCommand { get; }
    public RelayCommand ApplyCommand { get; }
    public RelayCommand CancelCommand { get; }

    public event Action? SaveAndClose;
    public event Action? Cancel;

    public void Save()
    {
        _settings.GlassTimeout = TimeSpan.FromMinutes(Math.Clamp(GlassMinutes, 1, 120));
        _settings.IconTimeout = TimeSpan.FromSeconds(Math.Clamp(IconSeconds, 5, 600));
        _settings.GlassOpacity = Math.Clamp(OpacityPercent, 5, 90) / 100.0;
        _settings.RemindInterval = TimeSpan.FromMinutes(Math.Clamp(RemindIntervalMinutes, 1, 120));
        _settings.DefaultTaskMinutes = Math.Clamp(DefaultMinutes, 1, 240);
        _settings.Theme = ThemeIndex switch
        {
            0 => ThemeMode.Light,
            1 => ThemeMode.Dark,
            _ => ThemeMode.FollowSystem,
        };
        _settings.SoundEnabled = SoundEnabled;
        _settings.AutoStart = AutoStart;

        _onSave(_settings);
    }
}
