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
   ~1 s, un rótulo de pregón dice el porcentaje real («Se hace saber que X tiene un 12 % de partirle la crisma a Y»),
   y al resolverse: si cae, el estandarte de siempre; si se salva, un rótulo burlón («¡Se salva! Por los pelos»).
2. **Qué decide.** Nada durante el partido (principio 9): es información y drama. Sí **cierra el círculo de RF-012d**: el
   número que vio en el ojeo y en la alineación es el que ve caer.
3. **Qué debería decidir.** Lo mismo: que arriesgar a un herido en el campo tenga un rostro (el 30 % que se le ve encima).
4. **Regla.** RF-012d (todo lo malo, previsible), RF-093 (dos vías de muerte). **No inventa regla de juego**: sólo hace
   visible una tirada existente.
5. **Sistemas.** `/Sim`: el motor emite el evento con la probabilidad ya calculada (RT-014); `Sim.Run.View` lo agrupa en un
   momento presentable; `/Game`: director y pantalla lo pintan con los materiales del pregón. `/data`: nada (textos en
   `data/l10n/`).
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
   (x4: comprimida y sin ralentizar). (iv) `Prohibido morir` anula la muerte: el evento lleva la tirada, el `INJURY`/`DEATH`
   posterior lleva `:cancelled`, y la pantalla ya sabe leerlo.
10. **Cómo se demuestra.** (a) Determinismo: hash FNV de 150 semillas × 2 configuraciones de los eventos **sin**
   `FATE_ROLL`, idéntico antes y después (`4852152750119329266`, 75.685 eventos); (b) `Sim.Tests/Engine/FateRollTests`;
   (c) frecuencia en runs completas (`fateMoments`, `runs.csv`); (d) capturas de la retransmisión.

## Decisión

**Motor** (`/Sim`, sin tocar el RNG):

- Evento `FATE_ROLL` (`EventType.FateRoll`, **presentación pura** como `PERK_TRIGGERED` y `TEAMS_RESET`: ningún perk se
  cuelga de él, no narra en el log). `Actor` = quien se la juega, `Opponent` = quien tira, `Detail` =
  `severe|death:puntosBase:hit|saved`, con la probabilidad en **base 10.000** ya con todos los modificadores.
- Probabilidad: para la **reincidencia** (RF-093 vía 1, jugador alineado con lesión grave sin tratar) es la de lesionar
  —la lesión ya lo mata—; para el resto es la de que la lesión sea **grave**, `lesión × grave` (`chance × severeChance /
  10.000`); para un perk letal, la de `LethalChanceAgainst` (la misma que el indicador de riesgo). Todas las cifras ya
  estaban calculadas: el evento las anota, **no cambia el consumo de RNG**.
- Se emite también cuando **no pasa nada** (el «se salva»), por el mismo motivo.
- Umbrales (**constantes de presentación, no de balance**): `FateRollMinSevereBasisPoints = 800` (8 %) y
  `FateRollMinDeathBasisPoints = 1500` (15 %). Procedencia, Regla H: **medidos** (abajo).

**Vista** (`Sim.Run.View.MatchMomentView`): `MomentKind.Fate`, nivel N3, **sin pausa**, sólo sobre jugadores del equipo del
jugador (el némesis rival queda fuera: sería un rótulo sobre el rival, y la ADR 0165 ya le da su propio momento en el
saque y en la esquela). No se funde con el `INJURY`/`DEATH` que le sigue. Su `Frame` arranca `FateLeadFrames` (8, ~0,5 s de
partido) **antes** del tick, para que la cámara lenta preceda al resultado.

**Presentación** (`/Game`): a x1, el director enseña la voz `Fate` con una **escala de tiempo** de 0,5 mientras el
fotograma no llega al de la tirada (~1 s real), con el rótulo y un sello que gira; al llegar, el sello se detiene y dice
«¡Se salva!» o cede a la presentación normal de la lesión/muerte. A x4: comprimida, sin ralentizar. A x16: no se muestra
(residuo: el `INJURY`/`DEATH` de siempre).

## Frecuencia (medida)

`Balance --full-runs 30 --seed 11` (90 runs con las tres doctrinas, ~1.160 partidos), columna `fateMoments` de `runs.csv`
(FATE_ROLL sobre jugadores propios; instrumento nuevo `Sim.Analysis.FateMomentCounter`, comprobado contra el caso
sabido: en el partido de referencia sin desgaste de acto no hay ninguna al 8 %/15 %):

| Umbral | Momentos propios por partido |
|---|---|
| 4 % (todo) | 1,17 |
| 8 % (todo) | 0,82 (de ellos 0,64 de muerte) |
| sólo muerte al 15 % | 0,34 |
| **8 % grave + 15 % muerte (elegido)** | **0,53** |

El encargo fijó ~1 por partido como techo: con 4 % se pasaba. **Ojo:** la política automática alinea heridos sin tratar y
lo hace mucho más que una persona (que ve el aviso de RF-012c), así que la cifra es un **techo**, no la experiencia
típica; y las tiradas de muerte de un mismo herido se repiten (cada entrada sobre él es una). Las tres cifras de arriba
son **LIKELY** hasta medirlas con jugadores humanos.

## Consecuencias

- Los eventos de un partido pueden llevar `FATE_ROLL`; los consumidores exhaustivos (`MatchLogView`,
  `EventTypeNames`) lo conocen. Los partidos son **idénticos** (hash de eventos sin ellos).
- `runs.csv` gana la columna `fateMoments`.
- Pendiente: perfil del némesis (¿también el rótulo cuando el que se la juega es el rival?) — decisión de diseño para
  otra sesión.
