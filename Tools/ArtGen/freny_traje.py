# The Traje Berserker (José, 2026-11-02): Freny's first whole skin, built from the "Player alt" art.
# Writes one frame per body frame he can show, named after it, into
# Assets/Sprites/Characters/Player/Resources/FrenyLook/traje.png (+ .meta); FrenyMotion draws the
# frame with the same name over the Animator's. A frame the alt art has (ALT) takes its body from
# there; the ones it lacks (the cuts, the dash, the kick, the poses) are the normal frame with the
# shirt painted as the alt's bare chest and harness. Either way the helmet stays where the normal
# frame has it, because the head layers (freny_heads.py: broken horn, whole with armor, Rage) are
# cut per normal frame and sit on it.
#   python freny_traje.py <project root>
import os
import random
import sys

from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from freny_heads import (BLACK, DARK, LIGHT, MID, ORANGE, PLUM, PX, SHADE, WHITE, helmet, repaint, sprites_of)
from sheetmeta import write_meta

ROOT = sys.argv[1]
PLAYER = os.path.join(ROOT, 'Assets/Sprites/Characters/Player')
ALT_DIR = os.path.join(ROOT, 'Assets/Sprites/Characters/Player alt')
OUT = os.path.join(PLAYER, 'Resources/FrenyLook/traje.png')

# every sheet the Animator plays (Player_Animator's clips), then FrenyMotion's poses
SHEETS = ['Player idle', 'Player walk', 'Player jump', 'Player jump ranged', 'Player hit', 'Player crouched',
          'Player dash', 'Player kick', 'Player ranged', 'Player melee long', 'Player slash', 'Player death',
          'Resources/FrenyPoses/poses']
# normal sheet -> the alt sheet with the same frames
ALT = {'Player idle': 'Player idle', 'Player walk': 'Player walk', 'Player jump': 'Player jump',
       'Player hit': 'Player hit', 'Player crouched': 'Player crouch', 'Player ranged': 'Player ranged',
       'Player melee long': 'Player melee long', 'Player death': 'Player death'}

# which alt frame goes with each normal frame, where the two breathe out of step: idle 1_0 has the
# head up and idle 1_1 down, the alt's idle the other way round. With i % len(alt) the alt's horn
# tips stuck out under the helmet and the chest climbed over the neck (José, 2026-11-02)
ALT_FRAMES = {'Player idle': [1, 0, 0]}

SKIN, SKIN_SHADE, STRAP = ORANGE, (187, 95, 16), (71, 52, 28)
SHIRT = (LIGHT, MID, SHADE, WHITE)
HELMET_COLOURS = {LIGHT: PLUM, MID: DARK, SHADE: BLACK, WHITE: PLUM}


def cells(path):
    img = Image.open(path + '.png').convert('RGBA')
    out = []
    for name, r, pivot in sprites_of(path + '.png.meta'):
        x0, y0 = int(r['x']), int(img.height - r['y'] - r['height'])
        out.append((name, img.crop((x0, y0, x0 + int(r['width']), y0 + int(r['height']))), r, pivot))
    return out


