# Inventario de datos: perks, objetos y consumibles

Extraccion pura desde `data/perks/*.json` (61 ficheros), `data/items/*.json` (34 ficheros) y `data/consumables/*.json` (4 ficheros), cruzada con `data/economy/perk-values.json` y `data/economy/item-values.json` (bloque `values`, MILESIMAS de punto de tasa de victoria x2, ADR 0087).

`UNKNOWN` significa que el campo no existe en el dato de origen. Las tablas estan ordenadas por `valor medido` descendente; los que no tienen valor medido van al final.

## Tabla 1 — Perks (61 filas)

| id | nombre es | rareza | kind | axis | race | family | trigger | scope | links | condición | efectos (tipo:canal:valor) | duración | limit | accumulatesAcrossMatches | lethal | positionOnly | tagsRequired | tagsForbidden | elseEffects (sí/no) | valor medido |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| deathless_march | Marcha sin fin | rare | filler | accumulation | Undead | null | MATCH_START | actor | [] |  | modifyProbability:tackle:valuePerCounter=50; addCounter:deathlessMarchMatches:1 | match | null | True | False | null | Undead | [] | no | 178 |
| battle_reader | Lector de la batalla | uncommon | filler | accumulation | null | null | MATCH_START | actor | [] |  | modifyProbability:intercept:valuePerCounter=100; addCounter:battleReaderMatches:1 | match | null | True | False | null | [] | [] | no | 150 |
| granite_line | Línea de granito | rare | conditional | startZone | null | wall | MATCH_START | actor | [] | startsIn(owner,'OwnThird') | modifyProbability:tackle:200; modifyProbability:shotOnTarget:-50 | match | null | False | False | null | [] | [] | no | 115 |
| killing_range | Distancia de tiro | rare | conditional | geometry | null | aim | SHOT | team | [] | distanceToGoal(actor) < 6 | modifyProbability:shotOnTarget:300 | play | null | False | False | null | [] | [] | no | 112 |
| captains_voice | Voz de capitán | uncommon | conditional | accumulation | null | null | MATCH_START | actor | [] | startsIn(owner,'Middle') | modifyProbability:tackle:valuePerCounter=100; addCounter:captainsVoiceMatches:1 | match | null | True | False | null | [] | [] | no | 104 |
| clean_sheet_legacy | Legado de portería a cero | uncommon | filler | accumulation | null | null | SAVE | actor | [] |  | modifyProbability:save:valuePerCounter=30; addCounter:cleanSheetSaves:1 | match | {"per": "match", "times": 1} | True | False | Goalkeeper | [] | [] | no | 98 |
| lane_reader | Lector de carriles | uncommon | filler | accumulation | null | null | RECOVERY | actor | [] |  | modifyProbability:intercept:valuePerCounter=100; addCounter:laneReaderRecoveries:1 | match | {"per": "match", "times": 1} | True | False | null | [] | [] | no | 83 |
| center_conductor | Conductor del centro | uncommon | conditional | startZone | null | null | MATCH_START | actor | [] | startsOn(owner,'Center') | modifyProbability:intercept:100 | match | null | False | False | null | [] | [] | no | 51 |
| scar_veteran | Veterano de cicatrices | rare | filler | accumulation | null | null | MATCH_START | actor | [] |  | modifyAttribute:strength:valuePerCounter=3; addCounter:scarVeteranMatches:1 | match | null | True | False | null | [] | [] | no | 43 |
| iron_gate | Puerta de hierro | rare | ruleBreaker | identity | Dwarf | null | INJURY | team | [] |  | cancelEvent:: |  | {"per": "match", "times": 1} | False | False | null | Dwarf | [] | no | 40 |
| first_touch_school | Escuela del primer toque | rare | conditional | composition | null | craft | MATCH_START | actor | [] | teammatesWithTag(owner,'Fine') > 1 | modifyProbability:pass:100; modifyProbability:interceptEvasion:300 | match | null | False | False | null | [] | [] | no | 26 |
| pivot_duo | Doble pivote | common | filler | alignment | null | null | MATCH_START | actor | left; right | linked(owner,'left') \|\| linked(owner,'right') | modifyProbability:tackle:100 | match | null | False | False | null | [] | [] | no | 26 |
| sharpshooter_drill | Ensayo de tirador | uncommon | filler | accumulation | null | aim | SHOT | actor | [] |  | modifyProbability:shotOnTarget:valuePerCounter=100; addCounter:sharpshooterShots:1 | match | {"per": "match", "times": 1} | True | False | null | [] | [] | no | 26 |
| covering_shadow | Sombra de cobertura | uncommon | conditional | alignment | null | null | MATCH_START | actor | behind | linked(owner,'behind') | modifyProbability:intercept:200 | match | null | False | False | null | [] | [] | no | 25 |
| forward_line | Línea de ataque | uncommon | filler | startZone | null | aim | MATCH_START | actor | [] | startsIn(owner,'AttackingThird') | modifyProbability:shotOnTarget:200 | match | null | False | False | null | [] | [] | no | 18 |
| gentle_giant | Gigante amable | rare | conditional | alignment | Orc | null | MATCH_START | actor | ahead | linked(owner,'ahead') | modifyProbability:intercept:100 | match | null | False | False | null | Orc | [] | no | 12 |
| iron_lungs | Pulmones de hierro | common | filler | accumulation | null | null | MATCH_START | actor | [] |  | modifyAttribute:stamina:valuePerCounter=3; addCounter:ironLungsMatches:1 | match | null | True | False | null | [] | [] | no | 12 |
| long_range_menace | Amenaza de larga distancia | common | filler | geometry | null | aim | SHOT | actor | [] | distanceToGoal(actor) >= 6 | modifyProbability:shotOnTarget:100 | play | null | False | False | null | [] | [] | no | 10 |
| own_third_anchor | Ancla del tercio propio | common | filler | startZone | null | wall | MATCH_START | actor | [] | startsIn(owner,'OwnThird') | modifyProbability:tackle:100 | match | null | False | False | null | [] | [] | no | 10 |
| poacher_instinct | Instinto cazagoles | uncommon | filler | accumulation | null | aim | GOAL | actor | [] |  | modifyProbability:shotOnTarget:valuePerCounter=100; addCounter:poacherInstinctGoals:1 | match | {"per": "match", "times": 1} | True | False | null | [] | [] | no | 10 |
| last_ditch | Último recurso | uncommon | conditional | geometry | null | wall | TACKLE | actor | [] | zone(actor) == 'Own' | modifyProbability:tackle:200 | play | null | False | False | null | [] | [] | no | 8 |
| spearpoint | Punta de lanza | uncommon | conditional | alignment | null | aim | MATCH_START | actor | ahead | linked(owner,'ahead') | modifyProbability:shotOnTarget:200 | match | null | False | False | null | [] | [] | no | 8 |
| iron_studs | Tacos de hierro | rare | ruleBreaker | geometry | null | butchery | TACKLE | actor | [] | zone(actor) == 'Opposing' | modifyProbability:tackleEvasion:-100 | play | null | False | True | null | [] | [] | no | 7 |
| sweeper_keeper | Portero líbero | common | filler | geometry | null | null | RECOVERY | actor | [] | zone(actor) == 'Own' | modifyLeash:leash:2 | play | null | False | False | Goalkeeper | [] | [] | no | 7 |
| box_predator | Depredador de área | common | filler | geometry | null | aim | SHOT | actor | [] | distanceToGoal(actor) < 3 | modifyProbability:shotOnTarget:100 | play | null | False | False | null | [] | [] | no | 6 |
| pack_mentality | Mentalidad de manada | uncommon | conditional | composition | null | butchery | MATCH_START | actor | [] | teammatesWithTag(owner,'Brute') > 2 | modifyAttribute:strength:10 | match | null | False | False | null | [] | [] | no | 6 |
| steady_hands | Manos firmes | common | filler | accumulation | null | craft | PASS_COMPLETED | actor | [] |  | modifyProbability:pass:valuePerCounter=50; addCounter:steadyHandsPasses:1 | match | {"per": "match", "times": 1} | True | False | null | [] | [] | no | 6 |
| brute_boots | Botas de bruto | common | filler | identity | null | butchery | MATCH_START | actor | [] | hasTag(owner,'Brute') | modifyAttribute:strength:10 | match | null | False | False | null | [] | [] | no | 5 |
| diagonal_press | Presión diagonal | common | filler | alignment | null | null | TACKLE | actor | diagonalAhead; diagonalBehind | linked(owner,'diagonalAhead') \|\| linked(owner,'diagonalBehind') | modifyProbability:tackle:100 | play | null | False | False | null | [] | [] | no | 5 |
| road_warrior | Curtido en mil batallas | uncommon | conditional | accumulation | null | null | RECOVERY | actor | [] | stat(actor,'tacklesWon') >= 2 | modifyProbability:tackle:200 | match | null | False | False | null | [] | [] | no | 5 |
| pit_veteran | Veterano del foso | common | conditional | accumulation | null | wall | TACKLE | actor | [] | startsIn(owner,'OwnThird') | modifyProbability:tackle:valuePerCounter=50; addCounter:pitVeteranTackles:1 | match | {"per": "match", "times": 1} | True | False | null | [] | [] | no | 4 |
| back_to_back | Espalda con espalda | common | filler | proximity | null | wall | TACKLE | actor | [] | nearAlly(actor,'Bulwark',2) | modifyProbability:tackle:100 | play | null | False | False | null | [] | [] | no | 3 |
| bruised_knuckles | Nudillos marcados | uncommon | conditional | accumulation | null | butchery | FOUL | actor | [] | hasTag(owner,'Brute') | modifyProbability:injure:valuePerCounter=30; addCounter:bruisedKnucklesFouls:1 | match | {"per": "match", "times": 1} | True | False | null | [] | [] | no | 3 |
| flank_specialist | Especialista de banda | common | filler | startZone | null | craft | MATCH_START | actor | [] | startsOn(owner,'LeftFlank') \|\| startsOn(owner,'RightFlank') | modifyProbability:dribble:100 | match | null | False | False | null | [] | [] | no | 3 |
| shadow_marker | Marcador de sombra | common | filler | proximity | null | butchery | TACKLE | actor | [] | nearAlly(actor,'Brute',2) | modifyProbability:injure:100 | play | null | False | False | null | [] | [] | no | 3 |
| game_management | Gestión del partido | common | filler | matchState | null | wall | TACKLE | actor | [] | scoreDiff() > 0 | modifyProbability:tackle:100 | play | null | False | False | null | [] | [] | no | 2 |
| blood_tithe | Diezmo de sangre | rare | ruleBreaker | composition | null | butchery | MATCH_START | actor | [] | teammatesWithTag(owner,'Brute') > 1 | modifyProbability:injure:100; modifyProbability:severeInjury:15 | match | null | False | False | null | [] | [] | no | 1 |
| cold_focus | Sangre fría | common | filler | identity | null | aim | SHOT | actor | [] | hasTag(actor,'Cold') | modifyProbability:shotOnTarget:100 | play | null | False | False | null | [] | [] | no | 1 |
| long_leash_legacy | Correa curtida | rare | filler | accumulation | null | null | MATCH_START | actor | [] |  | modifyLeash:leash:valuePerCounter=1; addCounter:longLeashMatches:1 | match | null | True | False | null | [] | [] | no | 1 |
| bulwark_stance | Postura de muro | common | filler | identity | null | wall | MATCH_START | actor | [] | hasTag(owner,'Bulwark') | modifyProbability:tackle:100 | match | null | False | False | null | [] | [] | no | 0 |
| fine_touch | Toque fino | common | filler | identity | null | craft | MATCH_START | actor | [] | hasTag(owner,'Fine') | modifyProbability:pass:100 | match | null | False | False | null | [] | [] | no | 0 |
| high_press_trigger | Disparador de presión alta | uncommon | conditional | geometry | null | null | PASS_FAILED | team | [] | zone(actor) == 'Opposing' | modifyProbability:intercept:100 | play | null | False | False | null | [] | [] | no | 0 |
| safety_net | Red de seguridad | common | filler | proximity | null | wall | SAVE | actor | [] | nearAlly(actor,'Defender',3) | modifyProbability:save:100 | play | null | False | False | Goalkeeper | [] | [] | no | 0 |
| scar_tissue | Tejido cicatricial | uncommon | filler | accumulation | null | butchery | INJURY | actor | [] |  | modifyProbability:injure:valuePerCounter=30; addCounter:scarTissueInjuries:1 | match | {"per": "match", "times": 1} | True | False | null | [] | [] | no | 0 |
| silky_veteran | Veterano de seda | uncommon | filler | accumulation | null | craft | DRIBBLE_WON | actor | [] |  | modifyProbability:dribble:valuePerCounter=100; addCounter:silkyVeteranDribbles:1 | match | {"per": "match", "times": 1} | True | False | null | [] | [] | no | 0 |
| wing_overlap | Solapamiento de banda | common | filler | alignment | null | craft | DRIBBLE_ATTEMPTED | actor | left; right | linked(owner,'left') \|\| linked(owner,'right') | modifyProbability:dribble:100 | play | null | False | False | null | [] | [] | no | 0 |
| crowd_control | Control de multitud | uncommon | conditional | proximity | null | craft | DRIBBLE_ATTEMPTED | actor | [] | nearOpponent(actor,'Fine',2) | modifyProbability:dribble:200 | play | null | False | False | null | [] | [] | no | -1 |
| home_ref | Árbitro casero | uncommon | conditional | matchState | null | null | FOUL | actor | [] | scoreDiff() < 0 | modifyBias:bias:10 |  | null | False | False | null | [] | [] | no | -1 |
| comeback_spirit | Espíritu de remontada | uncommon | conditional | matchState | null | null | PLAY_START | actor | [] | scoreDiff() < 0 | modifyAttribute:strength:10 | play | null | False | False | null | [] | [] | no | -3 |
| second_wound | La segunda herida | rare | ruleBreaker | matchState | null | null | INJURY | opposingTeam | [] | scoreDiff() <= 0 | modifyProbability:severeInjury:15 | match | null | False | True | null | [] | [] | no | -3 |
| mob_instigator | Instigador de la turba | rare | ruleBreaker | matchState | null | null | FOUL | team | [] | isMob() | cancelEvent:: |  | {"per": "match", "times": 2} | False | False | null | [] | [] | no | -7 |
| elf_touch | Toque | uncommon | filler | identity | Elf | null | MATCH_START | any | [] |  | modifyProbability:tackleEvasion:30 | match | UNKNOWN | False | False | null | [] | [] | no | UNKNOWN |
| fine_orchestra | Orquesta fina | uncommon | conditional | composition | null | craft | MATCH_START | actor | [] | teammatesWithTag(owner,'Fine') > 2 | modifyProbability:intercept:-200; modifyProbability:tackleEvasion:200 | match | null | False | False | null | Fine | [] | no | UNKNOWN |
| hot_blooded | Sangre caliente | uncommon | filler | identity | Orc | null | MATCH_START | any | [] |  | modifyKnockdownTicks:knockdownTicks:15 | match | UNKNOWN | False | False | null | [] | [] | no | UNKNOWN |
| marrow_thirst | Sed de médula | rare | ruleBreaker | startZone | null | butchery | TACKLE | actor | [] | startsIn(owner, 'AttackingThird') | modifyProbability:injure:200; modifyProbability:severeInjury:50 | play | null | False | True | null | Aggressive | [] | no | UNKNOWN |
| natural_leader | Líder nato | uncommon | conditional | identity | null | null | MATCH_START | actor | [] |  | modifyProbability:tackle:200 | match | null | False | False | null | Leader | [] | no | UNKNOWN |
| numb | No sienten nada | uncommon | filler | identity | Undead | null | MATCH_START | any | [] |  | immunity:mourning:; immunity:minorInjuryPenalty: |  | UNKNOWN | False | False | null | [] | [] | no | UNKNOWN |
| quick_learner | Adaptables | uncommon | filler | identity | Human | null | MATCH_END | any | [] |  | modifyExperience:experience:25 | run | UNKNOWN | False | False | null | [] | [] | no | UNKNOWN |
| roots | Raíces | uncommon | filler | identity | Dwarf | null | MATCH_START | any | [] |  | immunity:push: |  | UNKNOWN | False | False | null | [] | [] | no | UNKNOWN |
| skullsplitter | Partecráneos | legendary | ruleBreaker | identity | null | null | TACKLE | actor | [] |  | modifyProbability:injury:200 | play | null | False | True | null | Dirty | [] | no | UNKNOWN |
| unlikely_bulwark | Muro improbable | uncommon | conditional | identity | Elf | null | MATCH_START | actor | [] |  | modifyProbability:tackle:200; modifyLeash:leash:2 | match | null | False | False | null | Elf; Bulwark | [] | no | UNKNOWN |

