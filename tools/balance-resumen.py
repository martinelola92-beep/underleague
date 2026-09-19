#!/usr/bin/env python3
"""Resume un summary.csv de /Balance en pocas líneas, para no leerlo entero (skill balance-measure).

    tools/balance-resumen.py out/X/summary.csv                     # solo lo que está fuera de banda
    tools/balance-resumen.py out/X/summary.csv --base out/B/summary.csv [--top 10] [--metric M ...]

Sin --base: recuento IN/OUT/INFO y cada fila OUT con su banda.
Con --base: además, las métricas que cambian de estado (IN->OUT, OUT->IN), las que desaparecen o aparecen,
y las --top que más se mueven: las de banda por |Δ| / anchura de banda, las INFO por |Δ| relativo.
--metric fuerza a mostrar esas métricas aunque no destaquen. No interpreta: el ruido entre semillas no está
en summary.csv, así que un Δ grande aquí es un candidato a mirar, no una conclusión (Regla F).
"""
import argparse
import csv
import sys


def load(path):
    with open(path, newline="", encoding="utf-8") as f:
        rows = list(csv.DictReader(f))
    if not rows or set(rows[0]) != {"metric", "value", "rangeMin", "rangeMax", "status"}:
        sys.exit(f"{path}: no tiene la cabecera metric,value,rangeMin,rangeMax,status")
    return {r["metric"]: r for r in rows}


def num(s):
    try:
        return float(s)
    except (TypeError, ValueError):
        return None


def band(r):
    lo, hi = r["rangeMin"] or "", r["rangeMax"] or ""
    return f"[{lo or '-∞'}, {hi or '∞'}]" if lo or hi else ""


def line(r, base=None):
    s = f"  {r['metric']} = {r['value']} {band(r)} {r['status']}"
    if base is not None:
        a, b = num(base["value"]), num(r["value"])
        d = f"{b - a:+.2f}" if a is not None and b is not None else "?"
        s += f"   (base {base['value']} {base['status']}, Δ {d})"
    return s


def score(r, base):
    a, b = num(base["value"]), num(r["value"])
    if a is None or b is None or a == b:
        return 0.0
    lo, hi = num(r["rangeMin"]), num(r["rangeMax"])
    if lo is not None and hi is not None and hi > lo:
        return abs(b - a) / (hi - lo)
    return abs(b - a) / max(abs(a), 1e-9)


def main():
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("summary")
    ap.add_argument("--base")
    ap.add_argument("--top", type=int, default=8)
    ap.add_argument("--metric", nargs="*", default=[])
    a = ap.parse_args()

    new = load(a.summary)
    count = {}
    for r in new.values():
        count[r["status"]] = count.get(r["status"], 0) + 1
    print(f"{a.summary}: {len(new)} métricas · " + " · ".join(f"{k} {v}" for k, v in sorted(count.items())))

    base = load(a.base) if a.base else None
    outs = [r for r in new.values() if r["status"] == "OUT"]
    print(f"FUERA DE BANDA ({len(outs)}):" if outs else "FUERA DE BANDA: ninguna")
    for r in outs:
        print(line(r, base.get(r["metric"]) if base else None))

    if base is not None:
        flips = [m for m in new if m in base and new[m]["status"] != base[m]["status"]]
        print(f"CAMBIAN DE ESTADO ({len(flips)}):" if flips else "CAMBIAN DE ESTADO: ninguna")
        for m in flips:
            print(line(new[m], base[m]))
        gone, added = sorted(set(base) - set(new)), sorted(set(new) - set(base))
        if gone:
            print("SOLO EN LA BASE: " + ", ".join(gone))
        if added:
            print("SOLO EN LA NUEVA: " + ", ".join(added))
        moved = sorted((m for m in new if m in base and m not in flips), key=lambda m: -score(new[m], base[m]))
        moved = [m for m in moved[: a.top] if score(new[m], base[m]) > 0]
        print(f"MÁS MOVIDAS (top {a.top}; Δ/anchura de banda, o Δ relativo si no hay banda):")
        for m in moved:
            print(line(new[m], base[m]) + f"  · {score(new[m], base[m]):.2f}")

    for m in a.metric:
        if m in new:
            print("PEDIDA:" + line(new[m], base.get(m) if base else None)[1:])
        else:
            print(f"PEDIDA: {m} no existe en {a.summary}")


if __name__ == "__main__":
    main()
