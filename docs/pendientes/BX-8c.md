# BX-8c — Tras blocar, el portero despeja casi siempre: el bono de peligro con el área cerrada

Último hermano de [BX-8](./BX-8.md) (§3 de la primera revisión: «el bono de peligro tras blocar no se ha tocado, el
despeje sigue en 77 %… tras una parada sí puede haber rivales cerca (rechace). **Sin decidir**»). Hermano directo de
[BX-8b](./BX-8b.md).

## Observación

Lo que tendría en pantalla el revisor: el portero atrapa el balón y lo revienta hacia el centro, y el rival vuelve a
atacar enseguida. Es la segunda mitad del síntoma de BX-8 («lanza el balón hacia los rivales»), ahora en juego.

## Hipótesis

El peligro mide sólo la distancia del balón a la portería propia. Con el portero dueño del balón en su área, el área se
**cierra** (RF-057d, ADR 0152: todos los jugadores de campo, de los dos equipos, salen andando) y además el equipo que
tiró se repliega (ADR 0178). El «rechace» que la nota de BX-8 temía es el de una parada **rechazada**; en una atrapada
el balón está en las manos.

- **H0, activación.** **CONFIRMED.** Sonda de 200 partidos: tras 346 blocajes el portero despeja 299 (86 %).
- **H1, el bono de peligro es lo que hace ganar al despeje.** **CONFIRMED.** Volcado de utilidad (RT-098): el despeje
  sale con contexto 515-550 (150 de base + ~78 de peligro × 5; presión 0, porque el radio de presión es 1 casilla y los
  rivales están saliendo), contra un pase corto de 760-830 en puntuación final que pierde por 10-80 puntos. Sin el bono,
  el despeje baja a 37 de 336 (11 %).
- **H2, el despeje está justificado por un rival cerca.** **REJECTED.** Con un rival a menos de 2 casillas, tras el
  despeje se conserva el 35 % y tras el pase corto el 53-55 %.
- **H3, conservar no es la vara: perder un pase corto junto a la portería cuesta un tiro.** **REJECTED** con el A/B de
  población entera (abajo): el tiro rival en los 10 s siguientes al blocaje baja, no sube.

**Trampa de instrumento (Regla J):** la sonda clasificaba primero por `MatchTrace.ActionAt`, y mientras el portero
sostiene el balón (`Holding`) no decide, así que la etiqueta está caducada. Contaba 253 «despejes» antes y 252 después
con resultados completamente distintos. Clasificado por el evento que emite el portero (`Clearance`/`PassAttempted`)
cuadra. El instrumento de BX-8b, comprobado igual: allí la etiqueta y el evento coinciden en 63 de 63 faltas.

## Medición (sonda desechable, 200 partidos de `TestMatches.Reference`)

Blocajes en juego, primera acción del portero según su evento:

| | Base (`main` d7995de) | Sin bono con el área cerrada |
|---|---|---|
| Despeja | 299 de 346 (86 %) | 37 de 336 (11 %) |
| Pasa | 36 (conserva 35) | 290 (conserva 201, 69 %) |
| El equipo del portero conserva | 116 (34 %) | 213 (63 %) |
| Tiro rival en los 10 s siguientes | 196 (57 %) | 126 (38 %) |

Las otras posesiones del portero **dentro** de su área (balón recogido suelto o pase atrás), con la versión final. Instrumento:
la misma sonda, que cuenta cada vez que el portero pasa a ser dueño del balón en juego sin entrar en `Holding`; la
revisión, con otro criterio, contó 112-117 posesiones de este tipo en 200 partidos:

| | Base | Cambio |
|---|---|---|
| Despeja | 101 de 415 | 18 de 410 |
| Su equipo conserva | 44 % | 50 % |
| Tiro rival en los 10 s siguientes | 30 % | 24 % |

Fuera de su área el portero casi nunca tiene el balón (1 posesión en 200 partidos).

**Primera versión, corregida tras la revisión independiente:** leía el área cerrada que el motor publica al principio
del tick. Tras blocar daba igual (mientras dura `Holding` no decide), pero en la recogida suelta y el pase atrás el
portero decide en el mismo tick y la veía abierta: 567 de 1.317 evaluaciones (43 %). La regla escrita («área cerrada»)
no era la que se ejecutaba («tras blocar»). En esa versión la sonda llegó a decir que las otras posesiones no
cambiaban, y era el desfase.

