"""Phase 4 art: the Mina-aranha (spider mine), the Rede (net) and the Cajado do Mago (mage's staff).

Drawn in art pixels and saved 4x upscaled at 100 px a unit, like the Mine and Crawler sheets it
borrows its palette from (an art pixel is 0.04 units). The sheets' .meta files are written by
sheetmeta.py (Multiple mode, pivot at the bottom middle); single sprites copy the Vomit Puddle meta.
The opened net is the unused "Items/Net 100x100" art recoloured to a light rope, so it reads on the
dark enemies. Run from the repo root: python Tools/ArtGen/fase4_art.py
"""
import os
import re
import sys
import uuid

from PIL import Image

sys.path.insert(0, os.path.dirname(__file__))
from sheetmeta import write_meta  # noqa: E402

ROOT = '.'
SCALE = 4
MINE_DIR = 'Assets/Sprites/Characters/Mobs/SpiderMine/Resources/SpiderMine/'
ITEM_DIR = 'Assets/Sprites/Items/Resources/Fase4/'
SINGLE_TEMPLATE = 'Assets/Sprites/Characters/Mobs/Cyclops/Resources/Cyclops/Vomit Puddle_0.png.meta'

# the Mine / Crawler palette
D0 = (24, 36, 52, 255)
D1 = (34, 44, 63, 255)
D2 = (47, 55, 80, 255)
P0 = (54, 55, 120, 255)
P1 = (79, 64, 159, 255)
P2 = (120, 100, 210, 255)
LAMP_OFF = (90, 20, 30, 255)
LAMP = (200, 35, 40, 255)
LAMP_HOT = (255, 120, 90, 255)
WHITE = (255, 235, 220, 255)
CLEAR = (0, 0, 0, 0)

FW, FH = 24, 18   # spider mine frame, art pixels


def canvas(w, h):
    return Image.new('RGBA', (w, h), CLEAR)


def put(px, x, y, c, w, h):
    if 0 <= x < w and 0 <= y < h:
        px[x, y] = c


def body(px, ox, lift, lamp, cracks=0):
    """the dome: a riveted shell like the Mine's, the lamp on top; y grows down, feet at FH-1"""
    base = FH - 5 - lift          # the shell's underside
    cx = ox + FW // 2
    rows = [(-7, 7), (-8, 8), (-8, 8), (-7, 7), (-6, 6), (-4, 4)]   # bottom up
    for i, (a, b) in enumerate(rows):
        y = base - i
        for x in range(cx + a, cx + b):
            edge = x in (cx + a, cx + b - 1) or i == len(rows) - 1
            c = D0 if edge else (D1 if i < 2 else D2)
            put(px, x, y, c, ox + FW, FH)
    # spikes and rivets in purple, as on the Mine
    for x in (cx - 6, cx - 2, cx + 2, cx + 5):
        put(px, x, base - 2, P0, ox + FW, FH)
    for x in (cx - 9, cx + 8):
        put(px, x, base - 2, P1, ox + FW, FH)
        put(px, x, base - 3, P0, ox + FW, FH)
    put(px, cx - 5, base - 6, P1, ox + FW, FH)
    put(px, cx + 4, base - 6, P1, ox + FW, FH)
    # the lamp (the bright blink is drawn on top by the game)
    for x in (cx - 1, cx):
        put(px, x, base - 6, lamp, ox + FW, FH)
        put(px, x, base - 7, lamp, ox + FW, FH)
    put(px, cx - 2, base - 6, D0, ox + FW, FH)
    put(px, cx + 1, base - 6, D0, ox + FW, FH)
    # arming: hot cracks across the shell
    if cracks:
        for x, y in ((cx - 4, base - 2), (cx - 3, base - 3), (cx + 3, base - 1), (cx + 2, base - 2),
                     (cx - 1, base - 4), (cx + 4, base - 4))[:cracks * 2]:
            put(px, x, y, LAMP_HOT if cracks > 2 else LAMP, ox + FW, FH)
    return base


def legs(px, ox, base, phase, tuck=0):
    """three legs a side, in the Crawler's purple; phase moves them in pairs"""
    cx = ox + FW // 2
    for side in (-1, 1):
        for k, reach in enumerate((4, 7, 10)):
            lift = 1 if (k + phase + (side > 0)) % 2 == 0 else 0
            r = max(2, reach - tuck * 2)
            hip = cx + side * (r - 3)
            knee_y = base - 1 - lift
            foot_x = cx + side * r
            foot_y = FH - 1 - lift if tuck < 2 else FH - 2
            put(px, hip, base + 1, P0, ox + FW, FH)
            put(px, foot_x, knee_y, P1, ox + FW, FH)
            for y in range(knee_y + 1, foot_y + 1):
                put(px, foot_x + (side if y == foot_y and not tuck else 0), y, P1, ox + FW, FH)
            for x in range(min(hip, foot_x), max(hip, foot_x) + 1):
                put(px, x, knee_y, P1 if abs(x - foot_x) < 2 else P0, ox + FW, FH)


def sheet(frames, name):
    im = canvas(FW * len(frames), FH)
    px = im.load()
    for i, f in enumerate(frames):
        f(px, i * FW)
    big = im.resize((im.width * SCALE, im.height * SCALE), Image.NEAREST)
    path = MINE_DIR + name + '.png'
    big.save(path)
    w, h = FW * SCALE, FH * SCALE
    write_meta(ROOT, path + '.meta', [(f'{name}_{i}', i * w, 0, w, h, (0.5, 0.0)) for i in range(len(frames))])


