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
