# BP-A — Los atacantes no se colocan por detrás del último defensor (fuera de juego sin árbitro)

Estado: **Rechazada** (27 sep 2026, criterio del revisor: *«si los goles bajan mucho lo rechazamos»*).
Código revertido; queda la medición.

## Propuesta

No la regla del fuera de juego, sino la colocación: en campo rival, un atacante sin balón del equipo en
posesión no tiene su destino más allá de la línea (último defensor de campo rival, o el balón si está más
adelantado; `Utility.OffsideLineColumn`). Exentos el portador y el receptor de un pase en vuelo; sólo en
juego abierto. Implementada como recorte del destino en `MatchEngine.Move`, junto al área cerrada.

## Lo que ya había

AW-Q: sólo `FindSpace` recorta sus candidatas a la línea **más `findSpaceLineMarginCells` = 1,0**. Medido
(60 partidos, `c32f75a`): con el balón en juego, 38 atacante-fotogramas de cada 100 están más de 0,3
casillas por detrás del último defensor, pero **más de 1,5 casi nunca** (83 en 60 partidos). El 86 % son
`FindSpace`: pisan la línea dentro de la holgura, no acampan.

## Medición (4.000 partidos × 2 semillas contra `c32f75a`)

| | goles | tiros | entradas | lesiones |
|---|---|---|---|---|
| s1 base | 2,236 | 8,83 | 9,03 | 0,804 |
| s1 margen 0, todas las acciones | **1,544** | **6,64** | 11,96 | **0,951** |
| s1 margen 1, todas las acciones | 2,261 | 8,92 | 8,95 | 0,797 |
| s2 base | 2,171 | 8,79 | 9,76 | 0,412 |
| s2 margen 0, todas las acciones | **1,311** | **5,86** | 13,78 | 0,555 |
| s2 margen 1, todas las acciones | 2,229 | 8,79 | 9,78 | 0,427 |

- **Margen 0: rechazada.** Goles −31 % / −40 %; `shotsPerMatch` fuera de banda (6,6 / 5,9, mínimo 7),
  `injuriesPerMatch` fuera en s1 (0,95, máximo 0,90), `possessionChanges` fuera en s2 (28,2); empates del
  24-29 % al 37-40 %; entradas +3 / +4. Sin atacantes por delante de la defensa desaparecen el pase en
  profundidad y el mano a mano, y el juego se atasca en el centro.
- **Margen 1 extendido a todas las acciones: sin efecto** medible (nada fuera de banda, goles iguales).
  Coherente con que los atacantes ya estaban casi siempre dentro de esa holgura. No se aplica: no cambia
  nada que el jugador vea.

## Si se reabre

El sitio es un recorte en `Move` (una línea por tick, `OffsideLineColumn`) y el margen es la palanca: entre
0 (hunde los goles) y 1 (no cambia nada) no se ha medido ningún punto intermedio.
