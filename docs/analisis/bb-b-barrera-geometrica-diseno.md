# Design gate — BB-B como barrera geométrica

Precede a cualquier cambio de código. Responde a los puntos 1-6 exigidos antes de tocar `/Sim`, con la
evidencia de código que los sostiene. `docs/pendientes/BB-B.md` tiene el historial completo (parche
temporal, veredicto del revisor, hermanos). Este fichero es el diseño de reemplazo.

## 1. Qué significa "balón en juego" en una reanudación

No es un instante, es un **intervalo con dos bordes**, y el motor ya distingue ambos sin necesidad de
estado nuevo:

- **Borde de entrada** (cuándo deja de estar muerto): el tick en que `ResolveRestart` llama a
  `TakeRestart`/`TakePenalty` y le da la posesión al sacador (`SetOwner(taker)`,
  `MatchEngine.cs:2841`). Ahí `_restartTicksLeft` llega a 0 y `ctx.BallDead` pasa a `false`
  (`MatchEngine.cs:766`).
- **Borde de salida del riesgo** (cuándo deja de tener sentido protegerlo): no es el mismo tick. El balón
  está técnicamente "en juego" desde `TakeRestart`, pero **sigue en el pie del sacador** hasta que lo
  suelta —pase, tiro, regate interrumpido por pérdida—. RF-052 pide que el primer contacto ocurra "en los
  2 primeros segundos", no en el primer tick: el saque necesita ese margen para que el sacador decida y
  golpee, y es exactamente la ventana que la medición del revisor cronometró en 5 ticks (0,33 s) antes del
  parche y que **el árbol sin parchear** ya viola en 7/179 casos.

Definición operativa: **"balón en juego" para efectos de esta barrera es "el sacador ya no es su dueño"**,
no "`ctx.BallDead` es falso". Son cosas distintas y el parche descartado las confundía a medias: medía la
segunda con un estado nuevo (`KickoffPending`) en vez de derivar la primera del dueño del balón, que el
motor ya sabe en todo momento (`ctx.Ball.Owner`).

## 2. Qué acciones quedan prohibidas durante la transición

Ninguna, en el sentido de "descartar la acción". La reformulación clave del rediseño es que **no hace
falta prohibir ninguna acción de utilidad**: `EvaluateTackle` y `EvaluateBlock` siguen evaluando exactamente
igual que siempre. Lo que cambia es que **el rival no puede estar donde su propia evaluación necesitaría
que estuviera** para que la entrada o la carga tengan alcance. Es la diferencia entre "no se puede hacer
esto" (regla de comportamiento, invisible si no se ve en pantalla) y "no se puede estar aquí" (regla de
posición, visible por construcción). El principio de CLAUDE.md "observable > invisible" se lee así: si el
rival tiene prohibido acercarse, tiene que **verse** que no se acerca, no solo que su entrada falla en
silencio estando pegado al sacador (que es exactamente lo que medió el revisor: 0,75 casillas de media con
el parche puesto).

## 3. Condición espacial mínima que impide el comportamiento observado

Ya existe en el código, para un caso: `EnforceFreeKickClearance` (`MatchEngine.cs:2692`, ADR 0090).
Durante la cuenta atrás del saque de falta, todo rival a menos de `freeKickClearanceCells` (**2,0**
casillas) del balón es reubicado al borde de esa distancia, con velocidad a cero. Es una barrera real,
observable, medida y en verde desde el 9 sep 2026.

Los dos radios de acción que esta barrera tiene que superar están en `data/ai/weights.json`:

| Acción | Parámetro | Valor |
|---|---|---|
| Entrada (`EvaluateTackle`, rama del poseedor) | `tackleDistanceMaxCells` | 1,0 |
| Carga (`EvaluateBlock`) | `blockReachMaxCells` | 1,2 |

`2,0` dobla holgadamente el mayor de los dos (0,8 casillas de margen sobre 1,2). Es el mismo motivo por el
que ADR 0090 nunca tuvo el problema que tiene BB-B: la barrera de la falta cubre con margen ambas acciones
sin necesidad de tocar ninguna de las dos. **No hace falta buscar un valor nuevo**: la condición mínima es
"radio ≥ máx(tackleDistanceMaxCells, blockReachMaxCells) + margen de body separation", y 2,0 ya la cumple
con el mismo margen que lleva siete días en producción sin regresión conocida.

