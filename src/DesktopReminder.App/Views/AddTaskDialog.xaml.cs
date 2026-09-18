using System.Windows;
using DesktopReminder.App.ViewModels;

namespace DesktopReminder.App.Views;

/// <summary>新建任务对话框：确认后回调 (软件名, 备注, 时长)</summary>
public partial class AddTaskDialog : ThemeWindow
{
    public AddTaskDialog(AddTaskViewModel viewModel, Action<string, string, TimeSpan> onConfirm)
    {
        InitializeComponent();
        DataContext = viewModel;

        viewModel.Confirm += (name, note, duration) =>
        {
            onConfirm(name, note, duration);
            Close();
        };
        viewModel.Cancel += Close;

        Loaded += (_, _) =>
        {
            AppNameBox.Focus();
            AppNameBox.SelectAll();
        };
    }
}
