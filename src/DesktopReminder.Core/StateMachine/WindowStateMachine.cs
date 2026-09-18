namespace DesktopReminder.Core.StateMachine;

using DesktopReminder.Domain.Models;

/// <summary>
/// 窗口三态状态机（纯计算，不持有定时器，可独立单元测试）。
///
/// 状态规则（对应冻结需求 R2.2 ~ R2.9）：
///   - 无操作满 GlassTimeout（默认 5 分钟）→ 清晰 → 半透明
///   - 半透明再满 IconTimeout（默认 30 秒）→ 半透明 → 图标
///   - 用户交互（悬停/点击）→ 立即回清晰
///   - 提醒打断：图标 → 半透明，保持 IconTimeout 后若仍无交互再收成图标
///
/// UI 层持有定时器，周期性调用 <see cref="Evaluate"/> 获取目标状态并应用；
/// 应用层只负责「计算」，不负责「画」，保证可测试、可复用。
/// </summary>
public sealed class WindowStateMachine
{
    private readonly AppSettings _settings;
    private DateTime _lastInteraction;
    private DateTime _reminderBurstEnd = DateTime.MinValue;

    public WindowStateMachine(AppSettings settings)
    {
        _settings = settings;
        _lastInteraction = DateTime.Now;
    }

    /// <summary>当前状态（初始为清晰）</summary>
    public WindowState CurrentState { get; private set; } = WindowState.Active;

    /// <summary>状态变化事件（UI 层订阅后执行窗口动画/透明度切换）</summary>
    public event Action<WindowState>? StateChanged;

    /// <summary>用户交互（鼠标悬停/点击界面）：立即回清晰并重置计时</summary>
    public void RegisterInteraction(DateTime now)
    {
        _lastInteraction = now;
        TransitionTo(WindowState.Active, now);
    }

    /// <summary>提醒打断：从图标状态弹回半透明一段时间，给用户看见</summary>
    public void InterruptForReminder(DateTime now)
    {
        _reminderBurstEnd = now + _settings.IconTimeout;
        TransitionTo(WindowState.Glass, now);
    }

    /// <summary>用户主动收起为图标（点「－」或关闭按钮），立即生效不走超时</summary>
    public void CollapseToIcon(DateTime now)
    {
        _reminderBurstEnd = DateTime.MinValue;
        _lastInteraction = now - _settings.GlassTimeout - _settings.IconTimeout - TimeSpan.FromSeconds(1);
        TransitionTo(WindowState.Icon, now);
    }

    /// <summary>
    /// 计算并应用当前应处状态。返回目标状态（可能与当前相同）。
    /// 每次调用会推进计时判断；无操作超时到点即切换。
    /// </summary>
    public WindowState Evaluate(DateTime now)
    {
        WindowState target;

        // 提醒打断窗口期内强制半透明（不立即收起）
        if (now < _reminderBurstEnd)
        {
            target = WindowState.Glass;
        }
        else
        {
            var elapsed = now - _lastInteraction;
            if (elapsed < _settings.GlassTimeout)
            {
                target = WindowState.Active;
            }
            else if (elapsed < _settings.GlassTimeout + _settings.IconTimeout)
            {
                target = WindowState.Glass;
            }
            else
            {
                target = WindowState.Icon;
            }
        }

        if (target != CurrentState)
        {
            TransitionTo(target, now);
        }

        return target;
    }

    private void TransitionTo(WindowState state, DateTime now)
    {
        CurrentState = state;
        StateChanged?.Invoke(state);
    }
}
