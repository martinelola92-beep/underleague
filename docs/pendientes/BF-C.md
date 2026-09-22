# BF-C — El delantero pega sin balón porque no tiene nada mejor que hacer

Estado: **abierta, medida**. Es lo único que impide abrirle la entrada al marcado (ADR 0133 lo deja en 0).

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
