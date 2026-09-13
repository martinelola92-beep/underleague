# Catálogo de perks, objetos y consumibles

> Generado desde `/data` con `tools/generate-catalog.py` el 13 sep 2026. Las descripciones de los perks son las que produce
> `Sim.Perks.DescriptionGenerator` (RT-035, `dotnet run --project Balance -c Release -- --describe es`):
> no hay texto de efecto escrito a mano. Este documento es **derivado**; la fuente de verdad son los
> ficheros JSON de `/data` (RF-065, RT-031).

## Cómo se leen los números

Un perk no suma puntos porcentuales: **multiplica la cuota** del canal (`odds = p/(1−p)`, ADR 0050 P1).
Por eso el texto dice «multiplica por 2 sus opciones de robar el balón» y no «+15 %». Los valores legales
son `±15, ±30, ±50, ±100, ±200, ±300, ±500` en `/data`, que se leen como `k = 1 + n/100` y su inverso
exacto. El techo lo compra la rareza (ADR 0058): común ×2 · poco común ×3 · raro ×4 · legendario ×6.

Marcas: **☠** perk letal (puede matar; ADR 0046 y 0048) · **★** maestro de línea (ADR 0051: exige dos
perks de su línea y cierra otra para el resto de la run).

## Resumen

| Categoría | Cantidad |
|---|---|
| Perks (total en `/data/perks`) | 61 |
| — habilidades raciales (automáticas, no ocupan slot) | 5 |
| — perks obtenibles | 56 |
| — letales | 4 |
| Objetos de equipamiento | 34 |
| Consumibles | 4 |

Distribución RF-069 (objetivo 60/30/10 ± 8): relleno 34 (55,7 %) · condicional 20 (32,8 %) · rompe-reglas 7 (11,5 %)

## Habilidades raciales

Una por raza (RF-031b, ADR 0026). Se asignan solas a toda la plantilla de esa raza, son gratis, irrenunciables y **no ocupan slot de perk**.

| Nombre | id | Rareza | Acto | Tipo | Requisitos | Qué hace |
|---|---|---|---|---|---|---|
| **Toque** | `elf_touch` | Poco común | 1 | relleno | solo Elfos | Al empezar el partido, el portador multiplica por 1,3 su resistencia a las entradas. |
| **Sangre caliente** | `hot_blooded` | Poco común | 1 | relleno | solo Orcos | Al empezar el partido, el portador deja al rival derribado más tiempo con sus entradas. |
| **No sienten nada** | `numb` | Poco común | 1 | relleno | solo No-muertos | Al empezar el partido, el portador no entra en duelo cuando pierde a un vinculado y el portador no sufre penalización por lesiones leves. |
| **Adaptables** | `quick_learner` | Poco común | 1 | relleno | solo Humanos | Al terminar el partido, el portador gana un 25% más de experiencia. |
| **Raíces** | `roots` | Poco común | 1 | relleno | solo Enanos | Al empezar el partido, el portador no puede ser desplazado por empujones. |

## Perks por línea de build (ADR 0051)

Cuatro líneas con un maestro cada una. El maestro exige llevar ya dos perks de su línea y **cierra la línea opuesta** para el resto de la run.

### La Carnicería (`butchery`) — 8 perks

