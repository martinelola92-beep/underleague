# 0116. El escalón de "equipar" se recalibra contra el catálogo de 94 perks

**Fecha:** 2026-09-16
**Estado:** Aceptada e implementada (`Sim.Tests/Perks/EquipmentImpactTests.cs`)
**Decisión del revisor** (BA-N, `docs/pendientes/BA-N.md`). **Cambia el umbral de la puerta
`EquippingAGoodBuildIsWorthSeveralPointsOfWinRate`** (RT-057: cambio de rango, exige ADR). No modifica
ninguna ADR de precio de objeto (0038, 0086, 0087) ni ningún valor de `/data`.
**Requisitos:** RT-056, RT-057. Relacionado: ADR 0033 (el escalón "muy buena"), ADR 0038 (precio calculado
de objeto), ADR 0087 (valor de perk medido contra su control).

## Qué umbral existía

`EquippingAGoodBuildIsWorthSeveralPointsOfWinRate` exigía que equipar a los siete titulares de una build
"buena" con un objeto cada uno (RF-076, mezcla de rarezas del acto 3) subiera la tasa de victoria **al
menos 2,0 puntos** frente al mismo equipo sin objetos. El propio test ya documentaba, desde que se fijó
(paquete AZ), que el número no salía de una fórmula: lo medido en ese momento era 3,3 puntos y se eligió
2,0 como un valor limpio y por debajo de lo medido, con la única afirmación de que "equipar VALE (varios
puntos), no una cifra concreta".

## Por qué deja de ser apropiado con el catálogo actual

BA-N registró la misma métrica en tres momentos, con la catástrofe misma de siempre (96 plantillas, 32
partidos/plantilla/dirección, sin tocar el test):

| momento | catálogo | medido |
|---|---|---|
| antes de tanda 1 y 2 de perks | 61 perks | fallaba (<2,0) |
| + 6 primitivas y 13 perks de tanda 1 | ~74 perks | 2,0 |
| + 20 perks de tanda 2 | 94 perks | **1,7** |

La caída es monótona a lo largo de tres medidas sucesivas, cada una tras añadir perks y sin tocar ni un
objeto ni su precio — consistente con una sola causa (**LIKELY**, no aislada por experimento propio que
descarte una hipótesis rival): el catálogo pasó de 61 a 94 perks y se ha vuelto más fuerte en conjunto, así
que la contribución **marginal** de siete objetos, fija en términos absolutos, encoge frente a un
denominador (el resto de la build) que ha crecido. **No es que los objetos rindan menos** — nada en `/data`
ni en el sistema de objetos cambió durante las dos tandas.

## Qué representa ahora el umbral, y por qué es 1,0 y no otro número

El umbral deja de afirmar "varios puntos" (ADR 0033 lo pedía en plural, y un sistema que mide 1,7 no puede
prometer con margen real llegar establemente a 2 o más). Pasa a afirmar algo más modesto pero igual de
verificable: **equipar sigue produciendo un efecto real y medible, no un artefacto de ruido**.

El valor concreto, 1,0, es el límite de lo que la evidencia disponible permite justificar sin inventar una
fórmula nueva. Hay exactamente un precedente de cómo se fijó el umbral anterior por debajo de su propia
medida de calibración (3,3 medido → 2,0 elegido), y admite dos lecturas que no coinciden:

- Como **ratio** (2,0 / 3,3 ≈ 0,61) aplicado a la medida actual: 1,7 × 0,61 ≈ **1,0**.
- Como **margen absoluto** (3,3 − 2,0 = 1,3) aplicado a la medida actual: 1,7 − 1,3 ≈ **0,4** — demasiado
  cerca de cero y del error típico de medición (~0,9 a 96 plantillas) para afirmar nada con confianza.

Ninguna de las dos lecturas está registrada como la fórmula correcta en ningún sitio del proyecto —elegir
entre ellas sin decirlo sería la misma clase de ajuste silencioso que RT-057 prohíbe—. **Se elige la
lectura por ratio (1,0)**, decisión explícita del revisor, precisamente porque dista lo suficiente del
error de medición (~0,9) para seguir siendo una puerta con contenido real, y no está tan cerca de 1,7 que
equivalga a fijar el umbral igual a lo medido (lo que habría dejado la puerta sin margen de detección para
una regresión futura).

## Lo que esta ADR explícitamente NO resuelve

**La inconsistencia entre el valor calculado (ADR 0038) y el valor medido de un objeto sigue sin
resolverse.** El propio `docs/analisis/builds-analisis-sistemico.md` §9 demostró que la fórmula calculada
no predice el valor medido pieza a pieza (`+10 fuerza` mide 2, `+10 resistencia` mide 61; la suma de
atributos no predice nada: `+30` puede valer 0). Esta ADR no toca `data/economy/item-values.json`, no
toca la fórmula de la ADR 0038, y no decide cuál de las dos tablas debería mandar cuando difieren — esa
pregunta, que BA-N dejó explícitamente abierta, sigue abierta. Subir el umbral de vuelta a 2,0 usando la
fórmula calculada para justificar precios más altos habría repetido el error ya corregido de la ADR 0038;
esta ADR evita ese camino recalibrando el lado de la medición (el umbral de la puerta), no el lado del
precio.

## Lo que se mide

`dotnet test Sim.Tests --filter "FullyQualifiedName~EquippingAGoodBuildIsWorthSeveralPointsOfWinRate"`,
árbol con el umbral en 1,0: verde, 1,7 puntos medidos (sin cambio de comportamiento, solo del umbral).
43 puertas (una invocación): sin regresión atribuible a este cambio — el detalle completo, incluidas las
puertas que ya estaban rojas por causas ajenas a BA-N, está en `docs/pendientes/BA-N.md`.

## Consecuencias

- BA-N cerrada.
- El escalón "muy buena" de la ADR 0033 sigue existiendo, con una afirmación más modesta que la original.
- Si en el futuro se decide qué tabla de valor de objeto manda (calculada o medida) y se corrige la
  inconsistencia, esta puerta debería remedirse: podría volver a subir por encima de 1,0 sin que haga
  falta otra ADR de rango, siempre que siga dentro de esta franja; si sube por encima de 2,0 de forma
  estable, cabría revisar si el umbral puede volver a subir.
