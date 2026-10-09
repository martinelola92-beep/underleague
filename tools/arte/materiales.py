#!/usr/bin/env python3
"""Genera los materiales pintados de la interfaz (pase de materiales, arte por Claude desde el 9 oct 2026).

Mapas de DETALLE en escala de grises (media ~0,93): la interfaz los multiplica por el color de la paleta
(`Ink.Paper`, `Ink.Wood`...), así que el tono lo sigue decidiendo el código y el grano lo pone la textura.
Deterministas: la semilla es fija, volver a ejecutar da los mismos PNG byte a byte.

Uso:  out/arte-venv/bin/python tools/arte/materiales.py   (requiere pillow y numpy)
Salida: Game/Art/Textures/*.png
"""
from __future__ import annotations

import math
import os

import numpy as np
from PIL import Image, ImageFilter

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Game", "Art", "Textures")


def noise(rng: np.random.Generator, size: int, cells: int) -> np.ndarray:
    """Ruido de valor suave: rejilla aleatoria ampliada con bicúbica. 0..1."""
    grid = rng.random((cells, cells)).astype(np.float32)
    img = Image.fromarray((grid * 255).astype(np.uint8), "L").resize((size, size), Image.BICUBIC)
    return np.asarray(img, dtype=np.float32) / 255.0


def fbm(rng: np.random.Generator, size: int, base: int, octaves: int, falloff: float = 0.5) -> np.ndarray:
    total = np.zeros((size, size), np.float32)
    amp, norm, cells = 1.0, 0.0, base
    for _ in range(octaves):
        total += noise(rng, size, cells) * amp
        norm += amp
        amp *= falloff
        cells *= 2
    return total / norm


def normalize(a: np.ndarray) -> np.ndarray:
    lo, hi = np.percentile(a, 1), np.percentile(a, 99)
    return np.clip((a - lo) / max(hi - lo, 1e-6), 0, 1)


def save_gray(a: np.ndarray, name: str) -> None:
    rgb = np.repeat(np.clip(a, 0, 1)[..., None], 3, axis=2)
    Image.fromarray((rgb * 255).round().astype(np.uint8), "RGB").save(os.path.join(OUT, name), optimize=True)
    print(f"{name}: media {a.mean():.3f}")


