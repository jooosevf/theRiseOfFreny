"""Cuts the Golem's idle pose into body parts for the skeletal-animation prototype (GolemRig.cs).

The Golem's sheets are 40x40 pixel art drawn 4x (160x160 px at 100 PPU, one art pixel = 4 texels
= 0.04 units). Each part is cut from the first idle frame at the art resolution, placed on a 49x49
canvas with its joint (pivot) at the canvas centre, rotated with NEAREST at every ANGLE_STEP
degrees from -90 to +90, and only then scaled back up 4x. Rotating at the art resolution is what
keeps the parts pixel art: the blocks stay axis-aligned and the same size whatever the bone's
angle. Rotating the 160 px sprite in Unity instead gives rotated, uneven blocks (the "free" rig
variant exists to show the difference).

Output (Resources, loaded by GolemRig at runtime, sliced in code, no sheet metadata):
  Assets/Sprites/Characters/Mobs/Golem/Resources/GolemRig/<part>.png   13 frames of 196x196 px
  Assets/Sprites/Characters/Mobs/Golem/Resources/GolemRig/body.png     the whole idle frame (mesh variant)
It also prints the pivots in rig units, which GolemRig.cs copies.
"""
import os
from PIL import Image

ROOT = os.path.join(os.path.dirname(__file__), "..", "..")
SRC = os.path.join(ROOT, "Assets", "Sprites", "Characters", "Mobs", "Golem", "Golem idle.png")
OUT = os.path.join(ROOT, "Assets", "Sprites", "Characters", "Mobs", "Golem", "Resources", "GolemRig")
SCALE = 4          # texels per art pixel
CANVAS = 49        # art pixels; odd so that the centre is a pixel centre
ANGLE_STEP = 15
ANGLES = list(range(-90, 91, ANGLE_STEP))
BODY = (47, 55, 80, 255)   # the Golem's main colour, used to extend the torso under the arms

# part -> (rows: list of (y0, y1, x0, x1) inclusive rectangles at art resolution, pivot pixel (x, y))
# Front = the viewer's right (the Golem faces right in the sheet, flipped by localScale to face left).
PARTS = {
    "head":     ([(9, 13, 15, 23)], (19, 14)),
    "torso":    ([(14, 23, 14, 25), (24, 29, 13, 24), (30, 32, 13, 23)], (19, 32)),
    "armBack":  ([(16, 19, 9, 14), (20, 23, 9, 13), (24, 33, 9, 12)], (12, 18)),
    "armFront": ([(16, 19, 25, 30), (20, 29, 26, 30), (30, 33, 24, 28)], (28, 18)),
    "legBack":  ([(33, 39, 15, 18)], (17, 33)),
    "legFront": ([(33, 39, 20, 23)], (22, 33)),
}
# torso pixels painted in the body colour so a swung arm never uncovers a hole in the shoulder
TORSO_FILL = [(17, 29, 12, 13), (17, 29, 26, 27)]


def art_frame():
    im = Image.open(SRC).convert("RGBA").crop((0, 0, 160, 160))
    small = im.resize((40, 40), Image.NEAREST)
    assert list(small.resize((160, 160), Image.NEAREST).getdata()) == list(im.getdata()), "not clean 4x art"
    return small


def cut(frame, rects, fills=()):
    part = Image.new("RGBA", frame.size, (0, 0, 0, 0))
    for (y0, y1, x0, x1) in rects:
        box = (x0, y0, x1 + 1, y1 + 1)
        part.paste(frame.crop(box), box)
    for (y0, y1, x0, x1) in fills:
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                if part.getpixel((x, y))[3] == 0:
                    part.putpixel((x, y), BODY)
    return part


def main():
    os.makedirs(OUT, exist_ok=True)
    frame = art_frame()
    frame.resize((160, 160), Image.NEAREST).save(os.path.join(OUT, "body.png"))
    c = CANVAS // 2
    print("// pivots in rig units (art pixel = 0.04 u), relative to the sprite centre (20, 20):")
    for name, (rects, (px, py)) in PARTS.items():
        part = cut(frame, rects, TORSO_FILL if name == "torso" else ())
        canvas = Image.new("RGBA", (CANVAS, CANVAS), (0, 0, 0, 0))
        canvas.paste(part, (c - px, c - py))
        sheet = Image.new("RGBA", (len(ANGLES) * CANVAS * SCALE, CANVAS * SCALE), (0, 0, 0, 0))
        for i, angle in enumerate(ANGLES):
            rotated = canvas.rotate(angle, resample=Image.NEAREST, center=(c + 0.5, c + 0.5))
            big = rotated.resize((CANVAS * SCALE, CANVAS * SCALE), Image.NEAREST)
            sheet.paste(big, (i * CANVAS * SCALE, 0))
        sheet.save(os.path.join(OUT, name + ".png"))
        ux, uy = (px + 0.5 - 20) * 0.04, (20 - (py + 0.5)) * 0.04
        print(f"//   {name:9s} pivot ({ux:+.2f}, {uy:+.2f})")
    print(f"// {len(ANGLES)} frames of {CANVAS * SCALE}x{CANVAS * SCALE} px per part, step {ANGLE_STEP} deg")


if __name__ == "__main__":
    main()
