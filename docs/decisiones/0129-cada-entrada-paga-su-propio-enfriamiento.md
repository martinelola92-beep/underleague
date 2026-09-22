# 0129 — Cada entrada paga su propio enfriamiento

Estado: **Aceptada** (22 sep 2026, **decisión del revisor**: «implementa la Opción 2 y luego la Opción 3»
sobre las tres que dejó medidas `docs/pendientes/BE-A.md`). Cambia una regla de motor y mueve dos valores
de `/data`, así que pasa por `game-design-review` (esta nota) y por `balance-measure`.

## Problema

`MatchPlayer.TackleCooldown` era **un contador único** para dos acciones distintas. Al resolver una entrada
sin balón se ponía a `OffBallTackleCooldownTicks` —el enfriamiento largo, 12 s— y `Utility.EvaluateTackle`
lo consultaba antes de **cualquier** entrada.

**Consecuencia, medida:** pegarle a quien no lleva el balón dejaba al jugador **sin poder disputarlo**
durante todo ese rato. El freno pensado para dosificar los golpes estaba además gravando la acción de
fútbol que el juego sí quiere.

Se descubrió midiendo la ADR 0125 D2: al dar papel al centrocampista en la fase sin balón,
`tacklesPerMatch` caía de 6,92 a 6,0-6,6 sobre un suelo de 6,00, y no por diseño —el presupuesto de disputa
se lo comía el contador compartido—.

## El hermano que ya estaba resuelto

**El paquete U hizo exactamente esto con el bloqueo sin balón** (ADR 0030 §2). El paquete V los compartía
«para que un jugador no alternara las dos y repartiera golpes cada dos ticks»; el precio medido fue **la
mitad de las entradas por partido** (4,37 → 2,31), y se separaron con este argumento, que sigue escrito en
`MatchEngine`: *«el tope por acción sigue existiendo —cargar dos veces seguidas sigue costando
`block.cooldownTicks`— y disputar el balón deja de pagar por haber cargado»*.

Esta ADR no inventa un patrón: **aplica el que ya existe** al tercer caso, que se quedó fuera.

## Decisión

`MatchPlayer.OffBallTackleCooldown` es un contador propio. La entrada al portador paga
`TackleCooldownTicks`; la entrada al marcado sin balón paga `OffBallTackleCooldownTicks`. Cada rama de
`Utility.EvaluateTackle` comprueba **el suyo**: con un poseedor rival al alcance la entrada sigue siendo
siempre a él (ADR 0105), así que si ese enfriamiento está activo la acción se descarta y **no** se sustituye
por pegarle a otro.

El freno de la ADR 0105 no se toca: volver a pegar sin balón sigue costando el enfriamiento largo.

## Las diez preguntas, en corto

1. **Qué experimenta**: un defensa que acaba de pegar a su marcado puede volver a disputar el balón cuando
   la jugada pase por él. Antes se quedaba mirando 12 segundos sin que nada lo explicara.
2. **Qué decide hoy**: nada; era un acoplamiento invisible entre dos acciones distintas.
3. **Qué debería decidir**: nada tampoco — esto no es una mecánica nueva, es quitar un efecto secundario
   que el jugador no podía prever ni entender. RF-012d: lo malo tiene que ser previsible.
4. **Qué regla representa**: ninguna nueva. Enmienda la implementación de la ADR 0105 y hereda el criterio
   de la ADR 0030 §2 / paquete U.
5. **Sistemas**: `/Sim` (`MatchPlayer`, `MatchEngine`, `Utility`), `/data` (`sim/tuning.json`). Nada en `/Game`.
6. **Alternativas**: (a) dejarlo compartido y bajar el enfriamiento largo — no sirve: el enfriamiento largo
   es el freno de los golpes, bajarlo sube la violencia; (b) descontar solo una parte — un número nuevo sin
   significado; (c) contadores separados, que es lo que el proyecto ya hace con el bloqueo.
7. **Trade-off**: más disputas del balón, y con ellas más contacto total. Se paga con los dos
   enfriamientos, abajo.
8. **Estrategias**: devuelve al defensa que reparte leña la capacidad de defender de verdad, que es la
   condición para que la entrada sin balón pueda ser una identidad de puesto (ADR 0125 D2) y no un castigo.
9. **Degeneración**: `injuriesPerMatch` es la restricción que manda. Sin compensar sube a 0,84 y las
   disputas a 7,86; con los enfriamientos recalibrados queda en **0,79**, por debajo de la línea base.
10. **Cómo se demuestra**: las siete métricas obligatorias en banda en cuatro plantillas, las 43 puertas sin
    empeorar, y la puerta que el cambio rompía sin compensar (`coherentBuildsBeatNone_orc_violence`) en verde.

## Calibración, y por qué se mueven dos números

Separar el contador **libera contacto en las dos direcciones**: las disputas del balón dejan de estar
bloqueadas por los golpes, y los golpes dejan de estar bloqueados por las disputas. Medido en 500 partidos
(semilla 1), con el dato intacto: `tacklesPerMatch` 6,92 → **7,86** y entradas sin balón 5,09 → **5,67**.
Eso saca `coherentBuildsBeatNone_orc_violence` de banda (54,17 contra un mínimo de 58): más contacto para
todos es menos ventaja para quien la buscaba.

Los dos enfriamientos absorben lo liberado, cada uno el de su acción:

| | `TackleCooldownTicks` | `OffBallTackleCooldownTicks` | `tacklesPerMatch` | sin balón | `injuries` | `fouls` |
|---|---|---|---|---|---|---|
| línea base (contador compartido) | 60 | 180 | 6,92 | 5,09 | 0,81 | 7,50 |
| separado, sin compensar | 60 | 180 | 7,86 | 5,67 | 0,84 | 8,56 |
| **separado y compensado** | **90** | **280** | **7,65** | **4,55** | **0,79** | **7,54** |

El punto de trabajo elegido deja las faltas y las lesiones donde estaban —7,54 contra 7,50 y 0,79 contra
0,81— y **sube las disputas del balón**, que es exactamente lo que el arreglo pretende: eran las que el
contador compartido estaba pagando.

## Lo que esta ADR empeora, y por qué no lo arregla ella

`buildsWinDifferently_injuries` baja de **1,30 a 1,14** (umbral 1,40; la métrica **ya estaba roja**). Es el
cociente de lesiones entre la build de contacto y la técnica, y se comprime por la misma razón por la que
ya se remidió una vez —la ADR 0084 lo bajó de 1,5 a 1,4 porque el motor pasó a lesionar algo más a *todas*
las builds—: si el contacto es más accesible para todo el mundo, distingue menos.

**No se recalibra el umbral aquí, a propósito.** La ADR 0130 (Opción 3) mueve la diferenciación de la
violencia al canal de **rasgo**, que es justo el canal que debería sostener ese cociente. Bajar el umbral
antes de esa medición sería ajustar la puerta al resultado en vez de al juego. Si tras la 0130 sigue
comprimido, se remide con datos y con su propia nota.

## Hermanos

`docs/pendientes/BE-A.md` (Opción 2 de las tres) · ADR 0030 §2 y el paquete U (el mismo arreglo en el
bloqueo) · ADR 0105 (el freno que no se toca) · ADR 0125 D2 (la que lo destapó) · ADR 0084 (precedente de
remedir un umbral cuando cambia la escala del canal).
