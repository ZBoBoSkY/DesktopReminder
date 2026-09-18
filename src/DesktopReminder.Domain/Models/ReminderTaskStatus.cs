namespace DesktopReminder.Domain.Models;

/// <summary>任务状态</summary>
public enum ReminderTaskStatus
{
    /// <summary>进行中（倒计时中）</summary>
    Active = 0,

    /// <summary>已到期未处理（红色醒目显示，等待用户处理）</summary>
    Due = 1,

    /// <summary>已完成（进入已完成列表，可恢复或删除）</summary>
    Completed = 2
}