| Nombre | id | Rareza | Acto | Tipo | Requisitos | Qué hace |
|---|---|---|---|---|---|---|
| **Diezmo de sangre** ★ | `blood_tithe` | Raro | 2 | rompe-reglas | — | Al empezar el partido, si el portador tiene más de 1 Bruto en su equipo, el equipo multiplica por 2 sus opciones de lesionar y el equipo rival multiplica por 1,15 sus opciones de causar una lesión grave. Exige llevar ya 2 perks de La Carnicería. Cierra El Toque para el resto de la run. |
| **Botas de bruto** | `brute_boots` | Común | 1 | relleno | — | Al empezar el partido, si el portador es Bruto, el portador +10 de fuerza durante el partido. |
| **Marcador de sombra** | `shadow_marker` | Común | 1 | relleno | — | Al entrar, si el jugador tiene cerca un Bruto a 2 casillas, el jugador multiplica por 2 sus opciones de lesionar. |
| **Nudillos marcados** | `bruised_knuckles` | Poco común | 1 | condicional | — | Al hacer falta, si el portador es Bruto, el jugador multiplica por 1,3 sus opciones de lesionar por cada falta (hasta 3 veces) (máximo 1 por partido). |
| **Mentalidad de manada** | `pack_mentality` | Poco común | 1 | condicional | — | Al empezar el partido, si el portador tiene más de 2 Bruto en su equipo, los Bruto del equipo +10 de fuerza durante el partido. |
| **Tejido cicatricial** | `scar_tissue` | Poco común | 1 | relleno | — | En una lesión, el jugador multiplica por 1,3 sus opciones de lesionar por cada lesión (hasta 3 veces) (máximo 1 por partido). |
| **Tacos de hierro** ☠ | `iron_studs` | Raro | 3 | rompe-reglas | — | Al entrar, si el jugador está en zona rival, el rival divide por 2 su resistencia a las entradas. Puede matar al rival implicado en la jugada, aunque esté sano. |
| **Sed de médula** ☠ | `marrow_thirst` | Raro | 3 | rompe-reglas | etiqueta `Aggressive` | Al entrar, si el portador empieza en su tercio adelantado, el portador multiplica por 3 sus opciones de lesionar y el equipo rival multiplica por 1,5 sus opciones de causar una lesión grave. Puede matar al rival implicado en la jugada, aunque esté sano. |

### El Toque (`craft`) — 8 perks

| Nombre | id | Rareza | Acto | Tipo | Requisitos | Qué hace |
|---|---|---|---|---|---|---|
| **Escuela del primer toque** ★ | `first_touch_school` | Raro | 2 | condicional | — | Al empezar el partido, si el portador tiene más de 1 Fino en su equipo, el equipo multiplica por 2 sus opciones de pasar y el equipo multiplica por 4 su resistencia a las intercepciones. Exige llevar ya 2 perks de El Toque. Cierra La Carnicería para el resto de la run. |
| **Toque fino** | `fine_touch` | Común | 1 | relleno | — | Al empezar el partido, si el portador es Fino, el portador multiplica por 2 sus opciones de pasar. |
| **Especialista de banda** | `flank_specialist` | Común | 1 | relleno | — | Al empezar el partido, si el portador empieza en cualquier fila de su izquierda o si el portador empieza en cualquier fila de su derecha, el portador multiplica por 2 sus opciones de regatear. |
| **Manos firmes** | `steady_hands` | Común | 1 | relleno | — | Al completar un pase, el jugador multiplica por 1,5 sus opciones de pasar por cada pase (hasta 5 veces) (máximo 1 por partido). |
| **Solapamiento de banda** | `wing_overlap` | Común | 1 | relleno | — | Al encarar, si el portador tiene compañero de su izquierda o si el portador tiene compañero de su derecha, el compañero de su izquierda y compañero de su derecha multiplica por 2 sus opciones de regatear. |
| **Control de multitud** | `crowd_control` | Poco común | 1 | condicional | — | Al encarar, si el jugador tiene cerca un Fino rival a 2 casillas, el jugador multiplica por 3 sus opciones de regatear. |
| **Orquesta fina** | `fine_orchestra` | Poco común | 1 | condicional | etiqueta `Fine` | Al empezar el partido, si el portador tiene más de 2 Fino en su equipo, el equipo rival divide por 3 sus opciones de interceptar y el equipo multiplica por 3 su resistencia a las entradas. |
| **Veterano de seda** | `silky_veteran` | Poco común | 1 | relleno | — | Al ganar un regate, el jugador multiplica por 2 sus opciones de regatear por cada regate (hasta 5 veces) (máximo 1 por partido). |

### La Muralla (`wall`) — 8 perks

