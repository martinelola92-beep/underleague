# 0090. La falta señalada reanuda, el árbitro no lo ve todo, y la clínica cura la leve

**Fecha:** 2026-09-09
**Estado:** Aceptada e implementada (`Sim/Engine/MatchEngine.cs`, `Sim/Run/Systems/Medical/MedicalSystem.cs`, `Sim/Analysis/RunPolicy.cs`, `data/sim/tuning.json`, `data/economy/economy.json`)
**Decisión del revisor** (segunda partida, `pendientes.md` AZ-D, AZ-E, AZ-G). **Modifica RF-053** (añade el saque de falta a las reanudaciones instantáneas) y amplía la lectura de RF-094 (la clínica trata también la leve). RF-054 no se toca: la falta no es una pausa.
**Requisitos:** RF-053, RF-063, RF-091, RF-094, RF-119, RT-056, RT-057

## Lo que el revisor vio

«Veo "falta" en el log pero no cambia de posesión; un árbitro que pita falta debe parar el juego y darle el balón al
rival.» «Debemos poner un porcentaje en las faltas en el que el árbitro la pita/ignora, saca amarilla/roja,
lesiona/no lesiona.» «La clínica debe poder curar a los lesionados leves.»

## Lo que se implementa

1. **Saque de falta** (`RestartKind.FreeKick`). La falta señalada reanuda con el balón para el equipo que la
   sufre, en el punto de la falta, con cuenta atrás propia (`restart.freeKickTicks` **8**) y **barrera**:
   durante la cuenta atrás ningún rival puede estar a menos de `restart.freeKickClearanceCells` (**2,0**) del
   balón. Si la falta ya programa penalti, manda el penalti. El saque se abre **después** de resolver la
   lesión de la entrada: si el que sufrió la falta sale lesionado, no puede ser quien saque. Encaja en RF-053
   (reanudación instantánea) y no en RF-054 (pausa).
2. **`referee.whistlePercent`** (**80**): probabilidad de que el árbitro señale una falta que ha ocurrido. La no
   señalada queda en el registro (`Foul` con detalle `unseen`, RF-119), derriba igual al que entra (es la
   física de la entrada, no el castigo), no saca tarjeta, no da penalti y no reanuda, y mueve el criterio como
   acción sucia no vista (RF-063). Los rasgos de árbitro (RF-061, fase 3) modularán este número.
3. **La clínica cura la leve** a `economy.clinicMinorCost` (**4**; la grave sigue a 10): la leve solo cuesta un
   partido al −15 % (RF-091) y a igual precio casi nunca compensaría. La política automática solo la paga para
   un titular del siguiente partido, guardando el precio de una grave.

## Los porcentajes del árbitro, en una tabla (AZ-E)

Todos son datos de `data/sim/tuning.json`; el valor efectivo está medido con `--runs 2000`, semillas 1 / 7:

| Qué | Parámetro | Valor | Efectivo medido |
|---|---|---|---|
| La entrada es falta | `tackle.foulBase` 320 + `foulStrengthFactor` 5 (ADR 0041, escala ADR 0050 P2) | ~3,2 % + fuerza relativa | **5,17 / 3,67** faltas por partido sobre 10,8 / 11,8 entradas |
| El árbitro la pita | `referee.whistlePercent` | 80 % | 4,1 / 2,9 señaladas por partido |
| Amarilla | `tackle.yellowCardBase` 250 (+300 si es dura, ± criterio) | 2,5 % (5,5 % dura) | **0,27 / 0,19** por partido |
| Roja | `tackle.redCardBase` 10 (+20 si es dura, ± criterio); dos amarillas | 0,1 % (0,3 % dura) | **0,08 / 0,04** por partido |
| Penalti si es en el área | `referee.penaltyOnFoulInArea` | 80 % | 15 % de las señaladas no reanudan con saque de falta (penalti o final) |
| Lesión en la falta | `injury.onTackleBase` 140 + `onFoulBase` 60 + relativo (ADR 0041) | la falta suma 60 sobre 140 | `injuriesPerMatch` 0,91 / 0,74 |

Lo que no se mide aparte todavía: lesiones **por falta** frente a por entrada limpia. Es una fila de `MatchReport`
que no existe; se añade cuando haga falta.

## Lo que se mide

2.000 partidos, semillas 1 / 7, contra el estado de la tanda 1:

| Métrica | Tanda 1 | Tanda 2 | Banda |
|---|---|---|---|
| `possessionChanges` | 24,55 / 23,33 | 23,93 / 23,18 | 12-28 |
| `passChainAvgLength` | 2,06 / 2,19 | 2,01 / 2,13 | 2-4 |
| `shotsPerMatch` | 10,08 / 8,32 | 9,46 / 7,95 | 8-16 |
| `injuriesPerMatch` | 0,86 / 0,78 | 0,91 / 0,74 | 0,3-0,9 |
| `tacklesPerMatch` | 10,82 / 11,78 | 10,76 / 11,80 | 6-14 |
| `goalsPerMatch` | 2,85 / 2,28 | 2,56 / 2,09 | INFO |

Tres cosas se probaron y se descartaron por medición antes de llegar aquí: sin la barrera, el infractor y sus
compañeros rodeaban al que saca y las entradas subían de 10,8 a 12,5 y las lesiones a 1,11; con el saque
abierto antes de resolver la lesión, el lesionado podía ser el sacador; con 15 ticks de cuenta atrás, los ~4 s
de balón parado por partido dejaban `shotsPerMatch` en 7,8 en la semilla 7. Los dos valores que quedan al
borde (tiros 7,95 en la semilla 7 y lesiones 0,91 en la 1) partían de 8,32 y 0,86, ya en el borde tras AW-R
(ADR 0082), y la puerta estadística de referencia (semilla 1) está en verde.

## Consecuencias

- `possessionChanges` ya no sube con las faltas: el saque de falta lo hace un compañero del que la sufrió, así
  que no es un cambio de posesión; lo que cambia es que hay un segundo de balón parado.
- AZ-D, AZ-E y AZ-G cerradas. `whistlePercent` 80 y `clinicMinorCost` 4 son valores iniciales del revisor;
  la tabla de arriba es el sitio donde moverlos.
