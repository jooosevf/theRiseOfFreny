"""Freny's body parts for the skeletal animation (FrenyRig.cs): drawn by hand here, pixel by pixel,
with the joints painted in, instead of cut out of the idle frame (the first prototype did that
and showed seams at the hip and the shoulders as soon as a limb moved).

Freny's sheets are 50x50 pixel art drawn 4x (200x200 px cells at 100 PPU: one art pixel = 4 texels
= 0.04 units). Each part below is written at that art resolution on the idle frame's grid (frame 0
of "Player idle.png", y down), in his rest pose, with a round cap around its joint that hides
under the part it hangs from, so a turned limb never uncovers a hole:
  head           the helmet and the neck; the neck reaches under the collar
  torso          collar to the belt, with the crotch; the thighs hang under its hem
  upperArm/forearm (back and front)   the sleeve, the forearm ending in the fist
  thigh/shin (back and front)         the leg bends at the knee; the shin ends in the boot
At rest the parts put together are the idle frame, pixel for pixel (main() checks it).

Every part is placed on a 27x27 canvas with its joint pixel at the centre, rotated with NEAREST
every ANGLE_STEP degrees over the full turn (-180..180) and only then scaled up 4x, so the blocks
stay square and on the grid at any bone angle. FrenyRig snaps the parts' positions to the grid.

Variants, each a sheet of its own with the same 25 frames:
  head_<kind>      the helmet repainted: rage, berserker and rage_berserker (the HUD icons on it,
                   as freny_heads.py puts them on every frame) and the cosmetic helmets of
                   freny_cosmetics.py (gold, ice, viking, shadow, crown)
  <part>_shirt     the coat's greys as a light-grey mask, tinted in game with the chosen colour
                   (torso and the four arm parts), like the FrenyLook shirt sheet

Output: Assets/Sprites/Characters/Player/Resources/FrenyRig/<sheet>.png, 25 frames of 108x108 px,
loaded by FrenyRig at runtime and sliced in code (no sheet metadata). The sword is not here:
FrenyRig holds the blades freny_poses.py draws at every angle ("Freny blade_<deg>").

    python3 freny_rig.py <repo root>        prints the joints FrenyRig.cs copies
"""
import os, sys
from PIL import Image

ROOT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(__file__), "..", "..")
sys.path.insert(0, os.path.dirname(__file__))
PLAYER = os.path.join(ROOT, "Assets", "Sprites", "Characters", "Player")
OUT = os.path.join(PLAYER, "Resources", "FrenyRig")
SCALE = 4          # texels per art pixel
ART = 50           # art pixels per cell side
CANVAS = 27        # art pixels; odd so that the centre is a pixel centre
ANGLE_STEP = 15
ANGLES = list(range(-180, 181, ANGLE_STEP))

# the sheets' palette
PAL = {
    'A': (239, 239, 233, 255),   # white (helmet's shine)
    'B': (176, 162, 141, 255),   # light grey (helmet, collar)
    'C': (99, 86, 85, 255),      # shade grey
    'D': (129, 111, 109, 255),   # mid grey (the coat)
    'E': (202, 117, 56, 255),    # skin
    'F': (69, 40, 60, 255),      # plum (shirt, trousers)
    'G': (48, 27, 42, 255),      # dark plum (outline, boots)
}
GREYS = ('A', 'B', 'C', 'D')
COAT_GREYS = ('B', 'C', 'D')