| Nombre | id | Rareza | Acto | Tipo | Requisitos | Qué hace |
|---|---|---|---|---|---|---|
| **Línea de granito** ★ | `granite_line` | Raro | 2 | condicional | — | Al empezar el partido, si el portador empieza en su tercio atrasado, el equipo multiplica por 3 sus opciones de robar el balón y el equipo rival divide por 1,5 sus opciones de tirar a puerta. Exige llevar ya 2 perks de La Muralla. Cierra La Puntería para el resto de la run. |
| **Espalda con espalda** | `back_to_back` | Común | 1 | relleno | — | Al entrar, si el jugador tiene cerca un Muro (enanos) a 2 casillas, el jugador multiplica por 2 sus opciones de robar el balón. |
| **Postura de muro** | `bulwark_stance` | Común | 1 | relleno | — | Al empezar el partido, si el portador es Muro (enanos), el portador multiplica por 2 sus opciones de robar el balón. |
| **Gestión del partido** | `game_management` | Común | 1 | relleno | — | Al entrar, si la diferencia de goles es mayor que 0, el jugador multiplica por 2 sus opciones de robar el balón. |
| **Ancla del tercio propio** | `own_third_anchor` | Común | 1 | relleno | — | Al empezar el partido, si el portador empieza en su tercio atrasado, el portador multiplica por 2 sus opciones de robar el balón. |
| **Veterano del foso** | `pit_veteran` | Común | 1 | condicional | — | Al entrar, si el portador empieza en su tercio atrasado, el jugador multiplica por 1,5 sus opciones de robar el balón por cada entrada (hasta 5 veces) (máximo 1 por partido). |
| **Red de seguridad** | `safety_net` | Común | 1 | relleno | solo portero | Al parar, si el jugador tiene cerca un defensa a 3 casillas, el jugador multiplica por 2 sus opciones de parar. |
| **Último recurso** | `last_ditch` | Poco común | 1 | condicional | — | Al entrar, si el jugador está en zona propia, el jugador multiplica por 3 sus opciones de robar el balón. |

### La Puntería (`aim`) — 8 perks

| Nombre | id | Rareza | Acto | Tipo | Requisitos | Qué hace |
|---|---|---|---|---|---|---|
| **Distancia de tiro** ★ | `killing_range` | Raro | 2 | condicional | — | Al tirar, si el jugador está a menos de 6 casillas de portería, el jugador multiplica por 4 sus opciones de tirar a puerta. Exige llevar ya 2 perks de La Puntería. Cierra La Muralla para el resto de la run. |
| **Depredador de área** | `box_predator` | Común | 1 | relleno | — | Al tirar, si el jugador está a menos de 3 casillas de portería, el jugador multiplica por 2 sus opciones de tirar a puerta. |
| **Sangre fría** | `cold_focus` | Común | 1 | relleno | — | Al tirar, si el jugador es Frío, el jugador multiplica por 2 sus opciones de tirar a puerta. |
| **Amenaza de larga distancia** | `long_range_menace` | Común | 1 | relleno | — | Al tirar, si el jugador está a 6 casillas de portería o más, el jugador multiplica por 2 sus opciones de tirar a puerta. |
| **Línea de ataque** | `forward_line` | Poco común | 1 | relleno | — | Al empezar el partido, si el portador empieza en su tercio adelantado, el portador multiplica por 3 sus opciones de tirar a puerta. |
| **Instinto cazagoles** | `poacher_instinct` | Poco común | 1 | relleno | — | Al marcar, el jugador multiplica por 2 sus opciones de tirar a puerta por cada gol (hasta 5 veces) (máximo 1 por partido). |
| **Ensayo de tirador** | `sharpshooter_drill` | Poco común | 1 | relleno | — | Al tirar, el jugador multiplica por 2 sus opciones de tirar a puerta por cada tiro (hasta 5 veces) (máximo 1 por partido). |
| **Punta de lanza** | `spearpoint` | Poco común | 1 | condicional | — | Al empezar el partido, si el portador tiene compañero de delante, el compañero de delante multiplica por 3 sus opciones de tirar a puerta. |

## Perks sin línea — 24

No cuentan para ningún maestro ni cierran nada.

