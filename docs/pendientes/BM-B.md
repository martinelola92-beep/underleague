# BM-B — Una resolución publicada antes de tirarse no mira a sus participantes

Estado: **Cerrada (30 sep 2026), ADR 0180**. Antes: abierta (26 sep 2026). Hermana de [BM-A](./BM-A.md), que la destapó. Sin arreglo: cambiar
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


## Resolución (30 sep 2026) — ADR 0180

`game-design-review` de «qué debe hacer una `extraAction` sobre `TACKLE`»: **vuelve a entrar después de la primera**,
no antes. Se arma en la publicación previa y la ejecuta el motor al terminar la entrada (`FinishRepeatedTackle`).
Medido con traza de un fotograma por tick (300 partidos por perk, `PreResolutionParticipantsTests`):

| caso | antes | ahora | veredicto |
|---|---|---|---|
| 1 `extraAction` (`charge`, `bull_rush`) | 7 de 144 activaciones de Toro acababan el tick con el que entra derribado **y con el balón**; secuencias `won > missed` contra un portador sin balón | 0; la repetición va contra el mismo portador si sigue en pie (`missed > won` = «vuelve a por ella») o contra otro rival en pie | **CONFIRMED y arreglado** |
| 2 `setState` sobre el portador (`duelist`, `own_third_anchor`) | coherente: el derribo suelta el balón y la entrada se juega contra él | igual | **REJECTED como defecto**; documentado y con test |
| 3 `setState` sobre el defensor de un regate (`nutmeg`) | coherente: si se pierde, el defensor recupera el balón y `Decide` lo levanta (12 de 44 activaciones, ninguna termina con un derribado con balón) | igual | **REJECTED como defecto**; documentado y con test |
| 4 `injure` (`ankle_bite`) | 5 de 61 lesiones provocadas dejaban el balón en (-1,-1); una segunda lesión sobre el mismo cuerpo; `Tackle:won` contra un jugador que ya no está | 0: la entrada, el bloqueo o el regate se anulan si el participante salió del campo | **CONFIRMED y arreglado** |

Hallazgo añadido por la propiedad «nadie acaba un tick derribado con el balón»: `steamroller` (y la falta no vista)
dejaban el balón en los pies de un jugador tumbado (36 fotogramas en 120 partidos); ahora el balón se suelta al caer.
Valores antes/después de los perks: ver `docs/decisiones/0180-...` y la tabla de la ficha BC-C.

## Remedición sobre main tras el rebase (30 sep 2026, ADR 0175 incluida)

Tabla de la ADR 0087 (`--perk-values --rosters 192 --runs 16`), semillas 5/11/17, **main contra rama** (mismo instrumento,
mismo catálogo salvo los cambios de la rama), error típico de la diferencia entre medias de tres semillas. Instrumento
validado: `blood_tithe`, `duelist`, `nutmeg` y `own_third_anchor`, que la rama no toca, salen **idénticos al dígito**
en los dos lados (Regla J). El lote de referencia (`--runs 3000`, semillas 1 y 2) es igual en main y en rama salvo dos
métricas INFORMATIVAS del orden de 0,03 puntos (la referencia casi no lleva estos perks). Las builds de `/Balance` que
los usan (`elf_glass`, `orc_butchery`) **no se pueden medir**: fallan con «asigna N perks, solo tiene M slots» también en
main (BuildGate, ya rojo); la tabla por perk es el sustituto.

| perk | main | rama | diferencia (± e.t.) | etiqueta |
|---|---:|---:|---:|---|
| `charge` | −0,7 | +5,0 | +5,7 ± 11,1 | no separable del ruido |
| `bull_rush` | +3,0 | +6,0 | +3,0 ± 14,5 | no separable del ruido |
| `steamroller` | −8,3 | +0,7 | +9,0 ± 1,5 | **CONFIRMED**: el balón se suelta al caer el derribado y deja de dar ventaja al derribado que lo conservaba |
| `ankle_bite` | −7,0 | −12,3 | −5,3 ± 4,8 | ver [BM-C](./BM-C.md) |
| `duelist`, `nutmeg`, `own_third_anchor` | sin cambio | sin cambio | 0 | el instrumento: no los toca la rama |

La variación de las filas de acto (`charge`, `bull_rush`) queda dentro del ruido de una tabla con ±10-15 por semilla:
el arreglo es de **coherencia** (demostrado por la traza de un fotograma por tick, `PreResolutionParticipantsTests`),
no de valor, y no se vende como lo segundo.
