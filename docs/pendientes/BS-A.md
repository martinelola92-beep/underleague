# BS-A — Créditos de rival y Gaceta nombran al jugador de datos, no a quien ocupaba el puesto

**Estado: ABIERTA (29 sep 2026), sin evidencia de activación medida.** Encontrada por la revisión independiente
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
