# BI-H — El balón no parece jugado con los pies: la interacción va pegada al aro lógico

Estado: **ABIERTA, encargo del revisor en cola** (24 sep 2026; alcance ampliado dos veces, la segunda el
25 sep 2026: **todo contacto con el balón**, y **no sólo con los pies** — pies, cuerpo, cabeza y manos).
Reportada viendo la build.
**No empezar hasta cerrar el paquete de balón parado posicional** (`docs/plan-balon-parado-posicional.md`).

## El encargo, textual

> *«Quiero que mejores la interacción visual entre jugadores y balón.*
>
> *Ahora mismo la interacción parece demasiado ligada al aro/collider lógico del jugador: cuando recibe o
> mueve el balón, este puede cambiar de lado de forma poco natural y no transmite la sensación de que el
> jugador lo toca realmente con los pies.*
>
> *Quiero una solución simple y pragmática, no un sistema FIFA/PES ni una simulación física compleja.
> Mantén el collider/posición lógica actual para gameplay, pero introduce una capa sencilla de interacción
> basada en los pies/animación para que:*
>
> - *Las recepciones tengan en cuenta de qué lado llega el balón.*
> - *El balón no se teletransporte simplemente al lado/opuesto del jugador.*
> - *Los movimientos y controles transmitan mejor que el jugador está jugando con los pies.*
> - *Aproveche las animaciones de Mixamo que ya tenemos.*
> - *Sea robusto con las animaciones actuales y fácil de ampliar posteriormente.*
>
> *Prioriza mucho la mejora perceptual frente a la precisión física y evita rehacer sistemas existentes si
> no es necesario.*
>
> *Antes de implementar, revisa cómo está estructurada actualmente la interacción jugador-balón y propón la
> solución mínima que consiga una mejora visible.»*

## Alcance ampliado (2): todo contacto con el balón (24 sep 2026)

> *«Sobre la pelota BI-H debe aplicarse también a tiro, parada, saque… Todo lo que implique contacto con
> balón. Hay que mejorarlo un poco.»*

O sea que no es sólo la recepción y la conducción: **todo contacto**. La lista, para que nadie la reduzca a
lo fácil: recepción, control, conducción, pase, **tiro**, **parada del portero**, despeje, cabezazo y los
**saques** (banda, puerta, córner, falta, centro). Y el listón lo fija él: *«un poco»* — mejora perceptual
visible, no un sistema.

## Alcance ampliado (3): el contacto no es sólo con los pies (25 sep 2026)

> *«Añade al alcance de BI-H que la interacción visual no debe limitarse a los pies. Quiero que la solución
> tenga en cuenta, cuando corresponda al tipo de contacto, pies, cuerpo y cabeza. El objetivo es que pases,
> controles, despejes, tiros, cabezazos, paradas, etc. parezcan originarse visualmente en la parte del
> jugador que corresponde, en lugar de seguir dependiendo exclusivamente del aro lógico.*
>
> *Mantén exactamente el mismo principio: solución mínima y pragmática, presentación pura en `/Game`, sin
> modificar la posición real del balón en `/Sim` ni introducir física compleja.*
>
> *No hace falta que cada contacto sea físicamente preciso. Buscamos que visualmente el balón parezca
> interactuar con el jugador de forma natural y coherente con la animación que se está reproduciendo.*
>
> *Al revisar la implementación, identifica qué puntos del cuerpo y qué información de las animaciones
> existentes pueden aprovecharse con el menor coste posible. Prioriza claramente la mejora perceptual sobre
> la precisión.»*

Esto **no** cambia el principio del encargo, cambia el **catálogo de anclajes**: donde antes se leía «los
pies» ahora se lee «la parte del cuerpo que toca, según el tipo de contacto». El listón sigue siendo el
mismo (*«un poco»*, mejora perceptual visible) y la frontera sigue siendo la misma: **la posición del balón
la decide `/Sim`** y esto es un desplazamiento de dibujo (RT-014).

Consecuencia de diseño, y es la que ahorra trabajo: **el anclaje no puede ser una constante**. Un único
offset «a los pies» no sirve para un cabezazo ni para una parada, y una tabla de casos especiales por
evento tampoco es la solución mínima. Lo que se pide es **una función de tipo de contacto → punto del
cuerpo**, con un anclaje por defecto, que es una tabla de datos y no una rama por caso.

