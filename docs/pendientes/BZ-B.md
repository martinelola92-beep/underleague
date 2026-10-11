# BZ-B — La barrera de la reanudación teletransporta a los rivales al empezar la cuenta atrás

**Estado (11 oct 2026): implementado en la rama `bzb-barrera-andando`, NO fusionado.**
- Mueve el balance de razas fuera de su banda (D-29): los no-muertos pasan del 60 % y los orcos pierden 3 puntos, sin
  mecanismo aislado (abajo).
- Además, el saque de puerta queda fuera, contra la decisión del 24 sep. Las dos cosas son **decisión del revisor**.

**Observación** (BC-A y BX-7, anotada sin ficha): `EnforceRestartClearance` coloca de golpe en el borde de la barrera
(2,0 casillas) al rival que está demasiado cerca del balón. Choca con la decisión del revisor del 24 sep 2026 (BC-A):
*«en general en todas las paradas de juego debemos dar tiempo para que los jugadores se reposicionen de manera natural,
sin teletransportes. No me importa que se alargue el tiempo de gameplay»*. Esa decisión sólo se aplicó al saque de
centro.

## Medición (11 oct 2026, sonda de 200 partidos de referencia)

Desplazamientos de más de 0,3 casillas en un fotograma durante una reanudación. El paso andando no pasa de ~0,25.

| Reanudación | Saltos (jugador × fotograma) | Máximo |
|---|---|---|
| Falta | 1580 de 185 421 | 2,08 |
| Saque de banda | 339 de 69 939 | 1,88 |
| Saque de centro | 92 de 224 603 | 1,83 |
| Saque de puerta | 32 de 216 832 | 0,88 |
| Penalti, primer fotograma | 38 de 318 | 1,18 |
| Córner | 0 | 0,23 |

**~10 saltos visibles por partido**, casi todos en faltas y saques de banda.

- **Mecanismo, CONFIRMED** (semilla 1, tick 846, fotograma a fotograma). La falta se pita con el infractor pegado a la
  víctima. En el segundo fotograma de la reanudación, el primero en que corre la barrera, el rival salta 1,47 casillas
  de golpe.
- 716 de los 1580 saltos de las faltas son de un jugador **derribado**, que se desplaza tumbado.
- El penalti tiene su propia regla (`ClearPenaltyArea`) y queda fuera de este paquete.

## Revisión de diseño (`game-design-review`)

1. **Qué ve el jugador:** en cada falta, uno o dos rivales que saltan metro y medio, a veces tumbados.
2. **Qué decide:** nada; es la presentación de una regla.
3. **Qué debería ver:** a los rivales apartarse andando antes de que se saque, como en el saque de centro desde BC-A.
4. **Regla:** RF-052 y BB-B (la barrera de 2,0 casillas, que no cambia) y RF-053 (nadie se teletransporta). La decisión
   del revisor del 24 sep sobre todas las paradas. No se crea ninguna regla.
5. **Sistema:** `/Sim`, `MatchEngine`. Durante la cuenta atrás de banda, puerta, córner y falta, el rival que está
   dentro de la barrera o quiere entrar en ella anda hasta su salida (en `Move`, como el área cerrada de la ADR 0152).
   Además, la reanudación espera a que la barrera esté limpia, con el mecanismo del saque de centro: `_restartWaitTicks`
   y un tope, con el reloj del partido parado (BC-A). Al agotarse el tope, la colocación de golpe de siempre queda de
   red de seguridad, como BA-D.
6. **Alternativas:**
   - (A) **elegida**, andar y esperar;
   - (B) andar sin esperar: con 20 ticks de cuenta atrás, un derribado no llega, y se sacaría con un rival dentro, que
     es lo que BB-B prohíbe;
   - (C) cortinilla o velo como la del gol (ADR 0151): oculta el salto, no lo quita, y el revisor pidió quitarlo;
   - (D) no hacer nada.
8. **Estrategia:** ninguna nueva. La falta táctica sigue costando lo mismo.
9. **Degeneración:** que la espera se alargue sin fin; el tope lo impide. Que un rival quede atrapado en una esquina:
   la geometría no da (ADR 0142) y no bloquea la espera.
