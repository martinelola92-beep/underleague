# Underleague

Roguelite de gestión y autobatalla: el jugador dirige un equipo de fútbol 7 de criaturas fantásticas. Los partidos se resuelven solos en 60-90 s sobre una cuadrícula; todas las decisiones ocurren entre partidos. La identidad no es el fútbol, es la **carnicería administrada**: lesiones, muertes, prótesis y vínculos. El desgaste de la plantilla es el recurso central de la run.

PC (Steam), premium, sin online. **Estado del proyecto: `docs/project-state.md`** (editado a mano, no generado — actualízalo al cerrar un hito o una ADR, no aquí).

La fuente de verdad del diseño es `docs/requisitos.md` (v0.9.1). Cada requisito tiene identificador (`RF-xxx` funcional, `RT-xxx` técnico, `RA-xxx` arte, `UI-xxx` interfaz): cítalos en commits, ADRs, tests y comentarios cuando implementes o discutas uno.

## Cómo razonar sobre un cambio, antes de mirar un archivo

*(Auditoría de organización, decisión del revisor, 16 sep 2026: `docs/analisis/auditoria-organizacion-v2.md`.)*
No empieces por "¿qué archivo tengo que modificar?". Empieza por "¿qué concepto falta o está mal
representado?". Antes de tocar código:

1. ¿Qué está experimentando realmente el jugador?
2. ¿Cuál debería ser el comportamiento correcto?
3. ¿Qué regla del juego representa?
4. ¿Qué sistema debería ser responsable de esa regla — `/Sim`, `/Game` o `/data`?
5. ¿Es un problema aislado o hay un patrón hermano? (`docs/pendientes/README.md`)
6. ¿Qué otros sistemas pueden sufrir la misma causa?
7. ¿Existe ya una abstracción o convención del propio repositorio para esto? (mira antes de inventar una)
8. ¿La solución arregla la causa o solo oculta el síntoma?
9. ¿Qué consecuencias de segundo orden puede introducir?
10. ¿Cómo se demuestra que funciona?

**Piensa en sistemas, no en tickets.** Ante varios síntomas relacionados, busca causa común, estado común,
evento común o regla común antes de asumir que son N problemas independientes con N arreglos.

### Disparadores de skill — Regla B y Regla C

- **Toda mecánica de juego nueva o modificada** (perk, rasgo, evento, regla de economía, primitiva de
  motor) → skill `game-design-review`, **antes** de implementar.
- **Toda frontera de proyectos, abstracción nueva o primitiva de motor** → skill `architecture-review`.
- **Cualquier síntoma de partido, bug de comportamiento o "por qué el motor hizo X"** → skill
  `gameplay-debug`, siempre antes de proponer una causa.
- **Cambio que puede alterar comportamiento cuantificable** (pesos de IA, probabilidades, tablas de
  economía, catálogo de perks/objetos) → skill `balance-measure`. No cualquier cambio en `/Sim` o `/data`:
  una traducción, un DTO o un fix de replay no lo son.
- **Cualquier cambio en `/Game`** → skill `visual-review` antes de declarar que algo "se ve bien".

**Regla de precedencia**: si hay duda entre "esto es dato/balance" y "esto es mecánica/diseño", se ejecuta
`game-design-review`. Cuesta poco frente al coste de implementar una mecánica mala.

**Las skills se encadenan, no son excluyentes.** Una primitiva nueva típicamente pasa por
`game-design-review` → `architecture-review` → implementación → `balance-measure` (si toca balance) →
`build-and-test`.

### Regla de evidencia — Regla A

Antes de modificar código para explicar un comportamiento: consulta `docs/pendientes/` por el síntoma o el
sistema, enumera todas las hipótesis plausibles sin fijar un número, y ordénalas por **coste de
verificación × poder discriminativo** — no ejecutes la más barata si no distingue entre las hipótesis. **No
se modifica código mientras exista una medición de bajo coste capaz de discriminar entre ellas.** Protocolo
completo, con el instrumental del proyecto (RT-098, `MatchTrace`): skill `gameplay-debug`.

### Estado epistemológico — Regla F

Ninguna conclusión se escribe como "es así y ya está". Se etiqueta:

- **CONFIRMED** — reproducida con un experimento que la aísla.
- **LIKELY** — consistente con lo medido, sin experimento propio que la aísle de una alternativa.
- **REJECTED** — un experimento la contradijo. Si depende de un sistema que puede cambiar (una ADR, un
  rango de balance), se anota *"descartada bajo la ADR X"*, nunca *"falsa"* a secas — puede reabrirse si
  ese sistema cambia. Distinta de una hipótesis **sin evidencia de activación** (mecanismo real, nunca
  observado disparándose).