### Puntos del cuerpo por tipo de contacto, y de dónde sale ya la información

La columna «hueso» son nombres **verificados en el propio FBX** del personaje (`Game/models/soccer/X Bot.fbx`
trae los 65 huesos `mixamorig:*`; el importador de Godot sustituye el `:` por `_`, que es la forma que ya usa
`PlayerModel.RootBone` = `mixamorig_Hips`). Son **candidatos**, no una decisión tomada.

| Contacto | Debe parecer que toca con | Hueso candidato | Clip que ya existe | Con qué se distingue hoy |
|---|---|---|---|---|
| Recepción, control | pie (o pecho si llega alto) | `LeftFoot`/`RightFoot`, `Spine2` | `receive` | `BallOwnerAt` cambia + `BallHeightAt` |
| Conducción | pie, alternando | `LeftToeBase`/`RightToeBase` | `jog`/`run` | `PlayerState.Dribbling` |
| Pase, centro | pie | `LeftFoot`/`RightFoot` | `kick` | `PassAttempted`, `Cross`, `PlayerState.Passing` |
| Tiro, penalti | pie | `LeftFoot`/`RightFoot` | `kick`, `penalty` | `Shot`, `PlayerState.Shooting` |
| Cabezazo, duelo aéreo | cabeza | `Head` | `header` | `AerialDuel` + `BallHeightAt` alto |
| Despeje | pie o cabeza, según altura | según `BallHeightAt` | `kick` / `header` | `Clearance` + `BallHeightAt` |
| Parada del portero | manos | `LeftHand`/`RightHand` | `gk_save`, `gk_catch` | `Save`, `ShotBlocked` |
| Entrada | pie adelantado | `LeftFoot`/`RightFoot` | `tackle` | `PlayerState.Tackling` |
| Saques (banda) | manos | `LeftHand`/`RightHand` | `throwin` | fase de reanudación (ADR 0147) |
| Saques (puerta, córner, falta) | pie | `LeftFoot`/`RightFoot` | `kick` | fase de reanudación (ADR 0147) |
| Protección del balón | cuerpo | `Spine1`/`Spine2` | — | `PlayerState.Shielding` |

**Lo más barato que hay disponible, y hay que aprovecharlo antes de inventar nada** (CONFIRMED, leído en el
código):

- **`MatchTrace.BallHeightAt(frame)` existe** (ADR 0135). Es el discriminador de «pie o cabeza» y no cuesta
  nada: no hace falta un estado nuevo en `/Sim` para saber que un despeje fue de cabeza.
- **De los 15 clips cargados, 7 nunca se piden**: `header`, `receive`, `throwin`, `penalty`, `gk_save`,
  `gk_catch` y `standup` aparecen una sola vez en `PlayerModel.cs` — en la tabla `Clips` — y el `switch` de
  `Pose()` no los usa jamás. El pase y el tiro **comparten `kick`**. Es decir: una parte de la mejora
  perceptual ya está pagada y descargada, y sólo le falta el enganche. Empezar por ahí es lo más rentable
  que tiene este encargo.
- **`PlayerState` no tiene `Receiving`, `Heading` ni `Saving`**: el estado por jugador no basta para elegir
  la parte del cuerpo. El tipo de contacto sale de **los eventos del fotograma** (`Save`, `Clearance`,
  `AerialDuel`, `Shot`, `Cross`, `PassAttempted`) cruzados con la altura del balón. Añadir estados a `/Sim`
  para esto sería exactamente el error que el encargo prohíbe.
- **`MatchTrace.ActionAt(frame, player)` da la acción decidida**, no la consumada: es la única fuente de
  **anticipación** que hay, y el punto 3 de la sección anterior sigue siendo el que decide si la capa llega
  a tiempo o siempre parece tarde.
- El mecanismo más barato para leer el punto del cuerpo **ya animado** es el propio esqueleto:
  `Skeleton3D.GetBoneGlobalPose` (o un `BoneAttachment3D` por jugador) sobre el hueso de la tabla. No hay
  que anotar nada en los clips ni añadir marcadores: la postura ya está calculada ese fotograma, que es
  literalmente *«coherente con la animación que se está reproduciendo»*.

