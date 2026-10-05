#!/usr/bin/env python3
"""Builds the Microsoft Store super hero art and the Xbox images from the game's own sprites.

    python3 tools/make_store_art.py

Each image is composed at a low "Oric" resolution with the real sprites, then scaled up by
whole multiples with nearest-neighbour sampling, so every pixel stays crisp. Output goes to
stores/microsoft-store/.
"""
import os, re
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, 'stores', 'microsoft-store')
PAL = {'K': (0, 0, 0), 'R': (255, 0, 0), 'G': (0, 255, 0), 'Y': (255, 255, 0), 'B': (0, 0, 255),
       'M': (255, 0, 255), 'C': (0, 255, 255), 'W': (255, 255, 255)}
COL = {k: v for k, v in PAL.items()}

SPRITES = {}
for name in ['SpriteData.Xenon.cs', 'SpriteData.Zorgon.cs']:
    src = open(os.path.join(ROOT, 'XenZarg.Core', 'Graphics', name)).read()
    for m in re.finditer(r'public static readonly string\[\] (\w+) =\s*\[(.*?)\];', src, re.S):
        SPRITES[m.group(1)] = re.findall(r'"([^"]*)"', m.group(2))

FONT = [int(x, 16) for x in re.findall(r'0x([0-9A-F]{2})',
        open(os.path.join(ROOT, 'XenZarg.Core', 'Oric', 'OricFont.cs')).read())]


class Canvas:
    def __init__(self, w, h):
        self.img = Image.new('RGB', (w, h), (0, 0, 0))
        self.w, self.h = w, h

    def plot(self, x, y, c):
        if 0 <= x < self.w and 0 <= y < self.h:
            self.img.putpixel((x, y), c)

    def sprite(self, name, x, y, sx=1, sy=1, rows=None, tint=None):
        data = SPRITES[name]
        for j, row in enumerate(data):
            if rows and not (rows[0] <= j < rows[1]):
                continue
            jj = j - (rows[0] if rows else 0)
            for i, ch in enumerate(row):
                if ch in PAL:
                    c = COL[tint] if tint else PAL[ch]
                    for a in range(sx):
                        for b in range(sy):
                            self.plot(x + i * sx + a, y + jj * sy + b, c)

    def crop(self, name, x0, y0, x1, y1, x, y):
        """Copies part of a full-screen background (non-black pixels only)."""
        data = SPRITES[name]
        for j in range(y0, y1):
            for i in range(x0, x1):
                ch = data[j][i]
                if ch in PAL and ch != 'K':
                    self.plot(x + i - x0, y + j - y0, PAL[ch])

    def fill(self, x, y, w, h, c):
        for j in range(h):
            for i in range(w):
                self.plot(x + i, y + j, PAL[c])

    def text(self, s, x, y, scale=1, colors=('Y',)):
        """Oric ROM font; colours cycle per glyph row for the striped title look."""
        for k, ch in enumerate(s):
            g = FONT[(ord(ch) - 32) * 8:(ord(ch) - 32) * 8 + 8]
            for r, bits in enumerate(g):
                c = PAL[colors[r % len(colors)]]
                for b in range(6):
                    if bits & (1 << (5 - b)):
                        for a in range(scale):
                            for d in range(scale):
                                self.plot(x + (k * 6 + b) * scale + a, y + r * scale + d, c)

    def text_width(self, s, scale=1):
        return len(s) * 6 * scale

    def save(self, path, scale):
        os.makedirs(os.path.dirname(path), exist_ok=True)
        self.img.resize((self.w * scale, self.h * scale), Image.NEAREST).save(path)
        print(path, self.w * scale, 'x', self.h * scale)


