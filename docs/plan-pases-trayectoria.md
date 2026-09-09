# Plan: trayectoria del pase y del tiro, y el receptor (AZ-B)

**Fecha:** 2026-09-09. **Origen:** segunda partida del revisor (`pendientes.md` AZ-B). **Estado:** diseño; pendiente de ejecutar paso a paso tras AY. El paso 5 (`ThroughPass`) es regla de juego (RT-092) y exige ADR antes de escribirse.

Encargo de **solo diseño** (9 sep 2026). Nada de lo de aquí está implementado ni commiteado. Deriva de la
anotación del revisor: *«A la hora de hacer pases o tiros debemos tener en cuenta si hay jugadores en la
trayectoria. Los jugadores solo pueden recibir un pase si el balón llega a donde están ellos o si el balón
se ha lanzado en profundidad para que el receptor lo reciba en carrera.»*

Estilo y cadencia calcados de `docs/plan-intercepcion-disparo.md`: un cambio por paso, su parámetro, sus
tests, su lote y su criterio de parada. Todas las mediciones de este documento son propias (2000 partidos,
semilla 1, `data/balance/reference.json`, Release, sobre `HEAD` = `151c65a`), no citas de terceros.

---

## 0. Diagnóstico sobre el código actual

### 0.1 Referencia medida (2000 partidos, semilla 1, HEAD)

| Métrica | Valor | Banda RT-056 | Estado |
|---|---|---|---|
| possessionChanges | **24,02** | 12-28 | IN (3,98 de margen arriba) |
| passChainAvgLength | **2,31** | 2-4 | IN (0,31 de margen abajo) |
| shotsPerMatch | **10,04** | 8-16 | IN |
| tacklesPerMatch | 9,76 | 6-14 | IN |
| ballThirdMaxShare | 42,00 | 0-50 | IN |
| scorelineShare_1-0_to_3-2 | 85,15 | ≥50 | IN |
| goalsPerMatch | 2,75 | — | INFO |
| shotsOnTargetShare | 69,88 | — | INFO |
| saveRate | 57,61 | — | INFO |
| blockRate | 2,56 | — | INFO |

Dato importante para el resto del documento: **hoy hay más margen que cuando se intentó y se descartó el
paso 4** de `plan-intercepcion-disparo.md`. Entonces `passChainAvgLength` valía 2,02 (0,02 sobre el suelo)
y `possessionChanges` 25,10 con la banda leída como 12-25; hoy son 2,31 y 24,02 sobre 12-28. El presupuesto
existe, pero es finito y hay que gastarlo en un orden concreto (§2).

### 0.2 Instrumentación propia (no está en el repositorio)

Los números de esta sección salen de una copia limpia de `HEAD` fuera del repositorio, parcheada con contadores
`Interlocked` y descartada después:
una copia temporal fuera del repositorio, descartada al terminar
(`Sim/Engine/Diag.cs`, más ganchos en `LaunchPass`, `ResolvePassArrival`, `TryIntercept`, `LaunchShot`,
`TryBlockShot` y `Balance/Program.cs`). **El repositorio no se ha tocado.** El paso 0 de §2 propone llevar
al repositorio solo la parte barata de esto.

```
passesLaunched=114611 completed=64312 intercepted=5125 loose=44104 cancelled=1070
passRollOk=85158 rollOkButLoose=16433
corridor(<0,60)=45088 (39,3%) corridorCompleted=22969 (50,9%) corridorIntercepted=3210 (7,1%)
corridorNear(<0,60 y a ≤2 casillas del pasador)=37798 completados=21396 (56,6%)
dead(<0,30 = dentro del cuerpo)=19751 (17,2%) completados=8820 (44,7%)
leadApplied avg=1,44 casillas (n=114611); arrivalError avg=2,10 casillas (n=16433, solo roll ok)
interceptAttempts=171310 (1,49 por pase) -> 5125/171310 = 2,99% por intento
shotsLaunched=20077 corridor(<0,60)=9510 (47,4%) blocked=513 corridorBlocked=455 (4,8%)
```

### 0.3 (a) La trayectoria: qué mira hoy la evaluación del pase y qué hace la resolución

**Decisión (quién elige el pase).** `Sim/Engine/Utility.cs:748-825` (`EvaluatePass`):

- El **único** término de trayectoria es `Sim/Engine/Utility.cs:778`:
  `if (longPass && SegmentBlocked(players, p.Team, p.Position, mate.Position)) { continue; }`.
  Es decir: el pasillo se consulta **solo para el pase largo** (>`shortPassMaxCells` = 3 casillas) y como
  **descarte duro**, no como peso. El pase corto —que es la mayoría del juego: los pesos base de
  `ShortPass` son 420-600 frente a 135-320 de `LongPass` en `data/ai/weights.json`— **no mira la
  trayectoria en absoluto**.
- El radio del pasillo es `Sim/Engine/Utility.cs:72`: `private const float PassLaneRadius = 0.6f`. Está en
  **código**, no en `/data`, en contra del espíritu de RT-096 para un número que es de balance.
- El único filtro que sí mira rivales es `Sim/Engine/Utility.cs:773`,
  `HasOpponentWithin(players, mate, PitchConstants.PressureRadius)` (1,0 casillas): mira el **entorno del
  receptor**, no la línea. Un receptor con un rival encima se descarta; un receptor limpio con un orco
  plantado a mitad de camino puntúa igual que uno con la línea despejada.
- `data/ai/weights.json` → `context.passOpenReceiverBonus` = **220** es un bono **plano**: se cobra entero
  por tener cualquier receptor legal (`Utility.cs:800`). No existe ningún término que pese la congestión
  del pasillo. El ranking entre receptores candidatos (`Utility.cs:806-812`) es
  `advance − 20% × distancia`: **la geometría de los cuerpos no entra ni en la elección del receptor ni en
  la puntuación de la acción**.
- Respuesta directa a la pregunta del encargo: los rivales sobre la línea pasador→receptor **no se cuentan
  con ningún peso**. Solo existen como interruptor de sí/no, y solo para el pase largo.

**Resolución (qué le pasa al balón).** `Sim/Engine/MatchEngine.cs:897-928` (`TryIntercept`):

- Radio 0,9 casillas (`data/sim/tuning.json` → `pass.interceptRadiusCells`) alrededor de la posición
  **actual** del balón, un intento por rival y por pase (`Ball.InterceptAttempted[i]`).
