# Los tres puntos abiertos del análisis de builds — resueltos

Cierra los tres puntos que `builds-analisis-sistemico.md` dejó como accionables. Etiquetas: **MEDIDO**,
**DERIVADO**, **HIPÓTESIS**.

---

## 1. El instrumento no podía medir 16 de las 49 builds — **ARREGLADO**

**Causa** *(DERIVADO)*: `Balance/BuildBatchRunner.cs` construía los equipos con
`ToTeamSetup(ref rng, catalog, idBase)`, sin pasar el `ItemCatalog`. El parámetro existía con valor por
defecto `null` y `BuildConfig.ToTeamSetup` lanza si la build declara objetos, así que **cualquier build con
equipamiento abortaba el lote**: las cinco referencias neutras, los cinco escalones «excelente» y las seis
incoherentes. La puerta de fase 1 no lo sufría porque usa **otra implementación** (`Sim.Tests/Analysis/BuildFile.cs`),
que sí lo pasa: había dos caminos paralelos de build→equipo y el del CLI iba por detrás.

**Arreglo**: se hila el catálogo desde `Program.cs` —que ya lo cargaba para `--item-values`— hasta los tres
`ToTeamSetup` del runner, con `ItemCatalog? items = null` opcional en `RunMatrix`, `RunCampaign` y
`PlayCampaign`. Cuatro parámetros y cinco líneas; **ningún cambio de motor ni de datos**.

**Verificado** *(MEDIDO)*: `--builds dwarf_excellent,dwarf_neutral` abortaba y ahora produce `builds.csv` y
`perks.csv`. 689/689 unitarios en verde.

**Qué desbloquea**: los tres experimentos que el informe anterior tuvo que dejar en DATOS INSUFICIENTES —
sinergias, objeto A contra objeto B, y build completa contra build neutra— más el escalón «excelente» de la
ADR 0033, que nunca se había podido medir desde el CLI.

---

## 2. `RF-104` sin implementar — **el diagnóstico era incompleto: falta la familia entera**

Lo que el informe anterior decía: `ImmunityKind.Mourning` se concede y no la lee nadie.

**Lo que hay de verdad** *(MEDIDO, por búsqueda exhaustiva en `Sim/`, `Game/` y `Balance/`)*: no es que
falte el duelo. **Falta todo el sistema de vínculos, RF-100 a RF-106.**

| evidencia | qué demuestra |
|---|---|
| `new RunBond(...)` aparece **una sola vez** en todo el repositorio, en `RunSave.cs:548` —el **cargador** del guardado | **Nada crea un vínculo.** `Bonds` nace vacío y muere vacío |
| `BondProgress` solo se escribe y se lee en `RunSave.cs` | El progreso hacia un vínculo **no se acumula nunca** |
| `Game/` no contiene ni una referencia a `Bond` | **RF-106** (líneas entre retratos) sin implementar |
| `RunState.Mourning` solo aparece en serialización | **RF-104** sin implementar |
| `MarketSystem.cs:106`: `PlayerSalePerBond * player.Bonds.Count` | Hay un **precio** que multiplica por un número que siempre vale 0 |

*(DERIVADO)* El sistema está **declarado, serializado y tarifado**, y no ocurre. Los tipos (`BondKind`:
`Partnership`, `BloodDebt`, `Stonewall`), el estado, el guardado y hasta el efecto sobre el precio de venta
están escritos; lo único que falta es el sistema que los cree.

**Consecuencia sobre `numb`** *(DERIVADO)*: la habilidad racial de los no-muertos concede inmunidad al
duelo. Como el duelo no puede ocurrirle a nadie, **esa mitad de la habilidad es inmunidad a algo imposible**,
y su descripción generada (RT-035) promete una protección que el motor no puede dar.

**Por qué esto NO se arregla aquí.** Implementar RF-104 aislado es imposible: el duelo lo dispara un vínculo
que nunca existe. Implementar la familia entera es **una funcionalidad nueva**, no un arreglo: hay que
decidir qué eventos forman cada tipo de vínculo (RF-100 exige que sean «concretos y verificables»), y la
**magnitud de la penalización de duelo no está especificada en ningún sitio** —RF-104 dice «con penalización
de atributos» y no da número—, así que implementarlo obliga a inventar un valor de balance.

**Decisión del revisor**, con tres salidas y su coste:

