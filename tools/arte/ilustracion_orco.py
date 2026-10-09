#!/usr/bin/env python3
"""Ilustración del busto del orco, dibujada a mano en SVG (9 oct 2026; referencia del revisor: el portero Bolg de su
mockup de la plantilla — semirrealista, ceño hundido, colmillos, sombra en tres tonos, rayado de tinta, pañuelo rojo,
hombrera de pinchos, fondo rojo raído).

Una sola ilustración trabajada, no un generador: las variantes (tono de piel, cicatrices, pañuelo, pendiente) se
aplican encima. Salida: out/arte/ilustracion/orco_<v>.png a 512 px.
Uso:  out/arte-venv/bin/python tools/arte/ilustracion_orco.py
"""
from __future__ import annotations

import os
import random

import cairosvg

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "out", "arte", "ilustracion")
INK = "#15100c"

SKINS = [  # (luz, medio, sombra)
    ("#9aae5c", "#738a3e", "#4a5e28"),
    ("#8fa356", "#687f38", "#435523"),
    ("#a3a65a", "#7c803a", "#525622"),
    ("#7f9c58", "#5f7c3c", "#3d5428"),
]


def hatch(x0, y0, x1, y1, n, dx, dy, w=2.0, op=0.55):
    out = []
    for k in range(n):
        t = k / max(n - 1, 1)
        x, y = x0 + (x1 - x0) * t, y0 + (y1 - y0) * t
        out.append(f'<path d="M{x:.0f} {y:.0f} l{dx} {dy}" stroke="{INK}" stroke-width="{w}" opacity="{op}" stroke-linecap="round"/>')
    return "".join(out)


