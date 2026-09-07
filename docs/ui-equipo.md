# Pantalla de Equipo

Decisiones de diseño de la pantalla de **Equipo**, la primera que se diseña y la única que se diseña en
detalle: **las demás derivan de ella** (UI-021). Todo lo que aquí se fija —composición, tipografía,
color, componente de ficha, patrón de inspección y mapa de mandos— es el contrato que Alineación previa,
Partido, Mercado, Recompensa e Informe post-partido tienen que respetar salvo motivo escrito.

Implementación: `Game/Scenes/Equipo.tscn` (+ `Game/Screens/TeamScreen.cs`),
`Game/Scenes/PlayerCard.tscn` (+ `Game/Ui/PlayerCard.cs`), `Game/Ui/PitchView.cs`,
`Game/Ui/LegendView.cs`, `Game/Ui/Toast.cs`, `Game/Ui/Style.cs`, `Game/Ui/UiText.cs`,
`Game/Data/TeamState.cs`, y en `/Sim` el previsualizador `Sim/Perks/LineupPerkPreview.cs`.
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
│               │                           │   RIESGO DE MUERTE (AW-G)  │
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
- **Expandida** (UI-012): **media de atributos** (AW-M, media simple de los cinco sin ponderar por
  posición, en `Sim.Model.Attributes.Average`) junto a nivel, rareza, raza, posición y etiqueta de
  estilo; los **cinco atributos** con barra —y, si el jugador lleva un objeto equipado, el delta con
  signo y color que ese objeto le pone a cada atributo que toca (verde el que sube, rojo el que baja,
  reutilizando `Style.LinkCreated`/`Style.LinkBroken`, los mismos del aviso de perk)—; rasgos; perks
  **con su descripción generada** (RT-035) y los slots libres que quedan; habilidad racial; vínculos;
  objeto (AW-K: nombre y descripción generados igual que un perk, con `ItemDescriptions.Describe`, o el
  marcador de "sin objeto" si no lleva ninguno); estado; salario. **Solo una expandida a la vez**: lo
  garantiza la pantalla, no la ficha.
- **Reactiva** (UI-013): `Flash()` hace destellar la tira. Aquí lo dispara que el jugador cambie de
  casilla; en Partido lo disparará la activación de un perk, a la vez que el sprite.

Los bloques cortos (rasgos, estado, salario, y objeto cuando no lleva ninguno) van en una sola línea con
el título; los largos (perks, habilidad, vínculos, y objeto cuando sí lleva uno) llevan título propio y
texto envuelto. El alto lo calcula la ficha y el contenedor se recoloca solo, así que una ficha con
cuatro perks legendarios no rompe la lista.

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

### Modo de zonas de inicio (botón "Zonas del campo", Z / Y)

Los perks de colocación describen la cuadrícula con palabras —"empieza en su tercio adelantado", "en
cualquier fila de su izquierda"— y esas palabras no correspondían a nada visible. Una pulsación pinta
sobre la mitad propia los **tres tercios de inicio** (columnas 0-2, 3-5 y 6-7) con tres tintes y su
nombre escrito, y las **tres bandas** por filas (la 2 es la fila central; el resto, banda), separadas
con línea punteada para no confundirlas con los cortes de tercio. Los nombres no se escriben en la
pantalla: salen de `startZones` y `startFlanks` de `data/l10n`, que es de donde salen las descripciones
de los perks, así que el campo y el texto del perk no pueden llamar a lo mismo de dos maneras.

Mientras está encendido, el panel de texto explica las tres cosas que el jugador no puede deducir: que
un perk de inicio mira la **casilla de alineación** y no dónde acabe el jugador durante el partido, que
"banda" es cualquier fila menos la central, y que un vínculo une a dos titulares a **dos casillas o
menos** en la dirección que el perk pide (RF-044). Es excluyente con el modo de cobertura, por la misma
razón que este lo es con la zona individual.

### El nombre de la zona, dentro del texto del perk (AW-F)

El modo de zonas responde a "¿dónde están las zonas?" pero hay que saber que existe y pulsarlo, y
mientras está encendido tiñe las tres a la vez. La pregunta que se hace de verdad se hace **leyendo la
ficha**, y es más estrecha: "¿dónde está *ésta*?". Así que el nombre de la zona es interactivo dentro de
la propia descripción.

