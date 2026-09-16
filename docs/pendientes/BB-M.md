# BB-M — «Sed de médula» lesionó a un jugador lejos de la acción

**Estado:** Resuelta. Causa **CONFIRMED**, sin cambio de regla pendiente.

## Observación

«Me ha lesionado al jugador número 7 (delantero) y he metido el jugador número 8. Al rebobinar para ver
dónde estaba el jugador 7 en ese momento, ya no veo al jugador 7 sino al 8 [...] "Sed de médula" ha
lesionado a un jugador que estaba lejos de la acción, sin ningún rival cerca. Este perk no debería
funcionar así, solo debería lesionar a un jugador contiguo.»

## Hipótesis

1. **H1 — el perk selecciona mal el objetivo** (falla el motor). *(REJECTED.)*
2. **H2 — el motor selecciona bien, pero el replay/render muestra otro jugador** (falla la reproducción,
   no la regla). *(CONFIRMED.)*

Instrumentado como pidió el revisor **antes de tocar ninguna regla**: activador, víctima, posiciones y
distancia, medido sobre el motor y no sobre lo que pinta la pantalla.

## Experimento

`Sim.Tests/Perks/InjuryProximityTests.cs`, 150 partidos: **cero lesiones sin causante, y todos los
causantes a 0,6-1,2 casillas de la víctima** en el tick **anterior** al de la lesión. El motor está bien.

## Causa real — CONFIRMED

`ResolveInjury` llama a `LeavePitch`, que pone la posición del lesionado en **(-1,-1)**. En el tick de la
lesión el jugador ya no está donde se lesionó: ha desaparecido del campo. Esa es la explicación del
espejismo, no una selección de objetivo equivocada. **No se toca la ADR 0048.**

## Corrección a un veredicto propio anterior

Se había escrito «confirmado y es por diseño» confundiendo la vía **letal** (recorre el equipo rival
entero eligiendo al que peor lo tiene, produce MUERTE — en 60 semillas no se disparó ni una vez) con la vía
de **lesión** (exige contacto). Queda registrado como lección de `gameplay-debug`: no declarar una causa
CONFIRMED sin haber medido la vía de código exacta que produjo el evento observado.

## Hermanos

**BB-L** («el lesionado sale volando del campo») es la misma causa (`LeavePitch` → (-1,-1)) vista desde el
render en vez de desde el replay. Un arreglo de presentación en BB-L (retención + interpolación, ADR 0114)
resolvería la lectura visual de las dos a la vez.
