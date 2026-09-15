# Estilo visual

**Versión 2 · 15 de septiembre de 2026 · NO definitivo, se itera.**

Este documento fija el **tono** y sirve de base a los encargos de arte. Lo de abajo es el punto de
partida que dio el revisor, no una decisión cerrada: se espera cambiarlo.

Documentos de los que depende: **ADR 0102** (3D con toon y cámara ortográfica en tres cuartos),
**ADR 0103** (campo de seis filas), `docs/ui-partido.md` (briefing de la pantalla de Partido) y §5 de
`docs/requisitos.md` (RA-001..027), que este documento **contradice en un punto** y hay que resolverlo.

---

## 1. El tono: Lucky Tower

La referencia es **Lucky Tower**, con Castle Crashers y Pit People al lado. Lo que eso significa en
concreto, que es lo único que un encargo puede usar:

- **Línea negra gruesa y color plano.** Nada de textura realista, nada de degradados. La forma la hace el
  contorno, no la iluminación.
- **La muerte es un gag.** En Lucky Tower morir es lo divertido: la desgracia es aparatosa, exagerada y
  se repite. Eso encaja con la identidad del juego mejor que cualquier otra referencia posible, porque
  aquí **el desgaste de la plantilla es el recurso central** y un jugador se va a morir cada dos runs.
  Si la muerte se dibuja solemne, el juego se vuelve deprimente; si se dibuja como un gag, se vuelve lo
  que dice ser: **carnicería administrada**.
- **Reacción exagerada por encima de anatomía correcta.** Ojos como platos, miembros de goma, poses que
  se leen a tamaño de cromo.
- **Slapstick, no terror.** El gore es de tebeo: aparatoso, rojo plano, sin realismo.

Esto **resuelve una tensión** que arrastraban los requisitos. RA-025 pide «humor negro y gore» y RA-027
pide sangre persistente sobre el césped; sin una referencia de tono, eso podía salir tétrico. Con Lucky
Tower, la sangre es una mancha de tebeo y el obituario de RF-122 puede ser gracioso y triste a la vez,
que es exactamente lo que hace falta.

---

## 2. El punto de partida, literal

Se conservan tal cual los dieron, para poder comparar con lo que venga después.

### 2.1 Personajes y entorno

> Hand-drawn 2D concept art sheet for a dark fantasy soccer autobattler game, inspired by the art style of
> Lucky Tower, Castle Crashers, and Pit People. Clean 2D vector style, bold black lineart, vibrant flat
> colors with simple cel-shading, cartoon slapstick aesthetics.
>
> **Top section:** Full-body character showcases including a dark elf midfielder, a skeleton goalkeeper, a
> monstrous horned defender, and a goblin referee.
>
> **Bottom section:** Environment assets featuring a gothic obsidian soccer stadium, a flaming goalpost,
> and a spectral team banner.
>
> Clean presentation on a parchment background, highly detailed, readability focused, NPR cartoon
> aesthetic, high resolution. `--no realistic textures, 3d render, gradients`

### 2.2 Interfaz

> User Interface (UI) design concept art sheet for a dark fantasy soccer autobattler game. Flash/webtoon
> cartoon aesthetic, thick black outlines, flat colors, high legibility.
>
> **Panel 1 (Main HUD):** In-game match interface viewed from a fixed 2.5D isometric broadcast camera
> perspective showing a mini soccer pitch. Features comic-style player health bars, stamina meters, score
> banner at the top, and interactive perk action slots at the bottom.
>
> **Panel 2 (Shop & Bench):** Pre-match management screen showing an inventory shop grid with item cards
> for soccer gear (boots, helmets), player stat comparison panels, and bold medieval-style typography for
> text buttons.
>
> Clean graphic layout, dark fantasy comic UI icons, game asset reference layout, high resolution.
> `--no photo realism, smooth gradients, 3d depth`

---

## 2bis. Los prompts adaptados (v1)

