using System;
using System.Windows.Media.Animation;

namespace DesktopReminder.App.Views;

/// <summary>
/// 临界阻尼弹簧缓动（Apple 风格：damping 1.0、response 0.35s）。
/// 曲线 = 快速起步、平滑落定、无过冲。透明度/位移过渡用它，避免"突然变半透明"的生硬感。
/// 输入归一化时间 [0,1]，输出 [0,1]。
/// </summary>
public sealed class SpringEase : IEasingFunction
{
    /// <summary>响应时间（秒）：达到目标约 63% 所需的时间。0.3~0.4 是 Apple 推荐区间</summary>
    public double ResponseSeconds { get; set; } = 0.35;

    /// <summary>动画总时长（秒）：默认 = 2 × ResponseSeconds，视觉上 99% 完成</summary>
    public double DurationSeconds { get; set; } = 0.7;

    public double Ease(double normalizedTime)
    {
        if (normalizedTime <= 0) return 0;
        if (normalizedTime >= 1) return 1;

        // 临界阻尼阶跃响应：x(t) = 1 - (1 + ωt)·e^(-ωt)，ω = 2π / ResponseSeconds
        var t = normalizedTime * DurationSeconds;
        var w = 2.0 * Math.PI / ResponseSeconds;
        var u = w * t;
        return Math.Clamp(1.0 - (1.0 + u) * Math.Exp(-u), 0.0, 1.0);
    }
}
