# BA-K — Cortinilla o transición

**Estado:** Resuelta (16 sep 2026, `Game/Ui/MatchPitchView3D.cs`, `Game/Ui/MatchPitchView.cs`).
Verificado con captura (Xvfb, ver "Verificación" abajo).

## Observación

**Cortinilla o transición** en todo lo que teletransporte jugadores: cambios, faltas, rojas, sustituciones

## Causa real

`/Sim` no teletransporta a nadie sin motivo (RT-020: el tick lógico es el entero; un salto real de
`ResetPositions` en un saque de centro, o de `LeavePitch` a `(-1,-1)`, es correcto ahí). El problema estaba
en el render: `Interpolate` (3D) y `PositionOf` (2D) mezclaban con `Lerp`, **sin excepción**, la posición
del tick actual con la del siguiente — así que un salto de varias casillas entre dos ticks (BB-A: 19
jugadores de golpe en el saque de centro; BB-L: alguien deslizándose hacia `(-1,-1)` antes de desaparecer)
se enseñaba como un deslizamiento rapidísimo, no como una reforma o una salida.

## Arreglo

En ambas vistas, antes de interpolar:

1. **Si el jugador no sigue en el campo en el tick siguiente**, no se interpola hacia él: se mantiene en
   `here` hasta que desaparece de golpe en el tick correcto (arregla BB-L).
2. **Si el salto entre los dos ticks supera 0,6 casillas** (misma cota que
   `Sim.Tests.Engine.MatchRulesTests.TheRestartTakerStandsStillDuringTheDeadBall` y
   `GoalCelebrationPositionTests`, no un número nuevo — una zancada real mide 0,13-0,21), no se interpola:
   se corta en el punto medio del tick (`Alpha < 0.5` enseña `here`, `Alpha >= 0.5` enseña `next`) en vez
   de deslizar (arregla el resto de BB-A).
3. **Solo en 3D** (el cuerpo tiene un material propio, la 2D dibuja con `DrawCircle`/`DrawArc` cada
   fotograma sin ese punto de enganche): el modelo se atenúa hasta 0,15 de opacidad justo en el instante
   del corte y recupera opacidad hacia los dos bordes del tick — la "cortinilla" en sí, para que el salto
   se lea como intencional y no como un fallo de interpolación. Misma idea, sentido inverso, cuando un
   jugador **entra** al campo en ese tick (sustitución): rampa de aparición en vez de un `Visible = true`
   seco.

## Lo que NO cubre este arreglo

- **La cortinilla de opacidad es solo 3D.** La vista 2D corta el salto de posición (punto 1 y 2 arriba,
  verificado) pero no atenúa nada: dibuja con color sólido en cada fotograma y añadir un desvanecido ahí
  exigiría pasar un multiplicador de alfa por cada llamada de `DrawCircle`/`DrawArc`/`DrawText` de
  `DrawToken`, un cambio más invasivo que no se ha hecho en este ciclo.
- **La rampa de aparición al entrar en 3D no se ha verificado con captura**, solo el corte de salida (el
  partido de la semilla fija de `CaptureRunner` no tuvo ninguna sustitución en la ventana capturada).
  Mecanismo idéntico y ya verificado al de salida, pero la etiqueta correcta (Regla F) es LIKELY, no
  CONFIRMED, hasta que se vea en un caso real.
- **No toca `/Sim` ni RT-020**: el salto sigue siendo real y ocurre en un tick exacto; lo único que cambia
  es cómo se enseña entre dos ticks consecutivos.

## Verificación

Capturas (Xvfb, `Sim.Tests`-style: se ejecutó `dotnet build Game/Underleague.Game.csproj` y
`xvfb-run ... godot --path Game --scene res://Scenes/Capturas.tscn`, con un diagnóstico temporal —borrado
después— que forzaba `MatchScreen._frame`/`_carry`/`_playing` por reflexión con `Engine.TimeScale` casi a
cero, porque el propio `_Process` de `MatchScreen` pisa `pitch3d.Frame`/`Alpha` cada fotograma con su
reloj real y una asignación externa sin congelar el reloj no se sostiene ni un fotograma):

- **Antes del corte (`Alpha=0,1`)**: la masa de jugadores apiñada en juego abierto, tal como estaba justo
  antes del saque de centro.
- **En el corte y después (`Alpha=0,5` a `1,0`)**: la formación en línea del saque de centro, ya reformada,
  **sin ningún fotograma intermedio deslizante** entre las dos — exactamente el salto discreto que pedía
  el ticket, no una interpolación entre una masa de jugadores y una línea (que habría producido posiciones
  sin sentido a mitad de camino).
- **Atenuación visible**: los cuerpos en `Alpha=0,5` se ven claramente más oscuros/apagados que en
  `Alpha=0,7`, confirmando que la cortinilla de opacidad se aplica donde toca.

`dotnet build Underleague.slnx` y `dotnet build Game/Underleague.Game.csproj` limpios. No se ha tocado
`/Sim`: sin puertas, sin lote de `/Balance`, no aplica RT-054.

## Hermanos

Mismo hilo de causa (el reposicionamiento se resuelve por teletransporte en vez de por transición):
[BA-D](./BA-D.md) (cerrada — el sacador camina en vez de saltar, arreglo de `/Sim` distinto y anterior),
[BB-A](./BB-A.md), [BB-L](./BB-L.md).
