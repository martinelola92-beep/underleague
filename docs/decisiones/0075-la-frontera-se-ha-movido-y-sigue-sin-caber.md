# 0075. La frontera se ha movido, y sigue sin caber por siete décimas

**Fecha:** 2026-09-06
**Estado:** Aceptada. **Paquete de medición: no mueve ningún número de balance.** Recalcula con la `S` de
hoy la frontera que la ADR 0065 midió, y responde con número si los objetivos 4 y 5 de la ADR 0056 ya caben
juntos
**Requisitos:** RT-054, RT-056, **RT-057**
**Relacionada con:** **ADR 0065** (la frontera y su modelo), ADR 0064 (la identidad «la run es el producto
de las tres puertas»), ADR 0056 (los objetivos y la decisión del revisor de priorizar ganar runs),
ADR 0068 y **ADR 0069** (las dos subidas de `S`), ADR 0072 (el listón del slot), **ADR 0074** (la
recalibración de `the_hunt`, del mismo paquete)

## La pregunta

La ADR 0065 midió que los objetivos **4** (run de la build buena en 20-30%) y **5** (suelo sin build por
debajo del 10%) **no caben a la vez** con la separación de catálogo de entonces: con la buena al 20% el
suelo no baja de **13,29%**. Desde entonces `S = Σ ln R_n` ha subido dos veces —el eje del contador
(ADR 0069) y el listón del slot (ADR 0072)— y la run acaba de entrar en banda. **¿Dónde está la línea
ahora?**

## El modelo, que es el de la ADR 0065 sin cambiar una letra

Cada puerta es una logística sobre la diferencia de fuerza. `R_n` es la **razón de cuotas** entre la build
buena y el equipo sin build en la puerta *n* —una propiedad del hueco, que la dificultad del jefe no
cambia— y la dificultad desplaza las dos cuotas por igual. La run de cada perfil es el producto de sus tres
puertas (ADR 0064). Eligiendo libremente la dificultad de las tres, la frontera es el óptimo de

```
max  Π σ(x_n)     sujeto a   Π σ(x_n − ln R_n) = suelo
```

**Reproduce la ADR 0065 al decimal**: con sus `R = 1,2624 · 1,4624 · 1,3107` da buena máxima **15,68%** con
el suelo al 10% (la ADR midió 15,63) y suelo mínimo **13,27%** con la buena al 20% (la ADR, 13,29).

## Las razones de cuota de hoy, y que han cambiado de forma

Banco de 1.200 runs por lado (300 × semillas 1/1001/2001/3001), puertas exactas de
`BossWinsByAct / BossSamplesByAct`:

| | puerta 1 | puerta 2 | puerta 3 | `S` |
|---|---|---|---|---|
| Buena (contextual) | 71,58 | 46,57 | 61,15 | |
| Suelo (sin build) | 71,42 | 35,24 | 44,71 | |
| **`R` hoy** | **1,008** | **1,602** | **1,946** | **1,1450** |
| `R` en la ADR 0065 | 1,262 | 1,462 | 1,311 | 0,8837 |
| `R` en la ADR 0069 | 1,260 | 1,599 | 1,608 | 1,1750 |

`S` casi no se mueve desde la ADR 0069 (1,175 → 1,145), pero **la forma sí, y mucho**:

> **La puerta del acto 1 ha dejado de discriminar.** `R₁` pasa de 1,262 a **1,008**: la build buena y el
> equipo sin build pasan el jefe del acto 1 con la **misma** probabilidad (71,58 contra 71,42). Y `R₃` sube
> de 1,311 a **1,946**.

No es un accidente: el listón del slot de la ADR 0072 hace que la doctrina contextual **rechace** casi todo
en el acto 1 —el listón vale 38 al empezar y 26 a mitad de acto— y llegue al primer jefe con 4,34 perks en
vez de 5,29. Compra la separación donde la puerta muerde, que es lo que subió la tasa de victoria de la
run, y la paga donde el acto 1 «es el taller» (ADR 0033). El resultado es coherente con el diseño, pero
tiene consecuencia sobre la frontera y hay que decirla.

## La frontera hoy

| | buena máxima con suelo = 10% | suelo mínimo con buena = 20% |
|---|---|---|
| ADR 0065 (`S` 0,8837) | 15,68% | **13,29%** |
| ADR 0069 (`S` 1,1750) | 18,22% | 11,20% |
| **Hoy, dificultad libre (`S` 1,1450)** | **20,32%** | **9,80%** (ET 0,53) |
| **Hoy, con la puerta 1 donde la tabla la deja** | **19,00%** | **10,68%** (ET 0,41) |
| Hoy, tras la recalibración de la ADR 0074 (`S` 1,0887) | 18,51% | **11,04%** |

