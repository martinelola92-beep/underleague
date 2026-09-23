# BE-C — `MatchResolution` trata el partido completo en un sitio y solo hasta la derrota en otro

Estado: **síntoma 2 CERRADO; síntoma 1 ABIERTO y reabierto tras una conclusión falsa** (23 sep 2026). Dos
síntomas del mismo fichero, encontrados el 22 sep 2026 por el `independent-reviewer` sobre la ADR 0124.

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

## Medido (23 sep 2026): el síntoma 1 no era de código

800 runs con la política realista de `FullRunGateTests`, doctrina contextual: **10 883 partidos**, de los
que **25 runs** terminan por `NotEnoughPlayers` (0,23 % de los partidos). Sobre 56 derrotas muestreadas:

| | |
|---|---|
| Partidos con `defeatTick` **igual** al último tick | **56 de 56 (100 %)** |
| Ticks restantes tras `defeatTick` (media y máximo) | **0** |
| Estadística propia acumulada después de `defeatTick` | **0,00** (frente a ~5 eventos en todo el partido) |

### El argumento estructural que se dio, y por qué era falso

Se escribió que esto era estructural y no casualidad: `CheckForfeit` termina el partido en cuanto un equipo
baja de 5 en el campo, el mismo número que `RunRules.MinimumAvailablePlayers`, y **«como los jugadores en
el campo son un subconjunto de los disponibles en plantilla»** el umbral de plantilla no podría cruzarse
antes. Se llegó a escribir como comentario normativo dentro de `/Sim`.

**REJECTED por experimento** (revisión independiente, 23 sep 2026). Los del campo **no** son un subconjunto
de los disponibles: `RunLineup.CanStart` (`RunLineup.cs:124-131`) admite al **lesionado grave marcado**
(RF-093 vía 1), a quien `RunState.IsAvailable` (`RunState.cs:218`) **no** cuenta como disponible. Medido
con una plantilla de 7 = 5 sanos + 2 con lesión grave marcados:

```
TITULARES QUE SALEN AL CAMPO: 7
DISPONIBLES EN PLANTILLA:     5
tras UNA lesión grave más: disponibles=4 (defeatTick), en el campo quedarían 6 (sin incomparecencia)
```

Es decir: `defeatTick` se fija y **el partido sigue jugándose hasta el final**. **La ventana existe.** Y no
es un caso sólo de jugador humano: la política automática de `/Balance` alinea lesionados graves cuando la
plantilla va corta (`RunPolicy.cs:875-905`, `shortHanded`).

**Qué queda entonces de la medición.** El 56/56 pasa de CONFIRMED a **LIKELY**: es una muestra de la
política por defecto, no un experimento que aísle el caso. Para medirlo de verdad hay que **forzar** la
configuración (once con lesionados graves y plantilla al mínimo), no muestrear.

**Lección de método, que es lo que más duele aquí**: la conclusión se escribió como teorema —y dentro del
código, donde un lector futuro ya no la comprobaría— a partir de un razonamiento que nadie había
verificado. Es exactamente lo que la Regla F existe para impedir. Lo que un comentario afirma como
estructural, o lo demuestra un test, o se escribe como *«medido, no demostrado»*.

**Lo que sí se arregló de paso**: `CheckForfeit` llevaba **tres** `5` literales y
`RunRules.MinimumAvailablePlayers` es otra constante en otro fichero, midiendo lo mismo sin nada que las
atara. Ahora hay `MatchEngine.MinimumPlayersOnPitch` y un test que las ata
(`Sim.Tests/Run/ForfeitThresholdTests.cs`). Es una condición **necesaria y no suficiente**: útil, pero no
el invariante que se creyó tener, y su documentación lo dice ahora con esas palabras.

### Qué queda por decidir

La ventana existe, es rara y su gravedad sigue siendo **baja** —la run ha terminado—, así que no se arregla
a ciegas. Las opciones, sin decidir: que el paso 3b respete `defeatTick` como el bucle de bajas (coherente
con el contrato escrito, pero cambia lo que se guarda en runs perdidas), o **cambiar el contrato** y
aceptar que el historial de carrera recoge el partido entero porque el jugador lo jugó entero. Lo segundo
puede ser lo correcto: lo que pasó en el campo pasó, aunque la run ya estuviera perdida.

## Síntoma 2, arreglado (23 sep 2026)

`PlayedTicks` ya compara equipo (`Team == 0`), igual que el paso 3b catorce líneas más arriba. La
separación de rangos de id es real hoy —la plantilla propia crece de decenas en decenas y los rivales
arrancan en 1.000.000 y 2.000.000— pero **es implícita, no la fuerza ningún assert**: se rompería con un
`OpponentFirstPlayerId` más bajo o el día que los ids propios dejaran de reiniciarse por run (una
meta-progresión acumulativa, por ejemplo). Era el punto exacto donde una colisión futura habría fallado
**sin excepción y sin test**, devolviendo los ticks del jugador equivocado para decidir experiencia y
banquillo. Ahora no depende de la coincidencia numérica.

## Hermanos

- `docs/decisiones/0124-historial-de-carrera-y-atribucion-de-muerte.md` (enmienda) · [BE-B](./BE-B.md)
