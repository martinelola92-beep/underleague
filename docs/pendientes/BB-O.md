# BB-O — Un jugador fuera del campo puede conservar el balón hasta el final y congelar el partido

**Estado:** Abierta

## Observación

Hallazgo del `independent-reviewer` durante la tercera revisión de BB-B (barrera geométrica de
reanudación, 16 sep 2026), midiendo en dos árboles distintos (con y sin BB-B): un jugador que sale del
campo (lesión, expulsión) **mientras conserva la posesión del balón** puede quedarse como "dueño" hasta el
final del partido. Medido:

- HEAD (con la barrera de BB-B), semilla 144: **740 de 1200 fotogramas** (el 62 % del partido) con ese
  jugador fuera del campo (`onPitch=False`), estado `Passing`/`LongPass` congelado, posición fija, y
  **cero eventos entre el tick 461 y el 1200** salvo `PlayEnd lost` y `MatchEnd`.
- Base (sin BB-B), semilla 209: 530 de 1407 fotogramas (el 38 %) con el mismo patrón.
- Frecuencia: aproximadamente 1 de cada 400 partidos en ambos árboles.

**No lo causa BB-B** — ocurre igual en el árbol sin ninguno de los tres intentos de BB-B puesto—, pero la
instrumentación de las pruebas nuevas de BB-B lo destapó: una prueba que calcula "la ventana en la que el
sacador conserva el balón" recorriendo hasta que cambia el dueño puede quedarse recorriendo el resto del
partido si el dueño nunca cambia porque salió del campo. Ver `Sim.Tests/Engine/RestartClearanceTests.cs`,
que ahora corta también si `!trace.OnPitchAt(frame, taker)`, precisamente para no heredar este bug como un
falso positivo de sus propias pruebas.

## Análisis / estado actual

**Abierta, sin diagnosticar la causa raíz.** Hipótesis a comprobar (Regla A, sin descartar ninguna todavía):

- Un jugador que sale del campo mientras tiene el balón (lesión durante `Passing`/`Dribbling`/`Shooting`,
  o expulsión) no dispara el camino que suelta la posesión — en apariencia, ni `SetOwner` a otro jugador ni
  `ParkBall` se llaman al procesar la salida del campo con posesión.
- Podría compartir causa con la ADR conocida de "el balón sigue al dueño incluso si el dueño no está en
  condiciones de jugar" (buscar en `docs/decisiones/` y en el propio `MatchEngine.cs` el camino de
  lesión/expulsión de un jugador con posesión antes de escribir código).

**Impacto**: hasta 49 s de un partido de 60-90 s (RF-050) sin que pase nada observable — exactamente lo
que el principio rector de diseño prohíbe (RF-012d, "todo lo malo que pase debe haber sido previsible"; un
partido que se congela no es previsible, es un error).

## Hermanos

- [BB-B](./BB-B.md) — donde se detectó, de camino, sin ser el objeto de esa revisión.
- Posible relación con la máquina de estados de lesión/expulsión; revisar junto a `docs/simulacion.md`
  antes de tocar código.
