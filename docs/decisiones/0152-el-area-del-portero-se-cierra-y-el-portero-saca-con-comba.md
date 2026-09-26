# ADR 0152 — El área del portero se cierra cuando tiene el balón, y el portero despeja con comba

Fecha: 27 sep 2026 · Estado: **aceptada**. **Decisión del revisor**, en dos tiempos. Ficha:
[BN-A](../pendientes/BN-A.md). Enmienda la [ADR 0139](0139-el-balon-aereo-el-duelo-y-la-segunda-jugada.md) sólo en el despeje del portero.

## Decisión del revisor

> *Cuando el portero atrapa un balón todos los jugadores deben alejarse de él. En el fútbol real hay un
> área y cuando el portero tiene el balón deben salir de ella.*

Y, ante el efecto de segundo orden medido (abajo):

> *Lo del área hay que hacerlo, el problema es que el portero no saca bien. Mejora su habilidad de sacar y
> empeora la habilidad de interceptar un saque de puerta. Seguramente la solución sea tan sencilla como
> que el portero le dé mucha comba al balón, lo suficiente como para que el rival más cercano a su
> portería no llegue verticalmente (como en el fútbol real).*

## Decisión

1. **El área se cierra** (`MatchEngine.ClosedKeeperArea`) cuando su portero tiene el balón dentro de ella
   —lo atrapó, lo recogió suelto o acaba de sacar de puerta— o su equipo tiene un saque de puerta
   pendiente. Mientras está cerrada, ningún jugador de campo **de ninguno de los dos equipos** tiene su
   destino dentro: `Move` lo lleva al borde más cercano y **sale andando** (RF-053). Quien va por fuera no
   corta la esquina del área. **El teletransporte de los rivales en el saque de puerta
   (`ClearAreaOfOpponents`, BI-F) se retira**: la regla nueva ya los saca andando y al sacar no queda nadie
   dentro (medido).
2. **El despeje del portero lleva comba propia** (`tuning.clear.keeperPeakHeightCellsMilli` = **3500**). El
   de los jugadores de campo no cambia (1400), ni la distancia de nadie.
   - **Procedencia del 3500** (regla H): el vuelo sube como `4·A·t·(1−t)` y un salto llega hasta
     `ball.aerialReachHeightCells` = 1,7. Con A = 3,5 el balón queda por encima de 1,7 hasta t ≈ 0,85: sólo
     el último 15 % del vuelo está al alcance, así que quien esté debajo de la trayectoria no llega. Con
     1400 —la comba del centro, ADR 0136— el balón estaba al alcance **en toda la bajada**.
   - **Se probó también +3 casillas de distancia y se quitó**: no hacía falta (la comba sola deja 4 vueltas
     de 557 despejes) y era lo que movía el balance (−17 % de goles en 150 partidos). El revisor pidió comba.

## Por qué el segundo punto

Cerrar el área quitó el amontonamiento (2,4 compañeros a menos de una casilla del portero en cada saque
de puerta → 0) pero destapó un bucle que ya existía: el portero despeja, **un rival solo lo cabecea de
vuelta** sin ningún evento (ADR 0139: quien gana un balón alto lo cabecea hacia donde ataca, aunque no
se lo dispute nadie) y el portero lo vuelve a atrapar. 361 vueltas en 150 partidos antes; **548** con el
área cerrada, porque los compañeros ya no estaban cerca para disputar.

Se probó y **se descartó** que el jugador solo **controle** el balón en vez de cabecearlo: quitaba el
bucle (548 → 3), pero el despeje pasaba a caer en un rival libre delante del área y los goles subían
**+34 %** (2,21 → 2,97 y 2,05 → 2,74), con `possessionChanges` fuera de banda (30,3 / 30,6, máximo 28). La
causa de fondo no era el cabezazo, era **a dónde sacaba el portero**, que es lo que corrigió el revisor.

## Medición

**El bucle**, con un contador estructural —el siguiente dueño del balón tras el despeje del portero, tarde
lo que tarde—, validado contra el caso conocido (150 partidos):

| despeje del portero | le vuelve a él | a un compañero | a un rival | goles |
|---|---|---|---|---|
| comba de campo (1,4) | 551 | 87 | 487 | 274 |
| **comba del portero (3,5)** | **4** | **139** | 385 | 262 |
| comba 3,5 y +3 casillas (descartado) | 0 | 96 | 389 | 227 |

