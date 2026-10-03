# 0191. La decisión en vivo se simula una vez y en segundo plano

**Fecha:** 2026-10-04
**Estado:** Aceptada (autónoma: no cambia ninguna regla ni ningún partido; arreglo de rendimiento).
**Cierra:** BX-19 (`docs/pendientes/BX-playtest-3oct.md`).
**Requisitos:** RT-013, RT-014, RT-021, RT-024, RT-051, RT-061
**Relacionada:** ADR 0094 (decisiones como estado inicial), 0154 (orden táctica en vivo), 0183 (reproducir y reanudar), `docs/arquitectura.md` «Consumibles manuales durante el partido»

## Problema

BX-19, del revisor: *«Cuando cambio de tipo de juego (defensa, neutro, ofensivo) hay un parón, porque entiendo que se
vuelve a simular el juego.»* Lo mismo con un consumible, una sustitución, «que se quede el hueco» o «que siga jugando»:
las cinco pasan por `RunController.Answer`.

## Medición (`gameplay-debug`), antes de tocar nada

Hipótesis, todas plausibles leyendo `Answer`: (H1) la reproducción con traza simula el partido desde 0, y otra vez por
cada punto de sustitución (`ResolveAutomatically`); (H2) `EnterMatch` simula **el mismo partido otra vez** sin traza;
(H3) el guardado (JSON de 763 KB + escritura atómica); (H4) la pantalla rehace director, momentos y campo 3D
(`BindPlayback`, `Pitch3D.Bind`); (H5) Godot ejecuta el `Debug`, con `/Sim` sin optimizar; (H6) la traza (memoria).

Instrumentos: `Sim.Tests/Performance/ReplanCostProbeTests` (Release, mínimo de 5) y el modo `-- paron` del arnés
(`BroadcastCapture.MeasureReplanStall`: retransmisión real, 3 semillas × 6 decisiones, cronómetro por partes).

| | Antes | Qué dice |
|---|---|---|
| Godot `Debug`, por decisión (18) | **0,5-3,9 s** bloqueando el hilo principal: reproducción y `EnterMatch` a partes iguales; guardado 4-28 ms; pantalla 2-21 ms | H1+H2 son el parón (**CONFIRMED**); H3 y H4 **REJECTED** como causa (≤ 3 %) |
| Mismo arnés con `Optimize=true` en `Sim.csproj` (sólo ese cambio) | 34-700 ms | H5 **CONFIRMED**: ×5-10 |
| Release, `/Sim` solo (4 semillas) | un `Simulator.Run` 8-17 ms; reproducción 13-61 ms; `EnterMatch` 18-57 ms | El coste es (puntos de sustitución + 1) partidos, dos veces |

El equipo estaba con un lote de `/Balance` de otro agente en marcha: las cifras de Godot tienen ruido de contención
(por eso la Release se da en mínimos); las proporciones no cambian entre corridas.

## Opciones (`architecture-review`)

| | Qué | Veredicto |
|---|---|---|
| a) Snapshot/checkpoint del motor y re-simular desde el tick de la decisión | Clonar todo el estado de `MatchEngine` (RNG, perks, árbitro, turba, colas...) | **Rechazada.** Primitiva nueva y profunda en `/Sim`, con otro agente cambiándolo; un campo olvidado rompe RT-024 en silencio. Sólo reduce el coste a la mitad de media; no lo quita |
| b) Simular en un hilo de fondo | La pantalla no se congela; el reloj del partido sostiene el fotograma de la decisión | **Elegida**, con un catálogo propio por hilo (el patrón del arnés, CLAUDE.md «El paralelismo vive en el arnés») |
| c) Por trozos / incremental | Necesita lo mismo que a) | Rechazada por lo mismo |
| d) Optimizar lo medido | Quitar la simulación duplicada (H2) y compilar `/Sim` optimizado (H5) | **Elegida**, junto a b) |

La traza vieja **no** sirve para seguir reproduciendo mientras se simula: la decisión entra en el tick siguiente al que se
enseña, así que el fotograma siguiente ya puede ser distinto. Lo único válido es el actual; por eso se sostiene.

## La decisión

1. **`/Sim`: una simulación en vez de dos.** `MatchPlaybacks.PlayAndEnter` reproduce con traza y entra en el nodo con
   **la misma** resolución (`RunEngine.EnterResolvedMatch`, **interno**: un llamador externo podría pasar un partido que
   no es el del estado) cuando la reproducción no deja ningún punto de sustitución del jugador pendiente. Por qué es el
   mismo partido: las dos resoluciones sólo difieren en que la reproducción deja sin responder los puntos del jugador;
   si se llega al final sin ninguno, ambas tomaron las mismas respuestas en el mismo orden, y la traza sólo lee el motor.
   Con uno pendiente se resuelve aparte, como antes.
