"""Cuts Freny's idle pose into body parts for the skeletal-animation prototype (FrenyRig.cs),
the way golem_rig.py does for the Golem.

Freny's sheets are 50x50 pixel art drawn 4x (200x200 px cells at 100 PPU: one art pixel = 4 texels
= 0.04 units). The first idle frame is cut into: the helmet with the neck, the torso (shoulders to
the belt), an upper arm and a forearm with the fist for each arm, and the two legs. The hidden
coat under the arms is painted in, so a swung arm never uncovers a hole. Each part is placed on a
27x27 canvas with its joint at the centre, rotated with NEAREST every ANGLE_STEP degrees over the
full turn (-180..180: the sword arm goes over his head) and only then scaled back up 4x, so the
blocks stay square and aligned at any bone angle. The sword is not cut: FrenyRig holds the blades
freny_poses.py already draws at every angle ("Freny blade_<deg>").

Output (Resources, loaded by FrenyRig at runtime and sliced in code, no sheet metadata):
  Assets/Sprites/Characters/Player/Resources/FrenyRig/<part>.png   25 frames of 108x108 px
It also prints the joints in rig units, which FrenyRig.cs copies.
"""
import os
from PIL import Image

ROOT = os.path.join(os.path.dirname(__file__), "..", "..")
SRC = os.path.join(ROOT, "Assets", "Sprites", "Characters", "Player", "Player idle.png")
OUT = os.path.join(ROOT, "Assets", "Sprites", "Characters", "Player", "Resources", "FrenyRig")
SCALE = 4          # texels per art pixel
ART = 50           # art pixels per cell side
CANVAS = 27        # art pixels; odd so that the centre is a pixel centre
ANGLE_STEP = 15
ANGLES = list(range(-180, 181, ANGLE_STEP))
COAT = (129, 111, 109, 255)       # the coat's main colour, painted under the arms

# part -> (rectangles (y0, y1, x0, x1) inclusive at art resolution, joint pixel (x, y))
# Front = the viewer's right: Freny faces right in the sheet (flipX turns him), the sword arm is
# the front one, as in freny_poses.py (its shoulder is at (29.5, 31.5)).
PARTS = {
    "head":          ([(18, 27, 20, 30)], (25, 27)),              # helmet and the neck; joint at the neck's base
    "torso":         ([(28, 30, 18, 32), (31, 36, 23, 29), (37, 40, 18, 32)], (25, 40)),
    "upperArmBack":  ([(31, 33, 18, 22)], (20, 31)),
    "forearmBack":   ([(34, 36, 18, 23)], (20, 34)),
    "upperArmFront": ([(31, 33, 30, 32)], (31, 31)),
    "forearmFront":  ([(34, 36, 30, 32)], (31, 34)),
    "legBack":       ([(41, 49, 18, 25)], (22, 41)),
    "legFront":      ([(41, 49, 26, 32)], (28, 41)),
}
# torso pixels painted in the coat colour where the arms used to cover it
TORSO_FILL = [(31, 36, 21, 22), (31, 36, 30, 30)]
ORDER = ["head", "torso", "upperArmBack", "forearmBack", "upperArmFront", "forearmFront", "legBack", "legFront"]


def art_frame():
    im = Image.open(SRC).convert("RGBA").crop((0, 0, ART * SCALE, ART * SCALE))
    small = im.resize((ART, ART), Image.NEAREST)
    assert list(small.resize((ART * SCALE, ART * SCALE), Image.NEAREST).getdata()) == list(im.getdata()), "not clean 4x art"
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
                    part.putpixel((x, y), COAT)
    return part


def main():
    os.makedirs(OUT, exist_ok=True)
    frame = art_frame()
    c = CANVAS // 2
    print("// joints in rig units (art pixel = 0.04 u), relative to the sprite centre (25, 25):")
    for name in ORDER:
        rects, (px, py) = PARTS[name]
        part = cut(frame, rects, TORSO_FILL if name == "torso" else ())
        canvas = Image.new("RGBA", (CANVAS, CANVAS), (0, 0, 0, 0))
        canvas.paste(part, (c - px, c - py))
        sheet = Image.new("RGBA", (len(ANGLES) * CANVAS * SCALE, CANVAS * SCALE), (0, 0, 0, 0))
        for i, angle in enumerate(ANGLES):
            rotated = canvas.rotate(angle, resample=Image.NEAREST, center=(c + 0.5, c + 0.5))
            big = rotated.resize((CANVAS * SCALE, CANVAS * SCALE), Image.NEAREST)
            sheet.paste(big, (i * CANVAS * SCALE, 0))
        sheet.save(os.path.join(OUT, name + ".png"))
        ux, uy = (px + 0.5 - ART / 2) * 0.04, (ART / 2 - (py + 0.5)) * 0.04
        print(f"//   {name:14s} joint ({ux:+.2f}, {uy:+.2f})")
    print(f"// {len(ANGLES)} frames of {CANVAS * SCALE}x{CANVAS * SCALE} px per part, step {ANGLE_STEP} deg")


if __name__ == "__main__":
    main()
