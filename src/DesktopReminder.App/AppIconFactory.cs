using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopReminder.App.Themes;

namespace DesktopReminder.App;

/// <summary>
/// 矢量图标工厂（冻结需求 R4.4：全部矢量绘制，不用 emoji，缩放不失真）。
/// 职责：圆形图标、数字角标、倒计时标签——画成 BitmapSource，供窗口/托盘使用。
/// </summary>
public static class AppIconFactory
{
    /// <summary>
    /// 绘制图标窗口内容（圆形 + 可选右侧倒计时标签 + 对角角标）。
    /// </summary>
    /// <param name="countdownText">倒计时文本；null/空表示无任务（纯圆形）</param>
    /// <param name="badgeCount">角标数字（到期+过期数）</param>
    /// <param name="hasOverdue">是否有过期任务（角标变红）</param>
    /// <param name="circleSize">圆形直径</param>
    /// <param name="canvasWidth">画布宽度（含标签），角标按画布右上角定位，避免被裁剪</param>
    /// <param name="canvasHeight">画布高度</param>
    public static void DrawIcon(
        DrawingContext dc,
        string? countdownText,
        int badgeCount,
        bool hasOverdue,
        double circleSize,
        double canvasWidth,
        double canvasHeight,
        ThemeColors colors)
    {
        var hasTag = !string.IsNullOrEmpty(countdownText);
        var radius = circleSize / 2;

        // 圆形主体：径向渐变（高光在左上，Apple 风格立体感）
        var circleCenter = new Point(radius, canvasHeight / 2);
        var accentBrush = new RadialGradientBrush(
            Lighten(colors.AccentColor, 0.35), colors.AccentColor);
        accentBrush.GradientOrigin = new Point(0.32, 0.22);
        accentBrush.RadiusX = 1.0;
        accentBrush.RadiusY = 1.0;
        accentBrush.Freeze();
        dc.DrawEllipse(accentBrush, null, circleCenter, radius - 2, radius - 2);

        // 圆形中央：时钟符号（圆形描边 + 指针）
        var stroke = new Pen(new SolidColorBrush(colors.ForegroundColor), 2.5);
        stroke.Freeze();
        var clockCenter = circleCenter;
        var clockRadius = radius * 0.42;
        dc.DrawEllipse(null, stroke, clockCenter, clockRadius, clockRadius);
        var handPen = new Pen(new SolidColorBrush(colors.ForegroundColor), 2);
        handPen.Freeze();
        dc.DrawLine(handPen, clockCenter, new Point(clockCenter.X, clockCenter.Y - clockRadius * 0.75));
        dc.DrawLine(handPen, clockCenter, new Point(clockCenter.X + clockRadius * 0.6, clockCenter.Y + clockRadius * 0.15));

        // 右侧标签（圆角矩形，显示倒计时）
        if (hasTag && countdownText is not null)
        {
            var typeface = new Typeface("Microsoft YaHei");
            var text = new FormattedText(
                countdownText, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                typeface, circleSize * 0.26, new SolidColorBrush(colors.ForegroundColor), 1.25);
            var tagLeft = circleSize + 6;
            var tagTop = circleCenter.Y - text.Height / 2;
            var tagRect = new Rect(tagLeft, tagTop, text.Width + 16, text.Height + 8);
            var tagBrush = new SolidColorBrush(colors.SurfaceColor);
            tagBrush.Freeze();
            var tagPen = new Pen(new SolidColorBrush(colors.BorderColor), 1);
            tagPen.Freeze();
            dc.DrawRoundedRectangle(tagBrush, tagPen, tagRect, 8, 8);
            dc.DrawText(text, new Point(tagLeft + 8, tagTop + 4));
        }

        // 对角角标：右上角显示到期任务数（绿=倒计时，红=过期）。
        // 位置按画布右上角计算（旧版按 circleSize 推导致中心超出画布顶边，上半圆被裁剪）
        if (badgeCount > 0)
        {
            var badgeColor = hasOverdue ? colors.DueColor : colors.ActiveColor;
            var badgeText = ReminderSchedulerBadgeText(badgeCount);
            var badgeSize = circleSize * 0.42;
            var badgeCenter = new Point(canvasWidth - badgeSize * 0.5 - 1, badgeSize * 0.5 + 1);
            var badgeBrush = new SolidColorBrush(badgeColor);
            badgeBrush.Freeze();
            dc.DrawEllipse(badgeBrush, null, badgeCenter, badgeSize / 2, badgeSize / 2);

            var bf = new FormattedText(
                badgeText, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface("Microsoft YaHei"), badgeSize * 0.62, new SolidColorBrush(Colors.White), 1.25);
            dc.DrawText(bf, new Point(badgeCenter.X - bf.Width / 2, badgeCenter.Y - bf.Height / 2));
        }
    }

    private static string ReminderSchedulerBadgeText(int count) => count > 9 ? "9+" : count.ToString();

    /// <summary>颜色提亮（用于渐变高光）</summary>
    private static Color Lighten(Color c, double t) => Color.FromArgb(
        c.A,
        (byte)(c.R + (255 - c.R) * t),
        (byte)(c.G + (255 - c.G) * t),
        (byte)(c.B + (255 - c.B) * t));

    /// <summary>把 DrawingVisual 渲染成指定尺寸的 BitmapSource</summary>
    public static BitmapSource Render(DrawingVisual visual, int width, int height)
    {
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>BitmapSource → System.Drawing.Icon（供托盘 NotifyIcon 使用）</summary>
    public static System.Drawing.Icon? ToIcon(BitmapSource source)
    {
        try
        {
            using var ms = new MemoryStream();
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            encoder.Save(ms);
            ms.Position = 0;
            using var bmp = new System.Drawing.Bitmap(ms);
            var hicon = bmp.GetHicon();
            var icon = System.Drawing.Icon.FromHandle(hicon);
            return icon;
        }
        catch
        {
            return null;
        }
    }
}
