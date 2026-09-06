# 0073. La densidad por acto no estaba mal de un lado solo

**Fecha:** 2026-09-06
**Estado:** Aceptada e **implementada**. Toca **un solo fichero**, `data/balance/groups.json`
(`actDensity`), que es material de medición: **no cambia ninguna regla de juego** y no puede mover
ninguna métrica de run
**Cierra:** AS-B
**Requisitos:** RF-023, RF-071, RF-076, RT-055, RT-057
**Relacionada con:** **ADR 0040** (cada celda se mide con la densidad alcanzable en ese punto de la run),
ADR 0033 (la tabla, **intocada**), ADR 0043 (el escalón incoherente llega con más piezas), ADR 0049
(la última vez que se midió la densidad), **ADR 0055** (el objeto pasa a ser una compra),
**ADR 0072** (el listón del slot, del mismo paquete), ADR 0070 (que dejó AS-B abierta)

## La pregunta, y por qué la respuesta no es una sola

La ADR 0070 dejó anotado que el modelo de `groups.json` y el banco llevan mucho sin cuadrar:

| | modelo | banco |
|---|---|---|
| perks por acto | 5,25 / 7,5 / 8,75 | **5,3 / 11,8 / 12,4** |
| objetos por acto | 2,0 / 3,5 / 3,75 | **0,75 / 1,4 / 2,9** |

Y el encargo era averiguar **cuál de los dos está mal**. Medido, hay tres respuestas y ninguna es "el
modelo" a secas.

### 1. La comparación mezclaba dos cosas distintas

El 5,25 / 7,5 / 8,75 es la **media de los cuatro escalones** del modelo. El 5,3 / 11,8 / 12,4 es la
**doctrina contextual**, que en todas nuestras tablas es el escalón `buena` y **sólo ése**. Escalón contra
escalón, el modelo llevaba `good` en 5 / 8 / 9 contra un banco de 5,3 / 11,8 / 12,4: el exceso está en los
actos 2 y 3, no es un 60% repartido.

### 2. Los perks: se había ido el juego, no el modelo

La mayor parte de ese exceso era que la doctrina contextual **no tenía listón de slot** y se quedaba con
casi todo lo que le ofrecían (ADR 0072). Con el listón derivado puesto, el banco de 1.200 runs mide
**4,34 / 9,48 / 11,34**, y el exceso sobre el `good` del modelo baja de +3,8 / +3,4 a +1,5 / +2,3 — dentro
de lo que separa a dos escalones vecinos.

> **De la mitad de perks del desajuste, la causa era el juego, y arreglar AS-A la arregla sola.**

### 3. Los objetos: se había ido el modelo, y hay fecha

`economy.rewardItemWeight` pasó de 25 a **0** en el paquete AI (ADR 0055): *"un objeto es una compra, no
un trofeo"*. Ese mismo paquete midió que los objetos por run caían de **4,13 a 2,12** y **`groups.json` no
se tocó**: siguió declarando 2 / 4 / 6 titulares equipados en las tres puertas.

El banco de hoy mide **0,82 / 1,75 / 3,00** para la doctrina contextual y **1,01 / 1,88 / 3,24** incluso
en las runs que se **ganan**. Un escalón `muy buena` medido con **seis** titulares equipados en el jefe
final es un jugador que **no puede existir**, que es exactamente el error que la ADR 0040 se escribió para
corregir.

> **De la otra mitad, la causa era el modelo, y llevaba cinco paquetes desfasado con un ADR que explicaba
> por qué.**

## Las anclas, todas medidas

Sobre 1.200 runs (300 × semillas 1/1001/2001/3001) con el listón de la ADR 0072 puesto, densidad del once
al entrar en cada jefe:

| escalón | ancla | perks | objetos |
|---|---|---|---|
| `incoherent` | doctrina **gastadora** | 6,67 / 12,44 / 13,38 | 0,76 / 0,99 / 1,99 |
| `correct` y `good` | doctrina **contextual** | 4,34 / 9,47 / 11,34 | 0,82 / 1,75 / 3,00 |
| `excellent` | contextual, runs **ganadas** | 4,57 / 10,39 / 11,53 | 1,01 / 1,88 / 3,24 |

Y por qué cada ancla:

- **`incoherent` ← la gastadora.** Es la doctrina que **no rechaza nada**, y la ADR 0043 dice que por eso
  el escalón incoherente llega con **más** piezas que el correcto. El `_doc` anterior de `groups.json` ya
  usaba ese argumento; ahora usa además su número.
- **`correct` y `good` con la misma densidad.** La ADR 0033 separa los cuatro escalones por **calidad** —si
  los perks se activan, si son coherentes, si la colocación respeta lo que exigen— y la ADR 0040 dice que
  cada celda se instancia con **la densidad alcanzable en ese punto de la run**, que es una sola. Darles
  densidades distintas era hacer que la celda midiera dos cosas a la vez. Medido: darles la misma no
  aplana la escalera (70,78 contra 77,03 · 39,06 contra 62,66 · 24,06 contra 47,81).
