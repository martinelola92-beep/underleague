# ADR 0169 — La turba estrecha el campo (y acelera el juego)

Fecha: 29 sep 2026 · Estado: **aceptada, cifras provisionales** (decisión de diseño tomada sin consultar dentro de
lo que fija RF-055b; el gate 5 de Knavall lo desbloqueó el revisor el 29 sep: «las lesiones se arrastran», es
decir, **el desgaste es recurso de run**). **Requisitos:** RF-012d, RF-053, RF-055b, RF-055d, RT-014, RT-020,
RT-021, RT-023, RT-024. **Relacionada:** ADR 0167 (los tipos de turba, que dejó esto fuera por el gate 5),
ADR 0143 (nadie se teletransporta), ADR 0152 (el área cerrada: el precedente de «se sale andando»), ADR 0048
(las cinco condiciones de la muerte), Knavall F7 («turba real»).

## Lo que había (Regla G)

`tools/existe-ya.sh estrecha invadid`: RF-055b prometía tres cosas al entrar la turba —el árbitro se va, el campo
se estrecha 1 fila por lado con **casillas invadidas fijas y anunciadas**, y la velocidad global sube un 15 %—.
Sólo estaba la primera (`MOB_START` + `REFEREE_LEAVES`, fase `MobGoldenGoal`, sin árbitro; ADR 0158) y los tipos de
la ADR 0167. **El estrechamiento y el +15 % no existían**: «hoy es una etiqueta» (F7). Precedentes que sí existen y
se reutilizan: `_closedArea` + `PushOutOfArea` (ADR 0152: quien tiene su destino en una zona vetada va al borde
**andando**), `SendEveryoneHome`/`WalkTo` (ADR 0147: la reanudación recoloca andando) y `KickoffSpot`.

## Decisión

1. **La cuadrícula sigue siendo de 16×7.** `Pitch.Rows` es constante (7) y tiene ~90 usos (centro de portería,
   área, cámara, colocación, `LinkTable`, vistas). No se toca. Lo que cambia con la fase es **la banda jugable**:
   en tiempo reglamentario `[0, 7]`; en la turba `[1, 6]` (**`mob.narrowRowsPerSide`**, un dato de
   `data/sim/tuning.json`, provisional: 1, lo que dice RF-055b). Las filas invadidas son **siempre las exteriores**
   (fila 0 y fila 6): fijas, no sorteadas; el jugador las conoce desde la alineación (RF-012d).
2. **Dónde vive** (revisión de arquitectura, abajo): en el motor, como estado del partido (`_bandInset`, entero de
   filas por lado, 0 hasta la turba), nunca en la constante ni en la geometría compartida. Todo lo que hoy acota
   con `[0, Rows]` en una decisión de **movimiento o de balón** pasa por un único punto (`InBand`), que con
   `_bandInset == 0` devuelve el argumento sin tocarlo.
3. **Quien está en una fila invadida cuando empieza la turba se aparta andando** (RF-053, ADR 0143). La turba
   empieza en `RegulationEnd` seguida de un saque de centro que ya recoloca a todos andando (`SendEveryoneHome`):
   el destino de cada uno se acota a la banda y `WalkTo`/`Move` los llevan a su velocidad. Nadie salta. Un jugador
   que **ya está dentro** de la banda nunca sale de ella (su paso se acota); uno que está **fuera** sólo puede
   acercarse (su paso no le aleja más). Los empujones de cuerpos (`BodySeparation`) siguen la misma regla.
4. **El balón.** Sale de la banda por arriba o por abajo = fuera de banda: saque de banda desde el borde de la
   banda (el público lo devuelve), no desde el borde de la cuadrícula. Los córners se sacan desde el borde de la
   banda (esquina de la banda, no de la cuadrícula). El punto de saque de cualquier reanudación se acota a la banda.
5. **Cómo se aplica el +15 %** (RT-023, todo entero): `mob.speedPercent` = 15 (provisional). Se multiplica, en
   milésimas de casilla por tick y con división entera, **la velocidad de carrera del jugador** (`SpeedPerTick`:
   `milli × (100 + 15) / 100`, tras el resto de bonos, sobre el atributo ya cansado) **y las tres velocidades del
   balón** (pase, tiro, cabeceo/caída de cabeza), que además alimentan las predicciones de la utilidad
   (`UtilityContext.PassSpeedCellsPerTickMilli`) para que quien anticipa un pase use la misma velocidad que el
   balón. **No** se tocan: los ticks de las recuperaciones y de los estados (cooldowns), la fricción, la gravedad y
   el reloj lógico (15/s, RT-020) — «velocidad global» es la de movimiento, no la del reloj. La vara del cansancio
   (`ReferenceStepCenti`) queda constante a propósito (ADR 0142): correr más rápido cuesta más recorrido y, por
   tanto, más gasto. El +15 % vale **sólo desde `MOB_START`**.
