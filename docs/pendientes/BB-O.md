# BB-O — Un jugador fuera del campo puede conservar el balón hasta el final y congelar el partido

**Estado:** **RESUELTA (23 sep 2026)** — causa CONFIRMED con el camino reproducido tick a tick, arreglo
verificado contra el caso real y medido inerte en HEAD

## Observación

Hallazgo del `independent-reviewer` durante la tercera revisión de BB-B (barrera geométrica de
reanudación, 16 sep 2026), midiendo en dos árboles distintos (con y sin BB-B): un jugador que sale del
campo (lesión, expulsión) **mientras conserva la posesión del balón** puede quedarse como "dueño" hasta el
final del partido. Medido entonces:

- HEAD (con la barrera de BB-B), semilla 144: **740 de 1200 fotogramas** (el 62 % del partido) con ese
  jugador fuera del campo (`onPitch=False`), estado `Passing`/`LongPass` congelado, posición fija, y
  **cero eventos entre el tick 461 y el 1200** salvo `PlayEnd lost` y `MatchEnd`.
- Base (sin BB-B), semilla 209: 530 de 1407 fotogramas (el 38 %) con el mismo patrón.
- Frecuencia: aproximadamente 1 de cada 400 partidos en ambos árboles.

## Medición (23 sep 2026, 4000 partidos)

Dos arenas, para que la muestra **no sea vacua** en salidas del campo:

- 2000 partidos de referencia (`TestMatches.Reference`, semillas 1..2000);
- 2000 partidos frágiles-contra-brutales (`TestMatches.Brutal`, semillas 1..2000), que existe precisamente
  para producir lesiones y expulsiones en masa.

| Medida | Resultado |
|---|---|
| Salidas del campo (lesión no-`playOn`, muerte, roja) | **2 994** |
| De ellas, **con el balón en los pies** el tick anterior | **907** |
| Reanudaciones sacadas, comprobadas una a una | **61 392** |
| Sacador de reanudación **fuera del campo** | **0** |
| Rachas ≥ 5 fotogramas con el **dueño del balón fuera del campo** | **0** |

La muestra no es vacua: 907 salidas del campo **con posesión** y ni un solo fantasma. Nota de método: la
posesión se lee en el fotograma **anterior** al evento, porque `LeavePitch` pone la posición en `(-1,-1)`
y leer el mismo tick miente (trampa ya medida en BB-M y BA-L).

## Hipótesis

- **H1 — el camino "salgo del campo con el balón" no suelta la posesión.** **REJECTED.** Los tres motivos
  reales de salida comprueban la posesión y aparcan el balón **antes** de retirar al jugador:
  `SendOff` (`MatchEngine.cs:2533-2536`), `ResolveInjury` (`:2608-2611`) y `Kill` (`:3385-3388`), los tres
  con `if (ReferenceEquals(_ball.Owner, victim)) ParkBall(victim.Position);`. La sustitución no llama
  nunca a `LeavePitch` (`ApplySubstitutions`, `:489-524`, exige que el que sale ya esté fuera). Esas tres
  guardias están ahí desde el commit inicial del motor (`git log -S`), así que no son un arreglo posterior
  al hallazgo. Era la hipótesis principal de este fichero y **no era**.
