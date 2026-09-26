# BL-A — Lesionar al rival no ayuda a ganar

Estado: **ABIERTA** (26 sep 2026). Encargo del revisor tras el balance del paquete de actos.

## Síntoma medido

Con el protocolo de `perk-values.json` (`--perk-values --rosters 192 --runs 16`, semillas 5 y 11 sumadas),
dos perks cuyo único efecto es lesionar al rival salen **negativos** para quien los lleva:

| perk | efecto | valor (milésimas×2) |
|---|---|---:|
| `dirty_play` | en FALTA: lesiona y derriba al rival | **−9** |
| `ankle_bite` *(retirado)* | en cada ENTRADA: lesiona al rival | **−16** |

`ankle_bite` a una activación por partido seguía en −16 (pero con un portador único rotatorio el límite
apenas muerde, así que eso no discrimina frecuencia). La desviación por fila del lote era 8-12.

Es un agujero en el pilar de la carnicería: **hacer daño no ayuda a ganar**.

## Qué hace el motor tras una lesión (`MatchEngine.ResolveInjury`, leído)

1. `ShiftBiasAgainst(tackler.Team, biasShiftInjuryExtra = 2)` — el árbitro se pone en contra de quien
   lesiona (RF-063, deliberado).
2. `RaiseGrudgeAgainst(tackler, victim)` — represalia (ADR 0145).
3. Si la víctima llevaba el balón, `ParkBall` en su casilla.
4. Salvo una decisión de «seguir jugando» (ADR 0134 E, que el arnés de balance no toma), **toda** lesión
   saca a la víctima del campo (`RemoveFromPitch`).

## Hipótesis

- **H1** — el sesgo del árbitro en contra. *Sin medir.*
- **H2** — la represalia del equipo rival. *Sin medir.*
- **H3** — el suplente que entra está fresco y el que sale estaba cansado. *Sin medir.*
- **H4** — una lesión leve no le cuesta nada a la víctima dentro del partido. **REJECTED por lectura**:
  sin decisión de seguir jugando, toda lesión la saca del campo (punto 4).
- **H5** — (solo `ankle_bite`) lesiona ANTES de resolver la entrada: el balón queda aparcado y la entrada
  ya no puede robar (`carrierHasBall` falso). *Sin medir; explicaría que sea peor que `dirty_play`.*

## Plan de medición

Una celda por mecanismo, apagándolo en `tuning` con el perk armado, contra el mismo control. Empezar por
`dirty_play` —existe, y no tiene H5 encima—.

## Ronda 1 — el síntoma no sobrevive a su propio error típico (26 sep 2026)

Celdas de `dirty_play` en un worktree aislado a `0380220`, protocolo oficial (192 plantillas, campaña 16,
semillas 5 y 11), apagando mecanismos en `data/sim/tuning.json`:

| celda | `biasShiftInjuryExtra` | `GrudgeTicks` | valor |
|---|---:|---:|---:|
| base | 2 | 150 | **+7** |
| sin sesgo | 0 | 150 | +7 |
| sin rencor | 2 | 0 | +13 |
| sin ambos | 0 | 0 | +12 |

**La base no reproduce el −9.** El −9 salió del lote `f` (con `ankle_bite` todavía en el catálogo). Como
la semilla genera las plantillas a partir del catálogo, al cambiar el catálogo cambia la muestra: por semilla,
`f` daba −12/−7 y la base de ahora +3/+12. La variación entre muestras es de unos 15-20 puntos.

Error típico: sobre el catálogo completo (>100 filas), `rowDeviation` = sd(A−B)/2 sí estima el ET del
valor sumado, y vale **≈7** (`perk-values.json`). Con dos o tres perks en el lote, en cambio, esa misma cifra
no estima nada (4-8 sobre dos filas). Así que −9 está a 1,3 ET de cero, y −9 contra +7 es 1,6 ET de la
diferencia (√2·7 ≈ 10): **ruido**. **Regla J**: el síntoma se tomó como medido sin ponerle su error al lado.

- **Síntoma («lesionar resta»)**: **REJECTED como medido**. −9 y −16 están dentro del ruido de su propio
  lote. Lo que *sí* sigue en pie es la versión débil: **lesionar tampoco suma de forma detectable**, que para
  el pilar de la carnicería también es un problema de diseño.
- **H1, H2**: sin evidencia a esta potencia. Las diferencias entre celdas (0 a +6) están dentro del ruido.

Ronda 2 lanzada: 768 plantillas × 4 semillas (5, 11, 17, 23), base contra ambos mecanismos apagados, con
`dirty_play` y `ankle_bite` (restaurado solo en el worktree). El error típico se estima con la dispersión
entre las cuatro semillas, no con `rowDeviation`.
