# BB-J — «Mentalidad de manada» no sirve jugando con enanos

**Estado:** Previsibilidad RESUELTA (19 sep 2026, tooltip). La decisión de diseño sobre `pack_mentality` sigue ABIERTA — ver abajo

## Observación

**«Mentalidad de manada» no sirve jugando con enanos**, porque no habrá brutos. «Igual hay que tener en cuenta los estilos más que la raza»

## Análisis / estado actual

**Confirmado, y con número.** El perk ya usa **estilo**, no raza (`teammatesWithTag(owner,'Brute') > 2`), pero los enanos son **75 % Bulwark y solo 8 % Brute**: juntar tres brutos es casi imposible. La solución elegante es que cuente **la etiqueta del propio portador** («cuantos más como yo»), que es lo que dice la ficha de Manada del catálogo — pero **hoy no existe** esa función de condición: `teammatesWithTag` exige etiqueta literal

## Hermanos

_(por enlazar donde se detecten; ver `README.md` del directorio)_


## Implementación de la primitiva (16 sep 2026, orquestación de pendientes técnicos)

**Confirmado antes de tocar nada** cómo se evalúan las condiciones: `ConditionCompiler` compila cada
función una vez al cargar, y sus argumentos de tipo `Tag` son **literales de cadena fijados en el JSON**
(`ConditionArgKind.Tag`), no expresiones que se puedan anidar en tiempo de partido. No existía una
abstracción reutilizable para "cuenta lo mío" sin fijar una etiqueta de antemano.

**Añadida `teammatesWithSameStyle(who)`**: cuenta compañeros en campo con el mismo `StyleTag` que `who`,
resuelto en tiempo de partido a partir del propio jugador — sin etiqueta literal. Es una primitiva
genérica, no una excepción de Manada: cualquier perk futuro de composición puede usarla.

- `Sim/Perks/ConditionContext.cs` (interfaz `IPerkWorld`), `Sim/Engine/MatchEngine.cs` (implementación,
  comparación directa de `StyleTag`), `Sim/Perks/ConditionCompiler.cs` (función nueva, argumento único
  `Who`).
- Plantillas es/en en `data/l10n/{es,en}/templates.json` (RT-035: seis comparadores, como
  `teammatesWithTag`).
- `Sim.Tests/Perks/TeammatesWithSameStyleTests.cs`: cuenta correcta excluyendo al propio portador, y el
  caso que motivó el problema —un equipo entero del mismo estilo (enanos Bulwark) cuenta sobrado, donde
  `teammatesWithTag(owner,'Brute')` se quedaría casi siempre en cero—.

Suite completa: 757/757 en verde. `DataValidator`: 233 ficheros sin errores.

## Por qué `pack_mentality.json` NO se ha tocado — límite arquitectónico real, no un olvido

El **efecto** del perk apunta a `target: "withTag:Brute"` — un objetivo compilado **una sola vez, con la
etiqueta fija en el dato** (`PerkLoader.cs`, `EffectTarget.WithTag`), igual que la condición. Cambiar solo
la condición a `teammatesWithSameStyle(owner) > 2` dejaría un perk que **se dispara** con cualquier
composición pero **solo beneficia a los Brute** del equipo — que pueden no existir. Sería el mismo error
que motivó todo el paquete de organización: un test en verde, una mecánica equivocada, un perk que se
activa y no hace nada visible.

Arreglarlo de verdad exigiría una **segunda primitiva** —un objetivo de efecto que siga dinámicamente el
estilo del portador (`withTag:<propio estilo>`), no una etiqueta fija—, y esa primitiva hoy **no tiene los
tres consumidores** que exige PD-6 para justificarse. Es una decisión de diseño (¿vale la pena la
primitiva por un solo perk, o se rediseña `pack_mentality` de otra forma —por ejemplo, que el bonus lo
reciba el propio grupo contado, no una etiqueta fija—?), no un bug de implementación. Se detiene aquí.

## Hermanos

Ninguno detectado. La primitiva queda disponible para cualquier perk de composición futuro.


## 19 sep 2026 — se resuelve la previsibilidad, no el perk

El síntoma que abrió la ficha era *«no sirve jugando con enanos»*. Hay dos problemas dentro, y solo se
cierra uno.

**Lo que se arregla: el jugador ya no se entera después.** La ficha de jugador (Equipo, Ojeo, Fin de run)
y la del Mercado muestran ahora, bajo cada perk que cuente una etiqueta, cuántos lleva y cuántos necesita:

> `Bruto: 0 de 3 en la plantilla`

Eso es RF-012d literal —lo malo se sabe antes, con la información previa— y encaja con el planteamiento
del revisor: *un perk de rasgo **debe** rendir distinto según la plantilla; ahí entra la mano del jugador
que sabe armar una build. Lo que no puede es enterarse en el informe post-partido.*

- `Sim/Perks/PerkSquadRequirements.cs` (nuevo) y `LineupPerkRequirement` en `LineupPerkPreview.cs`.
- `Game/Ui/PlayerCard.cs`, `Game/Screens/MarketScreen.cs`, `Game/Ui/UiText.cs`.

**Lo que NO se arregla, y sigue siendo decisión de diseño.** `pack_mentality` sigue sin servir con
enanos. El bloqueo que esta ficha ya documentaba —el efecto apunta a `withTag:Brute`, etiqueta fija en el
dato, así que cambiar solo la condición daría un perk que se dispara y no beneficia a nadie— **no se ha
tocado**. El tooltip no lo esquiva: lo hace visible.

**Contexto de diseño que el revisor aportó y conviene no perder** (`docs/analisis/perks-condiciones-y-poblacion.md`):
de los 94 perks, **73 son universales, 12 de rasgo y 9 de raza**, que es justo el reparto pretendido. Los
9 de raza **declaran** su restricción; de los 12 de rasgo, solo 1 lo hace. Y **no hay que declararla en
los otros 11**: eso los convertiría en perks de raza, lo contrario de lo que el nivel 2 pretende. Lo que
faltaba no era una restricción — era información.

**Cobertura:** 8 de los 12 de rasgo. Los otros 4 (`back_to_back`, `shadow_marker`, `crowd_control`,
`cold_focus`) dependen de dónde estén los jugadores durante la jugada, no de la plantilla, y ahí el
previsualizador calla a propósito: un conteo de plantilla sería una pista honesta pero **no es la
condición**.

**Verificación visual, con su límite:** la ficha de Equipo se capturó y se revisó; la línea nueva no
aparece porque la plantilla de demo no lleva ningún perk de conteo, así que el texto está comprobado por
test y no por captura. Del Mercado no hay captura: la secuencia de `Capturas.tscn` se detiene tras
`informe.png` por **BA-L2**, abierta y anterior a este cambio.
