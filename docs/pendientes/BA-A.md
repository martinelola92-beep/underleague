# BA-A — BLOQUEO: un nodo de evento sin opciones y sin salida.

**Estado:** CERRADA

## Observación

**BLOQUEO: un nodo de evento sin opciones y sin salida.** «He entrado en un nodo de evento y no veo opciones pero tampoco me deja salir al mapa, por lo que tengo que cerrar y empezar otra run»

## Análisis / estado actual

**CERRADA (13 sep 2026, commit `dafc013`).** La sospecha era falsa: la carta **siempre** llega —`EventSystem.Card` lanza si el acto no tiene cartas y nunca devuelve `null`—. La causa real estaba en `/Game`: `Leaveable()` exigía `_entered || Phase == NodeOpen` y `_entered` se recalcula en cada `Rebuild`, así que si al resolver una carta la fase dejaba de ser `NodeOpen` con el nodo aún seleccionado quedaba `_entered = false`, la vista a `null` —ni descripción ni opciones— y el botón de salir deshabilitado. Ahora de una pantalla de nodo **siempre** se sale (`NodeScreen.Leaveable() => true`); habilitarlo no corrompe nada porque `Leave()` solo emite `LeaveNode` si de verdad hay un nodo abierto. Sin test por RT-084 (es interfaz), pero la mitad testable sí está afirmada: `EventTests.TheCatalogIsLoadedAndEveryCardOffersAWayOut` garantiza que toda carta tiene una opción **sin objetivo y sin coste**, que es la que queda pulsable con la plantilla tocada y sin oro

## Hermanos

_(por enlazar donde se detecten; ver `README.md` del directorio)_
