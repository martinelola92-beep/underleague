# Referencia: IA y física de balón en motores de fútbol open-source

Conclusiones aplicables a Underleague de tres investigaciones independientes sobre motores de fútbol
reales, cada una con código fuente leído directamente (no resumido de terceros, no memoria de
entrenamiento). El encargo excluyó a propósito el lado de aprendizaje por refuerzo de estos proyectos
—Underleague tiene la decisión de "sin ML" ya tomada y registrada, no se reconsidera aquí— y se centró en
la lógica de reglas/geometría que sigue funcionando por debajo de cualquier capa de IA que un proyecto
pueda tener encima.

Las tres investigaciones convergieron, sin coordinarse entre ellas, en la misma pieza que falta en
Underleague. Eso pesa más que cualquier hallazgo aislado.

## 1. El hallazgo que más importa: la intercepción no es una técnica aparte, es no saltarse el bucle normal

Hoy en Underleague, un disparo a portería vuela en línea recta hasta la línea de gol sin que nadie pueda
tocarlo por el camino (`TryIntercept` se salta explícitamente durante el vuelo de un tiro), y la parada se
calcula con fórmulas de atributos en el instante de llegada, sin mirar dónde está el portero. Es AW-A en
`docs/pendientes.md`.

**Google Research Football no tiene ningún caso especial de "intercepción de disparo".** El balón es un
objeto físico simulado en todo momento, y `AI_HasPossession` se llama para **cada jugador de los dos
equipos, cada tick**, comprobando distancia a la posición actual del balón. Si un jugador —incluido un
defensor rival— está dentro de un radio de alcance mientras el balón pasa cerca, gana la posesión y el
disparo se corta ahí. No hay una función de "¿puede este disparo ser interceptado?": la intercepción sale
gratis de tratar el balón en vuelo exactamente igual que un balón suelto en cualquier otro momento del
partido.
- `AI_HasPossession`: `third_party/gfootball_engine/src/onthepitch/AIsupport/AIfunctions.cpp:843-864`
- Llamada por jugador y por tick: `third_party/gfootball_engine/src/onthepitch/player/player.cpp:247-289`
- https://github.com/google-research/football/blob/master/third_party/gfootball_engine/src/onthepitch/AIsupport/AIfunctions.cpp#L843-L864

**HELIOS-base/librcsc (RoboCup 2D Soccer Simulation, código en producción real) ya resuelve esto por
ciclos discretos**, que es el mismo modelo que los ticks de Underleague, no física continua. Para cada
ciclo futuro compara `distancia(jugador, balón_en_ese_ciclo)` contra
`área_de_control + velocidad_máxima × ciclos_disponibles`, y devuelve el primer ciclo en que el alcance
supera la distancia:
```
for (int total_step = min_step; total_step < max_step; ++total_step) {
    ball_pos = M_ball_cache[total_step];
    if (pow(control_area + max_speed*(total_step + bonus - penalty) + 0.5, 2) < dist2(player_pos, ball_pos))
        continue;
    if (canReachAfterTurnDash(...)) return total_step;
}
```
- `rcsc/player/intercept_simulator_player.cpp`, `intercept_table.cpp` — https://github.com/helios-base/librcsc

