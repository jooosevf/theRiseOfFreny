# The Arena's normal chest in silver (José, 2026-11-02), so it can't pass for the golden one that
# only the Ciclope's key opens. Items/Chest 100x100.png recoloured, frame for frame, into
# Assets/Sprites/Items/Resources/BossLoot/Silver Chest.png (loaded by ArenaChest).
# Run: python silver_chest.py <project root>
import os
import sys

from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from sheetmeta import write_meta

ROOT = sys.argv[1]
OUT = os.path.join(ROOT, 'Assets/Sprites/Items/Resources/BossLoot')

# the chest's palette -> steel and silver
RECOLOUR = {
    (81, 41, 26): (72, 78, 92),       # wood -> dark steel
    (94, 53, 29): (108, 116, 132),    # wood light -> steel
    (219, 165, 40): (190, 198, 212),  # gold trim -> silver
    (251, 242, 54): (244, 248, 255),  # glints -> white
    (36, 25, 25): (28, 30, 40),       # the dark inside -> a cold shade
}

src = Image.open(os.path.join(ROOT, 'Assets/Sprites/Items/Chest 100x100.png')).convert('RGBA')
out = src.copy()
px = out.load()
for y in range(out.height):
    for x in range(out.width):
        r, g, b, a = px[x, y]
        if a and (r, g, b) in RECOLOUR:
            px[x, y] = RECOLOUR[(r, g, b)] + (a,)
path = os.path.join(OUT, 'Silver Chest.png')
out.save(path)
write_meta(ROOT, path + '.meta', [(f'Silver Chest_{i}', i * 100, 0, 100, 100, (0.5, 0.5)) for i in range(4)])
meta = path + '.meta'
text = open(meta).read().replace('textureCompression: 1', 'textureCompression: 0')
open(meta, 'w', newline='\n').write(text)
print('ok')