- La probabilidad es `MatchEngine.cs:1060-1074` (`InterceptChance`):
  `250 + 10 × (técnica_interceptor − técnica_pasador)` sobre 10.000, con suelo 200 (2%) del `Bounded` de la
  ADR 0050 P4. **A técnica pareja son 2,5%.**
- **La distancia real no entra en la fórmula.** Un rival a 0,05 casillas del balón y otro a 0,85 tiran
  exactamente la misma probabilidad. El radio es una puerta binaria, no una geometría.
- Medido: **2,99% de conversión por intento** (5.125 de 171.310). Un cuerpo plantado exactamente en la
  línea de pase deja pasar el balón el **97% de las veces**.
- Medido: el **39,3%** de todos los pases se lanzan con un rival a menos de 0,6 casillas del segmento
  pasador→destino; el **17,2%** con un rival a menos de 0,30, es decir **dentro de su propio cuerpo**
  (`bodyRadius` de `/data/races` = 0,28 enano/elfo … 0,38 orco). De esos pases «a través del cuerpo», el
  **44,7% llega al receptor**. Eso es, literalmente, lo que el revisor está viendo.

**El tiro.** `MatchEngine.cs:949-981` (`TryBlockShot`, AW-A paso 3) es el mismo mecanismo con el mismo
radio 0,9 y `InterceptChance × shot.blockChancePercent/100` = **~1,5% por intento**. Y la **decisión** de
tirar (`Utility.cs` `EvaluateShoot`) **no tiene ningún término de trayectoria**: sus cinco términos son
`shootInRangeBonus`, `shootDistancePenaltyPerCell`, `shootAnglePenaltyPerRow` y las dos pendientes por
atributo — todo geometría **hasta la portería**, nada sobre los cuerpos que hay en medio. La presión
(`shot.pressurePenalty`) se cuenta en `LaunchShot` con `CountOpponentsWithin` (`MatchEngine.cs:1440`), que
es un **radio alrededor del tirador**, no el pasillo.
Medido: **47,4% de los tiros se disparan con un defensa dentro de 0,6 casillas de la línea
tirador→portería, y solo el 4,8% de esos se bloquean.**

> **¿Basta con subir `blockChancePercent`?** **No, y es la palanca equivocada.** Multiplica de forma
> uniforme una cuota que es ciega a la distancia: llevarla de 50 a 100 dejaría al defensa clavado en la
> línea en ~3% —sigue siendo transparente— mientras **duplica** el bloqueo del defensa a 0,85 casillas, al
> que el balón visiblemente **no** toca. Es decir, empeora exactamente la queja del revisor (cuerpos que no
> se corresponden con lo que pasa) mientras finge arreglarla. Lo que falta es geometría **dentro** de la
> tirada, no magnitud; y una vez puesta, `blockChancePercent` recupera su significado honesto: «un tiro va
> más fuerte que un pase y por eso se bloquea menos» (0,70 frente a 0,25 casillas/tick).

### 0.4 (b) El receptor: adónde va el balón y quién puede quedárselo

1. **¿A la casilla del receptor o a donde va a estar?** A donde va a estar, ya hoy:
   `MatchEngine.cs:1332-1335`,
   `target = ClampToPitch(receiver.Position + receiver.Velocity × ticks)`.
   `Velocity` es el **desplazamiento del último tick** (`MatchEngine.cs:794`, `Velocity = next − Position`),
   que incluye el empuje de `BodySeparation` y los recortes de `ClampToPitch`/`ClampToArea`. Se extrapola en
   línea recta durante **todo** el vuelo (12 ticks en un pase de 3 casillas, a 0,25 casillas/tick).
   **Medido: el adelanto medio aplicado es de 1,44 casillas** — en un campo de 5 filas, eso es más de un
   cuarto del ancho. **Hoy no existe el «pase al pie»: todos los pases son en profundidad, sin decirlo.**
2. **Y sale mal a menudo.** `PassSucceeds` se decide al lanzar (`MatchEngine.cs:1330-1331`) y la llegada
   (`MatchEngine.cs:1076-1100`) exige además `Distance(receiver.Position, ball.Position) < PassArrivalRadius`
   (1,0, `MatchEngine.cs:24`). Si el receptor no está allí, el balón queda **suelto aunque la tirada hubiese
   salido bien**. **Medido: 16.433 pases (19,3% de las tiradas ganadas, 14,3% de todos los pases) acaban
   sueltos con la tirada a favor, con el receptor a 2,10 casillas de media del punto de llegada.** Eso es
   **tres veces** el canal de intercepción (4,5%) y es la mayor fuente de pérdida del pase.
   Es también por qué la tasa de acierto observada es 56,1% cuando la fórmula de `pass.baseSuccess`
   predice ~74%.
3. **¿Puede un tercero quedarse el balón que pasa por su casilla?** Un **rival**, sí, con el 2,99% de
   arriba. Un **compañero, nunca**: `MatchEngine.cs:909`,
   `if (player.Team == passer.Team || !CanTouchBall(player) || _ball.InterceptAttempted[i]) continue;`.
   Un compañero plantado en la línea es **completamente transparente**. Solo el receptor designado puede
   recibir, y solo en el tick de llegada.
4. **¿Existe el pase en profundidad a un espacio?** **No.** Todo destino sale de la posición de un
   compañero; sin receptor legal el destino es un punto fijo tres casillas adelante
   (`MatchEngine.cs:1302-1304`). Además, un compañero **marcado** ni siquiera es candidato
   (`Utility.cs:773`), así que la IA solo sabe pasar a compañeros **libres y esencialmente quietos**. El
   desmarque en carrera al que se le pone el balón por delante no está representado.
5. **Lo que ya existe y sostiene el pase en profundidad sin código nuevo de IA:**
   - `Utility.cs:451`: `Vec2 point = ball.InFlight ? ball.FlightTarget : ball.Position` — el que persigue
     corre al **destino del vuelo**, no a donde está el balón.
   - `MatchEngine.cs:561`: `NearestToBall` también se calcula contra `FlightTarget`. **El defensa más
     cercano a la casilla de destino ya es, por construcción, el perseguidor designado de su equipo**
     (precondición dura de AW-S, `Utility.cs:475`).
   - `Utility.cs:489-493`: el receptor previsto cobra `chaseBallIncomingPassBonus` = 700 y se le levanta el
     límite duro de zona (`IgnoreOuterLimit`).
   Es decir: **la carrera hacia la casilla de destino ya está disputada por los dos equipos.** Lo único que
   falta es un pase que apunte a una casilla vacía.

