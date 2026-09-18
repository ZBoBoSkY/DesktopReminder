using DesktopReminder.Core.StateMachine;
using DesktopReminder.Domain.Models;

namespace DesktopReminder.Tests;

/// <summary>窗口三态状态机测试（对应冻结需求 R2.2 ~ R2.9）</summary>
public class WindowStateMachineTests
{
    private static readonly DateTime Now = new(2026, 9, 19, 9, 0, 0);

    private static AppSettings Settings(
        int glassMinutes = 5,
        int iconSeconds = 30) =>
        new()
        {
            GlassTimeout = TimeSpan.FromMinutes(glassMinutes),
            IconTimeout = TimeSpan.FromSeconds(iconSeconds),
        };

    [Fact]
    public void InitialState_IsActive()
    {
        var sm = new WindowStateMachine(Settings());
        Assert.Equal(WindowState.Active, sm.CurrentState);
    }

    [Fact]
    public void Evaluate_StaysActive_BeforeGlassTimeout()
    {
        var sm = new WindowStateMachine(Settings());
        sm.RegisterInteraction(Now);

        Assert.Equal(WindowState.Active, sm.Evaluate(Now.AddMinutes(4)));
        Assert.Equal(WindowState.Active, sm.Evaluate(Now.AddMinutes(4).AddSeconds(59)));
    }

    [Fact]
    public void Evaluate_TransitionsToGlass_AfterGlassTimeout()
    {
        var sm = new WindowStateMachine(Settings());
        sm.RegisterInteraction(Now);

        Assert.Equal(WindowState.Glass, sm.Evaluate(Now.AddMinutes(5)));
    }

    [Fact]
    public void Evaluate_StaysGlass_WithinIconTimeoutWindow()
    {
        var sm = new WindowStateMachine(Settings());
        sm.RegisterInteraction(Now);

        Assert.Equal(WindowState.Glass, sm.Evaluate(Now.AddMinutes(5)));
        Assert.Equal(WindowState.Glass, sm.Evaluate(Now.AddMinutes(5).AddSeconds(29)));
    }

    [Fact]
    public void Evaluate_TransitionsToIcon_AfterFullTimeout()
    {
        var sm = new WindowStateMachine(Settings());
        sm.RegisterInteraction(Now);

        Assert.Equal(WindowState.Icon, sm.Evaluate(Now.AddMinutes(5).AddSeconds(30)));
    }

    [Fact]
    public void RegisterInteraction_ReturnsToActive_AndResetsTimer()
    {
        var sm = new WindowStateMachine(Settings());
        sm.RegisterInteraction(Now);

        // 先到图标状态
        Assert.Equal(WindowState.Icon, sm.Evaluate(Now.AddMinutes(10)));

        // 用户交互 → 立即清晰，且计时重置
        sm.RegisterInteraction(Now.AddMinutes(10));
        Assert.Equal(WindowState.Active, sm.CurrentState);
        Assert.Equal(WindowState.Active, sm.Evaluate(Now.AddMinutes(10).AddMinutes(4)));
        Assert.Equal(WindowState.Glass, sm.Evaluate(Now.AddMinutes(10).AddMinutes(5)));
    }

    [Fact]
    public void InterruptForReminder_ForcesGlass_ForIconTimeoutDuration()
    {
        var sm = new WindowStateMachine(Settings());
        sm.RegisterInteraction(Now);
        Assert.Equal(WindowState.Icon, sm.Evaluate(Now.AddMinutes(10)));

        sm.InterruptForReminder(Now.AddMinutes(10));
        Assert.Equal(WindowState.Glass, sm.CurrentState);

        // 打断窗口期内保持半透明
        Assert.Equal(WindowState.Glass, sm.Evaluate(Now.AddMinutes(10).AddSeconds(10)));

        // 窗口期结束且无交互 → 重新收成图标
        Assert.Equal(WindowState.Icon, sm.Evaluate(Now.AddMinutes(10).AddSeconds(30)));
    }

    [Fact]
    public void Evaluate_ReturnsToIcon_AfterReminderBurst_WhenUserKeepsWorking()
    {
        var sm = new WindowStateMachine(Settings());
        sm.RegisterInteraction(Now);
        sm.Evaluate(Now.AddMinutes(10)); // → Icon
        sm.InterruptForReminder(Now.AddMinutes(10)); // → Glass（提醒）
        sm.Evaluate(Now.AddMinutes(10).AddSeconds(30)); // 无交互 → Icon

        Assert.Equal(WindowState.Icon, sm.CurrentState);
    }

    [Fact]
    public void StateChanged_FiresOnlyOnActualTransition()
    {
        var sm = new WindowStateMachine(Settings());
        var events = new List<WindowState>();
        sm.StateChanged += s => events.Add(s);

        sm.RegisterInteraction(Now);
        sm.Evaluate(Now.AddSeconds(1)); // 仍 Active，不应触发事件
        sm.Evaluate(Now.AddMinutes(5)); // Active → Glass

        Assert.Equal(new[] { WindowState.Active, WindowState.Glass }, events);
    }
}
