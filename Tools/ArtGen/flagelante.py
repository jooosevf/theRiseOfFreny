# The Flagelante's weapons, drawn pixel by pixel in the game's rust and bone colours (art bible).
#   Resources/FrenyPoses/flails       the flail at every swing angle, pivoted at the grip, for
#                                     FrenyMotion: "Freny flail_<deg>", "Freny great flail_<deg>"
#   Resources/FrenyPoses/flagelante   the held flails and the icons of his slots (FrenyClasses)
#
#   python flagelante.py <repo root>
import math, os, random, sys
from PIL import Image
from sheetmeta import write_meta

ROOT = sys.argv[1]
OUT = os.path.join(ROOT, 'Assets/Sprites/Characters/Player/Resources/FrenyPoses')
PX = 4
ANGLES = list(range(-90, 181, 15))

OUTLINE = (26, 17, 25, 255)
IRON, IRON_LIT, IRON_DARK = (92, 88, 96, 255), (150, 142, 140, 255), (48, 44, 56, 255)
RUST, RUST_LIT = (134, 66, 42, 255), (190, 104, 56, 255)
WOOD, WOOD_DARK = (112, 74, 48, 255), (69, 40, 40, 255)
BONE = (214, 200, 170, 255)
EMBER, EMBER_LIT = (214, 96, 38, 255), (248, 178, 72, 255)


def outlined(pix):
    out = dict(pix)
    for (x, y) in pix:
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            if (x + dx, y + dy) not in pix:
                out[(x + dx, y + dy)] = OUTLINE
    return out


def ball(pix, cx, cy, r, spikes=8):
    for y in range(int(cy - r - 1), int(cy + r + 2)):
        for x in range(int(cx - r - 1), int(cx + r + 2)):
            d = math.hypot(x - cx, y - cy)
            if d <= r:
                lit = (x - cx) + (y - cy) < -r * 0.5
                dark = (x - cx) + (y - cy) > r * 0.7
                pix[(x, y)] = IRON_LIT if lit else IRON_DARK if dark else IRON
    for i in range(spikes):
        a = i * 2 * math.pi / spikes
        for t in (r + 1, r + 2):
            pix[(int(round(cx + math.cos(a) * t)), int(round(cy + math.sin(a) * t)))] = BONE if t > r + 1 else RUST_LIT


def chain(pix, x0, y0, x1, y1):
    steps = max(1, int(math.hypot(x1 - x0, y1 - y0)))
    for i in range(steps + 1):
        t = i / steps
        pix[(int(round(x0 + (x1 - x0) * t)), int(round(y0 + (y1 - y0) * t)))] = IRON_LIT if i % 2 else RUST


def flail(great, deg):
    """grip at (0, 0), up is -y: a wooden handle, then the chain and its ball along the angle"""
    rad = math.radians(deg)
    ux, uy = math.cos(rad), -math.sin(rad)
    px, py = -uy, ux
    pix = {}
    for u in range(0, 6):
        pix[(int(round(ux * u)), int(round(uy * u)))] = WOOD if u % 3 else WOOD_DARK
    hx, hy = ux * 5, uy * 5
    if great:
        for off, length, r in ((-5, 19, 2.6), (0, 24, 3.6), (5, 19, 2.6)):
            ex, ey = hx + ux * length + px * off, hy + uy * length + py * off
            chain(pix, hx, hy, ex - ux * r, ey - uy * r)
            ball(pix, ex, ey, r, 8 if r > 3 else 6)
    else:
        ex, ey = hx + ux * 13, hy + uy * 13
        chain(pix, hx, hy, ex - ux * 3, ey - uy * 3)
        ball(pix, ex, ey, 3.2)
    return outlined(pix)


