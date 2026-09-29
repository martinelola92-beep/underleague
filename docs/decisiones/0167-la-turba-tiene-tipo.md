# ADR 0167 — La turba tiene tipo, y se puede provocar

Fecha: 29 sep 2026 · Estado: **aceptada** (decisión de diseño tomada sin consultar dentro de lo que el revisor
fijó; ver «Qué decidió el revisor»). **Requisitos:** RF-012d, RF-055b, RF-055d, RF-082, RF-083, RF-085, RT-014,
RT-021, RT-022, RT-035. **Relacionada:** ADR 0048 (las cinco condiciones de la muerte), ADR 0158 (el árbitro
existe), ADR 0166 (los gritos cambian la orden: la maquinaria de orden y presión que esta ADR reutiliza),
Knavall F7 («turba real», bloqueada por el gate 5), `docs/plan-diversion.md` §0 y §5.8.

## Qué decidió el revisor (plan de diversión, 27 sep)

- **Turba**: «tipos distintos. Se anuncia el **tipo** ("salta uno y lesiona a uno"), **no la víctima**».
- **Consumibles**: «son el eje de la agencia durante el partido. Gritos y **provocar la turba** son consumibles».

## Lo que había (Regla G)

`tools/existe-ya.sh turba mob`: la turba es la prórroga a gol de oro tras un empate (`MOB_START` +
`REFEREE_LEAVES`, fase `MobGoldenGoal`, `IsMob`), **sin árbitro** (ni faltas, ni tarjetas, ni criterio: ADR
0158 §4). El estrechamiento, las casillas invadidas y el +15 % de velocidad de RF-055b **no están
implementados** (F7: «hoy es una etiqueta»; `Pitch.Rows` es constante). La interfaz ya la anuncia (pregón «el
árbitro abandona el campo», momento `Mob`, sonido de abucheo) y hay un disparador de consumible `mobStart`.

## Decisión

1. **Cada partido de catálogo trae un tipo de turba**, sorteado **antes** del partido con un flujo propio de la
   run (`OfferStream`, desplazamiento 9600, nunca el de partido: RT-022) y **derivado del nodo, no guardado**,
   como el árbitro (`IRunSystems.RefereeFor`). El ojeo y el mapa lo enseñan: «si hay empate, la turba: *Salta uno y lesiona a uno*». Se anuncia el
   tipo, nunca la víctima.
2. **Catálogo inicial de cuatro tipos**, en `data/mobs/*.json`, con peso de sorteo y efectos de datos:

   | id | nombre | al entrar la turba | provocada (consumible) |
   |---|---|---|---|
   | `plain` | La grada ruge | nada más que lo de siempre (sin árbitro, gol de oro) | nada: el consumible no sirve, y se sabe antes |
   | `invader` | Salta uno | un jugador de campo **al azar de los dos equipos** sale con lesión **leve** | lo mismo, ahora, con el árbitro en el campo |
   | `frenzy` | Frenesí | los dos equipos presionan al portador toda la turba (consigna `Press` de la ADR 0166) | los dos presionan 10 s |
   | `their_roar` | Su grada empuja | el rival juega la turba en orden ofensiva (orden de la ADR 0166) | el rival se vuelca 10 s |

   Primitiva nueva: **`mobInjury`** (lesión leve a un jugador de campo en el campo, al azar con el RNG del
   partido, sin autor). **La turba lesiona, no mata**: la lesión es siempre leve y nunca dispara la muerte por
   reincidencia de un lesionado grave (RF-012d y ADR 0048: la muerte sigue viniendo sólo de una entrada o de un
   perk). Los otros dos efectos reutilizan `StartShout` de la ADR 0166 con duración «hasta el final» al entrar
   la turba y 10 s provocada.
3. **Consumible «Provocar a la grada»** (`rile_the_crowd`, manual, táctico/sucio): aplica **ahora** el efecto del
   tipo de turba de este partido, con el árbitro todavía en el campo. No inicia la prórroga ni cambia el
   marcador; un solo uso (RF-085). Con `plain` no hace nada, y el jugador lo sabe antes de equiparlo.
