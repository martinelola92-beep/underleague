#!/usr/bin/env python3
"""Exploración de estilos de retrato (9 oct 2026; el revisor: «prueba más estilos de caras»).

Toma los renders de `out/arte/retratos/` (proporción real) y `out/arte/retratos-cabezon/` (cabeza ×1,45, tronco ×1,15)
y genera, por estilo, el juego completo en `out/arte/estilos/<estilo>/` más una hoja comparativa
`out/arte/estilos/comparativa.png` (una fila por estilo, una columna por raza). El estilo elegido se copia luego a
`Game/Art/Portraits/` con `--adoptar <estilo>`.

Estilos:
- `actual`: el del juego hoy (toon + contorno fino).
- `cabezon`: cabeza grande, mismo tratamiento — proporción de cómic.
- `cel`: cabezón, colores planos (paleta reducida por imagen), contorno grueso y tembloroso.
- `lapiz`: cabezón, grease pencil — trazo doble que tiembla, sombreado a rayas, papel.
- `oleo`: cabezón, pintado — pinceladas (filtro de moda), luz cálida y textura de lienzo.

Uso:  out/arte-venv/bin/python tools/arte/estilos_caras.py [--adoptar <estilo>]
"""
from __future__ import annotations

import os
import shutil
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
BASE = os.path.join(ROOT, "out", "arte")
OUT = os.path.join(BASE, "estilos")
RACES = ["human", "elf", "dwarf", "orc", "undead"]
VARIANTS = 8
SIZE = 256
INK = np.array([27, 21, 16], np.float32)
BACKDROP = {"human": (214, 180, 140), "elf": (176, 214, 196), "dwarf": (222, 190, 120), "orc": (170, 196, 110), "undead": (184, 196, 186)}

sys.path.insert(0, os.path.dirname(__file__))
from retratos import ORC_SHIFT, shift_greens  # noqa: E402


def load(folder: str, race: str, v: int) -> np.ndarray:
    im = Image.open(os.path.join(BASE, folder, f"{race}_{v}.png")).convert("RGBA").resize((SIZE * 2, SIZE * 2), Image.LANCZOS)
    a = np.asarray(im, np.float32)
    if race == "orc":
        rgb = shift_greens(a[..., :3], a[..., 3], ORC_SHIFT[v % len(ORC_SHIFT)])
        a = np.dstack([rgb, a[..., 3]])
    return a


def outline(alpha: np.ndarray, width: int) -> tuple[np.ndarray, np.ndarray]:
    mask = Image.fromarray(alpha.astype(np.uint8))
    grown = np.asarray(mask.filter(ImageFilter.MaxFilter(width)), np.float32)
    return np.clip(grown - alpha, 0, 255) / 255.0, grown


def edges(rgb: np.ndarray, alpha: np.ndarray, gain: float) -> np.ndarray:
    lum = Image.fromarray((rgb @ np.array([0.299, 0.587, 0.114])).astype(np.uint8))
    e = np.asarray(lum.filter(ImageFilter.FIND_EDGES), np.float32) / 255.0
    return np.clip((e - 0.1) * gain, 0, 1) * (alpha / 255.0)


def ink_over(rgb: np.ndarray, amount: np.ndarray) -> np.ndarray:
    return rgb * (1 - amount[..., None]) + INK * amount[..., None]


def finish(rgb: np.ndarray, alpha: np.ndarray) -> Image.Image:
    return Image.fromarray(np.dstack([np.clip(rgb, 0, 255), alpha]).astype(np.uint8), "RGBA").resize((SIZE, SIZE), Image.LANCZOS)


def style_toon(a: np.ndarray) -> Image.Image:
    rgb, alpha = a[..., :3], a[..., 3]
    rgb = np.clip((rgb - 128.0) * 1.04 + 122.0, 0, 255)
    poster = np.round(rgb / 255.0 * 5.0) / 5.0 * 255.0
    rgb = rgb * 0.65 + poster * 0.35
    rgb = ink_over(rgb, edges(rgb, alpha, 3.0) * 0.75)
    o, grown = outline(alpha, 9)
    return finish(ink_over(rgb, o), np.maximum(alpha, grown))


def style_cel(a: np.ndarray, seed: int) -> Image.Image:
    rgb, alpha = a[..., :3], a[..., 3]
    img = Image.fromarray(np.clip(rgb, 0, 255).astype(np.uint8)).filter(ImageFilter.ModeFilter(7))
    flat = np.asarray(img.quantize(colors=9, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE).convert("RGB"), np.float32)
    flat = np.clip((flat - 128.0) * 1.12 + 132.0, 0, 255)
    inner = edges(flat, alpha, 6.0)
    rgb = ink_over(flat, np.clip(inner * 1.2, 0, 1))
    # Contorno grueso que tiembla: la máscara desplazada por un ruido suave antes de crecer.
    rng = np.random.default_rng(seed)
    shift = Image.fromarray(alpha.astype(np.uint8)).transform(
        alpha.shape[::-1], Image.AFFINE, (1, rng.uniform(-0.01, 0.01), rng.uniform(-2, 2), rng.uniform(-0.01, 0.01), 1, rng.uniform(-2, 2)))
    o, grown = outline(np.maximum(alpha, np.asarray(shift, np.float32)), 15)
    return finish(ink_over(rgb, o), np.maximum(alpha, grown))


