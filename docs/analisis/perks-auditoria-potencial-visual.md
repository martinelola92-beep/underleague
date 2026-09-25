# Auditoría 13B — El potencial visual de los perks

**Encargo del revisor (25 sep 2026)**: no diseñar un catálogo de efectos, sino responder *cuánto sitio*
deja la arquitectura actual de perks para que un partido produzca momentos espectaculares y legibles, y
qué haría falta para que el rediseño de perks en curso (`perks-design-bible.md`) no cierre ese espacio sin
darse cuenta.

**Este documento no modifica nada del repositorio.** No propone un catálogo de efectos, no nombra
mecánicas concretas como decididas y no entra en implementación.

## Etiquetas (Regla F de `CLAUDE.md`)

- **MEDIDO** — leído directamente del código o de `/data`, o contado con aritmética exacta sobre ellos.
- **DERIVADO** — conclusión encadenada sobre algo medido.
- **HIPÓTESIS** — interpretación de diseño sin validar; se dice cómo se mediría.

No se ha lanzado ningún lote de `/Balance`: todo lo cuantitativo es un censo exacto de `data/perks/`
(102 ficheros) y de los enumerados de `/Sim`, que no necesita muestra.

---

# 0. Conclusión ejecutiva

**La pregunta del encargo**: *¿puede el sistema de perks de Knavall convertirse en una fuente de momentos
espectaculares y visualmente legibles, o su arquitectura es fundamentalmente demasiado estadística?*

**Respuesta: la arquitectura no es demasiado estadística. El catálogo sí lo es.** Son dos cosas distintas
y la distinción es la conclusión entera de esta auditoría.

1. **El catálogo de hoy es invisible por una mayoría abrumadora** *(MEDIDO)*: de 102 perks, **78 (76 %) no
   tienen un solo efecto que un espectador pueda ver**, y **35 de ellos son además `MATCH_START` con efecto
   puramente numérico**, es decir, literalmente los 22 jugadores aplicando modificadores de probabilidad
   invisibles al fútbol que el revisor describe en el encargo. **Cinco perks de 102 (≈5 %) producen un acto
   físico que alguien podría señalar y nombrar.**
2. **La arquitectura, en cambio, ya sostiene cuatro de las seis familias de espectáculo** que el encargo
   imagina, y una de ellas **ya está en el catálogo**: `earthquake` derriba a todos los rivales en un radio
   real de una casilla durante 18 ticks al entrar *(MEDIDO)*. Es una onda expansiva, existe, es determinista,
   y la pantalla ya sabe dibujar su consecuencia —la traza lleva `PlayerState` por jugador y por fotograma y
   `PlayerModel.Pose` la consume—. Lo que le falta no es motor: es **peso en el catálogo y gramática de
   presentación**.
3. **Hay exactamente dos huecos reales**, y no son del mismo tamaño *(MEDIDO)*:
   - **El balón.** De los **19 tipos de efecto**, **ninguno escribe sobre el balón**. Y sin embargo `Ball`
     ya tiene `Velocity`, `Z`, `VelocityZ`, `FlightArc`, `FlightTarget` y dueño, y la traza ya graba
     `BallAt`/`BallHeightAt` por fotograma y `/Game` ya los dibuja. **Es el hueco más barato y el de mayor
     rendimiento visual de todo el sistema**: el canal de presentación está hecho y lo que falta es el
     permiso para escribir en él.
   - **El espacio.** No existe **ningún** estado de mundo en el partido: ni casilla con estado, ni área
     activa, ni terreno *(MEDIDO: `Zone` son tres tercios y no hay nada más)*. La familia «zonas o campos
     temporales» es la única del encargo que **sí exige una abstracción nueva**, no una extensión.
4. **El riesgo que el encargo anticipa es real y está fechado.** La hoja de ruta de `perks-design-bible.md`
   §6 mueve el catálogo de *numérico invisible* a *conductual visible* —un jugador que prefiere disparar—,
   que es una mejora grande de legibilidad **y no es espectáculo**: produce fútbol normal con una
   distribución rara. De las 16 capacidades de esa hoja de ruta, **ninguna abre el balón y ninguna abre el
   espacio** *(MEDIDO)*. Si el rediseño se ejecuta tal cual, se gastará entero sin abrir este espacio
   expresivo, y después será mucho más caro abrirlo porque las 57 fantasías ya estarán escritas contra el
   vocabulario viejo.
