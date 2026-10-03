# BX — Partida del revisor del 3 oct 2026 (semilla 20260905, humanos)

Notas del revisor tras jugar la build `0463e22`, ordenadas por paquete. Cada punto lleva un id `BX-n`. Cuando un
punto tenga diagnóstico propio, abre su ficha y enlázala aquí.

Capturas: `capturas/playtest-3oct-saque-portero.png` (saque de puerta al centro, hacia el rival) y
`capturas/playtest-3oct-orcos-muertes.png` (informe: 2 muertes y 2 lesiones graves contra orcos).

## Paquete 1 — Equipo y plantilla (`/Game` + run)
- **BX-1** En un momento de la run, la pantalla de Equipo no dejaba alinear y luego el partido salía con 7 jugadores.
- **BX-2** Al meter un suplente, la pizarra lo muestra bien, pero la columna izquierda no lo pasa de «Suplentes» a «Titulares».
- **BX-3** Sustituir debería hacerse arrastrando y soltando (al menos en PC).
- **BX-4** El dorsal de cada jugador debería ser fijo toda la run.
- **BX-5** Al ganar o comprar un jugador con la plantilla llena, ofrecer cambiarlo por uno propio.
- **BX-6** Tras la tirada del destino, el pergamino no se oculta hasta que hay un gol.

## Paquete 2 — Conducta en el campo (`/Sim`)
- **BX-7** En una turba, el delantero se queda «bloqueado» sin poder volver a su campo porque el saque tarda mucho.
- **BX-8** El portero saca de puerta sin criterio: lanza el balón al centro, hacia los rivales, en vez de buscar a un compañero abierto. Captura.
- **BX-9** Al portero a veces le cuesta coger el balón con las manos: se queda unos ticks con él en los pies.
- **BX-10** A menudo el balón pasa al lado de un jugador solo y no lo coge (probabilidad de captura o intercepción).
- **BX-11** ¿Puede lesionarse gravemente un jugador en el minuto 0?
- **BX-12** El árbitro se teletransporta demasiado; no debería molestar tanto.
- **BX-13** Sigue habiendo stutter, sobre todo cuando los jugadores reculan tras un ataque y pasan a defender.

## Paquete 3 — Sensación del partido: timing, lectura, sonido (`/Game`, diseño)
- **BX-14** La barra de tiempo se llena y el partido no acaba. Mejor un reloj de 90 minutos como en el fútbol, y luego la prórroga sin reloj o algo así.
- **BX-15** Las faltas deberían pitarse DESPUÉS de la entrada. Hoy se pita, se para, se ven jugadores cayendo, pero no queda claro quién ha caído ni quién ha hecho la falta. Es lo que menos se percibe como jugador.
- **BX-16** La tirada del destino al lesionarse alguien tiene que ser algo gráfico, aunque pare el partido: por ejemplo, una ruleta verde y roja con una aguja que gira. Tiene que ser un evento curioso; hoy se siente atropellado.
- **BX-17** Timing y pausa (cita): «Una de las mayores virtudes del humor es el timing y la pausa. Pasan cosas demasiado rápido, no tengo tiempo a procesar, sale un cartel 1 segundo, escucho un grito, no sé quién es, no me divierto, no me entero, es frustrante. Busca pausas, zooms… No tiene por qué ser excesivo, pero hay que buscar algo.» Esto corrige la tendencia a evitar parones a toda costa.
- **BX-18** Gritos demasiado altos. Las lesiones graves y las muertes están bien; el resto debería bajar, y el ambiente subir un poco.

## Paquete 4 — Rendimiento de las decisiones
- **BX-19** Al cambiar la táctica (defensa, neutro, ataque) hay un parón, porque se vuelve a simular. Hay que solucionarlo.

## Paquete 5 — Muertes
- **BX-20** Contra unos orcos pegones, en un partido mataron a 2 jugadores y lesionaron gravemente a otros 2; en la run murieron 5. «Sed de médula igual es demasiado.» Esto va contra la condición «la muerte es rara» de la ADR 0048.

## Paquete 1 — estado tras la sesión del 3 oct (commits 413ee93..0209589)

Etiquetas de la Regla F. Capturas en `Game/screenshots/` (`equipo-once-*`, `equipo-arrastre-*`, `equipo-dorsal`, `cambio-*`).

