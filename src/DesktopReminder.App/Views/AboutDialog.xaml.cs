using System.Windows;

namespace DesktopReminder.App.Views;

public partial class AboutDialog : ThemeWindow
{
    public AboutDialog()
    {
        InitializeComponent();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