5. **El desplazamiento mínimo son dos piezas, no un sistema** (§7): un **canal de efecto que escriba sobre
   el cuerpo y sobre el balón** (una primitiva de impulso, que reutiliza el búfer de empuje que ya existe)
   y un **vocabulario cerrado de gesto declarado en el dato** que `/Game` lea genéricamente, con el mismo
   patrón que `MomentKind` y `ContactCue`. Sin la segunda, la primera produce sucesos que nadie entiende;
   sin la primera, la segunda no tiene nada que anunciar.
6. **Hay un presupuesto medido que condiciona qué tipo de espectáculo conviene** *(MEDIDO, ADR 0147/0148)*:
   la violencia del partido está pegada a su techo —`injuriesPerMatch` 0,78-0,79 contra 0,90, faltas un
   68 % por encima de la línea base y sin explicación cerrada—. **El espectáculo que se route por más
   contacto no cabe.** El que se route por el balón, por el espacio y por el movimiento sí. Esto no es una
   preferencia estética: es la banda de RT-056.

---

# 1. El hecho medido: el catálogo de hoy es invisible

## 1.1 Censo de efectos (`data/perks/*.json`, 102 perks)

| efectos | perks | tipo de efecto | ¿lo ve el espectador? |
|---:|---:|---|---|
| 57 | 53 | `modifyProbability` | no |
| 24 | 24 | `addCounter` | no (contabilidad) |
| 7 | 6 | `modifyTraitScalar` | no |
| 6 | 6 | `modifyLeash` | indirectamente (posición media) |
| 5 | 4 | `immunity` | no (salvo `push`, y por ausencia) |
| 4 | 4 | `modifyAttribute` | no |
| 4 | 4 | `shiftHome` | indirectamente |
| 4 | 4 | `cancelEvent` | por ausencia: algo que no pasó |
| 3 | 3 | `modifyMarkBias` | indirectamente |
| 3 | 3 | `extraAction` | **sí** |
| 2 | 2 | `modifyTackleBias` | indirectamente |
| 2 | 2 | `setState` | **sí** |
| 2 | 2 | `modifyBias` | no (criterio del árbitro) |
| 1 | 1 | `modifyZoneShape` | indirectamente |
| 1 | 1 | `injure` | **sí** |
| 1 | 1 | `modifyKnockdownTicks` | apenas (duración) |
| 1 | 1 | `relocate` | sí, pero **ilegible** (§4.B) |
| 1 | 1 | `modifyExperience` | no (fuera del partido) |

## 1.2 Las tres clases, contadas

*(MEDIDO)*

- **78 perks (76 %)** tienen **solo** efectos numéricos (`modifyProbability`, `modifyAttribute`,
  `addCounter`, `modifyLeash`, `modifyTraitScalar`, `modifyBias`, `modifyExperience`).
- **0 perks** mezclan un efecto numérico con uno observable. **Cero.** No es que el espectáculo esté
  repartido: es que las dos poblaciones son disjuntas.
- **24 perks** tienen algún efecto no numérico. De ellos:
  - **13 dan forma a la IA o a la geometría** (`shiftHome`, `modifyZoneShape`, `modifyMarkBias`,
    `modifyTackleBias`, `immunity`): cambian *tendencias*, no producen un suceso.
  - **11 escriben sobre el cuerpo o sobre el flujo de la jugada**. Y de esos once, **4 son `cancelEvent`**
    (un gol que no sube, una lesión que no ocurre: espectáculo por negación, el más difícil de leer),
    **1 es una duración** (`hot_blooded`) y **1 es un teletransporte** (`last_man`).

**DERIVADO: quedan cinco perks de 102 que añaden un acto físico visible** — `earthquake` (derribo en área),
`dirty_play` (lesión provocada), y los tres `extraAction` (`charge`, `double_shot`, `steamroller`).

## 1.3 El disparador confirma lo mismo

*(MEDIDO)* De los 102 perks, **48 se cuelgan de `MATCH_START`**. El resto se reparte entre `TACKLE` (11),
`INJURY` (8), `SHOT` (7), `FOUL` (6), `DEATH` (4), `RECOVERY` (4), `GOAL` (3) y siete disparadores más con
uno o dos cada uno.

**DERIVADO**: `MATCH_START` no es un momento de fútbol, es el instante en que se reparten las cartas. Un
perk colgado de ahí **no puede producir un suceso por construcción**: no hay situación a la que reaccionar,
no hay actor enfrentado a nadie, no hay sitio en el campo donde anclar nada. `MATCH_START` + efecto
numérico —**35 perks**— es la definición exacta del problema que el encargo describe.

## 1.4 El dato que cambia el diagnóstico: el terremoto ya existe

