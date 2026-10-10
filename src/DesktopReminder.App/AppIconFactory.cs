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
    /// 绘制图标窗口内容（"灵动岛"胶囊：圆形主体 + 进度环 + 一体式倒计时标签 + 对角角标）。
    /// 第 5 步升级：进度环显示最早到期任务的剩余时间占比；标签与圆形连成一体（无缝隙）；
    /// 有过期任务时整体转红色系。
    /// </summary>
    /// <param name="countdownText">倒计时文本；null/空表示无任务（纯圆形）</param>
    /// <param name="badgeCount">角标数字（到期+过期数）</param>
    /// <param name="hasOverdue">是否有过期任务（整体变红 + 角标变红）</param>
    /// <param name="progress">最早到期任务剩余时间占比 0..1（进度环）</param>
    /// <param name="circleSize">圆形直径</param>
    /// <param name="canvasWidth">画布宽度（含标签），角标按画布右上角定位，避免被裁剪</param>
    /// <param name="canvasHeight">画布高度</param>
    public static void DrawIcon(
        DrawingContext dc,
        string? countdownText,
        int badgeCount,
        bool hasOverdue,
        double progress,
        double circleSize,
        double canvasWidth,
        double canvasHeight,
        ThemeColors colors)
    {
        var hasTag = !string.IsNullOrEmpty(countdownText);
        var radius = circleSize / 2;
        var bodyColor = hasOverdue ? colors.DueColor : colors.AccentColor;

        // 圆形主体：径向渐变（高光在左上，Apple 风格立体感）；有过期任务时转红色系
        var circleCenter = new Point(radius, canvasHeight / 2);
        var accentBrush = new RadialGradientBrush(
            Lighten(bodyColor, 0.35), bodyColor);
        accentBrush.GradientOrigin = new Point(0.32, 0.22);
        accentBrush.RadiusX = 1.0;
        accentBrush.RadiusY = 1.0;
        accentBrush.Freeze();
        dc.DrawEllipse(accentBrush, null, circleCenter, radius - 2, radius - 2);

        // 进度环：贴圆边缘内圈，12 点方向顺时针，白色半透明弧（过期时不显示，整体已红）
        if (hasTag && !hasOverdue && progress > 0 && progress < 1)
        {
            var ringPen = new Pen(new SolidColorBrush(Color.FromArgb(0xE6, 0xFF, 0xFF, 0xFF)), 3.5);
            ringPen.Freeze();
            DrawProgressArc(dc, ringPen, circleCenter, radius - 4.25, progress);
        }

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

        // 一体式倒计时标签：圆角矩形左缘与圆形右缘重叠（无缝隙，视觉连成一体）
        if (hasTag && countdownText is not null)
        {
            var typeface = new Typeface("Microsoft YaHei");
            var text = new FormattedText(
                countdownText, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                typeface, circleSize * 0.26, new SolidColorBrush(colors.ForegroundColor), 1.25);
            var tagLeft = circleSize - 4; // 与圆形重叠，连体
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

    /// <summary>画进度弧：12 点方向顺时针，扫过 progress*360°</summary>
    private static void DrawProgressArc(DrawingContext dc, Pen pen, Point center, double radius, double progress)
    {
        var sweepAngle = 360.0 * Math.Clamp(progress, 0, 1);
        if (sweepAngle <= 0)
        {
            return;
        }

        var start = PointOnCircle(center, radius, -90);
        var end = PointOnCircle(center, radius, -90 + sweepAngle);
        var largeArc = sweepAngle > 180;

        var figure = new PathFigure { StartPoint = start, IsClosed = false, IsFilled = false };
        figure.Segments.Add(new ArcSegment(
            end, new Size(radius, radius), 0, largeArc, SweepDirection.Clockwise, true));
        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        geometry.Freeze();
        dc.DrawGeometry(null, pen, geometry);
    }

    private static Point PointOnCircle(Point center, double radius, double angleDegrees)
    {
        var rad = angleDegrees * Math.PI / 180.0;
        return new Point(center.X + radius * Math.Cos(rad), center.Y + radius * Math.Sin(rad));
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

    /// <summary>BitmapSource → System.Drawing.Icon（供托盘 NotifyIcon 使用）。
    /// 采用 PNG 嵌入 ICO 的方式（Vista+ 原生支持，完整保留 alpha 透明通道，
    /// 避免 GetHicon 丢透明导致托盘显示空白方块）。</summary>
    public static System.Drawing.Icon? ToIcon(BitmapSource source)
    {
        try
        {
            using var ms = new MemoryStream();
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            encoder.Save(ms);
            var png = ms.ToArray();

            // 包装成 ICO 文件格式（6 字节头 + 16 字节目录项 + PNG 数据）
            using var icoMs = new MemoryStream();
            using var w = new BinaryWriter(icoMs);
            w.Write((ushort)0);            // reserved
            w.Write((ushort)1);            // type: icon
            w.Write((ushort)1);            // count: 1
            w.Write((byte)64);             // width
            w.Write((byte)64);             // height
            w.Write((byte)0);               // palette
            w.Write((byte)0);               // reserved
            w.Write((ushort)1);             // planes
            w.Write((ushort)32);            // bit count
            w.Write((uint)png.Length);     // image size
            w.Write((uint)22);             // image offset (6+16)
            w.Write(png);
            icoMs.Position = 0;
            return new System.Drawing.Icon(icoMs);
        }
        catch
        {
            return null;
        }
    }
}