- **H2 — CONFIRMED, con el camino reproducido tick a tick: el sacador de una reanudación no se revalida,
  y `TakeRestart` le da la posesión sin comprobar que siga en el campo.**

  1. `BeginRestart` elige el sacador **una sola vez** (`MatchEngine.cs:2839`); `SelectTaker` (`:2932-2962`)
     sí filtra por `CanTouchBall`/`OnPitch`, pero **sólo en ese instante**.
  2. Durante la cuenta atrás (15 ticks para el córner, similar para el resto) los demás jugadores siguen
     actuando (AW-R, comentario en `:556-564`), así que el sacador puede recibir una entrada sin balón y
     acabar en `SendOff`/`ResolveInjury`. **Las guardias de H1 no saltan**, porque en ese momento el balón
     está aparcado y él no lo tiene.
  3. `_restartTaker` **nunca se vuelve a elegir ni a validar**. `ResolveRestart` (`:2992`) comprueba
     `_restartTaker.OnPitch` **sólo para recolocarlo**, y acto seguido llama a `TakeRestart` igual.
  4. `TakeRestart` (`:3043-3057`) hace `taker.Position = point` —deshaciendo el `(-1,-1)` de
     `LeavePitch`— y `SetOwner(taker)` **sin comprobar `OnPitch`**. Compárese con `TakePenalty`
     (`:3063-3068`), que **sí** valida con `CanTouchBall` y reprograma un saque de puerta si falla; esa
     guardia existe desde el commit inicial, el hermano no la tiene.
  5. `SetOwner` (`:1517`) lo deja en `Dribbling` y llama a `Decide`, que puede elegir `LongPass` →
     `EnterState(Passing, …)`. Desde el tick siguiente `UpdatePlayer` sale por `!OnPitch` (`:757`), así que
     el pase **nunca se ejecuta**: estado `Passing`/`LongPass` congelado con la posesión, exactamente el
     síntoma descrito.
  6. Nada lo rescata: `UpdateBall` (`:1111-1114`) sigue al dueño ciegamente sin mirar `OnPitch`;
     `ResolveTackle` (`:2169`) sale por `!carrier.OnPitch`, de modo que **nadie puede robárselo**; y **no
     existe ningún temporizador de inactividad** en todo `/Sim` (`CheckEndConditions`, `:3200-3231`, sólo
     mira el reloj; búsqueda de `watchdog|inactiv|TicksWithoutBall`: cero coincidencias).

  **Reproducido**, en el árbol del 16 sep, semilla 144, con la traza completa:

  | tick | qué pasa |
  |---|---|
  | 452 | el jugador 102 (defensa) recibe un `Tackle` *block* **durante el `Restart`** y sufre `Injury` *minor*: sale del campo, `OnPitch=false`, estado `Injured` |
  | 461 | `Recovery` detail `throwIn` **con actor 102** — el saque de banda se lo dan a él, ya retirado |
  | 462-1200 | `SetOwner` lo pone en `Passing` con el balón; `UpdatePlayer` lo salta por `!OnPitch`, el pase no se ejecuta nunca y no vuelve a pasar nada |

  Es exactamente la cadena predicha por la lectura del código, incluida la parte de que las guardias de H1
  no saltan: en el tick 452 el balón estaba aparcado por `BeginRestart`, así que el lesionado **no lo
  tenía** y no había nada que soltar.

- **H3 — se arregló entre el 16 y el 23 sep por otro camino.** **REJECTED como "arreglado", CONFIRMED
  como "el bug era real y reproducible".** Se midió el árbol del 16 sep (commit `3dd0b6d`) con el **mismo
  detector y las mismas semillas 1..2000**: **2 episodios en 2000 partidos** (~1/1000),
  **semilla 144, fotograma 460, 740 fotogramas de duración, jugador 9, estado `Passing`** —idéntico a lo
  que documentó el revisor— y semilla 1859, 48 fotogramas, también jugador 9 en `Passing`.

  Lo que **no** se puede concluir es que HEAD lo haya arreglado: entre el 16 y el 23 sep el motor cambió
  (ADR 0129, 0125 D1, 0134, RF-122), así que la semilla 144 **ya no genera el mismo partido**. La lectura
  correcta es: el bug existía y se reproducía; en HEAD no se observa en 4000 partidos; y **el agujero de
  `TakeRestart` sigue en el código sin guardia**. No está cerrado, está sin activar.

  Ese caso del 16 sep es, además, el **banco de pruebas del arreglo**: aplicando la guardia sobre ese
  árbol, la semilla 144 tiene que dejar de congelarse. Es la única forma de demostrar que el arreglo hace
  lo que dice, porque en HEAD no hay nada que arreglar que se pueda observar.

