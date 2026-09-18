using DesktopReminder.Domain.Models;
using DesktopReminder.Domain.Services;

namespace DesktopReminder.Tests;

/// <summary>领域层核心逻辑单元测试：到期计算、排序、格式化、状态行为</summary>
public class TaskSchedulerTests
{
    private static readonly DateTime Now = new(2026, 9, 18, 10, 0, 0);

    private static ReminderTask MakeTask(string appName, TimeSpan duration, DateTime? dueAt = null)
    {
        var task = new ReminderTask { AppName = appName, Duration = duration };
        if (dueAt is not null)
        {
            task.DueAt = dueAt.Value;
        }

        return task;
    }

    [Fact]
    public void SortActiveByDue_OrdersByDueAtAscending()
    {
        var later = MakeTask("Later", TimeSpan.FromMinutes(10), Now.AddMinutes(10));
        var earlier = MakeTask("Earlier", TimeSpan.FromMinutes(5), Now.AddMinutes(5));
        var completed = MakeTask("Done", TimeSpan.FromMinutes(5), Now.AddMinutes(1));
        completed.MarkCompleted(Now);

        var sorted = ReminderScheduler.SortActiveByDue(new[] { later, completed, earlier });

        Assert.Equal(2, sorted.Count);
        Assert.Equal("Earlier", sorted[0].AppName);
        Assert.Equal("Later", sorted[1].AppName);
    }

    [Fact]
    public void SortActiveByDue_KeepsAlreadyDueTasksInList()
    {
        // 到期任务（状态已变为 Due）必须仍出现在「进行中」列表，
        // 否则角标显示数字却找不到任务、也无法处理（历史 bug 回归防护）。
        var due = MakeTask("Due", TimeSpan.FromMinutes(1), Now.AddMinutes(-2));
        due.UpdateStatus(Now); // 模拟 Tick 刷新：Active → Due
        var overdue = MakeTask("Overdue", TimeSpan.FromMinutes(1), Now.AddMinutes(-30));
        overdue.UpdateStatus(Now);
        var notDue = MakeTask("NotDue", TimeSpan.FromMinutes(1), Now.AddMinutes(10));

        var sorted = ReminderScheduler.SortActiveByDue(new[] { due, overdue, notDue });

        Assert.Equal(3, sorted.Count);
        Assert.Equal("Overdue", sorted[0].AppName); // 过期最久排最上
        Assert.Equal("Due", sorted[1].AppName);
        Assert.Equal("NotDue", sorted[2].AppName);
    }

    [Fact]
    public void EarliestDue_ReturnsEarliestActiveTask()
    {
        var later = MakeTask("Later", TimeSpan.FromMinutes(10), Now.AddMinutes(10));
        var earlier = MakeTask("Earlier", TimeSpan.FromMinutes(5), Now.AddMinutes(5));

        Assert.Same(earlier, ReminderScheduler.EarliestDue(new[] { later, earlier }));
    }

    [Fact]
    public void EarliestDue_ReturnsNull_WhenNoActiveTasks()
    {
        var completed = MakeTask("Done", TimeSpan.FromMinutes(5));
        completed.MarkCompleted(Now);

        Assert.Null(ReminderScheduler.EarliestDue(new[] { completed }));
    }

    [Fact]
    public void CountDueOrOverdue_CountsOnlyDueTasks()
    {
        var due = MakeTask("Due", TimeSpan.FromMinutes(1), Now.AddMinutes(-2));
        var overdue = MakeTask("Overdue", TimeSpan.FromMinutes(1), Now.AddMinutes(-30));
        var notDue = MakeTask("NotDue", TimeSpan.FromMinutes(1), Now.AddMinutes(10));
        var completed = MakeTask("Completed", TimeSpan.FromMinutes(1), Now.AddMinutes(-1));
        completed.MarkCompleted(Now);

        Assert.Equal(2, ReminderScheduler.CountDueOrOverdue(new[] { due, overdue, notDue, completed }, Now));
    }

    [Fact]
    public void CountDueOrOverdue_StillCountsTasksAlreadyMarkedDue()
    {
        // 任务被 Tick 标记为 Due 后，角标仍应统计它（排除的只有已完成）
        var due = MakeTask("Due", TimeSpan.FromMinutes(1), Now.AddMinutes(-2));
        due.UpdateStatus(Now); // 模拟 Tick 刷新：Active → Due

        Assert.Equal(1, ReminderScheduler.CountDueOrOverdue(new[] { due }, Now));
    }