10. **Demostración:**
    - test de que no hay saltos en la cuenta atrás de las cuatro reanudaciones;
    - el de BB-B, que nadie está dentro del alcance al sacar, sigue igual;
    - medición de la espera (cuántas reanudaciones esperan, cuánto, y cuántas llegan al tope);
    - lote de balance y puertas.

**Trade-off (punto 7):**
- reanudaciones algo más largas en ticks del motor; el reloj del partido no se mueve;
- el sacador espera a veces. Si la espera es larga, se notará en la retransmisión, y se mide.

## Arreglo (11 oct 2026)

`MatchEngine`:
- **`_clearanceWalk`**, que se fija una vez por tick: la cuenta atrás de banda, córner y falta. No el saque de puerta
  (abajo), ni el de centro (tiene su propio camino de recolocación, BC-A), ni el penalti.
- **`Move`.** Un rival de campo dentro de la barrera anda hasta su salida (`ClearanceExit`: la misma salida que la
  colocación de golpe, con el deslizamiento por las líneas de BX-10b).
  - El que está fuera y quiere entrar se queda quieto si ya está a menos de borde + 0,5 (`ClearanceHoldCells`). Si no,
    va al borde.
  - El paso que la cortaría se proyecta al borde.
  - Va antes del área cerrada, para que la regla del área mande.
- **`EnforceRestartClearance`** sólo coloca al portero durante esas cuentas atrás (RF-057b).
- **La espera.** Al acabar la cuenta atrás, si queda alguien dentro (holgura 0,1, `ClearanceRespected`), la reanudación
  espera como el saque de centro, con el mismo tope (`kickoffMaxWaitTicks`, 180) y el reloj del partido parado.
  - Al agotar el tope, la colocación de golpe hace de red de seguridad y se saca.
  - El derribado cuenta: está dentro, y en cuanto se levanta sale andando.

## Lo que se ajustó al medir

- **Holgura de la espera, de 0,01 a 0,1.** Quien llega al borde se queda a 1,94-1,99, porque la separación de cuerpos
  lo vuelve a meter unas centésimas cada tick. Con 0,01, el 1 % de las faltas y varios córners agotaban el tope. Con
  0,1, ninguna: falta de 26 ticks de mediana (antes 20), máximo 30.
- **Quedarse quieto en el borde.** Llevar al borde a quien ya estaba en él lo hacía correr a lo largo de la barrera, y
  dos que querían entrar por el mismo sitio se empujaban: una racha de baile de 16 fotogramas
  (`DancingTeammatesTests`, tope 15). Ahora no hay rachas de 10 o más.
- **La barrera antes del área cerrada.** Al revés, la salida de la barrera ponía a gente dentro del área en el saque
  de puerta (`KeeperAreaTests`: 14).
- **El saque de puerta, fuera del paquete, y pendiente de decisión del revisor.** Medido en 1800 partidos (saque largo
  conservado por el equipo del portero):

  | `main` | Sin el saque de puerta | Con el saque de puerta |
  |---|---|---|
  | 1434 de 3009 (47,7 %) | 1377 de 2957 (46,6 %, ~0,85 ET) | 1249 de 2856 (43,7 %, ~3 ET) |

  - **Ni la espera ni quedarse quieto en el borde son la causa** (revisión independiente, hipótesis a y b). Con el saque
    de puerta dentro y sin quedarse quieto: 41,6 %. Sin espera en el saque de puerta: igual que con ella, porque los
    saques de puerta no esperan nunca.
  - **CONFIRMED que salir andando en el saque de puerta cuesta unos 4 puntos de saque largo. Mecanismo sin aislar.**
  - Descartado el efecto de selección: casi todos los saques de puerta son largos (93-96 %).
  - Choca con la decisión del 24 sep («en todas las paradas»), así que **no lo decido yo**. Quedan 22 saltos en 200
    partidos, de hasta 1,29.

## Resultado (sonda, 200 partidos)

| Reanudación | Saltos de más de 0,3 (antes → después) | Duración (antes → después) |
|---|---|---|
| Falta | 1585 → 15 (máximo 2,08 → 0,98) | 20 → 26 ticks de mediana, máximo 30 |
| Saque de banda | 339 → 0 | 20 → 20 |
| Córner | 0 → 0 | 45 → 45 |
| Saque de puerta | 32 (fuera del paquete) | 45 |

