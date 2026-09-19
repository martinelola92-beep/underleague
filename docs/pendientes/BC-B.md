# BC-B — El límite de usos de un perk no se respeta cuando su efecto vuelve a dispararlo

**Estado:** Abierta · causa CONFIRMED · error puro de motor

## Observación

«"Doble disparo" se ha activado 5 veces en el mismo partido.» (Partida del revisor, 19 sep 2026.)

## Causa

**CONFIRMED** leyendo el código y midiendo: en `Sim/Perks/EffectEngine.cs` el contador `subscription.Uses++`
se incrementa **después** de `ApplyEffects` (bloque del publicador, ~l. 271-305). Si el efecto publica de
nuevo el mismo evento (p. ej. `extraAction` → `RepeatShot` → `LaunchShot` → `SHOT`), la llamada anidada ve
`LimitReached == false` y el perk vuelve a dispararse hasta el tope de recursión `MaxDepth = 4`
(`Sim/Engine/Simulator.cs:40`).

## Medición

1.000 partidos Reference Human 50 vs 50 con `double_shot` (instrumento temporal en una copia de `a7a9e9c`):
activación en 952 partidos, **siempre exactamente 5 activaciones y 6 `SHOT` en el mismo tick**, 952 cortes de
recursión.

## Alcance

- `double_shot` — CONFIRMED.
- `charge` y `steamroller` usan la misma primitiva con `limit` 1 por partido; que `charge` encadene 65 veces
  en 20 partidos ([BB-Q](./BB-Q.md)) solo es posible saltándose el límite — **LIKELY**, sin medir.
- Toda métrica que cuente activaciones o sucesos de esos perks está inflada: el «+4,4 tiros» de
  `double_shot` en `docs/analisis/informe-decision-catalogo.md` es un artefacto de este fallo.

## Arreglo candidato (sin implementar)

Consumir el uso **antes** de aplicar los efectos (o marcar la suscripción como «en curso» durante la
aplicación). Decidir y dejar escrito si el límite se consume al activarse o al terminar. Afecta al consumo
de RNG de todo partido con esos perks → lote de `/Balance` y puertas.

## Hermanos

[BC-C](./BC-C.md) (diseño de `double_shot`), [BB-Q](./BB-Q.md).
