using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace DesktopReminder.App.WindowsApi;

/// <summary>
/// 按进程名把对应软件窗口切到前台。
/// 根治两个老问题：
/// 1) 很多进程（多窗口浏览器、UWP、启动器）process.MainWindowHandle 为 0，直接取会找不到；
///    改为 EnumWindows 枚举所有顶层可见窗口，按所属 PID 反查。
/// 2) 后台程序调 SetForegroundWindow 常被 Windows 前台锁拦住，切不上去；
///    用 AttachThreadInput 把本线程挂到前台线程的输入队列，绕过限制。
/// </summary>
public static class WindowSwitcher
{
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern int GetWindowTextLength(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr lpdwProcessId);
    [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
    [DllImport("user32.dll")] private static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool BringWindowToTop(IntPtr hWnd);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();

    private const int SW_RESTORE = 9;

    /// <summary>按进程名切前台；返回 true 表示成功切到一个可见窗口。</summary>
    public static bool SwitchTo(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return false;
        }

        // 1) 目标进程的全部 PID（一个进程名可能多个实例）
        var pids = new HashSet<uint>();
        try
        {
            foreach (var p in Process.GetProcessesByName(processName))
            {
                pids.Add((uint)p.Id);
                p.Dispose();
            }
        }
        catch
        {
            return false;
        }

        if (pids.Count == 0)
        {
            return false;
        }

        // 2) EnumWindows 找属于这些 PID 的第一个可见、有标题的顶层窗口
        IntPtr target = IntPtr.Zero;
        EnumWindows((hWnd, _) =>
        {
            if (!IsWindowVisible(hWnd) || GetWindowTextLength(hWnd) == 0)
            {
                return true;
            }

            GetWindowThreadProcessId(hWnd, out uint pid);
            if (pids.Contains(pid))
            {
                target = hWnd;
                return false; // 找到第一个就停
            }

            return true;
        }, IntPtr.Zero);

        if (target == IntPtr.Zero)
        {
            return false;
        }

        // 3) 绕过前台锁：挂到当前前台线程的输入队列
        var foreHwnd = GetForegroundWindow();
        var foreThread = GetWindowThreadProcessId(foreHwnd, IntPtr.Zero);
        var myThread = GetCurrentThreadId();
        var attached = AttachThreadInput(myThread, foreThread, true);

        try
        {
            if (IsIconic(target))
            {
                ShowWindowAsync(target, SW_RESTORE);
            }

            BringWindowToTop(target);
            SetForegroundWindow(target);
        }
        finally
        {
            if (attached)
            {
                AttachThreadInput(myThread, foreThread, false);
            }
        }

        return true;
    }
}
