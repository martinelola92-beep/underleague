# 0061. El peso de los atributos frente a la build no es un grado de libertad

**Fecha:** 2026-09-06
**Estado:** Aceptada. **Falsifica AL-B** (`fase2-diseno.md` §29). No mueve ningún número de balance:
el paquete entrega medición, no calibración
**Corrige:** la premisa de **AL-B** —"bajar el peso de los atributos sirve a tres objetivos a la vez"—
y cierra la conversación de la **P3 de la ADR 0050**, que la ADR 0057 suspendió y la 0058 reabrió
**Requisitos:** RT-055, RT-056, RT-057, RF-032
**Relacionada con:** ADR 0020 (velocidad), ADR 0025, ADR 0027, ADR 0033, ADR 0050 (P3), ADR 0054,
ADR 0056 (objetivos), ADR 0057, ADR 0058, ADR 0059, ADR 0060

## De dónde venía el encargo

La ADR 0058 midió que **dar build al rival vale dos puntos de suelo y los otros diez son nivel y
atributos**, y la 0059 lo dejó anotado como la conversación pendiente. La ADR 0060 agotó la capa de build
del rival —de 7/9 perks a 1/2 la build buena sube 5,7 puntos y ahí se acaba el combustible— y dejó AL-B
con un enunciado optimista: *la P3 al revés, bajando el peso de los atributos, sube la build buena, hunde
la que no tiene build y aleja a la mala de completar la run.*

**Los tres efectos existen. No ocurren a la vez.** Es lo que este paquete mide.

## Lo primero: el tipo de cambio entre un atributo y un perk

Sobre 1.200 runs por doctrina (300 × semillas 1/1001/2001/3001) y sondas de 600 (150 × cuatro semillas),
en partidos **ordinarios** del acto 2:

| unidad | efecto medido en el acto 2 | cómo se midió |
|---|---|---|
| **+1 punto en cada atributo de toda la plantilla** | **+1,33** puntos de tasa de victoria | `generation.budgetByRarity` +25 (= +5 por atributo), 600 runs |
| **+1 perk bien puesto en el once** | **+0,93** puntos | `economy.rewardPerkWeight` 0: el once pasa de 9,97 a 2,82 perks y la build buena de 57,97 a 51,33, 1.200 runs |
| **un nivel de toda la plantilla** (+2 en cuatro atributos) | **+2,1** puntos | `progression.attributesPerLevel` 2 → 3, 600 runs |

**Un punto de atributo sobre la plantilla vale 1,4 perks.** Y en la escala de la run entera, donde el once
termina con 9,97 perks y nivel medio 5,11:

| capa | lo que aporta a la tasa de victoria del acto 2 |
|---|---|
| **La build entera** (9,97 perks bien puestos) | **+6,6** |
| **El nivel entero** (los 8,2 puntos de atributo que dan 4,1 niveles) | **+13,6** |

Medido quitando cada capa: sin recompensas de perk la build buena cae de 57,97 a 51,33; con
`attributesPerLevel = 0` cae a 42,21 (de los cuales 1,9 son los dos perks menos que compra al empobrecerse,
descontados ya de la cifra). **La capa de nivel vale dos veces la capa de build.** Ese es el número que
AL-B pedía, y confirma el diagnóstico de las ADR 0057 y 0058 con una cifra en vez de una sospecha.

## Y lo segundo: ninguna forma de moverlo separa a los dos perfiles

Se probaron las tres formas que el encargo enumeraba —curva de nivel, peso de cada atributo en las fórmulas
del motor, presupuesto de generación— más el reparto por canal, todas sobre 600 runs por condición:

| palanca | buena, acto 2 | mediocre, acto 2 | **hueco** | suelo | run, buena |
|---|---|---|---|---|---|
| `attributesPerLevel` 0 | 42,21 | 36,02 | 6,19 | 3,17 | 5,17 |
| `attributesPerLevel` 1 | 50,64 | 40,91 | **9,74** | 6,50 | 8,67 |
| **base (2)** | **57,76** | **47,71** | **10,05** | 14,00 | 16,83 |
| `attributesPerLevel` 3 | 62,12 | 52,39 | **9,72** | 18,00 | 20,67 |
| presupuesto de generación +5 por atributo | 64,43 | 54,02 | **10,41** | 14,50 | 14,50 |
| factores de atributo del motor ×0,6 | 55,47 | 47,60 | 7,87 | 10,83 | 10,17 |
| factores de atributo del motor ×1,4 | 57,44 | 47,52 | **9,92** | 16,83 | 17,50 |

(La columna "suelo" de esta tabla es `runWinRate_noMarket` **sin** anular las recompensas de perk: es el
indicador barato, no el suelo sin build de la ADR 0056. Se compara consigo mismo —la fila base vale 14,00—,
no con el 10,66 de la medición completa, que sí lleva `rewardPerkWeight = 0`.)

