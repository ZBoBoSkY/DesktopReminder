namespace DesktopReminder.Core.Services;

/// <summary>
/// 获取上一个操作的软件名称（新建任务时自动填入）。
/// 由基础设施层实现（前台窗口进程名 → 中文映射表）。
/// </summary>
public interface IAppNameProvider
{
    /// <summary>返回上一个前台软件的用户友好名称；无法识别时返回「未知」</summary>
    string GetPreviousAppName();
}
