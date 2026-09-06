# Reajuste del bloque de rediseño espacial: resultados (paquete U)

Cierre del bloque de las ADR 0020-0030. Es la medición de referencia del rediseño y sustituye, como línea
base vigente, a `docs/balance/fase1-perks.md`, que queda como registro del motor anterior.

Todas las cifras son de **`/Balance`** o de la puerta de `Sim.Tests`, con la semilla y el tamaño de muestra
indicados en cada tabla. Comandos de reproducción al final (§9).

---

## 1. Qué era defecto y qué era calibración

El encargo pedía separar las dos cosas. Salieron **siete defectos**, cinco de ellos invisibles hasta que se
midió el comportamiento que decían tener:

| # | Defecto | Cómo se manifestaba | Corrección |
|---|---|---|---|
| D1 | El bloqueo compartía enfriamiento con la entrada (§6.8) | `tacklesPerMatch` 4,37 -> 2,31: cargar sin balón dejaba al jugador sin poder disputar el balón 150 ticks | Contador propio `MatchPlayer.BlockCooldown` |
| D2 | El dial de calidad no diferenciaba | `TeamGenerator` traducía `quality` a `nivel = quality/10`: calidad 60 contra 40 eran **16 puntos de presupuesto sobre 290**. Medido: el equipo "mejor" de calidad 60 ganaba el **40,8%** contra uno de calidad 50 | `quality` vuelve a ser la media objetivo de atributos: desplaza presupuesto (`+5` por punto) y banda (`+1` por punto). Nivel y rareza pasan a ser diales propios de `reference.json` y de las builds |
| D3 | El tercio atacante era inalcanzable | `LinkGeometry.ZoneOfHome` medía los tercios sobre las **16 columnas del campo** (11-15 = tercio atacante), pero una casilla-hogar vive en las columnas 0-7: `startsIn(owner,'AttackingThird')` era una condición imposible y `forward_line` letra muerta | Los tercios se miden sobre `Pitch.PlacementColumns` (8): 0-2, 3-5, 6-7 |
| D4 | Ningún vínculo direccional se resolvía | Con `Lineup.Default` (columnas 0, 2, 4, 6 y compañeros de línea a dos filas) **ninguna** de las siete relaciones de la ADR 0021 encontraba candidato: los seis perks del eje de colocación aplicaban siempre su `elseEffects` | Alineación por defecto 2-3-1: GK (0,2); DEF (2,1),(2,3); MID (3,2),(4,1),(4,3); FWD (6,2) |
| D5 | Un perk de vínculo no podía castigar la mala colocación | Los seis llevaban `condition: ""` (siempre cierta), así que sus `elseEffects` **nunca** se aplicaban: si el vínculo no existía, el perk simplemente no hacía nada | Condición explícita `linked(owner,'<relación>')` en los seis |
| D6 | Bucle de realimentación del árbitro | `biasFoulShiftPer10` 400 sobre un `foulBase` de 1.200: con \|criterio\| medio de 35,7 y el 10% de los partidos saturando en 100, las faltas se disparaban, las faltas movían el criterio y el criterio disparaba más faltas. Resultado: **1,04 rojas y 27,5% de incomparecencias por partido** | Efectos del criterio a 120/100/100 por cada 10 puntos y desplazamientos a la mitad; \|criterio\| medio 14,8, rojas 0,07, incomparecencias 2,65% |
| D7 | `Vec2.ToString()` desbordaba la pila | El `PrintMembers` generado para un `record struct` recorre las propiedades públicas, incluida `Normalized`, que es otro `Vec2`. Cualquier aserción fallida de xUnit que formateara un vector **abortaba la ejecución entera de los tests** (se veían 128 de 343) | `ToString()` escrito a mano + test de regresión |

Dos más, de contrato de datos, que el validador dejaba pasar:

- La escala de `modifyAttribute` (3/5/8/10) y `modifyLeash` (1/2) de `fase1b-diseno.md` §1.4 **no estaba
  implementada**: `brute_boots` y `fine_touch` llevaban 15 puntos de atributo y `sweeper_keeper`,
  `unlikely_bulwark` y `long_leash_legacy` daban 5 y 10 **casillas** de correa en un campo de cinco filas.
  Corregidos los valores y añadidos los `if/then` que faltaban en `data/schemas/perks.schema.json`.
- `noDeadPerks` exigía activación **por cada pareja (perk, build)**, incluidas las builds mal construidas a
  propósito, cuyo sentido es justamente que sus perks no se disparen. El criterio de §8 es "en alguna build
  que lo lleve": `BuildMetrics` emite ahora una fila informativa por pareja y una fila con estado por perk.

