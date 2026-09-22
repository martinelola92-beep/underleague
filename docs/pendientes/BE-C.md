# BE-C — `MatchResolution` trata el partido completo en un sitio y solo hasta la derrota en otro

Estado: **abierta, sin medir**. Dos síntomas del mismo fichero y de la misma causa, encontrados el 22 sep
2026 por el `independent-reviewer` sobre la ADR 0124.

## Síntoma 1 — el historial ignora `defeatTick`

La cabecera de `Sim/Run/MatchResolution.cs` documenta un contrato: el bucle de bajas **para en
`defeatTick`**, porque «los eventos posteriores no se aplican a la plantilla». El paso 3b (historial de
carrera, ADR 0124) aplica las estadísticas del **partido completo** sin mirar `defeatTick`.

Gravedad baja —la run ha terminado— pero el estado guardado deja de ser «el que había en ese instante», que
es justo lo que el fichero promete.

## Síntoma 2 — `PlayedTicks` no filtra por equipo

`MatchResolution.PlayedTicks` busca por `PlayerId` **sin comparar equipo**, justo el descuido que el paso 3b
sí evita catorce líneas más arriba (`ownStats[s].Team == 0`).

Hoy es inocuo porque `RivalTeamBuilder.OpponentFirstPlayerId = 2_000_000` separa los rangos de id. Pero
decide **quién cuenta como “jugó”** para experiencia y para el contador de banquillo, y no tiene test.

## Por qué van juntas

Son el mismo patrón —*el fichero no tiene una noción única de «qué parte del partido cuenta»*— y conviene
buscarle una respuesta común antes que parchear los dos sitios por separado. `CLAUDE.md`: piensa en
sistemas, no en tickets.

## Antes de tocar nada (Regla A)

La medición barata que discrimina: contar en un lote cuántos partidos terminan con `defeatTick` antes del
final y cuánto historial se acumula después de ese tick. Si es despreciable, el síntoma 1 es una corrección
de documentación y no de código.

## Hermanos

- `docs/decisiones/0124-historial-de-carrera-y-atribucion-de-muerte.md` (enmienda) · [BE-B](./BE-B.md)
