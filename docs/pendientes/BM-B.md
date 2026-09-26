# BM-B — Una resolución publicada antes de tirarse no mira a sus participantes

Estado: **Abierta** (26 sep 2026). Hermana de [BM-A](./BM-A.md), que la destapó. Sin arreglo: cambiar
cualquiera de los casos de abajo cambia perks medidos, y eso es RT-057.

## Síntoma (el 1, CONFIRMED por medida; del 2 al 4, leídos en el código y sin evidencia de activación)

`TACKLE` y `DRIBBLE_ATTEMPTED` se publican **antes** de resolverse (`MatchEngine.ResolveTackle`,
`ResolveBlock`, `TryDribbleDuel`), y lo que los perks hacen en esa publicación **no llega** a la
resolución, que tira sus dados con datos de antes:

1. **`extraAction` sobre `TACKLE`** (`charge`, `bull_rush`). La entrada repetida se resuelve **dentro** de
   la publicación previa, así que la entrada «segunda» del diseño es en realidad la **primera** que se
   tira. Si falla, tumba al que entra (`KnockedDownTicks / 2`), y la entrada original **se sigue tirando
   con él en el suelo** y puede ganarle el balón.
   *Medido (26 sep, instrumentación temporal de BM-A, `--full-runs 20 --seed 3`): 12 casos, los 12 con
   una repetición dentro de la publicación previa y el entrador en `Tackling` antes de publicar. Aplicar
   «un derribado no disputa» a estos casos cambiaba 44 de 450 runs de `--full-runs 150 --seed 3`.*
2. **`setState` sobre el conductor en `TACKLE`** (`duelist`, `own_third_anchor`). `carrierHasBall` se
   calcula **antes** de publicar: el derribo suelta el balón (`KnockDown` → `ParkBall`), pero la entrada
   se sigue tirando como si el conductor lo llevara, y cuenta como `TACKLE`.
3. **`setState` sobre el defensor en `DRIBBLE_ATTEMPTED`** (`nutmeg`). Si el regate se pierde,
   `SetOwner(defender)` da el balón a un jugador en el suelo. El `_doc` de `nutmeg` lo declara como su
   paga.
4. **`injure` sobre el conductor en `TACKLE`** (`ankle_bite`): la víctima sale del campo y la entrada se
   sigue tirando contra ella.

## Lo que dice el diseño

`bull_rush`: *«si la primera no la gana, vuelve a por ella en el mismo instante»*. `charge`: *«salen dos
despedidos»*. Los dos describen una entrada **después** de la primera, y `bull_rush` exige además que la
primera fallada **no** tumbe al que entra. Hoy los dos funcionan por accidente del orden: el perk lo
lanza la publicación previa y el derribo de la fallada no frena a la entrada externa.

## Hipótesis de arreglo (sin elegir)

- **A.** «Un derribado no disputa» para cualquier derribo (no sólo el de efecto, que es lo que BM-A
  aplica), **y** la repetición de `extraAction` pasa a resolverse tras la entrada original, sin el derribo
  de la fallada cuando la repite `bull_rush`. Cambia `charge` y `bull_rush`: hay que re-medirlos.
- **B.** Recalcular `carrierHasBall` y el estado del conductor después de la publicación previa. Cambia
  `duelist`, `own_third_anchor` y `ankle_bite`.
- **C.** Aceptar el orden actual y documentarlo como semántica de la publicación previa.

Cualquiera pasa por `game-design-review` (es qué hace un acto) antes de tocar código, y por la tabla de
valores (ADR 0087) después.
