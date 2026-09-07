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

## 6. Segunda ronda: línea defensiva, balón muerto y persecución

Investigación posterior (7 sep 2026), motivada por tres observaciones del revisor tras jugar la build de
Windows: (1) los defensas suben demasiado y el delantero rival se coloca a su espalda; (2) durante una pausa
—portero que atrapa, saque de banda— los jugadores se quedan clavados donde estaban, apiñados; (3) los
defensas persiguen el balón en exceso. Y una pregunta explícita: si el fuera de juego arreglaría (1).

Fuentes leídas directamente: los dos árboles locales del linaje `gfootball_engine` (el de Google y el
subconjunto de GameplayFootball), `librcsc` y el agente `helios-base` descargados del repositorio oficial, y
SimpleSoccer del repositorio ya citado en §2 y §3. Donde una fuente no responde, se dice.

Nota de verificación: los tres bloques de código de `gfootball_engine` que sostienen §6.1 y §6.3
(`AI_GetOffsideLine`, el cálculo de `offsideTrapX` y `ApplyOffsideTrap`) son **idénticos byte a byte y en las
mismas líneas** en los dos árboles locales, así que son del motor original de Konings, no una aportación de
Google. El árbitro (`referee.cpp`) solo existe en el árbol de Google, de modo que la aplicación de la regla
como falta solo se ha podido verificar ahí.

### 6.1 Línea defensiva y fuera de juego

**Sí, gfootball implementa fuera de juego, y lo implementa dos veces: como falta y como restricción de
posicionamiento.** Las dos capas son independientes y la segunda es la que importa para la observación (1).

*Capa árbitro.* `Referee::BallTouched` (`third_party/gfootball_engine/src/onthepitch/referee.cpp:309-371`)
funciona por instantáneas: en cada toque guarda la lista de jugadores del equipo que ha tocado que estaban más
allá de la línea (`offsidePlayers`); en el **toque siguiente**, si quien toca está en esa lista, se pita
—`match->StopPlay()`, `e_GameMode_FreeKick`, 2 s de preparación y 2 s más hasta el saque—. Es decir, se evalúa
en el instante del pase y se resuelve en la recepción, como la regla real. Es opcional
(`GetScenarioConfig().offsides`, `src/main.hpp:182-183`), se desactiva durante saques de banda y córners, y
también si el rival tiene un solo jugador activo.

*Cálculo de la línea.* `AI_GetOffsideLine`
(`third_party/gfootball_engine/src/onthepitch/AIsupport/AIfunctions.cpp:193-236`):

1. Recorre los rivales y localiza al **más adelantado** (normalmente el portero).
2. Vuelve a recorrerlos excluyendo a ese y se queda con el **segundo más adelantado** — el comentario del
   código lo dice con todas las letras: *"offside: we are actually looking for the one-but-deepest opponent
   (association football rule!)"*.
3. `offsideLine = max(segundo_más_adelantado, posición_del_balón)`.
4. Nunca cruza el medio campo hacia el propio: `if (offsideLine * side < 0) offsideLine = 0;`.
5. Parámetro opcional `futureSim_ms`: extrapola a los rivales por su velocidad actual antes de calcular.

Todo con dos pasadas lineales y una comparación de una sola coordenada. No hay geometría cara.

*Capa posicionamiento — la respuesta a la pregunta del revisor.* La posición de apoyo sin balón de **todo**
jugador de campo se recorta contra la línea, cada tick, siempre:
`ElizaController::GetSupportPosition_ForceField`
(`third_party/gfootball_engine/src/onthepitch/player/controller/elizacontroller.cpp:585-800`) declara
`forceNoOffside = true` fijo (`:603`), calcula `offsideX = AI_GetOffsideLine(match, _mentalImage, rival, 240)`
con 240 ms de anticipación (`:640-641`) y, tras resolver el campo de fuerzas (`:787`), **recorta la
coordenada de profundidad del destino a la línea** con un margen de 0,08 (`:790-793`). Un delantero no puede
aparcar a espaldas de la defensa porque su casilla-objetivo deja de existir ahí, no porque le piten.