```jsonc
// data/perks/earthquake.json  (MEDIDO)
{ "trigger": "TACKLE",
  "effects": [ { "type": "setState", "target": "adjacentOpponents",
                 "state": "KnockedDown", "ticks": 18 } ] }
```

Es una **onda expansiva**: al entrar, todos los rivales dentro de un radio **real** de una casilla —no la
casilla-hogar de la alineación, la `Position` del instante *(MEDIDO, `EffectTarget.AdjacentOpponents`)* — 
caen al suelo 1,2 s. Es determinista (recorrido por id ascendente, RT-041), es un efecto de área, y su
consecuencia **ya llega a la pantalla**: `MatchTrace.StateAt` graba `PlayerState` por jugador y fotograma, y
`MatchPitchView3D` lo pasa a `PlayerModel.Pose`.

**DERIVADO, y es el eje de todo el documento**: el motor no le impidió a nadie escribir un terremoto.
Escribir uno costó **un fichero JSON de seis líneas**. Lo que ha producido un catálogo del 76 % invisible no
es una barrera arquitectónica: es que el tipo de efecto barato —sumar un entero a una probabilidad— también
es el que no se ve, y nadie ha tenido que pagar por esa elección.

---

# 2. Qué puede ver la pantalla (el vocabulario real de presentación)

Cualquier juicio sobre «espectáculo» es ficción si no se sabe qué llega a `/Game`. Dos reglas lo gobiernan
y no se negocian:

- **RT-014**: el render consume eventos; **nunca calcula ni decide nada del partido**.
- **Regla 5**: perks y objetos son **datos**; el motor no nombra perks concretos (RT-034).

**DERIVADO de las dos juntas, y es la restricción de diseño más importante de esta auditoría: lo que no
está en la traza o en un evento no existe para el jugador, y `/Game` no puede inventarlo sin romper RT-014
ni hardcodear un perk sin romper la regla 5.**

## 2.1 Los canales que existen

*(MEDIDO, `Sim/Engine/MatchTrace.cs`)* Por fotograma: `tick`, `clockTick`, `phase`, `restart`,
`restartTaker`, balón (`x`, `y`, **`z`**), `ballOwner`, `ballInFlight`, y el rango de eventos del fotograma.
Por jugador y fotograma: `x`, `y`, **`state`**, `onPitch`, la zona de acción (hogar, sentido, profundidad,
retroceso, anchura), `markTarget`, la **acción decidida** y el punto objetivo.

*(MEDIDO, `Sim/Events/MatchEvent.cs`)* Cada evento lleva `Type`, `Tick`, `ClockTick`, `Team`, `Actor`,
`Target`, `Opponent`, **`Cell`**, `Zone`, `Phase`, `Bias`, `DistanceToGoal` y `Detail`. **Va anclado en el
campo**: un efecto puede dibujarse donde ocurrió.

*(MEDIDO, `/Game`)* `PlayerModel.Pose(paso, PlayerState, ContactCue)`; `ContactCue`/`ContactPart` deducidos
de la traza; gestos de cámara (sacudida y acercamiento) en `MatchPitchView3D`; la gramática narrativa
`MomentKind` × nivel **N1-N4** con congelado y cola de una plaza (`PresentationDirector`); y pools de sonido
por tipo de evento (`MatchEventSounds`). **No hay partículas ni shaders en la capa de partido** *(MEDIDO:
`grep` de `Particle|Shader` en `/Game` solo aparece en `PlayerStrip` y `AudioManager`)*, que es coherente
con la regla 10 —no se produce arte hasta cerrar la fase 2—.

## 2.2 Los dos agujeros de la capa de presentación

1. **`PERK_TRIGGERED` existe y nadie lo dibuja.** *(MEDIDO)* El evento se emite con el id del perk en
   `Detail`, su portador en `Actor` y su celda; `MatchEventSounds` lo clasifica explícitamente como
   contabilidad (*«los 19 `PerkTriggered` de un partido… meterlos sería ruido»*) y ninguna pantalla lo pinta.
   **La atribución llega y se tira.**
2. **`MomentKind` no tiene ninguna entrada de perk.** *(MEDIDO: 13 valores — `Kickoff`, `Foul`,
   `Consumable`, `Substitution`, `Yellow`, `MinorInjury`, `Goal`, `Red`, `SevereInjury`, `Mob`,
   `RefereeLeaves`, `Death`, `FullTime`.)* **DERIVADO: un perk no puede ser un momento.** No puede congelar
   la imagen, no puede llevar sello, no puede llevar estandarte, no puede ocupar la voz alta. La gramática
   que el proyecto construyó para que el jugador entienda lo que pasa **está cerrada a los perks**, que son
   justamente lo que el jugador construyó.

