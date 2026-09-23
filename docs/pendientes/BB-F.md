# BB-F — Un jugador se lesiona y no se ve en qué posición jugaba

**Estado:** Abierta

## Observación

**Un jugador se lesiona y no se ve en qué posición jugaba**, así que no se puede elegir sustituto

## Análisis / estado actual

**Duplica y confirma BA-I.** Sube de prioridad: el revisor lo ha vuelto a encontrar jugando

## Análisis 19 sep 2026 (partida del revisor, investigación de solo lectura)

- **La ventana no enseña la posición del que sale — CONFIRMED** (`Game/Screens/MatchScreen.cs:450-489`): solo
  su nombre (`:453-460`); cada candidato es «Nombre · Posición · Estado» (`:474-485`), sin atributos ni la
  opción que elegiría `SubstitutionPolicy.Default`.
- **«Si el partido está pre-simulado, ¿qué importa a quién pongo?» — la elección SÍ importa (CONFIRMED).**
  `RunController.Substitute` re-simula el partido desde el tick 0 con la sustitución en el estado inicial.
  En 467 pares de candidatos (400 runs reales): sucesos ≤T idénticos 100 %; algún suceso posterior cambia
  96 %; el marcador 70 %; el ganador 31 %. Con el candidato de la misma posición la diferencia de goles mejora
  +0,22 de media (LIKELY: efecto real pero pequeño frente al ruido).
- Por qué parece que no importa (LIKELY): el jugador solo ve una rama; casi todo el cambio es azar (desplaza
  el flujo de RNG); la ventana no da información para anticipar nada.
- Propuesta: posición y casilla del que sale, candidato de su misma posición resaltado, atributos o delta por
  candidato, y la opción por defecto marcada. Decidir en `game-design-review` si la elección debe tener un
  efecto legible. Fallos encontrados en el mismo camino: [BC-E](./BC-E.md), [BC-F](./BC-F.md).

## Corrección 23 sep 2026: la pantalla medida no era la que juega el revisor

**La queja literal ya estaba resuelta y el análisis del 19 sep apuntaba al fichero equivocado.** `Nav.Match`
es `Scenes/Retransmision.tscn` (`Game/Ui/Nav.cs:33`), o sea `BroadcastScreen`; `MatchScreen`
(`Scenes/Partido.tscn`) es la **vista de depuración** (`Nav.MatchDebug`, `:36`). Todo el CONFIRMED de arriba
—«solo su nombre», `MatchScreen.cs:450-489`— describe la de depuración.

La bandeja viva (`BroadcastScreen.OpenDecision`, `:405-459`) **ya enseña** la posición y la casilla del que
sale y marca el candidato que elegiría `SubstitutionPolicy.Default`. Verificado en la captura
`Game/screenshots/retrans-decision.png`, que ya estaba enganchada a la secuencia: «Sale · Ghash Matabueyes
— CEN · casilla E5 · lesionado → Drog Quiebraespinas — DEF · sano · **recomendado** / Drog Pisacráneos —
DEL · sano». Llegó en `f82a0c1` (19 sep, ADR 0119/0120), **el mismo día** en que se escribió este pendiente:
la partida del revisor es anterior a esa pantalla.

Lo que sigue faltando en la bandeja viva, y es lo que implementa la **ADR 0134**: el **riesgo letal por
candidato** contra este rival (RF-012c), la opción «que se quede el hueco», la opción «que siga jugando» —la
lesión leve deja de sacar del campo, decisión del revisor— y distinguir **leve** de **grave** en el texto de
quien sale, que hoy dice «lesionado» a secas y a partir de ahora decide qué opciones hay.

**Lección de método, no anecdótica:** el análisis del 19 sep leyó código y midió 467 pares de candidatos sin
ejecutar la pantalla. Una captura de diez segundos —la skill `visual-review`, que existe para esto— habría
evitado cuatro días de pendiente mal dirigido. Vale en los dos sentidos: no solo antes de decir «se ve
bien», también antes de decir «no se ve».

**Y la medición del 19 sep sigue siendo la que justifica el paquete.** Que la elección cambie el marcador el
70 % de las veces y el ganador el 31 % es lo que convierte «faltan datos en una ventana» en un problema de
RF-012d: es una decisión con consecuencia grande tomada con información insuficiente. Lo que cambió es
**qué** información faltaba —no el puesto, que ya estaba, sino el riesgo y las otras dos respuestas—.

## Hermanos

Mismo síntoma, causas relacionadas: [BA-I](./BA-I.md), [BC-E](./BC-E.md), [BC-F](./BC-F.md), [BC-H](./BC-H.md).
