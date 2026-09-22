# 0125 — La entrada sin balón tiene métrica propia y bono por puesto

Estado: **Aceptada** (22 sep 2026, **decisión del revisor**, ficha `docs/pendientes/BE-A.md`). Cambia una
regla de juego y un instrumento de medida, así que pasa por `game-design-review` (esta nota) y exige
`balance-measure` antes de cerrarse. **Enmienda la ADR 0105 §2.**

## Problema

`Utility.IsDefensiveRole` admitía **solo** `Position.Defender`, mientras su propio comentario decía
«defensa y centrocampista» citando la ADR 0105 §2. El revisor pide que **todos** los jugadores de campo
puedan entrar a su marcado sin balón, con los defensas y los más agresivos como los más probables.

**Medido antes de decidir nada** (500 partidos, semilla 1, contra el control de la misma semilla):

| configuración | `tacklesPerMatch` | `injuriesPerMatch` | reparto por puesto (entradas/partido-jugador) |
|---|---|---|---|
| **base: solo defensa** (el código) | **12,01** | **0,81** | DEF 1,95 · MID 0,64 · FWD 0,20 |
| DEF + MID (lo que la ADR 0105 **declaraba**) | 23,86 | 1,23 | MID **2,76** · DEF 1,73 |
| todos los roles | 27,64 | 1,34 | MID **2,74** · FWD 2,29 · DEF 1,64 |
| todos, bono 40 | 17,37 | 1,04 | MID 1,62 · FWD 1,44 · DEF 1,18 |
| todos, enfriamiento 540 (36 s) | 18,24 | 0,96 | MID 1,74 · FWD 1,49 · DEF 1,20 |

**Dos hallazgos, y los dos contradicen la intuición de partida:**

1. **La regla que el comentario describía habría roto el juego.** DEF+MID da 23,86 entradas contra un techo
   de 14. La implementación más estrecha del código es lo que estaba sosteniendo la banda.
2. **El orden sale invertido en todas las variantes que incluyen al centrocampista.** No lo arregla bajar el
   bono ni subir el enfriamiento. La causa no es el peso de `Tackle` sino **con qué compite y a quién
   marca**: el defensa tiene `CoverSpace` 420 y `Retreat` 320 disputándole la decisión y marca a un
   delantero que suele estar fuera de la jugada activa; el centrocampista marca a otro centrocampista, que
   está casi siempre dentro.

## El error de medida que había debajo

`MatchEngine` **ya distingue** las dos entradas —`TackleOffBall`, `OffBallFoulBase`, enfriamiento propio
(`OffBallTackleCooldownTicks`), detalles de evento `offBallFoul`/`offBallMissed`— pero
**`_report.Tackles++` se incrementa en las dos ramas** (`:2201` y `:2209`), así que `tacklesPerMatch` las
suma juntas.

Y `tacklesPerMatch` tiene banda 6-14 **como métrica de fútbol**: mide disputar el balón. *Golpear a quien no
lo lleva no es disputarlo.* Medir las dos cosas con la misma regla hacía que cualquier apertura de la
entrada sin balón saliera «fuera de banda» por construcción, midiera lo que midiera.

## Decisión

### D1 · La entrada sin balón se mide aparte

- `tacklesPerMatch` pasa a contar **solo la entrada al portador**. Conserva su banda 6-14 de RT-056, que es
  la que se calibró para eso.
- Se añade **`offBallTacklesPerMatch`**, al principio como `INFO` **sin banda**: no se inventa un rango
  antes de tener la distribución. La banda se fija en una ADR posterior, con datos.
- El motor no cambia: la información ya existe, solo deja de perderse al agregar.

*Esto no es un ajuste de balance: es arreglar el instrumento. Cualquier lectura anterior de
`tacklesPerMatch` que incluyera entradas sin balón mezclaba dos poblaciones.*

### D2 · El bono de entrada sin balón pasa a ser por puesto

