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

## Anotado tras la revisión independiente (29 sep 2026), sin arreglar

- **[CERRADO 29 sep 2026, ADR 0166]** ~~**Los «gritos» del entrenador son multiplicadores de éxito invisibles.**~~ Ahora `after_him`, `hold_the_line` y `push_forward` llevan el efecto `shout`: cambian la orden o la consigna de presión del equipo durante N segundos y el tablero lo enseña con cuenta atrás. Lo que sigue es el diagnóstico original: `after_him`, `push_forward` y
  `hold_the_line` usan sólo `modifyProbability`/`modifyAttribute` (catálogo BA-H): suben o bajan una cuota
  que el jugador no ve, y **no cambian la conducta que su nombre promete** (nadie «se le echa encima» ni
  «se queda atrás»; el equipo hace lo mismo con otra cuota). Es lo contrario de «comportamiento observable
  > modificadores numéricos invisibles» (CLAUDE.md, Principios). La primitiva que sí cambia conducta es la
  **orden táctica** (ADR 0154, `OrderChange`). **Propuesta, sin decidir:** que un grito sea un cambio
  temporal de orden o de utilidad (p. ej. `hold_the_line` = orden defensiva durante N segundos), con el
  mismo camino determinista que `OrderChange`. Requiere `game-design-review` y `architecture-review`.
- **Vender la reliquia de un compañero es posible y no se ha discutido.** Una reliquia es un objeto más
  (`TransferItem` con `ToPlayerId < 0` en un mercado abierto, o desde el cofre tras equiparla): su valor de
  venta convierte la muerte de un jugador con historia en oro. La ADR 0161 la deja «fuera de mercado y
  recompensas» pero no dice si es vendible. Vigilar `leftoverGoldShare` y decidir si se marca como no
  vendible.

## Hermanos

- **Otros multiplicadores invisibles con nombre de conducta** (revisión independiente de la ADR 0166, 29 sep
  2026, sin evidencia de impacto medida): `master_plan` (táctico, legendario), `smoke_flare` y
  `professional_foul` siguen subiendo o bajando cuotas que el jugador no ve, con nombres que prometen una
  conducta. Mismo diagnóstico que los gritos; candidatos al mismo tratamiento (orden, consigna o acción
  visible) cuando se revise el catálogo de consumibles. Requiere `game-design-review`.