## Arreglo propuesto

Dos capas, y la de abajo es la que importa:

1. **Invariante, no caso particular** — al principio de `UpdateBall`: si el dueño del balón no está en el
   campo, soltarlo donde esté. Cubre **todos** los caminos, incluidos los que no se han encontrado. Es la
   red que habría evitado el síntoma sin saber su causa.
2. **La guardia específica** — `TakeRestart` copia la de `TakePenalty`: si el sacador no puede tocar el
   balón, se reprograma en vez de dárselo. Evita **crear** la situación, en vez de repararla un tick
   después.

**Criterio de aceptación**: como el caso no ocurre en 4000 partidos, el cambio tenía que ser **inerte** —
la salida de `/Balance` byte a byte idéntica al baseline— y si se movía, es que el caso sí ocurría y la
medición tenía un fallo.

**Se movió en el primer intento, y el fallo estaba en el arreglo, no en la medición.** El lote de 10 000
partidos daba **3 partidos distintos**. Se comprobaron los tres uno a uno reconstruyendo el plan de
`BatchRunner`: **ninguno de los tres tenía el síntoma** —en ningún fotograma el dueño del balón estaba
fuera del campo—, así que la divergencia no era el bug arreglándose.

Era esto: la guardia de `TakeRestart` se había escrito con `CanTouchBall`, copiando a `TakePenalty`. Pero
`CanTouchBall` excluye además `KnockedDown` y `Celebrating`, que **sí están en el campo**. Es decir, el
arreglo también le quitaba el saque a quien estuviera derribado o celebrando —bastante más frecuente que
estar retirado—, y eso es **una regla de juego distinta**, no parte de este bug.

Ceñida la condición a `!taker.OnPitch`, que es exactamente el bug y nada más, el lote de 10 000 partidos
sale **byte a byte idéntico** al baseline en `summary.csv`, `matches.csv` y `players.csv`. El arreglo es
inerte donde el caso no ocurre, que es lo que debe ser una red de seguridad.

**Queda abierto, aparte**: ¿debería un jugador **derribado o celebrando** poder sacar una reanudación? Hoy
puede. Futbolísticamente no tiene sentido y el motor ya sabe decirlo (`CanTouchBall`), pero cambiarlo mueve
3 partidos de cada 10 000 y es una decisión de diseño con su propia medición. No se coló dentro de este
arreglo a propósito.

Nota sobre una lectura que parecía evidente y no lo era: el partido del índice 1395 pasaba de 1 200 ticks
y 18 cambios de posesión a 1 529 (gol de oro) y 28, que es clavada la firma de un partido congelado que se
descongela. **No lo era**: ese partido no tenía el síntoma en ningún fotograma. Era el efecto de la
guardia demasiado ancha desviando el camino de decisión. Una divergencia que "tiene toda la pinta" de
confirmar la hipótesis no la confirma; hubo que ir a mirar los tres partidos.

**Impacto cuando se activa**: hasta 49 s de un partido de 60-90 s (RF-050) sin que pase nada observable —
exactamente lo que el principio rector prohíbe (RF-012d).

## Lo que encontró la revisión independiente (Regla E)

Cuatro cosas que cambiaron el paquete, y una que lo mejoró sin cambiarlo:

1. **El bug se reproduce en HEAD, no sólo en el árbol del 16 sep.** Forzando el único disparador
   —`RemoveFromPitch(_restartTaker, Injured)` durante la cuenta atrás— sobre la semilla 1: 326, 235 y
   **1 185 fotogramas fantasma** según el tick elegido, contra **0** con el arreglo. Es mejor evidencia
   que la del árbol viejo y, sobre todo, es del motor de hoy.
2. **El cambio que se había hecho en `ParkBall` era código muerto y su comentario explicaba mal por qué
   existía**: la condición que sigue (`Dribbling`/`Passing`/`Shooting`) ya excluye a quien salió del campo,
   que está en `Injured` o `SentOff`. **Retirado del diff.**
