# BB-V — Nada comprueba que `docs/catalogo-perks-y-objetos.md` esté al día con `/data`

**Estado:** abierta. Descubierta el 3 oct 2026 al regenerar el catálogo por BB-U (`immovable`).

## Observación

`docs/catalogo-perks-y-objetos.md` se genera con `tools/generate-catalog.py` a partir de `/data` y de
`Balance --describe es`, pero **ningún test ni hook compara el fichero con lo que saldría de regenerarlo**. Al
regenerarlo para añadir `immovable`, el diff arrastró **cuatro descripciones ajenas al perk** que ya estaban
desfasadas respecto al generador (RT-035), es decir, cuyo efecto había cambiado sin que nadie regenerase el catálogo:

- `point_blank` («Al tirar…» pasa a «Al rechazarse su tiro…»),
- `double_shot` (igual),
- `ankle_bite` (gana «…y el portador multiplica por 4 sus opciones de hacer falta»),
- `last_man` («Al tirar, se reubica…» pasa a «Cuando un tiro rival va a puerta, se interpone…»).

El documento es de consulta de diseño («derivado de `/data`, se regenera, no se edita a mano»), así que un catálogo
viejo hace decidir sobre un efecto que ya no existe.

## Por qué importa

Es el mismo patrón que la Regla G con otra cara: una copia derivada que nadie contrasta. Cada cambio de efecto en
`/data/perks/` la deja mentir hasta el siguiente que se acuerde.

## Propuesta (no implementada)

Un test (o paso de `DataValidator`) que genere el catálogo en memoria y lo compare con el fichero versionado, o
al menos que compare el recuento y las descripciones de `--describe es`. Barato, determinista, sin `/Balance`.
