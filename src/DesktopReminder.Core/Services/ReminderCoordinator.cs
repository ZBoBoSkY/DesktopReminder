namespace DesktopReminder.Core.Services;

using DesktopReminder.Domain.Models;
using DesktopReminder.Domain.Services;

/// <summary>
/// 任务调度与提醒协调器（应用层核心）。
///
/// 职责（对应冻结需求 R1 模块）：
///   - 任务生命周期：新增 / 完成 / 稍后 / 恢复 / 删除，即时持久化
///   - 周期心跳 <see cref="Tick"/>：刷新到期状态、触发提醒
///   - 提醒规则：新到期立即提醒一次；已提醒的过期任务按 RemindInterval（默认 5 分钟）重复提醒
///   - 图标显示数据：最早到期任务、角标数字
///
/// 不持有定时器（由 UI 层 Timer 驱动 Tick），纯逻辑可独立单元测试。
/// </summary>
public sealed class ReminderCoordinator
{
    private readonly IReminderTaskRepository _repository;
    private readonly IReminderNotifier _notifier;
    private readonly AppSettings _settings;
    private readonly List<ReminderTask> _tasks;
    private readonly HashSet<Guid> _notifiedIds = new();
    private DateTime _lastRepeatRemindAt = DateTime.MinValue;

    public ReminderCoordinator(IReminderTaskRepository repository, IReminderNotifier notifier, AppSettings settings)
    {
        _repository = repository;
        _notifier = notifier;
        _settings = settings;
        _tasks = _repository.LoadTasks().ToList();
    }

    /// <summary>全部任务（含已完成），顺序稳定</summary>
    public IReadOnlyList<ReminderTask> Tasks => _tasks;

    /// <summary>进行中且最早到期的任务（图标标签倒计时的数据源）；没有返回 null</summary>
    public ReminderTask? EarliestActiveTask => ReminderScheduler.EarliestDue(_tasks);

    /// <summary>指定时刻的到期 + 过期任务总数（图标角标；时间由调用方传入，保证可测试）</summary>
    public int CountDue(DateTime now) => ReminderScheduler.CountDueOrOverdue(_tasks, now);

    /// <summary>新增任务：默认时长取配置，可覆盖；立即持久化</summary>
    public ReminderTask AddTask(string appName, string note, TimeSpan? duration = null, DateTime? now = null, string? processName = null)
    {
        var current = now ?? DateTime.Now;
        var task = new ReminderTask
        {
            AppName = appName,
            Note = note,
            ProcessName = processName,
            Duration = duration ?? TimeSpan.FromMinutes(_settings.DefaultTaskMinutes),
            CreatedAt = current,
            DueAt = current + (duration ?? TimeSpan.FromMinutes(_settings.DefaultTaskMinutes)),
        };

        _tasks.Add(task);
        Save();
        return task;
    }

    /// <summary>完成任务（进入已完成列表）</summary>
    public void CompleteTask(Guid id, DateTime? now = null)
    {
        var task = _tasks.FirstOrDefault(t => t.Id == id);
        if (task is null)
        {
            return;
        }

        task.MarkCompleted(now ?? DateTime.Now);
        _notifiedIds.Remove(id);
        Save();
    }

    /// <summary>稍后提醒：从现在起重新计时（冻结需求 R1.11）</summary>
    public void SnoozeTask(Guid id, TimeSpan span, DateTime? now = null)
    {
        var task = _tasks.FirstOrDefault(t => t.Id == id);
        if (task is null)
        {
            return;
        }

        task.Snooze(span, now ?? DateTime.Now);
        _notifiedIds.Remove(id);
        Save();
    }

    /// <summary>从已完成恢复为进行中：按原时长重新计时（冻结需求 R1.13）</summary>
    public void RestoreTask(Guid id, DateTime? now = null)
    {
        var task = _tasks.FirstOrDefault(t => t.Id == id);
        if (task is null)
        {
            return;
        }

        task.Restore(now ?? DateTime.Now);
        _notifiedIds.Remove(id);
        Save();
    }

    /// <summary>
    /// 删除任务（软删除：进入「已删除」库，数据保留，可恢复，冻结需求 R1.14）。
    /// 从已完成删除 = 确认任务完结不再需要；从进行中删除 = 放弃该任务。
    /// </summary>
    public void DeleteTask(Guid id)
    {
        var task = _tasks.FirstOrDefault(t => t.Id == id);
        if (task is null)
        {
            return;
        }

        task.MarkDeleted();
        _notifiedIds.Remove(id);
        Save();
    }

    /// <summary>彻底删除（从「已删除」库清除，不可恢复）</summary>
    public void HardDeleteTask(Guid id)
    {
        _tasks.RemoveAll(t => t.Id == id);
        _notifiedIds.Remove(id);
        Save();
    }

    /// <summary>从「已删除」库恢复为进行中：从现在起按原时长重新计时</summary>
    public void RestoreDeletedTask(Guid id, DateTime? now = null)
    {
        var task = _tasks.FirstOrDefault(t => t.Id == id);
        if (task is null)
        {
            return;
        }

        task.Restore(now ?? DateTime.Now);
        _notifiedIds.Remove(id);
        Save();
    }

    /// <summary>
    /// 周期心跳（UI 层每秒调用）。
    /// ① 刷新任务状态（到点 → 到期）；
    /// ② 新到期任务立即提醒；已提醒的过期任务按提醒间隔重复提醒；
    /// ③ 任务被处理后清理通知标记。
    /// </summary>
    public void Tick(DateTime now)
    {
        ReminderScheduler.RefreshStatuses(_tasks, now);

        var dueTasks = _tasks.Where(t => t.Status == ReminderTaskStatus.Due).ToList();
        var newlyDue = dueTasks.Where(t => !_notifiedIds.Contains(t.Id)).ToList();
        var repeatDue = dueTasks.Where(t => _notifiedIds.Contains(t.Id)).ToList();

        if (newlyDue.Count > 0)
        {
            _notifier.NotifyDue(newlyDue);
            foreach (var task in newlyDue)
            {
                _notifiedIds.Add(task.Id);
            }

            _lastRepeatRemindAt = now; // 首次提醒也计入节流，避免紧接着重复
        }
        else if (repeatDue.Count > 0 && now - _lastRepeatRemindAt >= _settings.RemindInterval)
        {
            _notifier.NotifyDue(repeatDue);
            _lastRepeatRemindAt = now;
        }

        // 清理已离开到期状态的任务的通知标记（完成/稍后/恢复后不再重复提醒）
        var activeIds = _tasks.Where(t => t.Status != ReminderTaskStatus.Due).Select(t => t.Id).ToHashSet();
        _notifiedIds.RemoveWhere(activeIds.Contains);
    }

    private void Save()
    {
        _repository.SaveTasks(_tasks);
    }
}
