# BB-T — La puerta de impacto de equipamiento cae a 0,5: era RUIDO, no el catálogo

**Estado:** Instrumento ARREGLADO (19 sep 2026): la puerta promedia ocho bases de semilla y vuelve a
verde. **El umbral sigue en 1,0 y sin tocar.** Queda abierta solo por esa decisión, que pide ADR (RT-057).

## Lo que esta ficha decía, y por qué estaba mal

La primera versión afirmaba, bajo el título "Causa, confirmada", que el catálogo más grande (94 → 102
perks) diluía lo que aportan los objetos, porque la puerta reparte perks del catálogo con
`PerkAssignment.AssignInitial` y porque el valor se movió al cambiar el catálogo.

**Eso era un antes/después con UNA semilla leído como causa.** Es exactamente el error que
`docs/pendientes/BB-P.md` describe —*"las puertas de un solo partido/semilla se leen como causa cuando son
ruido"*— cometido por quien había citado esa ficha el mismo día.

## Lo que dice la medición

`/Sim` está intacto entre `4462369` y HEAD (`git diff --stat 4462369..HEAD -- Sim/` vacío): solo cambiaron
`/data`, `/Sim.Tests` y `/docs`. Con el motor congelado byte a byte, midiendo la MISMA puerta sobre ocho
semillas base y comparando los dos catálogos en pareja:

| base | 94 perks | 102 perks | dif |
|---|---|---|---|
| **1000 (la del test)** | **1,40** | **0,46** | −0,94 |
| 2000 | 2,52 | 4,05 | +1,53 |
| 3000 | 2,28 | 2,00 | −0,28 |
| 4000 | 2,93 | 2,73 | −0,20 |
| 5000 | 2,23 | 2,03 | −0,20 |
| 6000 | 3,16 | 3,26 | +0,10 |
| 7000 | 3,09 | 2,26 | −0,83 |
| 8000 | 3,06 | 3,53 | +0,47 |

**Efecto pareado del cambio de catálogo: −0,04 puntos, error típico 0,28, IC95 % [−0,70, +0,61].** Cero.
El experimento acota el efecto máximo posible en ±0,7, y haría falta −0,94 para explicar la caída.

## La causa real: el umbral no tiene margen

`Sim.Tests/Perks/EquipmentImpactTests.cs`:

- Umbral **1,0** (`equippedRate - bareRate >= 1.0`), fijado por la ADR 0116 tras bajar 5,0 → 3,0 → 2,0 → 1,0.
- Muestra: 96 plantillas × 32 partidos × 2 direcciones = **6.144 partidos por brazo**.
- **Una sola semilla base**: `rosterSeed = 1000 + roster`.
- El comentario del propio fichero dice: *"Con 96 plantillas (6.144 partidos por brazo) el error baja a
  **~0,9**"*.

**Error típico ~0,9 contra un umbral de 1,0.** Con un valor real de ~2,5, esta puerta cruza el listón por
puro muestreo cada pocos commits. Medido en el barrido: **1 de 16 mediciones por debajo de 1,0 = 6,3 %**,
que es justo el *"del orden de 6 % de salir rojo por puro muestreo"* que la ADR 0116 presupuestó y aceptó.

La semilla 1000 —la única que el test usa— sale **la más baja de las ocho en los dos catálogos**: con 94
perks ya pasaba por 0,40 puntos, **menos de medio error típico**. Ya estaba al borde antes de tocar nada.

**Dato que lo remata**: la puerta se había degradado de **3,4** (`f1ce8b3`, 16 sep, cuando se fijó el
umbral) a **1,40** en `4462369` —el commit inmediatamente anterior al trabajo de catálogo— **sin que
ningún commit tocara perks ni objetos**. Esa caída de 2,0 puntos por resorteo es MAYOR que la de hoy
(0,94) y nadie la atribuyó a nada.

## Qué hay que decidir — y no es lo que decía la versión anterior

**NO recalibrar los objetos y NO bajar el umbral.** Las dos opciones que planteaba esta ficha partían de
una causa que los números descartan. Recalibrar objetos contra una medición que está 2 sd baja movería el
balance real del juego para perseguir ruido.

El arreglo es **de instrumento**, y está costeado: **promediar varias semillas base** en vez de fijar una.
Medido sobre 8 bases con el patrón `ThreadCatalogs` del proyecto: **3 m 14 s en Release** frente a 1 m 13 s
de la puerta actual, dentro del presupuesto de 8 minutos de las 43 puertas. Baja el error típico de ~0,87
a **~0,31**, con lo que un umbral de **2,0** daría ~3,5 % de falso positivo.

Es decir: **permitiría RECUPERAR el escalón "varios puntos" de la ADR 0033 que la ADR 0116 admitió haber
perdido**, en vez de seguir bajando el listón. Es la alternativa *"sube la muestra en vez de bajar el
umbral"* que la propia ADR 0116 dice que **nunca llegó a costear** (`BB-P`), y ahora está costeada.

