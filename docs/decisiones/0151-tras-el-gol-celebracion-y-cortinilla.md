# ADR 0151 — Tras el gol: dos segundos de celebración, cortinilla y todos en su sitio

Fecha: 26 sep 2026 · Estado: **aceptada**. **Decisión del revisor.**
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
   - **Los derribados se levantan y se recolocan como los demás** (*«derribados se curan y también
     vuelven a su posición»*, decisión del revisor sobre la primera versión de esta ADR, que los dejaba
     en el suelo). `KnockedDown` termina en ese tick. No es alivio escondido: el gol es un corte visible y
     el partido vuelve a empezar desde la formación.
   - **Los lesionados se recolocan y conservan su estado**: una lesión tiene consecuencias de run y no
     se cura por un gol (principio rector 11). Nadie más cambia de estado. La celebración ya ha terminado,
     así que no aplica la protección de BB-C (no teletransportar a quien celebra): nadie celebra en ese
     tick.
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
8. **Sólo en la Retransmisión**, que es la pantalla del partido (ADR 0119/0120): un velo negro encima del
   campo y debajo del tablero y las tiras, así que el marcador sigue a la vista. El **modo depuración**
   (F3, `MatchScreen`) conserva el corte seco de BA-K: es un instrumento para mirar fotogramas y el negro
   taparía justo el que se quiere inspeccionar. *(La primera redacción decía «las dos pantallas»; se
   corrigió al implementar.)* La cortinilla por jugador de BA-K sigue disparándose en ese fotograma,
   tapada por el negro.

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

## Implementación (26 sep 2026)

- **`/Sim`**: `ScheduleKickoffAfterGoal` programa el saque con `kickoffTicks + CelebratingTicks` y abre
  `_goalCelebrationTicks`; durante la celebración **todos** los que están en el campo corren estado y
  cuerpo (energía, contadores) pero no deciden ni andan —la primera versión dejaba a derribados y
  lesionados en `UpdatePlayer` y el que se levantaba echaba a andar—; `ResetTeamsAfterGoal` coloca, levanta
  a los derribados, aplica la **barrera de reanudación en el mismo tick** y emite `TEAMS_RESET`.
  - **La barrera dentro del reinicio no estaba en el plan**: `KickoffSpot` deja al que no saca a 1,4
    casillas del balón y la barrera (`restartClearanceCells` = 2) lo empujaba 0,6 al tick siguiente, un
    segundo salto fuera del fotograma tapado (lo cazó `NobodyJumpsDuringTheKickoff`). Aplicada en el
    reinicio, el salto entero cae bajo la cortinilla. La discrepancia 1,4 / 2 es anterior (BC-A, BB-B).
  - `EventTypeNames.IsPresentationOnly` junta los tres casos sueltos de `PERK_TRIGGERED` (cargador,
    plantillas de descripción, log) y cubre el evento nuevo.
- **`/Game`**: `Game/Match/ResetCut.cs`, sin Godot: recibe el fotograma que la reproducción quiere
  enseñar y devuelve el que se pinta y la opacidad del velo. `BroadcastScreen` lo aplica antes del
  director; `SeekTo` y la re-simulación de una sustitución lo reinician.
- **Pruebas**: `GoalResetTests` (un reinicio por gol, 30 ticks después, saque sin espera; nadie se mueve
  más de 0,10 casillas por tick durante la celebración —la separación de cuerpos mueve 0,06, medido—;
  tras el reinicio nadie en campo contrario, nadie derribado ni celebrando, sacador sobre el balón y nadie
  anda más de 0,6 hasta el saque). `NobodyJumpsDuringTheKickoff` exime **sólo** el fotograma del reinicio.
- **Capturas**: `Game/screenshots/cortinilla-{1..6}-*.png`, tomadas **reproduciendo** (`StepManual`), no
  con `SeekTo`, que la anula a propósito. Primera tanda sin cortinilla: el director devolvió un fotograma
  8 por delante del pedido y ya había pasado el reinicio —el arnés, no la pieza (regla J)—.

## Medición (4.000 partidos × 2 semillas, contra `fa2ad11`)

| semilla | variante | ticks | goles | entradas | sin balón | faltas | lesiones |
|---|---|---|---|---|---|---|---|
| 1 | base | 1924 | 2,308 | 8,46 | 3,80 | 7,09 | 0,800 |
| 1 | ADR 0151 | 1677 | 2,211 | 7,53 | 3,35 | 6,41 | 0,731 |
| 1 | + 110 ticks de espera | 1909 | 2,277 | 8,01 | 3,70 | 6,92 | 0,767 |
| 2 | base | 1851 | 2,073 | 8,60 | 2,33 | 4,42 | 0,423 |
| 2 | ADR 0151 | 1647 | 2,053 | 7,96 | 2,09 | 4,09 | 0,373 |
| 2 | + 110 ticks de espera | 1845 | 2,071 | 8,19 | 2,35 | 4,32 | 0,390 |