# part -> (origin x, origin y, rows, joint pixel (x, y)); rows are the pixels at art resolution,
# '.' for nothing. Front = the viewer's right: Freny faces right in the sheet (flipX turns him), the
# sword arm is the front one. Rows marked "cap" hide under the parent part at rest.
PARTS = {
    "head": (21, 18, [
        "..ABBCB.",
        ".ABBBDBB",
        ".BBBDBDB",
        "CCDBBDBB",
        "BBBDDBDD",
        "CDBBDBDB",
        "BBDBDBDB",
        ".BBDDBDC",
        "..BBDBD.",
        "...EEE..",
        "....EE..",      # cap: the neck under the collar
    ], (25, 28)),
    "torso": (19, 28, [
        "DDDDDDEEDDDD",
        ".CCDDDDFDDC.",
        "DDDCDDDFDDDC",
        "..GCDDDFDDG.",
        "..GDCCFBFCG.",
        "..GDDDCFCDG.",
        "..GDDDDCDDG.",
        "..GFDDCDCDG.",
        "..FFFDDCDFF.",
        "...FFFFFFFF.",
        "...FFFFFFFF.",
        "..FFFFFFFFF.",
        "..FFFFGGFFF.",
    ], (25, 40)),
    "upperArmBack": (18, 30, [
        ".DD",           # cap
        ".DD",
        "CDC",
        "DCD",
        "DDD",           # overlaps the forearm's cap
    ], (19, 30)),
    "forearmBack": (18, 33, [
        "DCD.",          # cap, over the upper arm
        "DDD.",
        "DDD.",
        ".DC.",
        ".CEE",
        "..EE",
    ], (19, 34)),
    "upperArmFront": (29, 30, [
        ".C",            # cap
        "GD",
        "GD",
        "GC",
        "GD",
    ], (30, 31)),
    "forearmFront": (29, 33, [
        "GC.",           # cap
        "GD.",
        "GD.",
        ".DC",
        ".CE",
        ".EE",
    ], (30, 34)),
    "thighBack": (20, 39, [
        ".FFFF",         # cap, under the hem
        ".FFFF",
        ".FFFF",
        "FFFF.",
        "FFFF.",
        "FFFF.",
        "FFF..",
    ], (22, 40)),
    "shinBack": (19, 44, [
        ".FFFF",         # cap, over the thigh
        ".FFF.",
        "FFFF.",
        "FFF..",
        "GG...",
        "FFF..",
    ], (21, 45)),
    "thighFront": (27, 39, [
        "FFF.",          # cap, under the hem
        "FFF.",
        "FFFF",
        "FFFF",
        "FFFF",
        "FFFF",
        "FFFF",
    ], (28, 40)),
    "shinFront": (27, 44, [
        "FFFF",          # cap, over the thigh
        "FFFF",
        "FFF.",
        ".FF.",
        ".GG.",
        ".FFF",
    ], (28, 45)),
}
# drawing order back to front (what the rest pose composite uses; FrenyRig sorts the same way)
ORDER = ["upperArmBack", "forearmBack", "thighBack", "shinBack", "thighFront", "shinFront",
         "torso", "head", "upperArmFront", "forearmFront"]
SHIRT_PARTS = ["torso", "upperArmBack", "forearmBack", "upperArmFront", "forearmFront"]


def pixels(part):
    """{(x, y): letter} at art resolution, on the frame's grid"""
    ox, oy, rows, _ = PARTS[part]
    return {(ox + x, oy + y): ch for y, row in enumerate(rows) for x, ch in enumerate(row) if ch != '.'}


def composite(parts=ORDER):
    pix = {}
    for name in parts:
        pix.update(pixels(name))
    return pix


def idle_frame():
    im = Image.open(os.path.join(PLAYER, "Player idle.png")).convert("RGBA").crop((0, 0, ART * SCALE, ART * SCALE))
    back = {v[:3]: k for k, v in PAL.items()}
    return {(x, y): back[im.getpixel((x * SCALE, y * SCALE))[:3]] for x in range(ART) for y in range(ART)
            if im.getpixel((x * SCALE, y * SCALE))[3] > 0}


def check_rest_pose():
    """the parts at rest must be the idle frame; the caps may add nothing visible"""
    drawn, frame = composite(), idle_frame()
    wrong = {p: (drawn.get(p), frame.get(p)) for p in set(drawn) | set(frame) if drawn.get(p) != frame.get(p)}
    if wrong:
        for p in sorted(wrong, key=lambda q: (q[1], q[0])):
            print("  mismatch at", p, "drawn", wrong[p][0], "frame", wrong[p][1])
        raise SystemExit("the parts at rest don't make the idle frame")


def sheet(pix, joint, colours):
    """a 25-frame sheet of one part rotated about its joint; pix: {(x, y): letter}"""
    c = CANVAS // 2
    canvas = Image.new("RGBA", (CANVAS, CANVAS), (0, 0, 0, 0))
    for (x, y), ch in pix.items():
        canvas.putpixel((c + x - joint[0], c + y - joint[1]), colours[ch])
    out = Image.new("RGBA", (len(ANGLES) * CANVAS * SCALE, CANVAS * SCALE), (0, 0, 0, 0))
    for i, angle in enumerate(ANGLES):
        rotated = canvas.rotate(angle, resample=Image.NEAREST, center=(c + 0.5, c + 0.5))
        big = rotated.resize((CANVAS * SCALE, CANVAS * SCALE), Image.NEAREST)
        out.paste(big, (i * CANVAS * SCALE, 0))
    return out


