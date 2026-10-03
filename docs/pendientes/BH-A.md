# BH-A — El motor no tiene ninguna defensa contra el silencio

**Estado:** Abierta, de primitiva. Nace de la revisión independiente de [BB-O](./BB-O.md) (23 sep 2026),
como efecto de segundo orden: no es un síntoma observado, es la clase entera a la que pertenecía BB-O.

## El hecho

**No existe ningún temporizador de inactividad en `/Sim`.** `CheckEndConditions`
(`Sim/Engine/MatchEngine.cs`) sólo mira el reloj: ticks de reglamento, paso a gol de oro y techo del gol de
oro. `CheckForfeit` sólo cuenta jugadores en el campo. Búsqueda de `watchdog|inactiv|TicksWithoutBall` en
todo `/Sim`: **cero coincidencias**.

Consecuencia: un partido puede pasarse **49 de sus 60-90 s** (RF-050) sin que ocurra un solo evento
observable y **el motor no se entera**. Fue exactamente lo que pasó en BB-O —740 de 1 200 fotogramas con
un dueño del balón fuera del campo— y sólo se descubrió porque un `independent-reviewer` estaba mirando
otra cosa.

## Por qué es una clase y no un caso

BB-O se arregló cerrando **su** camino (el sacador retirado que recibía la posesión) y poniendo una red
para ese estado concreto (`UpdateBall`: el dueño no puede estar fuera del campo). Pero el congelamiento
puede llegar por cualquier otro estado que no progrese:

- un `FlightTicksLeft` que no baja,
- un `StateTicksLeft` que no expira,
- un balón suelto que nadie puede recoger porque todos los candidatos están filtrados,
- el siguiente que nadie ha imaginado.

La red de BB-O cubre **una** forma de congelarse. Lo que el propio documento de BB-O decía querer —*«la red
que habría evitado el síntoma sin saber su causa»*— sólo lo da un invariante sobre el **silencio**, no
sobre el estado que lo produce.

## Por qué importa de verdad

Choca de frente con el principio rector (RF-012d, regla 11 de `CLAUDE.md`): *todo lo malo que pase en un
partido debe haber sido previsible*. Un partido que se congela no es malo-pero-previsible: es un error, y
además **silencioso**, que es la peor combinación para un juego que el jugador mira sin poder intervenir.

## Opciones, sin decidir

1. **Invariante en tests, no en el motor**: una puerta que juegue N partidos y falle si alguno tiene una
   racha de más de X ticks sin ningún evento observable. **No toca `/Sim`, no consume aleatoriedad, no
   cambia ninguna tirada** — y habría cazado BB-O sola. Es la más barata con diferencia.
2. **Evento `STALLED` en el motor**: el partido detecta el silencio y lo dice. Más honesto de cara al
   jugador (`eventos explícitos > transiciones invisibles`), pero es una primitiva nueva en `/Sim` y hay
   que decidir qué hace después de decirlo — ¿reanuda?, ¿termina?—, que ya es una regla de juego.
3. **No hacer nada** y confiar en que cada camino se cierre por separado, como se hizo con BB-O.

**Recomendación provisional: la 1, ya**, y la 2 sólo si la 1 encuentra algo. La 1 es un test, no un
cambio de motor: cuesta poco y convierte una clase entera de fallos en detectable. Antes de la 2 hace falta
`architecture-review` y `game-design-review`, porque un evento nuevo que el render consume es frontera y
regla a la vez.

## Cómo se mediría el umbral

No se puede elegir X a ojo. La medición previa es barata: en un lote de referencia, la **distribución de la
racha más larga sin evento observable** por partido. El umbral sale del percentil alto de esa
distribución, no de la intuición — un balón parado legítimo (una cuenta atrás de reanudación) ya produce
rachas cortas de silencio, y el test no puede confundirlas con un congelamiento.

## Hermanos

- [BB-O](./BB-O.md) — el caso que destapó la clase, ya cerrado por su propio camino.
- [BB-G](./BB-G.md) y [BC-G](./BC-G.md) — "el balón se queda parado / suelto y nadie lo coge": la otra
  familia de partidos que no progresan, ahí por falta de velocidad del balón y no por un estado atascado.

## Barrido de detectores (3 oct 2026)

**REJECTED en la build actual** (0/2.000 partidos con ≥ 150 ticks sin evento ni balón en movimiento, ni con el dueño del balón
fuera del campo). El detector sí lo ve en las builds viejas (semillas 40@618 con 628 ticks, 224@623, 389@882). La primitiva
sigue sin existir (el motor no tiene temporizador de inactividad); esto sólo dice que hoy no se dispara. Informe: [barrido-detectores-2026-10-03](../analisis/barrido-detectores-2026-10-03.md).

## Reaparece tras la ADR 0186 y se arregla en su causa (3 oct 2026, tarde)

El barrido de la tarde, sobre `main` con las ADR 0184/0186/0188, dio **1 de 1.000**: `run:130@1025`, 280 ticks sin evento.

- **CONFIRMED** (traza y volcado RT-098, `WorstCaseProbeTests.DumpWorstCase`): en el tick 1001 el centrocampista 4 gana
  una entrada, `SetOwner` lo pasa a `Dribbling` y Arrollador (`steamroller`, `extraAction` sobre `RECOVERY tackle`)
  repite la entrada contra 2000003, que está a 1,19 casillas. `NearestReachableRival` elegía con el alcance viejo
  (decisión + 0,3 = 1,3) y `ResolveTackle`, con `escapeBeyondDecisionReach` (enmienda de la ADR 0186), resuelve con 1,0:
  el rival era blanco y «escapado» a la vez, y esa rama devuelve a quien entra a `Positioning`, **con el balón**. Un
  dueño que no decide como portador elige colocarse sobre su propia casilla (`CoverSpace`); los rivales, a 2,2 casillas
  y fuera de su zona, puntúan quedarse (`CoverSpace` 477 contra `ChaseBall` 273). 304 fotogramas así hasta el final.
- **Censo de la clase** (`OwnerOutOfCarrierStateCensus`, dueño del balón en `Positioning`/`Chasing`/`Tackling`/`Blocking`
  en juego abierto, 500 `ref` + 500 `run`): **1 episodio, el de la semilla 130**; con `escapeBeyondDecisionReach` apagado,
  0. La referencia no lleva perks, así que no puede verlo.
- **Arreglo** (código, `MatchEngine.TackleResolveReach`): un solo alcance para resolver la entrada y para que la
  repetición elija blanco. Con la regla apagada sigue siendo 1,3 en las dos (bit a bit lo de antes).
- **Tests** (`RepeatTackleReachTests`): valor conocido (rival a 1,15: con la regla no hay blanco y quien entra sigue de
  portador; control con la regla apagada: la repetición se tira) y el caso real por semilla (falla sin el arreglo:
  304 fotogramas; con él, 0 y ningún caso del detector).
- Barrido de 1.000 con el código final: **0/1.000** en las dos trazas.

La primitiva sigue sin existir (no hay temporizador de inactividad en el motor); el detector del barrido es la opción 1.
