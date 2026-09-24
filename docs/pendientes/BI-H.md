# BI-H — El balón no parece jugado con los pies: la interacción va pegada al aro lógico

Estado: **ABIERTA, encargo del revisor en cola** (24 sep 2026). Reportada viendo la build.
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

## Alcance ampliado por el revisor (24 sep 2026)

> *«Sobre la pelota BI-H debe aplicarse también a tiro, parada, saque… Todo lo que implique contacto con
> balón. Hay que mejorarlo un poco.»*

O sea que no es sólo la recepción y la conducción: **todo contacto**. La lista, para que nadie la reduzca a
lo fácil: recepción, control, conducción, pase, **tiro**, **parada del portero**, despeje, cabezazo y los
**saques** (banda, puerta, córner, falta, centro). Y el listón lo fija él: *«un poco»* — mejora perceptual
visible, no un sistema.

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
