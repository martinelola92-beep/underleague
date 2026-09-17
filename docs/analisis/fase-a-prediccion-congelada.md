# Fase A — predicción congelada del analizador de adecuación de población

**Fecha:** 19 sep 2026. **Estado:** CONGELADO antes de medir nada.

Este fichero es la puerta de la Fase A de §22.8 del protocolo de balanceo
(`docs/analisis/protocolo-balanceo-automatizado.md`). Contiene la predicción del analizador
`Sim/Analysis/PopulationFitness.cs` para **los 94 perks del catálogo**, generada con
`Sim.Tests/Balance/FaseAPredictionTests.cs` y commiteada **antes** de ejecutar ninguna simulación nueva.

Existe por una razón concreta: la hipótesis de §21 salió de 13 perks ya medidos, así que "el analizador
explica esos 13" no demuestra nada. Lo único que prueba algo es acertar sobre los que **no** se usaron
para construirla — y para que eso sea comprobable, la predicción tiene que estar escrita antes, no
después. Si una medición posterior contradice esta tabla, **se corrige el analizador y se vuelve a
congelar; no se reescribe esta tabla**.

## Las afirmaciones falsables

1. **De los 81 perks fuera del conjunto de hipótesis, la raza solo debería importar en UNO**:
   `pack_mentality` (`teammatesWithTag(owner,'Brute') > 2`, raza afín Orc). Para los otros 80, cambiar la
   raza de la población de prueba **no debería mover la exposición**.
2. **De los 20 perks `ReadyForScreening` que la Fase A va a medir, la raza no debería importar en
   NINGUNO** (columna `raza_importa` = NO en los 20). Es la predicción de especificidad: si al medir
   resulta que cambiar de raza sube la exposición de varios de ellos, el analizador queda refutado.
3. **13 de los 81 deberían salir `WrongPosition`** — entre ellos `double_shot` (medido sobre un Defensa,
   acción exigida Shoot). Cambiar el portador a un Delantero **sí** debería moverlos.
4. **53 de los 81 son `GenuinelyRare`**: población y posición ya adecuadas. Si su exposición sale baja,
   no se arregla ni con raza ni con posición.

## Cómo leer la tabla

- `en_hipotesis`: `si` = se usó para construir la hipótesis en §21 (no vale como prueba).
- `raza_medida` / `rol_medido`: lo que el harness usa **hoy** (`perk.Race ?? Human`, y el portador que
  devuelve `PairedBalanceHarness.FindEligibleCarrierSlot` sobre la plantilla 0).
- `prediccion`: el motivo que el analizador reportaría **si** la exposición saliera por debajo del suelo.
  No es una predicción de que vaya a salir baja.
- `raza_importa`: la afirmación falsable directa — ¿debería cambiar la exposición al medir con la raza afín?

