# BB-P — Las puertas de un solo partido/semilla se leen como causa cuando son ruido de muestreo

**Estado:** Patrón medido en las 3 puertas activas de `BuildGateTests` (17 sep 2026, seis semillas cada
una). **Dos resultados distintos, no uno**: `CoherentBuildsBeatTheirBaseline` y
`BadBuildsLoseToTheirBaseline` confirman la hipótesis de ruido (ver "Tercer caso confirmado" abajo).
`BuildsWinDifferently`/`passChain` **no la confirma** — es una caída real, medida, anterior a esta sesión
(ver "Cuarto caso: no es ruido" abajo), y no se toca aquí.

## Observación

Encontrado por el `independent-reviewer` al revisar BA-N/ADR 0116 (16 sep 2026): varias puertas
estadísticas del proyecto son una diferencia de dos tasas medida sobre un número de partidos fijo, con un
error típico de muestreo (~0,9 puntos para `EquippingAGoodBuildIsWorthSeveralPointsOfWinRate`, sin medir
para las demás todavía) que es del mismo orden que el margen del propio umbral. **No es que falte
emparejar por semilla** —`EquippingAGoodBuildIsWorthSeveralPointsOfWinRate` ya lo hace (mismo `rosterSeed`,
mismas semillas de partido en los dos brazos) y aun así tiene ese error: el problema es solo de tamaño de
muestra frente al margen del umbral, no de diseño del experimento—. Cuando eso pasa, **cualquier cambio en
`/Sim` que no toque ni de lejos el sistema que la puerta mide puede moverla lo suficiente para cruzar el
umbral**, y esa correlación temporal ("cambié X, la puerta se movió") se lee como causalidad sin comprobar
si el movimiento ocurre igual sin X.

## Caso confirmado con medición directa

`EquippingAGoodBuildIsWorthSeveralPointsOfWinRate`, con el catálogo de perks **congelado** (mismo
`/data/perks/`, verificado byte a byte) en cinco commits que solo cambiaban código de `/Sim` ajeno a
objetos y perks: 1,7 / 2,1 / 1,7 / 3,0 / 3,4. Media 2,38, desviación muestral 0,78 — coincide con el error
típico teórico (~0,9) del tamaño de muestra de esa puerta. Detalle completo en
`docs/decisiones/0116-el-escalon-de-equipar-se-recalibra-contra-94-perks.md` y en `docs/pendientes/BA-N.md`
y `docs/pendientes/BA-M.md` (donde el mismo patrón produjo dos hipótesis causales falsas antes de esta
corrección).

## Segundo caso confirmado con medición directa (BB-C, 16 sep 2026)

`MatchRulesTests.AWhistledFoulRestartsWithAFreeKickForTheFouledTeamAndAnUnseenOneDoesNot`: con 50
semillas fijas, el arreglo de `MatchEngine.ResetPositions` de BB-C (sin relación de código con el saque de
falta) desplazó el recuento de un caso raro y documentado ("una falta de bloqueo durante la cuenta atrás
de otra hereda el saque de la primera") de dentro de tolerancia a 9/167 (5,4 %, por encima del tope
documentado del 5 %). Remedido con 250 semillas: la tasa real es **2,6 % sin el arreglo y 3,7 % con él**,
las dos muy por debajo del tope — 50 partidos era una muestra insuficiente para una cola de ese tamaño,
exactamente el mismo diagnóstico que el caso de equipar. Arreglado subiendo la muestra a 150 (mismo
remedio que BA-N: subir muestra, no bajar umbral). Detalle en `docs/pendientes/BB-C.md`.

**Mismo ciclo, un tercer síntoma emparentado sin puerta**: `RunEngineTests.ARunCanBePlayedFromStartToFinish`
(una run completa con semilla fija) dejó de dar Victoria con el mismo arreglo — la instancia más extrema
posible de este patrón, porque una run encadena 17-22 partidos y hereda la sensibilidad de cada uno. Es la
**segunda vez** que esta prueba concreta necesita un cambio de semilla por una razón de código ajena a lo
que prueba (la primera fue un cambio de tamaño de plantilla). No es una puerta de `Category=Gate`, pero es
la misma firma: un test de una sola semilla/partido operando sin margen frente a cualquier cambio real de
`/Sim`.

## Tercer caso confirmado: las tres celdas de tasa de victoria son ruido (17 sep 2026)

Medido con el instrumento que BB-P punto 1 pedía: 6 semillas (1-6) del árbol actual (`BuildGateTests`,
sin tocar código, solo variando la semilla del propio lote — más barato que congelar código y variar
commits, porque aquí la pregunta es "cuánto rebota este número solo", no "quién lo movió"):