def helmet_variants():
    """{kind: {(x, y): rgb}} the head part's pixels per helmet, built with freny_heads' and
    freny_cosmetics' own rules on the helmet pixels (everything above the neck)"""
    import freny_heads as fh
    head = pixels("head")
    helmet = {p for p, ch in head.items() if ch != 'E'}
    rgb = lambda ch: PAL[ch][:3]
    variants = {}
    icons = {kind: fh.icon_art(i, recolour) for kind, (i, recolour) in fh.ICONS.items()}
    for kind, pal in fh.PALETTES.items():
        pix = {p: fh.repaint(rgb(head[p]), pal) for p in helmet}
        pix.update(fh.put_icon(helmet, icons[kind]))
        variants[kind] = pix
    # freny_cosmetics runs main() on import, so its tables are copied here
    GOLD = {fh.LIGHT: (240, 200, 80), fh.MID: (208, 160, 48), fh.SHADE: (150, 104, 40), fh.WHITE: (255, 240, 170)}
    HELMETS = {
        'gold': GOLD,
        'ice': {fh.LIGHT: (170, 220, 240), fh.MID: (110, 160, 210), fh.SHADE: (70, 100, 160), fh.WHITE: (235, 250, 255)},
        'viking': {fh.LIGHT: (190, 195, 205), fh.MID: (130, 135, 150), fh.SHADE: (85, 88, 105), fh.WHITE: (235, 238, 245)},
        'shadow': {fh.LIGHT: (70, 50, 90), fh.MID: (48, 32, 64), fh.SHADE: (28, 18, 40), fh.WHITE: (190, 110, 250)},
        'crown': GOLD,
    }
    BONE, BONE_SHADE, HORN_TIP = (239, 228, 200), (196, 180, 150), (120, 100, 80)
    CROWN, CROWN_SHADE, JEWEL = (255, 214, 74), (196, 140, 40), (208, 47, 47)
    xs = [x for x, _ in helmet]; ys = [y for _, y in helmet]
    lo, hi, top = min(xs), max(xs), min(ys)
    for kind, pal in HELMETS.items():
        pix = {p: fh.repaint(rgb(head[p]), pal) for p in helmet}
        if kind == 'viking':
            for side, edge in ((-1, lo), (1, hi)):
                for dx, dy, col in ((1, 3, BONE_SHADE), (1, 2, BONE), (2, 1, BONE), (2, 0, BONE), (2, -1, HORN_TIP)):
                    pix[(edge + side * dx, top + dy)] = col
        elif kind == 'crown':
            mid = (lo + hi) // 2
            for x in range(lo + 1, hi):
                pix[(x, top - 1)] = CROWN_SHADE if x in (lo + 1, hi - 1) else CROWN
            for x in (lo + 1, mid, hi - 1):
                pix[(x, top - 2)] = CROWN
            pix[(mid, top - 1)] = JEWEL
        variants[kind] = pix
    for kind in variants:
        for p in head:
            variants[kind].setdefault(p, rgb(head[p]))   # the neck
    return variants


def main():
    os.makedirs(OUT, exist_ok=True)
    check_rest_pose()
    colours = {ch: col for ch, col in PAL.items()}
    print("// joints in the Sprite's space (art pixel = 0.04 u, from the cell's centre (25, 25)):")
    for name in ORDER:
        ox, oy, rows, joint = PARTS[name]
        sheet(pixels(name), joint, colours).save(os.path.join(OUT, name + ".png"))
        ux, uy = (joint[0] + 0.5 - ART / 2) * 0.04, (ART / 2 - (joint[1] + 0.5)) * 0.04
        print(f"//   {name:14s} joint ({ux:+.2f}, {uy:+.2f})")
    # the coat's greys as a tint mask: white-ish, so the tint shows true and the shading stays
    shirt = {'B': (255, 255, 255, 255), 'D': (205, 205, 205, 255), 'C': (150, 150, 150, 255)}
    for name in SHIRT_PARTS:
        pix = {p: ch for p, ch in pixels(name).items() if ch in COAT_GREYS}
        sheet(pix, PARTS[name][3], shirt).save(os.path.join(OUT, name + "_shirt.png"))
    for kind, pix in helmet_variants().items():
        letters = {p: str(i) for i, p in enumerate(pix)}
        cols = {str(i): tuple(pix[p]) + (255,) for i, p in enumerate(pix)}
        sheet(letters, PARTS["head"][3], cols).save(os.path.join(OUT, "head_" + kind + ".png"))
    # the cape hangs from the helmet's back-top corner (CapeArt.Anchor does the same on the frames)
    helmet = [p for p, ch in pixels("head").items() if ch != 'E']
    lo = min(x for x, _ in helmet); low = max(y for x, y in helmet if x == lo)
    jx, jy = PARTS["head"][3]
    print(f"//   cape anchor: ({(lo - jx) * 0.04:+.2f}, {(jy - low - 1) * 0.04 + 0.02:+.2f}) from the neck joint")
    print(f"// {len(ANGLES)} frames of {CANVAS * SCALE}x{CANVAS * SCALE} px per part, step {ANGLE_STEP} deg")


if __name__ == "__main__":
    main()
