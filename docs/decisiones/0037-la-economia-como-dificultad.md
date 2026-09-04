# 0037. La economía es la palanca de dificultad

**Fecha:** 2026-09-05
**Estado:** Aceptada (directriz del revisor)
**Requisitos:** RF-114b..k, RF-071b, RF-011b, RF-012d, RT-055
**Complementa:** ADR 0033 (los jefes son puertas de calidad de build)

## Contexto

La ADR 0033 fijó que los jefes son las puertas que miden la build. Esta ADR fija **de dónde sale la dificultad para cruzarlas**: del oro. En palabras del revisor, *"es la tienda la que te va a hacer tener un equipo bueno o no"*. Si el jugador puede comprar todo lo que ve, no hay build que construir: hay una lista que completar, y un jugador medio se pasa el juego sin pensar.

El objetivo declarado es que existan momentos de *"tenía que haber ahorrado: en esta tienda hay algo que prefiero"*.

## Decisión

**El oro es escaso por diseño, y la escasez es la fuente principal de dificultad.** No se ajusta para que el jugador pueda equiparse: se ajusta para que tenga que **elegir**, y para que elegir mal se note en la siguiente puerta.

### Cómo se mide algo que parece inmedible

El arrepentimiento no se puede medir. Lo que sí se puede medir es **si la decisión existe**, y se hace enfrentando tres políticas automáticas en `/Balance` sobre las mismas semillas:

| Política | Qué hace |
|---|---|
| **Gastadora** | compra lo primero que mejora la plantilla, en cuanto puede pagarlo |
| **Ahorradora** | no compra salvo que el artículo supere un listón alto; acumula |
| **Contextual** | compra según lo que falta para la puerta siguiente y lo que ya lleva |

**El criterio es que ninguna política pura domine y que la contextual gane a las dos.** Las tres lecturas posibles:

- Si la **gastadora** rinde igual o mejor que la contextual, sobra oro: no hay escasez y la tienda es un trámite.
- Si la **ahorradora** domina, comprar nunca compensa: la tienda es una trampa y el juego premia no jugarla.
- Si la **contextual** gana a las dos por un margen claro, la decisión existe y depende del contexto, que es exactamente lo que se busca.

Objetivo de partida: la contextual por encima de las dos puras en **al menos 8 puntos** de tasa de victoria de la run.

### Métricas de escasez

| Métrica | Rango objetivo | Por qué |
|---|---|---|
| Fracción del surtido que el jugador puede permitirse al llegar a un mercado | 20-35% | Por encima de la mitad no hay elección; por debajo del 15% la tienda es decorado |
| Compras por visita al mercado | 1-2 de 12-16 artículos | Se compra poco y se piensa |
| Oro sobrante al terminar la run | < 15% del ganado | Si sobra oro, la economía no aprieta |
| Runs en las que el jugador llega a un mercado sin poder comprar nada | 10-25% | Ha de doler a veces, pero no ser la norma |
| Tasa de victoria de la run con política contextual | 25-40% | Ya fijado en `fase2-diseno.md` §10 |

## Dos condiciones que hacen que esto sea tensión y no frustración

1. **El dilema tiene que ser informado, no ciego.** El jugador no sabe **qué** habrá en el próximo mercado —si lo supiera no habría dilema—, pero sí sabe que **habrá otro y cuándo**, porque el mapa lo enseña y RF-011b garantiza uno cada 3-4 nodos. Así la apuesta es "no sé qué saldrá" y no "no sé si volveré a ver una tienda", que es la diferencia entre tensión y azar. Cumple RF-012d.
2. **Arruinarse no puede ser irreversible.** Si gastar mal condena la run sin remedio, es frustración, y es el riesgo "espiral de muerte" del §8. Las vías de recuperación ya existen y pasan a ser obligatorias: vender jugadores y objetos (RF-114f), canteranos gratuitos (RF-114b) y el reroll (RF-071b). Lo que se pierde por una mala compra es **margen**, no la partida.

## Consecuencias

- El ajuste de economía deja de perseguir "que el jugador pueda comprar" y pasa a perseguir estos rangos. Es el criterio de salida económico de la fase 2.
- `/Balance` necesita las tres políticas, no una. La comparación entre ellas es la métrica; la política contextual sola no dice nada.
- Los precios se calibran **contra el oro por acto**, no en absoluto: lo que importa es cuántos artículos caben en el presupuesto de un acto, no cuánto cuesta cada cosa.
- El equipamiento (ADR 0036) es el sumidero más elástico, porque es transferible y acumulable; los precios de los objetos son la palanca fina de esta curva.