def idle(i):
    def f(px, ox):
        base = body(px, ox, i % 2, LAMP_OFF)
        legs(px, ox, base, 0)
    return f


def walk(i):
    def f(px, ox):
        base = body(px, ox, 1 if i % 2 else 0, LAMP_OFF)
        legs(px, ox, base, i % 2)
    return f


def arm(i):
    def f(px, ox):
        base = body(px, ox, max(0, 1 - i), LAMP if i % 2 == 0 else LAMP_HOT, cracks=i + 1)
        legs(px, ox, base, 0, tuck=min(2, i))
    return f


def single(img, path, pivot):
    """a lone sprite at 100 px a unit with its own meta (Vomit Puddle's, a new guid)"""
    img.save(path)
    meta = open(SINGLE_TEMPLATE).read()
    old = path + '.meta'
    guid = re.search(r'^guid: (\w+)', open(old).read(), re.M).group(1) if os.path.exists(old) else uuid.uuid4().hex
    meta = re.sub(r'^guid: \w+', 'guid: ' + guid, meta, count=1, flags=re.M)
    meta = re.sub(r'spritePivot: \{[^}]*\}', 'spritePivot: {x: %s, y: %s}' % pivot, meta)
    meta = meta.replace('spritePixelsToUnits: 32', 'spritePixelsToUnits: 100')
    meta = re.sub(r'spriteID: \w+', 'spriteID: ' + uuid.uuid4().hex, meta)
    open(old, 'w').write(meta)


def up(im):
    return im.resize((im.width * SCALE, im.height * SCALE), Image.NEAREST)


def lamp_glow():
    im = canvas(5, 5)
    px = im.load()
    for x, y in ((2, 0), (1, 1), (2, 1), (3, 1), (0, 2), (1, 2), (3, 2), (4, 2), (1, 3), (2, 3), (3, 3), (2, 4)):
        px[x, y] = LAMP
    px[2, 2] = WHITE
    for x, y in ((2, 1), (1, 2), (3, 2), (2, 3)):
        px[x, y] = LAMP_HOT
    return up(im)


ROPE = (196, 160, 104, 255)
ROPE_DARK = (120, 86, 52, 255)
ROPE_LIGHT = (232, 206, 150, 255)


def net_ball():
    """the net bundled up for the throw (also the shop and HUD icon)"""
    im = canvas(12, 12)
    px = im.load()
    for y in range(12):
        for x in range(12):
            dx, dy = x - 5.5, y - 6
            if dx * dx + dy * dy <= 27:
                px[x, y] = ROPE_DARK if (x + y) % 3 == 0 else ROPE if (x - y) % 3 else ROPE_LIGHT
    for x, y in ((5, 0), (6, 0), (6, 1), (7, 1), (0, 6), (11, 5), (2, 10), (9, 10)):
        px[x, y] = ROPE_DARK
    for x, y in ((3, 3), (8, 3), (4, 8), (7, 7)):
        px[x, y] = ROPE_LIGHT
    return up(im)


def net_open():
    """the unused Items/Net art in rope colours: dark knots, light strands"""
    src = Image.open('Assets/Sprites/Items/Net 100x100.png').convert('RGBA')
    small = src.resize((src.width // SCALE, src.height // SCALE), Image.NEAREST)
    px = small.load()
    for y in range(small.height):
        for x in range(small.width):
            r, g, b, a = px[x, y]
            if a == 0:
                continue
            px[x, y] = ROPE_DARK if r < 30 else ROPE
            if (x + y) % 7 == 0 and r >= 30:
                px[x, y] = ROPE_LIGHT
    return up(small)


WOOD_DARK = (70, 44, 30, 255)
WOOD = (120, 80, 48, 255)
WOOD_LIGHT = (160, 112, 68, 255)


def staff():
    """the Cajado do Mago: a crooked wooden staff with the sorcerers' purple orb"""
    im = canvas(9, 28)
    px = im.load()
    for y in range(7, 28):
        x = 4 + (1 if 14 <= y < 19 else 0)
        px[x, y] = WOOD
        px[x - 1, y] = WOOD_DARK
        if y % 5 == 0:
            px[x, y] = WOOD_LIGHT
    # the claw holding the orb
    for x, y in ((1, 6), (2, 7), (7, 6), (6, 7), (1, 3), (7, 3), (1, 4), (7, 4), (1, 5), (7, 5)):
        px[x, y] = WOOD_DARK
    for y in range(1, 7):
        for x in range(2, 7):
            dx, dy = x - 4, y - 3.5
            if dx * dx + dy * dy <= 7:
                px[x, y] = P1 if dx + dy > 0 else P2
    px[3, 2] = WHITE
    px[4, 3] = (190, 170, 255, 255)
    return up(im)


def main():
    os.makedirs(MINE_DIR, exist_ok=True)
    os.makedirs(ITEM_DIR, exist_ok=True)
    sheet([idle(i) for i in range(2)], 'SpiderMine Idle')
    sheet([walk(i) for i in range(4)], 'SpiderMine Walk')
    sheet([arm(i) for i in range(4)], 'SpiderMine Arm')
    single(lamp_glow(), MINE_DIR + 'SpiderMine Lamp.png', (0.5, 0.5))
    single(net_ball(), ITEM_DIR + 'Net Ball.png', (0.5, 0.5))
    single(net_open(), ITEM_DIR + 'Net Open.png', (0.5, 0.5))
    single(staff(), ITEM_DIR + 'Mage Staff.png', (0.5, 0.15))


if __name__ == '__main__':
    main()
