#!/usr/bin/env bash
# Pasa la batería de detectores de síntomas (docs/analisis/barrido-detectores-*.md) sobre N partidos por traza.
# Uso: tools/barrido-detectores.sh [partidos=100] [semilla0=1]
# Solo lee la traza de /Sim (RT-098), sin Godot. Salida: Game/screenshots/detectores/{resumen.md,casos.txt,peores.tsv}.
# Primero valida los detectores contra casos de respuesta conocida (Regla J) y sólo entonces barre.
set -euo pipefail
cd "$(dirname "$0")/.."
export UL_DET_THREADS="${UL_DET_THREADS:-1}"
export UL_DET_MATCHES="${1:-100}"
export UL_DET_SEED0="${2:-1}"
export UL_DET_OUT="${UL_DET_OUT:-$PWD/Game/screenshots/detectores}"
if [ -z "${FORZAR:-}" ] && pgrep -f "project Balance|testhost|godot" >/dev/null; then
  echo "hay un proceso pesado en marcha (Balance/testhost/godot); espera a que acabe (o FORZAR=1: el barrido es ligero, 1 hilo)" >&2
  exit 2
fi
timeout 600 dotnet test Sim.Tests -c Release --filter "FullyQualifiedName~SymptomDetectorsValidationTests" -m:1 -v q
timeout 1500 dotnet test Sim.Tests -c Release --no-build --filter "FullyQualifiedName~SymptomSweepTests" -m:1 -v q
echo "--> $UL_DET_OUT/resumen.md"