**Gameplay Football (vía el mismo motor `gfootball_engine`, ver nota de fuentes) añade el detalle que
faltaba: la comparación de tiempos vale para cualquier jugador, no solo el portero, y separa dos radios de
alcance.** `AI_GetTimeNeededForDistance_ms` simula, en pasos de 10 ms, la transición de la inercia actual
del jugador a sprint hacia el objetivo (~700 ms de "giro"), expandiendo un radio de alcance hasta que
supera la distancia al punto. Da dos resultados: `usual_ms` (radio 0,28 m, "alcance de pierna" normal) y
`optimistic_ms` (radio 0,9 m, entrada a la desesperada/estirada), usados para decidir si vale la pena una
entrada de última hora. `Player::UpdatePossessionStats` llama a esto **cada tick, para cada jugador**,
muestreando la trayectoria futura del balón.
- `AI_GetTimeNeededForDistance_ms`: `third_party/gfootball_engine/src/onthepitch/AIsupport/AIfunctions.cpp:380-519`
- `Player::UpdatePossessionStats`: `third_party/gfootball_engine/src/onthepitch/player/player.cpp:169-249`
- `Player::AllowLastDitch` (uso de los dos radios): `player.cpp:147`
- El portero en concreto intersecta la trayectoria extrapolada del balón con la línea de gol y, si no va a
  gol, corre a la bisectriz de los postes; si va a gol, se mueve al punto de su propia trayectoria de tiro
  más cercano a él (`Line::GetDistanceToPoint`, parámetro `u` recortado a [0,1]) —
  `GoalieDefaultStrategy::CalculateIfBallIsBoundForGoal`, `goalie_default.cpp:44-300`
- https://github.com/google-research/football/blob/master/third_party/gfootball_engine/src/onthepitch/player/controller/strategies/offtheball/goalie_default.cpp#L237-L300

### Cómo se vería en Underleague

Ninguna de las tres fuentes usa geometría continua imprescindible: HELIOS-base ya trabaja por ciclos
enteros, que es el modelo de Underleague. La traducción es directa y no exige tocar RT-023 (aritmética
entera):

1. Al lanzar un tiro, en vez de resolver solo "llegada", **precalcular la casilla del balón en cada tick**
   del vuelo (ya existe la velocidad de tiro y la trayectoria en línea recta).
2. En cada uno de esos ticks, para el portero y para cualquier defensor con la trayectoria dentro de su
   alcance, comprobar `distancia_en_casillas(jugador, casilla_del_balón_en_ese_tick) ≤ velocidad_del_jugador_en_casillas_por_tick × ticks_disponibles`
   — la misma cuenta entera que HELIOS-base, sustituyendo "área de control" por lo que ya haya de alcance
   de entrada en Underleague.
3. El primer jugador que cumpla la condición para el tick correspondiente es quien decide el desenlace: si
   es el portero, se resuelve como parada (con la fórmula de atributos que ya existe, pero ahora
   condicionada a que haya llegado); si es un defensor, como intercepción/bloqueo antes de que el disparo
   llegue a puerta.
4. El radio "normal" frente a "desesperado" de Gameplay Football es una separación barata de añadir después
   y encaja con el vocabulario que Underleague ya tiene de reservar casos "de última hora".

Esto no es una técnica añadida al motor: es dejar de saltarse, para el caso concreto del disparo, el mismo
bucle de proximidad que ya resuelve cualquier balón suelto en otro momento del partido. Encaja además con
RF-012d (todo lo malo debe ser previsible): un defensor en la trayectoria deja de ser invisible para el
cálculo.

## 2. Evaluación de la línea de pase: alcance dependiente del tiempo, no un radio fijo

Underleague ya comprueba bloqueo de segmento para el pase largo, pero con un radio fijo
(`PassLaneRadius`). Dos fuentes coinciden en una versión mejor: el "radio de peligro" de un rival sobre la
línea de pase no es constante, crece con el tiempo que tarda el balón en llegar a ese punto.

**SimpleSoccer** (Mat Buckland, *Programming Game AI by Example*) proyecta al rival al sistema de
coordenadas local del segmento pasador→receptor. Si está detrás del pasador, el pase se da por seguro. Si
no, calcula `TimeForBall` (tiempo del balón hasta el punto de la línea más cercano al rival) y
`alcance = velocidad_máxima_rival × TimeForBall + radios`; el pase es inseguro si la distancia lateral del
rival es menor que ese alcance.
- `SoccerTeam::isPassSafeFromOpponent`: `SoccerTeam.cpp:302-378`
- https://github.com/wangchen/Programming-Game-AI-by-Example-src/blob/master/Buckland_Chapter4-SimpleSoccer/SoccerTeam.cpp

