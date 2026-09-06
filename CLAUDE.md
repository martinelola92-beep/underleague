# Underleague

Roguelite de gestión y autobatalla: el jugador dirige un equipo de fútbol 7 de criaturas fantásticas. Los partidos se resuelven solos en 60-90 s sobre una cuadrícula; todas las decisiones ocurren entre partidos. La identidad no es el fútbol, es la **carnicería administrada**: lesiones, muertes, prótesis y vínculos. El desgaste de la plantilla es el recurso central de la run.

PC (Steam), premium, sin online. **Estado (5 sep 2026): fases 0 y 1 cerradas; fase 2 implementada y medida —bucle de run completo, tres jefes con modificadores, economía y `--full-runs` con tres políticas automáticas—, con la curva de puertas de la ADR 0033 en verde y cuatro decisiones abiertas antes de darla por cerrada (`docs/balance/fase2-resultados.md` §7). Pantalla de Equipo funcionando en Godot.**

La fuente de verdad del diseño es `docs/requisitos.md` (v0.9.1). Cada requisito tiene identificador (`RF-xxx` funcional, `RT-xxx` técnico, `RA-xxx` arte, `UI-xxx` interfaz): cítalos en commits, ADRs, tests y comentarios cuando implementes o discutas uno.

## Stack

| | |
|---|---|
| Motor | Godot 4.6.3 mono, **solo en `/Game`**; `net10.0` verificado (ADR 0008) |
| Lenguaje | C# sobre .NET 10 (LTS), `net10.0` en todo |
| Tests | xUnit, sin librerías fluidas |
| Condiciones de perks | NCalc con funciones propias, compiladas al cargar |
| Validación de `/data` | JsonSchema.Net |
| Steam | Steamworks.NET o Facepunch.Steamworks (fase 4) |
| RNG, bus de eventos, IA de utilidad, generación de mapa | Propios, sin librería (§9 de requisitos) |

Descartados con motivo registrado en `docs/decisiones/`: ECS, aprendizaje automático, punto fijo preventivo, motor de físicas de Godot para el partido, addons de IA de Godot dentro de `/Sim`.

## Entorno de trabajo

Claude Code y **Godot** corren en **WSL (Ubuntu 24.04)**. Detalle en `docs/entorno.md`.

- `/Sim`, `/Sim.Tests`, `/Balance` y `/tools` son .NET puro: se compilan y prueban **en WSL** con `dotnet`. Ahí ocurre casi todo el trabajo de las fases 0-2.
- `/Game` se compila y ejecuta con el Godot de Linux instalado en WSL (`~/.local/bin/godot`). **El editor de Windows no puede abrir el proyecto**: Godot no admite rutas UNC. No hay editor gráfico (WSLg deshabilitado en `.wslconfig`), así que las escenas se editan como texto y el resultado visual se verifica con capturas por Xvfb; la receta está en `docs/entorno.md`.
- Instalado en WSL: .NET SDK 10.0.111 (y 8.0.130), `global.json` fija el 10; `csharp-ls` 0.27. En Windows (4 sep 2026): .NET SDK 10.0.400 y Godot 4.6.3 .NET, ambos vía winget.

## Estructura de la solución (RT-010)

```
/Sim            Librería .NET pura. CERO referencias a Godot
/Sim.Tests      Pruebas unitarias, estadísticas y de determinismo
/Balance        Consola: N partidos sin Godot -> CSV
/Game           Proyecto Godot. Referencia a /Sim
/data           JSON: perks, objetos, razas, clubes, consumibles, pesos de IA, generadores de nombres
/tools          Validadores de /data y scripts auxiliares
/docs           Requisitos, arquitectura, decisiones, plan
```

## Reglas sin excepción

Proceden de los requisitos técnicos y no se negocian en un PR. Si parece necesario romper una, se abre un ADR antes de escribir código.