*(Una primera versión de esta ADR daba «548 → 0» con un contador de 40 ticks desde el despeje; el despeje
nuevo tarda 36-44 ticks sólo en volar, así que aquel contador no podía ver el bucle. Lo cazó la revisión
independiente.)*

**Balance**, 4.000 partidos × 2 semillas contra `c2ee03c`:

| | goles | tiros | cambios de posesión | entradas | lesiones | tercios (propio / centro / rival) |
|---|---|---|---|---|---|---|
| s1 base | 2,211 | 8,63 | 24,7 | 7,53 | 0,731 | 26,1 / 40,0 / 33,9 |
| s1 ADR 0152 | 2,234 | 8,72 | 25,8 | 8,14 | 0,788 | 24,2 / 44,5 / 31,4 |
| s2 base | 2,053 | 8,30 | 25,4 | 7,96 | 0,373 | 26,9 / 40,7 / 32,4 |
| s2 ADR 0152 | 2,098 | 8,43 | 26,3 | 8,52 | 0,395 | 25,4 / 44,3 / 30,3 |

- **Ninguna métrica sale de banda ni cambia de estado** (la única fuera, `betterTeamWinRate_human_60_vs_human_40`, ya lo estaba).
- **Goles y tiros iguales** dentro del ruido. **Más juego en el centro** (`ballThirdMaxShare` 40 → 44,
  banda ≤ 52) y **más entradas (+0,6) y lesiones (+6-8 %)**: el despeje del portero se pelea más lejos de
  su área. `betterTeamWinRate_human_60_vs_human_50` baja de 65,9 a 61,6 en s1 y de 94,0 a 92,2 en s2
  (métrica informativa, dos semillas).
- **La presión al portero con el balón queda cerrada por construcción** (ya casi no ocurría: 1 → 3
  entradas en 300 partidos). **Enmienda RF-057d** («el portero puede recibir cargas en su área») sólo
  mientras tiene el balón dentro del área, que es lo que el fútbol prohíbe; RF-057d sigue valiendo en
  cualquier otro momento.

**Puertas (`Category=Gate`): 5 rojas de 43, una más que antes del paquete.** Las cuatro de siempre (curva
de jefes, `orc_violence` ×2, `undead_none` 60,75 %) y dos métricas nuevas fuera, atribuibles a este
paquete porque es lo único que cambió desde la última ejecución (ADR 0151): **`badBuildsLoseToNone`**
`orc_misplaced` 46,43 y `elf_brawler` 45,21 (máximo 45) y **`elf_none` 36,80 %** (antes 41,80; mínimo 40).
**Sin descomponer** entre el área cerrada y la comba, y **sin compensar**: el revisor aplazó el balance
fino (*«ya haremos tweaks una vez tengamos el gameplay cerrado»*). Queda aquí para esa fase (RT-057).

## Lo que queda abierto (BN-A)

- En el saque de puerta la defensa espera **en fila en el borde del área**: es su posición de formación
  (LIKELY). Dar la posesión de la reanudación al que saca no lo cambia (REJECTED); en **todas** las
  reanudaciones cambia el carácter de córners y bandas (medido, no aplicado).
- **La comba se aplica a todo despeje del portero**, también en juego abierto y si ha salido del área
  (ADR 0141). El revisor habló de «sacar»; no se ha decidido distinguirlo. El saque de puerta que el
  portero resuelve con **pase** (no despeje) no cambia.
- **Saque de puerta sin portero** (expulsado o muerto): saca un jugador de campo con la comba de campo, y
  el bucle puede volver ahí. Raro, sin medir.
- `_closedArea` se calcula al principio del tick: si el portero atrapa a mitad de tick, el resto se mueve
  ese tick con el área abierta (tolerado, medido con `KeeperAreaTests`).
- El cabezazo sin disputa sigue sin evento (ADR 0139); ya no produce el bucle con el despeje del portero,
  pero existe en el resto del juego.
- En la retransmisión, un balón a 3,5 casillas se pinta a la altura de las vallas publicitarias: se ve y
  la sombra lo sitúa, pero puede leerse como si fuera a la grada. Sin decidir.
