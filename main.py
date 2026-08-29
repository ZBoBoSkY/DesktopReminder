# -*- coding: utf-8 -*-
"""桌面轻提醒 — 入口与应用控制器。

三态状态机：
  ACTIVE（激活态）：面板不透明，可交互
  DIM（弱化态）：面板半透明，hover 即激活
  ICON（图标态）：收起为小圆图标，点击才展开

闲置自动降级：鼠标离开面板 → idle_to_dim_秒 → 弱化态 → dim_to_icon_秒 → 图标态
周期性提醒：图标态每 periodic_reminder_minutes 自动展开为弱化态
"""
import json
import os
import sys
import winsound
from datetime import datetime

from PySide6.QtCore import QSharedMemory, QTimer
from PySide6.QtWidgets import (QApplication, QMessageBox, QSystemTrayIcon)

from app import tray as tray_mod
from app.add_dialog import AddTaskDialog
from app.float_icon import FloatIcon
from app.glass_panel import GlassPanel
from app.settings import APP_NAME, CONFIG_PATH, Settings
from app.settings_dialog import SettingsDialog
from app.task_manager import TaskManager, STATUS_DONE, STATUS_RUNNING
from app.tray import Tray
from app.window_monitor import WindowMonitor
from app.winutil import set_startup_with_windows, startup_enabled

MODE_ACTIVE = "active"
MODE_DIM = "dim"
MODE_ICON = "icon"
MODE_HIDDEN = "hidden"   # 最小化到托盘，面板和图标都不显示


