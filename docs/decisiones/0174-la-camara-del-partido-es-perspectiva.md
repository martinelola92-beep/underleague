# ADR 0174 — La cámara del partido es perspectiva

Fecha: 29 sep 2026 · Estado: **aceptada** (**decisión del revisor**, noche del 29 sep: «cámara en perspectiva para el
partido 3D, en lugar de la ortográfica de la ADR 0102»; cierra BA-F, con la lectura en partida real sin validar).
**Enmienda la ADR 0102** (la proyección), **precisa la ADR 0114** (los estados de cámara, que la ADR 0120 ya había
reducido a táctica fija más gestos) y **la enmienda del 19 sep de la ADR 0120** (que ya había puesto perspectiva en la
retransmisión, con FOV 30°, y no llegó a ninguna otra parte). **Requisitos:** RA-001, RA-002, RA-005, RA-008, RA-027,
RT-014, UI-002, UI-005. **No toca `/Sim`**: la cámara consume la traza y no decide nada del partido (RT-014).

## Lo que había (Regla G)

`tools/existe-ya.sh perspectiva camara` (que enseñó la enmienda de la ADR 0120 y los comentarios de `DecisionTray` y
`BroadcastScreen`) y la lectura de `MatchPitchView3D`, `BroadcastScreen` y `MatchScreen`:

- La perspectiva **ya existía en la retransmisión** desde el 19 sep (ADR 0120, enmienda): elevación 45°, FOV 30°,
  elegida entre cinco variantes A-E. Pero **la ADR 0102 seguía diciendo «ortográfica»**, igual que
  `docs/ui-partido.md` §1, `docs/estilo-visual.md` y BA-F, que se quedó «abierta: decidir si se acepta perspectiva»
  cuando ya estaba aceptada en la práctica. Cuatro documentos contradecían al código.
- **El modo depuración (F3, `MatchScreen`) seguía en ortográfica** con un encuadre (`OrthoSize` 5,3) escrito para un
  campo de 16×5 en un rectángulo de 1120×350. Con el campo de 16×7 y el rectángulo de 1120×500 **enseñaba 11,9 de
  las 16 columnas y unas 6,1 de las 7 filas** (5,3 / sen 60° por la relación de aspecto): ni las porterías ni los
  porteros entraban en pantalla y lo que se veía era una rejilla oscura. Es literalmente el síntoma de BA-F
  —«no veo a los porteros en pantalla, sigo viendo el grid como si fuera en 2D»— y **CONFIRMED** por la captura
  (`Game/screenshots/camara/antes-depuracion.jpg`, que es el `partido-3d.png` de antes) y por la cuenta.
- La perspectiva de la retransmisión (45°/30°) era **real pero leve**: el borde lejano del césped medía el 80 % del
  cercano (955 px contra 1190 a 1280×800, medidos con `DebugPitchCorners`).
- **Un defecto que la perspectiva destapó y nadie había visto**: el alcance del mapa de sombras direccional era 18
  unidades **contadas desde la cámara** (`DirectionalShadowMaxDistance`, pensado para los 11 de la ortográfica) y la
  cámara de la retransmisión estaba a 22,5: el césped entero caía entre 20,1 (borde cercano) y 25,0 (lejano) de profundidad y ningún jugador
  proyectaba sombra (RA-008 la exige y `ui-partido.md` §3 la llama «estructural»: sin ella el jugador no toca el
  suelo). **CONFIRMED con un experimento que lo aísla**: `solo-sombras-gol.jpg` es el mismo fotograma que
  `antes-gol.jpg` con la cámara de antes (45°/30°) y el alcance corregido de entonces (distancia + 9, antes de la fórmula
  actual) —y la grada más ancha, que con la luz de la escena no puede sombrear el campo—: el árbitro y las porterías
  proyectan sombra.

## Decisión

1. **Perspectiva en toda vista 3D del partido**, la retransmisión y el modo depuración por igual
   (`MatchPitchView3D.Perspective` pasa a `true` por defecto). La ortográfica de la ADR 0102 queda como opción del
   componente (`Perspective = false`), sin más uso hoy que las variantes A y B del arnés de capturas; **ya no es una vista
   del jugador** y no se ha vuelto a ejecutar con esta ADR.
