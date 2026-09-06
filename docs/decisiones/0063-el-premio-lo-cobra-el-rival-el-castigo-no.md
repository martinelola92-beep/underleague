# 0063. El premio del catálogo lo cobra también el rival; el castigo no

**Fecha:** 2026-09-06
**Estado:** Aceptada. **Falsifica AM-A por la vía de la palanca**, no del diagnóstico. No mueve ningún
número de balance: el paquete entrega medición, no calibración
**Corrige:** la premisa implícita de la ADR 0060 —que el premio y el castigo son la misma palanca leída
al revés— y la hipótesis de trabajo de este encargo (*los perks desbloquean comportamiento*)
**Requisitos:** RT-055, RT-056, RT-057, RT-092, RT-096, RT-098, RF-032
**Relacionada con:** ADR 0022, ADR 0030, ADR 0033, ADR 0038, ADR 0048, ADR 0050 (P1), ADR 0056
(objetivos), ADR 0058, ADR 0059, ADR 0060, ADR 0061

## De dónde venía el encargo

**AM-A**: los perks de la build mediocre valen **−1,8** puntos, así que hoy *no tener build es mejor que
tener una mala*. El encargo pedía dos mediciones antes de elegir nada —de dónde sale ese −1,8 y por qué
cuatro de las trece acciones no ganan nunca la tabla de utilidad— y daba una hipótesis a falsificar: que
los perks **desbloqueen comportamiento** en vez de mover números.

Las dos mediciones están hechas. Las dos hipótesis —la del encargo y la que salió de la medición— quedan
**falsificadas**, y las dos por la misma razón de fondo, que es lo que esta ADR añade.

## 1. El −1,8 es castigo entero, y el catálogo no tiene perks que resten estando bien puestos

Banco: 1.200 runs por doctrina (300 × semillas 1/1001/2001/3001), el mismo de §28.8 y §29.1; reproduce
la ADR 0060 al decimal. La segunda condición es el **catálogo sin `elseEffects`** —se borran los 17
bloques de castigo de `data/perks/`— y es un instrumento limpio para la doctrina gastadora, porque su
`Rank` de mercado es `-precio` y su `BestCarrier` es el elegible de menor id: **quitar los castigos no
cambia qué compra ni dónde lo pone**, solo lo que el perk hace en el campo.

| valor de los perks de recompensa, acto 2 | con castigo | sin castigo | castigo |
|---|---|---|---|
| Build **buena** (contextual) | **+6,64** (ET 1,06) | +6,14 (ET 0,63) | −0,49 |
| Build **mediocre** (gastadora) | **−1,85** (ET 1,38) | **+3,55** (ET 0,58) | **−5,40** |
| **Hueco** del acto 2 | **10,03** (ET 0,50) | **4,01** (ET 0,94) | 6,02 |

**El −1,8 es castigo entero.** Los perks de la build mediocre, cuando no pueden castigar, valen **+3,55**;
el castigo del perk mal puesto le cuesta **−5,40**. Y el castigo no toca a quien construye bien: la build
buena mide +6,64 con castigos y +6,14 sin ellos, que es la misma cifra dentro del error.

Y a la vez sale la otra mitad, que es la que ordena todo lo demás: **el 60% del hueco es castigo**
(10,03 → 4,01 al quitarlo). Sin castigo, para sostener un hueco de 9,8 el premio tendría que valer
**3,2 veces más** (`hueco = 1,54 + 2,59k ≥ 9,8 → k ≥ 3,19`), lo que dejaría a la build buena en el **71%**
del acto 2. El castigo no es un adorno del modelo: es lo que hace que el hueco quepa en una tasa de
victoria creíble.

**Perk a perk.** Se remide `--perk-values` (48 plantillas × 32 partidos, semillas 5 y 11 sumadas: 3.072
partidos por fila) sobre el catálogo sin `elseEffects` y se compara con la tabla vigente. El control es
perfecto: los **35 perks que no tienen castigo reproducen la tabla base exactamente** —diferencia media
0,0 y desviación 0,3 unidades, contra una desviación de fila de 23—, porque con las mismas semillas y los
mismos datos los partidos son los mismos. Eso hace que las diferencias de los 16 perks que sí castigan no
sean estimaciones sino **medidas exactas del castigo**:

| perk | valor base | sin poder castigar | castigo |
|---|---|---|---|
| `spearpoint` | −122 | **+57** | **179** (−8,9 puntos) |
| `bulwark_stance` | −46 | +16 | 62 |
| `own_third_anchor` | −79 | −29 | 50 |
| `gentle_giant` | +40 | +70 | 30 |
| `last_ditch` · `fine_touch` · `pack_mentality` | 7 / 18 / 9 | 17 / 27 / 15 | 10 / 9 / 6 |
| los otros nueve | | | ≤ 2 |

