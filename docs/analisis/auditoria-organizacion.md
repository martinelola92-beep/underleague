# Auditoría de la organización de trabajo

**15 sep 2026.** Encargo del revisor: auditar cómo está montado el sistema con el que se construye
Underleague —instrucciones, conocimiento, skills, reglas, agentes, hooks y herramientas— sin dar por buena
la estructura actual. **Diagnóstico y propuesta; no se ha cambiado nada todavía.**

Etiquetas: **MEDIDO** (contado en el repositorio) · **DERIVADO** · **JUICIO**.

---

## 1. Qué hay hoy, medido

| pieza | estado |
|---|---|
| `CLAUDE.md` | **28.034 caracteres, 202 líneas**. Un solo párrafo de «Estado» ocupa **5.809 car = 21 % del fichero**, cita **18 ADRs** y **29 cifras concretas** |
| Skills | **3**: `perk-authoring` (57 líneas), `sim-debug` (54), `balance-check` (31) |
| Agentes | **2**: `fast-worker` (sonnet), `deep-reasoner` (opus) |
| Commands | **0** |
| Hooks de Claude Code | **0** (los de `.git/hooks` son solo de Git LFS) |
| MCP | **0**, ni de proyecto ni global |
| `docs/` | **43 documentos** (15.853 líneas) + **108 ADRs** + 11 análisis |
| `docs/pendientes.md` | **383 líneas, 34 filas** en una tabla plana |
| CI | Completo: build, tests, puertas, `DataValidator`, lote de `/Balance`, y **comparación de huella de determinismo entre Windows y Linux** |
| `.claude/worktrees/` | **11 copias completas del repositorio, 302 MB**, con 11 `CLAUDE.md` obsoletos dentro |

---

## 2. Diagnóstico: cinco fallos estructurales

### 2.1 El fallo dominante no es de instrucciones: es de **disciplina de evidencia**

*(MEDIDO sobre el historial y sobre esta misma sesión.)* El mismo error se ha repetido **al menos cinco
veces**, y siempre con la misma forma: **una hipótesis plausible se convierte en decisión técnica sin
ejecutar el experimento barato que la habría separado**.

| caso | qué pasó |
|---|---|
| `ChaseBall pen=50` (auditoría 5) | Se aceptó descartando una puerta roja como ruido. Daño real: 3 puertas rojas → 5 → 7 |
| Ensanchar la formación (`cierre-siete-filas`) | Recomendado sobre **una** métrica en **una** variante sin lanzar puertas. Las cuatro fallaron. Retractado en ADR 0109 |
| Cámara en tres cuartos (13 sep) | Afirmada una relación geométrica al revés. La corrigió **un subagente**, con medidas |
| `a0a8b33` «las primitivas no mueven balance» | **Falso**, y medible: una puerta pasó de roja a verde por ese commit |
| Sed de médula (hoy) | Dije «confirmado y es por diseño». Era falso: confundí la vía letal con la de lesión. La instrumentación que pidió el revisor lo desmintió en minutos |
| BB-G (hoy) | **Dos** hipótesis escritas como código y revertidas: el muro de la zona y el portero acaparando la designación. Ninguna era la causa |

**Lo que hay hoy contra eso**: tres anécdotas embebidas en «Convenciones» de `CLAUDE.md`. Son *recordatorios*,
no *procedimiento*. Un recordatorio se lee y se olvida a los veinte turnos; un procedimiento se ejecuta.

**DERIVADO:** falta una skill de depuración con un **orden obligatorio** —instrumentar → medir → separar
hipótesis → decidir— y con las herramientas del proyecto ya nombradas (volcado de utilidad RT-098, traza,
semillas). Ese es el cambio de mayor impacto de toda la auditoría.

### 2.2 `CLAUDE.md` se usa como base de datos, y el revisor ya lo había prohibido

**El 21 % del fichero es un changelog**: dieciocho ADRs y veintinueve cifras en un párrafo. Eso es
información **que caduca**, y caducar dentro del fichero que se carga siempre tiene dos costes:

1. **Se lee en cada sesión** aunque casi nunca haga falta.
2. **Envejece sin que nadie lo note.** Ya pasó: se informó de BA-A y BA-B como abiertas cuando un commit
   las había cerrado, porque `pendientes.md` estaba rancio y se creyó sin cruzarlo con el código.

### 2.3 Contradicción entre dos instrucciones del propio revisor *(y es la causa del punto anterior)*

- **Instrucción antigua**: «*Estos errores que cometes anótatelos en `claude.md` o skills para que no se
  repita*».
- **Instrucción nueva** (§18): «*No uses `CLAUDE.md` como base de datos del proyecto*».

