using DesktopReminder.Domain.Models;
using DesktopReminder.Infrastructure.Persistence;

namespace DesktopReminder.Tests;

/// <summary>JSON 持久化测试：任务与配置的保存/加载/容错</summary>
public class JsonPersistenceTests
{
    private static string TempPath(string name)
    {
        var dir = Path.Combine(Path.GetTempPath(), "desktop-reminder-tests", Guid.NewGuid().ToString("N"));
        return Path.Combine(dir, name);
    }

    [Fact]
    public void TaskRepository_SavesAndLoads_Tasks()
    {
        var path = TempPath("tasks.json");
        var repo = new JsonTaskRepository(path);
        var task = new ReminderTask
        {
            AppName = "飞书",
            Note = "回复审批",
            Duration = TimeSpan.FromMinutes(15),
            DueAt = DateTime.Now.AddMinutes(15),
        };

        repo.SaveTasks(new[] { task });
        var loaded = repo.LoadTasks();

        Assert.Single(loaded);
        Assert.Equal("飞书", loaded[0].AppName);
        Assert.Equal("回复审批", loaded[0].Note);
        Assert.Equal(TimeSpan.FromMinutes(15), loaded[0].Duration);
    }

    [Fact]
    public void TaskRepository_LoadMissingFile_ReturnsEmpty()
    {
        var repo = new JsonTaskRepository(TempPath("not-exists.json"));

        Assert.Empty(repo.LoadTasks());
    }

    [Fact]
    public void TaskRepository_CorruptFile_ReturnsEmpty_NotCrash()
    {
        var path = TempPath("corrupt.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{ not valid json !!");

        var repo = new JsonTaskRepository(path);

        Assert.Empty(repo.LoadTasks());
    }

    [Fact]
    public void SettingsRepository_SavesAndLoads_AllValues()
    {
        var path = TempPath("settings.json");
        var repo = new JsonSettingsRepository(path);
        var settings = new AppSettings
        {
            GlassTimeout = TimeSpan.FromMinutes(3),
            IconTimeout = TimeSpan.FromSeconds(45),
            GlassOpacity = 0.25,
            RemindInterval = TimeSpan.FromMinutes(10),
            DefaultTaskMinutes = 7,
            Theme = ThemeMode.Dark,
            SoundEnabled = false,
            AutoStart = true,
            WindowX = 100,
            WindowY = 200,
            IconX = 50,
        };

        repo.SaveSettings(settings);
        var loaded = repo.LoadSettings();

        Assert.Equal(TimeSpan.FromMinutes(3), loaded.GlassTimeout);
        Assert.Equal(TimeSpan.FromSeconds(45), loaded.IconTimeout);
        Assert.Equal(0.25, loaded.GlassOpacity);
        Assert.Equal(TimeSpan.FromMinutes(10), loaded.RemindInterval);
        Assert.Equal(7, loaded.DefaultTaskMinutes);
        Assert.Equal(ThemeMode.Dark, loaded.Theme);
        Assert.False(loaded.SoundEnabled);
        Assert.True(loaded.AutoStart);
        Assert.Equal(100, loaded.WindowX);
        Assert.Equal(200, loaded.WindowY);
        Assert.Equal(50, loaded.IconX);
    }

    [Fact]
    public void SettingsRepository_LoadMissingFile_ReturnsDefaults()
    {
        var repo = new JsonSettingsRepository(TempPath("not-exists.json"));

        var loaded = repo.LoadSettings();

        Assert.Equal(TimeSpan.FromMinutes(5), loaded.GlassTimeout);
        Assert.Equal(TimeSpan.FromSeconds(30), loaded.IconTimeout);
        Assert.Equal(5, loaded.DefaultTaskMinutes);
        Assert.Equal(ThemeMode.FollowSystem, loaded.Theme);
    }

    [Fact]
    public void SettingsRepository_CorruptFile_ReturnsDefaults()
    {
        var path = TempPath("corrupt-settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "<<<>>>");

        var loaded = new JsonSettingsRepository(path).LoadSettings();

        Assert.Equal(TimeSpan.FromMinutes(5), loaded.GlassTimeout);
    }
}
