---
name: opencode-worker
description: Delegar la ejecución de un encargo cerrado (el 80 % del esquema 10-80-10) en OpenCode con modelos gratuitos, en un worktree aislado, leyendo solo un informe de ~15 líneas. Usar por defecto para trabajo mecánico con especificación cerrada —datos de /data, tests, esquemas, documentación derivada, código con interfaz fijada— en lugar de fast-worker, para no gastar cuota de Claude. También para consultar qué dice la documentación sobre algo (tools/opencode-consulta.sh) con citas verificadas, en vez de leer requisitos.md o ADRs enteras o lanzar Explore.
---

# OpenCode como ejecutor

Claude planifica y revisa; OpenCode ejecuta. El ahorro no está en el trabajo delegado en sí sino en que el
bucle editar → compilar → fallar → corregir ocurre **fuera** del contexto de la sesión principal, que solo
lee el informe y, si hace falta, partes del diff.

OpenCode lee `AGENTS.md` (no `CLAUDE.md`: medido, `AGENTS.md` tiene precedencia). Sus permisos están en
`opencode.json`: git de escritura denegado, prohibido salir del directorio de trabajo.

## Cuándo sí y cuándo no

| Delegar en OpenCode | No delegar |
|---|---|
| JSON de `/data` con formato y valores ya decididos | Decisiones de diseño, ADRs, etiquetas CONFIRMED/LIKELY/REJECTED |
| Tests con los casos enumerados en el encargo | Revisión de la Regla E (`independent-reviewer`, sigue en Opus) |
| Código con interfaz y comportamiento fijados | Hipótesis de `gameplay-debug`, interpretación de `/Balance` |
| Documentación derivada, traducciones en `data/l10n/` | Todo `/Game` que requiera `visual-review` |
| Renombrados, cambios repetitivos | Encargos ambiguos: si no cabe en la plantilla, no está cerrado |

`fast-worker` queda como reserva: encargos que necesitan algo de criterio, o cuando OpenCode ha fallado dos
veces el mismo encargo. En `/Sim` se empieza por piezas pequeñas hasta tener datos del piloto
(`docs/analisis/piloto-opencode.md`).

## Plantilla de encargo

Se escribe en el scratchpad (`<scratchpad>/encargos/<id>.md`); el `<id>` es el nombre del fichero.

```markdown
# Encargo <id> — <RF/RT-xxx>
Objetivo: <qué debe existir al terminar, en una o dos frases>.
Contexto a leer: <máx. 3 ficheros o secciones; nada de "lee docs/">
Interfaz / formato exacto: <firmas, esquema JSON, ids>
Comportamiento: <casos concretos, incluidos bordes: vacío, empates, negativos>
Reglas aplicables: <las de AGENTS.md que importan aquí, p. ej. orden determinista, aritmética entera>
Tests: <cuáles y dónde>
PERMITIDOS: <rutas o globs separados por espacio, p. ej. data/perks/*.json Sim.Tests/FooTests.cs>
VERIFICA: <comando exacto, con timeout, p. ej. timeout 180 dotnet test Sim.Tests -c Release --filter "FullyQualifiedName~Foo" -m:1 -v q>
```

`PERMITIDOS:` y `VERIFICA:` son obligatorias: el script las lee.

## Flujo

1. Escribir el encargo. Si no se puede rellenar cada campo sin "según veas", falta planificar.
2. `timeout 1000 tools/opencode-encargo.sh run <encargo.md> [modelo]`. Crea el worktree
   `.claude/worktrees/oc-<id>`, ejecuta, reintenta una vez con el modelo de reserva si no hubo ni informe ni
   cambios, comprueba el alcance y las APIs prohibidas en `/Sim`, y **repite la verificación por su cuenta**
   (no se fía del informe del ejecutor). Corre `DataValidator` si cambió `data/`.