2. **Elevación 45° —la que ya tenía— y FOV vertical 45°** en vez de 30° (`MatchPitchView3D.DefaultElevation`,
   `DefaultFov`), con el campo encajado por bisección a los bordes del césped (`PitchFit`). La cámara queda en
   (8; 11,9; 13,4), a **15,5 unidades del punto de mira** —que está 1,0 sobre el césped y 1,0 casillas hacia el fondo
   del centro del campo, la inclinación—; el borde lejano del césped mide el **72 %** del cercano (antes el 80 %).
3. **El FOV se define por su horizontal.** `Fov` sigue siendo un FOV vertical, pero **para un rectángulo 16:10** (67°
   horizontales); en cualquier otro se recalcula para conservar el horizontal. Es lo que hace que la misma cámara valga
   en el rectángulo de 2,24:1 del modo depuración (sin él, 45° verticales serían 86° horizontales) y en 16:9. Una esfera
   en el borde del encuadre se ve estirada 1/cos(FOV horizontal / 2): 1,09 con los 46° de antes, **1,20 con 67°**, 1,44
   con 92°. No hay umbral medido en partida; se eligió no pasar del 20 % para las cápsulas de los extremos
   (**provisional, sin medir**).
4. **`PitchFit`: dónde encaja el campo.** El rectángulo objetivo de la bisección deja de ser cuatro constantes en
   píxeles del prototipo y pasa a ser un valor de la vista, en fracciones del viewport: `Broadcast` (ancho 45-1235 de
   1280, borde cercano anclado en 690 de 800, con las tiras desde 735) y `Framed` (el modo depuración: 2 % de margen y
   campo centrado en el hueco).
5. **Sombras**: el alcance del mapa direccional es `(distancia encajada + 4,3) / 0,8` (`ShadowDepthBeyondCenter`,
   `ShadowFadeStart`): lo más lejano que recibe sombra, el borde inferior de las banderolas de la grada, está a 4,30 del
   punto de mira sobre el eje de la cámara a 45° (el techo de la grada, a 3,96; el borde lejano del césped, a 2,47), y el
   fundido de Godot empieza al 80 % del alcance, así que 24,7 con esta cámara. Se calcula con la distancia encajada, no
   con la del gesto, para que la resolución de la sombra no cambie durante un acercamiento.
6. **Entorno**: la grada se ensancha de 19,5 a 30 y la explanada de 24×13 a 56×26. Con el FOV más abierto los
   extremos de la grada y el borde de la explanada entraban en pantalla como un muro cortado y una diagonal.

## Cómo se eligió: capturas comparadas sobre los mismos fotogramas

`-- camara <elevación>/<FOV> ...` en `CapturasRetransmision.tscn` (`BroadcastCapture.CaptureCameraSweep`) fotografía
con cada variante **los mismos fotogramas** —plano general, gol, lesión, turba, cartel de perk, sangre y árbitro—, así
que lo único que cambia entre dos imágenes es la cámara. Las hojas `Game/screenshots/camara/candidatas-gol.jpg` (seis
candidatas, con la grada de antes) y `candidatas-gol-2.jpg` (tres, con la grada ancha) las ponen juntas. La tabla la mide
`tools/camara-encaje.py`, que reproduce `SolvePerspectiveFit` y se validó contra las esquinas que devuelve la cámara de
Godot (`DebugPitchCorners`): distancias 22,55 / 13,54 / 15,23 / 14,01 / 15,67 / 15,85 de Godot contra 22,5 / 13,5 /
15,2 / 14,0 / 15,7 / 15,9 del modelo, en 45/30, 55/50, 50/45, 45/50, 40/45 y 35/45. Las cifras de la cámara elegida
(encuadre, tamaño de ficha) las imprime además el propio arnés con la cámara real (`medidas.txt`).

