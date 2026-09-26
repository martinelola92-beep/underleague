# ADR 0151 — Tras el gol: dos segundos de celebración, cortinilla y todos en su sitio

Fecha: 26 sep 2026 · Estado: **aceptada, sin implementar**. **Decisión del revisor.**
**Enmienda RF-053** («nadie se teletransporta») **sólo para el saque de centro tras gol**, y con ello una
parte de [BC-A](../pendientes/BC-A.md) y de la [ADR 0147](0147-la-reanudacion-dura-lo-que-hace-falta-para-entenderla.md).
El resto de reanudaciones no cambia.

## Decisión del revisor

> *Cuando hay gol dejamos 2 segundos de jugador celebrando, quitamos la transición de jugadores a sus
> posiciones y vamos a poner un fundido a negro muy breve (cortinilla) y a la vuelta todos los jugadores
> deben estar en sus posiciones iniciales menos el jugador que va a sacar el balón.*

## Qué hay hoy (leído en el código, 26 sep 2026)

- Al marcar, el goleador entra en `Celebrating` durante `states.CelebratingTicks` = **30 ticks (2 s)**
  (`MatchEngine`, tras `ParkBall`), y **en el mismo tick** se programa el saque de centro
  (`ScheduleKickoff` → `BeginRestart`).
- `BeginRestart` llama a `SendEveryoneHome` y fija a cada uno su `KickoffSpot` (ADR 0147: la formación
  empujada `kickoffPushCells` = 2,5 casillas hacia el medio campo, sin cruzar la línea y, el que no saca,
  fuera del círculo). **Todos vuelven andando mientras el goleador celebra**, y el goleador empieza a
  volver cuando termina su celebración.
- El saque espera a `EveryoneInPlace()` con un tope de `restart.kickoffMaxWaitTicks` = **180 ticks
  (12 s)**. El tope se subió de 6 a 12 s en BC-A precisamente porque el goleador, que celebra 30 ticks y
  luego cruza el campo, no llegaba: **el caso peor de cada saque tras gol es él**.
- El reloj del partido está parado todo ese tiempo (`_clockTick`, ADR 0147): la espera cuesta **reloj de
  pared**, no fútbol.
- En pantalla, el gol ya tiene su propia pausa: estandarte N3 que **congela ~2 s** (`docs/ui/README.md`
  §4). Después se reproduce la vuelta andando.

Es decir: lo que el revisor quiere quitar es la caminata de vuelta, que hoy dura lo que tarde el goleador
en cruzar el campo (**sin medir en ticks**; lo único medido es que 6 s no bastaban).

## Decisión

### En `/Sim` (la regla)

1. **Tras un gol hay una fase de celebración de `CelebratingTicks` (30 ticks, 2 s, el dato que ya
   existe)**. Durante ella el balón está muerto y el reloj parado, como en cualquier reanudación, y
   **nadie vuelve a casa**: el goleador celebra y los demás se quedan donde están, parados. No se
   inventa conducta nueva (compañeros que corren a abrazarle, etc.); si se quiere, es otra decisión.
2. **Al terminar la celebración, en un solo tick, el motor recoloca a todos** y emite un evento explícito
   de ese instante (nombre propuesto `TEAMS_RESET` / `EventType.TeamsReset`, Detail `"goal"`), para que
   la presentación no tenga que **adivinar** el salto con un umbral de distancia (ver BA-K y el buscador
   del rastro: los umbrales sobre posiciones tienen filo; un evento no).
   - **«Posiciones iniciales» = `KickoffSpot`**, la colocación con la que se hace el **primer** saque de
     centro del partido. No `HomeCenter`: desde ahí el equipo volvería a caminar las 2,5 casillas de
     compresión durante la cuenta atrás, que es otra vez una transición, y la ADR 0147 midió que salir
     del saque replegado cambia el fútbol (`tacklesPerMatch` 6,86 → 5,69).
   - **El sacador, en el punto de saque, sobre el balón.** Es la lectura que se toma de *«menos el
     jugador que va a sacar»*: todos en su sitio y él ya en el balón. Así la vuelta del negro enseña el
     saque listo, sin nadie andando. *(Lectura alternativa, no tomada: que el sacador se quede donde
     estaba y camine al balón. Reintroduce justo la transición que se quiere quitar.)*
   - **Sólo jugadores en el campo** (`OnPitch`): la guarda que BC-A perdió al partir `ResetPositions` y
     que metía en el campo al suplente de una sustitución programada (ADR 0094) desde el tick 0. Es un
     requisito, no un detalle: sin ella diverge el partido entero y la resolución de sustituciones falla.
   - **Los estados se conservan**: un derribado o un lesionado se recoloca y sigue derribado o lesionado
     en su sitio (la cortinilla lo tapa). No se cura ni se levanta a nadie por ser gol: sería daño o
     alivio no anunciado (principio rector 11). La celebración ha terminado, así que ya no aplica la
     protección de BB-C (no teletransportar a quien celebra): nadie celebra en ese tick.
3. **Después, la cuenta atrás normal del saque** (`restart.kickoffTicks` = 15 ticks, 1 s) **sin espera
   adaptativa**: todos están en su sitio por construcción, así que `EveryoneInPlace` es cierto al
   instante y `kickoffMaxWaitTicks` deja de ser el caso peor del saque tras gol (sigue existiendo para el
   saque de centro del gol de oro, que no es tras gol, y como red de seguridad).
