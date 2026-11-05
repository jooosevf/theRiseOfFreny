"""Provisional art for the test dungeon's doors, save point, levers and gate, drawn pixel by pixel
at the art bible's scale (1 art pixel = 4 texels, PPU 100). Dark stone, black iron, a red ember.
Run: python world_art.py. Writes Assets/Sprites/Items/Resources/World/ and a .meta for new files."""
import os, uuid
from PIL import Image

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
OUT = os.path.join(REPO, 'Assets', 'Sprites', 'Items', 'Resources', 'World')
SAMPLE = os.path.join(REPO, 'Assets', 'Sprites', 'Items', 'Resources', 'Fase4', 'Mage Staff.png.meta')
SCALE = 4

STONE = (58, 56, 62, 255)
STONE_LIGHT = (84, 80, 88, 255)
STONE_DARK = (34, 32, 38, 255)
WOOD = (52, 34, 28, 255)
WOOD_DARK = (34, 22, 20, 255)
IRON = (44, 44, 50, 255)
IRON_LIGHT = (88, 88, 98, 255)
EMBER = (196, 40, 44, 255)
EMBER_CORE = (246, 150, 70, 255)
CLEAR = (0, 0, 0, 0)


def canvas(w, h):
    return [[CLEAR for _ in range(w)] for _ in range(h)]


def rect(px, x0, y0, x1, y1, color):
    for y in range(max(0, y0), min(len(px), y1 + 1)):
        for x in range(max(0, x0), min(len(px[0]), x1 + 1)):
            px[y][x] = color


def save(px, name):
    h, w = len(px), len(px[0])
    img = Image.new('RGBA', (w * SCALE, h * SCALE))
    for y in range(h):
        for x in range(w):
            for dy in range(SCALE):
                for dx in range(SCALE):
                    img.putpixel((x * SCALE + dx, y * SCALE + dy), px[y][x])
    os.makedirs(OUT, exist_ok=True)
    path = os.path.join(OUT, name + '.png')
    img.save(path)
    meta = path + '.meta'
    if not os.path.exists(meta):
        text = open(SAMPLE, encoding='utf-8').read()
        lines = text.split('\n')
        lines[1] = 'guid: ' + uuid.uuid4().hex
        text = '\n'.join(lines)
        text = text.replace('alignment: 9', 'alignment: 0').replace('spritePivot: {x: 0.5, y: 0.15}', 'spritePivot: {x: 0.5, y: 0.5}')
        text = text.replace('spriteID: 4fb277f43f8a4f21b7973cc61984d5d0', 'spriteID: ' + uuid.uuid4().hex)
        open(meta, 'w', encoding='utf-8', newline='\n').write(text)


def door():
    w, h = 36, 60
    px = canvas(w, h)
    rect(px, 0, 4, w - 1, h - 1, STONE)                    # the frame
    rect(px, 2, 0, w - 3, 3, STONE)
    rect(px, 0, 4, 1, h - 1, STONE_LIGHT)
    rect(px, 5, 7, w - 6, h - 1, WOOD)                     # the door
    for x in range(9, w - 6, 6):
        rect(px, x, 7, x, h - 1, WOOD_DARK)
    for y in (15, 44):
        rect(px, 5, y, w - 6, y + 2, IRON)
        rect(px, 5, y, w - 6, y, IRON_LIGHT)
    rect(px, w - 11, 29, w - 9, 32, IRON_LIGHT)            # the ring
    rect(px, 16, 2, 19, 5, EMBER)                          # the ember over it
    rect(px, 17, 3, 18, 4, EMBER_CORE)
    return px


def save_point():
    w, h = 26, 50
    px = canvas(w, h)
    rect(px, 2, 42, w - 3, h - 1, STONE)                   # the altar
    rect(px, 0, 46, w - 1, h - 1, STONE_DARK)
    rect(px, 2, 42, w - 3, 42, STONE_LIGHT)
    rect(px, 8, 18, w - 9, 41, STONE)                      # the column
    rect(px, 8, 18, 9, 41, STONE_LIGHT)
    rect(px, 5, 14, w - 6, 17, STONE_LIGHT)                # the bowl
    rect(px, 9, 5, w - 10, 13, EMBER)                      # the flame
    rect(px, 11, 1, w - 12, 5, EMBER)
    rect(px, 11, 8, w - 12, 13, EMBER_CORE)
    return px


def lever(on):
    w, h = 22, 34
    px = canvas(w, h)
    rect(px, 3, 26, w - 4, h - 1, STONE)                   # the base
    rect(px, 3, 26, w - 4, 26, STONE_LIGHT)
    if on:
        for i in range(14):                                # pulled to the right, the ember lit
            rect(px, 10 + i // 2, 25 - i, 11 + i // 2, 25 - i, IRON_LIGHT)
        rect(px, 14, 6, 19, 11, EMBER)
        rect(px, 16, 7, 18, 10, EMBER_CORE)
    else:
        for i in range(14):
            rect(px, 10 - i // 2, 25 - i, 11 - i // 2, 25 - i, IRON_LIGHT)
        rect(px, 1, 6, 6, 11, IRON)
    return px


def gate():
    w, h = 25, 75
    px = canvas(w, h)
    for x in (2, 8, 14, 20):
        rect(px, x, 0, x + 2, h - 1, IRON)
        rect(px, x, 0, x, h - 1, IRON_LIGHT)
        rect(px, x, h - 4, x + 2, h - 1, STONE_DARK)       # the points
    for y in (8, 26, 44, 62):
        rect(px, 0, y, w - 1, y + 2, IRON)
        rect(px, 0, y, w - 1, y, IRON_LIGHT)
    return px


if __name__ == '__main__':
    save(door(), 'Door')
    save(save_point(), 'Save Point')
    save(lever(False), 'Lever Off')
    save(lever(True), 'Lever On')
    save(gate(), 'Gate')
    print('wrote', OUT)