**Decisión del revisor (13 sep 2026): los prompts se adaptan a lo que ya había.** Eso resuelve §4.1 a
favor de la opción (a): **se mantienen RA-025 y RA-026**. Nada de calaveras, huesos ni marcos góticos; el
gore es de estadio, no de mazmorra. Las razas son las cinco del lanzamiento y el HUD es el del juego que
existe, no el de un juego con barras de vida.

Estos son los prompts que se usan. Los de §2 se conservan solo para comparar.

### 2bis.1 Personajes y entorno

> Hand-drawn 2D concept art sheet for a dark comedy fantasy **7-a-side football** management game,
> inspired by the art style of **Lucky Tower, Castle Crashers and Pit People**. Clean 2D vector style,
> bold black lineart, vibrant flat colours with simple three-step cel-shading, cartoon slapstick
> aesthetics. Every character reads as a **footballer first, monster second**: they wear team kits with
> numbered shirts, sleeves in the team colour, boots and shin guards.
>
> **Top section — the five playable races, full body, readable in silhouette:**
> - **Dwarf**, short and squat: a beard that covers half his body, and a helmet.
> - **Human**, the neutral reference: ordinary build, nothing exaggerated.
> - **Undead**, gaunt and narrow: exposed ribs and one empty eye socket — a footballer who happens to be
>   dead, *not* a decorative skeleton.
> - **Elf**, tall and thin: height, narrowness and a long mane.
> - **Orc**, the widest: huge shoulders, long arms and tusks.
>
> Include one **goalkeeper variant** with a distinctive kit, and the **referee**: a single neutral-race
> sprite with a whistle, plus small portrait busts showing different temperaments (strict, lenient, homer,
> one-eyed, cowardly, corrupt).
>
> **Bottom section — environment, real football culture crossed with black humour:** a run-down municipal
> stadium; advertising hoardings around the pitch for sinister sponsors (an undertaker, a limb clinic, a
> butcher); hand-painted ultras banners in the stands; a manual flip-number scoreboard; a stretcher and a
> medical bag on the touchline; **dried blood stains on the grass** as flat comic-book red shapes.
>
> Clean presentation on a parchment background, readability focused, NPR cartoon aesthetic, high
> resolution.
> `--no realistic textures, 3d render, gradients, skulls, bones, gothic architecture, obsidian, ghosts, spectral effects, dungeon imagery`

### 2bis.2 Interfaz

> User Interface (UI) concept art sheet for a dark comedy fantasy football management game.
> Flash/webtoon cartoon aesthetic, thick black outlines, flat colours, high legibility.
>
> **Panel 1 — match view.** A **16 × 6 tile pitch seen whole**, from a **fixed three-quarter orthographic
> camera** (no perspective, no scrolling, no zoom). Each player stands on a **coloured ring on the grass
> bearing his shirt number**; the ring is the player's actual footprint, so a broad orc's ring is visibly
> larger than a lean undead's. Team identity by **kit colour only**. Small comic icons above a player show
> what he is doing — chasing, dribbling, shooting, tackling, knocked down, injured, sent off. A **score
> banner** at the top, a **match event log** panel, and a **timeline scrub bar** with marks at the key
> moments.
> **This is an autobattler: there are NO health bars, NO stamina meters and NO action buttons during the
> match.** The only live inputs are a single consumable and a forced-substitution window.
>
> **Panel 2 — between matches.** The **Team screen**: a placement grid where players are dragged onto
> tiles, player cards showing five attributes and their perks, and three consumable slots. The **Market**:
> four stalls side by side — players, perks, equipment and consumables — as item cards with a price, in
> bold medieval display type.
>
> Clean graphic layout, comic UI icons, game asset reference layout, high resolution.
> `--no photo realism, smooth gradients, 3d depth, health bars, mana bars, ability hotbars, skulls, gothic frames`

### 2bis.3 Proporciones, si se piden modelos

No son decorativas: **el cuerpo dibujado es el volumen que simula el motor** (`bodyRadius` de
`data/races/`), así que un encargo de modelo tiene que respetarlas o el juego mentirá sobre quién bloquea
a quién.

