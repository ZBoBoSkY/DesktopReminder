# -*- coding: utf-8 -*-
"""悬浮图标 v2：胶囊形状，红色单角标，绿色倒计时文字。

- 没任务时：小圆角矩形（接近圆形），只显示时钟
- 有任务时：胶囊变宽，右侧显示最近到期任务的倒计时（绿色文字）
- 红色角标（右上）：已过期未处理的任务数
- 倒计时文字绿色 = 有任务正在倒计时；不足1分钟变红色
"""
from PySide6.QtCore import Qt, QPointF, QRectF, Signal
from PySide6.QtGui import QColor, QFont, QPainter, QPen
from PySide6.QtWidgets import QMenu, QWidget

# 尺寸
HEIGHT = 48
CIRCLE_W = 48          # 没任务时的宽度（正方形+大圆角≈圆形）
TAG_W = 72             # 倒计时区域宽度
MARGIN = 6             # 窗口边距（给角标留空间）
BADGE_SIZE = 16


class FloatIcon(QWidget):
    expandRequest = Signal()
    quitRequest = Signal()

    def __init__(self, app_ctx=None):
        super().__init__()
        self.app_ctx = app_ctx
        self.setWindowFlags(Qt.FramelessWindowHint
                            | Qt.WindowStaysOnTopHint | Qt.Tool)
        self.setAttribute(Qt.WA_TranslucentBackground, True)
        self._running_count = 0
        self._done_count = 0
        self._countdown = None       # 秒数，None 表示没有
        self._hover = False
        self._pressed = False
        self._moved = False
        self._drag_offset = None
        self._update_size()
        self.setToolTip("任务提醒")
        self.setCursor(Qt.PointingHandCursor)

    def _update_size(self):
        if self._countdown is not None:
            w = CIRCLE_W + TAG_W + MARGIN * 2
        else:
            w = CIRCLE_W + MARGIN * 2
        h = HEIGHT + MARGIN * 2
        self.setFixedSize(w, h)

    # ---------- 数据设置 ----------
    def set_counts(self, running_count, done_count):
        self._running_count = int(running_count)
        self._done_count = int(done_count)
        self.update()

    def set_countdown(self, seconds):
        """设置最近到期任务的倒计时秒数，None 表示没有倒计时任务。"""
        old = self._countdown
        if seconds is not None:
            seconds = max(0, int(seconds))
        self._countdown = seconds
        if (old is None) != (seconds is None):
            self._update_size()
        self.update()

    # ---------- 绘制 ----------
    def paintEvent(self, e):
        p = QPainter(self)
        p.setRenderHint(QPainter.Antialiasing)
        p.setRenderHint(QPainter.TextAntialiasing)

        # 胶囊主体区域（留出 margin 给角标）
        body_x = MARGIN
        body_y = MARGIN
        body_w = self.width() - MARGIN * 2
        body_h = HEIGHT
        radius = HEIGHT / 2.0
        body_rect = QRectF(body_x, body_y, body_w, body_h)

        # 阴影
        p.setBrush(QColor(0, 0, 0, 90))
        p.setPen(Qt.NoPen)
        p.drawRoundedRect(QRectF(body_x + 1, body_y + 2, body_w, body_h),
                          radius, radius)

        # 胶囊背景（深蓝灰玻璃）
        bg_alpha = 240 if self._hover else 215
        p.setBrush(QColor(15, 23, 42, bg_alpha))
        p.setPen(QPen(QColor(34, 211, 238, 200), 1.5))
        p.drawRoundedRect(body_rect, radius, radius)

        # 顶部高光（玻璃质感）
        highlight = QRectF(body_x + 2, body_y + 1, body_w - 4, body_h / 2)
        p.setBrush(QColor(255, 255, 255, 12))
        p.setPen(Qt.NoPen)
        p.drawRoundedRect(highlight, radius / 2, radius / 2)

        # 左侧时钟图标（在 CIRCLE_W 区域中心）
        clock_cx = body_x + CIRCLE_W / 2.0
        clock_cy = body_y + body_h / 2.0
        pen = QPen(QColor(226, 232, 240, 240), 2.0)
        pen.setCapStyle(Qt.RoundCap)
        p.setPen(pen)
        p.setBrush(Qt.NoBrush)
        p.drawEllipse(QPointF(clock_cx, clock_cy), 8.5, 8.5)
        p.drawLine(QPointF(clock_cx, clock_cy - 1), QPointF(clock_cx, clock_cy - 5))
        p.drawLine(QPointF(clock_cx, clock_cy - 1), QPointF(clock_cx + 3.2, clock_cy + 1.5))

        # 右侧倒计时文字（有任务时）
        if self._countdown is not None:
            tag_x = body_x + CIRCLE_W
            tag_rect = QRectF(tag_x, body_y, TAG_W, body_h)
            m, s = divmod(self._countdown, 60)
            h, m = divmod(m, 60)
            if h > 0:
                text = f"{h}:{m:02d}:{s:02d}"
            else:
                text = f"{m:02d}:{s:02d}"
            f = QFont("Consolas", 13)
            f.setBold(True)
            p.setFont(f)
            # 不足1分钟变红，否则绿色
            if self._countdown < 60:
                p.setPen(QColor(248, 113, 113))
            else:
                p.setPen(QColor(52, 211, 153))
            p.drawText(tag_rect, Qt.AlignCenter, text)

        # 红色角标（右上）：过期未处理数
        if self._done_count > 0:
            badge_x = body_x + body_w - BADGE_SIZE + 2
            badge_y = body_y - 4
            p.setBrush(QColor(248, 113, 113))
            p.setPen(QPen(QColor(255, 255, 255, 200), 1))
            p.drawEllipse(QRectF(badge_x, badge_y, BADGE_SIZE, BADGE_SIZE))
            f = QFont("Microsoft YaHei UI", 7)
            f.setBold(True)
            p.setFont(f)
            p.setPen(QColor(255, 255, 255))
            text = str(min(self._done_count, 99))
            p.drawText(QRectF(badge_x, badge_y, BADGE_SIZE, BADGE_SIZE),
                       Qt.AlignCenter, text)

        p.end()

    # ---------- 交互 ----------
    def enterEvent(self, e):
        self._hover = True
        self.raise_()
        self.activateWindow()
        self.update()
        super().enterEvent(e)

    def leaveEvent(self, e):
        self._hover = False
        self.update()
        super().leaveEvent(e)

    def mousePressEvent(self, e):
        if e.button() == Qt.LeftButton:
            self._pressed = True
            self._moved = False
            self._drag_offset = (e.globalPosition().toPoint()
                                 - self.frameGeometry().topLeft())
            self.raise_()
            self.activateWindow()

    def mouseMoveEvent(self, e):
        if self._pressed and (e.buttons() & Qt.LeftButton):
            self._moved = True
            self.move(e.globalPosition().toPoint() - self._drag_offset)

    def mouseReleaseEvent(self, e):
        if e.button() == Qt.LeftButton:
            if not self._moved:
                self.expandRequest.emit()
            self._pressed = False

    def contextMenuEvent(self, e):
        menu = QMenu(self)
        a1 = menu.addAction("展开面板")
        a1.triggered.connect(self.expandRequest)
        menu.addSeparator()
        a2 = menu.addAction("退出")
        a2.triggered.connect(self.quitRequest)
        menu.exec(e.globalPos())

    def moveEvent(self, e):
        super().moveEvent(e)
        if self.app_ctx and self.isVisible():
            self.app_ctx.save_icon_pos(self.pos())