Hoy `tackleMarkTargetBonus` es **un entero plano** que se suma **fuera** del multiplicador de puesto y
rasgo. Por eso un delantero con `Tackle` base 128 recibe el mismo empujón que un defensa con 255, y por eso
la diferenciación que el revisor pide **no puede salir**: el bono plano la borra.

Pasa a ser un **mapa por puesto**, igual que ya lo es `base` en `data/ai/weights.json` (`Goalkeeper`,
`Defender`, `Midfielder`, `Forward`). Es la única estructura del fichero que ya está parametrizada por
puesto, así que no se inventa un patrón: se aplica el que hay.

La fantasía que expresa, en palabras del revisor: **«tú no pasas de aquí y no vas a participar en la
jugada, puñetazo.»** Es una acción de **negación**, no de recuperación — y por eso merece su propia métrica
(D1) y su propio reparto (D2).

### D3 · Los tres roles de campo pueden, el portero no

`IsDefensiveRole` deja de ser una guarda por rol y pasa a decidirlo el dato: un puesto con bono 0 no entra
nunca. El portero queda fuera por `IsOutfield` —no tiene marca asignada—, no por esta guarda.

## Las diez preguntas, en corto

1. **Qué experimenta**: un jugador que no puede llegar al balón golpea al rival al que marca. Observable,
   con falta propia y sanción propia.
2. **Qué decide hoy**: nada — era invisible y solo del defensa.
3. **Qué debería decidir**: alinear a un agresivo en una banda concreta es aceptar que repartirá leña ahí.
4. **Qué regla representa**: enmienda la ADR 0105 §2. RF-057 sigue acotándola (`IsInActivePlay`).
5. **Sistemas**: `/Sim` (`Utility`, `MatchMetrics`), `/data` (`ai/weights.json`, su esquema). Nada en `/Game`.
6. **Alternativas descartadas**: subir el enfriamiento (medido: a 540 ticks, 36 s, sigue fuera de banda y
   sigue invertido) · bajar el bono plano (satura en 17,4) · dejarlo solo en defensa (pierde la intención).
7. **Trade-off**: más violencia legible a cambio de presupuesto de lesiones, que es el recurso más escaso.
8. **Estrategias**: da al centrocampista un papel en la fase sin balón, que es un hueco abierto del
   proyecto (dispara el 6,5 % de los tiros siendo el 43 % de los jugadores de campo).
9. **Degeneración**: el riesgo real es `injuriesPerMatch` (0,81 con techo 0,90) y, detrás, `deathsPerRun`.
   **Es la restricción que manda, y no es un artefacto de medida**: las entradas sin balón que son falta
   pasan por `ResolveInjury` como cualquier otra.
10. **Cómo se demuestra**: reparto por puesto con el orden pedido (defensa por delante), las siete métricas
    obligatorias `IN` con la definición nueva, y las 43 puertas medidas **contra una línea base tomada con
    el instrumento ya corregido** — no contra las cifras anteriores, que mezclaban poblaciones.

## Riesgo declarado

**`injuriesPerMatch` es la restricción que decide si esto cabe.** Con todos los roles abiertos está en 1,34
contra un techo de 0,90, y **eso no lo arregla D1**. Si con el bono por puesto calibrado no cabe, las
salidas son, por este orden: reducir el bono hasta que quepa, dejar al delantero en 0, o abrir una ADR para
recalibrar la banda —lo que arrastra `deathsPerRun` y es decisión del revisor, no de esta nota.

## Hermanos

`docs/pendientes/BE-A.md` · ADR 0105 (enmendada) · `docs/analisis/auditoria-conceptual-narrativa.md` §E

---

## Enmienda de medida — D1 implementada y medida (22 sep 2026)

**D1 está implementada; D2 y D3 no.** `report.Tackles` y `PlayerMatchStats.Tackles` cuentan desde hoy solo
la entrada al portador; la entrada al marcado sin balón va en `OffBallTackles` y sale como
`offBallTacklesPerMatch` (INFO, sin banda). La partición está probada por test contra el flujo de eventos,
en el agregado y por jugador (`MatchRulesTests.CarrierAndOffBallTacklesArePartitionedNotMixed`).