**DERIVADO**: aunque mañana se añadieran diez efectos espectaculares, con estos dos agujeros seguirían
ocurriendo sin nombre y sin pausa. **Espectáculo sin atribución es ruido** — es literalmente el criterio que
el encargo pide aplicar, y hoy la arquitectura de presentación lo incumple por omisión.

---

# 3. Preparación arquitectónica, mecanismo a mecanismo

El encargo pide evaluar nueve mecanismos. Veredicto con la evidencia delante.

| mecanismo | ¿existe hoy? | qué lo implementa | qué costaría abrirlo |
|---|---|---|---|
| **Fuerzas temporales** | **Sí, casi listo** | `BodySeparation` acumula empujes en un búfer de Jacobi con tope por tick y los aplica todos al final; `AddTacklePush` ya sube ese tope para una entrada *(MEDIDO)* | **Pequeño.** Un efecto que escriba en ese búfer reutiliza orden determinista, tope y suma conmutativa. No hay física nueva |
| **Override de movimiento** | **Sí, patrón hecho** | `EnterState(estado, ticks)`: `Dribbling`, `Shielding` y `Blocking` son el mismo molde —se entra con contador, no se vuelve a decidir, se corta al perder el balón— *(MEDIDO, ADR 0137)* | **Pequeño-medio.** Un estado con duración y desplazamiento propio es el molde existente. `PlayerState` está pensado para crecer por el final |
| **Cambios de estado del balón** | **NO** | Cero de 19 tipos de efecto tocan el balón. Pero `Ball` ya tiene `Velocity`, `Z`, `VelocityZ`, `FlightArc`, `FlightTarget`, `Owner`, y métodos `SetLoose`/`Head`/`Park` *(MEDIDO)*; la traza graba x, y, z y `/Game` los dibuja | **Pequeño, y es el de mayor rendimiento.** Falta el tipo de efecto y una API en `IPerkWorld`; **no falta ni estado ni canal de dibujo** |
| **Efectos espaciales** | **NO, y es el único hueco de verdad** | No existe estado de mundo: `Zone` son tres tercios; no hay casilla con estado ni lista de áreas activas *(MEDIDO)*. `Modifiers` es **por jugador**, con duración jugada/partido | **Grande.** Exige estado nuevo en el partido, evaluación por tick, un canal de traza nuevo y previsión en la pantalla de Equipo. Es **una abstracción nueva**, no una extensión |
| **Cambios de estado de jugador** | **Sí** | `setState(KnockedDown, ticks)`, `modifyKnockdownTicks`, `immunity(push)`, `relocate`, `injure` *(MEDIDO)*; la traza los publica y la pose los dibuja | **Nulo.** Está hecho y usado por 11 perks. Lo que falta es catálogo |
| **Cambios de entorno** | **NO** | No hay entorno. Ni clima, ni césped, ni grada con estado. La turba (`MobStart`, `RefereeLeaves`) es lo más parecido y es **estado de partido**, no de mundo *(MEDIDO)* | Mismo coste que el espacial, y comparte la pieza |
| **Tipos de evento especiales** | **Sí, con precedente explícito** | `EventType` ha crecido tres veces por legibilidad —`ShotPost`, `Cross`, `Clearance`— con el motivo escrito: *«el jugador recuerda el tiro al palo»*, *«evento explícito antes que transición invisible»*. Se añaden **al final** para no mover valores *(MEDIDO)* | **Pequeño.** La convención existe y está documentada en el propio enumerado |
| **Reacciones encadenadas** | **Sí, y acotadas** | `PublishAtDepth` con `_maxDepth` y `RecursionCuts` en el informe; `extraAction` repite tiro o entrada dentro del tick y reentra con la profundidad incrementada; matar publica `DEATH` y puede reentrar *(MEDIDO, RT-042)*. BC-B ya cerró el agujero de límite que esto abrió | **Nulo.** Es de los sistemas mejor resueltos del motor |
| **Hooks de presentación** | **A medias** | Traza + eventos con celda + `PERK_TRIGGERED` + poses + gestos de cámara + gramática N1-N4 + sonido. **Falta**: `MomentKind` de perk, y un vocabulario de gesto declarable en el dato (§2.2) | **Pequeño**, y es la mitad imprescindible del desplazamiento mínimo |

**DERIVADO — el veredicto de preparación**: de los nueve mecanismos, **seis están hechos o a un paso**, uno
(el balón) está **a un tipo de efecto de distancia con el canal de dibujo ya construido**, y **dos —espacio
y entorno, que son el mismo— exigen una abstracción que hoy no existe en ninguna forma**.

