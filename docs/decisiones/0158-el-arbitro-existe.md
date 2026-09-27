# ADR 0158 — El árbitro existe: nombre, rasgo, memoria y criterio que pesa

Fecha: 27 sep 2026 · Estado: **aceptada**. **Decisión del revisor**: *«árbitro con memoria, pero no se ve el
árbitro ni se le da la importancia suficiente»* y *«ataca árbitro primero»* (`docs/plan-diversion.md`).
**Implementa** RF-061, RF-061b, RF-062, RF-063 y la mitad de RF-119 que faltaba (faltas no señaladas).
**Cierra** D-22 (el motor ignoraba el rasgo) y el hueco `ui.scout.refereeGap`.
**Enmienda** la lectura de «casero» de RF-061 (abajo). **No incluye** el soborno (RF-064b..e): ADR aparte.
**Requisitos:** RF-012d, RF-055b, RF-061, RF-061b, RF-062, RF-063, RF-064, RF-119, RT-014, RT-021, RT-022,
RT-030, RT-031, RT-057, RT-096

## Lo que había (Regla G)

- **Criterio**: existe en el motor (`MatchEngine._bias`, −100..100, positivo favorece al local, que en la
  run es siempre el jugador, W-15). Mueve la falta, la tarjeta y el penalti (`BiasRollShift`).
- **Pero no pesa.** Cada acción sucia lo desplaza **1-2 puntos** (`tuning.referee.biasShift*`), así que en un
  partido apenas sale de 0. RF-064 dice que con ±60 el árbitro es decisivo; nunca se llega.
- **Árbitros de la run**: `RunState.Referees` guarda 6-8 (`RunReferee`, RF-061b) con rasgo y sobornos
  recibidos, pero `DefaultRunSystems.CreateReferees` los crea **todos neutros** y con nombre
  `referee_<i>`. El motor **ignora** `RefereeSetup.Trait` (D-22).
- **Falta no señalada**: el motor ya la emite (`Foul` con detalle `unseen`, ADR 0090) y mueve el criterio;
  sólo el informe no la cuenta.
- **Turba**: el árbitro ya se va (ADR 0145, `RefereeLeaves`).

## Decisión

### 1. Árbitros con nombre, de datos

`data/referees/referees.json` (esquema propio, RT-031/RT-032): un **plantel de 12-16 árbitros**, cada uno
con `id`, nombre localizado, rasgo, una **muletilla** localizada (tono de humor, la dice el pregón) y, si es
tuerto, el **lado** que no ve (`blindSide`: `top` o `bottom`). La run toma 6-8 al empezar (RF-061b) con un
flujo derivado de la semilla de run, **no** del de partido (RT-022). Qué árbitro pita cada nodo sigue siendo
derivable del nodo, como hoy.

### 2. Los rasgos cambian el partido (tabla en `data/sim/tuning.json`, RT-096)

Cifras **provisionales, sin medir** (Regla H). Se miden en el lote de esta ADR y se ajustan en la fase de
balance, no aquí.

| rasgo | efecto |
|---|---|
| neutro | el de hoy |
| estricto | pita el 95 % de las faltas; tarjetas +50 %; mueve el criterio ×1,5 |
| permisivo | pita el 55 %; tarjetas −50 %; mueve el criterio ×0,5 |
| casero | **favorece al rival**: arranca en −20 y mueve el criterio contra ti ×1,5 (abajo) |
| tuerto | no ve **ninguna** falta en su lado ciego (media banda, anunciada) |
| cobarde | nunca saca roja (ni por doble amarilla) |
| corrupto | como neutro en el campo; abarata el soborno (ADR del soborno) |
| incorruptible | como neutro en el campo; no admite soborno (ADR del soborno) |

**Enmienda de «casero».** RF-061 dice «favorece al equipo local». En la run el jugador es **siempre** el
local (W-15), así que leído al pie de la letra sería un árbitro que ayuda al jugador siempre. Se lee como
lo que el jugador vive: en la liga juegas en campo ajeno, y el casero es **el árbitro de la casa del
rival**.

### 3. El criterio pesa

