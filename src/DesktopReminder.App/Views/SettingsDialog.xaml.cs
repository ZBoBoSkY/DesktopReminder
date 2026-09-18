using System.Windows;
using DesktopReminder.App.ViewModels;

namespace DesktopReminder.App.Views;

/// <summary>设置对话框：保存时回调（App 层持久化 + 应用主题/自启）</summary>
public partial class SettingsDialog : ThemeWindow
{
    public SettingsDialog(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.SaveAndClose += Close;
        viewModel.Cancel += Close;
    }
}
