# 0186 — La entrada llega y la falta tira a la víctima (BV-B)

Fecha: 3 oct 2026 · Estado: **aceptada** (decisión del revisor: arreglar los dos fallos de simulación de BV-B) ·
Requisitos: RF-057, RF-063, RT-096, RT-098 · Ficha: [BV-B](../pendientes/BV-B.md) · Va con la [ADR 0184](0184-una-colocacion-se-sostiene.md)

## Problema

La pista de animación (BV-B) midió dos cosas que no son de la vista:

- **(i) entradas que golpean al aire**: 11 de 28 se resolvían con los dos a más de 0,9 casillas; se decidían a ~0,7 y
  durante los `TacklingTicks` (3) el rival se alejaba;
- **(ii) en una falta pitada caía quien la cometía y la víctima seguía de pie**, y se leía como «el que entra se tira».

## Causa (i), CONFIRMED con experimento que la aísla

Leído en `MatchEngine.ExecuteAction`: `Tackling` caía en la rama por defecto (velocidad 0), así que quien entraba se
quedaba quieto mientras la víctima seguía corriendo; `ResolveTackle` aceptaba hasta `tackleDistanceMaxCells` + 0,3 =
1,3 casillas. Instrumento `Sim.Tests/Engine/TackleReachTests.cs` (100 partidos de referencia; distancia entre los dos
al empezar el tick de la resolución y en el fotograma del suceso; validado: en toda entrada ganada la víctima está
derribada en ese fotograma). Con la regla apagada: p50 0,74 casillas al empezar el tick y **324 de 1.280** entradas a
más de 0,9 (57 % en el fotograma del suceso). Con la regla: p50 0,64 y **14 de 1.269** (16 % en el fotograma del
suceso, que ya incluye el paso que da la víctima después de la resolución, en el mismo tick).

## Decisión

Dos datos en `tuning.tackle`, `false` = el motor de antes (bit a bit, lo fija
`MobNarrowingTests.WithTheNewRulesOffEveryTraceIsTheOneBeforeAdr0184`):

1. `followVictimWhileTackling` = **true**: durante `Tackling` quien entra sigue a quien la recibe a su velocidad normal,
   **hasta 0,6 casillas** (`TackleContactCells`, alcance de una pierna; por debajo de la decisión, 1,0, y por encima
   del contacto de dos cuerpos). La resolución y su alcance no cambian. Es la opción «seguir a la víctima» de
   `docs/referencia-motores-futbol.md` frente a «volver a comprobar el alcance al resolver», que convertiría en fallo
   sin contacto lo que el jugador ve como entrada; seguirle hace que llegue.
   **Por qué 0,6 y no encima:** la primera versión le seguía hasta su posición y lo metía en su cuerpo; la separación
   de cuerpos empujaba al que recibía en el mismo tick en que disparaba, y el detector de tiros sin ángulo dejó de
   cuadrar con el contador del motor (`SymptomDetectorsValidationTests`, 108 frente a 81-104). Con 0,6 cuadra.
2. `whistledFoulDownsVictim` = **true**: en una falta **pitada** cae quien la recibe, con el derribo de una entrada
   ganada (`KnockdownTicksCausedBy`, la fuerza de quien entra); si llevaba el balón, se le escapa (un derribado no lo
   lleva, BM-B). Quien la comete **sólo cae si la entrada fue dura** —la misma «entrada dura» que ya sube la tarjeta
   (rasgo `Aggressive`/`Dirty` o 15 puntos más de fuerza)—: es la plancha. La falta **no pitada** no cambia (el
   infractor se derriba: es el freno medido de AZ-E).

`/Game` ya lo presenta sin tocarlo (Regla I, leído en `MatchPitchView3D.FallFor` y `PlayerModel.ChooseFallGesture`):
quien entra y cae hace la plancha y quien sigue de pie el toque; quien recibe y cae gira hacia el golpe y cae.

Test de valor conocido, con control (regla apagada) en el mismo test: `TacklesAreResolvedAtLegReach` (< 4 % a más de 0,9
al empezar el tick; con la regla apagada, 25 %) y `TheVictimOfAWhistledFoulGoesDown` (100 partidos: 315 faltas pitadas,
víctima en el suelo en las 315 e infractor en 294 —casi todas las faltas las hacen jugadores con rasgo de entrada dura:
328 de 381 en el censo `HardFoulsCensus`—; con la regla apagada, víctima en el suelo en 26 de 317 —derribos de otra
causa— e infractor en las 317).

## Efecto medido (con la ADR 0184 dentro)

Lote 10.000 × 2 semillas, media ± error típico; «0184» es el árbol con la sostenida y sin BV-B, «0184 + 0186» el final:

