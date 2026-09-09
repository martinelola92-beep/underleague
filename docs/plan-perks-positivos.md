# Plan: ningún perk negativo, y la muerte solo en la entrada (paquete AY)

**Fecha:** 2026-09-09. **Origen:** decisión del revisor tras revisar el catálogo completo (61 perks, 34 objetos).
**Estado:** plan aprobado en sus tres decisiones de regla; ejecución paso a paso, midiendo por tanda.

## Lo que el revisor decidió (9 sep 2026)

1. **Todos los perks son positivos.** Un perk nunca perjudica al equipo que lo lleva, en ninguna rama. Cuando su
   condición no se cumple, **no hace nada** (rama `else` neutra). El castigo del perk mal puesto sigue existiendo,
   pero cambia de naturaleza: en palabras del revisor, «un perk mal puesto es un perk que no tiene efecto (por
   ejemplo, que no esté en la zona en la que se activa)». Lo que cuesta es el slot irreversible que ocupa sin dar
   nada —y el oro, si se compró—, que es exactamente lo que el listón de coste de oportunidad de la ADR 0072 ya
   pone precio. Si para equilibrar hay que subir o bajar la magnitud de un efecto, se hace.
2. **Escalera desde 50.** Sin castigos, una build mal colocada vale lo que no tener build. Las puertas de fase 1
   que exigían "construir mal pierde contra no construir" (≤ 45 %, ADR 0056 objetivo 2, ADR 0078) se sustituyen
   por una escalera cuyo suelo es 50: no construir ≈ construir mal ≈ 50 · regular ≈ 55 · bien ≥ 60. La
   discriminación pasa entera al lado positivo.
3. **La muerte sigue existiendo al mismo nivel (banda 1,5-3 por run), pero nunca en el saque inicial.** La
   tirada letal se mueve a la jugada de contacto y su probabilidad sube para compensar. Si hace falta, los
   jugadores pueden ser **más agresivos en general** (más entradas) para sostener la banda.

## Inventario (medido sobre `/data` del 9 sep)

| Qué | Perks |
|---|---|
| Letales en el saque (`MATCH_START`) | `skullsplitter` (5.400, legendario, exige `Dirty`), `marrow_thirst` (4.200) |
| Letales en contacto | `iron_studs` (`TACKLE`, 760), `second_wound` (`INJURY`, 9.000, `scoreDiff() <= 0`) |
| Se penaliza a sí mismo en la rama principal | `brute_boots` (`owner: pass −50`) |
| Castigan en la rama `else` (ADR 0060) | 17: `brute_boots`, `bulwark_stance`, `center_conductor`, `covering_shadow`, `diagonal_press`, `fine_orchestra`, `fine_touch`, `flank_specialist`, `forward_line`, `gentle_giant`, `last_ditch`, `own_third_anchor`, `pack_mentality`, `pivot_duo`, `safety_net`, `spearpoint`, `wing_overlap` |
| Miden negativo (tabla ADR 0070) | 10: `spearpoint` −141, `forward_line` −115, `bulwark_stance` −53, `fine_touch` −49, `back_to_back` −37, `own_third_anchor` −24, `iron_studs` −17, `pack_mentality` −5, `safety_net` −1, `steady_hands` −1 |
| Ganan demasiado (acumuladores, ADR 0069) | `deathless_march` +308, `clean_sheet_legacy` +247, `killing_range` +209, `battle_reader` +168 (el siguiente es +119) |

Por qué el saque mata: desde la ADR 0048 `IsLethalVictim` es cualquier rival vivo en el campo, y la tirada es
`lethalChance × estado × proximidad × (fuerza − aguante)` con tope 8.000. Un `skullsplitter` marca en el saque
al rival que peor lo tiene y tira a matar con hasta un 80 %. El `_doc` del perk explica que `MATCH_START` era
el único momento alcanzable **antes** de la ADR 0048, cuando solo moría quien salía ya herido; la ADR 0048
quitó esa puerta y dejó el disparador donde estaba.

## Pasos

Cada paso: un cambio, tests filtrados, y la medición mínima que resuelve su duda (Release + paralelo: `--perk-values`
completo en minutos, las 42 puertas en 3 min).

### Paso 1 — La muerte sale del saque

- **Regla nueva en el cargador (RT-032):** un perk `lethal` no puede disparar en `MATCH_START` ni en `PLAY_START`.
  Error explícito al cargar. Es la garantía, no una convención.
