using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using DesktopReminder.App.Themes;
using DesktopReminder.App.ViewModels;
using DesktopReminder.App.Views;
using DesktopReminder.Core.Services;
using DesktopReminder.Core.StateMachine;
using DesktopReminder.Domain.Models;
using DesktopReminder.Domain.Services;
using DesktopReminder.Infrastructure.Logging;
using DesktopReminder.Infrastructure.Notifications;
using DesktopReminder.Infrastructure.Persistence;
using DesktopReminder.Infrastructure.Windows;
using Microsoft.Win32;

namespace DesktopReminder.App;

/// <summary>
/// 全局组装器（组合根）：把配置、领域、应用层、界面、托盘按固定顺序接好。
/// 唯一职责是「装配」，不包含业务逻辑；业务规则都在下层且已被单元测试覆盖。
/// </summary>
public partial class App : Application
{
    private const string MutexName = "DesktopReminder_SingleInstance_9f2c";
    private const string AutoStartValueName = "DesktopReminder";

    private Mutex? _mutex;
    private FileLogger _logger = null!;
    private AppSettings _settings = null!;
    private JsonSettingsRepository _settingsRepo = null!;
    private ReminderCoordinator _coordinator = null!;
    private ReminderEventBridge _reminderBridge = null!;
    private SystemReminderNotifier _soundNotifier = null!;
    private WindowStateMachine _stateMachine = null!;
    private MainViewModel _mainViewModel = null!;
    private MainWindow _mainWindow = null!;
    private IconWindow _iconWindow = null!;
    private TrayIcon _tray = null!;
    private DispatcherTimer _timer = null!;

    private static string DataDirectory => Path.Combine(AppContext.BaseDirectory, "data");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 单实例：第二个实例直接退出
        _mutex = new Mutex(true, MutexName, out var createdNew);
        if (!createdNew)
        {
            Shutdown();
            return;
        }

