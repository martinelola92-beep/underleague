# 0064. Perder un partido ordinario cuesta lo mismo a las tres builds

**Fecha:** 2026-09-06
**Estado:** Aceptada. **Falsifica la tercera salida de la ADR 0063 por medición.** No mueve ningún número
de balance: el paquete entrega medición y un instrumento
**Caracteriza:** RF-002c, el único parámetro grande del recorrido que seguía sin medir
**Requisitos:** RT-054, RT-055, RT-056, RT-057, RF-002c, RF-114g
**Relacionada con:** ADR 0033, ADR 0037, ADR 0043, ADR 0055, ADR 0056 (objetivos), ADR 0057, ADR 0058,
ADR 0060, ADR 0061, ADR 0063

## De dónde venía el encargo

La ADR 0063 dejó tres salidas a la incompatibilidad entre "comprar siempre mejor que no comprar" y el
objetivo 2 de la ADR 0056. La tercera —**bajar el suelo sin build por la vía de qué cuesta perder un
partido ordinario** (RF-002c)— era la única que nadie había medido, y era estructuralmente distinta de las
seis palancas falsificadas en cinco paquetes: **no es un número dentro del partido, es una consecuencia
fuera de él**, así que la oposición no lo comparte.

La pregunta del encargo, literal: *¿el equipo sin build pierde bastantes partidos ordinarios como para que
subir el precio de perder lo hunda, sin hundir por igual a quien construye bien?*

**La respuesta medida es no, y por una razón que no estaba prevista.**

## 1. Las tres builds pierden el mismo número de partidos ordinarios

Banco: 1.200 runs por perfil (300 × semillas 1/1001/2001/3001), el mismo de §28.8, §29.1 y §30.1, que
**vuelve a reproducirse al decimal** (buena 57,97 / 44,43 · mediocre 47,94 / 40,67 · mala 12,00 · suelo
10,66 → 10,67 · hueco 10,03 · run 17,00 · muertes 1,46 · derrotas del acto 1 29,74).

Los partidos ordinarios se separan de los de jefe con exactitud, no por estimación: la derrota contra el
jefe **termina la run**, así que la única derrota de jefe de una run es la terminal y se identifica por
`cause = BossMatchLost`. Todo lo demás —liga y élite— es ordinario.

| perfil | ordinarios **jugados** | ordinarios **perdidos** | tasa de derrota ordinaria |
|---|---|---|---|
| Build **buena** (contextual) | 11,09 | **4,03** | 36,3% |
| Build **mediocre/mala** (gastadora) | 10,24 | **4,16** | 40,7% |
| **Sin build** (suelo, `rewardPerkWeight = 0` y sin mercados) | 10,37 | **4,03** | 38,8% |

**4,03 contra 4,16 contra 4,03.** El equipo sin build **no** pierde más partidos ordinarios que el que
construye bien. Pierde a mayor **ritmo** —38,8% de los que juega frente a 36,3%— pero juega menos, porque
su run se corta antes en la puerta del jefe. La truncadura compensa el ritmo casi exactamente.

Dicho como palanca: **un castigo de D por derrota ordinaria es un impuesto uniforme de ~4·D por run para
los tres perfiles.** Eso no es una palanca de separación: es la palanca de oro de la ADR 0055, que ya
estaba falsificada.

**Y de paso sale un desajuste que hay que dejar escrito**: `winRateAct{n}`, la métrica con la que se
publican los objetivos 1 y 2 desde la ADR 0056, **incluye el partido de jefe**, mientras que la tabla de
la propia ADR 0056 dice "partidos **ordinarios**". Build buena, acto 2: **57,97** con jefe y **60,37** sin
él; acto 3: 44,43 y 43,26. El hueco no cambia (10,03 con jefe, 9,95 sin él) y los siete paquetes se
comparan entre sí con la misma métrica, así que no se toca nada (RT-057), pero **medido como su ADR lo
describe, el objetivo 1 ya está alcanzado en el acto 2 y no en el 3**. Queda como AO-D: es una elección de
lectura del revisor, no una calibración.

