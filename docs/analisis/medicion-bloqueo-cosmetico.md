# ¿Es cosmético el bloqueo de línea? Medición del 59 %

Informe de **medición con interpretación**. **No se ha cambiado ni una línea del repositorio** salvo este
fichero. No hay commit de código asociado. Responde a la pregunta que el revisor dejó abierta sobre el
dato §2.5 de `docs/analisis/decision-lineas-de-perks.md`:

> «¿Qué perks de la línea cerrada siguen apareciendo y qué función cumplen? Si son perks periféricos,
> perfecto. Si son precisamente los perks que definen la fantasía, entonces el bloqueo es cosmético.»

Etiquetas: **MEDIDO** (leído del código o de `/data`, o salido de un lote de `/Balance`), **DERIVADO**
(conclusión encadenada sobre algo medido), **HIPÓTESIS** (interpretación sin validar).

**Instrumento.** Dos lotes de `--full-runs 400 --seed 1` y `--seed 2` sobre el árbol limpio en `c0853f7`
(2.400 runs: 400 × 3 doctrinas × 2 semillas), más un arnés de **solo lectura fuera del repositorio**
(`ProjectReference` a `/Sim`, un catálogo por hilo) que (a) reconstruye la plantilla **inicial** exacta de
cada run y (b) ejercita `PerkPool` contra los cuatro maestros. Los dos lotes salen sanos:

| | semilla 1 | semilla 2 | banda |
|---|---|---|---|
| `runWinRate` | 25,00 | 25,75 | 20-30 |
| `matchesPerFullRun` | 19,54 | 19,31 | 18-22 |
| `deathsPerRun` | 1,60 | 1,57 | 1,5-3 |
| `masterDivergence` | 16,01 | 18,30 | ≥5 |

---

## 0. Veredicto

**(c) El bloqueo funciona a medias.** Y las dos mitades se separan limpiamente:

- **La mitad que funciona es la corona.** Nada de la línea cerrada se puede adquirir después del maestro:
  **32 de 32** perks bloqueados, por las **dos** vías (mercado y recompensa), en los **cuatro** maestros.
  No es una estimación, es exhaustivo sobre el catálogo. Y lo que se pierde no es un perk cualquiera: el
  maestro concentra el **59 %** (`aim`), el **81 %** (`wall`) y el **76 %** (`craft`) del valor medido de
  toda su línea, y en `aim` y en `wall` es el **único perk de ámbito de equipo** que la línea tiene. Dos
  doctrinas completas son imposibles y eso está bien cerrado. *(MEDIDO)*
- **La mitad que falla es la renuncia.** El **57,1 %** de las runs con maestro terminan llevando piezas de
  la línea que cerraron (media 1,09), el **87,0 %** de esas piezas escriben sobre el **canal propio** de
  esa línea —el **100 %** en `aim`, el **97 %** en `wall`—, y el recuento absoluto de piezas de la línea
  cerrada es **estadísticamente el mismo** que el de las runs que no cerraron nada (1,78 contra 1,98 en
  `aim`; 0,73 contra 0,87 en `wall`), **pese a que la run con maestro lleva casi el doble de perks en
  total** (14,4 contra 7,8). *(MEDIDO)*

**DERIVADO, y es la frase que contesta al revisor:** los perks que se cuelan **no son periféricos** —casi
nueve de cada diez tocan el canal que define la línea— pero **tampoco son los que definen la fantasía**,
porque el definidor es el maestro. El bloqueo separa el **poder** de las dos doctrinas y no separa el
**relato**. Lo que falla es la ficción, no la aritmética: la run que «se atrinchera y renuncia al remate»
termina con 1,78 perks de remate, los mismos que si nunca hubiera renunciado a nada.

---

## 1. Reproducción del 59 %

| | semilla 1 | semilla 2 | agregado |
|---|---|---|---|
| runs | 1.200 | 1.200 | 2.400 |
| con maestro | 466 (38,8 %) | 462 (38,5 %) | 928 (38,7 %) |
| **con perks de la línea cerrada** | **275 (59,0 %)** | **255 (55,2 %)** | **530 (57,1 %)** |
| media de piezas coladas | 1,06 | 1,11 | 1,09 |
| máximo en una run | 7 | 7 | 7 |