| elevación | FOV (v / h) | lejano / cercano | alto del campo | casilla lejana / cercana (alto) | ficha lejana / cercana | alto / ancho del orco (lejana / cercana) | elfo − enano en alto (lejana / cercana) |
|---|---|---|---|---|---|---|---|
| **ortográfica 60° (ADR 0102)** | — | 1,00 | — | igual en todas | 1,00 | **0,56 / 0,56** | **13,1 / 13,1 px** |
| 45° | 30° / 46° (**la de antes**) | 0,80 | 353 px | 42 / 61 px | 0,83 | 0,84 / 0,68 | 16,4 / 16,1 px |
| 35° | 45° / 67° | 0,69 | 296 px | 31 / 57 px | 0,73 | 0,94 / 0,78 | 16,1 / 18,3 px |
| 40° | 45° / 67° | 0,71 | 322 px | 34 / 61 px | 0,74 | 0,90 / 0,70 | 15,7 / 16,6 px |
| **45°** | **45° / 67°** (**elegida**) | **0,72** | **347 px** | **37 / 65 px** | **0,76** | **0,86 / 0,61** | **15,4 / 14,7 px** |
| 45° | 50° / 73° | 0,70 | 345 px | 36 / 66 px | 0,74 | 0,86 / 0,59 | 15,0 / 14,1 px |
| 50° | 45° / 67° | 0,74 | 371 px | 41 / 68 px | 0,78 | 0,81 / 0,52 | 14,9 / 12,6 px |
| 55° | 50° / 73° (**primera elección, descartada**) | 0,74 | 391 px | 43 / 72 px | 0,77 | 0,77 / **0,40** | 14,3 / **9,7 px** |
| 60° | 55° / 80° | 0,75 | 410 px | 45 / 75 px | 0,78 | 0,73 / 0,26 | 13,7 / 6,5 px |

*Procedencia (Regla H).* «Lejano / cercano»: ancho del borde lejano del césped entre el del cercano. «Alto del
campo»: del borde lejano al cercano, a 1280×800 con el encaje de la retransmisión (borde cercano anclado en 690,
ancho 45-1235). «Casilla»: alto en píxeles de una casilla en la fila más lejana y en la más cercana. «Ficha»: razón del
ancho de una ficha entre esas dos filas. «Alto / ancho del orco»: alto proyectado de un cuerpo de 0,855 entre su
diámetro de 0,76, **la línea base existente**: la ortográfica de 60° comprime lo vertical por cos 60° = 0,5
(`ui-partido.md` §2) y a 74 px por casilla (`docs/ui/README.md` §7: `Size` 10,75 a 16:10, 1280 / 17,2 = 74,4) da 0,56 y **13,1 px** entre un elfo (1,091 de alto) y un enano (0,738), que
comparten `bodyRadius` 30 y en cápsula solo se distinguen por la altura. **Los criterios** de elección son cuatro y solo el primero sale de un dato existente: (1) *no empeorar respecto a la
ortográfica de la ADR 0102 en ninguna fila* la altura de la ficha (alto/ancho ≥ 0,56 y elfo − enano ≥ 13 px), porque
RA-002 pide reconocer la raza por la silueta; (2) *perspectiva perceptible*: borde lejano ≤ 0,75 del cercano; (3) *filas
legibles*: la casilla lejana no baja de ~36 px; (4) *esquinas sin deformar*: estiramiento de una esfera en el borde ≤ 1,20
(decisión 3). **Los umbrales (2), (3) y (4) son míos, provisionales y sin medir en partida**, y hay que decir de dónde
salen los dos que deciden: el (2) no lo dijo el revisor —BA-F pide «perspectiva perceptible» sin cifra y se abrió el 16 sep,
antes de que existiera la cámara de 45°/30°; la decisión del 29 sep dice «cámara en perspectiva, en lugar de la ortográfica»
sin decir si la de 0,80 basta—; se fijó mirando `candidatas-gol.jpg`, donde 0,72-0,74 se percibe más que 0,80. El (3) sale de
lo que se ve en esa misma hoja: a 31-34 px de casilla las fichas de filas contiguas se solapan y el partido se lee como un
racimo. El (4) se fijó en el estiramiento de la propia elegida (1/cos 33,5° = 1,1998): es un umbral a medida que solo
desempata contra 45°/50° (1,24), que por lo demás pasa de (1) a (3). Si el revisor considera que la cámara de antes ya se percibía como 3D, **45°/30° es la mejor fila** por los
criterios (1) y (3) (0,84 / 0,68; 16,4 / 16,1 px; casilla lejana de 42; 353 px de campo) y revertir es cambiar
`DefaultFov`.

