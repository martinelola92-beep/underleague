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

---

## Implementación, primera pasada (25 sep 2026)

### Los tres puntos que la ficha exigía antes de tocar código, resueltos

1. **Cómo se ancla hoy** (`MatchPitchView3D:1489-1535`): el balón se dibuja en `BallAt(frame)` —el centro
   del aro lógico— más un **offset 2D plano**, `radio × 0,55` en la dirección de carrera, con la altura de
   `BallHeightAt`. **Un único ancla para todos los contactos**: eso es exactamente el síntoma.
2. **Qué hay en los modelos**: 65 huesos `mixamorig_*` verificados en el FBX (el importador cambia `:` por
   `_`), con `Head`, `LeftToeBase`/`RightToeBase`, `LeftHand`/`RightHand`, `Spine2`. Y **7 de los 15 clips
   cargados no se pedían nunca**: `receive`, `header`, `throwin`, `penalty`, `gk_save`, `gk_catch`,
   `standup`.
3. **La antelación** —el punto que decidía si esto era posible— **sobra**: `PassingTicks: 5` y
   `ShootingTicks: 5`, o sea **cinco ticks armando** el golpeo en estado `Passing`/`Shooting` y
   conservando el balón (0,33 s ≈ 20 fotogramas a 60 fps). Y la traza **está entera en memoria** cuando se
   reproduce: la vista puede mirar `BallOwnerAt(frame ± k)` sin predecir nada, porque lee un resultado ya
   calculado.

### Lo que dijo `architecture-review`

- **No hace falta abstracción nueva.** El patrón hermano ya existe: `PlayerModel.Pose(velocity, state)` —la
  vista pasa lo que dice la traza, el modelo decide sobre su esqueleto—. El ancla es **el método
  simétrico**, `PlayerModel.TryContactPoint(part, towards, out world)`, y la tabla contacto→hueso vive
  junto a la tabla clip→fichero, que es donde ya estaba el conocimiento del pack.
- **Elimina complejidad, no la mueve**: `radio × 0,55` en dirección de carrera era un **sustituto
  inventado** del pie. Con el hueso el dato es real. El offset se queda solo como respaldo.
- **Faltaba una pieza de frontera a medias**: la vista 3D recibía `Bind(trace, setup, catalog)` pero **no
  los eventos**, y RT-014 dice literalmente que *el render consume eventos*. `MatchScreen` ya los tenía
  (`Playback.Result.Events`, los usa `MatchFlashView`) y no se los pasaba. Sin ellos no se distingue una
  parada de un despeje: **`/Sim` no tiene estados de recibir, rematar ni parar** — el contacto vive en el
  evento, no en `PlayerState`.
- **Orden de implementación, y es diseño**: primero la animación, después el ancla. El ancla lee el hueso
  *de la animación que se está reproduciendo*; anclar a la cabeza mientras el modelo corre se ve **peor**.

### Qué se ha hecho

- `ContactCue` (gesto) y `ContactPart` (parte del cuerpo) en `PlayerModel`, con la tabla de huesos
  verificada. Cada parte lleva **dos huesos, izquierdo y derecho**, y se elige el **más cercano al balón**:
  es lo único que hace falta para que el golpeo salga del pie que toca, y cuesta una comparación de
  distancias en vez de un sistema de IK.
- `TryContactPoint` lee el `Skeleton3D` **ya animado** (`GetBoneGlobalPose` compuesto con la transformada
  del esqueleto), así que el punto es coherente con el clip en curso **sin anotar nada en los clips**.
- La vista deduce el gesto de los **eventos del tramo del fotograma** (`Save` → `gk_save`, `AerialDuel` →
  `header`, `Clearance` alto → `header`) y la recepción de la traza (el fotograma en que el balón pasa a
  tener dueño viniendo en vuelo).
- La parte del cuerpo sale de `BallHeightAt`: **portero → manos**, por encima de 0,75 casillas → cabeza,
  entre 0,45 y 0,75 → pecho, el resto → pie.
- **Válvula de seguridad**: si el hueso queda a más de **0,5 casillas** del centro del portador, se
  descarta y se vuelve al offset. Una estirada o un brazo en alto no pueden llevarse la pelota media
  casilla. Y el ancla **solo se aplica con dueño**, que es cuando la traza ya pone el balón encima del
  jugador y la vista ya lo apartaba: la posición de `/Sim` sigue mandando (RT-014).

### Cómo se verifica, y no es mirando

Se extiende el instrumento que ya existía (`BroadcastCapture`, *«medir, no mirar»*, que compara la
separación balón-jugador contra un objetivo de 0,175 casillas) con `DebugContacts()`: cuántos
fotogramas-jugador de **todo el partido** piden cada gesto y cada parte del cuerpo. **Un gesto que sale
cero veces es código muerto, y eso una captura no lo enseña.**

