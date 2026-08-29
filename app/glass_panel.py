# -*- coding: utf-8 -*-
"""玻璃主面板 v2：深蓝灰玻璃、左侧色条、等宽时间、渐变进度条。

状态由控制器控制：
- 激活态：opacity=1.0，完全不透明
- 弱化态：opacity=配置值（约0.35），半透明
鼠标移上 → panelActivated 信号；鼠标离开 → panelDeactivated 信号。
"""
from PySide6.QtCore import (QEasingCurve, QPoint, QPropertyAnimation, Qt,
                            QTimer, Signal)
from PySide6.QtGui import (QBrush, QColor, QFont, QFontMetrics, QLinearGradient,
                           QPainter, QPen)
from PySide6.QtWidgets import (QFrame, QGraphicsOpacityEffect, QHBoxLayout,
                               QLabel, QMenu, QProgressBar, QPushButton,
                               QScrollArea, QSizeGrip, QSizePolicy,
                               QToolButton, QVBoxLayout, QWidget)

from .task_manager import STATUS_DONE, STATUS_RUNNING
from .theme import (ACCENT, ACCENT_END, BG, DONE, EXPIRED, RUNNING, SOON,
                    SUCCESS, TEXT, TEXT_BRIGHT, TEXT_DIM, WARN)
from .winutil import enable_acrylic, force_foreground

QSS = """
QWidget { color: #f1f5f9; font-family: "Microsoft YaHei UI"; }
QToolTip { background:#0f172a; color:#e2e8f0; border:1px solid rgba(148,163,184,60); border-radius:6px; padding:5px 10px; }
QScrollArea { border:none; background:transparent; }
QScrollArea > QWidget > QWidget { background:transparent; }
QScrollBar:vertical { background:transparent; width:6px; margin:0; }
QScrollBar::handle:vertical { background: rgba(148,163,184,80); border-radius:3px; min-height:24px; }
QScrollBar::handle:vertical:hover { background: rgba(148,163,184,140); }
QScrollBar::add-line:vertical, QScrollBar::sub-line:vertical { height:0; }
QScrollBar::add-page:vertical, QScrollBar::sub-page:vertical { background:transparent; }

QFrame#row { background: rgba(30,41,59,150); border-radius:14px; }
QFrame#row:hover { background: rgba(51,65,85,180); }
QFrame#row[done="true"] { background: rgba(251,191,36,22); }
QFrame#row[breath="on"] { background: rgba(251,191,36,38); }
QFrame#row[breath="off"] { background: rgba(251,191,36,18); }

QToolButton#opBtn { border:none; border-radius:10px; color:#94a3b8; font-size:13px; }
QToolButton#opBtn:hover { color:#0f172a; background:qlineargradient(x1:0,y1:0,x2:1,y2:0, stop:0 #22d3ee, stop:1 #3b82f6); }
QToolButton#opBtn:pressed { background:qlineargradient(x1:0,y1:0,x2:1,y2:0, stop:0 #0891b2, stop:1 #2563eb); }
QToolButton#opBtn[done="true"] { color:#0f172a; background:#fbbf24; }
QToolButton#opBtn[done="true"]:hover { background:#f59e0b; }

QToolButton#headBtn { border:none; border-radius:8px; color:#94a3b8; font-size:14px; padding:3px 9px; }
QToolButton#headBtn:hover { background:qlineargradient(x1:0,y1:0,x2:1,y2:0, stop:0 rgba(34,211,238,40), stop:1 rgba(59,130,246,40)); color:#f8fafc; }
QToolButton#headBtn:pressed { background:qlineargradient(x1:0,y1:0,x2:1,y2:0, stop:0 rgba(34,211,238,60), stop:1 rgba(59,130,246,60)); }

QLabel#title { font-size:13px; font-weight:600; color:#f8fafc; }
QLabel#taskTitle { color:#e2e8f0; font-size:12px; }
QLabel#timeLabel { color:#f8fafc; font-size:13px; font-family:"Consolas","Courier New",monospace; font-weight:600; }
QLabel#empty { font-size:11px; color:#64748b; }

QMenu { background:#1e293b; color:#e2e8f0; border:1px solid rgba(148,163,184,50); border-radius:8px; padding:5px; }
QMenu::item { padding:6px 20px; border-radius:5px; }
QMenu::item:selected { background:#22d3ee; color:#0f172a; }
QMenu::separator { height:1px; background: rgba(148,163,184,40); margin:5px 10px; }

QFrame#card {
    border-top: 1px solid rgba(255,255,255,55);
    border-left: 1px solid rgba(255,255,255,25);
    border-right: 1px solid rgba(255,255,255,12);
    border-bottom: 1px solid rgba(255,255,255,8);
    border-radius: 14px;
    background: transparent;
}
"""