He estado obedeciendo la primera **apilando anécdotas** en «Convenciones», que es hoy la sección más grande
del fichero (5.788 car). Cada lección individual estaba justificada; el agregado es exactamente lo que la
instrucción nueva prohíbe. **No es que una de las dos esté mal: falta el destino correcto.** Una lección
debe convertirse en **procedimiento dentro de una skill**, o en **hook** si es mecánica, y solo en regla de
`CLAUDE.md` si es un principio corto e intemporal.

### 2.4 Los dos agentes están partidos por **potencia**, no por **perspectiva**

`fast-worker` y `deep-reasoner` son «sonnet para lo mecánico, opus para lo difícil». Eso ha funcionado y no
lo tocaría. Pero **no aporta lo único que un agente da y yo no puedo darme solo: aislamiento de contexto**.
Cuando reviso mi propio trabajo estoy anclado a mi propio razonamiento — se ve en esta sesión, donde
escribí dos arreglos para BB-G antes de medir. Un revisor que **no ha visto mi razonamiento** es la única
figura que rompe ese anclaje.

### 2.5 Cero automatización de lo que sí es mecánico y sí ha fallado

No hay hooks. Y hay al menos tres errores **recurrentes y puramente mecánicos**:

- Commits que mezclan `/Sim` y `/Game` (pasó el 13 sep y está anotado en `CLAUDE.md` como lección).
- `git add -A` con subagentes en marcha barriendo trabajo a medias.
- Tocar `/data` sin pasar `DataValidator`.

Son comprobaciones de tres líneas. Hoy dependen de que yo me acuerde.

---

## 3. Respuestas directas al cuestionario

**A. Duplicadas.** La regla de `-c Release` aparece **cuatro veces**: `CLAUDE.md` §Comandos, §Convenciones,
y los dos agentes. Lo mismo la de no lanzar `Category=Gate` (tres veces) y la del lote de `/Balance` (tres).
Es duplicación defensiva porque los agentes no heredan `CLAUDE.md` de forma fiable — legítima, pero debe
resolverse con **una fuente y una referencia**, no con copias que se desincronizan.

**B. Permanentes.** Solo lo intemporal y corto: las 11 reglas sin excepción, la estructura de la solución,
las convenciones de nombres y commits, el modo de trabajo autónomo, y **los criterios de razonamiento que
pide el revisor en §3-§9** (que hoy no están en ninguna parte).

**C. Skills.** Todo lo que sea un procedimiento con comandos: los comandos de compilación y prueba, la
receta de capturas, el lote de `/Balance`, la depuración del simulador, la autoría de perks.

**D. Procedimientos que deben ser skills.** Ver §5.

**E. Documentación, no instrucciones.** El párrafo de Estado entero, la tabla de ADRs recientes, el mapa de
documentación (que es un índice, no una instrucción), y las tres anécdotas de Convenciones.

**F. Subagentes.** Ver §6: solo uno nuevo está justificado.

**G. Hooks.** Ver §7.

**H. Capacidades que faltan.** *(JUICIO)* Por orden de dolor real: **(1)** ver el juego correr —hoy
bloqueado por BA-L, que es la capacidad más cara de no tener, porque el criterio de salida de la fase 3 es
«el partido se lee sin el log» y no puedo juzgarlo; **(2)** una consulta rápida de estado del proyecto que
no dependa de que yo recuerde; **(3)** comparación de capturas antes/después.

**I. Lo que induce comportamiento local.** Dos cosas concretas. La primera, **`pendientes.md` como tabla
plana de 34 filas**: su forma *empuja* a tratar cada anotación como un ticket, y es exactamente lo que el
revisor critica en §4 — hoy no hay ningún campo que relacione un síntoma con su causa ni con sus hermanos.
La segunda, que `CLAUDE.md` describe **flujo** (pasos: lee, implementa, testea, commitea) y no **criterio**
(qué preguntarse antes de tocar nada).

**J. Riesgo de perder contexto.** La compactación conserva lo que está en `docs/`, no lo que está en mi
cabeza. Lo que hoy **no** sobrevive de forma fiable: qué hipótesis ya se descartaron y por qué, qué
mediciones ya se hicieron y con qué resultado, y el estado real de las puertas rojas. Un ejemplo de esta
misma sesión: arrastré «3 puertas rojas» durante horas cuando eran 4.

**K. Información que debería estar estructurada.** El registro de pendientes (con causa y hermanos), el
registro de mediciones (métrica, valor, fecha, commit), y el estado de las puertas.

---

## 4. Arquitectura propuesta: quién guarda qué

