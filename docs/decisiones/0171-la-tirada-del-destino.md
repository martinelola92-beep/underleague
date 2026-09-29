# ADR 0171 — La tirada del destino

Fecha: 29 sep 2026 · Estado: **aceptada e implementada**. **Decisión del revisor** (`docs/plan-diversion.md` §0: «Tirada del
destino visible (tensión y humor): sí, sólo en tiradas graves o letales»); el diseño de detalle es propio, sin consultar,
dentro de esa autorización. **Requisitos:** RF-012d, RF-093, RT-014, RT-021, RT-024, RT-054. **Relacionadas:** ADR 0048
(un sano puede morir), ADR 0119/0120 (director de presentación), ADR 0134 (seguir jugando), ADR 0165 (némesis).

## Por qué

Hoy una lesión grave o una muerte «simplemente ocurre»: el jugador ve desaparecer a alguien y un estandarte de «Herido». El
principio 11 dice que todo lo malo debe haber sido **previsible**; la tirada ya lo es antes del partido (RF-012c), pero
durante él es invisible, y es justo el instante con más tensión y más humor del juego. Enseñar **el porcentaje real** de
la tirada convierte una desgracia en un suceso con suspense («¡tenía un 12 %!») y hace que la tirada que se salva también
cuente (`comportamiento observable > modificadores invisibles`, `eventos explícitos > transiciones invisibles`).

## Lo que había (Regla G)

`tools/existe-ya.sh` por *tirada / destino / probabilidad / suspense*: nada. El motor decide la lesión en
`MatchEngine.ResolveInjury` en **dos tiradas seguidas** —¿hay lesión? (`chance`) y, si la hay, ¿es grave? (`severeShare` ×
`Odds(SevereInjury)`)—; la muerte llega por dos vías, la reincidencia sobre una lesión grave sin tratar (RF-093 vía 1,
`Kill` tras `ResolveInjury`, **mata siempre** si la lesión cae) y el marcado de un perk letal (vía 2, `LethalRoll`). El
evento `INJURY`/`DEATH` sólo se emite **si la tirada sale mal**; el «se salva» no deja rastro. La probabilidad no viaja en
ningún evento y la retransmisión no puede calcularla (RT-014).

## Las diez preguntas

1. **Qué experimenta el jugador.** Cuando alguien **propio** se juega una lesión grave o la vida, el campo se ralentiza
   ~1 s, un rótulo de pregón dice el porcentaje real («Se hace saber: X ha dejado tocado a Y, y hay un 40 % de que sea grave»),
   y al resolverse: si cae, el estandarte de siempre; si se salva, un rótulo burlón («¡Se salva! Por los pelos»).
2. **Qué decide.** Nada durante el partido (principio 9): es información y drama. Sí **cierra el círculo de RF-012d**: el
   número que vio en el ojeo y en la alineación es el que ve caer.
3. **Qué debería decidir.** Lo mismo: que arriesgar a un herido en el campo tenga un rostro (el 30 % que se le ve encima).
4. **Regla.** RF-012d (todo lo malo, previsible), RF-093 (dos vías de muerte). **No inventa regla de juego**: sólo hace
   visible una tirada existente.
5. **Sistemas.** `/Sim`: el motor emite el evento con la probabilidad ya calculada (RT-014); `Sim.Run.View` lo agrupa en un
   momento presentable; `/Game`: director y pantalla lo pintan con los materiales del pregón. `/data`: nada (textos en
   `UiText`).
6. **Alternativas.** (a) Recalcular el porcentaje en `/Game`: **descartada**, duplica la fórmula del motor y la separa de
   la tirada real (justo lo que RT-014 y la ADR 0048 evitan). (b) Anunciar **todas** las lesiones: descartada, ruido; la
   pausa perdería su valor. (c) Un evento por tirada **previo** a la tirada: descartada, obligaría a emitir tirando
   antes de conocer el desenlace o a cambiar el orden del RNG. Elegida: **un evento `FATE_ROLL` emitido tras las tiradas**,
   en el mismo tick, con probabilidad y desenlace; la retransmisión lo presenta *antes* porque ve la secuencia ya jugada
   (simulación ≠ reproducción, docs/ui/README §3) y arranca su cámara lenta unos fotogramas antes del tick.
7. **Trade-off.** Cuesta un evento por tirada notable en la traza (raro, ver frecuencia) y una presentación más en la voz
   alta. Coste de oportunidad de la voz alta: el rótulo es N3 **sin congelar** (como la turba), así que cede ante
   cualquier N4 o decisión (una sola voz alta).
8. **Estrategias nuevas.** Ninguna mecánica. Refuerza «reducir el riesgo con la alineación» (condición 3 de la ADR 0048):
   el jugador ve cuánto le costó no hacerlo.
9. **Degeneración.** (i) Spam: se evita con el umbral y midiéndolo (abajo). (ii) Un perk letal que dispara muchas tiradas
   seguidas: el umbral por probabilidad las filtra y la cola de voces caduca. (iii) A x16 no debe pararse: no se muestra
   (x4: comprimida y sin ralentizar). (iv) `Prohibido morir` o una puerta que anule la lesión: el desenlace se decide **tras la
   cancelación** y cuenta como salvada (corregido tras la revisión independiente: antes se anunciaba como golpe).
10. **Cómo se demuestra.** (a) Determinismo: hash FNV de 150 semillas × 2 configuraciones de los eventos **sin**
   `FATE_ROLL`, idéntico antes y después (`4852152750119329266`, 75.685 eventos; se repite tras la enmienda); (b) `Sim.Tests/Engine/FateRollTests`;
   (c) frecuencia en runs completas (`fateMoments`, `runs.csv`); (d) capturas de la retransmisión.

## Decisión

