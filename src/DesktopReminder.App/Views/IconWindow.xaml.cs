using System.Windows;
using System.Windows.Input;
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
    private const double CircleSize = 46;
    private const double WindowMargin = 4;

    /// <summary>点击展开主界面</summary>
    public event Action? RequestOpenMain;

    /// <summary>右键菜单动作出口</summary>
    public event Action? RequestAddTask;
    public event Action? RequestOpenSettings;
    public event Action? RequestOpenAbout;
    public event Action? RequestExit;

    private bool _dragging;
    private Point _dragStart;
    private Point _winStart;
    private bool _moved;

    public IconWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 刷新图标内容（App 每秒调用）。
    /// 有任务 → 圆形 + 右侧标签（按文本宽度动态调整窗口宽）；无任务 → 纯圆形。
    /// </summary>
    public void Refresh(string? countdownText, int badgeCount, bool hasOverdue)
    {
        var visual = new System.Windows.Media.DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            AppIconFactory.DrawIcon(dc, countdownText, badgeCount, hasOverdue, CircleSize, ThemeManager.Current);
        }

        // 计算标签宽度（若有），决定窗口尺寸。
        // 拖拽期间冻结尺寸：窗口在移动中改 Width/Height 会让鼠标坐标系漂移 → 图标晃动（本轮修复）
        if (!_dragging)
        {
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
        }

        IconImage.Source = AppIconFactory.Render(visual, (int)Width, (int)Height);
        IconImage.Width = Width;
        IconImage.Height = Height;
    }

    private void IconWindow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragging = true;
        _moved = false;
        _dragStart = e.GetPosition(this);
        _winStart = new Point(Left, Top);
        CaptureMouse();
        e.Handled = true;
    }

    private void IconWindow_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        var pos = e.GetPosition(this);
        Left = _winStart.X + pos.X - _dragStart.X;
        Top = _winStart.Y + pos.Y - _dragStart.Y;
        if (Math.Abs(pos.X - _dragStart.X) > 3 || Math.Abs(pos.Y - _dragStart.Y) > 3)
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
}
