# -*- coding: utf-8 -*-
"""按窗口标题抓取屏幕上该窗口区域的截图（用于开发期可视化验证）。

用法：python scripts/shot.py <标题片段> <输出.png>
"""
import ctypes
import sys
from ctypes import wintypes

try:
    ctypes.windll.shcore.SetProcessDpiAwareness(2)
except Exception:
    try:
        ctypes.windll.user32.SetProcessDPIAware()
    except Exception:
        pass

from PySide6.QtWidgets import QApplication  # noqa: E402

user32 = ctypes.windll.user32
EnumWindowsProc = ctypes.WINFUNCTYPE(ctypes.c_bool, wintypes.HWND,
                                     wintypes.LPARAM)
found = []


def cb(hwnd, lparam):
    if user32.IsWindowVisible(hwnd):
        ln = user32.GetWindowTextLengthW(hwnd)
        if ln:
            buf = ctypes.create_unicode_buffer(ln + 1)
            user32.GetWindowTextW(hwnd, buf, ln + 1)
            if sys.argv[1] in buf.value:
                r = wintypes.RECT()
                user32.GetWindowRect(hwnd, ctypes.byref(r))
                found.append((r.left, r.top,
                              r.right - r.left, r.bottom - r.top))
    return True


user32.EnumWindows(EnumWindowsProc(cb), 0)
if not found:
    print("WINDOW_NOT_FOUND")
    sys.exit(2)

app = QApplication([])
screen = app.primaryScreen()
pm = screen.grabWindow(0)
x, y, w, h = found[0]
crop = pm.copy(max(0, x), max(0, y), w, h)
crop.save(sys.argv[2])
print("SAVED", sys.argv[2], w, h)
