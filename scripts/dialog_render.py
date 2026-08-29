# -*- coding: utf-8 -*-
"""渲染添加对话框和设置对话框，验证界面效果。"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from PySide6.QtWidgets import QApplication  # noqa: E402

from app.add_dialog import AddTaskDialog  # noqa: E402
from app.settings import Settings  # noqa: E402
from app.settings_dialog import SettingsDialog  # noqa: E402

app = QApplication([])

# 添加对话框
dlg1 = AddTaskDialog(current_info="chrome · 豆包工作台")
dlg1.show()
app.processEvents()
dlg1.grab().save("add_dialog_shot.png")
print("ADD_DIALOG_SAVED")

# 设置对话框
settings = Settings()
dlg2 = SettingsDialog(settings)
dlg2.show()
app.processEvents()
dlg2.grab().save("settings_dialog_shot.png")
print("SETTINGS_DIALOG_SAVED")

dlg1.close()
dlg2.close()
