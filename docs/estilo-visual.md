# Estilo visual

**Versión 0 · 13 de septiembre de 2026 · NO definitivo, se itera.**

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

## 6. Historial

| versión | fecha | qué cambió |
|---|---|---|
| v0 | 13 sep 2026 | Punto de partida del revisor: tono Lucky Tower y los dos prompts de §2 |
