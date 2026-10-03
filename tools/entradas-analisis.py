#!/usr/bin/env python3
"""BV-A (tercera pasada): entradas, faltas y caídas, medidas sobre lo que graba `-- movimiento <carpeta>`.

Lee traza.csv (posiciones y estados por tick), eventos.csv (TACKLE/FOUL/INJURY con el fotograma y los dos implicados como
índices de la traza), fotogramas.csv (lo dibujado a 30 fps, con la altura de la cadera `hipsY`) y pies.csv (perfil de los
clips). Responde, por suceso:

  - si el que entra llega al rival o golpea al aire: distancia entre los dos en el fotograma del suceso;
  - quién cae y cuándo respecto al suceso (desfase en ticks), y cuánto dura en el suelo según /Sim;
  - en lo dibujado (sólo jugadores con modelo): si la caída SE VE (la cadera baja de verdad), cuánto tiempo está en el
    suelo en pantalla, y si se levanta o «reaparece de pie» (la cadera sube en uno o dos fotogramas);
  - el golpe de la entrada: en qué segundo de su clip está el que entra cuando llega el suceso.

Uso: tools/entradas-analisis.py <carpeta> [<carpeta> ...]   Sólo lee; determinista.
"""
import csv
import math
import sys
from collections import defaultdict

FPS = 30.0
DOWN = {"KnockedDown", "Injured"}
# Cadera por debajo de esto = en el suelo; por encima de STAND = de pie. Medido en el propio instrumento: de pie la cadera
# del humano dibujado va a 0,49 casillas (medido, línea «cadera de pie» que imprime este script), tumbado a 0,08-0,15.
GROUND = 0.22
STAND = 0.45
# Distancia de cuerpo a cuerpo a la que una entrada puede tocar: dos radios de humano (0,32) más una pierna estirada
# en plancha. Provisional, sin medir: sólo separa «llega» de «golpea al aire» en el informe.
CONTACT = 0.9


def pct(v, p):
    if not v:
        return float("nan")
    v = sorted(v)
    return v[min(len(v) - 1, max(0, int(round(p / 100.0 * (len(v) - 1)))))]


def load(folder):
    trace = defaultdict(dict)  # player -> frame -> row
    with open(f"{folder}/traza.csv", newline="") as f:
        for r in csv.DictReader(f):
            trace[int(r["player"])][int(r["frame"])] = r
    events = []
    with open(f"{folder}/eventos.csv", newline="") as f:
        for r in csv.DictReader(f):
            events.append({k: (int(v) if k in ("frame", "tick", "actor", "opponent") else v) for k, v in r.items()})
    drawn = defaultdict(list)
    with open(f"{folder}/fotogramas.csv", newline="") as f:
        for r in csv.DictReader(f):
            # Los tramos cortos alrededor de cada contacto (caidaNN): cada uno es un episodio aparte.
            if r["window"].startswith("caida") and r["hasModel"] == "1":
                drawn[(r["window"], int(r["player"]))].append(r)
    return trace, events, drawn


def down_start(trace, p, frame, before=2, after=6):
    for g in range(frame - before, frame + after + 1):
        r = trace[p].get(g)
        prev = trace[p].get(g - 1)
        if r and prev and r["state"] in DOWN and prev["state"] not in DOWN:
            return g
    return None


def down_length(trace, p, start):
    g = start
    while trace[p].get(g) and trace[p][g]["state"] in DOWN:
        g += 1
    return g - start