Tocar el umbral de una puerta es cambio de rango de balance: **exige ADR (RT-057)**. No se hace aquí.

**Riesgo si no se hace**: mientras la puerta siga con una semilla base, saldrá roja al azar cada ~6 % de
commits y cada vez costará una investigación como esta. Ya ha pasado tres veces: BA-M, BA-N y esta.

## Hermanos

- `docs/pendientes/BB-P.md` — "las puertas de un solo partido/semilla se leen como causa cuando son ruido".
  Esta ficha es un caso más, y el más caro hasta ahora.
- `docs/pendientes/CAT-J.md` — las otras tres puertas rojas, preexistentes y sin investigar.
- ADR 0116 — quien bajó el umbral a 1,0 y presupuestó el 6 % de falsos positivos.


## 19 sep 2026 — instrumentación cambiada, umbral intacto, y el resultado guardado

Aplicado el plan del revisor en este orden: cambiar **solo** la instrumentación a 8 semillas · ejecutar ·
guardar el resultado · mirar la distribución · y solo entonces decidir sobre el umbral.

`EquipmentImpactTests` promedia ahora **ocho bases de semilla** (1000..8000) en vez de una. El paralelismo
vive en el arnés, como manda el proyecto: `Parallel.For` por índice, semilla función pura del índice, un
`Catalog` por hilo (`ThreadCatalogs`, porque las condiciones compiladas no son reentrantes) y las
reducciones después, en orden.

### El resultado, tal cual

```
partidos por brazo y base: 6144 · bases: 8 · total 98304
base | sin equipar | equipada | aporta
1000 |      53.30% |   53.76% |   0.46
2000 |      50.46% |   54.51% |   4.05
3000 |      52.83% |   54.83% |   2.00
4000 |      50.46% |   53.19% |   2.73
5000 |      51.77% |   53.81% |   2.03
6000 |      48.40% |   51.66% |   3.26
7000 |      50.60% |   52.86% |   2.26
8000 |      49.38% |   52.91% |   3.53

media 2.54 · sd entre bases 1.12 · error típico de la media 0.40
IC95 % aproximado: [1.75, 3.33]
mínimo 0.46 (base 1000) · máximo 4.05
```

Reproduce **exactamente**, cifra a cifra, el barrido independiente que diagnosticó la ficha. Y confirma
sobre el catálogo vigente lo que aquel decía: la base 1000 —la única que la puerta usaba— es la más baja
de las ocho, y la única por debajo de 1,0.

**Coste**: las 43 puertas pasan de **5 m 32 s a 7 m 05 s**. Dentro del presupuesto de 8 minutos que fija
`CLAUDE.md`, pero ya sin holgura: la cifra de 5 m 32 s que ese documento cita se ha quedado vieja.

### ¿Sigue 1,0 demasiado bajo? Los números, para decidir

Con la media en 2,54 y el error típico de la media en 0,40 (aproximación normal):

| umbral | falso positivo | avisa si el efecto se **halva** | avisa si **desaparece** |
|---|---|---|---|
| **1,0 (vigente)** | **0,01 %** | **25 %** | 99 % |
| 1,5 | 0,47 % | **72 %** | 100 % |
| 2,0 | **8,85 %** | 97 % | 100 % |
| 2,5 | 46 % | 100 % | 100 % |

Lectura:

- **1,0 ya no da falsos positivos** (0,01 %): el problema que abrió esta ficha está resuelto solo con la
  instrumentación, sin tocar el listón.
- Pero **1,0 apenas detecta nada**: si equipar pasara a aportar la mitad, esta puerta solo avisaría **1 de
  cada 4 veces**. Protege "equipar hace algo", no el escalón de la ADR 0033 — que es justo lo que la ADR
  0116 admitió haber perdido.
- **2,0 NO es seguro todavía**: 8,85 % de falso positivo, del mismo orden que el ~6 % que causó BA-M,
  BA-N y esta ficha. (El barrido independiente lo estimó en ~3,5 % usando un error típico de 0,31; medido
  sobre el catálogo vigente es 0,40, y con eso 2,0 vuelve a ser arriesgado.)
- **1,5 es el punto razonable**: 0,47 % de falso positivo y triplica la detección (25 % → 72 %).

**No se cambia aquí.** Subir el umbral de una puerta es un cambio de rango de balance y pide ADR
(RT-057). Lo que esta ficha aporta es la medición con la que decidirlo, que es lo que faltaba.

### Lo que esto desbloquea

La ADR 0116 dejó escrito que la alternativa *"sube la muestra en vez de bajar el umbral"* **nunca llegó a
costearse**, y `BB-P` lo recogió como pendiente. Ya está costeada y ejecutada: 1 m 33 s más de puertas a
cambio de un error típico tres veces menor.
