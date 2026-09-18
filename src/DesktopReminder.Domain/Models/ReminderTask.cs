namespace DesktopReminder.Domain.Models;

/// <summary>
/// 一条提醒任务。
/// 领域实体：只包含数据与自身行为（到期计算、完成、恢复、稍后），
/// 不依赖任何界面框架，可被单元测试直接验证。
/// </summary>
public sealed class ReminderTask
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>软件名称（用户友好名，如「飞书」「文件管理器」）</summary>
    public string AppName { get; set; } = string.Empty;

    /// <summary>备注（说明这个软件具体在做什么事，可为空）</summary>
    public string Note { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>任务时长（默认 5 分钟）</summary>
    public TimeSpan Duration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>到期时间。进行中任务的唯一计时依据。</summary>
    public DateTime DueAt { get; set; }

    public ReminderTaskStatus Status { get; set; } = ReminderTaskStatus.Active;

    public DateTime? CompletedAt { get; set; }

    public ReminderTask()
    {
        DueAt = CreatedAt + Duration;
    }

    /// <summary>剩余时间（负数表示已过期）</summary>
    public TimeSpan Remaining(DateTime now) => DueAt - now;

    /// <summary>是否已到期（仅进行中任务参与判断）</summary>
    public bool IsDue(DateTime now) => Status == ReminderTaskStatus.Active && DueAt <= now;

    /// <summary>刷新状态：进行中且到点 → 到期（Due）。幂等。</summary>
    public void UpdateStatus(DateTime now)
    {
        if (Status == ReminderTaskStatus.Active && DueAt <= now)
        {
            Status = ReminderTaskStatus.Due;
        }
    }

    /// <summary>稍后提醒：从现在起重新计时（不是顺延）</summary>
    public void Snooze(TimeSpan span, DateTime now)
    {
        Status = ReminderTaskStatus.Active;
        DueAt = now + span;
    }

    /// <summary>标记完成</summary>
    public void MarkCompleted(DateTime now)
    {
        Status = ReminderTaskStatus.Completed;
        CompletedAt = now;
    }

    /// <summary>从已完成恢复为进行中：从现在起按原时长重新计时</summary>
    public void Restore(DateTime now)
    {
        Status = ReminderTaskStatus.Active;
        DueAt = now + Duration;
        CompletedAt = null;
    }
}
