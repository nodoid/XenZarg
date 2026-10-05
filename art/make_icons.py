"""Generates every platform icon from the original Oric sprites (run from the repo root)."""
import os, re, subprocess, shutil
from PIL import Image

PAL = {'R': (255, 0, 0), 'G': (0, 255, 0), 'Y': (255, 255, 0), 'B': (0, 0, 255),
       'M': (255, 0, 255), 'C': (0, 255, 255), 'W': (255, 255, 255), 'K': (0, 0, 0)}
src = open('XenZarg.Core/Graphics/SpriteData.Xenon.cs').read()

def sprite(name):
    block = re.search(r'%s =\s*\[(.*?)\];' % name, src, re.S).group(1)
    return re.findall(r'"([^"]*)"', block)

def render(rows):
    w, h = max(len(r) for r in rows), len(rows)
    im = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    for y, r in enumerate(rows):
        for x, ch in enumerate(r):
            if ch in PAL:
                im.putpixel((x, y), PAL[ch] + (255,))
    return im

def icon(size, pad=0.12, wide=None):
    w, h = (wide or (size, size))
    canvas = Image.new('RGBA', (w, h), (0, 0, 0, 255))
    bird = render(sprite('BirdAardUp'))
    art = Image.new('RGBA', (20, 20), (0, 0, 0, 0))
    art.paste(bird, (1, 1), bird)
    avail = int(min(w, h) * (1 - 2 * pad))
    scale = max(1, avail // 20)
    big = art.resize((20 * scale, 20 * scale), Image.NEAREST)
    canvas.paste(big, ((w - big.width) // 2, (h - big.height) // 2), big)
    return canvas

def save(im, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    im.save(path)

# Android launcher icons
for folder, px in [('mdpi', 48), ('hdpi', 72), ('xhdpi', 96), ('xxhdpi', 144), ('xxxhdpi', 192)]:
    save(icon(px, 0.1), f'XenZarg.Android/Resources/drawable-{folder}/icon.png')
    save(icon(px * 2, 0.3), f'XenZarg.Android/Resources/drawable-{folder}/splash.png')

# iOS app icon set (opaque)
ios = 'XenZarg.iOS/AppIcon.xcassets/AppIcon.appiconset'
for f in os.listdir(ios):
    m = re.match(r'icon_(\d+)x\d+\.png', f)
    if m:
        icon(int(m.group(1)), 0.1).convert('RGB').save(os.path.join(ios, f))

# Windows .ico and MSIX logos
os.makedirs('XenZarg.WindowsDX/Windows/Assets', exist_ok=True)
icon(256, 0.06).save('XenZarg.WindowsDX/Icon.ico', sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
shutil.copy('XenZarg.WindowsDX/Icon.ico', 'XenZarg.DesktopGL/Icon.ico')
for name, size in [('Square44x44Logo', (44, 44)), ('Square150x150Logo', (150, 150)), ('StoreLogo', (50, 50)),
                   ('Wide310x150Logo', (310, 150)), ('SplashScreen', (620, 300)), ('Square71x71Logo', (71, 71)),
                   ('Square310x310Logo', (310, 310))]:
    icon(min(size), 0.08, wide=size).save(f'XenZarg.WindowsDX/Windows/Assets/{name}.png')
icon(44, 0.0).save('XenZarg.WindowsDX/Windows/Assets/Square44x44Logo.targetsize-44_altform-unplated.png')

# macOS .icns
iconset = 'art/XenZarg.iconset'
os.makedirs(iconset, exist_ok=True)
for s in [16, 32, 128, 256, 512]:
    icon(s, 0.1).save(f'{iconset}/icon_{s}x{s}.png')
    icon(s * 2, 0.1).save(f'{iconset}/icon_{s}x{s}@2x.png')
subprocess.run(['iconutil', '-c', 'icns', iconset, '-o', 'XenZarg.DesktopGL/XenZarg.icns'], check=True)
shutil.rmtree(iconset)
icon(1024, 0.1).convert('RGB').save('art/icon-1024.png')
print('icons done')