### Revisión independiente — Regla E

Antes de cerrar cualquier paquete que toque `/Sim`, `/data`, o una ADR: agente `independent-reviewer`. Le
llega el problema completo (`docs/pendientes/<ID>.md` con sus hipótesis descartadas, no solo la ganadora),
el diff, los tests — **nunca tu argumentación de por qué está bien**. Su plantilla incluye
`DESIGN CLAIM NOT PROVEN`: tests verdes demuestran que el código hace X, nunca que X sea la mecánica
correcta para Underleague — eso lo decide `game-design-review`.

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

- `/Sim`, `/Sim.Tests`, `/Balance` y `/tools` son .NET puro: se compilan y prueban **en WSL** con `dotnet`.
- `/Game` se compila y ejecuta con el Godot de Linux instalado en WSL (`~/.local/bin/godot`). **El editor de Windows no puede abrir el proyecto**: Godot no admite rutas UNC. No hay editor gráfico (WSLg deshabilitado en `.wslconfig`), así que las escenas se editan como texto y el resultado visual se verifica con capturas por Xvfb — skill `visual-review`.
- Instalado en WSL: .NET SDK 10.0.111 (y 8.0.130), `global.json` fija el 10; `csharp-ls` 0.27. En Windows: .NET SDK 10.0.400 y Godot 4.6.3 .NET, ambos vía winget.

## Estructura de la solución (RT-010)

