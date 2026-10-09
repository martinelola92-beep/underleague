#!/usr/bin/env bash
# Importa los recursos nuevos de Game/ (texturas, modelos, shaders) sin dejar basura en el árbol.
# El import abre el editor de Godot, que re-guarda los .cs con tabuladores al principio de línea (9 oct 2026: se coló
# MatchPitchView3D.cs entero en un commit). Este script importa y devuelve esas sangrías a espacios, sin tocar nada más.
set -euo pipefail
cd "$(dirname "$0")/../.."
timeout 600 godot --headless --path Game --import >/dev/null 2>&1 || true
python3 - <<'PY'
import re, subprocess
for f in subprocess.check_output(['git', 'diff', '--name-only', '--', '*.cs']).decode().split():
    s = open(f).read()
    t = re.sub(r'(?m)^\t+', lambda m: m.group(0).replace('\t', '    '), s)
    if t != s:
        open(f, 'w').write(t)
        print('sangría restaurada:', f)
PY
