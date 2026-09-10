# Plan: lo que el revisor vio en la segunda partida (paquete AZ)

**Fecha:** 2026-09-09. **Origen:** siete anotaciones del revisor (`pendientes.md` AZ-A..AZ-G) tras jugar la build.
**Estado:** tandas 1, 2 y 3 hechas y medidas (ADR 0090, ADR 0091; el paso 5 incluido); `buildsWinDifferently_passChain` volvió a verde. Quedan cuatro filas de puerta fuera tras la física del pase, con las opciones en la ADR 0091 (decisión del revisor), y la tanda 4 (AZ-F, sustitución en partido).

## Orden de ejecución y por qué

| Tanda | Qué | Toca | Se mide con |
|---|---|---|---|
| 1 | AZ-A saque + AZ-C pase atrás | `MatchEngine.cs`, `Utility.cs`, tests | `--runs 500` × 2 semillas: `injuriesPerMatch` (0,3-0,9), `shotsPerMatch` (8-16), `possessionChanges` (12-28) |
| 2 | AZ-D falta = reanudación + AZ-E `whistlePercent` + AZ-G clínica leve | `MatchEngine.cs` (`RestartKind.FreeKick`), `tuning.json`, `MedicalSystem.cs`, `economy.json`, `RunPolicy` | `--runs 2000`: `possessionChanges`, `foulsPerMatch`; `--full-runs 600`: clínica |
| 3 | AZ-B pasos 0-4 (`plan-pases-trayectoria.md`) | `MatchEngine.cs`, `Utility.cs`, `tuning.json`, `weights.json` | RT-056 completo por paso |
| 4 | AZ-F sustitución en partido (ADR de arquitectura) | superficie de `/Sim`, `MatchScreen`, `RunPolicy` | puertas de run |
| 5 | AZ-B paso 5 `ThroughPass` (ADR de regla) | motor + eventos | RT-056 |

Las tandas 1 y 2 son pequeñas y de efecto local; se hacen primero para que el revisor las vea en la siguiente
partida. La 3 es la que más mueve el fútbol y va paso a paso. La 4 y la 5 cambian reglas y llevan ADR.

## Tanda 1

### AZ-A — El saque "va y vuelve" (regresión de AW-R, `bfe96dc`)

Causa, con traza (`scratchpad/az-diag/kickoff.txt`): desde AW-R `UpdatePlayer` corre también con `_restartTicksLeft > 0`,
y `UtilityContext.BallDead` solo quita el **bono** de balón suelto a `ChaseBall`, no descalifica la acción; el
perseguidor designado (AW-S) camina hasta el balón aparcado durante el segundo de espera, y `TakeKickoff` →
`ResetPositions()` (`MatchEngine.cs:2300/2350`) lo devuelve de golpe al pitar. Saque inicial, cualquier semilla:
el id 106 recorre 1,44 casillas y salta de vuelta a `(9.50, 2.50)` en el tick 15. Sobre 50 partidos: 10,7
teletransportes > 0,5 casillas por saque de centro, 84 de ellos en `Chasing` (antes de AW-R: 4).

Arreglo (sin datos): designar y **congelar al sacador al abrir** la reanudación (`BeginRestart`, `MatchEngine.cs:2178`),
saltarlo en el bucle de `Step` mientras `wasRestarting`, y mover `ResetPositions()` de `TakeKickoff` a
`BeginRestart` para que la formación se recomponga al pitar y no un segundo después. Test nuevo en
`MatchRulesTests`: `TheRestartTakerStandsStillDuringTheDeadBall` (posición del `Actor` idéntica en toda la
cuenta atrás; en `kickoff` nadie salta más que la velocidad máxima por tick al resolver).
`FieldPlayersKeepMovingDuringADeadBall` sigue verde: ya excluye al sacador y al portero.

Decisión de diseño menor que se toma sin consultar: mover `ResetPositions` al pitido teletransporta al
goleador en el instante del gol (hoy celebra en el sitio 15 ticks). Se acepta; si el revisor prefiere la
celebración in situ, la pieza del sacador congelado ya resuelve la queja por sí sola.

### AZ-C — Solo ante el portero, pasa atrás

Causa, con volcado de utilidad (RT-098, semilla 12 tick 863: `ShortPass` 945 contra `Shoot` 743): no hay
precondición de línea limpia. `OpponentsAheadCount` (`Utility.cs:941`) cuenta al **portero** como rival por
delante, lo que desactiva la guarda de AW-D contra el pase atrás y hunde `Dribble`; `passUnderPressureBonus`
(+180, `Utility.cs:820`) se cobra por tener al portero a menos de 1 casilla. `EvaluateShoot` (`Utility.cs:972`)
no sabe si la línea está libre. Frecuencia: 3,46 pases atrás con línea limpia dentro de 8 casillas por partido
(5,4 % de los pases), 1,15 a ≤ 4 casillas.