4. **Lo que no cambia**: el primer saque del partido (arranque, `PlaceEveryoneHome` + caminata a
   `KickoffSpot`), el saque de centro que abre el gol de oro tras la turba, el gol de oro (termina el
   partido), y las otras cinco reanudaciones, que siguen llegando andando (RF-053 intacto para ellas).

### En `/Game` (la presentación, RT-014)

5. **Los dos segundos de celebración se ven enteros**, después del estandarte del gol (que no cambia).
6. **Cortinilla: fundido a negro muy breve alrededor del evento `TEAMS_RESET`**. La reproducción se
   **detiene** durante el fundido (es una pausa de presentación, como la congelación del gol; no gasta
   ticks del motor): funde a negro sobre el último fotograma de la celebración, cambia al fotograma del
   reinicio con la pantalla en negro, y vuelve. Duración **provisional, sin medir** (regla H): ~0,15 s de
   ida, ~0,1 s en negro y ~0,15 s de vuelta a x1. Se ajusta viéndolo, y la cifra final se anota aquí.
7. **Velocidades** (ADR 0120): a x4 el fundido dura la cuarta parte; a x16 es un **corte seco**. Al
   **saltar con la barra** por encima del reinicio no hay fundido: un salto de la barra no es
   reproducción.
8. **Un solo sitio para las dos pantallas**: el fundido es un velo a pantalla completa sobre el campo, así
   que vale igual para la vista 2D y la 3D, en la pantalla de Partido y en la de Retransmisión. La
   cortinilla por jugador de BA-K (atenuación en saltos > 0,6 casillas) seguirá disparándose en ese
   fotograma, tapada por el negro; no hace falta excepción.

## Por qué no contradice BC-A, y por qué sí la enmienda

BC-A quitó los teletransportes porque **se veían**: el goleador cruzaba el campo en un fotograma, o se
quedaba en campo rival al sacar. La queja era el salto **visible y sin explicación**. Aquí el salto
**no se ve**: lo tapa un corte explícito, que es la gramática de una retransmisión (tras el gol, la
realización corta y vuelve al saque). *«Eventos explícitos > transiciones invisibles»* (principios de
diseño de `CLAUDE.md`).

Pero la frase de RF-053 *«nadie se teletransporta: se llega andando»* deja de ser cierta para este caso, y
por eso esta ADR **enmienda** RF-053 en vez de reinterpretarlo. Al implementar, RF-053 gana la excepción
con referencia a esta ADR (`docs/requisitos.md` no se toca en este commit, que es sólo la decisión).

Lo que BC-A arregló sigue arreglado por otra vía: el goleador ya no puede estar en campo rival al sacar
porque se le coloca, y `KickoffFormationTests.NobodyFromTheScoringTeamIsStillInTheRivalHalfWhenTheKickoffIsTaken`
debe seguir en verde sin tocarlo.

## Consecuencias

- **Reloj de pared más corto y constante tras cada gol**: estandarte (~2 s) + celebración (2 s) +
  cortinilla (~0,4 s) + cuenta atrás (1 s), en vez de una caminata de duración variable con tope de 12 s.
  RF-050 lo vigila como techo; esto sólo puede bajarlo.
- **Balance: posible, pequeño, y hay que medirlo** (`balance-measure`). El reloj del partido no cambia,
  pero los **ticks** tras cada gol sí (menos ticks de balón muerto). Todo lo que cuente ticks sin mirar si
  el balón está en juego corre menos: enfriamientos de perk en segundos (`cooldownSeconds`, que el
  cargador pasa a ticks), la duración de un derribo, cualquier estado con duración. La ADR 0148 ya sacó
  el enfriamiento de entrada del balón muerto; los demás **no se han auditado** para esto. Además cambia
  **desde dónde** arranca cada uno la primera jugada tras el gol (formación completa en lugar de «el que
  llegó»), aunque BC-A ya exigía que todos estuvieran colocados. Métricas a vigilar: `goalsPerMatch`,
  `tacklesPerMatch`, las de RT-056, y las puertas.
- **Tests que cambian**:
  - `KickoffFormationTests.NobodyJumpsDuringTheKickoff` fallará **por diseño** en el tick del reinicio. Se
    reescribe: nadie salta **salvo en el tick de `TEAMS_RESET`**, y en ése todos quedan exactamente en su
    `KickoffSpot` y el sacador en el punto.
  - `GoalCelebrationPositionTests` (BB-C: el goleador no salta en el tick del gol) debe seguir en verde
    tal cual: el salto ocurre 30 ticks después, cuando ya no celebra.
  - Nuevos: nadie se mueve hacia casa durante la celebración; el suplente de una sustitución programada
    no entra en el campo por el reinicio (el caso de BC-A); determinismo (RT-024).
- **El replay y la traza**: el salto existe en la traza (RT-020, el tick lógico es entero). La
  presentación no lo interpola porque el evento le dice que es un corte.

## Cómo se implementa (orden, para la sesión que lo haga)

1. `architecture-review`: el evento nuevo y quién lo consume (capa `Sim/Run/View` si hace falta un dato
   presentable, o la pantalla directamente desde la traza).
2. `/Sim`: fase de celebración, reinicio en un tick, evento, tests. Commit propio (no se mezcla con `/Game`).
3. `balance-measure` contra el `HEAD` anterior, con varias semillas.
4. `/Game`: el velo y la pausa de presentación, con `visual-review` (secuencia de capturas: último
   fotograma de celebración, negro, vuelta con todos colocados).
5. `independent-reviewer` antes de cerrar (Regla E: toca `/Sim`).
6. `docs/requisitos.md`: excepción en RF-053 con referencia a esta ADR.
