# BX-8d — `EvaluatePress` premia apretar al portero dentro de su área, que el motor cierra

Hermano de [BX-8c](./BX-8c.md), señalado por su segunda revisión independiente (11 oct). Es el tercer lector del área
del portero:

- `EvaluatePress` suma `pressGoalkeeperExitBonus` (200, `data/ai/weights.json`) si el portador es el portero y está en
  su área. Mira la posición del portador, no la del balón, aunque con balón en los pies es la misma.
- Su comentario (ADR 0022) lo justifica como «el momento en que la presión gana el balón en zona de gol».
- Desde la ADR 0152 esa área está **cerrada** mientras el portero tiene el balón (`ClosedKeeperArea`):
  - `Move` lleva al borde a quien tenga el destino dentro;
  - quien aprieta no puede llegar al portero.

## Observación

Sale de la lectura del código, no de la retransmisión. El síntoma esperable: uno o dos rivales plantados en el borde
del área mientras el portero sostiene el balón, en vez de marcar a quien va a recibir el saque.

## Hipótesis

- **H0, activación.**
  - **Sin el grito «¡A por él!»: sin evidencia de activación.**
    - El bono hace ganar `PressCarrier` en 81 de 10 351 fotogramas con el portero en su área y el balón en los pies,
      en 200 partidos (`TestMatches.Reference`, semillas 1-200).
    - `PressCarrier` casi no se elige en ningún sitio: en juego abierto la sonda ve 0,00 rivales apretando por
      fotograma, lo mismo que midió [BX-13](./BX-13.md).
  - **Con el grito: CONFIRMED.**
    - El grito (ADR 0166, `press.PressCarrier` = 250) se forzó en los dos equipos todo el partido con un cambio
      desechable en `MatchEngine.Run`, ya revertido.
    - Rivales apretando al portero por fotograma: 0,02 sin el bono → 0,46 con él. Son delanteros en un 89 %.
    - El 43 % de los fotogramas tiene a alguien apretando; el 2 % tiene a tres o más.
- **H1, el bono le sirve al que aprieta**, que es la intención de la ADR 0022. **REJECTED bajo la ADR 0152:** no hay
  diferencia en la conservación del balón.
  - Medido en 400 partidos con el grito forzado, por posesión del portero: ¿el siguiente en tocar el balón es de su
    equipo?
  - Sin el bono, 1 023 de 1 621 (63,1 %); con él, 1 026 de 1 618 (63,4 %).
  - Las dos tandas por separado cambian de signo: semillas 1-100, 59 → 64 %; semillas 101-400, 65 → 63 %.
- **H2, el bono perjudica al que aprieta**, porque los delanteros dejan libres a los receptores del saque.
  **REJECTED**, con la misma medición que H1.

**Validación del instrumento (Regla J).**
- La sonda clasifica por `MatchTrace.ActionAt` de los rivales. Son jugadores de campo, no el portero en `Holding`, la
  etiqueta que se quedaba vieja en BX-8c.
- En juego abierto ve 1,9 rivales apretando por fotograma con el grito forzado y ~0 sin él, lo que el grito tiene que
  hacer.
- Sin el grito, el ~0 coincide con la medición independiente de BX-13.

## Decisión (11 oct 2026): no se toca

- El bono sólo cambia **dónde se ve** al delantero durante el grito: plantado en el borde del área en vez de en su
  sitio. No cambia el resultado.
- Esa imagen es la que pidió el revisor en la ADR 0178 («el delantero puede quedarse presionando, pero el resto debería
  replegar»), y es legible: la consigna de presión se ve en el campo.
- Quitarlo sería un cambio en `/data` y en el esquema sin efecto medible. Dejarlo no rompe ninguna regla, porque el
  área cerrada la hace cumplir `Move`, no la utilidad.

Lo único falso es el comentario de `EvaluatePress`, que prometía ganar el balón en zona de gol. Se corrige el comentario,
sin cambio de comportamiento.

## Queda abierto

- Si algún día `PressCarrier` sube de peso fuera del grito, o se reabre la ADR 0152, el bono vuelve a importar. Habría
  que repetir la sonda: el grito forzado en `MatchEngine.Run` y la conservación por posesión del portero.
