# Qué hace de verdad cada estilo en el campo

**19 sep 2026.** Medición pedida por el revisor **antes** de escribir el tercer perk de `Bulwark`
(`hold_the_line`), con un objetivo explícito: *"el objetivo no es «Bulwark tiene otro bonus numérico»,
es «cuando veo un Muro en partida, entiendo que ese jugador está plantado ahí por algo»"*.

Instrumento: `Sim.Tests/Analysis/BulwarkBehaviourBaselineTests.cs` (`Skip` por coste, ~40 s). Solo lee la
traza (RT-098). No toca nada.

## Cómo se midió, y por qué así

Primera pasada: round-robin de las cinco razas, agrupando por la etiqueta que a cada jugador le tocó.
**Descartada como concluyente**: la etiqueta correlaciona con la raza —un Muro es casi siempre un enano— y
el enano trae su propio sesgo de atributos, así que la comparación mezclaba estilo con raza.

Segunda pasada, la que vale: **estilo FORZADO** (`TeamGenerator.styleBySlot`) sobre plantillas **Human
idénticas**, mismas semillas, y **siempre contra el mismo rival Neutral**. Los cinco brazos solo se
diferencian en la etiqueta. 12 partidos por estilo, 84 jugadores y ~108.000 ticks cada uno.

## El sesgo de atributos SÍ llega

| estilo | fuerza | velocidad | técnica | resistencia | correa |
|---|---|---|---|---|---|
| Neutral | 55,33 | 57,45 | 58,90 | 57,21 | 47,61 |
| Brute | 58,17 | 56,41 | 56,39 | 58,78 | 46,75 |
| Fine | 52,99 | 58,81 | **61,29** | 55,30 | **48,11** |
| **Bulwark** | **57,19** | 55,88 | 57,58 | **60,05** | **45,79** |
| Cold | 54,17 | **54,75** | 60,89 | **61,25** | **45,43** |

Un Muro sale con **+1,9 de fuerza, +2,8 de resistencia y −1,8 de correa** frente a un Neutral. El reparto
funciona (la mitad del ±4 nominal, porque es redistribución de presupuesto a calidad 50).

## La conducta NO cambia

| estilo | CoverSpace | FindSpace | Retreat | casillas recorridas | distancia a su portería |
|---|---|---|---|---|---|
| Brute | **44,0 %** | 33,3 % | 9,9 % | 146,83 | 6,62 |
| **Bulwark** | 41,2 % | 36,5 % | 9,9 % | **148,67** | **7,09** |
| Cold | **40,5 %** | 36,9 % | 9,6 % | **143,67** | **7,09** |
| Fine | 42,5 % | 35,3 % | 9,4 % | 152,31 | 6,88 |
| Neutral | 43,6 % | 34,1 % | 9,2 % | **158,00** | 6,65 |

Tres lecturas, y ninguna es la que el estilo promete:

1. **`CoverSpace` se mueve 3,5 puntos entre los CINCO estilos** (40,5 – 44,0 %). Y el Muro no lidera: es
   **tercero**, por debajo de Brute y de Neutral. El estilo cuya descripción es *"no cede terreno"* cubre
   espacio **menos** que el bruto.
2. **El Muro recorre 148,67 casillas, MÁS que el Frío (143,67)**, pese a tener menos velocidad y menos
   correa que él. No es un ancla: se mueve casi tanto como cualquiera.
3. Lo único que sí apunta en la dirección esperada es la **distancia a su portería (7,09, la mayor)** —
   pero empatado con Cold, que no tiene ninguna identidad defensiva.

**Conclusión: los atributos cambian y las decisiones no.** La etiqueta de estilo hoy casi no se traduce en
conducta observable, que es justo lo que el revisor quería conseguir con el perk.

## Qué significa para `hold_the_line`

**Refuerza la idea y le pone un listón.** Hay un hueco real que llenar: hoy un Muro no se comporta como un
Muro. Pero:

- Un multiplicador pequeño sobre `CoverSpace` **no bastará**: tendría que mover la aguja más allá de la
  banda de 3,5 puntos que hoy separa a **todos** los estilos entre sí. Si el perk mueve dos puntos, el Muro
  con perk seguirá pareciéndose a un Bruto sin él.
- Esta tabla es el control contra el que medirlo. Sin ella, "el perk cambia la conducta" sería una opinión.

## El hermano incómodo

Esto **es el mismo síntoma que `CAT-J`** por otro instrumento. Allí
`buildsWinDifferently_injuries = 1,20` contra un mínimo de 1,40 dice que *las builds física y técnica se
distinguen menos de lo que el diseño pide*. Aquí, que **los cinco estilos se distinguen 3,5 puntos de
`CoverSpace`**.

Las dos mediciones, hechas por caminos independientes el mismo día, apuntan a lo mismo: **el motor
aplana las diferencias de identidad**. Si eso es cierto, un perk por estilo lo tapa en un jugador pero no
lo arregla — y conviene saberlo antes de escribir los perks, no después.
