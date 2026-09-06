# 0077. Un número que se mide a sí mismo, y por qué converge en una vuelta

**Fecha:** 2026-09-06
**Estado:** Aceptada e **implementada**. Toca **dos enteros de la política automática**
(`RunPolicyOptions.Act2GatePassPermille` 439 → **493**; `Act1GatePassPermille` se remide y **no se mueve**)
y añade dos palancas de medición a `/Balance` (`--act1-pass`, `--act2-pass`). **No toca ningún jefe,
ninguna banda, ningún dato del catálogo ni ningún número de economía**
**Cierra:** AU-D
**Requisitos:** RF-071, RT-023, RT-054, RT-056, **RT-057**
**Relacionada con:** **ADR 0072** (que puso las dos tasas en la política y escribió que remedirlas «es una
línea»), ADR 0074 (la recalibración de `the_hunt`, que es la que las dejó rancias), **ADR 0076** (el mismo
paquete; comparten causa)

## El defecto

El coste de oportunidad del slot (ADR 0072) descuenta las ofertas de los actos siguientes por la
probabilidad de llegar a ellos, y esa probabilidad la lleva la política escrita a mano:
`Act1GatePassPermille = 718` y `Act2GatePassPermille = 439`. Las dos se midieron sobre el banco de
**control**, antes de aplicar el listón y antes de que la ADR 0074 ablandara `the_hunt`. Hoy
`bossWinRateAct2` de la doctrina contextual vale **49,1-49,6**: **diez puntos** por encima del 43,9 que la
política usa.

Y actualizarlas no es una línea, porque **el número se mide a sí mismo**: cambiarlo mueve el listón, que
mueve lo que la run compra, que mueve la tasa de paso. Es un punto fijo, y antes de aplicarlo hay que
saber si converge.

## 1. La iteración, medida

Banco de 1.200 runs por punto (300 × semillas 1/1001/2001/3001), doctrina contextual, puertas exactas de
`BossWinsByAct / BossSamplesByAct`. `T(x)` es lo que mide el banco cuando la política **cree** `x`:

| vuelta | creencia `x` | medición `T(x)` | residuo |
|---|---|---|---|
| 0 | (718, **439**) | (717,5, **495,9**) | acto 2: **57** milésimas |
| 1 | (716, 496) | (717,5, 492,5) | acto 2: **3,5** milésimas |
| 2 | (717, 494) | (717,5, 491,3) | acto 2: **2,7** milésimas |
| 3 | (717, 493) | (717,5, 491,3) | acto 2: **1,7** milésimas |

**Una vuelta basta.** El residuo cae de 57 a 3,5 milésimas en la primera iteración y de ahí no baja más,
porque **ya está por debajo del ruido de la medida**: el error típico de `bossWinRateAct2` con 1.200 runs
es de **1,70 puntos, o sea 17 milésimas**, cinco veces el residuo. Las vueltas 2 y 3 no miden convergencia,
miden la misma cifra dos veces.

## 2. Que converja no es una observación: es una constante de Lipschitz medida

Cuatro puntos cerca del punto fijo no prueban nada sobre el resto del dominio. La contracción se mide
barriendo la creencia **de extremo a extremo**, con la misma muestra:

| creencia | listón del slot, acto 1 capa 0 | `T(x)` acto 1 | `T(x)` acto 2 |
|---|---|---|---|
| (**450**, **250**) | `N` = 37, cuantil 0,649, listón **25** | 719,2 | **480,9** |
| (718, 493) — el punto fijo | `N` = 50, cuantil 0,740, listón **38** | 717,5 | 491,3 |
| (**950**, **900**) | `N` = 68, cuantil 0,809, listón **48** | 717,5 | **477,4** |

La creencia recorre **500 y 650 milésimas** y el listón del acto 1 recorre de **25 a 48** —o sea la palanca
sí se mueve, y mucho—, pero la medición se mueve **1,7 y 3,5 milésimas**:

```
L = máx |T(x) − T(y)| / |x − y|  ≤  3,5 / 650  =  0,0054     (0,028 si se le atribuye a la señal
                                                              todo el ruido de las seis mediciones)
```

**`L ≤ 0,03`, treinta veces por debajo del error típico de una sola medición.** Por Banach el punto fijo
es **único y estable**, y desde un residuo inicial de 57 milésimas la primera iteración lo deja en
`0,03 × 57 = 1,7`, que es exactamente lo que el banco midió. **No hay ciclo, no hay bifurcación y no hace
falta amortiguar la iteración.**

## 3. Y por qué es tan plano, que es lo interesante

Dos razones, las dos medidas, y la segunda no es de este ADR:

1. **La autorreferencia pasa por un cuantil, que está cuantizado.** El listón es el valor del cuantil
   `1 − S/N` sobre **51** perks. Corregir el acto 2 de 439 a 493 mueve `N` en el acto 1 de **49 a 50
   ofertas** y el cuantil de 0,735 a 0,740: **el listón sale 38 en los dos casos**. En el acto 2, de 34 a
   35 ofertas y 0,794 a 0,800: **44 en los dos casos**. La corrección de diez puntos **no mueve el listón
   en ninguna de las dos situaciones que la ADR 0072 tabuló**; sólo lo mueve en capas intermedias, donde
   el redondeo cae del otro lado.
