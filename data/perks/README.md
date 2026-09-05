# Catálogo de perks (Paquete T, bloque de rediseño espacial)

40 perks en el formato de `docs/fase1b-diseno.md` §1.4: `value` en puntos porcentuales enteros de la escala 5/10/15/20/25/50, campo `axis` (uno de los ocho ejes de `docs/perks-ejes.md`), campo `race` (`null` = universal) y `links` para los perks de alineación. No incluye las 5 habilidades raciales (`quick_learner`, `hot_blooded`, `elf_touch`, `roots`, `numb`), que hace el paquete S en paralelo. Ningún perk universal condiciona por etiqueta de especie (ADR 0023); la variación individual dentro de una raza se expresa por etiqueta de estilo (`Brute`, `Fine`, `Bulwark`, `Cold`, `Neutral`, ADR 0024). Ningún perk mejora el pase directamente (canal saturado, `docs/balance/fase1-perks.md`): la circulación se expresa a través de `intercept`.

Distribución RF-069 (60/30/10 ± 8): con los cuatro letales de la ADR 0046 el catálogo pasa a **44 perks**: 24 filler (54,5%), 14 conditional (31,8%) y 6 ruleBreaker (13,6%), los tres dentro de la tolerancia de ±8.

Distribución por eje (`docs/perks-ejes.md`, sobre los 36 perks universales; el 10% exclusivo de raza no compite por la cuota):

| eje | perks | % | objetivo |
|---|---|---|---|
| identidad (`identity`) | 5 | 13.9% | 15% |
| acumulación (`accumulation`) | 7 | 19.4% | 20% |
| alineación (`alignment`) | 5 | 13.9% | 15% |
| zona de inicio (`startZone`) | 4 | 11.1% | 10% |
| geometría (`geometry`) | 5 | 13.9% | 15% |
| estado del partido (`matchState`) | 4 | 11.1% | 10% |
| composición (`composition`) | 2 | 5.6% | 5% |
| proximidad (`proximity`) | 4 | 11.1% | 10% |

Rareza: 16 `common`, 19 `rare`, 5 `legendary` (sobre 40).

`accumulatesAcrossMatches: true`: 7 perks (mínimo exigido: 7). Todos escalan sobre un canal con recorrido (`intercept`, `injure`, `save`, `shotOnTarget`, `dribble`, `leash`) o, en el caso de `road_warrior`, cruzan un umbral intramatch con `stat()` sin declarar contador propio.

`elseEffects` con castigo real (no un `-3` simbólico, un `-5`/`-10`/`-15` en la misma escala que el efecto): 17 perks.

## Arcos de build: líneas y maestros (ADR 0051)

Cuatro **líneas** (`family` en el fichero del perk, declaradas en `data/build/arcs.json`) con siete piezas
cada una, y **cuatro maestros**, uno por línea. Los maestros son el 6,6% del catálogo, dentro del 5-10%
que la ADR acota: si crecen más, el catálogo deja de ser un roguelite de piezas sueltas.

| línea | nombre visible | piezas | maestro | exige | cierra |
|---|---|---|---|---|---|
| `wall` | La Muralla | `bulwark_stance`, `own_third_anchor`, `last_ditch`, `back_to_back`, `pit_veteran`, `game_management`, `safety_net` | `granite_line` | 2 de La Muralla | La Puntería |
| `craft` | El Toque | `fine_touch`, `steady_hands`, `silky_veteran`, `flank_specialist`, `wing_overlap`, `crowd_control`, `fine_orchestra` | `first_touch_school` | 2 de El Toque | La Carnicería |
| `aim` | La Puntería | `box_predator`, `long_range_menace`, `cold_focus`, `forward_line`, `sharpshooter_drill`, `poacher_instinct`, `spearpoint` | `killing_range` | 2 de La Puntería | La Muralla |
| `butchery` | La Carnicería | `bruised_knuckles`, `shadow_marker`, `scar_tissue`, `brute_boots`, `pack_mentality`, `iron_studs`, `marrow_thirst` | `blood_tithe` | 2 de La Carnicería | El Toque |