class ReminderApp(QApplication):
    def __init__(self, argv, selftest=False, config_path=None,
                 start_icon=False):
        super().__init__(argv)
        self.selftest = selftest
        self.setQuitOnLastWindowClosed(False)
        self.setApplicationName(APP_NAME)
        self.setWindowIcon(tray_mod.make_app_icon())

        cfg = config_path or os.environ.get("REMINDER_CONFIG") or CONFIG_PATH
        self.settings = Settings(cfg)
        self.task_mgr = TaskManager(save_cb=self._persist)
        self.task_mgr.load(self.settings["tasks"])

        self.event_hook = None
        self.mode = MODE_ACTIVE
        self._add_dialog = None
        self._settings_dialog = None

        self.panel = GlassPanel(self)
        self.icon = FloatIcon(self)

        if QSystemTrayIcon.isSystemTrayAvailable() and not selftest:
            self.tray = Tray(self)
        else:
            self.tray = None

        self._wire()
        self._apply_positions()

        self.monitor = WindowMonitor()
        self.monitor.start()

        # 闲置计时器：鼠标离开后开始计时
        self._idle_timer = QTimer(self)
        self._idle_timer.setSingleShot(True)
        self._idle_timer.timeout.connect(self._on_idle_dim)

        # 弱化态转图标计时器
        self._dim_timer = QTimer(self)
        self._dim_timer.setSingleShot(True)
        self._dim_timer.timeout.connect(self._on_dim_to_icon)

        # 周期性提醒计时器（图标态时运行）
        self._periodic_timer = QTimer(self)
        self._periodic_timer.setSingleShot(False)
        self._periodic_timer.timeout.connect(self._on_periodic_remind)

        # 主心跳：每秒检查任务
        self.tick = QTimer(self)
        self.tick.timeout.connect(self._tick)
        self.tick.start(1000)

        if start_icon:
            self.collapse_to_icon()
        else:
            self.show_active()

        leftover = self.task_mgr.unacked_done()
        if leftover and not selftest:
            self._notify("待检查任务",
                         f"有 {len(leftover)} 项任务已完成未检查，打开面板确认一下")

    # ==================== 持久化与外部调用 ====================
    def _persist(self, raw):
        self.settings["tasks"] = raw
        self.settings.save()

    def _wire(self):
        self.panel.addTaskRequested.connect(self.open_add_dialog)
        self.panel.ackTask.connect(self.ack_task)
        self.panel.snoozeTask.connect(self.snooze_task)
        self.panel.removeTask.connect(self.remove_task)
        self.panel.collapseToIcon.connect(self.collapse_to_icon)
        self.panel.minimizeToTray.connect(self.minimize_to_tray)
        self.panel.panelActivated.connect(self._on_panel_activated)
        self.panel.panelDeactivated.connect(self._on_panel_deactivated)
        self.icon.expandRequest.connect(self.show_active)
        self.icon.quitRequest.connect(self.quit)

    def _notify(self, title, msg):
        if self.tray:
            self.tray.notify(title, msg)

    def _beep(self):
        if self.settings["sound_enabled"]:
            try:
                winsound.MessageBeep(winsound.MB_ICONASTERISK)
            except Exception:
                pass

    # ==================== 状态机 ====================
    def _apply_positions(self):
        geo = self.primaryScreen().availableGeometry() if self.primaryScreen() else None
        sp = self.settings["panel_pos"]
        if sp:
            self.panel.move(int(sp[0]), int(sp[1]))
        elif geo:
            self.panel.move(geo.right() - self.panel.width() - 24,
                            geo.top() + 10)
        ip = self.settings["icon_pos"]
        if ip:
            self.icon.move(int(ip[0]), int(ip[1]))
        elif geo:
            self.icon.move(geo.right() - self.icon.width() - 40,
                           geo.top() + 100)

    def _dim_opacity(self):
        """弱化态透明度（0.0~1.0）。"""
        return max(0.1, min(0.9, int(self.settings["opacity_percent"]) / 100.0))

    def show_active(self):
        """显示面板并设为激活态（不透明），带淡入动画。"""
        self._stop_idle_timers()
        self._periodic_timer.stop()
        self.icon.hide()
        self.panel.set_opacity(0.0)
        self.panel.show()
        self.panel.animate_opacity(1.0, 250)
        self.panel.toggle()
        self.mode = MODE_ACTIVE

    def show_dim(self):
        """显示面板并设为弱化态（半透明），带淡入动画，开始闲置计时。"""
        self._stop_idle_timers()
        self.icon.hide()
        self.panel.set_opacity(0.0)
        self.panel.show()
        self.panel.animate_opacity(self._dim_opacity(), 250)
        self.mode = MODE_DIM
        # 弱化态下，鼠标离开后 dim_to_icon_秒 收图标
        self._dim_timer.start(int(self.settings["dim_to_icon_seconds"]) * 1000)

    def collapse_to_icon(self):
        """收起为悬浮图标。"""
        self._stop_idle_timers()
        self.panel.hide()
        self.icon.show()
        self.icon.raise_()
        self.mode = MODE_ICON
        # 启动周期性提醒
        self._restart_periodic_timer()

    def minimize_to_tray(self):
        """最小化到托盘（面板和图标都隐藏）。"""
        self._stop_idle_timers()
        self._periodic_timer.stop()
        self.panel.hide()
        self.icon.hide()
        self.mode = MODE_HIDDEN

    def toggle_panel(self):
        """托盘左键：切换显示。"""
        if self.mode == MODE_ICON:
            self.show_active()
        elif self.mode in (MODE_ACTIVE, MODE_DIM):
            self.collapse_to_icon()
        elif self.mode == MODE_HIDDEN:
            self.show_active()

    def _stop_idle_timers(self):
        self._idle_timer.stop()
        self._dim_timer.stop()

    def _restart_periodic_timer(self):
        """根据配置重启周期性提醒计时器。"""
        self._periodic_timer.stop()
        mins = int(self.settings["periodic_reminder_minutes"])
        if mins > 0:
            self._periodic_timer.start(mins * 60 * 1000)

    # ==================== 闲置与周期性 ====================
    def _on_panel_activated(self):
        """鼠标移上面板：取消闲置计时，变激活态。"""
        if self.mode == MODE_DIM:
            self._stop_idle_timers()
            self.panel.animate_opacity(1.0)
            self.mode = MODE_ACTIVE
        elif self.mode == MODE_ACTIVE:
            self._stop_idle_timers()

    def _on_panel_deactivated(self):
        """鼠标离开面板：开始闲置计时。"""
        if self.mode == MODE_ACTIVE:
            self._idle_timer.start(
                int(self.settings["idle_to_dim_seconds"]) * 1000)
        elif self.mode == MODE_DIM:
            # 已经是弱化态，重启 dim 计时器
            self._dim_timer.start(
                int(self.settings["dim_to_icon_seconds"]) * 1000)

    def _on_idle_dim(self):
        """闲置到点：激活态 → 弱化态。"""
        if self.mode == MODE_ACTIVE:
            self.show_dim()

    def _on_dim_to_icon(self):
        """弱化态到点：弱化态 → 图标态。"""
        if self.mode == MODE_DIM:
            self.collapse_to_icon()

    def _on_periodic_remind(self):
        """周期性提醒：图标态自动展开为弱化态。"""
        if self.mode != MODE_ICON:
            return
        # 没有任务且配置为不提醒时，跳过
        if (not self.settings["remind_when_no_tasks"]
                and not self.task_mgr.has_active_tasks()):
            return
        self.show_dim()
        n = len(self.task_mgr.unacked_done())
        if n:
            self._notify("周期性提醒",
                         f"还有 {n} 项任务已完成未检查，30秒内可查看")

    # ==================== 主心跳 ====================
    def _tick(self):
        now = datetime.now()
        events = self.task_mgr.tick(
            now, int(self.settings["escalation_interval_minutes"]))
        for ev in events:
            self._handle_event(ev)
        self.panel.refresh(self.task_mgr.tasks, now)

        # 更新图标：颜色角标 + 倒计时标签
        running = [t for t in self.task_mgr.tasks
                   if t.status == STATUS_RUNNING]
        running_count = len(running)
        done_count = len(self.task_mgr.unacked_done())
        self.icon.set_counts(running_count, done_count)

        # 最近到期任务的倒计时
        if running:
            min_rem = min(int(t.remaining_seconds(now)) for t in running)
            self.icon.set_countdown(max(0, min_rem))
        else:
            self.icon.set_countdown(None)

    def _handle_event(self, ev):
        if self.event_hook:
            self.event_hook(ev)
        t = ev["task"]
        if ev["kind"] == "done":
            self._beep()
            self._notify("任务完成提醒",
                         f"「{t.title}」预计已完成，记得检查一下")
        elif ev["kind"] == "escalate":
            self._beep()
            self._notify("任务提醒",
                         f"「{t.title}」已完成 {ev['minutes']} 分钟，还没检查哦")

    def check_now(self):
        self._tick()
        running = self.task_mgr.running_count()
        done = len(self.task_mgr.unacked_done())
        self._notify("任务状态",
                     f"进行中 {running} 项；已完成待检查 {done} 项")

    # ==================== 任务操作 ====================
    def add_task(self, title, duration_seconds):
        self.task_mgr.add(title, duration_seconds)
        if not self.selftest and self.tray:
            self.tray.tray.showMessage(
                "已添加任务",
                f"「{title}」约 {duration_seconds // 60} 分钟后提醒检查",
                QSystemTrayIcon.Information, 3000)

    def ack_task(self, tid):
        self.task_mgr.ack(tid)

    def snooze_task(self, tid, minutes):
        self.task_mgr.snooze(tid, minutes)
        if self.tray:
            self._notify("已推迟", f"该任务将在 {minutes} 分钟后再次提醒")

    def remove_task(self, tid):
        self.task_mgr.remove(tid)

    # ==================== 对话框 ====================
    def open_add_dialog(self):
        """打开添加任务对话框，自动填入当前软件名。"""
        if self._add_dialog is not None and self._add_dialog.isVisible():
            self._add_dialog.raise_()
            self._add_dialog.activateWindow()
            return
        current_info = self.monitor.current_info()
        dlg = AddTaskDialog(current_info=current_info, parent=self.panel)
        dlg.taskAdded.connect(self._on_task_added)
        self._add_dialog = dlg
        dlg.show()
        dlg.raise_()
        dlg.activateWindow()

    def _on_task_added(self, title, duration_seconds):
        self.add_task(title, duration_seconds)

    def open_settings(self):
        """打开设置窗口。"""
        if self._settings_dialog is not None and self._settings_dialog.isVisible():
            self._settings_dialog.raise_()
            self._settings_dialog.activateWindow()
            return
        dlg = SettingsDialog(self.settings, parent=self.panel)
        dlg.settingsChanged.connect(self._on_settings_changed)
        self._settings_dialog = dlg
        dlg.show()
        dlg.raise_()
        dlg.activateWindow()

    def _on_settings_changed(self):
        """设置变更后：更新透明度、重启周期性计时器。"""
        if self.mode == MODE_DIM:
            self.panel.animate_opacity(self._dim_opacity())
        if self.mode == MODE_ICON:
            self._restart_periodic_timer()
        # 同步托盘菜单状态
        if self.tray:
            self.tray.act_sound.setChecked(bool(self.settings["sound_enabled"]))
            self.tray.act_auto.setChecked(self.autostart_enabled())

    # ==================== 托盘 / 设置 ====================
    def on_tray_activated(self, reason):
        if reason in (QSystemTrayIcon.Trigger, QSystemTrayIcon.DoubleClick):
            self.toggle_panel()

    def set_sound(self, on):
        self.settings["sound_enabled"] = bool(on)
        self.settings.save()
        if self.tray:
            self.tray.act_sound.setChecked(bool(on))

    def autostart_enabled(self):
        return startup_enabled()

    def set_autostart(self, on):
        ok = set_startup_with_windows(bool(on), self._launch_cmd())
        self.settings["autostart_enabled"] = bool(on) if ok else self.autostart_enabled()
        self.settings.save()
        if self.tray:
            self.tray.act_auto.setChecked(self.autostart_enabled())

    def _launch_cmd(self):
        if getattr(sys, "frozen", False):
            return f'"{sys.executable}"'
        pyw = os.path.join(os.path.dirname(sys.executable), "pythonw.exe")
        py = pyw if os.path.exists(pyw) else sys.executable
        script = os.path.abspath(os.path.join(os.path.dirname(__file__),
                                              "main.py"))
        return f'"{py}" "{script}"'

    def save_panel_pos(self, pos):
        self.settings["panel_pos"] = [pos.x(), pos.y()]
        self.settings.save()

    def save_icon_pos(self, pos):
        self.settings["icon_pos"] = [pos.x(), pos.y()]
        self.settings.save()

    # ==================== 退出 ====================
    def quit(self):
        self.settings.save()
        self.monitor.stop()
        if self.tray:
            self.tray.tray.hide()
        self.panel.hide()
        self.icon.hide()
        super().quit()


def run_selftest():
    import tempfile
    tmp = os.path.join(tempfile.gettempdir(), "reminder_selftest.json")
    try:
        os.remove(tmp)
    except OSError:
        pass
    app = ReminderApp(sys.argv, selftest=True, config_path=tmp)
    app.add_task("任务A：下载大文件", 5)
    app.add_task("任务B：智能体跑数据", 3)
    collected = []
    app.event_hook = collected.append

    def finish():
        print("SELFTEST_EVENTS=" + json.dumps(
            [{"kind": e["kind"], "title": e["task"].title,
              "minutes": e.get("minutes")} for e in collected],
            ensure_ascii=False))
        app.quit()

    QTimer.singleShot(9000, finish)
    return app.exec()


def main():
    if "--selftest" in sys.argv:
        return run_selftest()
    shm = QSharedMemory("DesktopReminderSingleton_v1")
    if not shm.create(1):
        QMessageBox.information(None, APP_NAME, "桌面轻提醒已经在运行了")
        return 0
    app = ReminderApp(sys.argv, start_icon=("--icon" in sys.argv))
    return app.exec()


if __name__ == "__main__":
    sys.exit(main())
