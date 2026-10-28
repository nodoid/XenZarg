#!/usr/bin/env python3
"""Builds the store screenshots, videos and graphics in stores/ from game captures.

    python3 tools/make_store_assets.py <capture-root>

<capture-root> must contain the output of the game's capture mode
(`XenZarg --capture <dir> stills|videos [--mobile]`):

    desktop/stills/*.png     macOS captures (desktop prompts)
    mobile/stills/*.png      captures with phone/tablet prompts
    windows/stills/*.png     captures made by the Windows build (run in Parallels)
    video-desktop/video/NN   frame folders + NN.wav from `videos`
    video-mobile/video/NN    same with --mobile

Every image is the native 240x224 Oric screen scaled with nearest-neighbour sampling
(exactly as the game draws it) and centred on black.
"""
import glob, os, re, shutil, subprocess, sys, wave
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
STORES = os.path.join(ROOT, 'stores')
# App preview videos are saved only in ~/Movies, never in the repository.
MOVIES = os.path.join(os.path.expanduser('~'), 'Movies', 'XenZarg')

# The ten screens used for every store, in listing order.
SHOTS = ['01-xenon-bird-aards', '02-xenon-title', '03-xenon-large-aards', '04-planet-radon',
         '05-zorgon-battle-star', '06-zorgon-spider', '07-zorgon-quadnogs', '08-zorgon-ship',
         '09-zorgon-castle', '10-zorgon-title']

# Frames kept from each video segment (50 per second), giving a ~28 s montage.
SEGMENTS = [('01', 75), ('02', 180), ('03', 150), ('04', 150), ('05', 150),
            ('06', 200), ('07', 150), ('08', 180), ('09', 150)]