Las líneas se cierran **por parejas**: La Muralla contra La Puntería y El Toque contra La Carnicería. Una
run puede cerrar como mucho **dos** arcos, uno de cada pareja, y las dos combinaciones posibles
(`granite_line` + `first_touch_school`, `killing_range` + `blood_tithe`) son las dos builds catalogadas
`human_granite` y `human_bloodrange`, que no comparten un solo perk.

**Un maestro solo se compra** (ADR 0055): no sale nunca como recompensa por ganar. Es lo que hace del
mercado parte del núcleo de la build —sin pasar por uno, el objetivo de la línea no existe— y lo que
convierte a las 28 piezas de línea (`frequency: 150`) en el camino hacia algo en vez de en piezas sueltas.

El bloqueo mira **hacia adelante**: lo que ya se lleva sigue funcionando —un perk no se puede retirar
(RF-072), así que apagarlo sería borrar algo ya pagado— y lo que desaparece es la posibilidad de
conseguir más de esa línea en lo que queda de run. Se anuncia en la descripción generada (RT-035) y en la
pantalla de recompensa antes de aceptar (RF-012d).

## Profundidad nativa (ADR 0051)

Cada perk declara `minAct` (el acto en el que empieza a aparecer) y, opcionalmente, `frequency` (el
*commonness* de Angband, 100 = lo normal). La curva que traduce la distancia al acto nativo en peso está
en `data/build/arcs.json`.

| acto nativo | perks | criterio |
|---|---|---|
| 1 | 47 | todo lo demás: el acto 1 es el taller (ADR 0043) |
| 2 | 10 | los `rare` y los cuatro maestros (`frequency: 300`, y un 20% de eso mientras les falte una pieza) |
| 3 | 4 | los letales: `iron_studs`, `marrow_thirst`, `second_wound`, `skullsplitter` |

Un perk **por debajo** de su acto nativo sale con el 12% de su peso (3% dos actos por debajo): la
aparición *fuera de profundidad*, rara y memorable. Un **maestro no la tiene**: no aparece nunca por
debajo del acto 2, y solo entra en el pool cuando a la run le falta como mucho una pieza de su línea.

## Perks