def parchment(size: int = 2048) -> None:
    rng = np.random.default_rng(20261009)
    # Moteado grande (manchas de humedad) con distorsión de dominio para que no parezca nube de ordenador.
    warp = fbm(rng, size, 6, 4)
    mott = fbm(rng, size, 5, 6)
    yy, xx = np.mgrid[0:size, 0:size].astype(np.float32)
    shift = ((warp - 0.5) * size * 0.08).astype(np.float32)
    ix = np.clip((xx + shift).astype(np.int32), 0, size - 1)
    iy = np.clip((yy + shift.T).astype(np.int32), 0, size - 1)
    mott = normalize(mott[iy, ix])

    # Fibras: ruido fino estirado en horizontal, como la pasta del papel.
    fib = noise(rng, size, 700)
    fib = np.asarray(Image.fromarray((fib * 255).astype(np.uint8)).filter(ImageFilter.BoxBlur(0)).resize((size // 8, size), Image.BICUBIC).resize((size, size), Image.BICUBIC), np.float32) / 255
    grain = noise(rng, size, 1024)

    # Cercos de manchas: anillos tenues, oscuros en el borde (lo que deja una taza o una gota de vino).
    stains = np.zeros((size, size), np.float32)
    for _ in range(9):
        cx, cy = rng.uniform(0, size, 2)
        r = rng.uniform(size * 0.03, size * 0.11)
        d = np.hypot(xx - cx, yy - cy) / r
        wob = (fbm(rng, 256, 4, 3) - 0.5) * 0.35
        wob = np.asarray(Image.fromarray(((wob + 0.5) * 255).astype(np.uint8)).resize((size, size), Image.BICUBIC), np.float32) / 255 - 0.5
        d = d + wob
        ring = np.exp(-((d - 1.0) ** 2) / 0.004) * 0.9 + np.clip(1 - d, 0, 1) * 0.25
        stains = np.maximum(stains, ring * rng.uniform(0.35, 0.8))

    # Motas de suciedad.
    specks = (rng.random((size, size)) > 0.9993).astype(np.float32)
    specks = np.asarray(Image.fromarray((specks * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.2)), np.float32) / 255

    value = (1.0
             - 0.20 * mott
             - 0.035 * (fib - 0.5)
             - 0.03 * (grain - 0.5)
             - 0.09 * stains
             - 0.5 * np.clip(specks * 3, 0, 1))
    save_gray(np.clip(value, 0, 1), "parchment_detail.png")


def wood(size: int = 2048) -> None:
    rng = np.random.default_rng(777)
    yy, xx = np.mgrid[0:size, 0:size].astype(np.float32)
    value = np.ones((size, size), np.float32)

    # Tablones horizontales de alto irregular, cada uno con su tono y su veta.
    y = 0
    edges = []
    while y < size:
        h = int(rng.uniform(150, 240))
        edges.append((y, min(y + h, size)))
        y += h
    streak_base = noise(rng, size, 64)
    for top, bottom in edges:
        tone = rng.uniform(0.78, 1.0)
        # Veta: ruido muy estirado en x, con ondulación.
        g = fbm(rng, 512, 8, 5)
        g = np.asarray(Image.fromarray((g * 255).astype(np.uint8)).resize((size * 2, max(bottom - top, 1) // 1), Image.BICUBIC), np.float32) / 255
        off = int(rng.uniform(0, size))
        g = g[:, off:off + size] if g.shape[1] >= off + size else np.roll(g, -off, axis=1)[:, :size]
        band = np.sin((g * 40.0) + (yy[top:bottom] * 0.08)) * 0.5 + 0.5
        streaks = 0.80 + 0.20 * normalize(g)
        lines = 1.0 - 0.18 * (band ** 6)
        value[top:bottom] = tone * streaks * lines
        # Nudos: elipses oscuras con anillos.
        for _ in range(int(rng.integers(0, 3))):
            kx, ky = rng.uniform(0, size), rng.uniform(top + 30, max(bottom - 30, top + 31))
            d = np.hypot((xx[top:bottom] - kx) / 2.4, yy[top:bottom] - ky) / rng.uniform(14, 26)
            value[top:bottom] *= 1 - 0.45 * np.exp(-d ** 2) - 0.08 * (np.sin(d * 9) * 0.5 + 0.5) * np.exp(-d / 2.5)
        # Junta: raya de tinta y borde biselado.
        value[top:top + 5] *= 0.25
        value[top + 5:top + 9] *= 0.75
        value[max(bottom - 4, top):bottom] *= 0.8
        # Testas de tablón (cortes verticales) cada cierto trecho.
        x = rng.uniform(-400, 0)
        while x < size:
            x += rng.uniform(500, 1100)
            xi = int(x)
            if 0 <= xi < size - 6:
                value[top:bottom, xi:xi + 4] *= 0.3
                value[top:bottom, xi + 4:xi + 7] *= 0.85
    value *= 0.92 + 0.08 * streak_base
    save_gray(np.clip(value, 0, 1), "wood_detail.png")


def brush(index: int, w: int = 1024, h: int = 256) -> None:
    """Trazo de brocha seca: blanco con alfa; la interfaz lo tiñe (rojo de los rótulos).

    Cada fila de píxeles es una cerda: empieza y acaba en su propio sitio (la cola se deshilacha en rayas), los
    bordes de arriba y abajo ondulan, y sólo cerca de los bordes y de la cola quedan huecos de pincel seco.
    """
    rng = np.random.default_rng(4000 + index)
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)

    def smooth_row(n: int, cells: int) -> np.ndarray:
        g = rng.random(cells).astype(np.float32)
        return np.interp(np.linspace(0, cells - 1, n), np.arange(cells), g).astype(np.float32)

    # Contorno: borde superior e inferior con ondulación media y mordiscos finos.
    top = h * 0.16 + smooth_row(w, 7) * h * 0.08 + smooth_row(w, 90) * h * 0.035
    bottom = h * 0.84 - smooth_row(w, 7) * h * 0.08 - smooth_row(w, 90) * h * 0.035
    # Cerdas: cada fila tiene su inicio y su final. Las del centro llegan más lejos; las de los bordes, menos.
    rel = np.abs(np.arange(h, dtype=np.float32) - h / 2) / (h / 2)
    bristle_hi = rng.random(h).astype(np.float32)
    bristle_lo = smooth_row(h, 18)
    ends = w * (0.80 + 0.17 * bristle_lo - 0.22 * rel ** 2) - bristle_hi * w * 0.10
    starts = w * (0.015 + 0.03 * rel ** 2 + 0.03 * smooth_row(h, 10)) + rng.random(h).astype(np.float32) * w * 0.012
    inside = (yy > top[None, :]) & (yy < bottom[None, :]) & (xx > starts[:, None]) & (xx < ends[:, None])
    # Pincel seco: huecos en filas finas, más abiertos hacia la cola y hacia los bordes.
    u = xx / w
    dry_rows = np.repeat(rng.random(h).astype(np.float32)[:, None], w, axis=1)
    dry_patch = fbm(rng, w, 12, 3)[:h, :w]
    pressure = 0.06 + 0.5 * np.clip((u - 0.55) / 0.4, 0, 1) ** 1.5 + 0.25 * rel[:, None] ** 3
    gaps = (dry_rows * 0.65 + dry_patch * 0.35) < pressure
    a = (inside & ~gaps).astype(np.float32)
    a = np.asarray(Image.fromarray((a * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.7)), np.float32) / 255
    shade = 0.84 + 0.16 * normalize(dry_patch * 0.6 + dry_rows * 0.4)
    rgba = np.zeros((h, w, 4), np.uint8)
    rgba[..., 0] = rgba[..., 1] = rgba[..., 2] = (shade * 255).astype(np.uint8)
    rgba[..., 3] = (np.clip(a, 0, 1) * 255).astype(np.uint8)
    Image.fromarray(rgba, "RGBA").save(os.path.join(OUT, f"brush_{index:02d}.png"), optimize=True)
    print(f"brush_{index:02d}.png")


def main() -> None:
    os.makedirs(OUT, exist_ok=True)
    parchment()
    wood()
    for i in range(4):
        brush(i)


if __name__ == "__main__":
    main()
