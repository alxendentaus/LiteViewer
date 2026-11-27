"""绘制日志查看器应用图标，输出多尺寸 ICO + 预览 PNG。"""
from PIL import Image, ImageDraw, ImageFilter
import os

ROOT = r'C:\Users\Administrator\WorkBuddy\2026-11-26-14-28-03'
OUT_DIR = os.path.join(ROOT, 'NativeLogViewer', 'Resources')
os.makedirs(OUT_DIR, exist_ok=True)

S = 512  # 高分辨率母版

def lerp(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(len(a)))

# ---- 透明背景，无底色方块 ----
img = Image.new('RGBA', (S, S), (0, 0, 0, 0))
d = ImageDraw.Draw(img, 'RGBA')

# ---- 文档纸张：白色圆角矩形，带描边和轻微阴影 ----
doc = [112, 78, 404, 438]
shadow = Image.new('RGBA', (S, S), (0, 0, 0, 0))
sd = ImageDraw.Draw(shadow)
sd.rounded_rectangle([doc[0] + 6, doc[1] + 10, doc[2] + 6, doc[3] + 10], radius=26, fill=(0, 0, 0, 70))
shadow = shadow.filter(ImageFilter.GaussianBlur(10))
img.alpha_composite(shadow)
d = ImageDraw.Draw(img, 'RGBA')
d.rounded_rectangle(doc, radius=26, fill=(250, 252, 255, 255), outline=(96, 132, 186, 255), width=6)

# ---- 顶部标题栏（浅灰）----
d.rounded_rectangle([doc[0], doc[1], doc[2], doc[1] + 52], radius=26, fill=(232, 238, 247, 255))
d.rectangle([doc[0], doc[1] + 30, doc[2], doc[1] + 52], fill=(232, 238, 247, 255))
# 三个小圆点（窗口按钮意象）
for i, col in enumerate([(230, 110, 105), (235, 185, 80), (110, 195, 130)]):
    cx = doc[0] + 34 + i * 28
    d.ellipse([cx - 8, doc[1] + 18, cx + 8, doc[1] + 34], fill=col)

# ---- 行号列 ----
ln_x = doc[0] + 30
for i in range(6):
    y = doc[1] + 78 + i * 52
    d.rounded_rectangle([ln_x - 4, y, ln_x + 20, y + 10], radius=5, fill=(205, 214, 228, 255))

# ---- 分隔线 ----
d.rectangle([doc[0] + 60, doc[1] + 70, doc[0] + 62, doc[3] - 28], fill=(224, 230, 240, 255))

# ---- 日志行：不同长度的条 + 级别色块 ----
tx = doc[0] + 82
rows = [
    (162, (232, 96, 92, 255)),     # ERROR 红
    (132, (232, 170, 72, 255)),    # WARN 黄
    (150, (74, 150, 226, 255)),    # INFO 蓝
    (108, (128, 140, 158, 255)),   # DEBUG 灰
    (142, (96, 178, 140, 255)),    # TRACE 绿
    (120, (128, 140, 158, 255)),
]
for i, (w, col) in enumerate(rows):
    y = doc[1] + 78 + i * 52
    # 级别小方块
    d.rounded_rectangle([tx, y - 2, tx + 16, y + 14], radius=4, fill=col)
    # 文本行
    d.rounded_rectangle([tx + 26, y + 1, tx + 26 + w, y + 11], radius=5, fill=(186, 198, 214, 255))

# ---- 放大镜（搜索意象），右下叠加 ----
mx, my, mr = 348, 360, 62
# 阴影
d.ellipse([mx - mr + 5, my - mr + 7, mx + mr + 5, my + mr + 7], fill=(0, 0, 0, 55))
# 镜圈
d.ellipse([mx - mr, my - mr, mx + mr, my + mr], fill=(248, 250, 254, 255), outline=(30, 96, 170, 255), width=12)
# 镜片
d.ellipse([mx - mr + 12, my - mr + 12, mx + mr - 12, my + mr - 12], fill=(214, 232, 250, 255))
# 手柄
hd = ImageDraw.Draw(img, 'RGBA')
hd.line([mx + 42, my + 42, mx + 84, my + 84], fill=(30, 96, 170, 255), width=26)
hd.line([mx + 42, my + 42, mx + 84, my + 84], fill=(52, 126, 204, 255), width=14)
# 镜面高光
d = ImageDraw.Draw(img, 'RGBA')
d.arc([mx - mr + 18, my - mr + 18, mx + mr - 18, my + mr - 18], start=200, end=280, fill=(255, 255, 255, 220), width=8)

# 保存母版预览（浅灰棋盘底，便于看清透明区域）
preview = Image.new('RGB', (S, S), (240, 242, 246))
pdraw = ImageDraw.Draw(preview)
cell = 32
for yy in range(0, S, cell):
    for xx in range(0, S, cell):
        if (xx // cell + yy // cell) % 2 == 0:
            pdraw.rectangle([xx, yy, xx + cell, yy + cell], fill=(226, 230, 236))
preview.paste(img, (0, 0), img)
preview.save(os.path.join(OUT_DIR, 'icon-preview.png'))

# ---- 生成多尺寸 ICO ----
sizes = [16, 24, 32, 48, 64, 96, 128, 256]
img.save(os.path.join(OUT_DIR, 'app.ico'), format='ICO',
         sizes=[(s, s) for s in sizes])
print('图标已生成:', os.path.join(OUT_DIR, 'app.ico'))
print('预览图:', os.path.join(OUT_DIR, 'icon-preview.png'))
print('ICO 大小:', os.path.getsize(os.path.join(OUT_DIR, 'app.ico')), 'bytes')
