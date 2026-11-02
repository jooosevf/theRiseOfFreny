# The Ciclope's treasure (José, 2026-11-01): the boss key it can drop and the golden chest that
# only that key opens. Both in the game's art pixels drawn at 4x, like the chest they come from.
#
#   python3 boss_loot.py <repo root>
#
# Writes into Assets/Sprites/Items/Resources/BossLoot (loaded by BossKey and GoldenChest):
# - "Golden Chest.png": Items/Chest 100x100.png (4 frames: shut, bursting, open, open) recoloured
#   from wood to gold, its gold trim to a pale gold and its glints to white, so it reads as the
#   normal chest's rich cousin and keeps the same opening animation;
# - "Boss Key.png": a big gold key whose bow is the Ciclope's purple eye, 6 frames of 80x48 with
#   a glint running along it (frames 0 and 5 plain, so it shines now and then while it loops).
# The .meta files (Point filter, uncompressed, sliced) keep their guid on regeneration.
import os, sys
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from sheetmeta import write_meta

ROOT = sys.argv[1]
OUT = os.path.join(ROOT, 'Assets/Sprites/Items/Resources/BossLoot')
PX = 4

# the chest's palette -> gold
RECOLOUR = {
    (81, 41, 26): (178, 118, 24),     # wood -> deep gold
    (94, 53, 29): (219, 160, 38),     # wood light -> gold
    (219, 165, 40): (255, 236, 150),  # gold trim -> pale gold
    (251, 242, 54): (255, 255, 236),  # glints -> white
    (36, 25, 25): (52, 26, 48),       # the dark inside -> a purple shade, the Ciclope's colour
}

GOLD_LO, GOLD, GOLD_HI, WHITE = (140, 92, 24), (219, 163, 42), (255, 216, 74), (255, 252, 230)
INK = (40, 22, 30)
EYE_W, IRIS, PUPIL = (238, 232, 240), (150, 70, 200), (30, 12, 40)


def golden_chest():
    src = Image.open(os.path.join(ROOT, 'Assets/Sprites/Items/Chest 100x100.png')).convert('RGBA')
    out = src.copy()
    px = out.load()
    for y in range(out.height):
        for x in range(out.width):
            r, g, b, a = px[x, y]
            if a and (r, g, b) in RECOLOUR:
                px[x, y] = RECOLOUR[(r, g, b)] + (a,)
    path = os.path.join(OUT, 'Golden Chest.png')
    out.save(path)
    write_meta(ROOT, path + '.meta', [(f'Golden Chest_{i}', i * 100, 0, 100, 100, (0.5, 0.5)) for i in range(4)])


def key_frame(glint):
    """20x12 art pixels: the eye bow on the left, the shaft and two teeth on the right"""
    W, H = 20, 12
    img = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    p = img.load()

    def put(x, y, c):
        if 0 <= x < W and 0 <= y < H:
            p[x, y] = c + (255,)

    # the bow: a ring of radius ~4.5 around (6, 6), outlined in ink
    cx, cy = 6, 6
    for y in range(H):
        for x in range(W):
            d = ((x - cx) ** 2 + (y - cy) ** 2) ** 0.5
            if d <= 5.3:
                put(x, y, INK)
            if d <= 4.4:
                put(x, y, GOLD if y >= cy else GOLD_HI)
            if d <= 2.9:
                put(x, y, EYE_W)
    # the eye inside: purple iris, black pupil, a white glint
    for (x, y) in [(5, 5), (6, 5), (7, 5), (5, 6), (6, 6), (7, 6), (5, 7), (6, 7), (7, 7)]:
        put(x, y, IRIS)
    put(6, 6, PUPIL)
    put(6, 7, PUPIL)
    put(5, 5, WHITE)
    # shading on the ring's lower right
    for (x, y) in [(9, 8), (8, 9), (9, 7), (7, 10), (10, 6)]:
        put(x, y, GOLD_LO)
    # the shaft
    for x in range(11, 19):
        put(x, 4, INK)
        put(x, 5, GOLD_HI)
        put(x, 6, GOLD)
        put(x, 7, INK)
    put(19, 5, INK)
    put(19, 6, INK)
    # two teeth hanging under the shaft
    for tx in (14, 17):
        for y in (8, 9):
            put(tx, y, GOLD_LO)
            put(tx + 1, y, GOLD)
        put(tx - 1, 8, INK)
        put(tx - 1, 9, INK)
        put(tx + 2, 8, INK)
        put(tx + 2, 9, INK)
        put(tx, 10, INK)
        put(tx + 1, 10, INK)
    # the glint: a short white diagonal sweeping left to right
    if glint is not None:
        for k in range(3):
            x, y = glint + k, 8 - k * 2
            for yy in (y, y + 1):
                if 0 <= x < W and 0 <= yy < H and p[x, yy][3] and p[x, yy][:3] not in (INK, PUPIL, IRIS):
                    p[x, yy] = WHITE + (255,)
    return img.resize((W * PX, H * PX), Image.NEAREST)


def boss_key():
    glints = [None, 2, 6, 11, 16, None]
    sheet = Image.new('RGBA', (80 * len(glints), 48), (0, 0, 0, 0))
    for i, g in enumerate(glints):
        sheet.paste(key_frame(g), (i * 80, 0))
    path = os.path.join(OUT, 'Boss Key.png')
    sheet.save(path)
    write_meta(ROOT, path + '.meta', [(f'Boss Key_{i}', i * 80, 0, 80, 48, (0.5, 0.5)) for i in range(len(glints))])


def uncompressed(meta):
    text = open(meta).read().replace('textureCompression: 1', 'textureCompression: 0')
    open(meta, 'w', newline='\n').write(text)


os.makedirs(OUT, exist_ok=True)
golden_chest()
boss_key()
for name in ('Golden Chest.png.meta', 'Boss Key.png.meta'):
    uncompressed(os.path.join(OUT, name))
print('ok')
