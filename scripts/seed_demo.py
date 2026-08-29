# -*- coding: utf-8 -*-
"""生成一个带示例任务的临时配置，用于可视化验证。"""
import json
import os
import sys
from datetime import datetime, timedelta

path = sys.argv[1] if len(sys.argv) > 1 else "demo_config.json"
now = datetime.now()


def iso(dt):
    return dt.isoformat()


config = {
    "auto_expand_minutes": 15,
    "escalation_interval_minutes": 10,
    "sound_enabled": True,
    "panel_pos": None,
    "icon_pos": None,
    "tasks": [
        {
            "id": "demo1",
            "title": "下载驱动包（预计15分钟）",
            "duration_seconds": 900,
            "created_at": iso(now - timedelta(seconds=300)),
            "status": "running",
            "completion_at": None,
            "last_notified": None,
        },
        {
            "id": "demo2",
            "title": "智能体跑数据（已超时）",
            "duration_seconds": 600,
            "created_at": iso(now - timedelta(seconds=720)),
            "status": "done",
            "completion_at": iso(now - timedelta(seconds=120)),
            "last_notified": now.isoformat(),
        },
        {
            "id": "demo3",
            "title": "文档转PDF并上传",
            "duration_seconds": 180,
            "created_at": iso(now - timedelta(seconds=60)),
            "status": "running",
            "completion_at": None,
            "last_notified": None,
        },
    ],
}
with open(path, "w", encoding="utf-8") as f:
    json.dump(config, f, ensure_ascii=False, indent=2)
print("SEEDED", path)
