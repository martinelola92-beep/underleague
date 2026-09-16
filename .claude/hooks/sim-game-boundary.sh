#!/usr/bin/env bash
# Avisa si un `git commit` va a mezclar /Sim (o /Sim.Tests, /Balance) y /Game en el mismo commit.
# Lección con nombre propio: el 13 sep 2026 se colaron asi cambios de /Game en dos commits de /Sim
# y /data, rompiendo la regla de CLAUDE.md de que un commit no mezcla /Sim y /Game.
set -euo pipefail
input="$(cat)"
command="$(echo "$input" | python3 -c 'import json,sys; print(json.load(sys.stdin).get("tool_input", {}).get("command", ""))' 2>/dev/null || true)"

case "$command" in
  *"git commit"*) ;;
  *) exit 0 ;;
esac

cd "$(git rev-parse --show-toplevel 2>/dev/null || echo .)" 2>/dev/null || exit 0
staged="$(git diff --cached --name-only 2>/dev/null || true)"
[ -z "$staged" ] && exit 0

has_sim=false
has_game=false
while IFS= read -r f; do
  case "$f" in
    Sim/*|Sim.Tests/*|Balance/*) has_sim=true ;;
    Game/*) has_game=true ;;
  esac
done <<< "$staged"

if $has_sim && $has_game; then
  echo "AVISO (sim-game-boundary): el índice mezcla /Sim (o /Sim.Tests, /Balance) y /Game." >&2
  echo "CLAUDE.md: 'un commit no mezcla /Sim y /Game'. Revisa 'git status' antes de seguir." >&2
fi
exit 0
