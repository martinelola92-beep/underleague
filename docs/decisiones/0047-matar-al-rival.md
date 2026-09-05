# 0047. Un perk letal propio no puede matar a nadie

**Fecha:** 2026-09-05
**Estado:** **Propuesta: decisión del revisor**
**Requisitos:** RF-093, RF-013, RF-002b, RF-032
**Contexto:** la ADR 0046 introdujo los perks letales y la medición destapó una asimetría

## El hallazgo

Los cuatro perks letales funcionan **contra el jugador** —las muertes por run han pasado de 0,06 a 0,64— pero **un perk letal en tu propia plantilla no puede matar a nadie**. Medido con una build orca que lleva los cuatro: los perks se disparan (entre el 71% y el 100% de los partidos) y producen **cero muertes rivales en sesenta partidos**.

La causa es estructural y encadena dos reglas razonables:

1. RF-093 exige que la víctima **no esté sana**: un jugador sano nunca muere.
2. Cuando alguien se lesiona, **sale del campo en el acto**, así que deja de ser alcanzable.
3. Los rivales se generan **siempre sanos** al construir el partido, porque no arrastran una run propia.

Resultado: la única forma de que exista una víctima válida es que alguien **entre al campo ya herido**, y eso solo lo hace el jugador, que es quien decide su alineación. Los rivales nunca lo hacen.

## Qué significa

El juego permite **sufrir** la carnicería pero no **administrarla**, que es justo lo que su propio nombre promete. Y deja a las builds de violencia sin su culminación: puedes lesionar, derribar y expulsar, pero no matar.

Hay una consecuencia adicional en la fantasía de raza: los orcos son *"el núcleo de las builds de violencia"* según la tabla de razas, y su techo queda por debajo de lo que el documento les promete.

## Tres salidas

1. **Que los rivales lleguen tocados.** Los equipos de los actos 2 y 3 también han jugado partidos: darles una probabilidad de arrancar con uno o dos jugadores con lesión leve o grave los hace vulnerables sin cambiar ninguna regla. Es la salida más barata, refuerza la ficción —el rival del acto 3 llega a rastras, como tú— y hace que **tus perks letales tengan sentido justo donde deben tenerlo**, tarde en la run.
2. **Que la lesión no saque del campo inmediatamente.** Un jugador lesionado podría seguir sobre el terreno unos ticks antes de retirarse, lo que abre una ventana para rematarlo. Es más fiel a la idea de "carnicería" pero toca el motor y afecta a todas las resoluciones, no solo a las letales.
3. **Aceptar la asimetría**: matar es cosa del rival y el jugador solo la sufre. Coherente con el tono —el desgaste es algo que te pasa, no algo que haces— pero decepcionante para una build de violencia y difícil de explicar a un jugador que acaba de recibir un perk letal.

## Recomendación

La **1**, y medir después la aniquilación con cuidado: matar rivales baja su plantilla y por debajo de cinco pierden por incomparecencia (RF-002b). Ganar así debe seguir siendo posible pero lento y caro, nunca la vía eficiente.

Si tras aplicarla los perks letales del jugador siguen sin producir muertes, entonces la respuesta honesta es la 3, y conviene decirlo en la descripción de esos perks en vez de prometer algo que no ocurre.
