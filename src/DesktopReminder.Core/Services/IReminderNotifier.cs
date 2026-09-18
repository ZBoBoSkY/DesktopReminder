namespace DesktopReminder.Core.Services;

using DesktopReminder.Domain.Models;

/// <summary>
/// 提醒通知出口（声音/系统通知），由 UI 层实现。
/// 应用层只负责「什么时候该提醒」，不负责「怎么提醒」。
/// </summary>
public interface IReminderNotifier
{
    /// <summary>任务到期提醒（可携带到期任务列表）</summary>
    void NotifyDue(IReadOnlyList<ReminderTask> dueTasks);
}