Lo que le falta a `EnforceFreeKickClearance` para cubrir BB-B no es el radio, es la **ventana**: solo se
llama mientras `_restartTicksLeft > 0` (`MatchEngine.cs:569`, condicionado además a
`_pendingRestart == RestartKind.FreeKick`), es decir, solo cubre el borde de entrada del intervalo del
punto 1, nunca el borde de salida del riesgo. Ahí es donde vivía el hueco de BB-B, y por construcción,
el mismo hueco existe **ya hoy** en el propio saque de falta para su ventana posterior a `TakeRestart`
(explica por qué la tabla de hermanos de `BB-B.md` mide 7 entradas en 148 saques de falta pese a tener ya
una barrera).

## 4. Los cuatro hermanos

`_restartTeam`, `_restartPoint` y `_restartTaker` ya son genéricos: `BeginRestart` los fija igual para
`ThrowIn`, `Corner`, `GoalKick`, `Kickoff` y `FreeKick` (`MatchEngine.cs:2600-2681`), y `SelectTaker` es
una única función compartida por los cinco (comentario AZ-A, `MatchEngine.cs:2712`). La única pieza
específica de un tipo es la **llamada** a `EnforceFreeKickClearance`, no el estado que usa. Generalizar la
barrera no es "arreglar 4 hermanos", es **quitar el `if (_pendingRestart == RestartKind.FreeKick)`** que
hoy limita a uno solo un mecanismo que ya es genérico. Corner no está en la lista de hermanos de
`BB-B.md` (0 entradas medidas) pero comparte la misma máquina de estado y no hay motivo para excluirlo del
mismo mecanismo: se cubre gratis.

## 5. `EvaluateBlock`

Sí necesitaba protección (hallazgo del revisor) y la solución geométrica se la da **sin tocarlo**: si
ningún rival puede estar a menos de 2,0 casillas del balón, ninguno puede estar a menos de
`blockReachMaxCells` (1,2) tampoco. No hace falta un segundo guard ni distinguir `Tackle` de `Block` en
ningún punto del diseño — es la ventaja de resolver en el espacio en vez de en la acción: cubre acciones
que ni siquiera se han enumerado explícitamente (un tercer tipo de contacto futuro quedaría cubierto
igual, sin cambio de código).

## 6. Por qué esto no es una regla temporal invisible con otro nombre

Hace falta **una pieza de estado** — saber cuándo el sacador ha soltado el balón para dejar de aplicar la
barrera — pero la distinción con `KickoffPending` no es "tiene estado" contra "no tiene estado", es **qué
hace el estado**: `KickoffPending` alimentaba un `if` que descartaba una acción entera sin mover a nadie
(la pantalla no correspondía con la regla). El estado propuesto aquí (ver §7) alimenta una reubicación de
posición, igual que ya hace `EnforceFreeKickClearance` con la cuenta atrás: la pantalla **es** la regla. Si
se quita el flag, el efecto en pantalla desaparece con él — no hay ningún comportamiento que solo exista
"en la cabeza" del motor.

## 7. Propuesta de abstracción mínima reutilizable

No es un mecanismo nuevo: es extender el que ya existe en dos ejes.

1. **Generalizar la llamada**, no la lógica. `EnforceFreeKickClearance` se renombra a
   `EnforceRestartClearance` y se llama para las cinco reanudaciones (`ThrowIn`, `Corner`, `GoalKick`,
   `Kickoff`, `FreeKick`) durante la cuenta atrás, no solo para `FreeKick`. Cero jugadores nuevos que
   proteger que no estuvieran ya cubiertos por el mismo bucle.
2. **Cubrir el borde de salida del riesgo**, no solo el de entrada. Se añade un campo genérico —
   `_restartClearanceOwner: MatchPlayer?` — fijado en `TakeRestart`/`TakePenalty` (donde ya existe la
   variable local `taker`, para las cinco reanudaciones a la vez, sin condicional por tipo) y limpiado en
   `UpdateContextCaches` en cuanto `ctx.Ball.Owner` deja de ser él (la misma regla de limpieza que ya tenía
   `_kickoffPendingTaker`, generalizada). Mientras `_restartClearanceOwner` no sea null, `Step` sigue
   llamando a `EnforceRestartClearance` aunque `_restartTicksLeft` ya sea 0.
3. **El centro de la barrera es el balón, no el punto fijo de saque.** `EnforceFreeKickClearance` usa
   `_restartPoint` porque durante la cuenta atrás el balón está aparcado ahí (`ParkBall`) y coinciden. Para
   cubrir también la ventana posterior a `TakeRestart` (el sacador puede empezar a regatear) hay que medir
   contra `_ball.Position` en vez de `_restartPoint`; en la cuenta atrás da el mismo resultado porque el
   balón no se mueve, así que no es un cambio de comportamiento para el caso ya medido de ADR 0090, solo
   una generalización correcta para el caso nuevo.
