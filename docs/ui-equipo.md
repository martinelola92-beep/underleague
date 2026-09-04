# Pantalla de Equipo

Decisiones de diseño de la pantalla de **Equipo**, la primera que se diseña y la única que se diseña en
detalle: **las demás derivan de ella** (UI-021). Todo lo que aquí se fija —composición, tipografía,
color, componente de ficha, patrón de inspección y mapa de mandos— es el contrato que Alineación previa,
Partido, Mercado, Recompensa e Informe post-partido tienen que respetar salvo motivo escrito.

Implementación: `Game/Scenes/Equipo.tscn` (+ `Game/Screens/TeamScreen.cs`),
`Game/Scenes/PlayerCard.tscn` (+ `Game/Ui/PlayerCard.cs`), `Game/Ui/PitchView.cs`,
`Game/Ui/LegendView.cs`, `Game/Ui/Style.cs`, `Game/Ui/UiText.cs`, `Game/Data/TeamState.cs`.
Capturas en `Game/screenshots/`.

Requisitos que cumple: UI-001..UI-006, UI-010..UI-014, UI-020, UI-021, RF-040..RF-045, RF-021..RF-023,
RF-106, RT-011, RT-014, RT-070, RT-071, RT-073, ADR 0028 y ADR 0029.

---

## 1. Qué es esta pantalla

Es **donde se toman todas las decisiones de plantilla** (UI-020): quién juega, dónde se coloca cada uno,
qué lleva puesto y qué se sabe de él. La alineación previa al partido no vuelve a plantear ninguna de
esas decisiones: solo las confirma, con opción de reposicionar (UI-020), de modo que si el jugador no
toca nada, "Empezar partido" es una pulsación (UI-003).

De ahí la regla de composición: **la lista de fichas y la cuadrícula tienen que verse a la vez**. La
decisión de colocación se toma mirando al jugador y al campo al mismo tiempo; una pantalla que obligue a
alternar entre las dos vistas ya ha fallado.

## 2. Composición

Resolución de diseño **1280x800** (RT-070), escalada con `canvas_items` + `keep`, así que la Steam Deck
la ve píxel a píxel y cualquier otra resolución la ve escalada sin recomponer nada.

```
┌ 0,0 ───────────────────────────────────────────────────────── 1280,800 ┐
│ EQUIPO   raza · club · plantilla de N                                  │  cabecera, 52 px
├───────────────┬────────────────────────────────────────────────────────┤
│ PLANTILLA     │ COLOCACIÓN                                             │
│               │  ┌──────────── cuadrícula 16x5, casilla de 52 px ────┐ │
│ TITULARES     │  │ mitad propia (0-7) clara · mitad rival (8-15)     │ │
│ · ficha 24 px │  └──────────────────────────────────────────────────┘ │
│ · ficha 24 px │  leyenda de capas y de vínculos                        │
│ · [expandida] │                                                        │
│ SUPLENTES     │  estado de la selección   │   ALINEACIÓN (lectura en   │
│ · ficha 24 px │  o del movimiento         │   texto de la cuadrícula)  │
│               │                                                        │
│               │  RATÓN ...                                             │
│               │  MANDO ...                                             │
└───────────────┴────────────────────────────────────────────────────────┘
   376 px            872 px
```

Reglas de composición que heredan las demás pantallas:

- **Columna izquierda de fichas, 376 px.** Es el ancho de la tira de UI-011 más el margen. En Partido son
  las dos tiras laterales; en Mercado, la columna de artículos. La ficha no cambia de ancho al cambiar de
  pantalla.
- **La ayuda de mandos vive abajo a la derecha, siempre, dos líneas: ratón y mando.** No es un tutorial:
  es la prueba visible de que los dos flujos de UI-006 existen.
- **Nada permanente sobre el campo** (UI-005). Sobre las fichas del campo no hay barras, ni iconos de
  mejora, ni estado físico: eso vive en las fichas (UI-014). Lo único que se pinta sobre el suelo es
  transitorio: la zona del jugador que se está manipulando, los vínculos que cambian y el mapa de
  cobertura mientras está encendido.

## 3. Tipografía y color

