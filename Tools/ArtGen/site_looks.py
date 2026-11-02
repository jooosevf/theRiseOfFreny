"""Cuts Freny's look layers for the site's Perfil (freny-online, perfil/*.png): his first idle
frame, the shirt mask and each helmet, on one same canvas, so the page can stack them.

    python Tools/ArtGen/site_looks.py <freny-online folder>
"""
import os
import re
import sys

from PIL import Image

ROOT = os.path.join(os.path.dirname(__file__), '..', '..', 'Assets', 'Sprites', 'Characters', 'Player')
FRAME = 'Player idle 1_0'
FULL = 320  # a canvas every layer fits in, with the frame's pivot in its centre


def sprite(path):
    """The frame out of a sheet, and its pivot in pixels from its bottom left."""
    meta = open(path + '.meta', encoding='utf-8').read()
    m = re.search(r'name: ' + re.escape(FRAME) + r'\s*\n\s*rect:\s*\n\s*serializedVersion: 2\s*\n\s*x: (\d+)\s*\n\s*y: (\d+)\s*\n'
                  r'\s*width: (\d+)\s*\n\s*height: (\d+)\s*\n\s*alignment: (\d+)\s*\n\s*pivot: \{x: ([-\d.]+), y: ([-\d.]+)\}', meta)
    if not m:
        raise SystemExit('no ' + FRAME + ' in ' + path)
    x, y, w, h = (int(m.group(i)) for i in range(1, 5))
    px, py = float(m.group(6)), float(m.group(7))
    sheet = Image.open(path).convert('RGBA')
    crop = sheet.crop((x, sheet.height - y - h, x + w, sheet.height - y))
    return crop, px * w, py * h


def place(crop, pivot_x, pivot_y):
    # the layer's pivot on the frame's pivot, as FrenyLookPreview does
    canvas = Image.new('RGBA', (FULL, FULL), (0, 0, 0, 0))
    canvas.alpha_composite(crop, (round(FULL / 2 - pivot_x), round(FULL / 2 - (crop.height - pivot_y))))
    return canvas


def main(out_root):
    out = os.path.join(out_root, 'perfil')
    os.makedirs(out, exist_ok=True)
    layers = {'freny': 'Player idle.png', 'shirt': 'Resources/FrenyLook/shirt.png'}
    for art in ('gold', 'ice', 'viking', 'shadow', 'crown', 'cyclops'):
        layers['helmet_' + art] = 'Resources/FrenyLook/helmet_' + art + '.png'
    layers['helmet_berserker'] = 'Resources/FrenyHeads/berserker_broken.png'
    placed = {name: place(*sprite(os.path.join(ROOT, path))) for name, path in layers.items()}

    boxes = [image.getbbox() for image in placed.values()]
    box = (min(b[0] for b in boxes), min(b[1] for b in boxes), max(b[2] for b in boxes), max(b[3] for b in boxes))
    side = max(box[2] - box[0], box[3] - box[1]) + 4
    left = (box[0] + box[2] - side) // 2
    top = (box[1] + box[3] - side) // 2
    window = (left, top, left + side, top + side)
    for name, image in placed.items():
        image.crop(window).save(os.path.join(out, name + '.png'))
    print('canvas', side, 'freny at', [v - w for v, w in zip(placed['freny'].getbbox(), (left, top, left, top))])


if __name__ == '__main__':
    main(sys.argv[1])
