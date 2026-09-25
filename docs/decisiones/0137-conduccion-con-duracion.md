# ADR 0137 — Conducir es un compromiso, no una intención que se reevalúa cada dos ticks

Fecha: 23 sep 2026 · Estado: **implementada y enmendada el 25 sep 2026** — de las dos puertas que dejó
abiertas, una **no era suya** (medido, ver la enmienda al final) y la otra tiene mecanismo medido y decisión
tomada. La dosis se confirma en `driveTicks: 12`.
Decisión del revisor. Ficha: [BI-D](../pendientes/BI-D.md). Encadenada tras `game-design-review` y
`architecture-review`.

## Por qué

El revisor, jugando la build con los modelos 3D: *«el balón no va en sus pies, no se nota como si lo
controlara»*, y después *«debería existir conducción con duración porque si no el regate tampoco entra en
juego»*.

**La premisa se midió antes de tocar nada**, porque el regate **no estaba instrumentado** en ninguna parte
—ni métrica en `/Balance`, ni campo en el informe, y `MatchLogView` omite `DRIBBLE_ATTEMPTED`—. Con
`DribbleMeasurementTests` (200 partidos, referencia, semilla 1):

| | base |
|---|---|
| regates intentados por partido | **0,85** |
| ticks conduciendo por partido | 39,7 de 1.200 (**3,31 %**) |
| conducciones por partido | 7,3 |
| duración media de una conducción | 5,45 ticks (0,36 s) |
| ticks con dueño del balón | 29,3 % |

**CONFIRMED**: el regate no entraba en juego —0,85 por partido contra 8,6 tiros, 12,5 entradas y ~43
pases—, y la técnica del conductor era por tanto un modificador invisible.

## Qué se cambia, y por qué es pequeño

La conducción **ya existía**: `PlayerState.Dribbling` mueve al portador con el balón al 80 % de su
velocidad. Lo que no existía era la **duración**: se entraba con `EnterState(Dribbling, 0)` y, como
`Dribbling` es un estado de decisión, el portador volvía a decidir cada `decisionIntervalTicks` (2) —donde
la utilidad casi siempre prefería el pase.

1. `dribble.driveTicks` en `/data`: la conducción se entra **con contador**.
2. La puerta de decisión respeta el contador: un estado de decisión con ticks pendientes no vuelve a
   decidir. **Comprobado inerte**: los tres estados de decisión se entran con 0 en las dieciocho llamadas
   del motor, así que la condición solo la nota la conducción.
3. **Invariante nuevo: conducir exige llevar el balón.** Si se lo quitan a mitad de la conducción, el
   estado se corta en el acto — sin esto el portador avanzaría al 80 % detrás de un balón ajeno y sin poder
   decidir hasta agotar el contador.

No hay primitiva nueva: `EnterState(estado, ticks)` es el patrón que el bloqueo usa desde el paquete U.

## Medido: seis configuraciones, cinco pasadas de las 43 puertas

Línea base tomada **en el mismo árbol** (`git stash`), no de memoria.

| `driveTicks` · cuota técnica | regates/partido | conduciendo | duración | puertas rojas |
|---|---|---|---|---|
| base (sin duración) | 0,85 | 3,31 % | 5,45 t | **2** |
| 6 · 0 % | 1,54 | 4,86 % | 8,55 t | 5 |
| 9 · 0 % | 2,51 | 6,74 % | 11,5 t | 4 |
| **12 · 0 %** | **2,69** | **9,02 %** | **15,4 t** | **3** |
| 12 · 100 % | 2,16 | 6,10 % | 10,2 t | 5 |

**El fútbol no se mueve.** Lote de `/Balance` (2.000 partidos × 2 semillas, contra línea base propia):
ninguna métrica fuera de banda y **ninguna cambia de estado**. `possessionChanges` 21,70 (banda 12-28),
`passChainAvgLength` 1,99 (1,80-3,50), `shotsPerMatch` 8,61, `injuriesPerMatch` 0,72. La única `OUT` de la
semilla 7 (`shotsPerMatch` 6,66) **ya estaba OUT en la base** (6,64).

