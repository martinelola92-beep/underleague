# ADR 0105 · La entrada sin balón

**Fecha:** 13 de septiembre de 2026 · **Estado:** aceptada · **Decisión del revisor**
**Toca:** RF-054, RF-063, la IA de utilidad (RT-096), `data/ai/weights.json`, `data/sim/tuning.json`
**Ataca:** AY-A, la caída de entradas de la ADR 0103, y dos acciones muertas

## El diagnóstico, que es lo que justifica el cambio

Un defensa sin balón elige entre estas utilidades (`data/ai/weights.json`, base + táctica):

| acción | total |
|---|---|
| CoverSpace | 560 |
| ChaseBall | 505 |
| **MarkOpponent** | **470**, y luego −30 por casilla de distancia al marcado |
| Retreat | 435 |
| Tackle | 420 |
| PressCarrier | 315 |
| **Block** (carga sin balón, ADR 0030 §2) | **60** |

**`MarkOpponent` pierde contra cubrir y perseguir antes incluso de aplicar la penalización de distancia**, y
`Block` está a 60 contra 560. Las dos son acciones que existen, están implementadas y **no se eligen
nunca**: la propia secuencia de capturas lo avisa —*«ningún jugador elige MarkOpponent en todo el partido»*—
y por eso `partido-marcaje.png` enseña la capa de marcaje vacía.

Es decir: **el marcaje es una promesa que el motor no cumple.** Se puede asignar en la pantalla de Equipo,
se dibuja, se describe en perks… y no ocurre.

## Decisión

**Un jugador defensivo entra a su marcado aunque el marcado no lleve el balón.**

1. `Tackle` deja de tener como único objetivo al poseedor. Cuando **no hay poseedor rival al alcance** y el
   jugador tiene un **marcado válido dentro de la distancia de entrada**, el marcado es objetivo legal.
2. Solo para **roles defensivos**: un delantero no persigue a su par por el campo.
3. El bonus de esa entrada es **menor** que el de entrar al poseedor: quitar el balón sigue siendo mejor
   que pegar a alguien que no lo tiene.
4. **`MarkOpponent` y `Block` suben de peso** lo justo para dejar de perder por goleada. Sin marcaje que
   ejecutar, el punto 1 no tiene a quién entrar.

## Y es falta, que es justo lo que se busca

Entrar a alguien que no lleva el balón **es falta en fútbol**, y aquí también: la entrada sin balón lleva
una probabilidad de falta **mucho mayor** que la normal (`tackle.foulBase` 320 hoy). Eso la conecta con
todo lo que ya existe y estaba infrautilizado:

- el árbitro señala el 80 % (`referee.whistlePercent`, ADR 0090) y la falta reanuda con saque de falta;
- las tarjetas (`yellowCardBase` 250, `redCardBase` 10) pasan a tener de qué alimentarse;
- el criterio del árbitro (RF-062) se mueve de verdad;
- **`mob_instigator`** —que anula una falta, RF-063— deja de ser un perk sin trabajo.

No es un efecto colateral: es la identidad del juego. Hacer una falta táctica a propósito, sabiendo que el
árbitro pita el 80 % de las veces, **es** carnicería administrada.

## Por qué esto cierra AY-A

AY-A lleva abierta desde el paquete AY porque el indicador de riesgo letal se volvió inexacto: los perks
letales se disparan en la **entrada**, la víctima es **quien recibe la entrada**, y antes del partido no se
sabe quién será. El indicador enseña un techo en el marcado y un **cero falso** en los demás, contra
RF-012d.

Con la entrada sin balón, **a quién se entra deja de ser una incógnita**: es el marcado, y el marcado lo
asigna el jugador en la pantalla de Equipo antes de confirmar. El indicador puede decir *«a este lo va a
entrar aquel»* con fundamento, y la contrajugada —mover el marcaje, o no poner al frágil delante del
portador letal— se convierte en una decisión real en vez de en un número que no se puede usar.

Es la vía que el revisor eligió sobre las tres de AY-A: en vez de refinar el número, **hacer predecible el
suceso que lo genera**.

## Lo que hay que medir, y el riesgo

La ADR 0103 bajó las entradas de 11,23 a 9,19 por dilución de densidad, dentro de banda. Esto empuja en
sentido contrario, así que hay que vigilar **por arriba**:

| métrica | banda | antes |
|---|---|---|
| `tacklesPerMatch` | 6-14 | 9,19 |
| `injuriesPerMatch` | 0,30-0,90 | 0,75 |
| `foulsPerMatch` | INFO | 5,17 / 3,67 |
| `yellowCardsPerMatch` | INFO | 0,27 / 0,19 |
| `redCardsPerMatch` | INFO | 0,08 / 0,04 |
| `deathsPerRun` | 1,5-3 | 1,85 |
| `possessionChanges` | 12-28 | 21,79 |

