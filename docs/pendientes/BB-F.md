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

## Hermanos

Mismo síntoma, causas relacionadas: [BA-I](./BA-I.md), [BC-E](./BC-E.md), [BC-F](./BC-F.md), [BC-H](./BC-H.md).
