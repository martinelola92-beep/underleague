#!/usr/bin/env python3
"""Construye el bloque de datos de data/economy/perk-values.json a partir de dos lotes de --perk-values.

Uso:  python3 tools/perk-values-table.py <loteA>/perk-values-by-match.csv <loteB>/perk-values-by-match.csv salida.json

Metodología (ADR 0070, ADR 0085, paquete AY paso 3): los dos lotes son semillas independientes y se SUMAN;
el valor de cada perk es la diferencia emparejada entre el brazo con el perk y su control (mismas
plantillas y semillas de partido, sin el perk), en milesimas de punto de tasa de victoria x2, al
horizonte de referencia (8) y a cada horizonte 1..L; la desviacion por fila es la mitad de la RMS de la
diferencia entre los dos lotes, horizonte a horizonte. Se ejecuta desde la raiz del repositorio (lee
data/perks/*.json para saber que perks acumulan entre partidos). No escribe el fichero final: el bloque se
pega a mano en perk-values.json, con su _doc actualizado (RT-057).
"""
import csv, json, math, sys, glob, collections

REF = 8

def load(path):
    cur = collections.defaultdict(lambda: [[0, 0, 0] for _ in range(64)])
    n = 0
    with open(path, encoding="utf-8") as f:
        for r in csv.DictReader(f):
            k = int(r["matchIndex"]); n = max(n, k + 1)
            cur[r["perk"]][k][0] += int(r["matches"])
            cur[r["perk"]][k][1] += int(r["wins"])
            cur[r["perk"]][k][2] += int(r["controlWins"])
    return {p: v[:n] for p, v in cur.items()}, n

def value_at(curve, h):
    m = sum(c[0] for c in curve[:h]); w = sum(c[1] for c in curve[:h]); cw = sum(c[2] for c in curve[:h])
    return int(round(1000.0 * (w - cw) / m * 2.0)) if m else 0

lotA, nA = load(sys.argv[1]); lotB, nB = load(sys.argv[2])
assert nA == nB, (nA, nB)
H = nA
assert set(lotA) == set(lotB), set(lotA) ^ set(lotB)
perks = sorted(lotA)
summed = {p: [[lotA[p][k][i] + lotB[p][k][i] for i in range(3)] for k in range(H)] for p in perks}
acc = {json.load(open(f, encoding="utf-8"))["id"] for f in glob.glob("data/perks/*.json")
       if json.load(open(f, encoding="utf-8"))["accumulatesAcrossMatches"]}

values = {p: value_at(summed[p], REF) for p in perks}
by_h = {p: [value_at(summed[p], h) for h in range(1, H + 1)] for p in perks if p in acc}

def dev(h):
    d = [value_at(lotA[p], h) - value_at(lotB[p], h) for p in perks]
    return math.sqrt(sum(x * x for x in d) / len(d)) / 2.0

devs = [dev(h) for h in range(1, H + 1)]
mean = sum(values.values()) / len(values)
out = {
    "values": values,
    "valuesByHorizon": by_h,
    "rowDeviation": int(round(devs[REF - 1])),
    "rowDeviationByHorizon": [int(round(x)) for x in devs],
}
json.dump(out, open(sys.argv[3], "w", encoding="utf-8"), indent=1)
print(json.dumps({
    "perks": len(perks), "matchesAtRef": sum(c[0] for c in summed[perks[0]][:REF]),
    "rowDeviation": out["rowDeviation"], "rowDeviationByHorizon": out["rowDeviationByHorizon"],
    "mean": round(mean, 1), "dispersion": round(math.sqrt(sum((v - mean) ** 2 for v in values.values()) / len(values)), 1),
    "min": min(values.items(), key=lambda kv: kv[1]), "max": max(values.items(), key=lambda kv: kv[1]),
    "negatives": sorted([(v, p) for p, v in values.items() if v < 0]),
}, ensure_ascii=False, indent=1))
