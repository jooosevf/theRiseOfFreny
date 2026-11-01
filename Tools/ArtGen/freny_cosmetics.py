# Builds Freny's cosmetic layers for the Visual screen (FrenyLook): for every frame of every sheet
# his Animator plays, the same helmet freny_heads.py finds, repainted in each cosmetic helmet's
# colours (some with horns or a crown on top), and a "shirt" mask: the grey of his shirt and
# sleeves in light greys, which the game tints with the chosen colour (FrenyLookRenderer draws it
# over the body with SpriteRenderer.color, so one sheet serves every colour).
#
#   python3 freny_cosmetics.py <repo root> [helmet id ...]
#
# With helmet ids, only those helmet sheets are written (the others and the shirt keep their metas
# untouched), e.g. `cyclops` for the golden chest's Elmo do Ciclope.
#
# Writes Assets/Sprites/Characters/Player/Resources/FrenyLook/{helmet_<id>,shirt}.png + .meta,
# one sprite per body frame, named after that frame and pivoted on the body's pivot. The capes are
# not here: they are drawn at runtime (CapeArt.cs). Rerun after changing any of his sheets.
import os, sys, random
from PIL import Image
from sheetmeta import write_meta
import freny_heads as fh

ROOT = sys.argv[1]
OUT = os.path.join(ROOT, 'Assets/Sprites/Characters/Player/Resources/FrenyLook')
PX = fh.PX
LIGHT, MID, SHADE, WHITE = fh.LIGHT, fh.MID, fh.SHADE, fh.WHITE

GOLD = {LIGHT: (240, 200, 80), MID: (208, 160, 48), SHADE: (150, 104, 40), WHITE: (255, 240, 170)}
HELMETS = {
    'gold': GOLD,
    'ice': {LIGHT: (170, 220, 240), MID: (110, 160, 210), SHADE: (70, 100, 160), WHITE: (235, 250, 255)},
    'viking': {LIGHT: (190, 195, 205), MID: (130, 135, 150), SHADE: (85, 88, 105), WHITE: (235, 238, 245)},
    'shadow': {LIGHT: (70, 50, 90), MID: (48, 32, 64), SHADE: (28, 18, 40), WHITE: (190, 110, 250)},
    'crown': GOLD,
    # the golden chest's own (FrenyLook: Unlock.GoldenChest): the Ciclope's purples, with its eye
    'cyclops': {LIGHT: (112, 92, 196), MID: (79, 64, 159), SHADE: (54, 55, 120), WHITE: (160, 140, 230)},
}
BONE, BONE_SHADE, HORN_TIP = (239, 228, 200), (196, 180, 150), (120, 100, 80)
CROWN, CROWN_SHADE, JEWEL = (255, 214, 74), (196, 140, 40), (208, 47, 47)
SCLERA, SCLERA_SHADE, IRIS, PUPIL = (239, 228, 200), (196, 180, 150), (208, 47, 47), (99, 13, 38)
# the shirt's greys in the mask: white-ish, so the tint shows true and the shading stays
SHIRT = {LIGHT: (255, 255, 255), MID: (205, 205, 205), SHADE: (150, 150, 150)}


def addons(kind, head, frame):
    """pixels added around the helmet: horns (viking), a crown, or the Ciclope's eye"""
    xs = [x for x, _ in head]; ys = [y for _, y in head]
    lo, hi, top = min(xs), max(xs), min(ys)
    extra = {}
    if kind == 'viking':
        for side, edge in ((-1, lo), (1, hi)):
            for dx, dy, c in ((1, 3, BONE_SHADE), (1, 2, BONE), (2, 1, BONE), (2, 0, BONE), (2, -1, HORN_TIP)):
                extra[(edge + side * dx, top + dy)] = c
    elif kind == 'crown':
        mid = (lo + hi) // 2
        for x in range(lo + 1, hi):
            extra[(x, top - 1)] = CROWN_SHADE if x in (lo + 1, hi - 1) else CROWN
        for x in (lo + 1, mid, hi - 1):
            extra[(x, top - 2)] = CROWN
        extra[(mid, top - 1)] = JEWEL
    elif kind == 'cyclops' and 'climb' not in frame:
        # one big eye on the front of the helmet (he faces right in his sheets); none from behind
        for (dx, dy), c in {(-3, 3): SCLERA_SHADE, (-2, 3): SCLERA, (-1, 3): SCLERA,
                            (-3, 4): SCLERA, (-2, 4): IRIS, (-1, 4): PUPIL}.items():
            if (hi + dx, top + dy) in head:
                extra[(hi + dx, top + dy)] = c
    return extra


