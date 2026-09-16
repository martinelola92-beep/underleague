# 0117. El bono de balón suelto sube para que perseguir gane la comparación que hoy pierde

**Fecha:** 2026-09-16
**Estado:** Aceptada e implementada (`data/ai/weights.json`, `context.chaseBallLooseBonus`).
**Decisión del orquestador** (Fase 3, BB-G, `docs/pendientes/BB-G.md`), con una corrección tras
`independent-reviewer`: la primera redacción de este cambio se había rechazado con evidencia de una sola
semilla; una segunda medición (pedida por el revisor) mostró que esa evidencia no discriminaba entre "lo
causó el cambio" y "es ruido preexistente de esas puertas", y la decisión se invirtió a ACEPTADO.
**Requisitos:** RT-054, RT-056, RT-057 (cambio de rango de balance, exige ADR). Relacionado:
`docs/pendientes/BB-G.md` (el síntoma y el diagnóstico H1/H2/H3), `docs/auditoria-ia-jugadores-9.md`
(precedente de un `ChaseBall pen` **distinto**, rechazado por el mismo tipo de criterio, con la lección
que este ADR aplica y luego corrige).

## El problema

«El balón se queda parado en el campo» (`docs/pendientes/BB-G.md`). Causa CONFIRMED con anterioridad
(volcado de utilidad RT-098): con el balón suelto y quieto, `ChaseBall` compite contra acciones de
colocación (`FindSpace`, `CoverSpace`) y pierde, porque el bono de balón suelto
(`context.chaseBallLooseBonus`) es demasiado pequeño frente a bonos fijos de esas otras acciones.

## Medición barata antes de tocar código (Regla A)

Instrumento temporal `Sim.Tests/Analysis/_BBG.cs`, 200 partidos del conjunto de referencia (RT-081):
**baseline 107 episodios de balón suelto y quieto ≥15 ticks** (44 % de los partidos con al menos uno,
el más largo 860 ticks — consistente en orden de magnitud con la muestra original de BB-G, 23/40).

Antes de elegir un valor nuevo, se volcó la tabla de utilidad (RT-098) del jugador más cercano al balón en
el punto medio del episodio más largo (partido 172, tick 771): un defensa a 0,62 casillas del balón
puntuaba `ChaseBall`=586 y perdía contra `CoverSpace`=738 (bono fijo `coverBetweenBallAndGoalBonus`, no
depende de la distancia). **Hueco medido: 152 puntos.**

Descomposición exacta del 586 (`Context`=183 sobre un `chaseBallLooseBonus` de 250), corregida tras
`independent-reviewer` — la primera redacción de este ADR decía "250 − 67 de penalización de distancia",
aritméticamente falsa:

- Penalización de distancia (`chaseBallDistancePenaltyPerCell=45` × 62 centicasillas / 100) = **27**.
- Penalización de salida de zona (`Sim/Engine/Utility.cs`, `OutsidePenalty`: `outsidePenaltyPerCell=120`
  × 62 / 100 × disciplina/100 × `disciplineWeightPercent=100`/100) = **40**, con la disciplina de ese
  jugador en torno a 55.
- 250 − 27 − 40 = **183** ✓, coincide con el `Context` volcado.

**Consecuencia de la corrección**: 40 de los 152 puntos del hueco los pone un castigo que escala con la
`Discipline` del jugador (rasgo que varía por raza), no la distancia al balón sin más. Un bono plano
compitiendo contra una penalización de zona escalada por raza es, por construcción, una intervención **no
uniforme entre razas** — el candidato más concreto para explicar por qué se movieron las puertas de
diferenciación de build (ver más abajo), más que "más disputa en todas partes" (redacción original,
descartada como explicación demostrada, queda como LIKELY).

## Hipótesis única e hiperparámetro

Subir `context.chaseBallLooseBonus` (`data/ai/weights.json`) de **250 a 410** (+160: el hueco de 152 más
8 de margen), sin grid search. Es la misma palanca que ya usaba el mecanismo de balón suelto, no una
primitiva nueva: sube un término existente, no cambia qué acciones compiten ni cómo.

**Precaución explícita**, tomada de `docs/auditoria-ia-jugadores-9.md` (un `ChaseBall pen` **distinto**
—Tackle-vs-ChaseBall, no éste— fue aceptado sobre un baseline sucio, rechazado al remedir sobre uno
limpio: 3 puertas rojas → 5 → 7, todas de diferenciación de build moviéndose juntas): se pre-registraron,
antes de medir, un criterio de aceptación (la métrica primaria baja ≥50 % y ninguna puerta de
diferenciación **nueva** que no se pueda explicar como ruido) y uno de rechazo (dos o más puertas del
clúster de diferenciación moviéndose juntas **por causa del cambio**), con la instrucción explícita de
remedir con otra semilla antes de aceptar una puerta nueva en rojo como señal.