### Dos riesgos que hay que anotar antes de implementar

1. **El hueso lo mueve el clip, y los clips de Mixamo venían con desplazamiento horneado en el raíz**
   ([BI-C](./BI-C.md), cerrada: se fija el horizontal al cargar y se conserva el vertical). Cualquier offset
   que se lea del esqueleto hay que leerlo **en el espacio del modelo** y no dar por hecho que el mundo
   coincide, o se recupera aquel 1,53 casillas por otra puerta.
2. **La presentación no puede contradecir al evento.** Si `/Sim` dice gol, el balón acaba en la portería
   aunque la cabeza del remate no llegue a tocarlo en pantalla: el desplazamiento visual va **acotado** y se
   resuelve siempre a favor de `/Sim`. Un anclaje que «espera» al hueso es una capa que decide, y eso ya no
   es presentación.

### Cómo se demuestra que está mejor

No hay métrica: el criterio es perceptual, así que **`visual-review` es obligatorio y por tipo de contacto**
— capturas de antes y después de, como mínimo, recepción, conducción, tiro, cabezazo, parada y un saque de
banda. Una captura de «se ve mejor» en general no vale para un encargo cuyo alcance es precisamente que
cada contacto se vea en su sitio.

## Lo que ya se sabe antes de empezar, y acota el trabajo

**Esto es `/Game`, no `/Sim`, y la frontera aquí no es negociable.** La posición del balón la decide el
motor y el render **sólo consume eventos** (RT-014): la capa que se pide es **presentación pura**, una
interpolación entre lo que dice `/Sim` y dónde se dibuja la pelota. Si en algún momento la capa visual
tuviera que *cambiar* dónde está el balón para el gameplay, el diseño está mal — es el fallo que
`architecture-review` existe para cazar.

Eso además hace el encargo **más fácil**, no más difícil: no hay que tocar `MatchEngine` ni arriesgar el
determinismo (RT-021/RT-024). Es un offset de render.

**Hermanos que ya están medidos y hay que leer antes:**

- [BI-C](./BI-C.md) — **cerrada**: los clips de Mixamo traían el desplazamiento horneado en el hueso raíz
  (corriendo se salía 1,53 casillas). Se fija el horizontal al cargar y se conserva el vertical. Quien toque
  animación aquí tiene que saber esto o volverá a pelearse con ello.
- [BI-D](./BI-D.md) — **abierta**: el balón tiene dueño el **32,3 %** del partido, con posesiones de
  **0,33 s** de media. Es directamente relevante: *«el jugador juega con los pies»* se aprecia sobre todo
  **conduciendo**, y hoy casi no se conduce. El revisor ya aprobó alargar la conducción, así que **BI-D va
  antes o a la vez**: hacer la capa visual sobre posesiones de un tercio de segundo es pulir algo que casi
  no se ve.
- **ADR 0137** (conducir es un compromiso) y **ADR 0135** (altura del balón) son el contexto.

## Lo primero que hay que hacer, y NO es escribir código

El propio encargo lo dice: *«revisa cómo está estructurada actualmente la interacción jugador-balón y
propón la solución mínima»*. Antes de tocar nada:

1. Dónde se dibuja hoy el balón en `/Game` y contra qué se ancla.
2. Qué huesos y qué clips hay disponibles en los modelos de Mixamo ya integrados.
3. Qué eventos de `/Sim` marcan un contacto (recepción, pase, control, conducción) y con cuánta antelación
   llegan — porque una capa visual que reacciona **después** del toque siempre parecerá tarde.

Ese punto 3 es el que decide si la solución mínima es posible: si el render sabe con un tick de antelación
que va a haber contacto, puede anticiparlo; si se entera en el mismo fotograma, no.

## Disparadores de skill que aplican

`architecture-review` (frontera `/Sim`-`/Game`, abstracción nueva de presentación) y **`visual-review`**
obligatoriamente antes de decir que se ve mejor — con capturas del antes y del después, porque el criterio
de éxito de este encargo es **perceptual** y no hay métrica que lo mida.
