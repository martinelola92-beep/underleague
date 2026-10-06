# Editar la interfaz en Godot y sustituir el dibujo por sprites

*(4 oct 2026, decisión del revisor: el arte de la interfaz lo produce él, pieza a pieza — `CLAUDE.md`, regla 10.)*

## Dónde se edita

- **Clon de Windows**: `C:\dev\underleague`. Se abre `C:\dev\underleague\Game\project.godot` con el
  Godot 4.6.3 **mono** de Windows.
- Antes de abrir una escena: botón **Build** (arriba a la derecha, el martillo) para compilar el C#. Sin
  compilar, las piezas `[Tool]` salen vacías o con un aviso de script.
- Al terminar: commit y push desde `C:\dev\underleague`, y avisar para que en WSL se haga `git pull`
  antes de tocar la misma pantalla.

## Qué está preparado

| Pieza | Escena | Estado |
|---|---|---|
| Tablero superior del partido (marcador, orden, criterio, consumibles, gritos, velocidad) | `Game/Ui/Broadcast/BroadcastBoard.tscn` | **Listo** (piloto) |
| Botón de consumible del tablero | `Game/Ui/Broadcast/ConsumableButton.tscn` | Listo |
| Etiqueta de grito del tablero | `Game/Ui/Broadcast/ShoutTag.tscn` | Listo |
| Estilos comunes de los botones del tablero | `Game/Ui/Broadcast/Tablero.tres` (tema) | Listo |
| Tira de jugador (escudo, dorsal, nombre, perks, estado, fatiga, destello) | `Game/Ui/Broadcast/PlayerStrip.tscn` | Listo. Por código: la marca de estado físico (`MarcaEstado`) y el escudo (`Escudo`) |
| Placa de banquillo | `Game/Ui/Broadcast/BenchPlaque.tscn` | Listo |
| Sello torcido (falta, amarilla, lesión, perk, consumible, anulado) | `Game/Ui/Broadcast/Stamp.tscn` | Listo. Cada tono tiene sus marcas (`MarcaFalta`, `MarcaAmarilla`, `MarcaCruz`, `MarcaAnulado`) y el código enseña las que tocan |
| Estandarte de gol, roja y lesión | `Game/Ui/Broadcast/HeraldBanner.tscn` | Listo. Dos grupos, `Propio` y `Rival`, con cintas, escudo, corona y títulos de su color. Por código: escudos y coronas (`Escudo`, `Corona`) |
| Banda de pregón y tirada del destino | `Game/Ui/Broadcast/ProclamationBand.tscn` | Listo. Por código: la trompeta (`Trompeta`) y el lacre que gira (`Lacre`); el porcentaje y el ✓/✗ son rótulos |
| Bando de muerte | `Game/Ui/Broadcast/Edict.tscn` | Listo. Por código: la trompeta (`Trompeta`) y el lacre en estrella (`Lacre`) |
| Acta del encuentro | `Game/Ui/Broadcast/MatchRecord.tscn` | Listo. Por código: los dos escudos (`EscudoPropio`, `EscudoRival`) |
| Bandeja de sustitución | `Game/Ui/Broadcast/DecisionTray.tscn` | Listo. Cada casilla es la escena `TraySlot.tscn` |
| Casilla de la bandeja (el que sale, candidato, respuesta) | `Game/Ui/Broadcast/TraySlot.tscn` | Listo. Por código: el escudo (`Escudo/Dibujo`) |
| **Pantalla del partido entera**, con las nueve piezas colocadas | `Game/Scenes/Retransmision.tscn` | Listo. Ver abajo |
| Resto de pantallas | — | Pendiente, solo si se piden |

## La pantalla del partido entera

`Game/Scenes/Retransmision.tscn` lleva las nueve piezas colocadas donde salen en el juego, sobre un lienzo
de 1920 × 1080 (el juego lo escala a la ventana). Ahí se **colocan**: mover el tablero, las tiras o la
bandeja se hace en esta escena; cambiar su **aspecto**, en la escena de cada pieza (doble clic en el
icono de escena del nodo, o abrir `Game/Ui/Broadcast/<Pieza>.tscn`).

- En el editor salen **todas a la vez** con datos de ejemplo, unas encima de otras (en el juego solo se
  ve cada una en su momento). Apaga el ojo de las que estorben mientras colocas otra.
- El campo 3D, el fondo y el velo los pone el código **por debajo** de todo: en el editor el fondo sale vacío.
- Las tiras y la bandeja están ancladas abajo y el tablero arriba: aguantan cualquier proporción de ventana.
- El **estandarte** cambia de lado en el juego (sale por el lado contrario a donde pasó la jugada): de lo
  que pongas en la escena se respeta la altura y el tamaño; el lado lo decide el código.
