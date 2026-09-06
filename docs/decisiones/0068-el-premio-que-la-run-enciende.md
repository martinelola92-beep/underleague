# 0068. El único premio que la oposición no puede cobrar es el que la run enciende

**Fecha:** 2026-09-06
**Estado:** Aceptada. **Primera medición en nueve paquetes en la que la razón de cuotas SUBE**, y a la vez
**falsifica la lectura ingenua de la pregunta del encargo**. No mueve ningún número de balance: el paquete
entrega medición
**Caracteriza:** AL-A, el recorrido del catálogo, con la unidad que la ADR 0065 dejó definida
**Requisitos:** RT-054, RT-055, RT-056, RT-057, RF-070, RF-032
**Relacionada con:** ADR 0033 (la tabla), ADR 0038, ADR 0050 (P1), ADR 0056 (objetivos), ADR 0058,
ADR 0060 (el recorrido por canal), ADR 0063 (el catálogo es compartido), ADR 0064 (el criterio),
ADR 0065 (la frontera), ADR 0067

## De dónde venía el encargo

La ADR 0065 cerró la última palanca de puerta y dejó una sola pregunta viva, con su número: para cumplir a
la vez los objetivos 4 (run de la build buena en 20-30%) y 5 (suelo < 10%) hay que elevar las tres razones
de cuota a la potencia **1,622**. Eso es catálogo, **AL-A**. Y el encargo lo formuló como una pregunta que
había que medir, no como una hipótesis que implementar:

> ¿Existe una forma de premio que la oposición no pueda cobrar **estructuralmente**?

**La respuesta es sí, existe una y sólo una** —el contador—, **y no basta.** Las dos mitades tienen
número.

## 0. La unidad, y el banco

Todo se mide con una sola cifra sumable, la separación en log-cuotas entre la build buena y el equipo sin
build a lo largo de las tres puertas:

```
S = Σ ln R_n            hoy 1,2624 · 1,4624 · 1,3107  →  S = 0,8837
S necesaria = 1,622 × S = 1,4333
```

El modelo logístico de la ADR 0065 se reproduce al decimal (con la `S` de hoy, buena máxima 15,68% con
suelo 10%, y suelo mínimo 13,27% con buena 20%; la ADR midió 15,63% y 13,29%). **`S` es lo único que
mueve la frontera**; la dificultad mueve el punto sobre ella.

Banco: 1.200 runs por perfil (300 × semillas 1/1001/2001/3001), el mismo de §28.8 en adelante, que vuelve
a reproducirse al decimal. Las tres puertas se leen ahora exactas de `BossWinsByAct / BossSamplesByAct`
(ADR 0067): **75,33 · 44,26 · 51,06** la buena y **70,75 · 35,19 · 44,32** el suelo. Ninguna condición
toca `data/economy/perk-values.json`, así que la doctrina contextual compra lo mismo en todas y lo que
cambia es lo que el perk hace en el campo.

## 1. Por qué el contador es el único, y por construcción

Los quince perks con `accumulatesAcrossMatches` (RF-070) valen `k^n`, donde `n` es un contador que su
portador carga **a lo largo de la run**, una unidad por partido como mucho. Y ese `n` la oposición no
puede tenerlo:

- `RivalTeamBuilder.Build` monta un `PlayerDefinition` por partido desde `data/rivals/`, cuyo esquema
  **no tiene campo de contador**.
- `BossDefinition.ToTeamSetup` genera el equipo del jefe desde cero con `TeamGenerator.Generate` y le pega
  los perks de su plantilla. Tampoco hay contador que pegar.
- `ProgressionRules.ApplyCounterDeltas` sólo escribe de vuelta en la plantilla **del jugador**, la única
  que persiste entre partidos.

Seis de los quince están escritos en `data/rivals/` y `data/bosses/` —`eternal_crown` lleva
`clean_sheet_legacy`— y **los seis valen `k⁰ = 1`**: el jefe los enseña en el informe de ojeo y no hacen
nada. Subir `valuePerCounter` es subir un premio que los **55 slots de perk de los tres jefes** no pueden
cobrar.

