#!/usr/bin/env python3
"""Figuras de papel para el partido (prototipo 2D, 9 oct 2026; el revisor: «¿y si haces 2D con 8 direcciones?»).

Recorta las tres vistas (frente, perfil, espalda) de cada hoja de modelo de `docs/ui/referencias/hojas-de-modelo/`,
quita el fondo de papel (relleno desde los bordes por parecido de color, más el panel de color de la hoja humana),
añade un contorno de tinta y deja `Game/Art/Figures/<raza>_<vista>.png` con los pies en el borde inferior. La vista de
perfil mira a la derecha (la vista de la izquierda es ella reflejada). Uso: out/arte-venv/bin/python tools/arte/figuras_papel.py
"""
from __future__ import annotations

import os
from collections import deque

import numpy as np
from PIL import Image, ImageFilter

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SRC = os.path.join(ROOT, "docs", "ui", "referencias", "hojas-de-modelo")
OUT = os.path.join(ROOT, "Game", "Art", "Figures")
RACES = ["orc", "human", "dwarf", "elf", "undead"]
VIEWS = ["front", "side", "back"]
HEIGHT = 512


def flood(lab: np.ndarray, seeds, tolerance: float, min_brightness: float, out: np.ndarray) -> None:
    """Marca en `out` lo conectado a las semillas a pasos de color parecido (vecino a vecino)."""
    h, w, _ = lab.shape
    queue: deque = deque(seeds)
    while queue:
        y, x = queue.popleft()
        if out[y, x]:
            continue
        out[y, x] = True
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            ny, nx = y + dy, x + dx
            if 0 <= ny < h and 0 <= nx < w and not out[ny, nx]:
                if np.abs(lab[ny, nx] - lab[y, x]).sum() < tolerance and lab[ny, nx].sum() > min_brightness:
                    queue.append((ny, nx))


def remove_background(rgb: np.ndarray, tolerance: float) -> np.ndarray:
    """Máscara de figura: el papel conectado al borde, el panel de color si lo hay y el papel encerrado son fondo."""
    h, w, _ = rgb.shape
    lab = rgb.astype(np.float32)
    background = np.zeros((h, w), bool)
    edges = [(0, x) for x in range(w)] + [(h - 1, x) for x in range(w)] + [(y, 0) for y in range(h)] + [(y, w - 1) for y in range(h)]
    flood(lab, edges, tolerance, 300, background)

    # Panel de color detrás de la figura (hoja humana): dos muestras a los lados, a la altura de los hombros; si
    # coinciden y no son papel, se rellena el panel desde ellas, vecino a vecino (su color se parece al de la túnica,
    # así que no vale buscarlo en toda la imagen).
    border = np.concatenate([lab[0], lab[-1], lab[:, 0], lab[:, -1]])
    paper = np.median(border, axis=0)
    seeds = [(int(h * fy), int(w * fx)) for fy in (0.18, 0.4, 0.6) for fx in (0.08, 0.92)]
    left, right = lab[seeds[0]], lab[seeds[1]]
    if np.abs(left - right).sum() < 40 and np.abs(left - paper).sum() > 60:
        flood(lab, seeds, tolerance * 0.8, 0, background)

    # Papel encerrado (entre las piernas, bajo el brazo): manchas grandes del color del papel.
    near = np.abs(lab - paper).sum(axis=2) < tolerance * 1.7
    background |= big_components(near & ~background, min_area=h * w // 150)
    figure = ~background
    # Limpia islas sueltas y bordes.
    m = Image.fromarray((figure * 255).astype(np.uint8)).filter(ImageFilter.MinFilter(3)).filter(ImageFilter.MaxFilter(3))
    return np.asarray(m) > 127


def big_components(mask: np.ndarray, min_area: int) -> np.ndarray:
    h, w = mask.shape
    labels = np.zeros((h, w), np.int32)
    keep = np.zeros((h, w), bool)
    current = 0
    for y0 in range(h):
        for x0 in range(w):
            if mask[y0, x0] and labels[y0, x0] == 0:
                current += 1
                pixels = []
                queue = deque([(y0, x0)])
                labels[y0, x0] = current
                while queue:
                    y, x = queue.popleft()
                    pixels.append((y, x))
                    for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                        ny, nx = y + dy, x + dx
                        if 0 <= ny < h and 0 <= nx < w and mask[ny, nx] and labels[ny, nx] == 0:
                            labels[ny, nx] = current
                            queue.append((ny, nx))
                if len(pixels) >= min_area:
                    ys, xs = zip(*pixels)
                    keep[list(ys), list(xs)] = True
    return keep


def largest_blob(mask: np.ndarray) -> np.ndarray:
    """Se queda con la mancha conectada más grande (la figura), tirando motas y sombras sueltas."""
    h, w = mask.shape
    labels = np.zeros((h, w), np.int32)
    best, best_size, current = 0, 0, 0
    for y0 in range(h):
        for x0 in range(w):
            if mask[y0, x0] and labels[y0, x0] == 0:
                current += 1
                size = 0
                queue = deque([(y0, x0)])
                labels[y0, x0] = current
                while queue:
                    y, x = queue.popleft()
                    size += 1
                    for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                        ny, nx = y + dy, x + dx
                        if 0 <= ny < h and 0 <= nx < w and mask[ny, nx] and labels[ny, nx] == 0:
                            labels[ny, nx] = current
                            queue.append((ny, nx))
                if size > best_size:
                    best, best_size = current, size
    return labels == best


def main() -> None:
    os.makedirs(OUT, exist_ok=True)
    for race in RACES:
        sheet = np.asarray(Image.open(os.path.join(SRC, f"{race}.jpg")).convert("RGB"))
        h, w, _ = sheet.shape
        for i, view in enumerate(VIEWS):
            part = sheet[:, i * w // 3:(i + 1) * w // 3]
            small = np.asarray(Image.fromarray(part).resize((part.shape[1] // 2, part.shape[0] // 2), Image.BILINEAR))
            mask_small = largest_blob(remove_background(small, tolerance=42))
            mask = np.asarray(Image.fromarray((mask_small * 255).astype(np.uint8)).resize((part.shape[1], part.shape[0]), Image.BILINEAR)) > 127
            # La sombra del suelo (elipse oscura bajo los pies) se recorta: por debajo de los pies no queda nada.
            ys, xs = np.nonzero(mask)
            top, bottom, left, right = ys.min(), ys.max(), xs.min(), xs.max()
            alpha = (mask * 255).astype(np.uint8)
            alpha = np.asarray(Image.fromarray(alpha).filter(ImageFilter.GaussianBlur(0.8)))
            rgba = np.dstack([part, alpha])[top:bottom + 1, left:right + 1]
            im = Image.fromarray(rgba, "RGBA")
            scale = HEIGHT / im.height
            im = im.resize((max(1, int(im.width * scale)), HEIGHT), Image.LANCZOS)
            # Contorno de tinta alrededor de la silueta.
            a = np.asarray(im.getchannel("A"), np.float32)
            grown = np.asarray(Image.fromarray(a.astype(np.uint8)).filter(ImageFilter.MaxFilter(5)), np.float32)
            pad = Image.new("RGBA", im.size, (27, 21, 16, 0))
            pad.putalpha(Image.fromarray(grown.astype(np.uint8)))
            pad.alpha_composite(im)
            pad.save(os.path.join(OUT, f"{race}_{view}.png"), optimize=True)
            print(f"{race}_{view}: {pad.size}")


if __name__ == "__main__":
    main()
