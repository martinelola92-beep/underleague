# ADR 0103 · El campo tiene seis filas

**Fecha:** 13 de septiembre de 2026 · **Estado:** aceptada · **Decisión del revisor** · **Cierra:** CAT-D
**Toca:** `Pitch.Rows`, `Pitch.AreaRows`, `LinkGeometry.FlankOfHome`, la formación por defecto, el esquema
de guardado, y **la calibración de todo lo que se mide en casillas**

## Qué se decide

`Pitch.Rows` pasa de **5 a 6**. El ancho (16 columnas) no cambia.

Con ello:

- **El centro son DOS filas, la 2 y la 3.** Hoy `LinkGeometry.FlankOfHome` hace `Rows / 2`, así que el
  centro es **1 fila de 5 (20 %)** y cada banda 2 (40 %). Con seis filas y centro doble quedan **tercios
  exactos: 33 / 33 / 33**. No es un parche para que cuadre un número par: es un reparto más limpio que el
  que había.
- **`Pitch.AreaRows` pasa de 3 a 4.** El área cubría el 60 % del ancho (3 de 5) y con 4 de 6 cubre el 67 %,
  que es lo más cercano a conservar su función. Bajarla al 50 % (3 de 6) encogería el dominio del portero y
  además empujaría en la dirección equivocada a **D-21**, que lleva abierta porque los penaltis son casi
  inalcanzables (4 en 500 partidos): un área proporcionalmente mayor los hace algo menos raros.
- **La cámara del ADR 0102 sube a 60° de elevación** por defecto, que es lo que este cambio hace posible.

## Por qué, y por qué ahora

Tres argumentos, y el tercero es el que lo desbloqueó.

**1. Proporción.** 16×5 es **3,2:1**. Un campo de fútbol 7 real ronda 1,5-1,7:1. Lo que había no era un
campo, era un pasillo. 16×6 lo deja en 2,67:1: sigue siendo una abstracción alargada —no se pretende otra
cosa— pero va hacia el fútbol en vez de alejarse.

**2. El centro de dos filas es mejor reparto**, como arriba.

**3. La cámara no cabe en cinco filas.** Éste es el argumento nuevo y es geométrico, no de gusto. El
rectángulo del campo en pantalla es 1120×350 = **3,20:1**. Lo que la cámara ortográfica en tres cuartos
dibuja dentro mide `16 / (filas × sen elevación)`:

| filas | elevación | en pantalla | |
|---|---|---|---|
| 5 | 45° | **4,52:1** | sobra el 29 % de alto: el campo flota en el marco |
| 5 | 60° | 3,70:1 | sobra el 14 % |
| 5 | 80° | 3,25:1 | llena, pero 80° **ya no es tres cuartos** |
| **6** | **55°** | **3,25:1** | **llena, y sigue siendo tres cuartos** |
| 6 | 60° | 3,08:1 | llena de alto, margen mínimo a los lados |

Con cinco filas solo se llena el marco poniéndose casi cenital, que es tirar por la borda la decisión del
ADR 0102. **Con seis, 55-60° llena y sigue siendo tres cuartos.** El revisor pidió decidir CAT-D después de
ver la cámara, y esto es lo que la cámara contestó.

## Lo que NO es

**No es un arreglo de solapamiento.** Eso ya lo resolvió el ADR 0102: en tres cuartos con sombra, dos
cuerpos solapados se leen como profundidad. Y manteniendo el rectángulo de pantalla, **más filas dan filas
más bajas**, así que como arreglo de dibujo iría en contra. Esta fila se añade por proporción, reparto y
encuadre, no por legibilidad de cuerpos.

## Lo que cuesta, y cómo se compensa

La densidad pasa de **80 a 96 casillas**: de 5,71 a 6,86 por jugador, **un 20 % más de espacio**. Más
espacio es menos duelos, y los duelos son la carnicería, que es el recurso central de la run. Hay que
compensarlo o el juego se ablanda.

**El compensador es `bodyRadius`, no la velocidad.** La velocidad está aguas arriba de todo —intercepción
del pase (ADR 0091), cobertura del portero, correa blanda, desgaste— y amplifica los sesgos raciales de
velocidad (ADR 0092): subirla no compensaría la densidad, cambiaría qué juego es. `bodyRadius` es el dial
literal de apelotonamiento, ya es dato por raza y lo consume `BodySeparation` (ADR 0020). Para conservar la
fracción de empaquetado con un 20 % más de superficie, el radio sube un **≈10 %** (√1,2 = 1,095).

Se mide en dos pasos y en este orden, porque el segundo depende del primero:

1. Seis filas **sin** compensar, para ver cuánto cae de verdad.
2. Seis filas con `bodyRadius` +10 %, para ver cuánto recupera.

Las cifras de las dos tandas se anotan abajo antes de dar por cerrada esta ADR.

## Lo que hay que preservar y es fácil de romper

