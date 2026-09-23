# BI-D — Nadie conduce el balón: tiene dueño un tercio del partido y 0,33 s de media

Estado: **ABIERTA, y no es un problema de render** (23 sep 2026). Es una pregunta de diseño para el
revisor, con números.

## De dónde sale

El revisor, jugando la maqueta de modelos 3D: *«aún así el balón no va en sus pies. No se nota como si lo
controlara»*. La primera hipótesis fue de presentación —el balón se apartaba en el eje del campo en vez de
hacia donde corre el jugador, y a 0,51 casillas ≈ 1 m de su centro— **y se arregló** (ver abajo). Pero al
intentar **capturar** a un humano conduciendo para verificarlo, no había ninguno. Eso destapó lo de verdad.

## Medido (partido del club humano, semilla 20260905, 1.200 ticks)

| | |
|---|---|
| ticks con **dueño** del balón | **387 de 1.200 — el 32,3 %** |
| posesiones en el partido | **77** |
| duración media | **5,0 ticks = 0,33 s** |
| posesión más larga de **cualquiera** | **14 ticks = 0,93 s** |
| posesión más larga del equipo propio | 5 ticks = 0,33 s |

**Dos tercios del partido el balón no es de nadie**: está suelto o en vuelo. Y cuando alguien lo tiene, lo
tiene un tercio de segundo.

## Por qué importa, y por qué no lo arregla el render

Ninguna colocación del balón hace que «se note el control» si **no hay control que mostrar**: no existe el
estado de *conducir* con duración. El motor resuelve la jugada como una cadena de toques —`passChainAvgLength`
2,01, `possessionChanges` 21,5 por partido, que son las métricas con banda— y el balón viaja entre pies.

Es coherente con lo que el motor dice de sí mismo, y **puede ser lo correcto**: Underleague resuelve el
partido por decisiones de utilidad tick a tick, no por dribbling. Pero entonces la lectura del jugador
—«esto no parece fútbol, nadie lleva el balón»— es una consecuencia de diseño, no un bug, y merece
decidirse a la vista de estos números.

## Lo que NO se ha hecho, a propósito

Alargar la posesión toca `/Sim`: mueve `possessionChanges` (hoy 21,52 con techo 28), `passChainAvgLength`
(2,01 en banda 1,80-3,50) y probablemente tiros y goles. Es **mecánica, no presentación**, así que pide
`game-design-review` y decisión del revisor antes de tocar una línea — y ADR si se toca (RT-057).

Tres caminos posibles, sin recomendación todavía porque ninguno está medido:

1. **Dejarlo** y aceptar que el balón es el protagonista, no el portador. Gratis, y consistente con la
   identidad del juego (el partido es una carnicería, no una exhibición técnica).
2. **Un estado de conducción con duración** en `/Sim`: el portador mantiene el balón mientras avanza, en vez
   de resolver un toque por tick. Es la que más cambia la sensación y la que más balance mueve.
3. **Solo presentación**: que el balón quede pegado a los pies del último tocador mientras esté suelto y
   cerca. Miente un poco —el balón no es suyo— y choca con *«comportamiento observable > modificadores
   invisibles»*: haría parecer que alguien lleva el balón cuando el motor dice que no.

## Lo que sí se arregló, que era real

El balón se apartaba del portador **en el eje largo del campo** (hacia la portería que ataca) y a **0,51
casillas ≈ 1 m** de su centro, cuando al pie le corresponden ~0,35 m (0,175 casillas). Ahora se aparta
**hacia donde corre** —la misma dirección que ya orienta al modelo— y a `radio × 0,55`, que escala con el
tamaño de la raza. Las cápsulas conservan la separación de siempre porque son gordas y el balón se les
metería dentro.
