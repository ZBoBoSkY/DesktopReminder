# -*- coding: utf-8 -*-
"""玻璃主面板 v2：深蓝灰玻璃、左侧色条、等宽时间、渐变进度条。

状态由控制器控制：
- 激活态：opacity=1.0，完全不透明
- 弱化态：opacity=配置值（约0.35），半透明
鼠标移上 → panelActivated 信号；鼠标离开 → panelDeactivated 信号。
"""
from PySide6.QtCore import (QEasingCurve, QPropertyAnimation, Qt, QTimer,
                            Signal)
from PySide6.QtGui import QColor, QFont, QFontMetrics
from PySide6.QtWidgets import (QFrame, QHBoxLayout, QLabel, QMenu,
                               QProgressBar, QPushButton, QScrollArea,
                               QSizeGrip, QSizePolicy, QToolButton,
                               QVBoxLayout, QWidget)

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

QFrame#row { background: rgba(30,41,59,150); border-radius:12px; }
QFrame#row:hover { background: rgba(51,65,85,170); }
QFrame#row[done="true"] { background: rgba(251,191,36,22); }

QToolButton#opBtn { border:none; border-radius:10px; color:#94a3b8; font-size:13px; }
QToolButton#opBtn:hover { color:#0f172a; background:#22d3ee; }
QToolButton#opBtn[done="true"] { color:#0f172a; background:#fbbf24; }

QToolButton#headBtn { border:none; border-radius:8px; color:#94a3b8; font-size:14px; padding:3px 9px; }
QToolButton#headBtn:hover { background: rgba(255,255,255,30); color:#f8fafc; }

QLabel#title { font-size:13px; font-weight:600; color:#f8fafc; }
QLabel#taskTitle { color:#e2e8f0; font-size:12px; }
QLabel#timeLabel { color:#f8fafc; font-size:13px; font-family:"Consolas","Courier New",monospace; font-weight:600; }
QLabel#empty { font-size:11px; color:#64748b; }

QMenu { background:#1e293b; color:#e2e8f0; border:1px solid rgba(148,163,184,50); border-radius:8px; padding:5px; }
QMenu::item { padding:6px 20px; border-radius:5px; }
QMenu::item:selected { background:#22d3ee; color:#0f172a; }
QMenu::separator { height:1px; background: rgba(148,163,184,40); margin:5px 10px; }
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
        self._stripe.setStyleSheet("background:#22d3ee; border-top-left-radius:12px; border-bottom-left-radius:12px;")
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
            f"background:{color_hex}; border-top-left-radius:12px; border-bottom-left-radius:12px;")

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


class GlassPanel(QWidget):
    """玻璃主面板。"""

    addTaskRequested = Signal()
    ackTask = Signal(str)
    snoozeTask = Signal(str, int)
    removeTask = Signal(str)
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
        self._build()
        self.setFont(QFont("Microsoft YaHei UI", 9))
        self.setContextMenuPolicy(Qt.CustomContextMenu)
        self.customContextMenuRequested.connect(self._panel_menu)

    # ---------- 窗口行为 ----------
    def showEvent(self, e):
        super().showEvent(e)
        if not self._acrylic:
            enable_acrylic(self.winId(), tint_alpha=100, dark=True)
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

    # ---------- 界面 ----------
    def _build(self):
        root = QVBoxLayout(self)
        root.setContentsMargins(10, 10, 10, 4)
        root.setSpacing(0)
        self._card = QFrame(self)
        self._card.setObjectName("card")
        card = QVBoxLayout(self._card)
        card.setContentsMargins(8, 6, 8, 4)
        card.setSpacing(6)
        root.addWidget(self._card, 1)

        # 标题栏
        head = QHBoxLayout()
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
        card.addLayout(head)

        # 任务列表
        self._scroll = QScrollArea()
        self._scroll.setWidgetResizable(True)
        self._scroll.setFrameShape(QFrame.NoFrame)
        self._scroll.setHorizontalScrollBarPolicy(Qt.ScrollBarAlwaysOff)
        self._task_host = QWidget()
        self._task_layout = QVBoxLayout(self._task_host)
        self._task_layout.setContentsMargins(0, 0, 4, 0)
        self._task_layout.setSpacing(6)
        self._task_layout.addStretch(1)
        self._scroll.setWidget(self._task_host)
        card.addWidget(self._scroll, 1)

        # 空状态
        self._empty = QLabel("还没有任务。\n点右上角 + 添加一条会自己跑完的工作。")
        self._empty.setObjectName("empty")
        self._empty.setAlignment(Qt.AlignCenter)
        self._empty.setVisible(False)
        card.addWidget(self._empty)

        # 右下角调整大小手柄
        grip_row = QHBoxLayout()
        grip_row.addStretch(1)
        self._grip = QSizeGrip(self)
        self._grip.setFixedSize(14, 14)
        grip_row.addWidget(self._grip)
        root.addLayout(grip_row)

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
    def refresh(self, tasks, now):
        active = [t for t in tasks if t.status != "ack"]
        has = len(active) > 0
        self._empty.setVisible(not has)
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
        if not self._user_resized:
            self.auto_height()

    def auto_height(self):
        """根据任务数量自动调整高度（有最大上限）。"""
        self._in_auto_resize = True
        try:
            screen = self.app_ctx.primaryScreen() if self.app_ctx else None
            max_h = int(screen.availableGeometry().height() * 0.6) if screen else 600
            task_count = len(self._rows)
            ideal = 50 + task_count * 66 + 40
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