**La primera elección fue 55°/50° y se descartó.** Daba el campo más alto (391 px) y la misma convergencia que 50°/45°, y
así se escribió el primer borrador de esta ADR. La revisión independiente la desmontó con una cuenta que faltaba: al
subir la elevación la fila **cercana** se ve casi desde arriba, y una ficha de esa fila queda aplastada (alto/ancho 0,40
contra los 0,56 de la ortográfica; elfo − enano 9,7 px contra 13,1). `descartada-55-50-modelos.jpg` frente a
`despues-modelos.jpg` lo enseña con los humanos de la maqueta. Incumple el criterio (1) y también lo incumplen 50°/45°
(0,52 y 12,6 px) y 60°/55°. **Cumplen (1): 45°/30°, 35°/45°, 40°/45°, 45°/45° y 45°/50°.** El (2) descarta 45°/30° (0,80),
el (3) descarta 35°/45° (31 px) y 40°/45° (34 px; campo de 322 px) y el (4) descarta 45°/50° (73° horizontales,
estiramiento de 1,24, a cambio de un 0,70 que no mejora la lectura). Queda 45°/45°, en `candidatas-gol-3.jpg` junto a las
demás con la grada ancha y el mismo fotograma.

**La inclinación no se barrió porque no es un grado de libertad**: la cámara mira siempre al centro del campo
desplazado por el eje «arriba en pantalla», y ese desplazamiento sale de anclar el borde cercano del césped en 690 de
800, que es donde lo pone el resto de la retransmisión (las tiras empiezan en 735 y la bandeja de decisión mide contra
el ancla, `DecisionTray`). Moverla es comerse las tiras o dejar hueco.

## Lo que la ADR 0102 temía, y cómo se contesta

La ADR 0102 descartó la perspectiva porque «la misma casilla mide distinto según dónde esté, y este juego se decide
en distancias de casilla que el jugador tiene que poder estimar a ojo (alcance de entrada, radio de intercepción,
correa)». La objeción es real y **no se desmiente: se acota**.

- **Lo local, en primer orden, no cambia.** Las distancias del juego son locales —una entrada alcanza a ~1 casilla, la
  intercepción a radio de ficha, la correa a un compañero—, y la huella de la ficha (su anillo), la franja de siega
  (una por columna) y las líneas de casilla están todas en el suelo, así que se escorzan igual en cada fila. Dos fichas a
  una casilla se ven a un anillo de distancia lo mismo arriba que abajo. Es un argumento geométrico, **no probado con un
  jugador**; y el anillo queda parcialmente bajo el cuerpo (ver «Consecuencias»).
- **Lo que se pierde** es comparar a ojo una distancia entre dos zonas distintas del campo por su tamaño en pantalla
  (una casilla mide 54 px de ancho arriba y 74 abajo). La ortográfica lo daba y la perspectiva no; el revisor lo cambia
  por la profundidad (BA-F: «perspectiva perceptible»).
- **RF-012d no se debilita, pero tampoco se ha medido**: el riesgo sigue explicándose antes del partido, no
  dependiendo de estimar píxeles durante él. Lo que sí podría cambiar es la lectura del «reducir el riesgo con la
  alineación» durante el partido; sin partida de un jugador, **LIKELY**.
- **Lo que se gana** es lo que pedía BA-F: el campo se lee como espacio 3D y los porteros se ven (con una excepción, más
  abajo), con las porterías con volumen y sus sombras.

## Efectos sobre lo que se proyecta a pantalla

Todo lo que se dibuja sobre el campo desde coordenadas de mundo (`MarkScreenPosition`, `DrawMarks`,
`DrawRefereeGesture`) pasa por `Camera3D.UnprojectPosition`, así que ninguno de los tres asume una proyección y **siguen
valiendo con cualquiera** y con la cámara moviéndose. Se comprobó con capturas —no se supuso— antes y después
(`Game/screenshots/camara/`, `antes-*.jpg` → `despues-*.jpg`, mismos fotogramas):