| Nombre | id | Rareza | Acto | Tipo | Requisitos | Qué hace |
|---|---|---|---|---|---|---|
| **Presión diagonal** | `diagonal_press` | Común | 1 | relleno | — | Al entrar, si el portador tiene compañero en diagonal hacia delante o si el portador tiene compañero en diagonal hacia atrás, el compañero en diagonal hacia delante y compañero en diagonal hacia atrás multiplica por 2 sus opciones de robar el balón. |
| **Pulmones de hierro** | `iron_lungs` | Común | 1 | relleno | — | Al empezar el partido, el portador +3 de resistencia por cada partido (máximo 10) durante el partido. |
| **Doble pivote** | `pivot_duo` | Común | 1 | relleno | — | Al empezar el partido, si el portador tiene compañero de su izquierda o si el portador tiene compañero de su derecha, el compañero de su izquierda y compañero de su derecha multiplica por 2 sus opciones de robar el balón. |
| **Portero líbero** | `sweeper_keeper` | Común | 1 | relleno | solo portero | Al recuperar el balón, si el jugador está en zona propia, el jugador +2 de correa durante la jugada. |
| **Lector de la batalla** | `battle_reader` | Poco común | 1 | relleno | — | Al empezar el partido, el portador multiplica por 2 sus opciones de interceptar por cada partido (hasta 4 veces). |
| **Voz de capitán** | `captains_voice` | Poco común | 1 | condicional | — | Al empezar el partido, si el portador empieza en su tercio central, el equipo multiplica por 2 sus opciones de robar el balón por cada partido (hasta 3 veces). |
| **Conductor del centro** | `center_conductor` | Poco común | 1 | condicional | — | Al empezar el partido, si el portador empieza en la fila central, el equipo multiplica por 2 sus opciones de interceptar. |
| **Legado de portería a cero** | `clean_sheet_legacy` | Poco común | 1 | relleno | solo portero | Al parar, el jugador multiplica por 1,3 sus opciones de parar por cada parada (hasta 3 veces) (máximo 1 por partido). |
| **Espíritu de remontada** | `comeback_spirit` | Poco común | 1 | condicional | — | Al empezar la jugada, si la diferencia de goles es menor que 0, el jugador +10 de fuerza durante la jugada. |
| **Sombra de cobertura** | `covering_shadow` | Poco común | 1 | condicional | — | Al empezar el partido, si el portador tiene compañero de detrás, el compañero de detrás multiplica por 3 sus opciones de interceptar. |
| **Disparador de presión alta** | `high_press_trigger` | Poco común | 1 | condicional | — | Al fallar un pase, si el jugador está en zona rival, el equipo multiplica por 2 sus opciones de interceptar. |
| **Árbitro casero** | `home_ref` | Poco común | 1 | condicional | — | Al hacer falta, si la diferencia de goles es menor que 0, criterio del árbitro +10. |
| **Lector de carriles** | `lane_reader` | Poco común | 1 | relleno | — | Al recuperar el balón, el jugador multiplica por 2 sus opciones de interceptar por cada recuperación (hasta 5 veces) (máximo 1 por partido). |
| **Líder nato** | `natural_leader` | Poco común | 1 | condicional | etiqueta `Leader` | Al empezar el partido, el equipo multiplica por 3 sus opciones de robar el balón. |
| **Curtido en mil batallas** | `road_warrior` | Poco común | 1 | condicional | — | Al recuperar el balón, si el jugador lleva al menos 2 entradas ganadas, el equipo multiplica por 3 sus opciones de robar el balón. |
| **Muro improbable** | `unlikely_bulwark` | Poco común | 1 | condicional | solo Elfos, etiqueta `Bulwark` | Al empezar el partido, el portador multiplica por 3 sus opciones de robar el balón y el portador +2 de correa durante el partido. |
| **Marcha sin fin** | `deathless_march` | Raro | 2 | relleno | solo No-muertos | Al empezar el partido, el equipo multiplica por 1,5 sus opciones de robar el balón por cada partido (hasta 3 veces). |
| **Gigante amable** | `gentle_giant` | Raro | 2 | condicional | solo Orcos | Al empezar el partido, si el portador tiene compañero de delante, el compañero de delante multiplica por 2 sus opciones de interceptar. |
| **Puerta de hierro** | `iron_gate` | Raro | 2 | rompe-reglas | solo Enanos | En una lesión, anula la lesión (máximo 1 por partido). |
| **Correa curtida** | `long_leash_legacy` | Raro | 2 | relleno | — | Al empezar el partido, el portador +1 de correa por cada 4 de partido (máximo 2) durante el partido. |
| **Instigador de la turba** | `mob_instigator` | Raro | 2 | rompe-reglas | — | Al hacer falta, en la turba, anula la falta (máximo 2 por partido). |
| **Veterano de cicatrices** | `scar_veteran` | Raro | 2 | relleno | — | Al empezar el partido, el portador +3 de fuerza por cada partido (máximo 10) durante el partido. |
| **La segunda herida** ☠ | `second_wound` | Raro | 3 | rompe-reglas | — | En una lesión, si la diferencia de goles es como mucho 0, el jugador multiplica por 1,15 sus opciones de causar una lesión grave. Puede matar al rival implicado en la jugada, aunque esté sano. |
| **Partecráneos** ☠ | `skullsplitter` | Legendario | 3 | rompe-reglas | etiqueta `Dirty` | Al entrar, el equipo rival multiplica por 3 sus opciones de lesionarse. Puede matar al rival implicado en la jugada, aunque esté sano. |