| perk | en_hipotesis | ready | raza_medida | rol_medido | poblacion | posicion | estilo | raza_afin | accion | rol_requerido | prediccion | raza_importa |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `ad_machine` | no | no | Human | Defender | NoStyleRequired | Unknown | - | - | - | - | LowExposure | NO |
| `back_to_back` | si | si | Human | Defender | WrongPopulation | Adequate | Bulwark | Dwarf | Tackle | Defender | WrongPopulation | SI |
| `battle_reader` | no | no | Human | Defender | NoStyleRequired | NoActionRequired | - | - | - | - | GenuinelyRare | NO |
| `blood_scent` | no | si | Human | Defender | NoStyleRequired | NoActionRequired | - | - | - | - | GenuinelyRare | NO |
| `blood_tithe` | si | no | Human | Defender | WrongPopulation | NoActionRequired | Brute | Orc | - | - | WrongPopulation | SI |
| `bloodhound` | no | si | Human | Defender | NoStyleRequired | NoActionRequired | - | - | - | - | GenuinelyRare | NO |
| `bodyguard` | no | no | Human | Defender | NoStyleRequired | NoActionRequired | - | - | - | - | GenuinelyRare | NO |
| `box_office` | no | no | Human | Defender | NoStyleRequired | NoActionRequired | - | - | - | - | GenuinelyRare | NO |
| `box_predator` | no | no | Human | Defender | NoStyleRequired | WrongPosition | - | - | Shoot | Forward | WrongPosition | NO |
| `bruised_knuckles` | si | no | Human | Defender | WrongPopulation | Adequate | Brute | Orc | Tackle | Defender | WrongPopulation | SI |
| `brute_boots` | si | no | Human | Defender | WrongPopulation | NoActionRequired | Brute | Orc | - | - | WrongPopulation | SI |
| `bulwark_stance` | si | si | Human | Defender | WrongPopulation | Adequate | Bulwark | Dwarf | Tackle | Defender | WrongPopulation | SI |
| `cannon` | si | si | Human | Defender | NoStyleRequired | WrongPosition | - | - | Shoot | Forward | WrongPosition | NO |
| `captains_voice` | no | no | Human | Defender | NoStyleRequired | Adequate | - | - | Tackle | Defender | GenuinelyRare | NO |
| `center_conductor` | no | no | Human | Defender | NoStyleRequired | NoActionRequired | - | - | - | - | GenuinelyRare | NO |
| `charge` | no | si | Human | Defender | NoStyleRequired | Adequate | - | - | Tackle | Defender | GenuinelyRare | NO |
| `clean_sheet_legacy` | no | no | Human | Goalkeeper | NoStyleRequired | FixedByPerk | - | - | - | - | GenuinelyRare | NO |
| `cold_focus` | si | no | Human | Defender | WrongPopulation | WrongPosition | Cold | Undead | Shoot | Forward | WrongPopulation | SI |
| `comeback_spirit` | no | no | Human | Defender | NoStyleRequired | NoActionRequired | - | - | - | - | GenuinelyRare | NO |
| `covering_shadow` | no | no | Human | Defender | NoStyleRequired | NoActionRequired | - | - | - | - | GenuinelyRare | NO |
| `crowd_control` | si | no | Human | Defender | WrongPopulation | WrongPosition | Fine | Elf | Dribble | Forward | WrongPopulation | SI |
| `deathless_march` | no | no | Undead | Defender | FixedByPerk | Adequate | - | - | Tackle | Defender | GenuinelyRare | NO |
| `deep_pivot` | no | si | Human | Midfielder | NoStyleRequired | FixedByPerk | - | - | - | - | GenuinelyRare | NO |
| `deep_run` | no | si | Human | Forward | NoStyleRequired | FixedByPerk | - | - | - | - | GenuinelyRare | NO |
| `diagonal_press` | no | no | Human | Defender | NoStyleRequired | Adequate | - | - | Tackle | Defender | GenuinelyRare | NO |
| `dirty_play` | no | no | Human | Defender | NoStyleRequired | Adequate | - | - | Tackle | Defender | GenuinelyRare | NO |
| `diver` | no | no | Human | Defender | NoStyleRequired | Adequate | - | - | Tackle | Defender | GenuinelyRare | NO |
| `double_shot` | no | si | Human | Defender | NoStyleRequired | WrongPosition | - | - | Shoot | Forward | WrongPosition | NO |
| `earthquake` | no | no | Human | Defender | NoStyleRequired | Adequate | - | - | Tackle | Defender | GenuinelyRare | NO |
| `elf_touch` | no | no | Elf | - | FixedByPerk | NoActionRequired | - | - | - | - | GenuinelyRare | NO |
| `fine_orchestra` | si | no | Human | - | WrongPopulation | NoActionRequired | Fine | Elf | - | - | WrongPopulation | SI |
| `fine_touch` | si | no | Human | Defender | WrongPopulation | NoActionRequired | Fine | Elf | - | - | WrongPopulation | SI |
| `first_touch_school` | si | no | Human | Defender | WrongPopulation | NoActionRequired | Fine | Elf | - | - | WrongPopulation | SI |
| `flank_specialist` | no | no | Human | Defender | NoStyleRequired | WrongPosition | - | - | Dribble | Forward | WrongPosition | NO |
| `forward_line` | no | no | Human | Defender | NoStyleRequired | WrongPosition | - | - | Shoot | Forward | WrongPosition | NO |
| `free_man` | no | si | Human | Defender | NoStyleRequired | NoActionRequired | - | - | - | - | GenuinelyRare | NO |
| `game_management` | no | si | Human | Defender | NoStyleRequired | Adequate | - | - | Tackle | Defender | GenuinelyRare | NO |
| `gathering_thirst` | no | no | Human | Defender | NoStyleRequired | Unknown | - | - | - | - | LowExposure | NO |
| `gentle_giant` | no | no | Orc | Defender | FixedByPerk | NoActionRequired | - | - | - | - | GenuinelyRare | NO |
| `granite_line` | no | no | Human | Defender | NoStyleRequired | Adequate | - | - | Tackle | Defender | GenuinelyRare | NO |
| `grudge` | no | si | Human | Defender | NoStyleRequired | Adequate | - | - | Tackle | Defender | GenuinelyRare | NO |
| `half_leg` | no | no | Human | Defender | NoStyleRequired | NoActionRequired | - | - | - | - | GenuinelyRare | NO |
| `hand_of_god` | no | no | Human | Goalkeeper | NoStyleRequired | FixedByPerk | - | - | - | - | GenuinelyRare | NO |
| `high_line` | no | si | Human | Defender | NoStyleRequired | FixedByPerk | - | - | - | - | GenuinelyRare | NO |
| `high_press_trigger` | no | no | Human | Defender | NoStyleRequired | Unknown | - | - | - | - | LowExposure | NO |
| `home_ref` | no | no | Human | Defender | NoStyleRequired | Adequate | - | - | Tackle | Defender | GenuinelyRare | NO |
| `hot_blooded` | no | no | Orc | - | FixedByPerk | NoActionRequired | - | - | - | - | GenuinelyRare | NO |
| `inheritance` | no | no | Human | Defender | NoStyleRequired | Unknown | - | - | - | - | LowExposure | NO |
| `iron_gate` | no | si | Dwarf | Defender | FixedByPerk | Unknown | - | - | - | - | LowExposure | NO |
| `iron_lungs` | no | no | Human | Defender | NoStyleRequired | NoActionRequired | - | - | - | - | GenuinelyRare | NO |
| `iron_price` | no | si | Human | Defender | NoStyleRequired | Unknown | - | - | - | - | LowExposure | NO |
| `iron_studs` | no | no | Human | Defender | NoStyleRequired | Adequate | - | - | Tackle | Defender | GenuinelyRare | NO |
| `kamikaze` | no | si | Human | Defender | NoStyleRequired | NoActionRequired | - | - | - | - | GenuinelyRare | NO |
| `killing_range` | no | no | Human | Defender | NoStyleRequired | WrongPosition | - | - | Shoot | Forward | WrongPosition | NO |
| `lane_reader` | no | no | Human | Defender | NoStyleRequired | Unknown | - | - | - | - | LowExposure | NO |
| `last_ditch` | no | si | Human | Defender | NoStyleRequired | Adequate | - | - | Tackle | Defender | GenuinelyRare | NO |
| `last_man` | no | no | Human | Defender | NoStyleRequired | FixedByPerk | - | - | Shoot | - | GenuinelyRare | NO |
| `life_insurance` | no | no | Human | Defender | NoStyleRequired | Unknown | - | - | - | - | LowExposure | NO |
| `line_keeper` | no | si | Human | Goalkeeper | NoStyleRequired | FixedByPerk | - | - | - | - | GenuinelyRare | NO |
| `loan` | no | no | Human | Defender | NoStyleRequired | NoActionRequired | - | - | - | - | GenuinelyRare | NO |
| `local_idol` | no | no | Human | Forward | NoStyleRequired | FixedByPerk | - | - | - | - | GenuinelyRare | NO |
| `long_leash_legacy` | no | no | Human | Defender | NoStyleRequired | NoActionRequired | - | - | - | - | GenuinelyRare | NO |
| `long_range_menace` | no | no | Human | Defender | NoStyleRequired | WrongPosition | - | - | Shoot | Forward | WrongPosition | NO |
| `low_block` | no | no | Human | Defender | NoStyleRequired | NoActionRequired | - | - | - | - | GenuinelyRare | NO |
| `marrow_thirst` | no | no | Human | Defender | NoStyleRequired | Adequate | - | - | Tackle | Defender | GenuinelyRare | NO |
| `mob_instigator` | no | no | Human | Defender | NoStyleRequired | Adequate | - | - | Tackle | Defender | GenuinelyRare | NO |
| `natural_leader` | no | no | Human | - | NoStyleRequired | Unknown | - | - | Tackle | Defender | LowExposure | NO |
| `no_dying` | no | no | Human | Defender | NoStyleRequired | Unknown | - | - | - | - | LowExposure | NO |
| `numb` | no | no | Undead | - | FixedByPerk | NoActionRequired | - | - | - | - | GenuinelyRare | NO |
| `own_third_anchor` | no | si | Human | Defender | NoStyleRequired | Adequate | - | - | Tackle | Defender | GenuinelyRare | NO |
| `pack_mentality` | no | no | Human | Defender | WrongPopulation | NoActionRequired | Brute | Orc | - | - | WrongPopulation | SI |
| `pit_veteran` | no | no | Human | Defender | NoStyleRequired | Adequate | - | - | Tackle | Defender | GenuinelyRare | NO |
| `pivot_duo` | no | no | Human | Defender | NoStyleRequired | Adequate | - | - | Tackle | Defender | GenuinelyRare | NO |
| `poacher_instinct` | no | no | Human | Defender | NoStyleRequired | WrongPosition | - | - | Shoot | Forward | WrongPosition | NO |
| `quick_learner` | no | no | Human | - | FixedByPerk | NoActionRequired | - | - | - | - | GenuinelyRare | NO |
| `road_warrior` | no | no | Human | Defender | NoStyleRequired | Adequate | - | - | Tackle | Defender | GenuinelyRare | NO |
| `roots` | no | no | Dwarf | - | FixedByPerk | NoActionRequired | - | - | - | - | GenuinelyRare | NO |
| `safety_net` | si | no | Human | Goalkeeper | NoStyleRequired | FixedByPerk | - | - | - | - | GenuinelyRare | NO |
| `scar_tissue` | no | no | Human | Defender | NoStyleRequired | Unknown | - | - | - | - | LowExposure | NO |
| `scar_veteran` | no | no | Human | Defender | NoStyleRequired | NoActionRequired | - | - | - | - | GenuinelyRare | NO |
| `second_wound` | no | si | Human | Defender | NoStyleRequired | Unknown | - | - | - | - | LowExposure | NO |
| `shadow` | no | si | Human | Midfielder | NoStyleRequired | FixedByPerk | - | - | - | - | GenuinelyRare | NO |
| `shadow_marker` | si | si | Human | Defender | WrongPopulation | Adequate | Brute | Orc | Tackle | Defender | WrongPopulation | SI |
| `sharpshooter_drill` | no | no | Human | Defender | NoStyleRequired | WrongPosition | - | - | Shoot | Forward | WrongPosition | NO |
| `silky_veteran` | no | no | Human | Defender | NoStyleRequired | WrongPosition | - | - | Dribble | Forward | WrongPosition | NO |
| `skullsplitter` | no | no | Human | Midfielder | NoStyleRequired | WrongPosition | - | - | Tackle | Defender | WrongPosition | NO |
| `spearpoint` | no | no | Human | Defender | NoStyleRequired | WrongPosition | - | - | Shoot | Forward | WrongPosition | NO |
| `steady_hands` | no | no | Human | Defender | NoStyleRequired | Unknown | - | - | - | - | LowExposure | NO |
| `steamroller` | no | si | Human | Defender | NoStyleRequired | Adequate | - | - | Tackle | Defender | GenuinelyRare | NO |
| `survivor` | no | no | Human | Defender | NoStyleRequired | Unknown | - | - | - | - | LowExposure | NO |
| `sweeper_keeper` | no | si | Human | Goalkeeper | NoStyleRequired | FixedByPerk | - | - | - | - | GenuinelyRare | NO |
| `tough_hide` | no | no | Human | Defender | NoStyleRequired | NoActionRequired | - | - | - | - | GenuinelyRare | NO |
| `unlikely_bulwark` | no | no | Elf | Midfielder | FixedByPerk | WrongPosition | - | - | Tackle | Defender | WrongPosition | NO |
| `wing_overlap` | no | no | Human | Defender | NoStyleRequired | WrongPosition | - | - | Dribble | Forward | WrongPosition | NO |
