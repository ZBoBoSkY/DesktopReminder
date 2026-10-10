using System.Windows.Media;
using DesktopReminder.App.Views;
using DesktopReminder.Domain.Models;
using DesktopReminder.Domain.Services;

namespace DesktopReminder.App.ViewModels;

/// <summary>
/// 单条任务的界面模型：显示倒计时与状态，按钮动作通过回调委托给协调器。
/// </summary>
public sealed class TaskItemViewModel : ObservableObject
{
    private readonly Action<ReminderTask> _onComplete;
    private readonly Action<ReminderTask> _onSnooze;
    private readonly Action<ReminderTask> _onRestore;
    private readonly Action<ReminderTask> _onDelete;
    private readonly Action<ReminderTask> _onJumpBack;
    private string _countdownText = "--:--";
    private bool _isDue;

    public ReminderTask Task { get; }

    public TaskItemViewModel(
        ReminderTask task,
        Action<ReminderTask> onComplete,
        Action<ReminderTask> onSnooze,
        Action<ReminderTask> onRestore,
        Action<ReminderTask> onDelete,
        Action<ReminderTask> onJumpBack)
    {
        Task = task;
        _onComplete = onComplete;
        _onSnooze = onSnooze;
        _onRestore = onRestore;
        _onDelete = onDelete;
        _onJumpBack = onJumpBack;
        CompleteCommand = new RelayCommand(_ => _onComplete(Task));
        SnoozeCommand = new RelayCommand(_ => _onSnooze(Task));
        RestoreCommand = new RelayCommand(_ => _onRestore(Task));
        DeleteCommand = new RelayCommand(_ => _onDelete(Task));
        JumpBackCommand = new RelayCommand(_ => _onJumpBack(Task), _ => HasProcessName);
    }

    public string AppName => Task.AppName;
    public string Note => Task.Note;
    public bool HasNote => !string.IsNullOrEmpty(Task.Note);
    public bool IsCompleted => Task.Status == ReminderTaskStatus.Completed;
    public string CompletedAtText => Task.CompletedAt?.ToString("HH:mm") ?? "";

    /// <summary>真实软件图标（按进程名取 exe 图标；取不到为 null，UI 回退首字母块）</summary>
    public ImageSource? AppIcon => AppIconCache.GetIcon(Task.ProcessName);
    public bool HasIcon => AppIcon != null;

    /// <summary>图标回退：软件名首字符（无真实图标时显示的圆角字母块）</summary>
    public string InitialLetter => string.IsNullOrEmpty(AppName) ? "?" : AppName[..1].ToUpperInvariant();

    public string CountdownText
    {
        get => _countdownText;
        private set => SetProperty(ref _countdownText, value);
    }

    /// <summary>是否已到期/过期（红色醒目，冻结需求 R1.8）</summary>
    public bool IsDue
    {
        get => _isDue;
        private set => SetProperty(ref _isDue, value);
    }

    public RelayCommand CompleteCommand { get; }
    public RelayCommand SnoozeCommand { get; }
    public RelayCommand RestoreCommand { get; }
    public RelayCommand DeleteCommand { get; }

    /// <summary>一键跳回该任务对应的软件窗口（需要进程名；旧任务无进程名则不可跳回）</summary>
    public RelayCommand JumpBackCommand { get; }
    public bool HasProcessName => !string.IsNullOrWhiteSpace(Task.ProcessName);

    /// <summary>每秒刷新倒计时与到期状态</summary>
    public void Refresh(DateTime now)
    {
        CountdownText = ReminderScheduler.FormatCountdown(Task.Remaining(now));
        IsDue = Task.Status != ReminderTaskStatus.Completed && Task.DueAt <= now;
    }
}