Todo lo demás fue calibración: 34 valores de `data/ai/weights.json` y `data/sim/tuning.json`, los valores
de 24 perks y la composición de las 15 builds.

---

## 2. RT-056: antes y después

2.000 partidos, semilla 1, `data/balance/reference.json`.
**Antes** = `1994bae` (cierre del paquete V). **Después** = estado de cierre del paquete U.

| Métrica | Rango | Antes | Después |
|---|---|---|---|
| `possessionChanges` | 12-25 | 23,15 IN | **23,61 IN** |
| `passChainAvgLength` | 2-4 | 1,56 **OUT** | **2,32 IN** |
| `shotsPerMatch` | 8-16 | 15,26 IN | **11,81 IN** |
| `scorelineShare_1-0_to_3-2` | >= 50 | 58,30 IN | **79,15 IN** |
| `share_over5goals` | < 5 (INFO) | 25,90 | **10,90** |
| `drawShareAtRegulation` | < 15 (INFO, I-11) | 21,85 | 27,75 |
| `ballThirdMaxShare` | <= 50 | 35,99 IN | **39,98 IN** |
| `tacklesPerMatch` | 6-14 | 2,40 **OUT** | **9,96 IN** |
| `injuriesPerMatch` | 0,3-0,8 | 0,57 IN | **0,62 IN** |
| `betterTeamWinRate` 60 vs 40 (Δ20) | 65-80 | 52,55 **OUT** | **69,37 IN** |
| `betterTeamWinRate` 60 vs 50 (Δ10, INFO) | 55-70 | 40,84 | 50,75 |

**Las cuatro rojas se cierran.** `drawShareAtRegulation` sube porque el partido tiene menos goles (3,59
frente a 4,18): es la inconsistencia I-11, matemáticamente incompatible con el resto de la fila de
resultados, y sigue siendo `INFO`.

Fuera de RT-056, en el mismo lote:

| | Antes | Después |
|---|---|---|
| Incomparecencias (RF-059) | **27,5%** | **2,65%** |
| Tarjetas rojas por partido | 1,04 | 0,07 |
| Faltas señaladas por partido | 4,57 | 3,90 |
| Bloqueos por partido | (sin columna) | 4,78 |
| Goles por partido | 4,18 | 3,59 |
| \|criterio final\| medio | 35,7 | 14,8 |
| Rendimiento | 272 partidos/s | 231 partidos/s |

Un 27,5% de partidos decididos por incomparecencia no es "balance apretado": es que el partido se
descomponía en una pelea. Los 231 partidos/s siguen muy por encima de los 167 que exige RT-051.

**Estabilidad entre semillas** (2.000 partidos cada una): `possessionChanges` 22,5-23,6 · `passChain`
2,31-2,41 · `shotsPerMatch` 9,0-11,8 · `tacklesPerMatch` 10,0-12,4 · `injuriesPerMatch` 0,39-0,64. La única
métrica con dispersión grande es `betterTeamWinRate` (69-84 según la semilla), y es **esperado**: mide dos
plantillas concretas y su muestra efectiva es el número de semillas, no el de partidos (`balance.md`).
Medida sobre **ocho parejas de plantillas y 3.200 partidos**, la media es **78,8%** para Δ20 y **66,6%**
para Δ10.

---

## 3. Valor marginal por atributo, remedido

+20 puntos del atributo a **toda** la plantilla contra una plantilla gemela sin el bono; 8 parejas de
plantillas, 3.200 partidos, local y visitante alternados (`reference.json.teams[].attributeBonus`, el
instrumento de medida que añade este paquete a `/Balance`).

| Atributo | Fase 1 (motor anterior, +10) | Paquete V (+20) | **Paquete U (+20)** |
|---|---|---|---|
| Fuerza | +2,4 | +5,4 | **+11,1** |
| Técnica | +3,4 | +0,8 | **+7,5** |
| Velocidad | **+0,4** | +2,1 | **+6,6** |
| Resistencia | +2,1 | +4,3 | **+3,0** |
| Correa | +2,3 | **−5,1** | **−1,6** |
| Los cinco a la vez | — | +7,3 | **+25,6** |

Tres respuestas a las preguntas del encargo:

1. **La velocidad ya no está muerta.** Era el atributo que el rediseño espacial tenía que resucitar
   (hallazgo 3 de `rediseno-espacial.md`): pasa de +0,4 por cada 10 puntos a **+6,6 por cada 20**, el
   tercero de los cinco. Lo que la resucita no es una fórmula nueva, son los cuerpos con volumen y la
   búsqueda de espacio: ahora llegar antes a un sitio significa ocuparlo.
