# Catálogo de perks (Paquete G, fase 1)

30 perks: `bloodlust` y `veteran` son los dos ejemplos del paquete F (motor de efectos); el resto (28) se
diseñó aquí según `docs/fase1-diseno.md` §7. Formato y campos en §2; catálogo de builds en §8.

Distribución RF-069 (28 perks propios): 16 `filler` (57%), 9 `conditional` (32%), 3 `ruleBreaker` (11%).
Con los dos de ejemplo (`veteran` filler, `bloodlust` conditional): 18/9/3 sobre 30 → 60% / 30% / 10%.

Rareza: 14 `common`, 10 `rare`, 4 `legendary` (propios); con los dos de ejemplo, 15/11/4 sobre 30.

## Perks

| id | familia | rareza | tipo | trigger | resumen | builds |
|---|---|---|---|---|---|---|
| `heavy_boots` | Violencia | common | filler | MATCH_START | +6 fuerza si es Bruto, si no −3 (slot desperdiciado) | orc_violence, orc_mob |
| `bone_breaker` | Violencia | rare | filler | TACKLE | +6% de lesionar al rival si es Fino; prohibido para Finos | orc_violence |
| `enforcer` | Violencia | rare | filler | INJURY (equipo) | cada lesión de un compañero da +1 fuerza (máx. 6) a los Brutos esa jugada | orc_violence, orc_mob |
| `berserker` | Violencia | common | filler | TACKLE | +6 fuerza y +15% de falta en cada entrada, sin condición | orc_violence, elf_brawler |
| `warpath` | Violencia | legendary | filler | MATCH_START | +1 fuerza por partido jugado (máx. 8, acumula entre partidos); solo Brutos | orc_violence |
| `silk_touch` | Técnica | common | filler | MATCH_START | +6 técnica si es Fino, si no −3 | elf_tiki_taka, orc_misplaced, human_random |
| `one_touch` | Técnica | common | conditional | PASS_ATTEMPTED | +8% de acierto de pase si hay un Fino adyacente | elf_tiki_taka, orc_misplaced |
| `matador` | Técnica | common | filler | DRIBBLE_ATTEMPTED | +12% de regate si el rival es Bruto; solo Finos | elf_tiki_taka, elf_glass |
| `showboat` | Técnica | common | filler | DRIBBLE_ATTEMPTED | +15% de regate si es Fino, si no −15% (antisinergia declarada) | elf_tiki_taka, orc_misplaced |
| `playmaker` | Técnica | legendary | conditional | MATCH_START | +8 técnica a todos los Finos del equipo si hay ≥3 Finos compañeros; si no, −3 al portador; solo Finos | elf_tiki_taka, elf_glass |
| `shield_wall` | Bloque/muro | rare | conditional | MATCH_START | +5 fuerza y +5% de robo a los Defensas adyacentes si hay algún Defensa adyacente; si no, −3 | human_wall, human_scattered, orc_violence |
| `anchor` | Bloque/muro | common | filler | MATCH_START | +1 de correa; solo Defensas | human_wall, human_scattered |
| `sweeper` | Bloque/muro | common | conditional | RECOVERY | +1 de correa esa jugada si recupera en zona propia | human_wall, human_counter, elf_tiki_taka, elf_glass, human_random |
| `goalkeeper_wall` | Bloque/muro | common | filler | MATCH_START | +8% de parada; solo porteros | human_wall |
| `counter_punch` | Contragolpe | common | conditional | RECOVERY (equipo) | +8 velocidad a los Delanteros esa jugada si un compañero recupera en zona propia | human_counter |
| `sprinter` | Contragolpe | common | filler | PLAY_START | +6 velocidad esa jugada si es Rápido, si no −3 | human_counter |
| `mob_lawyer` | Turba | legendary | ruleBreaker | FOUL | anula la falta propia en la turba (máx. 2/partido) | orc_mob, human_random |
| `street_fighter` | Turba | rare | conditional | TACKLE | +8 fuerza y −4 técnica esa jugada en la turba | orc_mob |
| `innocent_face` | Rompe-reglas | rare | ruleBreaker | CARD | anula la propia tarjeta (1/partido) | elf_glass, human_wall |
| `lucky_charm` | Rompe-reglas | rare | ruleBreaker | INJURY | anula la propia lesión (1/partido) si hay algún Defensa en el equipo | human_wall |
| `guardian_angel` | Rompe-reglas | legendary | conditional | MATCH_START | −12% de lesión grave si hay ≥2 Finos compañeros | elf_glass |
| `target_man` | Posición | rare | conditional | SHOT | +4 fuerza al tirar si es Bruto y tiene un centrocampista adyacente, si no −3; solo Delanteros | orc_violence, elf_brawler, human_random |
| `glass_cannon` | Fragilidad | common | filler | MATCH_START | +10 técnica y −10 resistencia, sin condición | elf_glass, elf_brawler |
| `lone_wolf` | Fragilidad | rare | conditional | MATCH_START | +8 fuerza si no tiene ningún centrocampista adyacente, si no −6 | human_random |
| `bloodline` | Escalado | rare | filler | INJURY | +1 fuerza por lesión sufrida en la carrera (máx. 10, acumula) | orc_violence, orc_mob |
| `poacher` | Escalado | rare | filler | GOAL | +2 técnica por gol marcado en la carrera (máx. 12, acumula) | elf_tiki_taka, human_counter, orc_violence |
| `bookworm` | Escalado | common | filler | PASS_COMPLETED | +1 técnica cada 25 pases completados en la carrera (máx. 6, acumula); solo centrocampistas con ≥2 compañeros centrocampistas | human_random |
| `iron_lungs_plus` | Escalado | common | filler | MATCH_START | +1 resistencia por partido jugado (máx. 8, acumula) | human_counter |