**Actualizado 27 sep 2026 (revisión independiente).** Los desplazamientos de RF-063 solo ocurren por
**acciones sucias** (una entrada o un bloqueo limpios que además lesionan no mueven nada) y **solo mientras
hay árbitro** (la turba, RF-055d, no tiene): `MatchEngine.ResolveBlock`, `ResolveInjury`,
`ShiftBiasAgainst`. Magnitudes medidas sobre `TestMatches.Brutal` con árbitro neutro (150 semillas de
calibración, `Sim.Tests.Engine.RefereeSaturationTests` lo comprueba con 60): con la primera cifra de esta
ADR (5/3/3/2/5/3/8) el |criterio| final medio llegaba a **56** y saturaba en ±100 con frecuencia — incumple
el objetivo de abajo por el lado alto—; con una cifra más baja (3/2/2/1/3/2/5) la mayoría de los partidos
**no** superaba ±30 — lo incumple por el lado bajo—. Las cifras finales son:

falta vista **4**, no vista **3**, dura **+3**, sin balón **+2**, lesión **+4**, amarilla **+3**, roja **+7**
(`tuning.referee.biasShift*`). **Siguen provisionales** (no hay medición de diversión ni de balance de
builds detrás, solo la banda de saturación): se recalibran en el lote de balance de esta ADR, no aquí.
Objetivo verificable, ahora medido: **el 66 % de los partidos supera ±30** de criterio final y el |criterio|
medio queda en **45,2**, por debajo del techo de 60.

### 4. Memoria: el árbitro se acuerda de ti

**Reescrito 27 sep 2026 (revisión independiente): la versión original mezclaba en la memoria lo que hacía el
RIVAL, el arranque hostil del propio casero y la turba** — `Grudge = FinalBias / 2` leía el criterio final
completo, así que un casero que arrancaba en −20 sin que nadie hiciera nada derivaba solo hacia −40 partido
a partido, y un partido sucio del RIVAL (que mueve el criterio A FAVOR del jugador) dejaba al árbitro con
mejor memoria del jugador sin que este hubiera hecho nada para merecerlo. La memoria tiene que ser **solo la
conducta propia**.

`MatchReport` gana `BiasShiftedAgainst[2]`: lo que el motor desplazó en contra de cada equipo por sus
PROPIAS acciones sucias, mientras hubo árbitro (nunca la turba). `RunReferee` gana **`Memory`** (el nombre
`Grudge` ya lo ocupa la represalia de la ADR 0145, `Utility.GrudgeBonus`/`GrudgeTicks`): al terminar un
partido que pitó,

```
memoria_nueva = clamp(memoria_vieja − desplazadoEnContra × memoryPercent/100
                       + (desplazadoEnContra == 0 ? cleanMatchBonus : 0),
                       ±memoryCap)
```