SNOOZE_OPTIONS = [(5, "5 分钟后"), (10, "10 分钟后"),
                   (15, "15 分钟后"), (30, "30 分钟后")]


def _fmt(seconds):
    seconds = int(seconds)
    h, m = divmod(seconds, 3600)
    m, s = divmod(m, 60)
    if h:
        return f"{h}:{m:02d}:{s:02d}"
    return f"{m:02d}:{s:02d}"


def _status_color(task, now):
    """根据任务状态和剩余时间返回对应的颜色。"""
    if task.status == STATUS_RUNNING:
        rem = int(task.remaining_seconds(now))
        if rem <= 60:
            return SOON       # 即将到期：橙色
        return RUNNING        # 进行中：青色
    return DONE               # 已完成待检查：琥珀色


class TitleBar(QWidget):
    """标题栏容器：固定高度，整个区域可拖拽（拖拽逻辑在父控件判断）。"""

    HEIGHT = 32

    def __init__(self, parent=None):
        super().__init__(parent)
        self.setFixedHeight(self.HEIGHT)


class AlarmIcon(QWidget):
    """矢量绘制的闹钟图标（青→蓝渐变，跟应用图标同风格）。"""

    def __init__(self, size=56, parent=None):
        super().__init__(parent)
        self.setFixedSize(size, size)

    def paintEvent(self, e):
        p = QPainter(self)
        p.setRenderHint(QPainter.Antialiasing)
        p.setRenderHint(QPainter.TextAntialiasing)
        w = self.width()
        h = self.height()
        cx = w / 2.0
        cy = h / 2.0 + 2
        radius = w * 0.30

        # 青→蓝渐变
        grad = QLinearGradient(cx - radius, cy - radius, cx + radius, cy + radius)
        grad.setColorAt(0.0, QColor(34, 211, 238))
        grad.setColorAt(1.0, QColor(59, 130, 246))

        # 顶部铃铛（左右）
        p.setBrush(QBrush(grad))
        p.setPen(Qt.NoPen)
        bell_w = radius * 0.35
        bell_h = radius * 0.25
        p.drawEllipse(QPointF(cx - radius * 0.75, cy - radius * 0.85), bell_w, bell_h)
        p.drawEllipse(QPointF(cx + radius * 0.75, cy - radius * 0.85), bell_w, bell_h)

        # 底部支架
        p.setBrush(QBrush(grad))
        p.drawRoundedRect(QRectF(cx - radius * 0.3, cy + radius * 0.85,
                                 radius * 0.6, radius * 0.18), 2, 2)

        # 表盘背景（深色）
        p.setBrush(QColor(15, 23, 42, 200))
        p.setPen(QPen(QBrush(grad), 2.5))
        p.drawEllipse(QPointF(cx, cy), radius, radius)

        # 时针
        p.setPen(QPen(QColor(226, 232, 240), 2.0, Qt.SolidLine, Qt.RoundCap))
        p.drawLine(QPointF(cx, cy), QPointF(cx - radius * 0.3, cy - radius * 0.15))
        # 分针
        p.setPen(QPen(QColor(34, 211, 238), 2.0, Qt.SolidLine, Qt.RoundCap))
        p.drawLine(QPointF(cx, cy), QPointF(cx + radius * 0.15, cy - radius * 0.45))
        # 中心点
        p.setBrush(QColor(34, 211, 238))
        p.setPen(Qt.NoPen)
        p.drawEllipse(QPointF(cx, cy), 2.0, 2.0)

        p.end()


