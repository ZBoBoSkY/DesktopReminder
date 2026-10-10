using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DesktopReminder.App.Views;

/// <summary>
/// 任务行软件图标缓存（第 2 步）：按进程名取真实软件图标（SHGetFileInfo 提取 exe 关联图标）。
/// - 进程名 → exe 路径：先查运行中进程的 MainModule，再回退注册表 App Paths
/// - 取图标：System.Drawing.Icon.ExtractAssociatedIcon（32px 系统图标）
/// - 转 WPF ImageSource 并 Freeze，按进程名缓存，避免每次重建 GDI 句柄
/// - 取不到图标（进程已退出且无 App Paths 注册）返回 null，UI 回退显示首字母块
/// </summary>
public static class AppIconCache
{
    private static readonly ConcurrentDictionary<string, ImageSource> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, string?> ExePathCache = new(StringComparer.OrdinalIgnoreCase);

    public static ImageSource? GetIcon(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return null;
        }

        if (Cache.TryGetValue(processName, out var cached))
        {
            return cached;
        }

        var icon = LoadFromProcess(processName);
        if (icon is not null)
        {
            Cache[processName] = icon;
        }

        return icon;
    }

    private static ImageSource? LoadFromProcess(string processName)
    {
        try
        {
            var exePath = FindExePath(processName);
            if (exePath is null)
            {
                return null;
            }

            using var icon = Icon.ExtractAssociatedIcon(exePath);
            if (icon is null)
            {
                return null;
            }

            // CreateBitmapSourceFromHIcon 会复制像素，不持有句柄，icon 释放后仍有效
            var source = Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        catch (Exception)
        {
            // 无图标资源 / 权限不足 / 文件占用：静默回退首字母块
            return null;
        }
    }

    private static string? FindExePath(string processName)
    {
        if (ExePathCache.TryGetValue(processName, out var cached))
        {
            return cached;
        }

        string? path = null;

        // 1) 运行中的进程 MainModule（最常见路径）
        try
        {
            foreach (var p in System.Diagnostics.Process.GetProcessesByName(processName))
            {
                using var _ = p;
                try
                {
                    var f = p.MainModule?.FileName;
                    if (!string.IsNullOrEmpty(f) && File.Exists(f))
                    {
                        path = f;
                        break;
                    }
                }
                catch
                {
                    // 32 位/64 位访问受限的进程，跳过
                }
            }
        }
        catch
        {
            // 枚举失败，走注册表
        }

        // 2) 注册表 App Paths 兜底（进程已退出时也能找到安装路径）
        if (path is null)
        {
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                    $@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\{processName}.exe");
                var candidate = key?.GetValue("") as string;
                if (!string.IsNullOrEmpty(candidate) && File.Exists(candidate))
                {
                    path = candidate;
                }
            }
            catch
            {
                // 注册表不可读，返回 null
            }
        }

        ExePathCache[processName] = path;
        return path;
    }
}
