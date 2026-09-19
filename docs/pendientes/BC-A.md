# BC-A — El goleador sigue en campo rival cuando el otro equipo saca de centro

**Estado:** Abierta · causa CONFIRMED · arreglo pendiente de `game-design-review` (cambia la regla de BB-C)

## Observación

«Tras un gol, mi delantero estaba en el campo contrario antes de que el rival hiciese el saque inicial.»
(Partida del revisor, 19 sep 2026.)

## Medición

**CONFIRMED** sobre 200 partidos `TestMatches.Reference` con traza (385 goles; instrumento temporal en una
copia de `a7a9e9c`, fuera del repositorio):

- **385/385** goles: el goleador sigue en la mitad rival, en estado `Celebrating`, cuando se saca de centro.
- Ningún otro jugador de su equipo está en campo rival al sacar (0/385) ni cruza durante la cuenta atrás
  (0/385); en el primer saque de cada partido no ocurre nunca (0/200).
- El goleador pasa en la mitad rival una mediana de 84 ticks desde el gol (p90 250). La infracción estricta
  (gol → saque) dura siempre 15 ticks; después sigue congelado otros 15 con el balón ya en juego.
- También se ve en los prototipos de la fase C.2 de UI (congelación del gol).

## Causa

Es el arreglo de BB-C: `ResetPositions` salta a propósito a quien celebra (`Sim/Engine/MatchEngine.cs:2986`).
La celebración dura 30 ticks (`data/sim/tuning.json:31`) y la cuenta atrás del saque 15 (`tuning.json:146`);
mientras celebra no se mueve (`MatchEngine.cs:889`, 985-1003), así que se queda donde marcó (a veces
dentro del área rival).

## Hipótesis descartadas

- Casillas-hogar pasado el medio campo — **REJECTED** (`PlacementColumns = 8`; 0/200 en el primer saque).
- Otros jugadores que cruzan durante la cuenta atrás — **REJECTED** (0/385).
- Solo render (interpolación, cortinilla BA-K) — **REJECTED**: la traza de `/Sim` ya trae esas posiciones.

## Arreglos candidatos (sin implementar)

- (a) Al resolver el saque de centro (`ResolveRestart`), terminar la celebración y recolocar a quien siga
  en campo contrario — la red de seguridad de BA-D aplicada al goleador; BA-K tapa el salto en pantalla.
- (b) Alargar la cuenta atrás hasta que acabe la celebración y dejar que vuelva andando.

Riesgos: (b) alarga el partido y mueve RT-056; (a) reintroduce un salto visible. Hoy el equipo que marca
tiene un delantero adelantado gratis tras el saque: quitárselo puede mover `goalsPerMatch` (sin medir).
`EnforceRestartClearance` tampoco filtra a quien celebra (hermano, sin medir).

## Hermanos

[BB-C](./BB-C.md) (origen), [BA-D](./BA-D.md), [BB-B](./BB-B.md).
