# 0033. Los jefes son puertas de calidad de build

**Fecha:** 2026-09-05
**Estado:** Aceptada (directriz del revisor)
**Requisitos:** RF-001b, RF-001c, RF-002b, RF-012b, RF-012d, RF-032, RF-071, RT-055
**Sustituye:** la segunda mitad de `scalingRewardsGoodBuilds` (D-28) y matiza la ADR 0032

## Contexto

El revisor fija el objetivo de diseño del juego: **la build es el núcleo, porque es donde el jugador decide.** De ahí se derivan dos exigencias concretas de curva:

> Hay que tener una **build buena** antes del primer jefe para superarlo, y una **build muy buena** para terminar la run.

Hasta ahora las métricas medían el balance entre builds (que ninguna domine, que las coherentes ganen a las incoherentes). Eso es necesario pero no dice nada sobre **la exigencia del juego**: un catálogo perfectamente equilibrado puede producir una run que se gana sin pensar, o una imposible.

## Decisión

**Los jefes son las puertas que miden la calidad de la build, y son el esqueleto de la dificultad.** Cada jefe exige un nivel de construcción y lo hace cumplir: perder contra un jefe termina la run (RF-002b), así que no es un partido más, es un examen.

Se define una escala de calidad de build, que es la que `/Balance` usará como eje:

| Nivel | Qué es |
|---|---|
| **Incoherente** | Perks que no se activan, colocación que rompe sus propios vínculos, sin criterio |
| **Correcta** | Perks que se activan, coherentes entre sí, colocación que respeta lo que exigen |
| **Buena** | Correcta, con una línea de sinergia clara y sus perks de escalado alimentados |
| **Muy buena** | Buena, además equipada, con la plantilla cuidada y el escalado acumulado durante toda la run |

Y la curva de exigencia que `/Balance` debe verificar:

| Puerta | Incoherente | Correcta | Buena | Muy buena |
|---|---|---|---|---|
| **Jefe del acto 1** | < 25% | 45-60% | 60-75% | 70-85% |
| **Jefe del acto 2** | < 15% | 30-45% | 55-70% | 65-80% |
| **Jefe final** | < 10% | 15-30% | 35-50% | 55-70% |

Lectura: una build **correcta** pasa el primer jefe con apuros y se queda por el camino; una build **buena** llega lejos pero no basta para el final; solo una build **muy buena** termina la run con holgura. Y una build incoherente no pasa la primera puerta, que es lo que convierte el acto 1 en una lección y no en un trámite.

**Esto sustituye a la segunda mitad de `scalingRewardsGoodBuilds`** (D-28), que pedía que las builds malas *decayeran* a lo largo de la campaña y resultó no ser medible: todas las builds caen cuando el rival mejora, y la que más caía era la que no llevaba ningún perk. La pregunta correcta no es cuánto cae una build mala, es **si pasa la puerta**. Es más directa, más fiel a lo que el jugador vive y no exige fabricar el número.

## Lo que esto obliga a proteger

1. **El jugador tiene que poder saber que no está listo, y a tiempo.** Si el jefe es una puerta dura, entrar sin saberlo es exactamente el riesgo "espiral de muerte: run perdida sin saberlo" del §8 de los requisitos. Las herramientas ya existen y pasan a ser **obligatorias**, no deseables: el informe de ojeo completo y gratuito del jefe (RF-012b), el distintivo de dificultad (RF-012), y el principio de que nada de lo que pase estaba sin anunciar (RF-012d). El nodo de jefe es visible desde el principio del acto: el jugador debe poder abrirlo, ver a qué se enfrenta y decidir cómo prepararse en los nodos que le quedan.
2. **Y tiene que poder hacer algo al respecto.** Una puerta sin herramientas para superarla es un muro. Los nodos previos al jefe deben ofrecer margen real de mejora: mercado alcanzable (RF-011b), recompensa con reroll (RF-071b), venta para financiar (RF-114f), canteranos (RF-114b). Si `/Balance` mide que una build correcta no puede convertirse en buena antes del jefe con el oro y los nodos disponibles, el problema es la economía, no la build.
3. **No confundir calidad de build con rareza.** La salvaguarda de la ADR 0027 —un equipo **sin ningún legendario** debe poder ganar al jefe final, medido hoy en 57,9%— sigue vigente y no se contradice con esto: exige que la *rareza* no sea la barrera. Esta ADR exige que la *construcción* sí lo sea. Un jugador con plantilla mediocre y build muy buena debe pasar; uno con legendarios y build incoherente, no.

## Consecuencias

- `/Balance` necesita la métrica de puertas: cada nivel de build contra cada jefe, con muestra suficiente. Es **la** métrica del juego y va a la puerta de fase 2.
- `data/balance/builds/` gana ejemplares de los cuatro niveles para cada raza de lanzamiento; hoy solo hay coherentes y malas, sin el escalón intermedio ni el superior.
- Los jefes (`data/bosses/`) se diseñan **contra esta tabla**: su plantilla y su modificador de regla se calibran hasta que la curva se cumpla, en vez de diseñarlos primero y medir después.
- La progresión dentro de la run (experiencia, perks acumulativos, equipamiento) es lo que separa "buena" de "muy buena": si no basta para cruzar la última puerta, hay que reforzarla.