2. **`Sim.csproj`: `Optimize=true` sin condición.** Godot ejecuta el `Debug`; `Sim.Tests`, `/Balance` y CI ya usan la
   Release, que es el `/Sim` de referencia. El juego juega ahora el mismo código optimizado que se mide.
3. **`/Game`: la decisión se simula en un `Task`.** `RunController.Answer` lanza `PlayAndEnter` con un **catálogo
   propio** (`_decisionCatalog`, cargado en segundo plano al abrir el partido con la misma fábrica que `Catalog`: los
   ficheros de la run o su instantánea, RT-061b). Los sistemas de la run se comparten: no tienen estado mutable, como en
   el arnés de `/Balance`. El resultado se aplica **en el hilo principal** (`TryCompleteDecision`, una vez por fotograma
   desde `BroadcastScreen`; `CompleteDecision`, que espera). La pantalla sostiene el fotograma de la decisión y no
   admite otra hasta reanudar con `AfterDecision`, igual que antes.
4. **Nada se pierde ni se encadena mal**: `Save`, `CommitMatch` y cada decisión nueva completan antes la pendiente. Una
   excepción de `/Sim` se relanza en el hilo principal al aplicar. El consumible manual que no llega a dispararse se
   retira sin aplicar nada (antes se volvía a simular el partido de antes; el resultado es el mismo).
5. **ADR 0183**: el guardado del peor caso se escribe al aplicar, decenas de ms después del clic. Un cierre forzado en
   ese intervalo pierde la decisión, nunca la duplica: el suelo guardado ya era el final del partido, así que no abre
   ninguna repetición con ventaja.

## Medición después

| | Antes | Después |
|---|---|---|
| Hilo principal en el clic (Godot `Debug`, 18 decisiones) | 0,5-3,9 s | **≤ 3 ms** |
| Fotograma en que se aplica (pantalla + guardado) | — (dentro del bloqueo) | **6-15 ms** (< 1 fotograma a 60 Hz) |
| Simulación de fondo, Release | 37-105 ms en el hilo principal | 19-55 ms fuera de él (≈ 1-3 fotogramas sostenidos a 60 Hz, el campo se sigue dibujando) |
| Partido del hilo de fondo frente al del hilo principal | — | **igual en 18/18** (huella de eventos y traza, y el `RunSave` del estado de después) |

`PlayAndEnterTests` (RT-024): 2 clubes × 8 semillas × las cinco clases de decisión (órdenes, suelo de lo visto,
consumible manual, y —sobre el primer punto del jugador que aparezca— sustituir, dejar el hueco y «que siga jugando») dan
**byte a byte** el mismo `RunSave`, resumen, eventos, traza y sustituciones que las dos llamadas de antes, por los dos
caminos (101 por el rápido, 4 por el de respaldo en esos primeros partidos; en actos posteriores, con más lesiones, el de
respaldo será más frecuente —LIKELY, sin medir—, y entonces la simulación de fondo cuesta lo de antes, pero fuera del hilo
principal). Forzar el camino rápido con un punto pendiente hace fallar el test (ejecutado dos veces al escribirlo; Regla J).
Bucle de `Sim.Tests` sin puertas: 1.958/1.958. Capturas `paron-tras-orden.png`, `consumible-2-usado.png`.

Las cifras de «antes» en Godot salen de una instrumentación temporal de `RunController.Answer` (cronómetros por parte)
que no se ha dejado en el árbol; el arnés `-- paron` que queda mide el camino nuevo.

**Tras la revisión independiente:** `BroadcastScreen` y la vista de depuración `MatchScreen` completan al entrar la
decisión pendiente (salir con F3 a mitad de una simulación enseñaba el partido viejo); el test cubre las cinco clases de
decisión y dos clubes; el arnés compara también el estado de después. Anotado, sin cambiar: si `/Sim` lanzara una
excepción con la decisión, ahora sale al aplicarla (o en `Save`/`CommitMatch`) y no en el clic; y la bandeja de sustitución
sigue visible los milisegundos que dura la simulación (sin efecto: la respuesta ya se tomó).

Sin lote de `/Balance` (`balance-measure`): no cambia ningún peso, probabilidad ni partido; la igualdad se mide en tests.

## Lo que queda fuera

- **Hasta 1-3 fotogramas con el reloj sostenido** (LIKELY imperceptible: el campo se dibuja, no hay tirón; no medido a
  60 Hz reales porque Xvfb dibuja a ~4 fps). Si se nota, el siguiente paso es la opción a) sólo para
  `ResolveAutomatically`, que rejuega el partido entero por cada punto de sustitución.
- `PlayMatch` (abrir la retransmisión) sigue simulando en el hilo principal: es una transición de pantalla, no un parón
  dentro del partido, y ya se beneficia de 1 y 2.
