namespace DesktopReminder.Infrastructure.Windows;

using System.Runtime.InteropServices;
using System.Text.Json;
using DesktopReminder.Core.Services;

/// <summary>
/// 前台窗口软件名称提供器（冻结需求 R1.2 / R1.3）。
///
/// 识别策略（多级回退，按用户确认的方案）：
///   1. Windows API 取前台窗口所属进程；
///   2. 进程名（不含 .exe）查映射表 → 中文友好名（如 explorer → 文件管理器）；
///   3. 映射表支持用户自定义文件（exe 同级 app_names.json），用户条目优先；
///   4. 查不到映射 → 返回英文进程名原样（不拼接窗口标题、不显示 EXE 标识）。
/// </summary>
public sealed class ForegroundAppNameProvider : IAppNameProvider
{
    private readonly IReadOnlyDictionary<string, string> _map;

    public ForegroundAppNameProvider(string? mappingFilePath = null)
    {
        _map = LoadMapping(mappingFilePath);
    }

    public string GetPreviousAppName()
    {
        try
        {
            var hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero)
            {
                return "未知";
            }

            GetWindowThreadProcessId(hwnd, out var processId);
            using var process = System.Diagnostics.Process.GetProcessById((int)processId);
            var processName = process.ProcessName; // 不含 .exe
            if (_map.TryGetValue(processName.ToLowerInvariant(), out var friendlyName))
            {
                return friendlyName;
            }

            return processName;
        }
        catch (Exception)
        {
            // 进程已退出 / 权限不足等：返回占位，由上层日志兜底
            return "未知";
        }
    }

    private static IReadOnlyDictionary<string, string> LoadMapping(string? filePath)
    {
        var map = new Dictionary<string, string>(BuiltinMapping, StringComparer.OrdinalIgnoreCase);

        if (filePath is not null && File.Exists(filePath))
        {
            try
            {
                var custom = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(filePath));
                if (custom is not null)
                {
                    foreach (var (key, value) in custom)
                    {
                        map[key] = value; // 用户配置优先，可覆盖内置
                    }
                }
            }
            catch (JsonException)
            {
                // 用户配置文件损坏：忽略，仅用内置映射
            }
        }

        return map;
    }

    /// <summary>内置映射表（与冻结需求文档附录一一致）</summary>
    private static readonly IReadOnlyDictionary<string, string> BuiltinMapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        // 浏览器
        ["chrome"] = "谷歌浏览器",
        ["msedge"] = "Microsoft Edge 浏览器",
        ["firefox"] = "Firefox 火狐浏览器",
        // 通讯办公
        ["wechat"] = "微信",
        ["qq"] = "QQ",
        ["wxwork"] = "企业微信",
        ["feishu"] = "飞书",
        ["lark"] = "飞书",
        ["dingtalk"] = "钉钉",
        ["wemeetapp"] = "腾讯会议",
        ["doubaowork"] = "豆包工作",
        // 文档处理
        ["winword"] = "Microsoft Word",
        ["excel"] = "Microsoft Excel",
        ["powerpnt"] = "Microsoft PowerPoint",
        ["wps"] = "WPS 办公软件",
        ["et"] = "WPS 表格",
        ["wpp"] = "WPS 演示",
        ["acrobat"] = "Adobe Acrobat",
        ["foxitreader"] = "福昕阅读器",
        // 开发工具
        ["code"] = "Visual Studio Code",
        ["devenv"] = "Visual Studio",
        ["idea64"] = "IntelliJ IDEA",
        ["pycharm64"] = "PyCharm",
        ["windowsterminal"] = "Windows 终端",
        ["cmd"] = "命令提示符",
        ["powershell"] = "Windows PowerShell",
        // 系统工具
        ["explorer"] = "文件管理器",
        ["taskmgr"] = "任务管理器",
        ["control"] = "控制面板",
        ["regedit"] = "注册表编辑器",
        ["mspaint"] = "画图",
        ["7zfm"] = "7-Zip 压缩管理器",
        // 影音网盘
        ["potplayermini64"] = "PotPlayer 播放器",
        ["cloudmusic"] = "网易云音乐",
        ["spotify"] = "Spotify 音乐",
        ["baidunetdisk"] = "百度网盘",
        ["quarkclouddrive"] = "夸克网盘",
        // 其他
        ["thunder"] = "迅雷",
        ["winrar"] = "WinRAR 压缩管理器",
        ["typora"] = "Typora 编辑器",
        ["obsidian"] = "Obsidian 笔记",
        ["xmind"] = "XMind 思维导图",
    };

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
}
