# BC-G — El balón se queda suelto en el córner y nadie lo coge

**Estado:** Abierta · tres mecanismos CONFIRMED · reabre parcialmente [BB-G](./BB-G.md)

## Observación

«El balón se ha quedado suelto en el córner y ningún jugador ha sido capaz de cogerlo; uno se ha quedado
acercándose y alejándose en el sitio tick a tick.» (Partida del revisor, 19 sep 2026.)

## Medición

1.000 partidos `TestMatches.Reference` con traza (instrumento temporal en una copia de `a7a9e9c`), episodios
de balón suelto y quieto ≥15 ticks en juego abierto: **114, de ellos 112 en esquinas**; 105 con el balón
exactamente sobre la línea (p. ej. (0,0), (16,7)); 113 empiezan tras `PassFailed:loose`. Casi todos cortos,
pero **tres bloqueos de 597, 361 y 310 ticks** (~0,3 % de partidos) y 22 con oscilación.

Casos reproducibles (semilla de `TestMatches.Reference`): 216 (ticks 1503-2099), 979 (446-755), 635 (344-704),
24 (529-576).

## Mecanismos (CONFIRMED con volcado de utilidad, RT-098)

- **M1 — ciclo `ChaseBall`/`Retreat` sin histéresis.** `ChaseBall` gana 45/casilla al acercarse y su
  penalización de zona se calcula sobre el balón; `Retreat` gana 40 + 90 por casilla en cuanto el jugador sale
  de su zona blanda (`Utility.cs:1569-1584`, `weights.json:40`, `tuning.json:171`). Equilibrio a ~2,3
  casillas: avanza y retrocede cada 2-4 ticks. Semilla 216, id104. Familia de [BB-K](./BB-K.md).
- **M2 — el límite duro de zona impide recoger.** El objetivo se recorta al límite exterior y la acción se
  descarta a <0,25 casillas de él (`Utility.cs:537-541`, `OuterLimitMinAdvance` en `:77`); nunca entra en el
  radio de recogida de 0,5 (`MatchEngine.cs:21`): mínimo medido 0,61. Semilla 979, id102. **Es la H1 de
  BB-G, descartada con la medición de 200 partidos de entonces: evidencia nueva para reabrirla.**
- **M3 — el designado prefiere `CoverSpace` (bono 150) a `ChaseBall`** (738 frente a 570, semilla 216, id2) y,
  por AW-S, nadie más de su equipo puede perseguir.

**LIKELY, sin aislar:** los destinos de pase se recortan al campo (`MatchEngine.cs:1660-1663`, `Utility.cs:308,
321`) y los límites de fuera son inclusivos (`MatchEngine.cs:2606-2613`): el balón nunca sale por el fondo →
**0 córneres en 1.000 partidos** (1.227 saques de banda). Hermano de [BB-N](./BB-N.md).

**REJECTED en esta muestra:** [BB-G2](./BB-G2.md) (portero como más cercano), 0/114.

## Arreglos candidatos (sin implementar)

1. `IgnoreOuterLimit` para `ChaseBall` sobre balón suelto (quedaría a 0,46 del balón: margen justo).
2. Inercia de `ChaseBall` mientras ya se persigue (`CurrentAction` existe) o sin bono de fuera de zona en
   `Retreat` para el designado con balón suelto — primitiva: `game-design-review` → `architecture-review` →
   `balance-measure`.
3. Si el designado rechaza perseguir, pasar la designación al siguiente más cercano.
4. Dejar que el pase recortado salga del campo o revisar los límites inclusivos (despertaría BB-N; mueve las
   métricas de reanudación de RT-056).

Toca la palanca de la ADR 0117 y de `ChaseBall pen = 50`: vigilar juntas las métricas de diferenciación.

## Hermanos

[BB-G](./BB-G.md), [BB-N](./BB-N.md), [BB-K](./BB-K.md), [BB-O](./BB-O.md), [BB-G2](./BB-G2.md).
