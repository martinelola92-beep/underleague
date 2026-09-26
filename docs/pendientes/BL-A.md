# BL-A — Lesionar al rival no ayuda a ganar

Estado: **CERRADA** (26 sep 2026): el síntoma era ruido de muestra. `ankle_bite` es LIKELY no negativo (≈ +10) y vuelve al catálogo; la paga de diseño queda sin demostrar (ver «Lo que queda abierto»). Encargo del revisor tras el balance del paquete de actos.

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

## Ronda 2 — con más potencia (26 sep 2026), corregida por la revisión independiente

Worktree aislado a `0380220` con `ankle_bite` restaurado, campaña 16, horizonte 8. La primera versión de esta
ronda usó 768 plantillas y dio `ankle_bite` +13,9 ± 2,1 («a 6,6 ET»). **La revisión la tumbó por dos motivos:**

1. **El instrumento no admite más de 500 plantillas** (Regla J). El sujeto r usa la semilla `índice*1000 + r`
   y el espejo `índice*1000 + 500 + r`: a partir de r = 500, el sujeto reutiliza el equipo del espejo r−500.
   268 de las 768 parejas reciclaban equipos. Por bloques, el que colisiona (500-767) daba 22,2 y el limpio
   (0-499) 9,5. Arreglado: `PerkValueRunner` e `ItemValueRunner` rechazan `rosters > MaxRosters` (500),
   con `ValueRunnerSeedSpaceTests`.
2. **Con 3 grados de libertad, el ET de cuatro semillas está en la cola baja.** Con ocho semillas a 192
   plantillas, `ankle_bite` tiene una sd de ≈18 por lote. Eso implica un ET de ~5 para cuatro lotes de
   500, no 2,1.

Medida limpia (500 plantillas, semillas 5, 11, 17 y 23; lotes `rev-500-*` de la revisión, recalculados):

| perk | s5 | s11 | s17 | s23 | media | ET honesto |
|---|---:|---:|---:|---:|---:|---:|
| `ankle_bite` | 5,0 | 8,5 | 15,5 | 9,0 | **+9,5** | ~5 |
| `dirty_play` (8 semillas × 192) | | | | | **+0,2** | 3,2 |

- **Síntoma «lesionar al rival resta»**: **REJECTED bajo la ADR 0087** (espejo con portador rotatorio) y
  con el arnés tal como está: sin la decisión de seguir jugando (ADR 0134 E) y **sin arrastrar lesiones entre
  partidos**. El −16 de la ronda 0 se reproduce al bit con el catálogo actual (semillas 5+11 a 192) y es un
  subconjunto del mismo mundo que el +9,5: ruido de muestra.
- **`ankle_bite` no es negativo**: **LIKELY** (≈ +10 ± 5,7 con el ET honesto). **Que sea positivo no
  está demostrado**: con la t de Student de 3 grados de libertad, la ADR 0150 lo clasifica como «sin
  efecto a esta potencia». Lo decisivo es que ninguna estimación razonable cae por debajo del umbral de
  retirada (−7): **la retirada no tenía base**.
- **«Lesionar al rival ayuda a ganar» como propiedad del sistema**: **no demostrado**. Descansa en una sola
  perk, `dirty_play` vale ≈0 y no se ha identificado el mecanismo de la ganancia.
- **H1 (sesgo del árbitro) y H2 (rencor)**: **sin evidencia de efecto a esta potencia**, no REJECTED. Los Δ
  pareados de la tabla de 768 (−2,0 ± 1,3 y −0,8 ± 3,4, con 3 grados de libertad) no excluyen un coste del
  tamaño del valor entero de la perk. Además, el medidor no ve el coste de H2 en la run: las lesiones de
  represalia sobre la plantilla propia son desgaste que no se arrastra.
- **H3 y H5**: sin medir.

La tabla de 768 se queda como registro, pero **ya no es evidencia**:

| perk | celda | s5 | s11 | s17 | s23 | media |
|---|---|---:|---:|---:|---:|---:|
| `dirty_play` | base | 6,5 | −2,6 | 9,8 | −2,0 | +2,9 |
| `dirty_play` | sin sesgo ni rencor | 3,6 | −3,3 | 4,6 | −1,0 | +1,0 |
| `ankle_bite` | base | 13,0 | 9,1 | 19,2 | 14,3 | +13,9 |
| `ankle_bite` | sin sesgo ni rencor | 21,8 | 6,2 | 11,7 | 12,7 | +13,1 |

**Consecuencia**: `ankle_bite` vuelve al catálogo con valor 10, y su fila de `perk-values.json` declara la
procedencia porque no sale del procedimiento de la tabla. Censo de lesiones (`PerkInjuryCensusTests`, que
vuelca pero no afirma): 0,59/partido con toda la línea armada contra 0,43 del espejo. Es una cota superior
dentro de RT-056, sin separar lesiones propias y rivales.

## Lo que queda abierto

- **DESIGN CLAIM NOT PROVEN — la paga.** El diseño de `ankle_bite` promete que «el árbitro aprende» y que
  «la expulsión es cuestión de tiempo» (`docs/analisis/perks-catalogo-de-actos.md`). Apagar el sesgo no
  mueve el valor a esta potencia, y no hay censo de tarjetas ni de represalias. Hay que pasarlo por
  `game-design-review` antes de darlo por diseñado. **Hecho el 26 sep: [BM-C](./BM-C.md).**
- **Problema hermano**: [BL-B](./BL-B.md), el umbral de la ADR 0087 con un `rowDeviation` global sobre filas
  de varianza alta.
