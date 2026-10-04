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
| Tiras de jugadores, banquillo, sello, estandarte, banda, edicto, acta, bandeja | — | Pendiente: siguen dibujadas por código |
| Resto de pantallas | — | Pendiente, solo si se piden |

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
