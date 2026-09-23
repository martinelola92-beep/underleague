# BI-B — El balón se ve raso siempre: la altura se calculaba, se guardaba y no la leía nadie

Estado: **CERRADA en la vista 3D** (23 sep 2026). Queda la vista 2D de depuración, a propósito.

Reportada por el revisor jugando la build de Windows: *«no se ve el arco del balón. Lo veo raso
siempre»*.

## Síntoma

El balón se dibuja a la misma altura en todo momento: un tiro alto, un centro y un pase al pie se ven
exactamente igual. Con una cámara en tres cuartos, además, subir el balón sin ninguna referencia en el
suelo sería indistinguible de alejarlo.

## Lo que discriminaba, y costó un grep

Hipótesis plausibles, ordenadas por **coste de verificación × poder discriminativo**:

1. La simulación no produce altura (la ADR 0135 dejó el vuelo a medias).
2. La produce pero la traza no la guarda (`MatchTrace` se escribió antes del paso 1).
3. La guarda pero el render no la lee.
4. La lee y el arco es tan bajo que no se distingue (`save.reachCells` 0,9 contra semiancho 1,0).

**Una sola medición las separa todas**: buscar los consumidores de la altura en el repositorio.

```
$ grep -rn "BallHeightAt\|BallInFlightAt" --include=*.cs Game Sim Balance
Game/Ui/MatchPitchView.cs:619:        if (trace.BallInFlightAt(frame))
Sim/Engine/MatchTrace.cs:164:    public float BallHeightAt(int frame) => _ballZ[frame];
Sim/Engine/MatchTrace.cs:170:    public bool BallInFlightAt(int frame) => _ballInFlight[frame];
```

**Hipótesis 3 CONFIRMED**, 1 y 2 REJECTED, 4 no hacía falta: `BallHeightAt` tenía **cero consumidores** —
solo su propia definición. `MatchPitchView3D` colocaba el balón en `new Vector3(x, BallRadius, z)`, una
constante. La altura del vuelo que la ADR 0135 añadió a `/Sim` (pasos 1 y 2: el balón tiene altura, el
marco es físico) llegaba intacta hasta la traza y se tiraba en el último metro.

Es el patrón que `CLAUDE.md` avisa de mirar: **un dato nuevo en `/Sim` sin nadie que lo consuma en
`/Game`**. La ADR 0135 midió el vuelo con métricas de `/Balance`, que no necesitan render, así que nada
falló en verde.

## Arreglado

`MatchPitchView3D.ApplyTrace` lee `BallHeightAt` e **interpola entre los dos ticks con el mismo `Alpha`**
que la posición, para que el arco sea una curva y no una escalera de 15 escalones por segundo. Y se añade
una **sombra en el césped** bajo el balón en vuelo, que se encoge con la altura: sin una referencia fija en
el suelo, la altura no se lee en una cámara en tres cuartos. Invisible mientras el balón va raso, así que
no cambia nada de lo que ya se veía.

## Lo que queda fuera a propósito

- **La vista 2D de depuración** (`MatchPitchView`, `Partido.tscn`) sigue dibujando el balón sin altura:
  solo usa `BallInFlightAt` para un aviso. Es la pantalla que el revisor llamó *desfasada* (23 sep 2026) y
  que `docs/ui/README` §3 pone «fuera del juego final»; no se toca.
- **La hipótesis 4 sigue sin medir**: que el arco *exista* no dice que sea legible ni que saque el balón
  del alcance del portero. Eso es lo que el paso 3 de la ADR 0135 tiene que resolver, y ahora por fin se
  puede **mirar** además de medir.