3. **Quedaba vivo un hermano**: `Step()` seguía llamando a `WalkRestartTaker` sobre un sacador ya
   retirado, sobrescribiéndole el `(-1,-1)` que `LeavePitch` deja como marca de "fuera del campo" (337
   fotogramas en el escenario forzado). No cambiaba ningún resultado —los consumidores de `/Sim` filtran
   por `OnPitch`— pero la traza lo enseñaba y `/Game` la lee. **Arreglado en el mismo paquete**, una línea.
4. **La guardia de `TakeRestart` cierra, de paso, un segundo fallo de la misma causa**: a un expulsado le
   hacía `EnterState(Positioning)` y le borraba el `SentOff`, con lo que `ApplySubstitutions` dejaba pasar
   una sustitución que la ADR 0094 prohíbe. Es el argumento más fuerte para arreglarlo ahí y no sólo con
   la red de `UpdateBall`.
5. **Confirmó que el determinismo aguanta**: `SelectTaker` no consume aleatoriedad (bucle sobre el array
   de jugadores ordenado por id, desempate estricto por índice, RT-097), así que reelegir no puede alterar
   el orden de consumo del RNG salvo en los partidos donde el caso se dispare — y ahí la divergencia es el
   arreglo. También que `_ball.Position` no puede valer `(-1,-1)` cuando salta la guardia, porque la copia
   desde el dueño está detrás de ella.

## Lo que NO se ha conseguido, y conviene que conste

**No hay un test que dispare el caso de forma natural.** El que hay vigila el invariante, y con el motor de
hoy pasaría también sin el arreglo: es una red, no una reproducción. Se intentó forzarlo con datos, por la
hipótesis de que lo que mantiene el caso en cero es la **barrera de BB-B** (`restartClearanceCells = 2.0`,
que aparta a los rivales del punto de saque). **REJECTED por medición**: con la barrera a `0.0`, en 8 000
partidos y **123 134 reanudaciones**, el sacador es retirado **0 veces** (1 107 jugadores sí salen del
campo durante una reanudación, pero nunca el que saca). No es la barrera lo que lo protege.

Queda como hipótesis **LIKELY, sin aislar**: que lo desactivara la **ADR 0134 E**, por la que la lesión
leve ya no saca del campo. El caso del 16 sep era exactamente una `Injury` *minor* que retiraba al sacador,
y hoy esa lesión ya no retira a nadie. Si esa ADR se moviera, el camino se reabre — y por eso el arreglo se
queda aunque no se pueda observar.

## Nota de diseño: por qué reelegir y no abortar (`game-design-review` abreviado)

La revisión señaló, con razón, que *«la reanudación la saca otro»* es **una regla de juego**, no un detalle
de implementación, y que no tenía ADR. Las tres alternativas reales:

- **Reelegir sacador** (lo implementado). Es lo que hace un árbitro: si el que iba a sacar se lo llevan en
  camilla, saca un compañero. No regala la posesión ni inventa un castigo.
- **Abortar y dar saque de puerta al rival**, como hace `TakePenalty`. Coherente con el hermano, pero
  convierte una lesión propia en una **pérdida de posesión**: un castigo que el jugador no ha podido
  prever ni evitar, justo lo que RF-012d prohíbe.
- **Repetir la cuenta atrás entera.** Correcto futbolísticamente, pero añade tiempo muerto a un partido de
  60-90 s (RF-050) por un caso que ocurre una vez cada varios miles de partidos.

Se elige la primera porque es la única que no introduce una consecuencia no anunciada. **No sube a ADR**
porque no cambia ninguna regla escrita en `docs/requisitos.md`: cubre un estado que hasta ahora no estaba
definido, y lo resuelve de la forma que menos altera lo que el jugador ya entiende. Si el revisor prefiere
que converja con `TakePenalty`, es una línea — pero entonces habría que cambiar también el penalti, porque
la asimetría entre los dos hermanos seguiría ahí.

