"""Generate the original, seamlessly looping aurora GIF used by the sample theme.

Requires Pillow: python -m pip install Pillow
"""

from __future__ import annotations

import math
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "samples" / "media" / "aurora-drift.gif"
WIDTH, HEIGHT = 640, 360
FRAME_COUNT = 36


def make_base() -> Image.Image:
    image = Image.new("RGB", (WIDTH, HEIGHT))
    pixels = image.load()
    top = (6, 15, 31)
    bottom = (20, 37, 57)
    for y in range(HEIGHT):
        t = y / (HEIGHT - 1)
        color = tuple(round(a * (1 - t) + b * t) for a, b in zip(top, bottom))
        for x in range(WIDTH):
            pixels[x, y] = color
    return image


def make_stars() -> Image.Image:
    stars = Image.new("RGBA", (WIDTH, HEIGHT))
    draw = ImageDraw.Draw(stars)
    # Fixed seeds keep the committed asset reproducible without random dependencies.
    for index in range(68):
        x = (index * 197 + 43) % WIDTH
        y = (index * 113 + 29) % HEIGHT
        radius = 1 if index % 7 else 2
        alpha = 80 + (index * 31) % 130
        draw.ellipse((x, y, x + radius, y + radius), fill=(194, 225, 255, alpha))
    return stars


def aurora_layer(phase: float, color: tuple[int, int, int], offset: float) -> Image.Image:
    layer = Image.new("RGBA", (WIDTH, HEIGHT))
    draw = ImageDraw.Draw(layer)
    upper: list[tuple[float, float]] = []
    lower: list[tuple[float, float]] = []
    for x in range(-24, WIDTH + 25, 8):
        p = x / WIDTH * math.tau
        center = 138 + offset + 38 * math.sin(p + phase) + 13 * math.sin(2 * p - phase)
        width = 40 + 13 * math.sin(p * 1.3 + phase)
        upper.append((x, center - width))
        lower.append((x, center + width))
    polygon = upper + list(reversed(lower))
    draw.polygon(polygon, fill=(*color, 100))
    return layer.filter(ImageFilter.GaussianBlur(22))


def main() -> None:
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    base = make_base()
    stars = make_stars()
    frames: list[Image.Image] = []
    for index in range(FRAME_COUNT):
        phase = math.tau * index / FRAME_COUNT
        frame = base.convert("RGBA")
        frame.alpha_composite(aurora_layer(phase, (39, 207, 191), 0))
        frame.alpha_composite(aurora_layer(phase + 1.7, (92, 129, 255), 34))
        frame.alpha_composite(stars)
        # Palette conversion keeps the 640x360, 36-frame GIF small enough to share.
        frames.append(frame.convert("RGB").quantize(colors=128, method=Image.Quantize.MEDIANCUT))

    frames[0].save(
        OUTPUT,
        save_all=True,
        append_images=frames[1:],
        duration=70,
        loop=0,
        optimize=True,
        disposal=2,
    )
    print(f"Created {OUTPUT.relative_to(ROOT)} ({OUTPUT.stat().st_size:,} bytes)")


if __name__ == "__main__":
    main()
