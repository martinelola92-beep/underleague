# 0076. `R₁` no se había caído: el banco no llegaba a medirlo

**Fecha:** 2026-09-06
**Estado:** Aceptada. **Paquete de medición: no mueve ningún número de balance.** Responde a AU-B con un
no, y de otro tipo del que se esperaba: la premisa —`R₁` = 1,008— es un **artefacto de muestra** del banco
de 1.200 runs. Con 14.544 runs por lado `R₁` vale **1,149** (`ln R₁` = 0,139 ± 0,026, cinco errores típicos
sobre cero) y **sube cuando el acto 1 lleva menos perks**, que es lo contrario de lo que AU-B proponía
**Cierra:** AU-B · **abre** AV-A y AV-B
**Requisitos:** RF-023, RF-070, RF-072, RT-054, RT-055, RT-056, **RT-057**
**Relacionada con:** **ADR 0075** (que abrió AU-B con la hipótesis que esta ADR falsifica y con la `S` que
corrige), **ADR 0074** (que derivó la muestra de la puerta de jefes con este mismo argumento, un nivel más
abajo), **ADR 0072** (el listón del slot, señalado como culpable), ADR 0064 (la run es el producto de las
tres puertas), ADR 0065 (la frontera y su modelo), ADR 0033 (el acto 1 es el taller), ADR 0070 (la tabla
de valor se mide en campaña de ocho partidos)

## La pregunta, y lo que se encontró en su lugar

La ADR 0075 midió que `R₁` —la razón de cuotas entre la build buena y el equipo sin build en la puerta del
acto 1— se había desplomado de **1,262** a **1,008**, lo atribuyó al listón del slot de la ADR 0072 y
escribió que recuperarla *«vale hoy tanto como subir `S`»*. **AU-B era ese experimento.**

Se ha hecho por tres caminos. Los dos primeros dieron un resultado plano que no encajaba con nada, y el
tercero explicó por qué:

> **`R₁` nunca se cayó.** El 1,008 sale de un banco de 1.200 runs cuyo error típico sobre `ln R₁` es
> **0,091**. Con 14.544 runs por lado, el mismo código y las mismas semillas miden **`R₁` = 1,149**
> (`ln R₁` = 0,139 ± 0,026). Y las primeras 300 runs de cada bloque del banco grande **reproducen bit a
> bit** el banco pequeño: no hay dos mediciones distintas, hay una medición y su subconjunto afortunado.

## 1. El primer camino: una corrección derivada, medida y descartada

La fórmula de la ADR 0072 tiene una omisión real y vale la pena escribirla, porque es lo que motivó el
experimento: **compara el valor de un perk que se coge ahora con el de la oferta que llenará ese slot más
adelante como si valieran lo mismo, y no valen lo mismo.** La run es el producto de las tres puertas
(ADR 0064), así que un perk cogido en la capa 0 del acto 1 juega **tres** y el que ocupe su slot en el
acto 3 juega **una**. Aceptar debería pedir `m · G_ahora ≥ C · G_fut`, o sea

```
listón = C · (G_fut / G_ahora)      G = puertas que quedan por jugar, descontadas por las mismas tasas
                                        de paso que ya descuentan las ofertas; G_fut promediado sobre
                                        las ofertas futuras que entran en N
```

Con las tasas de hoy el factor vale **0,807** en el acto 1, **0,883** en el acto 2 y **1,000** en el
acto 3 —no toca el último acto ni la propiedad de la ADR 0072 de que con coste de oportunidad cero el
listón es −1— y baja el listón del acto 1 de **38 a 30**. Hace exactamente lo que se le pide y **no compra
nada**:

| banco de 1.200 runs | control (ADR 0074) | **con exposición a puertas** | diferencia emparejada |
|---|---|---|---|
| Perks del once en la puerta 1 | 4,34 | **4,76** | |
| Recompensas rechazadas | 22,25% | 17,02% | |
| Puerta del acto 1 | 71,58 | 71,17 | dentro del error |
| Build buena, acto 2 (ordinarios) | 61,10 | **58,63** | **−2,47** (ET 0,80) |
| `masterDivergence` | 22,36 | 18,01 | −4,34 (2,91) |

Se deja implementada y **apagada** (`RunPolicyOptions.WeighsSlotBarByGateExposure`, `--slot-gates`).

## 2. El segundo camino: seis densidades, y una sospecha

Bancos de 1.200 runs, doctrina contextual, puerta 1 exacta de `BossWinsByAct / BossSamplesByAct`:

