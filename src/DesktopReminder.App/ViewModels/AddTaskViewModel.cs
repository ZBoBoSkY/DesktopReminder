using System.Windows.Input;

namespace DesktopReminder.App.ViewModels;

/// <summary>
/// 新建任务对话框 ViewModel（冻结需求 R1.4 ~ R1.6）。
/// 软件名自动识别填充、备注补充说明、固定时长选项 + 自定义分钟。
/// </summary>
public sealed class AddTaskViewModel : ObservableObject
{
    private string _appName = "";
    private string _note = "";
    private int _selectedOptionIndex;
    private string _customMinutes = "";

    public AddTaskViewModel(string detectedAppName)
    {
        _appName = detectedAppName;
        OkCommand = new RelayCommand(
            _ => Confirm?.Invoke(AppName.Trim(), Note.Trim(), GetDuration()),
            _ => CanConfirm());
        CancelCommand = new RelayCommand(_ => Cancel?.Invoke());
    }

    /// <summary>固定时长选项（分钟）</summary>
    public static IReadOnlyList<int> PresetMinutes { get; } = new[] { 5, 10, 15, 30, 60 };

    /// <summary>当前选中项；最后一个为「自定义」</summary>
    public int SelectedOptionIndex
    {
        get => _selectedOptionIndex;
        set
        {
            if (SetProperty(ref _selectedOptionIndex, value))
            {
                IsCustomVisible = value == PresetMinutes.Count;
                OnPropertyChanged(nameof(IsCustomVisible));
                ((RelayCommand)OkCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsCustomVisible { get; private set; }

    public string AppName
    {
        get => _appName;
        set { if (SetProperty(ref _appName, value)) { OnPropertyChanged(nameof(HasAppName)); } }
    }

    public bool HasAppName => !string.IsNullOrWhiteSpace(_appName);

    public string Note
    {
        get => _note;
        set => SetProperty(ref _note, value);
    }

    public string CustomMinutes
    {
        get => _customMinutes;
        set { if (SetProperty(ref _customMinutes, value)) { ((RelayCommand)OkCommand).RaiseCanExecuteChanged(); } }
    }

    public RelayCommand OkCommand { get; }
    public RelayCommand CancelCommand { get; }

    /// <summary>确认事件：携带 (名称, 备注, 时长)</summary>
    public event Action<string, string, TimeSpan>? Confirm;
    public event Action? Cancel;

    /// <summary>解析最终时长：固定选项选中取固定值，自定义取输入分钟数</summary>
    public TimeSpan GetDuration()
    {
        if (SelectedOptionIndex < PresetMinutes.Count)
        {
            return TimeSpan.FromMinutes(PresetMinutes[SelectedOptionIndex]);
        }

        return TimeSpan.FromMinutes(ParseMinutes());
    }

    private int ParseMinutes() => int.TryParse(_customMinutes, out var m) && m > 0 ? m : 5;

    private bool CanConfirm()
    {
        if (string.IsNullOrWhiteSpace(_appName))
        {
            return false;
        }

        // 自定义模式下必须输入合法分钟数
        if (SelectedOptionIndex >= PresetMinutes.Count)
        {
            return int.TryParse(_customMinutes, out var m) && m > 0;
        }

        return true;
    }
}
