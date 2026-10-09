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
  **Ficha: [BX-8](BX-8.md). Diagnosticada (10 oct), arreglo en la rama `bx8-saque-de-puerta`, pendiente de decisión del
  revisor.** El portero despejaba el 89 % de los saques y el rival se quedaba el 92 %. Dos causas CONFIRMED:
  - el bono de peligro del despeje (+620) se cobraba con el balón muerto;
  - su «pase corto» no tenía alcance.

  Con el arreglo conserva el 38 %, pero el jefe final se endurece (puerta roja, dos semillas). Falta decidir el tope
  del pase largo del portero.
- **BX-9** Al portero a veces le cuesta coger el balón con las manos: se queda unos ticks con él en los pies.
- **BX-10** A menudo el balón pasa al lado de un jugador solo y no lo coge (probabilidad de captura o intercepción).
- **BX-11** ¿Puede lesionarse gravemente un jugador en el minuto 0?
  **Respondida (9 oct), sin cambio de código.**
  - Dentro del partido, una lesión grave sólo sale de una entrada o una falta. Los únicos perks con efecto `injure`
    son `ankle_bite` (`TACKLE`) y `dirty_play` (`FOUL`); `blood_tithe` (`MATCH_START`) sólo modifica probabilidades.
  - Sonda desechable sobre 1.000 partidos de referencia, por minuto del reloj del partido:
    - el minuto 0 tiene 18 entradas en total, frente a 164 de media en los minutos 5-89 (el saque acaba de empezar);
    - **0 lesiones graves en el minuto 0** (ni en el 1), frente a 1,6 de media por minuto;
    - 0 muertes en todo el lote: los equipos de referencia no llevan perks letales.
  - Así que es posible en principio, por una entrada justo tras el saque, pero el minuto 0 es el **menos** probable.
  - Fuera del partido, las cartas de evento del mapa (`locker_room_brawl`, `blood_pit`, `fighting_pit`…) sí lesionan
    antes de jugar, con aviso en la carta. Un jugador así entra al partido ya lesionado.
  - **LIKELY**, sin poder comprobarlo sin la partida: eso es lo que vio el revisor. Pregunta abierta: ¿qué pantalla lo
    mostraba (log, informe o estandarte) y venía de una carta de evento?
- **BX-12** El árbitro se teletransporta demasiado; no debería molestar tanto.
  **Cerrada (9 oct), sólo `/Game`.** El árbitro no existe en `/Sim`: `MatchPitchView3D.ApplyReferee` lo pone 3 filas
  hacia la banda más cercana al balón, con un suavizado exponencial. Hipótesis:
  - (H1) **CONFIRMED** — el objetivo cambia de banda cada vez que el balón cruza la fila central, un salto de seis filas.
  - (H2) **CONFIRMED** — el seguimiento no tiene tope de velocidad.
  - (H3) colocación sin suavizar al saltar en la repetición: no medida, no hizo falta.

  Medición: sonda desechable sobre 40 partidos de referencia, que repite el seguimiento a 60 fps.
  - Antes: **28,5 cambios de banda por partido**; el árbitro más rápido que el p99 de los jugadores (2,81 c/s) el
    **32 %** del tiempo, con p95 de 13 c/s y máximo de 52, porque también perseguía pases y tiros.
  - Arreglo: histéresis (cambia de banda si el balón entra 2 filas en la otra mitad), tope de 2,8 c/s (el p99 de los
    jugadores) y zona muerta de 1 casilla, todo con el reloj del partido. Antes iba con el real y se movía con la
    imagen congelada.
  - Después: 9,5 cambios por partido, nunca más rápido que un jugador, mediana de 1 c/s y a 2,6 casillas del balón
    (p95 4,2).
  - Variantes medidas y descartadas: margen 1,5 con tope 3 o 3,5 (12,6 cambios, corre a tope casi siempre); zona
    muerta 1,5 (se queda más lejos, p95 4,4).
- **BX-13** Sigue habiendo stutter, sobre todo cuando los jugadores reculan tras un ataque y pasan a defender.

