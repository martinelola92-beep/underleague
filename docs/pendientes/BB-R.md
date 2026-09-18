# BB-R — Dos perks dicen ser MAESTROS y no exigen ni cierran nada

**Estado:** Abierta. Detectada al preparar el cuadre de perks por raza/rasgo (19 sep 2026). Sin arreglar:
es una decisión de diseño, no un bug de implementación.

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
