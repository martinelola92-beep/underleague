# 0040. La curva de puertas se mide con la build que cabe en ese punto de la run

**Fecha:** 2026-09-05
**Estado:** Aceptada
**Corrige:** ADR 0033 (la tabla sigue vigente; cambia con qué builds se mide)
**Requisitos:** RF-023, RF-071, RF-003b, RT-055

## El problema que destaparon las runs completas

La medición de runs completas dice que una política razonable llega al jefe del acto 1 con **4,3 perks** en el once y supera esa puerta el 47,6% de las veces, cuando la tabla de la ADR 0033 pide entre el 45% y el 60% para una build "correcta" y entre el 60% y el 75% para una "buena". La lectura inmediata fue *"la economía llega corta"*, y la salida propuesta era darle al club inicial una build hecha, tocando RF-023 y RF-005.

Esa lectura es errónea, y la aritmética lo demuestra: en el acto 1 hay 6 nodos de partido, unas 4 victorias, 2,6 recompensas de perk y una plantilla que arranca con 1-2 perks porque un común empieza con cero (RF-023). **Catorce perks no caben en el acto 1 de ninguna manera, y no deben caber**: si cupieran, la build estaría hecha antes de la primera puerta y el resto de la run sería un trámite.

El error está en **cómo se mide la curva**, no en la economía. Las builds de prueba tienen los catorce slots llenos porque representan una plantilla terminada, y se están enfrentando a los tres jefes por igual. Es decir: se compara al jugador del acto 1 con una plantilla que solo existe en el acto 3.

## Decisión

**Cada celda de la tabla de la ADR 0033 se mide con la densidad de build alcanzable en ese punto de la run**, no con la plantilla completa. La tabla de exigencia no cambia; cambia el material con el que se mide.

| Puerta | Densidad de referencia | De dónde sale |
|---|---|---|
| Jefe del acto 1 | ~4-5 perks, ~2 objetos | medido en runs completas |
| Jefe del acto 2 | ~9-10 perks, ~4 objetos | medido |
| Jefe final | ~14 perks, ~6 objetos | medido |

Los cuatro niveles de calidad (incoherente, correcta, buena, muy buena) siguen significando lo mismo —si los perks se activan, si son coherentes, si la colocación respeta lo que exigen, si además está equipada—, pero **se instancian con el número de piezas que corresponde a ese acto**. Una build "buena" del acto 1 es una build de cinco perks bien elegidos, no una de catorce.

Consecuencia inmediata: el resultado del 47,6% en la primera puerta deja de ser un suspenso y pasa a ser el dato que hay que contrastar contra su banda correcta, con builds de cinco perks a los cuatro niveles.

## Y la banda de victoria de la run se corrige

`fase2-diseno.md` §10 fija la tasa de victoria de la run en 25-40%. **Es incompatible con la propia tabla de la ADR 0033**: el producto de las tres celdas de "muy buena" da 29,5%, y la trayectoria realista que la tabla describe —una build que mejora entre actos— da entre 21,8% y 28,2%. La banda coherente es **20-30%**, y así queda.

## Consecuencias

- `data/balance/builds/` necesita los cuatro niveles **por acto**, no solo en su versión terminada: doce combinaciones por raza en lugar de cuatro. Se generan a partir de las completas quitando piezas, no escribiéndolas a mano.
- La puerta de la curva se remide con ese material; los jefes probablemente haya que recalibrarlos, porque hasta ahora los del acto 1 y 2 estaban ajustados contra builds imposibles para su momento.
- **No hace falta tocar RF-023 ni RF-005**: el club inicial no necesita traer una build hecha. La progresión de la build a lo largo de la run es el juego, y adelantarla al principio lo vaciaría.
