# BE-B — Un perk `injure` con `target: "actor"` acreditaría lesiones al compañero o a la propia víctima

Estado: **CERRADA (23 sep 2026)** con la opción 1, en los dos contadores: `InjuriesCaused` y
`DeathsCaused` comparan equipo al acreditar. Sigue siendo un mecanismo **sin evidencia de activación** —
ningún dato de `/data` usa hoy esa combinación—, y por eso el arreglo es inerte en balance. Encontrada el
22 sep 2026 por el `independent-reviewer` durante la revisión de la ADR 0124, leyendo código — no jugando.

## Síntoma

`MatchEngine.ResolveInjury` hace `tackler.InjuriesCaused++` **sin ninguna comparación de equipo**, y el
comentario de `MatchPlayer.InjuriesCaused` afirma «lesiones causadas **a rivales**».

Pero `PerkLoader` admite `EffectTarget.Actor` para el efecto `injure`, y `EffectEngine` resuelve `Actor`
como `context.Actor` **sin filtro de equipo**. Un perk con `{"type":"injure","target":"actor"}` suscrito a
un evento cuyo actor fuera el propio portador o un compañero acreditaría al portador por lesionar a los
suyos — o a la víctima por lesionarse a sí misma — y **eso entra en `RunCareer.InjuriesCaused`**, que desde
la ADR 0124 se persiste.

## Estado de la evidencia

**SIN EVIDENCIA DE ACTIVACIÓN.** Ningún dato de `/data` usa hoy esa combinación: el único `injure` de perk
es `dirty_play.json`, con `target: "opponent"`. No es un fallo observable; es un mecanismo que puede
dispararse el día que alguien escriba ese perk.

Lo que sí es falso **hoy**: el mensaje de validación del propio cargador («`injure` solo puede alcanzar a un
rival») no se cumple para `actor`.

## Por qué importa ahora y no antes

Antes de la ADR 0124 una atribución equivocada se perdía al acabar el partido. Ahora **se guarda**, y
RF-125 pone un umbral encima («provocar 30 lesiones en una sola run desbloquea orcos»): una cifra mal
acreditada deja de ser un detalle interno y pasa a desbloquear contenido.

## Opciones, sin decidir

1. Filtrar por equipo en `ResolveInjury` al acreditar (no al resolver la lesión: cambiar la lesión sería
   cambiar el motor). **Barata, y no altera ninguna tirada.**
2. Prohibir `target: "actor"` para `injure` en el cargador, que es lo que su propio mensaje ya afirma.
3. Aceptarlo como diseño y corregir el comentario y el mensaje.

**La 1 y la 2 no son excluyentes.** Ninguna se toca hasta que `game-design-review` diga si un perk puede
acreditar lesiones propias.

## Hay un hermano que esta ficha no decía: `DeathsCaused` (23 sep 2026)

`MatchEngine.Kill` hace `killer.DeathsCaused++` **con el mismo descuido**: sin comparar equipos, y con un
comentario que también afirma «muertes causadas **a rivales**» (`MatchPlayer.cs:499`). Son el mismo fallo
en dos sitios, y RF-125 premia contar cosas de esta familia. Cualquier arreglo que toque uno y no el otro
deja la mitad del patrón viva.

## Nota de diseño (`game-design-review`, 23 sep 2026)

1. **Qué experimenta el jugador.** Hoy nada: ningún dato de `/data` usa esa combinación. El día que la use,
   vería subir su contador de «lesiones causadas» al lesionar a un compañero o a sí mismo.
2. **Qué decisión toma.** Ninguna hoy. Potencialmente una mala: con RF-125 («30 lesiones en una run
   desbloquea orcos»), **le convendría lesionar a los suyos** para desbloquear contenido. Eso es
   degeneración, no decisión.
3. **Qué decisión debería tomar.** El contador debería medir daño al rival, que es lo que su nombre dice y
   lo que el logro premia.
4. **Qué regla representa.** RF-122 (historial de carrera) y RF-125 (el logro). **No inventa ninguna**: el
   comentario del propio campo ya dice «a rivales». Es hacer cumplir lo escrito.
5. **Sistemas.** Sólo `/Sim`: `MatchEngine.ResolveInjury` y `MatchEngine.Kill` al acreditar, `PerkLoader`
   al validar. Nada de `/Game`, ninguna lógica repartida.
6. **Alternativas.** Las tres de arriba.
7. **Trade-off.** La **1** no impide el perk, sólo no lo acredita: deja diseñable un perk de daño amigo sin
   que contamine el logro. La **2** cierra esa puerta de diseño. La **3** deja la degeneración abierta.
8. **Cómo cambia las estrategias.** Con la 1, un perk que hiera a los propios sigue siendo posible — y
   encaja de lleno en la identidad del juego, que es la **carnicería administrada**, no el fútbol. Con la 2
   se perdería esa vía por un problema que no era de diseño sino de contabilidad.
9. **Degeneración.** La 3 permite farmear el logro autolesionándose. Inaceptable.
10. **Cómo se demuestra.** Test de que acreditar sólo cuenta víctimas del otro equipo; y el lote de
    `/Balance` byte a byte idéntico, porque hoy ningún dato lo ejercita.

**Decisión: la 1, en los dos sitios, y NO la 2.** El fallo es de contabilidad, no de diseño: cerrar el
`target: actor` de `injure` quitaría una vía de diseño legítima para arreglar un contador que miente.
Además hay que **corregir el mensaje del cargador**, que hoy enumera `actor` dentro de «solo puede alcanzar
a un rival» — `EffectTarget.Actor` es el actor del evento, que puede ser un compañero o el propio portador.

**Fuera de alcance, anotado**: `ResolveInjury` también hace `ShiftBiasAgainst(tackler.Team, …)` sin
comparar equipos, así que lesionar a un compañero mueve el criterio del árbitro en tu contra. Es el mismo
descuido, pero eso **sí cambia el partido**, así que no entra en un arreglo de contabilidad: necesita su
propia medición.

## Hermanos

- `docs/decisiones/0124-historial-de-carrera-y-atribucion-de-muerte.md` (enmienda) · [BE-C](./BE-C.md)
