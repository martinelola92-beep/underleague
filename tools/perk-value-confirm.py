#!/usr/bin/env python3
"""Confirma (o descarta) el valor de uno o varios perks con N lotes independientes de --perk-values (ADR 0150).

Uso:  python3 tools/perk-value-confirm.py <lote1>/perk-values-by-match.csv <lote2>/... [--horizon 8]

Cada lote es una semilla distinta con el protocolo de la tabla (--rosters <= 500, campaña 16). El valor de
cada perk en cada lote es la diferencia emparejada 2000*(wins-controlWins)/matches hasta el horizonte; el
ET es la sd entre lotes / sqrt(N), CON ESA FILA, no el rowDeviation global de la tabla (BL-A, BL-B).

Veredicto (convención de la ADR 0150, no una cifra medida):
  PERJUICIO    media < -rowDeviation y media + t*ET < 0
  POSITIVO     media > +rowDeviation y media - t*ET > 0
con t el cuantil 97,5 % de la t de Student con N-1 grados de libertad (3,18 con 4 lotes): con pocos lotes
la sd es poco fiable y 2*ET se queda corto.
  SIN EFECTO   el resto (no distinguible del cero del instrumento a esta potencia)
Con menos de 4 lotes no da veredicto: la sd de 2-3 lotes no estima nada.

Los veredictos son LIKELY, nunca CONFIRMED (Regla F), y los lotes tienen que ser NUEVOS: no valen las
semillas de la tabla que marcó al candidato (ADR 0150). La herramienta se niega si recibe dos lotes
idénticos, lotes de más de 500 plantillas (el espacio de semillas del medidor colisiona), lotes con
distinto número de plantillas o campañas más cortas que el horizonte.

Opciones: --horizon H (8) · --row-deviation R (por defecto, el de data/economy/perk-values.json) ·
--sd-floor S: sd mínima por lote cuando hay una estimación externa mejor de la varianza de la fila (p. ej.
ocho lotes a 192 plantillas, escalada por sqrt(192/n)); el ET usa la mayor de las dos.
"""
import csv, json, math, sys, collections

args = [a for a in sys.argv[1:]]
H = 8
if "--horizon" in args:
    i = args.index("--horizon"); H = int(args[i + 1]); del args[i:i + 2]
sd_floor = 0.0
if "--sd-floor" in args:
    i = args.index("--sd-floor"); sd_floor = float(args[i + 1]); del args[i:i + 2]
row_dev_arg = None
if "--row-deviation" in args:
    i = args.index("--row-deviation"); row_dev_arg = int(args[i + 1]); del args[i:i + 2]
paths = args
if not paths:
    sys.exit(__doc__)

MAX_ROSTERS = 500  # Balance/PerkValueRunner.MaxRosters

# Cuantil 97,5 % de la t de Student por grados de libertad (tabla estándar).
T975 = {3: 3.182, 4: 2.776, 5: 2.571, 6: 2.447, 7: 2.365, 8: 2.306, 9: 2.262, 10: 2.228, 12: 2.179, 15: 2.131}

def t975(df):
    return T975.get(df) or next((T975[k] for k in sorted(T975, reverse=True) if k <= df), 1.96 if df > 30 else 2.131)

row_dev = row_dev_arg if row_dev_arg is not None else json.load(open("data/economy/perk-values.json", encoding="utf-8"))["rowDeviation"]

per_lot = []
curves = []
rosters_seen = {}
for p in paths:
    acc = collections.defaultdict(lambda: [0, 0, 0])
    curve = collections.defaultdict(list)
    first = {}
    last_index = -1
    with open(p, encoding="utf-8") as f:
        for r in csv.DictReader(f):
            k = int(r["matchIndex"]); last_index = max(last_index, k)
            curve[r["perk"]].append((k, r["matches"], r["wins"], r["controlWins"]))
            if k == 0:
                first[r["perk"]] = int(r["matches"])
            if k < H:
                a = acc[r["perk"]]
                a[0] += int(r["matches"]); a[1] += int(r["wins"]); a[2] += int(r["controlWins"])
    if last_index + 1 < H:
        sys.exit(f"ERROR: {p} tiene una campaña de {last_index + 1} partidos, menor que el horizonte {H}")
    # En el partido 0 cada plantilla medida juega uno: matches es el número de plantillas con portador.
    top = max(first.values(), default=0)
    if top > MAX_ROSTERS:
        sys.exit(f"ERROR: {p} tiene {top} plantillas; por encima de {MAX_ROSTERS} el espejo recicla equipos (BL-A)")
    rosters_seen[p] = top
    # La misma semilla da la misma curva de un perk al bit aunque el lote mida otros perks: eso delata un
    # lote repetido, que la media contaría dos veces (revisión de la ADR 0150).
    cur = {k: tuple(sorted(v)) for k, v in curve.items()}
    for q, other in curves:
        same = [k for k in cur if k in other and cur[k] == other[k]]
        if same:
            sys.exit(f"ERROR: {p} y {q} dan la misma curva al bit para {same}: son la misma semilla, no dos lotes")
    curves.append((p, cur))
    per_lot.append({k: 2000.0 * (w - c) / m for k, (m, w, c) in acc.items() if m})

if len(set(rosters_seen.values())) > 1:
    sys.exit(f"ERROR: lotes con distinto número de plantillas {sorted(set(rosters_seen.values()))}: la media sin ponderar los mezclaría")

perks = sorted(set.intersection(*(set(l) for l in per_lot)))
n = len(per_lot)
print(f"{n} lotes · horizonte {H} · rowDeviation de la tabla {row_dev}")
print(f"{'perk':24} {'por lote':40} {'media':>7} {'ET':>6}  veredicto")
for k in perks:
    v = [l[k] for l in per_lot]
    mean = sum(v) / n
    if n < 4:
        et, verdict = float("nan"), "SIN VEREDICTO (<4 lotes)"
    else:
        sd = math.sqrt(sum((x - mean) ** 2 for x in v) / (n - 1))
        et = max(sd, sd_floor) / math.sqrt(n)
        t = t975(n - 1)
        if mean < -row_dev and mean + t * et < 0:
            verdict = "PERJUICIO (LIKELY)"
        elif mean > row_dev and mean - t * et > 0:
            verdict = "POSITIVO (LIKELY)"
        else:
            verdict = "SIN EFECTO"
    lots = " ".join(f"{x:6.1f}" for x in v)
    print(f"{k:24} {lots:40} {mean:7.1f} {et:6.1f}  {verdict}")