## Lo que midió el primer experimento, y por qué su rechazo inicial fue un error de proceso

Con `chaseBallLooseBonus=410`, semilla 1 (la canónica de las puertas):

| métrica | baseline | experimento | delta |
|---|---|---|---|
| episodios ≥15 ticks (200 partidos) | 107 | **32** | −70 % |
| partidos con ≥1 episodio | 88/200 | 27/200 | −69 % |
| episodio más largo | 860 ticks | 38 ticks | −96 % |

43 puertas, semilla 1: `TheThreeDoctrinesBuyDifferently` y `BadBuildsLoseToTheirBaseline` pasan a verde
(antes rojas); `CoherentBuildsBeatTheirBaseline` (`orc_violence`) empeora de 56,67 a 55,62;
`BuildsWinDifferently` (`passChain`) pasa de verde a rojo, **1,1077 contra un umbral de 1,1100** (falla
por 0,0023). **La primera versión de este ADR rechazó el cambio aquí**, leyendo esas dos últimas puertas
como "dos métricas de diferenciación moviéndose juntas en la misma dirección" — el criterio de rechazo
pre-registrado, aplicado, pero **sin hacer la remedición con otra semilla que el propio criterio de
aceptación exigía** para una puerta nueva en rojo antes de contarla como señal.

`independent-reviewer` señaló el hueco: **el rechazo trataba un movimiento de una sola semilla como
prueba, exactamente el patrón que `docs/pendientes/BB-P.md` documenta para otras puertas** (BA-N es el
caso ya confirmado). Remedido con semilla 2, 4/4 combinaciones (baseline/experimento × semilla 1/2):

| | semilla 1 | semilla 2 |
|---|---|---|
| `orc_violence` (≥58), baseline | 56,67 (rojo) | 57,71 (rojo) |
| `orc_violence` (≥58), experimento | 55,62 (rojo) | **verde** |
| `passChain` (≥1,11), baseline | verde | **1,0931 (rojo)** |
| `passChain` (≥1,11), experimento | 1,1077 (rojo) | 1,0742 (rojo) |
| `BadBuildsLoseToTheirBaseline`, baseline | `elf_out_of_zone`=49,38 (rojo) | `elf_brawler`=50,00, `elf_out_of_zone`=45,42 (rojo) |
| `BadBuildsLoseToTheirBaseline`, experimento | verde | `elf_brawler`=46,04, `elf_out_of_zone`=49,79 (rojo) |

**Las tres puertas fallan en 3 de las 4 combinaciones, baseline incluido.** `passChain` falla incluso en
`baseline/semilla 2` (1,0931 < 1,11): no es un umbral que el experimento cruce, es un umbral que ya se
cruza sin tocar `chaseBallLooseBonus`. `orc_violence` fluctúa 55,62-57,71 en las cuatro celdas sin patrón
que separe baseline de experimento — el docstring de la propia puerta (`BuildGateTests.cs`) fija su error
típico en **2,3 puntos**, y las cuatro medidas caben dentro de ese margen. `BadBuildsLoseToTheirBaseline`
falla en tres de cuatro celdas con **distintos** builds cada vez (`elf_brawler`, `elf_out_of_zone`, o
ambos), la misma firma que `docs/pendientes/BB-P.md` documenta para puertas de un solo partido/semilla
operando cerca de su margen.

**Conclusión corregida**: ninguna de las dos puertas que motivaron el rechazo inicial muestra una señal
que sobreviva a una segunda semilla. Aplicando el criterio de aceptación tal como se pre-registró — "si
entra una sola puerta nueva en rojo, se remide para descartar ruido antes de contarla como una segunda" —
el experimento **se acepta**: la métrica primaria baja muy por encima del 50 % exigido, y ninguna puerta
de diferenciación muestra una degradación atribuible al cambio que no se explique igual de bien por el
ruido ya presente en el baseline.

**Neto de puertas** (semilla 1, canónica): 4 rojas → **3 rojas** (`TheThreeDoctrinesBuyDifferently` y
`BadBuildsLoseToTheirBaseline` se arreglan; `CoherentBuildsBeatTheirBaseline` sigue roja, dentro de su
rango de ruido habitual; `BuildsWinDifferently` entra, también dentro de rango de ruido, según la tabla de
arriba). `NoGateMetricIsOutOfRange` (agregador) sigue roja porque agrega las otras dos.