**La separación no mueve el juego** *(CONFIRMED)*: sobre el mismo lote de 500 partidos con el que se
escribió esta ADR, las otras 24 filas de `summary.csv` salen idénticas a la línea base y, partido a
partido, **todas** las columnas de `matches.csv` coinciden en los 500, con
`base.tackles == tackles + offBallTackles` exacto (verificado por la revisión independiente). Es
contabilidad, no comportamiento.

**Aviso sobre estas cifras: son de la semilla 1, que es el extremo del rango.** En `reference.json` la
semilla genera también las plantillas; con cinco, `offBallTacklesPerMatch` recorre **0,99-5,09**,
`tacklesPerMatch` **6,92-10,42** e `injuriesPerMatch` **0,43-0,81**. La banda de la métrica nueva se fija
en una ADR posterior y **con varias semillas** (precedente: ADR 0082, tres).

| | `tacklesPerMatch` | `offBallTacklesPerMatch` | `injuriesPerMatch` | `foulsPerMatch` |
|---|---|---|---|---|
| base (mezcladas) | 12,01 | — | 0,81 | 7,50 |
| separadas (D1) | **6,92** | **5,09** | 0,81 | 7,50 |
| `tackleMarkTargetBonus` 0 | 7,78 | 0,98 | 0,70 | 4,87 |

**Tres correcciones a lo que esta ADR daba por supuesto**, todas medidas y detalladas en
`docs/pendientes/BE-A.md`:

1. **El valor separado es 6,92, no ~9,19.** El 9,19 que circulaba era otro experimento (la métrica
   mezclada con la entrada sin balón apagada), no una separación.
2. **El margen de `tacklesPerMatch` está por abajo, no por arriba.** 6,92 con suelo 6,00 deja 0,92, y la
   entrada sin balón **sustituye** disputas además de sumarse (apagándola suben a 7,78). El riesgo de
   techo que describe la sección «Riesgo declarado» se midió con el instrumento mezclado; el riesgo real
   de D2 sobre esta métrica es el suelo. `injuriesPerMatch` (0,81 / 0,90) sigue siendo la restricción que
   manda, y de ella la entrada sin balón vale hoy ~0,11.
3. **D3, tal como está escrita, no se cumple sola.** «Un puesto con bono 0 no entra nunca» es falso: con
   `tackleMarkTargetBonus` en 0 quedan 0,98 entradas sin balón por partido, porque `offBall` es la
   intención con la que se decidió la entrada y esa intención puede ganar la utilidad sin bono. Si D2
   sustituye la guarda de rol por el mapa de bonos, hace falta **descalificar explícitamente** el bono 0.

**Una decisión queda abierta y va con D2**: `extraAction` sobre `TACKLE`/`RECOVERY` (`charge`,
`steamroller`) está clasificado como medible con `tacklesPerMatch`, pero `RepeatTackle` marca la
repetición como sin balón salvo que el nuevo objetivo lleve el balón, así que en el caso común el efecto
cae ahora en la métrica **sin banda**. No se ha tocado la clasificación en este paquete: cambiarla mueve el
inventario de perks medibles que `PerkAuditTests` fija con cuentas exactas, y eso es protocolo (RT-057).

**Mantener la banda 6-14 es una decisión, no una consecuencia.** La población es la misma clase de suceso
que la calibró —antes de la ADR 0105, `tacklesPerMatch` era ya solo la entrada al portador y valía 9,19—,
pero hoy esa población corre un tercio más abajo. La banda se conserva porque nada la ha invalidado, no
porque el número encaje holgado: **el suelo de 6,00 es desde hoy el límite vivo de la métrica**, y quien
lo mueva abre ADR (RT-057).