2. **La correa volvió a una escala normal, pero por poco.** La ADR 0028 predecía que dejaría de ser el
   efecto más potente del juego (±8 puntos por casilla) al convertirse en zona con forma. Lo consiguió, y
   de más: con `scaleFromLeashPercent` en 60/150 la correa era un **malus** de −5,1 puntos, porque una zona
   grande deshace la estructura del equipo. Estrechando la escala a 85/115 queda en **−1,6**, es decir,
   prácticamente neutra. **Sigue siendo un atributo que no compra nada** y se lleva entre el 10% y el 18%
   del presupuesto de generación: es la deuda principal que este paquete deja abierta (ver §8).
3. **La técnica estuvo a punto de ser un malus.** Con `longPassTechniqueSlope` en 16, un equipo con +20 de
   técnica ganaba el **40,9%**: prefería el pase largo, lo perdía y dejaba de rematar (1,01 goles a favor
   frente a 1,4 de la referencia). Con la pendiente en 10 y el pago de la técnica movido a la **resolución**
   (`pass.techniqueFactor` 6 -> 24, `shot.techniqueFactor` 9 -> 14) sube a +7,5. Es el ejemplo más claro de
   la lección de la ADR 0030: mover un atributo a la **decisión** puede volverlo negativo si la decisión que
   induce es peor que la que sustituye.

---

## 4. Criterio de salida de la fase 1

Puerta `Sim.Tests/Analysis/BuildGateTests.cs`, **reactivada** (sin `Skip`). Muestra: 40 plantillas × 12
partidos = **480 partidos por celda**, catorce celdas, 6.720 partidos, semilla 1, **30 s**. Plantillas
emparejadas, local/visitante y reparto de ids alternados (metodología del paquete I).

### 4.1 Builds coherentes contra su referencia de raza (>= 58%)

| Build | Raza | Tasa | Margen |
|---|---|---|---|
| `dwarf_fortress` | Enanos | 83,12 | +25,1 |
| `human_counter` | Humanos | 82,92 | +24,9 |
| `orc_mob` | Orcos | 79,17 | +21,2 |
| `orc_giants` | Orcos | 78,75 | +20,8 |
| `orc_violence` | Orcos | 77,92 | +19,9 |
| `elf_tiki_taka` | Elfos | 75,62 | +17,6 |
| `human_wall` | Humanos | 74,38 | +16,4 |
| `undead_grind` | No-muertos | 73,96 | +16,0 |
| `elf_bulwark` | Elfos | 67,71 | +9,7 |

### 4.2 Builds mal construidas a propósito (<= 45%)

| Build | Qué está mal | Tasa | Margen |
|---|---|---|---|
| `human_scattered` | Alineación que rompe los siete vínculos | 9,17 | −35,8 |
| `orc_misplaced` | Perks técnicos y de zona en orcos, todos con la condición falsa | 39,17 | −5,8 |
| `elf_brawler` | Perks de violencia en elfos (ninguno lleva `Brute` ni `Bulwark`) | 41,88 | −3,1 |
| `elf_out_of_zone` | Perks de zona de inicio colocados donde su condición es falsa | 42,08 | −2,9 |

### 4.3 Resto de la puerta

| Métrica | Umbral | Valor |
|---|---|---|
| `randomBuildNearNone_human_random` | 40-60 | **55,62** | *(renombrada a `randomBuildLosesToNone` y con techo ≤ 45 por la **ADR 0078**; hoy mide 40,62)*
| `buildsWinDifferently_injuries` (normalizada, ADR 0012) | >= 1,5 | **3,05** |
| `buildsWinDifferently_passChain` (normalizada, ADR 0012) | >= 1,3 | **1,39** |
| `noDeadPerks` | 0 | **0** (mínimo `home_ref` 4,4%) |
| `rf069_filler` | 60 ± 8 | **64,44** |
| `rf069_conditional` | 30 ± 8 | **31,11** |
| `rf069_ruleBreaker` | 10 ± 8 | **4,44** |

El margen más ajustado de toda la puerta es `elf_bulwark` (+9,7 puntos) frente a un error típico de 2,3
puntos con 480 partidos: la puerta es estable con esta muestra.

### 4.4 Activación por perk (`noDeadPerks`, mejor build de cada uno)

39 perks asignables (los cinco raciales los concede la raza y no ocupan slot, así que ninguna build puede
listarlos). Ninguno por debajo del 1%.

