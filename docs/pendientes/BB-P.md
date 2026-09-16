# BB-P — Las puertas de un solo partido/semilla se leen como causa cuando son ruido de muestreo

**Estado:** Abierta. Patrón confirmado, sin arreglar todavía en ningún caso concreto.

## Observación

Encontrado por el `independent-reviewer` al revisar BA-N/ADR 0116 (16 sep 2026): varias puertas
estadísticas del proyecto son una diferencia de dos tasas medida sobre un número de partidos fijo, sin
más de una semilla ni comparación emparejada, y con un error típico de muestreo (~0,9 puntos para
`EquippingAGoodBuildIsWorthSeveralPointsOfWinRate`, sin medir para las demás todavía) que es del mismo
orden que el margen del propio umbral. Cuando eso pasa, **cualquier cambio en `/Sim` que no toque ni de
lejos el sistema que la puerta mide puede moverla lo suficiente para cruzar el umbral**, y esa
correlación temporal ("cambié X, la puerta se movió") se lee como causalidad sin comprobar si el
movimiento ocurre igual sin X.

## Caso confirmado con medición directa

`EquippingAGoodBuildIsWorthSeveralPointsOfWinRate`, con el catálogo de perks **congelado** (mismo
`/data/perks/`, verificado byte a byte) en cinco commits que solo cambiaban código de `/Sim` ajeno a
objetos y perks: 1,7 / 2,1 / 1,7 / 3,0 / 3,4. Media 2,38, desviación muestral 0,78 — coincide con el error
típico teórico (~0,9) del tamaño de muestra de esa puerta. Detalle completo en
`docs/decisiones/0116-el-escalon-de-equipar-se-recalibra-contra-94-perks.md` y en `docs/pendientes/BA-N.md`
y `docs/pendientes/BA-M.md` (donde el mismo patrón produjo dos hipótesis causales falsas antes de esta
corrección).

## Candidatas sin confirmar todavía

Las 4 puertas que quedaron rojas al cerrar BB-B (`docs/decisiones/0115-la-barrera-de-reanudacion-cubre-las-cinco-no-solo-la-falta.md`),
cada una moviéndose de composición distinta en cada corrección real de `/Sim` de esa sesión sin que
ninguna corrección tocara los builds concretos que fallan:

- `TheThreeDoctrinesBuyDifferently` (`contextual` vs `saver`, compras por mercado)
- `CoherentBuildsBeatTheirBaseline` (`orc_violence`, 56,67 contra suelo 58,00)
- `BadBuildsLoseToTheirBaseline` (`elf_out_of_zone`, 49,38 contra techo 45,00)
- `NoGateMetricIsOutOfRange` (agrega las dos anteriores)

Ninguna de las cuatro tiene todavía su error típico medido de la misma forma que se hizo para la de
equipar. No se puede afirmar sin medirlo que sean el mismo patrón — es la hipótesis a comprobar primero,
no una conclusión.

## Qué haría falta, sin implementarlo aquí

1. Medir el error típico de cada una de las cuatro candidatas (congelando lo que miden y variando código
   ajeno de `/Sim`, como se hizo para BA-N) antes de decidir si son ruido o señal.
2. Para las que resulten ruido: subir la muestra (el proyecto ya tiene el precedente, paquetes Z y AZ de
   `EquipmentImpactTests.cs`) o, mejor, adoptar el patrón de diferencia **emparejada** contra un control
   que ya usa la ADR 0087 para el valor de un perk — reduce la varianza sin más partidos.
3. Ninguna corrección de umbral se decide por extrapolar un precedente de otra puerta (el error exacto que
   cometió la primera versión de la ADR 0116): cada puerta necesita su propio error típico medido.

## Hermanos

- `docs/pendientes/BA-N.md`, `docs/pendientes/BA-M.md` — el caso ya resuelto que reveló el patrón.
- `docs/decisiones/0115-la-barrera-de-reanudacion-cubre-las-cinco-no-solo-la-falta.md` — ya lo anotaba en
  general ("la firma de puertas de un solo partido/semilla operando cerca de su margen") sin abrir ficha.
- `docs/decisiones/0087-el-valor-de-un-perk-se-mide-contra-su-control.md` — el patrón de instrumento
  (diferencia emparejada) que probablemente resuelve esto sin subir la muestra.