---

# 4. El espacio de diseño que la arquitectura admite — por categorías

No es un catálogo de efectos y no debe leerse como una lista de cosas que el juego vaya a tener. Son las
**fronteras** del espacio, ordenadas por lo que el efecto escribe, que es la única clasificación que
predice el coste.

Cada categoría se evalúa contra los siete ejes del encargo: **(1)** conducta visible del jugador,
**(2)** conducta visible del balón, **(3)** efecto espacial visible, **(4)** interacción física visible,
**(5)** momento espectacular breve, **(6)** reacción en cadena, **(7)** estado de partido distintivo.

## A. Perks que convierten una interacción ordinaria de fútbol en un suceso excepcional

*La entrada que no acaba, el remate que se repite, el gol que no sube, la parada imposible.*

**Escribe sobre**: el flujo de resolución. **Soportado hoy**: `extraAction`, `cancelEvent`, `injure`,
`lethal` *(MEDIDO, 11 perks)*. Ejes **1, 4, 5, 6** cubiertos; **2, 3, 7** no.

**Es la categoría más fuerte que ya existe y la más alineada con el criterio de espectáculo del encargo**,
porque el suceso *es* la jugada de fútbol: no hay que explicar por qué se está viendo, sólo por qué ocurrió
dos veces. **Frontera dura**: `extraAction` sólo sabe repetir `SHOT` y `TACKLE` *(MEDIDO, RT-032)*, porque
son las dos resoluciones que `MatchEngine` sabe rehacer. Ampliarla a pase, regate, despeje o parada es
trabajo por resolución, no una generalización gratuita.

**Frontera de balance, medida**: el lado `TACKLE` de esta familia **paga del presupuesto de violencia, que
está agotado** (§6).

## B. Perks que escriben sobre el cuerpo

*Ser lanzado, caer, levantarse tarde, no poder ser movido, arrancar de golpe.*

**Escribe sobre**: `Position`, `Velocity`, `PlayerState`, el búfer de empuje. **Soportado hoy**: derribo,
duración del derribo, inmunidad al empuje, reubicación. Ejes **1, 4, 5** cubiertos; **3** sólo en la forma
degenerada de `adjacentOpponents`; **6** vía encadenamiento; **2, 7** no.

**Es la categoría con mejor relación entre coste y espectáculo**: el canal de dibujo está entero —posiciones
y estado por fotograma— y el búfer de empuje ya resuelve el problema difícil, que es el determinismo de N
cuerpos empujándose en el mismo tick.

**Un defecto de legibilidad ya presente, y conviene anotarlo antes de que el rediseño lo copie**:
`relocate` **teletransporta** *(MEDIDO: `last_man` reubica al portador entre el balón y su portería en el
instante del `SHOT`)*. Un jugador que aparece en otro sitio entre dos fotogramas es exactamente lo que los
principios del proyecto llaman una transición invisible. La misma fantasía recorrida en ticks —el molde
`EnterState(estado, ticks)`— sería a la vez más legible y más espectacular, y **no cuesta arquitectura
nueva**. *(HIPÓTESIS de diseño; se mediría con la pantalla delante, skill `visual-review`.)*

## C. Perks que alteran el comportamiento del balón

*El balón que no va donde debería, el que pesa, el que no se deja tocar, el que sigue vivo después de algo
que debería haberlo parado.*

**Escribe sobre**: `Ball`. **Soportado hoy: NO. Cero tipos de efecto.** Ejes **2, 5** cubiertos en cuanto se
abra; **6** trivialmente (un balón que hace algo raro genera la siguiente jugada); **1, 3, 4, 7** no.

**Es el hueco más rentable del sistema, y la razón es puramente arquitectónica** *(DERIVADO de §2.1)*: la
pantalla **ya dibuja** posición, altura, vuelo y dueño del balón fotograma a fotograma. Un efecto que
escriba ahí **se ve sin escribir una línea en `/Game`**. Ninguna otra categoría tiene esa propiedad.

**Fronteras**: `Z`, `VelocityZ` y `FlightArc` son `float` con el estatus de RT-023 (posiciones); las
magnitudes que los gobiernan son enteras en milésimas, y un efecto nuevo tiene que entrar por ahí o rompe
el determinismo. Y hay una frontera de **diseño**, no técnica, que conviene decidir antes que nada: el
proyecto mide el partido con bandas —tiros, goles, cadena de pases, cuota de tercio— y un balón que
desobedece es la clase de cosa que las mueve todas a la vez.

## D. Perks que crean perturbaciones espaciales localizadas

*Un trozo de campo que se comporta distinto mientras dure.*