        // 日志与全局异常兜底（冻结需求 R8.5）
        _logger = new FileLogger(Path.Combine(DataDirectory, "logs"));
        DispatcherUnhandledException += (_, args) =>
        {
            _logger.Error("UI 未处理异常", args.Exception);
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            _logger.Error("AppDomain 未处理异常", args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            _logger.Error("后台任务未观察异常", args.Exception);
            args.SetObserved();
        };

        try
        {
            StartupCore();
        }
        catch (Exception ex)
        {
            _logger.Error("启动失败", ex);
            MessageBox.Show($"启动失败：{ex.Message}", "桌面轻提醒", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    private void StartupCore()
    {
        // 1) 配置 + 主题（主题跟随系统变化）
        _settingsRepo = new JsonSettingsRepository(Path.Combine(DataDirectory, "settings.json"));
        _settings = _settingsRepo.LoadSettings();
        ThemeManager.Initialize(_settings);
        SystemEvents.UserPreferenceChanged += (_, _) =>
            Dispatcher.Invoke(ThemeManager.OnSystemThemeChanged);

        // 2) 应用层与基础设施
        var taskRepo = new JsonTaskRepository(Path.Combine(DataDirectory, "tasks.json"));
        _soundNotifier = new SystemReminderNotifier(_settings.SoundEnabled);
        _reminderBridge = new ReminderEventBridge(_soundNotifier);
        _coordinator = new ReminderCoordinator(taskRepo, _reminderBridge, _settings);
        _stateMachine = new WindowStateMachine(_settings);
        var appNameProvider = new ForegroundAppNameProvider(Path.Combine(DataDirectory, "app_names.json"));

        // 3) ViewModel 与窗口
        _mainViewModel = new MainViewModel(_coordinator, appNameProvider);
        _mainViewModel.RefreshFromCoordinator();
        _mainWindow = new MainWindow(_mainViewModel);
        _iconWindow = new IconWindow();
        _tray = new TrayIcon(_settings);
        _tray.UpdateCheckStates(_settings.SoundEnabled, _settings.AutoStart);

        // 4) 事件接线（所有交互只通过事件流进状态机/协调器）
        WireEvents();

        // 5) 记忆的位置恢复（含屏幕外防丢）
        RestorePositions();

        // 6) 每秒心跳：任务提醒 + 状态机 + 图标刷新
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => Heartbeat();
        _timer.Start();

        // 7) 首次显示（清晰状态）
        _mainWindow.Opacity = 1.0;
        ShowMainWindow(forceActive: true);
        _logger.Info("启动完成");
    }

    private void WireEvents()
    {
        _stateMachine.StateChanged += ApplyWindowState;

        // 主窗口
        _mainWindow.InteractionOccurred += () => _stateMachine.RegisterInteraction(DateTime.Now);
        _mainWindow.RequestHideToTray += () => _stateMachine.CollapseToIcon(DateTime.Now);
        _mainViewModel.RequestAddTask += OpenAddTaskDialog;
        _mainViewModel.RequestMinimizeToIcon += () => _stateMachine.CollapseToIcon(DateTime.Now);
        _mainViewModel.RequestOpenSettings += OpenSettingsDialog;
        _mainViewModel.RequestOpenAbout += () => new AboutDialog { Owner = _mainWindow }.ShowDialog();
        _mainViewModel.RequestExit += ExitApplication;
        _mainViewModel.RequestSnooze += ShowSnoozeOptions;
        _mainViewModel.RequestDelete += DeleteTaskWithConfirm;

        // 图标窗口
        _iconWindow.RequestOpenMain += () =>
        {
            _stateMachine.RegisterInteraction(DateTime.Now);
            ShowMainWindow(forceActive: true);
        };
        _iconWindow.RequestAddTask += OpenAddTaskDialog;
        _iconWindow.RequestOpenSettings += OpenSettingsDialog;
        _iconWindow.RequestOpenAbout += () => new AboutDialog { Owner = _mainWindow }.ShowDialog();
        _iconWindow.RequestExit += ExitApplication;

        // 托盘
        _tray.RequestShowMain += () =>
        {
            _stateMachine.RegisterInteraction(DateTime.Now);
            ShowMainWindow(forceActive: true);
        };
        _tray.RequestAddTask += OpenAddTaskDialog;
        _tray.RequestOpenSettings += OpenSettingsDialog;
        _tray.RequestOpenAbout += () => new AboutDialog { Owner = _mainWindow }.ShowDialog();
        _tray.RequestToggleSound += ToggleSound;
        _tray.RequestToggleAutoStart += ToggleAutoStart;
        _tray.RequestExit += ExitApplication;

        // 提醒事件桥
        _reminderBridge.DueNotified += OnDueNotified;
    }

    private void Heartbeat()
    {
        try
        {
            var now = DateTime.Now;

            _coordinator.Tick(now);
            _mainViewModel.Tick(now);
            _stateMachine.Evaluate(now);

            // 图标状态才渲染图标内容（省资源）
            if (_stateMachine.CurrentState == WindowState.Icon)
            {
                RefreshIconContent();
            }
        }
        catch (Exception ex)
        {
            _logger.Error("心跳异常", ex);
        }
    }

    private void RefreshIconContent()
    {
        var now = DateTime.Now;
        var earliest = _coordinator.EarliestActiveTask;
        var countdown = earliest is null ? null : ReminderScheduler.FormatCountdown(earliest.Remaining(now));
        var badge = _coordinator.CountDue(now);
        var hasOverdue = earliest is not null && earliest.DueAt <= now;

        _iconWindow.Refresh(countdown, badge, hasOverdue);
        _tray.RefreshBadge(badge, hasOverdue);
    }

    /// <summary>状态 → 窗口表现（平滑过渡）</summary>
    private void ApplyWindowState(WindowState state)
    {
        switch (state)
        {
            case WindowState.Active:
                _iconWindow.Hide();
                ShowMainWindow(forceActive: false);
                AnimateMainOpacity(1.0);
                break;

            case WindowState.Glass:
                _iconWindow.Hide();
                ShowMainWindow(forceActive: false);
                AnimateMainOpacity(_settings.GlassOpacity);
                break;

            case WindowState.Icon:
                _mainWindow.Hide();
                RefreshIconContent();
                _iconWindow.Show();
                _iconWindow.Activate();
                break;
        }
    }

    private void ShowMainWindow(bool forceActive)
    {
        if (!_mainWindow.IsVisible)
        {
            _mainWindow.Show();
        }

        if (forceActive)
        {
            _mainWindow.Activate();
        }
    }

    /// <summary>不透明度渐变（旧版「突然变半透明」的柔化修复，冻结需求 R3.4）</summary>
    private void AnimateMainOpacity(double target)
    {
        var animation = new DoubleAnimation(target, TimeSpan.FromMilliseconds(450))
        {
            AccelerationRatio = 0.3,
            DecelerationRatio = 0.3,
        };
        _mainWindow.BeginAnimation(UIElement.OpacityProperty, animation);
    }

    private void OnDueNotified(IReadOnlyList<ReminderTask> dueTasks)
    {
        // 图标状态被提醒 → 弹回半透明让用户看见
        if (_stateMachine.CurrentState == WindowState.Icon)
        {
            _stateMachine.InterruptForReminder(DateTime.Now);
        }

        _tray.ShowBalloon("任务到期", $"{dueTasks.Count} 项任务需要处理");
        _mainViewModel.RefreshFromCoordinator();
    }

    // ---------- 动作 ----------

    private void OpenAddTaskDialog()
    {
        var vm = new AddTaskViewModel(_mainViewModel.GetPreviousAppName());
        var dialog = new AddTaskDialog(vm, (name, note, duration) =>
        {
            _coordinator.AddTask(name, note, duration);
            _mainViewModel.RefreshFromCoordinator();
            _stateMachine.RegisterInteraction(DateTime.Now);
        })
        { Owner = _mainWindow };
        dialog.ShowDialog();
    }

    private void ShowSnoozeOptions(ReminderTask task)
    {
        var dialog = new SelectDurationDialog("稍后提醒", new[] { 5, 10, 15, 30 }, selected =>
        {
            _coordinator.SnoozeTask(task.Id, TimeSpan.FromMinutes(selected));
            _mainViewModel.RefreshFromCoordinator();
        })
        { Owner = _mainWindow };
        dialog.ShowDialog();
    }

    private void DeleteTaskWithConfirm(ReminderTask task)
    {
        var result = MessageBox.Show(
            $"确定删除「{task.AppName}」吗？删除后不可恢复。", "删除任务",
            MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (result == MessageBoxResult.Yes)
        {
            _coordinator.DeleteTask(task.Id);
            _mainViewModel.RefreshFromCoordinator();
        }
    }

    private void OpenSettingsDialog()
    {
        var vm = new SettingsViewModel(_settings, saved =>
        {
            _settingsRepo.SaveSettings(saved);
            ThemeManager.SetMode(saved.Theme);
            _soundNotifier.Enabled = saved.SoundEnabled;
            ApplyAutoStart(saved.AutoStart);
            _tray.UpdateCheckStates(saved.SoundEnabled, saved.AutoStart);
            _stateMachine.RegisterInteraction(DateTime.Now);
        });
        new SettingsDialog(vm) { Owner = _mainWindow }.ShowDialog();
    }

    private void ToggleSound()
    {
        _settings.SoundEnabled = !_settings.SoundEnabled;
        _soundNotifier.Enabled = _settings.SoundEnabled;
        _settingsRepo.SaveSettings(_settings);
        _tray.UpdateCheckStates(_settings.SoundEnabled, _settings.AutoStart);
    }

    private void ToggleAutoStart()
    {
        _settings.AutoStart = !_settings.AutoStart;
        ApplyAutoStart(_settings.AutoStart);
        _settingsRepo.SaveSettings(_settings);
        _tray.UpdateCheckStates(_settings.SoundEnabled, _settings.AutoStart);
    }

    private void ApplyAutoStart(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
            if (enabled)
            {
                var exe = Environment.ProcessPath;
                if (exe is not null)
                {
                    key?.SetValue(AutoStartValueName, $"\"{exe}\"");
                }
            }
            else
            {
                key?.DeleteValue(AutoStartValueName, throwOnMissingValue: false);
            }
        }
        catch (Exception ex)
        {
            _logger.Error("设置开机自启失败", ex);
        }
    }

    private void RestorePositions()
    {
        var screenLeft = SystemParameters.VirtualScreenLeft;
        var screenTop = SystemParameters.VirtualScreenTop;
        var screenRight = screenLeft + SystemParameters.VirtualScreenWidth;
        var screenBottom = screenTop + SystemParameters.VirtualScreenHeight;

        if (_settings.WindowX is double wx && _settings.WindowY is double wy &&
            wx >= screenLeft && wx < screenRight - 120 &&
            wy >= screenTop && wy < screenBottom - 60)
        {
            _mainWindow.Left = wx;
            _mainWindow.Top = wy;
        }

        if (_settings.IconX is double ix && _settings.IconY is double iy &&
            ix >= screenLeft && ix < screenRight - 20 &&
            iy >= screenTop && iy < screenBottom - 20)
        {
            _iconWindow.Left = ix;
            _iconWindow.Top = iy;
        }
    }

    private void ExitApplication()
    {
        try
        {
            _settings.WindowX = _mainWindow.Left;
            _settings.WindowY = _mainWindow.Top;
            _settings.IconX = _iconWindow.Left;
            _settings.IconY = _iconWindow.Top;
            _settingsRepo.SaveSettings(_settings);
        }
        catch (Exception ex)
        {
            _logger.Error("退出保存失败", ex);
        }

        _timer.Stop();
        _mainWindow.AllowClose = true;
        _mainWindow.Close();
        _iconWindow.Close();
        _tray.Dispose();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _logger.Info("退出");
        _logger.Dispose();
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }

    /// <summary>提醒事件桥：转发提醒同时通知 UI（打断图标状态、弹气泡）</summary>
    private sealed class ReminderEventBridge : IReminderNotifier
    {
        private readonly IReminderNotifier _inner;

        public ReminderEventBridge(IReminderNotifier inner) => _inner = inner;

        public event Action<IReadOnlyList<ReminderTask>>? DueNotified;

        public void NotifyDue(IReadOnlyList<ReminderTask> dueTasks)
        {
            _inner.NotifyDue(dueTasks);
            DueNotified?.Invoke(dueTasks);
        }
    }
}
