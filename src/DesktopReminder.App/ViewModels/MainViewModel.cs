using System.Collections.ObjectModel;
using System.Windows.Input;
using DesktopReminder.Core.Services;
using DesktopReminder.Domain.Models;
using DesktopReminder.Domain.Services;

namespace DesktopReminder.App.ViewModels;

/// <summary>
/// 主界面 ViewModel：任务列表（进行中/已完成）、标题栏动作、空状态。
/// 业务动作全部委托给协调器；本类只做展示层编排。
/// </summary>
public sealed class MainViewModel : ObservableObject
{
    private readonly ReminderCoordinator _coordinator;
    private readonly IAppNameProvider _appNameProvider;
    private bool _completedPanelVisible;
    private bool _deletedPanelVisible;
    private bool _hasCompleted;
    private bool _hasDeleted;

    public MainViewModel(ReminderCoordinator coordinator, IAppNameProvider appNameProvider)
    {
        _coordinator = coordinator;
        _appNameProvider = appNameProvider;

        AddTaskCommand = new RelayCommand(_ => RequestAddTask?.Invoke());
        MinimizeToIconCommand = new RelayCommand(_ => RequestMinimizeToIcon?.Invoke());
        ToggleCompletedCommand = new RelayCommand(_ => ToggleCompletedPanel());
        ToggleDeletedCommand = new RelayCommand(_ => ToggleDeletedPanel());
        OpenSettingsCommand = new RelayCommand(_ => RequestOpenSettings?.Invoke());
        OpenAboutCommand = new RelayCommand(_ => RequestOpenAbout?.Invoke());
        ExitCommand = new RelayCommand(_ => RequestExit?.Invoke());

        // 双击空列表/第一列 → 新建任务（冻结需求：空状态提示「双击新建任务」）
        DoubleClickAddCommand = new RelayCommand(_ => RequestAddTask?.Invoke());
    }

    // ---- UI 事件出口（由窗口绑定） ----
    public event Action? RequestAddTask;
    public event Action? RequestMinimizeToIcon;
    public event Action? RequestOpenSettings;
    public event Action? RequestOpenAbout;
    public event Action? RequestExit;
    public event Action? CompletedChanged;

    public string TitleText => "桌面轻提醒";

    public ObservableCollection<TaskItemViewModel> ActiveTasks { get; } = new();
    public ObservableCollection<TaskItemViewModel> CompletedTasks { get; } = new();
    public ObservableCollection<TaskItemViewModel> DeletedTasks { get; } = new();

    public bool HasActiveTasks => ActiveTasks.Count > 0;

    /// <summary>空状态提示（只有无任务时显示三行空列表，第一行浅字「双击新建任务」）</summary>
    public bool EmptyHintVisible => !HasActiveTasks;

    public bool CompletedPanelVisible
    {
        get => _completedPanelVisible;
        private set => SetProperty(ref _completedPanelVisible, value);
    }

    public bool DeletedPanelVisible
    {
        get => _deletedPanelVisible;
        private set => SetProperty(ref _deletedPanelVisible, value);
    }

    public bool HasCompleted
    {
        get => _hasCompleted;
        private set => SetProperty(ref _hasCompleted, value);
    }

    public bool HasDeleted
    {
        get => _hasDeleted;
        private set => SetProperty(ref _hasDeleted, value);
    }

    /// <summary>已到期/过期任务数（主界面顶部红色提示条）</summary>
    public int DueCount { get; private set; }

    public bool HasDue => DueCount > 0;
    public string DueBannerText => $"有 {DueCount} 项任务已到期，请处理";

    public RelayCommand AddTaskCommand { get; }
    public RelayCommand MinimizeToIconCommand { get; }
    public RelayCommand ToggleCompletedCommand { get; }
    public RelayCommand ToggleDeletedCommand { get; }
    public RelayCommand OpenSettingsCommand { get; }
    public RelayCommand OpenAboutCommand { get; }
    public RelayCommand ExitCommand { get; }
    public RelayCommand DoubleClickAddCommand { get; }

    /// <summary>当前上一个操作的软件名称（新建对话框自动填充用）</summary>
    public string GetPreviousAppName() => _appNameProvider.GetPreviousAppName();

