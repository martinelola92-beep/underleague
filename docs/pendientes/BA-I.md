# BA-I — La sustitución por lesión no debería ser obligatoria

**Estado:** Abierta

## Observación

**La sustitución por lesión no debería ser obligatoria**, y debería decir la **posición** del lesionado. «Los no-muertos no reciben penalización por lesión, así que igual prefiero seguir con él»

## Análisis / estado actual

**Abierta.** Buen caso: la inmunidad `minorInjuryPenalty` de los no-muertos (ADR 0026) hace que sustituir sea a veces un error, y hoy se fuerza

## Resolución 23 sep 2026 — ADR 0134, con la premisa corregida por el camino

**La premisa era falsa y la petición era buena igualmente**, que es una combinación que conviene dejar
escrita. Al medir para implementarla salió que `MatchEngine.RemoveFromPitch` (`:528-533`) apartaba del campo
**toda** lesión, leve o grave, sin mirar la raza, y que el `minorInjuryPenalty` del que habla la ADR 0026 es
−15 % de atributos **entre** partidos (`RunState.cs:312`), no dentro. O sea que «igual prefiero seguir con
él» no describía lo que el juego hacía: el no-muerto también se iba al banquillo, inmunidad o no.

Con eso sobre la mesa la ADR 0134 se escribió dejando la literalidad fuera («es otra mecánica, mayor»), y
**el revisor la metió dentro en el mismo día**: la lesión leve deja de sacar del campo si el jugador no
quiere. La ventana pasa a tener tres respuestas —sustituir, dejar el hueco, seguir jugando—, y quien se
queda paga **ya** la penalización de RF-091 en vez de al acabar el partido; sin ese coste, quedarse sería
siempre mejor que sustituir y no habría decisión que tomar.

**Y así la premisa se vuelve verdadera.** El inmune a `MinorInjuryPenalty` se queda en el campo **entero**,
que es exactamente lo que el revisor esperaba: la inmunidad deja de ser una línea de ficha y pasa a ser una
decisión visible en mitad del partido. Detalle en `docs/decisiones/0134-el-once-efectivo.md` apartado E.

La otra mitad del ticket —«debería decir la posición del lesionado»— ya estaba hecha en la pantalla que el
revisor juega desde `f82a0c1`; ver la corrección de [BB-F](./BB-F.md), que apuntaba al fichero equivocado.

## Hermanos

Mismo síntoma, causas relacionadas: [BB-F](./BB-F.md).