1. **`/Sim` no conoce Godot** ni ninguna API de presentación. La dependencia es `/Game -> /Sim`, nunca al revés (RT-011).
2. **`/Sim` no hace E/S**: no lee ficheros, no consulta el reloj (RT-012). Recibe estado inicial + semilla y devuelve secuencia ordenada de eventos + estado final (RT-013). El render consume eventos; nunca calcula ni decide nada del partido (RT-014).
3. **Determinismo**: ticks lógicos fijos a 15/s, interpolación solo en render (RT-020); toda aleatoriedad sale de instancias explícitas de RNG con semilla; prohibidos `System.Random` compartido, `Random.Shared`, `Guid.NewGuid`, `DateTime.Now`, `Environment.TickCount`, `HashCode` sin semilla y cualquier generador estático (RT-021); flujos de RNG **separados** para partido, mapa y recompensas (RT-022); atributos, probabilidades y contadores en **aritmética entera**, `float` solo para posiciones (RT-023).
4. **Orden determinista** siempre: perks simultáneos por rareza descendente, id de jugador ascendente, id de perk ascendente (RT-041); empates de utilidad por id de jugador ascendente (RT-097). Nunca se itera un `Dictionary`/`HashSet` sin ordenar para algo que afecte al resultado.
5. **Perks, objetos, razas, clubes y consumibles son datos** en `/data`, no código (RF-065, RT-031). Un perk consulta etiquetas, nunca jugadores concretos (RF-068). Las condiciones son expresiones NCalc compiladas una vez al cargar; sin reflexión ni código arbitrario en tiempo de partido (RT-034).
6. **Las descripciones se generan desde el efecto** con plantillas por tipo de efecto; no existe texto de efecto escrito a mano (RT-035).
7. Todo fichero de `/data` se valida contra esquema al arrancar y en cada commit; un dato inválido es un error explícito, nunca silencioso (RT-032, RT-083).
8. Sin ML en la IA (RT-091): utilidad ponderada con pesos por posición y rasgo en `/data` (RT-096), tabla de puntuaciones volcable por tick (RT-098).
9. Las pruebas estadísticas de 1.000 partidos (RT-081) valen más que las unitarias. No se escriben tests de interfaz (RT-084). El test de determinismo (RT-024) es obligatorio y corre en CI en Windows y Linux.
10. **No se produce arte hasta cerrar el diseño de la fase 2.** Solo placeholders; el arte previo se descarta (regla de fase, §7).
11. Principio rector de diseño: **todo lo malo que pase en un partido debe haber sido previsible** con la información previa (RF-012d). Ningún sistema nuevo introduce daño no anunciado. Desde la **ADR 0048** un jugador **sano también puede morir**, así que la previsibilidad ya no se apoya en una garantía sino en las **cinco condiciones** de esa ADR —se sabe antes, se puede evitar el partido, se puede **reducir el riesgo con la alineación**, el equipo del muerto vuelve al inventario, y la muerte es rara—: son requisito, no aspiración, y cualquier cambio que las debilite hay que medirlo.

## Modo de trabajo: desarrollo autónomo

El usuario actúa **únicamente como revisor**. Claude planifica, implementa, prueba, documenta, commitea y hace push por su cuenta, y solo consulta cuando una decisión cambia una regla de juego de `docs/requisitos.md`, tiene coste económico, o es irreversible fuera del repositorio.

- **Esquema 10-80-10**: la sesión principal (el modelo más capaz) hace el primer 10%, **planificar**: arquitectura, interfaces, criterios de éxito y restricciones, por escrito antes de que nadie codifique. El 80% de **ejecución** se delega a subagentes con modelos más baratos. El último 10% es **revisión** por la sesión principal contra el plan: huecos, desviaciones, qué falta antes de commitear.
- **Subagentes del proyecto** (`.claude/agents/`): `fast-worker` (sonnet) para trabajo mecánico con especificación cerrada: clases a partir de interfaces, tests, datos JSON, esquemas, documentación derivada. `deep-reasoner` (opus) para razonamiento pesado: diseño de algoritmos, depuración compleja, análisis de balance, divergencias de determinismo. `Explore` para búsquedas de solo lectura. Usa `fork` solo cuando el subagente necesite todo el contexto de la sesión.
- Cada encargo a un subagente es cerrado: qué ficheros puede tocar, qué interfaces debe respetar, qué tests deben pasar, y que no haga commit. Lanza en paralelo los encargos independientes. Siempre una revisión independiente antes de cerrar un hito.
- **Skills y plugins**: cuando un flujo se repita o requiera conocimiento específico, crea una skill en `.claude/skills/` (plugin `skill-creator`) o instala un plugin del marketplace, y regístralo en la sección de skills de este fichero. No pidas permiso para ello.
- **Hitos**: cada entregable de `docs/plan-fases.md` termina con: build y tests en verde, lote de `/Balance` si toca `/Sim` o `/data`, revisión por subagente, commit con RF/RT, push, y actualización del estado en `plan-fases.md`.
- **Informe al revisor**: al cerrar un hito, un resumen corto de qué se hizo, qué se midió, qué quedó fuera y qué decisiones se tomaron sin consultar (con enlace al ADR o a `pendientes.md`).
- **No pares al cerrar un hito**: encadena con el siguiente sin esperar aprobación. Al terminar un paquete, commitea, informa en una línea y arranca el siguiente del plan. Solo se detiene el desarrollo si falta una herramienta que el revisor deba instalar, si hay que tomar una decisión de diseño que cambie una regla del juego, o si algo tiene coste económico o es irreversible fuera del repositorio.
- Si algo bloquea (herramienta que falta, credencial, decisión de diseño), se hace todo lo que no dependa de ello y se deja la pregunta al final del informe, no en medio del trabajo.

