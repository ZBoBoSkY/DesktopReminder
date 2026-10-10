using System.Windows.Media;
using DesktopReminder.App.Themes;
using DesktopReminder.Domain.Models;
using WF = System.Windows.Forms;

namespace DesktopReminder.App;

/// <summary>
/// 系统托盘（冻结需求 R7）：右键菜单入口、双击显示主界面、
/// 图标带到期任务角标（数字变化才重建 Icon，避免频繁 GDI 开销）。
/// 托盘存在即程序常驻，关闭主界面 ≠ 退出。
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private readonly WF.NotifyIcon _notifyIcon;
    private readonly WF.ContextMenuStrip _menu;
    private readonly WF.ToolStripMenuItem _soundItem;
    private readonly WF.ToolStripMenuItem _autoStartItem;
    private System.Drawing.Icon? _currentIcon;
    private int _lastBadge = -1;
    private bool _lastOverdue;

    public event Action? RequestShowMain;
    public event Action? RequestAddTask;
    public event Action? RequestOpenSettings;
    public event Action? RequestOpenAbout;
    public event Action? RequestExit;
    public event Action? RequestToggleSound;
    public event Action? RequestToggleAutoStart;

    public TrayIcon(AppSettings settings)
    {
        _menu = new WF.ContextMenuStrip();

        var showMain = _menu.Items.Add("显示主界面");
        showMain.Click += (_, _) => RequestShowMain?.Invoke();
        _menu.Items.Add(new WF.ToolStripSeparator());

        var addTask = _menu.Items.Add("新建任务 (Ctrl+Alt+N)");
        addTask.Click += (_, _) => RequestAddTask?.Invoke();

        _menu.Items.Add(new WF.ToolStripSeparator());

        _soundItem = new WF.ToolStripMenuItem("声音提醒") { CheckOnClick = false };
        _soundItem.Click += (_, _) => RequestToggleSound?.Invoke();
        _menu.Items.Add(_soundItem);

        _autoStartItem = new WF.ToolStripMenuItem("开机自启") { CheckOnClick = false };
        _autoStartItem.Click += (_, _) => RequestToggleAutoStart?.Invoke();
        _menu.Items.Add(_autoStartItem);

        _menu.Items.Add(new WF.ToolStripSeparator());

        var settingsItem = _menu.Items.Add("设置");
        settingsItem.Click += (_, _) => RequestOpenSettings?.Invoke();

        var aboutItem = _menu.Items.Add("关于");
        aboutItem.Click += (_, _) => RequestOpenAbout?.Invoke();

        _menu.Items.Add(new WF.ToolStripSeparator());

        var exitItem = _menu.Items.Add("退出");
        exitItem.Click += (_, _) => RequestExit?.Invoke();

        _notifyIcon = new WF.NotifyIcon
        {
            Text = "桌面轻提醒",
            ContextMenuStrip = _menu,
            Visible = true,
        };
        _notifyIcon.DoubleClick += (_, _) => RequestShowMain?.Invoke();

        // 初始图标
        RefreshBadge(0, false);
    }

    /// <summary>刷新勾选状态（设置/自启变化后调用）</summary>
    public void UpdateCheckStates(bool soundEnabled, bool autoStart)
    {
        _soundItem.Checked = soundEnabled;
        _autoStartItem.Checked = autoStart;
    }

    /// <summary>刷新角标图标；仅数字/颜色变化时才重建（节流）</summary>
    public void RefreshBadge(int badgeCount, bool hasOverdue)
    {
        if (badgeCount == _lastBadge && hasOverdue == _lastOverdue)
        {
            return;
        }

        _lastBadge = badgeCount;
        _lastOverdue = hasOverdue;

        _currentIcon?.Dispose();
        _currentIcon = BuildTrayIcon(badgeCount, hasOverdue);
        _notifyIcon.Icon = _currentIcon;
    }

    /// <summary>
    /// 生成多尺寸 ICO（16/24/32/48，PNG 嵌入），托盘按系统 DPI 自动选最合适尺寸，
    /// 保证图标占满托盘格子（单尺寸 GetHicon 在高 DPI 下会被缩小成小点）。
    /// </summary>
    private static System.Drawing.Icon BuildTrayIcon(int badgeCount, bool hasOverdue)
    {
        var c = ThemeManager.Current;
        var sizes = new[] { 16, 24, 32, 48 };
        var pngs = new Dictionary<int, byte[]>();

        foreach (var s in sizes)
        {
            using var bmp = new System.Drawing.Bitmap(s, s, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = System.Drawing.Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                var accent = System.Drawing.Color.FromArgb(c.AccentColor.R, c.AccentColor.G, c.AccentColor.B);
                using var circleBrush = new System.Drawing.SolidBrush(accent);
                g.FillEllipse(circleBrush, 0, 0, s - 1, s - 1); // 圆占满

                // 时钟（小尺寸简化：只画指针）
                using var pen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(245, 245, 245), Math.Max(1.5f, s * 0.07f));
                var cx = s / 2f; var cy = s / 2f;
                g.DrawEllipse(pen, s * 0.28f, s * 0.28f, s * 0.44f, s * 0.44f);
                g.DrawLine(pen, cx, cy, cx, cy - s * 0.17f);
                g.DrawLine(pen, cx, cy, cx + s * 0.13f, cy + s * 0.03f);

                if (badgeCount > 0)
                {
                    var badgeColor = hasOverdue
                        ? System.Drawing.Color.FromArgb(c.DueColor.R, c.DueColor.G, c.DueColor.B)
                        : System.Drawing.Color.FromArgb(c.ActiveColor.R, c.ActiveColor.G, c.ActiveColor.B);
                    int b = (int)(s * 0.46);
                    using var bb = new System.Drawing.SolidBrush(badgeColor);
                    g.FillEllipse(bb, s - b, 0, b, b);
                    var text = badgeCount > 9 ? "9+" : badgeCount.ToString();
                    using var font = new System.Drawing.Font("Microsoft YaHei", b * 0.55f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Pixel);
                    using var tb = new System.Drawing.SolidBrush(System.Drawing.Color.White);
                    var sf = new System.Drawing.StringFormat { Alignment = System.Drawing.StringAlignment.Center, LineAlignment = System.Drawing.StringAlignment.Center };
                    g.DrawString(text, font, tb, new System.Drawing.RectangleF(s - b, 0, b, b), sf);
                }
            }

            using var ms = new System.IO.MemoryStream();
            bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            pngs[s] = ms.ToArray();
        }

        // 组装多尺寸 ICO
        using var icoMs = new System.IO.MemoryStream();
        using var w = new System.IO.BinaryWriter(icoMs);
        w.Write((ushort)0);            // reserved
        w.Write((ushort)1);            // type: icon
        w.Write((ushort)sizes.Length); // count
        int offset = 6 + 16 * sizes.Length;
        foreach (var s in sizes)
        {
            var png = pngs[s];
            w.Write((byte)(s >= 256 ? 0 : s));  // width
            w.Write((byte)(s >= 256 ? 0 : s));  // height
            w.Write((byte)0);                    // palette
            w.Write((byte)0);                    // reserved
            w.Write((ushort)1);                  // planes
            w.Write((ushort)32);                 // bpp
            w.Write((uint)png.Length);           // image size
            w.Write((uint)offset);               // offset
            offset += png.Length;
        }
        foreach (var s in sizes)
        {
            w.Write(pngs[s]);
        }
        icoMs.Position = 0;
        return new System.Drawing.Icon(icoMs);
    }

    /// <summary>气泡提示（图标状态提醒时告知有几项任务）</summary>
    public void ShowBalloon(string title, string message)
    {
        _notifyIcon.ShowBalloonTip(4000, title, message, WF.ToolTipIcon.Info);
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _currentIcon?.Dispose();
        _menu.Dispose();
    }
}