def style_pencil(a: np.ndarray, seed: int) -> Image.Image:
    rgb, alpha = a[..., :3], a[..., 3]
    paper = np.array([238, 226, 196], np.float32)
    rgb = rgb * 0.8 + paper * 0.2
    rgb = np.clip((rgb - 128.0) * 0.95 + 135.0, 0, 255)
    lum = rgb @ np.array([0.299, 0.587, 0.114]) / 255.0
    h, w = lum.shape
    yy, xx = np.mgrid[0:h, 0:w]
    hatch = (((xx + yy) % 9) < 2).astype(np.float32) * (lum < 0.42) + (((xx - yy) % 11) < 2).astype(np.float32) * (lum < 0.28)
    rgb = ink_over(rgb, np.clip(hatch, 0, 1) * 0.55 * (alpha / 255.0))
    rgb = ink_over(rgb, edges(rgb, alpha, 4.0) * 0.85)
    # Trazo doble: dos contornos desplazados, como un lápiz que pasa dos veces.
    rng = np.random.default_rng(seed)
    total = np.zeros_like(alpha)
    grown_all = alpha.copy()
    for _ in range(2):
        dx, dy = rng.uniform(-3, 3, 2)
        moved = np.asarray(Image.fromarray(alpha.astype(np.uint8)).transform(alpha.shape[::-1], Image.AFFINE, (1, 0, dx, 0, 1, dy)), np.float32)
        o, grown = outline(moved, 5)
        total = np.maximum(total, o)
        grown_all = np.maximum(grown_all, grown)
    return finish(ink_over(rgb, total * 0.9), grown_all)


def style_oil(a: np.ndarray) -> Image.Image:
    rgb, alpha = a[..., :3], a[..., 3]
    img = Image.fromarray(np.clip(rgb, 0, 255).astype(np.uint8)).filter(ImageFilter.ModeFilter(9)).filter(ImageFilter.SMOOTH_MORE)
    rgb = np.asarray(img, np.float32)
    warm = np.array([1.06, 1.0, 0.88], np.float32)
    rgb = np.clip(rgb * warm, 0, 255)
    canvas = np.asarray(Image.open(os.path.join(ROOT, "Game", "Art", "Textures", "parchment_detail.png")).convert("L").resize(alpha.shape[::-1]), np.float32) / 255.0
    rgb = rgb * (0.85 + 0.15 * canvas[..., None] / 0.93)
    rgb = ink_over(rgb, edges(rgb, alpha, 2.0) * 0.45)
    o, grown = outline(alpha, 7)
    return finish(ink_over(rgb, o * 0.85), np.maximum(alpha, grown))


STYLES = {
    "actual": ("retratos", lambda a, s: style_toon(a)),
    "cabezon": ("retratos-cabezon", lambda a, s: style_toon(a)),
    "cel": ("retratos-cabezon", style_cel),
    "lapiz": ("retratos-cabezon", style_pencil),
    "oleo": ("retratos-cabezon", lambda a, s: style_oil(a)),
}


def framed(face: Image.Image, race: str) -> Image.Image:
    """Como lo pinta Portrait.Draw: fondo del color de la raza con rayos, la figura y marco de tinta."""
    tile = Image.new("RGBA", (SIZE, SIZE), BACKDROP[race] + (255,))
    d = ImageDraw.Draw(tile)
    cx, cy = SIZE / 2, SIZE * 0.42
    for k in range(12):
        import math
        a0, a1 = k * math.tau / 12, (k + 0.5) * math.tau / 12
        d.polygon([(cx, cy), (cx + 400 * math.cos(a0), cy + 400 * math.sin(a0)), (cx + 400 * math.cos(a1), cy + 400 * math.sin(a1))],
                  fill=tuple(int(c * 0.9) for c in BACKDROP[race]) + (255,))
    tile.alpha_composite(face)
    ImageDraw.Draw(tile).rectangle([0, 0, SIZE - 1, SIZE - 1], outline=(27, 21, 16, 255), width=10)
    return tile


def main() -> None:
    if "--adoptar" in sys.argv:
        style = sys.argv[sys.argv.index("--adoptar") + 1]
        target = os.path.join(ROOT, "Game", "Art", "Portraits")
        for f in os.listdir(os.path.join(OUT, style)):
            shutil.copy(os.path.join(OUT, style, f), os.path.join(target, f))
        print(f"adoptado {style}")
        return

    sheet = Image.new("RGBA", (len(RACES) * 2 * 210 + 170, len(STYLES) * 210 + 10), (60, 44, 30, 255))
    label = ImageDraw.Draw(sheet)
    for row, (style, (folder, fn)) in enumerate(STYLES.items()):
        os.makedirs(os.path.join(OUT, style), exist_ok=True)
        label.text((10, row * 210 + 95), style.upper(), fill=(240, 226, 192, 255))
        for r, race in enumerate(RACES):
            for v in range(VARIANTS):
                face = fn(load(folder, race, v), r * 31 + v)
                face.save(os.path.join(OUT, style, f"{race}_{v}.png"), optimize=True)
                if v < 2:
                    sheet.alpha_composite(framed(face, race).resize((200, 200), Image.LANCZOS), (170 + (r * 2 + v) * 210, row * 210 + 10))
    sheet.save(os.path.join(OUT, "comparativa.png"))
    print("comparativa en", os.path.join(OUT, "comparativa.png"))


if __name__ == "__main__":
    main()