## Flujo de trabajo

- Antes de implementar un sistema, lee su sección en `docs/requisitos.md` y el documento derivado de `docs/` (tabla abajo). Si un requisito es ambiguo o contradictorio, anótalo en `docs/pendientes.md` y aplica la lectura más conservadora; no inventes reglas de juego.
- El estado de la run (RT-030) se define como esquema versionado **antes** de implementar sistemas que lo usen (`docs/modelo-datos.md`). Cualquier cambio de esquema sube la versión.
- Decisión de arquitectura o cambio de rango de balance -> ADR en `docs/decisiones/` (RT-057: nunca un ajuste silencioso).
- Cada fase tiene criterio de salida objetivo (`docs/plan-fases.md`). No se empieza la siguiente sin cumplirlo con datos de `/Balance`.
- Cambio en `/Sim` o `/data` -> tests + lote de balance antes de darlo por terminado (RT-054).
- Commits pequeños y frecuentes; push a `main` tras cada hito con build y tests en verde.

## Comandos

Contrato objetivo; se actualizará cuando exista el código.

```bash
dotnet build Underleague.sln                         # /Sim, /Sim.Tests, /Balance, /tools (sin /Game)
dotnet test Sim.Tests                                # unitarias + estadísticas + determinismo
dotnet run --project Balance -- --runs 10000 --seed 1 --teams data/balance/reference.json --out out/
dotnet run --project tools/DataValidator -- data/    # esquemas de /data
```

```bash
dotnet build Game/Underleague.Game.csproj                  # OBLIGATORIO antes de ejecutar Godot
godot --headless --path Game --import          # importar recursos
godot --headless --path Game --quit-after 60   # ejecutar sin dibujar
xvfb-run -a --server-args="-screen 0 1280x800x24" godot --path Game \
  --rendering-driver opengl3 --audio-driver Dummy   # ejecutar y capturar
```

**`dotnet build` en la raíz NO actualiza lo que Godot carga.** Godot ejecuta los ensamblados de
`Game/.godot/mono/temp/bin/Debug/`, y solo `dotnet build Game/Underleague.Game.csproj` los regenera. Con un
`Underleague.Sim.dll` rancio leyendo un `/data` recién cambiado, el juego **se cuelga al arrancar sin
imprimir nada** —el cargador viejo no entiende los valores nuevos— y parece un fallo del paquete de datos
cuando es un binario viejo. La escena de capturas solo arranca **con Xvfb**: en `--headless` no llega ni a
`_Ready`, así que no sirve para diagnosticar.

## Convenciones