**El riesgo concreto:** si la entrada sin balón sale barata, el partido se convierte en un correr a pegarse
y `passChainAvgLength` y `possessionChanges` se hunden. La probabilidad de falta es el freno, y por eso se
calibra **a la vez** que el bonus, no después.

## Medición

Calibrado **con los cuatro números a la vez**, 2.000 partidos por tanda, semilla 1, `reference.json`:

| | roles | bono | falta | enfriamiento | **entradas** | lesiones | faltas | tiros | cadena | alternancias |
|---|---|---|---|---|---|---|---|---|---|---|
| base | — | — | — | — | 9,19 | 0,75 | 4,34 | 8,41 | 2,17 | 21,79 |
| A | Def+Mid | 150 | 6000 | 60 | **23,90** | 1,21 | 15,85 | 6,87 | 2,06 | 17,79 |
| B | Def+Mid | 0 | 6000 | 60 | 14,55 | 0,97 | 8,60 | 7,69 | 2,09 | 20,20 |
| C | **Def** | 0 | 6000 | 60 | 10,34 | 0,77 | 5,15 | 8,27 | 2,15 | 21,45 |
| **final** | **Def** | **150** | **6000** | **180** | **12,14** | **0,82** | **7,76** | **8,31** | **2,12** | **20,53** |

**Solo `Defender`.** Lo mide B contra C: con el mismo bono, incluir al centrocampista sube las entradas de
10,34 a 14,55, las lesiones a **0,97 (fuera)** y hunde los tiros a **7,69 (fuera)**. Su `CoverSpace` pesa la
mitad que el del defensa (260 contra 420), así que la entrada le gana casi siempre.

**El bono conmuta, no dosifica.** Barrido: 62 → 10,55 · 65 → 12,82 · 67 → 14,45 · y de 70 a 120 **cifras
idénticas hasta la segunda decimal**. Para un defensa, `Tackle` y `MarkOpponent` son casi constantes
(`420 + bono` contra `600 − 30·d`), así que el bono es un acantilado de tres puntos de ancho. Entregar la
calibración sobre ese escalón sería frágil: cualquier retoque futuro de `CoverSpace` lo volcaría sin avisar.
Por eso el ritmo lo pone un **enfriamiento propio** (`offBallTackleCooldownTicks` 180 contra 60), que sí es
continuo: 60 → 14,55 · 120 → 13,38 · 180 → 12,29 · 240 → 11,75.

**La falta frena las consecuencias, no el número**: 3000 → 15,00 · 6000 → 14,49 · 9500 → 14,16. A 8000 las
lesiones se van a **0,92, fuera de banda**.

### Sigue pareciendo fútbol, y así se comprueba

Las faltas casi se duplican (4,34 → 7,76) y las tarjetas también (0,21/0,05 → 0,42/0,12) — que es lo que se
buscaba: el árbitro por fin tiene trabajo y `mob_instigator` tiene algo que anular. Lo que **no** se movió
es lo que mide si el partido se juega: cadena 2,17 → **2,12**, alternancias 21,79 → **20,53**, tiros 8,41 →
**8,31**. Con la entrada barata (tanda A) esos tres se hundían a la vez a 2,06 / 17,79 / 6,87; en el punto
final ni se enteran. El freno funciona.

Run completa (1.200 runs): `runWinRate` 25,58 → 23,50 (dentro), `deathsPerRun` 1,51 → 1,62 (dentro),
`brokeMarketRunShare` 9,33 (fuera) → 11,42 (**dentro**, de rebote: más lesiones, más gasto de clínica).

**686 tests rápidos y las 43 puertas en verde.**

## Lo que queda anotado

- **`MarkOpponent` ha revivido de verdad**: del **1,26 % al 5,68 %** de las decisiones en el censo de
  utilidad. La capa de marcaje deja de estar vacía y `partido-marcaje.png` deja de mentir.
- **`Block` sigue elegido el 0,00 %**, y **subirle el peso no lo arregla: medido y revertido** (CAT-F). El
  censo de utilidad da la razón: se **descarta el 74,9 %** de las veces antes de puntuar, porque su
  precondición es un alcance de **1,2 casillas** con una penalización de **300 por casilla**. Al alcance
  máximo la penalización se come el bono entero. Despertarlo exige cambiar **qué es una carga**, no cuánto
  pesa.
