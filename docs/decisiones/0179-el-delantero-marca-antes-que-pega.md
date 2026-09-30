# 0179 — El delantero sin balón marca antes de pegar (BF-C)

Fecha: 30 sep 2026 · Estado: **aceptada** · Requisitos: RF-057, RT-096 · Ficha: [BF-C](../pendientes/BF-C.md)

## Problema

Sin balón, `Tackle` del delantero (128 × 165 % = 211) ganaba a su `MarkOpponent` (120 × 150 % = 180). La ADR 0133 lo
dejó cerrado (`tackleMarkTargetBonus.Forward = 0`) porque abrirlo lo saturaba.

## Hipótesis (sonda, 600 partidos; delantero abierto a +1)

- **Subir `PressCarrier`** (ficha, camino 1): **REJECTED bajo esta ADR.** Con 300/360/420 el delantero presiona 3,7/6,8/9,5 %
  de sus decisiones pero las entradas sin balón siguen en 3,07/3,05/3,13 por partido (3,10 sin cambio).
- **`FindSpace` no se hunda** (camino 2): **REJECTED.** Está descartada fuera de posesión por precondición
  (`HoldingTeam`), no por el multiplicador; el censo la da descartada en el 55 % de las decisiones del delantero.
- **Subir su marca** (nuevo): **CONFIRMED.** Entradas sin balón totales 3,10 → 2,66 con `MarkOpponent` 210 (las del delantero 0,57 → 0,15).

## Decisión

`base.Forward.MarkOpponent` 120 → **210** (`weights.json`). Procedencia: deja a la marca 104 puntos por encima de la
entrada, el margen del centrocampista (ADR 0133: 450 − 346). Con eso el ajuste del delantero pasa a ser un **dial
graduado** (mismo lote, sonda): +1 → 0,08 entradas por delantero-partido, +40 → 0,18, +100 → 0,5, +150 → 0,77.

**No se abre** (`Forward` sigue en 0): gastar presupuesto de lesión es decisión del revisor. El delantero ahora sostiene
su posición junto a su marca (Mark 0,4 → 17 % de sus decisiones, Retreat 28 → 22 %, Cover 22 → 12 %): «sostener la
posición arriba», `referencia-motores-futbol.md`.

## Nota de `game-design-review` (resumen de las diez preguntas)

1 hoy el delantero no pega, pero abrirlo lo saturaba; 2 que su marca sea su tarea y la entrada una escalada ocasional; 3
RF-057 (contacto de quien disputa o marca); 4 `/data`; 5 hermanos BE-A/ADR 0133 (centrocampista); 6 cualquier puesto abierto
con una alternativa peor que pegar; 7 precedente ADR 0133 (margen de 104); 8 arregla la causa; 9 riesgo: el delantero se
queda pegado a su marca arriba (Retreat 28 → 22 %) y la sangre; el puesto sigue cerrado; 10 test de margen con la tabla
real, test de tasa con el puesto abierto (0,22 entradas por partido; antes 0,57) y lote. Cifra actualizada: 33 entradas de
delantero en 150 partidos (0,22 por partido).