La semilla 1 reproduce el dato del informe anterior **cifra a cifra** (59,0 %, media 1,06, máximo 7, sobre
466 runs con maestro). La semilla 2 baja a 55,2 %: el 59 % no es un número exacto, es una banda de
**55-59 %**. *(MEDIDO)*

Distribución de piezas coladas por run con maestro (semilla 1): 0 → 191, 1 → 161, 2 → 60, 3 → 26,
4 → 13, 5 → 9, 6 → 5, 7 → 1. **El 6,0 % de las runs con maestro (8,2 % en la semilla 2) terminan con
cuatro o más piezas de la línea que cerraron**; una llegó a **7 de las 7** piezas no-maestras de la línea
cerrada. *(MEDIDO)*

---

## 2. Qué impide exactamente el código

`blocksPerks` se carga en `Sim/Perks/PerkLoader.cs` (`ParseArc`, líneas 269-279) y se consume en un solo
sitio: `Sim/Run/Systems/PerkPool.cs`. `ClosedBy(state, catalog)` recorre `HeldPerkIds(state)` —la
plantilla **entera**, muertos incluidos (RF-122: el muerto sigue en `Roster`)— y acumula lo que cierran los
maestros que la run lleva puestos. Sobre eso:

| punto de control | qué hace | dónde |
|---|---|---|
| `Offerable(..., PerkSource.Market)` | el perk cerrado **no entra en el surtido del mercado** | `PerkPool.cs:83` |
| `Offerable(..., PerkSource.Reward)` | **no entra en las tres opciones de recompensa** | `PerkPool.cs:83` (misma línea, mismo filtro) |
| `Availability(...)` | devuelve `PerkAvailability.Closed` | `PerkPool.cs:171` |
| `Require(...)` | **lanza** `InvalidOperationException` | `PerkPool.cs:196` |
| consumidor de recompensa | `RewardSystem.cs:219` llama a `Require` antes de asignar | |
| consumidor de mercado | `MarketSystem.cs:144` llama a `Require` antes de cobrar | |

**Medido con el arnés, exhaustivamente**: puesto el maestro en la plantilla, para los 8 perks de la línea
que cierra, en los cuatro maestros —

```
blood_tithe        cierra [craft]:    víctimas=8  Closed=8  fuera del pool (mercado+recompensa)=8  Require lanza (ambas vías)=8
first_touch_school cierra [butchery]: víctimas=8  Closed=8  fuera del pool=8  Require lanza=8
granite_line       cierra [aim]:      víctimas=8  Closed=8  fuera del pool=8  Require lanza=8
killing_range      cierra [wall]:     víctimas=8  Closed=8  fuera del pool=8  Require lanza=8
control sin maestro: familias cerradas=0, perks cerrados=0
```

**Impide las tres cosas a la vez: ofrecer, elegir y comprar.** No impide *equipar* porque no existe
equipar: un perk se cobra sobre un portador en el mismo acto y no se puede retirar (RF-072). *(MEDIDO)*

**Y no hay ninguna otra vía de entrada de perks a la plantilla.** Auditadas todas: los fichajes, los
canteranos y los mercenarios del mercado salen de `GeneratedPlayers.Generate`, que **no asigna ningún
perk**; el jugador de recompensa, igual; `data/events/` no concede perks (cero referencias); los perks de
jefe viven en el equipo rival. El único reparto de perks fuera de `PerkPool` es
`PerkAssignment.AssignInitial`, que corre **una vez, al generar la plantilla del minuto cero**, y que
excluye a los maestros a propósito (`PerkAssignment.cs:49`). *(MEDIDO)*

> **DERIVADO, y cierra el apartado «¿comprados antes o después?»: después del maestro, cero.** No es que
> el lote no lo observe: es que el motor no tiene por dónde. Las 1.008 piezas coladas de los dos lotes
> **preceden todas** al maestro que cierra su línea.

---

## 3. El desglose antes/después

Con «después» descartado por construcción, el reparto que queda es *cuándo antes*:

| origen | semilla 1 | semilla 2 | agregado |
|---|---|---|---|
| ya venían en la **plantilla inicial** | 34 (6,9 %) | 34 (6,6 %) | 68 (6,7 %) |
| **adquiridas durante la run**, antes del maestro | 459 (93,1 %) | 481 (93,4 %) | 940 (93,3 %) |
| adquiridas **después** del maestro | 0 | 0 | **0** |