| Tramo | Perks |
|---|---|
| 100% | `battle_reader`, `brute_boots`, `bulwark_stance`, `center_conductor`, `covering_shadow`, `deathless_march`, `fine_orchestra`, `fine_touch`, `flank_specialist`, `forward_line`, `gentle_giant`, `long_leash_legacy`, `natural_leader`, `own_third_anchor`, `pack_mentality`, `pivot_duo`, `spearpoint`, `unlikely_bulwark` |
| 80-99% | `shadow_marker` 99,8 · `box_predator` 98,3 · `last_ditch` 94,0 · `safety_net` 93,1 · `sweeper_keeper` 90,6 · `poacher_instinct` 89,8 · `clean_sheet_legacy` 89,0 · `diagonal_press` 83,8 · `high_press_trigger` 81,2 · `cold_focus` 80,8 |
| 50-79% | `wing_overlap` 69,8 · `game_management` 54,2 · `crowd_control` 53,5 · `comeback_spirit` 52,9 · `long_range_menace` 52,3 |
| 1-15% | `iron_gate` 13,8 (una lesión propia por partido) · `back_to_back` 13,1 (`Bulwark` cerca en humanos) · `mob_instigator` 7,7 (falta durante la turba) · `scar_tissue` 6,2 · `road_warrior` 5,8 · `home_ref` 4,4 |

Los seis de la última fila son **condicionales duros por diseño** y su tasa mide exactamente lo que dicen:
`mob_instigator` solo existe si hay turba (27,8% de los partidos llegan al gol de oro) y `home_ref` solo si
hay falta yendo por detrás en el marcador.

---

## 5. Campaña y progresión (`scalingRewardsGoodBuilds`)

8 partidos por campaña contra `human_none` de calidad creciente (46, 48, ..., 60), 60 campañas por build,
semilla 1, local y visitante alternados. Con el dial de calidad arreglado, el rival gana **14 puntos en
cada atributo** a lo largo de la campaña y la plantilla propia sube cuatro niveles (32 puntos de
presupuesto, +6,4 por atributo): la dificultad sube de verdad, unos 7,6 puntos netos por atributo.

| Build | Partidos 1-4 | Partidos 5-8 | Δ |
|---|---|---|---|
| *(sonda)* `human_accum` | 72,92 | 86,25 | **+13,33** |
| `dwarf_fortress` | 92,50 | 91,67 | −0,83 |
| `human_counter` | 77,92 | 76,67 | −1,25 |
| `undead_grind` | 76,25 | 74,58 | −1,67 |
| `orc_giants` | 87,50 | 85,42 | −2,08 |
| `orc_mob` | 87,08 | 84,58 | −2,50 |
| `elf_tiki_taka` | 86,67 | 83,75 | −2,92 |
| `human_wall` | 73,75 | 70,42 | −3,33 |
| `orc_violence` | 85,42 | 81,67 | −3,75 |
| `elf_bulwark` | 81,67 | 75,83 | −5,83 |
| *(malas)* `human_scattered` | 10,00 | 10,00 | 0,00 |
| *(malas)* `elf_out_of_zone` | 65,83 | 65,42 | −0,42 |
| *(malas)* `elf_brawler` | 52,08 | 51,25 | −0,83 |
| *(malas)* `orc_misplaced` | 43,75 | 41,25 | −2,50 |
| *(sin criterio)* `human_random` | 56,25 | 48,75 | −7,50 |
| *(control, sin ningún perk)* `human_none` | 60,42 | 46,67 | **−13,75** |

**Primera mitad de la métrica: cumplida con holgura.** Ninguna build coherente cae más de 5,83 puntos, muy
dentro de los 10 que pide §8.

**Segunda mitad ("las malas caen >= 15"): sigue sin ser alcanzable** (D-28), y la medición con el control
explica por qué mejor que ninguna otra: **quien más cae es el equipo sin ningún perk** (−13,75). Una build
mala cae **menos** que la referencia, porque hasta una build mal construida lleva algún perk que funciona.
La métrica, tal y como está escrita, pide que un error de construcción se pague **en términos absolutos**
cuando lo que el motor produce es exactamente lo contrario: los perks —buenos o malos— amortiguan la subida
de dificultad. La causa de fondo es que **un perk malo es un malus estático**: cuesta lo mismo en el partido
1 que en el 8, así que la distancia a la referencia no se abre con el tiempo. Propuestas en §8.3.

### 5.1 Los perks de acumulación ya producen progresión (hallazgo 2)

Sonda `human_accum` (`data/balance/builds/human_accum.json`: los siete titulares con `battle_reader` +
`silky_veteran`, nada más). No es una build de juego: es el instrumento con el que se mide el eje de
acumulación aislado.

