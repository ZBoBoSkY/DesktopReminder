# -*- coding: utf-8 -*-
"""全局配色 / 主题常量（深色玻璃风 v2）。"""
from PySide6.QtGui import QColor

# 背景
BG          = QColor(15, 23, 42, 160)    # 深蓝灰面板底色（半透明）
BG_CARD     = QColor(30, 41, 59, 140)    # 任务卡片底色
BG_HOVER    = QColor(51, 65, 85, 160)    # hover 底色

# 文字
TEXT        = QColor(241, 245, 249, 240)  # 主文字
TEXT_DIM    = QColor(148, 163, 184, 220)  # 次要文字
TEXT_BRIGHT = QColor(248, 250, 252, 255)  # 高亮文字（时间等）

# 强调色（渐变：青 -> 蓝）
ACCENT      = QColor(34, 211, 238)        # 青色（渐变起点）
ACCENT_END  = QColor(59, 130, 246)        # 蓝色（渐变终点）
ACCENT_SOFT = QColor(34, 211, 238, 40)    # 强调色淡背景

# 状态色
RUNNING     = QColor(34, 211, 238)        # 进行中（青）
SOON        = QColor(251, 146, 60)        # 即将到期（橙，<1分钟）
EXPIRED     = QColor(248, 113, 113)       # 已过期（红）
DONE        = QColor(251, 191, 36)        # 已完成待检查（琥珀）
SUCCESS     = QColor(52, 211, 153)        # 成功/确认（绿）

# 兼容旧名称
WARN = DONE
DANGER = EXPIRED

# 边框
BORDER      = QColor(148, 163, 184, 50)   # 边框
BORDER_LIGHT= QColor(255, 255, 255, 60)   # 玻璃高光边框
