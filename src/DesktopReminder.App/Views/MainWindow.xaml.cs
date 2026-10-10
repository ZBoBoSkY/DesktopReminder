using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using DesktopReminder.App.ViewModels;
using DesktopReminder.App.WindowsApi;

namespace DesktopReminder.App.Views;

public partial class MainWindow : ThemeWindow
{
    public const double TitleBarHeight = 36;   // DIP
    public const double ResizeGripSize = 22;   // DIP
    private const double ButtonZoneWidth = 150; // 标题栏右侧四个按钮 + 边距（DIP）

    /// <summary>内联新建的时长胶囊列表（XAML x:Static 引用）</summary>
    public static IReadOnlyList<int> InlineMinutesSource => MainViewModel.InlinePresetMinutes;

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
        Loaded += MainWindow_Loaded;

        // 面板（已完成/已删除）展开或任务刷新后，重新自动测量高度，保证内容不被裁切。
        // 用户拖右下角手动调大小后，WndProc 会切回 Manual；下次面板切换再回到自动高度。
        viewModel.CompletedChanged += () =>
        {
            SizeToContent = SizeToContent.Height;
        };

        // 内联新建行展开后：光标停在备注输入框（软件名已自动填好）
        viewModel.InlineAddStarted += () => InlineNoteBox.Focus();
    }

    /// <summary>出现动画（WPF Window 禁止设置 RenderTransform，用淡入实现柔和的材质到达感；统一 spring 手感）</summary>
    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Opacity = 0;
        const double response = 0.3;
        var spring = new SpringEase { ResponseSeconds = response, DurationSeconds = response * 2 };
        BeginAnimation(OpacityProperty, new DoubleAnimation(1.0, TimeSpan.FromSeconds(response * 2))
        {
            EasingFunction = spring,
        });
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

    /// <summary>点任务行图标/名称区 → 一键跳回该任务对应的软件窗口（第 4 步）</summary>
    private void TaskJump_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 1)
        {
            return;
        }

        if ((sender as FrameworkElement)?.DataContext is TaskItemViewModel item &&
            item.JumpBackCommand.CanExecute(null))
        {
            item.JumpBackCommand.Execute(null);
        }
    }

    /// <summary>新任务行首次出现：从上方滑入 + 淡入（第 5 步动效，只播一次）</summary>
    private void TaskRow_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement row ||
            row.DataContext is not TaskItemViewModel item ||
            !item.IsNew)
        {
            return;
        }

        item.MarkNewPlayed();

        row.Opacity = 0;
        var transform = new TranslateTransform(0, -16);
        row.RenderTransform = transform;

        var spring = new SpringEase { ResponseSeconds = 0.35, DurationSeconds = 0.7 };
        transform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(380))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
        });
        row.BeginAnimation(OpacityProperty, new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(280))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
        });
    }

    /// <summary>完成按钮：行先淡出收起（220ms），动画结束后才真正完成任务（第 5 步动效）</summary>
    private void CompleteButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;

        if ((sender as FrameworkElement)?.DataContext is not TaskItemViewModel item)
        {
            return;
        }

        var row = FindAncestor<Border>(sender as DependencyObject);
        if (row is null)
        {
            item.CompleteCommand.Execute(null);
            return;
        }

        var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn },
        };
        fade.Completed += (_, _) => item.CompleteCommand.Execute(null);
        row.BeginAnimation(OpacityProperty, fade);
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
            if (current is T match)
            {
                return match;
            }
        }

        return null;
    }

    /// <summary>内联新建输入框键盘：回车保存、Esc 取消（全程键盘可完成）</summary>
    private void InlineInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        var vm = (MainViewModel)DataContext;
        if (e.Key == Key.Enter && vm.SaveInlineCommand.CanExecute(null))
        {
            vm.SaveInlineCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            vm.CancelInlineCommand.Execute(null);
            e.Handled = true;
        }
    }

    /// <summary>点击到期提示条 → 收起历史面板、滚动到第一个到期任务（任务已按到期升序，即列表顶部）</summary>
    private void DueBanner_Click(object sender, MouseButtonEventArgs e) => FocusFirstDueTask();

    /// <summary>到期直达：收起历史面板 + 任务列表滚到顶（图标/托盘展开主界面时也调用）</summary>
    public void FocusFirstDueTask()
    {
        var vm = (MainViewModel)DataContext;
        vm.CollapseHistoryPanels();
        TaskScroll.ScrollToTop();
    }

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