| métrica | umbral | media (n=6) | sd (n=6) | sd teórico (docstring) |
|---|---|---|---|---|
| `coherentBuildsBeatNone_orc_violence` | ≥58,00 | 59,83 | **2,33** | 2,3 |
| `badBuildsLoseToNone_elf_brawler` | ≤45,00 | 44,76 | **2,04** | 2,3 |
| `badBuildsLoseToNone_elf_out_of_zone` | ≤45,00 | 45,56 | **2,48** | 2,3 |

Valores brutos (semillas 1-6): `orc_violence` 60,00 / 57,92 / 57,29 / 60,00 / 63,96 / 59,79.
`elf_brawler` 48,13 / 43,33 / 46,25 / 42,71 / 44,17 / 43,96. `elf_out_of_zone` 43,54 / 50,00 / 43,75 /
44,38 / 46,88 / 44,79.

**Confirma la hipótesis de BB-P al pie de la letra**: la desviación empírica coincide con el ~2,3 que el
propio `BuildGateTests.cs` ya documentaba en su cabecera (480 partidos por celda, plantillas emparejadas),
y las tres medias caen dentro de **menos de un sd** de su propio umbral (0,07 σ para `orc_violence`, 0,12 σ
para `elf_brawler`, 0,22 σ para `elf_out_of_zone`). Es exactamente "un umbral sin margen frente a su
propio ruido de muestreo", el mismo patrón que `EquippingAGoodBuildIsWorthSeveralPointsOfWinRate`. **No se
toca ningún umbral aquí** — igual que con la puerta de equipar, cambiar un número sin una ADR (RT-057) y
sin decidir qué margen es aceptable (candidato a `game-design-review`, como ya apunta el punto 5 de esta
ficha) repetiría el error que corrigió la ADR 0116.

## Cuarto caso: `passChain` no es ruido — es una caída real, anterior a esta sesión (17 sep 2026)

Mismas 6 semillas, `buildsWinDifferently_passChain` (umbral ≥1,11, ADR 0062): **1,0977 / 1,0795 / 1,0872 /
1,1098 / 1,0996 / 1,0487** — media **1,0871**, sd **0,0215**. **Falla en las seis semillas, no en algunas**:
a diferencia de las tres métricas de arriba, esto no es una media que roza el umbral por los dos lados, es
una media establemente por debajo. La firma de ruido (BB-P) no aplica aquí tal cual — hace falta otra
explicación, y hay dos candidatas que **no son la misma**:

1. **La hipótesis fácil, y falsa**: "el umbral se derivó sin margen a propósito" (ADR 0062: el 1,11 sale
   de una build aislada de siete `fine_touch`, el techo teórico con perks comunes). **No es eso**: ADR 0062
   documenta que la puerta, con la build real `elf_tiki_taka` completa contra `orc_violence`, medía
   **1,233** en el momento de cerrarse (6 sep 2026) — margen real de 0,12, no cero. Verificado remidiendo
   ese commit exacto (`05689fe`) en worktree: **1,2321**, coincide con la ADR.
2. **Lo que sí es**: entre `05689fe` (ADR 0062, 6 sep) y `ad3c472` (revert dentro de BB-B, ya dentro de
   esta sesión pero **antes** de que este ciclo tocara nada — BA-N, BB-G, BB-C y BA-K vienen después), el
   valor ya había caído a **1,0836** (remedido en worktree, un solo commit, no una media). Es decir: **la
   caída de 1,233 a ~1,09 ya había ocurrido antes de que este ciclo (BA-N→BB-G→BB-C→BA-K) tocara una sola
   línea** — ninguno de los cuatro cambios de este ciclo es la causa. La ventana en la que ocurrió de
   verdad son los commits entre el 6 y el 16 de septiembre — la tanda 1 y 2 del catálogo (61→94 perks) y
   sus ADR asociadas (0090, 0105, 0110...) son las candidatas más plausibles por volumen de cambio, pero
   **no se ha aislado el commit exacto** — eso es una investigación de `gameplay-debug` (Regla A) propia,
   no lo que pedía medir esta ficha.

**No se toca `MinPassChainRatio` aquí.** La propia ADR 0062 ya avisa que este umbral "hay que volver a
derivarlo hacia arriba cuando AL-A se resuelva" — pero AL-A (`docs/pendientes.md`, "el recorrido de un
perk lo fija la base de su canal, no su magnitud") sigue abierta y es una decisión de fondo, no una
calibración de puerta. Bajar el umbral ahora, sin diagnosticar la caída, sería exactamente el error que
corrigió la ADR 0116: cambiar un número contra un síntoma sin saber si el instrumento está bien o si el
juego cambió de verdad. **Queda como hallazgo, no como arreglo**: hace falta (a) aislar el commit o el
paquete que causó la caída de 1,233 a ~1,09, entre el 6 y el 16 de septiembre, y (b) decidir con esa
causa en la mano si el umbral se recalibra (como la ADR 0116) o si es síntoma de un problema de diseño más
profundo emparentado con AL-A.

