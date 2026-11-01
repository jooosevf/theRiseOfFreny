"""Draws Freny's skeletal poses (FrenyRig.cs) without Unity, from the part sheets freny_rig.py makes,
so the poses and the joints can be judged here before a test on the PC: the same bones, the
same angles and the same grid snapping, written once more in Python. Writes a GIF per clip
(idle, walk, run, jump, the 3-hit combo) and a contact sheet, in the folder given.

    python3 freny_rig_preview.py <repo root> <out dir>

Keep the poses here and in FrenyRig.cs the same: this file is the drawing board, FrenyRig the game.
"""
import math, os, re, sys
from PIL import Image

ROOT, OUTDIR = sys.argv[1], sys.argv[2]
sys.path.insert(0, os.path.dirname(__file__))
import freny_rig as fr

R = fr.OUT
F = fr.CANVAS * fr.SCALE
STEP = fr.ANGLE_STEP
ZOOM = 2                                  # on top of the art's 4x
PAD = 100
VIEW = (PAD - 20, PAD + 40, PAD + 220, PAD + 200)   # what the GIFs show: 240x160 px of the cell

# the bones: name -> (parent, offset from the parent's joint in art pixels, y UP, part, draw order)
# offsets come from freny_rig.PARTS' joints; the pelvis is the root at the belt
J = {name: fr.PARTS[name][3] for name in fr.PARTS}
PELVIS = J["torso"]
def rel(a, b):
    return (a[0] - b[0], b[1] - a[1])      # y up
BONES = {
    "pelvis":        (None, (0, 0), None, 0),
    "torso":         ("pelvis", (0, 0), "torso", 0),
    "neck":          ("torso", rel(J["head"], PELVIS), "head", 2),
    "shoulderBack":  ("torso", rel(J["upperArmBack"], PELVIS), "upperArmBack", -6),
    "elbowBack":     ("shoulderBack", rel(J["forearmBack"], J["upperArmBack"]), "forearmBack", -5),
    "shoulderFront": ("torso", rel(J["upperArmFront"], PELVIS), "upperArmFront", 3),
    "elbowFront":    ("shoulderFront", rel(J["forearmFront"], J["upperArmFront"]), "forearmFront", 4),
    "hipBack":       ("pelvis", rel(J["thighBack"], PELVIS), "thighBack", -4),
    "kneeBack":      ("hipBack", rel(J["shinBack"], J["thighBack"]), "shinBack", -3),
    "hipFront":      ("pelvis", rel(J["thighFront"], PELVIS), "thighFront", 1),
    "kneeFront":     ("hipFront", rel(J["shinFront"], J["thighFront"]), "shinFront", 2),
}
FIST = (0.5, -3.5)                        # from the front elbow, art pixels y up (the fist's centre)

SHEETS = {name: Image.open(os.path.join(R, name + ".png")).convert("RGBA") for name in fr.PARTS}


def blades():
    """"Freny blade_<deg>": the blade at each angle, pivoted at the grip (from freny_poses.py)"""
    import freny_heads as fh
    path = os.path.join(fr.PLAYER, "Resources", "FrenyPoses", "blades.png")
    img = Image.open(path).convert("RGBA")
    out = {}
    for name, r, pivot in fh.sprites_of(path + ".meta"):
        x0, y0 = int(r['x']), int(img.height - r['y'] - r['height'])
        cell = img.crop((x0, y0, x0 + int(r['width']), y0 + int(r['height'])))
        out[name] = (cell, pivot)
    return out
BLADES = blades()


# ---- poses: bone angles in degrees, + = swung forward (the way he faces), 0 = hanging down -----

def pose(**kw):
    p = {b: 0.0 for b in BONES}
    p.update(kw)
    return p


def lerp(a, b, t):
    t = max(0.0, min(1.0, t))
    return {k: a[k] + (b[k] - a[k]) * t for k in a}