---

## 1. Principio de diseño que ordena todos los pasos

> **Comparar tiempos, no hacer crecer radios; puntuar, no descartar; cambiar la física antes que la
> decisión.**

Las tres mitades del principio son las tres razones por las que el paso 4 de
`plan-intercepcion-disparo.md` fracasó (§3), y las tres cosas que sí funcionaron en el paso 1 de AW-A (el
tick como paso del bucle, sin precálculo, sin radios nuevos).

---

## 2. Los pasos

Orden **no** negociable, y el motivo es de presupuesto: `possessionChanges` (24,02 sobre 12-28) es el
recurso escaso. Los pasos 1 y 3 **liberan** margen; los pasos 2, 4 y 5 lo **gastan**. Nunca se encadenan
dos pasos que gastan sin medir en medio.

| # | Palanca | Efecto esperado en `possessionChanges` | Ficheros |
|---|---|---|---|
| 0 | contadores INFO del pase | 0 (no toca motor) | `MatchReport`, `MatchMetrics`, `docs/balance.md` |
| 1 | pase al pie acotado | **baja** | `MatchEngine`, `tuning.json` |
| 2 | intercepción con geometría | **sube** | `MatchEngine`, `tuning.json` |
| 3 | el pasillo puntúa en la elección del pase | **baja** | `Utility`, `weights.json` |
| 4 | el pasillo puntúa en la decisión de tirar | baja poco | `Utility`, `weights.json` |
| 5 | pase en profundidad | **sube** | `Utility`, `MatchEngine`, `MatchPlayer`, `weights.json`, `EventType`/l10n |

---

### Paso 0 — instrumentación (sin cambio de comportamiento)

**Motivo.** Hoy el `summary.csv` no distingue un pase completado de uno interceptado de uno perdido, y los
pasos 1-5 mueven justamente esa mezcla. Sin esto se mide a ciegas, exactamente como pasó en el paso 0 de
AW-A con `Saves`.

**Cambios.** `MatchReport` ya lleva `PassesAttempted`/`PassesCompleted` por jugador
(`MatchReport.cs:39`, `players.csv` columnas 11-12). Faltan por equipo los dos desenlaces del fallo, que ya
se emiten como eventos (`PassFailed` con `Detail` `"intercepted"`, `"loose"`, `"cancelled"`):
- `MatchReport`: `PassesIntercepted[]` y `PassesLoose[]` (contadores incrementados donde se emite el
  evento, igual que `Saves` y `ShotsBlocked`).
- `Sim/Analysis/MatchMetrics.cs`: tres filas **INFO** (no gating, no exigen ADR de banda):
  `passCompletionRate` (referencia **56,11%**), `passInterceptRate` (**4,47%**), `passLooseRate`
  (**38,48%**).
- `docs/balance.md`: las tres filas en la tabla de informativas.

**Lo que NO se lleva al repositorio.** La cuota de «pases con rival en el pasillo» cuesta una pasada de
`DistanceToSegment` sobre 7 rivales **por pase, en todos los partidos**; con 115k pases por lote es un peaje
permanente por un número que solo se consulta al diseñar. Se queda como medición puntual con la copia
instrumentada de §0.2, que queda descrita ahí para poder rehacerla.

**Test.** `MatchRulesTests.ReportCountersAgreeWithTheEventStream` extendido a los dos contadores nuevos
(`PASS_ATTEMPTED == PASS_COMPLETED + intercepted + loose + cancelled`).
**Lote.** 2000 × semilla 1 y 2. **Criterio de salida:** tres filas nuevas, **el resto idéntico bit a bit**.
Si algo se mueve, el cambio no es lo que dice ser.

---

### Paso 1 — el pase al pie: el adelanto deja de ser una extrapolación ciega

**Es el primero a propósito.** Es el único paso que es ganancia neta (menos pérdidas accidentales,
`passChainAvgLength` arriba, `possessionChanges` abajo) y es el que **compra el presupuesto** que gastan
los pasos 2 y 5. Además responde la mitad literal de la anotación: *«los jugadores solo pueden recibir un
pase si el balón llega a donde están ellos»* — hoy el balón se lanza 1,44 casillas por delante de donde
están y en el 14,3% de los casos no hay nadie allí cuando llega.

**Regla.** El destino sigue adelantándose, pero por lo que el receptor **va a poder recorrer hacia donde
él mismo ha decidido ir**, con un tope absoluto en casillas:

```
dirección   = normalizar(receiver.TargetPoint − receiver.Position)   // adónde dice su utilidad que va
alcanzable  = ticks × receiver.SpeedPerTickMilli / 1000              // lo que le da tiempo a recorrer
lead        = min(alcanzable, Distance(receiver.Position, receiver.TargetPoint), pass.maxLeadCells)
target      = ClampToPitch(receiver.Position + dirección × lead)
```

Tres diferencias con lo de hoy, cada una atacando un fallo medido:
1. **La dirección sale de `TargetPoint`, no de `Velocity`.** `Velocity` es el paso del último tick e
   incluye el empuje de `BodySeparation` y los recortes de campo/área: extrapolarlo 12 ticks amplifica un
   zigzag de un tick en 1,4 casillas de error. `TargetPoint` es la casilla que la utilidad del receptor
   eligió este mismo tick (`Utility.Choose`, `Utility.cs:178`) y ya está acotada a su zona.
2. **`Distance(Position, TargetPoint)` acota el adelanto a donde el receptor de verdad quiere ir.** Si su
   objetivo está a media casilla, el pase no se le pone tres casillas por delante.
3. **`pass.maxLeadCells` es un techo absoluto.** Es literalmente el arreglo que la fila AW-A de
   `docs/pendientes.md` pedía para una futura vuelta al paso 4 («acotar el radio efectivo a un máximo
   absoluto, no solo bajar el factor»), aplicado aquí a la magnitud análoga.

**Parámetro nuevo.** `data/sim/tuning.json` → `pass.maxLeadCells`, **1.5** de partida (el adelanto medio
medido hoy es 1,44, así que 1,5 recorta la cola larga sin mover la mediana). Es resolución, no decisión, y
por eso va en `tuning.json` y no en `weights.json` (la misma frontera que declara el `_doc` de `block`).