| condición | listón, acto 1 capa 0 | perks del once en la puerta 1 | puerta 1 |
|---|---|---|---|
| `--min-perk-value 999999` (no coge ninguno) | ∞ | **0,99** | 70,50 |
| `--act1-pass 950 --act2-pass 900` | 48 | **3,74** | 71,75 |
| entregado | 38 | 4,31 | 71,75 |
| control ADR 0074 | 38 | 4,34 | 71,58 |
| `--slot-gates` | 30 | 4,76 | 71,17 |
| `--act1-pass 450 --act2-pass 250` | 25 | **4,80** | 71,92 |
| `--slot-bar-off` (pre ADR 0072) | 0 constante | **5,30** | 71,67 |

La densidad recorre un **42%** y la puerta se queda en **0,75 puntos** de rango. Con un error típico de
**1,30** por celda, eso no dice «la densidad no importa»: dice **«este banco no resuelve nada de lo que se
le está preguntando»**, que es literalmente el defecto que la ADR 0074 corrigió en la puerta de jefes y la
ADR 0072 en `FullRunGateTests`. **Un nivel más arriba, y sin corregir.**

## 3. La muestra, derivada

La cantidad que se juzga es el hueco entre el suelo mínimo que la frontera admite y el 10% del objetivo 5:
la ADR 0075 lo cifró en **0,68 puntos**. El criterio es el que la ADR 0074 escribió antes de medir: **el
hueco tiene que valer tres errores típicos.** Propagando el error binomial de las seis puertas por el
modelo de la ADR 0065 (Monte Carlo, 900 sorteos):

```
1.200 runs/lado  :  suelo mínimo 11,60  ET 1,34  ->  el hueco vale 1,2 ET     NO RESUELVE
7.272 runs/lado  :  suelo mínimo 12,60  ET 0,59  ->  el hueco vale 4,4 ET     resuelve
14.544 runs/lado :  suelo mínimo 12,02  ET 0,41  ->  el hueco vale 5,0 ET     RESUELVE
derivado con la forma medida: N >= 5.339 runs/lado
```

**Se adopta 14.544 runs por lado, que es 2,7 veces el mínimo derivado, y por un motivo medido.** Se
jugaron **dos** bancos independientes de 7.272 (`--full-runs 1818` sobre las semillas 1/1001/2001/3001 y
otra vez sobre 1/10001/20001/30001, éstas sin solape de rangos) y **discreparon más de lo cómodo**:
`S` = 0,936 y 1,105 con un ET declarado de 0,082 cada uno, o sea **1,5 desviaciones**. Con doce
comparaciones sobre la mesa eso es normal, pero es exactamente el tipo de diferencia que este paquete
existe para no volver a leer como señal. **El cierre es el pool de los dos.** Cuesta unos 45 minutos.

Y hay un segundo defecto de método que aparece por el camino y que hay que decir aparte:

> **El «ET» que estos ADR publican no es el error de la estimación.** Es la dispersión entre los **cuatro**
> bloques de 300 runs, un estimador con **tres grados de libertad**. En el banco de cierre de la ADR 0074
> los cuatro bloques dieron 22,33 / 20,67 / 19,67 / 20,33 y de ahí salió «20,75 (ET 0,57)», cuando el
> error **binomial** de una proporción sobre 1.200 runs es **1,16**. En el banco de este paquete los
> cuatro bloques dieron 20,00 / 20,67 / 20,00 / 20,00 y el mismo cálculo habría publicado **ET 0,17**,
> siete veces menor que el real. Con 1.818 runs por bloque los dos coinciden (0,44 contra 0,47), que es
> como se sabe que el problema son los grados de libertad y no el muestreo. **A partir de ahora la barra
> de una proporción es la binomial**; la dispersión entre bloques queda como diagnóstico.

## 4. Lo que cambia al medirlo bien

Mismo código, mismas semillas, mismos bloques: sólo se alargan. Las primeras 300 runs de cada bloque
**reproducen el banco pequeño bit a bit**, así que la comparación es limpia:

| | 1.200 runs/lado | **14.544 runs/lado** |
|---|---|---|
| Puertas, build buena | 71,75 / 49,13 / 57,48 | **72,06 ±0,37 / 48,15 ±0,49 / 56,43 ±0,70** |
| Puertas, suelo sin build | 71,42 / 38,86 / 43,08 | **69,19 ±0,38 / 38,73 ±0,49 / 44,11 ±0,81** |
| **`R`** | 1,016 / 1,520 / 1,786 | **1,149 / 1,469 / 1,641** |
| **`S`** | 1,015 **± 0,201** | **1,019 ± 0,058** |
| Tasa de victoria de la run | 20,17 ± 1,16 | **19,52 ± 0,33** |
| Suelo sin build | 11,41 ± 1,16 | **11,31 ± 0,26** |
| Hueco buena/mediocre, acto 2 | 13,35 | **11,03** |