Arreglo (precondición dura, espíritu AW-S/AW-Q; el hueco es de 202 puntos y ningún término razonable lo cierra):
en `EvaluatePass`, si el portador es de campo, está dentro de `shootBaseRangeCells` y **ningún rival de campo**
está a menos de `pass.interceptRadiusCells` (0,9, el mismo radio con el que `TryBlockShot` bloquea de verdad)
del segmento portador→portería, un receptor **por detrás** no es destino legal. Sin receptor, `EvaluatePass`
cae a `−passNoReceiverPenalty` y `Shoot` gana sin tocar `weights.json`. Colateral en la misma tanda: excluir al
portero de `OpponentsAheadCount` y del `HasOpponentWithin(PressureRadius)` de `Utility.cs:818`. Test en
`AttackActionsTests`: escenario a mano (portador a 2 casillas, portero en su sitio, corredor limpio, único
compañero 2 casillas por detrás) → `Shoot`; con un defensa en el corredor → `ShortPass`.

Palanca si `shotsPerMatch` se sale de banda, en este orden y sin datos nuevos: acotar por distancia (≤ 6 →
2,74/partido; ≤ 4 → 1,15) o exigir que el portero sea el único rival por delante (0,39).

## Tanda 2

### AZ-D — La falta señalada reanuda con el balón para el rival

Encaja en **RF-053** (reanudaciones instantáneas con animación de 1 s sin parar el reloj), no en RF-054 (solo
penalti y roja detienen): `RestartKind.FreeKick`, en el punto de la falta, para el equipo que la sufre, con la
misma cuenta atrás que banda o córner; el infractor sigue derribado (`KnockedDown`). Si la falta ya programa
penalti, manda el penalti. ADR corta: «Modifica RF-053» (añade la falta a la lista), decisión del revisor.
Vigilar `possessionChanges` (ADR 0081: 12-28): cada falta pitada es un cambio de posesión.

### AZ-E — Los porcentajes del árbitro, explícitos y en una tabla

Existen ya: `tackle.foulBase` 320 + `foulStrengthFactor`; `yellowCardBase` 250 / `redCardBase` 10 +
bonus de entrada dura + desplazamiento por criterio; `injury.onFoulBase` 60 (ADR 0041); `penaltyOnFoulInArea`
8.000. Falta **pita/ignora**: `referee.whistlePercent` (probabilidad de señalar una falta ocurrida; la no
señalada no reanuda y mueve el criterio como acción sucia no vista, RF-063, que es lo que hoy solo pasa cuando
un perk la anula). Entregable para el revisor: tabla en `docs/simulacion.md` con los cuatro porcentajes, su
valor de datos y su **valor efectivo medido** (`foulsPerMatch`, amarillas/rojas por falta, lesiones por falta
en `--runs 2000`). Los rasgos de árbitro que los modulan (RF-061) siguen siendo de fase 3.

### AZ-G — La clínica cura también la leve

`MedicalSystem.Treat` acepta `MinorInjury`; precio propio `economy.clinicMinorCost` (propuesta 4; la grave
sigue a 10); la política automática (`RunPolicy`) solo lo paga para un titular del siguiente partido con oro
por encima de su reserva. Test en `MedicalSystemTests`; medir `goldSpentClinicPerRun` y `deathsPerRun` con
`--full-runs 600` (menos leves alineadas = menos multiplicador ×8 de la tirada letal, ADR 0048).

## Tanda 3 — AZ-B

Diseño completo y medido en `docs/plan-pases-trayectoria.md`: pase al pie acotado → intercepción con
proximidad por `bodyRadius` → pasillo como puntuación del receptor → pasillo en la decisión de tirar. Un paso
por lote; los pasos que gastan `possessionChanges` (2) nunca seguidos sin medir.

## Tanda 4 — AZ-F

Motor reanudable con **puntos de decisión**: el partido corre hasta la lesión o muerte con banquillo disponible,
devuelve estado + eventos hasta ahí, la pantalla muestra la ventana, y el motor continúa con el sustituto
elegido. Determinista (RT-024): la decisión es parte de la entrada y queda en el registro del partido. Sin
banquillo disponible, el equipo sigue con uno menos (hoy). No hay cambios voluntarios. `/Balance` y las puertas
resuelven el punto con una política por defecto (mismo puesto, si no el suplente de mayor calidad). ADR de
arquitectura: modifica RF-054 (tercera pausa) y el §1 de requisitos («toda la decisión ocurre entre partidos»),
`arquitectura.md` (superficie de `/Sim`), `MatchScreen` (reproducción por tramos).

## Tanda 5 — AZ-B paso 5

`ThroughPass`: acción nueva y desenlace nuevo del pase (RT-092). ADR de regla antes de escribir código.