## Perks letales ☠

Los únicos perks que pueden matar (ADR 0046 vía 2, ADR 0048). Los cuatro cubren canales distintos, cada
activación marca a **un solo rival** —el que peor lo tiene— y todos se cobran **en la jugada**, nunca en el
saque (regla del cargador).

`lethalChance` es la probabilidad **base** de la tirada, en base 10.000 (900 = 9 %). Sobre ella actúan
multiplicativamente las tres cosas que el jugador decide antes de confirmar la alineación (RF-012c,
`data/sim/tuning.json`):

- **en qué estado alinea**: sano ×1, tocado ×8, lesión grave ×25;
- **a quién alinea**: fuerza del portador contra aguante de la víctima (`relativeFactor` 6);
- **dónde lo coloca**: distancia de emparejamiento entre portador y víctima.

La tirada final está acotada a `maxChance` 8.000 (80 %): al tope, un tocado marcado no vuelve. Por eso el
9.000 de *La segunda herida* no es «90 % de muertes»: es el letal de **menor conversión** de los cuatro,
porque exige una segunda lesión sobre el mismo jugador y el marcador tiene que ir empatado o por debajo.

| Nombre | id | Rareza | Acto | Canal / disparador | `lethalChance` base | Qué hace |
|---|---|---|---|---|---|---|
| **La segunda herida** | `second_wound` | Raro | 3 | INJURY | 9000 (90,0 %) | En una lesión, si la diferencia de goles es como mucho 0, el jugador multiplica por 1,15 sus opciones de causar una lesión grave. Puede matar al rival implicado en la jugada, aunque esté sano. |
| **Partecráneos** | `skullsplitter` | Legendario | 3 | TACKLE | 1200 (12,0 %) | Al entrar, el equipo rival multiplica por 3 sus opciones de lesionarse. Puede matar al rival implicado en la jugada, aunque esté sano. |
| **Sed de médula** | `marrow_thirst` | Raro | 3 | TACKLE | 900 (9,0 %) | Al entrar, si el portador empieza en su tercio adelantado, el portador multiplica por 3 sus opciones de lesionar y el equipo rival multiplica por 1,5 sus opciones de causar una lesión grave. Puede matar al rival implicado en la jugada, aunque esté sano. |
| **Tacos de hierro** | `iron_studs` | Raro | 3 | TACKLE | 760 (7,6 %) | Al entrar, si el jugador está en zona rival, el rival divide por 2 su resistencia a las entradas. Puede matar al rival implicado en la jugada, aunque esté sano. |

## Objetos de equipamiento

Un objeto **sube atributos y nada más** (ADR 0036). La rareza fija **cuántos** atributos toca
(común 1 · poco común 2 · raro 3 · legendario 4), la magnitud base es siempre **±10**, y el arquetipo
cambia la magnitud o el riesgo:

| Arquetipo | Qué cambia |
|---|---|
| normal | sin contrapartida |
| maldito | ×2 la magnitud, en lo que sube **y** en lo que baja (±20) |
| frágil | magnitud normal, pero se rompe con `breakChancePercent` por partido; cuesta el 55 % y aparece el doble de veces en la tienda |
| restringido | sin rareza, 3 atributos a magnitud normal, **solo para una raza**; abre una build que esa raza no puede permitirse por su sesgo racial |

Valor marginal medido por atributo (milésimas de punto de tasa de victoria por cada +20 repartidos entre los diez jugadores, fase 1b, ADR 0038): fuerza 111 · velocidad 66 · técnica 75 · resistencia 30 · correa 40. Es la tabla con la que se calcula el precio en vez de medirlo.

