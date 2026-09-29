# BS-A — Créditos de rival y Gaceta nombran al jugador de datos, no a quien ocupaba el puesto

**Estado: CERRADA para la esquela (29 sep 2026); el villano por créditos queda abierto (ver abajo).** Encontrada por la revisión independiente
de la [ADR 0165](../decisiones/0165-clanes-cerrados-y-nemesis.md) (clanes cerrados y némesis).

## Observación (de lectura de código, no reproducida en partida)

Con la ADR 0165 un puesto de un clan rival puede estar ocupado por alguien distinto del jugador de datos: un
**fichaje** (el titular murió) o un **némesis traspasado** de otro clan. Tres consumidores siguen leyendo la
identidad del rival por `equipo de acto + puesto` y el nombre del fichero de datos:

- la clave de créditos `rivalCredit:<teamId>:<puesto>:…` (`MatchResolution.ApplyRivalCredits`, ADR 0124);
- el villano de la Gaceta por créditos (`GazetteView`, `team.Players[idx].Name`);
- el epitafio «lo mató X del clan Y» (`GazetteView`, misma lectura).

Escenario: matas al orco del puesto 4; entra el fichaje «Grak»; Grak mata a uno de los tuyos. El epitafio dice
que lo mató el orco de datos, que ya estaba muerto. El villano de la Gaceta **prefiere al némesis** (que sí
lleva su nombre, `NemesisView.Villain`), así que el caso visible es sobre todo el epitafio.

## Hipótesis de arreglo

| # | Opción | Coste |
|---|---|---|
| A1 | Guardar el nombre del ocupante en el propio crédito (cambia el formato de clave de la ADR 0124) | Enmienda de ADR, migración de contadores |
| A2 | Clave por `clanId` en vez de `teamId` (el puesto es la misma persona entre actos, ADR 0165) y resolver el nombre con `RivalRoster` en el momento del hecho | No resuelve el nombre histórico si el ocupante cambió después |
| A3 | Anotar en `MatchSummary` los nombres rivales del partido y que la Gaceta los lea de la historia | Estado nuevo por partido |

Por decidir con `architecture-review`. Frecuencia esperada baja: exige que mate un fichaje o un némesis
traspasado. Medir antes cuántas muertes propias por run las causa un ocupante distinto del de datos.

## Hermanos

- `RivalHistory.Against` contaba reencuentros por fichero de acto: **arreglado** en la misma revisión
  (`AgainstClan`, el ojeo cuenta el clan en cualquier acto).

## Arreglo (29 sep 2026): opción A1 sin cambiar la clave de créditos

Al resolver el partido, junto a la causa de muerte (`deathCause:<id>`), se anota **quién ocupaba el puesto** del matador
en ese partido: `deathKiller:<id>` = código de `Sim/Run/Systems/Rivals/RivalKiller.cs` (0 = jugador de datos; generación
+ 1 = fichaje; 1.000.000 + id = némesis), leído con la memoria de rivales de **antes** del partido. Contador de clave
libre: no sube el guardado, y los guardados anteriores leen 0 (el jugador de datos, como antes). La esquela reconstruye
el nombre con ese código (`RivalKiller.Name`).

- **CONFIRMED**: `NemesisTests.TheEpitaphNamesTheSigningWhoKilledNotTheDataPlayer` reproduce el escenario (matas al
  titular del puesto 4, el fichaje mata a uno de los tuyos): falla con la esquela anterior y pasa con la nueva.
  `ADataPlayerKillerLeavesNoCodeAndKeepsItsName`: sin cambio de ocupante no se anota nada.

## Lo que queda abierto

- **El villano de la Gaceta por créditos** (cuando no hay némesis) suma créditos por `equipo:puesto` y lo nombra con el
  jugador de datos: mezcla a varios ocupantes del mismo puesto. El villano **prefiere al némesis**, que sí lleva su
  nombre, así que el caso visible es raro. Arreglarlo del todo pide la opción A2/A3 (créditos por ocupante).

## Revisión independiente (29 sep 2026)

Sin fallos bloqueantes: el código sale de la memoria de antes del partido, identifica al ocupante real (también el
némesis de banquillo que sale de titular, que conserva su id), el nombre reconstruido es el que se vio en el partido,
jefe y procedurales no anotan nada, y los guardados anteriores leen 0. Arreglado tras la revisión:

- **Hermano en el Ojeo** (`ScoutScreen`, reencuentro): el crédito más notable de una muerte nombraba al jugador de
  datos; ahora usa el mismo código.
- Test del némesis traspasado que mata en su clan nuevo (`TheEpitaphNamesAHandedOverNemesisWhoKillsInItsNewClan`).

**Objeción de fondo, aceptada como deuda:** esto es un segundo canal de identidad (un contador por muerto) al lado de
los créditos `equipo:puesto`, que siguen mezclando ocupantes en las lesiones y en el villano por créditos. Arregla lo
más visible (quién mató a quién); la representación correcta —créditos por ocupante (A2/A3)— queda abierta aquí.
