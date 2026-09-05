# 0053. El mapa se abre a cuatro carriles

**Fecha:** 2026-09-05
**Estado:** Aceptada (decisión del revisor)
**Requisitos:** RF-010, RF-011, RF-011b, RF-002d, RF-003b
**Revisa:** la decisión W-2 del paquete base (mercados en cuello de botella)

## Estado actual

`MapGenerator` construye capas de **ancho 2**, con alguna de 3, y las capas de mercado y de jefe son de **ancho 1**. Consecuencias:

- La decisión de ruta es **binaria** en cada paso: casi siempre eliges entre dos nodos, y a menudo entre dos del mismo tipo.
- **Todas las rutas pasan por todos los mercados.** Eso cumple RF-011b por construcción, pero vacía RF-002d, que describe jugar en inferioridad como *"una decisión legítima frente a **desviarse** hacia un mercado"*: si el mercado es inevitable, no hay desvío ni decisión, solo "gastar o no gastar".

## Decisión

**Ancho de cuatro carriles**, con la topología de Slay the Spire:

1. **Entrada única y apertura progresiva.** El acto empieza en **un solo nodo**, y desde ahí bifurca: 1 → 2 → 4, alcanzando el ancho completo en la tercera capa. Todo el mundo juega el mismo primer nodo, lo que da un arranque consistente, un punto de comparación entre runs y el sitio natural para la primera run guiada (RF-123).
2. **Divergencia y reconvergencia.** A partir de ahí los caminos se abren y se vuelven a cruzar; elegir una rama no cierra el mapa.
3. **Movimiento solo a carriles contiguos**: desde el carril *i* se puede ir a *i−1*, *i* o *i+1* de la capa siguiente, si existe la arista. Esto es lo que hace que la elección tenga **memoria**: subir mucho en un acto te deja lejos de la parte baja, y volver cuesta varias capas. La decisión deja de ser local.
4. **Los mercados dejan de ser cuello de botella**: ocupan uno o dos carriles de su capa, no la capa entera. Desviarse hacia ellos cuesta posición, y ahí vuelve la decisión que RF-002d describe.
5. **El jefe sigue siendo capa de ancho 1**: es el final del acto y todos los caminos convergen en él. El acto queda así **cerrado por los dos extremos** —una entrada, una salida— y abierto en medio, que es donde deben estar las decisiones.

## Lo que hay que garantizar, y es la parte difícil

RF-011b exige un mercado **alcanzable en dos saltos como máximo desde cualquier punto**. Con mercados en cuello de botella eso salía gratis; con cuatro carriles hay que **construirlo y comprobarlo**.

La garantía no puede quedar en un reintento perezoso: el paquete base la resolvió por construcción y hay un test que la verifica sobre **1.000 mapas generados**. Ese test sigue siendo el criterio. Si con cuatro carriles la propiedad no se puede garantizar con la separación actual de mercados, la salida es **más mercados**, no relajar la garantía — o, si resulta imposible sin llenar el mapa de tiendas, traer la relajación a tres saltos como un ADR propio y medido, nunca como un efecto colateral.

## Efectos esperados

- **Más decisiones y más distintas**: con cuatro carriles caben varios tipos de nodo por capa, así que la elección deja de ser "cuál de estos dos partidos" y pasa a ser "partido, mercado o clínica".
- **El nodo de élite y el de inscripción cobran sentido**: hoy compiten mal porque en una capa de ancho 2 casi siempre hay que cogerlos o perderlos; con cuatro carriles se pueden colocar como desvíos de verdad.
- **El coste de oportunidad se vuelve espacial**, no solo económico: ir a por el mercado te aleja del élite, y eso es una decisión que hoy no existe.

## Riesgos

- **Más nodos dibujados por acto**. El número de nodos que se **recorren** no cambia (11/12/12, decisión W-1); lo que cambia es cuántos se dibujan y descartan. Hay que revisar que el reparto de tipos siga cumpliendo el tope del 60% de partidos (RF-003b) **en el peor camino**, que es como se mide hoy.
- **Legibilidad**: cuatro carriles con aristas cruzadas necesitan dibujarse bien. La pantalla de mapa ya existe y tendrá que crecer.
- **La distribución de tipos por capa** hay que rehacerla: hoy una capa libre es entera de partidos o entera de servicios (decisión W-3, para cumplir RF-003b en el peor camino). Con cuatro carriles conviene mezclarlas, y entonces el tope del 60% hay que garantizarlo de otra forma.
