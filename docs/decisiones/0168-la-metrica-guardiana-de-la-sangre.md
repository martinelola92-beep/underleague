# ADR 0168 — La métrica guardiana de la sangre

Fecha: 29 sep 2026 · Estado: **aceptada**. **Decisión del revisor** (`docs/plan-diversion.md` §0: «Métrica guardiana de
la sangre: sí»; §5.9: «se mide en cada paso»). La definición y las bandas son decisión propia, sin consultar, dentro de
esa autorización (memoria: el revisor autoriza mover rangos con ADR y datos, RT-057). **Requisitos:** RT-054, RT-056,
RT-057. **Relacionada:** ADR 0048 (un sano puede morir; `deathsPerRun` 1,5-3), ADR 0082 (`injuriesPerMatch`), ADR 0167.

## Por qué

La identidad de Underleague no es el fútbol, es **la carnicería administrada** (CLAUDE.md): el desgaste de la plantilla
es el recurso central. Cada cambio de balance o de diversión puede lavarla sin que nada avise. El plan de diversión lo
pidió como puerta.

## Lo que había (Regla G)

- `deathsPerRun` (1,5-3, ADR 0048): sólo muertes, y con un punto ciego (abajo).
- `injuriesPerMatch` (0,3-0,9, ADR 0082, `MatchMetrics`, `docs/balance.md`): lesiones de partido **de los dos
  equipos**, leves incluidas, sin run.
- Sin banda: `injuriesPerMatchBothTeams`, `ownInjuriesPerMatch`, `severeInjuriesPerRun`.

Nada mira la sangre **propia y grave** (lo que desgasta la run) ni si una run avanza sin ella.

## Decisión

**Instrumento** (`Sim/Analysis/BloodCasualtyCounter.cs`): en cada partido de la run —también el que la termina—, los
jugadores propios **distintos** que sufren una lesión grave o mueren, contados desde los eventos del partido (no
anulados). Una grave que acaba en muerte es una baja; una lesión leve, también la de la turba (ADR 0167), no cuenta.
Test de respuestas sabidas: `BloodCasualtyCounterTests`. Columna `bloodCasualties` en `runs.csv`.

Dos filas en `FullRunMetrics` y la puerta `FullRunGateTests.TheBloodIsNeverWashedOut`:

| métrica | definición | banda | procedencia (Regla H) |
|---|---|---|---|
| `bloodPerMatch` | bajas de sangre propias / partidos jugados | ≥ 0,27 | base 0,34 (tabla); suelo al 80 %, **provisional** |
| `bloodlessPastAct1Share` | de las runs que superan el jefe del acto 1, % sin ninguna baja de sangre | ≤ 7 % | base 2,7-4,0 %; la puerta ve ~500 runs que pasan el acto 1 (error típico ≈ 0,8) y las rápidas ~125 (≈ 1,5): el techo queda a ≥ 2,7 errores típicos, **provisional** |

## Línea base (29 sep 2026)

`/Balance --full-runs 600 --seed {1,2}` sobre la rama con las ADR 0165-0167 (el instrumento no cambia el juego:
`runWinRate` 15,50 / 18,33, idéntico al lote sin él). Sobre las 1.800 runs de cada lote:

| | semilla 1 | semilla 2 |
|---|---|---|
| bajas de sangre por partido | 0,341 | 0,343 |
| runs que superan el acto 1 | 1.258 | 1.291 |
| de ellas, sin ninguna baja de sangre | 3,97 % | 2,71 % |
| runs con menos bajas de sangre que muertes (caso imposible: validación) | 0 | 0 |

**Lectura:** casi toda run que pasa del acto 1 sangra (97 %). La sangre propia es ~1 baja cada tres partidos.

No hay techo de sangre ni suelo de runs sin sangre: el exceso ya lo vigila `deathsPerRun` ≤ 3; esta puerta sólo impide
que la carnicería se lave.

## Lo que cambió tras la revisión independiente (29 sep 2026)

La primera versión de esta ADR medía `(muertes + subida de graves por nodo) / partidos` y `% de runs sin sangre` sobre
todas las runs. La revisión lo desmontó, con datos:

- **El instrumento era otro del que decía la ADR.** La subida de graves sólo se contaba en nodos de partido (no en
  cartas: la ADR afirmaba lo contrario), no veía el partido que termina la run, y una grave nueva quedaba tapada si otra
  grave moría en el mismo partido o un mercenario grave se iba. La «validación» que hice (88 runs con más graves que
  lesiones de partido, «por las cartas») era falsa: 87 de las 88 eran derrotas, porque `OwnInjuries` no cuenta el partido
  final. **REJECTED** mi explicación; el instrumento nuevo cuenta desde los eventos.
- **`% de runs sin sangre` medía la dificultad del jefe 1**, no la carnicería: el 76 % de las runs que mueren en el jefe 1
  no tienen sangre, y en runs de 10+ partidos sólo el 4 %. Endurecer el jefe 1 la pondría en rojo sin tocar la
  identidad. Ahora se mide sólo sobre las runs que superan el acto 1.
- **El error típico citado era de otra muestra**: la puerta juega 3 × 240 runs con semillas compartidas entre doctrinas.

## Hermanos anotados (sin arreglar aquí)

- `deathsPerRun` y el nuevo instrumento sólo ven **partidos**: las muertes del matasanos (clínica) y de cartas no cuentan
  en ninguna métrica de campaña. La carnicería de fuera del campo existe y no se vigila.
- `severeInjuriesPerRun` sigue con el contador de subida por nodo, sesgado como se describe arriba.