3. Leer **solo** la salida (~15 líneas). No abrir `oc-<id>.log` salvo que falte el informe.
4. Revisar el diff con criterio de coste:
   - `VEREDICTO: NO integrar…` → `tools/opencode-encargo.sh diff <id> <ruta>` de lo marcado, y corregir con
     un segundo encargo o a mano; si falla dos veces, `drop` y pasar a `fast-worker`.
   - `/Sim` o líneas marcadas "revisar a mano" → diff completo de esos ficheros.
   - `/data`, tests, docs → `git -C .claude/worktrees/oc-<id> diff --stat` y un muestreo.
5. `tools/opencode-encargo.sh apply <id>`: aplica al árbol principal **sin preparar** y borra el worktree.
6. Seguir el flujo normal del proyecto: tests, `balance-measure` si toca balance, `independent-reviewer`
   si toca `/Sim`, `/data` o una ADR, commit con rutas explícitas.
7. Anotar el resultado en `docs/analisis/piloto-opencode.md` mientras dure el piloto.

## Consultas de documentación

Para saber qué dice el repo sobre algo sin abrir `requisitos.md` ni varias ADR, en lugar de leerlos o de
lanzar `Explore`:

```bash
timeout 400 tools/opencode-consulta.sh "¿qué dice RF-012d sobre el daño no anunciado?" [docs/requisitos.md docs/decisiones/ ...]
```

Agente `lector` de `opencode.json` (sin edición, bash solo de lectura), en el árbol principal; el script
avisa si `git status` cambia. Devuelve la respuesta (≤ 5 frases) y cada cita `ruta:línea | fragmento`
comprobada contra el fichero: `✓` literal en esa línea, `~` a ±3 líneas, `✗` no aparece. Sale con 1 si
alguna cita falla o no hay citas. 16–32 s por consulta (medido, n=3).

Qué demuestra y qué no:

- `✓` demuestra que **el texto citado existe**, no que la respuesta lo interprete bien ni que no haya otro
  documento que lo contradiga. Si una decisión (ADR, etiqueta Regla F, cambio de regla) se apoya en la
  cita, se leen esas líneas con `sed -n` antes de escribirla — son pocas, que es el ahorro.
- Una `✗` invalida la respuesta entera, no solo esa cita: se repregunta acotando rutas o se lee a mano.
- "No consta" con citas verificadas es una respuesta válida (probado: no inventó una ADR inexistente).
- No sustituye a leer el requisito completo **al implementar** un sistema (Flujo de trabajo de `CLAUDE.md`);
  sirve para localizar, contrastar y responder preguntas puntuales.

## Modelos (medido 19 sep 2026, una tarea C# con tests ocultos — LIKELY, n=1)

| Modelo | Tiempo | Tests ocultos | Nota |
|---|---|---|---|
| `opencode/big-pickle` (por defecto) | 11–23 s | 7/7 | informe exacto |
| `opencode/muse-spark-1.3-contributor-free` (reserva) | 24–33 s | 7/7 | el más cuidadoso con el orden determinista |
| `opencode/mimo-v2.5-free` | 53 s | 7/7 | |
| `opencode/nemotron-3-ultra-free` | 223 s | 7/7 | lento |
| `opencode/nemotron-3.5-lightning-free` | 66 s | — | no hizo nada: no usar |

## Límites conocidos

- **Máximo 2 procesos de OpenCode en paralelo** (encargos y consultas sumados). Cinco a la vez se colgaron todos sin salida; dos a la vez funcionan.
  Causa no aislada (LIKELY: límite de concurrencia del plan gratuito).
- **OpenCode a veces no sale al terminar** (CONFIRMED: informe completo y proceso vivo 10 min después).
  El script espera la línea `NOTAS:` y cierra el grupo de procesos; nunca lanzar `opencode run` a pelo sin
  ese mecanismo o sin `timeout`.
- Los hooks de Claude (`data-validation`, `sim-game-boundary`, `build-check-before-push`) **no** actúan sobre
  OpenCode: por eso el script repite la verificación y valida `/data`, y el commit lo hace siempre Claude.
- El worktree parte de `HEAD`: lo no commiteado del árbol principal no está en él.