- **Dos tamaños de texto y ninguno menor de 11 px** (UI-004): **12 px** para el cuerpo y **17 px** para
  títulos y el nombre del jugador expandido. No hay un tercero, y la pantalla no crece un tamaño nuevo
  para resolver un problema de espacio: si algo no cabe, sobra texto.
- **Color y forma siempre juntos** (UI-002), sin modo daltónico:
  - *Posición*: color y silueta. Portero amarillo/cuadrado, defensa azul/triángulo hacia su portería,
    centrocampista verde/rombo, delantero rojo/triángulo hacia la portería rival. La silueta apunta
    adonde mira el jugador.
  - *Estado físico*: cuatro colores y cuatro iconos. Sano verde/círculo, lesión leve amarillo/triángulo,
    lesión grave rojo/barra partida, muerto gris/cruz.
  - *Capas de la zona de acción*: la zona lleva **borde continuo**; el margen exterior, **borde punteado
    y trama diagonal**. La distinción no depende de percibir dos tonos de azul.
- Fase 1 es **círculos de colores**, no arte (regla de fase, §7): el retrato de la ficha y la ficha del
  campo son el mismo círculo con la misma silueta, para que la lista y el campo se lean como una sola
  cosa. Cuando llegue el pixelart de fase 3 sustituye al círculo sin tocar la composición.

## 4. El componente de ficha (UI-010..UI-014)

`Game/Scenes/PlayerCard.tscn` es **el mismo componente** en Equipo, Alineación, Partido y Mercado
(UI-010). Es una escena independiente que solo recibe datos por `Bind(...)` y solo avisa hacia fuera con
la señal `Activated(playerId)`: no sabe qué pantalla la contiene ni conoce la cuadrícula. Reutilizarla es
instanciarla y llamar a `Bind`.

- **Colapsada** (UI-011): tira de **24 px** con retrato, icono de posición, nombre y barra de estado
  físico. Nada más. Es la unidad de medida de todas las listas del juego.
- **Expandida** (UI-012): nivel, rareza, raza, posición y etiqueta de estilo; los **cinco atributos** con
  barra; rasgos; perks **con su descripción generada** (RT-035) y los slots libres que quedan; habilidad
  racial; vínculos; objeto; estado; salario. **Solo una expandida a la vez**: lo garantiza la pantalla,
  no la ficha.
- **Reactiva** (UI-013): `Flash()` hace destellar la tira. Aquí lo dispara que el jugador cambie de
  casilla; en Partido lo disparará la activación de un perk, a la vez que el sprite.

Los bloques cortos (rasgos, objeto, estado, salario) van en una sola línea con el título; los largos
(perks, habilidad, vínculos) llevan título propio y texto envuelto. El alto lo calcula la ficha y el
contenedor se recoloca solo, así que una ficha con cuatro perks legendarios no rompe la lista.

## 5. Cuadrícula, colocación y zona de acción

- Cuadrícula de **16x5** completa (RF-040), con la **mitad propia** (columnas 0-7) en un verde más claro y
  la mitad rival apagada: se ve todo el campo, pero se distingue de un vistazo dónde se puede colocar.
  Las columnas y filas van numeradas porque la interfaz cita casillas por número.
- Colocación libre en la mitad propia **salvo el portero**, que ocupa una casilla fija y no la comparte
  (RF-041). Soltar a otro jugador en la casilla del portero es un movimiento inválido y el cursor se pone
  rojo antes de soltar: el error se avisa, no se castiga.
- Al **arrastrar o seleccionar** a un jugador se pintan **sus dos capas** (RF-045, ADR 0029): la **zona de
  acción** en tono sólido con borde continuo —"aquí estará"— y el **margen exterior** hasta el límite duro
  en tono claro con trama y borde punteado —"hasta aquí puede llegar"—. **Solo la del jugador
  manipulado**: siete zonas asimétricas superpuestas son una mancha que no informa de nada.
- La zona se recalcula **mientras se mueve**: lo que se ve es la zona que tendría si se soltara ahí.

## 6. Modo de cobertura del equipo

Una pulsación (X en el mando, C en el teclado) cambia el campo por el **mapa de calor de cobertura**
(ADR 0029 §4): cuántos jugadores tienen cada casilla dentro de su zona. **El número se escribe en la
casilla** además de pintarse el color, y las casillas que **no cubre nadie** llevan trama y borde rojos y
se cuentan en una línea de texto.