- Ticks de motor por partido: 1673 → 1674. El reloj del partido no cambia.
- **Residuo sin diagnosticar:** 15 saltos en faltas, de 0,44-0,98, con el rival a 1,4-1,56 del balón y fuera del primer
  fotograma.
- **Fuera de este paquete, anotados:**
  - el saque de centro (76 saltos de hasta 1,8): su barrera va por el camino de recolocación de BC-A;
  - el primer fotograma del penalti (38, `ClearPenaltyArea`).

## Ajustes de instrumento (Regla J), ninguno de motor

- **`MatchRulesTests`:** la ventana para encontrar el saque de la falta era la cuenta atrás + 4. Ahora es la cuenta atrás
  + 15, por la espera medida (la falta más larga, 30). Con el tope entero se comía la falta siguiente.
- **`PostMatchViewRefereeTests`:** contaba las faltas no pitadas también tras la salida del árbitro (la turba), que el
  informe no cuenta a propósito (ADR 0158 §4). La semilla elegida llega ahora a la turba.
- **`RepeatTackleReachTests.TheRealCaseNoLongerFreezes`:** fijado a `run:130`, que dejó de disparar Arrollador al cambiar
  las trayectorias. Ahora busca las runs en las que dispara, hasta tres, y exige el comportamiento en todas.
- **`ForwardOffBallTests` (0,32 por partido con techo 0,30, en las semillas 1-150):** con otras 300 semillas, `main` y
  el cambio dan exactamente lo mismo (70, 0,23 por partido). **LIKELY ruido**; tras quitar el saque de puerta vuelve a
  pasar.

**Tests nuevos:** `RestartClearanceTests.NobodyJumpsDuringTheCountdownOfARestartWithClearance` (falla en `main` con 1956
saltos) y `WhenTheRestartIsTakenNoRivalIsInsideTheClearance`, la guarda de BB-B, que pasa en las dos versiones.

## Balance (`--runs 3000`, semillas 1 y 2, base = el lote de BZ-A)

- Ninguna métrica cambia de estado.
- Las más movidas cambian de signo entre semillas: `share_over5goals` (INFO) +0,23 y −0,20;
  `betterTeamWinRate_human_60_vs_human_50` −4,8 y +1,6.
- Fuera de banda sólo `betterTeamWinRate_human_60_vs_human_40`, como en la base.

## Revisión independiente (Regla E, 11 oct)

**CONFIRMED:**
- el test de saltos discrimina (1956 en `main`);
- al sacar no queda nadie dentro;
- la espera no llega nunca al tope (200 partidos de referencia y 150 brutales; la falta más larga, 29);
- la ventana de `MatchRulesTests` y el arreglo de `PostMatchViewRefereeTests` son honestos;
- el determinismo no cambia.

Corregido tras la revisión:
- **El portero seguía saltando en las faltas** (14 de los 18 saltos que quedaban, a 2,00 exactos: la colocación de
  golpe). Ahora anda también. Su destino se acota a su área (RF-057b) y la espera no le cuenta.
  `WhereTheClearanceIsWalkedNobodyIsPlacedByIt` exige cero colocaciones de golpe en banda, córner y falta (en `main`,
  1918).
- **Las cifras de los comentarios** («15, hasta 0,98» y «32, hasta 0,88») eran de versiones intermedias; corregidas.
- **El saque de puerta:** medido como arriba, con 1800 partidos y las dos hipótesis que propuso.
- **`kickoffMaxWaitTicks`** gobierna ya la espera de banda, córner y falta; su `_doc` en `tuning.json` lo dice.
- **Test de la espera** (punto 10 de la revisión de diseño): `WaitingForTheClearanceNeverReachesTheCap`.

Anotado, sin cambio:
- **El sacador se coloca de golpe en el punto en el fotograma del saque** (la red de seguridad de BA-D). Con el cambio,
  22 en 200 partidos, hasta 2,76; en el emparejamiento brutal, 103 en 150. Ningún test lo ve, porque en ese fotograma
  la reanudación ya no está pendiente. Es hermano de este, bajo la misma decisión del 24 sep; sin ficha propia.
- **Un rival lesionado dentro de la barrera** no cuenta en la espera, y la colocación de golpe ya no lo mueve. No se dio
  en 350 partidos.
