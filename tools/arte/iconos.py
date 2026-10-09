#!/usr/bin/env python3
"""Rasteriza los iconos de game-icons.net (CC BY 3.0) que usa la interfaz, uno por glifo de `InkIcons.Glyph`.

Blanco sobre transparente: la interfaz los tiñe (tinta, oro, rojo). Los SVG originales se descargan con
`--descargar` a `out/arte/gi/` (no se versionan); los PNG resultantes y `Game/Art/Icons/CREDITS.md`, sí.

Uso:  out/arte-venv/bin/python tools/arte/iconos.py [--descargar]
"""
from __future__ import annotations

import io
import os
import sys
import urllib.request
import zipfile

import cairosvg

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SRC = os.path.join(ROOT, "out", "arte", "gi", "icons", "ffffff", "transparent", "1x1")
OUT = os.path.join(ROOT, "Game", "Art", "Icons")
ARCHIVE = "https://game-icons.net/archives/svg/zip/ffffff/transparent/game-icons.net.svg.zip"
SIZE = 128

# Glifo de InkIcons -> icono de game-icons.net (autor/nombre). Sólo los que el arte de referencia pinta como
# silueta de tinta (atributos, puestos, rasgos, estado, acciones); objetos, consumibles, cofre y poción siguen
# dibujándose por código en color hasta que tengan ilustración propia.
ICONS = {
    "Strength": "lorc/fist",
    "Speed": "lorc/winged-leg",
    "Technique": "lorc/brain",
    "Stamina": "lorc/heart-organ",
    "Leash": "lorc/crossed-chains",
    "Goalkeeper": "delapouite/gloves",
    "Defender": "lorc/checked-shield",
    "Midfielder": "lorc/run",
    "Forward": "delapouite/soccer-kick",
    "Crown": "lorc/crown",
    "Bench": "delapouite/park-bench",
    "Shirt": "lucasms/shirt",
    "Pitch": "delapouite/soccer-field",
    "Swap": "lorc/back-forth",
    "Back": "lorc/return-arrow",
    "Zones": "skoll/divided-square",
    "Coverage": "delapouite/eye-target",
    "Fist": "lorc/punch-blast",
    "Runner": "lorc/sprint",
    "Ball": "delapouite/soccer-ball",
    "Target": "lorc/target-arrows",
    "Drop": "lorc/drop",
    "ShieldHeart": "delapouite/heart-shield",
    "WhiteFlag": "lorc/flying-flag",
    "Horn": "delapouite/mighty-horn",
    "Sleep": "lorc/sleepy",
    "Paw": "lorc/paw",
    "Wall": "delapouite/brick-wall",
    "Perk": "lorc/star-swirl",
    "Racial": "lorc/dna1",
    "Healthy": "skoll/hearts",
    "MinorInjury": "lorc/bandage-roll",
    "SevereInjury": "lorc/broken-bone",
    "Dead": "lorc/tombstone",
    "Info": "delapouite/info",
}

AUTHORS = {
    "lorc": "Lorc",
    "delapouite": "Delapouite",
    "skoll": "Skoll",
    "lucasms": "Lucas",
    "carl-olsen": "Carl Olsen",
}


def download() -> None:
    os.makedirs(os.path.dirname(os.path.dirname(os.path.dirname(SRC))), exist_ok=True)
    data = urllib.request.urlopen(ARCHIVE, timeout=120).read()
    zipfile.ZipFile(io.BytesIO(data)).extractall(os.path.join(ROOT, "out", "arte", "gi"))


def main() -> None:
    if "--descargar" in sys.argv or not os.path.isdir(SRC):
        download()
    os.makedirs(OUT, exist_ok=True)
    used: dict[str, list[str]] = {}
    for glyph, ref in sorted(ICONS.items()):
        svg = os.path.join(SRC, ref + ".svg")
        with open(svg, "rb") as f:
            raw = f.read()
        # El SVG trae un fondo transparente y la figura blanca; se rasteriza tal cual.
        cairosvg.svg2png(bytestring=raw, write_to=os.path.join(OUT, glyph + ".png"), output_width=SIZE, output_height=SIZE)
        author, name = ref.split("/")
        used.setdefault(author, []).append(f"`{name}` ({glyph})")
    lines = [
        "# Iconos de game-icons.net",
        "",
        "Licencia **CC BY 3.0** (https://creativecommons.org/licenses/by/3.0/). Hay que citarlos en los créditos del",
        "juego. Generados por `tools/arte/iconos.py`; no se editan a mano.",
        "",
    ]
    for author in sorted(used):
        lines.append(f"- **{AUTHORS.get(author, author)}** (https://game-icons.net): " + ", ".join(sorted(used[author])))
    with open(os.path.join(OUT, "CREDITS.md"), "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")
    print(f"{len(ICONS)} iconos en {OUT}")


if __name__ == "__main__":
    main()
