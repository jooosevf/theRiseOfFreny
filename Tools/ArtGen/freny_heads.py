# Builds Freny's swappable helmets: for every frame of every sheet her Animator plays, finds the
# grey helmet (the pixels above the orange neck, flood-filled from the top of the figure) and
# puts the HUD's helmet icons on it (Items 100x100_6 for Rage, _7 for Berserker, and _7 in red
# for Rage with the Berserker armor), the same art the life bar shows. Grey pixels an icon leaves uncovered are repainted in its colours. Each
# helmet becomes one small sprite cropped to the head and pivoted so that, drawn on the body's
# transform, it sits exactly on the old helmet.
#
#   python3 freny_heads.py <repo root>
#
# Writes Assets/Sprites/Characters/Player/Resources/FrenyHeads/{rage,berserker,rage_berserker,berserker_broken}.png + .meta
# (and the HUD icons rage_berserker_icon.png, the red Berserker, and berserker_broken_icon.png, the cosmetic one-horn helmet); FrenyMotion loads them by the
# body sprite's name. Rerun it after changing the icons or any of her sheets. Frames with no helmet (white hit flash, death) get no head.
import os, re, sys, random
from PIL import Image
from sheetmeta import write_meta

ROOT = sys.argv[1]
PLAYER = os.path.join(ROOT, 'Assets/Sprites/Characters/Player')
OUT = os.path.join(ROOT, 'Assets/Sprites/Characters/Player/Resources/FrenyHeads')
PX = 4                                   # art pixel size in texels
SHEETS = ['Player idle', 'Player walk', 'Player jump', 'Player jump ranged', 'Player hit',
          'Player crouched', 'Player dash', 'Player kick', 'Player ranged', 'Player melee long',
          'Player slash', 'Resources/FrenyPoses/poses']

ORANGE = (202, 117, 56)
LIGHT, MID, SHADE, WHITE = (176, 162, 141), (129, 111, 109), (99, 86, 85), (239, 239, 233)
PLUM, DARK, BLACK = (69, 40, 60), (48, 27, 42), (26, 17, 25)
RED, DEEP_RED, PALE_RED = (208, 47, 47), (150, 32, 40), (240, 120, 105)

# the HUD icons drawn on the head: cell index in Items 100x100.png, and a recolour
RED_BERSERKER = {PLUM: RED, BLACK: PLUM}     # the horns stay bone
ICONS = {'rage': (6, {}), 'berserker': (7, {}), 'rage_berserker': (7, RED_BERSERKER), 'berserker_broken': (7, {})}
# the grey helmet's colours, for any helmet pixel an icon doesn't cover
PALETTES = {
    'rage': {LIGHT: RED, MID: DEEP_RED, SHADE: PLUM, WHITE: PALE_RED},
    'berserker': {LIGHT: PLUM, MID: DARK, SHADE: BLACK, WHITE: PLUM},
    'rage_berserker': {LIGHT: RED, MID: DEEP_RED, SHADE: PLUM, WHITE: RED},
    # the cosmetic Berserker helmet without its armor (Loadout.Cosmetic): one horn snapped off
    'berserker_broken': {LIGHT: PLUM, MID: DARK, SHADE: BLACK, WHITE: PLUM},
}
BONE = (176, 162, 141)


def break_horn(icon):
    """the icon with its right horn snapped off: only a short stump by the helmet stays"""
    pix, w, h = icon
    stump = lambda x, y: (x == w - 3 and y <= 4) or (x == w - 2 and y == 3)
    out = {p: c for p, c in pix.items() if not (c == BONE and p[0] >= w - 3 and not stump(*p))}
    return out, w, h
ALIGN = {0: (.5, .5), 1: (0, 1), 2: (.5, 1), 3: (1, 1), 4: (0, .5), 5: (1, .5), 6: (0, 0), 7: (.5, 0), 8: (1, 0)}


