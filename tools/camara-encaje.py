#!/usr/bin/env python3
"""Modelo de la cámara en perspectiva del partido (ADR 0174, BA-F): mide cómo encaja el campo de 16x7 casillas
en 1280x800 para una elevación y un FOV, con el mismo algoritmo que `MatchPitchView3D.SolvePerspectiveFit`
(bisección de la distancia hasta que el césped cabe en ancho y en alto, y del desplazamiento vertical hasta
anclar el borde cercano).

Sirve para la tabla de la ADR 0174 y para descartar candidatas antes de gastar un barrido de capturas
(`-- camara 45/45 ...` en `Game/Scenes/CapturasRetransmision.tscn`, minutos por variante). Se validó contra las
esquinas que devuelve la cámara de Godot (`DebugPitchCorners`): distancias 22,55 / 13,54 / 15,23 / 14,01 / 15,67 /
15,85 de Godot contra 22,5 / 13,5 / 15,2 / 14,0 / 15,7 / 15,9 de aquí, para 45/30, 55/50, 50/45, 45/50, 40/45 y 35/45.

Uso:
    tools/camara-encaje.py                    # las candidatas de la ADR 0174
    tools/camara-encaje.py 45/45 50/45        # elevación/FOV (grados), FOV vertical a 16:10

Solo geometría: no lee ficheros ni toca `/Sim`. Las constantes del rectángulo son `PitchFit.Broadcast`.
Las alturas y radios de las fichas son los de RA-002 con `bodyRadius` de `data/races/` (0,855 y 0,38 el orco;
1,091 el elfo y 0,738 el enano, que comparten radio 0,30).
"""
import math
import sys

WIDTH, HEIGHT = 1280, 800
COLUMNS, ROWS = 16, 7

# PitchFit.Broadcast, en píxeles de 1280x800: bordes laterales del césped, límite superior del hueco, ancla del
# borde cercano y suelo que ninguna variante puede pasar (las tiras empiezan en 735).
X_MIN, X_MAX, Y_MIN, Y_NEAR, Y_FLOOR = 45, 1235, 200, 690, 735

ORC_HEIGHT, ORC_DIAMETER = 0.855, 0.76
ELF_HEIGHT, DWARF_HEIGHT = 1.091, 0.738
FAR_ROW, NEAR_ROW = 0.5, ROWS - 0.5


def project(point, camera, target, focal):
    """Cámara de Godot: mira de `camera` a `target` con arriba (0,1,0); devuelve (x, y, profundidad) en píxeles."""
    forward = [target[i] - camera[i] for i in range(3)]
    norm = math.sqrt(sum(v * v for v in forward))
    forward = [v / norm for v in forward]
    up = (0, 1, 0)
    right = [
        forward[1] * up[2] - forward[2] * up[1],
        forward[2] * up[0] - forward[0] * up[2],
        forward[0] * up[1] - forward[1] * up[0],
    ]
    norm = math.sqrt(sum(v * v for v in right))
    right = [v / norm for v in right]
    screen_up = [
        right[1] * forward[2] - right[2] * forward[1],
        right[2] * forward[0] - right[0] * forward[2],
        right[0] * forward[1] - right[1] * forward[0],
    ]
    rel = [point[i] - camera[i] for i in range(3)]
    x = sum(rel[i] * right[i] for i in range(3))
    y = sum(rel[i] * screen_up[i] for i in range(3))
    z = sum(rel[i] * forward[i] for i in range(3))
    return (WIDTH / 2 + focal * x / z, HEIGHT / 2 - focal * y / z, z)


def rig(elevation, fov, distance, pan):
    e = math.radians(elevation)
    screen_up = (0, math.cos(e), -math.sin(e))
    center = (COLUMNS / 2, 0, ROWS / 2)
    center = tuple(center[i] + screen_up[i] * pan for i in range(3))
    camera = tuple(center[i] + (0, math.sin(e), math.cos(e))[i] * distance for i in range(3))
    focal = (HEIGHT / 2) / math.tan(math.radians(fov) / 2)
    return camera, center, focal


def extent(elevation, fov, distance, pan):
    camera, target, focal = rig(elevation, fov, distance, pan)
    corners = [(0, 0, 0), (COLUMNS, 0, 0), (0, 0, ROWS), (COLUMNS, 0, ROWS)]
    projected = [project(c, camera, target, focal) for c in corners]
    xs, ys = [p[0] for p in projected], [p[1] for p in projected]
    return min(xs), max(xs), min(ys), max(ys)


