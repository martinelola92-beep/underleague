#!/usr/bin/env python3
"""Atlas del público (pase de arte, 9 oct 2026; el revisor: «quiero que el público sean renders y no pastillas»).

Entrada: `out/arte/publico/<raza>_<v>_<color>_<postura>.png`, de `Game/Scenes/PublicoRender.tscn`.
Salida: `Game/Art/Crowd/crowd_atlas.png` — 30 figuras (5 razas × 2 variantes × 3 colores: own, rival, neutral) en 3
posturas (de pie, brazos arriba, brazos abiertos), celdas de 96×160, 6 figuras por fila (18 columnas × 5 filas).
Figura f = ((raza·2 + variante)·3 + color); celda = (f % 6)·3 + postura, fila = f // 6. Lo lee la grada en
`MatchPitchView3D.BuildStadium` con `Game/Art/Shaders/crowd.gdshader`.

Uso:  out/arte-venv/bin/python tools/arte/publico.py
"""
from __future__ import annotations

import os

import numpy as np
from PIL import Image, ImageFilter

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SRC = os.path.join(ROOT, "out", "arte", "publico")
OUT = os.path.join(ROOT, "Game", "Art", "Crowd")
RACES = ["human", "elf", "dwarf", "orc", "undead"]
VARIANTS = 2
COLORS = ["own", "rival", "neutral"]
POSES = 3
CELL_W, CELL_H = 96, 160
PER_ROW = 6
INK = np.array([27, 21, 16], np.float32)


def name(race: str, v: int, color: str, pose: int) -> str:
    return os.path.join(SRC, f"{race}_{v}_{color}_{pose}.png")


def main() -> None:
    os.makedirs(OUT, exist_ok=True)
    files = [name(r, v, c, p) for r in RACES for v in range(VARIANTS) for c in COLORS for p in range(POSES)]

    # Ventana común: la unión de todas las cajas, para que los pies de todas las figuras caigan en el mismo sitio.
    union = None
    for f in files:
        box = Image.open(f).getchannel("A").getbbox()
        if box is None:
            continue
        union = box if union is None else (min(union[0], box[0]), min(union[1], box[1]), max(union[2], box[2]), max(union[3], box[3]))
    left, top, right, bottom = union
    height = bottom - top
    width = int(height * CELL_W / CELL_H)
    cx = (left + right) // 2
    window = (cx - width // 2, top, cx - width // 2 + width, bottom)

    figures = len(RACES) * VARIANTS * len(COLORS)
    rows = (figures + PER_ROW - 1) // PER_ROW
    atlas = Image.new("RGBA", (PER_ROW * POSES * CELL_W, rows * CELL_H), (0, 0, 0, 0))
    for i, f in enumerate(files):
        figure, pose = divmod(i, POSES)
        im = Image.open(f).convert("RGBA").crop(window)
        # A tamaño de celda ×2 para dibujar la tinta y después reducir (línea limpia).
        im = im.resize((CELL_W * 2, CELL_H * 2), Image.LANCZOS)
        a = np.asarray(im, np.float32)
        rgb, alpha = a[..., :3], a[..., 3]
        rgb = np.clip((rgb - 128.0) * 1.08 + 128.0, 0, 255)
        mask = Image.fromarray(alpha.astype(np.uint8))
        grown = np.asarray(mask.filter(ImageFilter.MaxFilter(5)), np.float32)
        outline = np.clip(grown - alpha, 0, 255) / 255.0
        rgb = rgb * (1 - outline[..., None]) + INK * outline[..., None]
        alpha = np.maximum(alpha, grown)
        cell = Image.fromarray(np.dstack([rgb, alpha]).astype(np.uint8), "RGBA").resize((CELL_W, CELL_H), Image.LANCZOS)
        col = (figure % PER_ROW) * POSES + pose
        row = figure // PER_ROW
        atlas.alpha_composite(cell, (col * CELL_W, row * CELL_H))
    atlas.save(os.path.join(OUT, "crowd_atlas.png"), optimize=True)
    print(f"atlas {atlas.size} con {figures} figuras × {POSES} posturas")


if __name__ == "__main__":
    main()
