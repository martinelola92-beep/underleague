# BJ-A — Lo que la ADR 0148 dejó sin decidir sobre los enfriamientos

Estado: **ABIERTA** (25 sep 2026). Nace de la revisión independiente de la
[ADR 0148](../decisiones/0148-el-enfriamiento-de-entrada-es-disputa.md), que aprobó el cambio y señaló
cuatro cosas que esa ADR **no** resuelve. Ninguna es un bug: son decisiones no tomadas.

## 1. Los otros tres enfriamientos, y por qué esto es incómodo

La ADR 0148 establece que **un enfriamiento de entrada es disputa, no física**, así que no corre durante una
reanudación. Por ese mismo principio, deberían seguirlo:

- `DribbleDuelCooldown` — un duelo de regate **es** una disputa.
- `BlockCooldown` — una carga a un rival sin balón.
- `AerialCooldown` — un duelo aéreo.

Los tres siguen corriendo en la pausa. La razón que da la ADR es metodológica y es buena —no se cambian
cuatro cosas a la vez y luego se atribuye el efecto a una— pero hay que decir lo incómodo: **el principio se
aplicó exactamente donde bajaba el número y en ningún otro sitio**. Aunque el razonamiento sea correcto, la
forma es indistinguible de una racionalización, y por eso existe esta ficha en vez de una nota al pie.

**Cómo se cierra**: una tanda por enfriamiento, cada una con su baseline del mismo árbol.

## 2. Congelar contra reducir — nunca se comparó

`docs/plan-balon-parado-posicional.md` §6 planteaba tres salidas, y la que su propio autor decía que **más
le convencía** era la (a): **tasa reducida** (`deadBallRecoveryPercent`), no un congelado. La ADR 0148
implementa **todo-o-nada** y no midió la graduada. Las dos no están comparadas, así que no se sabe si el
congelado es la forma correcta o sólo la primera que se probó.

## 3. La regla es invisible para el jugador

Un enfriamiento que corre con el balón en juego y se para en una reanudación **no se ve, no se lee en
ningún sitio y no se puede planificar**. El proyecto ordena *comportamiento observable > modificadores
numéricos invisibles* y *eventos explícitos > transiciones invisibles*. La ADR 0148 es un arreglo legítimo
de motor, pero **no se pregunta ni una vez si el jugador debería enterarse**. Pide
`game-design-review` (Regla B: es una primitiva de motor modificada), que la ADR tampoco pasó.

## 4. Asimetría nueva entre la ADR 0142/0147 y la 0148

En una pausa larga, la **energía recarga** (es física) y el **enfriamiento de entrada no** (es disputa). Un
jugador vuelve al juego **descansado pero incapaz de entrar**. Es una interacción nueva entre dos sistemas
y nadie ha decidido si es deseable o un efecto colateral que hay que limar.

## Hermanos

- **[BI-D](./BI-D.md)** — de su censo salen dos hallazgos que tampoco tienen dueño: el portador se pasa el
  **78 % de sus ticks armando un pase**, y **proteger dura 50,51 ticks con `ShieldingTicks` en 12** (más de
  cuatro veces su contador, con el 84 % terminando *con* el balón).
- **[BB-P](./BB-P.md)** — «las puertas de una sola semilla se leen como causa cuando son ruido». Es el
  hermano con nombre de lo que pasó con `ARunCanBePlayedFromStartToFinish`, que se rompía con cada cambio
  real de `/Sim` (semilla 1 → 2 → 4) y ahora prueba ocho con un suelo de tres victorias.
