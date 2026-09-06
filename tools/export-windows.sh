#!/usr/bin/env bash
# Exporta una build jugable de /Game para Windows desde WSL y la deja en el escritorio de Windows,
# con /data al lado del ejecutable (GameData.FindDataDirectory sube desde el .exe). Receta en
# docs/entorno.md, "Build jugable en Windows".
#
#   tools/export-windows.sh [destino]     (por defecto /mnt/c/Users/urban/Desktop/Underleague)
set -euo pipefail
cd "$(dirname "$0")/.."
DEST="${1:-/mnt/c/Users/urban/Desktop/Underleague}"
GODOT="${GODOT:-$HOME/.local/bin/godot}"

dotnet build Game/Underleague.Game.csproj -v q -nologo
rm -rf out/win && mkdir -p out/win
"$GODOT" --headless --path Game --export-debug "Windows Desktop" ../out/win/Underleague.exe 2>&1 \
  | sed 's/\x1b\[[0-9;]*m//g' | grep -i 'error\|exited with' | grep -v backtrace || true
test -d out/win/data_Underleague.Game_windows_x86_64 || { echo "export .NET fallido: falta data_Underleague.Game_windows_x86_64" >&2; exit 1; }

mkdir -p "$DEST" && rm -rf "$DEST"/data_Underleague.Game_windows_x86_64 "$DEST"/data
cp -r out/win/* "$DEST"/
mkdir -p "$DEST/data" && cp -r data/* "$DEST/data/"
echo "build en $DEST ($(du -sh "$DEST" | cut -f1)): ejecuta Underleague.exe (o Underleague.console.exe para ver errores)"