4. **Descripciones** desde el efecto (RT-035), es/en; el tipo se anuncia con su nombre y su efecto generado.
5. **Determinismo**: el tipo es estado inicial del partido (`MatchSetup`); la víctima de `mobInjury` sale del
   RNG del partido en el tick de la entrada (o de la activación), con candidatos en orden de id (RT-041).

## Las diez preguntas (`game-design-review`)

1. **Qué experimenta el jugador.** Antes del partido sabe qué turba le espera si empata; al empatar, ocurre lo
   anunciado (alguien sale cojeando, todos se muerden, el rival se vuelca). Durante el partido puede provocarla.
2. **Qué decide.** Si le conviene empatar (una build de violencia quiere `frenzy`; una plantilla corta teme
   `invader`; una de contraataque quiere `their_roar`); si equipa «Provocar a la grada» y cuándo la usa.
3. **Qué debería decidir.** Lo mismo: RF-055d ya dice que la turba es una ventana que unos buscan y otros evitan;
   el tipo la hace legible y distinta en cada partido.
4. **Regla.** RF-055b/055d (la turba), RF-082 (el manual). Regla nueva: el **tipo de turba** y su anuncio; y
   «la turba lesiona, no mata». No toca RF-055b en lo que sí está implementado; el estrechamiento sigue sin hacer
   (F7) y no se decide aquí.
5. **Sistemas.** `/data`: `data/mobs/*.json` + esquema, consumible nuevo. `/Sim`: catálogo de turbas, tipo en el
   nodo y en `MatchSetup`, efecto `mobInjury` y `provokeMob`, aplicación en `MOB_START`. `/Game`: ojeo, mapa,
   pregón de la turba con el tipo, tablero. La lógica sólo en `/Sim` (RT-014); `/Game` lee una vista.
6. **Alternativas.** (a) Implementar RF-055b tal cual (estrechar y casillas invadidas): exige `Pitch`
   parametrizable y es F7, bloqueada. (b) Víctima anunciada: rechazada por el revisor. (c) Provocar = empezar la
   prórroga ya: rompe RF-055c/055b (la turba sólo con empate) y dejaría sin árbitro medio partido.
7. **Trade-off.** El empate deja de ser neutro: cada tipo reparte ventaja según la build. `invader` es azar
   simétrico (cualquiera de los dos equipos): coste para quien juega con banquillo corto. Provocar gasta el slot
   manual, que compite con los gritos.
8. **Estrategias.** Da a las builds violentas un motivo para buscar el empate con `frenzy`, a las técnicas uno
   para evitarlo, y a la orden táctica un uso nuevo (cerrar el partido para no llegar a la turba).
9. **Degeneración.** (a) `invader` suma lesiones: cabe en el presupuesto sólo si la turba es rara; se mide
   `injuriesPerMatchBothTeams` y `mobShare`. (b) Provocar `invader` en bucle: un solo uso. (c) Provocar
   `frenzy` con árbitro presente sube faltas y tarjetas de los dos: se mide. (d) **Dependencia del gate 5** de
   Knavall (¿el desgaste es de run o de acto?): esta ADR no lo decide; añade como mucho una lesión leve por
   partido con turba, y se mide contra la línea base.
10. **Cómo se demuestra.** Tests: el tipo se sortea igual con la misma semilla y nunca con el flujo de partido;
    cada tipo hace lo anunciado al entrar la turba (una lesión leve y sólo una; presión de los dos; orden
    ofensiva del rival) y nada antes; `mobInjury` nunca mata ni lesiona al portero; provocar aplica el efecto en
    su tick y `plain` no hace nada; determinismo. Lote `/Balance` con y sin tipos (`injuriesPerMatchBothTeams`,
    faltas, tarjetas, `runWinRate`, `deathsPerRun`). Capturas del ojeo con el tipo y de la retransmisión al
    entrar la turba.