**Google Research Football usa la misma idea para decidir hacia qué punto de la portería disparar**: traza
la línea pasador/tirador→objetivo, calcula distancia perpendicular de cada rival y compara
`ballToIntersect_sec` contra `oppToIntersect_sec`; la diferencia recortada es el "peligro", y
`odds = 1 - peligro`. Para el tiro, prueba tres puntos de mira (palo izquierdo, centro, palo derecho) y se
queda con el de mayor `odds`.
- `ElizaController::_GetPassingOdds`: `elizacontroller.cpp:1112-1159`; uso para el tiro en `:988-1032`
- https://github.com/google-research/football/blob/master/third_party/gfootball_engine/src/onthepitch/player/controller/elizacontroller.cpp#L1112-L1159

### Cómo se vería en Underleague

Discretizar la línea pasador→receptor en las casillas que atraviesa (Bresenham de grid, la misma técnica
que ya haría falta para el punto 1), y para cada rival cercano calcular su casilla proyectada más cercana a
esa línea. El "alcance" del rival en esa casilla se compara en ticks, igual que en el punto 1: un rival
lejos en la trayectoria pero con tiempo de sobra para llegar sigue siendo una amenaza real; uno cerca pero
con el balón ya pasado, no. Esto generaliza el bloqueo de segmento del pase largo (hoy radio fijo) y podría
extenderse también al corto sin coste grande, dado que la geometría de grid ya haría falta para el punto 1.

## 3. Posicionamiento sin balón: lo que ya existe es de la misma familia que estos motores reales

Ninguna de las tres fuentes usa un mapa de ocupación por casilla ni campos de potencial para el desmarque
sin balón (sí existe un campo de potencial real en Google Research Football, pero solo para la dirección de
regate del jugador **con** balón — `AI_GetBestDribbleMovement`, `AIfunctions.cpp:238-378` — no aplica a
esta pregunta). El posicionamiento sin balón en Gameplay Football es un slot de formación fijo deformado
por unos catorce sesgos de rol (profundidad, anchura, campo propio, enfoque de centrocampo, enfoque hacia
el balón, ponderados por posesión), sin predicción de dónde va a estar el balón ni ocupación entre
jugadores.
- `TeamAIController::GetAdaptedFormationPosition`: `teamAIcontroller.cpp:321-476`
- `AI_GetAdaptedFormationPosition`: `AIfunctions.cpp:35-159`

SimpleSoccer sí calcula una rejilla de puntos candidatos (`SupportSpotCalculator`) recalculada a baja
frecuencia (no cada tick — hay un regulador explícito de frecuencia, un recordatorio de rendimiento útil
por sí solo), con tres términos aditivos: pase seguro desde ese punto, poder marcar gol desde ahí, y una
función triangular de distancia óptima al portador del balón (máximo en la distancia ideal, cero fuera de
rango). **No tiene ningún término de aglomeración con compañeros** — se cita explícitamente para no
inflar la conclusión.
- `SupportSpotCalculator.cpp` — https://github.com/wangchen/Programming-Game-AI-by-Example-src/blob/master/Buckland_Chapter4-SimpleSoccer/SupportSpotCalculator.cpp

**Veredicto**: la correa por posición y el desplazamiento de bloque táctico que ya tiene Underleague son de
la misma familia que estos motores reales usan (slot deformado por sesgos, no mapa de ocupación). No hay
evidencia en ninguna de las tres fuentes de que falte un enfoque más sofisticado aquí. Lo único con motivo
real de incorporarse, si algún día se retoma el desmarque, es el término triangular de "distancia óptima al
portador" de SimpleSoccer como complemento (no sustituto) de la penalización de aglomeración que
`FindSpace`/`OfferSupport` ya tienen — y el "pase seguro desde esta casilla" del punto 2 como un término más
de esa misma utilidad, no solo del pase en sí.

## 4. Comparación con árboles de comportamiento

