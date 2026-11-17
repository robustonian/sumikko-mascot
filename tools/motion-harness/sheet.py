import os
import sys
from PIL import Image, ImageDraw

out = sys.argv[1]
cols = int(sys.argv[2])
files = sys.argv[3:]
ims = [Image.open(f).convert('RGB') for f in files]
w, h = ims[0].size
rows = (len(ims) + cols - 1) // cols
sheet = Image.new('RGB', (cols * w, rows * h), (20, 20, 20))
d = ImageDraw.Draw(sheet)
for i, (im, f) in enumerate(zip(ims, files)):
    x, y = (i % cols) * w, (i // cols) * h
    sheet.paste(im, (x, y))
    name = os.path.splitext(os.path.basename(f))[0]
    d.rectangle((x, y, x + 12 + 7 * len(name), y + 18), fill=(0, 0, 0))
    d.text((x + 5, y + 3), name, fill=(255, 255, 0))
sheet.save(out)
