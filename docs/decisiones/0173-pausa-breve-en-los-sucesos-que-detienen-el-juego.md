# ADR 0173 — Pausa breve en los sucesos que detienen el juego (sólo presentación)

Fecha: 30 sep 2026 · Estado: **aceptada** — **decisión del revisor** (29 sep, noche: *«pausar un poco en los eventos
que detienen el juego»*). Cierra **BB-D**. **No enmienda ningún requisito**: es presentación (RF-053 y RF-054 ya
paran el reloj en el motor; esto no toca un tick). **Relacionada:** ADR 0114 (cámara), 0119 y 0120 (el director agrupa
en `/Sim` y presenta en `/Game`; velocidades), ADR 0147 (la reanudación dura), ADR 0151 (celebración y cortinilla),
`docs/ui/README.md` §2, §4 y §6.

## Contexto

BB-D (16 sep) pedía *«parar unos segundos en los eventos que detienen el juego (gol, falta)»* y que los parones «no
gasten ticks sino que los añadan». El análisis de diseño de ese día (`docs/pendientes/BB-D.md`) ya concluyó que **lo que
hace falta es una pausa de presentación, y que va entera en `/Game`**: el partido está simulado, el render puede
quedarse en un fotograma sin que `/Sim` lo sepa, y meter «ticks de pausa» en el motor rompería lo que asume 1.200 ticks
de reglamento (RT-081, la curva de la ADR 0033) por un beneficio idéntico. Esta ADR **respeta ese análisis**.

Desde entonces el motor ganó dos cosas: la reanudación dura y para el reloj (ADR 0147: falta de tiro 20 ticks, córner y
saque de puerta 45) y el gol tiene su celebración y su congelado (ADR 0151, 2 s de director). **Lo que sigue sin tener
pausa de presentación** es el resto de lo que para el juego: la falta que el árbitro pita, la tarjeta y la lesión que
para el partido. Hoy el sello sale (N1/N2, 1-1,5 s) **mientras el juego sigue corriendo hacia la reanudación**: el
jugador ve la falta ya pasada.

## Decisión

1. **Pausa breve de 0,6 s a 1×** (`DirectorTimings.Hold`, **provisional, sin medir**, Regla H) en los sucesos que
   detienen el juego y no tienen congelado propio: **falta pitada, tarjeta amarilla y roja, lesión que para el partido**
   (leve, o grave del rival). El director congela el **fotograma anterior al suceso** (regla C.2 de la ADR 0119: en el
   del suceso el motor ya recolocó o retiró al implicado), enseña su sello y, pasada la pausa, salta al fotograma del
   suceso y sigue. La roja, que es una voz alta N3 y no congelaba (RF-054 la llama pausa dramática), habla **a la vez**.
2. **«El juego se ha parado» se lee de la traza, no se adivina por el tipo** (`Game/Match/PlayStops.cs`): el momento
   detiene el juego si en sus fotogramas el motor tiene pendiente **el saque de falta o el penalti que ese suceso provoca** (`MatchTrace.RestartAt`;
   un saque de banda, de puerta, de esquina o de centro en esos fotogramas no cuenta: el balón salió, el juego siguió,
   y pausar por él sería congelar sin que el suceso parara nada). Es el mismo
   dato con el que la vista 3D decide cómo se reanuda y lo escribe el motor con sus reglas (ADR 0143, 0147). Por eso
   una **falta que el árbitro no ve** —el juego sigue— no produce pausa, y un perk que anula el suceso tampoco.
   Una falta pitada en el mismo tick que un gol pierde su saque ante el de centro: no pausa (para el juego el gol,
   que tiene su congelado).
3. **Coherencia con el resto del director** (`docs/ui/README.md`):
   - **Una sola voz alta a la vez** (§2): no hay pausa mientras haya una voz alta en el escenario o en cola, y la pausa
     no es una voz (sólo lleva el sello, el canal callado).
   - **Velocidades** (§6): sólo a **×1**. A ×4 (retransmisión comprimida: sólo el gol, la N4 y las decisiones) y a ×16
     (sin pausa) no hay ninguna, y **cambiar de velocidad durante una pausa la corta**.
   - **Quien ya congela no se toca**: gol, muerte, final y decisión conservan su congelado. Tampoco el saque inicial, la
     turba ni el árbitro que se va (banda de pregón «sin congelar», §4), ni el consumible ni la sustitución (no paran nada).
   - **Residuo** (principio 5): durante la pausa el tablero, las tiras y el residuo del rival ya reflejan el suceso
     (`DirectorFrame.Held` alimenta `_residueMoment`, igual que la voz que pausa).
   - **Decisiones en vivo** (orden táctica y consumible manual, ADR 0154): no se pueden pulsar durante una pausa
     (`CanActNow` exige reproducción no congelada), igual que durante el gol: una decisión en el fotograma congelado
     entraría en el tick del suceso y re-simularía justo lo que se está enseñando.
   - **Pausas encadenadas** (revisión independiente): si el fotograma de congelado de una pausa queda por detrás del
     fotograma al que acaba de saltar la anterior, no hay segunda pausa; la imagen nunca retrocede.
4. **Sin dependencias con la tirada del destino** (ADR 0171, otra rama): no comparte estado ni código. Si esa ADR añade
   su propia presentación con pausa, entra en el director como una voz o un congelado más, y la regla de «una sola voz
   alta» ya decide quién manda.