### Lo que midió la verificación, y lo que destapó

`DebugContacts()` volcó, sobre un partido entero del club humano (1.947 fotogramas, 303 eventos):

| pasada | gestos | ancla con dueño |
|---|---|---|
| primera | **NINGUNO** | `Feet` 433 · `Hands` 59 |
| tras arreglar la guarda | `Receive` 36 | igual |
| tras cablear los eventos | **`Header` 2 · `Receive` 36 · `Save` 1** | igual |

**El instrumento pagó su coste en la primera línea.** «Gestos: NINGUNO» en un partido entero: los siete
clips estaban enganchados y no se disparaba ninguno. Una captura no lo habría enseñado —los modelos ocupan
unos pocos píxeles a esa distancia de cámara— y el paquete se habría entregado como si funcionara. Tres
defectos, los tres míos:

1. **La guarda de `_events is null` cortaba también la recepción**, que no necesita eventos. Estaba
   colocada al principio de la función y hacía `return` antes de llegar a esa rama.
2. **`MatchEvent.Actor` es el `Id` del jugador** (`actor.Id`, verificado en `MatchEngine:5377`), **no su
   índice en la traza**, y yo comparaba contra el índice. Los dos números no tienen nada que ver: en la
   puerta de builds un equipo arranca en el id 100001.
3. **Había DOS pantallas que montan la vista 3D** —`MatchScreen` y `BroadcastScreen`— y sólo cablé los
   eventos en una. La captura usa la otra.

Y un cuarto, de diseño, que salió al ver `Header 2`: **un gesto nacido de un evento dura un tick**, así que
el fotograma siguiente el `switch` de estado llamaba a `Switch("jog")` y **cortaba la animación** — se
lanzaba el remate y al instante volvía a correr. El disparo y el pase no lo sufren porque `Passing` y
`Shooting` duran cinco ticks. Se arregla con `_holdingCue`, el mismo patrón que ya encadena `trip` con
`fallen`: lo manda el reloj de la propia animación, no un temporizador aparte. Solo lo interrumpe irse al
suelo.

**La separación del balón al pie**: de `radio × 0,55` (un valor inventado) a **0,1 casillas** medidas
contra el hueso, con el objetivo de la captura en ~0,175. Y el ancla se reparte `Feet` 433 · `Hands` 59
fotogramas, que es exactamente lo esperable: con dueño el balón va al pie, salvo el portero.

**Lo que la captura NO demuestra**: a la distancia de cámara de `retrans-modelos-conduccion.png` los
modelos ocupan unos pocos píxeles y el balón no se distingue. La imagen sirve para ver que nada se rompió,
no para juzgar el ancla. Lo que sostiene el resultado es la medición.

### Lo que queda, y por qué

1. **Los saques no se pueden enganchar todavía, y es una carencia de `/Sim`.** `MatchPhase` sólo tiene un
   **`Restart` genérico**: la traza no lleva el tipo de reanudación, así que desde `/Game` no hay forma de
   distinguir un saque de banda de uno de puerta. El clip `throwin` sigue cargado y sin usar. Para
   cubrirlo hace falta que `/Sim` exponga el tipo de reanudación —un campo en la traza o un evento—, que
   es cambio de `/Sim` y va en su propio commit (un commit no mezcla `/Sim` y `/Game`). **El penalti sí**
   se enganchó: `MatchPhase.Penalty` es fase propia.
2. **`gk_catch` sigue sin disparar**: no hay evento que distinga una parada atrapada de una despejada.
   Mismo bloqueo que los saques.
3. **El ancla solo actúa con dueño.** El cabezazo y la parada mejoran por la **animación**, no por la
   posición del balón — moverlo cuando `/Sim` dice que no es de nadie es justo lo que el encargo prohíbe.
   Si al verlo en movimiento se queda corto, el paso siguiente es atraerlo al hueso durante los pocos
   fotogramas del contacto, con la misma válvula de 0,5 casillas.
4. **Falta verlo en movimiento.** La medición dice que se dispara y dónde ancla; si el remate se lee como
   remate es una pregunta que solo contesta el revisor mirando la build.

**Nota de honestidad sobre el penalti**: `ContactCue.Penalty` está enganchado a `MatchPhase.Penalty`, pero
el partido de referencia con el que se mide **no tiene ninguno**, así que sale 0 en el volcado. Queda como
**sin evidencia de activación** —mecanismo real, nunca observado disparándose—, que no es lo mismo que
«no funciona» ni que «funciona».