**Escribe sobre**: estado de mundo. **Soportado hoy: NO, y es el único caso que exige abstracción nueva.**
Ejes **3, 5, 7** cubiertos si se abre; **1, 4** indirectamente.

Lo que falta no es un tipo de efecto: es que **el partido no tiene un mapa de nada**. Haría falta, como
mínimo, una lista ordenada de áreas activas en el estado del partido, su evaluación por tick en el sitio
correcto (utilidad, movimiento o resolución, y no es lo mismo), un canal de traza nuevo para que la pantalla
la dibuje, y previsión en la alineación si puede hacer daño (regla 11).

**DERIVADO**: es la única de las seis categorías donde la respuesta honesta a *«¿encaja en el modelo
actual?»* es **no**. Y es también la que más se parece a un juego distinto: un campo con estado empieza a
competir con «fútbol con criaturas» como identidad. **No se recomienda decidirla dentro de esta auditoría**;
se recomienda que el rediseño de perks **no cierre su puerta** — concretamente, que ninguna decisión de las
tandas 1-3 de la biblia suponga que los efectos son siempre *por jugador*.

## E. Perks que cambian el estado del partido

*El partido entero se vuelve otra cosa durante un rato.*

**Escribe sobre**: fase, árbitro, reglas. **Soportado hoy: parcialmente, con un precedente fuerte.**
`MobStart` y `RefereeLeaves` son exactamente eso y son de motor; `modifyBias` mueve el criterio del árbitro
y `mob_instigator` cancela un evento para provocar la turba *(MEDIDO)*. Eje **7** cubierto; **5** a escala
larga.

**Es la categoría de más alcance por perk y la de mayor riesgo de degeneración**: cambia el partido de todo
el mundo, incluido el rival. Conviene mirarla con `game-design-review` antes que con arquitectura.

## F. Reacciones en cadena

**Escribe sobre**: el flujo de eventos. **Soportado, acotado y observable** *(MEDIDO, RT-042)*. Eje **6**
cubierto.

**Frontera de diseño, no técnica**: una cadena es legible **mientras se pueda contar en una frase**. El
motor la acota por profundidad; el diseño tiene que acotarla por **narrabilidad**, y ese límite es más
bajo que `_maxDepth`. *(HIPÓTESIS: se mediría con el histograma de profundidad alcanzada y la tasa de
`RecursionCuts`, que ya está en el informe.)*

## Las dos que el encargo insinúa y la arquitectura no admite como tales

- **Saltos poderosos**: la altura es **del balón, no del espacio** *(MEDIDO, ADR 0135: se descartó a
  propósito migrar `Vec2` a tres dimensiones)*. El duelo aéreo y el cabezazo existen, pero un jugador no
  tiene `Z`. Un «salto» sólo puede ser hoy **una pose y un alcance mayor**, no una trayectoria.
- **Explosiones con física**: no hay motor de físicas —se descartó el de Godot para el partido, con motivo
  registrado—. Una explosión sólo puede ser **N empujes en el búfer + N derribos**, que es justamente lo que
  hace `earthquake`. Eso no es una limitación grave: es la forma correcta de tenerla en este proyecto.

---

# 5. Espectáculo contra ruido: el filtro, y quién lo aplica hoy

El encargo pide que un efecto espectacular tenga relación con la fantasía, la condición de la build, la
situación de fútbol, el disparador y la decisión estratégica. **Lo notable es que la arquitectura ya obliga
a cuatro de las cinco**, y conviene decirlo porque significa que el filtro no hay que inventarlo:

| relación exigida | quién la garantiza hoy | estado |
|---|---|---|
| con la **situación de fútbol** | el `trigger` es un `EventType` del partido | **garantizada**… salvo `MATCH_START`, que es la fuga por la que se escapan 48 perks |
| con la **condición de la build** | la condición NCalc compilada, con `counter()`, `stat()`, etiquetas, vínculos y zona de salida | **garantizada** |
| con la **fantasía** | RT-035: la descripción se **genera desde el efecto**; no existe texto a mano | **garantizada por construcción** — y es una herramienta, no un impuesto: un efecto nuevo **obliga** a una plantilla, es decir, obliga a poder decir en una frase qué hace |
| con la **decisión estratégica** | `LineupPerkPreview` y sus funciones en la pantalla de Equipo; regla 11 y las cinco condiciones de la ADR 0048 | **garantizada para lo que hace daño** |
| con el **disparador visible en pantalla** | — | **NO garantizada**: §2.2, el perk no puede ser un momento |

**DERIVADO — el anti-patrón queda definido con precisión, y no hace falta juicio estético para aplicarlo**:

