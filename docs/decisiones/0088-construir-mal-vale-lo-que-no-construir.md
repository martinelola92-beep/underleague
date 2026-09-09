# 0088. Construir mal vale lo que no construir: la escalera de fase 1 arranca en 50

**Fecha:** 2026-09-09
**Estado:** Aceptada e implementada (`Sim/Analysis/BuildMetrics.cs`, `Sim.Tests/Analysis/BuildGateTests.cs`)
**Decisión del revisor.** Sustituye el objetivo 2 de la ADR 0056 en su lectura de «construir mal pierde» y el techo de la ADR 0078; deja sin efecto el mecanismo de castigo de la ADR 0060 (conservando su diagnóstico)
**Requisitos:** RF-032, RF-070, RF-072, RT-055, RT-057
**Relacionada:** ADR 0087 (el instrumento que hizo visible que los "negativos" eran ceros), paquete AY (`docs/plan-perks-positivos.md`)

## La decisión del revisor (9 sep 2026)

1. **Ningún perk es negativo.** Un perk nunca perjudica al equipo que lo lleva, en ninguna rama. «Un perk mal
   puesto es un perk que no tiene efecto (por ejemplo, que no esté en la zona en la que se activa)». Lo que
   cuesta es el slot irreversible que ocupa sin dar nada —y el oro, si se compró— que es lo que el listón de
   coste de oportunidad de la ADR 0072 ya pone precio.
2. **Escalera desde 50.** Sin castigos, una build mal colocada vale lo que no tener build. Las puertas que
   exigían «construir mal pierde contra no construir» (≤ 45 %) se sustituyen por una escalera cuyo suelo es
   50: no construir ≈ construir mal ≈ 50 · regular ≈ 55 · bien ≥ 60. La discriminación pasa entera al lado
   positivo.

## Lo que cambia

- **Datos (paso 2 de AY):** los 17 `elseEffects` de la ADR 0060 pasan a `[]`; `brute_boots` pierde su
  `pass −50` propio. Las descripciones generadas (RT-035) dejan de decir «; si no, …».
- **Puertas (paso 4):** `badBuildsLoseToNone_*` y `randomBuildLosesToNone_*` pasan de «≤ 45» a la banda
  **45-55** (`BuildMetrics.BadBuildMinWinRate`/`BadBuildMaxWinRate`): el suelo dice que un perk mal puesto
  no resta, el techo que no suma. `coherentBuildsBeatNone_*` (≥ 58) no se toca.
- **`noDeadPerks`** se juzga sobre las builds que colocan bien el perk (las coherentes de `groups.json`): un
  perk que solo aparece en builds mal construidas a propósito activa el 0 % por construcción —antes lo
  salvaba la rama `else`— y eso no es un perk muerto, es un perk sin build coherente que lo lleve (INFO).
  Afectaba a `flank_specialist`, `spearpoint` y `wing_overlap`.

## Lo que se mide

Celdas de las builds malas y de azar contra su referencia sin perks, con la muestra de la puerta (40
plantillas × 12 partidos, semilla 1), tras vaciar las ramas `else`:

| Build | Antes (con castigo) | Sin castigo | Banda nueva |
|---|---|---|---|
| `orc_misplaced` | ≤ 45 | 52,50 | 45-55 |
| `elf_brawler` | ≤ 45 | 47,92 | 45-55 |
| `human_scattered` | ≤ 45 | 50,62 | 45-55 |
| `elf_out_of_zone` | ≤ 45 | 50,21 | 45-55 |
| `human_random` (ADR 0078) | 40,62 | 54,17 | 45-55 |

Todas dentro, ninguna por debajo de 50 en más que el ruido: exactamente «construir mal vale lo que no
construir». `BuildGateTests` 8/8 en verde con la tabla emparejada de la ADR 0087 y los dos acumuladores
recortados.

## Alternativas descartadas

- **Mantener «≤ 45» y compensar por otra vía** (que el perk mal puesto cueste oro o experiencia además del
  slot): no cierra sin volver a introducir un malus, que es lo que el revisor descarta.
- **Quitar esas puertas sin sustituirlas:** se perdería una afirmación automatizada que sigue siendo
  verdadera y útil —que colocar mal no *ayuda*—; la banda 45-55 la conserva por los dos lados.

## Consecuencias

- El objetivo 2 de la ADR 0056 («mediocre 42-45 %») queda **sin sentido tal como estaba**: la build mediocre
  ya no puede perder contra la referencia. El objetivo que sobrevive es el **hueco** buena/mediocre (> 9,8
  puntos, ADR 0056 objetivo 1): se mide en el cierre del paquete AY (paso 5) con las puertas de jefes, y si
  la escalera de la ADR 0033 necesita recalibrar la calidad de algún jefe, será por ADR como la 0083.
- La ADR 0060 queda como diagnóstico («el castigo tiene recorrido donde el premio no lo tiene») sin
  mecanismo: el recorrido tendrá que venir del premio, que es AL-A.
- `docs/fase1-diseno.md` §8 y la fila de `docs/balance.md` que citan el «≤ 45 %» se leen desde esta ADR.
