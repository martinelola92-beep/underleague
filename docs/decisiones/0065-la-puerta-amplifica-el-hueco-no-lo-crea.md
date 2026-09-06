# 0065. La puerta amplifica el hueco; no lo crea

**Fecha:** 2026-09-06
**Estado:** Aceptada. **Falsifica las dos salidas que la ADR 0064 dejaba vivas** —empinar la pendiente de
los jefes dentro de la tabla y añadir un cuarto evento filtro—, las dos por medición. No mueve ningún
número de balance: el paquete entrega medición
**Caracteriza:** la última palanca que quedaba para los objetivos 4 y 5 de la ADR 0056
**Requisitos:** RT-054, RT-055, RT-056, RT-057, RF-001b, RF-001c, RF-012b, RF-014
**Relacionada con:** ADR 0027, ADR 0033 (la tabla), ADR 0048, ADR 0049, ADR 0055, ADR 0056 (objetivos),
ADR 0057, ADR 0058, ADR 0060, ADR 0061, ADR 0063, ADR 0064 (la identidad)

## De dónde venía el encargo

La ADR 0064 midió que **la tasa de victoria de la run es el producto de las tres puertas de jefe** y dejó
la única palanca viva para los objetivos 4 (run buena 20-30%) y 5 (suelo < 10%): que **cada puerta
discrimine más**. Hoy cada una discrimina 1,15 entre build buena y equipo sin build (producto 1,54) y
harían falta 1,26 (producto 2,0). Dos formas, las dos permitidas por el guardarraíl de la ADR 0033 —"si
una celda se sale, recalibras el jefe, nunca la tabla"—: **empinar la pendiente `correct`→`good`** de los
tres jefes, o **añadir un cuarto evento filtro**.

El encargo pedía medir cuánta discriminación se puede comprar por la primera vía y decir con un número si
llega. **No llega, y la medición además falsifica la segunda.** Y la razón por la que no llega es de forma,
no de magnitud, así que tampoco la arregla revisar la tabla.

## 0. El banco, y que vuelve a reproducir la ADR 0060 al decimal

Mismo protocolo que §28.8, §29.1, §30.1 y §31.1: 1.200 runs por perfil (300 × semillas 1/1001/2001/3001),
contextual = "buena", gastadora = "mediocre"/"mala", el suelo con `economy.rewardPerkWeight = 0` y la
política que esquiva mercados. Las puertas se separan de los partidos ordinarios con exactitud
(`BossSamplesByAct` / `BossWinsByAct`, ADR 0066), no por estimación.

| | ADR 0060/0061/0063/0064 | esta medición | ET |
|---|---|---|---|
| Build buena, actos 2/3 (con jefe) | 57,97 / 44,43 | **57,97 / 44,43** | 0,71 / 0,53 |
| Build mala completa la run | 12,00 | **12,00** | 0,87 |
| Suelo sin build | 10,66 | **10,66** | 0,56 |
| Hueco acto 2 (con jefe) | 10,03 | **10,03** | 0,50 |
| Tasa de victoria de la run | 17,00 | **17,00** | 1,29 |
| Muertes por run · derrotas del acto 1 | 1,46 · 29,74 | **1,46 · 29,74** | 0,02 · 1,87 |
| Puertas, buena | 75,33 · 44,25 · 51,00 | **75,33 · 44,25 · 51,00** | |
| Puertas, suelo | 70,75 · 35,18 · 44,29 | **70,75 · 35,18 · 44,29** | |

Y la curva de la ADR 0033, medida con las **dos** muestras que se usan en el proyecto: la de la puerta de
`Sim.Tests` (32 plantillas × 4 partidos = 640 por celda, con los contadores de carrera de la build) y la
sonda de `--boss-gate` con la que se hacen los experimentos de este paquete (25 × 8 = 1.000 por celda, sin
contadores):