**El hueco entre la build buena y la mediocre se queda entre 9,7 y 10,4 en todo el recorrido útil de la
palanca**, mientras la tasa de victoria de las dos se mueve veinte puntos. La única celda que rompe el
patrón es `attributesPerLevel = 0`, donde el hueco se hunde a 6,19 porque la build buena cae por debajo del
50% y las dos entran juntas en la parte plana de la sigmoide — que es exactamente la geometría de la
ADR 0059.

Dicho como enunciado, y es lo que esta ADR añade a las tres anteriores:

> **El peso de los atributos es el mismo número que la fuerza del rival.** Los atributos entran en el motor
> como una **diferencia** —técnica del pasador menos 50, presión del que entra menos técnica del conductor,
> fuerza del que bloquea menos la del bloqueado—, así que escalar ese peso, mover la curva de nivel o mover
> el presupuesto de generación son la misma operación sobre el mismo término. Mueven a las dos builds en el
> mismo sentido y por la misma cantidad, igual que hacía la capa de build del rival en la ADR 0058. **No es
> una palanca de separación: es un mando de dificultad.**

Y de ahí la incompatibilidad, con signo nuevo: `attributesPerLevel = 3` **alcanza dos objetivos de la
ADR 0056** —build buena al 62,12 (meta 60) y tasa de victoria de la run al 20,67 (meta 20-30, en banda por
primera vez)— y a la vez **rompe tres**: la mediocre sube a 52,39 (meta 42-45), el suelo a 18,00 (meta <10) y
la build mala completa la run el 15,00% (meta <2). `attributesPerLevel = 1` hace lo contrario: suelo 6,50
(**cumple** por primera vez) y build buena en 50,64.

## El experimento que lo cierra: compensar el rival no devuelve nada

Si el peso de los atributos fuera un grado de libertad de verdad, se podría bajar la curva de nivel y
compensar la dificultad con el rival, y quedaría una partida donde el mismo resultado medio se decide más
por la build. Se hizo: `attributesPerLevel = 1` con los atributos de los rivales ordinarios bajados 1/5/4
puntos por acto, la compensación calculada con el tipo de cambio de arriba.

| | base | `apl` 1 + rival compensado |
|---|---|---|
| buena, actos 1/2/3 | 74,24 / 57,76 / 45,90 | 73,79 / **58,12** / 44,49 |
| mediocre, acto 2 | 47,71 | 47,30 |
| **hueco acto 2** | **10,05** | **10,82** (ET 1,2) |
| tasa de victoria de la run, buena | 16,83 | **10,33** |
| build mala completa la run | 11,50 | 5,17 |
| **suelo sin build** | 10,66 | **5,17** |
| muertes por run | 1,43 | **1,19** |
| derrotas del acto 1 | 32,62 | **37,39** |

(Sondas de 600 runs por condición. La fila del **suelo sin build** es la única que se mide con
`rewardPerkWeight = 0` en las dos columnas; la sonda de 600 del caso base da 10,67, que coincide con el
10,66 de la medición completa de 1.200.)

**La compensación funciona exactamente donde se aplicó y en ningún otro sitio.** Los partidos ordinarios
vuelven a su sitio y el hueco no se mueve (10,82 contra 10,05, dentro del ruido). Lo que cambia es la run,
y por una razón que no es "peso de los atributos": el **jefe** no se compensó, y el jefe es el único rival
que no sale de `data/rivals/`. Con la curva de nivel a la mitad el once llega a las tres puertas cuatro
puntos de atributo más flojo, y ahí se va todo: la run de la build buena cae a 10,33 y el suelo a 5,17.

O sea: **la curva de nivel no es "cuánto pesan los atributos", es la moneda con la que se llega al jefe.**
Bajarla hunde el suelo (objetivo cumplido) rompiendo a la vez `deathsPerRun` (1,19, banda 1,5-3), las
derrotas del acto 1 (37,39 sobre un techo de 29,74) y la tasa de victoria de la run (10,33 sobre una banda
de 20-30). No es una calibración que se pueda apretar un poco menos: los tres se rompen antes de que el
suelo llegue al 10%.

## Dos hallazgos laterales que hay que registrar

**1. Los perks de la build mediocre son valor negativo, y ahí está el 85% del hueco.** Al poner
`rewardPerkWeight = 0` la doctrina **gastadora mejora**: 47,94 → **49,79** en el acto 2 (1.200 runs, ET
0,74). El hueco entre perfiles pasa de 10,03 a **1,54**. Descompuesto: los 9,97 perks de la build buena
valen **+6,6** puntos y los de la mediocre valen **−1,8**. Es la confirmación directa de la ADR 0060 —el
castigo del perk mal puesto es lo que separa— y a la vez el aviso de que **hoy no tener build es mejor que
tener una mala**, que es justo lo contrario de lo que los objetivos 3 y 4 de la ADR 0056 quieren
(`suelo < 10%` y `mala < 2%`).

