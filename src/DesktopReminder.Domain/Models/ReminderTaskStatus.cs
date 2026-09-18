namespace DesktopReminder.Domain.Models;

/// <summary>
/// 任务状态（三部分结构：进行中 / 已完成 / 已删除）
/// </summary>
public enum ReminderTaskStatus
{
    /// <summary>进行中（倒计时中）</summary>
    Active = 0,

    /// <summary>已到期未处理（红色醒目显示，等待用户处理）</summary>
    Due = 1,

    /// <summary>已完成（进入已完成库，可恢复激活或删除）</summary>
    Completed = 2,

    /// <summary>已删除（软删除，进入已删除库，可恢复；彻底清除需硬删除）</summary>
    Deleted = 3
}