## Las diez preguntas (`game-design-review`)

1. **Qué experimenta el jugador:** el pitido y la mano del árbitro llegan **a tiempo**: la imagen se queda un instante
   en la entrada, con el sello, y luego los jugadores caminan al saque, en vez de ver la falta ya pasada. 2-3. **Qué
   decide:** nada nuevo; es legibilidad (el análisis de BB-D). 4. **Regla:** ninguna (no hay RF de presentación); los
   principios *eventos explícitos > transiciones invisibles* y *que nada desaparezca sin explicación*. 5. **Sistemas:**
   sólo `/Game` (`PresentationDirector`, `PlayStops`, `BroadcastScreen`). `/Sim` no cambia: ni un tick, ni
   `MatchMomentView` (la política de velocidad `Present` sigue siendo la de la ADR 0119). 6. **Alternativas:** (B) ticks de
   pausa en el motor: descartada en el análisis de BB-D (rompe la duración de reglamento y las puertas); pausar **tras** el
   suceso en vez de antes: contra la regla C.2 y deja la pausa sobre el balón ya recolocado; una pausa por **tipo** de
   suceso (falta sí, lesión no) sin mirar la traza: se equivocaría con la falta no vista y con lo que un perk anula.
   7. **Trade-off:** ~+1,8 s por partido a cambio de legibilidad, y unos 0,6 s sin poder pulsar un botón. 8-9.
   **Estrategias y degeneración:** ninguna: no es una mecánica jugable. El riesgo propio es visual: apilar pausas
   (faltas seguidas) o congelar sobre otra voz; lo evita la regla de escenario libre y que la siguiente pausa sólo
   se abre cuando termina la anterior. 10. **Cómo se demuestra:** el censo de abajo, las tres capturas y una verificación
   del director, ahora **reproducible**: `Sim.Tests` enlaza `PresentationDirector.cs` y `PlayStops.cs` (como ya hacía la
   ADR 0171) y `PresentationDirectorHoldTests` prueba pausa y salto, ×4 y ×16, juego que no se para, voz en escenario,
   corte por cambio de velocidad, roja con voz, gol sin pausa, director sin política, pausas encadenadas sin retroceso
   y búsqueda hacia atrás; `PlayStopsTests` es el censo (120 partidos) con los dos casos de respuesta conocida.

## Implementación (30 sep 2026)

- `Game/Match/PlayStops.cs` (nuevo, sin Godot): `CanStopPlay(kind)` (falta, amarilla, roja, lesión leve, lesión grave)
  y `Holds(moment, trace)`, que además excluye lo anulado y mira los fotogramas del momento **sin margen**.
- `Game/Match/PresentationDirector.cs`: `DirectorTimings.Hold = 0,6`; el director recibe una función «¿detiene el
  juego?» (la pantalla le pasa `PlayStops.Holds` con su traza; sin ella se comporta como antes), un estado de pausa
  propio aparte del congelado de la voz, y `DirectorFrame.Held`.
- `Game/Screens/BroadcastScreen.cs`: crea el director con la política, lee el residuo de `Voice ?? Held` y expone
  `Frozen` al arnés de capturas. `BroadcastCapture` gana `-- pausa` (`pausa-1-congelada`, `pausa-2-sigue`,
  `pausa-3-reanuda`).

## Medición (300 partidos de referencia, sin lote de `/Balance`: no toca `/Sim`)

| | por partido |
|---|---|
| momentos que paran el juego y llevan pausa | **2,96** (cota superior: cuenta también la lesión grave propia, que ya congela) |
| de ellos, faltas pitadas | 2,63 de 3,61 momentos de falta (las otras 0,98 son las que el árbitro no ve; la falta con tarjeta es un momento de tarjeta, no de falta) |
| amarillas / lesión leve / roja / lesión grave | 0,19 / 0,06 / 0,04 / 0,06 |
| reloj de pared añadido a 0,6 s | **≤ +1,8 s** (+1,6 % sobre los 110,6 s de un partido a ×1) |

**Instrumento validado (Regla J), contra casos de respuesta conocida:** de las 298 faltas que el árbitro no vio, **0**
producen pausa; de las 784 que pitó, **todas menos las que un gol del mismo tick anula** (1 de 337 en el censo de
120 partidos). Con dos fotogramas de margen entraban 4 de las no vistas (1,3 %), así que se miran sólo los
fotogramas del propio momento. El censo vive ahora en `PlayStopsTests` (valla: 1,5-5 pausas por partido; medido 2,96).

**Los 0,6 s** salen de una regla, no de una medición: menos de la mitad del sello más corto (N1, 1 s), para que el sello
siga en pantalla cuando el juego se reanuda. Se ajusta viendo la build; el coste ya está medido (1,8 s por partido a 0,6 s;
2,4 s a 0,8 s; 3,0 s a 1 s).

## Queda abierto

- El **valor** de 0,6 s es del revisor: es lo que dice «un poco». Vive en `DirectorTimings.Default` y no en `/data`
  porque es de presentación.
- Un consumible manual **no se puede pulsar** durante la pausa: el botón se apaga ~0,6 s en cada falta pitada
  (unas tres por partido). No se ha medido si molesta.