class DeletedRow(QFrame):
    """回收站任务条目：淡化显示 + 恢复/永久删除按钮。"""

    def __init__(self, task, on_restore, on_permanent_delete):
        super().__init__()
        self.setObjectName("row")
        self._task = task
        self._on_restore = on_restore
        self._on_permanent_delete = on_permanent_delete
        self.setStyleSheet("background: rgba(100,116,139,40); border-radius:10px;")

        outer = QHBoxLayout(self)
        outer.setContentsMargins(0, 0, 0, 0)
        outer.setSpacing(0)

        # 左侧灰色色条
        stripe = QLabel()
        stripe.setFixedWidth(4)
        stripe.setStyleSheet("background:#64748b; border-top-left-radius:10px; border-bottom-left-radius:10px;")
        outer.addWidget(stripe)

        content = QHBoxLayout()
        content.setContentsMargins(10, 8, 10, 8)
        content.setSpacing(8)

        title = QLabel(task.title)
        title.setStyleSheet("color:#94a3b8; font-size:12px; text-decoration:line-through;")
        title.setWordWrap(True)
        title.setSizePolicy(QSizePolicy.Expanding, QSizePolicy.Preferred)

        # 删除时间
        if task.deleted_at:
            mins = int((_now_local() - task.deleted_at).total_seconds() // 60)
            if mins < 60:
                time_text = f"{mins}分钟前删除"
            else:
                hours = mins // 60
                time_text = f"{hours}小时前删除"
        else:
            time_text = ""
        time_label = QLabel(time_text)
        time_label.setStyleSheet("color:#64748b; font-size:10px;")
        time_label.setFixedWidth(80)
        time_label.setAlignment(Qt.AlignRight | Qt.AlignVCenter)

        btn_restore = QToolButton()
        btn_restore.setText("↩")
        btn_restore.setFixedSize(22, 22)
        btn_restore.setToolTip("恢复任务")
        btn_restore.setStyleSheet(
            "QToolButton{border:none;border-radius:8px;color:#94a3b8;font-size:13px;}"
            "QToolButton:hover{background:#22d3ee;color:#0f172a;}")
        btn_restore.clicked.connect(lambda: self._on_restore(self._task.id))

        btn_delete = QToolButton()
        btn_delete.setText("×")
        btn_delete.setFixedSize(22, 22)
        btn_delete.setToolTip("永久删除")
        btn_delete.setStyleSheet(
            "QToolButton{border:none;border-radius:8px;color:#94a3b8;font-size:14px;}"
            "QToolButton:hover{background:#f87171;color:#fff;}")
        btn_delete.clicked.connect(lambda: self._on_permanent_delete(self._task.id))

        content.addWidget(title, 1)
        content.addWidget(time_label)
        content.addWidget(btn_restore)
        content.addWidget(btn_delete)
        outer.addLayout(content, 1)


def _now_local():
    from datetime import datetime
    return datetime.now()


class TaskRow(QFrame):
    """单条任务：左侧色条 + 名称(自动换行) + 等宽时间 + 渐变进度条 + ✓/⏱ 按钮。"""

    def __init__(self, task, now, on_ack, on_snooze, on_remove):
        super().__init__()
        self.setObjectName("row")
        self._task = task
        self._on_ack = on_ack
        self._on_snooze = on_snooze
        self._on_remove = on_remove
        self.setContextMenuPolicy(Qt.CustomContextMenu)
        self.customContextMenuRequested.connect(self._menu)

        outer = QHBoxLayout(self)
        outer.setContentsMargins(0, 0, 0, 0)
        outer.setSpacing(0)

        # 左侧色条（3px，颜色随状态变化）
        self._stripe = QLabel()
        self._stripe.setFixedWidth(4)
        self._stripe.setStyleSheet("background:#22d3ee; border-top-left-radius:14px; border-bottom-left-radius:14px;")
        outer.addWidget(self._stripe)

        # 内容区
        content = QVBoxLayout()
        content.setContentsMargins(10, 8, 10, 8)
        content.setSpacing(5)
        outer.addLayout(content, 1)

        top = QHBoxLayout()
        top.setSpacing(8)
        self._dot = QLabel()
        self._dot.setFixedSize(7, 7)
        self._title = QLabel()
        self._title.setObjectName("taskTitle")
        self._title.setWordWrap(True)
        self._title.setSizePolicy(QSizePolicy.Expanding, QSizePolicy.Preferred)
        self._time = QLabel()
        self._time.setObjectName("timeLabel")
        self._time.setFixedWidth(92)
        self._time.setAlignment(Qt.AlignRight | Qt.AlignVCenter)
        self._ack = QToolButton()
        self._ack.setObjectName("opBtn")
        self._ack.setText("✓")
        self._ack.setFixedSize(24, 24)
        self._ack.setToolTip("确认已完成")
        self._ack.clicked.connect(lambda: self._on_ack(self._task.id))
        self._snooze = QToolButton()
        self._snooze.setObjectName("opBtn")
        self._snooze.setText("⏱")
        self._snooze.setFixedSize(24, 24)
        self._snooze.setToolTip("稍后提醒")
        self._snooze.clicked.connect(self._snooze_menu)
        top.addWidget(self._dot)
        top.addWidget(self._title, 1)
        top.addWidget(self._time)
        top.addWidget(self._ack)
        top.addWidget(self._snooze)
        content.addLayout(top)

        # 渐变进度条（5px，发光效果）
        self._bar = QProgressBar()
        self._bar.setFixedHeight(5)
        self._bar.setTextVisible(False)
        self._bar.setRange(0, 100)
        content.addWidget(self._bar)

        self.update_task(task, now)

        # 淡入动画
        self._opacity_effect = QGraphicsOpacityEffect(self)
        self._opacity_effect.setOpacity(0.0)
        self.setGraphicsEffect(self._opacity_effect)
        self._fade_anim = QPropertyAnimation(self._opacity_effect, b"opacity")
        self._fade_anim.setDuration(300)
        self._fade_anim.setStartValue(0.0)
        self._fade_anim.setEndValue(1.0)
        self._fade_anim.setEasingCurve(QEasingCurve.OutCubic)
        self._fade_anim.start()

    def _snooze_menu(self):
        menu = QMenu(self)
        for minutes, label in SNOOZE_OPTIONS:
            a = menu.addAction(label)
            a.triggered.connect(lambda _=False, m=minutes:
                                self._on_snooze(self._task.id, m))
        menu.exec(self._snooze.mapToGlobal(self._snooze.rect().bottomLeft()))

    def _menu(self, _pos):
        menu = QMenu(self)
        a = menu.addAction("删除该任务")
        a.triggered.connect(lambda: self._on_remove(self._task.id))
        menu.exec(self.mapToGlobal(self.rect().bottomLeft()))

    def update_task(self, task, now):
        self._task = task
        self._title.setText(task.title)
        color = _status_color(task, now)
        color_hex = color.name()

        # 左侧色条
        self._stripe.setStyleSheet(
            f"background:{color_hex}; border-top-left-radius:14px; border-bottom-left-radius:14px;")

        if task.status == STATUS_RUNNING:
            rem = int(task.remaining_seconds(now))
            self._time.setText(_fmt(rem))
            # 时间颜色：即将到期变橙色，否则亮白
            if rem <= 60:
                self._time.setStyleSheet("color:#fb923c; font-size:13px; font-family:Consolas,Courier New,monospace; font-weight:700;")
            else:
                self._time.setStyleSheet("color:#f8fafc; font-size:13px; font-family:Consolas,Courier New,monospace; font-weight:600;")
            self._dot.setStyleSheet(f"background:{color_hex}; border-radius:3px;")
            pct = int(task.remaining_seconds(now) / task.duration_seconds * 100)
            self._bar.setValue(pct)
            self._bar.setVisible(True)
            # 渐变进度条 + 发光：正常青→蓝，紧急橙→红
            if rem <= 60:
                grad = "qlineargradient(x1:0,y1:0,x2:1,y2:0, stop:0 #fb923c, stop:1 #f87171)"
                glow = "rgba(251,146,60,50)"
            else:
                grad = "qlineargradient(x1:0,y1:0,x2:1,y2:0, stop:0 #22d3ee, stop:1 #3b82f6)"
                glow = "rgba(34,211,238,40)"
            self._bar.setStyleSheet(
                "QProgressBar{background:rgba(148,163,184,25); border:none; border-radius:2px;}"
                f"QProgressBar::chunk{{background:{grad}; border-radius:2px;}}"
            )
            self.setProperty("done", "false")
            self._ack.setProperty("done", "false")
            self._ack.setToolTip("确认已完成")
        else:
            mins = task.done_minutes(now)
            self._time.setText(f"已完成{mins}分")
            self._time.setStyleSheet("color:#fbbf24; font-size:12px; font-family:Consolas,Courier New,monospace; font-weight:600;")
            self._dot.setStyleSheet("background:#fbbf24; border-radius:3px;")
            self._bar.setVisible(False)
            self.setProperty("done", "true")
            self._ack.setProperty("done", "true")
            self._ack.setToolTip("确认已检查")
        self.style().unpolish(self)
        self.style().polish(self)
        self._ack.style().unpolish(self._ack)
        self._ack.style().polish(self._ack)

    def set_breath(self, on):
        """设置呼吸灯状态（仅对 done 状态的任务生效）。"""
        if self._task.status == STATUS_DONE:
            self.setProperty("breath", "on" if on else "off")
            self.style().unpolish(self)
            self.style().polish(self)


class GlassPanel(QWidget):
    """玻璃主面板。"""

    addTaskRequested = Signal()
    ackTask = Signal(str)
    snoozeTask = Signal(str, int)
    removeTask = Signal(str)
    restoreTask = Signal(str)
    permanentDeleteTask = Signal(str)
    collapseToIcon = Signal()
    minimizeToTray = Signal()
    panelActivated = Signal()     # 鼠标移上
    panelDeactivated = Signal()   # 鼠标离开

    MIN_WIDTH = 280
    MIN_HEIGHT = 180

    def __init__(self, app_ctx=None):
        super().__init__()
        self.app_ctx = app_ctx
        self.setWindowTitle("桌面轻提醒")
        self.setWindowFlags(Qt.FramelessWindowHint
                            | Qt.WindowStaysOnTopHint | Qt.Tool)
        self.setAttribute(Qt.WA_TranslucentBackground, True)
        self.setStyleSheet(QSS)
        self.setMinimumSize(self.MIN_WIDTH, self.MIN_HEIGHT)
        self.resize(360, 300)
        self._drag_pos = None
        self._rows = {}
        self._acrylic = False
        self._user_resized = False
        self._opacity_anim = None
        self._pos_save_timer = QTimer(self)
        self._pos_save_timer.setSingleShot(True)
        self._pos_save_timer.timeout.connect(self._save_pos)
        # 呼吸灯 timer：到点任务背景缓慢闪烁
        self._breath_on = False
        self._breath_timer = QTimer(self)
        self._breath_timer.setInterval(1000)
        self._breath_timer.timeout.connect(self._on_breath)
        self._breath_timer.start()
        self._build()
        self.setFont(QFont("Microsoft YaHei UI", 9))
        self.setContextMenuPolicy(Qt.CustomContextMenu)
        self.customContextMenuRequested.connect(self._panel_menu)

    # ---------- 窗口行为 ----------
    def showEvent(self, e):
        super().showEvent(e)
        if not self._acrylic:
            enable_acrylic(self.winId(), tint_alpha=130, dark=True)
            self._acrylic = True

    def enterEvent(self, e):
        self.raise_()
        self.activateWindow()
        self.setFocus()
        force_foreground(self.winId())
        self.panelActivated.emit()
        super().enterEvent(e)

    def leaveEvent(self, e):
        self.panelDeactivated.emit()
        super().leaveEvent(e)

    def moveEvent(self, e):
        super().moveEvent(e)
        self._pos_save_timer.start(500)

    def resizeEvent(self, e):
        super().resizeEvent(e)
        if self.isVisible() and not self._in_auto_resize:
            self._user_resized = True

    _in_auto_resize = False

    def mousePressEvent(self, e):
        if e.button() == Qt.LeftButton:
            # 只在标题栏区域允许拖拽（顶部边距6 + card边距4 + 标题栏高度32 = 42）
            if e.pos().y() <= 42:
                self._drag_pos = (e.globalPosition().toPoint()
                                  - self.frameGeometry().topLeft())
                self.raise_()
                self.activateWindow()

    def mouseMoveEvent(self, e):
        if self._drag_pos is not None and (e.buttons() & Qt.LeftButton):
            self.move(e.globalPosition().toPoint() - self._drag_pos)

    def mouseReleaseEvent(self, e):
        self._drag_pos = None

    def mouseDoubleClickEvent(self, e):
        """双击标题栏区域：恢复自动高度。"""
        self._user_resized = False
        self.auto_height()

    def _save_pos(self):
        if self.app_ctx:
            self.app_ctx.save_panel_pos(self.pos())

    def _on_breath(self):
        """呼吸灯：切换所有到点任务的背景透明度。"""
        self._breath_on = not self._breath_on
        for row in self._rows.values():
            row.set_breath(self._breath_on)

    # ---------- 界面 ----------
    def _build(self):
        root = QVBoxLayout(self)
        root.setContentsMargins(6, 6, 6, 2)
        root.setSpacing(0)
        self._card = QFrame(self)
        self._card.setObjectName("card")
        card = QVBoxLayout(self._card)
        card.setContentsMargins(6, 4, 6, 2)
        card.setSpacing(6)
        root.addWidget(self._card, 1)

        # 标题栏（整个区域可拖拽）
        self._title_bar = TitleBar()
        head = QHBoxLayout(self._title_bar)
        head.setContentsMargins(0, 0, 0, 0)
        head.setSpacing(6)
        t = QLabel("⚡ 桌面轻提醒")
        t.setObjectName("title")
        head.addWidget(t)
        head.addStretch(1)
        b_add = QToolButton()
        b_add.setObjectName("headBtn")
        b_add.setText("+")
        b_add.setToolTip("添加任务")
        b_add.clicked.connect(self.addTaskRequested)
        b_min = QToolButton()
        b_min.setObjectName("headBtn")
        b_min.setText("−")
        b_min.setToolTip("收起为悬浮图标")
        b_min.clicked.connect(self.collapseToIcon)
        b_close = QToolButton()
        b_close.setObjectName("headBtn")
        b_close.setText("×")
        b_close.setToolTip("最小化到托盘（后台继续提醒）")
        b_close.clicked.connect(self.minimizeToTray)
        head.addWidget(b_add)
        head.addWidget(b_min)
        head.addWidget(b_close)
        card.addWidget(self._title_bar)

        # 任务列表
        self._scroll = QScrollArea()
        self._scroll.setWidgetResizable(True)
        self._scroll.setFrameShape(QFrame.NoFrame)
        self._scroll.setHorizontalScrollBarPolicy(Qt.ScrollBarAlwaysOff)
        self._task_host = QWidget()
        self._task_layout = QVBoxLayout(self._task_host)
        self._task_layout.setContentsMargins(0, 0, 4, 0)
        self._task_layout.setSpacing(4)
        self._task_layout.addStretch(1)
        self._scroll.setWidget(self._task_host)
        card.addWidget(self._scroll, 1)

        # 空状态（矢量闹钟图标+引导文字）
        self._empty = QWidget()
        empty_layout = QVBoxLayout(self._empty)
        empty_layout.setAlignment(Qt.AlignCenter)
        empty_layout.setSpacing(12)
        empty_layout.setContentsMargins(20, 24, 20, 24)
        empty_icon = AlarmIcon(size=56)
        empty_text = QLabel("还没有任务\n点右上角 + 添加一条会自己跑完的工作")
        empty_text.setAlignment(Qt.AlignCenter)
        empty_text.setObjectName("empty")
        empty_text.setWordWrap(True)
        empty_layout.addWidget(empty_icon, 0, Qt.AlignCenter)
        empty_layout.addWidget(empty_text)
        self._empty.setVisible(False)
        card.addWidget(self._empty)

        # 回收站区域（可折叠）
        self._deleted_expanded = False
        self._deleted_rows = {}
        self._deleted_widget = QWidget()
        deleted_layout = QVBoxLayout(self._deleted_widget)
        deleted_layout.setContentsMargins(0, 0, 0, 0)
        deleted_layout.setSpacing(4)

        # 回收站标题栏（点击展开/折叠）
        self._deleted_header = QFrame()
        self._deleted_header.setCursor(Qt.PointingHandCursor)
        header_layout = QHBoxLayout(self._deleted_header)
        header_layout.setContentsMargins(8, 4, 8, 4)
        header_layout.setSpacing(6)
        self._deleted_arrow = QLabel("▸")
        self._deleted_arrow.setStyleSheet("color:#64748b; font-size:11px;")
        self._deleted_arrow.setFixedWidth(14)
        self._deleted_title = QLabel("🗑 已删除")
        self._deleted_title.setStyleSheet("color:#64748b; font-size:11px;")
        self._deleted_count = QLabel("(0)")
        self._deleted_count.setStyleSheet("color:#64748b; font-size:11px;")
        header_layout.addWidget(self._deleted_arrow)
        header_layout.addWidget(self._deleted_title)
        header_layout.addWidget(self._deleted_count)
        header_layout.addStretch(1)
        self._deleted_header.mousePressEvent = self._toggle_deleted
        deleted_layout.addWidget(self._deleted_header)

        # 回收站任务列表
        self._deleted_list = QWidget()
        self._deleted_list_layout = QVBoxLayout(self._deleted_list)
        self._deleted_list_layout.setContentsMargins(0, 0, 0, 0)
        self._deleted_list_layout.setSpacing(4)
        self._deleted_list_layout.addStretch(1)
        self._deleted_list.setVisible(False)
        deleted_layout.addWidget(self._deleted_list)

        # 清空回收站按钮
        self._clear_deleted_btn = QPushButton("清空回收站")
        self._clear_deleted_btn.setStyleSheet(
            "QPushButton{background:transparent;color:#64748b;border:1px solid rgba(100,116,139,60);"
            "border-radius:6px;padding:3px 8px;font-size:10px;}"
            "QPushButton:hover{background:rgba(248,113,113,20);color:#f87171;border-color:rgba(248,113,113,100);}")
        self._clear_deleted_btn.setVisible(False)
        self._clear_deleted_btn.clicked.connect(self._clear_all_deleted)
        deleted_layout.addWidget(self._clear_deleted_btn)

        self._deleted_widget.setVisible(False)
        card.addWidget(self._deleted_widget)

        # 右下角调整大小手柄
        grip_row = QHBoxLayout()
        grip_row.addStretch(1)
        self._grip = QSizeGrip(self)
        self._grip.setFixedSize(10, 10)
        grip_row.addWidget(self._grip)
        root.addLayout(grip_row)

    def _toggle_deleted(self, e):
        """切换回收站展开/折叠。"""
        self._deleted_expanded = not self._deleted_expanded
        self._deleted_list.setVisible(self._deleted_expanded)
        self._clear_deleted_btn.setVisible(self._deleted_expanded and len(self._deleted_rows) > 0)
        self._deleted_arrow.setText("▾" if self._deleted_expanded else "▸")
        if not self._user_resized:
            self.auto_height()

    def _clear_all_deleted(self):
        """清空回收站（发送信号给控制器）。"""
        for tid in list(self._deleted_rows.keys()):
            self.permanentDeleteTask.emit(tid)

    def _on_restore(self, tid):
        self.restoreTask.emit(tid)

    def _on_permanent_delete(self, tid):
        self.permanentDeleteTask.emit(tid)

    def refresh_deleted(self, deleted_tasks):
        """刷新回收站任务列表。"""
        has_deleted = len(deleted_tasks) > 0
        self._deleted_widget.setVisible(has_deleted)
        self._deleted_count.setText(f"({len(deleted_tasks)})")

        ids = {t.id for t in deleted_tasks}
        for tid in list(self._deleted_rows):
            if tid not in ids:
                row = self._deleted_rows.pop(tid)
                self._deleted_list_layout.removeWidget(row)
                row.deleteLater()
        for t in deleted_tasks:
            if t.id not in self._deleted_rows:
                row = DeletedRow(t, self._on_restore, self._on_permanent_delete)
                self._deleted_rows[t.id] = row
                self._deleted_list_layout.insertWidget(
                    self._deleted_list_layout.count() - 1, row)

        self._clear_deleted_btn.setVisible(
            self._deleted_expanded and len(self._deleted_rows) > 0)
        if not self._user_resized:
            self.auto_height()

    def _panel_menu(self, pos):
        if not self.app_ctx:
            return
        menu = QMenu(self)
        a1 = menu.addAction("添加任务")
        a1.triggered.connect(self.addTaskRequested)
        a2 = menu.addAction("收起为悬浮图标")
        a2.triggered.connect(self.collapseToIcon)
        menu.addSeparator()
        a3 = menu.addAction("声音提示")
        a3.setCheckable(True)
        a3.setChecked(self.app_ctx.settings["sound_enabled"])
        a3.toggled.connect(self.app_ctx.set_sound)
        a4 = menu.addAction("开机自启")
        a4.setCheckable(True)
        a4.setChecked(self.app_ctx.autostart_enabled())
        a4.toggled.connect(self.app_ctx.set_autostart)
        a5 = menu.addAction("设置…")
        a5.triggered.connect(self.app_ctx.open_settings)
        menu.addSeparator()
        a6 = menu.addAction("退出")
        a6.triggered.connect(self.minimizeToTray)
        menu.exec(self.mapToGlobal(pos))

    # ---------- 数据刷新 ----------
    def refresh(self, tasks, now, deleted_tasks=None):
        active = [t for t in tasks if t.status != "ack"]
        has = len(active) > 0
        self._empty.setVisible(not has and not (deleted_tasks and len(deleted_tasks) > 0))
        self._scroll.setVisible(has)
        ids = {t.id for t in active}
        for tid in list(self._rows):
            if tid not in ids:
                row = self._rows.pop(tid)
                self._task_layout.removeWidget(row)
                row.deleteLater()
        for t in active:
            if t.id in self._rows:
                self._rows[t.id].update_task(t, now)
            else:
                row = TaskRow(t, now, self._on_ack, self._on_snooze,
                              self._on_remove)
                self._rows[t.id] = row
                self._task_layout.insertWidget(
                    self._task_layout.count() - 1, row)
        # 刷新回收站
        self.refresh_deleted(deleted_tasks or [])
        if not self._user_resized:
            self.auto_height()

    def auto_height(self):
        """根据任务数量自动调整高度（有最大上限）。"""
        self._in_auto_resize = True
        try:
            screen = self.app_ctx.primaryScreen() if self.app_ctx else None
            max_h = int(screen.availableGeometry().height() * 0.6) if screen else 600
            task_count = len(self._rows)
            ideal = 38 + task_count * 58 + 12
            ideal = max(self.MIN_HEIGHT, min(ideal, max_h))
            self.resize(self.width(), ideal)
        finally:
            self._in_auto_resize = False

    def set_opacity(self, opacity):
        """直接设置透明度（无动画）。"""
        self.setWindowOpacity(opacity)

    def animate_opacity(self, target, duration=400):
        """平滑渐变到目标透明度。"""
        if self._opacity_anim is not None:
            self._opacity_anim.stop()
        self._opacity_anim = QPropertyAnimation(self, b"windowOpacity")
        self._opacity_anim.setDuration(duration)
        self._opacity_anim.setStartValue(float(self.windowOpacity()))
        self._opacity_anim.setEndValue(float(target))
        self._opacity_anim.setEasingCurve(QEasingCurve.OutCubic)
        self._opacity_anim.start()

    def animate_pos(self, target_pos, duration=300):
        """位置弹性动画（OutBack 缓动，有轻微过冲效果）。"""
        if not hasattr(self, '_pos_anim') or self._pos_anim is None:
            self._pos_anim = QPropertyAnimation(self, b"pos")
        self._pos_anim.stop()
        self._pos_anim.setDuration(duration)
        self._pos_anim.setStartValue(self.pos())
        self._pos_anim.setEndValue(target_pos)
        self._pos_anim.setEasingCurve(QEasingCurve.OutBack)
        self._pos_anim.start()

    def _on_ack(self, tid):
        self.ackTask.emit(tid)

    def _on_snooze(self, tid, minutes):
        self.snoozeTask.emit(tid, minutes)

    def _on_remove(self, tid):
        self.removeTask.emit(tid)

    def toggle(self):
        self.raise_()
        self.activateWindow()
        self.setFocus()
