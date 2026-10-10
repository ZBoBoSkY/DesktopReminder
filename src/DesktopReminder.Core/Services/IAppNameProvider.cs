namespace DesktopReminder.Core.Services;

/// <summary>
/// 获取上一个操作的软件名称（新建任务时自动填入）。
/// 由基础设施层实现（前台窗口进程名 → 中文映射表）。
/// </summary>
public interface IAppNameProvider
{
    /// <summary>
    /// 轮询刷新（UI 层每秒调用）：记录上一个非本软件的前台窗口。
    /// 为什么需要轮询：用户点「新建任务」那一刻前台已经是本软件自己，
    /// 只有在点击之前持续记录，才能拿到用户真正上一个操作的软件。
    /// </summary>
    /// <param name="ownHandle">本软件主窗口句柄（跳过自己）</param>
    void Refresh(IntPtr ownHandle);

    /// <summary>返回上一个前台软件的用户友好名称；无法识别时返回「未知」</summary>
    string GetPreviousAppName();

    /// <summary>返回上一个前台软件的进程名（不含 .exe，用于取真实图标）；无可用时返回 null</summary>
    string? GetPreviousProcessName();
}