Ni SimpleSoccer ni HELIOS-base usan árboles de comportamiento para el detalle espacial. Los dos usan una
máquina de estados/árbol de decisión solo para el **rol táctico de alto nivel** (atacar/defender/perseguir)
y utilidad numérica para todo lo de bajo nivel (qué casilla, a quién pasar, si un pase es seguro). El
"estado táctico" de Underleague (en posesión / fuera de posesión / transición), que pondera los pesos base
de cada acción, ya cumple ese papel de selector de alto nivel sin necesitar un árbol aparte: es utility AI
jerárquica de facto. No hay evidencia, con lo encontrado, de que falte introducir un árbol de
comportamiento.

## 5. Descartado explícitamente, con motivo

- **Redes neuronales, políticas de RL, funciones de recompensa** de Google Research Football
  (`gfootball/env/`): fuera de alcance por decisión de arquitectura ya tomada (RT-091, sin ML), no
  reconsiderada aquí.
- **Física 3D continua de balón** (gravedad, drag, bote): Underleague no simula altura, y no hay necesidad
  de portar esa parte — la técnica de intercepción del punto 1 funciona igual sobre una trayectoria 2D en
  línea recta, discretizada en ticks.
- **Fórmula cuadrática de intercepción con blanco acelerado** ("Predictive Aim Mathematics for AI
  Targeting", gamedeveloper.com): resuelve el caso general de un blanco con aceleración variable.
  Innecesaria mientras el balón de Underleague viaje a velocidad constante por tick; la versión discretizada
  de HELIOS-base ya cubre el caso real.
- **Mapa de ocupación por casilla** (una rejilla que registre cuántos jugadores cubren cada celda, aparte
  de la distancia al rival/compañero más cercano): investigado explícitamente y no encontrado en ningún
  proyecto real de los tres investigados. No hay evidencia que lo respalde; mantener lo que ya existe.
- El repositorio literal "Gameplay Football" de Bastiaan Konings Schuiling
  (`BazkieBumpercar/GameplayFootball`) está descontinuado y depende de un motor no incluido
  ("Blunted2"); no es legible ni compilable. Lo citado en este documento bajo su nombre viene del fork que
  mantiene Google del mismo motor C++ original con licencia limpia (Apache 2.0), `google-research/football`,
  carpeta `third_party/gfootball_engine` — se anota aquí para que quede claro de dónde sale cada cita.

## Siguiente paso sugerido

El hallazgo del punto 1 es el más fuerte de los tres informes y ataca directamente AW-A, que sigue abierto
en `docs/pendientes.md`. Es un cambio en `/Sim` (probablemente `MatchEngine.Shoot`/`ResolveShotArrival` y
la lógica de intercepción), con el mismo tipo de coste que AW-D y AW-E: tests + lote de balance, porque
toca directamente cuántos goles se marcan.

## Fuentes

- Google Research Football — https://github.com/google-research/football (motor C++ en
  `third_party/gfootball_engine/src/`, Apache 2.0; excluida la parte de RL en `gfootball/env/`)
- HELIOS-base / librcsc (RoboCup 2D Soccer Simulation League) — https://github.com/helios-base/librcsc
- SimpleSoccer, Mat Buckland, *Programming Game AI by Example* —
  https://github.com/wangchen/Programming-Game-AI-by-Example-src/tree/master/Buckland_Chapter4-SimpleSoccer
- "Predictive Aim Mathematics for AI Targeting" —
  https://www.gamedeveloper.com/programming/predictive-aim-mathematics-for-ai-targeting (citada solo para
  descartarla como innecesaria, ver §5)
- "Gameplay Football" (Bastiaan Konings Schuiling) —
  https://github.com/BazkieBumpercar/GameplayFootball (repositorio original, no legible/compilable; ver
  nota de §5 sobre por qué las citas de este documento bajo ese nombre vienen del fork de Google)

No se encontró ningún GDC talk indexado específicamente sobre utility AI en deportes, ni ningún proyecto
Unity/C# o Rust comunitario con mapas de ocupación por casilla más allá de lo ya citado; se deja anotado en
vez de rellenar el hueco con una fuente débil.