def shield():
    pix = {}
    for y in range(-11, 12):
        half = 6 if y < 6 else 6 - (y - 5)
        for x in range(-half, half + 1):
            edge = abs(x) == half or y == -11
            pix[(x, y)] = IRON_LIT if edge else RUST if (x + y) % 5 else RUST_LIT
    for y in range(-8, 8, 4):
        for x in (-8, 8):
            pix[(x, y)] = BONE
            pix[(x + (1 if x > 0 else -1), y)] = BONE
    pix[(0, -2)] = BONE
    return outlined(pix)


def paves():
    pix = {}
    for y in range(-13, 14):
        for x in range(-8, 9):
            edge = abs(x) == 8 or abs(y) == 13
            pix[(x, y)] = IRON_LIT if edge else IRON if x % 4 else IRON_DARK
    for y in range(-7, 8):
        pix[(0, y)] = RUST_LIT
    for x in range(-5, 6):
        pix[(x, -2)] = RUST_LIT
    return outlined(pix)


def censer():
    pix = {}
    chain(pix, 0, -11, 0, -6)
    for y in range(-5, 6):
        half = int(round(math.sqrt(max(0, 30 - y * y))))
        for x in range(-half, half + 1):
            pix[(x, y)] = RUST if abs(y) > 1 else IRON_LIT
    for x in (-3, 0, 3):
        pix[(x, -1)] = EMBER_LIT
        pix[(x, 1)] = EMBER
    return outlined(pix)


def chain_icon():
    pix = {}
    for i in range(4):
        cx, cy = -7 + i * 5, 7 - i * 5
        for a in range(0, 360, 30):
            pix[(int(round(cx + 3 * math.cos(math.radians(a)))), int(round(cy + 3 * math.sin(math.radians(a)))))] = IRON_LIT if i % 2 else RUST_LIT
    return outlined(pix)


def repair():
    pix = {}
    for y in range(-5, 6):
        half = 5 if y < 2 else 5 - (y - 1)
        for x in range(-half, half + 1):
            pix[(x, y)] = RUST if (x + y) % 3 else RUST_LIT
    for t in range(-4, 5):
        pix[(t, t)] = BONE
    return outlined(pix)


def charge():
    pix = {}
    for y in range(-5, 6):
        for x in range(-4, 5):
            pix[(x, y)] = IRON_LIT if abs(x) == 4 or abs(y) == 5 else IRON
    pix[(0, 0)] = EMBER_LIT
    return outlined(pix)


def sheet(cells, cell, filename, seed):
    cols = 10
    rows = (len(cells) + cols - 1) // cols
    im = Image.new('RGBA', (cols * cell * PX, rows * cell * PX))
    sprites = []
    for i, (name, pix) in enumerate(cells):
        cx, cy = (i % cols) * cell, (i // cols) * cell
        for (x, y), c in pix.items():
            px, py = cx + cell // 2 + x, cy + cell // 2 + y
            if 0 <= px - cx < cell and 0 <= py - cy < cell:
                im.paste(c, (px * PX, py * PX, px * PX + PX, py * PX + PX))
        sprites.append((name, cx * PX, (rows - 1 - i // cols) * cell * PX, cell * PX, cell * PX,
                        ((cell // 2 + 0.5) / cell, 1 - (cell // 2 + 0.5) / cell)))
    path = os.path.join(OUT, filename)
    im.save(path)
    random.seed(seed)
    write_meta(ROOT, path + '.meta', sprites)


def main():
    swings = []
    for great in (False, True):
        for deg in ANGLES:
            swings.append((('Freny great flail_%d' if great else 'Freny flail_%d') % deg, flail(great, deg)))
    sheet(swings, 2 * 36 + 2, 'flails.png', 1616)
    icons = [
        ('Freny flail', flail(False, 45)), ('Freny great flail', flail(True, 45)),
        ('Mangual', flail(False, 45)), ('Mangual tres', flail(True, 45)),
        ('Escudo', shield()), ('Paves', paves()), ('Incensario', censer()), ('Corrente', chain_icon()),
        ('Reparo', repair()), ('Carga', charge()),
    ]
    sheet(icons, 2 * 36 + 2, 'flagelante.png', 1717)


main()
