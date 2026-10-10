using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using DesktopReminder.App.Themes;

namespace DesktopReminder.App.Views;

/// <summary>
/// 悬浮图标窗口（冻结需求 R4）：圆形 + 右侧倒计时标签 + 对角角标。
/// 无任务纯圆形；有任务右侧伸出标签显示最早到期任务倒计时。
/// 单击展开主界面；按住可拖拽移动（与旧版不同，整窗可拖）。
/// </summary>
public partial class IconWindow : ThemeWindow
{
    private const double CircleSize = 54;
    private const double WindowMargin = 4;

    /// <summary>点击展开主界面</summary>
    public event Action? RequestOpenMain;

    /// <summary>右键菜单动作出口</summary>
    public event Action? RequestAddTask;
    public event Action? RequestOpenSettings;
    public event Action? RequestOpenAbout;
    public event Action? RequestExit;

    private bool _dragging;
    private Point _dragScreenStart;
    private Point _winStart;
    private bool _moved;
    private bool _lastOverdue;

    public IconWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 出现动画（WPF Window 禁止设置 RenderTransform，用快速淡入实现柔和的材质到达感；统一 spring 手感）。
    /// </summary>
    private void IconWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Opacity = 0;
        const double response = 0.3;
        var spring = new SpringEase { ResponseSeconds = response, DurationSeconds = response * 2 };
        BeginAnimation(OpacityProperty, new DoubleAnimation(1.0, TimeSpan.FromSeconds(response * 2))
        {
            EasingFunction = spring,
        });
    }

    /// <summary>
    /// 刷新图标内容（App 每秒调用）。
    /// 有任务 → 圆形 + 一体式标签（按文本宽度动态调整窗口宽）；无任务 → 纯圆形。
    /// 拖拽期间完全跳过刷新（窗口移动中改尺寸/内容会与鼠标坐标系互相干扰 → 晃动）。
    /// </summary>
    /// <param name="countdownText">最早到期任务倒计时文本；null/空表示无任务</param>
    /// <param name="badgeCount">角标数字（到期+过期数）</param>
    /// <param name="hasOverdue">是否有过期任务（整体变红 + 轻微放大脉冲一次）</param>
    /// <param name="progress">最早到期任务剩余时间占比 0..1（进度环）</param>
    public void Refresh(string? countdownText, int badgeCount, bool hasOverdue, double progress)
    {
        if (_dragging)
        {
            return; // 拖拽中保持完全静止；松手后下一个心跳自然恢复
        }

        // 从"无过期"变"有过期"的瞬间：轻微放大脉冲（弹簧），提醒注意
        if (hasOverdue && !_lastOverdue)
        {
            PlayOverduePulse();
        }

        _lastOverdue = hasOverdue;

        var visual = new System.Windows.Media.DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            AppIconFactory.DrawIcon(dc, countdownText, badgeCount, hasOverdue, progress,
                CircleSize, Width, Height, ThemeManager.Current);
        }

        // 计算标签宽度（若有），决定窗口尺寸
        double width = CircleSize + WindowMargin * 2;
        if (!string.IsNullOrEmpty(countdownText))
        {
            var tf = new FormattedText(
                countdownText, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface("Microsoft YaHei"), CircleSize * 0.26, System.Windows.Media.Brushes.Black, 1.25);
            width += tf.Width + 6 + 16 + 4;
        }

        Width = Math.Max(width, CircleSize + WindowMargin * 2);
        Height = CircleSize + WindowMargin * 2;

        IconImage.Source = AppIconFactory.Render(visual, (int)Width, (int)Height);
        IconImage.Width = Width;
        IconImage.Height = Height;
    }

    /// <summary>到期瞬间的轻微放大脉冲（弹簧，约 0.5s）：1.0 → 1.08 → 回落 1.0</summary>
    private void PlayOverduePulse()
    {
        var transform = new System.Windows.Media.ScaleTransform(1.0, 1.0);
        IconImage.RenderTransform = transform;
        IconImage.RenderTransformOrigin = new Point(0.5, 0.5);

        var response = 0.4;
        var spring = new SpringEase { ResponseSeconds = response, DurationSeconds = response * 2 };
        var up = new DoubleAnimation(1.08, TimeSpan.FromSeconds(0.12)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
        var down = new DoubleAnimation(1.0, TimeSpan.FromSeconds(0.4)) { EasingFunction = spring, BeginTime = TimeSpan.FromSeconds(0.12) };
        transform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, up);
        transform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, up);
        transform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, down);
        transform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, down);
    }

    private void IconWindow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragging = true;
        _moved = false;
        _winStart = new Point(Left, Top);
        GetCursorPos(out var pt);
        _dragScreenStart = new Point(pt.X, pt.Y);
        CaptureMouse();
        e.Handled = true;
    }

    private void IconWindow_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        // 用屏幕物理坐标计算位移（GetCursorPos），再按 DPI 换算成 WPF 坐标。
        // 旧版用相对窗口坐标：窗口一动，相对坐标跟着漂移，形成正反馈 → 图标来回晃动。
        GetCursorPos(out var pt);
        var scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        Left = _winStart.X + (pt.X - _dragScreenStart.X) / scale;
        Top = _winStart.Y + (pt.Y - _dragScreenStart.Y) / scale;

        if (Math.Abs(pt.X - _dragScreenStart.X) > 3 || Math.Abs(pt.Y - _dragScreenStart.Y) > 3)
        {
            _moved = true;
        }
    }

    private void IconWindow_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        ReleaseMouseCapture();

        // 几乎没移动 → 视为单击 → 展开主界面
        if (!_moved)
        {
            RequestOpenMain?.Invoke();
        }
    }

    private void IconWindow_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        ContextMenu = (ContextMenu)FindResource("IconMenu");
        ContextMenu.IsOpen = true;
    }

    private void Menu_ShowMain(object sender, RoutedEventArgs e) => RequestOpenMain?.Invoke();
    private void Menu_AddTask(object sender, RoutedEventArgs e) => RequestAddTask?.Invoke();
    private void Menu_Settings(object sender, RoutedEventArgs e) => RequestOpenSettings?.Invoke();
    private void Menu_About(object sender, RoutedEventArgs e) => RequestOpenAbout?.Invoke();
    private void Menu_Exit(object sender, RoutedEventArgs e) => RequestExit?.Invoke();

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }
}