| id | eje | raza | rareza | tipo | trigger | resumen | builds |
|---|---|---|---|---|---|---|---|
| `back_to_back` | proximidad | — | common | filler | TACKLE | +15% de robo al entrar si tiene cerca un Muro propio | `human_random` |
| `battle_reader` | acumulación | — | rare | filler | MATCH_START | +5% de interceptar por cada partido jugado en la run (máx. 25%, acumula) | `elf_glass`, `elf_tiki_taka`, `human_counter`, `orc_giants`, `orc_violence`, `undead_grind` |
| `box_predator` | geometría | — | common | filler | SHOT | +20% de tiro a puerta al tirar cerca de portería | `human_counter`, `orc_giants`, `orc_violence`, `undead_grind` |
| `brute_boots` | identidad | — | common | filler | MATCH_START | +15 de fuerza si es Bruto; si no, -10 (adorno para quien no encaje) | `elf_brawler`, `orc_giants`, `orc_mob`, `orc_violence` |
| `bulwark_stance` | identidad | — | common | filler | MATCH_START | +15% de robo si es Muro; si no, -10% | `dwarf_fortress`, `elf_brawler`, `elf_bulwark` |
| `center_conductor` | zona de inicio | — | rare | conditional | MATCH_START | +10% de interceptar a todo el equipo si empieza en el carril central; si no, -10 de técnica | — |
| `clean_sheet_legacy` | acumulación | — | rare | filler | SAVE | +5% de parada por cada parada en la run (máx. 25%, acumula); solo porteros | — |
| `cold_focus` | identidad | — | common | filler | SHOT | +15% de tiro a puerta si es Frío | `undead_grind` |
| `comeback_spirit` | estado del partido | — | rare | conditional | PLAY_START | +10 de fuerza mientras el equipo va perdiendo | `human_counter`, `human_random` |
| `covering_shadow` | alineación | — | rare | conditional | MATCH_START | +20% de interceptar al compañero de detrás; sin él, -15% para el portador | — |
| `crowd_control` | proximidad | — | rare | conditional | DRIBBLE_ATTEMPTED | +20% de regate al encarar si tiene cerca a un rival Bruto | `elf_glass`, `elf_tiki_taka` |
| `deathless_march` | acumulación | Undead | legendary | filler | MATCH_START | exclusivo no-muerto: +5% de robo a todo el equipo por cada partido jugado en la run (máx. 25%, acumula) | `undead_grind` |
| `diagonal_press` | alineación | — | common | filler | TACKLE | +10% de robo al compañero en diagonal al entrar; sin él, -5 de fuerza | — |
| `fine_orchestra` | composición | — | rare | conditional | MATCH_START | +15% de regate a los Finos si hay más de dos en el equipo; si no, -10 de técnica; solo Finos | `elf_tiki_taka` |
| `fine_touch` | identidad | — | common | filler | MATCH_START | +15 de técnica si es Fino; si no, -10 | `elf_glass`, `elf_tiki_taka` |
| `flank_specialist` | zona de inicio | — | common | filler | MATCH_START | +15% de regate si empieza en una banda; si no, -10 de velocidad | — |
| `forward_line` | zona de inicio | — | rare | filler | MATCH_START | +20% de tiro a puerta si empieza en el tercio rival; si no, -15% | `elf_glass`, `elf_out_of_zone` |
| `game_management` | estado del partido | — | common | filler | TACKLE | +15% de robo al entrar mientras el equipo va ganando | `human_counter` |
| `gentle_giant` | alineación | Orc | legendary | conditional | MATCH_START | exclusivo orco: +20% de interceptar al compañero de delante; sin él, -10 de fuerza | `orc_giants` |
| `high_press_trigger` | geometría | — | rare | conditional | RECOVERY | +15% de interceptar a todo el equipo si un compañero recupera en el tercio rival | — |
| `home_ref` | estado del partido | — | rare | conditional | FOUL | mejora el criterio del árbitro mientras el equipo va perdiendo | `human_random`, `orc_mob` |
| `iron_gate` | identidad | Dwarf | legendary | ruleBreaker | GOAL | exclusivo enano: anula el primer gol que le marcan (1 por partido); solo porteros | `dwarf_fortress` |
| `last_ditch` | geometría | — | rare | conditional | TACKLE | +15% de robo al entrar en su propio tercio; si no, -10% | `undead_grind` |
| `long_leash_legacy` | acumulación | — | legendary | filler | MATCH_START | +5 de correa cada 4 partidos jugados en la run (máx. 10, acumula) | — |
| `long_range_menace` | geometría | — | common | filler | SHOT | +15% de tiro a puerta al tirar de lejos | `human_counter` |
| `mob_instigator` | estado del partido | — | legendary | ruleBreaker | FOUL | anula la falta de un compañero en la turba (máx. 2 por partido) | `orc_mob` |
| `natural_leader` | identidad | — | rare | conditional | MATCH_START | +10% de robo a todo el equipo; solo Líderes | — |
| `own_third_anchor` | zona de inicio | — | common | filler | MATCH_START | +15% de robo si empieza en su propio tercio; si no, -10% | `dwarf_fortress`, `elf_bulwark`, `elf_out_of_zone`, `human_wall` |
| `pack_mentality` | composición | — | rare | conditional | MATCH_START | +10 de fuerza a los Brutos si hay más de dos en el equipo; si no, -5 para el portador | `elf_brawler`, `orc_mob` |
| `pivot_duo` | alineación | — | common | filler | MATCH_START | +15% de robo al compañero de su columna; sin él, -10% para el portador | `human_scattered`, `human_wall` |
| `poacher_instinct` | acumulación | — | rare | filler | GOAL | +5% de tiro a puerta por cada gol marcado en la run (máx. 25%, acumula) | `orc_misplaced`, `orc_violence` |
| `road_warrior` | acumulación | — | rare | conditional | RECOVERY | tras 5 entradas ganadas en el partido, +10% de robo a todo el equipo | `orc_giants`, `orc_mob`, `orc_violence` |
| `safety_net` | proximidad | — | common | filler | SAVE | +15% de parada si tiene cerca a un compañero Frío; si no, -10%; solo porteros | `dwarf_fortress`, `elf_bulwark`, `human_wall` |
| `scar_tissue` | acumulación | — | rare | filler | INJURY | +5% de lesionar por cada lesión sufrida en la run (máx. 25%, acumula) | — |
| `shadow_marker` | proximidad | — | common | filler | TACKLE | +10% de lesionar al entrar si tiene cerca a un rival Fino | `orc_violence` |
| `silky_veteran` | acumulación | — | rare | filler | DRIBBLE_WON | +5% de regate por cada regate ganado en la run (máx. 25%, acumula) | `elf_glass`, `elf_tiki_taka`, `orc_misplaced` |
| `spearpoint` | alineación | — | rare | conditional | MATCH_START | +20% de tiro a puerta al compañero de delante si es Delantero; si no, -10 de técnica | `elf_tiki_taka` |
| `sweeper_keeper` | geometría | — | common | filler | RECOVERY | +5 de correa al recuperar en su propio tercio; solo porteros | `dwarf_fortress`, `elf_bulwark`, `human_wall` |
| `unlikely_bulwark` | identidad | Elf | rare | conditional | MATCH_START | exclusivo elfo: +20% de robo y +5 de correa; solo elfos con etiqueta Muro | `elf_bulwark` |
| `wing_overlap` | alineación | — | common | filler | DRIBBLE_ATTEMPTED | +15% de regate al compañero de su banda; sin él, -10 de velocidad | `elf_tiki_taka` |

