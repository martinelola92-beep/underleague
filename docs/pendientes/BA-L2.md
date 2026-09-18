# BA-L2 — `CaptureRunner` pierde el árbol de escena entre `informe` y `recompensa`

**Estado:** RESUELTA (19 sep 2026). La secuencia llega hasta el final y produce `mercado.png`. Queda un
defecto SEPARADO destapado por el arreglo: `recompensa.png` sale en blanco (ver abajo).

## Observación

Con la ruta de captura ya operativa (`--scene res://Scenes/Capturas.tscn`), la secuencia produce
`partido*`, `partido-3d*` e `informe.png` correctamente, pero se detiene ahí: no llega a capturar
`recompensa.png` ni `mercado.png`.

## Análisis

Dos excepciones seguidas justo después de `informe.png`:

```
ERROR: Parameter "data.tree" is null.
   at: get_tree (scene/main/node.h:549)
   [...] Underleague.Game.Screens.CaptureRunner+<Show>d__4.MoveNext() (CaptureRunner.cs:293)
ERROR: Parameter "p_source" is null.
   at: gd_mono_connect_signal_awaiter
   [...] Godot.GodotObject.ToSignal(...) (CaptureRunner.cs:293)
```

`Show()` llama a `GetTree()` sobre el propio `CaptureRunner`, y devuelve null — como si el nodo se hubiera
desconectado del árbol de escena entre la captura de `informe` y el intento de mostrar `recompensa`. No se
ha mirado más allá de esta localización: falta identificar qué paso entre medias saca a `CaptureRunner`
del árbol.

## Hermanos

Ninguno. Es un fallo de ciclo de vida propio de `CaptureRunner`, sin relación con la causa de BA-L (que era
de comando, no de código).


## Resolución (19 sep 2026)

**Causa: `CaptureRunner` instancia las pantallas del juego como HIJAS suyas**, no como escena raíz
(`Show()` hace `AddChild(instance)`). Cuando una de esas pantallas navega —`Nav.Go`/`Nav.Route` hacen
`GetTree().ChangeSceneToFile(...)`— se cambia la escena RAÍZ, que es `Capturas.tscn`, y con ella se destruye
el propio arnés. A partir de ahí `GetTree()` sobre `CaptureRunner` devuelve null y el recorrido muere.

Quién navegaba, capturado en el log del arreglo:

```
captura: informe.png
navegación silenciada (captura): se pedía ir a res://Scenes/Mapa.tscn
```

`ReportScreen._Ready()` enruta al Mapa justo después de `informe.png`. Eso explica exactamente por qué la
secuencia moría ahí y no antes.

**Arreglo**: `Nav.Suppressed` (`Game/Ui/Nav.cs`), que `CaptureRunner` enciende en `_Ready` y apaga al
terminar. Con él, una pantalla que decida navegar deja constancia en el log y se queda donde está —que es
lo que una captura necesita: enseñar la pantalla, no irse de ella. No afecta al juego: solo lo enciende el
arnés.

**Resultado**: la secuencia completa produce `partido*`, `partido-3d*`, `informe`, `recompensa`,
`mercado` y `mercado-perk` (captura nueva: la columna de perks con uno elegido, para poder juzgar el panel
de detalle, BB-J).

## Defecto SEPARADO que el arreglo destapa: `recompensa.png` sale en blanco

Antes no se producía; ahora se produce **vacía** (fondo gris, sin un solo control). La pista está en el
mismo log: si `ReportScreen` pide ir al **Mapa** tras el informe, es que para entonces el estado de la run
ya no tiene nodo abierto, así que cuando se instancia `Recompensa.tscn` no hay recompensa que enseñar y la
pantalla se dibuja vacía.

O sea: el cuelgue era el síntoma, y **el estado de la run en ese punto del recorrido también está mal**.
No se arregla aquí porque es otra cosa: hay que revisar en qué momento la secuencia cobra o resuelve la
recompensa (`ResolveRewards`, `run.Apply(new ChooseReward(...))`) frente a cuándo la captura.
