# The Arena's three rare power-ups (José, 2026-11-02), in the game's art pixels drawn at 4x, in the
# Dark Fantasy palette: bone, black iron, old blood, and a glow that only the front face has.
#
#   python3 power_ups.py <repo root>
#
# Writes into Assets/Sprites/Items/Resources/PowerUps (loaded by ArenaPowerUps): "Insta-kill.png"
# (a skull with burning eyes), "Nuke.png" (an iron bomb with a green radiation sign and a lit fuse)
# and "Magnet.png" (a blood-red horseshoe with a spark between its steel tips). Each is a sheet of
# 8 frames of 88x88: one full turn around the upright axis, squeezing like a coin, with the glow
# off on the back. The .meta files (Point filter, uncompressed, sliced) keep their guid.
import math, os, sys
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from sheetmeta import write_meta

ROOT = sys.argv[1]
OUT = os.path.join(ROOT, 'Assets/Sprites/Items/Resources/PowerUps')
PX = 4
SIZE = 22
FRAMES = 8
MIN_WIDTH = 3   # edge-on it is still a sliver, not nothing

INK = (28, 16, 24)
BONE_HI, BONE, BONE_LO, BONE_DK = (245, 238, 215), (222, 210, 182), (158, 140, 118), (96, 80, 76)
EYE, EYE_HOT = (214, 30, 36), (255, 150, 84)
IRON_HI, IRON, IRON_LO = (104, 104, 122), (58, 56, 70), (34, 32, 44)
SIGN, SIGN_HI = (120, 224, 74), (214, 255, 150)
EMBER, EMBER_HOT = (232, 96, 32), (255, 214, 110)
BLOOD_HI, BLOOD, BLOOD_LO = (206, 56, 58), (146, 24, 36), (86, 14, 28)
STEEL_HI, STEEL, STEEL_LO = (226, 226, 236), (160, 162, 180), (96, 98, 118)
SPARK, SPARK_HI = (176, 120, 255), (236, 216, 255)

# what the glow turns into on the back of the turn
BACK = {
    EYE: INK, EYE_HOT: INK,
    SIGN: IRON, SIGN_HI: IRON,
    SPARK: None, SPARK_HI: None,
}

SKULL_KEY = {'#': INK, 'L': BONE_HI, 'B': BONE, 'S': BONE_LO, 'D': BONE_DK, 'R': EYE, 'O': EYE_HOT}
SKULL = [
    '......########......',
    '....##LLLLLLBB##....',
    '...#LLLLLLBBBBBB#...',
    '..#LLLLBBBBBBBBBS#..',
    '.#LLLBBBBBBBBBBBBS#.',
    '.#LLBBBBBBBBBBBBBS#.',
    '.#LB#####BB#####BS#.',
    '.#B##RRR##B#RRR##S#.',
    '.#B#ROOR#BB#ROOR#S#.',
    '.#B##RR##BB##RR##S#.',
    '.#BB####B##B####BS#.',
    '..#SBBBB#DD#BBBBS#..',
    '..##SSBBB##BBBSS##..',
    '....#SBBBBBBBBS#....',
    '....#B#B#BB#B#B#....',
    '....#L#L#LL#L#L#....',
    '....#D#D#DD#D#D#....',
    '.....##########.....',
]


def blank():
    return Image.new('RGBA', (SIZE, SIZE), (0, 0, 0, 0))


def outlined(img):
    """a dark line around whatever is drawn, so it reads over any background"""
    out = img.copy()
    p, q = img.load(), out.load()
    for y in range(SIZE):
        for x in range(SIZE):
            if p[x, y][3]:
                continue
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx, ny = x + dx, y + dy
                if 0 <= nx < SIZE and 0 <= ny < SIZE and p[nx, ny][3]:
                    q[x, y] = INK + (255,)
                    break
    return out


def skull():
    img = blank()
    p = img.load()
    top = (SIZE - len(SKULL)) // 2
    left = (SIZE - len(SKULL[0])) // 2
    for y, row in enumerate(SKULL):
        for x, c in enumerate(row):
            if c != '.':
                p[left + x, top + y] = SKULL_KEY[c] + (255,)
    return img


