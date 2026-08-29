# -*- coding: utf-8 -*-
"""系统托盘：常驻后台、气泡提醒、右键菜单。"""
from PySide6.QtCore import Qt
from PySide6.QtGui import QColor, QFont, QIcon, QPainter, QPen, QPixmap
from PySide6.QtWidgets import QMenu, QSystemTrayIcon


def make_app_icon(px=64):
    pm = QPixmap(px, px)
    pm.fill(Qt.transparent)
    p = QPainter(pm)
    p.setRenderHint(QPainter.Antialiasing)
    p.setBrush(QColor(24, 28, 38, 245))
    p.setPen(QPen(QColor(34, 211, 238), 2))
    p.drawRoundedRect(2, 2, px - 4, px - 4, 14, 14)
    c = px / 2.0
    pen = QPen(QColor(235, 240, 248), 3)
    pen.setCapStyle(Qt.RoundCap)
    p.setPen(pen)
    p.setBrush(Qt.NoBrush)
    p.drawEllipse(c, c - 2, px * 0.30, px * 0.30)
    p.drawLine(c, c - 2, c, c - 2 - px * 0.10)
    p.drawLine(c, c - 2, c + px * 0.11, c + px * 0.03)
    p.end()
    return QIcon(pm)


class Tray:
    def __init__(self, app_ctx):
        self.app = app_ctx
        self.icon = make_app_icon()
        self.tray = QSystemTrayIcon(self.icon)
        self.tray.setToolTip("桌面轻提醒")

        menu = QMenu()
        a1 = menu.addAction("显示 / 收起面板")
        a1.triggered.connect(app_ctx.toggle_panel)
        a2 = menu.addAction("立即检查任务")
        a2.triggered.connect(app_ctx.check_now)
        menu.addSeparator()
        self.act_sound = menu.addAction("声音提示")
        self.act_sound.setCheckable(True)
        self.act_sound.setChecked(bool(app_ctx.settings["sound_enabled"]))
        self.act_sound.toggled.connect(app_ctx.set_sound)
        self.act_auto = menu.addAction("开机自启")
        self.act_auto.setCheckable(True)
        self.act_auto.setChecked(app_ctx.autostart_enabled())
        self.act_auto.toggled.connect(app_ctx.set_autostart)
        a_set = menu.addAction("设置…")
        a_set.triggered.connect(app_ctx.open_settings)
        menu.addSeparator()
        a3 = menu.addAction("退出")
        a3.triggered.connect(app_ctx.quit)
        self.tray.setContextMenu(menu)
        self.tray.activated.connect(app_ctx.on_tray_activated)
        self.tray.show()

    def notify(self, title, msg):
        try:
            self.tray.showMessage(title, msg,
                                  QSystemTrayIcon.Information, 8000)
        except Exception:
            pass
