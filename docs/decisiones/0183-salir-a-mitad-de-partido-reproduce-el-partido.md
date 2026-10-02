# 0183. Salir a mitad de partido reproduce el partido desde el estado de antes

**Fecha:** 2026-10-02
**Estado:** Aceptada (autónoma, sin cambio de regla: cumple RT-061 tal como está escrito). Implementada en `/Sim` (`PendingMatch`, `RunSave` v9) y en `/Game` (`RunController`, `BroadcastScreen`).
**Cierra:** BR-A (`docs/pendientes/BR-A.md`).
**Requisitos:** RT-061, RT-030, RT-013, RT-024, RT-012, RF-082, RF-120
**Relacionada:** ADR 0094 (la decisión dentro del partido es estado inicial), 0134 (el once efectivo), 0154 (orden táctica en vivo), `docs/fase2-diseno.md` W-5 y W-12

## Problema

RT-061: *«Salir a mitad de partido reproduce el partido desde la semilla al volver.»* Hoy no lo hace.
`RunController.PlayMatch` resuelve el partido entero antes de enseñarlo, así que mientras se ve la
retransmisión el estado en memoria ya es el de **después**, y ese es el que se guarda: al volver el partido
está jugado y no se ve (derrota: ya guardado en disco; victoria: recompensa pendiente). Además las decisiones
tomadas dentro del partido viven en `RunController.Decisions` y no están en el guardado.

## Qué ya era posible (medido leyendo el código, CONFIRMED por test)

Reproducir un partido desde el estado de **antes** ya es determinista: la semilla del partido es
`RngStreams.MatchSeed(runSeed, nodeId)` y no depende del camino (W-5), y las decisiones del jugador entran
como estado inicial de `EnterMatch` (ADR 0094). Lo que falta es **guardar** ese estado previo y esas
decisiones. No hace falta ninguna capacidad nueva del motor (sin primitiva nueva: no hay ADR de motor).

## Revisión de diseño (`game-design-review`, pase mínimo)

Qué debe pasar al salir a mitad de partido en un roguelite ironman. Tres opciones:

| Opción | Qué ve el jugador al volver | Veredicto |
|---|---|---|
| **A. Reanudar el mismo partido** (estado previo + decisiones + semilla; se rejuega determinista hasta el mismo final) | El partido otra vez, con sus decisiones ya tomadas | **Elegida.** Es el texto de RT-061; coherente con W-12 (el nodo abierto se reproduce, no se re-sortea) |
| B. Rejugarlo «desde cero» con las decisiones borradas | Mismo partido, pero puede decidir distinto sabiendo qué pasa | Rechazada: ver abuso |
| C. Salir lo da por jugado (lo que hace hoy) | El resultado, sin haberlo visto | Rechazada: enmienda RT-061 y pierde la tensión del partido; con una derrota la run se pierde sin verla |

1. **Fantasía**: cerrar el juego o suspender la Deck no te roba ni te regala nada; el partido es el que era.
2. **Decisión del jugador**: las mismas de siempre (sustitución forzada, que siga jugando, orden, consumible). No hay decisión nueva.
3. **Coste de oportunidad**: ninguno nuevo; salir no es una jugada.
4. **Interacciones**: la apuesta (ADR 0157) se reembolsa en `EnterMatch`, que se vuelve a ejecutar desde el estado previo, así que no se reembolsa dos veces ni se pierde.
5. **Degeneración posible (la que importa)**: *volver a tirar el resultado*. Con la opción A el resultado **no cambia nunca** porque el estado previo, la semilla y las decisiones guardadas son los mismos (test de anti-abuso). Queda una fuga de información: el jugador ve el futuro del partido, sale, y al volver **decide mejor** (activa el consumible o cambia de orden antes del gol que ya vio). Se cierra con `WatchedTick` (más abajo).
6. **Previsibilidad (RF-012d, las cinco condiciones de la ADR 0048)**: no se toca; el partido que se reproduce es el que era.
7. **Legibilidad**: al volver se ve el partido, no un salto al informe. Los controles en vivo se reactivan al llegar al tick que ya se había visto.
8. **Medición**: no cambia ningún peso, probabilidad ni catálogo. No aplica lote de `/Balance` (`balance-measure`): es un arreglo de replay/serialización, que la skill excluye expresamente. Lo que sí se mide es la igualdad byte a byte, en tests.