- **Las seis relaciones direccionales de la ADR 0021.** La formación por defecto 2-3-1 —GK (0,2); DEF
  (2,1),(2,3); MID (3,2),(4,1),(4,3); FWD (6,2)— se diseñó en el paquete U precisamente para que se
  resuelvan *ahead*, *behind*, *left*, *right* y las dos diagonales. Antes de ella, **seis perks del eje de
  colocación aplicaban siempre su `elseEffects` y eran maluses puros**. La formación de seis filas tiene que
  conservar esa propiedad; es condición de aceptación, no un detalle.
- **Los tres tercios de inicio de `startsIn()`** siguen ocupados: portero y defensas en el propio, medios en
  el centro, delantero en el atacante.
- **El guardado sube de versión** (RT-030): las casillas de la alineación viven en el estado.

## Consecuencia que hay que aceptar: no hay fila central

Con un número par de filas **no existe una fila central**. El centro geométrico del campo cae en la frontera
entre las filas 2 y 3 (`CenterRow` = 3,0), de modo que la portería queda perfectamente centrada, pero la
casilla-hogar del portero está media casilla descentrada mire donde mire. El motor lo corrige en el primer
tick —el portero se mueve en continuo— así que es cosmético en el saque inicial. Se anota porque es el
único inconveniente real de 6 frente a 5 o 7, y porque no tiene arreglo dentro de la decisión.

## Medición

**Sensación de fútbol (RT-056), 2.000 partidos, semilla 1, `reference.json`.** Antes y después sobre el
mismo lote, medido con un *worktree* del estado anterior para que la comparación sea limpia:

| métrica | 5 filas | **6 filas** | banda | |
|---|---|---|---|---|
| entradas por partido | 11,23 | **9,19** | 6-14 | dentro (−18 %) |
| **lesiones por partido** | **0,92** | **0,75** | 0,30-0,90 | **estaba FUERA y entra** |
| tiros por partido | 8,12 | 8,41 | 8-16 | dentro |
| goles por partido | 2,57 | 2,83 | INFO | |
| alternancias | 22,54 | 21,79 | 12-28 | dentro |
| cadena media de pases | 2,17 | 2,17 | 2-4 | dentro |
| tercio máximo del balón | 51,10 | 50,38 | ≤ 52 | dentro, con más margen |

Las entradas caen el 18 % que predecía la dilución de densidad, pero **siguen a mitad de banda**, y las
lesiones —que estaban **fuera por arriba** con cinco filas en este lote— entran. La sensación de fútbol no
empeora: mejora.

**La compensación de `bodyRadius` se midió y se DESCARTA.** Esta ADR la daba por necesaria (+10 %: 30→33,
32→35, 38→42, 28→31). Medida, no hace nada útil: `badBuildsLoseToNone_human_scattered` pasa de 56,04 a
56,46 —peor, no mejor— y las métricas de RT-056 se mueven en la tercera cifra (entradas 9,19 → 9,27,
lesiones 0,75 → 0,77). Revertida. **La hipótesis era mía y los datos la contradicen**: con las entradas y
las lesiones dentro de banda sin compensar, apretar los cuerpos solo habría vuelto a sacar las lesiones por
arriba. Es exactamente el motivo de haberla medido en dos tandas en vez de aplicarla de entrada.

**El instrumento de fase 1 hubo que recalibrarlo, y no todo cabe.** De las 44 builds de `/Balance`, solo
**6** definen alineación explícita, y son las posicionales: las cinco `*_incoherent` y `human_scattered`.
Sus casillas estaban calibradas para cinco filas —la fila 4 era el **borde lejano**— y con seis dejaron de
significar lo que medían. Se reexpresan con **una regla única y declarada, 4 → 5** (el borde es ahora la
fila 5), no ajustando hasta que pasen. Con ello las cinco `*_incoherent` vuelven a su sitio
(`human_incoherent` 17,50 % contra la referencia).

## Lo que queda ROJO, y por qué no lo arreglo aquí

Dos puertas de 43, las dos del instrumento de fase 1, no del juego:

- **`badBuildsLoseToNone_human_scattered` = 58,12** (techo 45). Verificado por geometría que la alineación
  dispersa nueva **no resuelve ninguna** de las seis relaciones direccionales, igual que la vieja: la
  dispersión no es la causa. Lo que se mide es que, con más espacio, **todas** las builds ganan más al
  equipo **sin ningún perk** (`human_random` 56,25, `human_scattered` 53,75 en una muestra corta). Es decir,
  más espacio hace que los perks pesen más frente a no tener ninguno.
- **`TheThreeDoctrinesBuyDifferently`**: la contextual compra 1,26 por mercado y la ahorradora 1,27.

Las dos apuntan a **D-34**, que lleva abierta desde la fase 1: `human_none` es una plantilla con **cero**
perks y cualquier build lleva **catorce**, así que comparar contra ella no mide equilibrio, mide cuánto
vale el catálogo entero. RT-055 nunca se ha cumplido. Este cambio de campo no crea ese problema: lo hace
visible en una puerta. Arreglarlo es **cambiar la referencia** —una build de perks neutros en vez de una
plantilla desnuda— y eso **exige su propio ADR** (RT-057) y su propia medición, no un ajuste de rango aquí.