- El texto de los tercios y las bandas se reescribió para que se sostenga solo fuera de esta pantalla
  (ficha, informe, Ojeo): "el tercio rival" —que se leía como el campo del rival— pasa a **"su tercio
  adelantado"**, "el centro del campo" —que se leía como el círculo central— a **"su tercio central"**, y
  "la banda izquierda" —que se leía como la fila 0— a **"cualquier fila de su izquierda"**. Ninguna de
  las seis frases usa ya una palabra del terreno de juego para nombrar una franja de la cuadrícula de
  alineación, que era el fondo de AW-F.
- En la ficha esas seis frases van **subrayadas y en color de acento** dentro de las secciones de PERKS y
  HABILIDAD RACIAL, las dos que pintan texto generado (RT-035). Pasar el ratón por encima abre un
  **tooltip** con la definición exacta —qué columnas o qué filas, y que lo que cuenta es la casilla en la
  que alineas y no dónde acabe el jugador en el partido— y, a la vez, **tiñe esa franja y sólo esa** sobre
  la cuadrícula, con el mismo tono de acento con el que está marcada la frase, borde continuo y su nombre
  rotulado. Funciona con el modo de zonas apagado y desde la ficha de cualquiera, titular o suplente: la
  zona la nombra el texto, no su portador.
- Es una ayuda **pasiva de sólo ratón**, como cualquier tooltip: no cambia ningún estado, así que no
  necesita camino equivalente por mando (UI-006 habla de las dos formas de *hacer* cosas). El camino de
  mando a la misma información existe y es el botón "Zonas del campo" (Z / Y).
- Lo que dice cada explicación vive en `data/l10n/<idioma>/templates.json`, en las claves con sufijo
  `Hint` de `startZones` y `startFlanks` —al lado de la frase corta que explican, para que no puedan
  divergir— y está en los dos idiomas. `/Sim` no las mira: `DescriptionGenerator.Describe` sigue
  devolviendo **texto plano y sin marcado**, que es lo que consumen el aviso, Ojeo y sus tests. El paso a
  BBCode lo hace `Game/Ui/ZoneHintText.cs`, que busca en ese texto plano las seis frases —leídas de las
  plantillas, nunca escritas en el código— y envuelve la primera aparición de cada una en
  `[url=zone:AttackingThird][hint="..."]…[/hint][/url]`; `RichTextLabel` pone el tooltip y emite
  `meta_hover_started`, y `PlayerCard` lo reenvía a la pantalla como la señal `ZoneHint`.
- El marcado **no cambia el alto de la ficha**: el texto se le da al `RichTextLabel` ya partido en líneas
  por `Style.Wrap` y con el autoajuste apagado, así que ocupa exactamente las mismas líneas de 14 px que
  contaba el dibujo a mano, y la ficha mide lo mismo lleve marcado o no.

### Aviso al soltar: qué perk se ha encendido o apagado

Soltar a un jugador puede activar o desactivar un perk —suyo o de un compañero— y hasta ahora eso pasaba
en silencio: el jugador se enteraba en el informe post-partido, que es exactamente lo que el principio
rector prohíbe (RF-012d). Al soltar aparece un aviso de unos tres segundos sobre el borde inferior del
panel del campo con **todos** los perks decidibles del movido —en verde los que se activan ahí, en rojo
los que no, porque saber que ahí *no* se activa es la mitad de la decisión— y solo los **cambios** de los
demás titulares. Un aviso nuevo sustituye al anterior; si no hay nada que decir, no hay aviso.

El estado lo resuelve `Sim.Perks.LineupPerkPreviewer` (la pantalla no evalúa ninguna condición, RT-014),
que compara dos alineaciones y responde solo por los perks que la colocación decide por completo
—`startsIn`, `startsOn`, `linked`, `hasTag`, `teammatesWithTag`, `adjacentCount`—. Un perk cuya condición
dependa de cómo vaya el partido (`zone`, `scoreDiff`, `stat`...) se omite: prometer una activación que el
partido puede desmentir sería peor que callar.

### Suplentes: se cogen y se sueltan igual que un titular (AW-L)

Hasta ahora activar una ficha —de titular o de suplente— hacía exactamente lo mismo: expandirla o
colapsarla. Un suplente no tenía forma de llegar al campo salvo arrastrando a un titular hasta su casilla
y viendo que `/Sim` ya resolvía la sustitución (`PlacementView.WithPlayerAt`, `TeamState.Move`): el hueco
era solo de interfaz.