| raza | ancho relativo | alto relativo | rasgo firma |
|---|---|---|---|
| Enano | 0,60 | 0,74 | barba y casco |
| Orco | **0,76** | 0,86 | hombros, brazos largos, colmillos |
| No-muerto | **0,56** | 0,87 | costillas, cuenca vacía |
| Humano | 0,64 | 0,91 | referencia neutra |
| Elfo | 0,60 | **1,09** | altura, estrechez, melena |

**Humano y no-muerto no se distinguen por proporción** —lo midió la prueba de siluetas con cápsulas—, así
que su separación tiene que venir entera del rasgo firma. Es la comprobación que hay que hacer en el
primer modelo, no en el último.

---

## 3. Lo que encaja, y encaja mejor de lo que parece

**El «2D vector» y el 3D del ADR 0102 no se contradicen.** Los prompts son para **concept art**, y el
concept art de un juego con modelos se dibuja en 2D: es lo normal. Lo que el pipeline tiene que hacer es
**parecerse a esa hoja**, y da la casualidad de que toon shading más contorno produce exactamente eso:
color plano en escalones y línea negra gruesa. Dicho de otro modo, **la dirección técnica que ya
elegimos es la que mejor reproduce este estilo**; con pixelart habría sido más difícil.

Correspondencias que ya están escritas y no hay que tocar:

| el prompt pide | ya estaba en requisitos |
|---|---|
| `bold black lineart` | **RA-004**: contorno teñido según el material, nunca negro puro |
| `vibrant flat colors`, `simple cel-shading` | **RA-003**: máximo 6 colores por sprite, tono base + sombra + luz = la rampa del toon |
| `readability focused` | **RA-002**: toda raza reconocible **en blanco y negro** |
| `fixed 2.5D isometric broadcast camera` | **ADR 0102**: ortográfica fija en tres cuartos, 60° |
| `score banner at the top` | ya existe en la pantalla de Partido |
| `item cards`, `stat comparison panels` | ya existen: Mercado y la ficha de `ui-equipo.md` |

---

## 4. Lo que choca, y hay que decidirlo

### 4.1 Esqueletos y gótico contra RA-026 — **la contradicción seria**

El prompt pide **portero esqueleto**, **estadio gótico de obsidiana** y **estandarte espectral**. RA-026
dice, literalmente, lo contrario:

> *Se evita explícitamente la iconografía de calaveras, huesos y marcos góticos, que remite a Blood Bowl y
> a un lenguaje ajeno. **El gore es de estadio, no de mazmorra.***

Y RA-025 pide **cultura futbolística real** —marcadores de estadio, vallas publicitarias, pancartas de
ultras, prensa deportiva— cruzada con humor negro.

**Son dos direcciones distintas y hay que elegir una:**

- **(a) Mantener RA-025/026:** el estadio es un campo de barrio con vallas de patrocinadores siniestros,
  no una catedral de obsidiana. Los no-muertos son jugadores de fútbol que resultan estar muertos, no
  esqueletos decorativos. Es lo que separa este juego de Blood Bowl, que es el vecino más obvio y más
  peligroso.
- **(b) Cambiar RA-025/026** hacia el gótico del prompt, con la ADR correspondiente. Es legítimo, pero
  conviene saber que se acerca al terreno de Blood Bowl **a propósito** y que la identidad tendrá que
  venir de otro sitio.

**No lo decido yo.** Mientras no se decida, el documento deja las dos sobre la mesa.

### 4.2 El HUD pide cosas que este juego no tiene

`comic-style player health bars`, `stamina meters` e `interactive perk action slots at the bottom`.

- **No hay barras de vida.** Un jugador tiene estado físico (sano, tocado, lesión grave, muerto), no
  puntos de vida. Una barra sugeriría que se puede curar dentro del partido.
- **No hay slots de acción interactivos durante el partido.** Es un **autobattler**: *todas* las
  decisiones ocurren entre partidos. Lo único que se pulsa en vivo es el consumible manual (RF-082) y la
  ventana de sustitución forzada (ADR 0094). Dibujar una barra de acciones prometería un juego distinto.
