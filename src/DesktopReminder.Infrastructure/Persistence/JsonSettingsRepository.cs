namespace DesktopReminder.Infrastructure.Persistence;

using System.Text.Json;
using DesktopReminder.Domain.Models;
using DesktopReminder.Domain.Services;

/// <summary>配置持久化：JSON 文件存储（与任务数据同目录）</summary>
public sealed class JsonSettingsRepository : IAppSettingsRepository
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private readonly string _filePath;

    public JsonSettingsRepository(string filePath)
    {
        _filePath = filePath;
    }

    public AppSettings LoadSettings()
    {
        if (!File.Exists(_filePath))
        {
            return new AppSettings();
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch (JsonException)
        {
            return new AppSettings();
        }
    }

    public void SaveSettings(AppSettings settings)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(settings, Options);
        File.WriteAllText(_filePath, json);
    }
}