## Lo que queda abierto de este mismo arreglo

- **Si al equipo no le queda nadie que pueda sacar, la reanudación no existe.** `SelectTaker` devuelve
  null, `TakeRestart` sale sin emitir `Recovery` y el balón queda suelto, así que **lo puede recoger el
  rival**. Medido: no congela el partido (51 y 195 eventos después en los casos forzados), pero es una
  transición invisible —sin evento y sin aviso— y el proyecto prefiere las explícitas.
- **El sacador de repuesto se teletransporta al punto de saque**, porque no ha caminado hacia él
  (`WalkRestartTaker` sólo caminó al original). Medido: 0,00-1,54 casillas en los casos forzados, pero sin
  cota superior si el equipo está diezmado. Es el artefacto que BA-D quitó, reapareciendo por una puerta
  nueva y muy estrecha.
- **¿Debería sacar una reanudación un jugador derribado o celebrando?** Hoy puede, y además **sacar le
  levanta**: `TakeRestart` le hace `EnterState(Positioning)`, que le corta el derribo y la celebración.

## Verificación del arreglo

Tres pruebas, porque ninguna sola bastaba:

1. **Contra el caso real** (lo que demuestra que sirve). En el árbol del 16 sep, donde el bug sí se
   dispara: **2 episodios en 2 000 partidos → 0**. En la semilla 144, el dueño fantasma pasa de **740
   fotogramas a 1 tick**: el balón queda suelto y al tick siguiente lo recoge otro jugador con
   normalidad. El partido deja de congelarse.
2. **Inercia en HEAD** (lo que demuestra que no rompe nada). Lote de 10 000 partidos,
   `data/balance/reference.json`, semilla 1, contra baseline del mismo árbol: `summary.csv`,
   `matches.csv` y `players.csv` **byte a byte idénticos**.
3. **Invariante vigilado**: `Sim.Tests/Engine/BallOwnerOnPitchTests.cs`. Su propia documentación advierte
   de lo que **no** demuestra: en HEAD el caso no se dispara, así que ese test pasaría también sin la
   guardia. Es una red para el futuro, no la prueba del arreglo; la prueba es la 1.
4. **Las 43 puertas**: 4 rojas, y las cuatro son las de [BF-B](./BF-B.md), preexistentes. Comprobado
   corriendo las mismas clases sobre un árbol limpio de HEAD: fallan igual y con los mismos valores
   (`orc_misplaced` 45,18 y rareza 43,75, idénticos al decimal). La de doctrinas se mueve dentro de su
   propio ruido (ahorradora 9,01 → 8,97 contra una contextual de 9,13), que es justo lo que BF-B dice de
   ella: una desigualdad estricta entre dos números que empatan.

Sobre la potencia de la muestra, que estuvo a punto de llevar a una conclusión falsa: con una tasa de
1 por cada 3 333 partidos, ver **cero** casos en 2 000 tiene probabilidad **0,55**, y en 4 000, **0,30**.
El "0 de 4 000" de la sonda y el "2 de 2 000" del árbol viejo nunca se contradijeron; simplemente la sonda
no tenía potencia. La primera explicación que se escribió para esa diferencia —"las arenas de test no
llevan perks y el lote sí"— **era falsa** y se descartó al comprobar que `data/balance/reference.json`
genera los equipos igual que `TestMatches`. Antes de explicar una diferencia, comprobar que existe.

## Hermanos

- [BB-G2](./BB-G2.md) — misma categoría epistemológica: mecanismo real, sin evidencia de activación.
- [BG-C](./BG-C.md) — "tres contratos que sólo se sostenían porque nadie podía ejercerlos": mismo patrón
  de guardia que falta y no se nota.
- [BB-B](./BB-B.md) — donde se detectó, de camino. `Sim.Tests/Engine/RestartClearanceTests.cs` ya se
  blindó contra este síntoma para no heredarlo como falso positivo.
