# Checks generated layers against the key illustration they were cut from.
#   python tools/parts-sheet/check_layers.py <key image> <folder with the layer PNGs> <output png>
# First makes sure every layer the app loads is there and has a transparent background; any
# problem there is listed and the exit status is 1 (nothing else is checked until they are fixed).
# Then stacks base, keyboard-arm, mouse-arm and head, writes key | stacked | difference side by
# side and prints how much of the picture differs. A layer that was moved, resized or redrawn
# shows up as a bright outline in the difference panel. (The app also repaints the torso over
# the far sleeve, so a thin difference where that sleeve leaves the body is expected.)
import os
import sys
from PIL import Image, ImageChops

ART = (1469, 1071)
# Stacked in this order to rebuild the key image.
LAYERS = ["base.png", "keyboard-arm.png", "mouse-arm.png", "head.png"]
# Loaded by the app as well (eyes and mouth are cut from it), but not part of the stack.
REQUIRED = LAYERS + ["face-expression.png"]
# The closed eyes and the open mouth may come as two heads instead of face-expression.png.
SEPARATE = ["eyes-closed.png", "mouth-open.png"]


def problems_with(folder):
    problems = []
    required = REQUIRED
    if all(os.path.exists(os.path.join(folder, name)) for name in SEPARATE):
        required = LAYERS + SEPARATE
    for name in required:
        path = os.path.join(folder, name)
        if not os.path.exists(path):
            problems.append("missing: " + name)
            continue
        layer = Image.open(path)
        if "A" not in layer.getbands():
            problems.append("no alpha channel (background not transparent): " + name)
            continue
        low, high = layer.getchannel("A").getextrema()
        if low > 0:
            problems.append("no transparent pixels (background not removed): " + name)
        elif high == 0:
            problems.append("completely transparent: " + name)
    return problems


def main():
    key_path, folder, out = sys.argv[1], sys.argv[2], sys.argv[3]
    problems = problems_with(folder)
    if problems:
        for problem in problems:
            print(problem)
        print("%d problem(s) with the layers; fix them and run this again." % len(problems))
        sys.exit(1)
    key = Image.open(key_path).convert("RGBA").resize(ART, Image.LANCZOS)
    stacked = Image.new("RGBA", ART, (0, 0, 0, 0))
    for name in LAYERS + [n for n in REQUIRED[len(LAYERS):] + SEPARATE if os.path.exists(os.path.join(folder, n))]:
        layer = Image.open(os.path.join(folder, name)).convert("RGBA")
        ratio = layer.width / layer.height
        if abs(ratio - ART[0] / ART[1]) > .01:
            print("WARNING %s is %dx%d: its aspect ratio differs from the artboard" % (name, layer.width, layer.height))
        if name not in LAYERS:
            continue
        layer = layer.resize(ART, Image.LANCZOS)
        covered = sum(layer.getchannel("A").histogram()[129:]) / (ART[0] * ART[1])
        print("%-18s covers %4.1f%% of the canvas" % (name, covered * 100))
        stacked.alpha_composite(layer)
    grey = (128, 128, 128, 255)
    a = Image.new("RGBA", ART, grey); a.alpha_composite(key)
    b = Image.new("RGBA", ART, grey); b.alpha_composite(stacked)
    difference = ImageChops.difference(a.convert("RGB"), b.convert("RGB")).convert("L")
    changed = sum(difference.histogram()[41:]) / (ART[0] * ART[1])
    print("pixels that differ clearly from the key image: %.2f%%" % (changed * 100))
    sheet = Image.new("RGB", (ART[0] * 3, ART[1]))
    sheet.paste(a.convert("RGB"), (0, 0))
    sheet.paste(b.convert("RGB"), (ART[0], 0))
    sheet.paste(difference.point(lambda v: min(255, v * 3)).convert("RGB"), (ART[0] * 2, 0))
    sheet.save(out)
    print(out)


if __name__ == "__main__":
    main()
