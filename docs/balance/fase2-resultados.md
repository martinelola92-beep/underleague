# Fase 2: resultados de balance (paquete Z)

> **Superado en parte por el paquete AA** (`docs/fase2-diseno.md` §18), que aplica las ADR 0036, 0038,
> 0039 (escala), 0040 y 0041 y vuelve a medirlo todo. Lo que sigue es el estado **antes** de esas cinco
> ADR y se conserva como registro de la medición que las motivó: las cifras de curva de puertas,
> economía, lesiones y doctrinas de este documento **ya no son las vigentes**. Las vigentes están en
> §18.5 (curva), §18.4 (lesiones y `tuning.json`) y §18.6 (doctrinas y métricas de escasez).

Cierre del bucle de run. Qué se midió, con qué política, qué cumple su rango y qué no —y por qué, con
números—. La medida de referencia es **500 runs por doctrina, semilla 1**, cinco razas de lanzamiento
repartidas por igual: 1.500 runs y 15.853 partidos en 89 s. Reproducible con:

```bash
dotnet run --project Balance -c Release -- --full-runs 500 --seed 1
```

La puerta automatizada es `Sim.Tests/Analysis/FullRunGateTests.cs` (categoría `Gate`), que juega el
mismo lote con 60 runs por doctrina en 14 s.

---

## 1. La política automática

`Sim.Analysis.RunPolicy`. No pretende jugar bien: pretende ser **legible y reproducible**, para que un
cambio en la economía se lea en la métrica y no en el criterio de quien mide (§10). Es pura y
determinista: mismo (setup, semilla, catálogo, doctrina) produce la misma run, y hay un test que lo
afirma.

### 1.1. Las siete reglas comunes

| # | Regla | Cómo decide |
|---|---|---|
| 1 | **Qué nodo** | Clínica si hay un lesionado grave sin tratar y el oro la cubre; si no, mercado; entre partidos, el de élite solo con 8 disponibles o más y si no el de menor dificultad; entre servicios, el evento si el oro no llega a una clínica y si no el entrenamiento. A igualdad, el id menor (RT-041) |
| 2 | **Quién juega** | Los siete de más *valor* por rol (1 POR, 2 DEF, 3 MED, 1 DEL, y el resto por valor). Valor = suma de los cinco atributos + 10 por perk + 8 si lleva objeto |
| 3 | **Cuándo se arriesga a un lesionado grave** | Cuando no hay siete disponibles, o cuando el oro no cubre su tratamiento y aun así es mejor que el suplente al que sustituiría. Quien está con lesión grave **ya no cuenta** para el mínimo de RF-002b, así que alinearlo no acerca la derrota por plantilla: lo que arriesga es perderlo para siempre (RF-093 vía 1) |
| 4 | **Clínica** | Trata al lesionado grave de más valor mientras los disponibles sean menos de 8 y el oro alcance |
| 5 | **Mercado** | Canteranos gratis mientras la plantilla no llegue a 13; luego un perk para un titular, luego un objeto para un titular sin objeto, luego un fichaje que mejore en atributos al titular más flojo, y un mercenario solo si faltan cuerpos. Vende al suplente de menos valor —nunca un canterano, un titular ni un mercenario— solo para hacer sitio a un fichaje. **Nunca compra consumibles**: el estado no lleva inventario (X-9), así que equiparlos no exige haberlos comprado y pagarlos es tirar oro |
| 6 | **Recompensa** | Prefiere el perk para un titular; luego el objeto para un titular sin objeto; luego el jugador |
| 7 | **Reroll** | Cuando ninguna de las tres opciones es un perk para un titular ni un objeto para un titular sin objeto, y el oro reservable cubre tres veces su coste |

Dos reglas transversales: mientras haya un lesionado grave sin tratar, la política **reserva** el precio
de la clínica y no lo gasta en el mercado; y **un perk solo se compra si encaja en su portador**
(`Sim.Analysis.PerkPlacement`), es decir si la parte de su condición que depende de la plantilla y de la
colocación —`hasTag`, `startsIn`, `startsOn`, `linked`, `teammatesWithTag` sobre `owner`— se cumple con
el once actual. Lo que mira al `actor`, al marcador o al reloj no es juzgable fuera del partido y se da
por bueno: la regla solo rechaza cuando está segura.

### 1.2. Las tres doctrinas de compra (ADR 0037)

