namespace DesktopReminder.Domain.Services;

using DesktopReminder.Domain.Models;

/// <summary>
/// 任务核心计算逻辑（纯静态函数，不依赖界面与存储，可独立单元测试）。
/// 所有「时间怎么算、怎么排、怎么显示」的规则都集中在这里，
/// 避免同样规则在界面层各写一份导致不一致。
/// </summary>
public static class ReminderScheduler
{
    /// <summary>进行中任务按到期时间升序排列（最早到期的在最上面）</summary>
    public static IReadOnlyList<ReminderTask> SortActiveByDue(IEnumerable<ReminderTask> tasks)
    {
        return tasks.Where(t => t.Status == ReminderTaskStatus.Active)
                    .OrderBy(t => t.DueAt)
                    .ToList();
    }

    /// <summary>
    /// 最早到期任务（未完成未删除任务的 DueAt 最早者，含已到期/过期）；
    /// 用于图标标签倒计时：有任务已过期就显示它（00:00），没有则显示下一个即将到期的。没有返回 null。
    /// </summary>
    public static ReminderTask? EarliestDue(IEnumerable<ReminderTask> tasks)
    {
        return tasks.Where(t => t.Status != ReminderTaskStatus.Completed && t.Status != ReminderTaskStatus.Deleted)
                    .OrderBy(t => t.DueAt)
                    .FirstOrDefault();
    }

    /// <summary>
    /// 到期 + 过期任务总数（用于图标角标数字）。
    /// 语义：所有未完成任务中已到点的（含已被标记为 Due 的），排除已完成与已删除。
    /// </summary>
    public static int CountDueOrOverdue(IEnumerable<ReminderTask> tasks, DateTime now)
    {
        return tasks.Count(t =>
            t.Status != ReminderTaskStatus.Completed &&
            t.Status != ReminderTaskStatus.Deleted &&
            t.DueAt <= now);
    }

    /// <summary>角标数字格式化：超过 9 显示「9+」</summary>
    public static string BadgeText(int count)
    {
        return count > 9 ? "9+" : count.ToString();
    }

    /// <summary>
    /// 倒计时格式化。
    /// 不足 1 小时：mm:ss（如 05:32）；达到 1 小时：h:mm:ss（如 1:05:32）。
    /// 已过期显示 00:00。取整规则：向上取整，保证「还剩 0.5 秒」显示 00:01 而非 00:00。
    /// </summary>
    public static string FormatCountdown(TimeSpan remaining)
    {
        if (remaining < TimeSpan.Zero)
        {
            remaining = TimeSpan.Zero;
        }

        var totalSeconds = (int)Math.Ceiling(remaining.TotalSeconds);
        if (totalSeconds >= 3600)
        {
            return $"{(totalSeconds / 3600):00}:{(totalSeconds % 3600 / 60):00}:{(totalSeconds % 60):00}";
        }

        return $"{(totalSeconds / 60):00}:{(totalSeconds % 60):00}";
    }

    /// <summary>批量刷新进行中任务的状态（到点 → 到期），幂等</summary>
    public static void RefreshStatuses(IEnumerable<ReminderTask> tasks, DateTime now)
    {
        foreach (var task in tasks)
        {
            task.UpdateStatus(now);
        }
    }
}
