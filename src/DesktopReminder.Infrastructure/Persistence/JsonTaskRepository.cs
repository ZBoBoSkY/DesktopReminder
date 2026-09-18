namespace DesktopReminder.Infrastructure.Persistence;

using System.Text.Json;
using DesktopReminder.Domain.Models;
using DesktopReminder.Domain.Services;

/// <summary>
/// 任务持久化：JSON 文件存储。
/// 便携版原则：数据写在 exe 同级的 data/ 目录，随软件一起带走。
/// 读损坏/缺失时返回空列表，绝不让软件启动失败（日志由上层兜底）。
/// </summary>
public sealed class JsonTaskRepository : IReminderTaskRepository
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private readonly string _filePath;

    public JsonTaskRepository(string filePath)
    {
        _filePath = filePath;
    }

    public IReadOnlyList<ReminderTask> LoadTasks()
    {
        if (!File.Exists(_filePath))
        {
            return new List<ReminderTask>();
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<List<ReminderTask>>(json) ?? new List<ReminderTask>();
        }
        catch (JsonException)
        {
            // 文件损坏：不崩溃，返回空列表（旧数据保留在磁盘上便于人工恢复）
            return new List<ReminderTask>();
        }
    }

    public void SaveTasks(IEnumerable<ReminderTask> tasks)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(tasks.ToList(), Options);
        File.WriteAllText(_filePath, json);
    }
}
