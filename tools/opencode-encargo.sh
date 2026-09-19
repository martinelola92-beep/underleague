#!/usr/bin/env bash
# Ejecuta un encargo cerrado con OpenCode en un worktree aislado y devuelve un informe corto,
# para que la sesión de Claude lea ~15 líneas en vez del trabajo entero. Skill: opencode-worker.
#
#   tools/opencode-encargo.sh run   <encargo.md> [proveedor/modelo]
#   tools/opencode-encargo.sh diff  <id> [ruta...]     # diff del worktree (incluye ficheros nuevos)
#   tools/opencode-encargo.sh apply <id>               # aplica el diff al árbol principal (sin preparar) y borra el worktree
#   tools/opencode-encargo.sh drop  <id>               # descarta el worktree
#
# <id> es el nombre del fichero de encargo sin .md. Variables: OC_TIMEOUT (s, por defecto 900).
set -uo pipefail

ROOT=$(git rev-parse --show-toplevel)
WTDIR=$ROOT/.claude/worktrees
MODEL_DEFAULT=opencode/big-pickle
MODEL_FALLBACK=opencode/muse-spark-1.3-contributor-free
TIMEOUT=${OC_TIMEOUT:-900}

strip() { sed 's/\x1b\[[0-9;]*m//g'; }
changed() { git -C "$1" status --porcelain --untracked-files=all | cut -c4- | sort -u; }