Ahora activar a un suplente **cuando no hay nadie cogido** lo coge en vez de expandirlo —coger tiene
prioridad sobre inspeccionar—, con el mismo pellizco visual que un titular cogido desde su casilla (borde
de acento, la misma marca que usa `Selected`): conceptualmente es la ficha que se está manipulando, así
que reutiliza la misma pista en vez de inventar una segunda. Soltarlo en una casilla libre lo añade a la
alineación; soltarlo sobre un titular lo sustituye. Volver a activar la ficha del suplente cogido lo
suelta sin colocar, el mismo gesto de cancelar que soltar donde se pulsó sobre una casilla.

Los dos caminos de entrada de UI-006 llegan al mismo sitio: un clic en la ficha (`OnCardActivated`) y el
botón de acción con el foco en la lista (`ui_accept` con `_focusRoster`) llaman al mismo método
(`ActivateRosterCard`), así que ratón y mando cogen y sueltan a un suplente exactamente igual.

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

### Riesgo de muerte por titular (AW-G)

Bajo la tabla de ALINEACIÓN, un segundo bloque de texto (`TituloRiesgo` / `Riesgo` en `Equipo.tscn`) lee
el mismo riesgo de muerte por titular que el ojeo (RF-012c, `ScoutScreen.BuildReport`), pero recalculado
sobre la alineación que se está mirando **en ese instante** —previsualización de un jugador cogido
incluida, no solo la ya guardada—, porque RF-012c pide poder reducir el riesgo con la alineación (ADR
0048) y eso exige ver el número moverse al mover una ficha, no solo al volver al ojeo.

- Se apoya en la misma fachada que el ojeo, `RunEngine.LethalRisks(state, nodeId, catalog, systems,
  lineup)`, con la alineación que ya calcula `RefreshPitch` (`_held >= 0 ? _state.Preview(...) :
  _state.Lineup`) y se recalcula en el mismo punto que todo lo demás que depende de la alineación
  (`TeamScreen.RefreshRisk`, llamado desde `RefreshPitch`).
- **Solo se muestra con una run en curso y un nodo de partido elegido** (`RunController.Instance.HasRun`
  y `SelectedNodeId >= 0`): sin nodo no hay rival del que salga el riesgo, y el bloque entero se oculta en
  vez de enseñar un "sin riesgo" que no sería cierto. Fuera de una run (plantilla de pruebas, rival del
  ojeo) tampoco se muestra.
- Sin ningún titular con riesgo, se dice explícitamente (`ui.scout.riskNone`, reutilizada del ojeo): la
  ausencia de riesgo es información igual de accionable que el riesgo (RF-012d).
- El hueco donde vive: se reclamaron los últimos ~160 px del bloque VÍNCULOS (`Vinculos` pasa de
  432-720 a 432-560 en `Equipo.tscn`), que sobraban en la práctica —el once es siempre de 7 titulares
  (fútbol 7) y el texto de vínculos no se acerca a llenar 288 px—, así que no hace falta encoger nada más
  ni tocar la cuadrícula, la leyenda o el panel de selección.

## 8. Un solo patrón de inspección y dos flujos de entrada

**Un solo patrón de inspección** (UI-001): *activar* a un jugador expande su ficha y pinta su zona. Da lo
mismo que la activación venga de un clic en la ficha, de un clic en su casilla o del botón de acción del
mando. Activar al ya activo lo colapsa.

**Dos flujos completos, ninguno adaptado del otro** (UI-006), sin acciones exclusivas de ratón (RT-071):

| Acción | Ratón | Mando / teclado |
|---|---|---|
| Mover el cursor | mover el ratón sobre la cuadrícula | cruceta o palanca izquierda |
| Inspeccionar | clic en la ficha o en la casilla | **A** (`ui_accept`) |
| Coger un jugador | pulsar sobre su casilla, o clic en su ficha si es suplente | **A** sobre su casilla, o con el foco en su ficha si es suplente |
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

- **Objeto**: resuelto (AW-K). `TeamState.EquippedItemOf(playerId)` busca el id de `RunPlayer.Item` en el
  roster de la run y lo resuelve en `StandardRunSystems.Items`; la ficha enseña su nombre, su descripción
  generada y el delta con signo y color en cada atributo que toca. Sigue habiendo un hueco real, no un
  placeholder: sin run detrás (equipo de pruebas, rival de ojeo) no hay ningún dato de equipamiento que
  leer, así que la fila se queda en "sin objeto" —el texto de la clave no se ha tocado porque sigue
  siendo cierto, aunque ya no huela a fase 2 pendiente.