| jefe | incoherente | correcta | buena | muy buena |
|---|---|---|---|---|
| `grimhold_guns` (puerta / sonda) | 21,6 / 18,4 [20-35] | 70,5 / 67,6 [65-80] | 80,6 / 84,6 [75-88] | 90,9 / 91,1 [85-95] |
| `the_hunt` | 10,9 / 8,9 [<15] | 38,9 / 39,6 [35-50] | 62,7 / 66,5 [60-72] | 80,8 / 78,6 [72-85] |
| `eternal_crown` | 2,3 / 4,3 [<10] | 26,4 / 25,0 [15-28] | **40,2 / 38,9** [40-55] | 58,6 / 62,3 [55-70] |

Con la muestra de la puerta **las doce celdas están en banda sin usar el margen de medida** de ±2,5 puntos
—confirma la ADR 0064— pero la del jefe final está clavada en el suelo: **40,2 sobre un mínimo de 40**, y
la sonda de 25 × 8 la mide en 38,9. Es exactamente lo que el encargo señalaba: **la puerta más plana es
también la que peor cumple la tabla**. Los experimentos de §1 se comparan siempre contra la sonda de 25 × 8,
que es la que los produjo.

## 1. Los modificadores de regla del jefe son lo que aplana la puerta, y no se pueden usar al revés

Cada modificador se aísla sustituyéndolo por uno del mismo tipo que no puede tocar nada
(`banChannel` sobre `Card`, canal que ningún perk del catálogo modifica), conservando el número que
RF-001b/RF-001c exigen. Sobre la tabla de puertas:

| condición | incoherente | correcta | **buena** | muy buena |
|---|---|---|---|---|
| `grimhold_guns` hoy → sin `singleCopy` | 18,4 → 18,4 | 67,6 → 67,6 | **84,6 → 84,6** | 91,1 → **93,8** |
| `the_hunt` hoy → sin `markStar` | 8,9 → 8,5 | 39,6 → 39,7 | **66,5 → 66,3** | 78,6 → **82,7** |
| `eternal_crown` hoy → sin `pushBack` | 4,3 → 4,7 | 25,0 → 25,0 | **38,9 → 38,9** | 62,3 → 62,3 |
| `eternal_crown` hoy → sin `banChannel` | 4,3 → **1,6** | 25,0 → 25,0 | **38,9 → 47,2** | 62,3 → **66,2** |

Tres lecturas, y las tres son del mismo hecho:

1. **`singleCopy` y `markStar` sólo tocan el escalón superior.** Los tres escalones que la tabla usa para
   medir la exigencia —incoherente, correcta, buena— **no se mueven ni una décima**. A la densidad de
   build que la ADR 0040 le da a cada acto, una build correcta no repite perks ni concentra en un
   portador, así que el modificador no tiene qué quitarle. Sobre la run tampoco compran nada: la razón de
   la puerta 1 pasa de 1,065 a 1,068 y la de la puerta 2 **baja** de 1,258 a 1,251.
2. **`iron_curtain` (el `pushBack` de columna 6 del jefe final) está inerte.** No mueve ninguna celda, y
   sobre la run el experimento con **los dos** modificadores neutralizados devuelve *exactamente* los
   mismos números que el experimento con sólo el `banChannel` fuera (75,33 · 44,25 · **56,00** y
   70,75 · 35,18 · **45,33**, al decimal). Con las alineaciones de hoy no hay titular por delante de la
   columna 6 al que retrasar. Es un modificador que el jugador ve en el informe de ojeo y que no hace
   nada.
3. **`sealed_goal` sí muerde, y muerde al revés.** Le cuesta **8,3 puntos** a la celda `buena`
   (38,9 frente a 47,2) y **cero** a la celda `correcta` (25,0 en las dos). Es un impuesto que sólo paga
   quien tiene build, porque lo que apaga —los nueve perks que suben `shotOnTarget`— es algo que la build
   correcta a esa densidad no lleva.

> **Los cuatro tipos de modificador de jefe que existen borran build**: `singleCopy` borra las copias,
> `markStar` borra al portador principal, `banChannel` borra un canal y `pushBack` rompe la colocación —y
> romper la colocación hace que los perks bien puestos paguen su `elseEffects`, que es *justo* el canal de
> la ADR 0060—. Por construcción cobran más a quien más ha construido. **El modificador de jefe no es una
> palanca para empinar la puerta: es lo que la aplana.**

