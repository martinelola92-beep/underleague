# BR-A — Guardar o salir a mitad de partido no reproduce el partido al volver

**Estado: cerrada (2 oct 2026, [ADR 0183](../decisiones/0183-salir-a-mitad-de-partido-reproduce-el-partido.md)). Texto original de la ficha a continuación.** Anotada al añadir el menú de pausa (29 sep 2026), que no la crea pero la hace
visible: ahora hay un botón «Guardar» y otro «Salir al menú principal» dentro de la retransmisión.

## Qué dice el requisito

RT-061: *«Salir a mitad de partido reproduce el partido desde la semilla al volver.»*

## Qué hace el código

`RunController.PlayMatch` resuelve el partido entero **antes** de enseñarlo (`Enter` → `RunEngine.EnterMatch`),
así que mientras corre la retransmisión `RunController.State` ya es el estado de **después** del partido:

- **Derrota o empate**: la fase vuelve a `OnMap` y `AfterTransition` ya ha escrito el guardado con el
  resultado. Cerrar o salir a mitad no cambia nada: al volver, el partido está jugado y **no se ve**.
- **Victoria**: la fase queda en `NodeOpen` (recompensa pendiente) y no se guarda; `Save()` desde el menú
  o desde el cierre de ventana (`_Notification`) escribe ese estado. Al volver, `Nav.For` lleva a la
  recompensa y el resto del partido y el informe **no se ven**.

En ningún caso se reproduce el partido. [LIKELY: leído en el código, no reproducido con una run.]

## Por qué no se arregla en el paquete del menú

Guardar `_stateBeforeMatch` en su lugar no basta: las decisiones tomadas dentro del partido
(sustituciones, «que siga jugando», órdenes, ADR 0094/0134/0154) viven en `RunController.Decisions` y no
están en el esquema del guardado, así que se perderían y el partido reproducido sería otro. Y en la
derrota el guardado posterior ya está en disco antes de que la retransmisión empiece. Es una decisión de
esquema de guardado (sube versión) y del orden «resolver → enseñar», no de interfaz.

## Opciones, sin decidir

1. Guardar el estado previo **más** las decisiones del partido (esquema nuevo) y no escribir el guardado
   posterior hasta que la retransmisión termine.
2. Enmendar RT-061: salir a mitad de partido lo da por visto (lo que hace hoy).

## Cierre

Opción 1 de las dos, que es además lo que RT-061 ya decía: no enmienda ningún requisito. Guardado de **antes**
del partido más `PendingMatch` (nodo, decisiones, tick más lejano visto), esquema 9 (la 8 sigue cargando); el
guardado de después se escribe al llegar al informe. Anti-abuso: el resultado no puede cambiar (mismo estado,
semilla y decisiones) y los controles en vivo se bloquean antes de lo ya visto. CONFIRMED por
`Sim.Tests/Run/PendingMatchTests.cs`.
