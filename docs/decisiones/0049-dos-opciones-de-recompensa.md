# 0049. La recompensa de un partido de liga ofrece dos opciones, no tres

**Fecha:** 2026-09-05
**Estado:** Aceptada (decisión del revisor)
**Modifica:** RF-071
**Requisitos:** RF-071, RF-071b, RF-114, RF-011b
**Motiva:** el diagnóstico de que el trampolín diluye el mercado (`fase2-diseno.md` §19)

## Contexto

Medido: una run reparte casi **diez elecciones gratis** y solo se compran unas seis piezas, así que el mercado decide apenas el 40% de la build. Por eso la doctrina de compra con criterio no consigue separarse de la de ahorrar: la tienda importa poco porque las recompensas ya te dan casi todo.

## Decisión

**Un partido de liga ofrece una elección entre dos opciones**, no entre tres (RF-071 decía tres). Menos opciones significa peor elección esperada, y eso devuelve al mercado su papel: si quieres una build buena, tienes que pasar por él.

**El escalonado de la ADR 0043 se conserva**, y ahora además significa algo:

| Nodo | Recompensa |
|---|---|
| Partido de liga | 1 de **2** |
| Partido de élite | 1 de **3**, con rareza mejorada |
| Jefe de acto | **2** elecciones de **3** + curación de plantilla |

Así el élite y el jefe no solo pagan más oro: **ofrecen mejores decisiones**. La diferencia entre ir por la ruta segura y la peligrosa deja de ser solo de oro y pasa a ser de calidad de build, que es lo que hace interesante el mapa.

Efecto secundario deseable: el **reroll** (RF-071b) gana valor, porque con dos opciones la probabilidad de que ninguna encaje sube.

## Riesgo a vigilar

Bajar la calidad de las recompensas **baja la calidad de la build final**, y la curva de puertas de la ADR 0033 está calibrada contra la densidad actual. Es probable que haya que recalibrar los jefes o compensar por el lado del mercado; lo que **no** puede pasar es que el acto 1 deje de ser el taller (ADR 0043) porque el jugador ya no llega con build a la primera puerta.

Si al medir resulta que la build final baja demasiado, la palanca de compensación es el mercado —más surtido, mejor rareza o precios más bajos—, no devolver la tercera opción: el objetivo es mover peso de lo gratis a lo comprado, no reducir el total.