Se probó también cambiar el canal de `sealed_goal`, que es calibración de dato pura:

| canal prohibido | incoherente | correcta | buena | muy buena |
|---|---|---|---|---|
| `shotOnTarget` (hoy, 9 perks) | 4,3 | 25,0 | 38,9 | 62,3 |
| `save` (2 perks) | 1,6 | 25,0 | **36,8** | **52,5** OUT |
| `intercept` (7) | 1,6 | 25,0 | 40,6 | **51,9** OUT |
| `tackle` (14) | 3,4 | 23,0 | 44,8 | 60,9 |
| `dribble` (4) · `pass` (4) | 1,6 / 2,5 | 25,1 / 25,0 | 47,2 | 66,2 |

No hay ningún canal que empine la pendiente: o cuesta más que el actual (`save`, `intercept`, y los dos
sacan de banda al escalón superior) o no cuesta nada (`dribble`, `pass`, que dan la misma tabla que no
tener modificador). **Empinar la puerta 3 por esta vía es exactamente "quitarle el modificador al jefe
final", con otro nombre.**

## 2. Cuánto compra la única vía que existe: la dificultad del jefe, y cuánto permite la tabla

Como el modificador no sirve, lo único que queda para mover una puerta es **su dificultad** (calidad,
nivel, plantilla), que es un desplazamiento a lo largo de la sigmoide. La tabla de la ADR 0033 acota ese
desplazamiento, y se puede escribir con un número: el **multiplicador de cuota** que cada jefe admite sin
que ninguna de sus cuatro celdas se salga.

| jefe | multiplicador de cuota permitido | qué lo limita |
|---|---|---|
| `grimhold_guns` | **1,109 – 1,335** (sólo puede ablandarse) | por abajo, la celda incoherente (18,4 < 20); por arriba, la celda `buena` (84,6 → 88) |
| `the_hunt` | **0,821 – 1,295** | por abajo `correcta` (39,6 → 35); por arriba `buena` (66,5 → 72) |
| `eternal_crown` | **1,047 – 1,167** | por abajo `buena` (38,9 → 40, que hoy está fuera); por arriba `correcta` (25,0 → 28) |

(Los tres rangos se derivan de la sonda de 25 × 8; con la muestra de la puerta el límite inferior de
`eternal_crown` es 1,00 en vez de 1,047, porque su celda `buena` mide 40,2 y no 38,9. La conclusión no
cambia: el jefe final sólo admite ablandarse, y muy poco.)

Y **el desplazamiento no separa perfiles: los mueve a los dos.** Con la puerta modelada como una logística
sobre la diferencia de fuerza, la razón entre los dos perfiles en la puerta *n* es
`(R_n + O_n) / (1 + O_n)`, donde `R_n` es la **razón de cuotas** —una propiedad del hueco de fuerza entre
las dos builds, que el jefe no cambia— y `O_n` la cuota de la build buena. Medidas hoy:
`R = 1,262 · 1,462 · 1,309`. Ablandar la puerta sube `O_n` y **baja** la razón; endurecerla la sube pero
hunde la tasa de la buena.

**Comprobado en el campo, no sólo en el modelo.** Se ablanda `the_hunt` de calidad 46 a **42**, que es el
máximo que la tabla permite (celdas 14,5 / 46,1 / 71,3 / 83,6: las cuatro dentro de banda), y se mide el
banco completo:

| | hoy | `the_hunt` a calidad 42 | previsión del modelo |
|---|---|---|---|
| Puertas, buena | 75,33 · 44,25 · 51,00 | 75,33 · **51,77** · 50,96 | · 50,4 · |
| Puertas, suelo | 70,75 · 35,18 · 44,29 | 70,75 · **43,67** · 43,02 | · 41,0 · |
| Razón de la puerta 2 | 1,258 | **1,186** | 1,229 |
| **Tasa de la run, buena** | 17,00 (ET 1,29) | **19,83** (ET 0,78) | 19,4 |
| **SUELO sin build** | 10,66 (ET 0,56) | **12,58** (ET 0,58) | 12,85 |
| Muertes por run | 1,46 | **1,55** | |
| Derrotas del acto 1 | 29,74 | **30,77** | |