**HELIOS-base hace exactamente lo mismo con la arquitectura invertida.** Ahí la falta la pita el servidor
(`rcssserver`), no el agente; el agente solo modela la línea y la usa como techo:

- `WorldModel::updateOffsideLine` (`rcsc/player/world_model.cpp:4279-4356`) parte de
  `M_their_defense_line_x` y toma el máximo con el punto de inercia del balón; devuelve el fondo del campo
  (línea desactivada) en saque de banda, córner y saque de puerta propio.
- `WorldModel::updateTheirDefenseLine` (`rcsc/player/world_model.cpp:4551-4694`) es el mismo algoritmo que
  gfootball: mantiene el primero y el **segundo** rival más adelantados y se queda con el segundo (`second_x`),
  usa el primero solo si no ve portero rival, toma el máximo con la posición del balón en el ciclo siguiente,
  y aplica `if (new_line < 0.0) new_line = 0.0` — el mismo tope de medio campo. Añade histéresis: si la línea
  nueva retrocede entre 5 y 13 m respecto a la anterior, solo retrocede 1 m por ciclo.
- `Strategy::updateFormation` (helios-base, `src/player/strategy.cpp:587-618`) recorta **las once posiciones
  base de la formación** a `offsideLineX() - 1.0` de una pasada, después de calcularlas.
  `Strategy::get_normal_dash_power` (`src/player/strategy.cpp:1116-1122`) da potencia de carrera máxima al
  jugador que se ha quedado en fuera de juego, para que salga.

**SimpleSoccer no implementa fuera de juego**: la palabra no aparece en `SoccerTeam.cpp` y no hay árbitro.

*Altura y compacidad de la línea.* Es un mecanismo aparte del fuera de juego, y es el que ataca de verdad la
observación (1). `TeamAIController::Process`
(`third_party/gfootball_engine/src/onthepitch/teamAIcontroller.cpp:101-152`) recalcula cada tick un
`offsideTrapX` = el punto más adelantado que la línea tiene permitido ocupar, como **máximo de cinco
candidatos**:

1. una base a `30 + 20 · offensivenessBias` metros de la portería propia;
2. la x del balón, pasada por una curva de repliegue que se endurece al acercarse a los 6 m de la portería;
3. la x del balón predicha a 700 ms;
4. el portador rival más 0,1-0,15 s de su movimiento más 4 m de cautela;
5. y —el término decisivo— **la propia línea de fuera de juego del equipo menos 4 m**
   (`allowSlackDistance = 4.0f`, `:143-149`, comentario *"slacking teammate as max"*). O sea: **la trampa
   nunca se pone a más de 4 m por delante del segundo jugador más retrasado del propio equipo**. Si un
   defensa se descuelga, el bloque entero deja de subir.

Y se aplica dos veces, no una: como recorte del límite trasero del bloque dentro de
`GetAdaptedFormationPosition` (`teamAIcontroller.cpp:417-422`), y otra vez sobre el destino individual de cada
defensa y cada centrocampista, cada tick, después de la componente defensiva —
`DefaultDefenseStrategy::RequestInput` (`.../strategies/offtheball/default_def.cpp:56`) y
`DefaultMidfieldStrategy::RequestInput` (`.../strategies/offtheball/default_mid.cpp:71`); el delantero
(`default_off.cpp`) no la aplica. `TeamAIController::ApplyOffsideTrap` (`teamAIcontroller.cpp:711-736`) no es
un recorte duro sino una **compresión**: la banda `[trap-2, trap+2]` se comprime en `[trap-2, trap]`, de modo
que quien está 2 m pasado acaba sobre la línea y quien está sobre ella no se mueve. Es un empujón suave y
continuo, no un teletransporte.

La compacidad propiamente dicha es otro número explícito: `adaptedDepth = depth · (offense_depthFactor ·
possessionBias + defense_depthFactor · (1 - possessionBias))` (`teamAIcontroller.cpp:376-379`) da la
profundidad del bloque, que se estrecha al perder la posesión, y de ahí salen `backXBound`/`frontXBound`
(`:405-411`), dentro de los cuales viven **todas** las casillas-hogar de la formación.

