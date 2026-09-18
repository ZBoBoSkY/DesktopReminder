using DesktopReminder.Core.Services;
using DesktopReminder.Domain.Models;
using DesktopReminder.Domain.Services;

namespace DesktopReminder.Tests;

/// <summary>任务协调器测试：生命周期、到期提醒、重复提醒、节流</summary>
public class ReminderCoordinatorTests
{
    private static readonly DateTime Now = new(2026, 9, 19, 9, 0, 0);

    private sealed class InMemoryTaskRepository : IReminderTaskRepository
    {
        public List<ReminderTask> Items { get; } = new();

        public IReadOnlyList<ReminderTask> LoadTasks() => Items;

        public void SaveTasks(IEnumerable<ReminderTask> tasks)
        {
            Items.Clear();
            Items.AddRange(tasks);
        }
    }

    private sealed class RecordingNotifier : IReminderNotifier
    {
        public List<List<ReminderTask>> Calls { get; } = new();

        public void NotifyDue(IReadOnlyList<ReminderTask> dueTasks) => Calls.Add(dueTasks.ToList());
    }

    private static (ReminderCoordinator Coordinator, InMemoryTaskRepository Repo, RecordingNotifier Notifier) Create(
        int remindIntervalMinutes = 5)
    {
        var repo = new InMemoryTaskRepository();
        var notifier = new RecordingNotifier();
        var settings = new AppSettings
        {
            DefaultTaskMinutes = 5,
            RemindInterval = TimeSpan.FromMinutes(remindIntervalMinutes),
        };
        var coordinator = new ReminderCoordinator(repo, notifier, settings);
        return (coordinator, repo, notifier);
    }

    [Fact]
    public void AddTask_WithDefaultDuration_UsesSettingsMinutes()
    {
        var (coordinator, _, _) = Create();

        var task = coordinator.AddTask("飞书", "回复审批", null, Now);

        Assert.Equal("飞书", task.AppName);
        Assert.Equal("回复审批", task.Note);
        Assert.Equal(TimeSpan.FromMinutes(5), task.Duration);
        Assert.Equal(Now.AddMinutes(5), task.DueAt);
    }

    [Fact]
    public void AddTask_WithCustomDuration_UsesGivenSpan()
    {
        var (coordinator, _, _) = Create();

        var task = coordinator.AddTask("文件管理器", "", TimeSpan.FromMinutes(15), Now);

        Assert.Equal(TimeSpan.FromMinutes(15), task.Duration);
        Assert.Equal(Now.AddMinutes(15), task.DueAt);
    }

    [Fact]
    public void AddTask_PersistsToRepository()
    {
        var (coordinator, repo, _) = Create();

        coordinator.AddTask("微信", "等回复", null, Now);

        Assert.Single(repo.Items);
        Assert.Equal("微信", repo.Items[0].AppName);
    }

    [Fact]
    public void CompleteTask_MovesToCompleted()
    {
        var (coordinator, _, _) = Create();
        var task = coordinator.AddTask("钉钉", "", null, Now);

        coordinator.CompleteTask(task.Id, Now.AddMinutes(1));

        Assert.Equal(ReminderTaskStatus.Completed, task.Status);
        Assert.Equal(Now.AddMinutes(1), task.CompletedAt);
    }

    [Fact]
    public void SnoozeTask_RestartsFromNow()
    {
        var (coordinator, _, _) = Create();
        var task = coordinator.AddTask("Word", "", null, Now);

        coordinator.SnoozeTask(task.Id, TimeSpan.FromMinutes(10), Now.AddMinutes(1));

        Assert.Equal(Now.AddMinutes(11), task.DueAt);
        Assert.Equal(ReminderTaskStatus.Active, task.Status);
    }

    [Fact]
    public void RestoreTask_ReactivatesWithFullDuration()
    {
        var (coordinator, _, _) = Create();
        var task = coordinator.AddTask("Excel", "", TimeSpan.FromMinutes(5), Now);
        coordinator.CompleteTask(task.Id, Now.AddMinutes(5));

        coordinator.RestoreTask(task.Id, Now.AddMinutes(30));

        Assert.Equal(ReminderTaskStatus.Active, task.Status);
        Assert.Equal(Now.AddMinutes(35), task.DueAt);
        Assert.Null(task.CompletedAt);
    }

    [Fact]
    public void DeleteTask_RemovesFromList()
    {
        var (coordinator, _, _) = Create();
        var task = coordinator.AddTask("Chrome", "", null, Now);

        coordinator.DeleteTask(task.Id);

        Assert.Empty(coordinator.Tasks);
    }

    [Fact]
    public void Tick_NotifiesOnce_WhenTaskBecomesDue()
    {
        var (coordinator, _, notifier) = Create();
        coordinator.AddTask("飞书", "", TimeSpan.FromMinutes(5), Now);

        coordinator.Tick(Now.AddMinutes(5)); // 正好到点

        Assert.Single(notifier.Calls);
        Assert.Single(notifier.Calls[0]);
        Assert.Equal("飞书", notifier.Calls[0][0].AppName);
    }

    [Fact]
    public void Tick_DoesNotRepeatNotify_BeforeRemindInterval()
    {
        var (coordinator, _, notifier) = Create();
        coordinator.AddTask("飞书", "", TimeSpan.FromMinutes(5), Now);

        coordinator.Tick(Now.AddMinutes(5));
        coordinator.Tick(Now.AddMinutes(7)); // 距上次提醒 2 分钟 < 5 分钟

        Assert.Single(notifier.Calls);
    }

    [Fact]
    public void Tick_RepeatsNotify_AfterRemindInterval()
    {
        var (coordinator, _, notifier) = Create();
        coordinator.AddTask("飞书", "", TimeSpan.FromMinutes(5), Now);

        coordinator.Tick(Now.AddMinutes(5)); // 第 1 次
        coordinator.Tick(Now.AddMinutes(11)); // 距上次 6 分钟 ≥ 5 分钟 → 第 2 次

        Assert.Equal(2, notifier.Calls.Count);
    }

    [Fact]
    public void Tick_StopsNotifying_AfterTaskHandled()
    {
        var (coordinator, _, notifier) = Create();
        var task = coordinator.AddTask("钉钉", "", TimeSpan.FromMinutes(5), Now);

        coordinator.Tick(Now.AddMinutes(5)); // 到期提醒
        coordinator.CompleteTask(task.Id, Now.AddMinutes(6)); // 用户处理
        coordinator.Tick(Now.AddMinutes(20)); // 不再提醒

        Assert.Single(notifier.Calls);
    }

    [Fact]
    public void EarliestActiveTask_ReturnsNearestDue()
    {
        var (coordinator, _, _) = Create();
        coordinator.AddTask("A", "", TimeSpan.FromMinutes(5), Now);
        coordinator.AddTask("B", "", TimeSpan.FromMinutes(30), Now);

        Assert.Equal("A", coordinator.EarliestActiveTask?.AppName);
    }

    [Fact]
    public void CountDue_CountsDueAndOverdue()
    {
        var (coordinator, _, _) = Create();
        coordinator.AddTask("A", "", TimeSpan.FromMinutes(5), Now);
        coordinator.AddTask("B", "", TimeSpan.FromMinutes(30), Now);

        coordinator.Tick(Now.AddMinutes(10)); // A 到期，B 未到期

        Assert.Equal(1, coordinator.CountDue(Now.AddMinutes(10)));
        Assert.Equal(2, coordinator.CountDue(Now.AddMinutes(40))); // 两件都过期
    }
}
