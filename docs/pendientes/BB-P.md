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
   ajeno de `/Sim`, como se hizo para BA-N) antes de decidir si son ruido o señal — barato: cinco
   `dotnet test` en worktrees por puerta, no un experimento nuevo que diseñar.
2. **Corregido tras una segunda revisión**: la primera versión de este punto proponía "pasar a diferencia
   emparejada, patrón de la ADR 0087" para `EquipmentImpactTests.cs` — pero esa puerta **ya** empareja por
   semilla (mismo `rosterSeed`, mismas semillas de partido, mismo rival en los dos brazos); no había
   margen ahí. Lo que de verdad falta, y es más barato: que cada puerta **calcule e imprima su propia
   dispersión** entre las 96 diferencias por plantilla que ya calcula (la RMS, igual que `rowDeviation` en
   la ADR 0087), en el mismo `_output.WriteLine` que ya usa. Eso habría evitado reconstruir cinco commits
   para conocer el ~0,9 de `EquipmentImpactTests.cs`, y convierte este punto 1 en segundos por puerta en
   vez de una campaña de remedición. Subir la muestra (`Rosters`, precedente en los paquetes Z y AZ de ese
   mismo fichero) sigue siendo la opción si la dispersión autoinformada confirma que hace falta más
   potencia, pero es la segunda opción, no la primera.
3. Ninguna corrección de umbral se decide por extrapolar un precedente de otra puerta (el error exacto que
   cometió la primera versión de la ADR 0116): cada puerta necesita su propio error típico medido.
4. Barrido pendiente y barato, hermano de este mismo hallazgo: comprobar si otras ADR de `docs/decisiones/`
   citan un "medido: X" anclado a un árbol que ya no es vigente, como hacía la primera versión de la ADR
   0116 con su "1,7 medido" de once commits atrás. No se ha hecho todavía.
5. **Decisión de diseño pendiente, no solo de instrumento** (candidata a `game-design-review`, Regla B):
   `EquipmentImpactTests.cs` con umbral 1,0 detecta del orden de 87 % si equipar dejara de aportar nada,
   pero solo del orden de 41 % si su aporte cayera a la mitad — ¿es aceptable que la puerta que vigila el
   escalón "muy buena" de la ADR 0033 deje pasar más de la mitad de una degradación a la mitad? No se
   decide aquí; queda como pregunta abierta, no como conclusión silenciosa dentro de la ADR 0116.

## Hermanos

- `docs/pendientes/BA-N.md`, `docs/pendientes/BA-M.md` — el caso ya resuelto que reveló el patrón.
- `docs/decisiones/0115-la-barrera-de-reanudacion-cubre-las-cinco-no-solo-la-falta.md` — ya lo anotaba en
  general ("la firma de puertas de un solo partido/semilla operando cerca de su margen") sin abrir ficha.
- `docs/decisiones/0087-el-valor-de-un-perk-se-mide-contra-su-control.md` — el patrón de `rowDeviation`
  (dispersión calculada dentro de una sola ejecución) que las puertas de tasa agregada no usan todavía,
  no el emparejamiento por semilla, que `EquipmentImpactTests.cs` ya tiene.
