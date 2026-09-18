using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using DesktopReminder.App.Themes;

namespace DesktopReminder.App.Views;

/// <summary>任务行倒计时颜色：到期/过期显示主题红，否则主题前景色</summary>
public sealed class DueForegroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var isDue = value is true;
        return isDue ? ThemeManager.Current.Due : ThemeManager.Current.Foreground;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