## Los 4 letales (ADR 0046)

`lethal: true` (RF-093 vía 2). **Solo matan a quien ya no está sano**, y el motor deja una sola ventana
para eso: una lesión sufrida en el partido saca al jugador del campo, así que el único herido alcanzable
es el que **salta al campo herido**. Por eso los dos de mayor conversión disparan en `MATCH_START`: no es
un adorno, es dónde el mecanismo existe. El informe de ojeo los destaca antes de jugar (RF-013,
`Scouting.LethalPerks`) y la descripción generada lo dice también (`layout.lethalSuffix`, RT-035).
Ninguno aparece en rivales del acto 1 (el acto 1 es el taller, ADR 0043).

| id | eje | rareza | trigger | canal | escasez | resumen |
|---|---|---|---|---|---|---|
| `skullsplitter` | identidad | legendary | MATCH_START | `injury` (+100) | `tagsRequired: Dirty` | el equipo rival tiene el doble de cuota de lesionarse; quien saltó al campo herido, muere |
| `marrow_thirst` | zona de inicio | rare | MATCH_START | `injure` (+100) y `severeInjury` (+50) | `tagsRequired: Aggressive` y empezar en el tercio rival | el portador lesiona más y las lesiones del rival tienden a graves |
| `second_wound` | estado del partido | rare | INJURY | `severeInjury` (+15) | solo mientras no van ganando | al lesionarse un rival, sus lesiones tienden a graves; remata al que ya estaba tocado |
| `iron_studs` | geometría | rare | TACKLE | `tackleEvasion` (−100) | solo presionando en el tercio rival | el rival al que entra resiste la mitad de bien la entrada |

Los valores son **porcentajes de cuota** de la escala única de la ADR 0050 P1 (`±15, ±30, ±50, ±100`), la
misma en todos los canales; la tabla de escalones por canal de la ADR 0035 queda retirada.

**Reparto en rivales** (ADR 0046: escasos y tardíos). Acto 1: ninguno. Acto 2: `act2_orc_warband`,
`act2_undead_deadwalkers` y `act2_dwarf_shieldwall` con `marrow_thirst`, `act2_human_tacticians` con
`second_wound`; los elfos quedan limpios. Acto 3: `act3_orc_warlords` con `skullsplitter`,
`act3_human_allstars`, `act3_undead_legion` y `act3_dwarf_ironkings` con `marrow_thirst` (más
`second_wound` e `iron_studs` donde ya estaban); los elfos, otra vez limpios. Los jefes no llevan
ninguno: su rival es procedural y no asigna perks.