| Build | p1 | p2 | p3 | p4 | p5 | p6 | p7 | p8 | Δ mitades |
|---|---|---|---|---|---|---|---|---|---|
| `human_accum` | 68 | 75 | 72 | 77 | 83 | 92 | 87 | 83 | **+13,3** |
| `human_wall` (coherente, sin acumulación) | 70 | 72 | 75 | 78 | 63 | 72 | 67 | 80 | −3,3 |
| `human_none` (sin perks) | 58 | 52 | 68 | 63 | 42 | 50 | 50 | 45 | −13,8 |

El hallazgo 2 de `rediseno-espacial.md` ("los perks de acumulación valen 0,2-0,4 puntos") queda
**resuelto**: una plantilla que solo acumula gana 27 puntos de tasa de victoria sobre el equipo sin perks a
lo largo de ocho partidos, y **sube** mientras el rival mejora 14 puntos por atributo. No hizo falta subir
su escala: bastó con **cambiarlos de canal**, que era la otra mitad de la recomendación. Los contadores
viven ahora sobre `intercept`, `save`, `shotOnTarget`, `dribble` y `tackle` —los canales con recorrido— y no
sobre atributos.

---

## 6. Rareza y jefe final (ADR 0027)

Puerta `Sim.Tests/Analysis/RarityAndBossTests.cs`, categoría `Gate`. 24 plantillas × 20 partidos = 480 por
comparación, semilla 1, plantillas emparejadas.

| Métrica | Condición | Valor |
|---|---|---|
| Común nivel 8 vs legendario nivel 2, sin perks en ninguno de los dos (RF-024) | 45-55% | **49,79%** |
| Común nivel 8 vs legendario nivel 8 (RF-024, "clara derrota") | < 40% | **38,75%** |
| Equipo **sin ningún legendario** con build coherente vs jefe final (salvaguarda ADR 0027) | razonable | **57,92%** |
| Comunes nivel 8 **sin perks** vs jefe final (RF-023b) | 30-70% | **38,75%** |

**Cómo está construido el jefe final.** No existe todavía como sistema de campaña, así que se monta como
un rival de `reference`-style: los diez jugadores generados con `rarity: legendary` y `level: 8` sobre la
misma calidad 50, **sin perks** — la lectura literal de "plantilla íntegramente legendaria" de RF-001c.
Todo lo que cambia respecto de un rival normal es el presupuesto de atributos que da la rareza (300 frente
a 250) y el nivel: 356 puntos de presupuesto frente a los 306 de un común de nivel máximo, y una banda de
atributos de [50, 86] frente a [40, 70]. El equipo del jugador es `human_wall` (una build coherente) con
**todos sus jugadores comunes y a nivel máximo**, que es el techo de una run en la que no ha tocado ni un
legendario.

