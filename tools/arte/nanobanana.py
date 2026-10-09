#!/usr/bin/env python3
"""Genera las ilustraciones del juego con Nano Banana (Gemini Image) a partir de tools/arte/encargo-nanobanana.json.

Necesita una clave de la API de Gemini en la variable GEMINI_API_KEY (https://aistudio.google.com/apikey).
Cada imagen cuesta dinero: el script dice cuántas va a pedir y el precio aproximado, y no llama a nada sin --si.

Uso:
  out/arte-venv/bin/python tools/arte/nanobanana.py                    # qué haría y cuánto costaría (no llama)
  out/arte-venv/bin/python tools/arte/nanobanana.py --si               # genera lo que falta
  out/arte-venv/bin/python tools/arte/nanobanana.py --si --grupo retratos --solo orc_0,orc_1
  out/arte-venv/bin/python tools/arte/nanobanana.py --si --forzar --solo orc_3   # rehace una pieza
  opciones: --modelo gemini-3-pro-image-preview  (más calidad, más caro; por defecto gemini-2.5-flash-image)

Gratis, a mano (app de Gemini o AI Studio, con su límite diario):
  out/arte-venv/bin/python tools/arte/nanobanana.py --hoja      # escribe out/arte/nanobanana/PROMPTS.md para copiar
  ... generas cada pieza en la app y guardas la imagen como <id>.png (orc_0.png…) en out/arte/nanobanana/entrada/
  out/arte-venv/bin/python tools/arte/nanobanana.py --importar  # recorta, reduce y coloca lo que haya en entrada/

Cada respuesta se guarda en bruto en out/arte/nanobanana/bruto/<grupo>/<id>.png (no versionado) y la pieza final,
recortada a cuadrado y reducida, en la ruta de 'salida' del grupo. Se anota la procedencia (modelo, prompt, fecha) en
Game/Art/nanobanana-procedencia.json.
"""
from __future__ import annotations

import argparse
import base64
import datetime
import io
import json
import os
import sys
import time
import urllib.error
import urllib.request

from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
ENCARGO = os.path.join(ROOT, "tools", "arte", "encargo-nanobanana.json")
BRUTO = os.path.join(ROOT, "out", "arte", "nanobanana", "bruto")
PROCEDENCIA = os.path.join(ROOT, "Game", "Art", "nanobanana-procedencia.json")
API = "https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent"

# Precio aproximado por imagen en dólares (orientativo; compruébalo en la página de precios de Gemini).
PRECIO = {"gemini-2.5-flash-image": 0.04, "gemini-3-pro-image-preview": 0.14}


def image_part(path: str) -> dict:
    with open(os.path.join(ROOT, path), "rb") as f:
        data = base64.b64encode(f.read()).decode()
    mime = "image/png" if path.lower().endswith(".png") else "image/jpeg"
    return {"inlineData": {"mimeType": mime, "data": data}}


def call(model: str, key: str, prompt: str, references: list[str], aspect: str | None) -> bytes:
    parts = [image_part(r) for r in references] + [{"text": prompt}]
    body: dict = {"contents": [{"parts": parts}], "generationConfig": {"responseModalities": ["IMAGE"]}}
    if aspect:
        body["generationConfig"]["imageConfig"] = {"aspectRatio": aspect}
    request = urllib.request.Request(
        API.format(model=model), data=json.dumps(body).encode(), method="POST",
        headers={"x-goog-api-key": key, "Content-Type": "application/json"})
    for attempt in range(4):
        try:
            with urllib.request.urlopen(request, timeout=180) as response:
                payload = json.load(response)
            break
        except urllib.error.HTTPError as e:
            detail = e.read().decode(errors="replace")[:400]
            if e.code in (429, 500, 503) and attempt < 3:
                wait = 20 * (attempt + 1)
                print(f"    {e.code}, reintento en {wait} s: {detail[:120]}")
                time.sleep(wait)
                continue
            raise SystemExit(f"error {e.code} de la API: {detail}")
    for candidate in payload.get("candidates", []):
        for part in candidate.get("content", {}).get("parts", []):
            inline = part.get("inlineData") or part.get("inline_data")
            if inline:
                return base64.b64decode(inline["data"])
    raise SystemExit(f"la respuesta no trae imagen: {json.dumps(payload)[:400]}")


