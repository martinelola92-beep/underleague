#!/usr/bin/env python3
"""Recalibracion de celdas de apuesta (ADR 0157, enmienda del 3 oct 2026).

Uso: tools/bet-recalibrar.py DIR [DIR...] [-- bet:dificultad ...]
Cada DIR es una salida de `Balance --full-runs N --bet-doctrine blind` con su bet-hits.csv; suma las
semillas. Para cada celda pedida (o, sin lista, para TODAS las de >= 100 tomadas que cambian de palabra
de la UI o cruzan Bonferroni) escribe: tomadas, cumplidas, medido %, error tipico binomial, anunciado %,
z, y la recalibracion que resultaria (frecuencia = medido, cobro = round(85 / medido), mismo criterio que
`comeback` d1: ADR 0157, decision 5). El error tipico es binomial y subestima el de conglomerados por
semilla: por eso se imprime tambien el medido por DIR (semilla) para ver la dispersion real.
"""
import csv
import math
import sys


def band(p):
    return 0 if p < 5 else 1 if p < 12 else 2 if p < 25 else 3


WORDS = ["rara vez", "de vez en cuando", "a menudo", "casi la mitad"]


def main(argv):
    dirs, wanted = [], []
    it = iter(argv)
    for a in it:
        if a == "--":
            wanted = [tuple([x.split(":")[0], int(x.split(":")[1])]) for x in it]
            break
        dirs.append(a)
    cells, per = {}, {}
    for d in dirs:
        with open(f"{d}/bet-hits.csv", newline="") as f:
            for r in csv.DictReader(f):
                k = (r["bet"], int(r["difficulty"]))
                c = cells.setdefault(k, [0, 0, 0, float(r["announcedPercent"])])
                c[0] += int(r["taken"]); c[1] += int(r["met"]); c[2] += int(r["netGold"])
                per.setdefault(k, []).append((int(r["taken"]), int(r["met"])))
    n = sum(1 for c in cells.values() if c[0] >= 100)
    alpha, lo, hi = 0.05 / max(1, n), 0.0, 10.0
    for _ in range(60):
        mid = (lo + hi) / 2
        if math.erfc(mid / math.sqrt(2)) > alpha: lo = mid
        else: hi = mid
    print(f"celdas >=100: {n}; Bonferroni |z| > {hi:.2f}")
    for k in sorted(cells):
        t, m, g, a = cells[k]
        if t < 100: continue
        p = m / t; pa = a / 100
        z = (m - t * pa) / math.sqrt(t * pa * (1 - pa)) if 0 < pa < 1 else 0.0
        flag = abs(z) > hi or band(100 * p) != band(a) or g > 0
        if (wanted and k not in wanted) or (not wanted and not flag): continue
        et = 100 * math.sqrt(p * (1 - p) / t)
        seeds = " ".join(f"{100*mm/tt:.1f}" if tt else "-" for tt, mm in per[k])
        pay = round(85 / (100 * p)) if p > 0 else 0
        print(f"{k[0]} d{k[1]}: {t} tomadas, medido {100*p:.2f} +- {et:.2f} % ({WORDS[band(100*p)]}), "
              f"anunciado {a:.2f} % ({WORDS[band(a)]}), z {z:+.1f}, neto {g:+d}; por semilla [{seeds}]; "
              f"recalibrado: freq {100*p:.2f}, cobro {round(8500/(100*p))}")


if __name__ == "__main__":
    main(sys.argv[1:])