Existe porque el coste de apiñar el equipo era invisible: una alineación concentrada cuesta entre 16 y 24
puntos de tasa de victoria y nada en la interfaz lo insinuaba. Este modo responde a la única pregunta que
importa —*¿qué parte del campo no cubre nadie?*— y convierte la colocación en una decisión informada.

Mientras está encendido no se pinta la zona individual: son dos lecturas del mismo espacio y superpuestas
no se entiende ninguna.

## 7. Vínculos direccionales

- Los vínculos de la alineación se dibujan **siempre**, como líneas entre casillas (RF-106), en gris
  tenue: son estructura, no alarma.
- Al mover a un jugador se ve **qué se crea y qué se rompe** (ADR 0029 §5): verde con flecha lo que
  aparecería al soltar, rojo punteado lo que desaparecería. El texto de al lado los nombra, agrupados por
  compañero: `+ Ghash Matabueyes (delante, derecha, diagonal delante)`.
- Se detallan los cambios **del jugador manipulado**, que son los que ha provocado a propósito. Los de sus
  compañeros —que también cambian, porque el vínculo es un candidato por relación— se cuentan en una línea
  de resumen en vez de llenar la pantalla.

## 8. Un solo patrón de inspección y dos flujos de entrada

**Un solo patrón de inspección** (UI-001): *activar* a un jugador expande su ficha y pinta su zona. Da lo
mismo que la activación venga de un clic en la ficha, de un clic en su casilla o del botón de acción del
mando. Activar al ya activo lo colapsa.

**Dos flujos completos, ninguno adaptado del otro** (UI-006), sin acciones exclusivas de ratón (RT-071):

| Acción | Ratón | Mando / teclado |
|---|---|---|
| Mover el cursor | mover el ratón sobre la cuadrícula | cruceta o palanca izquierda |
| Inspeccionar | clic en la ficha o en la casilla | **A** (`ui_accept`) |
| Coger un jugador | pulsar sobre su casilla | **A** sobre su casilla |
| Soltarlo | soltar el botón en otra casilla, o volver a pulsar | **A** en la casilla destino |
| Cancelar el movimiento | — (se cancela soltando donde estaba) | **B** (`ui_cancel`) |
| Colapsar la ficha | clic otra vez en la ficha | **B** |
| Modo de cobertura | botón **Cobertura del equipo** | **X** (`team_coverage`) / tecla **C** |
| Cambiar de lista a campo | clic donde toque | **derecha** desde la lista, **izquierda** en la columna 0 |

El cursor es **uno solo**: el ratón lo mueve al pasar por encima y la cruceta lo mueve casilla a casilla.
No hay dos estados de foco compitiendo. El anillo de foco tampoco necesita un botón propio para cambiar
de panel: salir por la izquierda de la columna 0 entra en la lista y salir por la derecha vuelve al campo.

## 9. Qué expone `/Sim` y por qué

La pantalla **no calcula nada del juego** (RT-014). Todo lo que se pinta lo resuelve
`Sim.Placement.PlacementView`, público y puro, sin E/S ni aleatoriedad:

| Método | Qué devuelve | Requisito |
|---|---|---|
| `CanPlace(position, cell)` | si una colocación es válida | RF-041 |
| `WithPlayerAt(lineup, players, id, cell)` | la alineación resultante de mover, con sus reglas de intercambio y de sustitución | RF-041 |
| `ZoneOf(player, cell, catalog)` | las dos capas de la zona, en casillas absolutas | RF-045, ADR 0028 |
| `Links(lineup)` | los vínculos direccionales de la alineación | RF-044, RF-106 |
| `Coverage(players, lineup, catalog)` | cuántos jugadores cubren cada casilla, y cuántos huecos hay | ADR 0029 §4 |

La geometría es **la misma que usa el motor** (`Sim.Engine.ActionZone`, `Sim.Perks.LinkGeometry`), no una
copia: lo que la pantalla promete es exactamente lo que la simulación va a aplicar. Una casilla pertenece
a una capa si su **centro** cae dentro, que es el punto con el que el motor decide si un jugador está
fuera de su zona.

