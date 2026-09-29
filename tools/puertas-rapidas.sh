#!/usr/bin/env bash
# Puertas estadísticas en modo rápido: la cuarta parte de la muestra de cada una (Sim.Tests/GateScale.cs),
# ~3 min frente a ~13. Para cazar roturas grandes mientras se trabaja; NO sustituyen a las completas antes de
# publicar un hito: los umbrales no cambian y con menos muestra hay más ruido.
#
# Por eso compara contra SÍ MISMO: tools/puertas-rapidas.base lista las puertas que ya están rojas en modo
# rápido en el commit de referencia, y el script sólo avisa de las NUEVAS (y de las que se han curado).
# Tras publicar un hito, se regenera con:  tools/puertas-rapidas.sh --rebase
#
# Uso: tools/puertas-rapidas.sh [--rebase] [porcentaje]
set -uo pipefail
cd "$(dirname "$0")/.."
rebase=0
if [ "${1:-}" = "--rebase" ]; then rebase=1; shift; fi
export UNDERLEAGUE_GATE_SCALE="${1:-25}"
out=$(mktemp)
echo "puertas al ${UNDERLEAGUE_GATE_SCALE} % de su muestra"
timeout 900 tools/test-resumen.sh Sim.Tests -c Release --filter "Category=Gate" -m:1 -v q | tee "$out" | head -1
grep -oE "FALLA [^ ]+" "$out" | sed 's/FALLA //' | sort > "$out.rojas"
if [ "$rebase" = 1 ]; then
  cp "$out.rojas" tools/puertas-rapidas.base
  echo "base regenerada: $(wc -l < tools/puertas-rapidas.base) rojas en modo rápido"
  exit 0
fi
nuevas=$(comm -13 tools/puertas-rapidas.base "$out.rojas")
curadas=$(comm -23 tools/puertas-rapidas.base "$out.rojas")
[ -n "$curadas" ] && { echo "CURADAS respecto a la base:"; echo "$curadas" | sed 's/^/  /'; }
if [ -n "$nuevas" ]; then
  echo "NUEVAS ROJAS respecto a la base (confirmar con las completas):"
  for t in $nuevas; do grep "FALLA $t" "$out" | sed 's/^ *FALLA /  /'; done
  exit 1
fi
echo "sin rojas nuevas respecto a la base ($(wc -l < tools/puertas-rapidas.base) ya conocidas en modo rápido)"
