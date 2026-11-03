# Draws the Conquistas' icons: a 40x40 badge per achievement, in the game's art pixels (1x; the
# sprites are drawn at 4x, the enemy strips of the site's Bestiário at 1x), from the game's own
# sprites plus a few shapes drawn here.
#
#   python3 achievement_icons.py <repo root> <bestiario folder> [site folder]
#
# <bestiario folder> holds the site's enemy strips (freny-online/bestiario/*_anda.png).
# Writes Assets/Sprites/Interface/Resources/Achievements/<id>.png (+ .meta, Point filter, kept
# on regeneration) and, with [site folder], <site folder>/conquistas/<id>.png for the site.
# Ids and order follow Achievements.cs.
import os, sys, uuid
from PIL import Image, ImageDraw

ROOT, BEST = sys.argv[1], sys.argv[2]
SITE = sys.argv[3] if len(sys.argv) > 3 else None
OUT = os.path.join(ROOT, 'Assets/Sprites/Interface/Resources/Achievements')
SPR = os.path.join(ROOT, 'Assets/Sprites')
N = 40

GOLD, GOLD_HI, GOLD_LO = (217, 163, 42), (255, 216, 74), (140, 92, 24)
INK = (29, 20, 32)
BG, BG_HI = (112, 82, 104), (136, 104, 124)
GLOW = (150, 118, 136)
RED, RED_HI = (211, 47, 47), (255, 110, 80)
WHITE = (238, 238, 230)
FIRE = [(255, 240, 200), (255, 200, 80), (255, 130, 40), (200, 60, 30)]


def load(path):
    return Image.open(path).convert('RGBA')


