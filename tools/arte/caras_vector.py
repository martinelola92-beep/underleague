#!/usr/bin/env python3
"""Caras dibujadas (vectorial) — estilo de retrato alternativo (9 oct 2026; el revisor: «prueba más estilos de caras»,
«me gusta el estilo grease pencil, cómic… Cult of the Lamb, Lucky Tower»).

Genera un busto por raza y variante con un generador propio: cabeza grande sobre hombros pequeños, ojos sencillos,
sombra plana de una sola forma, colorete, y tinta gruesa que tiembla (cada trazo se dibuja dos veces con un desvío,
como un lápiz que repasa). Cada raza tiene su silueta firma: el orco con colmillos y mandíbula cuadrada, el enano con
barba enorme, el elfo con orejas largas y melena, el no-muerto con cuencas y costuras, el humano con nariz rota. Las
variantes cambian pelo, cejas, boca, cicatrices y parche, siempre deterministas.

Salida: `out/arte/estilos/dibujo/<raza>_<v>.png` (256 px, fondo transparente), para la comparativa de
`tools/arte/estilos_caras.py`. Uso:  out/arte-venv/bin/python tools/arte/caras_vector.py
"""
from __future__ import annotations

import math
import os
import random

import cairosvg

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "out", "arte", "estilos", "dibujo")
RACES = ["human", "elf", "dwarf", "orc", "undead"]
VARIANTS = 8
INK = "#1b1510"
KIT, KIT_DARK, TRIM = "#2a4f8f", "#1c3666", "#d9a93a"


def shade(hex_color: str, k: float) -> str:
    r, g, b = (int(hex_color[i:i + 2], 16) for i in (1, 3, 5))
    return "#%02x%02x%02x" % tuple(max(0, min(255, int(c * k))) for c in (r, g, b))


class Pen:
    """Trazos con temblor: puntos desviados y curva suave (Catmull-Rom a Bézier)."""

    def __init__(self, seed: int):
        self.rng = random.Random(seed)

    def wobble(self, pts, amount):
        return [(x + self.rng.uniform(-amount, amount), y + self.rng.uniform(-amount, amount)) for x, y in pts]

    @staticmethod
    def smooth(pts, closed=True):
        n = len(pts)
        if n < 3:
            return "M %.1f %.1f L %.1f %.1f" % (*pts[0], *pts[-1])
        d = "M %.1f %.1f " % pts[0]
        rng = range(n) if closed else range(n - 1)
        for i in rng:
            p0, p1 = pts[(i - 1) % n if closed else max(i - 1, 0)], pts[i]
            p2, p3 = pts[(i + 1) % n], pts[(i + 2) % n if closed else min(i + 2, n - 1)]
            c1 = (p1[0] + (p2[0] - p0[0]) / 6, p1[1] + (p2[1] - p0[1]) / 6)
            c2 = (p2[0] - (p3[0] - p1[0]) / 6, p2[1] - (p3[1] - p1[1]) / 6)
            d += "C %.1f %.1f %.1f %.1f %.1f %.1f " % (*c1, *c2, *p2)
        return d + ("Z" if closed else "")

    def ellipse(self, cx, cy, rx, ry, n=16, wob=1.4, squash=None):
        pts = []
        for i in range(n):
            a = i / n * math.tau
            r = 1.0 if squash is None else squash(a)
            pts.append((cx + math.cos(a) * rx * r, cy + math.sin(a) * ry * r))
        return self.wobble(pts, wob)

    def shape(self, pts, fill, stroke=6.0, closed=True, extra=""):
        """Relleno + doble trazo de tinta (el segundo, desviado y más fino)."""
        d1 = self.smooth(pts, closed)
        d2 = self.smooth(self.wobble(pts, 1.6), closed)
        f = fill if closed else "none"
        return (f'<path d="{d1}" fill="{f}" stroke="{INK}" stroke-width="{stroke}" stroke-linejoin="round" stroke-linecap="round" {extra}/>'
                f'<path d="{d2}" fill="none" stroke="{INK}" stroke-width="{stroke * 0.45:.1f}" stroke-linejoin="round" stroke-linecap="round" opacity="0.8"/>')

    def line(self, pts, width=5.0):
        return self.shape(self.wobble(pts, 0.8), "none", width, closed=False)