## Candidatas — actualizado tras el tercer/cuarto caso (17 sep 2026)

De las 4 puertas que quedaron rojas al cerrar BB-B, **2 quedan confirmadas como ruido** (arriba:
`CoherentBuildsBeatTheirBaseline`/`orc_violence`, `BadBuildsLoseToTheirBaseline`/`elf_out_of_zone`, sd
empírico coincide con el teórico y las medias caen dentro de un sd de su umbral). `NoGateMetricIsOutOfRange`
hereda esa conclusión al agregarlas. **1 queda sin confirmar todavía**:

- `TheThreeDoctrinesBuyDifferently` (`contextual` vs `saver`, compras por mercado) — **no medida en este
  ciclo**: vive en `FullRunGateTests.cs`, mide sobre runs completas (240 por doctrina), no sobre el lote
  rápido de `BuildGateTests` (~17 s), así que barrer varias semillas cuesta minutos por semilla en vez de
  segundos — mismo protocolo pendiente, coste distinto. No está roja ahora mismo (pasa en el árbol actual),
  así que no bloquea nada; queda como la única candidata original de BB-B todavía sin medir.

`badBuildsLoseToNone_elf_brawler` (la build que de hecho falla ahora mismo en `BadBuildsLoseToTheirBaseline`,
no `elf_out_of_zone`, que hoy pasa) se añade a la lista de confirmadas — es la misma puerta, el mismo
patrón, otro build concreto cruzando el margen según la semilla, que es justo el fenómeno que BB-P describe.

## Qué haría falta, sin implementarlo aquí

1. **Hecho para 3 de las 4 (17 sep 2026, ver "Tercer caso" arriba)**, con una variante más barata que la
   propuesta original: en vez de congelar código y variar 5 commits (necesario cuando la pregunta es
   causal, como en BA-N), bastó variar la semilla del lote en el árbol actual — la pregunta aquí era
   "cuánto rebota este número por sí solo", no "quién lo movió". Queda `TheThreeDoctrinesBuyDifferently`
   (`FullRunGateTests`, coste por semilla mucho mayor) y, aparte, diagnosticar la caída real de `passChain`
   (no es ruido, ver "Cuarto caso" arriba — necesita `gameplay-debug`, no una remedición más).
2. **Corregido dos veces (segunda y tercera revisión)**: la primera versión de este punto proponía "pasar
   a diferencia emparejada, patrón de la ADR 0087" para `EquipmentImpactTests.cs` — pero esa puerta **ya**
   empareja por semilla; no había margen ahí (segunda corrección). La segunda versión decía que la puerta
   "ya calcula" 96 diferencias por plantilla y que bastaría imprimir su RMS — también falso: `WinsOf`
   devuelve un único entero acumulado, las diferencias por plantilla no existen en el código actual y
   habría que modificarlo para conservarlas; y el estadístico que reproduce el ~0,9 no es la RMS de esas
   diferencias (que daría del orden de 9, un orden de magnitud de más, porque mezcla la dispersión con la
   magnitud del efecto) sino la **desviación típica de las 96 diferencias, dividida por √96**. Lo que de
   verdad falta: cambiar `WinsOf` para que conserve las 96 diferencias por plantilla y que el test imprima
   `sd(diferencias)/√96` junto al resultado agregado. Eso habría evitado reconstruir cinco commits para
   conocer el ~0,9 de `EquipmentImpactTests.cs`, y convierte el punto 1 de esta lista en segundos por
   puerta en vez de una campaña de remedición. Subir la muestra (`Rosters`, precedente en los paquetes Z y
   AZ de ese mismo fichero) sigue siendo la opción si la dispersión autoinformada confirma que hace falta
   más potencia, pero es la segunda opción, no la primera.
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

- `docs/pendientes/BA-N.md`, `docs/pendientes/BA-M.md` — el caso que reveló el patrón (ambas cerradas).
- `docs/decisiones/0115-la-barrera-de-reanudacion-cubre-las-cinco-no-solo-la-falta.md` — ya lo anotaba en
  general ("la firma de puertas de un solo partido/semilla operando cerca de su margen") sin abrir ficha.
- `docs/decisiones/0087-el-valor-de-un-perk-se-mide-contra-su-control.md` — precedente de que el proyecto
  ya calcula una dispersión propia (`rowDeviation`) dentro de una sola ejecución para otro instrumento; no
  se ha verificado que su fórmula exacta sea la misma que la que necesita esta puerta (`sd/√n`), así que no
  se copia sin comprobar — el precedente es la idea, no la fórmula.
