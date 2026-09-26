# BL-B — El umbral de la ADR 0087 no vale para las filas de varianza alta

Estado: **ABIERTA** (26 sep 2026). Encontrado por la revisión independiente de [BL-A](./BL-A.md).

## Observación

La ADR 0087 trata como perjudicial un perk que vale por debajo de −`rowDeviation` (7). Ese 7 es la
desviación **media** de la tabla entera, y hay filas con varianza propia mucho mayor:

- `ankle_bite`: sd ≈18 por lote de 192 plantillas (ocho semillas), así que su ET en un lote de dos semillas
  es ≈13. Se retiró en `0380220` por un −16 que resultó ser ruido (BL-A).
- `bull_rush`: −8 con las semillas 5+11, y 0 con cuatro semillas.

Cualquier fila entre −7 y −15 puede ser igual de espuria. Las perks de lesión y las de evento raro son las
candidatas: pocas activaciones con mucho efecto cada una.

## Hermanos del mismo instrumento

- El espacio de semillas `índice*1000 + 500 + r` colisiona a partir de 500 plantillas. Los dos medidores de
  `/Balance` ya lo impiden (`MaxRosters`, `ValueRunnerSeedSpaceTests`), pero los arneses de `Sim.Tests` que
  copian el patrón no tienen guarda: `ActionHistogramTests`, `PerkSituationCensusTests`, y sin el +500
  `PerkInjuryCensusTests` y `BallSpeedCensusTests`. Ninguno pasa hoy de 500.
- `perk-values.json` no guarda procedencia por fila (protocolo, n, ET). Una fila medida aparte, como
  `ankle_bite`, solo lo avisa en el `_doc`, y un validador no puede comprobarlo.

## Hipótesis de arreglo (sin decidir; cambian una ADR, así que requieren ADR)

- Umbral por fila: retirar solo si el valor está por debajo de −k·ET de esa fila, con el ET sacado de la
  discrepancia entre semillas de esa misma fila.
- Antes de retirar una perk, segunda medición obligatoria con cuatro semillas más.

## Resuelto (26 sep 2026): el umbral

[ADR 0150](../decisiones/0150-un-perk-negativo-se-confirma-antes-de-retirarlo.md): con dos lotes, una fila por
debajo de −`rowDeviation` es un **candidato** (`toConfirm` en `tools/perk-values-table.py`). Se confirma con
≥ 4 lotes y `tools/perk-value-confirm.py`, usando el ET de esa fila y la t de Student. Validado contra los
lotes de BL-A, y endurecido tras la revisión independiente: rechaza lotes repetidos, de más de 500 plantillas
o de tamaños mezclados, y admite `--sd-floor`. **Siguen abiertos los hermanos**:

- la guarda de 500 plantillas en los arneses de `Sim.Tests`;
- la procedencia por fila que pueda comprobar un validador;
- el **techo positivo**: la ADR 0087 recortó por encima de +150 con la misma tabla de dos lotes;
- `RunPolicy.MeasuredValueFor` (`Sim/Analysis/RunPolicy.cs`, ADR 0072) usa el `rowDeviation` global como
  sigma de todas las filas, así que las filas ruidosas se encogen de menos;
- `data/economy/item-values.json` (`rowDeviation` 29) tiene la misma estructura y no está cubierto.