**Una conclusión propia refutada**: la semilla 1 sugería que el mejor equipo gana menos
(`betterTeamWinRate_human_60_vs_human_50` 61,26 → 56,76). **No replica**: en la semilla 7 se mueve al
revés (67,87 → 69,67). REJECTED — y es el precedente de siempre: una sola semilla no es evidencia.

**La cuota por técnica se descarta.** La idea era que la duración dependiera de la técnica para que la
conducción *diferenciara* en vez de aplanar. Medida: **no repara ninguna puerta** y añade dos fallos de
ruido. El término queda en `/data` con cuota **0** —inerte, comprobado: reproduce la versión plana cifra
por cifra— por si una calibración futura lo quiere.

## Lo que queda en rojo, y es una decisión, no un olvido

Con la configuración enviada (`driveTicks` 12, cuota 0): **3 rojas contra 2 de la base**.

| puerta | base | con conducción | |
|---|---|---|---|
| `badBuildsLoseToNone_orc_misplaced` | 45,05 **roja** | verde | **la arregla** |
| `badBuildsLoseToNone_elf_brawler` | 47,01 roja | 45,55 roja | mejora, sigue roja |
| `badBuildsLoseToNone_elf_out_of_zone` | verde | **46,04 roja** | **la rompe** |
| `coherentBuildsBeatNone_orc_violence` | verde | **57,16 roja** (mín. 58) | **la rompe** |

**`orc_violence` rompe en las cinco configuraciones** (56,4-57,9), así que no es dosis: es la mecánica.
Hipótesis causal, con mecanismo concreto: más conducción son más duelos de regate, y **cada duelo que el
conductor gana derriba al defensor** `dribble.lostKnockdownTicks` ticks — la build violenta paga un peaje
nuevo por existir. Se mide bajando ese derribo.

Y un aviso que vale más que este paquete: **varias puertas están a distancia de ruido de su umbral**
—doctrinas falló por 0,09, `ordinaryDefeatRateAct1` por 0,04, `orc_violence` por 0,06 en una dosis—. Leer
una sola pasada como «esta configuración rompe X» es sobreinterpretar; el patrón reproducible entre dosis
es lo único que cuenta, y por eso aquí se mide cinco veces.

## La hipótesis del derribo, refutada (misma sesión)

La explicación propuesta arriba —«más duelos, más defensores derribados, la build violenta paga un peaje»—
**se midió y es falsa**. Con `dribble.lostKnockdownTicks` de 6 a 3, sexta pasada de las 43 puertas:

`coherentBuildsBeatNone_orc_violence` = **57,16**, *idéntico* al valor con el derribo en 6. No se mueve ni
una centésima, así que ese mecanismo **no participa** en lo que la puerta mide. **REJECTED.** Y además
empeora: `orc_misplaced` vuelve a rojo, `elf_brawler` sube a 47,53 y caen dos puertas de run.

Se restaura `lostKnockdownTicks: 6`. Queda **sin explicación medida** por qué la conducción cuesta ~1 punto
a la build violenta; lo que sí está medido es que **le cuesta en las cinco dosis** y que no es por el
derribo del duelo.

## Estado final enviado

`dribble.driveTicks: 12`, `dribble.driveTicksTechniqueSharePercent: 0`, `lostKnockdownTicks: 6`.
Tres puertas en rojo contra dos de la base, el fútbol intacto en dos semillas, y dos decisiones abiertas
para el revisor: si `coherentBuildsBeatNone_orc_violence` y `badBuildsLoseToNone_elf_out_of_zone` se
compensan de otro modo, o si sus bandas se mueven con los datos de esta ADR (RT-057).

---

## Enmienda (25 sep 2026): una de las dos puertas que esta ADR se atribuyó no es suya

