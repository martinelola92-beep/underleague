# 0178 — La parada se asienta: el portero espera y el equipo que tiró se repliega (BA-J)

Fecha: 30 sep 2026 · Estado: **aceptada, cifras provisionales hasta el lote** · Requisitos: RF-057b, RT-096 · Ficha: [BA-J](../pendientes/BA-J.md)

## Problema

Tras una parada atrapada el portero soltaba el balón **siempre a los 5 ticks** (mediana, p10 y p90 = 5) con 1,8 de los
seis rivales a menos de 4 casillas y 4,9 en campo contrario (300 partidos, 664 paradas retenidas).

## Decisión

Dos datos en `tuning.save`: `holdTicks` = **15** y `retreatTicks` = **20**.

- **Pausa:** el portero que atrapa entra en el estado propio **`Holding`** (no decide ni se mueve; si pierde el balón se
  corta) durante `holdTicks`. No usa `Dribbling`: sostener no es conducir, y la traza lo distingue. Vale con el portero
  fuera del área y en un penalti (es lo que pasa al atrapar, no dónde); test `CatchingPutsTheKeeperInHoldingWhereverHeIs`.
- **Repliegue:** durante `retreatTicks` el equipo que tiró baja su casilla-hogar a donde la pondría
  `mentalityShift.Defensive` (−1 defensas, −2 medios). **No baja al delantero** —nota del revisor: «el delantero puede
  quedarse presionando, pero el resto debería replegar»— **ni pisa una orden ofensiva** (o el grito «¡Arriba!»), y una orden
  ya defensiva no baja dos veces. No toca `_context.Order`: la vista de gritos la reconstruye de los eventos y un
  repliegue por la orden la desmentía (`ShoutTests`/`MobTests` lo detectaron en el primer intento).

Procedencia (Regla H): 15 es el menor valor con el que, al soltar, los rivales a menos de 4 casillas bajan a menos de 1 por
partido. Medido con la versión final (100 partidos de referencia, `AfterTheSaveTests`): **sin pausa** suelta a los 5 ticks
con 1,95 rivales a < 4 casillas y 5,07 de 6 en campo contrario; **con la pausa** suelta a los ≥ 20 ticks con 0,76 y 3,28.

## Lo que ya no se sostiene

La tabla de la primera versión (3.000 partidos, pausa y repliegue por separado) se midió con un repliegue que bajaba
también al delantero y con el estado de regate: **no se reproduce** con la versión final y se retira. Lo que sí está medido
con el código final es el lote de referencia de todo el paquete (semilla 1, 10.000 partidos): entradas 7,90 → 7,12,
lesiones 0,70 → 0,66, tiros 8,78 → 9,26, goles 2,35 → 2,47, todo en banda; **no separa qué parte es de la pausa**. La
atribución (la pausa mueve el balance, el repliegue casi nada) es LIKELY. **Baja la sangre**: vigilar la puerta de la ADR 0168.
El síntoma «se reanuda en caliente» es visible, no de posesión (el receptor pierde el balón en 45 ticks el 77 % antes y el
76-78 % después; los saques de puerta, 73 %).

## Nota de `game-design-review` (resumen de las diez preguntas)

1 el portero ve pasar el balón sin pausa entre la parada y el saque; 2 sostenerlo y que el rival se retire; 3 posesión del
portero (ADR 0152: área cerrada); 4 `/Sim` + dos datos; 5 hermano BN-A; 6 lo mismo pasa tras cualquier posesión del
portero (saque de puerta ya tiene pausa); 7 precedente `Shielding`/`Dribbling` con contador y `mentalityShift`; 8 arregla
la decisión en caliente, no un número; 9 baja entradas y lesiones (sangre) y sube goles: medido en el lote; 10 test de
liberación ≥ hold con control, del repliegue sin tocar orden/delantero, y lote. Lectura del texto (LIKELY): «equipo
defensor» = el que tiró, que ahora defiende.