**Cambio de apoyo, sin comportamiento propio.** `MatchPlayer.SpeedPerTickMilli` (int), recalculado una vez
por tick en `MatchEngine.UpdateContextCaches` con la fórmula que hoy vive en `MatchEngine.SpeedPerTick`
(`MatchEngine.cs:798-830`, que depende de `_tick` por la fatiga). Lo necesitan también los pasos 5 y, si se
quiere, cualquier futura comparación de tiempos. **Es entero**, se lee sin división en coma flotante en el
bucle de utilidad, y RT-023 queda contento.

**Tests.**
1. `PassLeadTests.ALeadIsCappedByTheAbsoluteMaximum`: receptor con `TargetPoint` a 6 casillas y velocidad
   alta, pase de 8 casillas → `Distance(target, receiver.Position) == maxLeadCells` exacto (aritmética
   exacta con `maxLeadCells = 1.5`).
2. `ALeadNeverOvershootsTheReceiverIntention`: `TargetPoint` a 0,4 casillas → adelanto 0,4, no más.
3. `AStandingReceiverGetsThePassAtHisFeet`: `TargetPoint == Position` → `target == receiver.Position`.
4. `MatchRulesTests` y `StatisticalTests` filtrados, en verde.

**Lote (2000 × 2 semillas) y vigilancia.**

| Métrica | Esperado | Parada |
|---|---|---|
| `passLooseRate` (INFO, paso 0) | **baja** desde 38,48 | si no baja, el diagnóstico está mal y se para |
| `passCompletionRate` (INFO) | sube desde 56,11 | — |
| `passChainAvgLength` | sube desde 2,31 | > 4,0 (techo de banda) |
| `possessionChanges` | baja desde 24,02 | < 12 |
| `shotsPerMatch` | sube algo | > 16 |
| `ballThirdMaxShare` | vigilar | > 50 |

**Ajuste permitido (uno).** `maxLeadCells` 1,5 → 1,0 si `passChainAvgLength` se va por arriba de 4 o
`shotsPerMatch` de 16. Si con eso no entra, se para y se informa: el siguiente lever sería
`pass.baseSuccess`, y tocarlo aquí escondería el diagnóstico.

---

### Paso 2 — la intercepción deja de ser ciega a la distancia (el núcleo de «no atravesar cuerpos»)

**Regla.** `InterceptChance` se multiplica por un factor de proximidad que vale 1 en el borde del radio
(donde hoy) y sube hasta `pass.interceptContactPercent` cuando el balón pasa **dentro del cuerpo** del
rival. Todo entero salvo la distancia:

```
dCenti        = Utility.Centi(Distance(player.Position, ball.Position))   // ya existe, Utility.cs:323
contactCenti  = player.BodyRadiusCentiCells                               // ya existe, MatchPlayer.cs:182
radiusCenti   = Utility.Centi(pass.interceptRadiusCells)
factor        = dCenti <= contactCenti
              ? pass.interceptContactPercent
              : 100 + (pass.interceptContactPercent − 100) × (radiusCenti − dCenti)
                                                          / (radiusCenti − contactCenti)
chance        = Bounded(InterceptChance(player, passer) × factor / 100)
```

**Por qué el radio de contacto sale de `/data/races` y no es un número nuevo.** `bodyRadius` ya existe
(0,28 no-muerto … 0,38 orco, `data/races/*.json`), ya lo consume `BodySeparation` para el empuje entre
cuerpos, y es exactamente la definición de «el balón le pasa por encima». Así **un orco tapa más que un
elfo sin escribir una sola regla nueva**, lo cual es temático y es gratis. No se introduce ningún radio
inventado: el único número nuevo es *cuánto* multiplica el contacto.

**Parámetro nuevo.** `data/sim/tuning.json` → `pass.interceptContactPercent`, **600** de partida
(×6 sobre el 2,5% de hoy → ~15% para un cuerpo clavado en la línea; con la pendiente por técnica y varios
rivales, la cuota efectiva medida subiría del 2,99% actual a la horquilla del 8-12%). Se elige deliberadamente
**por debajo** de la «intercepción casi segura» que sugiere el encargo: el paso 3 (que la IA deje de elegir
esos pases) es la mitad que de verdad los hace desaparecer, y llevar la física al 90% antes de que la IA lo
sepa es cargar todo el coste en `possessionChanges` de golpe.

**Lo que este paso arregla del tiro sin escribir nada más.** `TryBlockShot` (`MatchEngine.cs:973`) ya llama
a `InterceptChance`, así que **hereda el factor de proximidad automáticamente**. Un defensa clavado en la
línea del disparo pasa de ~1,5% a ~9%; uno a 0,85 casillas se queda donde está. `shot.blockChancePercent`
**no se toca** y recupera su significado honesto (el tiro va a 0,70 casillas/tick contra 0,25 del pase).

**Lo que NO se toca.** `pass.interceptRadiusCells` (0,9), `pass.interceptBaseChance` (250),
`pass.interceptTechniqueFactor` (10), `save.reachCells`, `shot.blockChancePercent`, y el número de intentos
por rival y pase (sigue siendo uno). Nada de la fórmula de la ADR 0041/0050 cambia: se multiplica por fuera,
igual que hace el canal de perk con `ProbabilityScale.Apply`.

**Determinismo.** No se consumen tiradas nuevas: el mismo `_rng.Chance` de siempre, con otro argumento.
El orden de recorrido es el de `_players` (índice ascendente), sin tocar. RT-024 sigue valiendo, pero todas
las semillas cambian de resultado, lo cual es esperado (pasó igual en AW-A paso 1) y **rompe los tests con
margen fino**: hay que revisar `RefereeAndAbilitiesTests` y compañía, que ya se rompieron una vez por esto.

**Tests (`Sim.Tests/Engine/PassCorridorTests.cs`).**
1. `AnOpponentOnTheLineInterceptsFarMoreOftenThanOneAtTheEdge`: dos escenarios idénticos salvo la fila del
   rival (0,0 y 0,85 del segmento), 200 semillas cada uno → la tasa del primero es al menos 4× la del
   segundo.
2. `TheEdgeOfTheRadiusBehavesExactlyAsBefore`: rival a `interceptRadiusCells − 0,01` → `factor == 100`
   (aritmética exacta, comparación del entero, no de la tasa).
3. `TheContactFactorUsesTheRaceBodyRadius`: mismo escenario con orco (0,38) y con no-muerto (0,28) → el
   orco entra en la banda de contacto a una distancia a la que el no-muerto todavía no.
4. `NoExtraRngIsConsumed`: mismo número de tiradas antes y después con el factor forzado a 100.

**Lote y vigilancia.**

