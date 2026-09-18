namespace DesktopReminder.Domain.Services;

using DesktopReminder.Domain.Models;

/// <summary>配置仓储接口（由基础设施层实现）</summary>
public interface IAppSettingsRepository
{
    AppSettings LoadSettings();

    void SaveSettings(AppSettings settings);
}
