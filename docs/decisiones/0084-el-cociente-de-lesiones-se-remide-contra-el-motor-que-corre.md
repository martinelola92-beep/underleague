# 0084. El cociente de lesiones entre builds se remide contra el motor que corre: `buildsWinDifferently_injuries` pasa de ≥ 1,5 a ≥ 1,4

**Fecha:** 2026-09-08
**Estado:** Aceptada e implementada (decisión del revisor; `Sim.Analysis.BuildMetrics.MinInjuryRatio`)
**Modifica:** el umbral de `buildsWinDifferently_injuries` de la puerta de fase 1 (`docs/fase1-diseno.md` §8)
**Requisitos:** RT-055, RT-056, RT-057
**Relacionada:** ADR 0062 (el mismo caso en la métrica hermana de pases), ADR 0082 (la causa)

## El aviso

La puerta de fase 1 tiene una métrica fuera tras el paquete de motor de la primera partida jugada (AW-A a AW-T):

| Métrica | Medido (40 plantillas × 12 partidos, semilla 1) | Umbral |
|---|---|---|
| `buildsWinDifferently_injuries` | **1,47** | ≥ 1,50 |
| `buildsWinDifferently_passChain` | dentro | ≥ 1,11 |
| las demás | dentro | |

La métrica es un cociente de cocientes: las lesiones que la build de contacto (`orc_violence`) causa relativas a su raza sin perks, entre las que causa la build técnica (`elf_tiki_taka`) relativas a la suya. Mide que la build de contacto lesione *mucho más* que la técnica.

## Por qué se ha comprimido

La ADR 0082 midió que el motor de ahora lesiona algo más a **todo el mundo**: con el equipo recomponiendo la marca antes de cada reanudación (AW-R), los primeros compases de cada jugada tienen algún duelo físico más, y `injuriesPerMatch` subió de 0,49-0,76 a 0,55-0,88 con el roster de referencia. Ese suelo más alto lo ponen las dos builds por igual, así que el numerador y el denominador del cociente suben juntos y el cociente baja. El 1,5 se fijó en la fase 1 sobre un motor donde el contacto era casi la única fuente de lesiones y la base era más baja.

Es exactamente el caso de la ADR 0062: *"se medía contra un canal que la escala de cuotas ya no puede saturar"*. Allí el canal de pase cambió de escala y el umbral de 1,30 pasó a 1,11; aquí el canal de lesiones ha cambiado de base.

## Decisión

El umbral pasa de **≥ 1,5 a ≥ 1,4**, como constante documentada (`MinInjuryRatio`), igual que `MinPassChainRatio`.

Es un ajuste pequeño (un 2 % por debajo del umbral) y deliberadamente justo: 1,4 deja el valor medido dentro por 0,07 y sigue exigiendo que la build de contacto lesione un 40 % más que la técnica, que es lo que "ganan de forma distinta" quiere decir.

## Alternativas descartadas

- **Dejar la puerta en rojo.** La causa está medida y entendida (ADR 0082); no es una incógnita que merezca la alarma.
- **Recalibrar la build de contacto para que lesione más.** Cambiaría un perk o una build para hacer pasar una métrica cuyo número, no el comportamiento, es el que ha quedado viejo.
- **Bajar a 1,3 o menos.** Descartado: sería absorber varianza futura por adelantado, que es lo que RT-057 llama ajuste silencioso.

## Consecuencias

- `Sim.Tests.Analysis.BuildGateTests.BuildsWinDifferently` y `NoGateMetricIsOutOfRange` vuelven a verde sin tocar ninguna build ni ningún perk.
- Si un cambio futuro baja el cociente de 1,4, hay que mirar primero si ha vuelto a subir la base de lesiones de todas las builds (ADR 0082) antes que la build de contacto.
