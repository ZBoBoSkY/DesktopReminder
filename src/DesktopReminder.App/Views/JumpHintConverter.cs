using System.Globalization;
using System.Windows.Data;

namespace DesktopReminder.App.Views;

/// <summary>任务行「跳回软件」悬停提示：有进程名可跳回 → 提示点击，无 → 说明原因</summary>
public sealed class JumpHintConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? "点击回到该软件窗口" : "旧任务无进程记录，无法跳回";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
