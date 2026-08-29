# -*- coding: utf-8 -*-
"""生成 icon.ico（多尺寸内嵌 PNG，供 exe / 快捷方式使用）。"""
import os
import struct
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from PySide6.QtCore import QBuffer, QIODevice  # noqa: E402
from PySide6.QtGui import QGuiApplication  # noqa: E402

from app.tray import make_app_icon  # noqa: E402

OUT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                   "icon.ico")
app = QGuiApplication([])
sizes = [16, 24, 32, 48, 64, 128, 256]
entries = []
image_data = b""
offset = 6 + 16 * len(sizes)

for s in sizes:
    pm = make_app_icon(s).pixmap(s, s)
    buf = QBuffer()
    buf.open(QIODevice.WriteOnly)
    ok = pm.save(buf, "PNG")
    data = bytes(buf.data())
    buf.close()
    if not ok or not data:
        continue
    w = 0 if s == 256 else s
    entries.append(struct.pack(
        "<BBBBHHII", w, w, 0, 0, 1, 32, len(data), offset))
    image_data += data
    offset += len(data)

with open(OUT, "wb") as f:
    f.write(struct.pack("<HHH", 0, 1, len(entries)))
    f.write(b"".join(entries))
    f.write(image_data)
print("ICON_WRITTEN", OUT, len(entries), "sizes")