*(Enmendada el mismo día tras la revisión independiente: tres fallos graves de la primera versión, corregidos abajo.)*

**Motor** (`/Sim`, sin tocar el RNG):

- Evento `FATE_ROLL` (`EventType.FateRoll`, **presentación pura** como `PERK_TRIGGERED` y `TEAMS_RESET`: ningún perk se
  cuelga de él, no narra en el log). `Actor` = quien se la juega, `Opponent` = quien tira, `Detail` =
  `severe|death:puntosBase:hit|saved`, con la probabilidad **de ese dado**, en base 10.000 y con todos sus modificadores.
- **Qué dado se anuncia.** `severe` es el **segundo** dado de la lesión, «¿leve o grave?» (`severeChance`, ya con los
  canales de perks y órdenes), y sólo existe si hubo lesión; salvarse de él es **salir con una leve** (o con la lesión
  anulada por un perk). `death` es la tirada que mata: la de lesionar a quien ya venía herido (reincidencia, RF-093 vía 1,
  que mata siempre si cae) o la del perk letal (`LethalChanceAgainst`, la misma que el indicador de riesgo). La primera
  versión anunciaba `lesionar × grave` y sólo emitía el «se salva» cuando no hubo lesión: el rótulo decía 20 % y al jugador
  le caía el 37 %. Ahora el número es el del dado que se tira (test de honestidad: la fracción de golpes ≈ la media de los
  porcentajes anunciados).
- **El desenlace se decide tras la cancelación**: el evento se emite **después** del `INJURY`/`DEATH` del mismo tick, y un
  evento anulado (`iron_gate`, `no_dying`) cuenta como **salvada**. Un `hit` es siempre una lesión grave o una muerte que
  ocurrieron de verdad.
- **La muerte se anuncia una sola vez por jugador y partido** (la primera): un herido alineado se juega la vida en cada
  entrada (medido: 444 tiradas por 200 muertes) y repetir el rótulo era ruido.
- Umbrales (**constantes de presentación, no de balance**, procedencia en «Frecuencia»):
  `FateRollMinSevereBasisPoints = 4000` (el 40 % de `severeShare`: hoy anuncia toda lesión propia, porque el porcentaje
  base ya es alto) y `FateRollMinDeathBasisPoints = 1500` (15 %). **Provisionales.**

**Vista** (`Sim.Run.View.MatchMomentView`): `MomentKind.Fate`, nivel N3, **sin pausa**, sólo sobre jugadores del equipo del
jugador (el némesis rival queda fuera). Es **transparente para la fusión**: se anota aparte y el siguiente evento se
funde con el momento anterior al Fate como si no estuviera (con el Fate en medio, la lesión dejaba de fundirse con su
falta y su tarjeta, y la roja se perdía). Su `Frame` arranca `FateLeadFrames` (8, ~0,5 s de partido) **antes** del tick.

**Presentación** (`/Game`; textos en `UiText`, no en `data/l10n/`):

- A x1, el director da la voz `Fate` con escala de tiempo 0,5 (~1 s real) mientras el fotograma no llega al de la tirada;
  la banda de pregón enseña el porcentaje que trae el motor y un sello que gira. A x4: comprimida, **sin ralentizar y sin
  adelantar el resultado** (rueda hasta su fotograma). A x16: no se muestra. Cambiar de velocidad en plena cámara lenta
  restaura la escala. Una búsqueda del director (`Seek`, p. ej. tras una decisión de sustitución) no descarta una tirada
  cuyo fotograma de tirada aún no ha pasado.
- **El desenlace de un golpe real lo cuenta la propia presentación de la lesión o la muerte**, con «los dados lo han
  querido» en su estandarte o en su bando; **la banda del destino sólo enseña un resultado cuando se salva**
  («Sólo un rasguño: se libra de la grave por los pelos», «¡SE SALVA! Por los pelos»). La primera versión intentaba
  contar el golpe en la banda, que la pausa de la lesión tapaba siempre, y sólo lo escribía cuando era falso (evento anulado).

## Frecuencia (medida)

`Balance --full-runs 30` con las semillas 11, 12 y 13 (90 runs por semilla con las tres doctrinas), columna `fateMoments`
de `runs.csv` (`FATE_ROLL` sobre jugadores propios; instrumento `Sim.Analysis.FateMomentCounter`, comprobado contra un caso
de respuesta sabida y no nula en `FateRollTests`):

| Umbrales (grave / muerte) | Momentos propios por partido, por semilla | Media ± error típico |
|---|---|---|
| **4000 / 1500 (elegido, provisional)** | 0,677 · 0,713 · 0,618 | **0,67 ± 0,03** |

Con la emisión corregida el techo de ~1 por partido se cumple sin subir el umbral. Las cifras de la primera versión
(1,17 / 0,82 / 0,53 por partido con la tirada compuesta) **ya no valen**: medían otro dado. **Provisional, sin medir con
jugadores humanos:** la política automática alinea heridos sin tratar más que una persona, y en actos tardíos toda lesión
propia es una tirada anunciada; si se siente como demasiado, subir el umbral de `severe` por encima de 4000 la limita a
las lesiones con el riesgo de gravedad elevado por perks u órdenes.

## Consecuencias

- Los eventos de un partido pueden llevar `FATE_ROLL`; los consumidores exhaustivos (`MatchLogView`,
  `EventTypeNames`) lo conocen. Los partidos son **idénticos** (hash de eventos sin ellos).
- `runs.csv` gana la columna `fateMoments`.
- `Sim.Tests` enlaza `Game/Match/PresentationDirector.cs` (no toca Godot) para probar el ritmo del director.
- Pendiente: rótulo cuando el que se la juega es el némesis rival — decisión de diseño para otra sesión.