## Paquete 3 — Sensación del partido: timing, lectura, sonido (`/Game`, diseño)
- **BX-14** La barra de tiempo se llena y el partido no acaba. Mejor un reloj de 90 minutos como en el fútbol, y luego la prórroga sin reloj o algo así.
  **Cerrada (9 oct).** Hipótesis: (H1) la barra avanza con el tick del motor y el reglamentario lo cierra el reloj
  del partido, que se para mientras el equipo vuelve a sacar de centro (BC-A); (H2) el empate va a la turba con gol
  de oro, sin tope visible. Sonda de 120 partidos de referencia (test desechable, `SimConfig.Trace`): reglamentario
  1.200 ticks, partido medio 1.658; **en 120/120 la barra se llena antes del final**, 458 fotogramas de media
  (~30 s a 1×); 41/120 van a la turba (561 fotogramas de media tras el reglamentario). **H1 y H2 CONFIRMED**, las
  dos a la vez. Hermano por la Regla G: el estandarte del gol y el bando de la muerte ponían el minuto con el tick
  del motor (hasta «minuto 123», el mismo fallo que `MatchLogView.Minute` ya advertía), el aviso de repetición
  bloqueada y la marca del final del reglamentario en la pantalla de depuración. Arreglo, sólo `/Game`: reloj
  «26'» en el tablero (`%Reloj`, pieza editable) y barra con el reloj del partido; en la turba el reloj dice «Gol
  de oro». Captura `pausa-1-congelada.png`.
- **BX-15** Las faltas deberían pitarse DESPUÉS de la entrada. Hoy se pita, se para, se ven jugadores cayendo, pero no queda claro quién ha caído ni quién ha hecho la falta. Es lo que menos se percibe como jugador.
  **Implementada por la [ADR 0192](../decisiones/0192-la-pausa-ensena-quien-y-la-ruleta-del-destino.md)** (9 oct), pendiente de que el revisor la juegue: causa CONFIRMED por lectura del director (la ADR 0173 congelaba el fotograma anterior al suceso, así que el silbato llegaba antes de la entrada). La pausa llega ahora hasta 0,4 s después, con carteles FALTA / AL SUELO sobre los implicados y acercamiento. Captura `pausa-1-congelada.png`.
- **BX-16** La tirada del destino al lesionarse alguien tiene que ser algo gráfico, aunque pare el partido: por ejemplo, una ruleta verde y roja con una aguja que gira. Tiene que ser un evento curioso; hoy se siente atropellado.
  **Implementada por la ADR 0192** (9 oct), pendiente de partida: ruleta verde y roja con el partido congelado; el sector rojo es la probabilidad real. Hallado de paso (CONFIRMED, semilla 20260905): una entrada puede tirar dos dados al mismo jugador en el mismo tick (dos momentos) y la banda enseñaba uno cualquiera; ahora el que acertó o, si ninguno, el de la muerte, y la segunda tirada no se encola. Capturas `destino-*-2-ruleta-gira.png`, `destino-*-3-ruleta-parada.png`.
- **BX-17** Timing y pausa (cita): «Una de las mayores virtudes del humor es el timing y la pausa. Pasan cosas demasiado rápido, no tengo tiempo a procesar, sale un cartel 1 segundo, escucho un grito, no sé quién es, no me divierto, no me entero, es frustrante. Busca pausas, zooms… No tiene por qué ser excesivo, pero hay que buscar algo.» Esto corrige la tendencia a evitar parones a toda costa.
  **Atendida por la ADR 0192** (9 oct): sellos 1,6/2,2 s, pausa 1,4 s, carteles con nombre y acercamiento. Cifras provisionales: se validan con la siguiente partida.
- **BX-18** Gritos demasiado altos. Las lesiones graves y las muertes están bien; el resto debería bajar, y el ambiente subir un poco.
  **Atendida por la ADR 0192** (9 oct): esfuerzos y regates −9 dB, caída −5, lesión leve −8, ambiente −12 → −7 dB. A oído, sin medir.

## Paquete 4 — Rendimiento de las decisiones
- **BX-19** Al cambiar la táctica (defensa, neutro, ataque) hay un parón, porque se vuelve a simular. Hay que solucionarlo.
  **Cerrada por la [ADR 0191](../decisiones/0191-la-decision-en-vivo-se-simula-en-segundo-plano.md).** Medido con
  `-- paron` (arnés de la retransmisión) y `ReplanCostProbeTests`: cada decisión en vivo simulaba el partido **dos veces**
  (reproducción + `EnterMatch`, más una por punto de sustitución) con el `/Sim` del `Debug` sin optimizar, 0,5-3,9 s de
  bloqueo — H1/H2 (re-simulación duplicada) y H5 (`Debug` ×5-10) **CONFIRMED**; guardado y pantalla **REJECTED** como
  causa (≤ 3 %). Ahora una simulación, `/Sim` optimizado y en segundo plano con catálogo propio: ≤ 3 ms en el clic y
  6-15 ms al aplicar en el hilo principal; partido igual byte a byte. Queda (LIKELY imperceptible, sin medir a 60 Hz):
  1-3 fotogramas con el reloj sostenido mientras se simula.