- La **resistencia** sí existe como atributo, pero se lee en la ficha, no en el campo.

Lo que el HUD sí necesita, y está en `ui-partido.md` §6: dorsal, color de equipo, **estado** (persigue,
regatea, tira, entra, derribado, lesionado, expulsado), correa, marcaje y selección.

### 4.3 Las razas del prompt no son las del juego

El prompt pide **elfo oscuro**, **defensa con cuernos** y **árbitro goblin**.

- Las razas de lanzamiento son **cinco**: enano, humano, no-muerto, elfo, orco (RF-030, `data/races/`).
- **Elfo oscuro** y **demonio** (el de los cuernos) están en la tabla de RA-002 pero son **DLC**.
- **Goblin no existe** en ninguna lista. Y el árbitro, por RA-019b, es **una sola raza neutra con
  recolor**, no una raza propia.

Para una hoja de concept art exploratoria da igual; para un encargo de producción hay que corregirlo o se
paga arte de razas que no salen hasta el DLC.

---

## 5. Qué decidir en la iteración siguiente

1. **RA-026: gótico o estadio.** Es la que condiciona todo lo demás (§4.1).
2. **Qué encargo va primero.** Los **highlights** (RA-020..022) son estilo cómic, no dependen del 3D y son
   lo que más identidad da por euro. Los modelos de jugador dependen de la geometría, que ya está probada.
3. **Cuánta deformación aguanta la silueta.** Lucky Tower deforma mucho, y RA-002 exige reconocer la raza
   en blanco y negro. La prueba de cápsulas dio **3 de 5** y dijo que el rasgo firma —barba, cuernos,
   costillas, melena— es lo que tiene que separar humano de no-muerto. La deformación de tebeo **ayuda**
   a eso, pero hay que comprobarlo en el primer modelo, no en el último.
4. **La sangre**, que es lo más barato que más identidad da (RA-027, un nodo `Decal`): cuánta, de qué rojo
   y cuánto dura.
5. **Anatomía correcta o deformación de tebeo** (§5bis). El boceto del revisor va por anatomía creíble y
   §1 pide lo contrario. Condiciona el pipeline entero: con deformación, Mixamo chirría menos porque el
   modelo ya exagera; con anatomía creíble, mocap + modelo realista dan simulador serio, no Lucky Tower.
6. **Qué rasgo de CONTORNO se le inventa al no-muerto** (§5bis), porque el que tiene asignado en RA-002
   —costillas, cuenca vacía— es interior y no lee a 37 px.

## 5bis. 3D + Mixamo, y por qué la resolución no es el problema

**Conversación con el revisor, 15 sep 2026**, a partir de un boceto suyo a lápiz
(`docs/referencias/boceto-revisor-2026-09-15.jpg`): dos figuras de pie, construcción a la vista,
proporciones creíbles, complexión de matón de grada. La pregunta era si **3D + Mixamo** puede sustituir al
pipeline 2D sin perder el tono de Lucky Tower.

### El boceto: qué acierta y qué choca

Acierta en lo que más cuesta: **la complexión lee a hincha/matón**, que sirve a RA-025 (cultura
futbolística cruzada con humor negro) y esquiva RA-026 sin esfuerzo — ni calaveras, ni marcos góticos.

Choca de frente con §1 en un eje, y hay que decidirlo: Lucky Tower pide **«reacción exagerada por encima de
anatomía correcta»** y **«miembros de goma»**; el boceto es anatomía correcta por encima de exageración.
No es un refinamiento, es una bifurcación, y su consecuencia ya está escrita en §1: *«si la muerte se
dibuja solemne, el juego se vuelve deprimente»*. Con un jugador muriéndose cada dos runs, ese es el riesgo
tonal grande del proyecto.

### 3D sí; Mixamo, a medias

El 3D no está en discusión: lo decidió la **ADR 0102**. Y la mitad visual de Lucky Tower —línea negra
gruesa y color plano— sale nativa con toon shading y contorno por casco invertido.