## Tabla 2 — Objetos (34 filas)

| id | nombre es | rareza | archetype | minAct | attributeBonus (atributo:valor) | otros campos | valor medido |
|---|---|---|---|---|---|---|---|
| swiftrot_tendons | Tendones de podredumbre rápida | UNKNOWN | restricted | 2 | speed:10; technique:10; leash:10 | race=Undead | 88 |
| veteran_armband | Brazalete de veterano | uncommon | normal | 1 | technique:10; stamina:10 |  | 84 |
| warchiefs_pauldron | Hombrera del jefe de guerra | UNKNOWN | restricted | 2 | strength:10; stamina:10; speed:10 | race=Orc | 82 |
| gilded_charm | Talismán dorado | rare | fragile | 2 | strength:10; speed:10; technique:10 | breakChancePercent=20 | 73 |
| champions_sash | Faja de campeón | rare | normal | 2 | strength:10; technique:10; stamina:10 |  | 67 |
| verdant_circlet | Diadema verdeante | UNKNOWN | restricted | 2 | technique:10; stamina:10; speed:10 | race=Elf | 65 |
| windsong_sandals | Sandalias del canto del viento | UNKNOWN | restricted | 2 | speed:10; technique:10; leash:10 | race=Elf | 63 |
| cracked_visor | Visera agrietada | common | fragile | 1 | technique:10 | breakChancePercent=30 | 62 |
| sprinters_greaves | Grebas del velocista | uncommon | normal | 1 | speed:10; stamina:10 |  | 62 |
| endurance_belt | Cinturón de aguante | common | normal | 1 | stamina:10 |  | 61 |
| forgefire_ring | Anillo de fuego de forja | UNKNOWN | restricted | 2 | strength:10; stamina:10; leash:10 | race=Dwarf | 57 |
| iron_league_medal | Medalla de la liga de hierro | UNKNOWN | restricted | 2 | strength:10; technique:10; stamina:10 | race=Human | 56 |
| quicksilver_harness | Arnés de azogue | rare | normal | 2 | speed:10; technique:10; leash:10 |  | 55 |
| shaman_beads | Abalorios del chamán | UNKNOWN | restricted | 2 | technique:10; leash:10; speed:10 | race=Orc | 50 |
| duelists_gloves | Guantes del duelista | uncommon | normal | 1 | speed:10; technique:10 |  | 47 |
| focus_lens | Lente de enfoque | common | normal | 1 | technique:10 |  | 46 |
| loose_leash_charm | Amuleto de correa larga | common | normal | 1 | leash:10 |  | 46 |
| frayed_banner | Estandarte deshilachado | uncommon | fragile | 1 | stamina:10; leash:10 | breakChancePercent=25 | 40 |
| grimhold_anvil | Yunque de Grimhold | UNKNOWN | restricted | 2 | strength:10; stamina:10; technique:10 | race=Dwarf | 37 |
| glass_cannon_spikes | Tacos de cristal | uncommon | cursed | 1 | speed:20; technique:20; strength:-30 |  | 34 |
| grave_lantern | Farol de tumba | UNKNOWN | restricted | 2 | strength:10; technique:10; stamina:10 | race=Undead | 34 |
| martyrs_relic | Reliquia del mártir | rare | cursed | 2 | technique:20; stamina:20; leash:20; speed:-60 |  | 30 |
| deep_road_boots | Botas del camino profundo | UNKNOWN | restricted | 2 | speed:10; leash:10; technique:10 | race=Dwarf | 29 |
| bloodbanner | Estandarte de sangre | UNKNOWN | restricted | 2 | strength:10; stamina:10; technique:10 | race=Orc | 25 |
| heralds_pennant | Banderin del heraldo | UNKNOWN | restricted | 2 | leash:10; speed:10; stamina:10 | race=Human | 24 |
| worn_boots | Botas gastadas | common | normal | 1 | speed:10 |  | 20 |
| academy_whistle | Silbato de la academia | UNKNOWN | restricted | 2 | technique:10; stamina:10; leash:10 | race=Human | 16 |
| brutes_pauldron | Hombrera del bruto | common | cursed | 1 | strength:20; speed:-40 |  | 10 |
| chipped_blade | Hoja mellada | common | fragile | 1 | strength:10 | breakChancePercent=30 | 8 |
| ossuary_charm | Amuleto del osario | UNKNOWN | restricted | 2 | strength:10; stamina:10; leash:10 | race=Undead | 8 |
| iron_gauntlets | Guanteletes de hierro | common | normal | 1 | strength:10 |  | 2 |
| moonsteel_bracer | Brazal de acero lunar | UNKNOWN | restricted | 2 | strength:10; stamina:10; leash:10 | race=Elf | 0 |
| berserker_totem | Tótem del berserker | rare | cursed | 2 | strength:20; speed:20; stamina:20; technique:-60 |  | -3 |
| weighted_wraps | Vendas lastradas | uncommon | normal | 1 | strength:10; stamina:10 |  | -4 |