    [Fact]
    public void EarliestDue_IncludesAlreadyDueTasks()
    {
        var due = MakeTask("Due", TimeSpan.FromMinutes(1), Now.AddMinutes(-2));
        due.UpdateStatus(Now);
        var next = MakeTask("Next", TimeSpan.FromMinutes(1), Now.AddMinutes(10));

        // 已到期任务应优先显示（标签显示 00:00），而不是跳过它显示下一个
        Assert.Same(due, ReminderScheduler.EarliestDue(new[] { next, due }));
    }

    [Fact]
    public void EarliestDue_ExcludesCompleted()
    {
        var completed = MakeTask("Done", TimeSpan.FromMinutes(1), Now.AddMinutes(-5));
        completed.MarkCompleted(Now);
        var next = MakeTask("Next", TimeSpan.FromMinutes(1), Now.AddMinutes(10));

        Assert.Same(next, ReminderScheduler.EarliestDue(new[] { completed, next }));
    }

    [Theory]
    [InlineData(0, "0")]
    [InlineData(1, "1")]
    [InlineData(9, "9")]
    [InlineData(10, "9+")]
    [InlineData(99, "9+")]
    public void BadgeText_FormatsCount(int count, string expected)
    {
        Assert.Equal(expected, ReminderScheduler.BadgeText(count));
    }

    [Theory]
    // 用例数据单位统一为「分钟」：0 → 00:00；0.5 → 00:30；
    // 59.9 秒 = 0.9983 分钟 → 向上取整 01:00；
    // 5 分 32 秒 = 5.5333 分钟 → 05:32；
    // 已过期 → 00:00；
    // 超过 1 小时显示 h:mm:ss：60 分钟 → 01:00:00；125 分钟 → 02:05:00。
    [InlineData(0, "00:00")]
    [InlineData(0.5, "00:30")]
    [InlineData(0.9983, "01:00")]
    [InlineData(5.5333, "05:32")]
    [InlineData(-10, "00:00")]
    [InlineData(60, "01:00:00")]
    [InlineData(125, "02:05:00")]
    public void FormatCountdown_FormatsCountdown(double minutes, string expected)
    {
        var remaining = TimeSpan.FromMinutes(minutes);
        Assert.Equal(expected, ReminderScheduler.FormatCountdown(remaining));
    }

    [Fact]
    public void RefreshStatuses_MarksDueTasksAsDue()
    {
        var due = MakeTask("Due", TimeSpan.FromMinutes(1), Now.AddMinutes(-1));
        var notDue = MakeTask("NotDue", TimeSpan.FromMinutes(1), Now.AddMinutes(10));

        ReminderScheduler.RefreshStatuses(new[] { due, notDue }, Now);

        Assert.Equal(ReminderTaskStatus.Due, due.Status);
        Assert.Equal(ReminderTaskStatus.Active, notDue.Status);
    }

    [Fact]
    public void UpdateStatus_IsIdempotent()
    {
        var due = MakeTask("Due", TimeSpan.FromMinutes(1), Now.AddMinutes(-1));

        ReminderScheduler.RefreshStatuses(new[] { due }, Now);
        ReminderScheduler.RefreshStatuses(new[] { due }, Now);

        Assert.Equal(ReminderTaskStatus.Due, due.Status);
    }

    [Fact]
    public void Snooze_RestartsTimerFromNow()
    {
        var task = MakeTask("Task", TimeSpan.FromMinutes(5), Now.AddMinutes(1));
        task.Snooze(TimeSpan.FromMinutes(10), Now);

        Assert.Equal(ReminderTaskStatus.Active, task.Status);
        Assert.Equal(Now.AddMinutes(10), task.DueAt);
    }

    [Fact]
    public void MarkCompleted_SetsStatusAndTimestamp()
    {
        var task = MakeTask("Task", TimeSpan.FromMinutes(5));

        task.MarkCompleted(Now);

        Assert.Equal(ReminderTaskStatus.Completed, task.Status);
        Assert.Equal(Now, task.CompletedAt);
    }

    [Fact]
    public void Restore_ReactivatesWithFullDurationFromNow()
    {
        var task = MakeTask("Task", TimeSpan.FromMinutes(5));
        task.MarkCompleted(Now);

        // 恢复发生在 30 分钟后，从现在起按原时长 5 分钟重新计时
        task.Restore(Now.AddMinutes(30));

        Assert.Equal(ReminderTaskStatus.Active, task.Status);
        Assert.Equal(Now.AddMinutes(35), task.DueAt);
        Assert.Null(task.CompletedAt);
    }

    [Fact]
    public void DefaultTask_DueAtEqualsCreatedAtPlusDuration()
    {
        var task = new ReminderTask { Duration = TimeSpan.FromMinutes(5) };

        Assert.Equal(task.CreatedAt + TimeSpan.FromMinutes(5), task.DueAt);
    }
}