**El tiro rival en 10 s depende del instrumento:** con otra sonda (eventos `Shot` en 150 ticks) la revisión obtuvo 221
de 346 → 151 de 336 tras blocar, no 196 → 126. La dirección y el tamaño coinciden, la cifra exacta no: LIKELY.

## Arreglo (11 oct 2026)

`Utility.EvaluateClear`: el bono de peligro tampoco cuenta para el portero cuando tiene el balón en su área, que es lo
que la cierra (ADR 0152, RF-057d: nadie puede cargarle y los jugadores de campo salen). La condición pasa a
`Utility.KeeperHoldsInOwnArea`, y la usan los dos: `MatchEngine.ClosedKeeperArea` y el despeje, que la evalúa al decidir.
Sin dato nuevo.

Tests:
- `GoalKickDecisionTests.OnlyWhenNobodyCanThreatenDoesTheClearanceDropTheDangerBonus` sustituye a
  `OnlyTheDeadBallDropsTheDangerBonusOfTheClearance`. Antes fijaba lo contrario («tras blocar sigue cobrando el
  peligro»), que era la decisión pendiente. Ahora: sin bono en el saque y en la primera decisión tras blocar; con bono
  para un jugador de campo en su área, en juego y sin rival en el radio de presión (el control). Falla sin el cambio
  (semilla 1: 610 > 450). Lleva también la recogida suelta / pase atrás en el mismo tick, el caso del desfase.
- `TheKeepersTeamKeepsTheBallAfterACatchOftenEnough` (200 partidos, suelo 50 %): 133 de 408 (33 %) sin el cambio,
  260 de 409 (64 %) con él. Es otro instrumento que la sonda (cuenta cada entrada en `Holding` en el área).
- Muestras ampliadas, sin tocar ninguna cota:
  - `KeeperAreaTests.TheKeepersClearanceDoesNotComeBackToHim`, de 60 a 400 partidos. Con el portero sin despejar en
    su área, 150 partidos daban 52 despejes (eran 360), por debajo de su mínimo de muestra (100). Con 400: 132 despejes,
    9 vuelven, frente a la cota de 0,5 por partido.
  - `AKeeperShortPassStaysWithinShortRange`, de 60 a 200 partidos: el corto es el 2 % de los saques y 60 se quedaron sin
    ninguno.
- `MobNarrowingTests`: huellas renovadas (`main` sin el cambio pasaba las seis).

## Revisión de diseño (`game-design-review`, 11 oct)

1. **Qué ve el jugador:** el portero atrapa y revienta el balón, y el rival vuelve a atacar.
2. **Qué decide hoy:** nada; es conducta de la IA.
3. **Qué debería ver:** que una parada atrapada termine la jugada del rival: el portero la juega con un compañero si
   puede, y despeja si no. Es lo que dice la ADR 0141 §2 para la decisión del portero.
4. **Regla:** RF-057d y ADR 0152 (el área se cierra), ADR 0178 (la parada se asienta y el que tiró se repliega). No se
   crea ninguna: el arreglo lee la regla del área cerrada en vez de una posición.
5. **Sistema:** `/Sim`, `Utility.EvaluateClear` y la condición del área cerrada, compartida con `MatchEngine`. Nada en
   `/data` ni en `/Game`.
