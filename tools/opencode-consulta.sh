#!/usr/bin/env bash
# Pregunta a OpenCode qué dice la documentación del repo sobre algo y comprueba cada cita contra el fichero
# real, para que la sesión de Claude lea la respuesta y las líneas citadas en vez de los documentos enteros.
# Skill: opencode-worker (sección "Consultas de documentación").
#
#   timeout 400 tools/opencode-consulta.sh "¿qué dice RF-012d sobre el daño no anunciado?" [ruta...] [-m modelo]
#
# Las rutas opcionales acotan dónde buscar (p. ej. docs/requisitos.md docs/decisiones/). Agente `lector` de
# opencode.json: sin edición y con bash limitado a lectura. Variables: OC_TIMEOUT (s, por defecto 300).
# Sale con 0 si hay respuesta y todas las citas se verifican, 1 si alguna cita no aparece, 2 si no hay respuesta.
set -uo pipefail

ROOT=$(git rev-parse --show-toplevel)
MODEL=opencode/big-pickle
TIMEOUT=${OC_TIMEOUT:-300}
q=${1:?uso: opencode-consulta.sh \"pregunta\" [ruta...] [-m modelo]}; shift
paths=()
while [ $# -gt 0 ]; do
  case $1 in -m) MODEL=$2; shift 2 ;; *) paths+=("$1"); shift ;; esac
done
where="todo el repositorio (empieza por docs/ y docs/requisitos.md)"
[ ${#paths[@]} -gt 0 ] && where="${paths[*]}"

dir=$ROOT/.claude/consultas; mkdir -p "$dir"
log=$dir/$(date +%Y%m%d-%H%M%S)-$$.log
before=$(git -C "$ROOT" status --porcelain --untracked-files=all)

prompt=$(cat <<EOF
Esta vez NO eres el ejecutor de un encargo: eres un LECTOR. Ignora el "Informe final" de AGENTS.md y no
modifiques nada. Responde a la pregunta usando solo lo que está escrito en el repositorio.

Pregunta: $q
Dónde buscar: $where

Busca con grep y lee con sed -n 'A,Bp' las líneas relevantes. No respondas de memoria. Si el repositorio no
lo dice, dilo: "No consta" es una respuesta válida. Si hay contradicciones entre documentos, cítalas todas.

Termina con EXACTAMENTE este formato y nada después:
RESPUESTA: <máx. 5 frases>
CITA: <ruta>:<línea> | <fragmento copiado LITERALMENTE de esa línea, 5-20 palabras seguidas, sin "...">
CITA: ... (entre 1 y 6 citas; una por línea; una línea de fichero por cita)
FIN
EOF
)

s=$(date +%s)
( cd "$ROOT" && exec setsid opencode run --agent lector -m "$MODEL" "$prompt" ) > "$log" 2>&1 &
pid=$!; end=$(( s + TIMEOUT )); rc=""
# Como en opencode-encargo.sh: se espera el ARTEFACTO (la línea FIN), no a que el proceso salga.
while kill -0 "$pid" 2>/dev/null; do
  if sed 's/\x1b\[[0-9;]*m//g' "$log" | grep -q '^FIN *$'; then sleep 2; kill -TERM -- -"$pid" 2>/dev/null; rc=0; break; fi
  if [ "$(date +%s)" -ge "$end" ]; then kill -TERM -- -"$pid" 2>/dev/null; rc=124; break; fi
  sleep 2
done
wait "$pid" 2>/dev/null; rc=${rc:-$?}
echo "LECTOR: $MODEL · rc=$rc · $(( $(date +%s) - s )) s · log: ${log#$ROOT/}"

after=$(git -C "$ROOT" status --porcelain --untracked-files=all)
[ "$before" != "$after" ] && echo "ALARMA: el árbol de trabajo ha cambiado durante la consulta (git status)"

python3 - "$log" "$ROOT" <<'PY'
import re, sys, unicodedata
log, root = sys.argv[1:]
text = re.sub(r"\x1b\[[0-9;]*m", "", open(log, encoding="utf-8", errors="replace").read())
# Nos quedamos con el último bloque RESPUESTA..FIN (el modelo puede ensayar el formato antes).
start = text.rfind("\nRESPUESTA:")
if start < 0 and text.startswith("RESPUESTA:"):
    start = 0
if start < 0:
    print("SIN RESPUESTA: el lector no produjo el bloque RESPUESTA/FIN")
    sys.exit(2)
block = text[start:].split("\nFIN", 1)[0].strip().splitlines()

def norm(s):
    s = unicodedata.normalize("NFC", s).replace("**", "").replace("`", "")
    s = s.replace("—", "-").replace("–", "-")
    return re.sub(r"\s+", " ", s).strip().strip("\"'«»“”").strip().lower()

answer, cites = [], []
for l in block:
    if l.startswith("CITA:"):
        cites.append(l[5:].strip())
    elif not cites:
        answer.append(l.strip())
print(" ".join(a for a in answer if a))
bad = 0
for c in cites:
    m = re.match(r"(\S+?):(\d+)(?:-\d+)?\s*\|\s*(.+)$", c)
    if not m:
        print(f"  ✗ {c}  (formato de cita inválido)"); bad += 1; continue
    path, n, frag = m.group(1), int(m.group(2)), m.group(3)
    try:
        lines = open(f"{root}/{path}", encoding="utf-8").read().splitlines()
    except OSError:
        print(f"  ✗ {path}:{n}  (el fichero no existe)"); bad += 1; continue
    if n < 1 or n > len(lines):
        print(f"  ✗ {path}:{n}  (el fichero tiene {len(lines)} líneas)"); bad += 1; continue
    f = norm(frag)
    # Un fragmento con elisión ("a ... b") vale si cada trozo aparece, en orden, en la misma línea.
    parts = [x for x in re.split(r"\s*(?:\.\.\.|\u2026)\s*", f) if x]
    def found(k):
        t, i = norm(lines[k - 1]), 0
        for x in parts:
            i = t.find(x, i)
            if i < 0:
                return False
            i += len(x)
        return bool(parts)
    if found(n):
        print(f"  ✓ {path}:{n} | {frag.strip()}")
        continue
    near = [k for k in range(max(1, n - 3), min(len(lines), n + 3) + 1) if found(k)]
    if near:
        print(f"  ~ {path}:{near[0]} (citó {n}) | {frag.strip()}")
    else:
        print(f"  ✗ {path}:{n}  (el fragmento no aparece en esa línea ni a ±3) | {frag.strip()}"); bad += 1
if not cites:
    print("  (sin citas: la respuesta no es verificable)")
print("VEREDICTO: " + ("citas verificadas" if cites and not bad else f"{bad} cita(s) no verificadas: no fiarse de la respuesta" if bad else "sin citas"))
sys.exit(1 if bad or not cites else 0)
PY
