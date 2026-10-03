#!/usr/bin/env python3
"""Hojas de contacto y MP4 de los peores casos de la batería de detectores (tools/barrido-detectores.sh).

Entrada: la carpeta que deja `godot ... -- movimiento <carpeta> <semilla> ventana <etiqueta> <tickIni> <tickFin> <ids|->`
(BroadcastCapture.Movement.cs, modo `ventana`): `fotogramas.csv`, `tramos.csv` y las imágenes `<etiqueta>_NNNN.jpg`.
Salida (en <salida>): `hoja-<etiqueta>.png`, 16 fotogramas repartidos por el tramo y recortados alrededor de los
jugadores de foco, con tick, estado y clip; y, con --mp4, `<etiqueta>.mp4` (ffmpeg de ~/.local/bin).

Uso: tools/detectores-hoja.py <carpeta-godot> <salida> [--mp4]
Solo lee lo grabado: no decide nada del partido (RT-014).
"""
import csv
import os
import subprocess
import sys

from PIL import Image, ImageDraw


def main():
    if len(sys.argv) < 3:
        print(__doc__)
        return 1
    folder, out_dir = sys.argv[1], sys.argv[2]
    want_mp4 = "--mp4" in sys.argv
    os.makedirs(out_dir, exist_ok=True)

    windows = []
    with open(f"{folder}/tramos.csv", newline="") as f:
        for r in csv.DictReader(f):
            if r["images"] == "1":
                windows.append((r["label"], [int(x) for x in r["focus"].split()]))

    rows = {}
    with open(f"{folder}/fotogramas.csv", newline="") as f:
        for r in csv.DictReader(f):
            rows.setdefault((r["window"], int(r["player"])), []).append(r)

    ffmpeg = os.path.expanduser("~/.local/bin/ffmpeg")
    for label, focus in windows:
        if not focus:
            continue
        lead = rows.get((label, focus[0]), [])
        if len(lead) < 2:
            continue
        step = max(1, len(lead) // 16)
        picks = [lead[min(len(lead) - 1, i * step)] for i in range(16)]
        first = Image.open(f"{folder}/{label}_{int(lead[0]['n']):04d}.jpg")
        k = first.size[0] / 1280.0  # sx/sy salen en unidades del lienzo (1280 de ancho)
        xs, ys = [], []
        for p in focus:
            for r in rows.get((label, p), []):
                if r["visible"] == "1":
                    xs.append(k * float(r["sx"]))
                    ys.append(k * float(r["sy"]))
        cx, cy = (min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2
        half = max(170, (max(xs) - min(xs)) / 2 + 90, (max(ys) - min(ys)) / 2 + 110)
        tiles = []
        for r in picks:
            img = Image.open(f"{folder}/{label}_{int(r['n']):04d}.jpg").convert("RGB")
            box = (int(cx - half), int(cy - half - 20), int(cx + half), int(cy + half - 20))
            tile = img.crop(box).resize((360, 360))
            d = ImageDraw.Draw(tile)
            d.rectangle((0, 0, 360, 34), fill=(0, 0, 0))
            d.text((4, 2), f"{label} n{r['n']} tick{r['frame']} a{float(r['alpha']):.2f}", fill=(255, 255, 255))
            d.text((4, 17), f"#{focus[0]} {r['state']} {r['clip']}", fill=(255, 230, 120))
            tiles.append(tile)
        sheet = Image.new("RGB", (4 * 360, 4 * 360), (20, 20, 20))
        for i, t in enumerate(tiles):
            sheet.paste(t, ((i % 4) * 360, (i // 4) * 360))
        path = f"{out_dir}/hoja-{label}.png"
        sheet.save(path)
        print("hoja:", path)
        if want_mp4 and os.path.exists(ffmpeg):
            subprocess.run(
                [ffmpeg, "-y", "-loglevel", "error", "-framerate", "30", "-start_number", str(int(lead[0]["n"])),
                 "-i", f"{folder}/{label}_%04d.jpg", "-vf", "scale=960:-2", "-pix_fmt", "yuv420p", f"{out_dir}/{label}.mp4"],
                check=False)
            print("mp4:", f"{out_dir}/{label}.mp4")
    return 0


if __name__ == "__main__":
    sys.exit(main())