| Métrica | Esperado | Parada |
|---|---|---|
| `passInterceptRate` (INFO) | **sube** desde 4,47 | — (es el objetivo) |
| `possessionChanges` | **sube** desde lo que dejara el paso 1 | > 28 |
| `passChainAvgLength` | baja | < 2,0 |
| `blockRate` (INFO) | sube desde 2,56 | > 12 (sería un partido de rechaces) |
| `shotsPerMatch` | baja algo | < 8 |
| `goalsPerMatch` (INFO) | baja algo | < 1,5 |

**Ajuste permitido (uno) — y una excepción explícita a la regla del ajuste.** Si `possessionChanges` o
`passChainAvgLength` se salen, **el primer movimiento NO es bajar `interceptContactPercent`: es hacer el
paso 3 y volver a medir el par.** El paso 3 es la mitad compensatoria por construcción (la IA deja de
elegir el pase que ahora se intercepta) y bajar la magnitud antes de instalarla es diagnosticar el síntoma.
Solo si el **par** 2+3 sigue fuera se baja `interceptContactPercent` 600 → 350, una vez, y si tampoco entra
se para y se informa.

---

### Paso 3 — el pasillo entra en la **elección** del receptor como puntuación, nunca como descarte

**Regla.** Un entero de peligro 0..100 por segmento, y dos sitios donde se cobra: el **ranking entre
receptores candidatos** y la **puntuación de la acción**.

```
LaneDanger(players, team, from, to):            // máximo, no suma (§3, riesgo 1)
  peor = 0
  para cada rival vivo:
    d = DistanceToSegment(from, to, rival.Position)          // ya existe, Utility.cs:1124
    si d >= context.passLaneRadiusCells: continuar
    contact = rival.BodyRadiusCentiCells
    danger  = d <= contact/100 ? 100
            : 100 × (Centi(radio) − Centi(d)) / (Centi(radio) − contact)
    peor = max(peor, danger)
  devolver peor
```

En `EvaluatePass` (`Utility.cs:748-825`):
- el ranking pasa de `advance − Centi(distance) × 20/100` a
  `advance − Centi(distance) × 20/100 − context.passBlockedLaneRankPenalty × danger / 100`;
- la puntuación pasa a `score = passOpenReceiverBonus − context.passBlockedLanePenalty × bestDanger / 100`
  (más los términos que ya hay).

**El descarte del pase largo se queda.** `Utility.cs:778` (`longPass && SegmentBlocked`) sigue como está:
es la contención declarada de la ADR 0030 contra el partido de balonazos, y quitarla en el mismo paso
haría imposible saber cuál de los dos cambios movió la métrica. Su sustitución por el término de
puntuación queda como **paso 3b opcional**, medido aparte, solo si el lote muestra que el descarte se ha
vuelto redundante (`passLaneRadiusCells` ya lee 0,6 desde datos y el término penaliza lo mismo).

**Parámetros nuevos.** En `data/ai/weights.json` → `context` (es **decisión**, no resolución):
- `passLaneRadiusCells`: **0.6**. Es el traslado a datos de `Utility.cs:72`, sin cambiar el valor; deja de
  ser una constante de código, que es lo que RT-096 pide de un número de balance. Cero efecto en el lote.
- `passBlockedLanePenalty`: **200**. Elegido a propósito **por debajo** de `passOpenReceiverBonus` (220):
  un pasillo totalmente tapado casi anula el bono del receptor abierto pero **nunca** empuja la acción
  hasta el acantilado de `passNoReceiverPenalty` (−600), que es la trampa exacta que hundió al paso 4 (§3).
- `passBlockedLaneRankPenalty`: **150** (en centésimas de casilla de «avance equivalente»: un pasillo
  tapado del todo vale como retroceder 1,5 casillas al comparar receptores).

**Lo que este paso **no** puede hacer, por construcción.** No puede reducir el número de receptores legales.
`EvaluatePass` sigue devolviendo un receptor siempre que hoy devuelva uno; solo cambia **cuál** y **cuánto
puntúa**. Es la diferencia estructural con el paso 4 descartado y es la razón por la que
`passChainAvgLength` no puede caer por el mecanismo que lo hundió entonces.

**Tests (`Sim.Tests/Engine/UtilityTests`).**
1. `ThePassPrefersTheOpenReceiverOverTheBlockedOne`: dos compañeros a la misma distancia y el mismo avance,
   uno con un rival clavado en la línea → el receptor elegido es el limpio. **Antes de este paso, el test
   falla** (el desempate actual es por `advance` y luego por orden de índice).
2. `ABlockedLaneNeverDiscardsTheReceiver`: un solo compañero legal, con el pasillo tapado del todo →
   `eval.Receiver` no es null y la puntuación **no** es `−passNoReceiverPenalty`. Es el test que
   materializa la lección del paso 4.
3. `LaneDangerIsExactAtTheBoundaries`: 0 en el radio, 100 dentro del `bodyRadius`, y el valor entero exacto
   a mitad de camino (aritmética exacta).
4. `LaneDangerTakesTheWorstOpponentNotTheSum`: dos rivales tapando → 100, no 200.

**Lote y vigilancia.**

| Métrica | Esperado | Parada |
|---|---|---|
| `passInterceptRate` (INFO) | **baja** respecto del paso 2 | si no baja, la IA no se ha enterado: revisar antes de tocar magnitudes |
| `passChainAvgLength` | **sube** respecto del paso 2 | < 2,0 |
| `possessionChanges` | **baja** respecto del paso 2 | > 28 |
| `passCompletionRate` (INFO) | sube | — |
| `ballThirdMaxShare` | vigilar: la IA prefiere el pase limpio, que suele ser el corto y hacia atrás | > 50 |

**Ajuste permitido (uno).** `passBlockedLanePenalty` 200 → 120 si `ballThirdMaxShare` se dispara (señal de
que el equipo se ha vuelto conservador y circula en su tercio).

---

### Paso 4 — el mismo criterio para la **decisión** de tirar

**Regla.** `EvaluateShoot` gana un único término:
`score −= context.shootBlockedLanePenalty × LaneDanger(players, p.Team, p.Position, Pitch.GoalCenter(p.Team)) / 100`.

**Por qué es un paso aparte del 2.** El paso 2 ya arregló la **física** del tiro bloqueado gratis (`TryBlockShot`
hereda el factor). Este arregla la **decisión**: hoy `EvaluateShoot` no sabe que hay un cuerpo en medio y
por eso el 47,4% de los tiros salen contra un defensa. Meterlo junto al 2 impediría saber cuál de los dos
mueve `shotsPerMatch`, que es la métrica sensible aquí.

