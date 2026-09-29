# BC-H — El aviso de alineación incompleta es falso: el once se rellena solo

**Estado:** **CERRADA (29 sep 2026)** · CONFIRMED · las dos mitades resueltas (ADR 0134 y este cierre)

## Observación

«Antes de empezar el partido, si no tienes los 7 jugadores alineados (por lesiones, por ejemplo), avisaría al
jugador y le permitiría entrar en la alineación para solucionarlo.» (Partida del revisor, 19 sep 2026.)

## Estado actual

- Flujo: Mapa → Ojeo (`MapScreen.cs:311-318`) → «Empezar» (`ScoutScreen.cs:250-254, 314-318`) → Partido. El
  Ojeo **ya** tiene «Alinear» hacia Equipo (`ScoutScreen.cs:299-300`) y Equipo vuelve al Ojeo
  (`TeamScreen.cs:205`).
- Tras cada partido `PruneLineup` (`Sim/Run/MatchResolution.cs:217, 362-376`) quita de la alineación guardada
  a los no disponibles; `LineupWarnings` (`RunEngine.cs:265-270`) añade `Shorthanded` y el Ojeo muestra «juegas
  en inferioridad: con 5 en campo…» (`UiText.cs:168`, el 5 está fijo).
- **Pero no se juega con menos:** `RunLineup.Build` → `SelectStarters` (`Sim/Run/RunLineup.cs:213-240`)
  completa por rol y luego por id hasta 7. **CONFIRMED** con un test temporal: 9 disponibles y 6 (o 5)
  guardados → aviso `Shorthanded` y 7 titulares construidos.
- Solo hay inferioridad real con menos de 7 disponibles; por debajo de 5 termina la run (RF-002b). Nada bloquea
  «Empezar».

## Efectos colaterales

- «TU ONCE» del Ojeo (`ScoutScreen.cs:228`) y Equipo (`TeamScreen.cs:625`) muestran los guardados, no a quien
  jugará.
- `LethalRisks` usa la alineación guardada (`RunEngine.cs:~352`): el que entra de relleno no recibe su riesgo de
  muerte → choca con RF-012c y la condición 3 de la ADR 0048. **LIKELY**, sin medir.
- La inferioridad voluntaria de RF-002d no se puede elegir mientras haya banquillo.

## Propuesta (sin implementar)

Un único cálculo de «once efectivo» en `/Sim` (sobre `RunLineup.Build`) que alimente el aviso, «TU ONCE» y los
riesgos; aviso distinto para «hueco rellenado con X» e «inferioridad real», con acceso directo a «Alinear».
Decidir en `game-design-review` si el relleno automático se mantiene (UI-003) o pide confirmación, y si
RF-002d debe poder elegirse.

## Resolución parcial 23 sep 2026 — ADR 0134

La causa quedó establecida y es más grande que este pendiente: **no existe el concepto de «once efectivo»**.
`/Sim` decide quién juega dentro de `RunLineup.Build` y todos los consumidores de fuera vuelven a derivarlo
de `state.Lineup`, que es la intención guardada. La ADR 0134 lo convierte en **una sola computación**
(`RunLineup.Effective`) que alimenta el aviso, `LethalRisks` y «TU ONCE», y separa el aviso de **relleno**
(`FilledFromBench`) del de **inferioridad real** (`Shorthanded`).

Con eso se cierran los dos incumplimientos que este pendiente destapó: el aviso deja de mentir, y **el
jugador de relleno pasa a recibir su indicador de riesgo de muerte** — lo que faltaba era RF-012c, y con él
la condición 3 de la ADR 0048 («se puede reducir el riesgo con la alineación»), que no se puede cumplir
sobre alguien que no sabes que juega. El `LIKELY` de arriba queda **CONFIRMED** y resuelto.

