#!/usr/bin/env bash
# Turno para procesos PESADOS (lotes de /Balance, suite del bucle, puertas, censos).
#
# La máquina tiene 4 núcleos y cada uno de esos procesos ya los usa todos (Parallel.For). Dos a la vez no
# suman: se estorban (29 sep 2026: carga 78, agentes de más de 2 h). Este envoltorio hace cola con `flock`
# sobre un único fichero de bloqueo compartido por el repo principal y todos sus worktrees, así que los
# agentes pueden trabajar en paralelo y sus mediciones salen de una en una, sin coordinación manual.
#
# Uso:   tools/pesado.sh <segundos-de-timeout> <comando> [args...]
# Ej.:   tools/pesado.sh 1500 tools/test-resumen.sh Sim.Tests -c Release --filter "Category!=Gate&Category!=Diagnostic" -m:1 -v q
#        tools/pesado.sh 3600 dotnet run --project Balance -c Release -- --full-runs 1200 --seed 1 --out out/x/
#
# El timeout cuenta desde que se obtiene el turno, no desde que se pide. La espera por el turno no tiene
# límite: si otro proceso pesado se cuelga, lo corta su propio timeout. Compilar y los tests con filtro NO
# pasan por aquí: son ligeros.
set -euo pipefail
if [ $# -lt 2 ]; then
  echo "uso: tools/pesado.sh <segundos> <comando> [args...]" >&2
  exit 2
fi
secs="$1"; shift
lock=/home/martinelola92/underleague/out/.pesado.lock
mkdir -p "$(dirname "$lock")"
echo "[pesado] esperando turno ($(date +%H:%M:%S)): $*" >&2
# -o: el comando no hereda el descriptor del candado. Sin él, el servidor de compilación de Roslyn
# (VBCSCompiler), que sobrevive al build, se quedaba el candado para siempre (3 oct 2026: 9 min bloqueado).
exec flock -o "$lock" bash -c 'echo "[pesado] turno obtenido ($(date +%H:%M:%S))" >&2; exec timeout "$0" "$@"' "$secs" "$@"
