# BG-C — Tres huecos de contrato que la ADR 0134 destapa y no cierra

**Estado:** Abierta · CONFIRMED por lectura · sin evidencia de activación en ningún camino conocido

## Observación

Los tres salen de la **revisión independiente** de la ADR 0134 (23 sep 2026). Ninguno tiene reproducción en
un camino real conocido, y por eso no se arreglaron dentro del paquete: arreglarlos a ciegas habría añadido
superficie sin una medición que la justifique. Se anotan porque los tres son contratos que hasta ahora
nadie podía romper y ahora sí.

## 1. Un `PlayOn` huérfano se descarta en silencio

«Que siga jugando» (ADR 0134 E) **no** entra en `Answers` de `SubstitutionPoints.ResolveAutomatically`:
viaja en el estado inicial y el motor lo consulta en su tick. Si el partido, al re-resolverse, ya no llega a
esa lesión, nadie se entera. Una sustitución o un rechazo huérfanos sí son `ArgumentException`
(`Substitutions.cs:299-309`), que es lo que la ADR 0094 declaró: *«una respuesta que el partido no llegó a
pedir no se descarta en silencio»*.

**Por qué importa**: `RunEngine.ResolveMatch` pasa `usesPolicy: null`, o sea que **todos** los equipos usan
la política. Si el `PlayOn` queda huérfano, la decisión del jugador desaparece y **la política mete un
sustituto por su cuenta**. BC-E midió que a un 9,4 % de los puntos del jugador les cambia el partido por
delante, así que el caso no es teórico.

Lo complica que un `PlayOn` huérfano **no siempre es un error**: a diferencia de una sustitución, se puede
quedar sin punto por una decisión anterior legítima del propio jugador. Decidir si es error, si se ignora
declarándolo, o si hay que volver a preguntar, es diseño.

## 2. `Apply(SetLineup)` no valida casillas, y ahora las casillas viajan

`RunEngine.ApplyLineup` (`:614-642`) comprueba el número de titulares y que nadie esté muerto; de la casilla
no mira **nada**: ni rango, ni repetición, ni que la portería sea del portero. Daba igual mientras
`RunLineup.Build` las reasignaba todas — desde [BG-B](./BG-B.md) se propagan verbatim y la única red es
`Simulator.ValidateTeam`, que lanza **a mitad de partido**.

`PlacementView.CanPlace` protege el camino de `/Game`, pero `Apply(SetLineup)` es la puerta pública y su
propia documentación la llama «la única puerta por la que pasa una decisión del jugador» (RT-032).

## 3. `LineupWarnings` y `LethalRisks` pasan de no lanzar nunca a poder lanzar

Las dos entran ahora en `RunLineup.Effective` → `Build`, que lanza `InvalidOperationException` por debajo de
`RunRules.MinimumAvailablePlayers`. Está en su XML doc y es coherente —RF-002b dice que ahí la run ya
terminó—, pero es un cambio de contrato de dos APIs públicas que antes solo leían slots. No se ha encontrado
ningún camino de `/Game` que lo alcance.

## Cuarto, menor: `Pending` se encareció dentro del bucle de `/Balance`

Cada llamada calcula `Lethality.CarriersOf` y un riesgo por candidato, y `ResolveAutomatically` la llama
para los dos equipos en cada vuelta de cada partido de las 43 puertas. Es trabajo puro —no mueve ningún
resultado— pero antes no estaba, y **no se ha medido el coste**. Si alguna vez las puertas se notan más
lentas, empezar por aquí.

## Hermanos

[BG-A](./BG-A.md), [BG-B](./BG-B.md) — los tres salen del mismo paquete y de la misma causa: contratos que
solo se sostenían porque nadie podía ejercerlos.
