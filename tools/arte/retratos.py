#!/usr/bin/env python3
"""Posproceso de los retratos de busto (pase de arte, 9 oct 2026).

Entrada: `out/arte/retratos/<raza>_<v>.png`, renders con fondo transparente de `Game/Scenes/RetratosRender.tscn`
(los mismos modelos del partido, en el uniforme propio). Salida: `Game/Art/Portraits/<raza>_<v>.png` a 256 px con:
- tono de piel variado en el orco (el Imp no trae variantes),
- un poco de posterizado y contraste (de render a ilustración),
- líneas interiores de tinta (bordes de luminancia) y contorno grueso de silueta,
todo determinista. El fondo y el marco los sigue poniendo `Portrait.Draw` (color por raza): aquí sólo la figura.

Uso:  out/arte-venv/bin/python tools/arte/retratos.py
"""
from __future__ import annotations

import colorsys
import glob
import os

import numpy as np
from PIL import Image, ImageFilter

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SRC = os.path.join(ROOT, "out", "arte", "retratos")
OUT = os.path.join(ROOT, "Game", "Art", "Portraits")
SIZE = 256
INK = np.array([27, 21, 16], np.float32)

# Desplazamiento de matiz y saturación del verde del orco por variante: oliva, musgo, ceniza, jade...
ORC_SHIFT = [(0.0, 1.0, 1.0), (-0.04, 0.8, 0.85), (0.05, 0.7, 0.9), (-0.08, 0.9, 0.75),
             (0.02, 0.55, 1.0), (-0.02, 1.1, 0.7), (0.09, 0.6, 0.8), (-0.06, 0.75, 1.05)]


def shift_greens(rgb: np.ndarray, alpha: np.ndarray, shift: tuple[float, float, float]) -> np.ndarray:
    dh, ds, dv = shift
    flat = rgb.reshape(-1, 3) / 255.0
    out = flat.copy()
    for i, (r, g, b) in enumerate(flat):
        h, s, v = colorsys.rgb_to_hsv(r, g, b)
        if 0.17 < h < 0.42 and s > 0.25:  # verdes de piel
            out[i] = colorsys.hsv_to_rgb((h + dh) % 1.0, min(s * ds, 1.0), min(v * dv, 1.0))
    return (out.reshape(rgb.shape) * 255.0).astype(np.float32)


def process(path: str) -> None:
    name = os.path.basename(path)
    race, variant = name[:-4].split("_")
    im = Image.open(path).convert("RGBA").resize((SIZE * 2, SIZE * 2), Image.LANCZOS)
    a = np.asarray(im, np.float32)
    rgb, alpha = a[..., :3], a[..., 3]

    if race == "orc":
        small = Image.fromarray(a.astype(np.uint8)).resize((SIZE, SIZE), Image.LANCZOS)
        sa = np.asarray(small, np.float32)
        shifted = shift_greens(sa[..., :3], sa[..., 3], ORC_SHIFT[int(variant) % len(ORC_SHIFT)])
        big = Image.fromarray(np.dstack([shifted, sa[..., 3]]).astype(np.uint8)).resize((SIZE * 2, SIZE * 2), Image.LANCZOS)
        a = np.asarray(big, np.float32)
        rgb, alpha = a[..., :3], a[..., 3]

    # De render a ilustración: contraste suave y posterizado ligero (6 niveles mezclados al 35 %).
    rgb = np.clip((rgb - 128.0) * 1.04 + 122.0, 0, 255)
    poster = np.round(rgb / 255.0 * 5.0) / 5.0 * 255.0
    rgb = rgb * 0.65 + poster * 0.35

    # Líneas interiores: bordes de luminancia, oscurecidos con tinta.
    lum = Image.fromarray((rgb @ np.array([0.299, 0.587, 0.114])).astype(np.uint8))
    edges = np.asarray(lum.filter(ImageFilter.FIND_EDGES), np.float32) / 255.0
    edges = np.clip((edges - 0.10) * 3.0, 0, 1) * (alpha / 255.0)
    rgb = rgb * (1 - edges[..., None] * 0.75) + INK * edges[..., None] * 0.75

    # Contorno de silueta: la máscara dilatada menos la original, en tinta.
    mask = Image.fromarray(alpha.astype(np.uint8))
    grown = np.asarray(mask.filter(ImageFilter.MaxFilter(9)), np.float32)
    outline = np.clip(grown - alpha, 0, 255) / 255.0
    rgb = rgb * (1 - outline[..., None]) + INK * outline[..., None]
    alpha = np.maximum(alpha, grown)

    out = Image.fromarray(np.dstack([rgb, alpha]).astype(np.uint8), "RGBA").resize((SIZE, SIZE), Image.LANCZOS)
    out.save(os.path.join(OUT, name), optimize=True)


def main() -> None:
    os.makedirs(OUT, exist_ok=True)
    files = sorted(glob.glob(os.path.join(SRC, "*.png")))
    for path in files:
        process(path)
    print(f"{len(files)} retratos en {OUT}")


if __name__ == "__main__":
    main()