Pesos de sorteo iniciales (provisionales, sin medir, Regla H): `plain` 40, `invader` 20, `frenzy` 20,
`their_roar` 20.

## Revisión de arquitectura (`architecture-review`)

- **Patrón existente**: el árbitro de la ADR 0158 se deriva del nodo (`IRunSystems.RefereeFor`) y viaja en
  `MatchSetup`; el tipo de turba hace lo mismo: `IRunSystems.MobFor(state, node, catalog)` → `MobSetup` en
  `MatchSetup.Mob` (propiedad `init`, por defecto `null` = sin tipo). Sin estado nuevo en la run ni subida de
  guardado: misma semilla y mismo nodo, mismo tipo.
- **Frontera**: el motor recibe el tipo ya resuelto (`MobSetup`: id y efectos) y no consulta ningún catálogo.
  `/Game` lee el tipo de una vista (`MobView`: nombre y texto generado) y el efecto de los eventos
  (`MOB_START`, `INJURY` sin autor, `CONSUMABLE_USED`); nunca lo recalcula (RT-014).
- **Complejidad**: no se crea maquinaria de conducta; la presión y la orden reutilizan `StartShout` (ADR 0166). Lo
  único nuevo en el motor es la lesión sin autor de la turba.
- **Determinismo**: con `Mob` nulo o `plain` el motor no consume ni un número más del RNG: los partidos sin tipo
  son byte a byte los de antes (RT-024). La víctima sale del RNG del partido entre los jugadores de campo en el
  campo, ordenados por id (RT-041). El sorteo del tipo usa `OfferStream` (RT-022).
- **Segundo orden**: una `INJURY` sin autor (`Opponent` −1) ya la tratan `MatchResolution`, los créditos de rival
  (sin índice) y los némesis (sin causante) como una baja sin culpable; se comprueba con test. La lesión es
  cancelable por perks como cualquier `INJURY` y respeta la decisión de seguir jugando (ADR 0134 E).
- **Paralelismo**: ninguno nuevo.

## Implementación (29 sep 2026)

- `/data`: `data/mobs/mobs.json` (+ esquema), consumible `rile_the_crowd` (efecto `provokeMob`, 10 s), plantillas
  `provokeMob` y `mob*` es/en.
- `/Sim`: `MobSetup` en `MatchSetup.Mob`; `IRunSystems.MobFor` (sorteo en `MobCatalog.For`, `OfferStream` 9600);
  `MatchEngine.ApplyMob` al entrar la turba y `ProvokeMob` desde `EffectEngine`; `MobInjure` (lesión leve sin autor);
  `MatchShoutView` refleja la turba y la provocación de cualquiera de los dos equipos; `MobView` para anunciar.
- `/Game`: ojeo («SI HAY EMPATE, LA TURBA»), mapa («turba: …»), pregón de la turba con el tipo y placa del tablero
  «Frenesí · hasta el final». Capturas: `Game/screenshots/retrans-turba.png`, `ojeo-apuesta-sin-tomar.png`.
- Tests: `Sim.Tests/Engine/MobTests.cs` y `NemesisTests.AMobInjuryHasNoAuthor…`.

## Revisión independiente (29 sep 2026)

Confirmó: sin tipo o con `plain` el partido es byte a byte el de antes; el sorteo es de la run; la víctima sale del
RNG de partido entre candidatos ordenados, sin portero, sin autor y sin matar; seguir jugando se respeta; la vista y
el motor coinciden. Arreglado, con test:

- **Grave — la turba curaba una lesión grave**: una lesión leve sobre un lesionado grave lo dejaba en leve al
  resolver el partido. Ahora una lesión nunca mejora el estado (`MatchResolution`), lo que protege también a
  cualquier futura lesión leve que no pase por `ResolveInjury`.
