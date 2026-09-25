#!/usr/bin/env bash
# ¿Existe ya? — la comprobación de la Regla G de CLAUDE.md, antes de decir "esto no está implementado".
#
# Un `grep` por el identificador (EventType.X, la clase, el campo) NO toca la capa que suele tenerlo ya
# hecho: `Sim/Run/View/` traduce eventos a cosas presentables y no nombra los tipos de evento. Este script
# busca cada término en las cuatro zonas donde vive una capacidad, por separado, para que el hueco se vea.
#
#   tools/existe-ya.sh aviso cartel perk          # busca los tres términos
#   tools/existe-ya.sh PerkTriggered flash moment
#
# Da SIEMPRE los términos en los dos idiomas y por el CONCEPTO, no sólo por el identificador: el código es
# inglés y los comentarios y la documentación, español (ADR 0009).
set -uo pipefail

if [ $# -eq 0 ]; then
  echo "uso: tools/existe-ya.sh <término> [término...]" >&2
  exit 2
fi

raiz=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
cd "$raiz" || exit 1

zona() {
  local titulo=$1 ruta=$2 patron=$3
  local salida
  salida=$(grep -rn --binary-files=without-match -i "$patron" $ruta 2>/dev/null \
    | grep -v '/obj/\|/bin/\|\.uid:\|\.trx:' | head -8)
  if [ -n "$salida" ]; then
    printf '  %s\n%s\n' "$titulo" "$(printf '%s\n' "$salida" | sed 's/^/    /')"
  else
    printf '  %s: (nada)\n' "$titulo"
  fi
}

for termino in "$@"; do
  printf '\n=== %s ===\n' "$termino"
  # La primera a propósito: es la que un grep por identificador nunca toca, y la que más veces ya lo tiene.
  zona 'CAPA DE VISTA — Sim/Run/View (traduce eventos a cosas presentables)' 'Sim/Run/View' "$termino"
  zona 'Consumidores de la traza y pantallas — /Game' 'Game --include=*.cs' "$termino"
  zona 'Motor y datos — /Sim, /data' 'Sim data --include=*.cs --include=*.json' "$termino"
  zona 'Decisiones y análisis — /docs' 'docs --include=*.md' "$termino"
done

printf '\nRegla G: mientras alguna de estas zonas no se haya mirado, "no existe" es LIKELY, nunca CONFIRMED.\n'