- **Ninguna métrica sale de banda ni cambia de estado.** La única fuera de banda
  (`betterTeamWinRate_human_60_vs_human_40`, 99,70) ya lo estaba con el mismo valor.
- **Los partidos sin gol salen idénticos** (mismas entradas, mismo número): el efecto es sólo lo que pasa
  tras un gol, y la caída de entradas crece con los goles del partido.
- **Las lesiones bajan en torno al 10 %** (−8,6 % y −11,8 %, a ~3,4 errores típicos en las dos semillas,
  estimación de Poisson de la revisión independiente), y las faltas un 7-10 %. Dentro de banda, pero es
  el recurso central del juego (la carnicería administrada) y se dice con esas palabras.
- **Causa, CONFIRMED sólo en parte**: la duración del balón muerto. Devolver ~110 ticks de espera por gol
  (con todos ya colocados) recupera casi entero lo de sin balón y la mayor parte de faltas y lesiones, pero
  de las entradas sólo el **52 %** (s1) y el **36 %** (s2): en s2 la mayor parte sigue sin atribuir.
  **LIKELY, sin aislar**: que la parte explicada sea la energía recuperada parado (en ese rato también
  corren los enfriamientos de perk en segundos). Candidatos para el resto, sin experimento: los
  derribados que se levantan y la formación de partida.
- **No se compensa**: devolver la espera anula lo que la decisión pide, y el balance fino está aplazado
  por el revisor. Queda anotado aquí para cuando se haga.
- **Tras la revisión independiente** se quitó la barrera durante la celebración (abajo); el lote repetido
  sale **idéntico** en las dos semillas, como se esperaba: esas posiciones las sobrescribe el reinicio.
- **Puertas**: las mismas 4 rojas de 43 que antes (curva de jefes con `grimhold_guns` 56,76; `orc_violence`
  56,77, antes 55,60; `undead_none` 61,55 %, antes 60,33). Ninguna nueva.

## Revisión independiente (26 sep 2026) y lo que cambió

- **La cortinilla llegaba un fotograma tarde** (hallazgo principal): mientras se enseñaba el último
  fotograma de la celebración, la vista 3D ya interpolaba hacia el del reinicio y, durante ~33 ms, se veía
  a todos colocados antes de volver atrás y fundir. `ResetCut` arranca ahora al **llegar** a ese último
  fotograma, con la interpolación a cero.
- **La barrera saltaba al empezar la celebración**: empujaba a los del equipo que marcó a 2 casillas del
  centro en el primer tick (161 saltos de hasta 1,90 en 450 goles; ya pasaba en `fa2ad11`). Contradecía el
  punto 1 («los demás se quedan donde están»). No actúa durante la celebración; se aplica en el reinicio.
  El test la dejaba pasar por empezar un paso tarde: corregido.
- **Dos goles no llevan reinicio, y está bien**: el del último tick (el partido termina) y el que empata en
  el último tick y abre la turba en ese mismo tick (ese saque es el de la turba, punto 4). El test ya los
  distingue en vez de suponer «un reinicio por gol».
- **`CelebratingTicks = 0`** (el esquema lo permite) apagaba la regla en silencio y se volvía andando:
  ahora el reinicio es inmediato, con test.
- **Tolerancia de «ya colocado»** de 0,6 a 0,10 casillas (medido: 0,06).
- **Punto 8 corregido**: la cortinilla por jugador de BA-K ya no se ve antes del velo.

**Sin cubrir, anotado**: el test de un suplente con sustitución programada (ADR 0094) en el reinicio (la
guarda `OnPitch` está y la revisión la verificó leyendo, sin test); un test puro de `ResetCut` (vive en
`/Game`, que no tiene proyecto de tests, RT-084). Los enfriamientos de perk en segundos con ~250 ticks
menos de balón muerto por partido siguen sin auditar.

**DESIGN CLAIM NOT PROVEN**: que levantar a los derribados con el gol sea inocuo. Un derribo justo antes
del gol —el de Muro (`bulwark_stance`, BM-A) o cualquier otro— ahora se pierde en el reinicio. Es la
decisión del revisor; su interacción con los perks que derriban no ha pasado por `game-design-review`.