- **BX-1 — CONFIRMED (causa) / arreglado.** Hipótesis ordenadas: (a) `SetLineup` exigía ≥ 5 titulares y `PruneLineup` deja la
  guardada con menos tras las bajas; (b) `PlacementView.WithPlayerAt` admitía un octavo titular que `SetLineup` rechazaba;
  (c) lisiados (ADR 0187) o muertos puestos a mano; (d) BG-A/BG-C (`Effective` frente a la guardada). Ganó (a): con la guardada
  podada a < 5, **cualquier** movimiento lanzaba `ArgumentException` en la pantalla de Equipo (se quedaba muda) mientras
  `RunLineup.Build` completaba hasta 7 al jugar. Dos reglas del mismo hecho. Arreglo: el suelo de 5 lo garantiza `Build`, no
  `SetLineup` (acepta 1..7); `WithPlayerAt` rechaza el octavo; Equipo dice por qué no deja (lisiado/muerto, once completo,
  casilla del portero) con un cartel. (c) se descarta como causa de este síntoma: `Effective` ya sustituye al lisiado puesto a
  mano (`CrippledTests`), solo faltaba avisarlo. (d) sigue abierta tal como la describen BG-A/BG-C. Tests: `LineupEditTests`;
  `RunEngineTests.LineupAndConsumables_AreValidated` cambia (antes exigía que 4 titulares lanzara; esa era la causa).
  *No reproducido con la partida del revisor* (no hay guardado): la reproducción es el estado sintético del test, no el suyo.
- **BX-2 — CONFIRMED / arreglado.** Los brochazos «Titulares»/«Suplentes» se fijaban al construir la columna; al entrar un
  suplente en una casilla libre cambiaba el número de titulares y el que subía seguía bajo «Suplentes». Se rehacen al cambiar
  el reparto. Capturas `equipo-once-corto` / `equipo-once-completo`.
- **BX-3 — hecho.** Arrastre nativo de Godot desde la columna o el campo hasta una casilla o un titular; el cursor sigue al
  ratón (misma previsualización de `/Sim`) y la fila destino se resalta. El clic y el mando siguen. Capturas `equipo-arrastre-*`.
  Solo del arnés de capturas: el ratón sintético exige `Input.WarpMouse` además del evento.
- **BX-4 — hecho, esquema de guardado 10.** `RunPlayer.ShirtNumber`: primero libre ≥ 1 al entrar; los muertos conservan el suyo
  (nadie lo hereda); la traza lo respeta si todo el equipo lo trae (los rivales generados siguen repartidos). Los guardados de la
  8 y la 9 se numeran por id al cargar. Visible en la fila de Equipo.
- **BX-5 — hecho (recompensa, mercado, mercenario); el evento `Recruit` queda fuera.** `ReplacePlayerId` opcional en
  `ChooseReward`, `BuyOffer` y `HireMercenary`, una sola decisión atómica. El soltado se **vende** si el veto de la ADR 0108 lo
  permite (mercado) y si no se **descarta** sin cobrar (siempre en una recompensa): la regla de siempre, sin economía nueva.
  - *game-design-review breve.* Fantasía: el cambio de cromo — ganas uno, pierdes uno. Decisión y coste de oportunidad: el
    cuerpo soltado es real y la plantilla no crece (la ADR 0046 sigue mandando). Previsibilidad (RF-012d): el panel dice, antes
    de confirmar, a quién y qué le pasa (se vende por N / se descarta sin cobrar); no se ofrece a los muertos. Degeneración: no
    hay grifo — el canterano recién fichado no se vende (ADR 0108), así que se descarta sin cobrar; vender cobra lo que ya
    cobraría vender a mano y comprar después. No toca la política de `/Balance` (ninguna decisión suya trae `ReplacePlayerId`),
    por eso **no se ha lanzado lote**; medir solo si una política futura lo usa.
  - Fuera: la opción `Recruit` de un evento con la plantilla llena sigue bloqueada (`EventSystem`); es el mismo patrón y cabría
    en `MakeRoomFor`, pero el evento elige a quién señala y requiere un diseño aparte.
- **BX-6 — LIKELY / arreglado, sin captura.** `AfterDecision` crea un director nuevo y deja `_lastVoiceMoment = null`;
  `ApplyPresentation` compara `result.Voice == _lastVoiceMoment` (null == null) y sale sin ocultar el pergamino, que aguantaba
  hasta que otra voz (el gol) lo pisaba. Es el único camino que lo explica y coincide con el síntoma (ocurre tras la decisión de
  sustitución que abre una lesión por tirada); `SeekTo` ya hacía la limpieza que faltaba y ahora comparten `HideVoiceLayers`.
  No reproducido en pantalla: falta una captura de una tirada que acabe en decisión.