def fit(img, w, h, integer):
    """Scale the Oric screen to fit w x h (whole multiples on desktop, like the game)."""
    s = min(w / img.width, h / img.height)
    if integer and s >= 1:
        s = int(s)
    sw, sh = int(img.width * s), int(img.height * s)
    out = Image.new('RGB', (w, h), (0, 0, 0))
    out.paste(img.convert('RGB').resize((sw, sh), Image.NEAREST), ((w - sw) // 2, (h - sh) // 2))
    return out


def screenshots(src, dest, w, h, integer, count=len(SHOTS)):
    os.makedirs(dest, exist_ok=True)
    for name in SHOTS[:count]:
        fit(Image.open(os.path.join(src, name + '.png')), w, h, integer).save(os.path.join(dest, name + '.png'))
    print(f'{dest}: {count} x {w}x{h}')


def montage(video_root, tmp):
    """Joins the chosen frames of each segment and their audio into one sequence."""
    frames = os.path.join(tmp, 'frames')
    shutil.rmtree(tmp, ignore_errors=True)
    os.makedirs(frames)
    n = 0
    audio = bytearray()
    rate = 22050
    for seg, keep in SEGMENTS:
        files = sorted(glob.glob(os.path.join(video_root, seg, 'f*.png')))[:keep]
        for f in files:
            shutil.copy(f, os.path.join(frames, f'{n:05d}.png'))
            n += 1
        with wave.open(os.path.join(video_root, seg + '.wav')) as wv:
            rate = wv.getframerate()
            pcm = wv.readframes(wv.getnframes())
        want = len(files) * rate // 50 * 2
        audio += pcm[:want] + bytes(max(0, want - len(pcm)))
    wav = os.path.join(tmp, 'audio.wav')
    with wave.open(wav, 'wb') as wv:
        wv.setnchannels(1)
        wv.setsampwidth(2)
        wv.setframerate(rate)
        wv.writeframes(bytes(audio))
    return frames, wav, n


def encode(frames, wav, out, w, h, integer):
    sh = h if not integer else (h // 224) * 224
    sw = sh * 240 // 224
    os.makedirs(os.path.dirname(out), exist_ok=True)
    subprocess.run(['ffmpeg', '-y', '-loglevel', 'error', '-framerate', '50', '-i', os.path.join(frames, '%05d.png'),
                    '-i', wav,
                    '-vf', f'scale={sw}:{sh}:flags=neighbor,pad={w}:{h}:(ow-iw)/2:(oh-ih)/2:black,fps=30,format=yuv420p',
                    '-c:v', 'libx264', '-profile:v', 'high', '-level', '4.0', '-crf', '16', '-preset', 'slow',
                    '-c:a', 'aac', '-b:a', '256k', '-ar', '44100', '-ac', '2',
                    '-movflags', '+faststart', '-shortest', out], check=True)
    print('video', out)


def oric_text(text, scale, color):
    src = open(os.path.join(ROOT, 'XenZarg.Core', 'Oric', 'OricFont.cs')).read()
    data = [int(x, 16) for x in re.findall(r'0x([0-9A-F]{2})', src)]
    img = Image.new('RGBA', (len(text) * 6, 8), (0, 0, 0, 0))
    for i, ch in enumerate(text):
        g = data[(ord(ch) - 32) * 8:(ord(ch) - 32) * 8 + 8]
        for y, row in enumerate(g):
            for x in range(6):
                if row & (1 << (5 - x)):
                    img.putpixel((i * 6 + x, y), color)
    return img.resize((img.width * scale, img.height * scale), Image.NEAREST)


def feature_graphic(desktop, out):
    """1024x500 Google Play feature graphic from the start screen and Zorgon title."""
    g = Image.new('RGB', (1024, 500), (0, 0, 0))
    # Crop the start screen's own credit lines, repeated on the right instead.
    start = Image.open(os.path.join(desktop, '02-xenon-title.png')).convert('RGB').crop((0, 0, 240, 196))
    zor = Image.open(os.path.join(desktop, '10-zorgon-title.png')).convert('RGB').crop((40, 0, 200, 60))
    g.paste(start.resize((start.width * 2, start.height * 2), Image.NEAREST), (20, 54))
    g.paste(zor.resize((zor.width * 3, zor.height * 3), Image.NEAREST), (1024 - 40 - zor.width * 3, 60))
    t1 = oric_text('BY PFJ', 4, (255, 255, 255, 255))
    t2 = oric_text('BASED ON THE IJK ORIGINALS', 3, (0, 255, 255, 255))
    g.paste(t1, (1024 - 40 - 480 + (480 - t1.width) // 2, 300), t1)
    g.paste(t2, (1024 - 40 - 480 + (480 - t2.width) // 2, 360), t2)
    g.save(out)
    print('feature graphic', out)


def main(cap):
    desktop = os.path.join(cap, 'desktop', 'stills')
    mobile = os.path.join(cap, 'mobile', 'stills')
    windows = os.path.join(cap, 'windows', 'stills')
    icon = Image.open(os.path.join(ROOT, 'art', 'icon-1024.png')).convert('RGB')

    # Apple App Store (iOS): landscape iPhone 6.9" and 6.5", iPad 13".
    screenshots(mobile, os.path.join(STORES, 'app-store', 'iphone-6.9in-2868x1320'), 2868, 1320, False)
    screenshots(mobile, os.path.join(STORES, 'app-store', 'iphone-6.5in-2688x1242'), 2688, 1242, False)
    screenshots(mobile, os.path.join(STORES, 'app-store', 'ipad-13in-2752x2064'), 2752, 2064, False)
    icon.save(os.path.join(STORES, 'app-store', 'icon-1024.png'))

    # Mac App Store.
    screenshots(desktop, os.path.join(STORES, 'mac-app-store', 'screenshots-2880x1800'), 2880, 1800, True)
    screenshots(desktop, os.path.join(STORES, 'mac-app-store', 'screenshots-1440x900'), 1440, 900, True)

    # Google Play: phone (16:9) and 10" tablet, icon and feature graphic.
    gp = os.path.join(STORES, 'google-play')
    screenshots(mobile, os.path.join(gp, 'phone-screenshots-1920x1080'), 1920, 1080, False, 8)
    screenshots(mobile, os.path.join(gp, 'tablet-screenshots-2560x1600'), 2560, 1600, False, 8)
    icon.resize((512, 512), Image.NEAREST).save(os.path.join(gp, 'icon-512.png'))
    feature_graphic(desktop, os.path.join(gp, 'feature-graphic-1024x500.png'))

    # Microsoft Store: captured from the Windows (DirectX) build.
    ms = os.path.join(STORES, 'microsoft-store')
    screenshots(windows, os.path.join(ms, 'screenshots-3840x2160'), 3840, 2160, True)
    screenshots(windows, os.path.join(ms, 'screenshots-1920x1080'), 1920, 1080, True)
    screenshots(windows, os.path.join(ms, 'screenshots-1366x768'), 1366, 768, True)
    icon.resize((2160, 2160), Image.NEAREST).save(os.path.join(ms, 'box-art-2160x2160.png'))
    icon.resize((300, 300), Image.NEAREST).save(os.path.join(ms, 'store-logo-300x300.png'))
    poster = Image.new('RGB', (1440, 2160), (0, 0, 0))
    title = Image.open(os.path.join(windows, '02-xenon-title.png')).convert('RGB')
    poster.paste(title.resize((1440, 1344), Image.NEAREST), (0, 120))
    ztitle = Image.open(os.path.join(windows, '10-zorgon-title.png')).convert('RGB').crop((40, 0, 200, 60))
    poster.paste(ztitle.resize((1280, 480), Image.NEAREST), (80, 1560))
    poster.save(os.path.join(ms, 'poster-art-1440x2160.png'))

    # Videos (App Store app previews and the Mac App Store preview).
    tmp = os.path.join(cap, '_montage')
    if os.path.isdir(os.path.join(cap, 'video-mobile', 'video')):
        frames, wav, n = montage(os.path.join(cap, 'video-mobile', 'video'), tmp)
        os.makedirs(MOVIES, exist_ok=True)
        encode(frames, wav, os.path.join(MOVIES, 'XenZarg - iPhone app preview 1920x886.mp4'), 1920, 886, False)
        encode(frames, wav, os.path.join(MOVIES, 'XenZarg - iPad app preview 1600x1200.mp4'), 1600, 1200, False)
    if os.path.isdir(os.path.join(cap, 'video-desktop', 'video')):
        frames, wav, n = montage(os.path.join(cap, 'video-desktop', 'video'), tmp)
        os.makedirs(MOVIES, exist_ok=True)
        encode(frames, wav, os.path.join(MOVIES, 'XenZarg - Mac app preview 1920x1080.mp4'), 1920, 1080, True)
    shutil.rmtree(tmp, ignore_errors=True)


if __name__ == '__main__':
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    main(sys.argv[1])