`Sim.Generation.PerkAssignment` reparte los perks iniciales de la rareza (RF-023) de forma determinista y
uniforme entre los elegibles. Es **provisional a propósito**: la vía normal de conseguir un perk es la
recompensa por partido ganado (RF-071) y el mercado (RF-114e), las dos de fase 2. Sin él, una plantilla
recién generada no tiene perks y la ficha no puede enseñar lo que UI-012 exige.

## 10. Localización

Todo el texto sale de una clave (RT-073). El **vocabulario del juego** —posiciones, atributos, rasgos,
etiquetas de estilo, razas, relaciones de vínculo y las descripciones generadas de los perks— se lee de
`data/l10n/<idioma>/templates.json` a través del catálogo, así que ya está localizado de verdad. El
**mobiliario de la pantalla** —títulos, ayudas de control, etiquetas de campo— vive en `Game/Ui/UiText.cs`
indexado por clave (`ui.team.*`, `ui.card.*`, `ui.input.*`), precisamente para que en fase 4 el cuerpo de
`UiText.Get` pase a leer `data/l10n/<idioma>/ui.json` sin tocar ninguna pantalla.

## 11. Huecos conocidos

Se enseñan como huecos, no se rellenan con mentiras:

- **Objeto**: el equipamiento (RF-075..RF-076) es de fase 2. La ficha reserva la fila y dice
  "sin objeto (equipamiento: fase 2)".
- **Salario**: la economía (RF-114g..k) y los mercenarios (RF-110..113) son de fase 2, y `PlayerDefinition`
  no tiene salario. La ficha reserva la fila y lo dice.
- **Club**: los clubes son datos de fase 2; el subtítulo pone "club de pruebas" y la plantilla se genera
  con la raza orca.
- **Perks iniciales**: ver §9.
- **RF-022b** dice que la posición "restringe las filas y columnas donde puede colocarse". La única
  restricción que el diseño concreta hoy es la del portero (RF-041), así que es la única que se aplica:
  lectura conservadora. Si hacen falta más, se deciden en `requisitos.md`, no en la pantalla.
- No hay **tests** de `PlacementView`: RT-084 prohíbe tests de interfaz, pero `PlacementView` es `/Sim` y
  merece los suyos (zona por posición, vínculos frente a `LinkTable`, cobertura). Queda para el paquete
  que lo cierre.

## 12. Qué heredan las demás pantallas

1. La ficha de jugador es **esta escena**, con estos tres estados y este ancho. Nadie escribe otra.
2. **Activar = inspeccionar**, con el mismo botón y el mismo clic, en fichas, perks, objetos, nodos y
   artículos de mercado.
3. **Dos tamaños de texto**, 12 y 17. Ninguna pantalla añade un tercero.
4. **Color y forma**, siempre las dos cosas, con la tabla de colores de `Game/Ui/Style.cs`.
5. **Cursor único** para ratón y mando, y ayuda de mandos en dos líneas al pie.
6. Sobre el campo, **solo información transitoria**.
7. Lo que la pantalla necesite saber del juego se le pide a `/Sim` con un método puro. Si no existe, se
   añade a `/Sim`; no se calcula en la interfaz.

## 13. Cómo se regeneran las capturas

`--headless` **no** sirve: usa el renderizador nulo, ejecuta la pantalla pero no dibuja nada y
`GetImage()` devuelve null. Hace falta un servidor X virtual y el renderizador de software:

```bash
dotnet build Game/Game.csproj
godot --headless --path Game --import
xvfb-run -a --server-args="-screen 0 1280x800x24" \
  godot --path Game --rendering-driver opengl3 --audio-driver Dummy -- --screenshots
```

Deja en `Game/screenshots/` las cuatro capturas: `equipo.png` (estado inicial), `equipo-zona.png` (un
jugador cogido, con sus dos capas y los vínculos que se crean y se rompen), `equipo-cobertura.png` (modo
de cobertura) y `equipo-ficha.png` (ficha expandida con perk y descripción generada). Las tres últimas se
alcanzan **con eventos de mando sintéticos**, no llamando a los métodos por dentro: la secuencia comprueba
de paso que la navegación sin ratón lleva a los mismos estados.

El render es por software (Mesa/llvmpipe), así que las capturas valen para juzgar composición, color,
proporción y legibilidad, **no** fluidez.