Lo **único** que cambia entre las tres políticas es la regla 5 y el umbral de la 7, para que la
diferencia de tasa de victoria sea atribuible a la decisión de comprar y a nada más.

| Doctrina | Presupuesto | Listón | Reroll |
|---|---|---|---|
| **Gastadora** | todo el oro, sin reserva | ninguno: el artículo más barato que mejore **a alguien**, titular o suplente | en cuanto puede pagarlo |
| **Ahorradora** | todo el oro reservable | solo **raro o legendario**; los comunes no pasan | nunca |
| **Contextual** | todo el oro reservable | lo que le **falta al once**, prefiriendo el raro dentro del presupuesto; reparte los perks al titular con menos perks | con holgura (3× su coste) |

---

## 2. La pregunta de la ADR 0033: ¿la economía permite construir la build que cada puerta exige?

La curva de puertas (§16.6) demuestra que *una build muy buena* pasa el jefe final. Lo que faltaba por
medir es si la economía deja llegar a ella. Medido sobre las runs de la política contextual, agrupadas
por el acto en el que terminan —es decir, la plantilla con la que se llegó a esa puerta o se salió de
ella—:

| Puerta | Perks en el once | Objetos | Nivel medio | Contadores acumulados | Superada |
|---|---|---|---|---|---|
| **Jefe del acto 1** | **4,3** | 1,8 | 3,5 | 3,3 | **47,6%** |
| **Jefe del acto 2** | **9,7** | 3,6 | 5,4 | 12,1 | **43,3%** |
| **Jefe final** | **13,9** | 6,1 | 6,8 | 30,8 | **63,1%** |

Y la misma fila contra la curva de puertas, **remedida al cerrar el paquete Z** (semilla 1, 32
plantillas × 4 partidos por celda y raza, 640 partidos por celda, 7.680 en total, 34 s) para comprobar
que los ocho perks nuevos de RF-070 no la mueven: sale **idéntica** a la de `fase2-diseno.md` §16.6,
porque las builds de la puerta no los usan. Las doce celdas siguen dentro de su banda.

| Puerta | Incoherente | Correcta | Buena | Muy buena | **Run real** |
|---|---|---|---|---|---|
| **Acto 1** `grimhold_guns` | **7,8** (< 25) | **58,4** (45-60) | **68,1** (60-75) | **71,4** (70-85) | **47,6** |
| **Acto 2** `the_hunt` | **6,9** (< 15) | **37,2** (30-45) | **56,6** (55-70) | **73,1** (65-80) | **43,3** |
| **Acto final** `eternal_crown` | **6,2** (< 10) | **26,1** (15-30) | **45,3** (35-50) | **56,6** (55-70) | **63,1** |

**La respuesta es: la economía llega tarde.** Al jefe final se llega **por encima** del escalón "muy
buena" (63,1 frente a 56,6): trece o catorce perks en el once, objeto en seis de siete, contadores de
acumulación cerca del tope y nivel 7. Pero al jefe del **acto 1** se llega **por debajo de "correcta"**
—4,3 perks en el once frente a los 14 que lleva `*_correct`— y al del acto 2 justo en "correcta". La
build correcta que la ADR pide **antes** del primer jefe no se puede construir, y el problema es
aritmético, no de calibración:

- el acto 1 tiene **6 nodos de partido** (5 de liga o élite más el jefe) y **3 mercados**;
- se ganan de media **4 partidos**, o sea 4 tiradas de recompensa, de las que el 65% son de perk: **2,6
  perks** de recompensa;
- la plantilla inicial entra con **1 o 2 perks** en total (RF-023: un común entra con cero, un raro con
  uno; RF-005 da un solo jugador de rareza superior);
- y en tres mercados, con el oro del acto (**396** de media, **304** en las runs que se quedan ahí) y los precios que la escasez de la ADR 0037 exige,
  caben **1 o 2 compras por visita**, de las que solo una parte son perks: **2,2 perks comprados por
  run entera**.

Suma: entre **4 y 6 perks** en el once al llegar al primer jefe. Para llegar con 14 harían falta 4
compras de perk por mercado, que es exactamente lo contrario de lo que la ADR 0037 pide.