- **Provocar con la turba ya dentro** duplicaba la lesión o acortaba el frenesí a 10 s: ahora no hace nada.
- **«Hasta el final» no lo era**: un grito de presión durante el frenesí apagaba la presión de un solo equipo al
  acabar. La turba impone ahora la conducta **de base**; los gritos van encima y vuelven a ella.
- El desplazamiento 9600 queda en la tabla de `OfferStream`.

Anotado sin cambiar (decisiones tomadas):

- **Apuestas**: una lesión de la turba sobre la estrella rival cumple «Cazar a la estrella», y «Ojo por ojo» /
  «Sangre antes que goles» cuentan la lesión propia sin autor. Se deja así: las apuestas miden lo que pasa, no el
  mérito, y el tipo de turba se conoce antes de apostar.
- **La lesión de la turba puede dejar la plantilla bajo el mínimo** y terminar la run: es daño anunciado (el tipo
  sale en el ojeo y el mapa antes del partido), como cualquier lesión (RF-012d).
- **`/Balance`**: la política equipa `rile_the_crowd` como condicional (familia sucia → `scoreBehind`), un uso que la
  ADR no describe; el manual nunca se pulsa en la política. La medición de campaña mide eso.
- **«Frenesí» sube entradas sin árbitro**: más lesiones y muertes por la vía normal. «La turba lesiona, no mata» vale
  para su lesión propia, no para lo que pasa jugando; la condición 5 de la ADR 0048 (la muerte es rara) se mide abajo.
- **El invasor no se ve**: nadie salta al campo, sólo cae un jugador en el tick del pregón. Candidato a un gesto
  visual (una figura de la grada) cuando haya arte; hoy lo explica el pregón.
- **El texto del consumible es genérico** (remite al tipo del partido): se compra sin saber para qué partido sirve.
  Es el precio de que el tipo sea del partido y no del consumible.

## Medición de campaña (29 sep 2026)

`/Balance --full-runs 600 --seed {1,2}`, copias congeladas de la rama de clanes (ADR 0165) y de esta ADR ya revisada.

| métrica | antes s1 / s2 | con tipos de turba s1 / s2 |
|---|---|---|
| `injuriesPerMatchBothTeams` | 1,17 / 1,16 | 1,23 / 1,25 |
| `deathsPerRun` | 2,17 / 2,26 | 2,22 / 2,27 |
| `runWinRate` | 18,00 / 18,67 | 15,50 / 18,33 |
| `bossWinRateAct3` | 42,7 / 45,9 | 37,5 / 44,0 |
| `injuredAtEnd` (s2) | 0,59 | 0,70 |

**Lectura** (Regla F):
- **LIKELY: la turba con tipo sube las lesiones un 5-8 %** (+0,06 / +0,09 por partido, mismo signo en las dos
  semillas). Es lo esperado del invasor y del frenesí, y consume presupuesto de lesiones (gate 5 de Knavall, sin
  decidir): queda anotado para el balance.
- **LIKELY: no mueve las muertes** (+0,05 / +0,01): la condición 5 de la ADR 0048 se mantiene.
- **`runWinRate` baja 2,5 / 0,3**: mismo signo, pero la media (−1,4) está dentro del error típico de la diferencia
  (≈ 2,2 puntos). Sin conclusión; nada cambia de estado.
- `actsWithAllSinksAffordable` sale ahora 0 / 0,08 (antes 0,08 / 0): cambia de semilla con cualquier cambio que
  reordene las runs, lo que **refuerza** la lectura de cola de la roja nueva de la ADR 0165.

**Puertas completas** (29 sep 2026): **3 rojas de 45**, las mismas tres de `main` (curva de jefes
`eternal_crown_excellent` 41,6; `orc_violence` ×2, 53,4). La roja de cola que había dejado la ADR 0165
(`TheGoldOfAnActPaysTwoOrThreeSinksAndNeverAllOfThem`) vuelve a verde con este cambio, que reordena las runs: refuerza
que era una cola y no un aumento del oro.
