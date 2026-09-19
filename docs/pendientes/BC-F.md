# BC-F — Al sustituir, el partido re-simulado reasigna los dorsales

**Estado:** Abierta · CONFIRMED

## Observación

Encontrado al prototipar la fase C.2 de UI (19 sep 2026): al reanudar tras una sustitución, cuatro
compañeros aparecen con otro dorsal.

## Medición

Semilla 18, club `orc_ironworks`, primer partido; lesión leve del id 4 en el tick 929; entra el id 7.
Traza del equipo propio antes y después de `RunController.Substitute`:

| jugador | antes | después |
|---|---|---|
| Drog Zampalobos (id 3) | #4 | **#5** |
| Rukh Tragaclavos (id 4, sale) | #5 | #6 |
| Drog Piedraroja (id 5) | #6 | **#7** |
| Rukh Pisacráneos (id 6) | #7 | **#8** |
| Morg Quiebraespinas (id 7, entra) | — | **#4** |

**CONFIRMED:** el dorsal (`TracePlayer.Number`) se deriva de la posición del jugador en la lista del equipo
del `MatchSetup`; la sustitución inserta al suplente y desplaza la numeración. Justo cuando el jugador
intenta entender qué ha cambiado, cambian los números de quienes no han cambiado.

## Arreglo candidato (sin implementar)

El dorsal debe ser un atributo estable del jugador (asignado al construir la plantilla o el partido y
conservado en la re-simulación), no un índice. Localizar dónde se calcula `Number` al construir la traza.

## Hermanos

[BC-E](./BC-E.md), [BA-G](./BA-G.md) (identidad visible de los jugadores).