**Build de medida**: `data/balance/builds/orc_butchery.json` lleva los cuatro. Está fuera de
`groups.json` a propósito (como `elf_glass`): mide la aniquilación a mano, no entra en las puertas.

### Los 4 exclusivos de raza

Cada exclusivo apunta a una build de su raza **distinta** de las demás builds coherentes de esa raza, para no colapsar RF-032 (tres builds viables por raza):

| perk | raza | build a la que apunta | qué la distingue de las otras builds de su raza |
|---|---|---|---|
| `gentle_giant` | Orc | `orc_giants` | juego de apoyo por alineación (protege al delantero), distinto de la violencia de `orc_violence` (`injure`) y la turba de `orc_mob` |
| `unlikely_bulwark` | Elf | `elf_bulwark` | defensa Muro con la minoría de estilo opuesta a la élfica, distinto de la técnica de `elf_tiki_taka` y la fragilidad protegida de `elf_glass` |
| `iron_gate` | Dwarf | `dwarf_fortress` | anula el primer gol encajado; ancla la única build enana del catálogo de prueba |
| `deathless_march` | Undead | `undead_grind` | escalado de equipo entre partidos; ancla la única build no-muerta del catálogo de prueba |

## Builds (`data/balance/builds/`)

| id | raza | idea | resultado esperado |
|---|---|---|---|
| `dwarf_fortress` | Dwarf | fortaleza enana: portero con `iron_gate` (anula el primer gol) más `sweeper_keeper`/`safety_net`, central defensivo Muro que ancla su tercio | gana a `dwarf_none`; primera build enana del catálogo |
| `dwarf_none` | Dwarf | referencia sin perks | línea base |
| `elf_brawler` | Elf | perks que premian la etiqueta de estilo Bruta/Muro (`brute_boots`, `bulwark_stance`, `pack_mentality`) en un equipo élfico donde esa etiqueta es minoritaria (~10-12% por jugador, `styleTagWeights` de `data/races/elf.json`): casi siempre cae el `elseEffects`, y `pack_mentality` casi nunca ve tres Brutos | pierde contra `elf_none` |
| `elf_bulwark` | Elf | tercera identidad élfica: el exclusivo `unlikely_bulwark` (solo elfos con etiqueta Muro) ancla una defensa físicamente sólida, contraria a la imagen frágil-técnica de `elf_tiki_taka`/`elf_glass` | gana a `elf_none`; build de nicho, depende de que la tirada de estilo cabe |
| `elf_glass` | Elf | coherente pero frágil: técnica pura sin protección, delantero que solo rinde si empieza en el tercio rival (`forward_line`) y regatea rodeado de Brutos rivales (`crowd_control`) | gana a `elf_none`, pero con más riesgo/varianza que las builds anteriores |
| `elf_none` | Elf | referencia sin perks | línea base |
| `elf_out_of_zone` | Elf | `forward_line` (exige empezar en el tercio rival) en el portero y `own_third_anchor` (exige empezar en el tercio propio) en el delantero: la zona de inicio de cada uno es la contraria a la que pide su perk, `elseEffects` se dispara siempre, de forma determinista | pierde contra `elf_none` |
| `elf_tiki_taka` | Elf | técnica y alineación: Finos que se dan solapes de banda (`wing_overlap`) y un mediocentro que sirve al delantero (`spearpoint`, vínculo `ahead`), orquesta de mayoría Fina (`fine_orchestra`) | gana a `elf_none`; cadena de posesión mayor que `orc_violence` |
| `human_counter` | Human | transición: fuerza cuando va perdiendo (`comeback_spirit`), entradas más seguras cuando va ganando (`game_management`), delantero que dispara bien de lejos y de cerca | gana a `human_none` |
| `human_none` | Human | referencia sin perks | línea base |
| `human_random` | Human | mezcla arbitraria sin plan: un perk de proximidad, uno de estado del partido y uno de remontada repartidos sin criterio en un equipo Neutral que no cumple casi ninguna condición de raza | entre 40% y 60% contra `human_none` |
| `human_scattered` | Human | dos `pivot_duo` en los centrales, pero el `lineup` los separa a las filas 0 y 4 de la misma columna (distancia 4, fuera del radio 2 de la ADR 0021): el vínculo `beside` no existe nunca y el perk cae siempre en su `elseEffects` | pierde contra `human_none` |
| `human_wall` | Human | muro defensivo: portero cubierto (`sweeper_keeper`+`safety_net`), pareja de centrales vinculada `beside` (`pivot_duo`) que además anclan su tercio (`own_third_anchor`) | gana a `human_none` |
| `orc_giants` | Orc | tercera identidad orca (ADR 0023, punto 5): el exclusivo `gentle_giant` protege por alineación al delantero en vez de buscar el contacto directo; distinta de `orc_violence` y `orc_mob` | gana a `orc_none`; demuestra que el exclusivo abre una build nueva, no repite las universales |
| `orc_misplaced` | Orc | perks de acumulación (`poacher_instinct`, career de goles; `silky_veteran`, career de regates) puestos en los dos centrales de un equipo de contacto que casi nunca disparan ni regatean: contadores que no suben, slot muerto | pierde o iguala contra `orc_none` |
| `orc_mob` | Orc | apuesta por la turba: anula faltas propias del equipo en la turba (`mob_instigator`), árbitro más favorable cuando va perdiendo (`home_ref`), mayoría Bruta que se refuerza entre sí (`pack_mentality`) | gana a `orc_none`, pero diluido (la turba no siempre llega) |
| `orc_none` | Orc | referencia sin perks | línea base |
| `orc_violence` | Orc | contacto físico: fuerza bruta en defensa, lesiona a los Finos que tiene cerca (`shadow_marker`), delantero que escala tiro a puerta con los goles de la run (`poacher_instinct`) | gana a `orc_none` |
| `undead_grind` | Undead | primera build no-muerta: el exclusivo `deathless_march` escala el robo de todo el equipo partido a partido de la run, apoyado por `battle_reader` y un delantero frío que tira bien de lejos y de cerca | gana a `undead_none`, y la ventaja debería crecer con la run |
| `undead_none` | Undead | referencia sin perks | línea base |