- **Salario**: la economía (RF-114g..k) y los mercenarios (RF-110..113) son de fase 2, y `PlayerDefinition`
  no tiene salario. La ficha reserva la fila y lo dice.
- **Club**: los clubes son datos de fase 2. **Con una run en curso** (`RunController`, `ui-run-minima.md`)
  la pantalla enseña la plantilla de la run y su club, y cada movimiento entra en el estado como
  `SetLineup`; sin run —al regenerar estas capturas, o al abrir la escena suelta— sigue valiendo la
  plantilla de pruebas de raza orca con semilla fija, y el subtítulo pone "club de pruebas".
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
8. **"Ver equipo" vuelve adonde estaba** (AW-N): Mercado, Recompensa e Informe ofrecen un botón "Ver
   equipo" que deja escrito su propio nombre de escena en `Nav.ReturnTo` antes de navegar a Equipo, y el
   botón de vuelta de Equipo (`AddBackButton`) lo consume una sola vez y lo limpia. Sin desvío escrito,
   Equipo sigue decidiendo con su lógica de siempre (Ojeo si hay un nodo elegido, si no el Mapa); con
   desvío, gana el desvío. Es la única regla de "adónde volver" y vive en `Nav.cs`, no repartida por cada
   pantalla que quiera ofrecer el botón.

## 13. Cómo se regeneran las capturas

`--headless` **no** sirve: usa el renderizador nulo, ejecuta la pantalla pero no dibuja nada y
`GetImage()` devuelve null. Hace falta un servidor X virtual y el renderizador de software:

```bash
dotnet build Game/Underleague.Game.csproj
godot --headless --path Game --import
xvfb-run -a --server-args="-screen 0 1280x800x24" \
  godot --path Game --rendering-driver opengl3 --audio-driver Dummy -- --screenshots
```

Deja en `Game/screenshots/` diez capturas: `equipo.png` (estado inicial), `equipo-zona.png` (un jugador
cogido, con sus dos capas y los vínculos que se crean y se rompen), `equipo-cobertura.png` (modo de
cobertura), `equipo-ficha.png` (ficha expandida con perk y descripción generada), `equipo-objeto.png`
(ficha expandida con un objeto equipado: nombre y descripción generados en la sección de objeto, delta
con signo y color junto a cada atributo que el objeto toca —verde el que sube, rojo el que baja— y la
media de atributos en la cabecera, AW-K y AW-M), `equipo-zonas.png` (modo de zonas de inicio),
`equipo-aviso.png` (el aviso de perks tras un movimiento), `equipo-suplente.png` (un suplente cogido
desde su ficha, con la misma pista visual que un titular cogido, listo para soltar sobre una casilla —
AW-L), `equipo-sustitucion.png` (el mismo suplente ya sustituyendo a un titular de campo) y
`equipo-zona-frase.png` (la ficha de un portador de perk de zona con sus dos frases de banda marcadas, el
tooltip nativo abierto sobre una de ellas y esa banda —y sólo ésa— teñida sobre la cuadrícula, AW-F).
Todas menos la primera se alcanzan **con eventos de mando sintéticos**, no llamando a los métodos por
dentro: la secuencia comprueba de paso que la navegación sin ratón lleva a los mismos estados. La
excepción es la última: un *hover* no es una acción de entrada que se pueda inyectar, así que ese paso
mueve el **puntero de verdad** (`Input.WarpMouse`) hasta el centro de la frase marcada —la ficha sabe
dónde cae— y espera 1,2 s a que salte el tooltip antes de disparar; el hover que se ve en la captura es
el hover real, no un manejador llamado a mano. El movimiento de `equipo-aviso.png` lo elige la propia
secuencia previsualizando alineaciones hipotéticas, para que el aviso de la captura sea un aviso de
verdad y no un campo mudo; la plantilla de pruebas no tiene ningún jugador equipado, así que
`equipo-objeto` fuerza un objeto maldito de verdad (`berserker_totem`) en un titular, igual que
`equipo-aviso` fuerza un perk cuando la plantilla no trae ninguno.

El render es por software (Mesa/llvmpipe), así que las capturas valen para juzgar composición, color,
proporción y legibilidad, **no** fluidez.

Ninguna de las diez enseña el bloque de **riesgo de muerte** (AW-G, §7): la plantilla de pruebas con la
que se regeneran no tiene run detrás, y ese bloque solo se muestra con una en curso. Se ve en
`equipo-run.png`, que genera el recorrido `--tour` de `docs/ui-run-minima.md` §"Capturas", no `--screenshots`.
