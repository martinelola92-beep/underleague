# BA-H — Los consumibles no se pueden usar.

**Estado:** CERRADA en su parte principal (29 sep 2026); queda abierta la propuesta de dos slots

## Observación

**Los consumibles no se pueden usar.** Propuesta concreta del revisor: **dos slots**, comprar solo con slot libre, y que salga **ya equipado en el UI del partido** para usarlo con un clic

## Análisis / estado actual

**Abierta.** CAT-B dio el equipado en la pantalla de Equipo, pero el uso en vivo (RF-082, `ManualActivation`) no tiene interfaz

## Cierre (29 sep 2026)

- **Uso en vivo:** cada consumible manual equipado tiene su botón en el tablero de la retransmisión;
  pulsarlo lo activa en ese tick y el partido se vuelve a simular desde el estado previo, con el mismo
  patrón que la orden táctica (ADR 0154): `RunController.UseConsumable`, `MatchDecisions.ManualActivations`.
  Se apaga una vez usado (RF-085) y cuando la orden tampoco se puede cambiar. Test de determinismo en /Sim:
  activar en el tick T sólo cambia el partido desde T (`MatchConsumableManualTests`). Capturas
  `consumible-1-boton.png` y `consumible-2-usado.png` (`CapturasRetransmision.tscn`).
- **Catálogo:** de 4 a 20 consumibles, con los gritos del entrenador como tácticos manuales, todos con
  primitivas que ya existían.
- **Sin hacer:** la propuesta del revisor de **dos slots** y comprar sólo con hueco libre (hoy RF-080 dice
  tres y la pantalla de Equipo deja uno manual). Es un cambio de regla que no se ha tocado.
- Primitiva que falta para el plan de diversión: **provocar la turba** (necesita que la prórroga pueda
  empezar por un consumible; hoy sólo la abre el empate al final).

## Hermanos

_(por enlazar donde se detecten; ver `README.md` del directorio)_
