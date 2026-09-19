# BC-H — El aviso de alineación incompleta es falso: el once se rellena solo

**Estado:** Abierta · CONFIRMED · decisión de diseño pendiente

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

## Hermanos

[BA-I](./BA-I.md), [BB-F](./BB-F.md).