cmd_run() {
  local enc model id wt log permit verify
  enc=$(realpath "$1"); model=${2:-$MODEL_DEFAULT}
  id=$(basename "$enc" .md); wt=$WTDIR/oc-$id; log=$WTDIR/oc-$id.log
  permit=$(grep -m1 '^PERMITIDOS:' "$enc" | cut -d: -f2-)
  verify=$(grep -m1 '^VERIFICA:' "$enc" | cut -d: -f2-)
  if [ -z "$permit" ] || [ -z "$verify" ]; then echo "El encargo necesita las líneas PERMITIDOS: y VERIFICA:"; exit 2; fi
  if [ -e "$wt" ]; then echo "Ya existe $wt: usa diff, apply o drop"; exit 2; fi
  mkdir -p "$WTDIR"
  git -C "$ROOT" worktree add -q --detach "$wt" HEAD || exit 2

  # Los cuelgues del proveedor gratuito son intermitentes (medido 19 sep 2026): si el primer intento
  # no produce informe ni cambios, se reintenta una vez con el modelo de reserva.
  local attempt m s rc
  for attempt in 1 2; do
    m=$model; [ "$attempt" = 2 ] && m=$MODEL_FALLBACK
    s=$(date +%s)
    # OpenCode a veces no sale al terminar la sesión (medido 19 sep 2026: informe completo y proceso vivo
    # 10 min después). Se espera el ARTEFACTO —la línea NOTAS: del informe— y se cierra el grupo de procesos.
    ( cd "$wt" && exec setsid opencode run -m "$m" "$(cat "$enc")" ) > "$log" 2>&1 &
    local pid=$! end=$(( s + TIMEOUT )); rc=""
    while kill -0 "$pid" 2>/dev/null; do
      if strip < "$log" | grep -q '^NOTAS:'; then sleep 3; kill -TERM -- -"$pid" 2>/dev/null; rc=0; break; fi
      if [ "$(date +%s)" -ge "$end" ]; then kill -TERM -- -"$pid" 2>/dev/null; rc=124; break; fi
      sleep 2
    done
    wait "$pid" 2>/dev/null; rc=${rc:-$?}
    echo "EJECUTOR: $m · rc=$rc · $(( $(date +%s) - s )) s · intento $attempt"
    strip < "$log" | grep -q '^ESTADO:' && break
    if [ "$attempt" = 1 ] && [ -z "$(changed "$wt")" ]; then continue; fi
    break
  done

  echo "--- informe del ejecutor"
  strip < "$log" | grep -A3 '^ESTADO:' | tail -4
  [ -n "$(strip < "$log" | grep '^ESTADO:')" ] || echo "(sin informe; log: $log)"

  echo "--- ficheros"
  local f p ok alarm=0
  set -f
  while read -r f; do
    [ -z "$f" ] && continue
    ok=0; for p in $permit; do [[ $f == $p ]] && ok=1; done
    if [ "$ok" = 1 ]; then echo "  $f"; else echo "  FUERA DE ALCANCE: $f"; alarm=1; fi
  done < <(changed "$wt")
  set +f

  git -C "$wt" add -A -N . 2>/dev/null
  local banned review
  banned=$(git -C "$wt" diff -U0 -- 'Sim/*.cs' 'Sim/**/*.cs' | grep -E '^\+[^+]' \
    | grep -E 'System\.Random|Random\.Shared|Guid\.NewGuid|DateTime\.(Now|UtcNow)|Environment\.TickCount|HashCode\.|\bdynamic\b|\.GetMethod\(|Activator\.' )
  review=$(git -C "$wt" diff -U0 -- 'Sim/*.cs' 'Sim/**/*.cs' | grep -E '^\+[^+]' \
    | grep -cE '\b(float|double)\b|foreach *\(.*(Dictionary|HashSet|\.Keys|\.Values)')
  if [ -n "$banned" ]; then echo "--- APIS PROHIBIDAS en /Sim:"; echo "$banned" | head -5; alarm=1; fi
  [ "${review:-0}" -gt 0 ] && echo "--- revisar a mano: $review líneas nuevas en /Sim con float/double o foreach sobre Dictionary/HashSet"

  echo "--- verificación independiente:$verify"
  (cd "$wt" && timeout 900 bash -c "$verify") > "$log.verify" 2>&1; rc=$?
  strip < "$log.verify" | grep -E 'Passed!|Failed!|error [A-Z]+[0-9]+|Build FAILED|válid|valid|Total' | sort -u | head -4
  echo "  rc=$rc"
  if changed "$wt" | grep -q '^data/'; then
    (cd "$wt" && timeout 120 dotnet run --project tools/DataValidator -- data/) > "$log.data" 2>&1 \
      && echo "--- DataValidator: OK" || { echo "--- DataValidator: FALLA (ver $log.data)"; alarm=1; }
  fi
  [ "$rc" != 0 ] && alarm=1
  if [ "$alarm" = 0 ]; then echo "VEREDICTO: listo para revisar el diff"; else echo "VEREDICTO: NO integrar sin revisar a fondo"; fi
}

cmd_diff() { local wt=$WTDIR/oc-$1; shift; git -C "$wt" add -A -N . 2>/dev/null; git -C "$wt" diff -- "$@"; }

cmd_apply() {
  local id=$1 wt=$WTDIR/oc-$1 patch=$WTDIR/oc-$1.patch
  git -C "$wt" add -A && git -C "$wt" diff --cached --binary HEAD > "$patch"
  if git -C "$ROOT" apply "$patch"; then echo "Aplicado (sin preparar): $(git -C "$wt" diff --cached --name-only HEAD | tr '\n' ' ')"; cmd_drop "$id"
  else echo "No aplica limpio sobre el árbol principal; parche en $patch"; exit 1; fi
}

cmd_drop() { git -C "$ROOT" worktree remove --force "$WTDIR/oc-$1" && rm -f "$WTDIR/oc-$1".log* "$WTDIR/oc-$1".patch; echo "Worktree oc-$1 eliminado"; }

case "${1:-}" in
  run) shift; cmd_run "$@" ;;
  diff) shift; cmd_diff "$@" ;;
  apply) shift; cmd_apply "$@" ;;
  drop) shift; cmd_drop "$@" ;;
  *) sed -n '2,11p' "$0"; exit 2 ;;
esac
