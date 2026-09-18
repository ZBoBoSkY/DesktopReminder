# -*- coding: utf-8 -*-
"""生成 app.ico：从 256px 高清画起，高质量缩小到各尺寸（小图标不放大，无毛边）。"""
from PIL import Image, ImageDraw

ACCENT = (0x5E, 0x5C, 0xE6, 255)   # 深靛蓝
WHITE = (245, 245, 245, 255)


def render(s: int) -> Image.Image:
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.ellipse([0, 0, s - 1, s - 1], fill=ACCENT)          # 实心圆占满
    d.ellipse([s * 0.28, s * 0.28, s * 0.72, s * 0.72],
              outline=WHITE, width=max(1, int(s * 0.06)))  # 时钟圈
    cx, cy = s / 2, s / 2
    w = max(1, int(s * 0.05))
    d.line([cx, cy, cx, cy - s * 0.17], fill=WHITE, width=w)
    d.line([cx, cy, cx + s * 0.13, cy + s * 0.03], fill=WHITE, width=w)
    return img


# 只画 256px 高清母版，再高质量缩小到各尺寸
master = render(256)
sizes = [16, 24, 32, 48, 64, 128, 256]
out = r"C:\Users\zengbo\DoubaoWork\chats\2026-08-29\new-chat-1\DesktopReminder_v3\src\DesktopReminder.App\app.ico"
master.save(out, format="ICO", sizes=[(s, s) for s in sizes])
print("已生成:", out, "(256px 高清母版高质量缩小)")
