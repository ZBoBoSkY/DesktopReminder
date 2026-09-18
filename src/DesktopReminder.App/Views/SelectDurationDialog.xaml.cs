using System.Windows;
using System.Windows.Controls.Primitives;
using DesktopReminder.App.Themes;

namespace DesktopReminder.App.Views;

/// <summary>通用「选一个时长」对话框（稍后提醒的固定选项）</summary>
public partial class SelectDurationDialog : ThemeWindow
{
    public SelectDurationDialog(string title, IReadOnlyList<int> minutesOptions, Action<int> onSelected)
    {
        InitializeComponent();
        TitleText.Text = title;

        foreach (var minutes in minutesOptions)
        {
            var button = new Button
            {
                Content = $"{minutes} 分钟",
                Height = 34,
                Margin = new Thickness(4),
                Background = ThemeManager.Current.Surface,
                Foreground = ThemeManager.Current.Foreground,
                BorderBrush = ThemeManager.Current.Border,
                BorderThickness = new Thickness(1),
                Cursor = System.Windows.Input.Cursors.Hand,
            };
            button.Click += (_, _) =>
            {
                onSelected(minutes);
                Close();
            };
            OptionGrid.Children.Add(button);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