`/Balance` (2.000 partidos, semilla 1): todas las bandas obligatorias de RT-056 dentro de rango
(`possessionChanges` 21,45, `passChainAvgLength` 2,00, `shotsPerMatch` 8,84, `tacklesPerMatch` 11,93,
`injuriesPerMatch` 0,81, `scorelineShare` 86,00, `ballThirdMaxShare` 49,33) — sin movimiento fuera de
banda en el agregado, coherente con que la ventana de balón suelto es una fracción pequeña del partido.

## H1 (BB-G), reetiquetada

`docs/pendientes/BB-G.md` marcaba H1 ("el muro de la zona de acción bloquea al perseguidor designado")
REJECTED tras abrir el **límite duro** de zona para `ChaseBall` sobre balón suelto sin que cambiara el
número de balones muertos. Pero en el episodio volcado aquí `ChaseBall` **no está rechazado**
(`Rejected=False` en la fila de utilidad): no lo mata el límite duro, lo lastra la **penalización blanda**
de zona (`OutsidePenalty`, 40 de los 152 puntos del hueco). H1 queda **REJECTED solo para el límite
duro**; la penalización blanda de zona sobre `ChaseBall` con balón suelto no se había probado hasta este
ADR, que la ataca indirectamente subiendo el bono en vez de eximir la penalización.

## Lo que NO se implementa aquí, y por qué

- **Eximir o reducir `OutsidePenalty` solo para `ChaseBall` sobre balón suelto** (en vez de subir el bono
  plano): sería más local y no tocaría los casos donde `ChaseBall` ya gana con el jugador dentro de zona.
  Es la hipótesis que H1 nunca probó de verdad. Queda como candidato para si una vuelta futura necesita
  volver a tocar esta acción con menos efecto colateral.
- **Cambiar la geometría del equilibrio** (radio de recogida `PickupRadius=0,5` casillas, tolerancia de
  llegada, o una regla local "sobre un balón suelto, se recoge"): en el episodio volcado el jugador
  quedaba a 0,62 casillas, apenas 0,12 fuera del radio de recogida, sugiriendo que parte del atasco podría
  ser un problema de geometría del equilibrio, no solo de tabla de utilidad. No medido; candidato para
  `docs/pendientes/BB-G.md`.
- **Una precondición dura de tipo "perseguir es la única opción legal cuando el balón lleva N ticks
  suelto"**: es una **primitiva de motor con estado temporal por balón**, no un ajuste de peso — dispara
  `game-design-review` y `architecture-review` (CLAUDE.md, Regla B/C), no `balance-measure`. No se evalúa
  en este ADR ni se implementa como si fuera "otro ajuste de pesos".
- **Medir si el hueco de 152 puntos es típico o un caso extremo**: el instrumento solo volcó el episodio
  más largo. Un muestreo de los diez episodios más largos, con su propio hueco medido cada uno, diría si
  410 quedó corto, sobrado o ajustado para el caso típico — barato (mismo patrón que BB-P punto 1), no
  hecho aquí.

## Riesgo de diseño anotado, no resuelto aquí

`CoverSpace` gana con un `Context` fijo de 150 (`coverBetweenBallAndGoalBonus`) mientras `ChaseBall` paga
dos penalizaciones variables (distancia y zona). Una acción de colocación con bono constante compitiendo
contra una acción de disputa con penalizaciones variables es una asimetría estructural: en algún régimen
de distancia y disciplina, colocarse seguirá ganando a ir a por un balón que se tiene al lado, sin que el
jugador vea en pantalla ninguna razón (RF-012d, "comportamiento observable > modificadores numéricos
invisibles"). Subir `chaseBallLooseBonus` desplaza el punto donde eso ocurre, no lo elimina como diseño.
Queda anotado en `docs/pendientes/BB-G.md` como pregunta para `game-design-review`, no como conclusión de
esta ADR — el gate de este cambio es un peso de IA (Regla D), no una mecánica nueva.

## Lo que esta corrección deja escrito sobre el proceso

**Una puerta nueva en rojo tras un cambio de balance no es evidencia de causa hasta que sobrevive a una
segunda semilla.** La primera versión de esta decisión trató una única medición como suficiente para
rechazar un experimento con un efecto primario grande y claro; la reviewer independiente pidió
exactamente la medición barata (17 s) que el propio protocolo pre-registrado exigía y no se había hecho.
Es el mismo patrón que `docs/pendientes/BB-P.md` documenta para BA-N, aplicado esta vez a una decisión de
*aceptar o rechazar* un experimento, no solo a la narrativa histórica de una ADR ya cerrada.