6. **Interacción con los tipos de turba (ADR 0167).** Son ortogonales al estrechamiento: la banda depende de la
   fase, el tipo del efecto al entrar.
   - `invader` (Salta uno): el lesionado se sortea entre los jugadores de campo **en el campo** sin mirar en qué
     fila está (el que salta lo hace desde la grada, no desde la banda); el orden del sorteo no cambia.
   - `frenzy` (Frenesí): la presión se mide por distancia al portador; con la banda estrecha hay menos espacio
     para huir por la banda, así que el efecto crece (se mide).
   - `their_roar` (Su grada empuja): la orden ofensiva mueve columnas (`MentalityShift` es horizontal), no filas.
   - **Provocada** («Provocar a la grada», ADR 0167): sigue **sin** estrechar ni acelerar: la banda y el +15 %
     son de la fase de turba (`_goldenGoal`), no del tipo; provocar aplica el efecto del tipo con el árbitro
     todavía en el campo y con el campo entero.
7. **El partido reglamentario queda byte a byte igual.** Con `_bandInset == 0` y sin bono de velocidad, ninguna
   ruta cambia una sola operación ni consume un número más del RNG. Se demuestra con un test que compara el
   `Digest` de la traza de partidos que no llegan a la turba con el valor de antes del cambio (guardado como
   constante), y con el mismo test con `mob.narrowRowsPerSide = 0` y `speedPercent = 0`, que debe dar la traza
   **completa** de antes también en los partidos con turba.
8. **Render y anuncio.** El render no decide nada (RT-014): lee la cifra de `Catalog.Tuning.Mob` (dato, no
   constante) y, desde `MOB_START`, pinta **público** sobre las filas exteriores (tribuna que invade el césped,
   3D y 2D) y **no** recalcula la banda. El ojeo y el mapa ya anuncian el tipo de turba; se les añade la línea
   fija «en la turba el campo se estrecha (−1 fila por lado) y el juego va un 15 % más rápido», generada desde
   los datos (no cifras escritas a mano). El pregón de la turba lo repite.

## Las diez preguntas (`game-design-review`)

1. **Qué experimenta el jugador.** Al empatar, el campo se le echa encima: el público ocupa las bandas, los
   extremos se meten al centro, todo va más rápido. Lo sabía antes (ojeo, mapa, y las filas siempre son las mismas).
2. **Qué decide.** Cómo colocar a sus jugadores **sabiendo** que la fila 0 y la 6 desaparecen si hay empate: una
   alineación de extremos puros se aprieta contra los medios; una de centro compacto no nota nada. Y si le conviene
   buscar o evitar el empate (RF-055d).
3. **Qué debería decidir.** Lo mismo: RF-055b lo describe como «problema de colocación anticipable, no castigo
   aleatorio (RF-012d)».
4. **Regla.** RF-055b, casi literal. Regla nueva de contorno: la banda jugable es un estado de la fase, y el +15 %
   es de movimiento, no de reloj.
5. **Sistemas.** `/Sim` (motor: `_bandInset`, `InBand`, velocidades), `/data` (`mob` en `tuning.json` + esquema),
   `/Game` (vistas 3D y 2D, ojeo, mapa, pregón). La regla vive sólo en `/Sim`; `/Game` lee el dato.
6. **Alternativas.** (a) Cambiar `Pitch.Rows` por fase: ~90 usos, rompe colocación, cámara y todo el reglamentario.
   (b) Cuadrícula estrechada de verdad (12×… o 16×5): mismo coste y no da «casillas invadidas». (c) Sortear las filas
   invadidas cada partido: contradice RF-055b («casillas fijas y anunciadas»). (d) Acotar sólo los jugadores y
   dejar el balón en las 7 filas: el balón «sale» por debajo de un público que lo ocupa; incoherente. Se elige la
   banda por fase.
7. **Trade-off.** Menos espacio + más velocidad = más contactos: más entradas y lesiones **sin árbitro** en la fase
   que ya no tiene freno. Es el coste de que la turba sea el clímax; se mide (abajo). Los perks de banda pierden
   sitio; los de centro ganan.
