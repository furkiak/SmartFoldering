#!/usr/bin/env python3
"""Composes the README images from raw 2x window renders.

  dotnet run --project tools/Screenshots -- shots --scale 2 --matte
  python tools/make_readme_images.py shots docs/images
"""
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

src = Path(sys.argv[1])
out = Path(sys.argv[2])
out.mkdir(parents=True, exist_ok=True)


def wallpaper(w, h, seed):
    """Colourful abstract backdrop so the glass surfaces have something to refract."""
    rng = np.random.default_rng(seed)
    ys, xs = np.mgrid[0:h, 0:w].astype(np.float32)
    t = xs / w * 0.6 + ys / h * 0.4
    base = np.zeros((h, w, 3), np.float32)
    stops = [(0.0, (20, 30, 74)), (0.5, (70, 44, 128)), (1.0, (16, 92, 148))]
    for i in range(3):
        base[..., i] = np.interp(t, [s[0] for s in stops], [s[1][i] for s in stops])
    img = Image.fromarray(base.astype(np.uint8), "RGB").convert("RGBA")
    blobs = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    draw = ImageDraw.Draw(blobs)
    for color in [(255, 105, 180, 150), (64, 224, 208, 130), (255, 170, 60, 110), (120, 120, 255, 150), (90, 220, 120, 100)]:
        r = int(min(w, h) * rng.uniform(0.18, 0.32))
        cx, cy = int(rng.uniform(0, w)), int(rng.uniform(0, h))
        draw.ellipse((cx - r, cy - r, cx + r, cy + r), fill=color)
    return Image.alpha_composite(img, blobs.filter(ImageFilter.GaussianBlur(min(w, h) // 7)))


def load(name, scale=1.0):
    """Recovers an RGBA image from renders over black and over white (difference matte)."""
    black = np.asarray(Image.open(src / f"{name}.black.png").convert("RGB"), np.float32)
    white = np.asarray(Image.open(src / f"{name}.white.png").convert("RGB"), np.float32)
    alpha = np.clip(1.0 - (white - black).mean(axis=2) / 255.0, 0.0, 1.0)
    color = np.where(alpha[..., None] > 0.004, black / np.maximum(alpha[..., None], 0.004), 0.0)
    im = Image.fromarray(np.dstack([np.clip(color, 0, 255), alpha * 255.0]).astype(np.uint8), "RGBA")
    if scale != 1.0:
        im = im.resize((round(im.width * scale), round(im.height * scale)), Image.LANCZOS)
    return im


def compose(name, items, size, seed):
    canvas = wallpaper(*size, seed=seed)
    for im, (x, y) in items:
        canvas.alpha_composite(im, (x, y))
    canvas.convert("RGB").save(out / name, optimize=True)
    print("wrote", out / name, canvas.size)


# Hero: the main window with a notification card over its corner.
main = load("main-en-dark", 0.8)
toast = load("toast-en-dark", 0.8)
W, H = main.width + 140, main.height + 110
compose("hero.png", [(main, (40, 70)), (toast, (W - toast.width - 10, 8))], (W, H), seed=5)

# The dialogs: rule editor, preview, settings.
tiles = [load("editor-en-dark", 0.62), load("preview-en-dark", 0.62), load("settings-en-light", 0.62)]
g = 18
W = sum(t.width for t in tiles) + g * (len(tiles) + 1)
H = max(t.height for t in tiles) + g * 2
x, items = g, []
for t in tiles:
    items.append((t, (x, (H - t.height) // 2)))
    x += t.width + g
compose("dialogs.png", items, (W, H), seed=9)

# Light theme, the first run and Turkish.
tiles = [load("main-en-light", 0.5), load("welcome-en-dark", 0.5), load("main-tr-dark", 0.5)]
W = sum(t.width for t in tiles) + g * (len(tiles) + 1)
H = max(t.height for t in tiles) + g * 2
x, items = g, []
for t in tiles:
    items.append((t, (x, g)))
    x += t.width + g
compose("themes.png", items, (W, H), seed=13)