## Tabla 3 — Consumibles (4 filas)

| id | nombre es | rarity | family | effects |
|---|---|---|---|---|
| field_bandage | Vendaje de campaña | common | medical | modifyProbability:injury:-100 |
| low_blow_kit | Kit de golpe bajo | uncommon | dirty | modifyAttribute:strength:12; modifyProbability:foul:200 |
| lucky_charm | Amuleto de la suerte | uncommon | supernatural | modifyProbability:shotOnTarget:100 |
| smoke_flare | Bengala de humo | common | tactical | modifyProbability:tackleEvasion:100 |

## Hechos

### 1. Perks sin valor medido en perk-values.json (10)

- elf_touch
- fine_orchestra
- hot_blooded
- marrow_thirst
- natural_leader
- numb
- quick_learner
- roots
- skullsplitter
- unlikely_bulwark

### 2. Recuento por trigger, axis, kind y rareza

**Por trigger:**

- DRIBBLE_ATTEMPTED: 2
- DRIBBLE_WON: 1
- FOUL: 3
- GOAL: 1
- INJURY: 3
- MATCH_END: 1
- MATCH_START: 28
- PASS_COMPLETED: 1
- PASS_FAILED: 1
- PLAY_START: 1
- RECOVERY: 3
- SAVE: 2
- SHOT: 5
- TACKLE: 9

**Por axis:**

- accumulation: 16
- alignment: 6
- composition: 4
- geometry: 7
- identity: 13
- matchState: 5
- proximity: 4
- startZone: 6

**Por kind:**

- conditional: 20
- filler: 34
- ruleBreaker: 7

**Por rareza:**

- common: 19
- legendary: 1
- rare: 13
- uncommon: 28

### 3. Perks con elseEffects vacio Y condicion no vacia (40)

- back_to_back
- blood_tithe
- box_predator
- bruised_knuckles
- brute_boots
- bulwark_stance
- captains_voice
- center_conductor
- cold_focus
- comeback_spirit
- covering_shadow
- crowd_control
- diagonal_press
- fine_orchestra
- fine_touch
- first_touch_school
- flank_specialist
- forward_line
- game_management
- gentle_giant
- granite_line
- high_press_trigger
- home_ref
- iron_studs
- killing_range
- last_ditch
- long_range_menace
- marrow_thirst
- mob_instigator
- own_third_anchor
- pack_mentality
- pit_veteran
- pivot_duo
- road_warrior
- safety_net
- second_wound
- shadow_marker
- spearpoint
- sweeper_keeper
- wing_overlap