def sprites_of(meta_path):
    meta = open(meta_path).read()
    if re.search(r'\n  spriteMode: 1\n', meta):            # Single: the whole texture is one sprite
        w, h = Image.open(meta_path[:-len('.meta')]).size
        align = int(re.search(r'\n  alignment: (\d+)', meta).group(1))
        p = re.search(r'\n  spritePivot: \{x: ([\d.\-e]+), y: ([\d.\-e]+)\}', meta)
        pivot = (float(p.group(1)), float(p.group(2))) if align == 9 else ALIGN[align]
        yield os.path.basename(meta_path)[:-len('.png.meta')], {'x': 0, 'y': 0, 'width': w, 'height': h}, pivot
        return
    sheet = meta[meta.index('  spriteSheet:'):]
    for block in re.split(r'\n    - serializedVersion: 2\n', sheet)[1:]:
        name = re.search(r'name: (.+)', block).group(1).strip()
        r = {k: float(re.search(r'\n        %s: ([\d.\-]+)' % k, block).group(1)) for k in ('x', 'y', 'width', 'height')}
        align = int(re.search(r'\n      alignment: (\d+)', block).group(1))
        if align == 9:
            p = re.search(r'pivot: \{x: ([\d.\-e]+), y: ([\d.\-e]+)\}', block)
            pivot = (float(p.group(1)), float(p.group(2)))
        else:
            pivot = ALIGN[align]
        yield name, r, pivot


def helmet(cell):
    """art-pixel coordinates (x, y down) of the helmet in a cell image, or None"""
    w, h = cell.width // PX, cell.height // PX
    px = lambda x, y: cell.getpixel((x * PX, y * PX))
    opaque = lambda x, y: px(x, y)[3] > 0
    grey = lambda x, y: opaque(x, y) and px(x, y)[:3] in (LIGHT, MID, SHADE, WHITE)
    orange = lambda x, y: opaque(x, y) and px(x, y)[:3] == ORANGE
    if len({px(x, y)[:3] for x in range(w) for y in range(h) if opaque(x, y)}) < 3:
        return None                                   # the white hit flash
    # the neck: the highest orange pixel with helmet right on top of it (hands are orange too,
    # but have coat or nothing above them)
    def top_near(x):   # highest opaque pixel within a head's width of column x
        return min((y for y in range(h) for c in range(max(0, x - 6), min(w, x + 7)) if opaque(c, y)), default=h)
    necks = [(y, x) for y in range(1, h) for x in range(w)
             if orange(x, y) and grey(x, y - 1) and y - top_near(x) <= 12]
    if necks:
        neck, xc = min(necks)
        inside = lambda x, y: y < neck and opaque(x, y)
        seed = (xc, neck - 1)
    else:
        # crouched: the helmet sits on the coat with no neck showing; take the grey block on top,
        # at most 8 rows tall and as wide as its upper rows
        top = next((y for y in range(h) if any(opaque(x, y) for x in range(w))), None)
        if top is None or not any(grey(x, top) for x in range(w)):
            return None
        xs = [x for y in range(top, top + 4) for x in range(w) if grey(x, y)]
        lo, hi = min(xs) - 1, max(xs) + 1
        inside = lambda x, y: top <= y < top + 8 and lo <= x <= hi and grey(x, y)
        seed = next((x, top) for x in range(w) if grey(x, top))
    seen, stack = {seed}, [seed]
    while stack:
        x, y = stack.pop()
        for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
            if 0 <= nx < w and 0 <= ny < h and (nx, ny) not in seen and inside(nx, ny):
                seen.add((nx, ny))
                stack.append((nx, ny))
    # a raised arm (climbing) can touch the helmet: keep the 9 columns holding most of its light
    # paint, the helmet's own width
    light = lambda x, y: px(x, y)[:3] in (LIGHT, WHITE)
    left = max(range(min(x for x, _ in seen), max(x for x, _ in seen) + 1),
               key=lambda l: sum(1 for x, y in seen if l <= x < l + 9 and light(x, y)))
    seen = {(x, y) for x, y in seen if left <= x < left + 9}
    return seen if len(seen) >= 12 else None


def repaint(c, pal):
    """the helmet colour's counterpart; shades the old palette lacks go to the nearest by brightness"""
    if c in pal:
        return pal[c]
    lum = lambda k: 0.3 * k[0] + 0.59 * k[1] + 0.11 * k[2]
    return pal[min(pal, key=lambda k: abs(lum(k) - lum(c)))]


def icon_art(index, recolour):
    """the icon's art pixels {(x, y): rgb}, cropped to the drawing and recoloured, and its size"""
    items = Image.open(os.path.join(ROOT, 'Assets/Sprites/Items/Items 100x100.png')).convert('RGBA')
    cell = items.crop((index * 100, 0, index * 100 + 100, 100))
    cell = cell.crop(cell.getbbox())
    w, h = cell.width // PX, cell.height // PX
    pix = {(x, y): cell.getpixel((x * PX, y * PX))[:3] for x in range(w) for y in range(h)
           if cell.getpixel((x * PX, y * PX))[3] > 0}
    return {p: recolour.get(c, c) for p, c in pix.items()}, w, h