**¿Arreglaría el fuera de juego la incoherencia que describe el revisor?** No por sí solo, y conviene separar
las dos mitades:

- Lo que impide que el delantero acampe a la espalda de la defensa **no es el silbato, es el recorte
  posicional** (`forceNoOffside` en gfootball, el recorte de las once posiciones en HELIOS-base). Los dos
  motores lo hacen en el código de posicionamiento del atacante, y funcionaría igual aunque nadie pitara nada.
- Que nuestros defensas suban demasiado no es un problema de fuera de juego en absoluto: es que nuestro
  `blockShift` (`InPossession: +4.0` casillas sobre 16 columnas, `OutOfPossession: -1.0`, asimetría 4 a 1) no
  tiene **ningún techo**. gfootball nunca deja que el bloque suba por encima del mínimo entre balón, portador
  rival y su propio hombre más retrasado; nosotros desplazamos las casillas-hogar una cuarta parte del campo y
  no comprobamos nada. Ese es el mecanismo que falta, y no requiere la regla.

*Corrección a §3 de este documento*: allí se afirmaba que en gfootball el único campo de potencial es el del
regate. Es inexacto: `ElizaController::GetSupportPosition_ForceField` (`elizacontroller.cpp:585-800`) es un
campo de fuerzas real para el posicionamiento sin balón, con siete tipos de foco (posición base con carril
alterno, repulsión de rivales ponderada por rol ×1,0 a ×2,2, repulsión de compañeros, repulsión de cuatro
puntos futuros de la trayectoria del balón, atracción y repulsión simultáneas al portador para mantener la
distancia ideal). La conclusión de §3 —que nuestra correa por posición es de la misma familia— sigue en pie;
lo que no se sostiene es que allí no haya campos de potencial fuera del regate. El detalle importa aquí porque
**ese** es el sitio donde vive el recorte de fuera de juego.

### 6.2 Reposicionamiento durante balón muerto

Los tres motores resuelven la pausa, y ninguno la resuelve congelando a los jugadores donde estaban. Hay dos
recetas distintas.

**gfootball: teletransporte a una formación de saque, y a partir de ahí sí se quedan quietos.** La secuencia
de `Referee::Process` (`third_party/gfootball_engine/src/onthepitch/referee.cpp:93-250`) es:

1. el balón sale → `match->StopPlay()`, `stopTime = ahora`, `prepareTime = ahora + 2000`,
   `startTime = prepareTime + 2000` (para el saque de centro tras gol, 500 + 500);
2. en `prepareTime`, `Referee::PrepareSetPiece` (`referee.cpp:275-299`) llama a `Match::ResetSituation` y a
   `TeamAIController::PrepareSetPiece` de los **dos** equipos;
3. en `startTime`, `StartPlay()` + `StartSetPiece()`; el saque se cierra cuando el ejecutor toca el balón, con
   400 ms de gracia después.

El paso 2 es el que importa: `TeamAIController::PrepareSetPiece` (`teamAIcontroller.cpp:739-940`) recorre a
todos los jugadores y llama a `Player::ResetPosition(posición, foco)` para cada uno —un teletransporte
instantáneo (`HumanoidBase::ResetPosition`, `player/humanoid/humanoidbase.cpp:814-836`: fija posición, ángulo
y pone a cero todas las componentes de movimiento) que además les hace mirar al balón—. El portero va a
`0,98 · pitchHalfW` sobre su línea. Y los de campo se colocan reutilizando **la misma**
`AI_GetAdaptedFormationPosition` de juego abierto, solo que con límites y focos propios de cada reanudación:

- saque de puerta: `backXBound/frontXBound` a `0,5 / -0,2` del semicampo para el equipo que saca, `0,4 / -0,1`
  para el que defiende;
- córner: el atacante empujado hasta `0,96` del campo rival con `xFocusStrength 0,7` y `midfieldFocus 0,9`; el
  defensor comprimido entre `0,98` y `0,5` de su propio campo con el foco en Y sobre el balón;