(Unidades de la tabla: **20 por punto** de tasa de victoria. Cuatro perks concentran el **83%** del
castigo del catálogo, y `spearpoint` él solo el 51%.)

**Y la respuesta a "¿hay perks que valen negativo incluso bien puestos?" es: no de forma apreciable.**
Dieciséis perks miden negativo sin poder castigar, pero entre **−2,2 y −0,3 puntos** contra una
desviación de fila de **1,15**; solo cuatro llegan a dos desviaciones y los cuatro tienen explicación que
no es "el perk resta":

- `cold_focus`, `box_predator`, `long_range_menace`, `poacher_instinct`: `actor shotOnTarget ×2` en una
  jugada. Es **AL-A** en su forma más pura —canal de base alta, sin recorrido—; el signo es ruido
  alrededor de cero.
- `clean_sheet_legacy`: perk de **acumulación**, con el contador a cero. El instrumento mide **un
  partido**, así que subestima por construcción a los 15 perks que acumulan entre partidos (RF-070).
  Queda anotado como límite del instrumento, no como defecto del perk.
- `iron_studs`: perk **letal**. Matar a un rival no es una palanca de tasa de victoria y el instrumento
  no la mide.

Dicho al revés, que es como hay que decirlo: **el problema del catálogo no es que haya perks que resten;
es que hay perks que no suman.** Medidos sin su castigo, **22 de los 51 valen menos de un punto** de tasa
de victoria. Eso es AL-A cuantificado perk a perk.

## 2. Las cuatro acciones muertas lo están a propósito, y despertarlas rompe RT-056

Se añade a `/Balance` el modo **`--utility-census N`**: repite el mismo partido de referencia una vez por
(jugador, tick) muestreado con `SimConfig.DumpUtility` y acumula las tablas. No toca `/Sim`: es el
volcado de RT-098, agregado. Sobre 6.433 decisiones del partido `human_50` contra `human_50`:

| acción | descartada % | **elegida %** | score medio | mejor score | margen medio al ganador |
|---|---|---|---|---|---|
| `FindSpace` | 59,4 | **39,50** | 975 | 1.497 | 3 |
| `CoverSpace` | 9,9 | **36,59** | 395 | 955 | 335 |
| `Retreat` | 0,0 | **18,79** | 290 | 843 | 399 |
| `ChaseBall` | 14,5 | 2,58 | −76 | 1.255 | 807 |
| `MarkOpponent` | 14,5 | **0,75** | 98 | 600 | 633 |
| `Block` | **74,9** | **0,06** | 8 | 682 | 714 |
| `PressCarrier` | **81,5** | **0,05** | 51 | 485 | 473 |
| `OfferSupport` | 59,4 | **0,00** | 133 | 396 | 845 |

Las tres respuestas que el encargo pedía distinguir, y son distintas entre sí:

1. **No es un peso mal puesto en `/data`.** Ninguna de las cuatro pierde por poco: pierden por 473 a 845
   puntos, y el **mejor score que han sacado nunca** está por debajo del score **medio** del ganador.
2. **`Block` y `PressCarrier` están descartadas el 75% y el 82% de las veces**, y no por peso: no hay
   rival al alcance de la carga dentro de la jugada activa (RF-057, `blockReachMaxCells` 1,2) o no hay
   poseedor rival. Con peso infinito seguirían sin poder elegirse en tres de cada cuatro decisiones.
3. **`OfferSupport` está genuinamente dominada.** Es legal exactamente cuando `FindSpace` lo es (equipo
   con balón) y compite contra ella con 80-160 de peso base contra 200-460 y con 150 de multiplicador
   táctico contra 210. Es lo que `fase1b-diseno.md` §16 ya decía —`FindSpace` la sustituye de hecho y
   RT-092 la mantiene viva— visto ahora en la tabla.

**Y despertarlas cuesta la puerta de la sensación de fútbol.** Se sube el peso base de cada una lo que el
censo dice que hace falta para que gane, una por una, y se mide el lote de referencia (600 partidos,
semilla 1):

| variante | entradas/partido | lesiones/partido | cadena de pases | `betterTeamWinRate` |
|---|---|---|---|---|
| base | 9,78 | 0,71 | 2,25 | 79,00 |
| `MarkOpponent` ×3 | **0,55** OUT | 0,25 OUT | 1,63 OUT | 67,00 OUT |
| `OfferSupport` ×5 | 11,94 | 0,91 OUT | 1,79 OUT | 63,00 OUT |
| `PressCarrier` ×3 | **2,95** OUT | 0,26 OUT | 2,25 | 80,00 |
| `Block` ×9 | 9,45 | **1,31** OUT | 2,28 | 84,00 |