`data/balance/groups.json`: `coherent` son las 9 builds que deben ganar a la referencia de su raza (incluye las tres nuevas de exclusivo — `orc_giants`, `elf_bulwark`, `undead_grind` — y `dwarf_fortress`); `bad` son las cuatro que deben perder; `random` es `human_random` (control, no debe ganar ni perder claramente); `baselineByRace` añade `Dwarf` y `Undead` a las tres razas de fase 1. `elf_glass` (frágil) queda fuera de `groups.json` a propósito, igual que en el catálogo de fase 1: se mide y se lee a mano, no entra en las puertas automáticas de `coherentBuildsBeatNone`/`badBuildsLoseToNone`.

## Notas de asignación

- Ninguna build (ni las malas) asigna un perk fuera de su `positionOnly` (`sweeper_keeper`, `safety_net`, `clean_sheet_legacy`, `iron_gate` solo en el slot 0, portero) ni fuera de la raza de su `race` (los exclusivos solo aparecen en builds de su propia raza).
- La incoherencia de las builds malas **no** viene de perks de otra raza (ADR 0023: el juego no los ofrecería), sino de tres mecanismos distintos: colocación que rompe el vínculo que el perk necesita (`human_scattered`), perks de acumulación en un rol que no realiza esa acción (`orc_misplaced`), y zona de inicio contraria a la que pide el perk, con `elseEffects` disparándose siempre de forma determinista (`elf_out_of_zone`). `elf_brawler` añade un cuarto mecanismo: apostar por una etiqueta de estilo minoritaria en la raza.
- `data/balance/reference.json` no se ha tocado: no referencia perks ni builds, solo raza y calidad media.