| Situación | Qué se comprobó |
|---|---|
| **Cartel de perk** (ADR 0112) | `antes-perk.jpg` → `despues-perk.jpg`: el pergamino «Toque» sigue sobre la cabeza de su ficha. Va anclado 0,35 por encima del cuerpo y tiene tamaño fijo en pantalla, así que en la fila lejana (ficha al 0,76 de la cercana) pesa proporcionalmente algo más; se lee igual |
| **Árbitro** (ADR 0158 §6) | `antes-arbitro-falta.jpg` → `despues-arbitro-falta.jpg` y `…-no-senalada.jpg`: «¡Falta!» y «¿?» sobre su cabeza. Hallazgo anterior a esta ADR y sin tocar: cuando el balón va por la banda lejana el árbitro se coloca sobre la línea y la valla de patrocinio lo tapa a medias |
| **Sangre** (RA-027) | `antes-sangre.jpg` → `despues-sangre.jpg`: calcomanías planas en el suelo, que se acortan en las filas lejanas igual que las líneas del campo |
| **Turba** | `antes-turba.jpg` → `despues-turba.jpg`: el árbitro se ha ido, el pregón es una banda de UI a todo lo ancho y el campo con los catorce se lee. Nada de lo que se dibuja desde coordenadas de mundo cambia |
| **Acercamiento de gesto** (`ApplyPunch`) | `antes-tiro.jpg` → `despues-tiro.jpg`: el acercamiento ×1,15 mueve la cámara por el rayo ojo-objetivo y el píxel del objetivo **no se mueve**: el arnés mide «desplazamiento 0 px en pantalla real» con la cámara de antes y con la de ahora (`medidas.txt`) |
| **Plano general y porterías** | `antes-base.jpg` → `despues-base.jpg`. Medido por el arnés a 1280×800 (`ReportFraming`, en píxeles físicos; `medidas.txt`): el borde cercano del césped va de x 45 a 1235 y el lejano de 209 a 1071, entre y 343 y 690 (sobre las tiras), y **las dos porterías, con postes, larguero y fondo de red, de x 55 a 1225 de 1280**; el arnés avisa si algo se sale y no avisó en ninguno de los barridos (sin líneas `WARNING: cam-` en los registros). Con la cámara de antes: lejano 163-1117, porterías 38-1242 |

## Las diez preguntas, abreviadas (`game-design-review`)

No es una mecánica, pero **retira una regla de legibilidad de la ADR 0102** y por eso se contesta. (1) El jugador ve un
campo con profundidad y a los dos porteros. (2-3) No decide nada nuevo; decidía mal si no leía bien las filas lejanas.
(4) Representa RA-001 (tres cuartos), RA-002 (silueta) y RA-008 (sombra); la regla «distancias estimables a ojo» de la
ADR 0102 pasa de global a local. (5) Solo `/Game`. (6) Alternativas: la ortográfica que había, una cámara baja de
retransmisión y una elevación mayor (más campo). (7) Coste: la casilla ya no mide lo mismo en todo el campo y la ficha
lejana pierde un 24 %. (8-9) Sin efecto en las estrategias; puede degenerar si las fichas lejanas dejan de leerse, y
esa es la razón del criterio (3) y del riesgo abierto. (10) Se demuestra con capturas de los mismos fotogramas y la
tabla anterior, no con un test (RT-084).

## Consecuencias

- **Los niveles de detalle de la ADR 0114** no cambian: la táctica fija es el estado del partido y los gestos (ADR 0120)
  son un dolly, no un zoom: **con perspectiva el acercamiento es un acercamiento de verdad**, con paralaje (lo de
  delante crece más que lo de detrás). Era lo que ya ocurría en la retransmisión desde el 19 sep.
- **El briefing de arte** (`docs/ui-partido.md` §1 y §2) da por buena una casilla igual en todo el campo. Con
  perspectiva **la ficha lejana mide 0,76 de la cercana en ancho**, y en alto la cuenta es la contraria: **la fila más
  cercana es la peor para la altura** y la lejana la peor para el ancho. Un orco (radio 0,38) pasa de 55,0 a 41,7 px de ancho
  entre la fila cercana y la lejana (con la cámara de antes: 55,5 y 46,0), y su alto/ancho de 0,61 a 0,86. El criterio de silueta debe cumplirse contra la fila
  lejana para el ancho y contra la cercana para el alto (elfo frente a enano: 14,7 px).
- **`OrthoSize` y `PanUp`** dejan de usarse en la vista del jugador; se conservan solo para `Perspective = false`, que
  no se ha vuelto a ejecutar con esta ADR (el código de esa rama no cambia, pero su elevación por defecto ya no es la
  60° de la ADR 0102: hay que fijarla junto con `OrthoSize`). Las capturas de silueta de `CaptureRunner` corren ahora en
  perspectiva a propósito: es la cámara que ve el jugador.