| | s1 antes | s1 0184 | s1 0184+0186 | s2 antes | s2 0184 | s2 0184+0186 |
|---|---|---|---|---|---|---|
| goles | 2,479 ± 0,012 | 2,541 ± 0,013 | 2,475 ± 0,013 | 2,189 ± 0,013 | 2,151 ± 0,013 | 2,083 ± 0,012 |
| tiros | 9,30 | 9,46 | 9,31 | 8,53 | 8,61 | 8,48 |
| entradas | 7,63 ± 0,04 | 9,01 ± 0,04 | 9,28 ± 0,04 | 9,04 ± 0,04 | 11,01 ± 0,05 | 11,25 ± 0,04 |
| entradas sin balón | 3,39 | 3,63 | 4,00 | 2,01 | 2,45 | 2,62 |
| faltas | 6,26 | 6,73 | 7,09 | 3,91 | 4,53 | 4,71 |
| lesiones | 0,692 ± 0,009 | 0,759 ± 0,009 | 0,795 ± 0,009 | 0,395 ± 0,006 | 0,465 ± 0,007 | 0,468 ± 0,007 |

BV-B sola suma un 3 % de entradas, un 4-5 % de faltas y hasta un 5 % de lesiones (s1), y quita un 3 % de goles: las
entradas que antes se escapaban por alcance ahora llegan. Todo en banda. **Las lesiones no se corrigen aquí** (decisión
del revisor: se bajarán por otro sitio, p. ej. la dificultad de generar una lesión).

Runs completas (`--full-runs 240`, semillas 1-3, contextual, 720 runs por brazo): muertes por partido **0,152 ± 0,008 →
0,149 ± 0,008 (0184) → 0,159 ± 0,008 (0184+0186)**, banda 0,11-0,22: la muerte sigue siendo rara (ADR 0048); por run
1,40 → 1,31 → 1,47 ± 0,07. `runWinRate` 16,8 → 13,5 → 14,3 % de media (9,6 / 15,4 / 17,9).

Detectores (`tools/barrido-detectores.sh`, 100 partidos, `main` → final, casos por partido ± e.t., traza `ref` / `run`):
BB-K 0,79 ± 0,13 → 0,87 ± 0,12 / 0,64 → 0,73; BO-A 0,02 → 0,04 / 0,04 → 0,02; **BN-A 0,60 ± 0,08 → 0,72 ± 0,09 /
0,42 ± 0,06 → 0,56 ± 0,08**. BB-K y BO-A sin cambio distinguible del ruido. **BN-A sube en las dos trazas** (≈ 1-1,4
errores típicos en cada una): LIKELY un empeoramiento leve, sin aislar entre 0184 y 0186; queda anotado en BV-B para la
siguiente pasada. Mejoran BC-G (0,13 → 0,06) y BA-E (0,27 → 0,18).

## Nota de `game-design-review` (diez preguntas)

1 el jugador ve entradas que pegan al aire y faltas en las que se tira el que entra; 2 que la entrada llegue y que la
falta tumbe a quien la sufre; 3 RF-057 (contacto de quien disputa) y RF-063 (el árbitro pita la falta); 4 `/Sim`
(`ExecuteAction`, `ResolveFoul`) y dos datos; `/Game` ya lo dibuja por estado; 5 alternativas: comprobar el alcance de
nuevo al resolver (más entradas falladas sin contacto: lo contrario de lo que se ve), alargar el alcance (golpes aún más
lejanos); 6 el bloqueo también resuelve con margen (`BlockReachMaxCells` + 0,3) y no se toca: sin medida de que golpee al
aire; 7 más entradas efectivas = algo más de sangre, medido; 8 la entrada dura (rasgo) sigue tirando al infractor: el
jugador sucio paga con el suelo; 9 degeneración: un derribado no saca la falta (la elección de sacador ya excluye
derribados); 10 instrumento con control, tests de valor conocido, lotes y puertas.

## Puertas y lo que queda abierto

`Category=Gate` completa con el código final: ver la ADR 0184 («Puertas»). **La que es de esta ADR:**
`RaceBalanceTests.NoLaunchRaceDominatesOrUnderperformsWithoutPerks` (D-29, cada raza sin perks entre 40 y 60 % contra
la media de las otras cuatro): **`elf_none` sale por abajo**. Tres semillas, `main` → final: 41,92 → 39,17 · 42,75 →
39,65 · 41,62 → 39,62 (media 42,1 → 39,5, la misma bajada en las tres). Atribución con la semilla 1: sólo BV-B (la
sostenida a 0) da 37,70; el final sin `followVictimWhileTackling` da 40,90. **Es seguir a la víctima**: el elfo vivía
de que las entradas se le quedaran cortas al escapar, y ahora le llegan. No se toca ninguna banda ni ningún atributo de
raza. Propuesta para el revisor (sin implementar ni medir): que quien entra siga a la víctima pero la resolución vuelva a
exigir el alcance de la decisión (`tackleDistanceMaxCells`, sin el margen de 0,3) —así la velocidad sigue sirviendo para
escapar de una entrada, que es la identidad del elfo— o un dato por raza; las dos cambian una regla y necesitan su
propia medición.
