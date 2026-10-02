#!/usr/bin/env python3
"""BV-A: métricas de movimiento de los modelos 3D y hojas de contacto.

Lee lo que graba `-- movimiento <carpeta>` (Game/Screens/BroadcastCapture.Movement.cs):
  fotogramas.csv  una fila por fotograma REAL (30 fps fijos) y jugador: lo que se dibuja
  traza.csv       una fila por tick lógico y jugador: lo que escribió /Sim
  tramos.csv      los tramos grabados, con los jugadores en foco
  <tramo>_NNNN.jpg  fotogramas completos de los tramos con imágenes

Uso: tools/movimiento-analisis.py <carpeta> [--hojas <carpeta-salida>]
Solo lee y resume; determinista (sin aleatoriedad).
"""
import csv
import math
import sys
from collections import defaultdict

FPS = 30.0
TPS = 15.0
LOCO = {"idle", "gk_idle", "jog", "run"}
# Umbrales de PlayerModel.Pose (copiados para replicar su elección sobre la traza; si cambian allí, aquí también).
MOVING = 0.15
RUN = 1.9
FREE = {"Positioning", "Chasing", "Dribbling"}


def wrap(a):
    while a > math.pi:
        a -= 2 * math.pi
    while a < -math.pi:
        a += 2 * math.pi
    return a


def pct(values, p):
    if not values:
        return float("nan")
    v = sorted(values)
    k = min(len(v) - 1, max(0, int(round(p / 100.0 * (len(v) - 1)))))
    return v[k]


def load_frames(path):
    rows = defaultdict(list)  # (window, player) -> filas en orden de n
    with open(path, newline="") as f:
        for r in csv.DictReader(f):
            for k in ("n", "frame", "player", "team", "hasModel", "visible", "frozen"):
                r[k] = int(r[k])
            for k in ("alpha", "timescale", "x", "z", "yaw", "clipTime", "speedScale", "inputSpeed", "naturalSpeed", "sx", "sy"):
                r[k] = float(r[k])
            rows[(r["window"], r["player"])].append(r)
    return rows


def frame_metrics(rows, window, label=None):
    """Métricas de lo DIBUJADO en un tramo, sólo jugadores con modelo, visibles y en estado libre."""
    switches = 0
    flicker = 0
    seconds = 0.0
    yaw_jumps = []
    yaw_big = 0
    yaw_frames = 0
    turn_at_boundary = []
    slide = []
    in_place = 0
    in_place_total = 0
    for (w, p), seq in rows.items():
        if w != window or not seq[0]["hasModel"]:
            continue
        last_change = None
        prev = None
        prev2 = None
        for r in seq:
            if not r["visible"]:
                prev = None
                continue
            seconds += 1.0 / FPS
            if prev is not None:
                # Cambio de clip de locomoción (el parpadeo): A->B; y si vuelve a A en <= 0,3 s, parpadeo.
                if r["clip"] != prev["clip"] and r["clip"] in LOCO and prev["clip"] in LOCO:
                    switches += 1
                    if last_change is not None and r["n"] - last_change[0] <= 9 and last_change[1] == r["clip"]:
                        flicker += 1
                    last_change = (r["n"], prev["clip"])
                dyaw = abs(wrap(r["yaw"] - prev["yaw"]))
                yaw_frames += 1
                if dyaw > 1e-4:
                    yaw_jumps.append(math.degrees(dyaw))
                if math.degrees(dyaw) > 30:
                    yaw_big += 1
                dx, dz = r["x"] - prev["x"], r["z"] - prev["z"]
                body = math.hypot(dx, dz) * FPS
                feet = r["naturalSpeed"] * r["speedScale"]
                if r["state"] in FREE and r["clip"] in ("jog", "run") and r["naturalSpeed"] > 0.01:
                    in_place_total += 1
                    # Corriendo en el sitio = cuerpo quieto y pies andando. Con la reproducción parada y los
                    # pies también parados (ritmo 0) no cuenta: es una imagen congelada, que es lo correcto.
                    if body < 0.05 and feet > 0.05:
                        in_place += 1
                    elif body >= 0.05 and feet > 0.05:
                        slide.append(body / feet)
                if prev2 is not None:
                    ax, az = prev["x"] - prev2["x"], prev["z"] - prev2["z"]
                    if math.hypot(ax, az) > 1e-3 and math.hypot(dx, dz) > 1e-3:
                        turn_at_boundary.append(math.degrees(abs(wrap(math.atan2(dz, dx) - math.atan2(az, ax)))))
            prev2 = prev
            prev = r
    return {
        "segundos-jugador": seconds,
        "cambios de clip/s/jugador": switches / seconds if seconds else 0,
        "parpadeos (A-B-A <=0,3 s)/s/jugador": flicker / seconds if seconds else 0,
        "% fotogramas con giro de yaw >30°": 100.0 * yaw_big / yaw_frames if yaw_frames else 0,
        "% fotogramas con yaw cambiando": 100.0 * len(yaw_jumps) / yaw_frames if yaw_frames else 0,
        "giro de yaw cuando cambia (p50/p90/max °)": (pct(yaw_jumps, 50), pct(yaw_jumps, 90), max(yaw_jumps) if yaw_jumps else 0),
        "giro de la trayectoria entre fotogramas (p50/p90/p99 °)": (pct(turn_at_boundary, 50), pct(turn_at_boundary, 90), pct(turn_at_boundary, 99)),
        "patinaje cuerpo/pies (p10/p50/p90; 1 = pies clavados)": (pct(slide, 10), pct(slide, 50), pct(slide, 90)),
        "% fotogramas corriendo con el cuerpo quieto": 100.0 * in_place / in_place_total if in_place_total else 0,
    }


