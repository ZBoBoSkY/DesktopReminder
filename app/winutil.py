# -*- coding: utf-8 -*-
"""Windows 原生能力：亚克力玻璃效果、前台进程名、开机自启。"""
import ctypes
from ctypes import wintypes


def enable_acrylic(hwnd, tint_alpha=110, dark=True):
    """为指定窗口启用 Windows 10/11 亚克力(acrylic)背景模糊。

    tint_alpha: 0-255 着色透明度（越高越不透明）
    dark: True=深色着色, False=浅色着色
    """
    try:
        class ACCENT_POLICY(ctypes.Structure):
            _fields_ = [("AccentState", ctypes.c_uint),
                        ("AccentFlags", ctypes.c_uint),
                        ("GradientColor", ctypes.c_uint),
                        ("AnimationId", ctypes.c_uint)]

        class WINDOWCOMPOSITIONATTRIBDATA(ctypes.Structure):
            _fields_ = [("Attribute", ctypes.c_int),
                        ("Data", ctypes.POINTER(ACCENT_POLICY)),
                        ("SizeOfData", ctypes.c_size_t)]

        accent = ACCENT_POLICY()
        accent.AccentState = 4                # ACCENT_ENABLE_ACRYLICBLURBEHIND
        accent.AccentFlags = 2
        base = 0x000000 if dark else 0xFFFFFF
        accent.GradientColor = (int(tint_alpha) << 24) | base   # AABBGGRR

        data = WINDOWCOMPOSITIONATTRIBDATA()
        data.Attribute = 19                   # WCA_ACCENT_POLICY
        data.SizeOfData = ctypes.sizeof(accent)
        data.Data = ctypes.pointer(accent)

        ok = ctypes.windll.user32.SetWindowCompositionAttribute(
            int(hwnd), ctypes.byref(data))
        return bool(ok)
    except Exception:
        return False


def force_foreground(hwnd):
    """尝试把窗口设为前台（配合 Qt.activateWindow 使用，增强抢焦点效果）。"""
    try:
        ctypes.windll.user32.SetForegroundWindow(int(hwnd))
        return True
    except Exception:
        return False


def get_process_name(pid):
    """根据 PID 取进程可执行文件名（如 chrome.exe）。"""
    try:
        kernel32 = ctypes.windll.kernel32
        PROCESS_QUERY_LIMITED_INFORMATION = 0x1000
        handle = kernel32.OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION,
                                      False, int(pid))
        if not handle:
            return str(pid)
        try:
            buf = ctypes.create_unicode_buffer(1024)
            size = wintypes.DWORD(1024)
            if kernel32.QueryFullProcessImageNameW(handle, 0, buf,
                                                   ctypes.byref(size)):
                return buf.value.rsplit("\\", 1)[-1]
            return str(pid)
        finally:
            kernel32.CloseHandle(handle)
    except Exception:
        return str(pid)


def set_startup_with_windows(enable, command):
    """写入 / 删除当前用户的开机自启注册表项。"""
    import winreg
    key_path = r"Software\Microsoft\Windows\CurrentVersion\Run"
    try:
        key = winreg.OpenKey(winreg.HKEY_CURRENT_USER, key_path, 0,
                             winreg.KEY_SET_VALUE)
        try:
            if enable:
                winreg.SetValueEx(key, "桌面轻提醒", 0, winreg.REG_SZ, command)
            else:
                try:
                    winreg.DeleteValue(key, "桌面轻提醒")
                except FileNotFoundError:
                    pass
        finally:
            winreg.CloseKey(key)
        return True
    except Exception:
        return False


def startup_enabled():
    """查询开机自启是否已开启。"""
    import winreg
    key_path = r"Software\Microsoft\Windows\CurrentVersion\Run"
    try:
        key = winreg.OpenKey(winreg.HKEY_CURRENT_USER, key_path, 0,
                             winreg.KEY_READ)
        try:
            winreg.QueryValueEx(key, "桌面轻提醒")
            return True
        except FileNotFoundError:
            return False
        finally:
            winreg.CloseKey(key)
    except Exception:
        return False
