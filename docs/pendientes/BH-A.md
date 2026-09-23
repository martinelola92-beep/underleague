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