Tres lecturas, y la del medio es la incómoda:

1. **`S` casi no se mueve** (1,015 → 1,019) **pero su barra se divide por 3,5**, y con ella el 1,145 de la
   ADR 0075 y el 1,175 de la ADR 0069 dejan de ser noticias: todo lo publicado desde el paquete AQ cabe
   dentro de un error típico del banco con el que se midió.
2. **La forma sí se mueve, y mucho.** `ln R` = **0,139 / 0,385 / 0,495** en vez de 0,016 / 0,418 / 0,580:
   una escalera creciente y suave, que es exactamente la que la ADR 0033 diseña —el acto 1 discrimina
   menos porque *es el taller, no el examen*— en vez de una primera puerta muerta y una tercera
   desbocada. **La ADR 0075 construyó su conclusión principal sobre esa forma.**
3. **Y la tasa de victoria de la run no estaba donde se dijo.** El estado que entregó la ADR 0074, medido
   con 7.272 runs, da **19,68 ± 0,47**, no los 20,75 que aquella ADR publicó; el de hoy, con 14.544, da
   **19,52 ± 0,33**. La ADR 0072 declaró el objetivo 6 «en banda por primera vez» sobre esta medida.
   **AV-A.**

## 5. El tercer camino: quitar el listón, que es lo que AU-B proponía

Con la muestra que resuelve, el experimento de AU-B se puede hacer de verdad. `--slot-bar-off` es
literalmente lo que la ADR 0075 propuso —el juego de antes de la ADR 0072, con sus 5,3 perks en el primer
jefe— y con **7.272 runs por condición sobre las mismas semillas**, diferencia emparejada por bloque:

| | entregado | `--slot-bar-off` | diferencia |
|---|---|---|---|
| Perks del once en la puerta 1 | 4,31 | **5,29** | +0,98 |
| **Puerta del acto 1** | **72,14** ±0,53 | **70,85** ±0,53 | **−1,29 (4,4 ET emparejado)** |
| `R₁` contra el mismo suelo | **1,157** | **1,086** | **−0,063** en `ln R₁` |
| Puertas 2 y 3 | 47,83 / 56,93 | 45,71 / 52,09 | |
| Tasa de victoria de la run | **19,57** | **16,83** | **−2,74 (3,5 ET emparejado)** |
| `contextualAdvantage` | +2,24 | **−0,49** | |

> **Devolverle perks al acto 1 no sube `R₁`: lo baja.** El listón del slot no gastó la primera puerta, la
> **construyó**; y hacer lo que AU-B proponía cuesta 2,74 puntos de tasa de victoria de la run y la
> ventaja de construir entera, que es justo lo que la ADR 0072 había comprado.

Es coherente con lo que ya se sabía y nadie había juntado: la tabla de valor **ordena** y no dictamina
(ADR 0038, AT-C), así que aceptar más perks no es aceptar mejores; y el once tiene quince slots
(ADR 0072), así que un perk mediocre en el acto 1 es un slot que el acto 2 ya no tiene.

## 6. La frontera, remedida con la muestra que la resuelve

| | puerta 1 | puerta 2 | puerta 3 | `S` |
|---|---|---|---|---|
| `R` en la ADR 0065 | 1,262 | 1,462 | 1,311 | 0,8837 |
| `R` en la ADR 0069 | 1,260 | 1,599 | 1,608 | 1,1750 |
| `R` en la ADR 0075 (1.200 runs) | 1,008 | 1,602 | 1,946 | 1,1450 |
| **`R` hoy (14.544 runs)** | **1,149 ±0,030** | **1,469 ±0,042** | **1,641 ±0,071** | **1,0186 ± 0,0580** |

| | buena máxima con suelo = 10% | suelo mínimo con buena = 20% |
|---|---|---|
| ADR 0065 | 15,68% | 13,29% |
| ADR 0075, dificultad libre | 20,32% | 9,80% |
| ADR 0075, puerta 1 donde estaba | 19,00% | 10,68% |
| **Hoy, dificultad libre** | **17,51%** | **11,70%** |
| **Hoy, con la puerta 1 donde está** | **17,19%** | **12,02% (ET 0,41)** |

Tres consecuencias, y las tres corrigen a la ADR 0075:

