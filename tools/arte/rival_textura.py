#!/usr/bin/env python3
"""Textura del rival de un modelo generado: el azul de la camiseta virado a gules (9 oct 2026).
Uso: out/arte-venv/bin/python tools/arte/rival_textura.py Game/models/races/orc_gen_Image_0.png
Escribe <nombre>_rival.png al lado."""
import colorsys
import sys

import numpy as np
from PIL import Image

src = sys.argv[1]
im = Image.open(src).convert("RGB")
a = np.asarray(im, np.float32) / 255.0
flat = a.reshape(-1, 3)
out = flat.copy()
for i, (r, g, b) in enumerate(flat):
    h, s, v = colorsys.rgb_to_hsv(r, g, b)
    if 0.52 < h < 0.74 and s > 0.15:
        out[i] = colorsys.hsv_to_rgb(0.995, min(max(s * 1.6, 0.6), 0.9), min(v * 1.1, 1.0))
Image.fromarray((out.reshape(a.shape) * 255).astype(np.uint8)).save(src[:-4] + "_rival.png", optimize=True)
print("escrito", src[:-4] + "_rival.png")