- **La proyección del paso al borde** (`ClearanceExit(next)`) desliza por la línea y podría ser más que un paso junto a
  una línea. No apareció ningún salto así; sin test propio.
- **`TheRealCaseNoLongerFreezes`** barre hasta 1500 runs. Si Arrollador se vuelve más raro, se alarga en silencio.
- **Quedarse quieto en el borde** puede leerse como un poste parado sin motivo. Pide `visual-review` cuando se vea en
  `/Game`.
- **Efectos de segundo orden:** la falta dura ~6 ticks más (~1,3 s de reloj de pared por partido, con el reloj del
  partido parado), y en los ticks parados se recupera energía (el precio de la ADR 0147).

## Puertas, y por qué no se fusiona (11 oct)

Puertas (`Category=Gate`, con el portero andando):
- rojas `orc_violence` (57,40; ya roja);
- `MatchOrderTests` (esta vez el ofensivo; ya oscilaba en BX-10b);
- y una nueva, **`RaceBalanceTests.NoLaunchRaceDominatesOrUnderperformsWithoutPerks`: no-muertos 60,67 %** (banda
  40-60, D-29).

**Medido por semilla** (`RaceBalanceTests` con las semillas 1-4, cambiando sólo la constante):

| Raza | `main` | BZ-B | BZ-B + congelar la espera | BZ-B sin quedarse quieto |
|---|---|---|---|---|
| no-muertos | 58,5 / 59,0 / 59,9 / 59,9 (59,3) | 60,7 / 59,9 / 61,2 / 60,5 (60,6) | 61,6 / 61,0 / 60,1 / 61,0 (60,9) | 61,1 / 58,8 / 62,0 / 60,4 (60,6) |
| orcos | 48,5 / 48,6 / 49,4 / 48,4 (48,7) | 45,9 / 44,4 / 44,4 / 46,5 (45,3) | 44,6 / 45,3 / 45,8 / 46,5 (45,6) | 45,1 / 44,6 / 45,9 / 45,6 (45,3) |
| elfos | 41,7 de media | 43,5 | 43,4 | 43,0 |

- Los orcos bajan unos 3 puntos y los no-muertos suben 1,3 en las cuatro semillas. **CONFIRMED que es efecto de
  BZ-B, no ruido.**
- **Hipótesis REJECTED:**
  - **la falta a favor se vuelve más peligrosa y castiga al que hace faltas.** El peligro existe: en 1000 partidos de
    referencia, tiros en los 10 s tras la falta 1084 → 1168 y goles 334 → 358. Congelar a todos menos a los que salen
    de la barrera mientras se espera lo devuelve a su valor (goles 339, el mismo 9,5 % por falta). Pero el balance de
    razas no se mueve con eso (tercera columna);
  - **quedarse quieto en el borde** (cuarta columna).
- **Queda:** salir andando en sí. Las razas difieren en cuerpo, velocidad y faltas, y el orco, lento y faltoso, es quien
  más tarda en salir. Sin experimento que lo aísle.

**Lo que hay en la rama:** todo lo de arriba, sin la congelación. Suite completa en verde (2045 de 2045) y huellas
renovadas.

**La congelación queda como experimento, fuera de la rama.** Congelar durante la espera a todos menos a los que salen
devolvía la falta a su peligro de antes. Pero rompía la garantía de BB-B: en la semilla 96, un rival quedaba dentro al
sacar, a 1,05 del balón, y la ventana del sacador tenía dos fotogramas con un rival a menos de 1,3. Si se retoma, hay
que resolver eso primero.

## Decisiones del revisor

1. **¿Se acepta que la barrera andada cueste ~3 puntos a los orcos** (y saque a los no-muertos de la banda D-29), o se
   compensa (otra palanca de raza), o se busca otra forma de quitar el salto? La alternativa barata, sacar a los 20
   ticks y colocar de golpe a quien siga dentro, deja el salto en el 89 % de las faltas: sólo lo mueve del principio al
   final.
2. **El saque de puerta:** andarlo cuesta ~4 puntos de saque largo conservado (1800 partidos, ~3 ET). ¿Se anda igual
   («todas las paradas»), o se queda con el salto (22 por cada 200 partidos, hasta 1,29)?