## Paquete 5 — Muertes
- **BX-20** Contra unos orcos pegones, en un partido mataron a 2 jugadores y lesionaron gravemente a otros 2; en la run murieron 5. «Sed de médula igual es demasiado.» Esto va contra la condición «la muerte es rara» de la ADR 0048. **Ficha: [BX-20](BX-20.md). Cerrada por la [ADR 0190](../decisiones/0190-un-carnicero-se-cobra-una-vida-por-partido.md)**: el mismo portador letal mataba en serie (1,75 por partido el de Partecráneos); ahora una vida por portador y partido, y `marrow_thirst` 900 → 1800 para no salir de banda.

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
  No reproducido en pantalla: falta una captura de una tirada que acabe en decisión. `PlayMomentSound` no suena dos veces por
  esto: el arreglo solo oculta capas (no añade ninguna llamada de sonido) y la voz de después de la decisión suena como antes.

### Revisión independiente (MERGE CON ARREGLOS) — respuestas

- **BX-1 cerrado de verdad.** `game-design-review`: (A) mostrar el relleno marcado y editable, o (B) colocarlo ya en la pizarra.
  Es la misma cosa si se pinta `RunLineup.Effective` (B) y se marca lo que entra de oficio (A): se hizo ambas, con **una sola
  función** (Equipo llama a `Effective`, la misma que `Build`). Legible: el jugador ve siete, sabe cuáles entraron por falta
  de titular («De oficio», `equipo-relleno`) y los mueve como a cualquiera; confirmar cualquier colocación guarda el once
  mostrado y la marca desaparece. Coste: ninguno de balance (el partido juega lo mismo que antes). Degeneración: ninguna; sigue
  existiendo «jugar con los que he puesto» (`PlayShort`), que `Effective` respeta. Test: `TheElevenEquipoShowsIsTheElevenThatPlays`.
- **BX-5, `game-design-review` completo.** Fantasía: el cambio de cromo. Decisión: ¿quién sobra? Coste de oportunidad: el soltado
  es un cuerpo con nivel, perks y vínculos; no hay deshacer (se dice en el panel). Interacciones: (1) *clínica/lesiones*:
  soltar a un grave o lisiado cobra su precio de estado (25 %) o nada si no ha jugado —no es una vía de blanqueo mejor que vender
  a mano—; (2) *ADR 0108*: el veto de «aún no ha jugado» manda, así que comprar-canterano-y-soltar no genera oro; (3) *RF-002b*:
  quien llega entra sano, los disponibles no bajan; (4) *vínculos/duelo*: soltar rompe vínculos igual que `Release`/venta
  (regla existente, no nueva); (5) *economía de la recompensa*: soltar a un jugador es pagar con un cuerpo lo que no se puede
  pagar con oro, y solo se descarta (no se cobra). Degeneración posible: rotar plantilla para cazar rarezas —acotado por el
  coste (pierdes al soltado) y por una recompensa por nodo—. Sin medición: no hay política de `/Balance` que lo use (Regla H:
  sin cifras nuevas).
- **Dorsales.** Criterio: el dorsal es la identidad visible del jugador en la run. Los de **muertos no se reutilizan** (siguen en
  la plantilla como memorial, RF-122); los de **vendidos o descartados sí** (el primero libre): el jugador ya no está, y una
  plantilla de 9-12 no puede quedar con huecos para siempre; la confusión es mínima porque quien se va deja de aparecer.
  Tests: `ADeadPlayersNumberIsRetired_AndASoldOnesIsFreeForTheNextSigning`.
- **ADR 0183.** `RunController.Apply` rechaza un cambio (`ReplacePlayerId`) con una reproducción en curso; con un partido abierto
  normal, `CloseStaleMatch` ya lo confirma antes de aplicar. Es una guarda de `/Game` (el partido a medias vive en el
  controlador); no hay test posible en `Sim.Tests`.
- **Tests añadidos:** soltar tocado/grave/lisiado (precio frente a descarte) `ReplacingAnInjuredOrCrippledPlayer…`; guardado de
  versión 8 migrado `ARealVersionEightSaveLoadsAndIsNumbered`.