- saque de banda: límites anclados a ±30 y ±20 m de la x del balón;
- saque de centro: formación reflejada en campo propio, prohibido el círculo de 9,4 m, ±2 m de ruido para que
  no se solapen, y los dos jugadores más cercanos al centro llevados al punto de saque
  (`teamAIcontroller.cpp:806-814`).

No es una "formación de saque" escrita a mano: es la formación de siempre evaluada con otros límites. Después,
durante los 2 + 2 s, los jugadores que no sacan efectivamente no deciden nada —todas las ramas de
`ElizaController::GetCommands` que producen movimiento sin balón están guardadas por
`match->IsInPlay() && !match->IsInSetPiece()` (`elizacontroller.cpp:308, 338, 351, 382, 398, 416`)—, pero se
quedan quietos **en la forma a la que se les acaba de teletransportar**, no en la que tenían cuando murió el
balón. Detalle de eficiencia que nos sirve: en modo sin animaciones el motor ni siquiera simula el tiempo
muerto, lo salta con `match->BumpActualTime_ms(1900)` (`referee.cpp:166, 179, 207`).

**SimpleSoccer: la receta opuesta, siguen moviéndose durante la pausa y el juego no se reanuda hasta que están
colocados.**

- `PrepareForKickOff::Enter` (`Buckland_Chapter4-SimpleSoccer/TeamStates.cpp:140-148`) borra los punteros de
  portador, apoyo, receptor y jugador más cercano al balón, y llama a `SoccerTeam::ReturnAllFieldPlayersToHome`
  (`SoccerTeam.cpp:435-450`), que despacha `Msg_GoHome` a todos los jugadores de campo.
- `ReturnToHomeRegion::Execute` (`FieldPlayerStates.cpp:421-450`): mientras `!GameOn()`, el jugador sigue
  dirigiéndose a su destino y solo pasa a `Wait` cuando `AtTarget()`; el paso a `ChaseBall` está explícitamente
  condicionado a `GameOn()`.
- `Wait::Enter` (`FieldPlayerStates.cpp:~485`) contiene el detalle exacto que nos falta:
  `if (!player->Pitch()->GameOn()) player->Steering()->SetTarget(player->HomeRegion()->Center());` — con el
  juego parado, el destino de quien espera es el centro de su región, y `Wait::Execute` sigue llegando a él.
- `PrepareForKickOff::Execute` (`TeamStates.cpp:150-157`) **no reanuda** hasta que `AllPlayersAtHome()` es
  cierto para los dos equipos; `PrepareForKickOff::Exit` es quien llama a `SetGameOn()`.
- Y cada vez que el equipo cambia de estado táctico, `SoccerTeam::UpdateTargetsOfWaitingPlayers`
  (`SoccerTeam.cpp:563-579`) reasigna el destino de todos los que están en `Wait` o `ReturnToHomeRegion`: un
  jugador parado nunca se queda con un destino rancio.

**HELIOS-base** no tiene "pausa" en absoluto desde el punto de vista del agente: el servidor sigue mandando
ciclos y el agente sigue decidiendo. El modo de juego es un **parámetro** del mismo código de posicionamiento
—`Strategy::updateFormation` fuerza `max_x = 0.0` en `BeforeKickOff` y `AfterGoal_`
(`src/player/strategy.cpp:590-594`), y `WorldModel::updateOffsideLine` desactiva la línea en saque de banda,
córner y saque de puerta (`rcsc/player/world_model.cpp:4287-4306`)— nunca una suspensión de él.

Los tres, con recetas distintas, coinciden en lo mismo: **el balón muerto es el momento en que se recoloca a
todo el mundo**, no un intervalo en el que se deja de recolocar.

### 6.3 Persecución del balón por los defensas

Ninguno de los tres motores usa una penalización blanda para evitar que varios defensas vayan al balón. Los
tres usan **designación dura con histéresis**, y le dan al resto algo concreto que hacer.

**gfootball, tres capas superpuestas:**

