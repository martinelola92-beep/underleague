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

---

## Decisión de diseño del revisor (23 sep 2026)

> «Yo creo que debería existir conducción con duración porque si no regate tampoco entra en juego.»

### La premisa, medida (200 partidos, referencia, semilla 1, `DribbleMeasurementTests`)

| | por partido |
|---|---|
| **regates intentados** | **0,85** |
| ganados / perdidos | 0,69 / 0,16 |
| posesiones | 70,7 |
| ticks con dueño | **29,3 %** |
| duración media de una posesión | **5,49 ticks = 0,37 s** |
| posesión más larga de cada partido | 19,9 ticks = 1,33 s (máximo absoluto 49 ticks = 3,27 s) |

**CONFIRMED**: el regate no entra en juego. 0,85 por partido contra 8,6 tiros, 12,5 entradas, 7,4 faltas y
~43 pases. Existe en `Utility.EvaluateDribble` —con pendientes por técnica y velocidad— y es invisible.
Cuando ocurre funciona (81 % ganados), pero casi nunca ocurre.

**Y el regate no estaba instrumentado**: ni métrica en `/Balance`, ni campo en el informe, y `MatchLogView`
omite `DRIBBLE_ATTEMPTED`. Antes de esta medición, «el regate no entra en juego» era una impresión.

### Las diez preguntas (`game-design-review`)

1. **Qué experimenta el jugador.** Hoy el balón salta de pie en pie: un partido es una cadena de toques y
   disputas, y **nadie lleva el balón**. Con conducción, alguien avanza con él y aparece la tensión de «¿le
   van a entrar?», que es justo donde vive la carnicería de este juego.
2. **Qué decisión toma con esto.** Hoy, **ninguna**: la técnica influye en la utilidad del regate, pero con
   0,85 regates por partido un bicho técnico no se distingue por regatear. Es un modificador invisible, que
   es la señal de alarma del propio principio (`comportamiento observable > modificadores invisibles`).
3. **Qué decisión debería tomar.** Alinear técnica y velocidad arriba debería tener una consecuencia que se
   vea: «este lleva el balón» contra «este lo pierde».
4. **Qué regla representa.** El **regate** sí está en los requisitos (eventos `REGATE_INTENTADO /
   GANADO / PERDIDO`, §218). La **conducción con duración NO está**: es una regla nueva y hay que decirlo.
   Y le aplica RF-012d: si conducir expone a la entrada, tiene que poder anunciarse antes.
5. **Qué sistemas.** `/Sim`: `Utility.EvaluateDribble` y el motor (un estado de conducción con duración
   toca la transferencia de balón y el enfriamiento de la entrada). `/data`: pesos de IA
   (`DribbleOpenSpaceBonus`, `DribbleOpponentAheadPenalty`, pendientes de técnica y velocidad). `/Game`:
   **ya está listo** — la animación y el balón al pie existen desde hoy y no tienen nada que mostrar.
6. **Alternativas.** (a) conducción como acción con duración; (b) **subir el peso del regate en `/data`**,
   sin tocar el motor; (c) que el portador pase menos, atacando `passChainAvgLength` de frente.
7. **Trade-off.** Conducir tiene que **costar riesgo**: más tiempo con el balón, más exposición a la
   entrada y a la lesión. Si conducir es gratis, es poder gratis.
8. **Cómo cambia las estrategias.** Técnica y velocidad pasan a valer; la build violenta gana objetivos que
   antes no existían (alguien a quien entrarle). Riesgo: si todos conducen, se **aplana** la
   diferenciación, que es el patrón que ya rompió tres intentos de la familia BE.
9. **Puede degenerar.** Sí: un buen conductor cruzando el campo sin oposición baja pases, intercepciones y
   sucesos — menos carnicería por partido. `possessionChanges` (hoy 21,52, banda 12-28) puede caer, y
   `passChainAvgLength` (2,01 en banda 1,80-3,50) subir fuera de banda.
10. **Cómo se demuestra.** `DribbleMeasurementTests` como línea base (ya existe), las 43 puertas y un lote
    de `/Balance` con dos semillas, vigilando `possessionChanges`, `passChainAvgLength`, `shotsPerMatch` y
    las métricas de diferenciación de builds.

### Recomendación: (b) antes de (a), y es la regla del proyecto, no pereza

La vía (b) —subir los pesos del regate en `/data`— **no toca el motor, se mide hoy mismo y discrimina**: si
con el regate en 5-6 por partido la técnica ya se nota y la posesión se alarga sola (regatear ocupa ticks
sin soltar el balón), la mecánica nueva no hace falta. Y si no basta —el regate sube pero la posesión sigue
en 0,37 s— entonces (a) está **justificada con datos** en vez de por intuición, que es exactamente lo que
la Regla A pide: *no se modifica código mientras exista una medición de bajo coste capaz de discriminar*.

**Pendiente de la palabra del revisor**: empezar por (b) y medir, o ir directo a (a).
