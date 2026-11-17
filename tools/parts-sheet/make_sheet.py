# Builds the parts reference sheet handed to an image generator when a new character is made.
#   python tools/parts-sheet/make_sheet.py <project folder>
# Writes assets/parts-reference/parts-sheet.png (all panels) and one full-size image per panel.
# Transparent areas are filled with one flat colour so the generator can see where each layer is empty.
#
# The panels show the split a NEW character should be generated in. It differs from the files this
# character shipped with in one way: the neck belongs to the body, so here it is moved from head.png
# onto the base, along the same outline app/CharacterView.cs uses to do that at run time.
import os
import sys
from PIL import Image, ImageChops, ImageDraw, ImageFont

ART = (1469, 1071)
EMPTY = (169, 193, 217, 255)     # flat blue-grey standing in for transparency
INK = (33, 24, 19, 255)
PAPER = (255, 255, 255, 255)
# collarForeground in CharacterView.cs: the torso repainted over the far sleeve.
COLLAR = [(470, 525), (640, 525), (654, 548), (671, 556), (684, 564), (696, 572), (705, 580), (713, 588),
          (720, 596), (727, 604), (732, 612), (737, 620), (742, 628), (747, 636), (751, 644), (755, 652),
          (758, 660), (761, 668), (764, 676), (767, 684), (770, 692), (772, 700), (774, 708), (776, 716),
          (777, 724), (779, 732), (780, 740), (783, 748), (787, 756), (790, 764), (792, 772), (797, 800),
          (700, 806), (561, 792), (481, 661)]
# neckRegion in CharacterView.cs: the neck stub that head.png carries under the jaw.
NECK = [(553, 549), (565, 547.5), (575, 546), (585, 543.5), (589, 541.5), (602, 543), (604, 550), (612, 553),
        (622, 556), (622, 560), (620, 566), (617, 574), (614, 583), (605, 588), (595, 588), (583, 584),
        (572, 576), (561, 566), (556, 555)]
PANELS = [
    ("1-composite", "1  COMPOSITE", "all layers stacked"),
    ("2-base", "2  BASE", "torso WITH NECK + desk + keyboard"),
    ("3-keyboard-arm", "3  KEYBOARD ARM", "far sleeve + hand, one piece"),
    ("4-mouse-arm", "4  MOUSE ARM", "near sleeve + hand + mouse, one piece"),
    ("5-head", "5  HEAD", "head and hair, NO NECK"),
    ("6-face-expression", "6  FACE EXPRESSION", "eyes closed + mouth open"),
]
ORDER = ["STACKING ORDER (back to front)", "2 base", "3 keyboard arm",
         "torso of 2 again, over the far sleeve", "4 mouse arm", "5 head", "",
         "Blue-grey = transparent.", "Every panel is the SAME full canvas:",
         "a part stays where it is in panel 1."]


def font(size, bold=False):
    for name in (("arialbd.ttf", "segoeuib.ttf") if bold else ("arial.ttf", "segoeui.ttf")):
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            pass
    return ImageFont.load_default()


def layer(root, name):
    return Image.open(os.path.join(root, "assets", name)).convert("RGBA").resize(ART, Image.LANCZOS)


def clipped(image, polygon, inside):
    # The image with only the pixels inside (or outside) the polygon, edges anti-aliased.
    scale = 4
    mask = Image.new("L", (ART[0] * scale, ART[1] * scale), 0)
    ImageDraw.Draw(mask).polygon([(x * scale, y * scale) for x, y in polygon], fill=255)
    mask = mask.resize(ART, Image.LANCZOS)
    if not inside:
        mask = mask.point(lambda v: 255 - v)
    result = image.copy()
    result.putalpha(ImageChops.multiply(image.getchannel("A"), mask))
    return result


def main():
    root = sys.argv[1]
    out = os.path.join(root, "assets", "parts-reference")
    os.makedirs(out, exist_ok=True)
    base, keyboard, mouse = layer(root, "base.png"), layer(root, "keyboard-arm.png"), layer(root, "mouse-arm.png")
    head, face = layer(root, "head.png"), layer(root, "face-expression.png")
    # The app's stacking order: base, far arm, torso again over the far sleeve, near arm, head.
    composite = base.copy()
    composite.alpha_composite(keyboard)
    composite.alpha_composite(clipped(base, COLLAR, True))
    composite.alpha_composite(mouse)
    composite.alpha_composite(head)

    # Neck: off the head, onto the body.
    body = base.copy()
    body.alpha_composite(clipped(head, NECK, True))
    head_only, face_only = clipped(head, NECK, False), clipped(face, NECK, False)
    images = [composite, body, keyboard, mouse, head_only, face_only]

    for old in os.listdir(out):
        if old.startswith("panel-"):
            os.remove(os.path.join(out, old))
    flat = []
    for (name, _, _), image in zip(PANELS, images):
        panel = Image.new("RGBA", ART, EMPTY)
        panel.alpha_composite(image)
        panel.convert("RGB").save(os.path.join(out, "panel-" + name + ".png"))
        flat.append(panel)

    # 4 x 2 grid; every panel is the full artboard at the same scale. The last cell holds the notes.
    columns, margin, title, label = 4, 30, 110, 70
    width = 3200
    panel_w = (width - margin * (columns + 1)) // columns
    panel_h = round(panel_w * ART[1] / ART[0])
    height = title + 2 * (panel_h + label) + margin * 2
    sheet = Image.new("RGBA", (width, height), PAPER)
    draw = ImageDraw.Draw(sheet)
    draw.text((margin, 22), "PARTS REFERENCE SHEET", font=font(60, True), fill=INK)
    draw.text((margin + 830, 42), "Panel 1 = finished picture. Panels 2-6 = the layers it is cut into.",
              font=font(32), fill=INK)
    heading_font, note_font = font(36, True), font(24)
    for i in range(columns * 2):
        x = margin + (i % columns) * (panel_w + margin)
        y = title + (i // columns) * (panel_h + label + margin)
        if i >= len(PANELS):
            for row, line in enumerate(ORDER if i == len(PANELS) else []):
                draw.text((x + 10, y + label + 20 + row * 44), line, font=font(32, row == 0), fill=INK)
            continue
        _, heading, note = PANELS[i]
        draw.text((x, y), heading, font=heading_font, fill=INK)
        draw.text((x + 4, y + 40), note, font=note_font, fill=INK)
        sheet.paste(flat[i].resize((panel_w, panel_h), Image.LANCZOS), (x, y + label))
        draw.rectangle([x - 2, y + label - 2, x + panel_w + 1, y + label + panel_h + 1], outline=INK, width=3)
    sheet.convert("RGB").save(os.path.join(out, "parts-sheet.png"))
    print(os.path.join(out, "parts-sheet.png"), sheet.size)


if __name__ == "__main__":
    main()
