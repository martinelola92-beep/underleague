#!/usr/bin/env python3
"""Atlas del público con las figuras de papel (9 oct 2026): el mismo formato que tools/arte/publico.py (30 figuras ×
3 posturas, celdas 96×160) pero con los recortes ilustrados de Game/Art/Figures/, para que la grada y el campo sean del
mismo dibujo. Variante 0 = de frente, 1 = de espaldas; colores: propio (azul, tal cual), rival (azul → rojo), neutral
(azul → pardo). Posturas: quieto, reflejado, y saltando (aplastado y subido: lo que el shader enseña como euforia).
Uso: out/arte-venv/bin/python tools/arte/publico_papel.py
"""
import colorsys
import os

import numpy as np
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
FIG = os.path.join(ROOT, "Game", "Art", "Figures")
OUT = os.path.join(ROOT, "Game", "Art", "Crowd", "crowd_atlas.png")
RACES = ["human", "elf", "dwarf", "orc", "undead"]
CELL_W, CELL_H, PER_ROW, POSES = 96, 160, 6, 3


def recolor(im: Image.Image, target_hue: float | None, sat: float) -> Image.Image:
    if target_hue is None:
        return im
    a = np.asarray(im.convert("RGBA"), np.float32) / 255.0
    out = a.copy()
    flat = a[..., :3].reshape(-1, 3)
    res = out[..., :3].reshape(-1, 3)
    for i, (r, g, b) in enumerate(flat):
        h, s, v = colorsys.rgb_to_hsv(r, g, b)
        if 0.52 < h < 0.72 and s > 0.15:
            res[i] = colorsys.hsv_to_rgb(target_hue, min(max(s * 1.6, sat), 0.9), min(v * 1.1, 1.0))
    out[..., :3] = res.reshape(out[..., :3].shape)
    return Image.fromarray((out * 255).astype(np.uint8), "RGBA")


def cell(im: Image.Image, pose: int) -> Image.Image:
    canvas = Image.new("RGBA", (CELL_W, CELL_H), (0, 0, 0, 0))
    h = CELL_H - 8 if pose != 2 else CELL_H - 20
    w = min(CELL_W, int(im.width * h / im.height * (1.08 if pose == 2 else 1.0)))
    fig = im.resize((w, h), Image.LANCZOS)
    if pose == 1:
        fig = fig.transpose(Image.FLIP_LEFT_RIGHT)
    y = CELL_H - h if pose != 2 else 0
    canvas.alpha_composite(fig, ((CELL_W - w) // 2, y))
    return canvas


def main() -> None:
    atlas = Image.new("RGBA", (PER_ROW * POSES * CELL_W, 5 * CELL_H), (0, 0, 0, 0))
    colors = [(None, 0), (0.995, 0.65), (0.08, 0.35)]
    figure = 0
    for race in RACES:
        for variant, view in enumerate(["front", "back"]):
            base = Image.open(os.path.join(FIG, f"{race}_{view}.png")).convert("RGBA")
            small = base.resize((base.width * CELL_H * 2 // base.height, CELL_H * 2), Image.LANCZOS)
            for hue, sat in colors:
                tinted = recolor(small, hue, sat)
                for pose in range(POSES):
                    col = (figure % PER_ROW) * POSES + pose
                    atlas.alpha_composite(cell(tinted, pose), (col * CELL_W, (figure // PER_ROW) * CELL_H))
                figure += 1
    atlas.save(OUT, optimize=True)
    print("atlas", atlas.size)


if __name__ == "__main__":
    main()
