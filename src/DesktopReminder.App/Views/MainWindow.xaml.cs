using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using DesktopReminder.App.ViewModels;
using DesktopReminder.App.WindowsApi;

namespace DesktopReminder.App.Views;

public partial class MainWindow : ThemeWindow
{
    public const double TitleBarHeight = 36;   // DIP
    public const double ResizeGripSize = 22;   // DIP
    private const double ButtonZoneWidth = 150; // 标题栏右侧四个按钮 + 边距（DIP）

    /// <summary>用户与窗口交互（悬停/激活）→ App 注册到状态机</summary>
    public event Action? InteractionOccurred;

    /// <summary>用户点了关闭（×/Alt+F4）→ App 收起到托盘</summary>
    public event Action? RequestHideToTray;

    /// <summary>允许真正关闭（App 退出时置 true）</summary>
    public bool AllowClose { get; set; }

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        HwndSource.FromHwnd(hwnd)?.AddHook(WndProc);
    }

    /// <summary>
    /// WM_NCHITTEST：系统级拖拽与调大小（根治旧版「只有文字能拖」的问题）。
    /// 标题栏整行 → HTCAPTION（可拖拽）；按钮区 → HTCLIENT（保持可点击）；
    /// 右下角热区 → HTBOTTOMRIGHT（仅此一处可调大小）。
    ///
    /// DPI 校准（本轮修复）：WM_NCHITTEST 的 lParam 是物理像素坐标，
    /// GetWindowRect 也是物理像素，但 WPF 的 ActualWidth/常量是 DIP。
    /// 高 DPI（125%/150%）下直接比较会错位 → 全部换算为物理像素后比较。
    /// </summary>
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeWindow.WM_NCHITTEST)
        {
            var raw = lParam.ToInt64();
            var pt = new Point(raw & 0xFFFF, (raw >> 16) & 0xFFFF);

            NativeWindow.GetWindowRect(hwnd, out var rect);
            var windowRect = new Rect(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);

            // DIP → 物理像素换算
            var dpi = VisualTreeHelper.GetDpi(this);
            var scaleX = dpi.DpiScaleX;
            var scaleY = dpi.DpiScaleY;
            var titleBarPx = TitleBarHeight * scaleY;
            var gripPx = ResizeGripSize * scaleX;
            var buttonZonePx = new Rect(
                windowRect.Width - ButtonZoneWidth * scaleX, 0,
                ButtonZoneWidth * scaleX, titleBarPx);

            // 用户开始拖动右下角 → 停止自动高度，改为手动尺寸
            if (pt.X >= windowRect.Right - windowRect.Left - gripPx &&
                pt.Y >= windowRect.Bottom - windowRect.Top - gripPx)
            {
                SizeToContent = SizeToContent.Manual;
            }

            handled = true;
            return NativeWindow.HitTest(
                new Point(pt.X, pt.Y), windowRect, titleBarPx, gripPx,
                resizeEnabled: true, buttonZone: buttonZonePx);
        }

        return IntPtr.Zero;
    }

    private void Countdown_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ((MainViewModel)DataContext).DoubleClickAddCommand.Execute(null);
        }
    }

    /// <summary>空状态提示区域双击 → 新建任务（冻结需求）</summary>
    private void EmptyHint_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ((MainViewModel)DataContext).DoubleClickAddCommand.Execute(null);
        }
    }

    private void Window_MouseEnter(object sender, MouseEventArgs e) => InteractionOccurred?.Invoke();

    private void Window_Activated(object sender, EventArgs e) => InteractionOccurred?.Invoke();

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!AllowClose)
        {
            e.Cancel = true;
            Hide();
            RequestHideToTray?.Invoke();
        }
    }
}