def nuke():
    img = blank()
    p = img.load()
    cx, cy, r = 10.5, 12.5, 8.2
    for y in range(SIZE):
        for x in range(SIZE):
            dx, dy = x - cx, y - cy
            d = math.hypot(dx, dy)
            if d > r:
                continue
            # lit from the upper left, like the rest of the game's items
            light = (-dx - dy) / (r * 1.41)
            c = IRON_HI if light > 0.62 and d > r - 3.2 else IRON_LO if light < -0.35 else IRON
            # the radiation sign: three blades and a dot
            angle = math.degrees(math.atan2(-dy, dx)) % 360
            blade = min(abs((angle - a + 180) % 360 - 180) for a in (90, 210, 330)) <= 31
            if d <= 1.3:
                c = SIGN_HI
            elif 2.2 <= d <= 5.8 and blade:
                c = SIGN_HI if light > 0.25 else SIGN
            p[x, y] = c + (255,)
    # the cap
    for x in range(8, 14):
        for y in range(3, 5):
            p[x, y] = (IRON_HI if x < 10 else IRON_LO) + (255,)
    # the fuse, burning at its tip
    for x, y in ((13, 2), (14, 2), (15, 1), (16, 1)):
        p[x, y] = BONE_LO + (255,)
    p[17, 1] = EMBER + (255,)
    p[18, 1] = EMBER_HOT + (255,)
    p[17, 0] = EMBER_HOT + (255,)
    return outlined(img)


def magnet():
    img = blank()
    p = img.load()
    cx, cy, outer, inner = 10.5, 9.5, 9.2, 4.2
    for y in range(SIZE):
        for x in range(SIZE):
            dx, dy = x - cx, y - cy
            d = math.hypot(dx, dy)
            on_arc = y <= cy and inner <= d <= outer
            leg = y > cy and y <= 19 and inner <= abs(dx) <= outer
            if not (on_arc or leg):
                continue
            tip = y >= 15
            # rounded by the light: bright on the outer upper left, dark inside and on the right
            across = (abs(dx) - inner) / (outer - inner) if leg else (d - inner) / (outer - inner)
            right = dx > 0
            if tip:
                c = STEEL_HI if not right and across > 0.55 else STEEL_LO if right and across > 0.55 else STEEL
            else:
                c = BLOOD_HI if not right and across > 0.55 else BLOOD_LO if right or across < 0.25 else BLOOD
            p[x, y] = c + (255,)
    img = outlined(img)
    p = img.load()
    # the spark jumping between the tips (over the outline: it is light, it needs none)
    for x, y in ((8, 17), (9, 16), (10, 17), (11, 16), (12, 17), (13, 16)):
        p[x, y] = SPARK + (255,)
    p[10, 17] = SPARK_HI + (255,)
    p[11, 16] = SPARK_HI + (255,)
    return img


def back_of(front):
    """the far side: the same shape turned around, its glow out and a little darker"""
    out = front.transpose(Image.FLIP_LEFT_RIGHT)
    p = out.load()
    for y in range(SIZE):
        for x in range(SIZE):
            r, g, b, a = p[x, y]
            if not a:
                continue
            c = (r, g, b)
            if c in BACK:
                to = BACK[c]
                p[x, y] = (0, 0, 0, 0) if to is None else to + (255,)
            elif c != INK:
                p[x, y] = (int(r * 0.72), int(g * 0.72), int(b * 0.72), 255)
    return out


def squeezed(face, factor):
    """the face turned around its upright axis: fewer columns, the first and last always kept"""
    box = face.getbbox()
    art = face.crop(box)
    w = max(MIN_WIDTH, round(art.width * factor))
    columns = [round(i * (art.width - 1) / max(1, w - 1)) for i in range(w)]
    out = blank()
    left = (SIZE - w) // 2
    for i, column in enumerate(columns):
        out.paste(art.crop((column, 0, column + 1, art.height)), (left + i, box[1]))
    return out


def sheet(name, front):
    back = back_of(front)
    cell = SIZE * PX
    img = Image.new('RGBA', (cell * FRAMES, cell), (0, 0, 0, 0))
    for i in range(FRAMES):
        c = math.cos(2 * math.pi * i / FRAMES)
        frame = squeezed(front if c >= 0 else back, abs(c))
        img.paste(frame.resize((cell, cell), Image.NEAREST), (i * cell, 0))
    path = os.path.join(OUT, name + '.png')
    img.save(path)
    write_meta(ROOT, path + '.meta', [(f'{name}_{i}', i * cell, 0, cell, cell, (0.5, 0.5)) for i in range(FRAMES)])
    meta = path + '.meta'
    text = open(meta).read().replace('textureCompression: 1', 'textureCompression: 0')
    open(meta, 'w', newline='\n').write(text)


os.makedirs(OUT, exist_ok=True)
sheet('Insta-kill', skull())
sheet('Nuke', nuke())
sheet('Magnet', magnet())
print('ok')