1. *Un solo perseguidor designado por equipo.* `Team::UpdateDesignatedTeamPossessionPlayer`
   (`third_party/gfootball_engine/src/onthepitch/team.cpp:182-187`) lo fija como el jugador más cercano al
   balón, y `Team::Process` (`team.cpp:474-509`) lo refina por **tiempo** hasta el balón con histéresis
   explícita: el aspirante solo releva al titular si
   `timeRating = (tiempo_aspirante + 500) / (tiempo_titular + 500) < 0.8` —un 20 % mejor—, con el cociente
   multiplicado o dividido por 0,5 según quién tenga la posesión, y con `+0.2` y `×1.2` a favor del titular si
   este llega al balón antes que el rival más cercano. Comentario del código: *"switch only if other player is
   somewhat better, to overcome possession-chaos"*.
2. *Como mucho dos cazadores.* Un jugador no designado solo va a por el portador rival si se cumplen cuatro
   condiciones a la vez (`elizacontroller.cpp:416-500`): su equipo no tiene la mejor posesión; **no** tiene
   asignado marcaje al hombre; el portador está a menos de `huntDistanceThreshold = 10 + 10 · (1 - mindset)`
   metros, escalado por fatiga, velocidad actual y dificultad de la IA (`:425-431`); y está entre los **dos
   más cercanos** al portador (`huntingPlayersNum = 2`, `AI_GetClosestPlayers(..., 2)` y una comprobación
   explícita de pertenencia, `:460-476`). Si falla cualquiera, se vuelve a su posición de formación. Son
   compuertas, no penalizaciones.
3. *Marcaje exclusivo.* `TeamAIController::CalculateManMarking` (`teamAIcontroller.cpp:647-709`) asigna
   marcador a los **tres** rivales más peligrosos (`numMarkedOpponents = 3`), ordenados por `dangerFactor`
   (distancia a un punto que es 80 % boca de portería + 20 % balón a +100 ms, con +0,05 si es el portador,
   `:157-180`). Para cada uno elige al mejor marcador libre según `CalculateMarkingQuality` (`:598-645`:
   distancia a la recta rival→portería, posición sobre esa recta, ×0,6 si ya le han pasado) y **lo saca del
   grupo** (`players.erase`), de modo que nadie marca a dos y nadie se dobla.

**HELIOS-base:** `Bhv_BasicMove::execute` (helios-base, `src/player/bhv_basic_move.cpp:73-88`) intercepta solo
si `!kickableTeammate && (self_min <= 3 || (self_min <= mate_min && self_min < opp_min + 3))`, es decir, si es
el más rápido de su equipo en llegar al balón medido en **ciclos** por la tabla de intercepción, o si puede
llegar en tres ciclos. Si no, va a su posición de formación y punto. Compuerta dura, sin gradaciones.

**SimpleSoccer:** entrar en `ChaseBall` exige `player->isClosestTeamMemberToBall()`
(`Buckland_Chapter4-SimpleSoccer/FieldPlayerStates.cpp:~315` desde `ReturnToHomeRegion` y `~395` desde
`Wait`), un booleano que calcula una sola vez por actualización `SoccerTeam::CalculateClosestPlayerToBall`
(`SoccerTeam.cpp:105-127`). Un perseguidor por equipo por construcción; el resto están en `Wait`,
`ReturnToHomeRegion` o `SupportAttacker`.

**Contraste con lo nuestro.** `chaseBallNotNearestPenalty: 300` es una penalización sobre una escala donde
`Defender.ChaseBall = 380` de base con multiplicador táctico `125` fuera de posesión, y donde conviven
`chaseBallLooseBonus: 250` y `chaseBallIncomingPassBonus: 700`. Esos 300 puntos equivalen a unas 6,7 casillas
del término de distancia (`chaseBallDistancePenaltyPerCell: 45`): al segundo y al tercer defensa más cercanos
les sigue saliendo más rentable perseguir que sus alternativas (`MarkOpponent 320 × 150`,
`CoverSpace 420 × 140`). Penaliza, pero no descalifica —y los tres motores descalifican—. Además, ninguno de
los tres deja la designación al vaivén del tick: gfootball la protege con el umbral del 20 %, SimpleSoccer la
recalcula una vez por actualización y la usa como precondición binaria.

### 6.4 Conclusiones aplicables

