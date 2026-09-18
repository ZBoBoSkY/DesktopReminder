namespace DesktopReminder.Infrastructure.Notifications;

using System.Media;
using DesktopReminder.Core.Services;
using DesktopReminder.Domain.Models;

/// <summary>
/// 默认提醒实现：系统提示音（冻结需求确认：不用音乐文件，保持体积小）。
/// 声音开关（SoundEnabled）由 UI 层在调用前判断，或后续版本在此实现。
/// </summary>
public sealed class SystemReminderNotifier : IReminderNotifier
{
    private readonly bool _enabled;

    public SystemReminderNotifier(bool enabled = true)
    {
        _enabled = enabled;
    }

    public void NotifyDue(IReadOnlyList<ReminderTask> dueTasks)
    {
        if (!_enabled)
        {
            return;
        }

        SystemSounds.Exclamation.Play();
    }
}