**2. La velocidad no está muerta, y la cifra de la ADR 0020 lleva dos motores de retraso.** El encargo
citaba +0,4 puntos por la velocidad y −1,2 en orcos. Eso es de antes de los cuerpos con volumen; **D-25 ya
lo había corregido** (+6,6 con `FindSpace`) y esta medición lo confirma por otra vía: partiendo el peso de
los atributos canal a canal y midiendo cada mitad por separado, **la velocidad es el canal más caro de
todos**.

| canal cuyo peso de atributo se parte por la mitad | buena, acto 2 | coste |
|---|---|---|
| ninguno (base) | 57,76 | — |
| `movement` (velocidad) | 54,76 | **−3,00** |
| `shot` (técnica y fuerza del rematador) | 55,61 | −2,15 |
| `pass` (técnica del pasador y del interceptor) | 56,58 | −1,18 |
| `dribble` (técnica contra cobertura) | 57,17 | −0,59 |
| `save` (atributo del portero contra técnica del rematador) | 57,01 | −0,75 |
| `tackle` (presión contra técnica) | 57,26 | −0,50 |

Ninguna mitad de canal mueve el hueco fuera del ruido, así que **el reparto tampoco es una palanca de
separación**; pero la tabla sí desmiente que quede un atributo sin valor al que se le pueda quitar peso
gratis. Bajar el peso "en plano" no mataría un atributo muerto: no hay ninguno.

## Decisión

**1. No se toca ningún número.** Ni `progression.attributesPerLevel`, ni `generation.budgetPerLevel`, ni
`generation.budgetByRarity`, ni los factores de atributo de `data/sim/tuning.json`, ni los atributos de
`data/rivals/`. Los seis objetivos de la ADR 0056 quedan **exactamente** donde los dejó la ADR 0060.

**2. La P3 de la ADR 0050 se retira, en los dos sentidos.** La ADR 0057 la suspendió por subir el peso de
los atributos y la 0058 la reabrió por el reparto del suelo. Queda medido que ni subirla ni bajarla separa
perfiles, así que deja de ser una decisión pendiente: es un mando de dificultad como cualquier otro y se
usará, si se usa, para eso y con ese nombre.

**3. Queda escrito qué palancas están agotadas y por qué.** Cuatro paquetes y cinco palancas: oro y precios
(ADR 0055), lo que vale un perk (P1), techo por rareza y capa de build del rival (ADR 0058), pago por
coherencia (ADR 0059, falsificado antes de escribirse), y peso de los atributos (esta). **La única que ha
abierto el hueco es la asimetría premio/castigo de la ADR 0060, y su techo está medido** (AL-D). Todo lo
demás mueve a los dos perfiles a la vez porque todo lo demás entra en el motor como el mismo término.

**4. Los objetivos 1 y 2 de la ADR 0056 hay que revisarlos o buscar la palanca en AL-A.** Con la build
buena en 57,97 y la mediocre en 47,94, alcanzar 60 y 42-45 pide un hueco de **16,5 puntos**, un 65% más que
el que hay. Ninguna palanca de fuerza lo da: hace falta que el catálogo tenga más recorrido entre construir
bien y construir regular, y **AL-A dice dónde no lo tiene** —la mitad del catálogo vive en canales de base
alta donde multiplicar la cuota no compra tasa de victoria—. Esa es la decisión de fondo que queda.

## Qué falsificaría esta decisión

- **Que aparezca una palanca de atributos que sí discrimine.** Tendría que actuar sobre algo que la build
  buena y la mediocre **no** compartan; hoy comparten nivel, presupuesto de generación, mezcla de rarezas
  (ADR 0059) e incluso el número de perks. La única diferencia medida entre las dos es **dónde ponen los
  perks**, y eso no es un atributo.
- **Que el hueco se mueva con el peso de los atributos en una medición mayor.** Las siete condiciones de la
  tabla están a 600 runs por doctrina, con error típico de 0,6 a 1,8 puntos en el hueco. Una diferencia
  real de más de dos puntos se habría visto; una de medio punto, no. Si alguien encuentra medio punto ahí,
  no cambia la conclusión: haría falta un 65% más de hueco.
- **Que `attributesPerLevel = 3` deje de romper el suelo.** Es la única celda que alcanza dos objetivos a
  la vez (build buena 62,12 y run 20,67). Si el suelo bajara por otra vía —AL-A, o un rediseño de lo que
  cuesta perder un partido ordinario (RF-002c)—, esa celda pasaría a ser una opción real y esta ADR habría
  que releerla como "hoy no, pero por el suelo".