> Un efecto espectacular colgado de `MATCH_START`, o cuyo efecto sea una probabilidad, **es ruido por
> construcción**: no hay situación que lo explique ni acto que lo muestre. Y a la inversa: un efecto
> colgado de un evento de fútbol, condicionado a algo que el jugador construyó, que escribe sobre un cuerpo
> o sobre el balón, y que la pantalla puede nombrar, **cumple los cinco criterios sin que nadie tenga que
> acordarse de ellos.**

**El ideal del encargo** —*«el jugador entiende por qué acaba de pasar algo espectacular porque construyó
hacia esa posibilidad»*— se traduce en una prueba de una línea que cualquier perk futuro puede pasar o
fallar: **¿puede la pantalla decir, en el instante, quién lo hizo y por qué se cumplió?** Hoy la respuesta
es *no* para los 102, porque `PERK_TRIGGERED` se tira. Ése es el arreglo más barato del documento.

---

# 6. El sesgo, dicho explícitamente, y el presupuesto que lo condiciona

**¿Está la arquitectura enfocada en multiplicadores de probabilidad? No. ¿Lo está el catálogo? Sí,
abrumadoramente, y por dos causas estructurales, no por accidente** *(DERIVADO)*:

1. **El vocabulario de efectos tiene 19 tipos y su centro de gravedad está en el jugador**: 0 escriben el
   balón, 0 escriben el espacio. Lo que no se puede nombrar no se diseña.
2. **El efecto barato y el efecto invisible son el mismo efecto.** `modifyProbability` es una suma entera,
   no necesita orden, no necesita traza, no necesita plantilla nueva, no toca la pantalla y no mueve ninguna
   banda de forma sorprendente. Compite con un derribo en área que sí toca todo eso. **El catálogo es el
   resultado esperado de ese gradiente**, y ningún rediseño que no lo cambie va a producir otro resultado.

**Y hay un presupuesto medido que decide qué clase de espectáculo cabe** *(MEDIDO, ADR 0147/0148 y
`docs/project-state.md`)*: el balón parado posicional dejó el partido un 68 % más violento —faltas 4,73 →
7,96, entradas sin balón 2,45 → 4,62— y la ADR 0148 recuperó un tercio de la brecha; `injuriesPerMatch`
está en **0,78-0,79 contra un techo de 0,90** y el resto de la subida **sigue sin explicación medida**.

**DERIVADO, y es una guía de diseño concreta que sale de un número, no de un gusto**: el espectáculo que se
route por **más contacto** (entradas, cargas, derribos, lesiones) compite por un presupuesto casi agotado y
además tensiona la regla 11 y las cinco condiciones de la ADR 0048. El que se route por **el balón, el
espacio y el movimiento** no gasta de esa banda. **Es un argumento más a favor de abrir el canal del balón
antes que cualquier otro**, independiente del argumento de coste de §4.C — dos razones distintas apuntando
al mismo sitio.

## El riesgo que el encargo nombra, cuantificado

La hoja de ruta de `perks-design-bible.md` §6 tiene 16 capacidades (C1-C16). *(MEDIDO, leyendo la tabla)*:
once van a **intención, geometría, objetivo, reloj y contadores**; C9 es presentación; C10 y C16 son
validación y texto; C12 son vínculos; C13 y C14 están **recomendadas que no**. **Ninguna abre el balón.
Ninguna abre el espacio. Ninguna añade un `MomentKind` de perk.**

**DERIVADO**: el rediseño, tal como está escrito, convierte 57 fantasías de *numéricas invisibles* en
*conductuales legibles* —que es un avance real y grande— **y agota su presupuesto ahí**. El riesgo no es que
el rediseño prohíba el espectáculo; es que **escriba las 57 fichas contra un vocabulario que no lo
contiene**, y que después reabrirlo obligue a reescribirlas. Ése es exactamente el «eliminar accidentalmente
el espacio expresivo» del encargo, y tiene fecha: la **tanda 2** de esa hoja de ruta, que es donde se
congela el lenguaje de intención.

---

# 7. El desplazamiento mínimo para abrir el espacio

Si la respuesta del revisor a la pregunta final es «sí, queremos ese espacio», esto es lo **mínimo**, en
orden de coste creciente. **No es una propuesta de implementación y no nombra ninguna mecánica concreta.**