def finish(raw: bytes, target: str, side: int | None) -> None:
    im = Image.open(io.BytesIO(raw)).convert("RGB")
    if side:
        w, h = im.size
        s = min(w, h)
        im = im.crop(((w - s) // 2, (h - s) // 2, (w + s) // 2, (h + s) // 2)).resize((side, side), Image.LANCZOS)
    os.makedirs(os.path.dirname(target), exist_ok=True)
    im.save(target, optimize=True)


ENTRADA = os.path.join(ROOT, "out", "arte", "nanobanana", "entrada")


def write_sheet(encargo: dict) -> None:
    """Una sección por pieza con el prompt completo, para pegarlo en la app de Gemini junto a la referencia."""
    lines = ["# Prompts para Nano Banana (a mano)", "",
             "En gemini.google.com (o aistudio.google.com) adjunta la imagen de referencia", "",
             "    " + ", ".join(os.path.join(ROOT, r) for r in encargo["referencias"]), "",
             f"pega el prompt de cada pieza y guarda la imagen como `<id>.png` en `{ENTRADA}`.",
             "Después: `out/arte-venv/bin/python tools/arte/nanobanana.py --importar`.", ""]
    for grupo in encargo["grupos"]:
        lines.append(f"## {grupo['id']}")
        lines.append("")
        for pieza in grupo["piezas"]:
            prompt = " ".join(x for x in (encargo["estilo"], grupo.get("estilo_extra", ""), pieza["prompt"]) if x)
            if grupo.get("aspecto"):
                prompt += f" Aspect ratio {grupo['aspecto']}."
            lines += [f"### {pieza['id']}", "", "```", prompt, "```", ""]
    path = os.path.join(ROOT, "out", "arte", "nanobanana", "PROMPTS.md")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    os.makedirs(ENTRADA, exist_ok=True)
    with open(path, "w", encoding="utf-8") as f:
        f.write("\n".join(lines))
    print("hoja en", path)


def import_manual(encargo: dict) -> None:
    targets = {}
    for grupo in encargo["grupos"]:
        for pieza in grupo["piezas"]:
            targets[pieza["id"]] = (grupo, os.path.join(ROOT, grupo["salida"].format(id=pieza["id"])))
    procedencia = {}
    if os.path.exists(PROCEDENCIA):
        with open(PROCEDENCIA, encoding="utf-8") as f:
            procedencia = json.load(f)
    done = 0
    for name in sorted(os.listdir(ENTRADA)) if os.path.isdir(ENTRADA) else []:
        stem, ext = os.path.splitext(name)
        if ext.lower() not in (".png", ".jpg", ".jpeg", ".webp") or stem not in targets:
            if ext:
                print(f"  ignoro {name}: no es <id>.png de ninguna pieza")
            continue
        grupo, target = targets[stem]
        with open(os.path.join(ENTRADA, name), "rb") as f:
            finish(f.read(), target, grupo.get("lado"))
        if target.startswith(os.path.join(ROOT, "Game")):
            procedencia[os.path.relpath(target, ROOT)] = {"modelo": "nano banana (app, a mano)", "fecha": datetime.date.today().isoformat()}
        print(f"  {stem} -> {os.path.relpath(target, ROOT)}")
        done += 1
    with open(PROCEDENCIA, "w", encoding="utf-8") as f:
        json.dump(procedencia, f, ensure_ascii=False, indent=1, sort_keys=True)
    print(f"{done} importadas")


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--si", action="store_true", help="llama de verdad a la API (cuesta dinero)")
    ap.add_argument("--forzar", action="store_true", help="regenera aunque la pieza ya exista")
    ap.add_argument("--grupo", default=None)
    ap.add_argument("--solo", default=None, help="ids separados por comas")
    ap.add_argument("--modelo", default="gemini-2.5-flash-image")
    ap.add_argument("--hoja", action="store_true", help="escribe la hoja de prompts para usarlos a mano")
    ap.add_argument("--importar", action="store_true", help="coloca las imágenes de out/arte/nanobanana/entrada/")
    args = ap.parse_args()

    with open(ENCARGO, encoding="utf-8") as f:
        encargo = json.load(f)

    if args.hoja:
        write_sheet(encargo)
        return
    if args.importar:
        import_manual(encargo)
        return
    only = set(args.solo.split(",")) if args.solo else None

    # Ya generada = anotada en la procedencia (los retratos renderizados de antes ocupan la misma ruta y se sustituyen).
    hechas = set()
    if os.path.exists(PROCEDENCIA):
        with open(PROCEDENCIA, encoding="utf-8") as f:
            hechas = set(json.load(f))

    todo = []
    for grupo in encargo["grupos"]:
        if args.grupo and grupo["id"] != args.grupo:
            continue
        for pieza in grupo["piezas"]:
            if only and pieza["id"] not in only:
                continue
            target = os.path.join(ROOT, grupo["salida"].format(id=pieza["id"]))
            rel = os.path.relpath(target, ROOT)
            done = rel in hechas if rel.startswith("Game") else os.path.exists(target)
            if done and not args.forzar:
                continue
            todo.append((grupo, pieza, target))

    precio = PRECIO.get(args.modelo, 0.05)
    print(f"{len(todo)} imágenes con {args.modelo}, ~{len(todo) * precio:.2f} $")
    for grupo, pieza, target in todo:
        print(f"  {grupo['id']}/{pieza['id']} -> {os.path.relpath(target, ROOT)}")
    if not args.si:
        print("(no se ha llamado a nada: añade --si para generar)")
        return
    key = os.environ.get("GEMINI_API_KEY")
    if not key:
        raise SystemExit("falta GEMINI_API_KEY (https://aistudio.google.com/apikey)")

    procedencia = {}
    if os.path.exists(PROCEDENCIA):
        with open(PROCEDENCIA, encoding="utf-8") as f:
            procedencia = json.load(f)

    for grupo, pieza, target in todo:
        prompt = " ".join(x for x in (encargo["estilo"], grupo.get("estilo_extra", ""), pieza["prompt"]) if x)
        print(f"  generando {grupo['id']}/{pieza['id']}…", flush=True)
        raw = call(args.modelo, key, prompt, grupo.get("referencias", encargo["referencias"]), grupo.get("aspecto"))
        raw_path = os.path.join(BRUTO, grupo["id"], pieza["id"] + ".png")
        os.makedirs(os.path.dirname(raw_path), exist_ok=True)
        with open(raw_path, "wb") as f:
            f.write(raw)
        finish(raw, target, grupo.get("lado"))
        if target.startswith(os.path.join(ROOT, "Game")):
            procedencia[os.path.relpath(target, ROOT)] = {
                "modelo": args.modelo, "prompt": prompt, "fecha": datetime.date.today().isoformat()}
            with open(PROCEDENCIA, "w", encoding="utf-8") as f:
                json.dump(procedencia, f, ensure_ascii=False, indent=1, sort_keys=True)
    print("hecho")


if __name__ == "__main__":
    sys.exit(main())