## 2. Y perder ya cuesta un tercio de la economía de la run

El coste de hoy (RF-002c: no paga oro ni recompensa, aplica lesiones y experiencia con normalidad),
medido sobre las mismas 1.200 runs:

| perfil | oro no cobrado | oro ganado | **derrotas como % de la economía** | recompensas perdidas | perks que no se compran |
|---|---|---|---|---|---|
| Build buena | 44,1 | 86,22 | **33,8%** | 4,03 | 1,8 |
| Build mediocre/mala | 44,7 | 71,13 | **38,6%** | 4,16 | 1,9 |
| Sin build | 43,6 | 77,84 | **35,9%** | 4,03 | 1,8 |

(Oro no cobrado = derrotas ordinarias del acto × el oro base de ese acto, 9/11/13. "Perks que no se
compran" divide ese oro entre 24, el precio de un perk raro.)

**Perder ya es el segundo sumidero de la run**, sólo por detrás del mercado (63,4 de oro con la build
buena) y por delante de la clínica, la matrícula y los rerolls **juntos** (22,0). Y las tres builds pagan
la misma factura: 44 de oro y 4 recompensas. No hay un canal que encarecer; hay un canal que ya está
cobrado, y cobrado por igual.

## 3. Encarecerlo, medido en cinco magnitudes: no mueve nada de lo que tiene que mover

Se añade el instrumento (`economy.defeatGoldPenalty` fijo y `economy.defeatGoldPenaltyPercent` sobre el
oro en mano) y se mide con él en sondas de 600 runs y confirmación de 1.200 en las dos magnitudes vivas.
+12 de oro por derrota es **más de lo que paga ganar** (9/11/13); −50% del oro en mano es la mitad de la
bolsa en cada tropiezo.

| condición | buena a2 | mediocre a2 | hueco | run buena | **run mala** | **SUELO sin build** | derrotas a1 |
|---|---|---|---|---|---|---|---|
| **hoy** | 57,97 (0,71) | 47,94 (0,70) | 10,03 | 17,00 (1,28) | **12,00** (0,87) | **10,67** (0,56) | 29,74 |
| −50% del oro | 57,78 (0,42) | 47,34 (0,66) | 10,44 | 16,33 (1,43) | 8,58 (1,46) | **10,58** (0,96) | 31,30 |
| −75% del oro | 57,37 (0,63) | 47,67 (0,29) | 9,71 | 16,25 (1,35) | 10,58 (0,25) | **11,50** (0,73) | 30,16 |
| +3 / +6 / +12 de oro (600 runs) | 57,9 / 58,8 / 57,4 | 46,9 / 47,9 / 48,4 | — | — | — | **9,50 / 8,67 / 11,17** | — |

- **El suelo no se mueve**: 10,67 → 10,58 → 11,50, y en las sondas 9,50 / 8,67 / 11,17 **sin orden**: el
  castigo más duro da el suelo más alto. Es ruido alrededor de 10,7 con error típico 0,6-1,1.
- **La build buena tampoco**: 57,97 → 57,78 → 57,37 en el acto 2, y su tasa de victoria de la run **baja**
  (17,00 → 16,25), que es la dirección contraria a la meta de 20-30.
- **La build mala no tiene tendencia**: 12,00 → 8,58 → 10,58. El 8,58 de −50% no sobrevive a subir el
  castigo.
- **Y dos guardarraíles se mueven en contra**: las derrotas del acto 1 suben de 29,74 a 30,2-31,3 (techo
  29,74) y el hueco cae a **9,71** con −75%, por debajo del suelo de 9,8.

## 4. Por qué falla, que es lo que este paquete añade

El poder de compra que el castigo destruye, medido perk a perk de bolsillo (600 runs, +6 por derrota):

| perfil | oro gastado en mercado, hoy → con castigo | perks al final |
|---|---|---|
| Build buena | 63,05 → **49,09** (−22%) | 12,07 → 11,49 |
| Build mediocre | 71,95 → 61,88 (−14%) | 14,23 → 13,70 |
| **Sin build** (suelo) | 8,94 → **5,29** | 1,56 → **1,35** |

> **El castigo está denominado en la moneda que sólo tiene quien construye.** El perfil sin build termina
> la run con 22 de oro sin gastar y 33 quemados en rerollear recompensas que no quiere: tiene un colchón
> de ~55 de oro **inútil** que absorbe cualquier peaje antes de que le llegue a la build, y la build que
> le queda son 1,6 perks. La build buena termina con 11 de oro sobre 86 ganados: su restricción está
> apretada y el peaje le llega entera.

La palanca no es neutra, es **regresiva**: cobra donde aprieta y no cobra donde sobra. Y eso vale para
cualquier denominación del castigo que se pueda pagar —oro, clínica, matrícula, perder un perk, perder un
objeto—: **todas son cosas de las que el perfil sin build tiene menos.**

## 5. La identidad que cierra la pregunta: la run es el producto de tres puertas

Medido, no supuesto:

| perfil | puerta 1 | puerta 2 | puerta 3 | **producto** | tasa de victoria de la run |
|---|---|---|---|---|---|
| Build buena | 75,33 | 44,25 | 51,00 | **17,00** | **17,00** |
| Build mediocre/mala | 68,58 | 33,29 | 52,75 | **12,04** | **12,00** |
| Sin build | 70,75 | 35,18 | 44,29 | **11,02** | 10,67 |

> **La tasa de victoria de la run es, al decimal, el producto de las tres tasas de victoria contra los
> jefes. Los veinte partidos ordinarios no aparecen en el producto.**

(La pequeña diferencia del perfil sin build es la única otra vía de derrota, quedarse sin plantilla:
0,009 por run.) Es la ADR 0057 —*"el jefe filtra; el resto del recorrido, no"*— convertida en aritmética.
Los partidos ordinarios sólo pueden actuar por el canal **indirecto** —menos oro, peor build, peor
jefe— y ese canal es exactamente el que la ADR 0055 midió y falsificó.

## Decisión

**1. No se toca ningún número de balance.** Ni `data/perks/`, ni `data/rivals/`, ni `data/bosses/`, ni
`data/ai/weights.json`, ni `data/map/`, ni el resto de `data/economy/economy.json`. Los seis objetivos de
la ADR 0056 quedan **exactamente** donde los dejó la ADR 0060 y las seis puertas siguen verdes (598/598 en
Release).

**2. La tercera salida de la ADR 0063 queda falsificada por medición.** Encarecer la derrota ordinaria no
baja el suelo, no hunde a la build mala, baja la tasa de victoria de la build buena y sube las derrotas del
acto 1. No hay calibración que arregle eso: el defecto es de forma, no de magnitud.

**3. El instrumento se queda, en cero.** `defeatGoldPenalty` y `defeatGoldPenaltyPercent` entran en
`data/economy/economy.json` con valor **0** —el juego de hoy es idéntico— y en el esquema como opcionales,
con la medición citada en su `_doc`. Es lo mismo que la ADR 0063 hizo con `--utility-census`: RF-002c era
el último parámetro grande sin caracterizar y ahora se puede volver a medir con un comando. Se añade
también el volcado `runs-nomarket.csv` en `/Balance`, sin el cual el perfil sin build no se podía
desglosar por acto.

**4. Queda escrito el criterio que explica los siete paquetes.** Una palanca separa perfiles sólo si el
número que mueve cumple **las dos** condiciones:

> **(a)** la oposición no lo tiene, y **(b)** el perfil bueno del jugador no lo tiene.
>
> Las seis palancas de las ADR 0055-0063 fallan por **(a)**: oro, valor del perk, rareza, fuerza del
> rival, peso de los atributos y ámbito del premio los cobra también el rival. Esta séptima cumple (a)
> —el precio de perder es sólo del jugador— y falla por **(b)**: la build buena pierde 4,03 partidos
> ordinarios por run y la que no construye, 4,03.
>
> El **castigo del perk mal puesto** (ADR 0060) sigue siendo lo único medido que cumple las dos, y por eso
> sigue siendo el 60% del hueco.

## Dónde queda la asimetría, con el número al lado

De §5 sale la única palanca que la medición deja viva para los objetivos 4 (run buena 20-30%) y 5 (suelo
< 10%), y no es de recorrido sino **de puerta**:

```
run = P1 · P2 · P3
buena  0,7533 · 0,4425 · 0,5100 = 17,0%     meta 20-30%
suelo  0,7075 · 0,3518 · 0,4429 = 11,0%     meta < 10%
razón por puerta   1,065 · 1,258 · 1,151 = 1,54       hace falta 2,0 (1,26 por puerta)
```

**Cada puerta discrimina hoy 1,15 de media y hace falta que discrimine 1,26.** Y la puerta ya es el sitio
donde los perfiles más se separan: en el acto 2 el jefe separa **9,1 puntos** (44,25 contra 35,18) y el
partido ordinario **6,9** (60,37 contra 53,45). Cualquier presión que se mueva del jefe al partido
ordinario **reduce** la discriminación por unidad de presión; es lo contrario de lo que hace falta.

Las dos formas de subir ese 1,15, las dos permitidas por el guardarraíl ("si un jefe se sale, recalibras
el jefe, nunca la tabla"):

- **Empinar la pendiente de los tres jefes** entre `correct` y `good` de la tabla de la ADR 0033, que es
  la franja donde viven los dos perfiles de la run. Hoy la curva medida es 67,6 → 84,6 en el acto 1,
  39,6 → 66,5 en el 2 y 25,0 → 38,9 en el final: **el acto 3 es la puerta más plana y la que menos separa**
  (razón 1,151), y es donde la ADR 0033 permite tocar al jefe sin tocar la tabla.
- **Un cuarto evento filtro por run**, que añade un factor más al producto: con la misma razón de 1,15 por
  puerta, cuatro puertas dan 1,77 en vez de 1,54 y con 1,20 dan 2,07.

Las dos son paquetes de jefe, no de catálogo, y las dos tienen que respetar el hueco del acto 2 (≥ 9,8),
que **no depende del producto** porque se mide en partidos ordinarios.

## Qué falsificaría esta decisión

- **Que los partidos ordinarios dejen de ser opcionales para la truncadura.** La igualdad de 4,03 contra
  4,03 se sostiene porque la run mala se corta antes. Si el mapa obligara a jugar los **mismos** partidos
  ordinarios a todo el mundo antes de la puerta —o si el número de nodos por acto dejara de ser un
  recorrido con alternativas—, el equipo sin build jugaría los mismos 17 ordinarios que el bueno y
  perdería 7,8 frente a 7,0. Esa razón de **1,10** sigue siendo pequeña, pero deja de ser 1,00.
- **Que aparezca un castigo denominado en algo que el perfil sin build tenga de sobra.** Lo único medido
  que tiene de sobra es **oro sin gastar** (~55 por run frente a 11 de la build buena), y un castigo
  proporcional al oro **en mano** ya se midió aquí (−25/−50/−75%) sin efecto, porque el perfil sin build
  ni siquiera nota que se lo quiten.
- **Que la run deje de ser el producto de tres puertas.** Es una identidad de las reglas de hoy (RF-002,
  RF-002c): dos vías de derrota y una de ellas, quedarse sin plantilla, ocurre 0,009 veces por run. Si esa
  segunda vía subiera, el producto dejaría de ser exacto y los partidos ordinarios entrarían en la cuenta
  por sí mismos.
