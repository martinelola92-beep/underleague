# BF-C — El delantero pega sin balón porque no tiene nada mejor que hacer

Estado: **implementada (30 sep 2026, [ADR 0179](../decisiones/0179-el-delantero-marca-antes-que-pega.md))**; el puesto sigue cerrado a propósito. Es lo único que impide abrirle la entrada al marcado (ADR 0133 lo deja en 0).

## Síntoma

Sin balón, la entrada de un delantero **ya gana a sus propias alternativas** antes de cualquier ajuste.
Con los pesos de `data/ai/weights.json` y el multiplicador táctico de `OutOfPossession`:

| puesto | `Tackle` | `MarkOpponent` | `CoverSpace` | `Retreat` | `PressCarrier` |
|---|---|---|---|---|---|
| Defensa | 420 | **600** | 588 | 368 | 247 |
| Centrocampista | 346 | **450** | 364 | 253 | 297 |
| **Delantero** | **211** | 180 | 168 | 161 | 231 |

El defensa tiene que remontar 180 puntos para decidir una entrada y el centrocampista 104. **El delantero
no tiene que remontar nada**: su mejor acción fuera de posesión, salvo `PressCarrier` cuando hay a quién
presionar, es pegarle a su marca.

**Eso no es un delantero agresivo: es un delantero sin tarea.** Y explica el «orden invertido» que la
ADR 0125 midió sin poder explicar —al abrir el rol salía FWD 2,29 y MID 2,74 contra DEF 1,64— y por qué
cualquier ajuste positivo lo satura.

## Qué cuesta hoy

Abrirlo con `Forward: -221` (el valor más suave que lo deja participando) da 0,13-0,15 entradas por
partido-jugador y sube `injuriesPerMatch` de **0,77 a 0,84** en la plantilla más expuesta, contra un techo
de 0,90. Es presupuesto de lesión —el recurso más escaso del proyecto— gastado en un comportamiento que
viene de un hueco, no de una identidad.

## Lo que hay que decidir (no es calibración)

**¿Qué hace un delantero cuando su equipo no tiene el balón?** En los motores de referencia
(`docs/referencia-motores-futbol.md`) hace dos cosas que aquí pesan poco: **presionar** la primera línea de
salida y **sostener la posición** arriba para la contra. Aquí `PressCarrier` vale 140 de base y `FindSpace`
se desploma fuera de posesión (460 → 69 tras el multiplicador táctico), así que se queda sin nada.

Caminos, sin medir ninguno:

1. **Subir `PressCarrier` del delantero** para que la presión sea su tarea sin balón. El más barato y el
   que más se parece al fútbol; hay que mirar qué le hace a `possessionChanges`.
2. **Que `FindSpace` no se hunda tanto fuera de posesión para el delantero**: sostener la posición arriba
   es una tarea real y hoy la táctica se la quita.
3. **Bajarle el `Tackle` base**: el más directo y el peor: también dejaría de disputar el balón cuando sí
   toca.

## Antes de tocar nada (Regla A)

La medida barata que discrimina ya existe: el **histograma de acción por puesto**
(`Sim.Tests/Analysis/ActionHistogramTests.cs`, instrumento de la Tanda 0). Dice qué elige de verdad un
delantero fuera de posesión y cuántas veces `PressCarrier` queda descartada por falta de objetivo, que es
la hipótesis principal y no está comprobada.

## Hermanos

`docs/pendientes/BE-A.md` (de donde sale) · ADR 0133 (lo deja en 0 y dice por qué) · ADR 0125 (midió el
orden invertido sin explicarlo) · `docs/project-state.md` (el papel abierto del centrocampista, que era el
mismo problema un puesto más atrás y se cerró con la ADR 0133)

## Arreglo (30 sep 2026, ADR 0179)

- **Hoy no pega:** con `Forward: 0` hace 0,00 entradas sin balón por partido. El defecto es el que la ficha decía
  para cuando se abra: abierto a +1 pega 0,57 por partido (0,29 por delantero-partido) porque su marca (180) pierde
  contra su entrada (211).
- **Hipótesis 1 (subir `PressCarrier`) REJECTED bajo esta ADR:** 300/360/420 no bajan las entradas sin balón (3,07/3,05/3,13
  contra 3,10). **Hipótesis 2 (`FindSpace`) REJECTED:** está descartada por precondición fuera de posesión, no por
  el multiplicador. **Hipótesis 3 (bajar `Tackle`) no probada.** **CONFIRMED:** subir `MarkOpponent`.
- **Arreglo:** `base.Forward.MarkOpponent` 120 → 210 (margen de 104 sobre la entrada, el del centrocampista).
  Abierto a +1: 0,22 entradas de delanteros por partido en el test permanente (0,15 en la sonda de 600 partidos); el ajuste es un dial (+40: 0,35; +100: 1,03; +150: 1,53 por partido).
- **Sangre:** la sonda no muestra coste con el puesto cerrado. Abrirlo es decisión del revisor.

## Barrido de detectores (3 oct 2026)

**(a) REJECTED, (b) LIKELY.** (a) Ningún delantero provoca un `Tackle offBall*` en 2.000 partidos (el puesto está cerrado,
ADR 0133; el detector da 0 también en las builds viejas, así que no discrimina). (b) La **elección** de `Block`/`Tackle` por un
delantero sin rival con balón persiste: 1,9 episodios y 11,9 ticks por partido (`ref`; pre-ADR 0179: 14,5), pero **el
instrumento no separa antes/después con claridad** (otra build vieja da 9,8), así que no se puede decir que el arreglo no
funcionara ni que sí. Peores: `run` semilla 794 tick 405 (31 ticks seguidos, delantero 6). Informe: [barrido-detectores-2026-10-03](../analisis/barrido-detectores-2026-10-03.md).