**La carrera del jugador no se toca**: `RunCareer.Tackles` sigue sumando las dos entradas. Estrecharla
sería decidir qué recuerda una run de un jugador (RF-122) y eso es `game-design-review`, no un efecto
colateral de arreglar un instrumento.

**Por qué `injuriesPerMatch` sí sigue mezclando las dos poblaciones** (~14 % de las lesiones y ~35 % de
las faltas vienen hoy de la entrada sin balón): porque mide **daño**, no fútbol. Una lesión es una lesión
venga de donde venga; disputar el balón y golpear a quien no lo lleva, no. Esa es la línea, y es la que
explica por qué se separa una métrica y no la otra.

`offBallTacklesPerMatch` no recibe banda hasta tener la distribución de D2 delante.


---

## Segunda enmienda — D2/D3: estructura implementada, calibración devuelta al revisor (22 sep 2026)

**Implementado**: `tackleMarkTargetBonus` es ya un **mapa por puesto** con esquema propio y los cuatro
puestos obligatorios; `Utility.IsDefensiveRole` **se ha borrado** —quién entra sin balón lo decide el
dato—; y **0 significa que ese puesto no DECIDE entrar nunca**, comprobado en el código, porque la primera
enmienda midió que un 0 no desactiva la acción por sí solo. *El alcance de ese «nunca» es la decisión, no
el motor entero*: `MatchEngine.RepeatTackle` (el efecto `extraAction`) puede fabricar una entrada sin balón
sin mirar el mapa — agujero conocido, **sin evidencia de activación**, con test propio y anotado en la
ficha junto a la decisión abierta de `extraAction`, que resulta ser el mismo problema por dos caminos. Con el mapa en `Defender: 150 · Midfielder: 0 ·
Forward: 0` el juego es **byte a byte el de antes**: la palanca está montada y sin estrenar.

**No implementado**: abrir los tres puestos de verdad. Se midieron siete configuraciones y **todas las que
consiguen el reparto pedido rompen algo**:

- con el ajuste solo (190 / −40 / −221, enfriamiento 280) el orden sale bien —DEF 1,21 · MID 0,23 ·
  FWD 0,14 por partido-jugador— pero `tacklesPerMatch` cae a **6,26 sobre un suelo de 6,00**;
- añadiendo el contador de enfriamiento separado y recalibrando, las dos bandas quedan cómodas
  (`tacklesPerMatch` 7,31 · `injuriesPerMatch` 0,80) pero caen **dos puertas más**;
- y el patrón que comparten todas: **repartir la violencia entre los tres puestos aplana la diferenciación
  de builds** (`buildsWinDifferently_injuries` 1,30 → 1,21 → 1,05). Ningún valor del mapa lo evita.

**Dos hallazgos que esta enmienda añade a la ADR**, los dos medidos y detallados en `docs/pendientes/BE-A.md`:

1. **Por qué salía el orden invertido**, que la ADR midió sin explicar: sin balón, la entrada de un
   delantero ya gana a sus propias alternativas (211 contra 180) mientras que la de un defensa pierde por
   180 contra la suya. El delantero no necesita bono; **necesita que se le reste**. Por eso el mapa lleva
   signo.
2. **El enfriamiento de la entrada es un contador único**: pegar sin balón impide disputar el balón
   durante todo el enfriamiento largo. Separarlos —lo que el paquete U ya hizo con el bloqueo— sube
   `tacklesPerMatch` de 6,92 a 7,86 sin tocar un número, **pero hunde la build violenta** (`orc_violence`
   54,17 contra un mínimo de 58). Es un problema propio, con precedente propio, y merece su ADR.

**Lo que decide el revisor** (las tres opciones, con su coste, están en la ficha): aceptar el aplanamiento
y recolocar la curva de puertas de builds; separar el contador de enfriamiento en una ADR aparte y
recalibrar después; o cambiar el canal para que el ajuste entre en el **multiplicador de rasgo** y sea la
agresividad —no el puesto— lo que diferencie, que es lo que esta misma ADR pedía con las palabras del
revisor («los defensas y **los más agresivos**»).