**Mixamo es mocap: movimiento anatómicamente correcto por construcción**, o sea justo lo que §1 manda
subordinar. Pero el reparto es asimétrico y ahí está lo aprovechable:

| qué | veredicto |
|---|---|
| **Locomoción** (correr, andar, girar, esperar) — el 90 % del tiempo en pantalla | **Mixamo vale.** A 66 px por casilla nadie lee la sutileza, y ahorra el coste combinatorio de animar raza × acción × dirección |
| **Los ~10 momentos que llevan el tono** (la entrada que rompe, la muerte, la lesión, la celebración) | **Mixamo no vale.** La animación de muerte es el activo con más identidad del proyecto; una muerte de mocap es exactamente lo «solemne» que §1 prohíbe |

**Mixamo lleva las carreras, no la carnicería. Y la carnicería es el juego.**

Dos avisos más:
- **El rig no resuelve la identidad.** Lo que separa razas —barba, costillas, melena, hombros, colmillos—
  es trabajo de **modelado**, y Mixamo no aporta nada ahí (ver `ui-partido.md` §5).
- **El estilo y el pipeline interactúan.** Modelo de anatomía creíble + mocap realista = simulador de
  fútbol serio. Si se va a 3D + Mixamo, conviene un muñeco **más** caricaturesco que el boceto, no menos,
  porque la exageración del modelo es lo único que le pelea al realismo del movimiento.

### La resolución no es el cuello de botella; el contorno sí

El fallo de la prueba de cápsulas (**3 de 5 razas**, `ui-partido.md` §5) es un artefacto del placeholder,
no un límite del 3D: con modelos se recuperan todos los grados de libertad que a una cápsula le faltan.
Pero «más resolución» mezcla dos cosas muy distintas:

- **Detalle geométrico: ilimitado y gratis.** Con 20 figuras el recuento de polígonos es irrelevante.
- **Píxeles en pantalla: fijos.** Calculado sobre el campo de siete filas: `MatchPitchView` da 1120×500 px
  para 16×7 casillas con margen de un radio de ficha, o sea **66 px por casilla**, y una ficha mide
  `0,28 × 2 = 0,56` casillas → **unos 37 px de ancho**. Para comparar: RA-001 autoriza el sprite 2D a
  escala 3x, y un humano de 12×17 base son 36×51 renderizados. **Es casi el mismo presupuesto**: el 3D no
  da más píxeles, da más libertad de forma dentro de los mismos.

> **Regla para el briefing: solo cuenta el detalle que cambia el CONTORNO.** A 37 px y en blanco y negro,
> lo que va por dentro de la silueta no existe.

**Trampa concreta que hay que resolver antes del segundo modelo:** el rasgo firma que RA-002 asigna al
**no-muerto** es *«costillas, cuenca ocular vacía»*, y las dos cosas son detalle **interior**. No van a leer
nunca a ese tamaño, y el no-muerto es precisamente la raza que la prueba de silueta señaló como problema
(humano y no-muerto son la misma mancha). Hay que **inventarle un rasgo de contorno**: postura encorvada,
algo que le falte del cuerpo, miembros de longitud rara. El humano es la referencia neutra, así que el que
se mueve es el no-muerto.

**Y construir las firmas en horizontal.** A 60° de elevación lo que está de pie se comprime por el coseno,
y cos(60°) = 0,5: **la altura vale la mitad y la anchura vale entera**. Hombros, envergadura, volumen de
barba, apertura de cuernos, cola que sobresale — todo eso conserva su valor; ser alto o ser bajo, no. Es
lo que explica la prueba de cápsulas: el orco salió inconfundible por ancho y el enano se quedó a medias
porque su rasgo es la altura, que es el eje que la cámara aplasta.

### Qué robarle a Hades

Referencia que trajo el revisor. Lectura de sus técnicas (**interpretación, no medición**):

1. **Luz de borde.** Un filo de luz que separa al personaje del fondo: es un contorno hecho de luz, y a
   diferencia de una línea de tinta no engorda la silueta.
2. **Separación de VALOR, no de tono.** Suelos oscuros y desaturados, personajes claros y saturados. En
   blanco y negro el personaje sigue destacando, que es literalmente la prueba de RA-002.