- No renombres los nodos con `%` (`Tablero`, `Tira1`…`Tira7`, `Banquillo`, `Sello`, `Estandarte`, `Banda`,
  `Bando`, `Acta`, `Bandeja`).

## Cómo está hecha una pieza

- **Todo lo que se ve es un nodo** de la escena: `Panel` (placas, paños, fondo), `Button`, `Label`,
  `ProgressBar`. Se mueven y redimensionan con el ratón.
- **El dibujo de tinta es un recurso, no código**: cada fondo es un `InkStyleBox` en *Theme Overrides →
  Styles* del nodo. Es el relleno provisional.
- **El código solo rellena los nodos marcados con `%`** (nombre único, el icono `%` junto al nombre en el
  árbol de escena): pone textos, enciende y apaga botones, enseña u oculta piezas. No mira la posición, el
  tamaño, el color ni la textura.
- **En el editor la pieza sale rellena con un partido inventado** («Altos Hornos FC» 1-0 «Yunque Verde»,
  dos consumibles, un grito). En el juego esos textos los pone el partido real.

## Sustituir un dibujo por un sprite

1. Importa el PNG dentro de `Game/` (por ejemplo `Game/Ui/Broadcast/arte/`).
2. Selecciona el nodo (por ejemplo `Bloque/PlacaPropia`).
3. Inspector → *Theme Overrides → Styles → panel* → desplegable → **New StyleBoxTexture**.
4. Arrastra el PNG a su *Texture*. Si el sprite tiene marco y debe estirarse sin deformarse, rellena los
   *Texture Margin* (nueve partes).
5. Para volver a la tinta: desplegable → *New InkStyleBox* (o deshacer).

Para un **botón**, los estados son estilos distintos: `normal` (apagado), `pressed` (encendido: la orden
vigente, la velocidad elegida, la pausa activa, el consumible listo), `disabled` (no se puede pulsar ahora)
y `hover`. **Para cambiar todos los botones a la vez se edita el tema `Tablero.tres`**; para cambiar uno
solo, sus *Theme Overrides*.

Para el **escudo** de un paño (`Escudo`, dibujado por código): ocúltalo (ojo en el árbol) y añade al lado
un `TextureRect` con tu escudo.

## Añadir elementos nuevos

- **Decorativos** (un marco, un remache, una textura, un adorno): añádelos como hijos donde quieras. Si no
  llevan `%` el código no los toca. Ponles *Mouse → Filter = Ignore* si quedan encima de un botón, para no
  taparle los clics.
- **Que enseñen un dato del partido** (por ejemplo la posesión): añade el nodo, márcalo con `%` y ponle un
  nombre, y pide que se conecte. Eso sí necesita código.

## Lo que no hay que hacer

- **No renombrar ni borrar los nodos con `%`**: el código los busca por nombre. Moverlos de padre sí se
  puede: el `%` los encuentra en cualquier sitio de la escena.
- No escribir textos en los `Label` con `%`: los sobrescribe el código. Su fuente, tamaño, color y
  alineación sí son tuyos.
- La fila `FilaAcciones` la llena el código con consumibles y gritos: se edita su posición, y el aspecto de
  sus botones y etiquetas en `ConsumableButton.tscn` y `ShoutTag.tscn`.
- La pista del medidor de criterio (`PistaCriterio`) sigue dibujada por código porque se mueve con el
  dato; la placa y el rótulo que la rodean sí son editables.
- **Lo que sigue dibujado por código** va siempre en su propio nodo pequeño (`InkShield`, `InkTrumpet`,
  `InkBurst`, `InkCrown`, `FateSeal`, `StripStateMark`, `BiasTrack`). Para poner un sprite se oculta ese
  nodo (ojo en el árbol) y se añade un `TextureRect` al lado; el código no lo toca.
- **Piezas con variantes por dato** (estandarte propio/rival, tono del sello, casilla de la bandeja, barra
  de fatiga verde/oro/sangre, fondo normal/apagado de la tira): la escena lleva **todas las variantes** como
  nodos hermanos y el código enseña solo la que toca. Para cambiar el aspecto de una variante, se edita su
  nodo; las que no se ven en ese momento se ven apagando el ojo de las demás.
- **Excepciones de color o tamaño que pone el código**, porque dependen del dato: la tinta del texto del
  sello (un color por tono) y el tamaño de letra de los títulos del estandarte, del bando y del acta,
  que baja hasta que el texto cabe en el ancho del nodo (parte de la fuente y del tamaño de la escena).
  Las tiras atenuadas (muerto o expulsado) bajan la opacidad de sus textos, no su color.
- Al sello lo tuerce el código (giro determinista según el orden de creación); el giro no se guarda en la
  escena.
- En la bandeja, el ancho de cada casilla lo reparte el código a partir del ancho del nodo `Fila`.
