"""The Rastejante of the Poço de Ácido: a bloodshot eyeball on six spindly legs.

Writes Assets/Sprites/Characters/Mobs/Crawler/Resources/Crawler/
  Crawler Walk.png  4 frames, 32x32 each (legs stepping)
  Crawler Drop.png  2 frames (legs pulled in, then flung out: the drop off the ceiling)
imported at 32 px a unit, pivot in the middle (the body is 1 unit, the legs touch the bottom row).
Drawn facing right, standing on the floor; the game turns it round walls and ceilings.
Run from the repo root.
"""
import math
import os
import re
import sys

from PIL import Image

sys.path.insert(0, os.path.dirname(__file__))
from sheetmeta import write_meta  # noqa: E402

S = 32
OUT = "Assets/Sprites/Characters/Mobs/Crawler/Resources/Crawler/"

OUTLINE = (28, 34, 22, 255)
SCLERA = (232, 234, 214, 255)
SCLERA_SHADE = (186, 196, 160, 255)
SCLERA_DARK = (140, 150, 118, 255)
VEIN = (176, 46, 52, 255)
IRIS = (120, 206, 48, 255)
IRIS_DARK = (66, 130, 30, 255)
PUPIL = (16, 18, 12, 255)
SHINE = (255, 255, 255, 255)
LEG = (126, 94, 168, 255)      # the tentacle's purple
LEG_LIGHT = (178, 148, 216, 255)

CX, CY, R = 16, 12, 8          # the eyeball (y down)


def put(px, x, y, c):
    if 0 <= x < S and 0 <= y < S:
        px[x, y] = c


def line(px, x0, y0, x1, y1, c):
    steps = max(abs(x1 - x0), abs(y1 - y0), 1)
    for i in range(steps + 1):
        put(px, round(x0 + (x1 - x0) * i / steps), round(y0 + (y1 - y0) * i / steps), c)


def leg(px, hip, knee, foot):
    line(px, *hip, *knee, LEG)       # one pixel, outlined dark with the ball (reads at a distance)
    line(px, *knee, *foot, LEG)
    put(px, knee[0], knee[1] - 1, LEG_LIGHT)


def legs(px, frame, drop):
    # three per side, hips along the lower half of the ball, feet on the bottom row
    hips = [(10, 16), (16, 19), (22, 16)]
    for i, (hx, hy) in enumerate(hips):
        for side in (-1, 1):
            if drop is None:
                phase = (frame + i + (0 if side < 0 else 2)) % 4
                lift = (0, 1, 2, 1)[phase]
                reach = (0, 1, 0, -1)[phase]
                foot = (hx + side * (5 + i % 2) + reach, S - 1 - lift)
                knee = (hx + side * (4 + i % 2), hy + 3 - lift)
            elif drop == 0:   # pulled in, about to let go
                foot = (hx + side * 3, hy + 6)
                knee = (hx + side * 4, hy + 3)
            else:             # flung out, falling
                foot = (hx + side * (8 + i), hy + 2 - i)
                knee = (hx + side * (5 + i), hy + 4)
            leg(px, (hx + side, hy), knee, foot)


def eye(px, frame, drop):
    look = 3 if drop is None else 0
    dy = 0 if drop is None else 2 if drop == 1 else 1
    for y in range(S):
        for x in range(S):
            d = math.hypot(x - CX, y - CY)
            if d <= R + 0.5:
                # lit from the top left
                light = (x - CX) * 0.4 + (y - CY) * 0.6
                c = SCLERA if light < 2 else SCLERA_SHADE if light < 5 else SCLERA_DARK
                put(px, x, y, c)
    # veins crawling in from the back of the ball
    for vx, vy, wob in ((-8, -2, 1), (-7, 4, -1), (-3, 8, 1), (2, -8, -1)):
        x, y = CX + vx, CY + vy
        for k in range(4):
            put(px, x, y, VEIN)
            x += 1 if vx < 0 else -1 if vx > 0 else 0
            y += (wob if k % 2 else 0) + (1 if vy < -4 else -1 if vy > 4 else 0)
    ix, iy = CX + look, CY + dy - 1
    for y in range(iy - 4, iy + 5):
        for x in range(ix - 4, ix + 5):
            d = math.hypot(x - ix, y - iy)
            if d <= 4.3:
                put(px, x, y, IRIS if d < 3.2 else IRIS_DARK)
    for y in range(iy - 2, iy + 3):   # a slit pupil, wider when it drops
        put(px, ix, y, PUPIL)
        if drop is not None or frame % 2:
            put(px, ix + 1, y, PUPIL)
    put(px, ix - 2, iy - 2, SHINE)
    put(px, ix - 1, iy - 3, SHINE)
    # outline
    src = px
    edge = []
    for y in range(S):
        for x in range(S):
            if src[x, y][3] == 0:
                for ox, oy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    nx, ny = x + ox, y + oy
                    if 0 <= nx < S and 0 <= ny < S and src[nx, ny] in (SCLERA, SCLERA_SHADE, SCLERA_DARK, VEIN, LEG, LEG_LIGHT):
                        edge.append((x, y))
                        break
    for x, y in edge:
        put(px, x, y, OUTLINE)


def frame_image(frame, drop=None):
    im = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    px = im.load()
    legs(px, frame, drop)
    eye(px, frame, drop)
    return im


def sheet(name, frames):
    im = Image.new("RGBA", (S * len(frames), S), (0, 0, 0, 0))
    for i, f in enumerate(frames):
        im.paste(f, (i * S, 0))
    path = OUT + name + ".png"
    im.save(path)
    meta = path + ".meta"
    write_meta(".", meta, [(f"{name}_{i}", i * S, 0, S, S, (0.5, 0.5)) for i in range(len(frames))])
    text = open(meta).read()
    text = re.sub(r"spritePixelsToUnits: \d+", "spritePixelsToUnits: 32", text)
    text = re.sub(r"textureCompression: \d+", "textureCompression: 0", text)
    open(meta, "w").write(text)


def main():
    os.makedirs(OUT, exist_ok=True)
    sheet("Crawler Walk", [frame_image(f) for f in range(4)])
    sheet("Crawler Drop", [frame_image(0, 0), frame_image(0, 1)])


if __name__ == "__main__":
    main()