8. **Estrategias.** Da un motivo real para alinear jugadores de centro en el partido en el que se sospecha el
   empate, y a las builds de violencia (que ya buscaban la turba, RF-055d) más contacto.
9. **Degeneración.** (a) Que la turba mate demasiado: la muerte sigue viniendo sólo de entradas/perks, pero hay más
   entradas: se mide `deathsPerRun` (condición 5 de la ADR 0048). (b) Atasco: un jugador atrapado entre la banda y
   un rival sin poder apartarse: se acota **el destino**, no se bloquea el paso; `BodySeparation` sigue empujando
   dentro de la banda. (c) Turbas que no acaban: se mide la duración y el % decidido por gol frente al desempate
   (`goldenGoalMaxTicks` no cambia). (d) Un jugador con posesión en una fila invadida al empezar la turba: el
   saque de centro pone el balón en el centro, no le afecta.
10. **Cómo se demuestra.** Tests: banda cerrada (nadie con Y < 1 o > 6 tras apartarse, y **nadie salta** más de un
    paso por tick en toda la turba); el balón fuera de banda = saque desde el borde de la banda; el +15 % (paso de
    un jugador y velocidades de pase/tiro en turba = base × 1,15 entero; en reglamentario, iguales); sin turba, traza
    idéntica; con los dos parámetros a 0, traza idéntica incluso con turba; determinismo. Lote `/Balance` antes y
    después; captura de la turba estrechada.

## Revisión de arquitectura (`architecture-review`)

- **Patrón existente**: el estado de fase del motor (`_goldenGoal`, `_closedArea`) con reglas que lo consultan. El
  estrechamiento es del mismo género que el área cerrada: un veto geométrico por fase, con salida andando.
  Reutiliza `WalkTo`, `KickoffSpot`, `SendEveryoneHome` y el patrón «acotar destino, no teletransportar».
- **Frontera**: `Pitch.Rows` y la geometría compartida (`/Sim/Model`, `Placement`, `LinkTable`) quedan intactos:
  la **colocación** y la cámara siguen viendo 16×7 y el reglamentario también. La banda es **estado del partido**
  (`MatchEngine`), no del modelo, y llega a `/Game` como dato de `Tuning` (no como constante duplicada).
- **Determinismo**: la banda se decide en el tick de `MOB_START` (misma condición que `_goldenGoal`); las
  velocidades son enteras y se derivan de datos + fase; nada nuevo consume RNG. `InBand` es una función pura del
  estado del partido. Sin `Dictionary` ni orden nuevo (RT-041).
- **Aritmética**: `milli * (100 + percent) / 100` en `int` (RT-023); sólo la posición sigue en `float`.
- **Segundo orden**: (i) `ClampToPitch` es estático y lo llama la utilidad en ~10 sitios para **elegir objetivos**;
  no se hace dependiente de la fase (los objetivos que caen en fila invadida se acotan **al ejecutarse**, en
  `Move`, así que un jugador que «quiere» ir a una fila invadida se queda en el borde de la banda: la utilidad ve
  su posición real, no la deseada). (ii) `CheckOutOfBounds` comprueba primero Y y luego X: se mantiene el orden y
  sólo cambian los límites. (iii) La zona de acción (`ClampToZone`) no cambia. (iv) Un perk/rasgo que dependa de
  «estar en la banda» (`Wide`, `onWing`, `startsIn`) lee la **casilla-hogar**, que no cambia.
- **Paralelismo**: ninguno.

## Implementación

- `/data`: `mob` en `data/sim/tuning.json` (`narrowRowsPerSide` 1, `speedPercent` 15) y esquema.
- `/Sim`: `MobTuning` en `Catalog.Tuning.Mob`; `MatchEngine._bandInset` (0 hasta `MOB_START`), `InBand`, velocidades
  entera de jugador y balón, saque de banda y córner desde el borde; `BodySeparation` acotado a la banda.
- `/Game`: público sobre las filas invadidas (3D y 2D) desde `MOB_START`; línea del ojeo, del mapa y del pregón.
- Tests: `Sim.Tests/Engine/MobNarrowingTests.cs`.

## Medición (29 sep 2026, provisional, sin medir)

Pendiente de rellenar en el mismo commit que cierra la implementación (Regla H: las cifras 1 fila y 15 % vienen de
RF-055b, no de una medición; lo que se mide es lo que hacen).
