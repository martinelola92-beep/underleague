# 0078. Una build al azar es una build mala, y la banda decía lo contrario

**Fecha:** 2026-09-06
**Estado:** Aceptada e **implementada**. Toca **una métrica de la puerta de fase 1**
(`randomBuildNearNone` → `randomBuildLosesToNone`, banda 40-60 → techo ≤ 45) y el `_doc` de
`data/balance/groups.json`. **No toca ningún jefe, ninguna banda de la ADR 0033, ningún dato del
catálogo, ninguna magnitud de economía ni la política de run**
**Cierra:** AU-A · y con ella el techo de AL-D
**Requisitos:** RF-024, RF-069, RT-055, RT-056, **RT-057**
**Relacionada con:** **ADR 0056** (la decisión del revisor del 6 de septiembre: *construir mal sale peor
que no construir*), **ADR 0060** (el castigo del perk mal puesto se paga en el equipo), ADR 0062 (la otra
vez que un umbral de esta puerta se rederivó en vez de calibrarse), **ADR 0074** (que midió la celda a
cinco semillas y la devolvió al revisor)

## El defecto

`randomBuildNearNone_human_random` exige que la build de perks tomados al azar quede **entre el 40% y el
60%** contra su referencia sin perks. La ADR 0074 la midió a cinco semillas: **41,67 / 40,83 / 38,75 /
41,04 / 40,83**, media **40,62** con desviación **1,10**. Con la semilla 3 la puerta **falla**, y con la
semilla 1 —la que la puerta usa— pasa por **0,56 desviaciones** de margen.

Y aquella ADR dejó escrito por qué no la tocaba: no es un problema de muestra. Subir la muestra haría que
la celda pasara **siempre, en 40,6**, que sigue siendo la misma afirmación.

> El umbral no está descalibrado. **Contradice una decisión que se tomó después de escribirlo.**

## De dónde sale la banda, y qué afirma

La banda es de la fase 1 (`fase1-diseno.md` §8) y su lectura está en su propio nombre: *«la build sin
criterio se queda **cerca** de no construir»*, 50 ± 10. Es una afirmación de **neutralidad**: coger perks
al azar ni ayuda ni estorba.

Desde entonces han pasado dos cosas, y las dos la invalidan:

1. **La ADR 0060.** Cuando se escribió la banda, un perk mal puesto no costaba nada: simplemente no se
   activaba. Desde la ADR 0060 el perk mal puesto **lo paga el equipo**, y ése es el mecanismo que abrió
   el hueco entre perfiles (6,27 → 10,03 puntos). Una build al azar lleva perks mal puestos por
   construcción — `human_random` lleva dos de ocho, más `forward_line` (valor **−115**) y
   `own_third_anchor` (−24) repetido.
2. **La decisión del revisor de la ADR 0056**, del 6 de septiembre, tomada sobre la medición de la
   ADR 0063: *«El revisor elige que **construir mal salga peor**»*, y lo dice literalmente sobre comprar
   **sin criterio** — *«el mercado puede salirte a deber si compras sin criterio: es la misma idea que en
   cualquier roguelike de mazo, donde meter una carta mala es peor que no meter ninguna»*.

**«Sin criterio» es exactamente lo que `human_random` es.** Y el suelo de 40 de la banda afirma que una
build sin criterio **no puede** ser claramente peor que no construir, que es la frase contraria.

La medida lo confirma como una serie temporal, no como un accidente:

| medición | `human_random` contra `human_none` |
|---|---|
| fase 1, `fase1-perks.md` (2.000 partidos) | **50,80** |
| fase 1, cierre | 49,3 |
| fase 1b, `fase1b-resultados.md` | **55,62** |
| **hoy** (ADR 0074, cinco semillas) | **40,62** (DT 1,10) |

El número **se ha ido moviendo hacia donde el diseño lo empuja** a medida que el castigo se implementaba.
La banda no lo siguió.

## Y ya vetó el diseño una vez: AL-D

`randomBuildNearNone` está anotada en `pendientes.md` como **«el techo de la palanca de castigo»**: al
subir el castigo del perk mal puesto (`flank_specialist` a `team dribble -200`, §28.6) la métrica cayó a
**38,54** y **se bajó la palanca, no el umbral**. Es decir: este suelo ya impidió una vez que el juego
hiciera lo que el revisor decidió que hiciera. Eso no es un guardarraíl, es un obstáculo.

## La rederivación, y por qué no es relajar nada

La decisión del revisor no es una banda, es un **orden**: coherente > no construir > mal construida. La
métrica pasa de una banda simétrica a la afirmación de ese orden, y el número **no se inventa**:

