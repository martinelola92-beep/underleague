# BB-L — El lesionado «sale volando» del campo

**Estado:** Resuelta (16 sep 2026, BA-K, `Game/Ui/MatchPitchView3D.cs` y `Game/Ui/MatchPitchView.cs`).

## Observación

**El lesionado «sale volando» del campo** (se teletransporta fuera)

## Causa real

No era literal: nadie "vuela", pero el render sí interpolaba hacia una posición inválida. `Interpolate`
(3D)/`PositionOf` (2D) mezclaban con `Lerp` la posición del tick actual con la del **siguiente**, sin
comprobar si el jugador seguía en el campo en ese siguiente tick. Cuando un jugador se lesiona y
`LeavePitch` lo manda a `(-1,-1)` (`OnPitchAt` pasa a falso en el tick siguiente), el fotograma justo
antes de que desaparezca ya estaba deslizando su posición **hacia** `(-1,-1)` — de ahí que pareciera salir
volando en esa dirección en vez de simplemente desaparecer donde cayó.

## Arreglo (BA-K, mismo cambio que BB-A)

Si el jugador no sigue en el campo en el tick siguiente (`!trace.OnPitchAt(frame + 1, player)`), la
interpolación ya no mezcla hacia `next`: se queda en `here` hasta que el jugador desaparece de golpe en el
tick correcto, sin deslizamiento previo. Ver `docs/pendientes/BA-K.md` para el detalle completo (incluida
la verificación visual) y `docs/pendientes/BB-A.md` para el caso hermano (saque de centro).

## Hermanos

Mismo hilo de causa (el reposicionamiento se resuelve por teletransporte en vez de por transición): [BB-A](./BB-A.md), [BB-C](./BB-C.md), [BB-D](./BB-D.md), [BB-M](./BB-M.md).