RACE = {
    "human": dict(skins=["#e8b98f", "#d9a274", "#c48a5e", "#f0c9a2"], hair=["#3b2a1e", "#1f1a17", "#7a4422", "#c9a05a"]),
    "elf": dict(skins=["#f3dcc4", "#ead1b4", "#e4c9a8", "#f6e3cf"], hair=["#f2dc8c", "#e8ecef", "#5a3a28", "#c9a45a"]),
    "dwarf": dict(skins=["#e2a77e", "#d39370", "#eab58c", "#c98563"], hair=["#b5502a", "#7a4a25", "#c8622a", "#b8b2a6"]),
    "orc": dict(skins=["#7fa04a", "#6f8f3c", "#8aa858", "#5f7f3a"], hair=["#1f1a17", "#3b2a1e", "#2a2420", "#4a3a2a"]),
    "undead": dict(skins=["#b9c4b0", "#a8b5a2", "#c4cbbc", "#9eab97"], hair=["#d8d8d0", "#6b6b66", "#3a3a36", "#a8a89c"]),
}


def face(race: str, v: int) -> str:
    pen = Pen(hash((race, v)) % 100000 + v * 7919)
    rng = random.Random(v * 131 + len(race))
    skin = RACE[race]["skins"][v % 4]
    hair = RACE[race]["hair"][(v * 3 + 1) % 4]
    skin_dark = shade(skin, 0.78)
    parts = []

    # Hombros y camiseta (azur con ribete de oro), pequeños: la cabeza manda.
    shoulders = pen.wobble([(28, 256), (40, 214), (78, 196), (128, 192), (178, 196), (216, 214), (228, 256)], 1.5)
    parts.append(pen.shape(shoulders + [(228, 262), (28, 262)], KIT, 6))
    parts.append(pen.line([(98, 200), (128, 226), (158, 200)], 5))
    parts.append(pen.shape(pen.wobble([(104, 228), (152, 228), (150, 238), (106, 238)], 1), TRIM, 4))

    # Cabeza: forma por raza.
    cx, cy = 128, 112
    if race == "orc":
        rx, ry = 78, 70
        squash = lambda a: 1.0 + 0.10 * max(0.0, math.sin(a)) ** 2  # mandíbula ancha abajo
    elif race == "dwarf":
        rx, ry = 76, 66
        squash = None
    elif race == "elf":
        rx, ry = 62, 72
        squash = lambda a: 1.0 - 0.12 * max(0.0, math.sin(a)) ** 3  # barbilla fina
    elif race == "undead":
        rx, ry = 64, 72
        squash = lambda a: 1.0 - 0.18 * max(0.0, math.sin(a)) ** 4  # cráneo
    else:
        rx, ry = 66, 68
        squash = None

    # Pelo de detrás (melenas del elfo y largo del humano).
    if race == "elf" or (race == "human" and v % 4 == 2):
        back = pen.wobble([(cx - rx - 8, cy - 10), (cx - rx - 14, cy + 60), (cx - rx + 4, cy + 96), (cx, cy + 40),
                           (cx + rx - 4, cy + 96), (cx + rx + 14, cy + 60), (cx + rx + 8, cy - 10), (cx, cy - ry - 8)], 2)
        parts.append(pen.shape(back, hair, 6))

    # Orejas.
    if race == "elf":
        for s in (-1, 1):
            parts.append(pen.shape(pen.wobble([(cx + s * (rx - 6), cy - 4), (cx + s * (rx + 52), cy - 34), (cx + s * (rx - 2), cy + 18)], 1.2), skin, 5))
    elif race == "orc":
        for s in (-1, 1):
            parts.append(pen.shape(pen.wobble([(cx + s * (rx - 8), cy - 8), (cx + s * (rx + 26), cy - 22), (cx + s * (rx - 4), cy + 16)], 1.2), skin, 5))
    else:
        for s in (-1, 1):
            parts.append(pen.shape(pen.ellipse(cx + s * (rx - 2), cy + 8, 12, 16, 10, 0.8), skin, 5))

    head = pen.ellipse(cx, cy, rx, ry, 18, 1.6, squash)
    parts.append(pen.shape(head, skin, 6.5))
    # Sombra plana a un lado (una sola forma, recortada a la cabeza).
    parts.append(f'<clipPath id="h{race}{v}"><path d="{pen.smooth(head)}"/></clipPath>')
    # Media luna: la cabeza menos ella misma desplazada hacia la luz (arriba a la izquierda).
    parts.append(f'<mask id="m{race}{v}"><rect width="256" height="256" fill="white"/>'
                 f'<ellipse cx="{cx - 14}" cy="{cy - 12}" rx="{rx * 0.98:.0f}" ry="{ry * 0.98:.0f}" fill="black"/></mask>')
    parts.append(f'<path d="{pen.smooth(head)}" fill="{skin_dark}" mask="url(#m{race}{v})" clip-path="url(#h{race}{v})"/>')
    # Rayado de lápiz en la sombra (grease pencil), muy fino.
    hatch = "".join(f'<line x1="{cx + 20 + k * 9}" y1="{cy + 20}" x2="{cx + 60 + k * 9}" y2="{cy - 30}" stroke="{INK}" stroke-width="1.6" opacity="0.35"/>' for k in range(-2, 6))
    parts.append(f'<g mask="url(#m{race}{v})" clip-path="url(#h{race}{v})">{hatch}</g>')

    # Ojos: óvalos de tinta con brillo; cejas por variante (enfado, sorna, sorpresa).
    ey = cy + (2 if race != "dwarf" else -4)
    gap = 26 if race != "orc" else 30
    brow = [(-10, -4), (-4, 4), (-12, -6), (0, 0), (-8, 6), (-14, -2), (2, -6), (-6, -8)][v]
    for s in (-1, 1):
        ex = cx + s * gap
        if race == "undead":
            parts.append(pen.shape(pen.ellipse(ex, ey, 15, 13, 12, 1.0), "#2a2a24", 4))
            glow = "#9fe07a" if (v + (s > 0)) % 2 else "#2a2a24"
            parts.append(f'<circle cx="{ex}" cy="{ey}" r="4.5" fill="{glow}"/>')
        elif race == "human" and v % 4 == 3 and s > 0:
            parts.append(pen.shape(pen.ellipse(ex, ey, 15, 12, 10, 0.8), INK, 3))  # parche
            parts.append(pen.line([(ex - 14, ey - 10), (cx + 70, ey - 30)], 3.5))
        else:
            parts.append(pen.shape(pen.ellipse(ex, ey, 7.5, 10, 10, 0.6), INK, 1.5))
            parts.append(f'<circle cx="{ex - 2}" cy="{ey - 3}" r="2.6" fill="#fff6d8"/>')
        bx, by = ex, ey - 20
        parts.append(pen.line([(bx - 13 * s * -1 if False else bx - 13, by + (brow[0] if s < 0 else brow[1])), (bx + 13, by + (brow[1] if s < 0 else brow[0]))], 6))

    # Nariz.
    if race in ("human", "dwarf"):
        big = 1.5 if race == "dwarf" else 1.0
        parts.append(pen.shape(pen.ellipse(cx + 2, cy + 24, 9 * big, 8 * big, 10, 0.6), shade(skin, 0.9), 4.5))
    elif race == "orc":
        parts.append(pen.line([(cx - 8, cy + 26), (cx - 3, cy + 30)], 4))
        parts.append(pen.line([(cx + 8, cy + 26), (cx + 3, cy + 30)], 4))
    elif race == "undead":
        parts.append(pen.shape(pen.wobble([(cx - 6, cy + 30), (cx + 6, cy + 30), (cx, cy + 20)], 0.6), "#2a2a24", 3))

    # Colorete (menos el no-muerto).
    if race != "undead":
        for s in (-1, 1):
            parts.append(f'<ellipse cx="{cx + s * 42}" cy="{cy + 30}" rx="11" ry="6" fill="#e07a6a" opacity="0.45"/>')

    # Boca.
    my = cy + 46
    if race == "orc":
        parts.append(pen.shape(pen.wobble([(cx - 30, my - 4), (cx + 30, my - 4), (cx + 24, my + 10), (cx - 24, my + 10)], 1), "#5a1410", 5))
        for s in (-1, 1):
            parts.append(pen.shape(pen.wobble([(cx + s * 22, my + 6), (cx + s * 30, my - 22), (cx + s * 14, my + 2)], 0.6), "#fbf3dc", 4))
    elif race == "undead":
        parts.append(pen.shape(pen.wobble([(cx - 22, my - 6), (cx + 22, my - 6), (cx + 18, my + 6), (cx - 18, my + 6)], 1), "#efe9d8", 4.5))
        for k in range(-2, 3):
            parts.append(pen.line([(cx + k * 8, my - 6), (cx + k * 8, my + 6)], 2.5))
        parts.append(pen.line([(cx - 40, cy - 30), (cx - 20, cy - 44)], 3))  # costura
        for k in range(3):
            t = k / 2
            x, y = cx - 40 + 20 * t, cy - 30 - 14 * t
            parts.append(pen.line([(x - 4, y - 5), (x + 4, y + 5)], 2.5))
    else:
        kind = v % 3
        if kind == 0:
            parts.append(pen.line([(cx - 16, my), (cx, my + 6), (cx + 18, my - 2)], 5))
        elif kind == 1:
            parts.append(pen.shape(pen.wobble([(cx - 18, my - 2), (cx + 18, my - 2), (cx + 10, my + 10), (cx - 10, my + 10)], 0.8), "#5a1410", 4.5))
            parts.append(f'<rect x="{cx - 10}" y="{my - 2}" width="20" height="5" fill="#fbf3dc"/>')
        else:
            parts.append(pen.line([(cx - 14, my + 4), (cx + 14, my + 4)], 5))

    # Barba del enano (enorme, con trenzas) y del humano en alguna variante.
    if race == "dwarf" or (race == "human" and v % 4 == 1):
        size = 1.0 if race == "dwarf" else 0.6
        beard = pen.wobble([(cx - rx + 6, cy + 10), (cx - rx + 10, cy + 60 * size + 20), (cx - 30, cy + 70 + 40 * size),
                            (cx, cy + 80 + 46 * size), (cx + 30, cy + 70 + 40 * size), (cx + rx - 10, cy + 60 * size + 20),
                            (cx + rx - 6, cy + 10), (cx + 30, my - 8), (cx, my - 12), (cx - 30, my - 8)], 2)
        parts.append(pen.shape(beard, hair, 6))
        if race == "dwarf":
            for s in (-1, 1):
                parts.append(pen.shape(pen.ellipse(cx + s * 22, cy + 130, 9, 18, 10, 1), hair, 4.5))
                parts.append(pen.shape(pen.ellipse(cx + s * 22, cy + 148, 7, 5, 8, 0.5), TRIM, 3))
            parts.append(pen.line([(cx - 14, my), (cx + 16, my - 2)], 5))  # boca en la barba

    # Pelo / tocado de arriba.
    top = cy - ry
    hv = v % 4
    if race == "dwarf" and v % 2 == 0:
        helm = pen.wobble([(cx - rx - 6, cy - 14), (cx - rx + 4, top - 6), (cx, top - 22), (cx + rx - 4, top - 6), (cx + rx + 6, cy - 14)], 1.5)
        parts.append(pen.shape(helm + [(cx + rx + 6, cy - 6), (cx - rx - 6, cy - 6)], "#9aa0a6", 6))
        for s in (-1, 1):
            parts.append(pen.shape(pen.wobble([(cx + s * (rx - 6), top + 10), (cx + s * (rx + 34), top - 36), (cx + s * (rx + 6), top + 24)], 1), "#efe6d0", 5))
    elif race == "orc":
        knot = pen.ellipse(cx, top - 6, 16, 14, 10, 1.2)
        parts.append(pen.shape(knot, hair, 5))
        parts.append(pen.line([(cx - 22, top + 18), (cx - 10, top + 10)], 4))
    elif race == "undead":
        if hv in (0, 2):
            for k in range(-2, 3):
                parts.append(pen.line([(cx + k * 12, top + 6), (cx + k * 14 + 4, top - 10)], 3.5))
    elif race == "elf":
        fringe = pen.wobble([(cx - rx + 2, cy - 10), (cx - rx + 6, top + 6), (cx, top - 6), (cx + rx - 6, top + 6), (cx + rx - 2, cy - 10), (cx + 20, top + 30), (cx - 10, top + 18)], 1.5)
        parts.append(pen.shape(fringe, hair, 6))
    else:
        if hv == 0:
            tuft = pen.wobble([(cx - 50, top + 26), (cx - 30, top - 6), (cx, top - 14), (cx + 34, top - 4), (cx + 52, top + 24), (cx + 10, top + 14)], 1.5)
            parts.append(pen.shape(tuft, hair, 6))
        elif hv == 1:
            parts.append(pen.shape(pen.wobble([(cx - 8, top + 4), (cx, top - 30), (cx + 8, top + 4)], 1), hair, 5))  # cresta
        elif hv == 2:
            fringe = pen.wobble([(cx - rx + 2, cy - 4), (cx - rx + 4, top + 6), (cx, top - 8), (cx + rx - 4, top + 6), (cx + rx - 2, cy - 4), (cx, top + 26)], 1.5)
            parts.append(pen.shape(fringe, hair, 6))
        # hv == 3: calvo, con su parche.

    # Desgaste por raza y variante: lo que hace que no haya dos iguales y que se note que esto es carnicería.
    grit = (v * 5 + len(race) * 3) % 8
    if race == "orc":
        if grit in (0, 3, 6):  # pintura de guerra
            for s in (-1, 1):
                parts.append(f'<path d="M {cx + s * 18} {cy - 8} L {cx + s * 44} {cy + 4} L {cx + s * 18} {cy + 10} Z" fill="#8e1f1f" opacity="0.85"/>')
        if grit in (1, 4):  # aro en la nariz
            parts.append(f'<circle cx="{cx}" cy="{cy + 36}" r="7" fill="none" stroke="{TRIM}" stroke-width="3.5"/>')
        if grit in (2, 5, 7):  # colmillo roto: tapa la punta del derecho
            parts.append(f'<circle cx="{cx + 28}" cy="{cy + 22}" r="7" fill="{skin}"/>')
        if grit in (5, 6):  # casco de hierro
            cap = pen.wobble([(cx - rx + 6, cy - 22), (cx - rx + 14, top + 2), (cx, top - 12), (cx + rx - 14, top + 2), (cx + rx - 6, cy - 22)], 1.2)
            parts.append(pen.shape(cap + [(cx + rx - 6, cy - 14), (cx - rx + 6, cy - 14)], "#7d8086", 6))
            for k in (-30, 0, 30):
                parts.append(f'<circle cx="{cx + k}" cy="{cy - 24}" r="3.5" fill="{INK}"/>')
    elif race == "human":
        if grit in (1, 6):  # venda en la frente
            band = pen.wobble([(cx - rx + 4, cy - 34), (cx + rx - 4, cy - 40), (cx + rx - 2, cy - 26), (cx - rx + 6, cy - 20)], 1)
            parts.append(pen.shape(band, "#efe6d0", 4.5))
            parts.append(f'<circle cx="{cx + 20}" cy="{cy - 30}" r="5" fill="#b3121b" opacity="0.8"/>')
        if grit in (3, 7):  # barba de tres días
            for k in range(26):
                a = math.pi * (0.15 + 0.7 * k / 25)
                x, y = cx + math.cos(a) * rx * 0.72, cy + math.sin(a) * ry * 0.72 + 8
                parts.append(f'<circle cx="{x:.1f}" cy="{y:.1f}" r="1.3" fill="{INK}" opacity="0.55"/>')
    elif race == "elf":
        if grit in (0, 4):  # diadema
            parts.append(pen.shape(pen.wobble([(cx - rx + 8, cy - 34), (cx, cy - 46), (cx + rx - 8, cy - 34), (cx + rx - 8, cy - 28), (cx, cy - 40), (cx - rx + 8, cy - 28)], 0.8), TRIM, 3.5))
            parts.append(f'<circle cx="{cx}" cy="{cy - 42}" r="5" fill="#3f8fbf" stroke="{INK}" stroke-width="2.5"/>')
        if grit in (2, 6):  # tatuaje
            parts.append(pen.line([(cx - 40, cy + 12), (cx - 34, cy + 24), (cx - 40, cy + 34)], 3))
            parts.append(pen.line([(cx + 40, cy + 12), (cx + 34, cy + 24), (cx + 40, cy + 34)], 3))
    elif race == "dwarf":
        # Cejas pobladas: un manojo sobre cada ojo.
        for s in (-1, 1):
            parts.append(pen.shape(pen.ellipse(cx + s * 26, cy - 22, 16, 7, 8, 1.2), hair, 4))
        if grit in (1, 5):  # parche
            parts.append(pen.shape(pen.ellipse(cx - 26, cy - 4, 14, 12, 10, 0.8), INK, 3))
    elif race == "undead":
        if grit in (0, 3):  # mandíbula vendada
            parts.append(pen.shape(pen.wobble([(cx - 40, my - 12), (cx + 40, my - 16), (cx + 42, my), (cx - 38, my + 4)], 1.2), "#d8cfb4", 4))
        if grit in (2, 6):  # yelmo oxidado
            cap = pen.wobble([(cx - rx, cy - 18), (cx - rx + 10, top), (cx, top - 14), (cx + rx - 10, top), (cx + rx, cy - 18)], 1.2)
            parts.append(pen.shape(cap + [(cx + rx, cy - 10), (cx - rx, cy - 10)], "#8a5a3a", 6))
        if grit in (5, 7):  # gusano
            parts.append(pen.line([(cx + 30, cy - 50), (cx + 40, cy - 44), (cx + 36, cy - 36)], 4))

    # Cicatriz (variantes 2 y 5).
    if v in (2, 5) and race != "undead":
        sx = cx - 34 if v == 2 else cx + 30
        parts.append(pen.line([(sx - 6, cy - 16), (sx + 8, cy + 14)], 3.5))
        for k in range(3):
            y = cy - 10 + k * 10
            parts.append(pen.line([(sx - 6 + k * 4, y - 3), (sx + 6 + k * 4, y + 1)], 2.5))

    return f'<svg xmlns="http://www.w3.org/2000/svg" width="256" height="256" viewBox="0 0 256 256">{"".join(parts)}</svg>'


def paper(path: str) -> None:
    """Grano de papel multiplicado sobre la tinta y el color (no sobre el fondo transparente)."""
    import numpy as np
    from PIL import Image
    im = np.asarray(Image.open(path).convert("RGBA"), np.float32)
    grain = np.asarray(Image.open(os.path.join(ROOT, "Game", "Art", "Textures", "parchment_detail.png")).convert("L").crop((300, 300, 556, 556)), np.float32) / 255.0
    im[..., :3] *= (0.82 + 0.18 * grain / 0.93)[..., None]
    Image.fromarray(np.clip(im, 0, 255).astype(np.uint8), "RGBA").save(path, optimize=True)


def main() -> None:
    os.makedirs(OUT, exist_ok=True)
    for race in RACES:
        for v in range(VARIANTS):
            path = os.path.join(OUT, f"{race}_{v}.png")
            cairosvg.svg2png(bytestring=face(race, v).encode(), write_to=path, output_width=256, output_height=256)
            paper(path)
    print("caras dibujadas en", OUT)


if __name__ == "__main__":
    main()