def walk(phase, run=False):
    """legs against the arms; the knee bends while the leg swings through and straightens to land"""
    s = math.sin(phase * math.tau)
    c = math.cos(phase * math.tau)
    leg, knee, arm, elbow = (48, 55, 45, 65) if run else (32, 35, 24, 20)
    return pose(
        torso=14 if run else 0, neck=-14 if run else 0,
        hipFront=leg * s, kneeFront=-knee * max(0.0, c),
        hipBack=-leg * s, kneeBack=-knee * max(0.0, -c),
        shoulderFront=-arm * s, elbowFront=elbow * max(0.0, -s) + (25 if run else 0),
        shoulderBack=arm * s, elbowBack=elbow * max(0.0, s) + (25 if run else 0),
    )


RISE = pose(torso=6, neck=4, hipFront=50, kneeFront=-65, hipBack=-20, kneeBack=-25,
            shoulderFront=-35, elbowFront=-25, shoulderBack=-50, elbowBack=-20)
FALL = pose(torso=-4, neck=-10, hipFront=25, kneeFront=-25, hipBack=-25, kneeBack=-10,
            shoulderFront=70, elbowFront=40, shoulderBack=-60, elbowBack=-30)


def jump(vy):
    return lerp(FALL, RISE, (vy + 3) / 7)


# the combo (FrenyMotion's numbers): arm and blade angles, 0 = forward, up +
SWINGS = [(45, 105, -15, -15, 0.035, 0.065), (-45, -60, 75, 105, 0.03, 0.06), (90, 150, -45, -75, 0.06, 0.07)]
WINDUP_PULL, RECOVER = 15, 0.22


def swing(step, t, base, combo_continues=False):
    """the sword arm through hit `step` at time t since it started; returns the pose and the blade
    angle (None when the sword is away)"""
    arm_from, blade_from, arm_to, blade_to, windup, cut = SWINGS[step - 1]
    pull = math.copysign(WINDUP_PULL, blade_from - blade_to)
    if t < windup:
        k = t / windup
        arm, blade, elbow = arm_from, blade_from + pull * k, -60
    else:
        k = min(1.0, (t - windup) / cut)
        k = 1 - (1 - k) ** 3
        arm = arm_from + (arm_to - arm_from) * k
        blade = blade_from + pull + (blade_to - blade_from - pull) * k
        elbow = -60 * (1 - k)
    done = t - windup - cut
    back = 0.0 if combo_continues else max(0.0, min(1.0, (done - 0.12) / RECOVER))
    p = dict(base)
    p["shoulderFront"] = (arm + 90) * (1 - back) + base["shoulderFront"] * back
    p["elbowFront"] = elbow * (1 - back) + base["elbowFront"] * back
    p["shoulderBack"] = (-arm * 0.4) * (1 - back) + base["shoulderBack"] * back
    if step == 3:
        p["torso"] += (-8 if t < windup else 10) * (1 - back)
    return p, (blade if back < 1 else None)


# ---- drawing --------------------------------------------------------------------------------------

def solve(p, mirror=1):
    """world position (art px, y up, from the pelvis) and total angle of every bone"""
    out = {}
    for name, (parent, off, _, _) in BONES.items():
        if parent is None:
            out[name] = ((0.0, 0.0), 0.0)
            continue
        (px, py), pa = out[parent]
        a = math.radians(pa)
        x = px + off[0] * math.cos(a) - off[1] * math.sin(a)
        y = py + off[0] * math.sin(a) + off[1] * math.cos(a)
        out[name] = ((x, y), pa + p[name] * mirror)
    return out


