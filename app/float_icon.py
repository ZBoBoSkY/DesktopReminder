# -*- coding: utf-8 -*-
"""悬浮图标：可拖动的小圆钮，带颜色角标和倒计时标签。

- 没任务时：纯圆形
- 有任务倒计时时：圆形右侧伸出小标签，显示最近到期任务的倒计时
- 红色角标（右上）：已过期未处理的任务数
- 绿色角标（左下）：正在倒计时的任务数
"""
from PySide6.QtCore import Qt, QPointF, QRectF, Signal
from PySide6.QtGui import QColor, QFont, QPainter, QPen
from PySide6.QtWidgets import QMenu, QWidget

CIRCLE_SIZE = 54
TAG_WIDTH = 64
TAG_HEIGHT = 30
MARGIN = 4


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
            w = CIRCLE_SIZE + TAG_WIDTH + MARGIN * 2
        else:
            w = CIRCLE_SIZE + MARGIN * 2
        h = CIRCLE_SIZE + MARGIN * 2
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

        cx = MARGIN + CIRCLE_SIZE / 2.0
        cy = self.height() / 2.0
        size = CIRCLE_SIZE if not self._hover else CIRCLE_SIZE + 3

        # 阴影
        p.setBrush(QColor(0, 0, 0, 80))
        p.setPen(Qt.NoPen)
        p.drawEllipse(QPointF(cx + 1, cy + 2), size / 2, size / 2)

        # 圆形主体
        bg = QColor(26, 30, 40, 235 if self._hover else 205)
        p.setBrush(bg)
        p.setPen(QPen(QColor(34, 211, 238, 220), 1.5))
        p.drawEllipse(QPointF(cx, cy), size / 2, size / 2)

        # 时钟图标
        pen = QPen(QColor(230, 240, 250, 240), 2.2)
        pen.setCapStyle(Qt.RoundCap)
        p.setPen(pen)
        p.setBrush(Qt.NoBrush)
        p.drawEllipse(QPointF(cx, cy - 1), 8.5, 8.5)
        p.drawLine(QPointF(cx, cy - 1), QPointF(cx, cy - 5))
        p.drawLine(QPointF(cx, cy - 1), QPointF(cx + 3.2, cy + 1.5))

        # 红色角标（右上）：过期未处理数
        if self._done_count > 0:
            bx = cx + size / 2 - 6
            by = cy - size / 2 - 2
            self._draw_badge(p, bx, by, self._done_count,
                              QColor(248, 113, 113))

        # 绿色角标（左下）：倒计时任务数
        if self._running_count > 0:
            bx = cx - size / 2 - 4
            by = cy + size / 2 - 10
            self._draw_badge(p, bx, by, self._running_count,
                              QColor(52, 211, 153))

        # 右侧倒计时标签
        if self._countdown is not None:
            self._draw_countdown_tag(p, cx + size / 2 + MARGIN, cy)

        p.end()

    def _draw_badge(self, p, x, y, count, color):
        p.setBrush(color)
        p.setPen(Qt.NoPen)
        p.drawEllipse(x, y, 16, 16)
        f = QFont("Microsoft YaHei UI", 7)
        f.setBold(True)
        p.setFont(f)
        p.setPen(QColor(255, 255, 255))
        text = str(min(count, 99))
        p.drawText(QRectF(x, y, 16, 16), Qt.AlignCenter, text)

    def _draw_countdown_tag(self, p, x, cy):
        # 标签背景
        tag_rect = QRectF(x, cy - TAG_HEIGHT / 2.0, TAG_WIDTH, TAG_HEIGHT)
        p.setBrush(QColor(26, 30, 40, 230))
        p.setPen(QPen(QColor(34, 211, 238, 180), 1))
        p.drawRoundedRect(tag_rect, 8, 8)

        # 倒计时文字
        m, s = divmod(self._countdown, 60)
        h, m = divmod(m, 60)
        if h > 0:
            text = f"{h}:{m:02d}:{s:02d}"
        else:
            text = f"{m:02d}:{s:02d}"
        f = QFont("Consolas", 11)
        f.setBold(True)
        p.setFont(f)
        # 剩余不足1分钟变红色
        color = QColor(248, 113, 113) if self._countdown < 60 else QColor(167, 243, 208)
        p.setPen(color)
        p.drawText(tag_rect, Qt.AlignCenter, text)

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