**La salvaguarda se cumple con holgura**: 57,92%. La ADR 0027 no hay que revisarla. El aviso que sí hay que
dejar escrito es que **el 57,92% viene de los perks, no del nivel**: los mismos comunes de nivel 8 sin
perks ganan el 38,75%. Es exactamente el contrapeso 2 de la ADR ("los perks acumulativos y los vínculos son
el canal del común"), y significa que si la fase 2 recorta la generosidad del pool de perks, esta métrica
se cae con ella. Hay que volver a medirla cuando el jefe final tenga perks y objetos propios.

---

## 7. Palancas movidas

### 7.1 `data/sim/tuning.json`

| Clave | Antes | Después | Por qué |
|---|---|---|---|
| `states.TackleCooldownTicks` | 100 | 60 | Recuperar entradas |
| `pass.baseSuccess` | 9.200 | 7.700 | Dar recorrido al canal `pass` (D-26): con 9.200 y tope 9.800 un perk de +25 pp valía +6 |
| `pass.techniqueFactor` | 6 | 24 | La técnica paga en la resolución |
| `pass.distancePenaltyPerCell` | 130 | 105 | Compensa la bajada de base |
| `pass.pressurePenalty` | 600 | 480 | Igual |
| `pass.interceptTechniqueFactor` | 6 | 14 | Diferenciar por técnica en el canal con más recorrido |
| `dribble.baseWin` | 8.400 | 7.200 | Dar recorrido al canal `dribble` (D-26) |
| `dribble.attackerTechniqueFactor` | 10 | 18 | |
| `dribble.defenderSpeed/StrengthFactor` | 5 / 5 | 9 / 9 | |
| `shot.baseQuality` | 4.875 | 4.625 | Compensa la subida del factor de técnica (el término no está centrado en 50) |
| `shot.techniqueFactor` | 9 | 14 | |
| `save.attributeWeightPercent` | 8 | 20 | Un portero mejor para de verdad: ±8 puntos de porcentaje por ±20 de atributo |
| `tackle.strengthFactor` / `speedFactor` / `carrierTechniqueFactor` | 6 / 4 / 8 | 12 / 8 / 14 | Contraste por atributo sin mover la media |
| `tackle.foulBase` | 1.200 | 320 | Faltas y tarjetas (D6) |
| `tackle.foulStrengthFactor` | 12 | 5 | La fuerza **castigaba**: subía la falta el doble de lo que subía la entrada ganada |
| `tackle.yellowCardBase` / `redCardBase` | 2.000 / 300 | 250 / 10 | D6 |
| `tackle.hardTackleYellowBonus` / `RedBonus` | 1.500 / 200 | 300 / 20 | D6 |
| `injury.onTackleBase` / `onFoulBase` | 170 / 420 | 40 / 110 | Lesiones en rango con tres veces más entradas |
| `referee.biasFoulShiftPer10` | 400 | 120 | D6 |
| `referee.biasCardShiftPer10` / `biasPenaltyShiftPer10` | 300 / 250 | 100 / 100 | D6 |
| `referee.biasShift*` (siete claves) | 3/2/3/2/4/2/5 | 1/1/1/1/2/1/2 | D6: el criterio ya no satura |
| `block.foulBase` | 4.500 | 1.500 | D6 |
| `actionZone.scaleFromLeashPercent` | 60 / 150 | 85 / 115 | La correa era un malus de −5,1 puntos |

### 7.2 `data/ai/weights.json`

| Clave | Antes | Después |
|---|---|---|
| `base.*.Tackle` (GK/DEF/MID/FWD) | 200 / 210 / 150 / 90 | 165 / 255 / 210 / 128 |
| `base.*.Shoot` (DEF/MID/FWD) | 120 / 260 / 520 | 77 / 188 / 385 |
| `base.*.ShortPass` (GK/DEF/MID/FWD) | 560 / 420 / 460 / 340 | 600 / 500 / 560 / 420 |
| `base.*.Dribble` (DEF/MID/FWD) | 180 / 300 / 360 | 140 / 240 / 300 |
| `base.*.Block` (DEF/MID/FWD) | 55 / 60 / 80 | 35 / 40 / 55 |
| `context.shootInRangeBonus` | 500 | 388 |
| `context.shootDistancePenaltyPerCell` | 40 | 50 |
| `context.tackleBallCarrierBonus` | 200 | 195 |
| `context.pressCarrierBonus` | 120 | 60 |
| `context.blockTargetBonus` | 200 | 160 |
| `context.longPassTechniqueSlope` | 12 | 10 |

`shortPassTechniqueSlope` se probó en 8 para alargar la cadena de pases y se **revirtió** a 2: rompía la
ADR 0030 §1 (el pase largo dejaba de ser la acción del técnico). La cadena se alargó por la vía correcta,
bajando el número de tiros.

### 7.3 Perks y builds

- **24 perks** cambian de valor, y **ocho cambian de canal o de condición**: `fine_touch` (atributo técnica
  -> canal `pass`), `fine_orchestra` (regate propio -> `intercept` del rival + `tackleEvasion` propia),
  `brute_boots` (gana un `pass −10` que es su contrapartida), `shadow_marker` y `crowd_control` y
  `safety_net` y `road_warrior` (condiciones que no se cumplían nunca en el emparejamiento que las mide),
  `spearpoint` (deja de filtrar por etiqueta `Forward`, que con la alineación por defecto no alcanza).
- Los `elseEffects` pasan a **morder**: `own_third_anchor`, `bulwark_stance` y `spearpoint` castigan al
  **equipo entero**, no solo al portador. Es lo que hace que `human_scattered` se hunda al 9,17%.
- Las **15 builds** se rehacen con los siete titulares ocupados (antes usaban tres o cuatro).
- `/Balance` y la puerta ganan tres instrumentos de medida en `data/balance/*`: `level` y `rarity`
  uniformes, `styles` y `traits` por slot (una build que prueba `unlikely_bulwark` ya no depende de que el
  dado dé un elfo `Bulwark`, que ocurre el 12% de las veces y tumbaba el lote), y `attributeBonus` en
  `reference.json` para medir el valor marginal de un atributo.

---

## 8. Conclusiones de diseño que el revisor debe conocer

### 8.1 La escala de valores de perk no cabe en una sola tabla

`fase1b-diseno.md` §1.4 fija una escala única de puntos porcentuales (5/10/15/20/25/50) para
`modifyProbability`. Los canales sobre los que actúa **no viven en la misma escala**:

| Canal | Base | Qué hace un perk de +5 pp | Qué hace uno de +25 pp |
|---|---|---|---|
| `intercept` | 250 (2,5%) | **triplica** el canal | lo multiplica por 11 |
| `injure` | 40 en entrada limpia (0,4%) | lo multiplica por **13** | por 63 |
| `tackle` | 2.800 (28%) | +18% relativo | +89% relativo |
| `save` | 5.000 (50%) | +10% relativo | +50% relativo |
| `pass` | 7.700 (77%) | +6% relativo | +32%, y el tope de 9.800 se come parte |

El paso mínimo de la escala (5 pp) es **demasiado grande** para `intercept` e `injure` y demasiado pequeño
para `pass`. En este paquete se ha resuelto a mano —los perks de intercepción y de lesión están todos en el
escalón de 5 o de 10— pero es una trampa para quien escriba el catálogo de lanzamiento. Recomendación para
la fase 2: o la escala se expresa en **múltiplos de la base del canal** (×1,5, ×2, ×3) en vez de en puntos
absolutos, o se suben las bases de `intercept` y de `injure` para que los cinco canales vivan en el mismo
orden de magnitud. Cualquiera de las dos exige un ADR.

### 8.2 La correa sigue sin comprar nada

Es la deuda principal. Con la zona de acción de la ADR 0028, +20 de correa vale **−1,6 puntos**: una zona
más grande deshace la estructura del bloque y no compra ninguna ventaja a cambio. El presupuesto de
generación le dedica entre el 10% (portero) y el 18% (defensa, centro del campo) de sus puntos, así que
**cada punto de calidad que compra correa se tira**. Con `positionShare` como está, un jugador "mejor" es
un 14% menos mejor de lo que dice su presupuesto.

Las dos salidas son de diseño y ninguna se puede tomar aquí:

- **Sacar la correa del presupuesto** y volverla un descriptor posicional (lo que era antes de la ADR
  0028). Modifica la ADR 0028 y la 0025.
- **Darle un canal positivo**: que una zona mayor compre algo medible —llegar a balones sueltos fuera de la
  zona, cubrir más carril de pase— y no solo permiso para alejarse. Es mecánica nueva.

### 8.3 Por qué una build mala no decae (D-28) y qué la haría decaer

Los perks incoherentes son **maluses estáticos**: el −25 pp de pase de `fine_touch` en un orco vale lo
mismo en el partido 1 que en el 8. El control de §5 lo deja en evidencia: quien más cae al subir la
dificultad es **el equipo sin ningún perk** (−13,75), y las builds malas caen entre 2,5 y 0,0 puntos. Una
build mala **amortigua** la subida de dificultad mejor que no llevar nada, porque hasta ella lleva algún
perk que funciona. Ninguna mecánica del motor actual hace que un error de construcción **se pague más caro
con el tiempo**.

Lo que sí lo haría, en orden de coste:

1. **Reformular la métrica** (lo que ya proponía D-28): medir la **distancia a la referencia**, no la caída
   absoluta. Con los números de §5, `orc_misplaced` está **16,7 puntos por debajo** de `human_none` en la
   primera mitad de la campaña y 5,4 por debajo en la segunda, mientras que `human_wall` está 13,3 por
   encima y luego 23,8: el diseño **sí** se nota, y la distancia entre una build coherente y la referencia
   **crece** con la campaña. Lo que no ocurre es que la build mala se hunda en términos absolutos. El
   criterio pasaría a ser "la distancia de la build coherente a la referencia crece y la de la mala no".
   Cuesta un ADR y nada de código.
2. **Desgaste persistente** (fase 2, RF-090..094): si una lesión deja al jugador fuera N partidos, una build
   que provoca contacto sin poder ganarlo pierde plantilla y no la recupera. Es la mecánica que el
   documento ya tiene prevista y la que de verdad convierte un error en una espiral.
3. **Contadores negativos**: un `elseEffects` que acumule (un `addCounter` de "partidos jugado fuera de
   sitio") haría el malus creciente. Es simétrico a los perks de acumulación y el motor ya lo soporta; solo
   hay que escribir los perks.

### 8.4 RT-055 no se cumple, y la culpa es del punto de comparación

Medido al cierre con `--builds ... --vs human_none --rosters 20 --home-away --seed 1`:

| Sin perks (referencias de raza) | vs `human_none` | | Builds coherentes | vs `human_none` |
|---|---|---|---|---|
| `elf_none` | 68,50% | | `human_counter` | 86,25% |
| `orc_none` | 58,25% | | `elf_bulwark` | 84,50% |
| `dwarf_none` | 50,75% | | `orc_mob` | 84,00% |
| `human_none` | 50,00% | | `elf_tiki_taka` | 83,75% |
| `undead_none` | 48,50% | | `orc_giants` | 83,50% |
| | | | `dwarf_fortress` | 83,25% |
| | | | `orc_violence` | 81,50% |
| | | | `human_wall` | 76,25% |
| | | | `undead_grind` | 68,25% |
| | | | `human_random` | 49,75% |

Dos lecturas:

- **Entre razas hay 20 puntos** (elfos 68,5%, no-muertos 48,5%) y las cinco caben en la banda 30-70% de
  RT-055, pero los elfos están claramente por delante. No lo explica el sesgo de atributos: el de los orcos
  (+14 fuerza, −10 técnica) vale **+3,5 puntos** con las curvas de §3, así que los orcos deberían ir por
  delante de los humanos y solo van ocho puntos por encima. La diferencia la ponen los parámetros no
  numéricos —`bodyRadius` (elfo 30, humano 32, orco 38) y `discipline` (elfo 35, orco 45, humano 55)— que
  nunca se han calibrado. La puerta de fase 1 es **ciega** a esto porque compara cada build contra la
  referencia de su propia raza, que es lo correcto para medir el diseño de una build y lo equivocado para
  medir el de una raza.
- **Las nueve builds coherentes superan el 70% de RT-055** (68,25% a 86,25%), y eso **no** es una build
  rota: es el punto de comparación. `human_none` es una plantilla con **cero perks**, y una build con los
  siete titulares equipados lleva **catorce** (RF-023: dos slots por común). Comparar un equipo completo
  contra uno sin equipar no mide equilibrio, mide cuánto vale el catálogo entero. En la fase 1 las builds
  usaban tres o cuatro titulares y la comparación tenía sentido; desde este paquete no.

Ninguna de las dos se toca aquí. La primera es trabajo de `data/races/*.json`, que estaba fuera del
reajuste pedido y que obliga a repetir toda la calibración (D-29). La segunda es un cambio de criterio de
RT-055 y por tanto **exige un ADR** (RT-057): o la referencia pasa a ser una build de perks neutros en vez
de una plantilla desnuda, o la banda se ensancha. Anotado como D-34.

### 8.5 Lo que hace que una build gane, en orden

De las mediciones de §4 y de las tandas intermedias:

1. **Cegar la intercepción del rival o triplicar la propia** (`center_conductor`, `fine_orchestra`,
   `high_press_trigger`). Es el canal de mayor palanca del juego, con diferencia.
2. **El portero** (`safety_net`: +20 pp de parada sobre una base del 50%).
3. **El remate a puerta** (`box_predator`, `forward_line`: +25 pp cada uno sobre una base del 70%).
4. **La entrada** (`own_third_anchor`, `last_ditch`, `bulwark_stance`).
5. **Los atributos**, en último lugar: +20 de fuerza a **toda** la plantilla valen 11,1 puntos, así que
   +10 a **un** jugador valen unos 0,8. Un perk que sube un atributo es, en la práctica, un perk de relleno
   —que es justamente lo que `kind: filler` quiere decir—, pero conviene tenerlo medido antes de diseñar el
   catálogo de lanzamiento: `brute_boots` (+10 de fuerza) vale la décima parte que `center_conductor`
   (+5 pp de intercepción a todo el equipo).

---

## 9. Cómo reproducir

```bash
# RT-056 (tabla de §2) — código de salida 0 si todo está en rango
dotnet run --project Balance -c Release -- --runs 2000 --seed 1

# Puertas (fase 0, fase 1 y rareza/jefe final)
dotnet test -c Release -v q

# Matriz de builds contra una referencia concreta
dotnet run --project Balance -c Release -- --builds elf_tiki_taka --vs elf_none \
    --rosters 20 --runs 800 --home-away --seed 1 --out out/tt

# Campaña de 8 partidos (tabla de §5); --runs es el número de CAMPAÑAS por build
dotnet run --project Balance -c Release -- --builds human_accum,human_none,human_wall \
    --campaign 8 --runs 60 --home-away --seed 1 --out out/camp

# Valor marginal de un atributo (tabla de §3): reference.json con attributeBonus
#   { "id": "hi", "race": "Human", "quality": 50, "attributeBonus": { "speed": 20 } }
dotnet run --project Balance -c Release -- --runs 3200 --seed 1 --teams <fichero> --out out/margin
```