def fit(elevation, fov):
    """(distancia, desplazamiento) como SolvePerspectiveFit: menor distancia que cabe, borde cercano en Y_NEAR."""
    def fits(d):
        x0, x1, y0, y1 = extent(elevation, fov, d, 0)
        return (x1 - x0) <= X_MAX - X_MIN and (y1 - y0) <= Y_FLOOR - Y_MIN

    high = 80.0
    while not fits(high) and high < 5000:
        high *= 2
    low = 1.0
    for _ in range(50):
        mid = (low + high) / 2
        if fits(mid):
            high = mid
        else:
            low = mid
    distance = high
    lo, hi = -20.0, 20.0
    for _ in range(50):
        mid = (lo + hi) / 2
        if extent(elevation, fov, distance, mid)[3] < Y_NEAR:
            lo = mid
        else:
            hi = mid
    return distance, (lo + hi) / 2


def row(elevation, fov):
    distance, pan = fit(elevation, fov)
    camera, target, focal = rig(elevation, fov, distance, pan)

    def ground(x, z):
        return project((x, 0, z), camera, target, focal)

    def body_height(z, height):
        bottom = project((COLUMNS / 2, 0, z), camera, target, focal)
        top = project((COLUMNS / 2, height, z), camera, target, focal)
        return abs(bottom[1] - top[1])

    def body_width(z, width):
        left = project((COLUMNS / 2 - width / 2, 0, z), camera, target, focal)
        right = project((COLUMNS / 2 + width / 2, 0, z), camera, target, focal)
        return abs(right[0] - left[0])

    near_width = ground(COLUMNS, ROWS)[0] - ground(0, ROWS)[0]
    far_width = ground(COLUMNS, 0)[0] - ground(0, 0)[0]
    horizontal_fov = 2 * math.degrees(math.atan(math.tan(math.radians(fov) / 2) * WIDTH / HEIGHT))
    token = body_width(FAR_ROW, ORC_DIAMETER) / body_width(NEAR_ROW, ORC_DIAMETER)
    far_cell = abs(ground(COLUMNS / 2, 0)[1] - ground(COLUMNS / 2, 1)[1])
    near_cell = abs(ground(COLUMNS / 2, ROWS - 1)[1] - ground(COLUMNS / 2, ROWS)[1])
    orc_far = body_height(FAR_ROW, ORC_HEIGHT) / body_width(FAR_ROW, ORC_DIAMETER)
    orc_near = body_height(NEAR_ROW, ORC_HEIGHT) / body_width(NEAR_ROW, ORC_DIAMETER)
    elf_dwarf_far = body_height(FAR_ROW, ELF_HEIGHT) - body_height(FAR_ROW, DWARF_HEIGHT)
    elf_dwarf_near = body_height(NEAR_ROW, ELF_HEIGHT) - body_height(NEAR_ROW, DWARF_HEIGHT)
    camera_height = distance * math.sin(math.radians(elevation))
    return (
        f"| {elevation:.0f}° | {fov:.0f}° / {horizontal_fov:.0f}° | {distance:.1f} | {camera_height:.1f} | "
        f"{far_width / near_width:.2f} | {ground(0, ROWS)[1] - ground(0, 0)[1]:.0f} px | "
        f"{far_cell:.0f} / {near_cell:.0f} px | {token:.2f} | "
        f"{orc_far:.2f} / {orc_near:.2f} | {elf_dwarf_far:.1f} / {elf_dwarf_near:.1f} px |"
    )


def main():
    if len(sys.argv) > 1:
        pairs = [tuple(float(v) for v in a.split("/")) for a in sys.argv[1:]]
    else:
        pairs = [(45, 30), (35, 45), (40, 45), (45, 45), (45, 50), (50, 45), (55, 50), (60, 55)]

    print(
        "| elevación | FOV (v / h) | distancia | altura (sobre el punto de mira) | lejano / cercano | alto del campo | "
        "casilla lejana / cercana | ficha lejana / cercana | alto / ancho del orco (lejana / cercana) | "
        "elfo menos enano en alto (lejana / cercana) |"
    )
    print("|---|---|---|---|---|---|---|---|---|---|")
    for elevation, fov in pairs:
        print(row(elevation, fov))


if __name__ == "__main__":
    main()
