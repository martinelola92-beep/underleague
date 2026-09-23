# BB-N — El saque de córner no ocurre nunca

**Estado:** Abierta — causa CONFIRMED y **decisión tomada (23 sep 2026): se modela**, vía [ADR 0135](../decisiones/0135-el-balon-tiene-altura.md). Se cierra con el paso 4 de `docs/plan-altura-del-balon.md`

## Observación

Hallazgo del `independent-reviewer` durante la revisión de BB-B (barrera geométrica de reanudación,
16 sep 2026): en 60 partidos de referencia (dos árboles distintos, con y sin la barrera), **cero saques de
córner**. `RestartKind.Corner`, `ScheduleCorner`, `CornerTicks` y el `case RestartKind.Corner` de
`ResolveRestart` existen en el código y nunca se ejercitan en la muestra.

## Medición (23 sep 2026, 2000 partidos de referencia, semillas 1..2000)

Sonda sobre los eventos `RECOVERY` por detalle:

| Reanudación | Veces en 2000 partidos |
|---|---|
| `freeKick` | 7 870 |
| `kickoff` | 6 551 |
| `goalKick` | **3 289** |
| `throwIn` | 2 436 |
| `corner` | **1** |

Además: **cero** fotogramas con el balón suelto y la `X` fuera del campo (`CheckOutOfBounds` resuelve en el
mismo tick, consistente).

El córner **no es imposible: es 1 de cada 3 290 llegadas a la línea de fondo.** La hipótesis "nunca se
programa" queda REJECTED en sentido literal; la buena es "la condición es casi inalcanzable".

## Hipótesis

- **H1 — el córner nunca se programa.** **REJECTED**: se programó 1 vez en 2000 partidos.
- **H2 — se programa y algo lo reconvierte antes de resolverse.** **REJECTED**: la única llamada a
  `ScheduleCorner` (`MatchEngine.cs:2741`) va directa a `BeginRestart`, y `ResolveRestart`
  (`MatchEngine.cs:3003`) tiene su `case` completo y funcional. La resolución está entera; lo que no
  ocurre es la programación.
- **H3 — el balón nunca llega a la línea de fondo.** **REJECTED**: 3 289 saques de puerta lo prueban.
- **H4 — CONFIRMED: cuando el balón cruza la línea de fondo, el último en tocarlo es SIEMPRE el
  atacante**, así que `CheckOutOfBounds` (`MatchEngine.cs:2731-2742`) siempre toma la rama de saque de
  puerta. La lógica de decisión es correcta; lo que falta son las dos vías por las que un **defensor**
  mandaría el balón fuera por su propia línea de fondo:

  1. **Tiro desviado o despejado a córner: imposible por construcción.** `CheckOutOfBounds` sale de
     inmediato mientras el balón es un tiro en vuelo (`MatchEngine.cs:2713`, `_ball.IsShot`), y
     `ResolveShotArrival` (`:1948-1958`) resuelve **siempre** saque de puerta cuando el tiro va fuera. Hay
     un comentario en `:1956` que lo dice explícitamente. **Ningún disparo puede producir un córner.**
  2. **Rechace o despeje defensivo que sale por el fondo: imposible por velocidad.** El bloqueo de un tiro
     deja el balón con `SetLoose(new Vec2(0f, 0f))` (`MatchEngine.cs:1256`), es decir **parado** donde
     estaba. De los tres `SetLoose` del motor, sólo el del **pase fallado** (`:1430`) da velocidad, y es
     `LooseBallSpeed = 0.1f` con `looseBallFrictionPercent = 92`: recorrido máximo
     `0.1 / (1 − 0.92)` = **1,25 casillas** más allá del destino del pase, y cualquiera a menos de
     `PickupRadius = 0.5` lo recoge antes.

  Por tanto la **única** vía viva hacia el córner es: un **pase de un defensor hacia su propia línea de
  fondo**, que falle, que ruede hasta salir dentro de ese margen de 1,25 casillas, y que ni su portero ni
  nadie toque por el camino — justo la zona donde el portero está siempre. De ahí el 1 de 2000.

## Decidido: se modela (23 sep 2026)

**El revisor decide modelar el córner**, y con él algo mayor: que el balón tenga altura y que un toque
defensivo pueda desviarlo en vez de atraparlo siempre. La salida **B** —retirar `RestartKind.Corner`—
queda **rechazada**.

Todo el diseño está en **[ADR 0135](../decisiones/0135-el-balon-tiene-altura.md)** y en
`docs/plan-altura-del-balon.md`. Esta ficha se cierra cuando el paso 4 de ese plan esté medido y los
córners aparezcan con una frecuencia de fútbol.

## Qué decisión quedaba

No es un bug con arreglo evidente: es una **regla de fútbol que el motor no modela**. Las dos salidas
posibles cambian el partido y necesitan `game-design-review` y ADR antes de tocar código:

- **A — el tiro desviado puede irse a córner**: que `ResolveShotArrival` consulte el último toque en vez de
  forzar saque de puerta, y/o que un bloqueo defensivo (`:1256`) deje el balón con velocidad hacia la línea.
- **B — aceptar que el córner no existe** y retirar `RestartKind.Corner` con su tuning, para que el código
  no prometa una regla que no ocurre (deuda muerta, y engaña a quien lea el motor).

Coste de oportunidad real: el córner es una situación de **ataque estático** que hoy no existe, y varios
perks/rasgos de área (rematadores, `Brute` en el área) tendrían ahí su momento natural.

## Hermanos

- [BB-G](./BB-G.md) — "el balón se queda parado en el campo": misma área del motor. La causa común
  medida es la misma: **el balón suelto casi no tiene velocidad**, así que ni sale del campo ni llega a
  ningún sitio.
- [BC-G](./BC-G.md) — "el balón se queda suelto en el córner y nadie lo coge".
- [BB-B](./BB-B.md) — donde se detectó, de camino, sin ser el objeto de esa revisión.