La plantilla inicial aporta poco por una razón medida: **las 400 plantillas iniciales de cada lote llevan
exactamente un perk cada una** (reconstruidas con `RunEngine.Start` sobre la misma semilla y la misma raza
que la fila de `runs.csv`; 1.200 de 1.200 filas casan de raza, así que la reconstrucción es la buena).
*(MEDIDO)*

> **DERIVADO: la mezcla es legítima en el sentido estricto del reglamento y no lo es en el sentido del
> diseño.** Nadie compra lo que ya no puede comprar. Lo que pasa es que a la altura del acto 2 —el acto
> nativo del maestro (ADR 0051), y solo se compra (ADR 0055)— la run **ya tiene** casi todo lo que iba a
> tener nunca de la línea contraria. La renuncia se firma cuando ya no queda casi nada a lo que renunciar.

**Límite conocido, y no se puede cerrar sin instrumentar `/Sim`** (prohibido por el encargo): *en qué
jugador* está cada pieza colada —titular, suplente o cadáver— no se puede leer de `runs.csv`, que solo da
recuentos. El único indicador disponible es agregado: en las runs con maestro, `starterPerks/perks` vale
**75,5 %** (semilla 1) y **74,4 %** (semilla 2), es decir que **uno de cada cuatro perks de la plantilla
no está en el once**. `HeldPerkIds` recorre `Roster` entero y el muerto se queda dentro (RF-122), con
`deathsPerRun` 1,6 sobre una plantilla de 9-10: parte del 57 % son perks sobre un cadáver. Aplicando ese
75 % plano, la cifra «se cuela en el **once**» estaría en el entorno del **45-52 %** en vez del 57 %.
*(DERIVADO, aproximado; el reparto exacto exige una fila nueva en `RunPlayResult`.)*

---

## 4. Tabla por línea

Runs con **un solo** maestro (las de dos maestros se excluyen para no mezclar dos líneas cerradas).

| maestro | su línea | cierra | runs s1 | con piezas de la cerrada | media | runs s2 | con piezas | media |
|---|---|---|---|---|---|---|---|---|
| `killing_range` | `aim` | `wall` | 269 | 128 (**47,6 %**) | 0,63 | 257 | 112 (**43,6 %**) | 0,65 |
| `granite_line` | `wall` | `aim` | 68 | 53 (**77,9 %**) | **1,81** | 64 | 41 (**64,1 %**) | **1,64** |
| `first_touch_school` | `craft` | `butchery` | 43 | 23 (53,5 %) | 0,70 | 45 | 19 (42,2 %) | 0,76 |
| `blood_tithe` | `butchery` | `craft` | 20 | 14 (70,0 %) | 1,15 | 19 | 15 (78,9 %) | 1,16 |

**El caso peor es `granite_line`, y es el caso que más importa.** Su `_doc` dice: *«quien se atrinchera
renuncia a mejorar el remate»*. Medido: **tres de cada cuatro** runs que se atrincheran terminan con
perks de remate, **1,8 de media**, y los perks de remate de este catálogo suben `shotOnTarget` los ocho.
La frase del fichero de datos no describe lo que pasa. *(MEDIDO)*

`killing_range` sale mejor parado (44-48 %) por una razón que no es el bloqueo: `wall` es la línea de
valor medido más bajo del catálogo (mediana 3,5, con tres piezas a 0) y una política que ordena por valor
no la compra nunca, cierre o no cierre. Ver §6.

---

## 5. Clasificación: ¿definidor o periférico?

Dos criterios medibles, como pedía el encargo.

**Criterio (a), valor relativo a la mediana de su línea** (`data/economy/perk-values.json`, ADR 0087;
`rowDeviation` 7):

| línea | mediana | valor total | del cual el maestro |
|---|---|---|---|
| `aim` | 10,0 | 191 | 112 (**59 %**) |
| `wall` | 3,5 | 142 | 115 (**81 %**) |
| `craft` | 0,0 | 34 | 26 (**76 %**) |
| `butchery` | 3,0 | 25 | 1 (4 %) |

**Criterio (b), canal principal de la línea** (leído de los `effects` de `/data`):

