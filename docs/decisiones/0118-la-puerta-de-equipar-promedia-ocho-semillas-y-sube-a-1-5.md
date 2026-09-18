# 0118. La puerta de equipar promedia ocho semillas, y con eso el umbral sube a 1,5

**Fecha:** 2026-09-19
**Estado:** Aceptada e implementada (`Sim.Tests/Perks/EquipmentImpactTests.cs`).
**Decisión del revisor** (BB-T, `docs/pendientes/BB-T.md`). **Cambia el umbral de la puerta
`EquippingAGoodBuildIsWorthSeveralPointsOfWinRate` de 1,0 a 1,5** (RT-057: cambio de rango, exige ADR). No
toca ningún valor de `/data`, ningún precio de objeto ni ninguna otra puerta.
**Requisitos:** RT-056, RT-057. Relacionado: ADR 0033 (el escalón "muy buena"), **ADR 0116** (fijó el 1,0
que esta sustituye), ADR 0115 (el resorteo que explica por qué esta puerta se movía sola).

## Contexto

La ADR 0116 bajó este umbral a 1,0 con un razonamiento correcto y una restricción que no pudo levantar:
el error típico de la medición era **~0,9 puntos** y el valor verdadero rondaba 2,4, así que un umbral de
2,0 salía rojo por puro muestreo del orden del **34 %** de las veces y 1,0 lo bajaba al **~6 %**.

Esa misma ADR dejó escrito que existía una alternativa mejor —**subir la muestra en vez de bajar el
umbral**— y que **nunca llegó a costearse**. `docs/pendientes/BB-P.md` lo recogió como pendiente.

El 19 sep 2026 la puerta volvió a salir roja (0,5 puntos) y se atribuyó al crecimiento del catálogo de
perks. Esa causa quedó **REJECTED** con una medición sobre ocho semillas y el motor congelado byte a byte:
el efecto del cambio de catálogo es **−0,04 ± 0,28 puntos**, es decir cero. Era el tercer falso positivo
de esta misma puerta (BA-M, BA-N, BB-T), y cada uno costó una investigación completa.

## Decisión

**Dos cambios, y el orden importa.**

1. **La puerta promedia ocho bases de semilla** (1000..8000) en vez de una. El paralelismo vive en el
   arnés, como manda `CLAUDE.md`: `Parallel.For` por índice, semilla función pura del índice, un
   `Catalog` por hilo (`ThreadCatalogs`) y las reducciones después, en orden.
2. **Con esa muestra, y solo entonces, el umbral sube de 1,0 a 1,5.**

Se implementó y se midió el punto 1 **antes** de decidir el punto 2, a propósito: el listón se fija contra
la dispersión medida del instrumento nuevo, no contra la esperada.

## Lo que se mide (19 sep 2026, catálogo de 102 perks)

```
base 1000  0,46   base 5000  2,03
base 2000  4,05   base 6000  3,26
base 3000  2,00   base 7000  2,26
base 4000  2,73   base 8000  3,53

media 2,54 · sd entre bases 1,12 · error típico de la media 0,40 · IC95 % [1,75 , 3,33]
```

La base 1000 —la única que la puerta usaba— es la **más baja de las ocho**, y la única por debajo de 1,0.
También lo era con el catálogo de 94 perks (1,40 frente a 2,52..3,16 del resto): no es casualidad de este
catálogo, es esa semilla.

## Por qué 1,5 y no otro número

Con media 2,54 y error típico de la media 0,40 (aproximación normal):

| umbral | falso positivo | avisa si el efecto se **halva** | avisa si **desaparece** |
|---|---|---|---|
| 1,0 (el de la ADR 0116) | 0,01 % | **25 %** | 99 % |
| **1,5 (esta ADR)** | **0,47 %** | **72 %** | 100 % |
| 2,0 | **8,85 %** | 97 % | 100 % |
| 2,5 | 46 % | 100 % | 100 % |

- **1,0 ya no daba falsos positivos** con la muestra nueva. El problema de BB-T se resolvía solo con la
  instrumentación. Pero **1,0 apenas detecta**: si equipar pasara a aportar la mitad, avisaría 1 de cada 4
  veces. Eso protege "equipar hace algo", no el escalón de la ADR 0033 — exactamente la pérdida que la ADR
  0116 admitió y lamentó.
- **2,0 sigue sin ser seguro**: 8,85 %, del mismo orden que el ~6 % que ya produjo tres investigaciones.
  (El diagnóstico previo lo estimó en ~3,5 % con un error típico de 0,31; medido sobre el catálogo
  vigente es 0,40, y con eso 2,0 vuelve a ser ruleta.)
- **1,5 triplica la detección** (25 % → 72 %) manteniendo el falso positivo por debajo del 0,5 %. Es el
  punto donde la puerta recupera parte del escalón de la ADR 0033 sin volver a la enfermedad que se acaba
  de curar.

**No se elige 2,0 aunque "recupere el escalón entero"**: un 8,85 % de rojo aleatorio vuelve a hacer que
cada commit inocente cueste una investigación, que es el coste real que esta ADR existe para eliminar.

## Coste

Las 43 puertas pasan de **5 m 32 s a 7 m 05 s**. Dentro del presupuesto de 8 minutos de `CLAUDE.md`, pero
**sin holgura**: la cifra de 5 m 32 s que ese documento cita queda obsoleta con esta ADR. Si otra puerta
necesita el mismo tratamiento —`CAT-J` es candidata—, habrá que revisar el presupuesto o paralelizar más,
no añadir semillas a ciegas.

## Consecuencias

- La alternativa que la ADR 0116 dejó sin costear queda costeada y aplicada: **1 m 33 s más de puertas a
  cambio de un error típico tres veces menor**.
- El patrón es reutilizable: cualquier puerta cuyo margen sea del orden de su error típico sufre lo mismo.
  `passChain = 1,10` contra un rango que empieza en `1,11` (CAT-J) tiene esa pinta.
- Lo que esta ADR **no** hace: no recalibra objetos, no toca precios, y no dice nada sobre si el valor
  verdadero de 2,54 es el correcto para la ADR 0033. Solo fija cómo se mide y dónde está el listón.
