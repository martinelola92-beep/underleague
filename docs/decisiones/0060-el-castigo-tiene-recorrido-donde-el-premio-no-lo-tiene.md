# 0060. El castigo tiene recorrido donde el premio no lo tiene

**Fecha:** 2026-09-06
**Estado:** Aceptada e **implementada** (`fase2-diseno.md` §28)
**Corrige:** el **punto 3** de la ADR 0059, cuya premisa de discriminación queda **falsificada por medición**
**Requisitos:** RT-055, RT-057, RF-032, RF-072
**Relacionada con:** ADR 0033, ADR 0038, ADR 0048, ADR 0050 (P1), ADR 0056 (objetivos), ADR 0057, ADR 0058, ADR 0059

## Lo que la fase de medición de la ADR 0059 devuelve

**1. El instrumento estaba desafinado, pero el desafine era ruido, no sesgo.** `data/economy/perk-values.json`
se regenera con **3.072 partidos por perk** en vez de 384 (dos lotes independientes, semillas 5 y 11, que se
suman). A 384 partidos la desviación por fila es de **51 unidades** de esa escala y la dispersión real entre
perks es de **50**: *la mitad de la varianza de la tabla vieja era ruido de medición*. Con el instrumento
afinado, las tablas **no se mueven**: build buena 52,43 → 52,28 en el acto 2 y 41,99 → 42,03 en el 3, hueco
6,27 → 6,81 (ET 0,7). Lo que sí cambia es el orden de compra —entran seis perks que no tenían fila, entre
ellos los tres maestros medibles— y la build mala **empeora**, de 9,92% a 12,17% de runs completadas.

**2. Concentrar satura, pero no por el techo del canal: por la base del canal.** Medido sobre partidos
reales (4.800 por celda, mismo portador, un perk sintético por escalón):

| ×2 acumulados sobre un mismo portador | 1 | 2 | 3 | 4 |
|---|---|---|---|---|
| `pass` (base 77%) | +0,54 | +0,23 | +1,56 | **+0,19** |
| `shotOnTarget` (base 78,5%) | +1,23 | +0,27 | +0,27 | +0,73 |
| `dribble` (base 72%) | −0,67 | +1,60 | +0,58 | +0,08 |
| `tackle` (base 28%) | +1,81 | +2,42 | +4,04 | +3,79 |
| `intercept` (base 2,5%) | +2,06 | +3,58 | +10,02 | **+15,54** |
| mezcla `pass`+`tackle`+`dribble`+`intercept` | +0,54 | +1,85 | +1,48 | **+3,58** |

Cuatro ×2 sobre el pase compran **0,19 puntos** de tasa de victoria; los mismos cuatro ×2 repartidos entre
cuatro canales compran **3,58**. La sospecha de la ADR 0059 se confirma **en los canales de base alta** y se
falsifica en los de base baja, donde concentrar no satura: **acelera**. El 2%-98% de `tuning.resolution`
casi no interviene; lo que manda es que doblar una cuota mueve poco una probabilidad que ya está alta.

**3. La premisa de discriminación del punto 3 es falsa.** La ADR 0059 justificaba el pago por coherencia con
*"una build mediocre nunca completa una línea, así que nunca cobra"*. Medido sobre 1.200 runs por doctrina,
es al revés: la doctrina **mediocre** (gastadora) lleva **4,31** perks distintos de su mejor línea y cierra
**0,43** maestros por run; la **buena** (contextual) lleva **3,24** y cierra **0,22**. El 86,5% de las runs
mediocres tiene tres o más piezas de una línea, frente al 71,2% de las buenas. La razón es estructural: la
contextual **rechaza** perks (termina con 9,4 distintos frente a 10,8) y su filtro es el valor medido, que es
ciego a la línea. **Un pago por coherencia le pagaría más a la build mediocre que a la buena y cerraría el
hueco en vez de abrirlo.**

## Lo que sí separa a los dos perfiles, medido

La diferencia entre las dos doctrinas es casi exactamente **el valor medido de los perks que llevan**:
287,9 milésimas por run la buena frente a 69,9 la mediocre, con 9,4 y 10,8 perks respectivamente. Y la
mecánica que lo produce está en el código de la política y es exactamente la del juego: la contextual
comprueba `PerkPlacement.Fits` antes de dar un perk a un portador —no compra lo que solo va a aplicar su
castigo— y **la gastadora no**. Es la definición operativa de "construir bien" que este proyecto ya tenía.

Y ahí es donde la aritmética de cuotas da la palanca, porque **es asimétrica**. Medido con el mismo
instrumento, sobre todo el equipo:

| canal | ×1,3 | ×2 | ÷2 | ÷3 | ÷4 |
|---|---|---|---|---|---|
| `pass` | +2,33 | **+2,02** | −1,02 | −2,70 | **−4,35** |
| `tackle` | −0,12 | +4,73 | −3,50 | −6,38 | −6,50 |
| `dribble` | — | — | −2,33 | −5,17 | −7,65 |
| `shotOnTarget` | +2,88 | +8,12 | −8,88 | −14,12 | −20,60 |

**En un canal de base alta el premio satura y el castigo no.** Doblar la cuota de pase del equipo entero
vale 2 puntos y no crece; dividirla vale 1, 2,7 y 4,4 puntos y sigue creciendo. El catálogo tiene recorrido
justo en el lado que **solo paga quien pone el perk donde no funciona**.

