namespace DesktopReminder.Core.StateMachine;

/// <summary>窗口三态</summary>
public enum WindowState
{
    /// <summary>清晰（可交互）</summary>
    Active = 0,

    /// <summary>半透明（弱显示，不影响浏览，悬停即激活）</summary>
    Glass = 1,

    /// <summary>图标（最小占位，点击激活，可拖拽移动）</summary>
    Icon = 2
}
