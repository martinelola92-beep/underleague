#!/usr/bin/env bash
# Bloquea 'git push' si algún commit del rango a publicar no compila por separado.
# Lección con nombre propio: el 16 sep 2026 el commit 9f4d25b (BB-J) se llevó
# Sim/Engine/MatchEngine.cs sin Sim/Engine/Utility.cs -> origin/main quedó publicado y roto
# (CS1061) hasta el commit de arreglo 96234de. Lo detectó el independent-reviewer compilando
# origin/main en un worktree limpio, no un hook. Este hook hace ese mismo chequeo antes de que
# el push llegue al remoto: "cada commit debe pasar compilación por separado antes de publicarse".
set -euo pipefail
input="$(cat)"
command="$(echo "$input" | python3 -c 'import json,sys; print(json.load(sys.stdin).get("tool_input", {}).get("command", ""))' 2>/dev/null || true)"

case "$command" in
  *"git push"*) ;;
  *) exit 0 ;;
esac

repo_root="$(git rev-parse --show-toplevel 2>/dev/null || echo .)"
cd "$repo_root" || exit 0

# Rango de commits que este push va a publicar: los que están en HEAD y no en el remoto
# ya conocido (@{u}). Si no hay upstream configurado no podemos acotar el rango con
# fiabilidad sin tocar la red, así que avisamos y comprobamos solo HEAD (mejor que nada).
range=""
if upstream="$(git rev-parse --abbrev-ref --symbolic-full-name '@{u}' 2>/dev/null)"; then
  range="${upstream}..HEAD"
else
  echo "AVISO (build-check-before-push): sin upstream configurado, no se puede acotar el rango exacto." >&2
  echo "Comprobando solo HEAD." >&2
  range="HEAD~1..HEAD"
fi

commits="$(git rev-list --reverse "$range" 2>/dev/null || true)"
if [ -z "$commits" ]; then
  # Rango vacío (nada nuevo que publicar, o HEAD~1 no existe en un repo de un solo commit).
  exit 0
fi

# Rutas cuyo cambio puede afectar a la compilación (RT-010): Sim, Sim.Tests, Balance, tools,
# y los ficheros de proyecto/solución. Un commit que no las toque no puede haber roto el build
# respecto a su padre, así que se salta (ahorra minutos en commits solo de /data o /docs).
buildable_pattern='^(Sim/|Sim\.Tests/|Balance/|tools/|.*\.csproj$|.*\.slnx?$|global\.json$)'

tmp_base="$(mktemp -d)"
cleanup() {
  set +e
  git worktree list --porcelain 2>/dev/null | awk '/^worktree /{print $2}' | grep -F "$tmp_base" | while IFS= read -r wt; do
    git worktree remove --force "$wt" 2>/dev/null
  done
  rm -rf "$tmp_base"
}
trap cleanup EXIT

failed_commit=""
failed_log=""
checked=0

while IFS= read -r sha; do
  [ -z "$sha" ] && continue
  touched="$(git show --name-only --pretty=format: "$sha" 2>/dev/null || true)"
  if ! echo "$touched" | grep -qE "$buildable_pattern"; then
    continue
  fi
  checked=$((checked + 1))
  wt="$tmp_base/wt-$sha"
  if ! git worktree add --detach --quiet "$wt" "$sha" >/dev/null 2>&1; then
    continue
  fi
  set +e
  build_out="$(cd "$wt" && timeout 300 dotnet build Underleague.slnx -c Release -m:1 -v q 2>&1)"
  build_rc=$?
  set -e
  git worktree remove --force "$wt" >/dev/null 2>&1 || true
  if [ "$build_rc" -ne 0 ]; then
    failed_commit="$sha"
    failed_log="$(echo "$build_out" | tail -20)"
    break
  fi
done <<< "$commits"

if [ -n "$failed_commit" ]; then
  short="$(git rev-parse --short "$failed_commit" 2>/dev/null || echo "$failed_commit")"
  reason="El commit $short no compila por separado (dotnet build Underleague.slnx -c Release). Push bloqueado: cada commit debe compilar antes de publicarse. Últimas líneas del error:
${failed_log}"
  python3 -c '
import json, sys
reason = sys.argv[1]
print(json.dumps({
    "hookSpecificOutput": {
        "hookEventName": "PreToolUse",
        "permissionDecision": "deny",
        "permissionDecisionReason": reason,
    },
    "systemMessage": "build-check-before-push: bloqueado — un commit del rango no compila. Ver detalle.",
}))
' "$reason"
  exit 0
fi

exit 0