**Queda abierta la mitad de antes del partido.** RF-002d dice que jugar en inferioridad es una *decisión
legítima*, y mientras haya banquillo sigue sin poder elegirse: el relleno la impide. La ADR 0134 da la mitad
de dentro del partido («que se quede el hueco») y deja decidido el mecanismo para la otra —la marca explícita
de contadores de `RunLineup.RiskCounterPrefix`, que no sube la versión del guardado (W-11)—, pero no la
implementa, porque hace falta tocar el editor de colocación de la pantalla de Equipo.

**Dos pasos concretos, en este orden:**

1. **Un escenario de captura con un titular no disponible.** Hoy la secuencia (`--tour`, `ojeo.png`) arranca
   con la plantilla sana, así que no hay hueco, no sale ningún aviso y el `FilledFromBench` nuevo **no se
   regresiona solo**: lo cubren los tests de `/Sim` y nada más. Es barato y es lo que convierte el arreglo en
   permanente, igual que hizo el paquete de memoria de la fase 1.
2. **El hueco deliberado antes del partido**, con el mecanismo ya decidido arriba.

Tres instancias más de la misma causa, fuera del paquete porque mueven balance: [BG-A](./BG-A.md).

## Cierre (29 sep 2026): la captura y el hueco deliberado

Los dos pasos que dejó la ADR 0134, en este orden, con `game-design-review` ya hecha en
`docs/analisis/once-efectivo-diseno.md` (el pase mínimo de hoy: lo único nuevo es *dónde* se decide).

1. **La captura que regresiona el aviso.** `CapturasOjeo.tscn` (`ScoutCaptureRunner`) deja a un centrocampista con lesión
   grave fuera de la alineación guardada, como lo deja un partido, y saca `ojeo-relleno.png`: «Urk Rompehuesos sale de
   oficio: tú no lo alineaste, y el hueco lo ha tapado el equipo», y en «TU ONCE» el séptimo marcado «de oficio».
   Antes la secuencia arrancaba con la plantilla sana y esa rama sólo la cubrían los tests de `/Sim`.
2. **El hueco deliberado (RF-002d), con el mecanismo que ya había decidido la ADR 0134**: un contador de la run, no un
   campo del estado. `SetLineup(Lineup, PlayShort)` fija `RunLineup.ShortCounter` (`fieldShort`, W-11: **sin subir el
   guardado**); `SelectStarters` no completa el once con el banquillo mientras esté puesto (sí hasta el mínimo de 5, para
   que una baja posterior no rompa el partido); `MatchResolution` lo borra al terminar el partido, junto a la marca de
   RF-093. **Se decide partido a partido y nunca se hereda.** El aviso distingue `ShortByChoice` (elegida, se deshace con
   una pulsación) de `Shorthanded` (real: ni con el banquillo entero se llega a siete). `/Game`: junto a «Confirmar y
   jugar», «Dejar el hueco vacío» / «Que el banquillo tape el hueco» (`ojeo-hueco.png`: seis en TU ONCE); mover una ficha en
   Equipo conserva la decisión. La captura pulsa los botones de verdad (la señal del clic) y comprueba el estado.

**Lo que no se ha tocado y sigue como estaba**: la política de `/Balance` no pide `PlayShort` nunca, así que un
`SetLineup` corriente no escribe el contador (`ASetLineupWithoutTheDecisionNeverWritesTheCounterAndClearsIt`) y los
estados salen byte a byte iguales. **Lo que no se puede medir hoy** es si jugar con seis es rentable: el instrumento
(`RunPolicy`) no ejerce la decisión, mismo hueco que [CAT-C](./CAT-C.md). La nota de diseño lo anticipó («si la medición
dijera que la política mejora rechazando…, se mide, no se supone»); queda por medir con una doctrina que la use, y por
eso `PlayShort` no está en ninguna puerta.

**Sigue abierto, ya fuera de este pendiente**: «TU ONCE» de Equipo lista la alineación guardada, no el once efectivo
([BG-A](./BG-A.md)); y la pantalla de Equipo no tiene un control propio para la decisión (se toma en el ojeo, que es donde
se advierte, como pide RF-002d).

## Hermanos

[BA-I](./BA-I.md), [BB-F](./BB-F.md).