**Recomendación**: el club inicial tiene que traer una build, no una bolsa de comunes sin perks. Es lo
único que cierra el hueco sin romper la escasez, y toca RF-023/RF-005, así que **exige un ADR**. Las
alternativas —más partidos en el acto 1 (choca con RF-003b), más de una recompensa por victoria (choca
con RF-071), o abaratar los perks (choca con la ADR 0037)— empeoran otra cosa.

### 2.1. Y aunque se cerrara, la banda 25-40% no cabe

Con la tabla de la ADR 0033, la tasa de victoria de una run es el producto de las tres celdas:

| Trayectoria | Producto |
|---|---|
| Correcta en las tres puertas | 0,584 × 0,372 × 0,261 = **5,7%** |
| Buena en las tres | 0,681 × 0,566 × 0,453 = **17,5%** |
| **Muy buena en las tres** | 0,714 × 0,731 × 0,566 = **29,5%** |
| Trayectoria que la ADR describe (buena → buena/muy buena → muy buena) | **21,8% a 28,2%** |

El **techo** de la banda de §10 (40%) está por encima de lo que la propia curva permite aunque se
juegue perfecto, y su punto medio (32,5%) solo se alcanza con una build muy buena en las **tres**
puertas, lo que contradice el texto de la ADR ("una build buena antes del primer jefe... solo una muy
buena termina la run"). **La banda coherente con la ADR 0033 es 20-30%, no 25-40%.** Cambiarla exige un
ADR; se deja anotado y no se fuerza.

Medido hoy: **13,0%**, con la trayectoria "por debajo de correcta → correcta → muy buena".

---

## 3. Métricas de run (§10) y de escasez (ADR 0037)

500 runs por doctrina, semilla 1. La columna *estado* es la de `summary.csv`.

| Métrica | Rango | Medido | Estado |
|---|---|---|---|
| Tasa de victoria de la run (contextual) | 25-40% | **13,0** | OUT |
| Derrotas por bajar de 5 jugadores | < 35% de las derrotas | **0,0** | IN |
| Duración de una run completa | 18-22 partidos | **20,0** | IN |
| Muertes por run | 0,5-2 | **0,00** | OUT |
| Sumideros que paga el oro de un acto (RF-114k) | 2-3, nunca 4 | **2,40** (los cuatro: 0% de los actos) | IN |
| Fracción del surtido asequible al llegar al mercado | 20-35% | **40,5** | OUT |
| Compras por visita al mercado | 1-2 | **1,43** | IN |
| Oro sobrante al terminar la run | < 15% del ganado | **23,2** | OUT |
| Runs que llegan a un mercado sin poder comprar nada | 10-25% | **49,2** | OUT |
| Ventaja de la contextual sobre las dos puras | ≥ 8 puntos | **0,8** | OUT |

### 3.1. Las tres doctrinas

| Doctrina | Jefe 1 | Jefe 2 | Jefe final | **Run** | Compras/mercado | Oro sobrante |
|---|---|---|---|---|---|---|
| **Contextual** | 47,6 | 43,3 | 63,1 | **13,0** | 1,43 | 23,2% |
| **Gastadora** | 42,2 | 39,3 | 48,2 | **8,0** | 1,47 | 23,5% |
| **Ahorradora** | 48,6 | 41,6 | 60,4 | **12,2** | 1,00 | 37,4% |

Lectura, con la propia rúbrica de la ADR 0037: **la contextual gana claramente a la gastadora (+5,0
puntos) y empata con la ahorradora (+0,8)**. Es decir, *elegir bien a quién le das lo que compras* vale
cinco puntos de run —la gastadora compra lo mismo pero se lo da a suplentes—, mientras que *comprar o
no comprar* apenas mueve la aguja. En los términos de la ADR: **la ahorradora no está por debajo, así
que comprar todavía no compensa lo suficiente**.

La causa está identificada y es externa a la economía: **un objeto equipado no vale casi nada hoy**. La
medida de la costura 16.2 (Z-6) fue de −0,0 / −0,6 / −6,4 puntos según el jefe, y `EquipmentImpactTests`
mide +5 puntos para los siete titulares equipados sobre un espejo. La **ADR 0036** reescribe el
equipamiento a bonos de atributo (+10 por atributo, 1/2/3 atributos según rareza, ~0,55/1,1/1,65 puntos
de tasa de victoria por objeto) y todavía **no está implementada**: `data/items/*.json` sigue en el
formato de efectos condicionales. Con ella, siete titulares equipados valen entre 4 y 11 puntos y el
mercado pasa a mover la run de verdad. **La ventaja de 8 puntos de la ADR 0037 no es medible hasta que
la ADR 0036 esté aplicada**, porque el sumidero más elástico —el equipamiento, que la propia ADR 0037
llama "la palanca fina de esta curva"— hoy no compra nada.

Además, la diferencia entre doctrinas tiene una desviación de unos **2 puntos con 500 runs** y de **5,5
con 60**: medida con 60 runs, la ventaja de la contextual salta de +8,3 (semilla 1) a −3,3 (semilla 7).
El criterio de la ADR 0037 necesita lotes de 500 runs por doctrina como mínimo; por eso la puerta
automatizada **no** lo afirma.

### 3.2. Las condiciones 1 y 2 de la ADR 0037

- **El dilema es informado, no ciego.** El mapa enseña dónde están los mercados desde que empieza el
  acto y RF-011b garantiza uno cada tres capas; `RunPolicy.MarketsLeftInAct` es exactamente el dato con
  el que la doctrina contextual decide, y sale del mapa, no de la semilla del surtido. La apuesta es "no
  sé qué saldrá", nunca "no sé si volveré a ver una tienda". ✔
- **Arruinarse no es irreversible.** Las tres vías de recuperación existen y se ejercitan: canteranos
  gratuitos (3,2 por run), venta de suplentes (0,3 por run, 39 de oro) y reroll. Ninguna run del lote
  terminó por quedarse sin plantilla, y ninguna quedó bloqueada sin poder comprar durante todo un acto.
  ✔

---

## 4. Las cinco métricas que no cumplen, con su causa

**4.1. Tasa de victoria 13,0% (banda 25-40).** Dos causas sumadas, las dos con número en §2: la build
llega tarde (4,3 perks en el once al primer jefe frente a los 14 de `*_correct`) y la banda es más alta
de lo que la curva de la ADR 0033 permite (techo real 29,5%, trayectoria descrita 21,8-28,2%). Ninguna
de las dos se arregla con la economía sola.

**4.2. Muertes por run 0,00 (banda 0,5-2).** No es la política: es que **en el bucle de run casi no hay
lesiones**. Medido: **0,04 lesiones propias por partido**, contra las 0,62 por partido (los dos equipos)
que RT-056 mide en el lote de referencia de fase 1. La causa es la fórmula de `tuning.injury`:

```
probabilidad = 40 + (falta ? 110 : 0) + 5·(fuerza del que entra − 50) − 5·(resistencia de la víctima − 50)
```

Los dos términos se miden **contra 50**, que es el nivel 1. La progresión suma 2 puntos por nivel a todos
los atributos (RF-027), así que un titular de nivel 6 tiene ~62 de resistencia y resta **60** puntos
básicos sobre una base de 40: la entrada limpia se acota a cero y solo la falta (110) sigue lesionando.
Un equipo que sube de nivel se vuelve **inmune a las lesiones**, que es justo lo contrario de la
identidad del juego ("carnicería administrada"). Consecuencias medidas: 0,21 lesiones graves por run,
**cero tratamientos de clínica** en 500 runs y cero muertes.

Las dos vías de muerte de RF-093 están habilitadas en el motor y la política ejercita la primera
(alinea a un lesionado grave cuando no puede pagar su clínica), pero con 0,2 lesiones graves por run no
hay material. **No se ha tocado `data/sim/tuning.json`**: es un valor global de fase 1 y moverlo
recalibra RT-056 y las puertas de fase 1 y 2 a la vez. La corrección correcta es hacer la fórmula
**relativa** —comparar al que entra con su víctima en vez de a los dos con 50—, y es un reajuste con su
propio ADR (RT-057).

**4.3. Fracción asequible del surtido 40,5% (banda 20-35).** El surtido son 15-16 artículos, de los que
2 o 3 son gratis (canteranos y mercenario) y por tanto siempre "asequibles": el suelo estructural de la
métrica es ~18%. El resto son 13 artículos de pago repartidos en tres rarezas, y con el oro típico al
llegar a un mercado (**213**) casi todos los comunes caben. Se ha añadido `market.priceSpreadPercent`
(70%) para que el precio de cada artículo se disperse **dentro** de su rareza: sin dispersión la
fracción asequible salta de 0 a 1 con el oro y la métrica no tiene ningún valor intermedio. Con
dispersión baja de 82% a 40%. Bajarla más exige subir los precios otra vez, y eso empuja hacia arriba
la métrica 4.5 (quedarse sin poder comprar), que ya está fuera por el otro lado: **las dos métricas se
oponen y no hay configuración que cumpla las dos con el catálogo actual**.

**4.4. Oro sobrante 23,2% (techo 15).** La plantilla se satura: catorce slots de perk en un once de
comunes (RF-023) y siete objetos. Cuando el once está lleno, la política no tiene qué comprar y el oro
se queda quieto —180 de oro de media al terminar—. El sumidero que debería absorberlo es el fichaje de
pago, y por eso los fichajes y las recompensas de jugador entran ahora **con el nivel del acto**
(`economy.recruitLevelByAct = [1, 4, 6]`): un fichaje de nivel 1 en el acto 3 era oro tirado, porque
ningún criterio razonable alinea a un nivel 1 junto a titulares de nivel 7. Aun así, con los precios
que la escasez exige, un fichaje raro cuesta 730 y la run rara vez llega a pagarlo (0,09 fichajes por
run). Con la ADR 0036 aplicada el equipamiento vuelve a ser un sumidero real y esta métrica debería
bajar sola.

**4.5. Runs que llegan a un mercado sin poder comprar nada 49,2% (banda 10-25).** La métrica cuenta
runs con **al menos una** visita en blanco, y una run visita entre 3 y 9 mercados: por visita la cifra
es del **9%**, dentro de lo razonable. Con 5,4 visitas de media, 9% por visita da 40-50% por run casi
mecánicamente. O la métrica se mide por visita, o la banda por run tiene que subir a 35-60%; se deja
anotado en `pendientes.md` en vez de forzar el número.

---

## 5. La economía, acto a acto

Valores finales en `data/economy/economy.json`. Lo que se movió respecto del paquete X y por qué:

| Clave | X | Z | Motivo |
|---|---|---|---|
| `startingGold` (nuevo) | — | 80 | El club empieza con lo justo para un artículo; antes lo ponía el llamador |
| `goldAct1/2/3` | 60/90/130 | 95/105/120 | Curva más plana: los precios no escalan con el acto, así que el oro tampoco puede |
| `clinicCost` | 90 | 180 | Un tratamiento tiene que competir con una compra, no ser calderilla (RF-114k) |
| `mercenaryBaseWage` / `…PerRarityStep` | 10/8 | 16/12 | Ídem: el salario es un sumidero, no un redondeo (D-3) |
| `market.perkPriceByRarity` | 35/75/160 | 270/570/1140 | Los precios se calibran **contra el oro del acto** (ADR 0037): con 395 de oro en el acto 1 y tres mercados, un común es media visita |
| `market.itemPriceByRarity` | 30/70/150 | 240/510/1020 | Ídem, un escalón por debajo del perk |
| `market.playerPriceByRarity` | 45/95/190 | 350/730/1460 | Un fichaje es la compra grande del acto |
| `market.consumablePrice` | 15 | 150 | Era el artículo que hacía asequible el surtido entero |
| `market.priceSpreadPercent` (nuevo) | — | 70 | Sin dispersión dentro de la rareza no hay fracción asequible intermedia (§4.3) |
| `market.perkOffers` / `itemOffers` / `playerOffers` | 3/3/1 | 4/4/3 | Surtido de 15-16 artículos, que es lo que la ADR 0037 describe |
| `recruitLevelByAct` (nuevo) | — | 1/4/6 | Un fichaje de nivel 1 en el acto 3 no lo alinea nadie (§4.4) |
| `rewardPerkWeight` / `PlayerWeight` / `ItemWeight` | 40/30/30 | 65/10/25 | La recompensa es la única fuente de build que no cuesta oro: se inclina hacia el perk |
| `market.recruitQuality` / `youthQuality` | 52/22 | 58/30 | Un fichaje que no mejora a nadie no es un sumidero; un canterano de calidad 22 no llega a nada ni en el acto 3 (RF-114c) |
| `market.playerSaleBaseByRarity` y escalones | 20/50/110, 6/12 | 80/175/360, 14/26 | Vender es una de las tres vías de recuperación de la ADR 0037 y tenía que valer algo con los precios nuevos |

Y lo que la run produce con esos valores (política contextual, media por acto sobre las runs que juegan
ese acto):

| Acto | Partidos | Mercados | Oro ganado en el acto | Oro sin gastar al terminar la run ahí | Compras acumuladas | Perks en el once | Objetos |
|---|---|---|---|---|---|---|---|
| 1 | 6 | 3 | 396 | 123 | 4,9 | 4,3 | 1,8 |
| 2 | 7 | 3 | 539 | 201 | 8,0 (acumuladas) | 9,7 | 3,6 |
| 3 | 7 | 3 | 604 | 296 | 11,9 (acumuladas) | 13,9 | 6,1 |

**Uso real de los cuatro sumideros** (por run, política contextual): mercado **674** de oro, clínica
**0**, rerolls **2,8**, salarios **0**. RF-114k se cumple en el sentido en que está escrito —el oro de
un acto paga 2,40 sumideros de media y **nunca los cuatro**— pero conviene decir lo que la cifra
esconde: **en la práctica solo se usa uno**. La clínica no se usa porque casi no hay lesiones graves
(§4.2); los mercenarios no se usan porque con trece jugadores nunca faltan cuerpos; el reroll casi no se
usa porque la política solo lo gasta cuando ninguna de las tres opciones sirve. Dos de los cuatro
sumideros son hoy **contenido muerto**, y el primero lo es por una causa de fase 1.

---

## 6. Causas de derrota

500 runs, política contextual:

| Causa | Runs | % de las derrotas |
|---|---|---|
| Perder contra un jefe (RF-002b vía 1) | 435 | **100,0%** |
| Bajar de 5 jugadores disponibles (RF-002b vía 2) | 0 | 0,0% |
| Victoria | 65 | — |

| Dónde termina la run | % de runs |
|---|---|
| Cae en el jefe del acto 1 | 52,4 |
| Cae en el jefe del acto 2 | 27,0 |
| Cae en el jefe final | 7,6 |
| Gana la run | 13,0 |

Las tres puertas hacen de puertas y ninguna otra cosa mata: es exactamente lo que la ADR 0033 pide del
esqueleto de dificultad. La contrapartida es que la segunda vía de derrota de RF-002b —quedarse sin
plantilla— **no se ejercita nunca**, por la misma razón que no hay muertes (§4.2).

Tasa de victoria por raza (100 runs cada una): enanos 14,0 · orcos 16,0 · no-muertos 13,0 · elfos 11,0 ·
humanos 11,0. Cinco puntos de dispersión, la mitad que la que la puerta de razas admite.

---

## 7. Conclusiones para el revisor

1. **La economía llega tarde, no llega corta.** Al jefe final la run llega por encima del escalón "muy
   buena" de la ADR 0033; al primero llega por debajo de "correcta". El hueco es del acto 1 y es
   aritmético: con 6 partidos, 3 mercados y una plantilla inicial sin perks no caben 14 perks en el
   once. **Lo que hay que decidir es si el club inicial trae una build** (toca RF-023/RF-005 y exige un
   ADR).
2. **La banda 25-40% de §10 no es compatible con la tabla de la ADR 0033.** El producto de las tres
   celdas "muy buena" es 29,5%; la trayectoria que la propia ADR describe da 21,8-28,2%. La banda
   coherente es **20-30%**.
3. **El criterio de la ADR 0037 no se puede medir todavía.** La contextual gana a la gastadora por 5
   puntos, pero empata con la ahorradora, y la causa es que el equipamiento no vale nada hasta que se
   aplique la **ADR 0036**. Es el bloqueo más importante que este paquete deja abierto: implementarla y
   volver a medir.
4. **Las lesiones han desaparecido del bucle de run** (0,04 por partido, frente a 0,62 en el lote de
   fase 1) porque la fórmula de lesión se mide contra el nivel 1 y la progresión sube la resistencia.
   Con ellas se van la clínica, las prótesis futuras, las muertes y media identidad del juego. Es un
   defecto de fase 1 que solo se ve jugando runs completas.
5. **Dos de los cuatro sumideros son contenido muerto** (clínica y mercenarios), consecuencia directa
   del punto anterior y del tamaño de plantilla.
6. **La escasez y el "poder comprar algo" se oponen**: bajar la fracción asequible del surtido sube las
   visitas en blanco. Con el catálogo actual no hay configuración que cumpla las dos bandas de la ADR
   0037 a la vez; la dispersión de precio dentro de la rareza es lo que las acerca y ya está aplicada.