1. **El techo baja de 60 a 45.** Es una afirmación **más fuerte**, no más débil: donde antes se admitía
   que una build al azar ganara el 60% —mejor que no construir y casi tanto como una coherente— ahora se
   le exige perder. El 45 no es nuevo: es el techo que la propia §8 le pone a `badBuildsLoseToNone`, y
   **§8 ya lista `human_random` entre las «malas a propósito»**, junto a `orc_misplaced`, `elf_brawler` y
   `human_scattered`. La única incoherencia era que la agrupaba con ellas en la prosa y la juzgaba con
   otra regla en el número. Es **un solo número** en el código (`BuildMetrics.BadBuildMaxWinRate`), no
   dos.
2. **El suelo de 40 desaparece.** No se sustituye por otro más bajo: se quita, porque lo que afirmaba
   —«construir sin criterio no puede salir claramente peor que no construir»— es exactamente lo que la
   ADR 0056 declaró falso. Dejar un suelo cualquiera sería volver a poner un número contra el que
   calibrar (RT-057). Lo que protege a la métrica por abajo ya existe y es otra: `badBuildsLoseToNone` y
   RT-055 acotan el catálogo, y las builds de prueba están excluidas de RT-055 por escrito.
3. **La métrica se renombra.** `randomBuildNearNone` afirmaba la neutralidad en su propio nombre; pasa a
   `randomBuildLosesToNone`. **Sigue siendo una métrica aparte** y `human_random` sigue en su propio grupo
   de `groups.json`, porque es la única build cuyos perks los eligió el **RNG** y no un diseño adverso:
   si algún día una build al azar volviera a ganar, hay que verlo en su propia línea.

## Lo que mide con el umbral nuevo

| | valor | umbral | margen |
|---|---|---|---|
| `randomBuildLosesToNone_human_random`, cinco semillas | **40,62** (DT 1,10) | ≤ 45 | **+4,38 = 4,0 desviaciones** |
| la misma con la semilla de la puerta (1) | 41,67 | ≤ 45 | +3,33 |
| la peor de las cinco (semilla 3) | 38,75 | ≤ 45 | +6,25 |

**Las cinco semillas pasan**, y el margen cumple el criterio que la ADR 0074 derivó para la otra puerta
—que un umbral valga **tres** desviaciones para no ser una moneda—: aquí vale **4,0**. Antes valía 0,56.

**No se toca la muestra** de `BuildGateTests` (40 plantillas × 12 partidos, 480 por celda). La ADR 0074 ya
comprobó que los otros umbrales de esta puerta siguen valiendo y con más holgura que cuando se calibraron
(el más ajustado, `coherentBuildsBeatNone_orc_mob`, a 5,4 desviaciones), así que el defecto era de esta
celda y sólo de ésta.

## Decisión

1. **`randomBuildNearNone` pasa a `randomBuildLosesToNone` y su banda 40-60 pasa a un techo de ≤ 45**, el
   mismo de `badBuildsLoseToNone` y el mismo número en el código.
2. **El suelo de 40 se quita**, no se baja: afirmaba lo contrario de la decisión del revisor de la
   ADR 0056.
3. **`human_random` sigue en su propio grupo** de `data/balance/groups.json` y con su propia línea de
   métrica.
4. **Nada más se toca**: ni la muestra de la puerta, ni los otros umbrales, ni el catálogo, ni las builds.
5. **Cierra el techo de AL-D**: la palanca del castigo del perk mal puesto ya no está acotada por esta
   métrica. Su otro techo —la celda `incoherente` de `grimhold_guns`, hoy 29,31 sobre un mínimo de 20—
   sigue vigente.

## Qué falsificaría esta decisión

- **Que el revisor no quisiera decir esto.** La decisión de la ADR 0056 se tomó sobre la doctrina
  gastadora del bucle de run, no sobre una build de la puerta de fase 1. Esta ADR la aplica a las dos
  porque «sin criterio» describe a las dos; si el revisor quisiera que sólo valga para la compra, la
  banda de `human_random` habría que discutirla de nuevo.
- **Que `human_random` deje de ser una build sin criterio.** Sus ocho perks se tomaron con
  `RngStreams.Rewards` sobre el catálogo de entonces. Si el catálogo cambia de forma —por ejemplo si
  desaparecen los perks de valor negativo— hay que **volver a sortearla**, no reinterpretar la métrica.
- **Que el techo de 45 deje de separarla de las malas a propósito.** Hoy `human_random` (40,62) y las
  malas a propósito comparten techo y eso es intencionado, pero si algún día se quisiera afirmar que una
  build al azar es **mejor** que una adversa, haría falta una segunda medición que hoy no existe: nadie ha
  medido cuánto valen `orc_misplaced`, `elf_brawler` y `human_scattered` con la muestra de hoy.
