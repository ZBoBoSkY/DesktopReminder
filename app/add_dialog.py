# -*- coding: utf-8 -*-
"""添加任务二级弹窗：自动识别当前软件名 + 备注 + 预设/自定义时长。"""
from PySide6.QtCore import Qt, Signal
from PySide6.QtGui import QFont
from PySide6.QtWidgets import (QDialog, QDialogButtonBox, QHBoxLayout,
                               QLabel, QLineEdit, QPushButton, QSpinBox,
                               QVBoxLayout, QWidget)

QSS = """
QDialog { background: #1a1e28; color: #ebf0f8; }
QLabel { color: #cfe0f5; font-size: 12px; }
QLineEdit {
    background: rgba(255,255,255,26); border:1px solid rgba(255,255,255,40);
    border-radius:8px; padding:6px 10px; color:#eef3fb;
    selection-background-color:#22d3ee; selection-color:#0b0f16;
}
QLineEdit:focus { border:1px solid #22d3ee; }
QPushButton#durBtn {
    background: rgba(255,255,255,18); color:#cfe0f5; border:1px solid rgba(255,255,255,30);
    border-radius:7px; padding:5px 0;
}
QPushButton#durBtn:hover { background: rgba(255,255,255,32); }
QPushButton#durBtn[selected="true"] { background: rgba(34,211,238,70); border:1px solid #22d3ee; color:#fff; }
QSpinBox {
    background: rgba(255,255,255,26); border:1px solid rgba(255,255,255,40);
    border-radius:6px; padding:3px 6px; color:#eef3fb;
}
QDialogButtonBox QPushButton {
    padding:6px 18px; border-radius:8px; border:none;
}
QDialogButtonBox QPushButton[dialogButtonRole="acceptRole"] {
    background:#22d3ee; color:#0b0f16; font-weight:600;
}
QDialogButtonBox QPushButton[dialogButtonRole="acceptRole"]:hover { background:#67e8f9; }
QDialogButtonBox QPushButton[dialogButtonRole="rejectRole"] {
    background: rgba(255,255,255,18); color:#cfe0f5;
}
QDialogButtonBox QPushButton[dialogButtonRole="rejectRole"]:hover { background: rgba(255,255,255,32); }
"""


class AddTaskDialog(QDialog):
    """添加任务对话框。

    参数 current_info: 当前前台窗口信息，格式 "进程名 · 窗口标题"，
    会自动填入任务名称输入框。
    """
    taskAdded = Signal(str, int)   # title, duration_seconds

    PRESETS = [("5分", 300), ("10分", 600), ("15分", 900),
               ("30分", 1800), ("1时", 3600)]

    def __init__(self, current_info="", parent=None):
        super().__init__(parent)
        self.setWindowTitle("添加任务")
        self.setWindowFlags(self.windowFlags() | Qt.WindowStaysOnTopHint)
        self.setFixedWidth(380)
        self.setStyleSheet(QSS)
        self.setFont(QFont("Microsoft YaHei UI", 9))
        self._duration = 600
        self._build_ui(current_info)

    def _build_ui(self, current_info):
        lay = QVBoxLayout(self)
        lay.setContentsMargins(18, 16, 18, 16)
        lay.setSpacing(12)

        # 任务名称
        lay.addWidget(QLabel("任务名称（自动识别当前软件，可追加备注）："))
        self._title_input = QLineEdit()
        self._title_input.setPlaceholderText("如：chrome · 查资料")
        if current_info:
            self._title_input.setText(current_info)
            self._title_input.selectAll()
        self._title_input.returnPressed.connect(self._on_accept)
        lay.addWidget(self._title_input)

        # 预计时长
        lay.addWidget(QLabel("预计时长："))
        dur_row = QHBoxLayout()
        dur_row.setSpacing(6)
        self._dur_btns = {}
        for label, sec in self.PRESETS:
            b = QPushButton(label)
            b.setObjectName("durBtn")
            b.setProperty("selected", "true" if sec == self._duration else "false")
            b.clicked.connect(lambda _=False, s=sec: self._select_duration(s))
            dur_row.addWidget(b)
            self._dur_btns[sec] = b
        lay.addLayout(dur_row)

        # 自定义时长
        custom_row = QHBoxLayout()
        custom_row.setSpacing(8)
        custom_row.addWidget(QLabel("自定义："))
        self._custom_spin = QSpinBox()
        self._custom_spin.setRange(1, 1440)
        self._custom_spin.setValue(10)
        self._custom_spin.setSuffix(" 分钟")
        self._custom_spin.valueChanged.connect(self._on_custom_changed)
        custom_row.addWidget(self._custom_spin)
        custom_row.addStretch(1)
        lay.addLayout(custom_row)

        # 按钮
        btn_box = QDialogButtonBox(QDialogButtonBox.Ok | QDialogButtonBox.Cancel)
        btn_box.button(QDialogButtonBox.Ok).setText("添加")
        btn_box.button(QDialogButtonBox.Cancel).setText("取消")
        btn_box.accepted.connect(self._on_accept)
        btn_box.rejected.connect(self.reject)
        lay.addWidget(btn_box)

    def _select_duration(self, sec):
        self._duration = sec
        for s, b in self._dur_btns.items():
            b.setProperty("selected", "true" if s == sec else "false")
            b.style().unpolish(b)
            b.style().polish(b)

    def _on_custom_changed(self, val):
        # 用户调整自定义时，自动选中自定义时长
        self._duration = val * 60
        for s, b in self._dur_btns.items():
            b.setProperty("selected", "false")
            b.style().unpolish(b)
            b.style().polish(b)

    def _on_accept(self):
        title = self._title_input.text().strip()
        if not title:
            self._title_input.setFocus()
            return
        self.taskAdded.emit(title, self._duration)
        self.accept()