def art(cell):
    """art pixels {(x, y): rgb} of a cell"""
    return {(x, y): cell.getpixel((x * PX, y * PX))[:3] for x in range(cell.width // PX) for y in range(cell.height // PX)
            if cell.getpixel((x * PX, y * PX))[3] > 0}


def to_cell(pix, size):
    out = Image.new('RGBA', size)
    for (x, y), c in pix.items():
        out.paste(c + (255,), (x * PX, y * PX, x * PX + PX, y * PX + PX))
    return out


def bare_chest(pix, head):
    """the normal frame's shirt as the alt's chest: skin, the shaded side, the harness' X, marked knees"""
    neck = max(y for _, y in head) + 1
    xs = sorted(x for x, _ in head)
    centre = (xs[0] + xs[-1] + 1) // 2
    out = dict(pix)
    below = [p for p in pix if p not in head and p[1] >= neck]
    shirt = {p for p in below if pix[p] in SHIRT}
    if not shirt:
        return out
    # the chest runs down the middle column, the tie's dark pixels included
    rows = [y for y in sorted({y for _, y in shirt}) if any((x, y) in shirt for x in range(centre - 3, centre + 4))]
    top, bottom = rows[0], min(rows[-1], rows[0] + 8)        # the dash's smear runs down to his feet
    for p in below:
        if pix[p] in SHIRT:
            out[p] = SKIN_SHADE if pix[p] == SHADE else SKIN
        elif top <= p[1] <= bottom and abs(p[0] - centre) <= 2 and pix[p] in (PLUM, DARK):
            out[p] = SKIN                                   # the tie
    height = bottom - top + 1
    for i in range(height):
        reach = round(4 - 8 * i / max(1, height - 1))
        for x in (centre + reach, centre - reach):
            if out.get((x, top + i)) in (SKIN, SKIN_SHADE):
                out[(x, top + i)] = STRAP
    # the knees: a light patch on each leg, half way down what is below the chest
    legs = [p for p in below if p[1] > bottom and pix[p] in (PLUM, DARK)]
    if legs:
        knee = bottom + (max(y for _, y in legs) - bottom) // 2
        row = sorted(x for x, y in legs if y == knee)
        start = None
        for x, nxt in zip(row, row[1:] + [None]):
            start = x if start is None else start
            if nxt != x + 1:
                mid = (start + x) // 2
                out[(mid, knee)] = LIGHT
                if (mid, knee + 1) in out:
                    out[(mid, knee + 1)] = LIGHT
                start = None
    return out


def helmet_only(pix, head):
    """the helmet's pixels in the Berserker's colours, for whatever the head layer doesn't cover"""
    return {p: repaint(pix[p], HELMET_COLOURS) for p in head}


def dressed(cell, alt_cell):
    pix = art(cell)
    if len(set(pix.values())) < 3:
        return cell                                         # the white hit flash stays his outline
    # the fall's last frames lie too low for helmet()'s search: no helmet there either
    try:
        head = helmet(cell)
    except IndexError:
        head = None
    if head is None:
        return alt_cell if alt_cell is not None else to_cell(pix, cell.size)   # the fall: nothing sits on it
    if alt_cell is None:
        out = bare_chest(pix, head)
    else:
        # the alt's body, its own helmet and horns out, the normal helmet's place in
        neck = max(y for _, y in head) + 1
        xs = [x for x, _ in head]
        out = {p: c for p, c in art(alt_cell).items() if not (p[1] < neck and min(xs) - 4 <= p[0] <= max(xs) + 4)}
    out.update(helmet_only(pix, head))
    return to_cell(out, cell.size)


def main():
    tiles = []
    for sheet in SHEETS:
        normal = cells(os.path.join(PLAYER, sheet))
        alt = cells(os.path.join(ALT_DIR, ALT[sheet])) if sheet in ALT else []
        for i, (name, cell, r, pivot) in enumerate(normal):
            frames = ALT_FRAMES.get(sheet)
            alt_cell = alt[frames[i] if frames else i % len(alt)][1] if alt else None
            if alt_cell is not None and alt_cell.size != cell.size:
                alt_cell = None
            tiles.append((name, dressed(cell, alt_cell), pivot))
    # the frames stay whole cells: the death's Ragdoll slices them by fractions of the cell.
    # A full 2048 sheet goes on in traje2.png (the shotgun poses, 2026-11-05); FrenyLook reads both
    size, x, y, page, pages = 2048, 0, 0, [], []
    for name, tile, pivot in tiles:
        if x + tile.width > size:
            x, y = 0, y + 200
        if y + 200 > size:
            pages.append(page)
            x, y, page = 0, 0, []
        page.append((name, tile, x, y, pivot))
        x += tile.width
    pages.append(page)
    for n, placed in enumerate(pages):
        out = OUT if n == 0 else OUT.replace('traje.png', 'traje%d.png' % (n + 1))
        height = size if n == 0 else max(py for _, _, _, py, _ in placed) + 200
        atlas = Image.new('RGBA', (size, height))
        sprites = []
        for name, tile, px, py, pivot in placed:
            atlas.paste(tile, (px, py))
            sprites.append((name, px, height - py - tile.height, tile.width, tile.height, pivot))
        atlas.save(out)
        random.seed(2026 + n)
        write_meta(ROOT, out + '.meta', sprites)
        print(os.path.basename(out), len(sprites), 'frames')


main()
