# 0089. La fila «muy buena» baja con el techo del catálogo

**Fecha:** 2026-09-09
**Estado:** Aceptada e implementada (`data/bosses/eternal_crown.json`, `data/bosses/the_hunt.json`)
**Decisión del revisor. Modifica la tabla de la ADR 0033** en dos celdas: `eternal_crown` muy buena 55-70 → **50-70**, `the_hunt` muy buena 72-85 → **70-85**
**Requisitos:** RT-055, RT-057
**Relacionada:** ADR 0069 (el eje del contador al techo), ADR 0083 (mismo tipo de decisión, en la celda incoherente), ADR 0087/0088 (paquete AY)

## Contexto

El revisor pidió recortar «las que ganan demasiado». Con el instrumento emparejado (ADR 0087) solo dos perks
superaban +150: `deathless_march` (303) y `clean_sheet_legacy` (186), los dos acumuladores que la ADR 0069
había subido a propósito para que la fila «muy buena» llegara a su banda. Las cinco builds `*_excellent` de
la tabla llevan uno de los dos.

Medido con la muestra de la ADR 0074 (64 × 16 = 5.120 partidos por celda, semilla 1):

| Techo del contador | `deathless_march` | `clean_sheet_legacy` | `eternal_crown` muy buena | `the_hunt` muy buena |
|---|---|---|---|---|
| 4 / 5 (ADR 0069) | 303 | 186 | 56,4 | 78,8 |
| 2 / 2 | 125 | ~90 | 50,6 | 68,9 |
| **3 / 3** | **178** | **98** | **52,3** | **71,0** |

Las otras diez celdas quedan dentro de banda con cualquiera de los tres techos; las incoherentes suben (sin
castigo del perk mal puesto, ADR 0088) y siguen dentro: `grimhold_guns` 29,7 (15-35), `the_hunt` 13,1 (≤ 15),
`eternal_crown` 4,4 (≤ 10). Ningún jefe lleva perks letales, así que el paso 1 de AY no interviene aquí.

## Decisión

Techo de **tres pasos** para los dos acumuladores, y las dos bandas «muy buena» bajan a **50-70** y **70-85**.
La fila baja porque el catálogo ya no tiene un perk de 300, no porque el jefe sea más duro: la escalera sigue
creciente (`eternal_crown` 4,4 / 23,5 / 48,9 / 52,3; `the_hunt` 13,1 / 39,5 / 63,7 / 71,0).

## Alternativas descartadas

- **Devolver los dos perks a su techo:** renuncia a la regla del revisor para esos dos.
- **Bajar la calidad de los dos jefes** (ADR 0083): sus celdas incoherentes están al borde del techo
  (`the_hunt` 13,1 sobre 15) y subirían con un jefe más flojo.

## Consecuencias

- El hueco buena/muy buena de `eternal_crown` queda estrecho (48,9 → 52,3): es la forma de la escalera del
  acto 3, el mismo problema que AU-C describe en el acto 2. Anotado; se mide en el paso 5 de AY.
- `docs/fase2-diseno.md` §40 recoge la tabla; `BossGateTests` en verde con estas bandas.
