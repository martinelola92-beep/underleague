#!/usr/bin/env bash
# Recuerda validar /data (RT-032/RT-083) tras editar cualquier fichero bajo data/**.
set -euo pipefail
input="$(cat)"
path="$(echo "$input" | python3 -c 'import json,sys; d=json.load(sys.stdin).get("tool_input",{}); print(d.get("file_path") or d.get("path") or "")' 2>/dev/null || true)"

case "$path" in
  */data/*|data/*) ;;
  *) exit 0 ;;
esac

echo "RECORDATORIO (data-validation): tocaste $path. Antes de cerrar: 'dotnet run --project tools/DataValidator -- data/' (RT-032/RT-083)." >&2
exit 0