| línea | canal | piezas que lo tocan |
|---|---|---|
| `aim` | `shotOnTarget` | **8 de 8** |
| `wall` | `tackle` | **7 de 8** (solo `safety_net` escribe `save`) |
| `butchery` | `injure`/`severeInjury` | 5 de 8 |
| `craft` | `pass`/`intercept(Evasion)` (el canal del maestro) | 4 de 8; las otras 4 van a `dribble` |

**Resultado sobre las 1.008 piezas coladas realmente observadas:**

| criterio | semilla 1 | semilla 2 | agregado |
|---|---|---|---|
| **tocan el canal propio de su línea** | 440/493 (**89,2 %**) | 437/515 (**84,9 %**) | 877/1.008 (**87,0 %**) |
| — de las que son de `aim` | 162/162 (**100 %**) | 162/162 (**100 %**) | **100 %** |
| — de las que son de `wall` | 221/227 (**97,4 %**) | 207/217 (95,4 %) | **96,4 %** |
| — de las que son de `butchery` | 33/57 (57,9 %) | 48/85 (56,5 %) | 57,0 % |
| — de las que son de `craft` | 24/47 (51,1 %) | 20/51 (39,2 %) | 44,9 % |
| **definidoras** (canal propio **y** valor ≥ mediana de su línea) | 276 (56,0 %) | 262 (50,9 %) | 538 (**53,4 %**) |

Las diez piezas coladas más frecuentes de la semilla 1, con su clase:

| perk | línea | n | valor | mediana de su línea | canal | clase |
|---|---|---|---|---|---|---|
| `last_ditch` | wall | 62 | 8 | 3,5 | `tackle` | **definidor** |
| `own_third_anchor` | wall | 47 | 10 | 3,5 | `tackle` | **definidor** |
| `pit_veteran` | wall | 31 | 4 | 3,5 | `tackle` | **definidor** |
| `game_management` | wall | 31 | 2 | 3,5 | `tackle` | periférico |
| `sharpshooter_drill` | aim | 29 | **26** | 10,0 | `shotOnTarget` | **definidor** |
| `back_to_back` | wall | 28 | 3 | 3,5 | `tackle` | periférico |
| `spearpoint` | aim | 27 | 8 | 10,0 | `shotOnTarget` | periférico |
| `box_predator` | aim | 26 | 6 | 10,0 | `shotOnTarget` | periférico |
| `long_range_menace` | aim | 26 | 10 | 10,0 | `shotOnTarget` | **definidor** |
| `bulwark_stance` | wall | 22 | 0 | 3,5 | `tackle` | periférico |

> **DERIVADO: los dos criterios no dicen lo mismo, y la diferencia es exactamente la respuesta.** Por
> **canal**, el 87 % de lo que se cuela es material de la doctrina que se cerró, y en las dos líneas que
> mueven el juego (`aim`, `wall`) es el 100 % y el 96 %: *no hay perks periféricos que colar*, porque
> `aim` es ocho maneras de rematar mejor y `wall` siete de robar mejor. Por **valor**, lo que se cuela son
> piezas de 0 a 10 milésimas mientras la corona vale 112-115. El bloqueo **retira el 95 %** del valor de
> la línea (§6) y **no retira casi nada de su identidad**.
>
> Los `_doc` de los cuatro maestros describen el mecanismo por su identidad, no por su valor («renuncia a
> mejorar el remate», «renuncia a lesionar»). Medida contra esa promesa, la exclusión **no se cumple**.

---

## 6. Cuánto suprime de verdad el bloqueo (control)

La medida que decide. Dos controles independientes, los dos sobre piezas **no-maestras**:

**Control A, dentro de la misma run** (la línea cerrada contra una línea *neutra* —ni la propia ni la
cerrada— de esa misma run, que nadie bloquea):

| | semilla 1 | semilla 2 |
|---|---|---|
| línea **propia** del maestro | 3,63 | 3,67 |
| línea **cerrada** | **0,90** | **0,91** |
| línea **neutra** (control) | 0,98 | 0,96 |
| **supresión** | **9 %** | **5 %** |

**Control B, contra las runs que no cerraron nada** (n = 734 y 738):