| capa | contenido | criterio |
|---|---|---|
| **`CLAUDE.md`** | Identidad del juego (3 líneas), las 11 reglas sin excepción, estructura, convenciones, modo de trabajo, **criterios de razonamiento**, índice de skills. **Objetivo: ≤ 12.000 caracteres** *(hoy 28.034)* | Lo que es cierto en **toda** sesión y **no caduca** |
| **Skills** | Procedimientos con comandos y trampas conocidas | Lo que se necesita **a veces** y es **repetible** |
| **`docs/estado.md`** *(nuevo)* | El párrafo de Estado, generado y fechado | Lo que **caduca** |
| **ADRs** | Decisiones y su porqué | Lo que **no debe reabrirse sin leerlo** |
| **`docs/*.md`** | Conocimiento de dominio | Lo que se **consulta**, no lo que se **obedece** |
| **Hooks** | Comprobaciones mecánicas | Lo que **no debe depender de que yo me acuerde** |

---

## 5. Skills: cuáles de las siete propuestas sobreviven

El revisor propone siete y pide que no se creen por tener buen nombre. **Sobreviven tres, se fusionan dos y
se rechazan dos.**

### Se crean

**`gameplay-debug`** — *la más importante de toda la auditoría.*
- **Cuándo**: cualquier síntoma observado en el juego, propio o del revisor.
- **Objetivo**: impedir que una hipótesis se convierta en código sin experimento previo.
- **Qué hace**: obliga a un orden —**1)** enunciar las dos hipótesis rivales; **2)** diseñar la medición que
  las separa; **3)** ejecutarla; **4)** solo entonces decidir— y trae el instrumental ya nombrado: volcado de
  utilidad (RT-098, `SimConfig.DumpUtility`), traza (`SimConfig.Trace`), semillas, y la advertencia de que
  **una posición leída en el tick equivocado miente** (`LeavePitch` → `(-1,-1)`, que hoy me ha costado dos
  hipótesis falsas).
- **Qué NO hace**: proponer arreglos. Termina en un diagnóstico con evidencia.

**`visual-review`** — porque hoy **no verifico nada visualmente** y el criterio de salida de fase 3 es visual.
- **Cuándo**: cualquier cambio en `/Game`.
- **Qué hace**: la receta de las tres entradas de captura, el `timeout` obligatorio, comprobar la **marca de
  tiempo** del PNG, y comparar antes/después.
- **Qué NO hace**: declarar que algo «se ve bien» sin haber mirado el fichero.

**`balance-measure`** — reemplaza a `balance-check`, que hoy solo sabe lanzar el lote.
- Añade lo que falta y ha fallado: **medir siempre contra un baseline del mismo árbol** (la técnica del
  `git stash` que hoy ha funcionado dos veces), **una hipótesis por tanda**, y mirar **todas** las métricas
  de diferenciación, no solo la que motivó el cambio.

### Se fusionan

**`simulation-audit`** se absorbe en `gameplay-debug`: el 90 % de su contenido es el mismo instrumental.
**`godot-development`** ya existe como plugin (`godot-prompter`, 55 skills). Duplicarlo es peor que usarlo.

### Se rechazan

**`game-design-review`** y **`architecture-review`** *(JUICIO, y va contra la propuesta del revisor)*: son
**marcos de pensamiento**, no procedimientos. No tienen comandos, ni trampas, ni pasos verificables. Una
skill que solo dice «piensa en la experiencia del jugador» no cambia el comportamiento; lo que sí lo cambia
es que ese criterio esté **siempre cargado** en `CLAUDE.md` —donde hoy no está— y que exista un **agente
revisor** que lo aplique sin estar anclado a mi razonamiento.

### Se conservan

`perk-authoring` y `sim-debug` (este último renombrado y ampliado dentro de `gameplay-debug`).

---

## 6. Agentes: solo uno nuevo está justificado

Los seis propuestos por el revisor no superan su propio filtro («no por modularidad estética»). Un agente
se justifica por **aislamiento de contexto**, **perspectiva** o **workflow propio** — y `fast-worker` /
`deep-reasoner` ya cubren la ejecución.

**Se crea uno: `independent-reviewer`.**
- **Por qué**: es la única figura que no puede anclarse a mi razonamiento, porque **no lo ha visto**.
- **Cuándo**: antes de cerrar cualquier paquete que toque `/Sim`, `/data` o una ADR.
- **Qué recibe**: el diff y el encargo original. **No** mi explicación de por qué está bien.
- **Qué devuelve**: qué afirma el commit que no esté demostrado, qué efecto de segundo orden no se ha
  medido, y qué problema hermano no se ha buscado.
- **Evidencia de que hace falta** *(MEDIDO)*: la corrección de la cámara del 13 sep vino de un subagente, no
  de mí; y hoy he escrito dos arreglos de BB-G antes de medir.

Los demás —Gameplay Designer, Visual QA, Balance Analyst— **no** se crean: su valor está en el *criterio*,
que debe vivir en `CLAUDE.md` y en las skills, no en un proceso aparte.

---

## 7. Hooks: tres, y solo tres

Solo los que eliminan un error **recurrente y mecánico** ya observado:

