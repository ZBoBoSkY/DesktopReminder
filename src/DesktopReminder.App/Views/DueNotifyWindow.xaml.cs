using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DesktopReminder.App.Themes;
using DesktopReminder.Domain.Models;

namespace DesktopReminder.App.Views;

/// <summary>
/// 自制到期通知弹窗（第 4 步，替代系统 Toast）：
/// 屏幕右下角小窗，列出全部到期任务，每行可直接「完成 / 稍后 5 分钟 / 稍后 10 分钟」。
/// 不抢焦点（不 Activate），用户可继续手头工作；任务全部处理后自动隐藏。
/// </summary>
public partial class DueNotifyWindow : ThemeWindow
{
    private readonly Action<ReminderTask> _onComplete;
    private readonly Action<ReminderTask, int> _onSnooze;

    public DueNotifyWindow(Action<ReminderTask> onComplete, Action<ReminderTask, int> onSnooze)
    {
        InitializeComponent();
        _onComplete = onComplete;
        _onSnooze = onSnooze;

        // 主题资源在构造函数里立即注册：Refresh 会在 Show() 之前构建行（FindResource 主题色），
        // 不能等到 Loaded 才注册，否则首次刷新就抛「资源找不到」异常、弹窗永不显示。
        ApplyTheme();
    }

    /// <summary>刷新内容并显示；到期任务为空时自动隐藏</summary>
    public void Refresh(IReadOnlyList<ReminderTask> dueTasks)
    {
        if (dueTasks.Count == 0)
        {
            Hide();
            return;
        }

        TitleText.Text = $"{dueTasks.Count} 项任务到期";
        ListPanel.Children.Clear();
        foreach (var task in dueTasks)
        {
            ListPanel.Children.Add(BuildRow(task));
        }

        if (!IsVisible)
        {
            Show();
        }

        // 内容高度变化后重新定位到屏幕右下角（工作区，避开任务栏）
        Dispatcher.BeginInvoke(PlaceAtBottomRight);
    }

    private Border BuildRow(ReminderTask task)
    {
        var row = new Border
        {
            CornerRadius = new CornerRadius(8),
            Background = (Brush)FindResource("ThemeHover"),
            Padding = new Thickness(8, 6, 4, 6),
            Margin = new Thickness(0, 0, 0, 6),
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // 左：软件名（加粗）+ 备注（次要色）
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var name = new TextBlock
        {
            Text = task.AppName,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Foreground = (Brush)FindResource("ThemeForeground"),
        };
        text.Children.Add(name);

        if (!string.IsNullOrEmpty(task.Note))
        {
            text.Children.Add(new TextBlock
            {
                Text = task.Note,
                FontSize = 11,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = (Brush)FindResource("ThemeSecondaryText"),
                Margin = new Thickness(0, 2, 0, 0),
            });
        }

        grid.Children.Add(text);
        Grid.SetColumn(text, 0);

        // 右：完成 / 稍后5 / 稍后10
        var actions = new StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
        };
        actions.Children.Add(MakeActionButton("完成", (Brush)FindResource("ThemeActive"), () => _onComplete(task)));
        actions.Children.Add(MakeActionButton("稍后5", (Brush)FindResource("ThemeSecondaryText"), () => _onSnooze(task, 5)));
        actions.Children.Add(MakeActionButton("稍后10", (Brush)FindResource("ThemeSecondaryText"), () => _onSnooze(task, 10)));
        grid.Children.Add(actions);
        Grid.SetColumn(actions, 1);

        row.Child = grid;
        return row;
    }

    private Button MakeActionButton(string text, Brush foreground, Action action)
    {
        var button = new Button
        {
            Content = text,
            FontSize = 12,
            Padding = new Thickness(8, 3, 8, 3),
            Margin = new Thickness(4, 0, 0, 0),
            Background = (Brush)FindResource("ThemeHover"),
            BorderThickness = new Thickness(0),
            Foreground = foreground,
            Cursor = System.Windows.Input.Cursors.Hand,
        };
        button.Click += (_, _) => action();
        return button;
    }

    private void PlaceAtBottomRight()
    {
        var workArea = SystemParameters.WorkArea;
        UpdateLayout();
        Left = workArea.Right - ActualWidth - 16;
        Top = workArea.Bottom - ActualHeight - 16;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Hide();
}