| línea | sin maestro s1 | como cerrada s1 | supr. | sin maestro s2 | como cerrada s2 | supr. |
|---|---|---|---|---|---|---|
| `aim` | 1,98 | 1,78 | **10 %** | 1,82 | 1,74 | **4 %** |
| `wall` | 0,87 | 0,73 | 16 % | 0,73 | 0,71 | 3 % |
| `butchery` | 0,61 | 0,68 | −10 % | 0,64 | 0,85 | −33 % |
| `craft` | 0,90 | 1,04 | −16 % | 0,88 | 1,24 | −41 % |

Y el detalle que convierte esto en una acusación: **la run con maestro lleva 14,4 perks de plantilla y la
run sin maestro 7,8** (semilla 1; 15,2 contra 7,6 en la 2). Una run que **casi duplica** su cosecha de
perks termina con **el mismo número absoluto** de piezas de la línea que juró no volver a tocar —y en
`butchery` y `craft`, con más. *(MEDIDO)*

Lo único que el bloqueo sí retira, y lo retira entero:

- el **maestro contrario**, imposible por construcción (§2);
- en valor medido, la run conserva de media el **4,9 %** (s1) y el **4,4 %** (s2) del valor catalogado de
  la línea que cerró. El 95 % que falta **es el maestro**, no las piezas. *(MEDIDO)*
- solo el **5,0 %** de las runs con maestro terminan con más piezas de la línea cerrada que de la suya
  propia (maestro aparte); el 8,6 % con tantas o más. La build sigue estando escorada a su doctrina.
  *(MEDIDO)*

---

## 7. Qué se responde y qué no

**Se responde:**

1. El 59 % se reproduce (59,0 % / 55,2 %; banda 55-59 %).
2. El código impide **ofrecer, elegir y comprar**, en las dos vías y en los cuatro maestros, 32 de 32
   perks. No existe otra vía de entrada de perks a la plantilla.
3. Reparto: **0 % después del maestro**, 93,3 % adquiridas durante la run antes de él, 6,7 % venían en la
   plantilla inicial.
4. **El 87 % de lo colado escribe en el canal propio de la línea cerrada** (100 % en `aim`, 96 % en
   `wall`); el 53 % son definidoras por el criterio conjunto canal+valor.
5. Por línea: `granite_line` 78 %/64 % con 1,8 piezas; `blood_tithe` 70 %/79 %; `first_touch_school`
   54 %/42 %; `killing_range` 48 %/44 %.
6. El bloqueo suprime entre el **3 % y el 16 %** de las piezas de la línea cerrada, y **el 95 % de su
   valor**, que es el maestro.

**No se responde, y por qué:**

- **Titular o suplente.** `runs.csv` no lleva ids por jugador y el encargo prohíbe tocar `/Sim`. Proxy
  agregado: 75 % de los perks de plantilla están en el once. Una fila nueva en `RunPlayResult`
  (`finalPerksByStarter`) lo cerraría en una tarde.
- **El acto en que se colaron.** `perkHorizon` agrega por acto y capa sin ids. Importa porque el precio de
  la exclusión decae con el acto (§2.5 del informe anterior) y aquí no se ha podido cruzar.
- **Si el jugador humano se comportaría igual.** Todo esto es la política automática de la ADR 0037, que
  compra por valor medido y publicado. Un humano que persiga la fantasía en vez del número podría cerrar
  antes y colar menos. *(HIPÓTESIS; el experimento que la falsaría es una doctrina de política que compre
  solo dentro de su línea desde el acto 1.)*

---

## 8. Reproducción

```bash
dotnet build Underleague.slnx -c Release -m:1 -v q
dotnet run --project Balance -c Release -- --full-runs 400 --seed 1 --out <tmp>/out1 --quiet
dotnet run --project Balance -c Release -- --full-runs 400 --seed 2 --out <tmp>/out2 --quiet
```

El arnés de solo lectura (proyecto de consola fuera del repositorio con `ProjectReference` a
`Sim/Sim.csproj`) tiene dos modos: `initial <semilla> <n>` vuelca los perks de la plantilla inicial de
cada run reconstruida con `RunEngine.Start`, y `block` ejercita `PerkPool.ClosedBy` / `Offerable` /
`Require` contra los cuatro maestros. Las tablas salen de cruzar `runs.csv` (columnas `seed`, `race`,
`masters`, `finalPerks`, `perks`, `starterPerks`) con `data/perks/*.json` y
`data/economy/perk-values.json`.
