# BB-R — Dos perks dicen ser MAESTROS y no exigen ni cierran nada

**Estado:** **CERRADA (30 sep 2026): la premisa era falsa, REJECTED.** `blood_tithe` y `first_touch_school`
SON maestros en el dato desde `d216e1a` (ADR 0051): declaran `requiresPerks` y `blocksPerks`, el cargador
los lee, el pool los exige y los cierra, y la descripción generada lo dice. El aviso que la ficha dejó en
sus dos `_doc` era el falso, y se ha retirado. Ver «Resolución» al final; lo de abajo es el texto original,
que se conserva porque el error es de método (Regla J) y merece quedar.

## Observación

`blood_tithe` y `first_touch_school` describen en su `_doc` un rol de **maestro** de la ADR 0051:

- `blood_tithe`: *"MAESTRO de La Carnicería (ADR 0051), el rompe-reglas de los cuatro. **Exige dos perks
  de su línea** como los demás…"*
- `first_touch_school`: *"MAESTRO de El Toque (ADR 0051). **Exige dos perks de su línea y cierra La
  Carnicería**: el equipo que juega bien renuncia a lesionar."*

En el dato, los dos declaran **`requires: null` y `blocks: null`**. No exigen nada y no cierran nada. Solo
tienen `family` (`butchery` / `craft`), que agrupa pero no obliga.

**Medido:** ningún perk del catálogo (0 de 94) declara `requires` ni `blocks`. La maquinaria de maestros
existe en `/Sim` (`PerkDefinition.IsMaster`, `DescriptionGenerator.DescribeArc`, `PerkLoader`,
`layout.requiresSuffix`/`blocksSuffix`) y **no tiene ni un solo consumidor**.

## Por qué importa

1. **El jugador lee una promesa que no se cumple.** La descripción generada no menciona ninguna exigencia
   ni ningún cierre —porque no los hay—, pero el `_doc` de diseño sí, y el catálogo derivado
   (`docs/catalogo-perks-y-objetos.md`) se regenera de ahí.
2. **La ADR 0051 no está implementada en datos.** «Los maestros son entre el 5 % y el 10 % del catálogo»
   dice la regla; hoy son el 0 %.
3. **Tercer caso del mismo patrón en un día**, tras `steamroller` (`stat(target,'down')` imposible, BB-Q)
   y `shadow` (`links: ahead` que nunca se consulta): **el `_doc` describe un mecanismo que el dato no
   tiene**. Los tres se han encontrado mirando datos, no jugando. Conviene una pasada sistemática.

## Lo que hay que decidir (no es un arreglo mecánico)

- ¿Se rellenan `requires`/`blocks` en estos dos y se cierran las líneas `butchery`/`craft` como dice la
  ADR 0051? Eso son **cambios de balance reales** —un perk que cierra una línea para el resto de la run
  es irreversible (RF-072)— y pasa por `game-design-review`.
- ¿O se retiran las frases de maestro de los dos `_doc` y la ADR 0051 queda como pendiente de fase?

**No se toca nada hasta que lo decidas.** Lo que sí hay que evitar es dejarlo como está: el texto de
diseño y el dato dicen cosas distintas.

## Hermanos

- `docs/pendientes/BB-Q.md` — mismo patrón, `steamroller`.
- `docs/analisis/informe-decision-catalogo.md` — `shadow`, mismo patrón.


## 19 sep 2026 — mitad segura aplicada

Los `_doc` de `blood_tithe` y `first_touch_school` conservan su descripción de diseño —es información
útil, y borrarla perdería el porqué— pero ahora llevan detrás un aviso explícito de que **hoy no exigen ni
cierran nada**, con el número: **0 de 102 perks** declaran `requires`/`blocks`, cuando la ADR 0051 pide
entre el 5 % y el 10 % del catálogo.

Se elige avisar en vez de borrar la frase porque el problema no es que la intención esté escrita, es que
se leía como si estuviera implementada. Un lector del catálogo generado ya no puede confundirlas.

**Lo de fondo sigue abierto y es tuyo**: rellenar `requires`/`blocks` es un cambio de balance real —un
cierre de línea es irreversible dentro de la run (RF-072)— y necesita su ADR. La alternativa es aparcar la
ADR 0051 explícitamente y quitar la promesa de los dos textos.


## Resolución (30 sep 2026) — REJECTED, y por qué el instrumento mentía

**Experimento que la tumba (CONFIRMED).** `BuildArcTests.TheTwoPerksThatSayTheyAreMastersAreMastersAndTheirDocDoesNotSayOtherwise`
carga el catálogo real por el cargador de producción y comprueba, para los dos perks: `IsMaster`, `Family`,
`Requires = (línea propia, 2)`, `Blocks.Families = [la opuesta]` y que están en `Catalog.Perks.Masters`. Pasa.
`git show 4462369^:data/perks/blood_tithe.json` (el árbol **anterior** al commit que abrió esta ficha) ya
traía `"requiresPerks": { "family": "butchery", "count": 2 }` y `"blocksPerks": { "families": ["craft"] }`;
llevaban ahí desde el primer commit de los arcos.

**Causa del error (Regla J, instrumento sin validar).** La ficha midió «0 de 94 perks declaran `requires`
ni `blocks`» buscando esas claves. Las del dato se llaman **`requiresPerks`** y **`blocksPerks`**. Nadie
contrastó el instrumento con un caso cuya respuesta se conociera (un perk que se sabe maestro), que es
exactamente lo que la regla pide. La consecuencia fue un aviso falso escrito en dos ficheros de `/data`,
propagado a `docs/catalogo-perks-y-objetos.md`.

**Estado real de la maquinaria (no «sin un solo consumidor»)**: cuatro maestros (`blood_tithe`,
`first_touch_school`, `granite_line`, `killing_range`), uno por línea (`butchery`, `craft`, `wall`, `aim`),
con pool (`PerkPool.Availability`/`ClosedBy`), política de run (`RunPolicy`), pantalla de recompensa
(`RewardView`), descripción generada y `PerkArcTests` sobre el catálogo real.

**Qué se hizo**: se retiró el aviso falso de los dos `_doc` (dato sin efecto en partido: sólo un campo de
documentación) y se añadió el test de arriba, que falla si un `_doc` de maestro vuelve a decir lo que el
dato no dice.

**Lo que SÍ queda de la ficha, y es otra cosa** *(LIKELY, sin medir su importancia)*: la ADR 0051 acota los
maestros al **5-10 %** del catálogo y hoy son 4 de 111 = **3,6 %**; `MastersAreASmallShareOfTheCatalog`
acepta desde el 3 %, cifra sin procedencia escrita (Regla H). No es un perk que incumple su texto; es una
banda de la ADR que el catálogo no cumple, y subir maestros es una decisión de diseño de línea (cada uno
cierra otra línea para siempre, RF-072), no un arreglo de esta ficha. Anotado aquí para quien abra ese
frente.
