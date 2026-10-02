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

### Anti-abuso: `WatchedTick` (enmendado tras la revisión independiente)

El guardado lleva el tick más lejano que el jugador **llegó a ver**. Al reanudar, nada de lo que decide el
jugador vale por debajo de él (`PendingMatch.CanDecideAt`: sólo desde el tick siguiente): ni la activación
manual ni el cambio de orden, **ni las sustituciones, los rechazos y «que siga jugando»**. Sin eso podía ver un
gol, salir y decidir mejor al volver.

**La salida limpia no es la única salida.** Un cierre forzado (kill, Steam Deck sin `WM_CLOSE`, caída) no
ejecuta ningún `Save()`, y con un guardado que sólo se escribe al salir deja el suelo a 0 (derrota) o el mapa
de antes del nodo (victoria). Por eso la regla es la conservadora:

1. **`PlayMatch` escribe el guardado del partido a medias antes de enseñarlo**, sea derrota o victoria, con
   `WatchedTick` = último tick del partido (el peor caso: `PendingMatch.BeforeShowing`). Cada decisión
   (`Answer`) lo vuelve a escribir con el mismo suelo pesimista.
2. Un `Save()` **limpio** (pausa, salir al menú, `WM_CLOSE`) lo baja al tick visto de verdad. El suelo nunca
   baja por debajo del de una reanudación anterior: tras un cierre forzado se conserva el peor caso aunque la
   siguiente salida sea limpia.
3. Tras un cierre forzado la repetición sale con **todos los controles bloqueados** y el resultado no cambia.
   Los puntos de sustitución que el jugador no llegó a responder y caen por debajo del suelo se resuelven con
   la política por defecto **antes de enseñar la reproducción** (`ResolveBlockedPoints`), para que lo que se ve
   y lo que se aplica sean el mismo partido; sin esto la ventana quedaría abierta y sin respuesta posible.
4. La regla vive también en `RunController` (`ChangeOrder`, `UseConsumable`, `Substitute`, `Decline`,
   `PlayOn`), no sólo en la pantalla: una pantalla nueva no puede saltársela.

**Cómo se le explica al jugador** (`game-design-review`, el bloqueo es visible): un texto corto bajo el marcador
mientras la reproducción no pasa de lo ya visto —«Partido retomado: ya lo habías visto hasta el minuto N. No se
puede decidir hasta entonces.» o, si lo vio entero, «ya lo habías visto entero; sólo verlo»—. Lo genera el
minuto del suelo (`MatchLogView.Minute`), desaparece solo al llegar a lo no visto y no esconde ningún daño:
nada nuevo ocurre en el partido, sólo se explica un control que no responde. El texto está en `UiText` (la
interfaz del juego sólo tiene `Es`; `data/l10n` es del catálogo de `/data`).

## Un partido abierto no puede quedarse desfasado

`_matchOpen` se cierra (`CommitMatch`) al llegar al informe, y también —por si se salió sin pasar por él, caso
de la vista de depuración (`Nav.MatchDebug`) o de un arnés de capturas— cuando se navega a cualquier pantalla
que no sea del partido (`Nav.Go`) y cuando entra o decide algo ajeno al partido (`Enter`, `Apply`, `JumpTo*`,
`SeedForCapture`, `PlayMatch`). Sin esto cada `Save()` posterior reescribiría en silencio el estado viejo.

## Consecuencias que se documentan

- **Victoria**: `CommitMatch` guarda ahora el estado de después con la fase `NodeOpen` (recompensa pendiente),
  que antes sólo se escribía al cerrar. Es el mismo estado que habría escrito un cierre en ese punto.
- **Un guardado de la versión 8 hecho a mitad de partido** (el comportamiento antiguo) se comporta como antes: es
  un estado de después del partido, no trae `pendingMatch`, y al volver el partido está jugado y no se ve. La
  migración sube la versión a 9 al cargar, y al reescribirlo sale una 9 completa.

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
3. Nada de lo que decide el jugador vale antes de `WatchedTick`; el guardado de antes de enseñar el partido lleva el peor caso.
4. Un guardado de la versión 8 carga sin partido pendiente.

## Lo que queda fuera

- Guardar el partido a mitad de **jugada** o la posición del campo: se reproduce desde el tick 0 (RT-051:
  60-90 s a 15 ticks/s, coste despreciable). No se guarda un punto de reanudación en el motor.
- El resultado de un partido sin ver (autosimulado por `/Balance` o por políticas): no pasa por aquí.
