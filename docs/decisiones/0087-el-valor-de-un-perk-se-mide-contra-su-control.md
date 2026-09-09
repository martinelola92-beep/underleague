# 0087. El valor de un perk se mide contra su control emparejado, no contra el 50 %

**Fecha:** 2026-09-09
**Estado:** Aceptada e implementada (`Balance/PerkValueRunner.cs`, `tools/perk-values-table.py`, `data/economy/perk-values.json`)
**Corrige:** el instrumento de la ADR 0070 (y de la ADR 0038 antes), sin cambiar su metodología de espejo ni de campaña
**Requisitos:** RT-023, RT-054, RT-057
**Relacionada:** ADR 0072 (el listón del slot lee la tabla), ADR 0085 (curva por horizonte), paquete AY (`docs/plan-perks-positivos.md`)

## El aviso

El paso 2 del paquete AY vació las ramas `else` de los 17 perks que castigaban al portador mal colocado y
remidió la tabla: **18 perks seguían midiendo negativo** por su rama principal, con `rowDeviation` 19 y
algunos a tres desviaciones (`safety_net` −55, `back_to_back` −48, `bruised_knuckles` −44). Todos tienen
efectos exclusivamente positivos sobre su propio equipo. La regla del revisor —«ningún perk debe ser
negativo»— parecía exigir un paso de magnitudes sobre perks que, leídos, no restan nada.

## Lo que se encontró

El valor de la ADR 0070 era **absoluto**: `(tasa de victoria del sujeto con el perk − 50 %) × 2`, en
milésimas. Supone que el sujeto sin el perk gana exactamente el 50 % contra su espejo. No es verdad para
**cada** perk: cada uno se mide sobre su propia pareja de plantillas (`RngStreams.Generation(seed,
perkIndex·1000 + roster)` y `+500`), y esa pareja concreta tiene su propio sesgo. Medido con las mismas
plantillas y las mismas semillas de partido, **sin** el perk (192 parejas × 16 partidos, semilla 5):

| Perk | Con el perk | Control (sin él) | Diferencia |
|---|---|---|---|
| `back_to_back` | 48,54 % | 48,57 % | −1 |
| `bulwark_stance` | 48,50 % | 48,50 % | 0 |
| `high_press_trigger` | 48,37 % | 48,37 % | 0 |
| `safety_net` | 49,74 % | 49,74 % | 0 |
| `fine_touch` | 50,26 % | 50,23 % | +1 |
| `game_management` | 49,84 % | 49,64 % | +4 |
| `deathless_march` | 65,56 % | 49,02 % | **+331** (la tabla decía 272) |

Los "negativos" no perjudican a nadie: **no hacen nada** en el espejo, y el número negativo era el sesgo
de su pareja de plantillas. El mismo sesgo, con el otro signo, infravaloraba a `deathless_march` en 59
unidades. Lo que hizo visible el problema fue el instrumento nuevo de las paralelizaciones (medida de
control con las mismas semillas), que costaba minutos en vez de horas.

## Decisión

`--perk-values` juega **dos brazos** por pareja y partido, con la misma semilla: el sujeto con el perk y el
sujeto sin él. El valor es la **diferencia emparejada** `(victorias − victorias del control) / partidos ×
2.000`, en la misma escala que antes; la curva por horizonte (ADR 0085) es la diferencia acumulada por
índice de partido; la desviación por fila sigue siendo la mitad de la RMS de la diferencia entre los dos
lotes independientes, ahora sobre valores emparejados. `perk-values.csv` y `perk-values-by-match.csv`
llevan las columnas `controlWins`/`controlWinRate`. La construcción del bloque JSON, que hasta hoy era un
script fuera del repositorio, queda en `tools/perk-values-table.py`. El coste se dobla (dos brazos) y sigue
siendo de minutos.

Ninguna decisión que lea la tabla cambia de código: el listón del slot (ADR 0072) es un cuantil de la
propia tabla y se rederiva solo; los pesos del pool (ADR 0038) leen los mismos campos.

## Lo que se mide

Tabla completa emparejada, semillas 5 y 11 sumadas (3.072 partidos por perk y brazo al horizonte 8, 51
perks; `tools/perk-values-table.py`):

| | Tabla absoluta (paso 2) | Tabla emparejada (paso 3) |
|---|---|---|
| `rowDeviation` (horizonte 8) | 19 | **7** |
| `rowDeviationByHorizon` (1 → 16) | 47 … 17 | 16 … 5 |
| Perks por debajo de cero | 18 (hasta −55) | 5, todos entre −7 y −1 (dentro de una desviación) |
| Mediana / p75 | 13 / 31,5 | 6 / 26 |
| Máximo | `deathless_march` 272 | `deathless_march` 178 |
| Dispersión | 58,5 | 41,6 |

Ningún perk perjudica al equipo que lo lleva: el mínimo es `mob_instigator` −7 con ruido 7. Los dos únicos
perks por encima de +150 se recortaron por el techo del contador, no por el paso — el canal multiplica la
cuota y bajar solo el paso apenas movió la medida (`deathless_march` 303 → 251 con `valuePerCounter`
100 → 50; con `maxValue` 4 → 2, 125; `clean_sheet_legacy` 186 → 185 con 50 → 30; con `maxValue` 5 → 2, ~90).
Con dos pasos caían las celdas «muy buena» de la ADR 0033 (todas las `*_excellent` llevan uno de los dos), así
que el techo queda en **tres pasos**: `deathless_march` **178** y `clean_sheet_legacy` **98** (ADR 0089). La
ADR 0069 había subido esos techos para empujar la run: el paso 5 mide lo que cuesta bajarlos.

## Consecuencias

- La regla «ningún perk negativo» pasa a ser **comprobable**: un valor por debajo de −`rowDeviation` es
  un perjuicio real; entre −`rowDeviation` y +`rowDeviation` es un perk que no hace nada en el espejo. Lo
  segundo es una limitación conocida del espejo con portador rotatorio (AT-C: un bonus condicionado a una
  etiqueta o a una zona que el portador al azar rara vez cumple) y no un defecto del perk.
- Las tablas históricas (ADR 0070, 0072, 0085) mezclaban efecto y sesgo de pareja con una amplitud del
  orden de ±30 unidades por fila. Ninguna conclusión de banda depende de una fila concreta, pero el listón
  del slot se derivaba de una distribución más ancha de lo real: el paso 5 de AY mide el efecto en la run.
- `AV-B` (ADR 0085) midió "la run no lo nota" con la tabla absoluta; su conclusión no cambia porque el
  sesgo de pareja es el mismo a todos los horizontes de una misma fila.
