import csv
import sys
from PIL import Image, ImageDraw

# plot.py trace.csv out.png events t0 t1
rows = list(csv.DictReader(open(sys.argv[1])))
events = [float(e.split(':')[0]) for e in sys.argv[3].split(';')[1:]]
t0, t1 = float(sys.argv[4]), float(sys.argv[5])
W, H = 1500, 700
im = Image.new('RGB', (W, H), (255, 255, 255))
d = ImageDraw.Draw(im)
series = [('lift', (220, 40, 40), 1), ('x', (40, 90, 220), 1), ('y', (40, 170, 90), 1), ('flick', (200, 130, 0), 3), ('nod', (150, 60, 200), 20)]
lo, hi = -130, 130


def px(t):
    return 40 + (t - t0) / (t1 - t0) * (W - 60)


def py(v):
    return H - 30 - (v - lo) / (hi - lo) * (H - 60)


for v in range(lo, hi + 1, 10):
    d.line((40, py(v), W - 20, py(v)), fill=(225, 225, 225) if v else (120, 120, 120))
    if v % 20 == 0:
        d.text((5, py(v) - 6), str(v), fill=(0, 0, 0))
t = t0
while t <= t1 + 1e-9:
    d.line((px(t), 30, px(t), H - 30), fill=(235, 235, 235))
    d.text((px(t) - 8, H - 22), '%.1f' % t, fill=(0, 0, 0))
    t += 0.1 if t1 - t0 <= 2.5 else 0.5
for e in events:
    if t0 <= e <= t1:
        d.line((px(e), 30, px(e), H - 30), fill=(0, 0, 0))
for i, (name, color, scale) in enumerate(series):
    pts = [(px(float(r['t'])), py(float(r[name]) * scale)) for r in rows if t0 <= float(r['t']) <= t1]
    d.line(pts, fill=color, width=2)
    d.text((60 + i * 110, 8), '%s x%d' % (name, scale), fill=color)
im.save(sys.argv[2])