def gesture_metrics(rows, window, clips):
    """Gestos (clips que no son locomoción): cuánto duran en pantalla, si se cortan antes de acabar y si
    el cuerpo se desliza mientras suenan (un gesto en el sitio con el cuerpo yendo a >1 casilla/s)."""
    out = defaultdict(lambda: {"veces": 0, "seg": [], "cortado": 0, "frames": 0, "deslizando": 0})
    for (w, p), seq in rows.items():
        if w != window or not seq[0]["hasModel"]:
            continue
        cur = None
        start = 0
        maxt = 0.0
        for i, r in enumerate(seq):
            clip = r["clip"]
            if clip not in LOCO and clip and i > 0:
                g = out[clip]
                g["frames"] += 1
                body = math.hypot(r["x"] - seq[i - 1]["x"], r["z"] - seq[i - 1]["z"]) * FPS
                if body > 1.0:
                    g["deslizando"] += 1
            if clip != cur:
                if cur is not None and cur not in LOCO and cur:
                    g = out[cur]
                    g["veces"] += 1
                    g["seg"].append((i - start) / FPS)
                    length = clips.get(cur, 0)
                    if length and maxt < 0.9 * length and seq[i - 1]["clipTime"] >= 0:
                        g["cortado"] += 1
                cur, start, maxt = clip, i, 0.0
            if r["clipTime"] >= 0:
                maxt = max(maxt, r["clipTime"])
    res = {}
    for k, g in sorted(out.items()):
        res[k] = (f"veces {g['veces']}, en pantalla p50 {pct(g['seg'], 50):.2f}s (clip {clips.get(k, 0):.2f}s), "
                  f"cortados antes del 90 % {g['cortado']}, fotogramas deslizando >1 c/s {100.0 * g['deslizando'] / g['frames'] if g['frames'] else 0:.0f} %")
    return res


def trace_metrics(path):
    """Replica la elección de locomoción de PlayerModel.Pose sobre la traza ENTERA, tick a tick."""
    by_player = defaultdict(list)
    with open(path, newline="") as f:
        for r in csv.DictReader(f):
            by_player[int(r["player"])].append(r)
    switches = flicker = ticks = 0
    speeds = []
    near_threshold = 0
    turns = []
    for p, seq in by_player.items():
        if seq[0]["hasModel"] != "1":
            continue
        clips = []
        for f in range(len(seq) - 1):
            a, b = seq[f], seq[f + 1]
            if a["onPitch"] != "1" or b["onPitch"] != "1" or a["state"] not in FREE:
                clips.append(None)
                continue
            dx, dy = float(b["x"]) - float(a["x"]), float(b["y"]) - float(a["y"])
            step = math.hypot(dx, dy)
            if step > 0.6:
                clips.append(None)
                continue
            v = step * TPS
            speeds.append(v)
            if 1.6 <= v <= 2.2:
                near_threshold += 1
            clips.append("idle" if v <= MOVING else ("run" if v > RUN else "jog"))
            ticks += 1
            if f > 0:
                c = seq[f - 1]
                ex, ey = float(a["x"]) - float(c["x"]), float(a["y"]) - float(c["y"])
                if math.hypot(ex, ey) > 0.02 and step > 0.02 and c["state"] in FREE:
                    turns.append(math.degrees(abs(wrap(math.atan2(dy, dx) - math.atan2(ey, ex)))))
        for i in range(1, len(clips)):
            if clips[i] and clips[i - 1] and clips[i] != clips[i - 1]:
                switches += 1
                if i >= 2 and clips[i - 2] == clips[i]:
                    flicker += 1
    secs = ticks / TPS
    hist = defaultdict(int)
    for v in speeds:
        hist[min(int(v / 0.25), 14)] += 1
    return {
        "segundos-jugador (estado libre)": secs,
        "cambios de clip/s/jugador (réplica)": switches / secs if secs else 0,
        "parpadeos A-B-A en 2 ticks/s/jugador": flicker / secs if secs else 0,
        "% ticks con velocidad 1,6-2,2 c/s (al lado del umbral 1,9)": 100.0 * near_threshold / len(speeds) if speeds else 0,
        "giro entre ticks (p50/p90/p99 °)": (pct(turns, 50), pct(turns, 90), pct(turns, 99)),
        "% ticks con giro >45°": 100.0 * sum(1 for t in turns if t > 45) / len(turns) if turns else 0,
        "histograma velocidad (c/s, 0,25)": {f"{k*0.25:.2f}": hist[k] for k in sorted(hist)},
    }


