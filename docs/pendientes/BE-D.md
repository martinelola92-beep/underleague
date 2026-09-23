# BE-D — RF-125 («30 lesiones en una run») será subcontable si se suma `Career` de la plantilla

Estado: **RESUELTA de diseño (23 sep 2026)** — el contador es de la run, no de la plantilla, y lo dicta
RF-125b. Queda por implementar cuando exista RF-125, que depende del perfil entre runs (gate 4 de la ADR
0123). Encontrada el 22 sep 2026 por el `independent-reviewer` como efecto de segundo orden de la ADR 0124.

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

## Resuelta por lectura de requisito (23 sep 2026): el contador es del CLUB

No hacía falta una decisión nueva: **RF-125b ya la tenía tomada.** Dice que los logros de desbloqueo son
visibles *«con su progreso, para que el jugador pueda perseguirlos de forma deliberada»*. Un progreso que
**baja** cuando vendes a alguien no se puede perseguir de forma deliberada — el jugador no tiene manera de
saber que vender a un veterano le va a quitar veinte lesiones del marcador—. Sumar `Career` de la
plantilla viva incumple RF-125b por construcción, así que queda descartado como implementación de RF-125.

**El contador pertenece a la run**, en `RunState.Counters` con clave libre (sin subir versión de esquema,
como ya hizo la ADR 0124). Cuenta lo que hizo **el club**: un jugador vendido, dado de baja o muerto no
borra el daño que ya provocó, igual que no devuelve el oro que ganó.

Hay un segundo argumento, que esta ficha ya apuntaba sin sacarle la consecuencia: **el contador de run
cierra además el hueco de las cartas de evento.** `RunCareer` sólo recoge lo que pasa en un partido, así
que las lesiones que provocan los eventos de `data/events/` no entran en él por diseño. Un contador que
vive en la run puede sumar desde donde haga falta, y entonces «30 lesiones» significa treinta lesiones,
no «treinta lesiones de las que cuentan».

**No se implementa aquí**: RF-125 (el desbloqueo de razas) no existe todavía, y su hogar natural es el
perfil entre runs, que es el gate 4 de la ADR 0123 y sigue bloqueado. Lo que esta ficha deja resuelto es
**cómo se cuenta cuando se implemente**, que era justo lo que estaba sin decidir y lo que habría llevado a
la implementación obvia y equivocada.

`RunCareer` se queda como está: es el historial **del jugador**, y para eso —la ficha, la memoria de un
veterano— está bien que se vaya con él cuando se va.

## Nota

`RunCareer` es además un historial **solo de partido**: las lesiones que provocan las cartas de evento van
por `EventCatalog`, no por `MatchEngine.ResolveInjury`, así que tampoco entran. Coherente, pero el nombre no
lo dice y cualquier logro que cuente lesiones lo heredará.

## Hermanos

- `docs/decisiones/0124-historial-de-carrera-y-atribucion-de-muerte.md` (enmienda) · [BE-B](./BE-B.md)