El objetivo 4 se roza —19,83 sobre una meta de 20— **y el objetivo 5 se aleja dos puntos**. El modelo
acierta las dos direcciones y las dos magnitudes, así que se puede usar para responder la pregunta del
encargo sin gastar un lote por combinación.

## 3. La frontera, que es la respuesta con número

Eligiendo **libremente** la dificultad de las tres puertas (sin la restricción de la tabla, es decir
suponiendo que la tabla de la ADR 0033 se revisara):

| | con las razones de cuota de hoy | con la puerta 3 empinada al máximo medido (`R₃` 1,309 → 1,535) |
|---|---|---|
| Si la buena gana el **20%** de las runs… | el suelo es como mínimo **13,29%** | como mínimo **12,17%** |
| Si el suelo se queda en el **10%**… | la buena gana como mucho **15,63%** | como mucho **16,96%** |

> **Los objetivos 4 y 5 no son alcanzables a la vez recalibrando las puertas, y no lo son ni siquiera
> moviendo la tabla de la ADR 0033.** El punto de hoy —17,00 con un suelo de 10,66— ya está **sobre** esa
> frontera: no hay discriminación que comprar, sólo intercambio.

Lo que haría falta, medido: elevar las tres razones de cuota a la potencia **1,622**, es decir pasar de
`1,262 · 1,462 · 1,309` a `1,459 · 1,852 · 1,548`. En la puerta 2, y dejando a la build buena donde está,
eso es que **el hueco de la puerta pase de 9,1 a 14,3 puntos** (el suelo bajaría de 35,18 a 30,0). Es un
**62% más de separación en log-cuotas**, y ninguna calibración de jefe la da, porque la calibración es un
número que **los dos perfiles del jugador comparten**.

**Y el cuarto evento filtro tampoco.** Con las razones de hoy, la mejor tasa de la buena compatible con un
suelo del 10% es:

| | tres puertas | + una cuarta con razón 1,065 | 1,151 | 1,258 | 1,462 |
|---|---|---|---|---|---|
| Buena máxima con suelo = 10% | 15,63% | 15,62% | 15,63% | 15,87% | 16,82% |

La aritmética de la ADR 0064 —"cuatro puertas a 1,15 dan 1,77 en vez de 1,54"— es correcta pero incompleta:
mantiene fijas las tasas de paso, y con una puerta más el producto de la buena cae por debajo del 20%.
Para devolverlo hay que ablandar las cuatro, y ablandar baja la razón de cada una. **Añadir una puerta
mueve el punto sobre la misma frontera, no la desplaza.** Queda falsificada la segunda salida de AO-A, y
con ella la única razón que había para tocar la estructura de tres actos que el revisor fijó.

## 4. Y un guardarraíl que es incompatible con el objetivo 4 por aritmética

`defeatShareAct1` no es la tasa de derrota del acto 1: es la **cuota** de las runs perdidas que se pierden
en el acto 1, y vale `(1 − P₁) / (1 − producto)`. Con `P₁` en 75,33 —el acto 1 sin tocar— sube sola en
cuanto sube la tasa de victoria de la run:

| tasa de la run | 17,00 (hoy) | 18,67 | 19,83 | 20,00 |
|---|---|---|---|---|
| `defeatShareAct1` con `P₁` = 75,33 | **29,74** | 30,33 | 30,77 | **30,84** |

> **El techo de 29,74% y la meta de 20-30% de la run no pueden cumplirse a la vez** mientras la puerta 1
> deje pasar al 75,33%: para que la run llegue al 20% con la cuota de derrotas del acto 1 por debajo de
> 29,74 hace falta `P₁ ≥ 76,21`. No es una relajación pedirlo: es que las dos cifras son la misma
> aritmética leída dos veces, y hoy se contradicen. **Se para aquí y se devuelve al revisor** en vez de
> compensarlo ablandando el jefe del acto 1.

## Decisión

**1. No se toca ningún número de balance.** Ni `data/bosses/`, ni `data/perks/`, ni `data/rivals/`, ni
`data/economy/`, ni `data/balance/`, ni `data/map/`. Los seis objetivos de la ADR 0056 quedan **exactamente**
donde los dejó la ADR 0060 y las seis puertas siguen verdes (**598/598 en Release**, 184 ficheros de
`/data` validados).