- `skullsplitter` y `marrow_thirst` pasan a disparar en la jugada de contacto del portador (`TACKLE`; si el motor
  distingue falta, también `FOUL`). Su bonus de lesión al equipo rival, que era de partido, se conserva como
  efecto de partido cobrado en la primera entrada (o se reescribe como efecto por entrada si mide mejor).
- **Compensar la banda:** `deathsPerRun` mide 1,44 (banda 1,5-3). Con la tirada solo en contacto habrá menos
  tiradas; subir `lethalChance` de los cuatro letales hasta que `--full-runs` devuelva 1,5-3. Si no llega solo
  con el perk, subir la agresividad general (peso base de `Tackle` en `data/ai/weights.json`), vigilando
  `tacklesPerMatch` (6-14) e `injuriesPerMatch` (0,3-0,9) de RT-056.
- **Mide:** `--full-runs 1200 --seed 1` y `--seed 7` (`deathsPerRun`, `runWinRate`, `ordinaryDefeatRateAct1`),
  `StatisticalTests` (RT-056), `BossGateTests` (los jefes llevan letales).

### Paso 2 — Rama `else` neutra y sin autocastigo

- Las 17 ramas `elseEffects` pasan a `[]`. `brute_boots` pierde su `owner: pass −50`.
- El generador de descripciones deja de emitir "; si no, …" para esos perks (RT-035: sale solo del efecto).
- **Mide:** `--perk-values` completo (misma metodología ADR 0070: campaña, dos semillas). Esperado: los diez
  negativos suben a ≥ 0 ± ruido; la dispersión de la tabla se comprime. `SlotBar` (ADR 0072) se rederiva solo,
  es un cuantil.
- **Puertas de fase 1** en rojo esperado: `BuildGateTests.BadBuildsLoseToTheirBaseline` y
  `randomBuildLosesToNone`. Es el paso 4 quien las sustituye; hasta entonces se documenta el rojo, no se
  maquilla.

### Paso 3 — Techo a los que ganan demasiado

- Los cuatro acumuladores por encima de +150: bajar el paso del contador o su tope (`limit`, "hasta N veces")
  hasta que el máximo medido quede del orden de +150 (el siguiente perk mide +119). Es magnitud, no regla:
  dentro de la autoridad del encargo.
- Riesgo medido: la ADR 0069 subió esos topes para llevar la run a ~19-20 %. Bajarlos empuja `runWinRate`
  hacia abajo; el paso 2 (los mediocres dejan de castigarse) la empuja hacia arriba. Se mide el neto, no se
  supone.
- **Mide:** `--perk-values` de esos cuatro (`--perks`), después `--full-runs` × 2 semillas.

### Paso 4 — Escalera desde 50 (ADR)

- ADR nueva: sustituye la afirmación "construir mal pierde" de las ADR 0056 (objetivo 2) y 0078 por la escalera
  desde 50. `BadBuildsLoseToTheirBaseline` → `BadBuildsAreNoWorseThanNone` (45-55). `randomBuildLosesToNone` →
  banda 45-55. El objetivo "mediocre 42-45 %" de la ADR 0056 se rebasa a ≈ 55 con el hueco buena/mediocre
  medido de nuevo; el hueco (> 9,8) se conserva como exigencia.
- **Mide:** `BuildGateTests` completa, `BossGateTests` (12 celdas: la tabla de la ADR 0033 puede necesitar
  recalibrar la calidad de algún jefe, como en la ADR 0083), `RaceBalanceTests`.

### Paso 5 — Cierre

- Banco de cierre: `--full-runs` a la muestra de la ADR 0076 con el motor nuevo (cubre también **AX-B**:
  las dos celdas de jefe que se habían movido por el motor).
- ADR (decisión del revisor, modifica RF-072 en su lectura de "un perk mal puesto"), `fase2-diseno.md` §41,
  `pendientes.md` (AY-*), `CLAUDE.md`, y el catálogo publicado regenerado.

## Lo que NO se toca

- Los efectos sobre el **rival** (`opposingTeam`, `opponent`) siguen: bajar al contrario es positivo para el
  portador.
- Los objetos malditos (contrapartida por diseño, RF-077 §"Maldito") no entran: son objetos, no perks.
- Las habilidades raciales (ADR 0026) y los cinco perks sin medir de esa familia.
