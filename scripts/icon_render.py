# -*- coding: utf-8 -*-
"""直接渲染悬浮图标控件，验证绘制效果（不受屏幕 DPI 坐标影响）。"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from PySide6.QtWidgets import QApplication  # noqa: E402

from app.float_icon import FloatIcon  # noqa: E402

app = QApplication([])
w = FloatIcon()
w.set_badge(2)
w.show()
app.processEvents()
out = sys.argv[1] if len(sys.argv) > 1 else "icon_render.png"
w.grab().save(out)
print("SAVED_ICON_RENDER", out)
w.close()
