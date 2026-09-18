namespace DesktopReminder.Infrastructure.Notifications;

using System.Media;
using DesktopReminder.Core.Services;
using DesktopReminder.Domain.Models;

/// <summary>
/// 默认提醒实现：系统提示音（冻结需求确认：不用音乐文件，保持体积小）。
/// <see cref="Enabled"/> 可运行时切换（设置界面「声音提醒」开关）。
/// </summary>
public sealed class SystemReminderNotifier : IReminderNotifier
{
    public SystemReminderNotifier(bool enabled = true)
    {
        Enabled = enabled;
    }

    /// <summary>声音开关（设置界面即时生效）</summary>
    public bool Enabled { get; set; }

    public void NotifyDue(IReadOnlyList<ReminderTask> dueTasks)
    {
        if (!Enabled)
        {
            return;
        }

        SystemSounds.Exclamation.Play();
    }
}