### Anti-abuso: `WatchedTick`

El guardado lleva el tick más lejano que el jugador **llegó a ver**. Al reanudar, una activación manual o un
cambio de orden sólo valen desde ese tick (antes de él, el jugador ya conocía el futuro). Las sustituciones
forzadas no necesitan guarda: sólo estaban respondidas las de ticks ya vistos, porque la retransmisión se
detiene en cada una hasta que se responde. Es una consecuencia de «sin trampas por recarga» de RT-061, no una
regla nueva; si el revisor prefiere no restringir los controles, basta con no leer `WatchedTick` en
`BroadcastScreen.CanActNow` y el resto sigue valiendo.

## Revisión de arquitectura (`architecture-review`)

- **Frontera**: la serialización y el tipo `PendingMatch` viven en `/Sim` (`Sim/Run/PendingMatch.cs`,
  `Sim/Run/Save/RunSave.cs`), sin E/S (RT-012): convierte a texto y desde texto, el fichero lo escribe
  `/Game`. `/Game` decide *cuándo* se guarda cada cosa. La dependencia sigue siendo `/Game -> /Sim`.
- **Abstracción**: `PendingMatch(NodeId, Decisions, WatchedTick)` **no es parte de `RunState`**. Meterlo en el
  estado habría obligado a limpiarlo en cada transición y habría hecho representable «un estado después del
  partido que dice estar a mitad». Es un acompañante del estado **previo**: el guardado es
  `(estado previo, partido pendiente?)`.
- **Esquema (RT-030)**: `schemaVersion` 8 -> **9**. La 9 sólo **añade** el campo `pendingMatch` (objeto o `null`).
  `RunSave.Load` lee la 9 y la 8 (`MinimumReadableVersion`): la 8 carga como una 9 sin partido pendiente. Es
  una migración **explícita y declarada**, no silenciosa. Las anteriores a la 8 se siguen rechazando con
  mensaje claro, como antes. `data/schemas/run-save.schema.json` declara el campo.
- **Plomería de decisiones**: `MatchDecisions` ya es el contenedor de todo lo decidido (ADR 0094/0134/0154);
  se serializa entero, incluido `PlayOn.After` (atributos ya resueltos por `/Sim.Run`, para no recalcularlos
  al volver: es el mismo valor que entró al motor).
- **Determinismo (RT-021/RT-024)**: sin `Dictionary` iterado, sin reloj. El orden de las listas es el de las
  decisiones. Idempotencia comprobada: `Save(Load(Save(x))) == Save(x)`.

## La regla

1. Mientras un partido se está viendo, **lo que se guarda es el estado de antes del partido** más su
   `PendingMatch` (nodo, decisiones, tick visto). El guardado posterior al partido **no se escribe** hasta que
   el jugador llega al informe (el partido se «cierra» ahí).
2. Al volver, el juego retoma ese guardado, vuelve a entrar en el nodo con las decisiones guardadas y
   reproduce el partido hasta el mismo final.
3. Los controles en vivo (consumible manual, orden) quedan bloqueados antes de `WatchedTick`.
4. Un guardado de la versión 8 carga sin partido pendiente.

## Lo que queda fuera

- Guardar el partido a mitad de **jugada** o la posición del campo: se reproduce desde el tick 0 (RT-051:
  60-90 s a 15 ticks/s, coste despreciable). No se guarda un punto de reanudación en el motor.
- El resultado de un partido sin ver (autosimulado por `/Balance` o por políticas): no pasa por aquí.