def xenon_battle(c, x, y, bolts=True, ship=(80, 122)):
    """The Xenon fighter firing up at Aards under the Zorgon Battle Star."""
    c.sprite('Saucer1', x + 20, y, 4, 3)                    # the Battle Star (96 x 36)
    c.fill(x + 62, y + 38, 12, 6, 'R')                      # its nuclear reactor
    c.fill(x + 64, y + 39, 8, 4, 'Y')
    for i in range(24):                                     # force field, with a gap burnt through
        if i not in (16, 17):
            c.fill(x + 20 + i * 4, y + 50, 4, 2, 'C' if i % 5 else 'W')
    c.sprite('BirdAardUp', x + 6, y + 62)
    c.sprite('BirdAardDown', x + 44, y + 74)
    c.sprite('LargeAard1', x + 104, y + 66)
    c.sprite('SmallAard1', x + 120, y + 96)
    c.sprite('Explosion', x + 76, y + 64)
    c.sprite('BombWhiteRed', x + 18, y + 92)
    if ship:
        sx, sy = ship
        if bolts:
            c.fill(x + sx + 5, y + sy - 22, 2, 7, 'W')
        c.sprite('PlayerShip', x + sx, y + sy)


def zorgon_tower(c, x, y, height):
    """Zorgon and Princess Roz on top of the castle tower, with the great bird and our hero."""
    c.crop('BridgeBackground', 176, 36, 240, min(189, 36 + height), x, y + 18)
    c.sprite('ZZorgonOnTowerF0', x - 6, y)
    c.sprite('ZPrincessRoz', x + 21, y - 5)
    c.sprite('ZHelpBubble', x + 30, y - 18)


def super_hero(titled):
    c = Canvas(320, 180)
    zorgon_tower(c, 250, 40, 150)
    if not titled:
        xenon_battle(c, 18, 14)
        c.sprite('ZBirdMissionBirdF0', 176, 52)
        c.sprite('ZHeroJumpLeftF0', 182, 66)
        c.sprite('ZAlienF0', 206, 118)
        c.sprite('ZMothershipF0', 168, 140)
        return c
    # Titled: the battle fills the top, the fighter climbs on the right and the title sits
    # along the bottom left, clear of the artwork.
    xenon_battle(c, 18, 2, ship=None)
    c.sprite('ZBirdMissionBirdF0', 176, 40)
    c.sprite('ZHeroJumpLeftF0', 182, 54)
    c.fill(213, 104, 2, 7, 'W')
    c.sprite('PlayerShip', 208, 126)
    c.sprite('ZAlienF0', 160, 96)
    c.text('XENZARG', 12, 118, 4, ('Y', 'Y', 'G', 'G'))
    c.text("XENON 1 & ZORGON'S REVENGE", 14, 156, 1, ('C',))
    c.text('BY PFJ', 14, 168, 1, ('W',))
    return c


def key_art():
    """584 x 800 portrait art with the title (composed at 146 x 200, scaled x4)."""
    c = Canvas(146, 200)
    t = 'XENZARG'
    c.text(t, (146 - c.text_width(t, 3)) // 2, 6, 3, ('Y', 'Y', 'G'))
    for i, line in enumerate(['XENON 1 &', "ZORGON'S REVENGE"]):
        c.text(line, (146 - c.text_width(line)) // 2, 34 + i * 9, 1, ('C',))
    c.text('BY PFJ', (146 - c.text_width('BY PFJ')) // 2, 54, 1, ('W',))
    xenon_battle(c, -2, 70, ship=(80, 108))
    return c


def square():
    """1:1 promotional art without the title (composed at 180 x 180)."""
    c = Canvas(180, 180)
    xenon_battle(c, 4, 18)
    zorgon_tower(c, 140, 76, 100)
    c.sprite('ZBirdMissionBirdF0', 114, 40)
    c.sprite('ZHeroJumpLeftF0', 120, 54)
    return c


def main():
    hero = super_hero(False)
    hero.save(os.path.join(OUT, 'super-hero-art-3840x2160.png'), 12)
    hero.save(os.path.join(OUT, 'super-hero-art-1920x1080.png'), 6)
    titled = super_hero(True)
    xbox = os.path.join(OUT, 'xbox')
    titled.save(os.path.join(xbox, 'titled-hero-art-3840x2160.png'), 12)
    titled.save(os.path.join(xbox, 'titled-hero-art-1920x1080.png'), 6)
    key_art().save(os.path.join(xbox, 'branded-key-art-584x800.png'), 4)
    sq = square()
    sq.save(os.path.join(xbox, 'featured-promotional-square-art-2160x2160.png'), 12)
    sq.save(os.path.join(xbox, 'featured-promotional-square-art-1080x1080.png'), 6)


if __name__ == '__main__':
    main()