| hook | evento | qué hace | evidencia |
|---|---|---|---|
| **Frontera `/Sim`–`/Game`** | `PreToolUse` en `git commit` | Avisa si el índice mezcla `Sim/` y `Game/` | Pasó el 13 sep; está en `CLAUDE.md` como lección |
| **`/data` sin validar** | `PostToolUse` tras editar `data/**` | Recuerda `DataValidator` | Convención dura RT-032/RT-083 |
| **`git add -A` con subagentes vivos** | `PreToolUse` en `git add` | Bloquea el comodín | Lección explícita en `CLAUDE.md` |

**No** se crean hooks que lancen tests o el lote de `/Balance` automáticamente: van contra la regla medida
de que las puertas y el lote se lanzan **con criterio**, no por reflejo.

---

## 8. MCP y herramientas de Godot

**Hoy: cero MCP.** *(MEDIDO.)*

**Lo que tenemos**: ejecutar Godot headless, capturas por Xvfb, compilar `/Game`, y el plugin
`godot-prompter` (conocimiento, no herramientas).

**Lo que falta, por dolor real**: estado en runtime, árbol de nodos, errores de ejecución legibles, y
comparación de capturas.

**Recomendación *(JUICIO)*: no instalar ningún MCP de Godot todavía, y arreglar BA-L primero.** Razones:

1. **El problema de hoy no es de capacidad, es que la capacidad que hay está rota.** La escena de capturas
   no produce nada. Un MCP encima de un pipeline roto no arregla nada y añade una pieza que mantener.
2. Los MCP de Godot que existen dependen en general de un **editor abierto**, y aquí no hay editor: WSLg
   está deshabilitado y el editor de Windows no puede abrir el proyecto (rutas UNC).
3. El riesgo real es **sustituir razonamiento por herramienta**, que es lo que el propio encargo advierte.

**Orden**: arreglar BA-L → medir si sigue faltando algo → **entonces** evaluar MCP con un caso de uso
concreto, no antes.

---

## 9. Estructura `.claude/` propuesta

```
.claude/
├── settings.json
├── agents/
│   ├── fast-worker.md
│   ├── deep-reasoner.md
│   └── independent-reviewer.md      (nuevo)
├── skills/
│   ├── gameplay-debug/SKILL.md      (nuevo; absorbe sim-debug)
│   ├── visual-review/SKILL.md       (nuevo)
│   ├── balance-measure/SKILL.md     (sustituye a balance-check)
│   ├── perk-authoring/SKILL.md      (se conserva)
│   └── build-and-test/SKILL.md      (nuevo: la fuente ÚNICA de los comandos)
└── hooks/
    ├── sim-game-boundary.sh
    ├── data-validation.sh
    └── no-add-all.sh
```

`build-and-test` existe para matar la duplicación de §3.A: los comandos viven **una vez**, y `CLAUDE.md` y
los agentes **referencian** en lugar de copiar.

---

## 10. Plan de migración

**P0 — imprescindible**
1. **Borrar los 11 worktrees huérfanos** (302 MB, con `CLAUDE.md` obsoletos dentro que contaminan búsquedas).
2. **Extraer el Estado a `docs/estado.md`** y dejar en `CLAUDE.md` un enlace de una línea. −21 % de golpe.
3. **Crear `gameplay-debug`.** Es la que ataca el fallo dominante.
4. **Añadir a `CLAUDE.md` los criterios de razonamiento** de §3-§9 del encargo, que hoy **no están en
   ninguna parte** — el fichero describe flujo, no criterio.

**P1 — importante**
5. `visual-review` + arreglar **BA-L** (van juntas: la skill sin el pipeline no sirve).
6. `build-and-test` y **desduplicar** los comandos de los dos agentes.
7. `independent-reviewer`.
8. **Reestructurar `pendientes.md`** con campos de **causa** y **hermanos**, para que su forma deje de
   empujar hacia el ticket suelto.

**P2 — opcional**
9. Los tres hooks.
10. `balance-measure`.
11. Evaluar MCP de Godot, **solo después** de BA-L.

---

## 11. Lo que NO haría, y por qué

*(El encargo pide señalar conflictos, §23.)*

- **No crearía las siete skills propuestas.** Dos de ellas son marcos de pensamiento y una skill no es el
  sitio de un marco de pensamiento: es el sitio de un procedimiento.
- **No crearía seis agentes.** Cinco aportarían separación estética; el coste es real (cada uno relee
  contexto) y el beneficio, imaginario.
- **No instalaría un MCP todavía.** Primero hay que arreglar la capacidad que ya existe y está rota.
- **No convertiría esto en un sistema grande de golpe.** El encargo avisa contra sobreingeniería en §20, y
  aplica también a esta auditoría: **P0 son cuatro cambios**, y tres de ellos son borrar y mover cosas.