1. **El fuera de juego no hace falta para que el delantero deje de acamparse a espaldas de la defensa; hace
   falta el recorte que se deriva de la línea.** Los dos motores que la calculan la usan como techo de la
   posición objetivo del atacante sin balón (`forceNoOffside`, `elizacontroller.cpp:790-793`; recorte de las
   once posiciones, helios-base `src/player/strategy.cpp:601-617`), y eso funciona aunque nadie pite. En
   Underleague sería código nuevo en `Sim/Engine/Utility.cs`, en la evaluación de `FindSpace`/apoyo: una
   columna máxima por equipo que recorte la casilla-objetivo. No cambia ninguna regla de juego, no toca
   `/data`, y no necesita decisión del revisor.
2. **La línea se calcula igual en los dos motores y la fórmula es aritmética entera pura:** segundo rival más
   adelantado (excluyendo al más adelantado, que suele ser el portero), máximo con la columna del balón, y
   nunca por detrás del centro del campo. En nuestra rejilla de 16 columnas es una función de una línea sobre
   una lista ya ordenada. Coste: una función pura en `/Sim`, sin datos nuevos. Merece la pena calcularla
   aunque no se implemente la regla, porque es la entrada de los puntos 1 y 3.
3. **El techo de la línea defensiva es un mecanismo distinto del desplazamiento del bloque, y es el que nos
   falta.** gfootball recalcula cada tick un tope (`offsideTrapX`, `teamAIcontroller.cpp:108-152`) que es el
   mínimo entre balón, balón a 700 ms, portador rival + 4 m y **su propio segundo hombre más retrasado + 4 m**,
   y lo aplica al límite trasero del bloque y al destino individual de defensas y centrocampistas
   (`ApplyOffsideTrap`, compresión suave, no recorte duro). Nuestro `blockShift` de `+4.0` casillas en posesión
   sobre 16 columnas no tiene tope de ninguna clase. Esto **no** se arregla bajando el número en
   `data/ai/weights.json`: bajar el desplazamiento empeoraría el ataque sin resolver el descuelgue. Hace falta
   un término nuevo en `MatchEngine.UpdateBlockShift` que acote el desplazamiento por la columna del defensa
   propio más retrasado y por la del balón. Es cambio de `/Sim` con impacto directo en goles: ADR y lote de
   balance.
4. **Para el balón muerto hay dos recetas probadas y las dos son baratas; la nuestra no es ninguna de las
   dos.** (a) gfootball recoloca a todo el mundo de golpe al abrir la reanudación, reutilizando la misma
   maquinaria de casillas-hogar con límites propios de cada tipo de saque, y solo entonces los deja quietos.
   (b) SimpleSoccer mantiene el posicionamiento corriendo durante la pausa y no reanuda hasta que todos están
   en su sitio. Para nuestra ventana de 15 ticks (45 en penalti) la receta (a) encaja mejor y es la más barata:
   que `BeginRestart` recalcule las casillas-hogar con límites de reanudación y coloque a los jugadores en un
   solo paso, sin necesidad de descongelar `Decide()` durante los 15 ticks. Es código en
   `Sim/Engine/MatchEngine.cs` (`BeginRestart`/`Step`, en torno a las líneas 394-420 y 2078), no cambia reglas
   ni datos, pero cambia las posiciones de todas las reanudaciones: hay que medirlo.
5. **Contra el exceso de persecución, el arreglo es estructural, no una penalización mayor.** Los tres motores
   convierten "no eres el perseguidor" en una **precondición**: uno designado por equipo con histéresis del
   20 % (gfootball), el más rápido en ciclos (HELIOS-base), el más cercano del equipo (SimpleSoccer); y como
   mucho un segundo cazador, más marcaje exclusivo con el marcador sacado del grupo. El primer paso más
   barato dentro de nuestra arquitectura de utilidad es convertir `chaseBallNotNearestPenalty` en compuerta:
   `ChaseBall` indisponible si no estás entre los N más cercanos —N pequeño, con histéresis para que no oscile
   tick a tick—, dejando que los pesos ya existentes de `MarkOpponent`/`CoverSpace` recojan al resto. Es
   código en `Sim/Engine/Utility.cs` más un parámetro nuevo en `data/ai/weights.json`; sin cambio de reglas.
