# BV-C — `elf_none` sale de la banda D-29 y dos puertas en rojo tras las ADR 0184 y 0186

**Estado:** abierto, **decisión del revisor**. Las bandas **no se mueven** (la de D-29 no tiene otra procedencia que la
propia puerta). Hermanas: [BV-A](./BV-A.md), [BV-B](./BV-B.md), BB-P (puertas que miden una sola plantilla).

## 1. `RaceBalanceTests` — `elf_none` 39,1-39,9 % (banda 40-60)

| | semilla 1 | 2 | 3 |
|---|---|---|---|
| `main` | 41,92 | 42,75 | 41,62 |
| final (0184 + 0186) | 39,08 | 39,90 | 39,90 |
| sin seguir a la víctima (semilla 1) | 40,90 | | |
| sólo 0186, sostenida a 0 (semilla 1) | 37,70 | | |

**Causa (CONFIRMED con el dato apagado):** seguir a la víctima durante la entrada (ADR 0186). **REJECTED** como arreglo:
quitar el margen de alcance (`escapeBeyondDecisionReach`, 39,6 de media) y «seguir sólo a la propia velocidad» (ya es así:
test `AFasterVictimOutrunsTheTackleAndASlowerOneIsCaught`). **Por qué** (medido): el portador al que entran casi no se
mueve durante la entrada (p50 0,04 casillas), así que no hay carrera que ganar; lo que cambia es que la entrada que antes
se quedaba corta ahora llega (+5 % de entradas resueltas), y el elfo «pierde si le tocan» (fuerza −6, sin velocidad de
raza). Opciones para el revisor, sin medir: un ajuste de raza del elfo (técnica o evasión), o que la evasión en la
entrada (`TackleWinChance`) pese más la técnica del portador.

**Actualización (3 oct 2026, ADR 0185 encendida, `accelTicks` 3 acotado al juego abierto):** `elf_none` **41,27 · 44,10 ·
42,30** (semillas 1-3), dentro de la banda; la puerta completa en verde. Mecanismo **sin aislar** (no se ha medido qué
parte del arranque lo devuelve). El enano queda alto
(58,65 · 57,15 · 58,58, techo 60). `betterTeamWinRate` de la puerta también en verde en esa pasada.

## 2. `StatisticalTests` — `betterTeamWinRate` 60-40 por encima de 90 en la semilla 1

90,96 con el código final (1.000 partidos). Ocho semillas pareadas, antes → final (ADR 0184): media 88,5 → 90,8,
diferencia **+2,4 ± 1,3**. Tres de ocho semillas ya estaban por encima de 90 antes de ningún cambio: la puerta mide una
pareja de plantillas por semilla (BB-P).

## 3. `FullRunGateTests.TheThreeDoctrinesBuyDifferently` — sólo en la semilla que fija la puerta

Ahorradora − contextual en oro sin gastar, tres semillas de 240 runs: antes +1,28 · +0,97 · +0,76; final +0,07 · −0,19 ·
−0,26. Compras por mercado, contextual − ahorradora: antes +0,05 · +0,06 · +0,08; final +0,09 · +0,04 · +0,04 (siguen
comprando distinto).
