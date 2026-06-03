#!/usr/bin/env python3
"""One-off art prep: the source ship PNGs have a baked-in light checkerboard
background (no real alpha). This keys that background out to true transparency
(border flood-fill, so the hull interior is never punched through), downscales
with Lanczos, and writes the optimized PNGs into web/public/assets.

Run from web/: python3 art-src/make_transparent.py
"""
import os
from PIL import Image, ImageDraw

SRC = os.path.join(os.path.dirname(__file__))
OUT_SHIPS = os.path.join(SRC, "..", "public", "assets", "ships")
OUT_FX = os.path.join(SRC, "..", "public", "assets", "fx")

SHIP_MAX = 768   # max dimension after downscale (≈768x512, hull long side ~512)
SMOKE_MAX = 384


def downscale(img, max_dim):
    w, h = img.size
    s = max_dim / max(w, h)
    if s < 1:
        img = img.resize((round(w * s), round(h * s)), Image.LANCZOS)
    return img


def key_checkerboard(path_in, path_out, max_dim, thresh=110):
    img = downscale(Image.open(path_in).convert("RGBA"), max_dim)
    # Flood-fill transparency in from every border pixel, so only the background
    # connected to the edges is removed (the ship's interior stays intact).
    seeds = []
    w, h = img.size
    for x in range(0, w, 8):
        seeds.append((x, 0)); seeds.append((x, h - 1))
    for y in range(0, h, 8):
        seeds.append((0, y)); seeds.append((w - 1, y))
    for s in seeds:
        if img.getpixel(s)[3] != 0:  # not yet cleared
            ImageDraw.floodfill(img, s, (0, 0, 0, 0), thresh=thresh)
    img.save(path_out)
    return img.size


def key_smoke(path_in, path_out, max_dim):
    img = downscale(Image.open(path_in).convert("RGBA"), max_dim)
    px = img.load()
    w, h = img.size
    # Soft puff is LIGHT on a DARK checkerboard, so alpha tracks brightness
    # (dark background → transparent, bright smoke → opaque). Recolour to a flat
    # near-white so the renderer's grey tint reads cleanly.
    floor = 60.0
    for y in range(h):
        for x in range(w):
            r, g, b, _ = px[x, y]
            lum = (r + g + b) / 3
            a = max(0, min(255, int((lum - floor) / (255 - floor) * 255)))
            px[x, y] = (240, 240, 240, a)
    img.save(path_out)
    return img.size


if __name__ == "__main__":
    for name in ("first_rate.png", "third_rate.png", "frigate.png"):
        size = key_checkerboard(os.path.join(SRC, name), os.path.join(OUT_SHIPS, name), SHIP_MAX)
        print(f"ship {name}: {size}")
    size = key_smoke(os.path.join(SRC, "smoke.png"), os.path.join(OUT_FX, "smoke.png"), SMOKE_MAX)
    print(f"smoke: {size}")