**Pieza 1 — Un canal de efecto que escriba sobre el cuerpo y sobre el balón.** Una primitiva de **impulso**,
no un catálogo de fenómenos: quién lo recibe (los objetivos ya existen, incluido el de área real), en qué
dirección respecto a algo del partido (el vocabulario simbólico ya existe: `RelocationPoint` hace justo eso
para posiciones) y con qué magnitud entera. Sobre el cuerpo entra por el búfer de `BodySeparation`, que ya
resuelve el determinismo; sobre el balón entra por `Ball`, que ya tiene el estado y ya se dibuja.
**Desbloquea las categorías B y C enteras y la mitad física de A.**

**Pieza 2 — Un vocabulario cerrado de gesto, declarado en el dato y leído genéricamente por `/Game`.** Es el
patrón que el repositorio ya usa dos veces —`MomentKind` y `ContactCue`— y la **única** forma de que un perk
se vea sin que `/Game` nombre perks (regla 5) ni decida nada del partido (RT-014). Con ella: un
`MomentKind` de perk, y `PERK_TRIGGERED` deja de tirarse. **Sin esta pieza, la pieza 1 produce sucesos
anónimos**, que es la definición de ruido del propio encargo.

**Pieza 3 — sólo si se quiere la categoría D** — estado de mundo en el partido. **Es un sistema, no una
extensión**, y esta auditoría recomienda **no decidirlo ahora** y limitarse a **no cerrarle la puerta**.

**Pregunta 6 para §6.5 de la biblia**, que hoy no está y debería: *¿el lenguaje de intención de la tanda 2
va a poder expresar efectos que no sean por jugador y que no sean multiplicadores de prioridad?* Si la
respuesta es no, hay que saberlo **antes** de escribir las 57 fichas, no después.

---

# 8. Lo que no hay que hacer

- **No añadir partículas ni efectos a lo que hay.** Sería arte antes de la fase 2 (regla 10) y, peor,
  espectáculo sin causa: el problema no es que `modifyProbability` se vea poco, es que **no hay nada que
  ver**.
- **No convertir `extraAction` en el comodín del espectáculo.** Es el efecto visible más barato que existe
  y por eso es el que el catálogo va a querer usar para todo; tres perks ya lo comparten. Repetir una acción
  es una fantasía, no todas.
- **No resolver esto subiendo los números.** Un `modifyProbability` grande no se vuelve visible por ser
  grande: se vuelve **decisivo e inexplicable**, que es peor.
- **No prometer con la descripción lo que el motor no hace.** Ya hay un caso vivo (`numb` y los vínculos
  RF-104, §6.3 de la biblia). Un efecto espectacular mal descrito es el mismo error con más ruido.

---

# 9. Qué mediría antes de decidir nada

1. **Cuántos perks de una build real se activan alguna vez en un partido, y cuántos de esas activaciones
   producen algo observable.** El instrumento existe —`MatchReport.PerkActivations` y el evento
   `PERK_TRIGGERED`— y la cifra convierte «el catálogo es invisible» de recuento estático a tasa medida.
2. **Cuántos momentos N3/N4 tiene un partido hoy y cuántos huecos libres deja el director** (cola de una
   plaza, caducidad 1,5 s). Es el techo real de cuántos momentos espectaculares caben **antes** de diseñar
   ninguno: si el partido ya está lleno, el espectáculo compite con el gol.
3. **El histograma de profundidad de cadena y la tasa de `RecursionCuts`** en un lote normal: dice si las
   reacciones en cadena ya ocurren y nadie las ve, o si no ocurren nunca.
4. **El reparto de la violencia por canal** tras la ADR 0148, que sigue abierto (BJ-A): sin él no se sabe
   cuánto presupuesto queda para la mitad física de la categoría A.

---

# 10. Para el revisor

1. **La pregunta del encargo, respondida**: la arquitectura **no** es fundamentalmente demasiado
   estadística — sostiene seis de los nueve mecanismos de espectáculo y ya produjo una onda expansiva. **El
   catálogo sí lo es**: 76 % invisible, 5 perks de 102 con un acto físico visible.
2. **Dos huecos, de tamaños muy distintos**: el balón (pequeño, con el canal de dibujo ya hecho, el de mayor
   rendimiento) y el espacio (una abstracción nueva; no se recomienda decidirla ahora).
3. **El arreglo más barato del documento no es un efecto**: es dejar de tirar `PERK_TRIGGERED` y darle al
   perk una entrada en la gramática de momentos. Sin eso, ningún rediseño se nota — que es, textualmente, la
   quinta pregunta abierta de `perks-design-bible.md` §6.5.
4. **Decisión que pido antes de la tanda 2 del rediseño**: si el lenguaje de intención debe poder expresar
   efectos que no sean «por jugador» y «multiplicador de prioridad». Es la única puerta que el rediseño
   puede cerrar sin querer, y se cierra ahí.