con las tres constantes en `tuning.referee.memory` (`memoryPercent` 50, `cleanMatchBonus` 10, `memoryCap`
40, **provisionales**). El siguiente partido con ese árbitro **empieza** en esa memoria más
`tuning.referee.memory.homerInitialBias` (−20, el arranque del casero, movido aquí desde una constante de
C# para que no sea una cifra mágica). El emparejamiento entre el árbitro que pitó y su entrada en
`RunState.Referees` es por **id** (`RefereeSetup.RefereeId` = `RunReferee.Id`), no por nombre: dos árbitros
nunca comparten nombre en el mismo idioma (`RefereeLoader` lo valida), pero el id es la identidad de verdad
y el nombre es presentación. Así la memoria es **legible y anticipable**: el ojeo dice «Bartolo se acuerda de
ti: empieza a −15». Es también lo que da sentido a portarse bien en un partido contra el árbitro con el que
vas a jugar el jefe. **Sube el esquema de guardado 5 → 6** (RT-030; cargar otra versión sigue siendo error
explícito); el campo se llama `memory` en el guardado, no `grudge`.

### 5. Informe: faltas no señaladas

`RefereeReport` gana las faltas **no señaladas** por equipo, contadas en la capa de vista desde la secuencia
de eventos (`Foul`/`unseen`). Sin cambio en el motor.

### 6. `/Game`: que se vea

- **Ojeo** (RF-012b, RF-061): ficha del árbitro con nombre, rasgo, lo que hace el rasgo (una línea compuesta
  desde la tabla, RT-035), su muletilla y la línea de memoria. Se retira `ui.scout.refereeGap`.
- **Mapa** (RF-061): el nodo de partido dice qué árbitro pita.
- **Partido** (RF-062, RF-063): el **criterio siempre visible** en el tablero y un **texto flotante** con cada
  desplazamiento. Un **árbitro en el campo**: avatar de presentación que sigue la jugada a distancia, levanta
  el brazo cuando pita y **mira hacia otro lado** en la falta no señalada, y sale corriendo al empezar la
  turba. Es presentación pura: su posición no existe en el motor, no decide nada y sólo reacciona a eventos
  (RT-014).
- **Informe**: faltas no señaladas y se retira `ui.report.refereeGap`.

## Las diez preguntas (`game-design-review`)

1. **Qué experimenta el jugador.** Un personaje con nombre que ve venir en el ojeo, que se enfada con él
   durante el partido y que se acuerda después.
2. **Qué decide.** Qué partido elegir en el mapa según quién pita; alinear a sus Brutos o no contra un
   estricto; colocar la violencia en el lado ciego del tuerto; portarse bien antes de volver a encontrarse
   con un árbitro.
3. **Qué debería decidir.** Lo mismo. El riesgo es que el rasgo sea invisible en el efecto (se mide).
4. **Regla.** RF-061..063, con la enmienda de «casero».
5. **Sistemas.** `/Sim`: `RunReferee` y la creación de árbitros (`Run/Systems/Referees/`, catálogo cargado
   con los demás sistemas de run), `RefereeFor` (criterio inicial), `MatchResolution` (memoria),
   `MatchEngine` (rasgos: tasa de pitido, tarjetas, roja, lado ciego, multiplicador de desplazamiento),
   `PostMatchView`. `/data`: `referees/`, `sim/tuning.json`. `/Game`: ojeo, mapa, tablero del partido, vista
   3D, informe.
6. **Alternativas.** (a) Sólo presentación, sin rasgos: el árbitro se ve pero no importa, que es justo la
   queja. (b) Rasgos como perks de equipo: esconde al árbitro detrás de un sistema ajeno.
7. **Trade-off.** Un árbitro con peso añade varianza a los partidos; se compensa con que **todo se anuncia**
   en el ojeo y el mapa (RF-012d).
8. **Estrategias.** Da contrapeso real a las builds de violencia (RF-064e) y un motivo para las mitigaciones
   (RF-064f) que hoy no tienen enemigo.
9. **Degeneración.** Un árbitro permisivo más una build de violencia puede disparar las lesiones; un
   estricto puede vaciar el campo de rojas. Se vigilan `injuriesPerMatch`, `redCardsPerMatch` y la tasa de
   victoria de las builds de violencia. El cobarde elimina la roja y quita un freno: se mide.
10. **Cómo se demuestra.** Tests de cada rasgo con partidos construidos (misma semilla, rasgo distinto,
    comparación de faltas pitadas o rojas); test de memoria en la run; test de carga y validación de
    `data/referees/`; determinismo (RT-024); lote de campaña con las puertas antes y después; capturas del
    ojeo y del partido.

## Revisión independiente (27 sep 2026)

La primera implementación (commit `ad2f4a4`) pasaba build, `DataValidator` y toda la suite `Category!=Gate`,
pero la revisión independiente encontró que **tests en verde no demuestran que la regla fuera la correcta**
(«DESIGN CLAIM NOT PROVEN»). Corregido en el árbol, sin subir de versión de guardado otra vez (la v6 no se
había publicado todavía):

### Lo corregido

- **La memoria mezclaba lo que hacía el rival, el arranque del casero y la turba** (§4 de arriba): reescrita
  para que sea solo `BiasShiftedAgainst[jugador]`, un contador nuevo del motor que excluye por construcción
  la turba y lo que hizo el equipo contrario.
- **Un bloqueo limpio movía el criterio como si fuera una falta no vista** (`MatchEngine.ResolveBlock`,
  rama `isFoul == false`): no lo es — sin falta no hay acción sucia (RF-063) — y ya no desplaza nada.
- **Una lesión sin falta también movía el criterio** (`ResolveInjury`): ahora solo si la entrada o el
  bloqueo que la causó fue falta (`isFoul`, que el motor ya calculaba y no usaba para esto).
- **La turba movía el criterio** pese a que RF-055d dice que no tiene árbitro: `ShiftBiasAgainst` ahora
  no hace nada con `IsMob`, y el informe (`PostMatchView`) no cuenta como «no señalada por él» ninguna falta
  posterior al evento `RefereeLeaves`.
- **El emparejamiento entre partido y memoria era por nombre**, no por identidad: dos árbitros con el mismo
  nombre (posible antes de que `RefereeLoader` empezara a exigir nombres únicos) se habrían confundido.
  Ahora es por `RefereeSetup.RefereeId` = `RunReferee.Id`.
- **`tuning.referee.whistlePercent` era un dato muerto**: nadie lo leía fuera del rasgo neutro, que ya tenía
  su propia entrada en `tuning.referee.traits.neutral.whistlePercent`. Retirado del dato, del esquema y de
  `RefereeTuning`.
- **`RunReferee.Grudge` se renombra a `Memory`**: `Grudge` ya nombra la represalia de la ADR 0145
  (`Sim.Engine.Utility.GrudgeBonus`/`GrudgeTicks`); dos conceptos con el mismo identificador en el mismo
  proyecto es exactamente lo que el glosario de identificadores existe para evitar. `RefereeCardView.Grudge`
  (la vista para `/Game`) conserva su nombre a propósito, para no romper una firma pública mientras otro
  trabajo toca `/Game` en paralelo.
- **Magnitudes de §3 recalibradas por saturación**, medido (no a ojo): con las cifras originales el
  |criterio| final medio de un partido brutal con árbitro neutro llegaba a 56, saturando en ±100 con
  frecuencia; con una tercera cifra probada de camino, la mayoría de los partidos se quedaba por debajo de
  ±30. Las cifras finales, con su medición, están en §3.
- **`data/referees/referees.json` valida nombres únicos** (es y en), higiene de datos aunque el
  emparejamiento de memoria ya no dependa de ellos.

### Anotado, sin corregir en este paquete

- Los perks que usan `modifyBias` (`diver`, `home_ref`) pierden peso relativo con las magnitudes nuevas
  —más alto el techo de saturación, más pequeño el empujón relativo de un solo perk— y **no se han vuelto a
  medir**. Cualquier trabajo de balance sobre esos perks tiene que partir de las cifras de esta ADR, no de
  las anteriores.
- **RF-064e** (contrapeso a las builds de violencia): el árbitro ya pesa, pero el contrapeso llega **antes**
  que el soborno (RF-064b..e, ADR aparte todavía sin escribir) — un jugador con oro puede anular el freno en
  cuanto esa ADR exista, y hoy no hay nada que lo compense.
- **Las puertas de partido suelto de `/Balance`** siguen construyendo el árbitro neutro de siempre: no
  ejercitan ningún rasgo. Es una decisión existente (el árbitro de datos es del bucle de run, no de un
  partido suelto), anotada aquí para que quien mida balance sepa que las puertas no ven esto.
- **El tuerto anota aunque no pite** (RF-063): en su lado ciego, la falta no señalada sigue desplazando el
  criterio como cualquier otra no señalada (`ShiftBiasAgainst` no distingue "no señalada porque no la vio"
  de "no señalada porque decidió no pitarla"). Es una lectura defendible de "el árbitro toma nota aunque no
  pite" — pero no está escrita como decisión, solo como comportamiento actual.
- **El perk «Árbitro casero» favorece al portador** (bono a quien lo lleva) **y el rasgo casero favorece al
  rival** (penaliza al jugador): mismo nombre, direcciones opuestas. No es un error — son sistemas distintos
  con la misma metáfora futbolística—, pero un jugador que lea los dos nombres seguidos puede esperar que
  «casero» signifique lo mismo las dos veces. Anotado para quien escriba el texto de ojeo de `/Game`.