Y una segunda asimetría, también medida: un castigo sobre el **portador** no vale nada (−0,15 a −0,98
puntos, dentro del ruido); sobre el **equipo** vale de −1 a −8,9. El castigo con dientes es el del equipo.

## Decisión

**1. `data/economy/perk-values.json` se regenera con ocho veces la muestra** y el `_doc` deja escrito el
protocolo nuevo (dos lotes de 48×32 que se suman) y por qué: a 384 partidos la tabla medía tanto ruido como
señal.

**2. El perk mal puesto lo paga el equipo, no su portador.** Los castigos de `elseEffects` que estaban en
`owner` pasan a `team` en los seis perks de duración `match` que los tenían (`fine_touch`, `fine_orchestra`,
`center_conductor`, `flank_specialist`, `forward_line`, `pivot_duo`) y en los dos de magnitud pequeña
(`brute_boots`, `covering_shadow`). Los de duración `play` no se tocan: son momentáneos y su ámbito es la
acción. Un jugador fuera de sitio desordena al equipo, no solo a sí mismo — y es lo único del catálogo que
**por construcción** solo paga quien construye mal.

**3. El techo de la rareza acota lo que el perk da, no lo que quita.** `elseEffects` gana un escalón de la
escala sobre `ProbabilityScale.CeilingFor` (`DrawbackCeilingFor`: común 200, poco común 300, raro y
legendario 500; con contador, `CounterDrawbackCeilingFor`). El motivo no es de gusto sino de aritmética: la
rareza es lo que se **paga** en el mercado, y el castigo no se paga, se elige; y como el premio satura y el
castigo no, con el mismo techo el castigo se queda sin recorrido justo donde hace falta. Lo aplica
`PerkLoader`, que ya sabe si está leyendo `effects` o `elseEffects`.

**4. La capa de build del rival ordinario baja de 2/7/9 a 2/1/2 perks por plantilla**, conservando
**siempre** el perk letal. Es el punto 4 de la ADR 0059 y viene con un hallazgo: recortar la capa sin
proteger los letales hunde `deathsPerRun` de 1,44 a **0,55**, muy por debajo de su banda. La letalidad del
rival ordinario no es parte de su "capa de build": es la ADR 0048.

## Qué se ha conseguido y qué no

| Objetivo (ADR 0056) | ADR 0058 | tras afinar | final | ET | meta | |
|---|---|---|---|---|---|---|
| Build buena, actos 2/3 | 52,43 / 41,99 | 52,28 / 42,03 | **57,97 / 44,43** | 0,71 / 0,53 | 60% | falta 2,0 |
| Build mediocre, actos 2/3 | 46,15 / 34,43 | 45,47 / 35,52 | **47,94 / 40,67** | 0,70 / 0,25 | 42-45% | se pasa 2,9 |
| Build mala completa la run | 9,92% | 12,17% | **12,00%** | 0,87 | < 2% | no |
| Suelo sin build | 10,09% | 10,50% | **10,66%** | 0,56 | < 10% | no |
| **Hueco buena/mediocre, acto 2** | **6,27** | **6,81** | **10,03** | **0,50** | **> 9,8** | **sí, por primera vez** |
| Tasa de victoria de la run | 15,92% | 16,67% | **17,00%** | 1,28 | 20-30% | falta 3,0 |

**El objetivo central de la ADR 0056 —la separación entre perfiles— se alcanza por primera vez en cuatro
paquetes**, y se alcanza con una palanca que no toca a nadie que construya bien: medido aislado —solo el
cambio de ámbito de `owner` a `team`, con el rival sin tocar y sobre dos semillas— la build buena se queda
exactamente donde estaba (52,27 frente a 52,28), la mediocre baja de 45,47 a 43,69 y el hueco pasa de 6,81 a
8,58.

**Lo que no se alcanza tiene una razón medida y no es de calibración.** La capa de build del rival ordinario
está **agotada**: de 7/9 perks a 1/2 la build buena sube 5,7 puntos en el acto 2 y ahí se acaba el
combustible, porque lo que queda del rival son los perks **letales**, que no se pueden quitar. El 60% no es
alcanzable retocando esa capa; hace falta otra palanca o revisar el objetivo.

## Qué falsificaría esta decisión

- **Que el castigo empiece a morder a quien construye bien.** Se vigila con la columna "correcta" de la
  ADR 0033 y con la build buena sin tocar el rival; si esa cifra baja, el castigo ya no discrimina.
- **Que `randomBuildNearNone` no vuelva a banda.** Ya ocurrió una vez en este paquete (38,54 con
  `flank_specialist` a ÷3 sobre el equipo, banda 40-60) y se corrigió **bajando la palanca**, no el umbral.
  Es el techo real de esta palanca, junto con la celda `incoherente` del jefe del acto 1 (21,6 sobre un
  mínimo de 20).
- **Que la build mala siga completando la run por encima del 10%.** Con el castigo del perk mal puesto en su
  máximo tolerable la mediocre se queda en el 12%: si eso no baja, el problema no es cómo se castiga la
  incoherencia sino cuántos partidos ordinarios se pueden perder sin terminar la run (RF-002c).