def analyse(folder):
    trace, events, drawn = load(folder)
    out = {}
    dist = []
    air = 0
    falls = defaultdict(int)
    lag = []
    length = []
    kinds = defaultdict(int)
    for e in events:
        if e["type"] not in ("Tackle", "Foul", "Injury") or e["actor"] < 0:
            continue
        kinds[f'{e["type"]}:{e["detail"]}'] += 1
        a, o, f = e["actor"], e["opponent"], e["frame"]
        if e["type"] == "Tackle" and o >= 0 and trace[a].get(f) and trace[o].get(f):
            pa, po = trace[a][f], trace[o][f]
            d = math.hypot(float(pa["x"]) - float(po["x"]), float(pa["y"]) - float(po["y"]))
            dist.append(d)
            if d > CONTACT:
                air += 1
            for who, p in (("entra", a), ("recibe", o)):
                s = down_start(trace, p, f)
                if s is not None:
                    falls[f'{e["detail"]}: cae el que {who}'] += 1
                    lag.append(s - f)
                    length.append(down_length(trace, p, s))
    out["sucesos"] = dict(sorted(kinds.items()))
    out["distancia entre los dos en la entrada, casillas (p10/p50/p90/max)"] = (pct(dist, 10), pct(dist, 50), pct(dist, 90), max(dist) if dist else 0)
    out[f"entradas a más de {CONTACT} casillas (golpean al aire)"] = f"{air} de {len(dist)}"
    out["quién cae"] = dict(sorted(falls.items()))
    out["desfase caída − suceso, ticks (min/p50/max)"] = (min(lag) if lag else 0, pct(lag, 50), max(lag) if lag else 0)
    out["ticks en el suelo según /Sim (p10/p50/p90)"] = (pct(length, 10), pct(length, 50), pct(length, 90))

    # Lo dibujado: episodios de suelo de jugadores con modelo en el tramo largo.
    stand = []
    episodes = 0
    visible = 0
    ground_secs = []
    pops = 0
    rise_rates = []
    rise_clips = []
    reach_ground = []
    for p, seq in drawn.items():
        for r in seq:
            if r["state"] not in DOWN and r["clip"] in ("run", "jog") and r["visible"] == "1":
                stand.append(float(r["hipsY"]))
        i = 0
        while i < len(seq):
            if seq[i]["state"] in DOWN and (i == 0 or seq[i - 1]["state"] not in DOWN):
                j = i
                while j < len(seq) and seq[j]["state"] in DOWN:
                    j += 1
                episodes += 1
                hips = [float(seq[k]["hipsY"]) for k in range(i, min(len(seq), j + 45))]
                low = [k for k, h in enumerate(hips) if h < GROUND]
                if low:
                    visible += 1
                    reach_ground.append(low[0] / FPS)
                    ground_secs.append(len(low) / FPS)
                    # Cómo se levanta: desde el último fotograma en el suelo hasta el primero de pie.
                    last = low[-1]
                    up = next((k for k in range(last, len(hips)) if hips[k] > STAND), None)
                    if up is not None:
                        secs = (up - last) / FPS
                        rise_rates.append(secs)
                        rise_clips.append(f"{secs:.2f}s:{seq[min(len(seq) - 1, i + last)]['clip']}")
                        if secs <= 2 / FPS:
                            pops += 1
                i = j
            else:
                i += 1
    out["cadera de pie corriendo, casillas (p50)"] = pct(stand, 50)
    out["episodios de suelo dibujados (jugadores con modelo)"] = episodes
    out[f"caídas que se ven (cadera < {GROUND})"] = f"{visible} de {episodes}"
    out["tiempo hasta tocar el suelo, s (p50/max)"] = (pct(reach_ground, 50), max(reach_ground) if reach_ground else 0)
    out["tiempo en el suelo en pantalla, s (p10/p50/p90)"] = (pct(ground_secs, 10), pct(ground_secs, 50), pct(ground_secs, 90))
    out["levantarse: del suelo a de pie, s (p10/p50/p90)"] = (pct(rise_rates, 10), pct(rise_rates, 50), pct(rise_rates, 90))
    out["reaparece de pie (≤ 2 fotogramas)"] = f"{pops} de {len(rise_rates)}"
    out["cada levantada (segundos:clip al dejar el suelo)"] = " ".join(rise_clips)

    # El golpe: en qué segundo del clip `tackle` está quien entra en el fotograma del suceso (sólo con modelo).
    at_contact = []
    no_clip = 0
    by_frame = {}
    for (w, p), seq in drawn.items():
        for r in seq:
            by_frame.setdefault((p, int(r["frame"])), r)
    for e in events:
        if e["type"] == "Tackle":
            r = by_frame.get((e["actor"], e["frame"]))
            if r is None:
                continue
            if r["clip"] == "tackle":
                at_contact.append(float(r["clipTime"]))
            else:
                no_clip += 1
    out["clip de entrada en el fotograma del suceso: segundo del clip (p10/p50/p90)"] = (pct(at_contact, 10), pct(at_contact, 50), pct(at_contact, 90))
    out["entradas dibujadas sin el clip en el contacto"] = f"{no_clip} de {no_clip + len(at_contact)}"
    return out


def main():
    for folder in sys.argv[1:]:
        print(f"== {folder}")
        for k, v in analyse(folder).items():
            if isinstance(v, tuple):
                v = "/".join(f"{x:.2f}" if isinstance(x, float) else str(x) for x in v)
            elif isinstance(v, float):
                v = f"{v:.3f}"
            print(f"  {k}: {v}")


if __name__ == "__main__":
    main()