| salida | qué implica |
|---|---|
| **A — implementar la familia** | Funcionalidad de fase 3. Hay que diseñar los disparadores de los tres tipos de vínculo y elegir la penalización de duelo. Toca economía (el precio de venta deja de multiplicar por cero) y el desgaste de plantilla, que es el recurso central |
| **B — retirar RF-100..106 del alcance de lanzamiento** | Coherente con RF-103, que ya aplaza las rivalidades. Obliga a quitar la inmunidad al duelo de `numb` —que entonces se queda con una sola mitad— y a limpiar el término de precio |
| **C — dejarlo como está** | El coste es el que este informe acaba de medir: requisitos que el código declara cumplir y no cumple, y una habilidad racial que promete lo que no puede dar |

**Recomiendo B o A, no C.** C es la única que degrada con el tiempo, porque cada informe futuro volverá a
descubrir lo mismo.

---

## 3. La ADR 0038 contra los valores medidos — **no era una contradicción; era una comparación mal hecha (mía)**

**Lo que el informe anterior afirmó**: «el orden de valor por atributo es el inverso del declarado».

**Eso estaba mal, y el error era mío** *(corregido en el informe)*. Los dos números miden **experimentos
distintos**:

| | qué mide | unidad | resultado |
|---|---|---|---|
| Tabla de la ADR 0038 (`fase1b-resultados.md`) | **+20 al atributo en TODA la plantilla** | puntos de tasa de victoria | fuerza 11,1 · técnica 7,5 · velocidad 6,6 · resistencia 3,0 |
| `data/economy/item-values.json` (`--item-values`) | **un objeto equipado a UN jugador**, contra su espejo sin él | misma unidad que los perks | resistencia 61 · técnica 46 · correa 46 · velocidad 20 · fuerza 2 |

No son la misma magnitud y llamarlas «inversas» fue un error de lectura.

**Lo que sí es un problema real, y es el que queda accionable** *(DERIVADO)*: la ADR 0038 define el precio
de un objeto como

```
valor(objeto) = Σ (bonus_atributo × valorMarginal_atributo)
```

es decir, **tasa una pieza individual con una tabla medida sobre la plantilla entera**. Los valores por
pieza demuestran que esa sustitución no se sostiene: un +10 de fuerza mide **2** y un +10 de resistencia
mide **61**, cuando la tabla predice casi lo contrario.

**HIPÓTESIS de por qué**, y es comprobable: la **fatiga es por jugador** (`SpeedPerTick` escala el frenado
por la resistencia **del propio jugador**), así que un objeto de resistencia rinde **entero** en su portador
durante todo el partido. La fuerza, en cambio, solo paga cuando ese jugador concreto entra en un duelo, que
es una fracción pequeña de sus acciones. Una tabla de plantilla promedia sobre siete jugadores y esconde esa
diferencia.

**Además, la tabla de la ADR es inestable entre versiones del motor** *(MEDIDO, en la propia
`fase1b-resultados.md`)*: la fuerza pasó de +2,4 a +5,4 a +11,1 en tres paquetes consecutivos y la
resistencia de +2,1 a +4,3 a +3,0. Se midió por última vez el **6 de septiembre**, antes de la física del
pase (ADR 0091), las razas recalibradas (0092), las seis filas (0103), las **siete** filas, la banda de
tiros (0109) y el ajuste de `Shoot` (0110).

**Atenuante importante** *(MEDIDO)*: `EconomyData` documenta que la tabla medida de objetos **no la consulta
ninguna decisión todavía** («Paso 1 de AT-A»). Su único lector es `RunPolicy`, que usa la media como listón
de compra en la medición. Así que **hoy la contradicción no está haciendo daño en el juego**; está esperando
al paso 2 de AT-A.

**Decisión del revisor**: antes de que la tabla medida empiece a fijar precios, hay que elegir si el precio
se **calcula** con una tabla de plantilla (ADR 0038, y entonces hay que remedirla sobre el motor actual) o
se **mide** por pieza (y entonces la fórmula de la ADR 0038 sobra). **Las dos cosas a la vez no.**

---

## Estado del árbol

Un solo cambio de código, el del punto 1: `Balance/BuildBatchRunner.cs` y `Balance/Program.cs`. No toca
`/Sim`, ni `/data`, ni el motor, ni ningún valor de balance. 689/689 unitarios en verde. Los puntos 2 y 3
terminan en decisión del revisor y **no se ha cambiado nada por ellos**.