- **El dorsal sigue tapado por el cuerpo** en las cápsulas de las razas sin modelo, con cualquier cámara (el anillo y el
  número van en el suelo, bajo el cuerpo; la ADR 0102 ya lo preveía: «con un modelo real la prueba de profundidad
  vuelve; si aun así queda tapado, levantarlo»). Hermano de BA-F, **no cerrado aquí**: se anota en su ficha.
- **La vista de depuración** (`Framed`, 1120×500) usa la misma cámara en un rectángulo menor: el césped queda en y
  267-545 y la casilla lejana mide 30 px, por debajo del criterio (3). No es una vista del jugador y se deja así.
- **16:9**: medido con el mismo arnés en una ventana de 1920×1080 (visible 1422×800 lógicos, `medidas.txt`): el borde
  lejano mide 0,725 del cercano (958 / 1322 px) contra 0,724 a 16:10 (862 / 1190), que es lo que persigue definir el FOV
  por su horizontal, y el césped (50-1372) y las dos porterías (61-1361 de 1422) caben. El hueco de las tiras a 16:9 sigue
  siendo un tema de `BroadcastScreen`, no de esta ADR.

## Alternativas descartadas

- **Dejar la ortográfica de la ADR 0102 en el modo depuración.** Es lo que causó el síntoma de BA-F; y dos cámaras
  distintas para «la misma» pantalla hacen que lo que se mide en una no valga en la otra.
- **Cámara baja de retransmisión (35°-40°).** Es la más «fútbol», pero apelotona las filas (tabla y `candidatas-gol.jpg`).
- **Elevación de 50° a 60° con el FOV abierto** (más campo, la misma convergencia): aplasta la altura de la fila
  cercana por debajo de la ortográfica (tabla). Fue la primera elección.
- **FOV de 50° o más a 45°.** Converge un poco más (0,70) pero el horizontal pasa de 70° y las esquinas se estiran más de
  un 24 %, sin mejorar la lectura.
- **Corregir el tamaño de la ficha lejana** (agrandarla para igualarla a la cercana). `bodyRadius` no es visual: lo que
  se ve **es** el volumen que simula el motor (ADR 0102, «lo que se ve no puede mentir sobre quién bloquea a quién»).

## Riesgo abierto

- **El cambio de FOV (30° → 45°) descansa en un juicio, no en una petición del revisor**: si considera que la cámara que ya
  había (45°/30°, 0,80) se percibía como 3D, se vuelve a ella cambiando `DefaultFov`; las demás piezas de esta ADR
  (perspectiva en depuración, encaje por vista, sombras, entorno) no dependen de ese valor. Decisión pendiente del revisor
  sobre las capturas `antes-*` y `despues-*`.
- **La escena de N3/N4 tapa la portería izquierda** —y con ella al portero propio— en el saque inicial y en cada gol,
  lesión grave o turba: el pergamino ocupa el 26 % del ancho a la izquierda hasta y≈525 de 800 y la boca de la portería
  cae en y 405-540 (`despues-base.jpg`, `despues-gol.jpg`), con esta cámara y con la de antes (405-546). **La cámara no
  puede arreglarlo**: es la decisión C5 de `docs/ui/README.md` §9 (posición de la N3) y sigue abierta. Hasta que se
  cierre, «los porteros se ven» es cierto en el plano general y falso durante esos acontecimientos.
- La ficha de la fila lejana mide ≈ 31 px de ancho en el no-muerto y ≈ 33 en el enano y el elfo, por debajo de los ~37
  px que la ADR 0114 daba de referencia (la ortográfica los daba en todo el campo). Las capturas se leen —equipos y
  estados se distinguen en la fila lejana—, pero no se ha medido una partida de un jugador con esto: es **LIKELY** que se
  lea bien, no **CONFIRMED**. Tampoco se ha comprobado que una perspectiva del 0,72 baste para que el revisor deje de
  ver «el grid como si fuera en 2D» (**LIKELY**, ver BA-F H3).
- El **Forward+** de la build de Windows no es el renderizador de las capturas (compatibilidad por Xvfb): el alcance y
  el sesgo de las sombras se han comprobado en este último.
