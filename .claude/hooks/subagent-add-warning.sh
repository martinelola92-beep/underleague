#!/usr/bin/env bash
# Avisa sobre 'git add -A'/'git add .': barre trabajo a medias de subagentes hacia el commit
# (lección de CLAUDE.md, 13 sep 2026). NOTA: no se puede detectar de forma fiable desde un hook de
# shell si hay subagentes vivos ahora mismo (ese estado vive en el proceso del harness, no en el
# arbol de procesos del sistema) — por eso este aviso es incondicional y breve, no un bloqueo.
set -euo pipefail
input="$(cat)"
command="$(echo "$input" | python3 -c 'import json,sys; print(json.load(sys.stdin).get("tool_input", {}).get("command", ""))' 2>/dev/null || true)"

case "$command" in
  *"git add -A"*|*"git add ."*|*"git add --all"*) ;;
  *) exit 0 ;;
esac

echo "AVISO (subagent-add-warning): 'git add' con comodín. Si hay subagentes trabajando en el mismo árbol," >&2
echo "usa rutas explícitas ('git add Sim Sim.Tests data docs') y mira 'git status' antes de commitear." >&2
exit 0