3. **El fondo se calla donde se juega.** El detalle decorativo vive en los bordes; la zona jugable es
   plana a propósito.
4. **Autorizar grande y mostrar pequeño.** Los personajes de Hades son sprites 2D pintados a alta
   resolución. Refuerza lo de arriba: el cuello de botella es el diseño de forma, no los píxeles.
5. **Y el que de verdad manda: Hades no pide leer el cuerpo.** Lo que hay que saber se dibuja en el suelo
   con formas explícitas. El personaje solo dice *dónde está y de quién es*.

**Hades no es un análogo justo**, y conviene decirlo: allí hay un héroe y unos pocos enemigos de
arquetipos muy distintos. Aquí hay **veinte figuras** que deben distinguirse por equipo, identidad y
estado, y catorce de ellas son «futbolistas humanoides con equipación». El problema se parece más a un RTS
o a Into the Breach.

**Pero el proyecto ya llegó solo al punto 5**: la vista 2D pinta el estado con un anillo, el poseedor con
un halo, el marcaje con líneas y la intención con un punto. Eso es el telegrafiado de Hades aplicado, y es
justo la deuda que `ui-partido.md` §6 le apunta a la 3D (estado y correa: la 2D los tiene, la 3D no).

> **Reparto de carga que sale de todo esto:** el modelo carga **solo** con de qué equipo es (color, ya
> resuelto) y de qué raza es (silueta, en horizontal). **Todo lo dinámico sigue en anillos, halo, sombra y
> líneas.** A 37 px y con veinte cuerpos, pedirle al modelo que exprese el estado es pedirle lo que no
> puede dar.

Lo barato y de efecto inmediato: **luz de borde y contraste de valor contra un césped deliberadamente
apagado.** Compra legibilidad sin pedirle un píxel más al modelo y no contradice RA-005 (luz superior
izquierda constante) ni RA-004 (contorno oscuro teñido, nunca negro puro).

### Pruebas que debe pasar el primer modelo

No «¿mola?», sino dos pasa/no pasa con arnés que ya existe:

1. **Silueta (RA-002).** ¿Se separan **humano y no-muerto** en blanco y negro, a 60°, a 66 px por casilla?
   La captura es `partido-3d-silueta.png`, que no lleva anillos ni dorsales a propósito. `ui-partido.md`
   §5 ya dejó escrito que hay que comprobarlo **en el primer modelo, no en el último**.
2. **Mixamo contra el tono.** Una animación de derribo de Mixamo y una exagerada a mano, lado a lado a
   escala de partido. Si a 37 px la diferencia no se ve, Mixamo gana por coste; si se ve, ya se sabe qué
   animaciones hay que hacer a mano.

**Bloqueante práctico antes de invertir en 3D:** Godot corre aquí en WSL **sin editor gráfico** y lo visual
se comprueba con capturas por Xvfb — y **la escena de capturas está colgada** (`pendientes.md`, BA-L).
Importar y retargetear FBX y afinar un material toon a ciegas es ingrato; arreglar BA-L va antes, o se
trabaja sin instrumentos justo en la parte que se juzga por el ojo.

## 6. Historial

| versión | fecha | qué cambió |
|---|---|---|
| v0 | 13 sep 2026 | Punto de partida del revisor: tono Lucky Tower y los dos prompts de §2 |
| **v2** | 15 sep 2026 | §5bis: 3D + Mixamo (locomoción sí, los ~10 momentos del tono no), el presupuesto real de 37 px por ficha y la regla de que solo cuenta el contorno, la trampa del rasgo interior del no-muerto, construir firmas en horizontal por el coseno, y qué robarle a Hades. Boceto del revisor en `docs/referencias/` |
| **v1** | 13 sep 2026 | Prompts adaptados a lo ya decidido (§2bis): se mantienen RA-025 y RA-026 —ni calaveras ni gótico—, las cinco razas de lanzamiento con su rasgo firma, y el HUD real sin barras de vida ni botones de acción |
