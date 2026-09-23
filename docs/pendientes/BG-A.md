# BG-A — Tres sitios más tratan la alineación guardada como si fuera el once que juega

**Estado:** Abierta · CONFIRMED por lectura · **no se toca en la ADR 0134 porque mueve balance**

## Observación

Encontrado de camino al arreglar la familia [BA-I](./BA-I.md) / [BB-F](./BB-F.md) / [BC-H](./BC-H.md)
(`docs/decisiones/0134-el-once-efectivo.md`). La causa común de esa familia —`state.Lineup` es la
**intención** del jugador y no el once que salta al campo, que lo decide `RunLineup.Build`— tiene tres
instancias más que la ADR 0134 **deja sin tocar a propósito**, porque corregirlas redistribuye recursos y
desplaza las puertas, y eso exige su propia medición (`balance-measure`), no colarse en un paquete cuyo
criterio de aceptación es «ni una tirada se mueve».

## Las tres, por severidad

1. **`EventSystem.Experience(onlyStarters: true)`** (`Sim/Run/Systems/Events/EventSystem.cs:155`). Una carta
   que da experiencia «a los titulares» la reparte sobre `state.Lineup`. Consecuencias en las dos
   direcciones: **el jugador de relleno que sí jugó no cobra**, y un titular guardado que no llegó a jugar
   —porque estaba lesionado y `PruneLineup` aún no había pasado— sí cobra. Es la única de las tres que
   reparte un **recurso de progresión** (RF-025), así que es la única que puede cambiar niveles y con ellos
   las puertas.
2. **`RunPolicy.CountStarters`** (`Sim/Analysis/RunPolicy.cs:1441`). La política automática cuenta titulares
   sobre la guardada. Severidad menor: ya lleva un repliegue (`count > 0 ? count : RunRules.MaxStarters`) y
   es una **heurística de política**, no una regla del juego. Pero es instrumento de medición, así que si se
   corrige hay que saber que mueve lo que la política decide.
3. **`MarketScreen`** (`Game/Screens/MarketScreen.cs:279`). Parte la lista de plantilla en «titulares» y
   resto con la guardada. Es presentación y, en el mercado, discutiblemente lo correcto —ahí el jugador está
   editando su intención, no mirando un partido—. Se anota por completar el censo, no porque haya que
   cambiarla.

## Qué haría falta para cerrarla

La primitiva ya existirá cuando esto se ataque: `RunLineup.Effective(state)` (ADR 0134 A). El trabajo no es
inventar nada, es **decidir si la carta de experiencia habla del once efectivo o de la intención** —que es
una pregunta de diseño, no de implementación: una carta que dice «tus titulares entrenan» probablemente
habla de a quién alineas, y una que dice «tus titulares cobran una prima por el partido» habla de quién
jugó— y después medir el reparto de experiencia y las 43 puertas.

## Hermanos

[BC-H](./BC-H.md) (misma causa, mitad ya cerrada), [BA-I](./BA-I.md), [BB-F](./BB-F.md).