6. **Lo único que necesita decisión del revisor es el fuera de juego como regla pitada** (falta, saque, evento
   nuevo, reanudación nueva). No está en `docs/requisitos.md` ni en `docs/simulacion.md`, y no existe en el
   código. Los tres motores separan limpiamente las dos mitades —el silbato (árbitro) y el recorte posicional
   (IA)— y para las tres observaciones del revisor solo hace falta la segunda. Recomendación: implementar los
   puntos 1, 2, 3 y 5, y **no** implementar el silbato sin decisión explícita; si algún día se implementa,
   entra en el marco de RF-012d como cualquier otro sistema (el jugador tiene que poder anticiparlo), lo que
   probablemente exija señalizar la línea en el render antes que la falta.

## Fuentes

- Google Research Football — https://github.com/google-research/football (motor C++ en
  `third_party/gfootball_engine/src/`, Apache 2.0; excluida la parte de RL en `gfootball/env/`). Ficheros
  citados en §6, todos bajo `third_party/gfootball_engine/src/`: `main.hpp`, `onthepitch/referee.cpp`,
  `onthepitch/teamAIcontroller.cpp`, `onthepitch/team.cpp`, `onthepitch/AIsupport/AIfunctions.cpp`,
  `onthepitch/player/player.cpp`, `onthepitch/player/humanoid/humanoidbase.cpp`,
  `onthepitch/player/controller/elizacontroller.cpp` y
  `onthepitch/player/controller/strategies/offtheball/{default_def,default_mid,default_off}.cpp`
- HELIOS-base / librcsc (RoboCup 2D Soccer Simulation League) — https://github.com/helios-base/librcsc
  (§6.1 cita `rcsc/player/world_model.cpp`, `updateOffsideLine` y `updateTheirDefenseLine`:
  https://github.com/helios-base/librcsc/blob/master/rcsc/player/world_model.cpp)
- helios-base, el agente que usa librcsc — https://github.com/helios-base/helios-base (§6.1 y §6.3 citan
  `src/player/strategy.cpp` —`Strategy::updateFormation`, `Strategy::get_normal_dash_power`— y
  `src/player/bhv_basic_move.cpp` —`Bhv_BasicMove::execute`—). El árbitro que pita el fuera de juego no está
  aquí sino en el servidor `rcssserver`, que no se ha consultado: el agente solo modela la línea.
- SimpleSoccer, Mat Buckland, *Programming Game AI by Example* —
  https://github.com/wangchen/Programming-Game-AI-by-Example-src/tree/master/Buckland_Chapter4-SimpleSoccer
  (§2 y §3 citan `SoccerTeam.cpp` y `SupportSpotCalculator.cpp`; §6.2 y §6.3 añaden `FieldPlayerStates.cpp`
  —`ChaseBall`, `ReturnToHomeRegion`, `Wait`— y `TeamStates.cpp` —`PrepareForKickOff`—)
- "Predictive Aim Mathematics for AI Targeting" —
  https://www.gamedeveloper.com/programming/predictive-aim-mathematics-for-ai-targeting (citada solo para
  descartarla como innecesaria, ver §5)
- "Gameplay Football" (Bastiaan Konings Schuiling) —
  https://github.com/BazkieBumpercar/GameplayFootball (repositorio original, no legible/compilable; ver
  nota de §5 sobre por qué las citas de este documento bajo ese nombre vienen del fork de Google). Para §6 se
  compararon además los dos árboles a mano: `AI_GetOffsideLine`, el cálculo de `offsideTrapX` y
  `ApplyOffsideTrap` son idénticos byte a byte y en las mismas líneas en ambos, luego son del motor original.

No se encontró ningún GDC talk indexado específicamente sobre utility AI en deportes, ni ningún proyecto
Unity/C# o Rust comunitario con mapas de ocupación por casilla más allá de lo ya citado; se deja anotado en
vez de rellenar el hueco con una fuente débil.