def shirt(cell, head):
    """the shirt and sleeves: the biggest grey patch outside the helmet, with any grey patch next to it"""
    w, h = cell.width // PX, cell.height // PX
    px = lambda x, y: cell.getpixel((x * PX, y * PX))
    grey = lambda x, y: px(x, y)[3] > 0 and px(x, y)[:3] in SHIRT and (x, y) not in head
    seen, patches = set(), []
    for y in range(h):
        for x in range(w):
            if (x, y) in seen or not grey(x, y):
                continue
            patch, stack = {(x, y)}, [(x, y)]
            seen.add((x, y))
            while stack:
                cx, cy = stack.pop()
                for nx, ny in ((cx + 1, cy), (cx - 1, cy), (cx, cy + 1), (cx, cy - 1)):
                    if 0 <= nx < w and 0 <= ny < h and (nx, ny) not in seen and grey(nx, ny):
                        seen.add((nx, ny))
                        patch.add((nx, ny))
                        stack.append((nx, ny))
            patches.append(patch)
    if not patches:
        return None
    body = max(patches, key=len)
    bx0 = min(x for x, _ in body) - 2; bx1 = max(x for x, _ in body) + 2
    by0 = min(y for _, y in body) - 2; by1 = max(y for _, y in body) + 2
    pix = set(body)
    for patch in patches:
        if patch is not body and len(patch) >= 2 and all(bx0 <= x <= bx1 and by0 <= y <= by1 for x, y in patch):
            pix |= patch
    return {p: SHIRT[px(*p)[:3]] for p in pix}


def atlas(name, entries):
    """entries: (frame name, {(x, y): rgb}, frame rect, pivot) -> one sheet, a sprite per frame"""
    tiles = []
    for frame, pix, r, pivot in entries:
        xs = [x for x, _ in pix]; ys = [y for _, y in pix]
        bx, by = min(xs), min(ys)
        tw, th = max(xs) - bx + 1, max(ys) - by + 1
        tile = Image.new('RGBA', (tw * PX, th * PX))
        for (x, y), c in pix.items():
            tile.paste(c + (255,), ((x - bx) * PX, (y - by) * PX, (x - bx + 1) * PX, (y - by + 1) * PX))
        pvx = r['width'] * pivot[0] - bx * PX
        pvy = r['height'] * pivot[1] - (r['height'] - (by + th) * PX)
        tiles.append((frame, tile, (pvx / tile.width, pvy / tile.height)))
    cw = max(t.width for _, t, _ in tiles)
    ch = max(t.height for _, t, _ in tiles)
    cols = max(1, 1024 // cw)
    rows = (len(tiles) + cols - 1) // cols
    sheet = Image.new('RGBA', (cols * cw, rows * ch))
    sprites = []
    for i, (frame, tile, piv) in enumerate(tiles):
        cx, cy = (i % cols) * cw, (i // cols) * ch
        sheet.paste(tile, (cx, cy))
        sprites.append((frame, cx, sheet.height - cy - tile.height, tile.width, tile.height, piv))
    path = os.path.join(OUT, name + '.png')
    sheet.save(path)
    random.seed(sum(map(ord, name)))   # stable internal ids
    write_meta(ROOT, path + '.meta', sprites)
    print(name, len(sprites), 'sprites', sheet.size)


def main():
    os.makedirs(OUT, exist_ok=True)
    only = sys.argv[2:]
    helmets = {kind: [] for kind in HELMETS if not only or kind in only}
    shirts = []
    for sheet in fh.SHEETS:
        img = Image.open(os.path.join(fh.PLAYER, sheet + '.png')).convert('RGBA')
        for name, r, pivot in fh.sprites_of(os.path.join(fh.PLAYER, sheet + '.png.meta')):
            x0, y0 = int(r['x']), int(img.height - r['y'] - r['height'])
            cell = img.crop((x0, y0, x0 + int(r['width']), y0 + int(r['height'])))
            head = fh.helmet(cell)
            if head is None:
                continue
            for kind in helmets:
                pal = HELMETS[kind]
                pix = {p: fh.repaint(cell.getpixel((p[0] * PX, p[1] * PX))[:3], pal) for p in head}
                pix.update(addons(kind, head, name))
                helmets[kind].append((name, pix, r, pivot))
            body = shirt(cell, head)
            if body:
                shirts.append((name, body, r, pivot))
    for kind, entries in helmets.items():
        atlas('helmet_' + kind, entries)
    if not only:
        atlas('shirt', shirts)


main()