6. **Alternativas:**
   - (A) **elegida**, sin bono con el área cerrada, también en la recogida suelta y el pase atrás: RF-057d ya dice que
     ahí nadie puede cargar al portero, así que el peligro tampoco describe amenaza;
   - (A') sólo tras blocar: más estrecha, y dejaría al portero despejando un pase atrás que nadie puede disputarle;
   - (B) una marca propia de «balón atrapado» (fue el primer experimento; da lo mismo y duplica el concepto: Regla G);
   - (C) el bono de peligro multiplicado por la presión en todas partes: más general, pero cambia también el despeje en
     juego abierto de los jugadores de campo, que funciona;
   - (D) no hacer nada.
7. **Trade-off:** el portero deja de alejar el balón por sistema. Si el pase sale mal, la pérdida es más cerca de su
   portería. Medido: aun así el tiro rival en los 10 s siguientes baja de 57 % a 38 %.
8. **Estrategia:** un portero con buenos atributos de pase y defensas que se ofrezcan pasan a valer algo después de
   una parada. Las builds de presión alta encuentran al portero jugando corto: el receptor del pase es un objetivo de
   entrada. **Sin medir.**
9. **Degeneración:** que el partido pierda ocasiones. Medido: tiros −0,35 / −0,33 por partido y goles −0,10 / −0,11
   (semillas 1 y 2). Tiros 7,76-7,94, dentro de la banda 7-15 pero acercándose al suelo; BX-8 ya los había bajado
   (~−1). Lo que desaparece son sobre todo tiros rivales tras un blocaje: contraataques regalados, no ataque propio
   (LIKELY, por la sonda de la revisión: ~70 tiros menos en 200 partidos). **Disparador:** la banda de tiros es una
   puerta (`StatisticalTests`); si cruza 7, se abre ficha y se mide antes de tocar la banda (RT-057).
10. **Demostración:** los dos tests de arriba, la sonda y el lote.

## Balance (`--runs 3000`, semillas 1 y 2, base = `main` d7995de)

| | Tiros | Goles |
|---|---|---|
| s1 base → cambio | 8,29 → 7,94 | 2,14 → 2,04 |
| s2 base → cambio | 8,09 → 7,76 | 2,06 → 1,95 |

Ninguna métrica cambia de estado. Fuera de banda sólo `betterTeamWinRate_human_60_vs_human_40`, ya fuera en la base
(90,6 → 93,0 con s1; 99,2 → 99,4 con s2; [BV-C](./BV-C.md)). Suben las cuotas INFO de tiros de ángulo cerrado y desde
la línea de fondo (+1 a +2 puntos), la misma composición que dejó BX-8: menos contraataques tras el despeje regalado.

## Puertas (`Category=Gate`, semilla de la puerta)

Rojas: `betterTeamWinRate_human_60_vs_human_40` (91,57) y `coherentBuildsBeatNone_orc_violence` (57,24), las dos ya
rojas en la base. **Nueva roja: `badBuildsLoseToNone_elf_out_of_zone` = 45,34** (techo 45). Medida por semilla con
`BuildGateTests.MetricsWithSeed`, base → cambio:

| | s1 | s2 | s3 | s4 | Media |
|---|---|---|---|---|---|
| `elf_out_of_zone` | 43,33 → 48,75 | 44,17 → 42,71 | 42,71 → 46,46 | 48,96 → 45,83 | 44,79 → 45,94 |
| `orc_violence` | 58,33 → 57,29 | 56,46 → 55,00 | 54,17 → 55,00 | 62,92 → 59,38 | 57,97 → 56,67 |

Error típico de una lectura, ~2,3 puntos (480 partidos). `elf_out_of_zone`: deltas de signo cambiante, +1,15 de media
(~0,7 ET): **LIKELY ruido**. La métrica ya estaba en el filo: [CAT-J](./CAT-J.md) la midió en 8 semillas con media
45,39 y roja en 6. `orc_violence`: −1,3 (~0,8 ET), tres de cuatro a la baja, ya roja: LIKELY ruido, a vigilar con
CAT-J. No se mueve ninguna banda.

## Queda abierto

- El portero con el balón **fuera** de su área sigue cobrando el peligro. Es casi inexistente (1 en 200 partidos).
- ~~**Hermano nuevo, `EvaluatePress`**~~: cerrado sin cambio en [BX-8d](./BX-8d.md). Con el grito «¡A por él!» pone
  a un delantero en el borde del área, y la conservación del balón no cambia.
- Hermano de instrumento: `Danger`, `Pressure` y el área cerrada que usa `Move` se calculan una vez al principio del
  tick y se leen dentro de él. Ya era así antes; ahora se sabe que una decisión a mitad de tick puede ver un dato viejo.
  El despeje ya no; el desalojo del área (`Move`, `InOpenPlay`) sí, con un tick de retraso.
- La pérdida del pase corto del portero junto a su portería no tiene medición propia (la sonda sólo mide el tiro en
  10 s, en conjunto).