**2. Las dos salidas que la ADR 0064 dejaba vivas quedan falsificadas por medición.** Empinar la pendiente
`correct`→`good` no es posible con los instrumentos que el jefe tiene —sus cuatro tipos de modificador
borran build, y dos de los tres que hay están inertes en los tres escalones que la tabla usa—; y un cuarto
evento filtro no desplaza la frontera, sólo mueve el punto sobre ella.

**3. La tabla de la ADR 0033 no se revisa, porque no es lo que limita.** La frontera medida sin ninguna
restricción de tabla (buena 20% ⇒ suelo ≥ 13,29%, o 12,17% con la puerta 3 empinada) es prácticamente la
misma que con ella. Proponer una ADR que mueva la tabla sería mover el número que no manda.

**4. Queda escrito el tercer modo de fallo de AO-B.** El criterio de la ADR 0064 decía que una palanca
separa sólo si **(a)** la oposición no tiene ese número y **(b)** la build buena tampoco. La calibración
del jefe falla por una tercera vía, que hay que añadir al criterio:

> **(c)** el número tiene que ser de **uno** de los dos perfiles del jugador. La dificultad de la puerta
> la sufren los dos por igual, así que es un **mando de dificultad**, no una palanca de separación —
> exactamente lo mismo que la fuerza del rival (ADR 0058), el peso de los atributos (ADR 0061) y el ámbito
> del premio (ADR 0063).
>
> **La puerta amplifica el hueco que ya existe —de forma multiplicativa, que es la identidad de la ADR
> 0064— pero no lo crea.** Lo que decide los objetivos 4 y 5 no es cómo de dura es la puerta, es cuánta
> fuerza compra construir bien: **AL-A**, el recorrido del catálogo.

**5. Y se devuelve al revisor una contradicción aritmética**, la del §4: `defeatShareAct1 ≤ 29,74%` y
`runWinRate ∈ [20,30]` no son compatibles con la puerta 1 donde está.

## Lo que el instrumento no puede ver, y hay que decirlo antes de leer nada de arriba

`RunPolicy` **nunca lee `BossRuleModifiers`** al componer la alineación ni al repartir perks: pasa la
llamada al sistema de jefes y no la consulta. La build automática con la que se miden los dos perfiles
**no puede prepararse contra el modificador**, que es justo lo que RF-012b y RF-014 le dan a un jugador
humano —el informe de ojeo completo y gratuito, el nodo de jefe visible desde el principio del acto—.

Por eso todo lo medido aquí sobre los modificadores es una **cota inferior de su valor de diseño**: se
miden como impuesto puro sobre quien más build lleva porque el que los sufre no puede hacer nada al
respecto. Un jugador que ve «Portería sellada» en el informe y deja de comprar perks de remate no paga los
8,3 puntos que paga la política. Antes de tocar un modificador de jefe **por lo que aquí se mide** hay que
enseñarle el modificador a la política, y eso es un paquete propio.

## Qué falsificaría esta decisión

- **Que aparezca un modificador de jefe que no borre build.** Los cuatro tipos de hoy lo hacen. Uno que
  cambiara las reglas del campo sin tocar el once del jugador —el tamaño de la portería, el reloj, la
  turba— sería un modificador con signo neutro, y entonces la puerta podría endurecerse sin cobrarle al
  que construye. Es la única forma de subir `R` desde el lado del jefe.
- **Que la política aprenda a leer el informe de ojeo.** Cambiaría el signo de los cuatro modificadores en
  la medida, y con él las tres razones de cuota.
- **Que `R` suba desde el lado del catálogo (AL-A).** La frontera se desplaza con `R`, no con la
  dificultad: `R` a la 1,622 hace alcanzables los objetivos 4 y 5 a la vez con las puertas donde están.
- **Que la segunda vía de derrota deje de ser despreciable.** La identidad "run = producto de tres
  puertas" sostiene todo el §3 y se apoya en que quedarse sin plantilla ocurre 0,009 veces por run.