Es el primer canal en nueve paquetes que cumple la condición **(a)** de la ADR 0064 *por construcción y
no por calibración*. Y cumple las otras dos, medidas:

| | contadores por run | perks de acumulación en el once |
|---|---|---|
| Build **buena** (contextual) | **15,82** (ET 0,50) | 3,11 |
| Build **mediocre** (gastadora) | **9,41** (ET 0,48) | 2,72 |
| **Sin build** (suelo) | **2,22** (ET 0,07) | 0,52 |

**(b)** la build buena no lo tiene al máximo, y **(c)** le pertenece: 1,7 a 1 contra la mediocre con sólo
un 14% más de perks de acumulación, y 7 a 1 contra el suelo. Cargar un contador exige haber comprado el
perk **pronto**, habérselo puesto a un titular que **sigue vivo** y seguir **jugando partidos**. Es,
literalmente, construir a lo largo de la run.

## 2. Lo que el eje vale hoy, y lo que valen las otras dos mitades del catálogo

Cuatro condiciones, cada una con el banco completo (1.200 runs por perfil **y** 1.200 del suelo):

| condición | R₁ · R₂ · R₃ | **S** | run buena | suelo |
|---|---|---|---|---|
| **hoy** | 1,262 · 1,462 · 1,311 | **0,8837** | 17,00 (ET 1,28) | 10,67 (ET 0,56) |
| sin el eje de acumulación (`accumulatesAcrossMatches: false` ×15) | 1,153 · 1,470 · 1,330 | **0,8119** | 15,58 | 10,00 |
| sin castigo (`elseEffects` vacíos, 17 perks) | 1,260 · 1,342 · 1,609 | **1,0013** | 15,92 | 9,25 |
| la oposición **sin catálogo** (71 slots fuera de `rivals/` y `bosses/`, salvo los letales) | 1,160 · 1,187 · 1,456 | **0,6952** | 37,67 | 29,50 |
| **necesaria** | | **1,4333** | | |

- **El eje vale hoy 0,072 de los 0,884: el 8,1%** de toda la separación del catálogo. Y la pone entera en
  las puertas 1 y 3: en la del acto 2 vale **cero**.
- **El castigo no separa a la buena del suelo, sino de la mediocre**: quitarlo *sube* `S`. No contradice a
  la ADR 0063 —el hueco de partido ordinario entre buena y mediocre se hunde de 9,91 a 4,22, que es su
  medición reproducida— sino que precisa qué objetivo sirve: el 2, no el 4 ni el 5. **El suelo no tiene
  build que castigar.**
- **Y la lectura ingenua de la pregunta del encargo queda falsificada.** Quitarle el catálogo a la
  oposición **baja** `S` de 0,884 a 0,695 mientras dispara la run al 37,67% y el suelo al 29,50%: es el
  mando de dificultad más potente medido en nueve paquetes, y es un **mando**, no una palanca.

> **No se trata de que la oposición deje de cobrar un premio que existe. Se trata de que exista un premio
> que la oposición no puede encender.**

## 3. El techo del eje, con el número que el encargo pedía antes de intentar nada

`ProbabilityScale.CounterCeilingFor` ya acota lo que puede valer **una copia** de un efecto con contador
—común 50, poco común 100, raro 200—, y el catálogo está **por debajo** de ese techo justo en los canales
que la ADR 0060 §28.3 midió con recorrido (`intercept` base 2,5%, `tackle` 28%, `save`) y **en** el techo
en los que no lo tienen (`pass` 77%, `dribble` 72%, `shotOnTarget` 78,5%). Subiendo al techo los efectos
con contador de los canales con recorrido:

| | R₁ · R₂ · R₃ | **S** | potencia | run buena | suelo | hueco acto 2 |
|---|---|---|---|---|---|---|
| hoy | 1,262 · 1,462 · 1,311 | 0,8837 | 1,000 | 17,00 (1,28) | 10,67 (0,56) | 9,91 |
| eje al techo, 4 canales | 1,255 · 1,612 · 1,534 | **1,1323** | **1,281** | **19,50** (0,80) | **10,83** (0,91) | 10,79 |
| eje al techo, 3 canales (sin `dribble`) | 1,288 · 1,579 · 1,482 | **1,1034** | **1,249** | **19,08** (0,90) | 10,83 (0,87) | 9,81 |