### Normales — 11 objetos

| Nombre | id | Rareza | Acto | Atributos | Nota |
|---|---|---|---|---|---|
| **Cinturón de aguante** | `endurance_belt` | Común | 1 | +10 resistencia | — |
| **Lente de enfoque** | `focus_lens` | Común | 1 | +10 técnica | — |
| **Guanteletes de hierro** | `iron_gauntlets` | Común | 1 | +10 fuerza | — |
| **Amuleto de correa larga** | `loose_leash_charm` | Común | 1 | +10 correa | — |
| **Botas gastadas** | `worn_boots` | Común | 1 | +10 velocidad | — |
| **Guantes del duelista** | `duelists_gloves` | Poco común | 1 | +10 velocidad, +10 técnica | — |
| **Grebas del velocista** | `sprinters_greaves` | Poco común | 1 | +10 velocidad, +10 resistencia | — |
| **Brazalete de veterano** | `veteran_armband` | Poco común | 1 | +10 técnica, +10 resistencia | — |
| **Vendas lastradas** | `weighted_wraps` | Poco común | 1 | +10 fuerza, +10 resistencia | — |
| **Faja de campeón** | `champions_sash` | Raro | 2 | +10 fuerza, +10 técnica, +10 resistencia | — |
| **Arnés de azogue** | `quicksilver_harness` | Raro | 2 | +10 velocidad, +10 técnica, +10 correa | — |

### Malditos — 4 objetos

Suben el doble y bajan el doble. Excelentes en el jugador correcto y ruinosos en el resto: la decisión es a quién se lo pones.

| Nombre | id | Rareza | Acto | Atributos | Nota |
|---|---|---|---|---|---|
| **Hombrera del bruto** | `brutes_pauldron` | Común | 1 | +20 fuerza, -20 velocidad | Maldito comun: sube un atributo (lo que su rareza permite) el doble, y baja el doble en velocidad. Excelente en un central de bloque bajo, ruinoso en cualquiera que tenga que cubrir campo. |
| **Tacos de cristal** | `glass_cannon_spikes` | Poco común | 1 | +20 velocidad, +20 técnica, -20 resistencia | Maldito poco comun: el extremo que desborda a cualquiera y al que cualquier entrada parte por la mitad, porque la resistencia es lo que aguanta la lesion. |
| **Tótem del berserker** | `berserker_totem` | Raro | 2 | +20 fuerza, +20 velocidad, +20 resistencia, -20 técnica | Maldito raro: una bestia fisica que no sabe jugar. En un central o un pivote destructor es una ganga; en el organizador del equipo tira la build a la basura. |
| **Reliquia del mártir** | `martyrs_relic` | Raro | 2 | +20 técnica, +20 resistencia, +20 correa, -20 fuerza | Maldito raro: el organizador que lo ve todo, cubre todo el campo y no gana un solo duelo. Baja fuerza, que es el atributo mas caro del catalogo. |

### Frágiles — 4 objetos

Baratos y frecuentes, pero se rompen.

| Nombre | id | Rareza | Acto | Atributos | Rotura/partido |
|---|---|---|---|---|---|
| **Hoja mellada** | `chipped_blade` | Común | 1 | +10 fuerza | 30 % |
| **Visera agrietada** | `cracked_visor` | Común | 1 | +10 técnica | 30 % |
| **Estandarte deshilachado** | `frayed_banner` | Poco común | 1 | +10 resistencia, +10 correa | 25 % |
| **Talismán dorado** | `gilded_charm` | Raro | 2 | +10 fuerza, +10 velocidad, +10 técnica | 20 % |

### Restringidos por raza — 15 objetos

Sin rareza y sin contrapartida: la restricción **es** el coste. Cada uno abre la build que el sesgo racial le niega a esa raza.