1. **El hueco no es de 0,68 puntos: es de 2,02**, y ahora está resuelto (**5,0 ET**) en vez de insinuado
   (1,2 ET). Lo que falta ya no es *«un +9,3% de `S`»* sino elevar las tres razones a la potencia
   **1,321**, o sea `S` = **1,345** contra la 1,019 de hoy: un **+32%**.
2. **La vía de la puerta del acto 1 no existe, y no por la tabla de la ADR 0033 sino por aritmética.**
   Como `R₁` = 1,149 no es uno, abrir esa puerta **cuesta separación**: el barrido completo, de 72% a
   ~100% de paso, mueve el suelo mínimo de **12,02 a 11,70**. Ni abriéndola del todo se llega al 10%. La
   ADR 0075 concluyó lo contrario porque partía de `R₁ ≈ 1`.
3. **Y «la forma importa» sobrevive, débil.** La misma `S` repartida por igual entre las tres puertas da
   **12,56** contra los 12,02 de la forma real: **medio punto**, no el 1,10 que la ADR 0075 midió.

## Decisión

1. **AU-B se cierra en negativo, y su premisa queda falsificada por muestra.** `R₁` vale **1,149**
   (`ln R₁` = 0,139 ± 0,026); el 1,008 de la ADR 0075 es una desviación de un banco que no resuelve
   `ln R₁` mejor que ±0,091. **No hay nada que recuperar**, y hacer lo que AU-B proponía —devolverle perks
   al acto 1— baja `R₁` y cuesta 2,74 puntos de tasa de victoria de la run.
2. **El banco de cierre de un paquete que toque la frontera pasa de 1.200 a 14.544 runs por lado**, dos
   bloques independientes de 7.272 (`--full-runs 1818` × cuatro semillas, con y sin solape de rangos).
   El mínimo derivado del criterio «el hueco tiene que valer tres errores típicos» es 5.339; se duplica
   con creces porque los dos bancos de 7.272 discreparon 1,5 ET en `S`. Cuesta unos 45 minutos.
3. **La barra de error de una proporción es la binomial.** La dispersión entre los cuatro bloques tiene
   tres grados de libertad y ha publicado errores hasta **siete veces** menores que el real; queda como
   diagnóstico, no como barra.
4. **`S` y la frontera se publican siempre con su error típico**, y con él **la `S` de las ADR 0068,
   0069 y 0075 deja de sostener una serie**: 1,175, 1,145 y 1,019 caben dentro del error del banco con el
   que se midieron.
5. **No se toca ningún número de balance.** Ni el listón, ni el catálogo, ni ningún jefe, ni ninguna
   banda. La ponderación por exposición a puertas se queda **apagada**: es una corrección correcta de la
   fórmula, y la medición dice que el término que corrige no manda.
6. **Lo que falta para los objetivos 4 y 5 vuelve a ser catálogo (AL-A)**, y es más de lo que se creía:
   **+32%** de `S`, no +9,3%.

## Qué falsificaría esta decisión

- **Que 14.544 runs tampoco resuelvan.** El criterio se cumple para el hueco de la frontera (5,0 ET), no
  para todo: la pertenencia de la tasa de victoria de la run a la banda 20-30 sigue sin decidirse —el
  punto está a **0,48 puntos** del borde con un ET de 0,33, o sea a 1,5 desviaciones, y para que valiera 3
  harían falta del orden de **60.000 runs por lado**—. Ver **AV-A**.
- **Que los dos bloques del banco de cierre no basten.** Discreparon 1,5 ET en `S`; el pool lo promedia,
  no lo explica. Si un tercer bloque volviera a caer fuera, el modelo de error binomial se estaría
  quedando corto y habría que medir el factor de diseño como hizo la ADR 0074 con la puerta de jefes.
- **Que la razón de cuotas deje de ser invariante a la dificultad.** La ADR 0074 dejó medido el
  experimento sin querer: ablandar `the_hunt` tres puntos movió `R₂` un 3,4%. Si con otro jefe se moviera
  mucho más, el modelo de la ADR 0065 dejaría de valer y la puerta volvería a ser una palanca.
- **Que el suelo deje de medirse como lo mide la ADR 0057.** Comprobado que la definición no contamina la
  comparación: el equipo sin build de `rewardPerkWeight = 0` (1,53 perks, puertas 71,42 · 38,86 · 42,77 en
  el banco pequeño) y el que juega la economía normal sin coger ningún perk (0,92 perks, **71,00 · 39,67 ·
  42,27**) miden lo mismo.
