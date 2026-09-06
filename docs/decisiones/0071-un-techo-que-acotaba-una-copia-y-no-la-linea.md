# 0071. Un techo que acotaba una copia, y decía que acotaba la línea

**Fecha:** 2026-09-06
**Estado:** Aceptada e **implementada**. Toca `/Sim` (una validación nueva) y **un número** de
`data/perks/`
**Cierra:** AR-B
**Requisitos:** RT-031, RT-032, RT-054, RT-057, RF-070
**Relacionada con:** ADR 0050 (P1, la escala de cuotas), ADR 0058 (el techo por rareza),
ADR 0060 (lo que vale el ámbito de equipo), ADR 0069 (donde se detectó)

## El defecto

`ProbabilityScale.CounterCeilingFor` existe, según su propio comentario, para que **"cinco copias de un
raro no claven su canal en el 98%"**. No lo consigue, y no puede: acota `k`, no `k^maxValue`.

Un efecto con contador tiene **dos** magnitudes y sólo una estaba validada:

| | qué es | quién la acotaba |
|---|---|---|
| `valuePerCounter` | lo que vale **una unidad** del contador | `CounterCeilingFor(rareza)` |
| `k^maxValue` | lo que el motor **acaba aplicando** | **nadie** |

Y `maxValue` es libre entre 1 y 10, así que la segunda no está acotada por la primera. Dos casos, los dos
reales:

- **`deathless_march`** (raro, `team`, `tackle`) cabía en su techo de rareza con `k = 3` y `maxValue` 5, y
  su línea valía `3⁵ = 243` sobre el robo de **todo el equipo**: la cuota del canal pasa de 0,597 a 145 y
  el motor la clava en su `probabilityCeiling` del 98% desde el quinto partido. Es **literalmente** la
  patología que el comentario decía evitar (ADR 0069 §34.4).
- Un **común** con `k = 1,5` y `maxValue` 10 vale ×57,7 sin salirse de ningún techo.

Y el techo tampoco distingue el **ámbito**: el mismo `k` sobre el portador y sobre los siete titulares no
vale lo mismo, y la ADR 0060 lo midió en **2-4×** a favor del equipo (§28.5).

## Decisión

**1. El techo por rareza se queda como está** y su comentario se corrige: acota **una unidad**, que es lo
único que puede acotar, y deja de decir que acota la línea.

**2. Se añade un segundo techo, `ProbabilityScale.CounterLineCeilingFor(rareza, ámbitoDeEquipo)`**, que
acota `k^maxValue` —el multiplicador que el motor aplica cuando el contador llega a su tope— y lo hace
distinto según el efecto caiga sobre un jugador o sobre un equipo entero (`team`, `opposingTeam`):

| rareza | línea sobre **un jugador** | línea sobre **el equipo** |
|---|---|---|
| común | ×8 | ×2 |
| poco común | **×32** | ×8 |
| raro | ×64 | **×16** |
| legendario | ×128 | ×32 |

Se acota la **magnitud** de la línea y no su dirección: multiplicar la cuota por k y dividirla por k son
la misma cifra con dos verbos (ADR 0058), y una línea que divide por 1.024 desborda el canal igual que una
que multiplica por 1.024. Hoy los quince perks con contador son todos positivos, así que esa mitad de la
comprobación no caza nada: está para que no haya que acordarse.

El techo de un `elseEffects` con contador es cuatro veces mayor, por la misma razón y con la misma
aritmética que `DrawbackCeilingFor`: la rareza acota lo que el perk **da**, no lo que quita cuando está
mal puesto (paquete AL).

**3. `deathless_march` baja de `maxValue` 5 a 4**, que es el único perk del catálogo que el techo nuevo
caza. Su línea pasa de ×32 a ×16, justo en su techo.

## De dónde salen los números, medidos

**El ámbito individual no es un criterio nuevo: es el catálogo que la ADR 0069 midió y entregó, convertido
en regla.** Ese catálogo lleva líneas de ×32 en perks poco comunes sobre un solo jugador —`lane_reader`,
`silky_veteran`, `sharpshooter_drill`, `poacher_instinct`— con las doce celdas de la ADR 0033 en banda y
un banco de 1.200 runs medido, así que **×32 en poco común es lo que hay evidencia de que el juego
tolera**. Los otros tres escalones se derivan de ése doblando: no hay medición que los sostenga y por eso
están ahí como techo y no como objetivo — hoy ningún perk raro ni legendario los usa.

**El factor 4 del ámbito de equipo** es el extremo prudente del 2-4× que midió la ADR 0060, y son dos
escalones de la escala.

**Y que ×16 es donde el canal deja de pagar está medido**, con el instrumento en campaña de la ADR 0070
(`--perk-values --rosters 96 --runs 8 --seed 5`, 768 partidos por condición, desviación de fila ~36
unidades):

| `deathless_march`, línea sobre el robo del equipo | valor medido | lo que compra doblar |
|---|---|---|
| `maxValue` 3 → ×8 | 240 | |
| `maxValue` 4 → **×16** | **279** | **+39** |
| `maxValue` 5 → ×32 | 289 | +10 |

**Doblar de ×8 a ×16 compra 39 unidades; doblar de ×16 a ×32 compra 10, que es menos de un tercio del
error de fila.** El canal satura exactamente donde el techo nuevo lo corta: la cuota del robo pasa de
0,597 a 9,55 (90,5% de probabilidad) a ×16 y a 19,1 (95,0%) a ×32, y de ahí ya no hay recorrido que
comprar. No se elige el número que hace pasar una celda: se elige el último que todavía compra algo.

## Lo que cuesta, medido

Banco completo de 1.200 runs (300 × semillas 1/1001/2001/3001), **con la tabla de valor anterior** para
que el único cambio sea éste:

| | ADR 0069 | `deathless_march` a `maxValue` 4 | |
|---|---|---|---|
| Tasa de victoria de la run | 19,42 (ET 1,26) | **19,50** (ET 1,29) | +0,08 |
| Suelo sin build | 10,58 | **10,58** | = |
| Buena, actos 2/3 | 62,03 / 46,45 | **62,07 / 46,38** | |
| Mediocre, actos 2/3 | 51,28 / 39,64 | **51,26 / 39,69** | |
| Hueco del acto 2 | 10,75 | **10,81** | |
| Separación `S` | 1,1682 | **1,1762** | |

**No cuesta nada**, y era de esperar: la ADR 0069 ya midió que el perk aparece en 42 de 1.200 runs y que a
×32 no compraba nada. Lo que cambia es que ahora el catálogo **no puede** volver a escribirlo.

## Qué falsificaría esta decisión

- **Que algún perk raro o legendario de ámbito individual necesite pasar de ×32.** Los escalones ×64 y
  ×128 están derivados, no medidos: el día que un perk los use habrá que medir si el canal aguanta.
- **Que el ámbito de equipo valga menos de 4×.** La ADR 0060 midió 2-4× y se ha tomado el extremo
  prudente. Si se midiera 2×, el techo de equipo se quedaría corto en un escalón.
- **Que el techo tenga que mirar el canal.** No lo mira, y la patología es del canal: ×32 sobre el robo
  (base 37,4%) deja el canal en el 95% y ×32 sobre la intercepción (base 2,5%) en el 45%. Un techo por
  canal sería más exacto y obligaría a que el cargador de perks conociera `data/sim/tuning.json`, que hoy
  no conoce. Queda anotado y no se hace.
