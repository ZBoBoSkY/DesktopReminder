namespace DesktopReminder.Domain.Services;

using DesktopReminder.Domain.Models;

/// <summary>任务仓储接口（数据持久化的边界，由基础设施层实现）</summary>
public interface IReminderTaskRepository
{
    IReadOnlyList<ReminderTask> LoadTasks();

    void SaveTasks(IEnumerable<ReminderTask> tasks);
}
