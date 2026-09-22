# BE-D — RF-125 («30 lesiones en una run») será subcontable si se suma `Career` de la plantilla

Estado: **abierta, de diseño, antes de implementar el logro**. Encontrada el 22 sep 2026 por el
`independent-reviewer` como efecto de segundo orden de la ADR 0124.

## El problema

RF-125 propone desbloquear orcos «provocando 30 lesiones en una sola run». Desde la ADR 0124 existe de
dónde sacar la cifra: sumar `RunPlayer.Career.InjuriesCaused` de la plantilla.

Pero **las contribuciones de un jugador vendido, dado de baja o de un mercenario que se marcha desaparecen
del total**: `WithoutPlayer` lo saca de la plantilla y su carrera se va con él. El jugador vería su progreso
**bajar** sin explicación, lo que choca de frente con RF-125b, que exige que el progreso sea visible y
perseguible de forma deliberada.

## Por qué no se arregla aquí

Porque la respuesta correcta depende de una decisión de diseño que nadie ha tomado: **¿el logro cuenta lo
que hizo el CLUB o lo que hicieron los jugadores que siguen vivos en la plantilla?**

- Si cuenta el club, el contador pertenece a la **run**, no al jugador, y va en `RunState.Counters` con
  clave libre (sin subir versión de esquema).
- Si cuenta a los jugadores, el comportamiento actual es correcto y lo que hay que arreglar es la
  **expectativa**, no el número.

## Nota

`RunCareer` es además un historial **solo de partido**: las lesiones que provocan las cartas de evento van
por `EventCatalog`, no por `MatchEngine.ResolveInjury`, así que tampoco entran. Coherente, pero el nombre no
lo dice y cualquier logro que cuente lesiones lo heredará.

## Hermanos

- `docs/decisiones/0124-historial-de-carrera-y-atribucion-de-muerte.md` (enmienda) · [BE-B](./BE-B.md)