| Nombre | id | Raza | Acto | Atributos | Qué abre |
|---|---|---|---|---|---|
| **Botas del camino profundo** | `deep_road_boots` | Enanos | 2 | +10 velocidad, +10 correa, +10 técnica | ABRE una build que los enanos no pueden permitirse: velocidad -14 y correa -18 de sesgo racial les prohiben presionar arriba y cubrir campo. Con estas botas el enano puede jugar adelantado, que es justo lo contrario de lo que le sale solo (ADR 0036: al menos uno por raza tiene que abrir, no reforzar… |
| **Anillo de fuego de forja** | `forgefire_ring` | Enanos | 2 | +10 fuerza, +10 resistencia, +10 correa | — |
| **Yunque de Grimhold** | `grimhold_anvil` | Enanos | 2 | +10 fuerza, +10 resistencia, +10 técnica | — |
| **Brazal de acero lunar** | `moonsteel_bracer` | Elfos | 2 | +10 fuerza, +10 resistencia, +10 correa | ABRE una build que los elfos no pueden permitirse: con fuerza -12 y resistencia -6 un elfo no sostiene un bloque bajo ni gana una entrada. Con el brazal si, y deja de tener que ganar todos los partidos con el balon. |
| **Diadema verdeante** | `verdant_circlet` | Elfos | 2 | +10 técnica, +10 resistencia, +10 velocidad | — |
| **Sandalias del canto del viento** | `windsong_sandals` | Elfos | 2 | +10 velocidad, +10 técnica, +10 correa | — |
| **Silbato de la academia** | `academy_whistle` | Humanos | 2 | +10 técnica, +10 resistencia, +10 correa | — |
| **Banderin del heraldo** | `heralds_pennant` | Humanos | 2 | +10 correa, +10 velocidad, +10 resistencia | ABRE una build que los humanos no pueden permitirse: sin sesgo racial, el humano nunca tiene correa ni velocidad de sobra para el futbol total de zonas amplias. Con el banderin puede jugar a moverse todo el rato en vez de a mantener la forma. |
| **Medalla de la liga de hierro** | `iron_league_medal` | Humanos | 2 | +10 fuerza, +10 técnica, +10 resistencia | — |
| **Estandarte de sangre** | `bloodbanner` | Orcos | 2 | +10 fuerza, +10 resistencia, +10 técnica | — |
| **Abalorios del chamán** | `shaman_beads` | Orcos | 2 | +10 técnica, +10 correa, +10 velocidad | ABRE una build que los orcos no pueden permitirse: con tecnica -10 un orco no juega al pase ni mantiene la posicion. Con los abalorios puede ganar sin pegar, que es la build que su sesgo racial le niega. |
| **Hombrera del jefe de guerra** | `warchiefs_pauldron` | Orcos | 2 | +10 fuerza, +10 resistencia, +10 velocidad | — |
| **Farol de tumba** | `grave_lantern` | No-muertos | 2 | +10 fuerza, +10 técnica, +10 resistencia | — |
| **Amuleto del osario** | `ossuary_charm` | No-muertos | 2 | +10 fuerza, +10 resistencia, +10 correa | — |
| **Tendones de podredumbre rápida** | `swiftrot_tendons` | No-muertos | 2 | +10 velocidad, +10 técnica, +10 correa | ABRE una build que los no-muertos no pueden permitirse: con velocidad -10 el contraataque les esta vedado y solo saben ganar por desgaste. Con los tendones pueden correr, que es la build contraria a la que su sesgo empuja. |

## Consumibles

Un solo partido, se gastan al usarlos. El efecto **no tiene portador**: lo usa el entrenador y alcanza a
**todos los jugadores de su equipo que estén en el campo** en ese instante, hasta el final del partido
(`EffectEngine.ResolveConsumables`, RF-080..082).

| Nombre | id | Rareza | Familia | Efecto |
|---|---|---|---|---|
| **Vendaje de campaña** | `field_bandage` | Común | médica | divide por 2 sus opciones de lesionarse |
| **Kit de golpe bajo** | `low_blow_kit` | Poco común | sucia | +12 fuerza; multiplica por 3 sus opciones de hacer falta |
| **Amuleto de la suerte** | `lucky_charm` | Poco común | sobrenatural | multiplica por 2 sus opciones de tirar a puerta |
| **Bengala de humo** | `smoke_flare` | Común | táctica | multiplica por 2 sus opciones de resistir entradas |

## Cómo regenerar este documento

```bash
dotnet run --project Balance -c Release -- --describe es > out/describe-es.txt   # RT-035
python3 tools/generate-catalog.py out/describe-es.txt
```

El resto sale de leer `/data/perks`, `/data/items`, `/data/consumables`, `/data/races` y
`/data/equipment/equipment.json`.
