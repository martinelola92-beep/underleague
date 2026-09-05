# 0055. Sin pasar por el mercado no se gana

**Fecha:** 2026-09-05
**Estado:** Aceptada (directriz del revisor)
**Requisitos:** RF-071, RF-114, RF-114e, RF-002d
**Complementa:** ADR 0037 (la economía es la dificultad), ADR 0049 y ADR 0051

## La directriz

*"El mercado tiene que ser gran parte del núcleo de la build. Solo con los perks de las victorias no debería darte opción a ganar."*

## Por qué hoy probablemente sí se puede

Una run reparte del orden de **diez recompensas gratis**, y un once tiene **catorce slots de perk**. Con tres opciones por recompensa, las victorias solas bastan para **llenar casi toda la build**. El mercado añade **calidad**, no cantidad — y por eso la ventaja de comprar con criterio se quedó en +2,0 puntos cuando buscábamos 8: si con lo gratis ya tienes una build, comprar es un extra.

Es la misma causa que el paquete anterior identificó al recortar las opciones: *el trampolín diluye el mercado*. Solo que el problema no era el número de opciones, sino que **las recompensas cubren el volumen entero de la build**.

## Métrica

**Ganar la run sin entrar en ningún mercado debe ser prácticamente imposible: por debajo del 5%.** Con el mapa de cuatro carriles, esquivar los mercados es posible en el 98,9% de los actos (ADR 0053), así que la medición es directa: una política que nunca entra en mercado y juega bien todo lo demás.

Se mide, no se supone. Si hoy esa política gana el 15%, el problema está cuantificado.

## Palancas, de la que menos daño hace a la que más

1. **Los perks maestros solo se compran** (ADR 0051). Es la palanca más limpia y la que mejor encaja: los maestros son el **objetivo** de una build, exigen dos o tres perks previos y son lo que separa una build buena de una completa. Si no salen como recompensa, **una build sin mercado se queda a medias por definición**, sin necesidad de recortar nada más.
2. **El equipamiento, solo en el mercado.** Equipar vale +8,2 puntos medidos; si las recompensas dejan de dar objetos, saltarse el mercado cuesta ese margen entero. Además es coherente: un objeto es una compra, no un trofeo.
3. **Menos nodos que den perk.** Que algunos partidos paguen solo oro. Es la palanca más burda y la que más se nota como recorte, así que es la última.

Las dos primeras cambian **qué** se consigue en cada sitio; la tercera cambia **cuánto** se consigue en total, y eso vacía la run.

## Lo que hay que vigilar

- **El mercado no puede volver a ser obligatorio de facto.** Acabamos de conseguir que se pueda esquivar (RF-002d): la decisión debe ser *"me desvío y pago el precio en ruta"*, no *"si no voy, pierdo seguro". El objetivo es que saltárselo sea una **mala decisión**, no una imposible.
- **Con oro escaso**, hacer el mercado imprescindible aprieta dos veces. Si al medir la tasa de victoria general cae por debajo de su banda, la palanca a mover es el **oro**, no devolver los perks a las recompensas.