**Parámetro nuevo.** `data/ai/weights.json` → `context.shootBlockedLanePenalty`, **250** de partida
(comparable a `shootInRangeBonus` = 388: un tiro totalmente tapado pierde dos tercios de su bono de estar
en rango, pero un delantero `Scorer` con buena técnica todavía puede decidir tirarla; no es un veto).

**Tests.**
1. `TheShotIsWorthLessWithADefenderOnTheLine`: mismo tirador, misma distancia, con y sin defensa en la
   línea → la puntuación cae exactamente `shootBlockedLanePenalty × danger / 100`.
2. `ADefenderBesideTheLineDoesNotCountAsBlocking`: defensa a la misma distancia pero fuera del radio → 0.

**Lote y vigilancia.**

| Métrica | Esperado | Parada |
|---|---|---|
| `shotsPerMatch` | **baja** (se dejan de intentar los tiros tapados) | < 8 |
| `shotsOnTargetShare` (INFO) | sube (los que se intentan son mejores) | — |
| `blockRate` (INFO) | **baja** respecto del paso 2 | — (es el objetivo) |
| `goalsPerMatch` (INFO) | estable o algo arriba | < 1,5 o > 4,5 |
| `scorelineShare_1-0_to_3-2` | IN | OUT |

**Ajuste permitido (uno).** `shootBlockedLanePenalty` 250 → 150 si `shotsPerMatch` baja de 8.

---

### Paso 5 — el pase en profundidad (la otra mitad literal de la anotación)

Es el paso más grande y el único que introduce una acción nueva. **No se planifica en detalle hasta cerrar
los pasos 1-4 y medir el margen que quede**; lo de abajo es el diseño, no el encargo.

**Acción nueva.** `PlayerAction.ThroughPass`, tercera banda del pase, siguiendo el patrón exacto con el que
la ADR 0030 §1 partió `Pass` en `ShortPass`/`LongPass`: entrada propia en `weights.json` → `base` por
posición (Forward alto, Goalkeeper 0) y en `tactical`, más su pendiente por técnica. Los datos mandan; el
código solo la evalúa.

**Candidatos (acotados y deterministas).** Para cada compañero que **va hacia delante**
(`(mate.TargetPoint.X − mate.Position.X) × direction > 0`), tres casillas candidatas: su posición más el
vector unitario hacia su `TargetPoint` por `L ∈ {2, 3, 4}` casillas (un array `static readonly` como
`SpaceDistances` de `EvaluateFindSpace`), acotadas a campo y a la línea defensiva rival con el mismo
`OffsideLineColumn` + `findSpaceLineMarginCells` de AW-Q. **El recorte por la línea no es cosmético**: sin
él, la casilla de destino y la casilla a la que la propia utilidad del receptor le deja llegar divergen, y
el balón muere allí.
Coste acotado: ≤6 compañeros × 3 casillas = 18 candidatos por decisión, del mismo orden que
`EvaluateFindSpace` ya paga (RT-051: no crece con el tablero).

**Legalidad: una carrera en ticks, entera y sin radios.** Esta es la traducción honesta de las dos fuentes
que el encargo pide citar, y es la parte que hace que este paso **no** sea el paso 4 descartado:

```
ticksBall     = FlightTicks(Distance(passer.Position, cell), ball.passSpeedCellsPerTickMilli)
ticksReceiver = ceil(Distance(mate, cell)   × 1000 / mate.SpeedPerTickMilli)
ticksDefender = min sobre rivales de  ceil(Distance(rival, cell) × 1000 / rival.SpeedPerTickMilli)

legal  ⟺  ticksReceiver ≤ ticksBall + context.throughPassLateTicks
     ∧    ticksReceiver + context.throughPassMarginTicks ≤ ticksDefender
```

- `FlightTicks` es el helper que ya existe (`MatchEngine.cs:1378`), extraído a estático compartido.
- `SpeedPerTickMilli` es el campo entero que instala el paso 1.
- **Es una comparación de números de tick: adimensional.** No escala con el tamaño del campo, que es
  exactamente lo que le faltaba al paso 4 en una rejilla de 5×16.
- **Fuentes.** gfootball `ElizaController::_GetPassingOdds`
  (`elizacontroller.cpp:1112-1159`, citado en `docs/referencia-motores-futbol.md` §2) compara
  `ballToIntersect_sec` contra `oppToIntersect_sec` y convierte la diferencia recortada en `odds = 1 −
  peligro`, que usa para **elegir entre tres puntos de mira**, no para descartarlos.
  librcsc/HELIOS-base (§1 del mismo documento) resuelve la intercepción comparando
  `control_area + max_speed × steps` contra la distancia **en ciclos**, que es literalmente
  `ticksReceiver` contra `ticksBall`. Las dos comparan **tiempos**; ninguna hace crecer un radio en metros.

**Puntuación.** `context.throughPassBase + avance × context.findSpaceAdvanceBonusPerCell / 100
− context.passBlockedLanePenalty × LaneDanger(passer → cell) / 100 + Slope(throughPassTechniqueSlope,
p.Technique)`. Un pase en profundidad **también** tiene que atravesar el pasillo: se reutiliza el término
del paso 3, no se inventa otro.

**Resolución.** `Ball.IsThroughPass` (bool, limpiado en `LaunchPass`). `PassReceiver` = el corredor, para
que `chaseBallIncomingPassBonus` y `NearestToBall` (§0.4.5) manden a los dos equipos a la casilla **sin una
sola línea nueva de IA**. En `ResolvePassArrival`, una rama nueva: se lo lleva **quien esté más cerca del
punto de llegada dentro de `PassArrivalRadius`, sea el receptor o un defensa**, con empate por id
ascendente (RT-097); si no hay nadie, suelto como hoy. La carrera la resuelve la simulación tick a tick, no
una predicción — misma filosofía que el paso 1 de AW-A («el tick es el paso del bucle»).
Si el defensa llega antes: `PassFailed` con `Detail` nuevo `"beaten"` y `Recovery`, no `"intercepted"` (no
lo cortó en vuelo, ganó la carrera). Eso significa **entrada nueva en `EventType`, texto es/en en
`data/l10n/` y fila en `UiText`**, y por tanto commits separados de `/Sim`, `/data` y `/Game`.

