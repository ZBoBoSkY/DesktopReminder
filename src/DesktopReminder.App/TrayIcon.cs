using System.Windows.Media;
using DesktopReminder.App.Themes;
using DesktopReminder.Domain.Models;
using WF = System.Windows.Forms;

namespace DesktopReminder.App;

/// <summary>
/// 系统托盘（冻结需求 R7）：右键菜单入口、双击显示主界面、
/// 图标带到期任务角标（数字变化才重建 Icon，避免频繁 GDI 开销）。
/// 托盘存在即程序常驻，关闭主界面 ≠ 退出。
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private readonly WF.NotifyIcon _notifyIcon;
    private readonly WF.ContextMenuStrip _menu;
    private readonly WF.ToolStripMenuItem _soundItem;
    private readonly WF.ToolStripMenuItem _autoStartItem;
    private System.Drawing.Icon? _currentIcon;
    private int _lastBadge = -1;
    private bool _lastOverdue;

    public event Action? RequestShowMain;
    public event Action? RequestAddTask;
    public event Action? RequestOpenSettings;
    public event Action? RequestOpenAbout;
    public event Action? RequestExit;
    public event Action? RequestToggleSound;
    public event Action? RequestToggleAutoStart;

    public TrayIcon(AppSettings settings)
    {
        _menu = new WF.ContextMenuStrip();

        var showMain = _menu.Items.Add("显示主界面");
        showMain.Click += (_, _) => RequestShowMain?.Invoke();
        _menu.Items.Add(new WF.ToolStripSeparator());

        var addTask = _menu.Items.Add("新建任务");
        addTask.Click += (_, _) => RequestAddTask?.Invoke();

        _menu.Items.Add(new WF.ToolStripSeparator());

        _soundItem = new WF.ToolStripMenuItem("声音提醒") { CheckOnClick = false };
        _soundItem.Click += (_, _) => RequestToggleSound?.Invoke();
        _menu.Items.Add(_soundItem);

        _autoStartItem = new WF.ToolStripMenuItem("开机自启") { CheckOnClick = false };
        _autoStartItem.Click += (_, _) => RequestToggleAutoStart?.Invoke();
        _menu.Items.Add(_autoStartItem);

        _menu.Items.Add(new WF.ToolStripSeparator());

        var settingsItem = _menu.Items.Add("设置");
        settingsItem.Click += (_, _) => RequestOpenSettings?.Invoke();

        var aboutItem = _menu.Items.Add("关于");
        aboutItem.Click += (_, _) => RequestOpenAbout?.Invoke();

        _menu.Items.Add(new WF.ToolStripSeparator());

        var exitItem = _menu.Items.Add("退出");
        exitItem.Click += (_, _) => RequestExit?.Invoke();

        _notifyIcon = new WF.NotifyIcon
        {
            Text = "桌面轻提醒",
            ContextMenuStrip = _menu,
            Visible = true,
        };
        _notifyIcon.DoubleClick += (_, _) => RequestShowMain?.Invoke();

        // 初始图标
        RefreshBadge(0, false);
    }

    /// <summary>刷新勾选状态（设置/自启变化后调用）</summary>
    public void UpdateCheckStates(bool soundEnabled, bool autoStart)
    {
        _soundItem.Checked = soundEnabled;
        _autoStartItem.Checked = autoStart;
    }

    /// <summary>刷新角标图标；仅数字/颜色变化时才重建（节流）</summary>
    public void RefreshBadge(int badgeCount, bool hasOverdue)
    {
        if (badgeCount == _lastBadge && hasOverdue == _lastOverdue)
        {
            return;
        }

        _lastBadge = badgeCount;
        _lastOverdue = hasOverdue;

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            AppIconFactory.DrawIcon(dc, countdownText: null, badgeCount, hasOverdue, 32, ThemeManager.Current);
        }

        var bitmap = AppIconFactory.Render(visual, 32, 32);
        _currentIcon?.Dispose();
        _currentIcon = AppIconFactory.ToIcon(bitmap);
        _notifyIcon.Icon = _currentIcon;
    }

    /// <summary>气泡提示（图标状态提醒时告知有几项任务）</summary>
    public void ShowBalloon(string title, string message)
    {
        _notifyIcon.ShowBalloonTip(4000, title, message, WF.ToolTipIcon.Info);
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _currentIcon?.Dispose();
        _menu.Dispose();
    }
}