- **Idioma** (ADR 0009): código C#, claves JSON, ids, eventos y etiquetas en **inglés**; documentación, comentarios de diseño y commits en **español**; texto visible por el jugador siempre localizado (es/en) desde `data/l10n/`. La correspondencia con los términos del documento de requisitos está en `docs/glosario-identificadores.md`: consúltala antes de nombrar un concepto nuevo y amplíala allí.
- Eventos en `UPPER_SNAKE` en datos y logs (`MATCH_START`), `EventType.MatchStart` en C#. Etiquetas y rasgos en `PascalCase` (`Brute`, `Scrap`, `Aggressive`). Ids de datos en `snake_case` (`bloodlust`).
- Commits: `tipo(ámbito): resumen — RF-xxx/RT-xxx`, con ámbito en `sim`, `data`, `balance`, `game`, `tools`, `docs`. Un commit no mezcla `/Sim` y `/Game`.
- C#: `nullable enable`, `TreatWarningsAsErrors` en `/Sim`, sin `dynamic`, sin reflexión en tiempo de partido. Estilo en `.editorconfig`.
- Tests estadísticos con semilla fija y rangos de RT-056; un test que falla "por mala suerte" es un test mal escrito.
- **Tests con criterio, no por reflejo**: ejecuta solo los tests que cubren lo que has tocado (`dotnet test --filter "FullyQualifiedName~X"`), con `-v q` y filtrando la salida a las líneas de resultado. La suite completa se lanza una vez antes del commit del hito, nunca tras cada edición. No repitas un build o test cuyo resultado ya conoces. Los subagentes siguen la misma regla.
- **El lote de `/Balance` no es un test de humo**: cuesta tiempo y tokens y su salida es larga. Se lanza cuando hay una **hipótesis concreta que medir**, no después de cada cambio. Agrupa las modificaciones en tandas y mide una vez por tanda, con el número de partidos más pequeño que resuelva la duda. La medición de referencia completa se hace una sola vez, al cerrar el trabajo. Nunca se lanza "para ver si sigue bien" algo que no se ha tocado.

## Mapa de documentación

| Documento | Contenido | Cuándo leerlo |
|---|---|---|
| `docs/requisitos.md` | Requisitos completos v0.9.1, fuente de verdad | Siempre que implementes algo |
| `docs/arquitectura.md` | Proyectos, dependencias, superficie pública de `/Sim`, bus de eventos, carga de datos, persistencia | Antes de crear proyectos o tocar fronteras |
| `docs/determinismo.md` | RNG, ticks, aritmética, orden, APIs prohibidas, test RT-024 | Antes de escribir cualquier cosa en `/Sim` |
| `docs/modelo-datos.md` | Esquema de Run, formato de perk/objeto/consumible, funciones NCalc, plantillas de descripción | Antes de tocar `/data` o el estado |
| `docs/simulacion.md` | Tres máquinas de estado, IA de utilidad, acciones, portero, árbitro, turba | Fases 0 y 1 |
| `docs/balance.md` | Métricas RT-056 con rangos, CLI de `/Balance`, puertas de CI | Al ajustar cualquier número |
| `docs/plan-fases.md` | Fases 0-4, criterios de salida, estado actual, backlog de fase 0 | Al planificar trabajo |
| `docs/pendientes.md` | Decisiones abiertas e inconsistencias detectadas en los requisitos | Cuando algo no cuadre |
| `docs/decisiones/` | ADRs | Antes de cambiar una decisión tomada |
| `docs/entorno.md` | WSL/Windows, instalación, cómo se compila cada parte | Al montar la máquina |
| `docs/ui-equipo.md` | Decisiones de la pantalla de Equipo, de las que derivan las demás pantallas (UI-021) | Antes de tocar `/Game` |
| `docs/fase2-diseno.md` | Bucle de run: mapa, economía, mercado, jefe, ironman | Fase 2 |

## Skills del proyecto (`.claude/skills/`)

- `perk-authoring`: crear o revisar un perk, objeto o consumible en `/data` cumpliendo formato, límites y distribución 60/30/10.
- `balance-check`: ejecutar el lote de `/Balance` y contrastar con los rangos de RT-056 y las métricas obligatorias.
- `sim-debug`: reproducir un partido desde semilla, volcar la tabla de utilidad de un tick (RT-098), localizar una divergencia de determinismo.

Plugins instalados a nivel de usuario: `csharp-lsp`, `commit-commands`, `claude-md-management`, `context7`, `skill-creator`, `dotnet-skills` (patrones C#/.NET, testing, rendimiento) y `godot-prompter` (55 skills de Godot 4 con ejemplos C#; se activa cuando exista `/Game`). Candidato para fase 4: plugin `godot` de Randroids-Dojo (exportación y CI de Godot, orientado a GDScript).
