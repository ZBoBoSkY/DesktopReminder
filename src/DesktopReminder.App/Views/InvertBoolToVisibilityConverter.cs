using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DesktopReminder.App.Views;

/// <summary>反转可见性：true → Collapsed，false → Visible（空态提示用）</summary>
public sealed class InvertBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
