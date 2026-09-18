using System.Runtime.InteropServices;
using System.Windows;

namespace DesktopReminder.App.WindowsApi;

/// <summary>
/// 窗口级交互原生封装（冻结需求 R3.1~R3.3）。
/// 拖拽/调大小统一走 WM_NCHITTEST（系统级处理，不被子控件拦截——这是旧版反复出 bug 的根治方案）。
/// </summary>
public static class NativeWindow
{
    public const int WM_NCHITTEST = 0x0084;
    public const int HTCLIENT = 1;
    public const int HTCAPTION = 2;
    public const int HTBOTTOMRIGHT = 17;

    /// <summary>
    /// 计算 WM_NCHITTEST 命中区域。
    /// </summary>
    /// <param name="screenPoint">鼠标屏幕坐标（WM_NCHITTEST 的 lParam）</param>
    /// <param name="windowRect">窗口屏幕矩形</param>
    /// <param name="titleBarHeight">标题栏高度（CSS 像素）</param>
    /// <param name="resizeGripSize">右下角调大小热区边长</param>
    /// <param name="resizeEnabled">是否允许调大小</param>
    /// <param name="buttonZone">标题栏按钮区域（返回 HTCLIENT 保持按钮可点击）；null 表示整个标题栏可拖拽</param>
    public static IntPtr HitTest(
        Point screenPoint,
        Rect windowRect,
        double titleBarHeight,
        double resizeGripSize,
        bool resizeEnabled,
        Rect? buttonZone)
    {
        var x = screenPoint.X - windowRect.Left;
        var y = screenPoint.Y - windowRect.Top;

        // 1) 右下角调大小热区（只有这一个热区，其他边缘不响应——冻结需求 R3.3）
        if (resizeEnabled &&
            x >= windowRect.Width - resizeGripSize &&
            y >= windowRect.Height - resizeGripSize)
        {
            return (IntPtr)HTBOTTOMRIGHT;
        }

        // 2) 标题栏整行可拖拽；按钮区域例外（保持可点击）
        if (y <= titleBarHeight)
        {
            if (buttonZone is not null && buttonZone.Value.Contains(new Point(x, y)))
            {
                return (IntPtr)HTCLIENT;
            }

            return (IntPtr)HTCAPTION;
        }

        return (IntPtr)HTCLIENT;
    }

    [DllImport("user32.dll")]
    public static extern IntPtr GetCursorPos(out POINT pt);

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
