# -*- coding: utf-8 -*-
"""后台线程：轮询前台窗口，记录"正在做什么"的切换时间线。"""
import threading
import time
from datetime import datetime

from .winutil import get_process_name


class WindowMonitor(threading.Thread):
    def __init__(self, on_change=None, interval=1.0):
        super().__init__(daemon=True)
        self.on_change = on_change
        self.interval = interval
        self._stop = threading.Event()
        self.current_title = ""
        self.current_proc = ""
        self.history = []            # [(timestamp, title, proc), ...]
        self._lock = threading.Lock()

    def stop(self):
        self._stop.set()

    def run(self):
        import ctypes
        from ctypes import wintypes
        user32 = ctypes.windll.user32
        last = None
        while not self._stop.is_set():
            try:
                hwnd = user32.GetForegroundWindow()
                title, proc = "", ""
                if hwnd:
                    ln = user32.GetWindowTextLengthW(hwnd)
                    buf = ctypes.create_unicode_buffer(ln + 1)
                    user32.GetWindowTextW(hwnd, buf, ln + 1)
                    title = buf.value
                    pid = wintypes.DWORD()
                    user32.GetWindowThreadProcessId(hwnd, ctypes.byref(pid))
                    proc = get_process_name(pid.value)
                cur = (title, proc)
                with self._lock:
                    self.current_title, self.current_proc = cur
                if cur != last:
                    last = cur
                    if title:
                        with self._lock:
                            self.history.append((time.time(), title, proc))
                            if len(self.history) > 40:
                                self.history = self.history[-40:]
                        if self.on_change:
                            try:
                                self.on_change(title, proc)
                            except Exception:
                                pass
            except Exception:
                pass
            time.sleep(self.interval)

    def snapshot(self):
        with self._lock:
            return (self.current_title, self.current_proc, list(self.history))

    def current_info(self):
        """返回当前前台软件名（去掉 .exe 后缀，不拼接窗口标题）。"""
        _, proc, _ = self.snapshot()
        if not proc:
            return ""
        name = proc
        if name.lower().endswith(".exe"):
            name = name[:-4]
        return name

    def history_text(self, n=6):
        _, _, hist = self.snapshot()
        if not hist:
            return "暂无窗口切换记录"
        lines = []
        for ts, t, p in hist[-n:]:
            tm = datetime.fromtimestamp(ts).strftime("%H:%M:%S")
            lines.append(f"{tm}  {p} · {t}")
        return "\n".join(lines)
