# -*- coding: utf-8 -*-
"""配置持久化：任务与偏好存到 %APPDATA%\\DesktopReminder\\config.json。"""
import json
import os

APP_NAME = "桌面轻提醒"
APP_KEY = "DesktopReminder"


def app_data_dir():
    base = os.environ.get("APPDATA") or os.path.expanduser("~")
    d = os.path.join(base, APP_KEY)
    try:
        os.makedirs(d, exist_ok=True)
    except Exception:
        pass
    return d


CONFIG_PATH = os.path.join(app_data_dir(), "config.json")

DEFAULTS = {
    # 提醒相关
    "periodic_reminder_minutes": 5,     # 图标态周期性自动展开半透明的间隔
    "escalation_interval_minutes": 10,   # 已完成未确认任务的升级提醒间隔
    "remind_when_no_tasks": False,       # 没有任务时是否也周期性展开提醒
    # 状态切换时间
    "idle_to_dim_seconds": 60,           # 闲置多久后变为半透明
    "dim_to_icon_seconds": 30,           # 半透明后多久再收为图标
    # 显示
    "opacity_percent": 35,                # 弱化态透明度（0=完全透明，100=不透明）
    # 功能开关
    "sound_enabled": True,                # 提醒时是否发声
    "autostart_enabled": False,           # 开机自启
    # 位置记忆
    "panel_pos": None,
    "icon_pos": None,
    # 任务数据
    "tasks": [],
}


class Settings:
    def __init__(self, path=CONFIG_PATH):
        self.path = path
        self.data = dict(DEFAULTS)
        self.load()

    def load(self):
        try:
            with open(self.path, "r", encoding="utf-8") as f:
                loaded = json.load(f)
            for k in DEFAULTS:
                if k in loaded:
                    self.data[k] = loaded[k]
        except Exception:
            pass

    def save(self):
        try:
            with open(self.path, "w", encoding="utf-8") as f:
                json.dump(self.data, f, ensure_ascii=False, indent=2)
        except Exception:
            pass

    def __getitem__(self, k):
        return self.data[k]

    def __setitem__(self, k, v):
        self.data[k] = v