Y hay que leer las dos filas de «hoy» juntas, porque dicen cosas distintas:

1. **Con la dificultad libre —que es como la ADR 0065 la calculó— la frontera ya cruza la esquina.** Con la
   buena al 20% el suelo baja a **9,80%**, por debajo del 10 que pide el objetivo 5. Sobre el papel, los
   dos objetivos ya caben.
2. **Pero el óptimo que lo consigue exige abrir la puerta del acto 1 casi del todo.** Como `R₁ ≈ 1`, esa
   puerta no separa: sólo cuesta runs a los dos perfiles por igual, así que el óptimo la deja pasar al
   ~100% y concentra toda la exigencia en las puertas 2 y 3. Barrido, con las razones de hoy:

   | pasa la puerta 1 | 71,6% (hoy) | 75% | 80% | 85% | 90% | 95% | ~100% |
   |---|---|---|---|---|---|---|---|
   | suelo mínimo con la buena al 20% | **10,68** | 10,54 | 10,36 | 10,20 | 10,05 | 9,92 | 9,81 |

   **Para bajar del 10% haría falta que el jefe del acto 1 dejara pasar más del 91% de las runs**, y la
   tabla de la ADR 0033 no lo permite: su celda `incoherente` tiene techo 35 y hoy mide 29,31.

3. **Y `S` no es lo único que mueve la frontera: la forma también.** La misma `S` de hoy repartida por
   igual entre las tres puertas (`R` = 1,465 cada una) da buena máxima **17,49%** y suelo mínimo
   **11,78%** — bastante peor que la forma real. Concentrar la separación donde la puerta ya es dura
   compra más que repartirla. La ADR 0069 escribió que *«`S` es lo único que mueve la frontera»*: con
   `R₁ ≈ 1` esa frase deja de ser exacta.

**El punto de hoy vuelve a estar exactamente sobre su frontera.** Con la puerta 1 donde está, el modelo
predice que una run del 20,33% no puede tener un suelo por debajo del **10,91%**, y el banco mide
**10,92%**. No queda intercambio, sólo más `S`.

## Decisión

1. **No se toca ningún número de balance.** El paquete entrega medición.
2. **Los objetivos 4 y 5 siguen sin caber a la vez, pero el hueco ha pasado de 3,29 puntos a 0,68**
   (13,29 → 10,68 de suelo mínimo con la buena al 20%). Tras la recalibración de `the_hunt` de la
   ADR 0074, **1,04**.
3. **Lo que falta ya no es «un 62% más de separación»**: con la puerta 1 donde está, hace falta elevar las
   tres razones a la potencia **1,093**, o sea `S` = **1,251** contra la 1,145 de hoy, un **+9,3%** — y
   **+14,6%** sobre la 1,089 que deja la ADR 0074. La ADR 0065 pedía la potencia **1,622**. Con la
   dificultad libre la potencia que hace falta es **0,975**, es decir menos que ninguna: por eso la
   frontera de la primera fila ya cruza la esquina.
4. **Y aparece una vía que antes no existía: la puerta del acto 1.** No separa (`R₁ = 1,008`), así que hoy
   es dificultad pura; recuperar su discriminación vale tanto como subir `S`. La ADR 0072 la gastó a
   propósito y a cambio de la tasa de victoria de la run, y **eso no se revierte sin medirlo**: es
   **AU-B**.

## Qué falsificaría esta decisión

- **Que `R₁` vuelva a separar.** Es el número que más ha cambiado y el que más margen tiene: si el listón
  del slot dejara de vaciar el acto 1 sin devolver la tasa de victoria, la frontera se movería sin tocar
  ninguna dificultad.
- **Que la identidad «run = producto de tres puertas» deje de valer.** Sostiene todo el cálculo y se apoya
  en que quedarse sin plantilla ocurre 0,009 veces por run.
- **Que el suelo deje de medirse con `rewardPerkWeight = 0` y la política que esquiva mercados.** Las tres
  `R` salen de comparar esa condición con la contextual; es la definición de la ADR 0057 y no se ha tocado.
- **Que la ADR 0065 §4 siga viva.** Su contradicción aritmética se resolvió con la ADR 0067
  (`ordinaryDefeatRateAct1`, hoy 25,23 sobre un techo de 30), así que ya no acota la puerta 1 por ese lado.