```
/Sim            Librería .NET pura. CERO referencias a Godot
/Sim.Tests      Pruebas unitarias, estadísticas y de determinismo
/Balance        Consola: N partidos sin Godot -> CSV
/Game           Proyecto Godot. Referencia a /Sim
/data           JSON: perks, objetos, razas, clubes, consumibles, pesos de IA, generadores de nombres
/tools          Validadores de /data y scripts auxiliares
/docs           Requisitos, arquitectura, decisiones, plan, pendientes/, project-state.md
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

## Principios de diseño

Cuando dos implementaciones técnicamente válidas son equivalentes, prioriza la que haga las reglas del
juego más legibles para el jugador:

- comportamiento observable > modificadores numéricos invisibles
- identidad memorable > bonus genéricos
- reglas locales > cambios globales de IA
- eventos explícitos > transiciones invisibles
- feedback simple > cinemáticas complejas — el jugador no necesita una escena, necesita que nada
  desaparezca sin explicación
- consecuencias legibles > datos internos
- especialización significativa > combinaciones arbitrarias
- sistemas reutilizables > excepciones
- medición > intuición · comparación > opinión · demostración > suposición

## Modo de trabajo: desarrollo autónomo

El usuario actúa **únicamente como revisor**. Claude planifica, implementa, prueba, documenta, commitea y hace push por su cuenta, y solo consulta cuando una decisión cambia una regla de juego de `docs/requisitos.md`, tiene coste económico, o es irreversible fuera del repositorio.

- **Esquema 10-80-10**: la sesión principal (el modelo más capaz) hace el primer 10%, **planificar**: arquitectura, interfaces, criterios de éxito y restricciones, por escrito antes de que nadie codifique. El 80% de **ejecución** se delega a subagentes con modelos más baratos. El último 10% es **revisión** por la sesión principal contra el plan: huecos, desviaciones, qué falta antes de commitear.
- **Subagentes del proyecto** (`.claude/agents/`): `fast-worker` (sonnet) para trabajo mecánico con especificación cerrada. `deep-reasoner` (opus) para razonamiento pesado. `independent-reviewer` (opus) para revisión sin ver el razonamiento del implementador — Regla E arriba. `Explore` para búsquedas de solo lectura. Usa `fork` solo cuando el subagente necesite todo el contexto de la sesión.
- Cada encargo a un subagente es cerrado: qué ficheros puede tocar, qué interfaces debe respetar, qué tests deben pasar, y que no haga commit. Lanza en paralelo los encargos independientes. Siempre una revisión independiente antes de cerrar un hito.
- **Skills y plugins**: cuando un flujo se repita o requiera conocimiento específico, crea una skill en `.claude/skills/` (plugin `skill-creator`) o instala un plugin del marketplace, y regístralo en la sección de skills de este fichero. No pidas permiso para ello.
- **Nunca `git add -A` con subagentes en marcha.** Trabajan sobre el mismo árbol, así que barre su trabajo a medias hacia tu commit. Prepara siempre rutas explícitas (`git add Sim Sim.Tests data docs`) y mira `git status` antes de commitear. Hay un hook que avisa (`.claude/hooks/subagent-add-warning.sh`), pero no sustituye a mirar `git status`.
- **Cada commit publicado compila por separado, no solo el HEAD final.** El 16 sep 2026 el commit `9f4d25b` (BB-J) se llevó `MatchEngine.cs` sin `Utility.cs`: `origin/main` quedó publicado y roto (`CS1061`) hasta el commit de arreglo. Un hook lo bloquea automáticamente antes de publicar: `.claude/hooks/build-check-before-push.sh` (`PreToolUse` sobre `git push`) compila en un worktree aislado cada commit del rango `@{u}..HEAD` que toque una ruta compilable (`Sim`, `Sim.Tests`, `Balance`, `tools`, `.csproj`/`.slnx`, `global.json`) y deniega el push si alguno falla. No es una alternativa a separar bien los commits, es la red de seguridad para cuando se separan mal.
- **Hitos**: cada entregable termina con: build y tests en verde, lote de `/Balance` si toca `/Sim` o `/data` (skill `balance-measure`), revisión por subagente, commit con RF/RT, push, y actualización de `docs/project-state.md`.
- **Informe al revisor**: al cerrar un hito, un resumen corto de qué se hizo, qué se midió, qué quedó fuera y qué decisiones se tomaron sin consultar (con enlace al ADR o a `docs/pendientes/`).
- **Dentro de un hito no se para a pedir aprobación.** Solo se detiene el desarrollo si falta una herramienta que el revisor deba instalar, si hay que tomar una decisión de diseño que cambie una regla del juego, o si algo tiene coste económico o es irreversible fuera del repositorio.
- **Una sesión por hito.** Al cerrar un hito, deja en `docs/project-state.md` el siguiente paso concreto (qué hito, por dónde empezar, qué leer) para que una sesión limpia arranque solo con ese fichero; da el informe y para. El revisor hace `/clear` y pide seguir. Motivo (19 sep 2026): ~85 % del consumo venía de la sesión principal arrastrando >150k de contexto durante sesiones de 8+ h.
- Si algo bloquea, se hace todo lo que no dependa de ello y se deja la pregunta al final del informe, no en medio del trabajo.

## Flujo de trabajo

- Antes de implementar un sistema, lee su sección en `docs/requisitos.md` y el documento derivado de `docs/` (tabla abajo). Si un requisito es ambiguo o contradictorio, anótalo en `docs/pendientes/` y aplica la lectura más conservadora; no inventes reglas de juego.
- El estado de la run (RT-030) se define como esquema versionado **antes** de implementar sistemas que lo usen (`docs/modelo-datos.md`). Cualquier cambio de esquema sube la versión.
- Decisión de arquitectura o cambio de rango de balance -> ADR en `docs/decisiones/` (RT-057: nunca un ajuste silencioso).
- Cada fase tiene criterio de salida objetivo (`docs/plan-fases.md`). No se empieza la siguiente sin cumplirlo con datos de `/Balance`.
- Cambio en `/Sim` o `/data` -> tests + lote de balance antes de darlo por terminado (RT-054).
- Commits pequeños y frecuentes; push a `main` tras cada hito con build y tests en verde.

## Comandos, capturas y disciplina de procesos

Ver skills **`build-and-test`** (comandos, presupuestos de tiempo, disciplina de procesos) y **`visual-review`**
(capturas). Resumen innegociable: **todo proceso largo va en `timeout`**, se espera un **artefacto con marca de
tiempo** (nunca CPU alta), al doble del presupuesto se mata y se diagnostica, y dos esperas fallidas se anotan
en `docs/pendientes/` y se sigue.

## Convenciones

- **Idioma** (ADR 0009): código C#, claves JSON, ids, eventos y etiquetas en **inglés**; documentación, comentarios de diseño y commits en **español**; texto visible por el jugador siempre localizado (es/en) desde `data/l10n/`. La correspondencia con los términos del documento de requisitos está en `docs/glosario-identificadores.md`.
- Eventos en `UPPER_SNAKE` en datos y logs (`MATCH_START`), `EventType.MatchStart` en C#. Etiquetas y rasgos en `PascalCase` (`Brute`, `Scrap`, `Aggressive`). Ids de datos en `snake_case` (`bloodlust`).
- Commits: `tipo(ámbito): resumen — RF-xxx/RT-xxx`, con ámbito en `sim`, `data`, `balance`, `game`, `tools`, `docs`. Un commit no mezcla `/Sim` y `/Game` (hay un hook que avisa: `.claude/hooks/sim-game-boundary.sh`).
- C#: `nullable enable`, `TreatWarningsAsErrors` en `/Sim`, sin `dynamic`, sin reflexión en tiempo de partido. Estilo en `.editorconfig`.
- Tests estadísticos con semilla fija y rangos de RT-056; un test que falla "por mala suerte" es un test mal escrito.
- **El paralelismo vive en el arnés, no en `/Sim`**: `/Balance` y las puertas de `Sim.Tests` juegan sus partidos con `Parallel.For` sobre un array por índice, con cada semilla función pura del índice (`RngStreams.MatchSeed(seed, índiceGlobal)`) y las reducciones después, en orden. `/Sim` no conoce `Parallel` (RT-021) y **no es reentrante**: cada hilo del arnés juega con **su propio `Catalog`**; compartir uno entre hilos da resultados distintos en cada ejecución. Aceptación: salida byte a byte idéntica a la secuencial (ver skill `architecture-review`).

## Mapa de documentación

| Documento | Contenido | Cuándo leerlo |
|---|---|---|
| `docs/requisitos.md` | Requisitos completos v0.9.1, fuente de verdad | Siempre que implementes algo |
| `docs/project-state.md` | Estado vigente del proyecto, editado a mano | Al empezar una sesión |
| `docs/arquitectura.md` | Proyectos, dependencias, superficie pública de `/Sim`, bus de eventos, carga de datos, persistencia | Antes de crear proyectos o tocar fronteras |
| `docs/determinismo.md` | RNG, ticks, aritmética, orden, APIs prohibidas, test RT-024 | Antes de escribir cualquier cosa en `/Sim` |
| `docs/modelo-datos.md` | Esquema de Run, formato de perk/objeto/consumible, funciones NCalc, plantillas de descripción | Antes de tocar `/data` o el estado |
| `docs/simulacion.md` | Tres máquinas de estado, IA de utilidad, acciones, portero, árbitro, turba | Antes de tocar `Utility.cs` |
| `docs/balance.md` | Métricas RT-056 con rangos, CLI de `/Balance`, puertas de CI | Al ajustar cualquier número |
| `docs/plan-fases.md` | Fases 0-4, criterios de salida, backlog | Al planificar trabajo |
| `docs/pendientes/README.md` | Índice de problemas de gameplay activos, un fichero por problema | Ante cualquier síntoma, antes de hipotetizar (Regla A) |
| `docs/decisiones/` | ADRs | Antes de cambiar una decisión tomada |
| `docs/entorno.md` | WSL/Windows, instalación, cómo se compila cada parte | Al montar la máquina |
| `docs/ui-equipo.md` | Decisiones de la pantalla de Equipo (UI-021) | Antes de tocar `/Game` |
| `docs/ui-partido.md` | Pantalla de Partido: geometría y cámara (ADR 0102, 0114) | Antes de tocar la pantalla de Partido o de encargar arte |
| `docs/estilo-visual.md` | Tono visual (Lucky Tower), 3D + cámara dinámica (ADR 0114) | Antes de encargar arte o tocar la cámara |
| `docs/fase2-diseno.md` | Bucle de run: mapa, economía, mercado, jefe, ironman | Fase 2 |
| `docs/catalogo-perks-y-objetos.md` | Catálogo derivado de `/data`, se regenera, no se edita a mano | Al diseñar o revisar contenido de `/data` |
| `docs/referencia-motores-futbol.md` | Conclusiones de motores de fútbol open-source, con fuentes citadas | Al tocar intercepción/parada del portero o líneas de pase |

## Skills del proyecto (`.claude/skills/`)

- `gameplay-debug`: investigar un síntoma de partido, un bug de comportamiento, o "por qué el motor hizo X" — antes de escribir ningún arreglo. Regla A.
- `game-design-review`: evaluar una mecánica nueva o modificada antes de implementarla — las diez preguntas. Regla B.
- `architecture-review`: revisar una frontera de proyectos o una abstracción nueva antes de implementarla.
- `balance-measure`: medir un cambio que puede alterar comportamiento cuantificable, con baseline real y behavioral audit. Regla D.
- `visual-review`: ejecutar Godot, capturar y comparar antes de declarar que algo "se ve bien".
- `build-and-test`: comandos de compilación, prueba y validación — fuente única.
- `perk-authoring`: crear o revisar un perk, objeto o consumible en `/data` cumpliendo formato, límites y distribución 60/30/10.

Plugins instalados a nivel de usuario: `csharp-lsp`, `commit-commands`, `claude-md-management`, `context7`, `skill-creator`, `dotnet-skills` (desactivado en este proyecto por consumo: `.claude/settings.json`) y `godot-prompter` (55 skills de Godot 4 con ejemplos C#).
