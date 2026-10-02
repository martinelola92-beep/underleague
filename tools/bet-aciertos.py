#!/usr/bin/env python3
"""Aciertos de la apuesta contra lo anunciado (ADR 0157, enmienda del 2 oct 2026).

Uso: tools/bet-aciertos.py DIR [DIR...]
Cada DIR es una salida de `Balance --full-runs N --bet-doctrine blind|...` con su bet-hits.csv.
Suma las celdas (condicion x dificultad) de todos los DIR y escribe: por condicion, por palabra de
la UI (los tramos de ScoutScreen.FrequencyKey: <5 %, 5-12 %, 12-25 %, >=25 %) y las celdas con
>=100 tomadas cuya z de calibracion supera el umbral de Bonferroni para el numero de celdas.
"""
import csv
import math
import sys

BANDS = ["rara vez (<5%)", "de vez en cuando (5-12%)", "a menudo (12-25%)", "casi la mitad (>=25%)"]


def band(announced_percent):
    return 0 if announced_percent < 5 else 1 if announced_percent < 12 else 2 if announced_percent < 25 else 3


def z_score(met, taken, p):
    var = taken * p * (1 - p)
    return (met - taken * p) / math.sqrt(var) if var > 0 else 0.0


def main(dirs):
    cells = {}
    for d in dirs:
        with open(f"{d}/bet-hits.csv", newline="") as f:
            for r in csv.DictReader(f):
                c = cells.setdefault((r["bet"], int(r["difficulty"])), [0, 0, 0, float(r["announcedPercent"])])
                c[0] += int(r["taken"])
                c[1] += int(r["met"])
                c[2] += int(r["netGold"])

    print("Por condicion: tomadas, cumplidas, medido %, anunciado % ponderado, z, oro neto, neto por apuesta")
    for bet in sorted({k[0] for k in cells}):
        sel = [c for k, c in cells.items() if k[0] == bet]
        taken = sum(c[0] for c in sel)
        met = sum(c[1] for c in sel)
        net = sum(c[2] for c in sel)
        exp = sum(c[0] * c[3] / 100 for c in sel)
        var = sum(c[0] * (c[3] / 100) * (1 - c[3] / 100) for c in sel)
        z = (met - exp) / math.sqrt(var) if var else 0.0
        if taken:
            print(f"{bet:20s} {taken:7d} {met:6d} {100*met/taken:6.2f} {100*exp/taken:6.2f} z {z:+.1f} oro {net:+d} {net/taken:+.2f}")

    print("\nPor palabra de la UI: tomadas, cumplidas, medido %, anunciado % ponderado, z")
    for b in range(4):
        sel = [c for c in cells.values() if band(c[3]) == b]
        taken = sum(c[0] for c in sel)
        met = sum(c[1] for c in sel)
        exp = sum(c[0] * c[3] / 100 for c in sel)
        var = sum(c[0] * (c[3] / 100) * (1 - c[3] / 100) for c in sel)
        z = (met - exp) / math.sqrt(var) if var else 0.0
        if taken:
            print(f"{BANDS[b]:28s} {taken:7d} {met:6d} {100*met/taken:6.2f} {100*exp/taken:6.2f} z {z:+.1f}")

    n = sum(1 for c in cells.values() if c[0] >= 100)
    # Umbral de Bonferroni bilateral al 5 % para n celdas: z tal que 2*(1-Phi(z)) = 0,05/n
    alpha = 0.05 / max(1, n)
    lo, hi = 0.0, 10.0
    for _ in range(60):
        mid = (lo + hi) / 2
        if math.erfc(mid / math.sqrt(2)) > alpha:
            lo = mid
        else:
            hi = mid
    print(f"\nCeldas con >=100 tomadas: {n}; umbral de Bonferroni |z| > {hi:.2f}")
    for (bet, d), c in sorted(cells.items()):
        if c[0] >= 100:
            z = z_score(c[1], c[0], c[3] / 100)
            marks = []
            if abs(z) > hi:
                marks.append("CALIBRACION fuera de Bonferroni")
            if c[2] > 0:
                marks.append("oro neto positivo")
            if band(100 * c[1] / c[0]) != band(c[3]):
                marks.append("otra palabra")
            if marks:
                print(f"  {bet} d{d}: {c[0]} tomadas, medido {100*c[1]/c[0]:.2f}% anunciado {c[3]:.2f}% z {z:+.1f} neto {c[2]:+d}  [{'; '.join(marks)}]")


if __name__ == "__main__":
    if len(sys.argv) < 2:
        sys.exit(__doc__)
    main(sys.argv[1:])