2. **Y aunque lo moviera, la run apenas lo notaría en las puertas.** Con el listón en 25 el banco mide
   puertas 71,92 · 48,09 y con el listón en 48 mide 71,75 · 47,74, mientras los perks del once en el jefe
   del acto 1 van de **4,80 a 3,74**. Ésa es la misma insensibilidad que mide la **ADR 0076**, y es la
   razón profunda de que este punto fijo sea tan cómodo: **el bucle está roto no por diseño, sino porque
   el eslabón que va del listón a la puerta es casi nulo.**

Lo segundo hay que decirlo con incomodidad: **este punto fijo converge por la misma razón por la que la
palanca no sirve para nada.**

## 4. Lo que cuesta, medido y emparejado

Banco de 1.200 runs por lado, mismas semillas, diferencia emparejada por bloque:

| | control (718/439) | **entregado (718/493)** | diferencia emparejada |
|---|---|---|---|
| Tasa de victoria de la run | 20,75 | **20,17** | **−0,58** (ET 0,60) — no significativa |
| La misma, con el banco de 7.272 runs (ADR 0076) | **19,68** ±0,47 | **19,57** ±0,47 | **−0,11** — tampoco |
| `contextualAdvantage` | 2,50 | **1,92** | −0,58 (0,60) |
| Build buena, actos 2/3 | 61,10 / 49,22 | **60,95 / 49,54** | −0,15 (0,11) / +0,32 (1,53) |
| Build mediocre, actos 2/3 · mala completa | 47,60 / 35,06 · 10,58 | **47,60 / 35,06 · 10,58** | **intactas** (la gastadora no usa el listón) |
| Hueco del acto 2 | 13,49 | **13,35** | |
| `masterDivergence` | 22,36 | **24,69** | **+2,33** (0,59) |
| Perks del once en las tres puertas | 4,34 / 9,48 / 11,26 | 4,31 / 9,27 / 11,21 | |
| `deathsPerRun` · `ordinaryDefeatRateAct1` | 1,45 · 25,23 | **1,45 · 25,26** | +0,00 / +0,03 |

**Nada se mueve por encima de su error salvo `masterDivergence`, que mejora.** Y el renglón añadido dice
por qué hay que desconfiar del propio banco: con la muestra que la ADR 0076 deriva, el coste de esta
corrección no es medio punto sino **una décima**, y las dos cifras de partida bajan un punto. El punto fijo
no cambia por ello —`T` se midió sobre la puerta, no sobre la run, y la puerta del acto 2 tiene el mismo
valor en los dos bancos dentro de su error—, pero el **coste** hay que leerlo en la columna nueva. Se aplica igualmente,
porque el motivo no es que gane nada: es que **la política estaba usando un número que sabemos falso**, y
dejarlo porque corregirlo cuesta media décima no significativa es calibrar contra el resultado, que es lo
que RT-057 prohíbe.

## Decisión

1. **`Act2GatePassPermille` pasa de 439 a 493**, la media ponderada de las cuatro mediciones del entorno
   del punto fijo (3.442 llegadas al jefe del acto 2, ET 0,85).
2. **`Act1GatePassPermille` no se mueve.** Se remide y vuelve a dar **71,75%** (861 de 1.200), los mismos
   718 milésimas que la ADR 0072 escribió. La rancia era una sola de las dos.
3. **El punto fijo converge en una vuelta, y está demostrado**, no observado: `L ≤ 0,03` medido sobre todo
   el dominio (450-950 y 250-900 milésimas), único y estable por Banach.
4. **Las dos tasas siguen viviendo en la política**, no en `/data`. La ADR 0072 las puso ahí para que
   remedirlas fuera una línea y lo es; y con `L ≤ 0,03` la autorreferencia es real pero **numéricamente
   irrelevante**, así que derivarlas en tiempo de ejecución —que es lo que AU-D preguntaba si convenía—
   añadiría un mecanismo para mover el listón cero unidades.
5. **`/Balance` gana `--act1-pass N` y `--act2-pass N`**, las palancas con las que se midió el punto fijo
   y con las que se remide sin recompilar.
6. **No se toca ningún jefe, ninguna banda, ningún dato del catálogo ni ningún número de economía.**

## Qué falsificaría esta decisión

- **Que el catálogo deje de tener 51 perks.** La planitud del punto fijo se apoya en que el listón es un
  cuantil de una distribución discreta y basta: con un catálogo mucho más grande el cuantil deja de ser
  una escalera gruesa y `L` sube. Hay que remedir `L`, no sólo las tasas.
- **Que el eslabón listón → puerta deje de ser nulo** (ADR 0076). Si algún día la puerta respondiera a lo
  que el once lleva, este punto fijo dejaría de ser cómodo y habría que amortiguar la iteración.
- **Que `TakeablePerkOffersPerLayerPermille` cambie.** El 2,30 medido entra multiplicando en `N` y mueve el
  cuantil mucho más que las tasas de paso; es el número que de verdad decide el listón.
- **Que las tasas se midan sobre otra doctrina.** Son las de la **contextual**, que es la única que usa el
  listón. Si otra doctrina lo usara, cada una necesitaría las suyas.
