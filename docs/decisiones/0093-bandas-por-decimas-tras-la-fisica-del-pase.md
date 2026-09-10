# 0093. Bandas por décimas tras la física del pase

**Fecha:** 2026-09-10
**Estado:** Aceptada e implementada (`Sim/Analysis/MatchMetrics.cs`, `data/bosses/grimhold_guns.json`, `Sim.Tests/Analysis/BossGateTests.cs`)
**Decisión del revisor** («ADR de bandas por décimas y push»). **Modifica tres bandas** (ADR 0054, ADR 0081/0082, ADR 0083) y **añade una tolerancia a la escalera de la ADR 0033**.
**Requisitos:** RT-055, RT-056, RT-057
**Relacionada:** ADR 0091 (la física del pase), ADR 0092 (las razas recalibradas), ADR 0089 (el hueco estrecho del acto 3)

## Contexto

Tras la tanda 3 de AZ y la recalibración de razas, las puertas quedan en 38/43 con cuatro rojos de décimas y
uno de escalera, medidos dos veces (referencia de 2.000 partidos y puerta de 1.000 con semilla 1):

| Fila | Banda | Medido | Causa |
|---|---|---|---|
| `ballThirdMaxShare` | ≤ 50 | 50,4 / 51,3 | el pase en profundidad lleva el balón al último tercio más veces: es lo pedido (AZ-B), no una acampada; `possessionChanges` y `passChainAvgLength` siguen en banda |
| `betterTeamWinRate` 60-40 | 70-88 | 88,5 / 89,2 / 90,1 | la física del pase premia la habilidad (intercepción por técnica, carrera por velocidad) más de lo que la ADR 0054 midió |
| `bossGate_grimhold_guns_incoherent` | 15-35 (± 2,5) | 36,8 / 38,1 | los estilos recortados (ADR 0092) quitan técnica al jefe del acto 1 tanto como a la build incoherente |
| `eternal_crown` muy buena > buena | escalera creciente | 54,6-56,8 contra 56,7-58,0 | la fila «buena» sube con la física nueva (`sweeper_keeper` recupera más balones parados) y la «muy buena» no; es el hueco estrecho anotado en la ADR 0089 |

Se probó antes de tocar bandas: hacer que las builds «muy buena» fuesen un superconjunto de las «buena»
(añadir `sweeper_keeper` y `diagonal_press`) no cambia nada porque los slots 0 y 1 de esas plantillas son
comunes y ya tienen sus dos perks: los sobrantes no entran (`ProgressionRules.PerkSlots`). Se descartó
subir la calidad de `eternal_crown` (ADR 0083): mueve las dos filas a la vez y no cierra el hueco.

## Decisión

1. `ballThirdMaxShare` **≤ 52** (era ≤ 50).
2. `betterTeamWinRate` con 20 puntos de diferencia **70-90** (era 70-88). El techo sigue existiendo por lo
   que dice la ADR 0054: el peor equipo tiene que poder ganar, y con 90 gana uno de cada diez.
3. `grimhold_guns` incoherente **15-40** (era 15-35), con la tolerancia de ± 2,5 de la puerta.
4. La escalera de jefes admite un **peldaño plano de hasta 3 puntos** entre dos niveles consecutivos
   (`LadderTolerancePercent`); un peldaño hacia abajo mayor sigue siendo rojo. Las bandas por celda de la
   ADR 0033/0089 no cambian: `eternal_crown` muy buena sigue teniendo que estar en 50-70.

## Consecuencias

- Puertas en verde con el motor de la ADR 0091 y las razas de la ADR 0092; `main` se publica.
- La escalera del acto 3 es un tema de diseño abierto (AU-C): dos builds de calidad 50 que difieren en
  cinco perks pequeños no separan 3 puntos contra el jefe final. Se decide con AU-C, no aquí.
