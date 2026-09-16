# Auditoría de la organización de trabajo — V2: autocrítica de la V1

**16 sep 2026.** El revisor no aprobó `auditoria-organizacion.md` (V1, commit `b2009c2`) tal cual. Valoración:
diagnóstico 8/10, disciplina de evidencia 9/10, arquitectura 7/10, **razonamiento de diseño 6/10**, plan
7/10. Encargo explícito: *no implementar todavía*; demostrar dónde la arquitectura propuesta en V1
seguiría provocando los mismos errores que diagnosticaba, en siete ejes concretos, y producir V2.

Esto es autocrítica real, no una ronda de "tienes razón en todo". Hay puntos donde adopto el cambio tal
cual, puntos donde lo adopto con una pieza añadida que faltaba, y un punto donde mantengo mi posición y
explico por qué. Etiquetas de siempre: **MEDIDO / DERIVADO / HIPÓTESIS / JUICIO**.

---

## 0. El fallo más incómodo, primero: la sección de MCP de la V1 cometía el propio error que auditaba

Antes de entrar en los siete ejes, esto merece decirse aparte porque es el más serio.

V1 concluía "no instalar MCP todavía" razonando: *(1)* BA-L está rota, *(2)* la causa probablemente no es
falta de herramienta, *(3)* los MCP de Godot dependen de editor abierto. **La (2) nunca se midió.** Es
exactamente la forma del error que toda la auditoría diagnostica: una hipótesis plausible ("el problema no
es de herramienta") convertida en decisión ("no evaluar la herramienta") sin el experimento barato que la
habría separado de la alternativa.

Y la (3) —el supuesto factual sobre el que se apoyaba el rechazo— **tampoco se comprobó**, y estaba
**incompleta**. Ahora sí, buscado: existen MCP de Godot en dos familias distintas —
**automatización de editor** (Godot MCP Toolkit, Godot-MCP de IvanMurzak, Godot MCP Pro: 100+ herramientas
para construir escenas por lenguaje natural, que sí necesitan el editor vivo) y **runtime/captura**
(`Coding-Solo/godot-mcp` afirma edición headless sin ventana de editor; `Erodenn/godot-mcp-runtime` se
enfoca en ejecución). La primera familia es la que descartaba el argumento (3) y es efectivamente el
descarte correcto **para esa familia** —aquí las escenas se editan como texto, no por lenguaje natural
sobre un editor abierto—. Pero la segunda familia apunta directo a BA-L y **nunca se consideró por
separado**: V1 trató "MCP de Godot" como una sola cosa homogénea.

**Corrección:** no se rechaza MCP. Se prueba, acotado a la familia runtime/captura, con un timebox y un
criterio de éxito/fracaso, **como parte del propio diagnóstico de BA-L** —puede que sea la pieza que lo
resuelve, no una distracción de él—. Detalle en §7.

---

## 1. Diseño vs. implementación — el eje que V1 más descuidó, y por qué

**Autocrítica honesta:** V1 construyó su pieza central (`gameplay-debug`) alrededor del fallo que **acababa
de vivir en la misma sesión** (Sed de médula, BB-G). Es sesgo de recencia: el error más reciente y doloroso
se convirtió en el centro de la arquitectura, y el trabajo que de verdad domina esta fase del proyecto
—diseñar mecánicas nuevas (perks, primitivas, cámara)— quedó con una frase genérica en "Convenciones" en
vez de un procedimiento.

**Evidencia contra mi propia V1, de esta misma sesión:** la ADR 0112 (el flash de perk) se escribió
**después** de empezar a implementar —el estado que dejé al cortar contexto fue *"JUST WRITTEN, NOT YET
COMPILED"*—, no antes. Las seis primitivas de tanda 1 se encargaron a subagentes con una especificación
cerrada pero sin una nota de diseño previa revisada aparte: diseño e implementación salieron en el mismo
encargo. Si `gameplay-debug` hubiera sido la única pieza nueva, ninguno de estos dos casos habría cambiado.

**Lo que V2 añade:** una skill de diseño con gate explícito (§4), y **no** el "feature lifecycle" completo
de 8 etapas que propone el revisor tal cual — ver §6 para el porqué.

---

## 2. Hipótesis múltiples — el sesgo estaba en mi propia redacción

Escribí en V1: *"obliga a enunciar las **dos** hipótesis rivales"*. El revisor tiene razón en que fijar dos
es un sesgo, y puedo señalar de dónde vino: lo copié casi literal del ejemplo del propio encargo original
(§7, "Hipótesis A / Hipótesis B"), que usaba dos como ilustración, y yo lo convertí en regla.

**Y hay un segundo problema, más interesante, que el revisor no señaló y que encuentro revisando esta
misma sesión:** aunque se corrija a "todas las plausibles", nada obliga a **ordenarlas por coste de
verificación**. En BB-G de hoy hice tres intentos **secuenciales**, dos de ellos escribiendo código antes
de medir:

1. hipótesis "el muro de la zona bloquea al perseguidor" → parche escrito → medido → **falso** → revertido
2. hipótesis "el portero acapara la designación" → parche escrito → medido → **falso** → revertido
3. **volcado de la tabla de utilidad (RT-098)** → causa real en un volcado

El tercero era el **más barato y más directo** de los tres —RT-098 existe exactamente para "por qué el
motor eligió esto"— y fue el último que probé. Con dos falsos positivos de por medio. Enumerar hipótesis
sin ordenarlas por coste no habría evitado nada de esto: las tres estaban "enumeradas" mentalmente desde el
principio, el fallo fue el **orden de ataque**.

**Lo que V2 añade:** el protocolo de `gameplay-debug` no solo enumera sin fijar número; **ordena** por
coste de verificación, y pone explícitamente **el volcado del propio mecanismo de decisión (RT-098,
traza) antes que cualquier hipótesis sobre el código de alrededor** — instrumentar el "qué" antes de
adivinar el "por qué".

---

## 3. Memoria de hipótesis descartadas — de acuerdo con el diagnóstico, matizo el artefacto

El revisor señala, correctamente, que `docs/estado.md` (mi propuesta V1) no resuelve esto: contar
que BA-G está abierta no es lo mismo que saber que dos causas concretas ya se probaron y por qué fallaron.

**Donde sí adopto un cambio de forma respecto a lo que propone el revisor:** su propuesta son tres ficheros
globales (`hypotheses.md` / `measurements.md` / `rejected.md`) o uno solo con filas. Prefiero otra forma,
por una razón concreta: una hipótesis y su medición **solo tienen sentido dentro del problema al que
pertenecen**; partirlas en ficheros globales por *tipo* obliga a cruzar referencias por id constantemente,
que es justo el defecto que ya tiene `pendientes.md` como tabla plana (§4).

**Y aquí hay algo que la V1 debería haber visto y no vio:** el proyecto **ya tiene el patrón correcto para
esto**, aplicado a decisiones — `docs/decisiones/`, un fichero por ADR más un índice. La misma forma sirve
para un problema abierto: **un fichero por problema sistémico**, con su propia historia de hipótesis,
experimentos y resultado, más un índice que mantiene la vista rápida. No es una idea nueva que haya que
inventar y mantener; es reutilizar una convención que ya funciona. Que V1 no lo notara es un ejemplo
concreto del punto 20 del encargo —"¿esta abstracción elimina complejidad real o la mueve?"— aplicado al
revés: aquí la abstracción **ya existía** y V1 no la reconoció.

**Segundo problema, que ni el revisor ni mi V1 nombran:** un artefacto de memoria solo sirve si se
**consulta**, no solo si existe. Nada en V1 obligaba a mirar el registro antes de proponer una hipótesis
nueva. Esto es una disciplina, no una estructura de ficheros, y por eso tiene que estar en el **primer
paso** del procedimiento de `gameplay-debug`, no en un documento aparte que confío en recordar.

---

## 4. Revisión independiente — el diseño de V1 tenía una fuga de la que yo controlo

`independent-reviewer` (V1) recibe "el diff y el encargo", no mi argumentación. Correcto en el papel. La
fuga: **yo decido qué diff, qué tests y qué contexto le paso.** Si me dejo un fichero fuera porque no creo
que sea relevante, el revisor nunca lo ve — y "no creo que sea relevante" es precisamente el tipo de juicio
que un revisor independiente existe para poner en duda.

Ejemplo de esta misma sesión: los dos parches de BB-G que escribí y reverté no dejaron rastro en el árbol.
Si hubiera cerrado esa tarea con un commit y un `independent-reviewer`, este habría visto un diagnóstico
limpio sin saber que hubo dos intentos fallidos antes — que es información relevante para juzgar si el
diagnóstico final es sólido o si es el tercero de una serie y podría no ser el último.

**Lo que V2 añade a la plantilla del revisor** (adoptando el esquema VERIFIED / UNVERIFIED / ASSUMED /
REGRESSION RISK / SECOND-ORDER EFFECTS / BROTHER PROBLEMS / MISSING TEST / DESIGN CONCERN que propone el
revisor, que es bueno y se incorpora tal cual): el encargo al revisor **incluye el registro de intentos
descartados** del problema (§3), no solo el commit final. Consecuencia práctica: mantener ese registro dejó
de ser opcional para mí, porque el revisor lo necesita para hacer su trabajo.

---

## 5. Game design — la ausencia más grande de la V1, aceptada sin reservas

Aquí no hay matiz: V1 se equivocaba. Rechacé `game-design-review` como skill razonando que era "un marco de
pensamiento sin comandos ni trampas" y que su sitio era `CLAUDE.md`. El error está en la premisa: **una
skill no tiene que ejecutar comandos para ser un procedimiento.** Un protocolo de diez preguntas en un
orden fijo, con lo que cuenta como respuesta válida en cada una, es exactamente tan procedimental como una
receta de compilación.

**Pero hay una razón real por la que dudé, y merece decirse en vez de callarse:** si `game-design-review`
es una skill, depende de que yo **reconozca** que la tarea es "de este tipo" para invocarla — y no
reconocerlo es justo el fallo de organización que toda esta auditoría investiga (comportamiento
demasiado local/ticket-driven). Una skill que hay que acordarse de invocar tiene el mismo punto ciego que
una regla que hay que acordarse de aplicar.

**Por eso V2 no se limita a "crear la skill": añade el disparador que la V1 y la propuesta del revisor no
tienen.** Una línea corta en `CLAUDE.md` —no el protocolo entero, solo el disparador— que dice: *toda
petición de crear o modificar una mecánica de juego (perk, rasgo, evento, regla de economía, primitiva
nueva) invoca `game-design-review` antes de escribir dato o código.* Eso convierte la skill de "disponible
si me acuerdo" a "parte del camino obligatorio para ese tipo de tarea", igual que `perk-authoring` ya
funciona hoy. El protocolo completo (las diez preguntas) vive en la skill; el gatillo, en `CLAUDE.md`.

---

## 6. Evolución de features — adopto el fondo, no la forma completa

De acuerdo en el fondo: no queremos implementar una mecánica entera para descubrir después que era mala.
Y hay evidencia a favor en esta sesión — el tope de Herencia se corrigió **antes** de que el perk existiera
comparándolo contra la progresión del juego, que es precisamente diseñar antes de medir con el jugador real
puesto en riesgo.

**Donde no adopto la propuesta tal cual:** el lifecycle de ocho etapas (IDEA → DESIGN → PROTOTYPE →
MEASURE → IMPLEMENT → VERIFY → REVIEW → DOCUMENT) con puerta en cada una, aplicado a **todo**, es
desproporcionado para el ritmo real del proyecto. Dato de esta sesión: **33 perks** de datos puros se
diseñaron, especificaron, encargaron, implementaron, probaron y commitearon en dos tandas dentro de una
sola sesión. Ocho puertas formales por cada uno de los 33 habría sido parálisis, y es exactamente lo que el
punto 20 del propio encargo pide evitar ("¿la abstracción elimina complejidad real o la mueve a otro
sitio?").

**Lo que sí hace falta, con umbral explícito:** el gate no se aplica al perk suelto, se aplica a lo que el
perk **usa** — la primitiva nueva, la regla de motor nueva, el cambio de ADR. En esta sesión eso son
~9-10 elementos (7 primitivas, 2 ADR, el reparto de `kind`), no 33. Regla concreta: *cualquier cambio que
añada un `EffectType`, toque una regla ya fijada por ADR, o introduzca un canal de balance nuevo, exige una
nota de diseño corta —3 a 5 líneas: qué decide, con qué compite, qué se mide— escrita antes de implementar.
Un perk que solo usa primitivas existentes no la necesita.* Es una versión de tres pasos
(DISEÑO breve → IMPLEMENTAR+VERIFICAR → REVISAR+DOCUMENTAR), no de ocho, y el umbral es "¿esto es una
primitiva/regla nueva, o es un dato que reutiliza lo que ya existe?".

---

## 7. MCP / observación — ya corregido en §0; el criterio de prueba

Para que la corrección del §0 no repita el mismo error en sentido contrario (aceptar MCP sin medir, en vez
de rechazarlo sin medir), la prueba tiene forma de experimento con criterio de parada, no de instalación:

**Alcance:** solo la familia runtime/captura (`Coding-Solo/godot-mcp` es el candidato con la afirmación más
directa de operación headless; se verifica esa afirmación, no se da por buena). Se descarta explícitamente
la familia de automatización de editor (100+ herramientas para construir escenas por lenguaje natural):
aquí las escenas se editan como texto por decisión ya tomada (`docs/entorno.md`), así que esa familia
resuelve un problema que este proyecto no tiene.

**Prueba mínima, con timebox de 30 minutos:** ¿puede, en este WSL sin editor gráfico, **(1)** ejecutar la
escena de capturas, **(2)** capturar un fotograma, **(3)** devolver un error de arranque legible? Si las
tres, es infraestructura real para diagnosticar BA-L y se adopta. Si ninguna funciona sin editor abierto,
se descarta con el hallazgo escrito (no se vuelve a proponer sin evidencia nueva) y BA-L se sigue atacando
como hasta ahora: instrumentación desde `/Sim` más capturas por Xvfb.

**No se instala antes de intentar diagnosticar BA-L por el camino ya conocido.** Si el camino conocido lo
resuelve primero, la prueba de MCP se pospone sin coste: ya no sería la infraestructura que falta, sería
una comodidad, y el punto 20 del encargo aplica de nuevo.

---

## 8. Lo demás de la crítica del revisor: adoptado sin reservas

- **`pendientes.md`**: se sustituye por `docs/pendientes/<id>.md` (un fichero por problema, patrón ADR) +
  índice. Un fichero puede tener varios síntomas y enlazar a sus hermanos.
- **`docs/estado.md`**: no se "genera". Es `docs/project-state.md`, editado a mano, con una regla de
  actualización explícita (al cerrar un hito o una ADR), y **solo** decisiones vigentes, sistemas activos,
  bloqueantes y trabajo en curso — nada derivable de `git log`.
- **Hooks**: "tres, y solo tres" se corrige a "tres justificados hoy, con el criterio de cuándo añadir un
  cuarto: error mecánico recurrente con coste de falso positivo bajo". Y el de `git add -A` deja de ser un
  bloqueo incondicional: se activa **solo** cuando hay agentes en segundo plano vivos (`ListAgents` no
  vacío), y avisa en vez de bloquear cuando no los hay.
- **12.000 caracteres**: objetivo, no ley. El criterio real es el que da el revisor: "¿esto hace falta
  prácticamente siempre?".
- **`architecture-review`**: mismo razonamiento que `game-design-review` (§5) — se crea como skill, con
  disparador corto en `CLAUDE.md` (cambio que toca una frontera de proyectos, o que introduce una
  abstracción nueva).

---

## 9. Arquitectura V2

```
CLAUDE.md   (objetivo ≤ 12k car., no ley — solo lo que hace falta SIEMPRE)
├── identidad de Underleague (3 líneas)
├── 11 reglas sin excepción
├── estructura de la solución
├── principios de diseño y de razonamiento (los del encargo, §3-§10, resumidos)
├── DISPARADORES cortos de skill (una línea cada uno):
│     · mecánica de juego nueva/modificada  -> game-design-review
│     · frontera de proyectos / abstracción -> architecture-review
│     · síntoma de partido, bug, comportamiento raro -> gameplay-debug
│     · cambio en /Sim o /data              -> balance-measure
│     · cambio en /Game                     -> visual-review
├── convenciones (nombres, commits, C#)
├── modo de trabajo autónomo
└── mapa de documentación (índice, no contenido)

.claude/
├── agents/
│   ├── fast-worker.md
│   ├── deep-reasoner.md
│   └── independent-reviewer.md      (nuevo — solo diff+tests+registro de intentos, NUNCA mi argumentación)
├── skills/
│   ├── gameplay-debug/              (nuevo — protocolo de §2 con orden por coste, y consulta obligatoria
│   │                                  del registro de problemas antes de hipotetizar)
│   ├── game-design-review/          (nuevo — las 10 preguntas del encargo §10)
│   ├── architecture-review/         (nuevo)
│   ├── balance-measure/             (sustituye balance-check: baseline por git stash, una hipótesis por
│   │                                  tanda, todas las métricas de diferenciación)
│   ├── visual-review/               (nuevo — receta de capturas + comparación antes/después)
│   ├── perk-authoring/              (se conserva)
│   └── build-and-test/              (nuevo — fuente única de los comandos, hoy duplicados 3-4 veces)
└── hooks/
    ├── sim-game-boundary.sh         (PreToolUse en git commit)
    ├── data-validation.sh           (PostToolUse tras editar data/**)
    └── subagent-add-warning.sh      (condicionado a agentes vivos, avisa — no bloquea, si no hay ninguno)

docs/
├── project-state.md                 (nuevo — editado a mano, no generado)
├── pendientes/
│   ├── README.md                    (índice: id, título, estado, enlace)
│   └── BB-G.md, BA-L.md, ...        (un fichero por problema, con hipótesis/experimentos/hermanos)
├── decisiones/                      (ADRs, sin cambios — es el patrón que se reutiliza arriba)
└── (resto sin cambios: requisitos, arquitectura, determinismo, etc.)
```

---

## 10. Todavía no se implementa nada

Igual que en V1. Esto es diagnóstico y propuesta corregida. Antes de tocar `CLAUDE.md`, `.claude/` o mover
`pendientes.md`, espero el visto bueno del revisor sobre esta V2 — en particular sobre los tres puntos
donde no adopté la propuesta tal cual (§3 el artefacto de memoria, §6 el lifecycle de tres etapas en vez de
ocho, §7 el alcance acotado de MCP), porque son los únicos donde esta versión se aparta de lo que pidió.