**Parámetros nuevos** (`weights.json` → `context`): `throughPassLateTicks` (**4**: el receptor puede llegar
hasta cuatro ticks después que el balón, porque el balón se para donde cae),
`throughPassMarginTicks` (**3**: hay que ganarle al defensa por al menos tres ticks para que valga la pena),
`throughPassBase` (**180**) y `throughPassTechniqueSlope` (**12**, la pendiente más inclinada de la tabla
después del pase largo: el pase en profundidad es lo más difícil que hay).

**Tests.**
1. `AThroughPassIsIllegalWhenTheDefenderArrivesFirst`: aritmética exacta de los tres conteos de ticks.
2. `AThroughPassIsIllegalWhenTheReceiverIsTooLate`.
3. `TheRaceIsResolvedByArrival`: escenario con el defensa un tick más rápido → el evento es `"beaten"`,
   no `"completed"`, en las 50 semillas (la carrera no tiene tirada; es determinista por geometría).
4. `AStandingTeammateIsNeverAThroughPassCandidate`.
5. Determinismo RT-024 y `ReportCountersAgreeWithTheEventStream` con el desenlace nuevo.

**Lote y vigilancia.** Es el paso que más gasta presupuesto:

| Métrica | Esperado | Parada |
|---|---|---|
| `possessionChanges` | **sube** (un pase en profundidad fallado es una pérdida limpia) | > 28 |
| `passChainAvgLength` | baja algo | < 2,0 |
| `shotsPerMatch` | **sube** | > 16 |
| `ballThirdMaxShare` | **sube** (es su objetivo: llevar el balón arriba) | > 50 |
| `goalsPerMatch` (INFO) | sube | > 4,5 |
| `scorelineShare_1-0_to_3-2` | IN | OUT |

**Ajuste permitido (uno).** `throughPassMarginTicks` 3 → 5 (exigir más ventaja) si `possessionChanges` se
sale. Si no basta, se para: la palanca siguiente sería el peso base en `weights.json`, y eso es una
conversación de estilo de juego con el revisor, no un ajuste.

---

## 3. Riesgos: qué rompió el paso 4 descartado y por qué esto no lo repite

El paso 4 de `docs/plan-intercepcion-disparo.md` §6 (radio de pasillo dependiente del tiempo) se implementó
y se descartó sin commitear el 7 sep 2026 (fila AW-A de `docs/pendientes.md`). Falló por **tres** razones, y
las tres están excluidas por construcción aquí:

**Riesgo 1 — el radio crecía sin techo, y en 5×16 eso es medio campo.**
Su radio efectivo era `PassLaneRadius + closingCellsPerCellTraveled × t × longitud`, que en un pase largo
llegaba a 2-4 casillas **en un campo de 5 filas de ancho**. La propia nota de cierre lo dice: *«una futura
vuelta necesitaría rediseñar el crecimiento del radio (por ejemplo con un máximo absoluto en casillas, no
solo el factor lineal)»*.
**Aquí:** ningún paso hace crecer un radio. El paso 2 usa un radio **fijo** (0,9, el de hoy) y varía la
*probabilidad* dentro de él. El paso 3 usa un radio **fijo** (0,6, el de hoy, ahora en datos) y devuelve un
entero **acotado a 0..100** (`max`, no suma: dos rivales tapando siguen valiendo 100, precisamente para
que el término no pueda crecer con la densidad). El paso 1 tiene un techo absoluto en casillas
(`pass.maxLeadCells`) que es el arreglo que la nota pedía. El paso 5 no usa radios: compara **ticks**, que
es adimensional y no escala con el tablero.

**Riesgo 2 — descartaba en vez de puntuar, y en `EvaluatePass` un descarte no es un descarte, es un
acantilado.** Este es el mecanismo que hay que nombrar y que no está escrito en ninguna parte del repositorio:
`EvaluatePass` guarda **un solo** receptor (el mejor por `rank`) y, si no queda ninguno, la acción no baja
un poco — se lleva `score = −context.passNoReceiverPenalty` = **−600** (`Utility.cs:796`). Con `ShortPass`
en 560 base para un centrocampista, ese −600 la manda por debajo de `Dribble`. Así que quitar receptores
**no mueve el pase a otro sitio: elimina el pase**, el portador regata, le entran, y la cadena termina.
Por eso el paso 4 hundió `passChainAvgLength` sin mejorar `possessionChanges`: no es que los pases fallaran
más, es que **había menos pases**.
**Aquí:** ni el paso 3 ni el paso 5 quitan un solo candidato. El paso 3 solo **reordena y resta puntos**,
con la magnitud elegida (200) explícitamente por debajo del bono que compensa (220) y a un orden de
magnitud del acantilado (600). El paso 5 solo **añade** una acción, nunca quita receptores de las otras dos
bandas. El test 2 del paso 3 (`ABlockedLaneNeverDiscardsTheReceiver`) existe para que esta propiedad no se
pierda en un refactor futuro.

**Riesgo 3 — cambiaba la decisión sin cambiar nunca la física, así que no tenía mitad compensatoria.**
El paso 4 enseñaba a la IA a temer un pase que en realidad llegaba el 97% de las veces (§0.3): le estaba
enseñando una mentira, y el coste no se recuperaba por ningún lado.
**Aquí el orden es el inverso y es deliberado:** el paso 2 hace que el pase tapado **de verdad** se
intercepte, y solo después el paso 3 se lo cuenta a la IA. Los dos empujan `possessionChanges` en
direcciones opuestas por construcción, así que el par tiene un punto de equilibrio; el paso 4 no lo tenía.
De ahí la excepción explícita del paso 2: **si el paso 2 se sale de banda, el primer movimiento es hacer el
paso 3, no bajar la magnitud del paso 2.**

**Riesgo 4 — el presupuesto de `possessionChanges` es finito y el orden lo consume.**
Baseline 24,02 sobre 12-28. Pasos 1, 3 y 4 lo liberan; pasos 2 y 5 lo gastan. El orden 1→2→3→4→5 alterna a
propósito y nunca encadena dos pasos que gastan. Si tras el paso 4 el margen restante es menor de ~1,5
puntos, **el paso 5 no se hace**: se informa al revisor y se le ofrece la ADR de banda de RT-057 con los
datos, que es lo que el propio plan de AW-A dejó como decisión abierta 2.