def quarter(img, box):
    # a 4x sprite back to its art pixels (each art pixel is a 4x4 block)
    part = img.crop(box)
    return part.resize((part.width // 4, part.height // 4), Image.NEAREST)


def trim(img):
    return img.crop(img.getbbox())


def frame(strip, count, i=0):
    w = strip.width // count
    return trim(strip.crop((w * i, 0, w * (i + 1), strip.height)))


def badge():
    img = Image.new('RGBA', (N, N), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle((0, 0, N - 1, N - 1), 6, fill=INK)
    d.rounded_rectangle((1, 1, N - 2, N - 2), 5, fill=GOLD_LO)
    d.rounded_rectangle((2, 2, N - 3, N - 3), 4, fill=GOLD)
    d.rounded_rectangle((4, 4, N - 5, N - 5), 3, fill=INK)
    d.rounded_rectangle((5, 5, N - 6, N - 6), 2, fill=BG)
    d.ellipse((9, 9, N - 10, N - 10), fill=GLOW)   # a soft light behind the picture
    d.line((6, 6, N - 7, 6), fill=BG_HI)
    d.line((3, 2, N - 4, 2), fill=GOLD_HI)
    return img


def framed(draw):
    # the picture is drawn on its own layer and cut to the badge's inside, so nothing covers the rim
    img = badge()
    layer = Image.new('RGBA', (N, N), (0, 0, 0, 0))
    draw(layer)
    mask = Image.new('L', (N, N), 0)
    ImageDraw.Draw(mask).rounded_rectangle((5, 5, N - 6, N - 6), 2, fill=255)
    clipped = Image.new('RGBA', (N, N), (0, 0, 0, 0))
    clipped.paste(layer, (0, 0), Image.composite(layer.getchannel('A'), Image.new('L', (N, N), 0), mask))
    img.alpha_composite(clipped)
    return img


def paste(img, part, x, y):
    img.alpha_composite(part, (int(x), int(y)))


def centre(img, part, dx=0, dy=0):
    paste(img, part, (N - part.width) // 2 + dx, (N - part.height) // 2 + dy)


def outline(part, color=INK):
    # a 1 px dark rim round a sprite, so it reads on the badge
    w, h = part.size
    out = Image.new('RGBA', (w + 2, h + 2), (0, 0, 0, 0))
    a = part.getchannel('A')
    for ox, oy in ((0, 1), (2, 1), (1, 0), (1, 2)):
        rim = Image.new('RGBA', part.size, color + (255,))
        rim.putalpha(a)
        out.alpha_composite(rim, (ox, oy))
    out.alpha_composite(part, (1, 1))
    return out


# 3x5 digits and the few letters the badges use (the M is 5x5)
FONT = {
    '0': '111101101101111', '1': '010110010010111', '2': '111001111100111', '3': '111001111001111',
    '5': '111100111001111', 'x': '000101010101000', 'M': '1000111011101011000110001', '.': '000000000000010',
    '!': '010010010000010', ':': '000010000010000',
}


def text(img, s, x, y, color, scale=1, rim=True):
    d = ImageDraw.Draw(img)
    for pass_ in ((INK, 1), (color, 0)) if rim else ((color, 0),):
        c, grow = pass_
        cx = x
        for ch in s:
            bits = FONT[ch]
            w = len(bits) // 5   # 3 wide, the M 5
            for i, b in enumerate(bits):
                if b == '1':
                    px, py = cx + (i % w) * scale, y + (i // w) * scale
                    d.rectangle((px - grow, py - grow, px + scale - 1 + grow, py + scale - 1 + grow), fill=c)
            cx += (w + 1) * scale
    return img


def blast(img, cx, cy, r):
    d = ImageDraw.Draw(img)
    for i, c in enumerate(reversed(FIRE)):
        rr = r - i * r / 4.5
        for k in range(12):   # a ragged ring: spikes every 30 degrees
            import math
            a = k * math.pi / 6
            sx, sy = cx + math.cos(a) * rr * 1.25, cy + math.sin(a) * rr * 1.25
            d.polygon([(cx + math.cos(a - 0.3) * rr * 0.8, cy + math.sin(a - 0.3) * rr * 0.8), (sx, sy),
                       (cx + math.cos(a + 0.3) * rr * 0.8, cy + math.sin(a + 0.3) * rr * 0.8)], fill=c)
        d.ellipse((cx - rr, cy - rr, cx + rr, cy + rr), fill=c)


def grenade():
    g = Image.new('RGBA', (11, 13), (0, 0, 0, 0))
    d = ImageDraw.Draw(g)
    d.ellipse((0, 2, 10, 12), fill=(70, 86, 52))
    d.ellipse((2, 4, 5, 7), fill=(120, 140, 80))
    d.line((0, 7, 10, 7), fill=(50, 62, 38))
    d.line((5, 2, 5, 12), fill=(50, 62, 38))
    d.rectangle((3, 0, 7, 2), fill=(150, 150, 140))
    d.point((8, 0), fill=(150, 150, 140))
    d.point((9, 1), fill=(150, 150, 140))
    return g


def sword():
    s = Image.new('RGBA', (5, 18), (0, 0, 0, 0))
    d = ImageDraw.Draw(s)
    d.rectangle((2, 0, 2, 12), fill=WHITE)
    d.rectangle((1, 1, 1, 12), fill=(170, 180, 200))
    d.rectangle((0, 13, 4, 13), fill=GOLD)
    d.rectangle((2, 14, 2, 16), fill=(110, 70, 40))
    d.point((2, 17), fill=GOLD)
    return s


items = load(os.path.join(SPR, 'Items/Items 100x100.png'))
shotgun = trim(quarter(items, (200, 0, 300, 100)))
coin = trim(quarter(load(os.path.join(SPR, 'Interface/Cash.png')), (0, 0, 44, 48)))
heart = trim(quarter(load(os.path.join(SPR, 'Interface/HealthFull.png')), (0, 0, 52, 52)))
freny = trim(quarter(load(os.path.join(SPR, 'Characters/Player/Player idle.png')), (0, 0, 200, 200)))
cyclops = frame(load(os.path.join(BEST, 'ciclope_anda.png')), 5)
supreme = frame(load(os.path.join(BEST, 'feiticeiro_supremo_anda.png')), 2)
shielded = frame(load(os.path.join(BEST, 'escudeiro_anda.png')), 6)


def milionario(img):
    for x, y in ((9, 21), (19, 21), (14, 17)):
        paste(img, outline(coin), x, y)
    text(img, '1M', 9, 7, GOLD_HI, 2)
    return img


def half(part):
    # a big 1x sprite at half size: each 2x2 block takes its most solid pixel, so lines survive
    w, h = part.width // 2, part.height // 2
    out = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    src = part.load()
    for y in range(h):
        for x in range(w):
            best = max((src[x * 2 + i, y * 2 + j] for i in (0, 1) for j in (0, 1)), key=lambda p: (p[3], -sum(p[:3])))
            out.putpixel((x, y), best)
    return out


def maior_a_queda(img):
    body = half(cyclops).rotate(-32, expand=True, resample=Image.NEAREST)
    paste(img, outline(body), 7, 12)
    d = ImageDraw.Draw(img)
    for sx, sy in ((9, 9), (16, 7), (30, 11)):   # stars round the falling giant's head
        d.point([(sx, sy - 1), (sx - 1, sy), (sx, sy), (sx + 1, sy), (sx, sy + 1)], fill=GOLD_HI)
    d.line((6, 33, 33, 33), fill=(90, 64, 80))   # the floor he lands on
    return img


def feitico(img):
    tilted = supreme.rotate(-22, expand=True, resample=Image.NEAREST)
    paste(img, outline(tilted), 15, 5)
    d = ImageDraw.Draw(img)
    d.ellipse((6, 18, 13, 25), fill=(120, 20, 30))
    d.ellipse((7, 19, 12, 24), fill=RED)
    d.ellipse((8, 20, 10, 22), fill=RED_HI)
    for x, y in ((14, 21), (16, 20), (18, 19)):   # the ball flying back at him
        d.point((x, y), fill=(255, 150, 170))
    return img


def cinco_coelhos(img):
    blast(img, 16, 20, 9)
    paste(img, outline(grenade()), 9, 13)
    text(img, 'x5', 21, 24, GOLD_HI, 2)
    return img


def ops(img):
    blast(img, 20, 24, 11)
    tilted = freny.rotate(35, expand=True, resample=Image.NEAREST)
    paste(img, outline(tilted), 6, 5)
    text(img, '!', 29, 7, GOLD_HI, 2)
    return img


def doze(img):
    gun = shotgun.resize((shotgun.width * 2, shotgun.height * 2), Image.NEAREST)
    paste(img, outline(gun), 5, 22)
    d = ImageDraw.Draw(img)
    for y in (12, 17, 22):   # three pellets in a fan
        d.line((30, 22, 34, y), fill=GOLD_HI)
    text(img, 'x3', 8, 8, GOLD_HI, 2)
    return img


def escudo_pra_que(img):
    paste(img, outline(shielded.transpose(Image.FLIP_LEFT_RIGHT)), 6, N - shielded.height - 6)
    blade = sword().rotate(-60, expand=True, resample=Image.NEAREST)
    paste(img, outline(blade), 20, 10)
    text(img, 'x10', 22, 7, GOLD_HI, 1)
    return img


def menina_dos_olhos(img):
    head = trim(cyclops.crop((2, 0, 34, 30)))
    paste(img, outline(head), 6, 8)
    d = ImageDraw.Draw(img)
    cx, cy = 15, 13   # on his eye
    d.ellipse((cx - 5, cy - 5, cx + 5, cy + 5), outline=GOLD_HI)
    for a, b in (((cx - 8, cy), (cx - 4, cy)), ((cx + 4, cy), (cx + 8, cy)), ((cx, cy - 8), (cx, cy - 4)), ((cx, cy + 4), (cx, cy + 8))):
        d.line((a, b), fill=GOLD_HI)
    blade = sword().rotate(40, expand=True, resample=Image.NEAREST)
    paste(img, outline(blade), 19, 16)
    return img


def quinta_marcha(img):
    d = ImageDraw.Draw(img)
    for y, c in ((25, FIRE[3]), (27, FIRE[2]), (29, FIRE[1]), (31, FIRE[0])):   # speed lines under it
        d.line((6 + (31 - y), y, 33, y), fill=c)
    text(img, '50x', 8, 10, (255, 255, 255), 2)
    return img


def caca_ciclopes(img):
    paste(img, outline(half(cyclops)), 7, 12)
    text(img, 'x3', 21, 7, GOLD_HI, 2)
    return img


def exterminador(img):
    blade = sword().rotate(-45, expand=True, resample=Image.NEAREST)
    paste(img, outline(blade), 5, 6)
    paste(img, outline(blade.transpose(Image.FLIP_LEFT_RIGHT)), 19, 6)
    text(img, '500', 8, 24, GOLD_HI, 2)
    return img


def maratonista(img):
    big = heart.resize((heart.width * 2, heart.height * 2), Image.NEAREST)
    paste(img, outline(big), (N - big.width - 2) // 2, 6)
    text(img, '30:00', 8, 29, GOLD_HI, 1)
    return img


def duro_de_matar(img):
    big = heart.resize((heart.width * 2, heart.height * 2), Image.NEAREST)
    paste(img, outline(big), (N - big.width - 2) // 2, 6)
    text(img, '10:00', 8, 29, GOLD_HI, 1)
    return img


ICONS = [
    ('million', milionario), ('cyclops', maior_a_queda), ('supreme', feitico), ('grenade5', cinco_coelhos),
    ('oops', ops), ('shotgun3', doze), ('backstab', escudo_pra_que), ('eye', menina_dos_olhos),
    ('combo50', quinta_marcha), ('survivor', duro_de_matar), ('cyclops3', caca_ciclopes),
    ('kills500', exterminador), ('marathon', maratonista),
]

META = '''fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
  isReadable: 0
  textureSettings:
    serializedVersion: 2
    filterMode: 0
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 0
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: 40
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 0
  alphaUsage: 1
  alphaIsTransparency: 1
  textureType: 8
  textureShape: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  platformSettings:
  - serializedVersion: 3
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    physicsShape: []
  spritePackingTag:
  userData:
  assetBundleName:
  assetBundleVariant:
'''

os.makedirs(OUT, exist_ok=True)
folder_meta = OUT + '.meta'
if not os.path.exists(folder_meta):
    with open(folder_meta, 'w') as f:
        f.write('fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n'
                '  userData:\n  assetBundleName:\n  assetBundleVariant:\n' % uuid.uuid4().hex)
for name, draw in ICONS:
    img = framed(draw)
    path = os.path.join(OUT, name + '.png')
    img.save(path)
    if not os.path.exists(path + '.meta'):
        with open(path + '.meta', 'w') as f:
            f.write(META.format(guid=uuid.uuid4().hex))
    if SITE:
        os.makedirs(os.path.join(SITE, 'conquistas'), exist_ok=True)
        img.save(os.path.join(SITE, 'conquistas', name + '.png'))
print('wrote', len(ICONS), 'icons to', OUT)
