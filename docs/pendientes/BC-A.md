# BC-A — El goleador sigue en campo rival cuando el otro equipo saca de centro

**Estado:** **CERRADA** (24 sep 2026, decisión del revisor) · causa CONFIRMED · arreglo medido y con test
permanente (`KickoffFormationTests`). Ver la sección de cierre al final.

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

## CERRADA (24 sep 2026) — decisión del revisor: la reanudación espera, no teletransporta

El revisor, jugando: *«en general en todas las paradas de juego debemos dar tiempo para que los jugadores
se reposicionen de manera natural, sin teletransportes. No me importa que se alargue el tiempo de gameplay
(el reloj del partido seguiría parado)»*. Es el **arreglo (b)** de esta ficha, y la decisión que faltaba.

Tres piezas, y las tres hacían falta:

1. **El reloj del partido se separa del tick del motor.** `_clockTick` sólo avanza con el balón en juego.
   Sin esto, esperar a que la gente se coloque costaría minutos de fútbol y un gol en el minuto 80 acortaría
   el partido más que uno en el 10.
2. **El saque de centro no coloca a nadie de golpe**: se les manda a casa y **vuelven andando**.
3. **La reanudación espera** a que todos estén colocados, con un tope de 12 s
   (`restart.kickoffMaxWaitTicks`). Sólo el saque de centro: es la única reanudación que reordena a los
   once, y en un saque de banda o un córner la gente sigue jugando (AW-R), que está bien.

**Y una trampa medida, que costó una iteración**: fijar `TargetPoint` y confiar **no funciona** —`Decide()`
lo sobrescribe al tick siguiente con lo que diga la utilidad—, así que la gente se quedaba donde estaba.
La primera versión dejó **3,14 jugadores** en campo rival al sacar, **peor que el teletransporte** (1,00).
Hay que caminarlos explícitamente, como ya hacía `WalkRestartTaker` con el sacador.

| | antes | primera versión | ahora |
|---|---|---|---|
| goleador en campo rival al sacar | **85,7 %** | 39,1 % | **0,0 %** |
| jugadores del equipo que marcó en campo rival | 1,00 | 3,14 | **0,00** |

El tope tuvo que subir de 6 a 12 s: con seis el goleador celebra treinta ticks y no le da tiempo a cruzar
el campo andando, y se quedaba fuera de sitio el 39 % de las veces.

## La trampa que costó más cara: el suplente colocado en el campo

Partir `ResetPositions` en dos —`PlaceEveryoneHome` para el arranque, `SendEveryoneHome` para la
reanudación— **perdió por el camino la guarda `!player.OnPitch`**, y eso puso en el campo, desde el tick 0,
al suplente que trae una sustitución programada (ADR 0094): `Bench()` lo deja en `(-1,-1)` y la versión
nueva lo mandaba a su casilla-hogar como a cualquier otro.

**Cuatro tests de run en rojo**, todos con el mismo mensaje: *«la sustitución del jugador N por el M en el
tick T no es legal»*. El mecanismo, una vez visto, es directo: la resolución automática de sustituciones
simula, ve una lesión en T, añade la sustitución y **vuelve a simular**, apoyándose en que un cambio en T
no altera el partido antes de T. Con el suplente metido en el campo desde el principio, el partido
divergía **entero**, el que se lesionaba en T dejaba de lesionarse, y el motor rechazaba la sustitución.

**Dos hipótesis equivocadas antes de la buena**, y las dos por razonar en vez de medir:

- *«la espera depende de las posiciones, así que amplifica la divergencia»* → **REJECTED**: sustituida por
  una cuenta atrás fija de 120 ticks, los cuatro tests seguían en rojo. La espera se quitó por nada y hubo
  que devolverla.
- *«el problema es la longitud de la cuenta atrás»* → **REJECTED** en una sola medición: con
  `kickoffTicks` en 15 falla igual, sólo que en otro tick.

Lo que lo resolvió no fue una medición más sino **leer el diff entero buscando qué cambia el partido en el
tick 0**, que es lo único que podía producir ese síntoma. Es exactamente la Regla A al revés: se gastaron
dos iteraciones en hipótesis plausibles sin preguntarse antes qué clase de causa era compatible con el
síntoma.

**Y una consecuencia de diseño**: una cuenta atrás fija no vale para esto aunque funcionara. Habría que
dimensionarla para el peor caso —el goleador, que celebra treinta ticks y luego cruza el campo entero— y
entonces **todos** los saques de centro pagarían ese peor caso. La espera adaptativa dura lo que hace falta.

## Test permanente

`Sim.Tests/Engine/KickoffFormationTests.cs`: en 60 semillas, ningún jugador del equipo que marcó está en
campo rival en el fotograma del saque de centro. Es el hermano de `GoalCelebrationPositionTests` (BB-C) y
hacen falta los dos: aquel prohíbe teletransportar al que celebra, éste exige que acabe volviendo. Con uno
solo, cualquiera de los dos arreglos rompe al otro.