Ejemplos del paquete F (no tocados aquí): `bloodlust` (rare, conditional, TACKLE) y `veteran` (common,
filler, MATCH_START, acumula) — no se usan en ninguna build propia de este paquete, quedan disponibles
para builds futuras.

### Cobertura de requisitos del encargo

- Distribución: 60/30/10 (ver arriba).
- `accumulatesAcrossMatches: true`: `warpath`, `bloodline`, `poacher`, `bookworm`, `iron_lungs_plus` (5,
  + `veteran` = 6).
- `tagsRequired`/`tagsForbidden`: `enforcer`, `warpath` (Bruto), `playmaker`, `matador` (Fino),
  `bone_breaker` (prohibido Fino), `target_man` (Delantero) — 6.
- `elseEffects`: `heavy_boots`, `silk_touch`, `showboat`, `shield_wall`, `playmaker`, `sprinter`,
  `target_man`, `lone_wolf` — 8.
- `positionOnly`: `anchor` (Defensa), `goalkeeper_wall` (Portero), `target_man` (Delantero), `bookworm`
  (Centrocampista) — 4.
- `adjacent`/`adjacentCount`/`adjacentWithTag`: `one_touch`, `shield_wall`, `lone_wolf`, `target_man` — 4.
- `teammatesWithTag`: `playmaker`, `guardian_angel`, `bookworm` — 3.
- `cancelEvent` (CARD/INJURY/FOUL): `mob_lawyer`, `innocent_face`, `lucky_charm` — 3.

## Builds (`data/balance/builds/`)

| id | raza | idea | resultado esperado |
|---|---|---|---|
| `human_none` | Human | referencia sin perks | — (línea base) |
| `orc_none` | Orc | referencia sin perks | — (línea base) |
| `elf_none` | Elf | referencia sin perks | — (línea base) |
| `orc_violence` | Orc | contacto físico: entradas duras, lesionar Finos, escalado de fuerza (`warpath`, `bloodline`), delantero Bruto apoyado por un centrocampista adyacente | gana a `orc_none` (≥58%) |
| `elf_tiki_taka` | Elf | pases y regates entre Finos, cadena de adyacencias (`one_touch`), `playmaker` legendario que sube la técnica de todo el bloque Fino | gana a `elf_none` (≥58%); cadena de pases claramente mayor que `orc_violence` |
| `human_wall` | Human | bloque defensivo: dos Defensas adyacentes con `shield_wall`, portero reforzado, `lucky_charm`/`innocent_face` para no perder efectivos | gana a `human_none` (≥58%) |
| `human_counter` | Human | velocidad y transición: recuperar en zona propia y lanzar a los Delanteros, delantero rápido que escala resistencia y goles | gana a `human_none` (≥58%) |
| `orc_mob` | Orc | apuesta por el gol de oro de la turba: `mob_lawyer` anula faltas propias, `street_fighter` y `enforcer` premian el caos | gana a `orc_none` (≥58%) |
| `elf_glass` | Elf | coherente pero frágil: `glass_cannon` sube técnica y baja resistencia, `guardian_angel` y `playmaker` la protegen solo si el equipo es mayoritariamente Fino | gana a `elf_none`, pero con más riesgo (más lesiones/menos margen) que las demás coherentes |
| `orc_misplaced` | Orc | perks técnicos (`silk_touch`, `showboat`, `one_touch`) puestos en un equipo Bruto: condición `hasTag(owner,'Fine')`/`adjacent(...,'Fine')` siempre falsa, sin bonus y con castigo real de `showboat`/`silk_touch` | pierde contra `orc_none` (≤45%) |
| `elf_brawler` | Elf | perks de violencia (`glass_cannon` + `berserker`, `target_man`) en un equipo Fino: mucha falta y poca fuerza real, `target_man` castiga por no ser Bruto | pierde contra `elf_none` (≤45%) |
| `human_random` | Human | mezcla arbitraria y fija de familias sin criterio (turba, escalado, posición, fragilidad) en un equipo Neutral que no cumple casi ninguna condición de raza | entre 40% y 60% contra `human_none` |
| `human_scattered` | Human | dos `shield_wall` + `anchor` en los Defensas, pero el `lineup` los separa a distancia no adyacente a propósito, así que `shield_wall` cae siempre en su `elseEffects` (−3 fuerza) en vez de activarse | pierde contra `human_none` (≤45%) |

Notas de asignación: todas las combinaciones de `positionOnly`/`tagsRequired`/`tagsForbidden` se
comprobaron a mano contra `data/schemas/perks.schema.json` y `Sim/Perks/*` — ninguna build (ni las malas)
tiene una asignación inválida; la incoherencia de `orc_misplaced`, `elf_brawler` y `human_random` viene de
condiciones que se evalúan a falso (o a su `elseEffects`), y la de `human_scattered` viene del `lineup`
rompiendo la adyacencia que `shield_wall` necesita, no de un perk mal asignado.