**Riesgo 5 — todas las semillas se mueven en los pasos 1, 2 y 5.**
Cambia el número y el argumento de las tiradas consumidas por partido. Es esperado (pasó en AW-A paso 1) y
no es un fallo, pero rompe los tests con margen estadístico fino. `RefereeAndAbilitiesTests.
TheInitialCriterionChangesTheMatch` ya se rompió una vez por exactamente esto y se arregló subiendo la
muestra, no tocando el motor. Hay que preverlo y aplicar el mismo criterio (RT-056: un test que falla por
mala suerte es un test mal escrito), no revertir.

**Riesgo 6 — cosas que deliberadamente NO se proponen, con motivo.**
- **Que un compañero desvíe el pase de su propio equipo.** Sería la lectura literal de «no atravesar
  cuerpos» y es cierto que hoy un compañero en la línea es 100% transparente (`MatchEngine.cs:909`). Pero
  cada desvío así es un pase perdido **sin** cambio de posesión: pega directo a `passChainAvgLength` (2,31,
  el margen más estrecho que hay) y **no tiene mitad compensatoria** — la IA no puede evitar a sus propios
  compañeros sin dejar de tener receptores, que es el Riesgo 2 otra vez. Se deja fuera y se anota.
- **Descartar el pase cuando hay un rival cerca del pasador** (la formulación literal del encargo: «un rival
  sobre la línea a menos de X casillas del pasador bloquea el pase raso»). Es un descarte, es decir el
  Riesgo 2. La medición dice además que **el 84% de los pases con el pasillo tapado tienen ese rival a
  ≤2 casillas del pasador** (37.798 de 45.088): descartarlos sería quitar de golpe el 33% de todos los pases
  del partido. El paso 2 consigue el mismo efecto **por la resolución** (ese pase sale, y se pierde) sin
  tocar la oferta de receptores, y el paso 3 hace el resto por la puntuación. **Misma intención, sin el
  acantilado.**
- **Anchura de la portería / dispersión del punto de mira.** Sigue abierta desde `plan-intercepcion-disparo.md`
  §8; toca reglas de juego (`shootAnglePenaltyPerRow`, la definición de «fuera») y no entra aquí.

**Riesgo 7 — coste por tick.** `LaneDanger` es O(rivales) por candidato: ≤7 rivales × ≤7 candidatos × 2
bandas = ~98 `DistanceToSegment` por decisión, cada 2 ticks — el mismo orden que `EvaluateFindSpace` ya paga
con `SpaceDirections × SpaceDistances × NearestOpponentDistance`. RT-051 (la evaluación no crece con el
tablero) se respeta: nada de esto depende del número de casillas. Como control de humo, vigilar
`partidos/s` en el lote (hoy 121/s en Release, 2000 partidos en 16,5 s).

---

## 4. Motor y render: qué parte es cada una

**El render es honesto; lo que el revisor ve es lógica.** Estimación: **~95% motor, ~5% render**, y el 5%
es acabado, no corrección.

- `Game/Ui/MatchPitchView.cs:165-172` interpola el balón con `Mathf.Lerp(BallAt(frame), BallAt(frame+1),
  Alpha)`. Es **exactamente** la interpolación que RT-020 permite en el render, sobre las posiciones reales
  de dos ticks consecutivos, y el docstring de la clase (`MatchPitchView.cs:14-16`) lo declara: *«No calcula
  nada (RT-014)»*. `MatchTrace` guarda la posición del balón todos los ticks. **El render no inventa ni un
  píxel de trayectoria.**
- No hay ningún efecto de suavizado, spline o predicción que pudiera «saltarse» un cuerpo: el balón dibujado
  pasa por donde el motor dice que pasa.
- Lo que el render sí hace es **hacerlo visible**: un pase avanza 0,25 casillas por tick, así que cruzar el
  diámetro de un cuerpo (~0,6 casillas) le lleva 2-3 ticks, es decir 3 fotogramas legibles a x1 con 15
  ticks/s. El ojo tiene tiempo de sobra para leer «le ha atravesado». Un motor que interceptase el 3% de
  las veces pero con el balón dibujado en saltos de 2 casillas por tick no habría generado esta anotación.
  El campo animado no causó el problema: lo destapó, igual que destapó AW-A.
- **Único trabajo de `/Game` que merece la pena**, y solo después de los pasos 2-4: dar realimentación
  visible al bloqueo y a la intercepción (un destello en el que bloquea, ya que `SHOT_BLOCKED` existe como
  evento desde AW-A paso 3 y hoy solo alimenta el ticker de texto). Es acabado, no cambia ninguna métrica,
  y va en commit separado de `/Sim` por convención.

---

## 5. Resumen del orden propuesto

| Paso | Palanca | Parámetro nuevo | Dónde | Métrica que lo vigila | Esfuerzo |
|---|---|---|---|---|---|
| 0 | contadores `PassesIntercepted`/`PassesLoose` + 3 filas INFO | — | `MatchReport`, `MatchMetrics` | todo idéntico | pequeño |
| 1 | pase al pie acotado | `pass.maxLeadCells` = 1.5 | `tuning.json` | `passLooseRate` ↓, `passChainAvgLength` ↑ | medio |
| 2 | intercepción con factor de proximidad por `bodyRadius` | `pass.interceptContactPercent` = 600 | `tuning.json` | `passInterceptRate` ↑, `possessionChanges` ↑ | medio |
| 3 | el pasillo puntúa la elección del receptor | `passLaneRadiusCells` 0.6, `passBlockedLanePenalty` 200, `passBlockedLaneRankPenalty` 150 | `weights.json` | `passInterceptRate` ↓, `passChainAvgLength` ↑ | medio |
| 4 | el pasillo puntúa la decisión de tirar | `shootBlockedLanePenalty` = 250 | `weights.json` | `shotsPerMatch` ↓, `shotsOnTargetShare` ↑ | pequeño |
| 5 | pase en profundidad (`ThroughPass`) | `throughPassLateTicks` 4, `throughPassMarginTicks` 3, `throughPassBase` 180, `throughPassTechniqueSlope` 12 | `weights.json` | `ballThirdMaxShare` ↑, `possessionChanges` ↑ | grande |

Se propone **0 → 1 → 2 → 3 → 4 y parar para revisión jugando la build** (el revisor pidió ver esto en
pantalla), y solo después decidir si queda presupuesto de `possessionChanges` para el paso 5.

**Decisión que se deja al revisor, no bloquea los pasos 0-4:** el paso 5 introduce una **acción nueva** y un
desenlace nuevo del pase (`"beaten"`), es decir toca `docs/requisitos.md` §RT-092 (lista de acciones
mínimas) y el flujo de eventos. Es regla de juego, así que necesita ADR y visto bueno antes de escribirse.
