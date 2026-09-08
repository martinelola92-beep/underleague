# 0086. Un objeto de mercado también cuesta el slot, no solo el oro

**Fecha:** 2026-09-08
**Estado:** Aceptada e implementada, activa por defecto (`RunPolicyOptions.MinItemValueMarket`; `null` = listón del slot del nodo, igual que su gemelo de perks)
**Cierra:** AT-A (`docs/pendientes.md`), paso 2
**Requisitos:** RF-076, RT-023, RT-054, RT-057
**Relacionada:** ADR 0038 (precio del objeto), ADR 0072/0073 (listón de perk = coste de oportunidad del slot), ADR 0085 (AV-B, mismo patrón de instrumento de dos pasos)

## El aviso (AT-A)

Un perk de recompensa cuesta el slot; uno comprado en el mercado cuesta el slot **y** el oro. Desde la ADR 0072 la compra de un perk en el mercado pasa por `WorthASlot`: un gate de coste de oportunidad contra el valor **medido** de ese perk concreto. La compra de un objeto en el mercado (`NextMarketAction`, bloque "(c)") nunca tuvo ese gate: solo mira rareza, presupuesto y encaje de atributos. El listón del mercado debía ser "el del slot MÁS el del oro" y no se podía sumar porque `ItemScale.ValueOf` (la estimación calculada) y la tabla de perks estaban en normalizaciones distintas —un +20 repartido entre diez jugadores contra un perk sobre un solo portador—.

El paso 1 (commit `882f732`) midió cada objeto **exactamente como se mide un perk** (`--item-values`, mismo espejo puro) y dejó `data/economy/item-values.json` cargado en `EconomyConfig.ItemValues`, en la misma unidad que `PerkValues`. Su propio `_doc` avisaba de la condición que este paso 2 hereda: el **nivel** del catálogo está bien determinado (+37 milésimas de media, error típico 4) pero la **dispersión entre objetos (27) no se separa del ruido de fila (21)**, así que la tabla no ordena objeto a objeto a esta muestra —al contrario que la de perks, 73 contra 17—. Preguntado, el revisor optó explícitamente por el listón agregado con el nivel, no por remedir hasta poder ordenar objeto a objeto.

## Lo que se implementa

- `ItemWorthASlot(economy, options, bar)` (`Sim/Analysis/RunPolicy.cs`), gemela de `WorthASlot` pero con el **nivel agregado** en vez del valor por objeto: no contextual → `true`; tabla vacía (instantánea sin `item-values.json`) → `true`, igual que un perk sin tabla; si no, `economy.ItemValues.MeanValue >= bar`. El gate es todo o nada: si el catálogo vale el slot en este nodo se compra el objeto que mejor encaje (ADR 0036); si no, no se compra ninguno.
- En `NextMarketAction`, el bloque "(c)" reutiliza el mismo `bar` (`SlotBar`, ADR 0072) que ya calcula el bloque "(b)" para los perks del mercado — no hay un segundo coste de oportunidad, es el mismo slot. Solo toca el **mercado**: el objeto de recompensa no pasa por ningún listón y este paso no lo cambia.
- `RunPolicyOptions.MinItemValueMarket` (`int?`) y `--min-item-value-market N` en `/Balance`, gemelos exactos de `MinPerkValueMarket`, para poder fijar un listón constante y barrerlo en la medición.
- Tests de punta a punta (`Sim.Tests/Analysis/RunPolicyItemSlotTests.cs`, 7 tests): las doctrinas puras nunca se bloquean, una tabla vacía no bloquea, un listón por encima del nivel cierra el mercado de objetos a cero, el corte está exactamente en `MeanValue` (`>=`), y el listón real del slot adelgaza el canal sin cerrarlo.

## Lo que se mide

`--full-runs 1.200`, dos semillas, listón real (por defecto) contra el listón forzado abierto (`--min-item-value-market -1000000`, el mercado de objetos de antes de este paso):

| Semilla | `runWinRate` gate | sin gate | Δ | `itemsOnRoster` gate | sin gate | `perksOnStarters` gate | sin gate | `mastersReached` gate | sin gate |
|---|---|---|---|---|---|---|---|---|---|
| 1 | 20,83 | 19,83 | +1,00 | 1,28 | 1,88 | 9,08 | 8,53 | 27,67 | 17,50 |
| 7 | 19,83 | 19,67 | +0,16 | 1,23 | 1,85 | 8,93 | 8,53 | 26,25 | 19,17 |

El efecto **estructural** es consistente en las dos semillas y es la afirmación central de esta ADR: el oro que antes se iba en un objeto que no valía el slot pasa a más perks en los titulares y, sobre todo, a bastantes más maestros desbloqueados (+10,17 y +7,08). `contextualAdvantage` sube en las dos (1,42 → 2,42 y −0,75 → −0,58), sin salir de su banda abierta (AD-D, objetivo 8). El efecto sobre `runWinRate` en sí es consistentemente positivo pero de magnitud muy ruidosa entre semillas (+1,00 frente a +0,16): es precisamente la métrica que AV-A ya identificó como necesitada de un banco de decenas de miles de runs para resolverse a tres errores típicos, y dos semillas de 1.200 no lo cambian. **Esta ADR no reclama cerrar AV-A**; deja anotado que el punto estimado se mueve en la dirección que AV-A necesita, para que una remedición futura de esa entrada lo tenga en cuenta.

Ninguna métrica sale de su cota de no regresión: `FullRunGateTests` (240 runs/doctrina, semilla 1, corredores deliberadamente anchos de la ADR 0037/§19) pasa 15/15 con el gate activo por defecto.

## Por qué mejora en vez de solo redistribuir

El gate no prohíbe comprar: cuando cierra, el turno de mercado sigue en `NextMarketAction` y el oro que no se lleva un objeto mediocre queda disponible para el siguiente perk o fichaje que sí pase su propio listón. Como el listón de perk (ADR 0072) y el de objeto comparten el mismo `bar` —el mismo coste de oportunidad del mismo slot—, lo que el paso 2 hace es dejar de gastar un slot barato en el objeto medio (+37) cuando el coste de oportunidad del momento vale más que eso, exactamente la comparación que AT-A pedía y que antes no existía.

## Alternativas descartadas

- **Gate por objeto concreto**, como `WorthASlot` con los perks. Descartado explícitamente por el revisor: la tabla de AT-A no separa la dispersión entre objetos (27) del ruido de fila (21) a la muestra medida (2.016 partidos/objeto); preguntar por el objeto concreto ordenaría ruido.
- **Remedir con muchas más parejas antes de implementar nada.** Es la alternativa que el revisor rechazó a favor de avanzar con el nivel agregado; sigue disponible como paso futuro si alguna decisión llegara a necesitar el orden objeto a objeto.
- **Extender el gate a la recompensa.** Fuera de alcance de este paso: el objeto de recompensa no cuesta oro, así que el argumento de "slot MÁS oro" no se le aplica igual, y tocarlo es una decisión aparte.

## Consecuencias

- AT-A cerrada (los dos pasos). `docs/pendientes.md` recoge el cierre y añade la referencia cruzada a AV-A.
- `--min-item-value-market` queda disponible en `/Balance` para cualquier remedición futura del listón de objetos.