**Es la primera condición en nueve paquetes en la que `S` sube**, y sube donde tiene que subir: la build
buena gana 2,5 puntos de run, **el suelo no se entera** (10,67 → 10,83, dentro del error) y la mediocre
tampoco (12,00 → 11,58). La diferencia entre las dos sondas vuelve a confirmar AL-A: la de tres canales no
toca `silky_veteran` (`dribble`, base 72%) y da lo mismo.

> **Y no llega. El eje de acumulación, exprimido hasta el techo que la rareza permite, cubre el 45% del
> camino: lleva la potencia de 1,000 a 1,281 cuando hace falta 1,622.** Con 1,281 la mejor combinación
> posible sigue siendo buena 17,58% con suelo 10%, o suelo 11,69% con buena 20%.

## 4. Y lo que impide gastar ni siquiera ese 45%

La sonda de puertas (25 × 8) sobre la condición del eje al techo:

| jefe | incoherente | correcta | **buena** | **muy buena** |
|---|---|---|---|---|
| `grimhold_guns` hoy → al techo | 18,4 → **18,4** | 67,6 → **67,6** | 84,6 → **88,8** | 91,1 → **95,4** OUT |
| `the_hunt` | 8,9 → **8,9** | 39,6 → **39,6** | 66,5 → **74,6** OUT | 78,6 → **89,2** OUT |
| `eternal_crown` | 4,3 → **4,3** | 25,0 → **25,0** | 38,9 → **47,7** | 62,3 → **77,9** OUT |

**`incoherente` y `correcta` no se mueven ni una décima.** Es exactamente "empinar la pendiente
`correct`→`good`", lo que la ADR 0064 pedía y la ADR 0065 demostró que no se puede comprar **desde el lado
del jefe**: desde el catálogo sí, porque los escalones de abajo no llevan contadores y los de arriba sí. Y
de paso arregla la única celda que estaba clavada en su suelo (la `buena` del jefe final, 38,9 → 47,7).

Pero al techo se sale de la tabla por arriba, y **no hay dosis que compre run sin tocar `muy buena`**. Se
probó la única forma de dosificar que la escala permite —"el contador **paga antes** y llega al mismo
sitio": la magnitud por copia sube al techo y `maxValue` se acorta para que `k^max` no cambie
(`battle_reader` 1,5⁵ = 7,59 → 2,0³ = 8,00, y así los seis)—: las **doce celdas vuelven a banda**
(92,9 · 79,4 · 63,9 arriba) y la run se queda en **17,67** (ET 1,35), que es 17,00 dentro del error.

> **El escalón `muy buena` de la ADR 0033 y la build con la que la doctrina contextual llega al jefe final
> son el mismo punto del eje: los dos con el contador a 5.** Todo lo que el eje le da a la run se lo da
> también a esa celda, y acortar la línea para salvar la celda se lo quita a la run en la misma medida.

## 5. La segunda palanca del mismo eje, y por qué también se cae

`--perk-values` mide **un solo partido**, así que mide los quince perks de acumulación con el contador a
cero, es decir **inertes** (AN-B, ADR 0063). En la tabla vigente eso son cinco valores negativos —
`clean_sheet_legacy` **−42**, `scar_veteran` −18, `pit_veteran` −13, `lane_reader` −8,
`poacher_instinct` −6— y `RunPolicy.WorthASlot` los compara con `MinPerkValue` = 0 **sólo en la doctrina
contextual**:

> **El instrumento le dice a la build buena que los perks cuyo valor sólo existe a lo largo de una run no
> valen nada, y la build buena los rechaza; la gastadora, que no pregunta, los compra.** En 1.200 runs la
> contextual termina con `clean_sheet_legacy` **0** veces y `poacher_instinct` 4; la gastadora, 99 y 1.

La sonda pone esos cinco a 0 —lo mínimo para que `WorthASlot` los deje pasar; es la cota del arreglo, no
el arreglo:

| | R₁ · R₂ · R₃ | S | run buena | suelo | perks de acumulación | contadores |
|---|---|---|---|---|---|---|
| hoy | 1,262 · 1,462 · 1,311 | **0,8837** | 17,00 (1,28) | 10,67 (0,56) | 3,11 | 15,82 |
| el instrumento deja de rechazarlos | 1,123 · 1,397 · 1,275 | **0,6928** | **15,75** (1,05) | 10,58 (0,55) | **4,18** | **19,16** |

**La build buena acumula un 21% más de contadores y gana un 7% menos de runs.** El sesgo es real, pero
corregirlo bajando el listón sale caro: a las magnitudes de hoy esos cinco perks siguen valiendo poco
incluso cargados y el slot se lo quitan a uno que vale más. **El orden importa: primero la magnitud,
después volver a medir el valor.**

## Decisión

**1. No se toca ningún número de balance.** Ni `data/perks/`, ni `data/rivals/`, ni `data/bosses/`, ni
`data/economy/`, ni `data/balance/`. Los seis objetivos de la ADR 0056 quedan **exactamente** donde los
dejó la ADR 0060, las doce celdas de la ADR 0033 en banda (sonda 25 × 8 idéntica al decimal a la de la
ADR 0065) y la suite en **599/599** verdes en Release, con 184 ficheros de `/data` validados.

**2. Queda medido que AL-A tiene una palanca real y una sola**, el contador, y queda medido que **no basta
por sí sola**: 45% del camino a la potencia 1,622. Los objetivos 4 y 5 **no son alcanzables a la vez** ni
con la puerta (ADR 0065) ni con el catálogo tal y como está hoy el eje. El plan de repliegue que el
revisor fijó en la cabecera de la ADR 0065 sigue en pie.

**3. Se falsifica la lectura ingenua de "un premio que la oposición no pueda cobrar".** Quitarle el
catálogo a la oposición baja la separación. La condición (a) de la ADR 0064 hay que leerla en positivo:
no *quitarle* a la oposición un número que tiene, sino *darle al jugador* un número que la oposición **no
puede tener**, y el único que existe es el que la run enciende.

**4. El paquete siguiente está especificado y medido**: subir el eje al techo de rareza en los canales con
recorrido **y recalibrar los tres jefes a la vez**, que es lo que el guardarraíl de la ADR 0056 autoriza
("si una celda se sale, recalibras el jefe, nunca la tabla"). Con el eje al techo, `eternal_crown` tiene
por primera vez margen para endurecerse —su celda `buena` sube de 38,9 a 47,7, con 7,7 puntos por encima
de su suelo de 40, que hoy no tiene—, `the_hunt` necesita unos 4 puntos, y **`grimhold_guns` no puede
endurecerse**, porque su celda `incoherente` ya está pegada a su mínimo. Es un paquete de jefe, con banco
propio.

**5. Y queda una segunda palanca del mismo eje, medida y no aplicada** (§5 de este ADR, AN-B): el
instrumento que le dice a la doctrina contextual lo que vale un perk lo mide **con el contador a cero**,
así que puntúa en negativo justo a los perks cuyo valor sólo existe a lo largo de una run
—`clean_sheet_legacy` −42, `scar_veteran` −18, `pit_veteran` −13, `lane_reader` −8, `poacher_instinct`
−6— y `WorthASlot` hace que **la build buena los rechace** mientras la gastadora, que no pregunta, los
compra.

## Qué falsificaría esta decisión

- **Que aparezca un segundo número que sólo la run pueda encender.** El contador es el único hoy; un
  premio que dependa de la **historia** de la plantilla —partidos jugados juntos, vínculos, cicatrices—
  tendría la misma propiedad estructural y sumaría a `S` sin que la oposición pueda cobrarlo.
- **Que el escalón `muy buena` de la ADR 0033 deje de coincidir con la build de la run.** Es lo que hoy
  ata el eje. Si el modelo de build "muy buena" pasara a describir algo que la política no alcanza, la
  tabla dejaría de acotar el eje —pero eso es revisar la tabla, que la ADR 0065 dejó descartado por otra
  razón y que aquí tampoco se propone.
- **Que la oposición pase a jugar runs.** Si un rival o un jefe pudiera llegar al partido con contadores
  cargados, el contador dejaría de cumplir (a) y este ADR se cae entero.
