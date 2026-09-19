#!/usr/bin/env bash
# Ejecuta `dotnet test` y devuelve un resumen corto en vez de la salida entera (skill build-and-test):
# una línea de totales y una por test fallido con la primera línea de su mensaje. Si no compila, los
# errores distintos del compilador. El log y el .trx completos quedan en la ruta que imprime.
#
#   timeout 960 tools/test-resumen.sh Sim.Tests -c Release --filter "Category=Gate" -m:1 -v q
#
# Los argumentos van tal cual a `dotnet test`. El `timeout` lo pone quien llama (presupuesto por tarea).
# Sale con el código de `dotnet test`. Variable: TR_DIR (directorio de resultados; por defecto uno temporal).
set -uo pipefail

dir=${TR_DIR:-$(mktemp -d "${TMPDIR:-/tmp}/test-resumen.XXXXXX")}
mkdir -p "$dir"
log=$dir/dotnet-test.log
mark=$dir/.inicio; : > "$mark"
s=$(date +%s)
dotnet test "$@" --logger "trx;LogFileName=resultado.trx" --results-directory "$dir" > "$log" 2>&1
rc=$?
t=$(( $(date +%s) - s ))

trx=$(find "$dir" -name '*.trx' -newer "$mark" 2>/dev/null | head -1)
if [ -z "$trx" ]; then
  echo "SIN RESULTADOS · rc=$rc · ${t} s · log: $log"
  grep -oE '[^ ]+\.cs\([0-9]+,[0-9]+\): error [A-Z]+[0-9]+: [^[(]*' "$log" | sed "s|^$PWD/||" | sort -u | head -10
  grep -qE 'error [A-Z]+[0-9]+' "$log" || tail -5 "$log"
  exit $rc
fi

python3 - "$trx" "$rc" "$t" "$log" <<'EOF'
import sys, xml.etree.ElementTree as ET
trx, rc, t, log = sys.argv[1:]
ns = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
root = ET.parse(trx).getroot()
res = root.findall(".//t:UnitTestResult", ns)
by = {}
for r in res:
    by.setdefault(r.get("outcome"), []).append(r)
tot = " · ".join(f"{k} {len(v)}" for k, v in sorted(by.items()))
print(f"TESTS: {len(res)} · {tot} · rc={rc} · {int(t)//60} m {int(t)%60:02d} s · trx: {trx}")
for r in sorted(by.get("Failed", []), key=lambda r: r.get("testName")):
    msg = r.findtext(".//t:ErrorInfo/t:Message", default="", namespaces=ns)
    first = " / ".join(l.strip() for l in msg.splitlines() if l.strip())[:240]
    print(f"  FALLA {r.get('testName')} — {first}")
if not res:
    print(f"  (el .trx no tiene resultados; log: {log})")
EOF
exit $rc
