# BC-B — El límite de usos de un perk no se respeta cuando su efecto vuelve a dispararlo

**Estado:** Resuelta (19 sep 2026) · el uso se consume al activarse

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

## Resolución (19 sep 2026)

`subscription.Uses++` pasa a ejecutarse **antes** de `ApplyEffects` (`Sim/Perks/EffectEngine.cs`), igual que un
consumible marca `Used` antes de aplicarse. Semántica escrita en `docs/modelo-datos.md` (`limit.per`).

- **CONFIRMED también para `charge`** (lo que aquí era LIKELY): con el código anterior daba 5 activaciones por
  partido con límite 1. `steamroller` no llegaba a encadenarse con estas plantillas.
- Revisión independiente (40 partidos): `double_shot` 180 → 36 activaciones, 6 → 2 `SHOT` del portador en un
  tick, cortes de recursión 36 → 0; `charge` 135 → 27, 6 → 2 `TACKLE`, 27 → 0.
- Pruebas: `RecoveryExtraActionTests.APerkThatRetriggersItselfStillRespectsItsLimit` (activaciones ≤ límite y
  como mucho dos acciones del portador en un tick; falla con el código anterior). `ExistingPerksAreUnchanged`
  vuelve a fijar `charge` en 13 (los 65 se midieron con el fallo).
- Balance: `--full-runs 300 --seed 1` antes/después sin cambios de estado (`runWinRate` 18,33 → 18,67, ruido);
  43 puertas idénticas a la línea base del día (las mismas 3 rojas preexistentes, mismos valores).
- **Queda abierto:** la potencia resultante de `charge` y `double_shot` no ha pasado por `game-design-review`
  (DESIGN CLAIM NOT PROVEN de la revisión); `double_shot` sigue sin cambiar el resultado ([BC-C](./BC-C.md)).
  Un `per: play` que cierra su jugada dentro de sus efectos recupera el uso en el mismo tick (latente: ningún
  perk del catálogo usa `per: play`). `PerkLoader` no exige `limit` a `extraAction` (hermano latente).

## Hermanos

[BC-C](./BC-C.md) (diseño de `double_shot`), [BB-Q](./BB-Q.md).
