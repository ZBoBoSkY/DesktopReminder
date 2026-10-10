using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
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

    // 全局快捷键 Ctrl+Alt+N（快速新建任务）：MOD_ALT|MOD_CONTROL + VK_N
    private const int HotkeyId = 0xD001;
    private const uint ModControlAlt = 0x0001 | 0x0002;
    private const uint VkN = 0x4E;
    private const int WmHotkey = 0x0312;

    private Mutex? _mutex;
    private HwndSource? _hotkeySource;
    private bool _hotkeyRegistered;
    private FileLogger _logger = null!;
    private AppSettings _settings = null!;
    private JsonSettingsRepository _settingsRepo = null!;
    private ReminderCoordinator _coordinator = null!;
    private ReminderEventBridge _reminderBridge = null!;
    private SystemReminderNotifier _soundNotifier = null!;
    private ForegroundAppNameProvider _appNameProvider = null!;
    private WindowStateMachine _stateMachine = null!;
    private MainViewModel _mainViewModel = null!;
    private MainWindow _mainWindow = null!;
    private IconWindow _iconWindow = null!;
    private TrayIcon _tray = null!;
    private DispatcherTimer _timer = null!;

    private static string DataDirectory => Path.Combine(AppContext.BaseDirectory, "data");

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left; public int Top; public int Right; public int Bottom; }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out NativePoint lpPoint);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out NativeRect lpRect);

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    /// <summary>鼠标物理坐标是否落在窗口矩形内（半透明状态恢复清晰的系统级兜底）</summary>
    private static bool IsPointerInside(Window window)
    {
        if (!window.IsVisible)
        {
            return false;
        }

        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero || !GetCursorPos(out var pt) || !GetWindowRect(hwnd, out var rect))
        {
            return false;
        }

        return pt.X >= rect.Left && pt.X <= rect.Right && pt.Y >= rect.Top && pt.Y <= rect.Bottom;
    }

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
        _appNameProvider = new ForegroundAppNameProvider(Path.Combine(DataDirectory, "app_names.json"));

        // 3) ViewModel 与窗口
        _mainViewModel = new MainViewModel(_coordinator, _appNameProvider);
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
        _mainWindow.SourceInitialized += (_, _) => RegisterGlobalHotkey();
        _mainViewModel.RequestAddTask += OpenAddTaskDialog;
        _mainViewModel.RequestMinimizeToIcon += () => _stateMachine.CollapseToIcon(DateTime.Now);
        _mainViewModel.RequestOpenSettings += OpenSettingsDialog;
        _mainViewModel.RequestOpenAbout += () => new AboutDialog { Owner = _mainWindow }.ShowDialog();
        _mainViewModel.RequestExit += ExitApplication;
        _mainViewModel.RequestSnooze += ShowSnoozeOptions;
        _mainViewModel.RequestDelete += DeleteTaskWithConfirm;
        _mainViewModel.RequestHardDelete += HardDeleteWithConfirm;

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

            // 轮询记录上一个前台软件（新建任务自动捕捉的数据源）
            _appNameProvider.Refresh(new WindowInteropHelper(_mainWindow).Handle);

            _coordinator.Tick(now);
            _mainViewModel.Tick(now);

            // 半透明状态下鼠标已移入窗口 → 立即恢复清晰（用户"移到界面上=要操作"）。
            // 系统级判断（GetCursorPos + GetWindowRect，物理像素），不依赖 WPF 路由事件，可靠。
            if (_stateMachine.CurrentState == WindowState.Glass && IsPointerInside(_mainWindow))
            {
                _stateMachine.RegisterInteraction(now);
            }
            else
            {
                _stateMachine.Evaluate(now);
            }

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
            // 用户从图标/托盘点进来：如有到期任务，直达第一个（收起历史面板+滚到顶）
            _mainWindow.FocusFirstDueTask();
        }
    }

    /// <summary>
    /// 不透明度过渡（A2：临界阻尼弹簧，响应 0.35s、时长 0.7s，快速起步、平滑落定、无过冲）。
    /// A1 可中断性由 WPF 动画系统保证：BeginAnimation 未设 From 时，每次都从"当前显示值"继续，
    /// 所以用户在过渡中途移入鼠标/触发交互，会从当前不透明度平滑接回，绝不闪断。
    /// </summary>
    private void AnimateMainOpacity(double target)
    {
        const double response = 0.35;
        var spring = new SpringEase { ResponseSeconds = response, DurationSeconds = response * 2 };
        var animation = new DoubleAnimation(target, TimeSpan.FromSeconds(response * 2))
        {
            EasingFunction = spring,
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

    /// <summary>全局快捷键 Ctrl+Alt+N：任何软件里都能一键新建任务（配合"自动捕捉上一个软件"）</summary>
    private void RegisterGlobalHotkey()
    {
        try
        {
            var hwnd = new WindowInteropHelper(_mainWindow).Handle;
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            _hotkeySource = HwndSource.FromHwnd(hwnd);
            _hotkeySource?.AddHook(HotkeyWndProc);
            _hotkeyRegistered = RegisterHotKey(hwnd, HotkeyId, ModControlAlt, VkN);
            _logger.Info(_hotkeyRegistered
                ? "全局快捷键 Ctrl+Alt+N 注册成功"
                : "全局快捷键注册失败（可能已被其他程序占用）");
        }
        catch (Exception ex)
        {
            _logger.Error("注册全局快捷键失败", ex);
        }
    }

    private IntPtr HotkeyWndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            Dispatcher.BeginInvoke(OnGlobalHotkeyPressed);
            handled = true;
        }

        return IntPtr.Zero;
    }

    private void OnGlobalHotkeyPressed()
    {
        // 快捷键即交互：重置收起计时、显示主界面（含到期直达）、弹出新建任务
        _stateMachine.RegisterInteraction(DateTime.Now);
        ShowMainWindow(forceActive: true);
        OpenAddTaskDialog();
    }

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
            $"确定删除「{task.AppName}」吗？删除后可在「已删除」中找回。", "删除任务",
            MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (result == MessageBoxResult.Yes)
        {
            _coordinator.DeleteTask(task.Id);
            _mainViewModel.RefreshFromCoordinator();
        }
    }

    private void HardDeleteWithConfirm(ReminderTask task)
    {
        var result = MessageBox.Show(
            $"确定彻底删除「{task.AppName}」吗？此操作不可恢复。", "彻底删除",
            MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (result == MessageBoxResult.Yes)
        {
            _coordinator.HardDeleteTask(task.Id);
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
        if (_hotkeyRegistered)
        {
            UnregisterHotKey(new WindowInteropHelper(_mainWindow).Handle, HotkeyId);
            _hotkeySource?.RemoveHook(HotkeyWndProc);
            _hotkeyRegistered = false;
        }
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
