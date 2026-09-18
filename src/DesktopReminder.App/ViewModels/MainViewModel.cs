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
    private bool _hasCompleted;

    public MainViewModel(ReminderCoordinator coordinator, IAppNameProvider appNameProvider)
    {
        _coordinator = coordinator;
        _appNameProvider = appNameProvider;

        AddTaskCommand = new RelayCommand(_ => RequestAddTask?.Invoke());
        MinimizeToIconCommand = new RelayCommand(_ => RequestMinimizeToIcon?.Invoke());
        ToggleCompletedCommand = new RelayCommand(_ => ToggleCompletedPanel());
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

    public bool HasActiveTasks => ActiveTasks.Count > 0;

    /// <summary>空状态提示（只有无任务时显示三行空列表，第一行浅字「双击新建任务」）</summary>
    public bool EmptyHintVisible => !HasActiveTasks;

    public bool CompletedPanelVisible
    {
        get => _completedPanelVisible;
        private set
        {
            if (SetProperty(ref _completedPanelVisible, value))
            {
                OnPropertyChanged(nameof(CompletedPanelVisible));
            }
        }
    }

    public bool HasCompleted
    {
        get => _hasCompleted;
        private set => SetProperty(ref _hasCompleted, value);
    }

    public RelayCommand AddTaskCommand { get; }
    public RelayCommand MinimizeToIconCommand { get; }
    public RelayCommand ToggleCompletedCommand { get; }
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

        // 进行中按到期时间升序（最早到期在最上，冻结需求 R1.16）
        foreach (var task in ReminderScheduler.SortActiveByDue(_coordinator.Tasks))
        {
            ActiveTasks.Add(CreateItem(task));
        }

        foreach (var task in _coordinator.Tasks.Where(t => t.Status == ReminderTaskStatus.Completed))
        {
            CompletedTasks.Add(CreateItem(task));
        }

        HasCompleted = CompletedTasks.Count > 0;
        OnPropertyChanged(nameof(HasActiveTasks));
        OnPropertyChanged(nameof(EmptyHintVisible));
        CompletedChanged?.Invoke();
    }

    private TaskItemViewModel CreateItem(ReminderTask task) => new(
        task,
        onComplete: t => { _coordinator.CompleteTask(t.Id); RefreshFromCoordinator(); },
        onSnooze: t => RequestSnooze?.Invoke(t),
        onRestore: t => { _coordinator.RestoreTask(t.Id); RefreshFromCoordinator(); },
        onDelete: t => RequestDelete?.Invoke(t));

    /// <summary>稍后/删除需要确认弹窗（由窗口层实现，含固定选项）</summary>
    public event Action<ReminderTask>? RequestSnooze;
    public event Action<ReminderTask>? RequestDelete;

    /// <summary>每秒刷新所有任务的倒计时显示</summary>
    public void Tick(DateTime now)
    {
        foreach (var item in ActiveTasks)
        {
            item.Refresh(now);
        }
    }

    private void ToggleCompletedPanel() => CompletedPanelVisible = !CompletedPanelVisible;
}