- **`excellent` ← las runs ganadas.** Es literalmente la definición de la ADR 0033: *"el escalado
  acumulado durante toda la run"*.

`counterCap` **no** se remide en este paquete y queda anotado.

## Las doce celdas

Muestra de la puerta —32 plantillas × 4 partidos = 640 por celda, semilla 1, la misma que corre
`Sim.Tests`—, **sin tocar ningún jefe y sin tocar ninguna banda**:

| jefe | incoherente | correcta | buena | muy buena |
|---|---|---|---|---|
| `grimhold_guns` | **27,66** [20-35] | **70,78** [65-80] | **77,03** [75-88] | **93,28** [85-95] |
| `the_hunt` | **13,28** [<15] | **39,06** [35-50] | **62,66** [60-72] | **81,72** [72-85] |
| `eternal_crown` | **4,84** [<10] | **24,06** [15-28] | **47,81** [40-55] | **65,62** [55-70] |

Las doce dentro de banda y la escalera monótona en los tres jefes. Con la sonda de 25 × 8 (semilla 1):
29,70 / 66,50 / 81,10 / 94,80 · 12,30 / 39,50 / 61,10 / 81,10 · 4,80 / 24,20 / 43,50 / 61,50.

**Dos de las tres celdas ajustadas de la ADR 0070 se despegan**: `grimhold_guns` muy buena baja de 95,00
—clavada en su techo— a **93,28**, y `the_hunt` correcta sube de 36,88 —a 1,9 de su suelo— a **39,06**.
Quedan ajustadas `the_hunt` incoherente (13,28 sobre un techo de 15, igual que antes) y `the_hunt` buena
(62,66 sobre un suelo de 60).

## Y un aviso que hay que escribir, porque no es de este paquete

Con **cuatro veces la muestra** (64 plantillas × 8 partidos = 2.560 por celda, semilla 11) **dos celdas
caen fuera de banda, y ya caían antes**:

| celda | densidades viejas | densidades nuevas | banda |
|---|---|---|---|
| `the_hunt` buena | **57,81** | **57,81** | 60-72 |
| `grimhold_guns` muy buena | **95,35** | **95,00** | 85-95 |

Es decir: el "las doce en banda" de los últimos paquetes depende de la muestra concreta de la puerta
(32 × 4, semilla 1), que no resuelve ±2,5. **Este paquete no lo empeora** —las dos celdas miden lo mismo o
mejor con las densidades nuevas— pero tampoco lo arregla, y es el mismo defecto de muestra que la ADR 0072
corrigió en `FullRunGateTests`. Queda como **AT-B**.

## Decisión

1. **`data/balance/groups.json` → `actDensity` se remide** con las anclas de arriba:

   | | perks acto 1/2/3 | objetos acto 1/2/3 |
   |---|---|---|
   | `incoherent` | 7 / 12 / 13 | 1 / 1 / 2 |
   | `correct` | 4 / 9 / 11 | 1 / 2 / 3 |
   | `good` | 4 / 9 / 11 | 1 / 2 / 3 |
   | `excellent` | 5 / 10 / 12 | 1 / 2 / 3 |

2. **La tabla de la ADR 0033 no se toca, ni ningún jefe, ni ninguna banda, ni `counterCap`.**
3. **La respuesta a "cuál de los dos estaba mal" es: los perks el juego, los objetos el modelo**, y la
   comparación que abrió la pregunta mezclaba la media de cuatro escalones con una sola doctrina.

## Qué falsificaría esta decisión

- **Que `correct` y `good` tengan que diferenciarse en densidad.** La nota AA-17 de `groups.json` decía
  que con una sola densidad por acto `buena` y `muy buena` empatan en el acto 1; eso sigue siendo cierto y
  por eso `excellent` conserva su propia ancla. Si algún día `correct` y `good` empataran, harían falta
  dos anclas y hoy sólo hay una doctrina que las mida a las dos.
- **Que la muestra de la puerta se quede corta.** Ver el aviso: con 2.560 partidos por celda dos celdas se
  salen, con las densidades viejas y con las nuevas. **AT-B.**
- **Que `counterCap` esté igual de desfasado.** No se ha remedido. Los topes de hoy (1/2/3/4 · 2/2/4/5 ·
  1/2/5/5) vienen del paquete AA y la ADR 0070 sí remidió los contadores del **fichero de build**, no los
  del recorte.
- **Que la ancla de `excellent` deje de ser "las runs ganadas".** Es la lectura literal de la ADR 0033,
  pero es una muestra condicionada al resultado: si la tasa de victoria se moviera mucho, la ancla se
  movería con ella sin que el escalón cambie de significado.