def contact_sheets(folder, rows, out_dir):
    from PIL import Image, ImageDraw

    windows = []
    with open(f"{folder}/tramos.csv", newline="") as f:
        for r in csv.DictReader(f):
            if r["images"] == "1":
                windows.append((r["label"], [int(x) for x in r["focus"].split()]))
    import os

    os.makedirs(out_dir, exist_ok=True)
    made = []
    for label, focus in windows:
        seqs = {p: rows.get((label, p), []) for p in focus}
        lead = seqs[focus[0]]
        if len(lead) < 16:
            continue
        # Los 16 fotogramas más movidos del jugador principal: cambios de clip + giros de yaw > 20°.
        score = [0] * len(lead)
        for i in range(1, len(lead)):
            if lead[i]["clip"] != lead[i - 1]["clip"]:
                score[i] += 3
            if abs(math.degrees(wrap(lead[i]["yaw"] - lead[i - 1]["yaw"]))) > 20:
                score[i] += 1
        if label in ("reanudacion", "pausa"):
            # Lo que importa es la transición congelado -> reanudado: se centra en ella.
            idx = next((i for i in range(1, len(lead)) if (lead[i]["frozen"] or lead[i]["frame"] == lead[i - 1]["frame"] and lead[i]["alpha"] == lead[i - 1]["alpha"])), 8)
            start = max(0, min(len(lead) - 16, idx - 4))
        else:
            start = max(range(0, len(lead) - 15), key=lambda s: (sum(score[s:s + 16]), -s))
        span = range(start, start + 16)
        # sx/sy salen en unidades del lienzo (1280 de ancho, project.godot, stretch canvas_items); la imagen
        # es la ventana real. Se escala por el cociente, medido en la primera imagen.
        first = Image.open(f"{folder}/{label}_{lead[start]['n']:04d}.jpg")
        k = first.size[0] / 1280.0
        xs = [k * seqs[p][i]["sx"] for p in focus for i in span if i < len(seqs[p]) and seqs[p][i]["visible"]]
        ys = [k * seqs[p][i]["sy"] for p in focus for i in span if i < len(seqs[p]) and seqs[p][i]["visible"]]
        cx, cy = (min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2
        half = max(150, (max(xs) - min(xs)) / 2 + 80, (max(ys) - min(ys)) / 2 + 100)
        tiles = []
        for i in span:
            r = lead[i]
            img = Image.open(f"{folder}/{label}_{r['n']:04d}.jpg").convert("RGB")
            box = (int(cx - half), int(cy - half - 20), int(cx + half), int(cy + half - 20))
            tile = img.crop(box).resize((360, 360))
            d = ImageDraw.Draw(tile)
            d.rectangle((0, 0, 360, 34), fill=(0, 0, 0))
            d.text((4, 2), f"n{r['n']} tick{r['frame']} a{r['alpha']:.2f}{' CONG' if r['frozen'] else ''}", fill=(255, 255, 255))
            d.text((4, 17), f"#{focus[0]} {r['clip']} t{r['clipTime']:.2f} x{r['speedScale']:.2f} yaw{math.degrees(r['yaw']):.0f}", fill=(255, 230, 120))
            tiles.append(tile)
        sheet = Image.new("RGB", (4 * 360, 4 * 360), (20, 20, 20))
        for k, t in enumerate(tiles):
            sheet.paste(t, ((k % 4) * 360, (k // 4) * 360))
        path = f"{out_dir}/hoja-{label}.png"
        sheet.save(path)
        made.append(path)
    return made


def main():
    folder = sys.argv[1]
    rows = load_frames(f"{folder}/fotogramas.csv")
    windows = sorted({w for (w, _) in rows})
    print("== REPLICA SOBRE LA TRAZA (partido entero, jugadores con modelo) ==")
    for k, v in trace_metrics(f"{folder}/traza.csv").items():
        print(f"  {k}: {v}")
    for w in windows:
        print(f"== DIBUJADO, tramo '{w}' ==")
        for k, v in frame_metrics(rows, w).items():
            if isinstance(v, tuple):
                v = "/".join(f"{x:.2f}" for x in v)
            elif isinstance(v, float):
                v = f"{v:.3f}"
            print(f"  {k}: {v}")
    clips = {}
    try:
        with open(f"{folder}/clips.csv", newline="") as f:
            for r in csv.DictReader(f):
                clips[r["clip"]] = float(r["length"])
                print(f"  clip {r['clip']}: {float(r['length']):.2f} s, bucle {r['loop']}, zancada {r['naturalSkelPerSecond']}/s (esqueleto)")
    except FileNotFoundError:
        pass
    for w in windows:
        print(f"== GESTOS, tramo '{w}' ==")
        for k, v in gesture_metrics(rows, w, clips).items():
            print(f"  {k}: {v}")
    if "--hojas" in sys.argv:
        out = sys.argv[sys.argv.index("--hojas") + 1]
        for p in contact_sheets(folder, rows, out):
            print("hoja:", p)


if __name__ == "__main__":
    main()