Esta ADR se cerró con dos puertas «pendientes de decisión» y un misterio declarado: *«queda sin explicación
medida por qué la conducción cuesta ~1 punto a la build violenta»*. Al ir a decidirlas se remidió primero,
porque entre medias entró el **balón parado posicional (ADR 0147)** y el árbol ya no era el mismo.
Instrumento nuevo: `Sim.Tests/Analysis/DribbleGateContrastTests.cs`, que vuelca **las catorce celdas** de la
puerta —no sólo las rojas— con su media sobre las ocho bases de semilla **y el valor por semilla**, que es
lo que permite saber si una diferencia entre dos árboles es señal. Volcados en `docs/balance/bi-d/`.

**1. `coherentBuildsBeatNone_orc_violence`: la atribución no se sostiene. LIKELY.** Δ(0→12) = −0,26 ± 1,00
y Δ(0→18) = +0,03 ± 1,0: **plana en las tres dosis**. El mecanismo que esta ADR propuso —más conducción, más
duelos, más defensores derribados— exige que el efecto crezca con la dosis, y no crece. Dicho con
honestidad: ese contraste **no tiene potencia para rechazar un efecto de −1 punto**; lo que cierra el caso
es la planitud en dosis más un tratamiento mucho mayor que tampoco la movió (la ADR 0147 subió las faltas
un 68 % y la celda pasó de 57,16 a 57,24). El misterio se disuelve porque **no hay efecto que explicar**,
no porque se haya medido su ausencia. Y la celda no está establecidamente roja en ninguna dosis: 57,50
contra 58 son 0,5 ET. Pasa a [CAT-J](../pendientes/CAT-J.md).

**2. `badBuildsLoseToNone_elf_out_of_zone` sí es suya: +2,97 (4,9 σ). CONFIRMED.** Pero **sólo esa**. La
lectura tentadora —«las builds mal construidas pierden menos cuanto más se conduce»— **no se sostiene**:
`elf_brawler` (+0,8 σ) y `orc_misplaced` (+0,5 σ) están bajo el ruido, y `human_scattered` se mueve al
revés con **−2,1 σ**. Entre las coherentes suben dos con señal (`human_counter` +3,0 σ, `orc_giants`
+2,0 σ). La conducción mueve celdas en las dos direcciones y sin patrón por familia; el mecanismo queda
**sin demostrar**.

**3. La dosis se queda en 12.** La 18 restaura exactamente la conducción que esta ADR envió (15,23 t contra
15,4 t) y el riesgo que pedía (35,8 % de conducciones perdidas contra 24,3 %), pero sube significativamente
dos celdas de builds malas hacia su techo (+5,1 σ y +2,4 σ). La 0 deja `noDeadPerks` en 0,12: la conducción
**resucita `crowd_control`**, de 1,80 % a 13,15 % de activación, y ese argumento a favor de la mecánica no
estaba aquí. **Aviso**: los tres cruces concretos del techo de 45 que produce la dosis 18 están dentro del
ruido contra el umbral (0,4-0,9 ET) — lo que decide es el contraste pareado, no la posición contra la banda.

**4. Las cifras de conducción de esta ADR ya no describen el juego.** La conducción se ha encogido sola:
cuota real **9,02 % → 2,52 %** de los ticks. Dos mecanismos, y ninguno es que la corten (el 75,7 % agota su
contador): se decide conducir un **44 % menos** que entonces (4,1 contra 7,3 conducciones por partido, las
dos con el parámetro a 0) y las rachas ya no se **reencadenan**. Detalle y etiquetas en
[BI-D](../pendientes/BI-D.md).

**5. Aviso de instrumentación que afecta a todo el proyecto**: el porcentaje de esta ADR se calculaba sobre
el literal `1200`. Desde la ADR 0147 un partido dura **1.825,8 fotogramas** de motor, así que cualquier
métrica expresada «de 1.200» está inflada un 52 %.
