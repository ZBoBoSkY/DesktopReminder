# -*- coding: utf-8 -*-
"""设置窗口：图形化修改所有可配置参数。"""
from PySide6.QtCore import Qt, Signal
from PySide6.QtGui import QFont
from PySide6.QtWidgets import (QCheckBox, QDialog, QDialogButtonBox,
                               QFormLayout, QHBoxLayout, QLabel, QSlider,
                               QSpinBox, QVBoxLayout, QWidget)

QSS = """
QDialog { background: #1a1e28; color: #ebf0f8; }
QLabel { color: #cfe0f5; font-size: 12px; }
QSpinBox {
    background: rgba(255,255,255,26); border:1px solid rgba(255,255,255,40);
    border-radius:6px; padding:3px 6px; color:#eef3fb;
}
QCheckBox { color: #eef3fb; spacing: 8px; }
QCheckBox::indicator { width:16px; height:16px; border-radius:4px;
    border:1px solid rgba(255,255,255,40); background:rgba(255,255,255,18); }
QCheckBox::indicator:checked { background:#22d3ee; border:1px solid #22d3ee; }
QSlider::groove:horizontal { height:4px; background:rgba(255,255,255,26); border-radius:2px; }
QSlider::sub-page:horizontal { background:#22d3ee; border-radius:2px; }
QSlider::handle:horizontal { width:14px; height:14px; margin:-5px 0;
    background:#fff; border-radius:7px; border:2px solid #22d3ee; }
QDialogButtonBox QPushButton {
    padding:6px 18px; border-radius:8px; border:none;
}
QDialogButtonBox QPushButton[dialogButtonRole="acceptRole"] {
    background:#22d3ee; color:#0b0f16; font-weight:600;
}
QDialogButtonBox QPushButton[dialogButtonRole="acceptRole"]:hover { background:#67e8f9; }
"""


class SettingsDialog(QDialog):
    settingsChanged = Signal()   # 设置变更后通知控制器

    def __init__(self, settings, parent=None):
        super().__init__(parent)
        self.settings = settings
        self.setWindowTitle("设置")
        self.setWindowFlags(self.windowFlags() | Qt.WindowStaysOnTopHint)
        self.setFixedWidth(400)
        self.setStyleSheet(QSS)
        self.setFont(QFont("Microsoft YaHei UI", 9))
        self._build_ui()

    def _build_ui(self):
        lay = QVBoxLayout(self)
        lay.setContentsMargins(18, 16, 18, 16)
        lay.setSpacing(14)

        form = QFormLayout()
        form.setSpacing(10)
        form.setLabelAlignment(Qt.AlignLeft | Qt.AlignVCenter)

        # 周期性提醒间隔
        self._periodic = QSpinBox()
        self._periodic.setRange(1, 120)
        self._periodic.setSuffix(" 分钟")
        self._periodic.setValue(int(self.settings["periodic_reminder_minutes"]))
        self._periodic.valueChanged.connect(self._on_change)
        form.addRow("周期性提醒间隔：", self._periodic)

        # 升级提醒间隔
        self._escalation = QSpinBox()
        self._escalation.setRange(1, 120)
        self._escalation.setSuffix(" 分钟")
        self._escalation.setValue(int(self.settings["escalation_interval_minutes"]))
        self._escalation.valueChanged.connect(self._on_change)
        form.addRow("超时升级提醒间隔：", self._escalation)

        # 半透明透明度
        opacity_row = QHBoxLayout()
        opacity_row.setSpacing(8)
        self._opacity_slider = QSlider(Qt.Horizontal)
        self._opacity_slider.setRange(10, 90)
        self._opacity_slider.setValue(int(self.settings["opacity_percent"]))
        self._opacity_label = QLabel(f"{self._opacity_slider.value()}%")
        self._opacity_label.setFixedWidth(40)
        self._opacity_slider.valueChanged.connect(
            lambda v: self._opacity_label.setText(f"{v}%"))
        self._opacity_slider.valueChanged.connect(self._on_change)
        opacity_row.addWidget(self._opacity_slider)
        opacity_row.addWidget(self._opacity_label)
        form.addRow("半透明透明度：", opacity_row)

        # 闲置转半透明时间
        self._idle_dim = QSpinBox()
        self._idle_dim.setRange(10, 600)
        self._idle_dim.setSuffix(" 秒")
        self._idle_dim.setValue(int(self.settings["idle_to_dim_seconds"]))
        self._idle_dim.valueChanged.connect(self._on_change)
        form.addRow("闲置转半透明：", self._idle_dim)

        # 半透明转图标时间
        self._dim_icon = QSpinBox()
        self._dim_icon.setRange(10, 600)
        self._dim_icon.setSuffix(" 秒")
        self._dim_icon.setValue(int(self.settings["dim_to_icon_seconds"]))
        self._dim_icon.valueChanged.connect(self._on_change)
        form.addRow("半透明转图标：", self._dim_icon)

        lay.addLayout(form)

        # 开关
        self._sound = QCheckBox("提醒时播放提示音")
        self._sound.setChecked(bool(self.settings["sound_enabled"]))
        self._sound.toggled.connect(self._on_change)
        lay.addWidget(self._sound)

        self._autostart = QCheckBox("开机自动启动")
        self._autostart.setChecked(bool(self.settings["autostart_enabled"]))
        self._autostart.toggled.connect(self._on_change)
        lay.addWidget(self._autostart)

        self._remind_no_tasks = QCheckBox("没有任务时也周期性提醒")
        self._remind_no_tasks.setChecked(bool(self.settings["remind_when_no_tasks"]))
        self._remind_no_tasks.toggled.connect(self._on_change)
        lay.addWidget(self._remind_no_tasks)

        lay.addStretch(1)

        # 按钮
        btn_box = QDialogButtonBox(QDialogButtonBox.Ok)
        btn_box.button(QDialogButtonBox.Ok).setText("完成")
        btn_box.accepted.connect(self.accept)
        lay.addWidget(btn_box)

    def _on_change(self, *_):
        """任何设置变更时即时写入配置并保存。"""
        self.settings["periodic_reminder_minutes"] = self._periodic.value()
        self.settings["escalation_interval_minutes"] = self._escalation.value()
        self.settings["opacity_percent"] = self._opacity_slider.value()
        self.settings["idle_to_dim_seconds"] = self._idle_dim.value()
        self.settings["dim_to_icon_seconds"] = self._dim_icon.value()
        self.settings["sound_enabled"] = self._sound.isChecked()
        self.settings["autostart_enabled"] = self._autostart.isChecked()
        self.settings["remind_when_no_tasks"] = self._remind_no_tasks.isChecked()
        self.settings.save()
        self.settingsChanged.emit()