4. **Un solo radio para las cinco**, no cinco parámetros. `freeKickClearanceCells` pasa a llamarse
   `restartClearanceCells` en `RestartTuning`/`tuning.json` y se aplica a las cinco por igual. Ver §8: es
   la pregunta abierta que corresponde a `game-design-review`, no una decisión ya tomada aquí.

Ningún cambio en `Utility.cs`. Ninguna primitiva nueva en el sistema de utilidad. El área de cambio entera
es la máquina de estado de reanudaciones en `MatchEngine.cs`, ya dueña de este mecanismo.

## 8. Preguntas para `game-design-review` — resueltas

1. **¿Un radio único o uno por tipo de reanudación?** **Empezar con el único valor 2,0** (el ya medido y
   en verde por ADR 0090) para las cinco. No existe ningún RF que pida explícitamente una regla de zona
   para el saque de puerta —a diferencia del fútbol real, donde sí es obligatoria—, así que inventarla
   ahora sería añadir una regla nueva no pedida (protocolo de diseño, pregunta 4: "si no existe, no la
   inventes sin decirlo"). La medición del punto 11 discrimina esto sin necesidad de decidirlo a priori:
   si con 2,0 el saque de puerta sigue teniendo entradas en la ventana —el área mide 2×4 casillas y un
   punto de saque cerca del borde podría dejar el radio corto en algún eje—, se escala a una regla de zona
   como segunda vuelta, con datos. No antes.
2. **¿Proteger también a un compañero marcado lejos del balón?** **No ampliar el diseño todavía.** RF-057
   ya exige que el contacto ocurra solo entre quien disputa el balón o está en la trayectoria de la jugada
   activa; un compañero marcado lejos del balón en el instante del saque —cuando el balón casi no se ha
   movido— muy probablemente ya falla `IsInActivePlay` (ni está cerca del balón ni en el corredor hacia la
   portería rival) sin necesidad de que la barrera lo cubra. Se mide explícitamente en el punto 11
   (cargas a un jugador que no es el sacador, durante la ventana); solo si el número no es cero es una
   segunda vuelta con datos, no una suposición de partida.
3. **Duración máxima del `_restartClearanceOwner`.** **No añadir techo ahora.** Hoy no existe ningún perk
   que premie retener el balón indefinidamente en una reanudación, así que un límite de ticks sería
   protegerse de un caso que no puede ocurrir todavía —sobre-ingeniería, no la lectura conservadora que
   pide CLAUDE.md ("documenta la limitación, no fabriques una excepción" aplica también al revés: no
   fabriques la protección antes de que exista el problema). Queda **documentado como riesgo de vigilancia**
   para cuando el catálogo C1/C2 introduzca un perk de retención de balón: en ese momento, el techo (RF-052,
   ~2 s / 30 ticks) se añade junto con ese perk, no antes.

## 8b. Alternativa considerada y descartada

**Repulsión continua de movimiento** en vez de corrección posicional discreta: en lugar de reubicar
(`clamp`) al rival que invade el radio, sesgar su vector de movimiento en `UpdatePlayer` para que nunca
llegue a entrar. Más suave visualmente, pero rompe el patrón ya establecido (`BodySeparation.Resolve`,
`EnforceFreeKickClearance` — ambos corrigen posición después del hecho, no antes) y es más difícil de
mantener determinista tick a tick con aritmética entera (RT-021, RT-023) que un clamp discreto. Se
descarta por consistencia con el mecanismo ya probado, no por inferioridad conceptual.

## 9. Medición barata antes de implementar (punto 9 del proceso)

No hace falta una búsqueda de parámetros: el valor candidato (2,0) ya está en producción y medido para un
hermano. La medición barata que discrimina es una sola pregunta, no un barrido: **¿basta 2,0 aplicado a las
cinco reanudaciones para llevar a 0 las entradas dentro de la ventana en los cuatro hermanos medidos por el
revisor (kickoff, banda, puerta, falta), sin sacar de banda `tacklesPerMatch`/`injuriesPerMatch`
(RT-056)?** Si la respuesta es sí con el mismo lote de 60 semillas que usó el revisor, no hace falta
segunda vuelta. Si no, la siguiente pregunta barata es cuál de los tres hermanos concretos falla, no un
barrido de valores.

## 10. Siguiente paso

Este documento es el gate previo a tocar código (puntos 1-9 del proceso). Antes de implementar:
`game-design-review` sobre las tres preguntas del §8 (resuelto, §8/§8b), y `architecture-review` porque
el cambio cruza estado de partido (`MatchEngine`) y posicionamiento (reubicación forzada de jugadores)
aunque no toque `Utility.cs`.

## 11. Veredicto `game-design-review` (protocolo de diez preguntas)

1. **Qué experimenta el jugador**: un cordón visible alrededor del balón en toda reanudación —igual que ya
   ve hoy en el saque de falta—, no un rival pegado sin poder actuar por una razón invisible.
2. **Qué decisión toma el jugador**: ninguna directa (es regla de núcleo, no contenido elegible), pero sí
   indirecta en construcción de equipo: un build de presión alta pierde la vía de "robar en el segundo 0"
   en TODAS las reanudaciones, no solo en la falta.
3. **Qué decisión debería tomar**: la misma — coincide con el estado ya aceptado desde ADR 0090 para la
   falta; esto solo lo iguala en las otras cuatro.
4. **Qué regla representa**: RF-052 de forma directa para el saque de centro. Para banda/córner/puerta
   **no existe un RF que pida esta barrera explícitamente** — se extiende por analogía con el precedente ya
   aceptado de ADR 0090 (que sí modificó formalmente RF-053 para la falta). Queda dicho aquí sin
   disfrazarlo de requisito preexistente; candidato a su propio ADR al cerrar (§10).
5. **Qué sistemas intervienen**: solo `/Sim` (`MatchEngine.cs`, `Catalog.cs`, `DataLoader.cs`) y
   `/data/sim/tuning.json`. Cero reparto de lógica con `/Game` (RT-014 intacto): el reposicionamiento se ve
   porque ya viaja como posición de jugador en el evento de cada tick, igual que el saque de falta hoy.
6. **Alternativas**: la rechazada (inmunidad temporal), la propuesta (barrera geométrica, radio único) y
   la descartada en §8b (repulsión continua de movimiento).
7. **Trade-off**: menos robos inmediatos en cualquier reanudación a cambio de más posesión sostenida para
   quien saca — coste de oportunidad legible para builds de presión/marcaje agresivo. Vigilar con las
   mismas puertas que detectaron el problema del parche rechazado (`TheThreeDoctrinesBuyDifferently`,
   `BadBuildsLoseToTheirBaseline`): la lección de `ChaseBall pen=50` (CLAUDE.md) es que varias métricas de
   diferenciación de builds moviéndose juntas no es ruido, y ya se movieron juntas una vez con el enfoque
   equivocado.
8. **Cómo cambia las estrategias**: empuja a los builds de presión/marcaje agresivo (rasgo `Aggressive`,
   tag `Brute`) a expresar su ventaja en el resto de la jugada en vez de en el primer contacto tras una
   reanudación — no anula la estrategia, la reubica.
9. **Puede degenerar**: un único vector identificado (retención indefinida del balón por un perk futuro),
   sin caso hoy — documentado como vigilancia (§8, punto 3), no como excepción fabricada de antemano.
10. **Cómo se demuestra**: §9 (medición barata, una sola pregunta) + §11 del proceso completo (métricas
    explícitas por los cuatro hermanos) + las puertas de balance ya nombradas. Suficiente para un cambio
    que toca `/Sim`.

**Veredicto**: diseño coherente, sin regla inventada disfrazada de requisito, con un solo punto declarado
como extensión por analogía (banda/córner/puerta bajo RF-053, no RF-052) y un riesgo de degeneración
documentado en vez de prevenido en falso. Procede a `architecture-review`.

## 12. Veredicto `architecture-review`

**¿Ya existe el patrón?** Sí, y es la base entera de este diseño: `EnforceFreeKickClearance`
(`MatchEngine.cs:2692`, ADR 0090). No se propone una abstracción nueva, se **generaliza** la que ya está en
producción. Cumple el principio de la propia skill antes de cualquier otra pregunta.

**Orden del tick (`Step`, `MatchEngine.cs:520-590`)**, verificado línea a línea:

1. `UpdateContextCaches` (limpieza de `_restartClearanceOwner`) corre **antes** que nadie decida —
   idéntico orden que ya validó el revisor para `_kickoffPendingTaker` ("VERIFIED: el orden de ticks no
   deja hueco").
2. El bucle de `UpdatePlayer` (donde corren `EvaluateTackle`/`EvaluateBlock`) va **antes** que
   `EnforceRestartClearance`, en el mismo tick. Consecuencia: el clamp de un tick corrige la posición que
   **el siguiente** tick usará para decidir, nunca la de sí mismo — el mismo desfase de un tick que ya
   tiene `EnforceFreeKickClearance` hoy y que ya está medido y en verde. No es un desfase nuevo introducido
   por esta generalización.
3. El tick de transición (última cuenta atrás → primer tick de juego abierto) queda cubierto: el clamp
   todavía corre ese tick con `wasRestarting == true`; el primer tick con `_restartClearanceOwner` activo y
   `wasRestarting == false` hereda posiciones ya corregidas por el tick anterior. Sin hueco de un tick sin
   protección en la transición.

**Determinismo (RT-020/021/023)**: sin RNG, sin `Dictionary`/`HashSet` sin ordenar, aritmética de `Vec2` ya
usada en el mecanismo existente. El bucle de `EnforceRestartClearance` corrige cada rival de forma
independiente (no hay contención entre jugadores como sí la hay en `Marking.Assign`), así que iterar en
orden de índice de array en vez de `PlayerInTurnOrder` no introduce asimetría de resultado — mismo patrón
que ya pasa RT-024 hoy.

**Esquema de datos**: renombrar `freeKickClearanceCells` → `restartClearanceCells` en
`tuning.schema.json`/`tuning.json` **no** es un cambio de esquema versionado. RT-030 ("cualquier cambio de
esquema sube la versión") rige el estado de la run persistido (`docs/modelo-datos.md`), no la configuración
estática de `/data` que se valida en cada arranque (RT-032/083) y no se guarda en ninguna partida. Un
`grep` de uso + `DataValidator` basta; no hace falta versión nueva.

**RT-011/RT-014**: sin cambios. El reposicionamiento viaja como posición de jugador en los eventos ya
existentes (igual que el saque de falta hoy); no hace falta ningún evento nuevo ni ninguna decisión nueva
en `/Game`.

**Efecto de segundo orden identificado, no bloqueante**: durante la ventana nueva (`_restartClearanceOwner`
activo tras `TakeRestart`), un rival con `ChaseBall` activo intentará cerrar sobre el balón y el clamp lo
devolverá cada tick — la misma interacción que ya existe hoy durante la cuenta atrás del saque de falta
(ADR 0090 no reportó problema de "vibración" visible ni una métrica afectada por ello). Se hereda, no se
introduce: **vigilar en la captura visual (punto 11 del proceso, `visual-review` si hace falta), no
bloquea la implementación**.

**Veredicto**: sin problema de arquitectura. Procede a implementar según §7, con las tres decisiones del
§8 ya resueltas y el punto de vigilancia de `ChaseBall` anotado (no bloqueante).

## 13. Resultados de la medición (punto 11 del proceso) — implementado, no cerrado

Implementado exactamente según §7 (`EnforceRestartClearance`, `_restartClearanceOwner`,
`restartClearanceCells` = 2,0 para las cinco), más una corrección encontrada al medir (§13a) y dos gaps
reales encontrados al medir (§13b) que **no se corrigen en este commit** — son la decisión del punto 14.

### 13a. Bug heredado de ADR 0090, corregido aquí

`Utility.ClampToArea` acota al **área de portero** (RF-057b): en todos los demás sitios del motor
(`Move`, `BodySeparation.Resolve`, `Utility.cs:522`) solo se llama si `!player.IsOutfield`.
`EnforceFreeKickClearance` la llamaba **sin esa condición**, para cualquier rival — un jugador de campo
clamped durante un saque de falta se teletransportaba al rectángulo diminuto del área propia. Nunca se
había notado porque ninguna prueba comprobaba el desplazamiento de un rival (solo el del sacador). Al
generalizar a saque de centro, con el balón en el centro del campo, el teletransporte se hizo evidente
(el test `TheRestartTakerStandsStillDuringTheDeadBall` lo cazó: jugador 4 desplazado 4,11 casillas en el
tick de resolución). Corregido: `ClampToPitch` primero, `ClampToArea` solo si `!player.IsOutfield`, mismo
patrón que el resto del motor. Este bug **ya estaba en producción** desde ADR 0090; no es nuevo de BB-B,
solo se descubrió aquí.

### 13b. Dos gaps geométricos reales, medidos, sin corregir

Con la corrección de 13a aplicada, `Sim.Tests/Engine/RestartClearanceTests.cs` (60 semillas) mide:

| Reanudación | Disputas / total (antes → después) | Causa |
|---|---|---|
| Centro | 8/187 → **0/190** | — cerrado |
| Banda | 5/79 → **3/61** | geometría de borde |
| Puerta | 7/84 → **5/92** | geometría de borde |
| Falta | no medido antes (0 protección post-saque) → **13/190** | compañero marcado sin balón (9/13) + geometría de borde (4/13) |

**Geometría de borde** (banda, puerta, y 4/13 de falta): el punto de saque está sobre una línea del campo
por construcción (banda: Y=0/Rows; puerta: X=0/Columns). Parte del círculo de exclusión de 2,0 casillas
cae fuera del campo; `Utility.ClampToPitch` recorta la corrección a esa línea, dejando al rival más cerca
del balón que el radio pretendido. No es el desfase de un tick esperado del mecanismo (eso se mide aparte
y da 1/190 en falta, dentro de tolerancia) — es que el radio completo no cabe junto a un borde.

**Compañero marcado sin balón** (9/13 de falta, la mayoría del residual de falta): la barrera protege un
círculo alrededor del **balón**, no alrededor de cada jugador del equipo que saca. El saque de centro no
sufre esto porque `ResetPositions` reforma a los catorce jugadores antes del saque, alejando a todos los
rivales de todos; la falta no reforma a nadie, así que un marcaje de juego abierto puede seguir pegado a
un compañero del sacador que está lejos del balón, y `Block`/el marcaje sin balón conecta ahí sin que la
barrera lo vea. **Es exactamente la pregunta que el §8 punto 2 dejó abierta** ("¿lo cubre ya
`IsInActivePlay`?") — medido: no lo cubre, y no es un caso raro.

### Puertas (43, semilla 1, una sola invocación)

| | Base (sin BB-B) | Geométrica (esta) |
|---|---|---|
| Rojas | 5 de 43 | 5 de 43 |
| `TheGoldOfAnActPaysTwoOrThreeSinksAndNeverAllOfThem` | ROJA | **verde** |
| `CoherentBuildsBeatTheirBaseline` (`orc_giants` 57,92, mín 58) | ROJA | **verde** |
| `EquippingAGoodBuildIsWorthSeveralPointsOfWinRate` | ROJA | **verde** |
| `BuildsWinDifferently` (`passChain`) | ROJA (1,08, mín 1,11) | ROJA (**1,11**, exacto en el borde) |
| `NoGateMetricIsOutOfRange` | ROJA (2 métricas) | ROJA (2 métricas, **distintas**) |
| `BadBuildsLoseToTheirBaseline` (`elf_out_of_zone` 48,75, rango 10-45) | verde | **ROJA (nueva)** |
| `BetterTeamWinRateIsInRange` (`human_60_vs_human_40` 69,88, rango 70-90) | verde | **ROJA (nueva)** |

**Misma lección de `ChaseBall pen=50` (CLAUDE.md), otra vez**: el recuento total (5→5) no dice nada por sí
solo. Tres puertas que estaban rotas **antes de tocar BB-B** (sin relación aparente con reanudaciones)
ahora pasan; dos puertas que estaban verdes ahora están rojas, ninguna cerca de su límite salvo
`passChain`, que mejora pero no cruza. `badBuildsLoseToNone_elf_out_of_zone` sugiere que ese build "malo"
lo era en parte por quedar mal parado en las reanudaciones —hipótesis, no confirmada—; `betterTeamWinRate`
sugiere que menos robos inmediatos en cualquier reanudación nivela ligeramente a un equipo mejor contra
uno peor. Ninguna de las dos se investiga más aquí: es la composición nueva la que hay que llevar al
revisor, no una afirmación de "sin regresión".

### Decisión pendiente (punto 14 del proceso) — no se cierra sin ella

La solución geométrica funciona **por completo** para el saque de centro (el caso que abrió BB-B) y
**reduce sustancialmente** los otros tres sin cerrarlos. Cerrar banda/puerta/falta del todo exige elegir
entre:

1. **Aceptar el residual medido** (banda 3/61, puerta 5/92, falta 13/190 — todas mejoras reales sobre el
   estado sin protección) y las dos puertas nuevas rojas, documentando ambas como límite conocido.
2. **Extender la barrera al borde del campo**: en vez de recortar la corrección con `ClampToPitch`,
   deslizarla a lo largo de la línea (mantener la distancia al balón moviéndose en paralelo al borde en
   vez de perpendicular a él) — cierra banda/puerta, no toca el gap de compañero marcado de falta.
3. **Extender la barrera a los compañeros del sacador**, no solo al balón, para la falta — un cambio de
   alcance real (de "nadie cerca del balón" a "nadie cerca de nadie del equipo que saca"), con su propio
   coste de segundo orden que no se ha medido.
4. **Revertir esta implementación** y devolver BB-B a "detenido" si el revisor considera que las dos
   puertas nuevas rojas son peores que el problema original.

No eliminado ni descartado: el código de esta sesión queda en el árbol, sin commitear, a la espera de la
decisión del punto 14 y de la revisión completa del punto 13 (`independent-reviewer` con el problema
entero, el parche descartado, esta hipótesis, el diff y las métricas de arriba — no solo el diff).

## 15. Veredicto del `independent-reviewer` — el diagnóstico del §13 estaba mal, se revierte (B)

El revisor midió por su cuenta (compiló los tres árboles: base, solo el bugfix de §13a, y el commit
completo) y encontró tres cosas que cambian la decisión:

1. **El commit mezclaba dos cambios sin separarlos**: el bugfix de §13a (independiente, real, hereda de
   ADR 0090) y la generalización de la barrera (B). Separados: solo (A) da **3 puertas rojas de 43**, mejor
   que las 5 de la base y las 5 del commit mixto. Ninguna de las opciones del §14 contemplaba esta quinta
   salida porque ninguna medición previa las había separado.
2. **La causa del residual del §13 era falsa.** Midió las 21 disputas residuales de banda/puerta/falta:
   **ninguna** ocurre a menos de 2,08 casillas del balón (mínimo medido, media 2,96) — la barrera **nunca
   se rompe por geometría de borde**, esa hipótesis está **REJECTED por medición directa**. Las 21 son
   contacto (`block`/`offBallMissed`) contra un **compañero del sacador** lejos del balón, la misma causa
   en las tres reanudaciones, no dos causas distintas como decía este documento.
3. **La generalización tocaba el penalti sin declararlo.** La guarda de `Step` (`wasRestarting || ...`) es
   cierta también para `RestartKind.Penalty`, así que la barrera actuaba 45 ticks (3 s) sobre toda la
   defensa y el portero en cada penalti — RF-054, que ADR 0090 dejaba expresamente intocado. Medido: 223 →
   7 frames con un defensor a menos de 1,95 casillas del balón durante esa cuenta atrás. Nadie lo había
   medido ni decidido.
4. La única regresión atribuible de verdad a (B) es `betterTeamWinRate_human_60_vs_human_40` (69,88,
   mínimo 70) — **métrica obligatoria de RT-056**, verde en base y en solo-(A), roja solo con la barrera
   generalizada puesta. `BadBuildsLoseToTheirBaseline` la rompe (A), no (B) — de hecho (B) la mejora (2
   métricas fuera → 1).

**Decisión aplicada**: se revierte (B) por completo (`_restartClearanceOwner`, la llamada generalizada, el
uso de `_ball.Position`, el renombrado de dato) y se conserva (A) —el fix de `ClampToArea`/`IsOutfield`,
único bug real e independiente del alcance— sobre el mecanismo original de ADR 0090 (solo saque de falta).
Confirmado por mí, no solo por el revisor: build y `Category!=Gate` (755/755) en verde, las 43 puertas dan
**3 rojas** (`BuildsWinDifferently`/`passChain`=1,09, `BadBuildsLoseToTheirBaseline` con dos builds fuera
de rango, `NoGateMetricIsOutOfRange` agregando las dos) — exactamente el número que midió el revisor para
el árbol solo-(A), y `betterTeamWinRate` vuelve a verde.

**Lo que queda abierto, sin decidir todavía:**

- **BB-B en sí sigue sin resolver.** El saque de centro sigue teniendo el hueco original (el parche
  temporal descartado, luego revertido); la barrera geométrica generalizada que lo cerraba también se
  revirtió. Vuelve a `docs/pendientes/BB-B.md` como abierto, con el diagnóstico correcto esta vez: el
  residual no es geometría de borde, es contacto contra un compañero del sacador lejos del balón — una
  solución futura tiene que proteger eso, no el borde del campo.
- **`BadBuildsLoseToTheirBaseline` con (A) puesto**: dos builds (`orc_misplaced`, `elf_brawler`) ganan más
  de lo que deberían contra el baseline sin perks. Es consecuencia de corregir un bug real de ADR 0090
  (los jugadores ya no se teletransportan al área ajena durante un saque de falta), no una regresión que
  se pueda revertir sin reintroducir el bug. Decisión pendiente, propia, sin resolver aquí: ¿se acepta el
  rango nuevo con una ADR que lo mida con más partidos, o hace falta retocar los dos builds "malos" para
  que vuelvan a perder con claridad?
- **El saque de córner no ocurre nunca** en las muestras medidas (0 en 60 partidos, en los dos árboles).
  Hermano nuevo, sin ficha propia todavía — candidato a `docs/pendientes/` aparte, no de este paquete.
- El caso del portero con `ClampToArea` devolviéndolo dentro del radio de exclusión (medido en penalti, no
  reproducido para el saque de falta en solitario) queda anotado en el código como límite conocido, sin
  corregir.

No hay lote de `/Balance` todavía para (A) en solitario (RT-054, skill `balance-measure`): las 43 puertas y
`Category!=Gate` bastan para verificar que no rompe nada existente, pero el rango nuevo de
`BadBuildsLoseToTheirBaseline` pide su propio lote antes de decidir su ADR.

## 16. Tercer intento — corrige las tres causas exactas que el revisor encontró

Decisión del revisor (16 sep 2026, "tercer intento de BB-B ya"): reimplementar la barrera geométrica de
las cinco reanudaciones, corrigiendo específicamente los tres fallos del §15, no repitiendo el diseño a
ciegas.

**Corrección 1 — la guarda de `Step` excluye el penalti por construcción.** Nueva función
`IsClearanceRestart(RestartKind)`, expuesta `internal` para probarla directamente: devuelve `true` para
`ThrowIn`/`GoalKick`/`Corner`/`Kickoff`/`FreeKick` y `false` para `Penalty`/`None`. La guarda de `Step` pasa
de `wasRestarting` a sola a `(wasRestarting && IsClearanceRestart(_pendingRestart)) || _restartClearanceOwner is not null`.

**Corrección 2 — techo de duración.** `_restartClearanceOwnerTicks` cuenta los ticks que el sacador lleva
con el balón tras tomar la reanudación; al superar `RestartClearanceMaxTicks` (30, ~2 s por RF-052) la
barrera se libera igual que si hubiera pasado el balón. Sin esto, un saque de falta real de la muestra
retenía el balón 37 ticks con la barrera activa todo ese tiempo.

**Corrección 3 — la medición mide el problema literal, no "hubo un Tackle en el partido".**
`Sim.Tests/Engine/RestartClearanceTests.cs`, reescrito: `NobodyTacklesTheRestartTakerWhileTheyStillHaveTheBall`
filtra por `Opponent == sacador` y `Detail` de disputa del balón (`won`/`missed`/`foul`, no `offBall*` ni
`block*`); `NoRivalIsEverCloserThanTheClearanceDuringTheWindow` muestrea cada fotograma de la ventana (no
solo el de resolución) pero limita la exigencia a los primeros `RestartClearanceMaxTicks` ticks, porque
pasado el techo la barrera se libera **a propósito** (corrección 2) y un rival acercándose ahí es el
comportamiento correcto, no una brecha.

### Resultado medido (60 semillas)

- **Cero disputas contra el sacador** en las cinco reanudaciones (incluido corner, aunque con 0 casos en
  la muestra — BB-N). Cero fotogramas con un rival dentro del radio de exclusión **dentro del techo de
  duración** en las cinco.
- **El penalti queda intacto**: `IsClearanceRestart(Penalty)` es `false` por construcción, verificado con
  una prueba directa, no con un partido completo.
- **43 puertas**: `betterTeamWinRate_human_60_vs_human_40` **vuelve a verde** (la regresión de la métrica
  obligatoria de RT-056 la causaba la fuga al penalti, no la barrera en sí — confirmado al excluirlo).
  `BuildsWinDifferently`/`passChain` también verde (mejora sobre 1,08→1,09→1,11→verde de los tres intentos
  anteriores). Quedan **3 rojas**: `BadBuildsLoseToTheirBaseline` (`elf_out_of_zone`=48,12, mismo build que
  en el segundo intento, no los dos del bugfix en solitario), `NoGateMetricIsOutOfRange` (agrega la
  anterior), y **nueva**: `TheThreeDoctrinesBuyDifferently` (`contextual`=1,16 vs `saver`=1,19 compras por
  mercado — la contextual debería comprar más que la ahorradora y ahora casi empata, invertido por 0,03).
  Mismo recuento que el árbol solo-bugfix (3), composición otra vez distinta.

`TheThreeDoctrinesBuyDifferently` es la misma puerta que rompió el **primer** intento (la inmunidad
temporal rechazada) — señal que no se investiga más aquí, se lleva íntegra al revisor: podría ser que
cualquier cambio en la ventana de disputa de una reanudación mueva la composición de compras
(diferenciación de doctrinas) de forma sistemática, no solo un artefacto de este diseño concreto.

### Sin decidir todavía

- `BadBuildsLoseToTheirBaseline` sigue sin ADR (consecuencia del bugfix de ADR 0090, no de la barrera —
  confirmado: aparece con (A) en solitario, con distinto build cada vez).
- `TheThreeDoctrinesBuyDifferently`, nueva, con margen mínimo (0,03) pero en la dirección equivocada.
- Ningún lote de `/Balance` (RT-054) todavía para (A)+(B) juntos.
- Pendiente: revisión completa del `independent-reviewer` sobre este tercer intento, con el historial
  íntegro de los dos anteriores — no solo el diff de este.