def orc(v: int) -> str:
    rng = random.Random(v)
    light, mid, dark = SKINS[v % len(SKINS)]
    bandana = ["#9e1b1b", "#7a1414", "#a8321e", "#5a1a1a"][v % 4]
    S = f'stroke="{INK}" stroke-linejoin="round" stroke-linecap="round"'
    p = []

    # Fondo: paño rojo oscuro raído con rayas de pincel.
    p.append('<rect width="512" height="512" fill="#5c1712"/>')
    for k in range(14):
        y = 20 + k * 36 + rng.randint(-6, 6)
        p.append(f'<path d="M-10 {y} C120 {y - 10} 380 {y + 12} 530 {y - 4}" stroke="#3e0f0c" stroke-width="{rng.randint(6, 14)}" opacity="0.6" fill="none"/>')
    p.append('<path d="M0 0 L512 0 L512 90 C380 70 200 110 0 80 Z" fill="#2a0c0a" opacity="0.5"/>')

    # Hombros: cuero y hombrera de hierro con pinchos (izquierda del cuadro = hombro derecho del orco).
    p.append(f'<path d="M0 512 L0 420 C40 380 110 360 160 356 L352 356 C410 362 470 384 512 420 L512 512 Z" fill="#3a2a1e" {S} stroke-width="6"/>')
    p.append(f'<path d="M20 512 C30 430 70 392 150 380 L170 512 Z" fill="#2b1f16" opacity="0.7"/>')
    # Hombrera izquierda (de hierro, con remaches)
    p.append(f'<path d="M-10 420 C10 360 70 330 140 340 C150 380 120 430 60 460 C30 470 0 470 -10 470 Z" fill="#5d5f63" {S} stroke-width="7"/>')
    p.append(f'<path d="M0 450 C40 440 100 410 128 360" stroke="#8a8d92" stroke-width="5" fill="none"/>')
    for (x, y) in [(40, 380), (80, 365), (110, 395), (70, 425)]:
        p.append(f'<circle cx="{x}" cy="{y}" r="6" fill="#2a2a2c" {S} stroke-width="2"/>')
    for (x, y, a, b) in [(20, 360, -30, -60), (70, 338, -6, -70), (118, 340, 22, -62)]:
        p.append(f'<path d="M{x - 12} {y} L{x + a} {y + b} L{x + 12} {y + 4} Z" fill="#b7b9bc" {S} stroke-width="5"/>')
        p.append(f'<path d="M{x} {y} L{x + a} {y + b}" stroke="#6f7176" stroke-width="3"/>')
    # Hombrera derecha de pieles
    p.append(f'<path d="M370 360 C430 350 500 380 530 430 L530 512 L400 512 C410 470 390 420 370 360 Z" fill="#5a3d26" {S} stroke-width="7"/>')
    p.append(hatch(400, 390, 500, 430, 12, -14, 40, 3, 0.5))

    # Cuello grueso.
    p.append(f'<path d="M178 300 C176 340 168 366 160 380 L352 380 C344 360 336 340 334 300 Z" fill="{mid}" {S} stroke-width="7"/>')
    p.append(f'<path d="M300 310 C306 340 318 362 340 378 L352 380 C344 360 336 340 334 300 Z" fill="{dark}"/>')

    # Pañuelo rojo anudado al cuello.
    p.append(f'<path d="M150 360 C200 392 312 392 362 360 C372 380 368 404 350 420 C300 440 210 440 160 420 C142 402 140 380 150 360 Z" fill="{bandana}" {S} stroke-width="7"/>')
    p.append(f'<path d="M232 418 L196 492 L246 470 L268 500 L282 420 Z" fill="{bandana}" {S} stroke-width="6"/>')
    p.append(f'<path d="M170 410 C220 428 300 428 350 404" stroke="#3e0c0c" stroke-width="5" fill="none" opacity="0.7"/>')
    p.append(hatch(250, 430, 270, 470, 5, 12, 20, 2.5, 0.5))

    # Orejas puntiagudas, hacia atrás.
    p.append(f'<path d="M150 190 C110 170 70 150 30 150 C70 190 100 230 156 262 Z" fill="{mid}" {S} stroke-width="7"/>')
    p.append(f'<path d="M140 206 C110 190 86 178 64 172 C92 200 118 222 148 240" fill="{dark}" opacity="0.8"/>')
    p.append(f'<path d="M362 190 C402 170 442 150 482 150 C442 190 412 230 356 262 Z" fill="{mid}" {S} stroke-width="7"/>')
    p.append(f'<path d="M372 206 C402 190 426 178 448 172 C420 200 394 222 364 240" fill="{dark}" opacity="0.8"/>')
    if v % 3 == 1:
        p.append(f'<circle cx="452" cy="176" r="10" fill="none" stroke="#c9982f" stroke-width="5"/>')
        p.append(f'<circle cx="470" cy="166" r="8" fill="none" stroke="#c9982f" stroke-width="4"/>')

    # Cabeza: cráneo ancho, frente baja, mandíbula enorme y cuadrada.
    head = ("M176 120 C190 70 240 52 260 52 C290 52 330 70 340 120 C350 150 352 180 356 210 "
            "C366 236 370 268 356 300 C340 334 306 352 256 354 C206 352 172 334 156 300 "
            "C142 268 146 236 156 210 C160 180 164 150 176 120 Z")
    p.append(f'<path d="{head}" fill="{mid}" {S} stroke-width="8"/>')
    p.append(f'<clipPath id="hc"><path d="{head}"/></clipPath>')
    # Luz (tres cuartos desde arriba a la izquierda) y sombra a la derecha.
    p.append(f'<g clip-path="url(#hc)">'
             f'<path d="M160 120 C200 60 260 60 290 80 C270 140 240 200 230 260 C220 300 190 320 150 300 Z" fill="{light}"/>'
             f'<path d="M300 90 C330 120 350 170 356 230 C362 280 340 330 290 352 C320 300 326 220 300 90 Z" fill="{dark}"/>'
             f'{hatch(318, 160, 344, 300, 11, 18, 14, 2.4, 0.45)}'
             f'</g>')
    # Pelo: mechón ralo negro en la coronilla.
    p.append(f'<path d="M206 76 C220 50 250 36 266 40 C252 50 246 58 244 70 C260 52 282 48 300 56 C280 62 270 70 266 80 Z" fill="{INK}"/>')

    # Ceño: frente con arrugas y arco superciliar pesado.
    for k, (y, w) in enumerate([(118, 38), (132, 46), (146, 40)]):
        p.append(f'<path d="M{256 - w} {y + (2 if k % 2 else 0)} C{256 - w / 2} {y - 6} {256 + w / 2} {y - 6} {256 + w} {y}" stroke="{INK}" stroke-width="3.5" fill="none" opacity="0.75"/>')
    p.append(f'<path d="M180 176 C206 152 232 156 252 178 L246 186 C228 172 206 170 184 190 Z" fill="{dark}" {S} stroke-width="6"/>')
    p.append(f'<path d="M332 176 C306 152 280 156 260 178 L266 186 C284 172 306 170 328 190 Z" fill="{dark}" {S} stroke-width="6"/>')
    p.append(f'<path d="M250 170 L256 196 L262 170" stroke="{INK}" stroke-width="4" fill="none"/>')

    # Ojos hundidos, pequeños y furiosos (amarillo enfermizo).
    for s in (-1, 1):
        ex = 256 + s * 40
        p.append(f'<path d="M{ex - 24} 196 C{ex - 10} 186 {ex + 10} 186 {ex + 24} 198 C{ex + 10} 208 {ex - 10} 208 {ex - 24} 196 Z" fill="#1d1a10" {S} stroke-width="4"/>')
        p.append(f'<path d="M{ex - 16} 197 C{ex - 6} 191 {ex + 8} 191 {ex + 16} 198 C{ex + 6} 204 {ex - 6} 204 {ex - 16} 197 Z" fill="#e8c84a"/>')
        p.append(f'<circle cx="{ex + s * -2}" cy="198" r="4.5" fill="{INK}"/>')
        p.append(f'<circle cx="{ex - 4}" cy="195" r="1.6" fill="#fff6d0"/>')
        # Bolsas bajo los ojos
        p.append(f'<path d="M{ex - 22} 214 C{ex - 8} 222 {ex + 8} 222 {ex + 22} 212" stroke="{INK}" stroke-width="3" fill="none" opacity="0.7"/>')

    # Nariz chata de cerdo.
    p.append(f'<path d="M238 222 C240 206 272 206 274 222 C282 236 276 250 256 252 C236 250 230 236 238 222 Z" fill="{mid}" {S} stroke-width="6"/>')
    p.append(f'<path d="M262 214 C272 220 276 236 268 248 C276 244 280 230 274 222 Z" fill="{dark}"/>')
    p.append(f'<ellipse cx="246" cy="242" rx="5" ry="4" fill="{INK}"/><ellipse cx="266" cy="242" rx="5" ry="4" fill="{INK}"/>')
    # Surcos nasolabiales
    p.append(f'<path d="M226 230 C214 250 210 272 216 292" stroke="{INK}" stroke-width="4" fill="none" opacity="0.8"/>')
    p.append(f'<path d="M286 230 C298 250 302 272 296 292" stroke="{INK}" stroke-width="4" fill="none" opacity="0.8"/>')

    # Boca abierta en un gruñido, con dientes y dos colmillos de abajo enormes.
    mouth = "M206 284 C226 268 286 268 306 284 C304 306 290 322 256 324 C222 322 208 306 206 284 Z"
    p.append(f'<path d="{mouth}" fill="#3b0d0b" {S} stroke-width="7"/>')
    p.append(f'<clipPath id="mc"><path d="{mouth}"/></clipPath><g clip-path="url(#mc)">')
    for k in range(6):
        x = 216 + k * 15
        p.append(f'<path d="M{x} 276 L{x + 7} 292 L{x + 14} 276 Z" fill="#e9dfc0" {S} stroke-width="2.5"/>')
    for k in range(5):
        x = 224 + k * 14
        p.append(f'<path d="M{x} 324 L{x + 7} 310 L{x + 14} 324 Z" fill="#d8ccaa" {S} stroke-width="2.5"/>')
    p.append('</g>')
    broken = v % 2 == 1
    p.append(f'<path d="M210 300 C204 278 206 256 218 236 C222 258 226 280 226 304 Z" fill="#efe5c6" {S} stroke-width="5"/>')
    if broken:
        p.append(f'<path d="M286 304 C286 292 288 282 292 274 L302 272 C304 282 304 294 302 302 Z" fill="#efe5c6" {S} stroke-width="5"/>')
    else:
        p.append(f'<path d="M302 300 C308 278 306 256 294 236 C290 258 286 280 286 304 Z" fill="#efe5c6" {S} stroke-width="5"/>')
    p.append(f'<path d="M214 292 C212 274 214 258 218 246" stroke="#a89a78" stroke-width="3" fill="none"/>')
    # Barbilla partida y sombra de mandíbula
    p.append(f'<path d="M246 340 C252 346 260 346 266 340" stroke="{INK}" stroke-width="3.5" fill="none" opacity="0.8"/>')

    # Cicatrices por variante.
    if v % 4 in (0, 2):
        p.append(f'<path d="M210 140 L234 230" stroke="{INK}" stroke-width="5"/><path d="M210 140 L234 230" stroke="#c2766a" stroke-width="2"/>')
        for k in range(4):
            y = 156 + k * 20
            x = 214 + k * 5.3
            p.append(f'<path d="M{x - 8} {y} l16 -4" stroke="{INK}" stroke-width="3"/>')
    if v % 4 in (1, 3):
        p.append(f'<path d="M300 250 C318 262 326 280 324 300" stroke="{INK}" stroke-width="5" fill="none"/>'
                 f'<path d="M300 250 C318 262 326 280 324 300" stroke="#c2766a" stroke-width="2" fill="none"/>')

    # Salpicadura de barro/sangre en la mejilla.
    for k in range(5):
        x, y = 180 + rng.randint(0, 30), 250 + rng.randint(0, 30)
        p.append(f'<circle cx="{x}" cy="{y}" r="{rng.uniform(1.5, 4):.1f}" fill="#3a1410" opacity="0.7"/>')

    # Marco rasgado del paño.
    p.append(f'<rect x="4" y="4" width="504" height="504" fill="none" stroke="{INK}" stroke-width="10"/>')
    return f'<svg xmlns="http://www.w3.org/2000/svg" width="512" height="512" viewBox="0 0 512 512">{"".join(p)}</svg>'


def main() -> None:
    os.makedirs(OUT, exist_ok=True)
    for v in range(4):
        cairosvg.svg2png(bytestring=orc(v).encode(), write_to=os.path.join(OUT, f"orco_{v}.png"))
    print("orcos en", OUT)


if __name__ == "__main__":
    main()
