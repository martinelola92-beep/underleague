# BE-B — Un perk `injure` con `target: "actor"` acreditaría lesiones al compañero o a la propia víctima

Estado: **abierta, mecanismo real sin evidencia de activación**. Encontrada el 22 sep 2026 por el
`independent-reviewer` durante la revisión de la ADR 0124, leyendo código — no jugando.

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

## Hermanos

- `docs/decisiones/0124-historial-de-carrera-y-atribucion-de-muerte.md` (enmienda) · [BE-C](./BE-C.md)