Las cuatro rompen RT-056, y tres lo rompen **por el mismo sitio**: la entrada y la lesión, que es donde
vive la ADR 0048. Reproduce lo que `fase1b-diseno.md` §21 ya había medido (presionar hundía las entradas
de 13,0 a 1,0 y las lesiones de 0,82 a 0,05) y §19 (el bloqueo escala tan rápido que a peso 300 daba 22
faltas y 37 incomparecencias de 40 partidos).

**La hipótesis del encargo queda falsificada**: las cuatro acciones no están dormidas por descuido, están
amortiguadas a propósito y el precio de despertarlas está medido en RT-056. Un perk que las desbloquee
mueve el mismo canal, solo que a un séptimo de escala; y como el encargo mismo avisaba, si se dispararan
habría que revalidar los cinco paquetes anteriores. No se fuerza.

## 3. La palanca que la medición sí sostenía, y por qué también se cae

De §1 sale una palanca con la forma correcta: si el −1,8 es castigo entero y el premio de la build
mediocre ya es positivo, basta con que **el premio valga más**, porque el premio lo cobra sobre todo
quien coloca bien (+6,14 la buena contra +3,55 la mediocre, 1,7 a 1). La aritmética dice cuánto:

```
hueco = 1,54 + V_buena − V_mediocre          (1,54 = la diferencia entre las dos doctrinas SIN build)
comprar > no comprar   →   V_mediocre > 0
hueco ≥ 9,8            →   V_buena ≥ 8,26     (hoy 6,64: hace falta un +24%)
```

Y la ADR 0060 decía dónde estaba ese recorrido, en su propia tabla: **el mismo efecto vale de dos a
cuatro veces más sobre el equipo que sobre el portador** (`pass` ×2: +0,54 en el portador, +2,02 en el
equipo; `tackle` ×2: +1,81 y +4,73). El punto 2 de la ADR 0060 movió los **castigos** de `owner` a
`team`; su espejo es mover los **premios**, y además arregla una asimetría que hoy se lee en la propia
descripción generada: hay ocho perks cuyo castigo alcanza al equipo entero y cuyo premio alcanza a un
solo jugador (`fine_touch`, `flank_specialist`, `own_third_anchor`, `bulwark_stance`, `pivot_duo`,
`forward_line`, `spearpoint`, `covering_shadow`).

Se implementó en `/data` y se midió (600 runs, 2 semillas, contra el mismo protocolo sobre la base, con
`perk-values.json` regenerado para el catálogo nuevo):

| | base | premio al equipo |
|---|---|---|
| buena, acto 2 | 57,53 | **56,28** |
| mediocre, acto 2 | 47,01 | 46,50 |
| hueco | 10,53 | 9,78 |
| acto 1 | 75,84 | **72,39** |
| **derrotas del acto 1** | **28,38** | **43,98** |
| **muertes por run** | **1,46** | **1,04** |
| tasa de victoria de la run | 14,83 | 9,17 |

**La build buena empeora.** Y la causa no es de calibración:

> **El catálogo es compartido, y la oposición lo lleva bien puesto.** Cinco de los ocho perks están en
> `data/rivals/`, y los tres jefes los llevan: `grimhold_guns` (acto 1) tiene **14 slots de perk** —dos
> `own_third_anchor`, `bulwark_stance`, dos `pivot_duo`, `forward_line`—, `the_hunt` otros 14 y
> `eternal_crown` **27**. El jugador llega a esas tres puertas con **3,2 / 6,3 / 8,6** perks
> (`groups.json`, `actDensity`). Subir lo que da un perk **bien puesto** le da al jefe del acto 1 catorce
> perks mejores y al jugador tres.

Se aisló en dos pasos. Quitando los ocho perks a los quince rivales, el acto 2 se recupera (56,28 →
56,96) pero el acto 1 **no** (72,39 → 73,32, derrotas 40,55): lo que queda es el jefe. Quitándoselos
también a los tres jefes —19 slots menos, que es un recorte bruto y no una recalibración— el mismo cambio
da buena 58,48 · mediocre 49,17 · hueco 9,31 · **run 23,33** · `deathsPerRun` **1,68** · derrotas del
acto 1 22,62. Es una **cota superior**, no un resultado; lo que demuestra es que el margen existe **si se
recalibra a la oposición a la vez**.

## La afirmación que cierra las cinco palancas anteriores

La ADR 0060 §28.10 ya había medido la mitad: *"las otras nueve celdas quedan idénticas, lo que confirma
que los tres jefes tienen sus perks bien puestos: ninguno paga el castigo nuevo"*. La otra mitad es la que
faltaba y es la que este paquete añade:

> **El castigo es el único canal del catálogo que la oposición no comparte.** Ni los quince rivales ni los
> tres jefes colocan mal un perk, así que ninguno paga nunca el castigo; y todos cobran el premio, y lo
> cobran con dos y tres veces más perks que el jugador en cada puerta. Por eso el castigo es la única
> palanca que ha abierto el hueco, y por eso **su espejo no es una palanca sino un mando de dificultad**,
> exactamente como el peso de los atributos de la ADR 0061.