def draw(p, blade=None, great=False):
    """the idle frame's 200x200 cell (4x) with PAD px of room around it for the sword"""
    cell = Image.new("RGBA", (fr.ART * fr.SCALE + 2 * PAD, fr.ART * fr.SCALE + 2 * PAD), (0, 0, 0, 0))
    world = solve(p)
    layers = []
    for name, (parent, off, part, order) in BONES.items():
        if part is None:
            continue
        (x, y), angle = world[name]
        frame = max(0, min(len(fr.ANGLES) - 1, round((angle + 180) / STEP)))
        sx, sy = PELVIS[0] + round(x), PELVIS[1] - round(y)       # snapped to the grid
        tile = SHEETS[part].crop((frame * F, 0, frame * F + F, F))
        layers.append((order, tile, (PAD + (sx - fr.CANVAS // 2) * fr.SCALE, PAD + (sy - fr.CANVAS // 2) * fr.SCALE)))
    if blade is not None:
        (ex, ey), ea = world["elbowFront"]
        a = math.radians(ea)
        fx = ex + FIST[0] * math.cos(a) - FIST[1] * math.sin(a)
        fy = ey + FIST[0] * math.sin(a) + FIST[1] * math.cos(a)
        deg = max(-90, min(180, round(blade / STEP) * STEP))
        tile, (pvx, pvy) = BLADES[("Freny great blade_" if great else "Freny blade_") + str(deg)]
        sx, sy = PELVIS[0] + round(fx), PELVIS[1] - round(fy)
        layers.append((-7 if blade > 120 else 5, tile, (PAD + round(sx * fr.SCALE - pvx * tile.width), PAD + round(sy * fr.SCALE - (1 - pvy) * tile.height))))
    for _, tile, at in sorted(layers, key=lambda l: l[0]):
        paste_any(cell, tile, at)
    return cell


def paste_any(cell, tile, at):
    layer = Image.new("RGBA", cell.size, (0, 0, 0, 0))
    layer.paste(tile, at)
    cell.alpha_composite(layer)


def clip(name, frames, ms=60):
    bg = (54, 42, 60, 255)
    big = []
    for cell in frames:
        im = Image.new("RGBA", cell.size, bg)
        im.alpha_composite(cell)
        im = im.crop(VIEW).resize((240 * ZOOM, 160 * ZOOM), Image.NEAREST)
        big.append(im.convert("P", palette=Image.ADAPTIVE))
    big[0].save(os.path.join(OUTDIR, name + ".gif"), save_all=True, append_images=big[1:], duration=ms, loop=0, disposal=2)
    return frames


def main():
    os.makedirs(OUTDIR, exist_ok=True)
    sheet_cells = []
    sheet_cells += clip("idle", [draw(pose())] * 4, 200)[:1]
    sheet_cells += clip("walk", [draw(walk(i / 16)) for i in range(16)], 55)[0:16:4]
    sheet_cells += clip("run", [draw(walk(i / 12, run=True)) for i in range(12)], 45)[0:12:3]
    sheet_cells += clip("jump", [draw(jump(vy)) for vy in [7, 5, 3, 1, -1, -3, -5, -7, -7]], 90)[::2]
    combo = []
    base = pose()
    for step in (1, 2, 3):
        _, _, _, _, windup, cut = SWINGS[step - 1]
        for i in range(10):
            t = i / 9 * (windup + cut + 0.06)
            p, blade = swing(step, t, base, combo_continues=step < 3)
            combo.append(draw(p, blade))
        if step == 3:
            for i in range(1, 6):
                p, blade = swing(step, windup + cut + 0.12 + i / 5 * RECOVER, base)
                combo.append(draw(p, blade))
    clip("combo", combo, 35)
    sheet_cells += combo[4::10]
    air = []
    for i in range(8):
        t = i / 7 * 0.16
        p, blade = swing(2, t, jump(2))
        air.append(draw(p, blade))
    clip("air_hit", air, 40)
    sheet_cells += air[4:5]
    n = len(sheet_cells)
    sheet = Image.new("RGBA", (n * 240 * ZOOM, 160 * ZOOM), (54, 42, 60, 255))
    for i, cell in enumerate(sheet_cells):
        im = Image.new("RGBA", cell.size, (54, 42, 60, 255))
        im.alpha_composite(cell)
        sheet.paste(im.crop(VIEW).resize((240 * ZOOM, 160 * ZOOM), Image.NEAREST), (i * 240 * ZOOM, 0))
    sheet.save(os.path.join(OUTDIR, "poses.png"))
    print("wrote", OUTDIR)


if __name__ == "__main__":
    main()