def save_icon(icon, name):
    """an icon as its own tight sprite, for the HUD portrait"""
    pix, w, h = icon
    im = Image.new('RGBA', (w * PX, h * PX))
    for (x, y), c in pix.items():
        im.paste(c + (255,), (x * PX, y * PX, x * PX + PX, y * PX + PX))
    path = os.path.join(OUT, name + '_icon.png')
    im.save(path)
    random.seed(7)
    write_meta(ROOT, path + '.meta', [('Freny ' + name.replace('_', ' ') + ' helmet', 0, 0, im.width, im.height, (0.5, 0.5))])


def put_icon(head, icon):
    """the icon centred over the helmet's columns and resting on its top row"""
    art, w, _ = icon
    xs = [x for x, _ in head]; ys = [y for _, y in head]
    ox = (min(xs) + max(xs) + 1 - w) // 2
    oy = min(ys)
    return {(x + ox, y + oy): c for (x, y), c in art.items()}


def main():
    os.makedirs(OUT, exist_ok=True)
    icons = {kind: icon_art(i, recolour) for kind, (i, recolour) in ICONS.items()}
    icons['berserker_broken'] = break_horn(icons['berserker_broken'])
    entries = []                       # (name, pixels {(x,y): rgb per helmet}, origin, pivot px)
    for sheet in SHEETS:
        img = Image.open(os.path.join(PLAYER, sheet + '.png')).convert('RGBA')
        for name, r, pivot in sprites_of(os.path.join(PLAYER, sheet + '.png.meta')):
            # meta rects are bottom-up; PIL is top-down
            x0, y0 = int(r['x']), int(img.height - r['y'] - r['height'])
            cell = img.crop((x0, y0, x0 + int(r['width']), y0 + int(r['height'])))
            head = helmet(cell)
            if head is None:
                continue
            per = {}
            for kind, pal in PALETTES.items():
                pix = {p: repaint(cell.getpixel((p[0] * PX, p[1] * PX))[:3], pal) for p in head}
                icon = put_icon(head, icons[kind])
                if 'climb' in name:     # seen from behind: the helmet's colours, only the horns stick out
                    icon = {p: c for p, c in icon.items() if p not in head}
                pix.update(icon)
                per[kind] = pix
            entries.append((name, per, r, pivot))

    random.seed(1978)
    for kind in PALETTES:
        tiles = []
        for name, per, r, pivot in entries:
            pix = per[kind]
            xs = [x for x, _ in pix]; ys = [y for _, y in pix]
            bx, by = min(xs), min(ys)
            tw, th = max(xs) - bx + 1, max(ys) - by + 1
            tile = Image.new('RGBA', (tw * PX, th * PX))
            for (x, y), c in pix.items():
                tile.paste(c + (255,), ((x - bx) * PX, (y - by) * PX, (x - bx + 1) * PX, (y - by + 1) * PX))
            # the body sprite's pivot, in texels from the tile's bottom-left
            pvx = r['width'] * pivot[0] - bx * PX
            pvy = r['height'] * pivot[1] - (r['height'] - (by + th) * PX)
            tiles.append((name, tile, (pvx / tile.width, pvy / tile.height)))
        cols, cw, ch = 10, 96, 96
        rows = (len(tiles) + cols - 1) // cols
        atlas = Image.new('RGBA', (cols * cw, rows * ch))
        sprites = []
        for i, (name, tile, piv) in enumerate(tiles):
            cx, cy = (i % cols) * cw, (i // cols) * ch
            atlas.paste(tile, (cx, cy))
            sprites.append((name, cx, atlas.height - cy - tile.height, tile.width, tile.height, piv))
        atlas.save(os.path.join(OUT, kind + '.png'))
        write_meta(ROOT, os.path.join(OUT, kind + '.png.meta'), sprites)
        print(kind, len(sprites), 'heads', atlas.size)
    save_icon(icons['rage_berserker'], 'rage_berserker')
    save_icon(icons['berserker_broken'], 'berserker_broken')



# freny_cosmetics.py imports the helmet finder without rebuilding these sheets
if __name__ == '__main__':
    main()