    /// <summary>从协调器全量同步任务列表（新增/完成/恢复/删除后调用）</summary>
    public void RefreshFromCoordinator()
    {
        ActiveTasks.Clear();
        CompletedTasks.Clear();
        DeletedTasks.Clear();

        // 进行中按到期时间升序（最早到期在最上，冻结需求 R1.16）
        foreach (var task in ReminderScheduler.SortActiveByDue(_coordinator.Tasks))
        {
            ActiveTasks.Add(CreateItem(task));
        }

        foreach (var task in _coordinator.Tasks.Where(t => t.Status == ReminderTaskStatus.Completed))
        {
            CompletedTasks.Add(CreateItem(task));
        }

        foreach (var task in _coordinator.Tasks.Where(t => t.Status == ReminderTaskStatus.Deleted))
        {
            DeletedTasks.Add(CreateItem(task));
        }

        HasCompleted = CompletedTasks.Count > 0;
        HasDeleted = DeletedTasks.Count > 0;
        RefreshDueCount(DateTime.Now);
        OnPropertyChanged(nameof(HasActiveTasks));
        OnPropertyChanged(nameof(EmptyHintVisible));
        CompletedChanged?.Invoke();
    }

    private void RefreshDueCount(DateTime now)
    {
        var due = ReminderScheduler.CountDueOrOverdue(_coordinator.Tasks, now);
        if (due != DueCount)
        {
            DueCount = due;
            OnPropertyChanged(nameof(DueCount));
            OnPropertyChanged(nameof(HasDue));
            OnPropertyChanged(nameof(DueBannerText));
        }
    }

    private TaskItemViewModel CreateItem(ReminderTask task)
    {
        var isDeleted = task.Status == ReminderTaskStatus.Deleted;
        return new TaskItemViewModel(
            task,
            onComplete: t => { _coordinator.CompleteTask(t.Id); RefreshFromCoordinator(); },
            onSnooze: t => RequestSnooze?.Invoke(t),
            onRestore: isDeleted
                ? t => { _coordinator.RestoreDeletedTask(t.Id); RefreshFromCoordinator(); }
                : t => { _coordinator.RestoreTask(t.Id); RefreshFromCoordinator(); },
            onDelete: isDeleted
                ? t => RequestHardDelete?.Invoke(t)
                : t => RequestDelete?.Invoke(t));
    }

    /// <summary>稍后/删除需要确认弹窗（由窗口层实现，含固定选项）</summary>
    public event Action<ReminderTask>? RequestSnooze;
    public event Action<ReminderTask>? RequestDelete;

    /// <summary>已删除库的「彻底删除」（不可恢复，仍需确认）</summary>
    public event Action<ReminderTask>? RequestHardDelete;

    /// <summary>从已删除库恢复</summary>
    public void RestoreDeleted(ReminderTask task)
    {
        _coordinator.RestoreDeletedTask(task.Id);
        RefreshFromCoordinator();
    }

    /// <summary>从已删除库彻底清除（不可恢复）</summary>
    public void HardDelete(ReminderTask task)
    {
        _coordinator.HardDeleteTask(task.Id);
        RefreshFromCoordinator();
    }

    /// <summary>每秒刷新所有任务的倒计时显示</summary>
    public void Tick(DateTime now)
    {
        foreach (var item in ActiveTasks)
        {
            item.Refresh(now);
        }

        // 到期瞬间出现提示条
        RefreshDueCount(now);
    }

    private void ToggleCompletedPanel()
    {
        CompletedPanelVisible = !CompletedPanelVisible;
        if (CompletedPanelVisible)
        {
            DeletedPanelVisible = false; // 两个历史面板互斥
        }
    }

    private void ToggleDeletedPanel()
    {
        DeletedPanelVisible = !DeletedPanelVisible;
        if (DeletedPanelVisible)
        {
            CompletedPanelVisible = false;
        }
    }

    /// <summary>收起历史面板（到期直达时调用，避免遮挡任务列表）</summary>
    public void CollapseHistoryPanels()
    {
        CompletedPanelVisible = false;
        DeletedPanelVisible = false;
    }
}