Con eso, las **seis** palancas probadas en cinco paquetes tienen la misma explicación en una línea: oro y
precios (ADR 0055), lo que vale un perk (P1), techo por rareza y capa de build del rival (ADR 0058), pago
por coherencia (ADR 0059), peso de los atributos (ADR 0061) y ahora el ámbito del premio — **todas mueven
al jugador y al rival a la vez**. La de la ADR 0060 no, y es la única que funcionó.

## Decisión

**1. No se toca ningún número.** Ni `data/perks/`, ni `data/rivals/`, ni `data/bosses/`, ni
`data/ai/weights.json`, ni `data/economy/`. Los seis objetivos de la ADR 0056 quedan **exactamente** donde
los dejó la ADR 0060 y las seis puertas siguen verdes (598/598 en Release).

**2. La hipótesis "los perks desbloquean comportamiento" se retira**, con el precio medido: las cuatro
acciones candidatas rompen RT-056 al despertarlas y tres de las cuatro por el canal de la entrada y la
lesión, que es donde vive la ADR 0048. Si alguna vez se retoma, no es un paquete de perks: es un paquete
de motor con revalidación completa de los cinco anteriores.

**3. Queda escrito que el premio no es una palanca de separación mientras la oposición comparta el
catálogo.** Cualquier subida del premio exige recalibrar a la vez `data/rivals/` y los tres jefes, y eso
es un paquete propio: el guardarraíl lo permite —"si un jefe se sale, recalibras el jefe, nunca la
tabla"— pero son 24 slots de perk de jefe y quince plantillas de rival, no un ajuste.

**4. `--utility-census` se queda en `/Balance`.** Es el volcado de RT-098 agregado y es lo que faltaba
para poder responder "¿por qué esta acción no gana nunca?" con las tres respuestas separadas —descartada,
por debajo, o no evaluada— en vez de con una intuición.

## Lo que esto le devuelve al revisor: dos objetivos incompatibles, con el número que lo prueba

El encargo pedía que **comprar sea siempre mejor que no comprar**. Eso es `V_mediocre > 0`, y `V_mediocre`
se mide contra el mismo equipo sin build: la doctrina gastadora sin recompensas de perk gana el **49,79%**
(ET 0,74) de los partidos ordinarios del acto 2.

> **"Comprar siempre mejor que no comprar" fija a la build mediocre en 49,8 o por encima. El objetivo 2 de
> la ADR 0056 la quiere en 42-45. Los dos no pueden ser verdad a la vez**, porque "mediocre al 42-45%"
> *es*, literalmente, la frase "construir mal es peor que no construir".

No es una calibración que falte: es una elección de diseño que hay que hacer. Las tres salidas, con lo que
cuesta cada una:

- **Aceptar que la mediocre viva en 49-50** y revisar el objetivo 2 de la ADR 0056. Es la única que no
  necesita ninguna palanca nueva, pero exige subir el premio un 24% para que el hueco aguante, y eso pide
  el paquete de recalibración del punto 3.
- **Mantener el objetivo 2** y aceptar que comprar mal salga a deber, que es lo que hay hoy: el hueco de
  10,03 se conserva y AM-A se cierra como "así es el juego", con el coste que el encargo señala —que no
  comprar sea una estrategia—.
- **Bajar el suelo sin build**, que es lo único que movería a las dos a la vez en el sentido bueno. La
  ADR 0061 ya midió que por la vía de los atributos rompe `deathsPerRun`, las derrotas del acto 1 y la
  tasa de victoria de la run antes de llegar; queda abierta la vía de **qué cuesta perder un partido
  ordinario** (RF-002c), que es la que la ADR 0060 dejó apuntada y nadie ha medido todavía.

## Qué falsificaría esta decisión

- **Que la oposición deje de llevar el catálogo del jugador.** Si `data/rivals/` y los tres jefes pasaran
  a construirse con perks propios —o con los mismos perks pero con su valor congelado—, el premio volvería
  a ser una palanca de separación y esta ADR habría que releerla como "hoy no, por cómo están hechos los
  jefes".
- **Que un perk de comportamiento no toque el canal de la entrada.** Las cuatro acciones medidas lo tocan;
  una acción **nueva** que no compita con `Tackle` ni con `FindSpace` no está descartada por esta
  medición, solo no existe.
- **Que el instrumento de `--perk-values` deje de medir un solo partido.** Los 15 perks de acumulación
  (RF-070) están sistemáticamente infravalorados en la tabla que decide el peso del pool y el orden de
  compra de la doctrina contextual. No se ha tocado (RT-057), pero es un sesgo conocido y con nombre.
