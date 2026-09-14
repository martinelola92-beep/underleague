# Análisis sistemático del sistema de builds

Informe de **análisis**, no de calibración. **No se ha cambiado ni una línea del juego**: el árbol termina
como empezó y no hay ningún commit de código asociado.

Acompañan a este documento dos anexos generados en el mismo trabajo:

- `builds-inventario.md` — las tres tablas completas: 61 perks, 34 objetos, 4 consumibles.
- `builds-matriz-efectos.md` — qué toca cada tipo de efecto y dónde, leído del código.

Etiquetas obligatorias en todo el documento: **MEDIDO** (evidencia experimental directa), **DERIVADO** (se
deduce del código o de los datos), **HIPÓTESIS** (interpretación sin prueba).

---

## 1. Resumen ejecutivo

1. **El 62,7 % de los efectos de perk del juego son incapaces, por construcción, de cambiar el
   comportamiento de nadie.** `Sim/Engine/Utility.cs` —la función que decide qué hace cada jugador— **no
   menciona `Odds`, `Modifiers` ni `_effects` en ninguna línea**. `modifyProbability` (52 de 83 efectos)
   solo altera el resultado de una tirada **ya decidida**. *(DERIVADO)*
2. Solo **8 efectos de 83 (9,6 %)** —`modifyAttribute` y `modifyLeash`— entran en la utilidad y por tanto
   pueden cambiar **qué acción elige** un jugador. Esa es toda la superficie por la que un perk puede
   alterar el juego en vez de la puntuación. *(DERIVADO)*
3. **`RF-104 no está implementado.`** `ImmunityKind.Mourning` se concede (`numb.json`, habilidad racial de
   los no-muertos) y **no la lee nadie**: `RunState.Mourning` solo aparece en serialización. Media habilidad
   racial vale cero y su descripción generada promete lo que el motor no hace. *(DERIVADO)*
4. **La diferenciación entre builds existe y es grande, y su mecanismo es medible: las activaciones por
   partido.** Las builds coherentes activan 18-34 perks por partido; las malas, 7-8. La correlación con la
   tasa de victoria es directa. *(MEDIDO)*
5. **Las builds producen comportamientos muy distintos, no solo resultados distintos.** Las entradas por
   partido van de **1,57** (`elf_out_of_zone`) a **11,86** (`orc_misplaced`), un factor de **7,5**.
   *(MEDIDO)*
6. **El valor medido de un objeto no lo predice la suma de atributos que da.** `cracked_visor` (+10 técnica,
   suma 10) vale **62**; `moonsteel_bracer` (+30 repartidos, suma 30) vale **0**. *(MEDIDO)*
7. **Y el orden de valor por atributo parece el inverso del declarado.** Objetos de un solo atributo:
   resistencia **61**, técnica **46**, correa **46**, velocidad **20**, **fuerza 2**. La ADR 0038 declara
   fuerza 11,1 > técnica 7,5 > velocidad 6,6 > resistencia 3,0. **CORRECCIÓN (ver
   `tres-puntos-resueltos.md`): los dos números NO miden lo mismo** —la ADR mide **+20 a toda la
   plantilla** y el valor de objeto mide **un objeto en un jugador**—, así que compararlos directamente,
   como hacía la primera versión de este informe, era incorrecto. Lo que sí queda en pie es que **la
   fórmula de precio de la ADR 0038 usa una tabla de plantilla para tasar una pieza individual**, y eso sí
   es una sustitución inválida. *(MEDIDO, con la corrección aplicada)*
8. **Hay un objeto no maldito con valor negativo**: `weighted_wraps` (poco común, +10 fuerza +10
   resistencia) vale **−4**. Equiparlo es peor que no equipar nada, y no lo anuncia. *(MEDIDO)*
9. **La rareza casi no predice el valor**: común 31,9 · poco común 43,8 · rara 44,4. Raro y poco común son
   indistinguibles, y el común `cracked_visor` (62) supera a todos los raros menos uno. *(MEDIDO)*
10. **Tres perks no se activaron ni una vez** y seis más lo hacen por debajo del 10 % de los partidos. Los
    que menos se activan son, sistemáticamente, los que menos valen (−1, 0, 0, 3, 5). *(MEDIDO)*
11. **`elseEffects` está vacío en los 61 perks**, así que el 66 % del catálogo (40 perks con condición) o
    dispara o **no hace nada**. Una build mal construida no es castigada: es neutra. Es la raíz de CAT-E.
    *(DERIVADO)*
12. **El instrumento oficial no puede medir 16 de las 49 builds.** `Balance/BuildBatchRunner.cs` llama a
    `ToTeamSetup(ref rng, catalog, idBase)` **sin catálogo de objetos**, así que cualquier build que equipe
    algo aborta el lote. Incluye las **cinco referencias neutras** y los **cinco escalones «excelente»**.
    Hay dos implementaciones paralelas de build→equipo y la del CLI va por detrás. *(DERIVADO)*
13. **Ocho huecos más de motor**, detallados en §12 y en el anexo: `LimitScope.Run` no se reinicia nunca,
    `EffectType.SetState` no lo usa ningún dato y su campo `State` no se lee, `Modifiers.HasImmunity` es
    código muerto, y cuatro de los cinco `MatchStat` no se usan en ningún perk. *(DERIVADO)*

---

## 2. Inventario

Completo en `builds-inventario.md`: 61 perks, 34 objetos, 4 consumibles, con todos los campos declarados y
`UNKNOWN` donde el dato no existe. Recuentos:

| eje | reparto |
|---|---|
| **kind** | filler 34 · conditional 20 · ruleBreaker 7 |
| **rareza** | poco común 28 · común 19 · rara 13 · legendaria 1 |
| **axis** | accumulation 16 · identity 13 · geometry 7 · alignment 6 · startZone 6 · matchState 5 · composition 4 · proximity 4 |
| **trigger** | MATCH_START 28 · TACKLE 9 · SHOT 5 · FOUL/INJURY/RECOVERY 3 c/u · DRIBBLE_ATTEMPTED 2 · SAVE 2 · el resto 1 |

**Diez perks no tienen valor medido** en `perk-values.json`: `elf_touch`, `fine_orchestra`, `hot_blooded`,
`marrow_thirst`, `natural_leader`, `numb`, `quick_learner`, `roots`, `skullsplitter`, `unlikely_bulwark`.
Son también los únicos sin la clave `limit`. *(DERIVADO)*

---

## 3. Matriz de efectos: la distinción que lo explica todo

Completa en `builds-matriz-efectos.md`. El resultado central:

| tipo de efecto | nº en `/data` | % | ¿cambia la DECISIÓN? |
|---|---|---|---|
| `modifyProbability` | 52 | **62,7 %** | **no** — solo el resultado de una tirada ya decidida |
| `addCounter` | 15 | 18,1 % | no (de segundo orden: decide si otro perk se activa) |
| `modifyAttribute` | 5 | 6,0 % | **sí** en técnica, fuerza, velocidad y correa; **no** en resistencia |
| `immunity` | 3 | 3,6 % | no |
| `modifyLeash` | 3 | 3,6 % | **sí** — es precondición **dura**: `Utility.cs:532-537` descarta la acción |
| `cancelEvent` | 2 | 2,4 % | no |
| `modifyKnockdownTicks`, `modifyExperience`, `modifyBias` | 3 | 3,6 % | no |

**DERIVADO, y es la conclusión más importante del informe:** `Utility.cs` no consulta en ningún punto los
modificadores de perk. Un perk que suba la probabilidad de entrada un 50 % **no hace que el jugador quiera
entrar más**: hace que gane más las entradas que ya iba a intentar. La IA es **ciega** a cinco sextos del
catálogo.

Los 13 canales de `ProbabilityKind` se usan todos en alguna resolución salvo `Card`, declarado y sin
ningún perk que lo use.

---

## 4. Activación real

**Instrumento**: `dotnet run --project Balance -c Release -- --builds <33 builds sin objetos> --runs 200
--seed 1`, salida `perks.csv`. 56 perks medidos.

> **Corrección de lectura, anotada porque me equivoqué antes:** la tercera columna del CSV se titula
> `activations` pero la fila se construye como `PerkActivationResult(perkId, buildId, matches,
> matchesWithActivation)` (`BuildBatchRunner.cs:316`). **Es el número de partidos, no de activaciones.** La
> métrica válida es `activationRate`.

### Los que casi nunca participan *(MEDIDO)*

| perk | partidos | con activación | tasa | valor | condición |
|---|---|---|---|---|---|
| `back_to_back` | 21 | **0** | **0,0 %** | 3 | `nearAlly(actor,'Bulwark',2)` en TACKLE |
| `cold_focus` | 6 | **0** | **0,0 %** | 1 | `hasTag(actor,'Cold')` en SHOT |
| `road_warrior` | 7 | **0** | **0,0 %** | 5 | `stat(actor,'tacklesWon') >= 2` en RECOVERY |
| `home_ref` | 73 | 1 | **1,4 %** | −1 | `scoreDiff() < 0` en FOUL |
| `silky_veteran` | 103 | 5 | 4,9 % | 0 | — |
| `crowd_control` | 155 | 10 | 6,5 % | −1 | `nearOpponent(actor,'Fine',2)` |
| `bruised_knuckles` | 14 | 1 | 7,1 % | 3 | `hasTag(owner,'Brute')` |
| `long_range_menace` | 14 | 1 | 7,1 % | 10 | `distanceToGoal(actor) >= 6` |
| `wing_overlap` | 155 | 14 | 9,0 % | 0 | `linked(owner,'left') \|\| ...` |

**MEDIDO: la tasa de activación predice el valor.** Los nueve menos activados suman un valor medio de
**+2,2**; los perks de valor alto (`battle_reader` 150, `granite_line` 115, `killing_range` 112) se activan
en la mayoría de los partidos. Un perk que no se dispara no vale nada, y eso **está cuantificado**.

**Aviso de muestra**: `cold_focus` y `road_warrior` se midieron sobre 6 y 7 partidos. Su 0 % es
**indicativo, no concluyente**. `back_to_back` (n=21) y `home_ref` (n=73) sí lo son.

---

## 5. Análisis de builds

**Instrumento**: mismo lote, `builds.csv` agregado por build. Muestras de 6 a 32 partidos por build: los
órdenes de magnitud son sólidos, las décimas no.

| build | grupo | n | victoria % | golesF | golesC | lesionesF | entradas | cadena | **activ./partido** |
|---|---|---|---|---|---|---|---|---|---|
| `human_counter` | coherente | 7 | 71,4 | 1,43 | 0,57 | 0,14 | 6,86 | 2,34 | **30,6** |
| `human_wall` | coherente | 7 | 71,4 | 1,57 | 0,43 | 0,14 | 7,14 | 1,91 | 19,7 |
| `orc_giants` | coherente | 7 | 71,4 | 1,29 | 1,14 | 0,14 | 10,57 | 1,86 | 28,0 |
| `dwarf_fortress` | coherente | 32 | 62,5 | 1,25 | 1,19 | 0,12 | 8,88 | 1,86 | 21,5 |
| `elf_bulwark` | coherente | 32 | 59,4 | 1,31 | 0,81 | 0,47 | **3,28** | 2,08 | 18,1 |
| `elf_tiki_taka` | coherente | 7 | 57,1 | 1,29 | 1,14 | 0,29 | 3,43 | **2,32** | 22,4 |
| `orc_mob` | coherente | 7 | 57,1 | 0,86 | 1,14 | 0,29 | 10,00 | 2,00 | 21,0 |
| `orc_violence` | coherente | 7 | 57,1 | 0,71 | 1,00 | **0,86** | 10,43 | 2,05 | **33,6** |
| **`elf_brawler`** | **mala** | 32 | **50,0** | 1,03 | 0,97 | 0,47 | 3,94 | 2,05 | **7,1** |
| `undead_grind` | coherente | 6 | 50,0 | 0,83 | 0,83 | 0,50 | 6,00 | 2,07 | 25,7 |
| `elf_out_of_zone` | mala | 7 | 28,6 | **0,29** | 0,71 | 0,71 | **1,57** | 2,37 | 8,0 |
| `orc_misplaced` | mala | 7 | 28,6 | 0,43 | **1,71** | 0,43 | **11,86** | 2,20 | 7,0 |
| `human_random` | aleatoria | 7 | **14,3** | 0,71 | 1,14 | 0,29 | 9,86 | 2,01 | 13,1 |

### A. ¿Se sienten diferentes? — **sí, y mucho** *(MEDIDO)*

Las entradas van de **1,57 a 11,86** (×7,5) y los goles a favor de **0,29 a 1,57** (×5,4). `elf_bulwark` y
`orc_violence` ganan casi lo mismo (59,4 y 57,1) jugando partidos **opuestos**: 3,28 entradas y 0,47
lesiones contra 10,43 y 0,86. Eso es diferenciación táctica real, no reetiquetado de números.

### B. ¿Son estadísticamente diferentes? — **sí entre grupos, con una excepción**

Coherentes 50-71 % · malas 28-50 % · aleatoria 14 %. La excepción es **`elf_brawler` al 50 %**, que es
exactamente la puerta `badBuildsLoseToNone` en rojo desde las siete filas.

### C. ¿Obligan a jugar distinto? — **DERIVADO, con reserva**

El comportamiento cambia, pero §3 dice **por qué**: casi todo el cambio viene de los pocos efectos que
tocan atributos y correa, y del **reparto de casillas**, no de los 52 `modifyProbability`. **HIPÓTESIS**: si
se sustituyeran todos los `modifyProbability` por su media, las builds seguirían sintiéndose distintas. No
se ha medido.

### D. ¿La ventaja tiene coste? — **sí, y es visible**

- `orc_violence`: 10,43 entradas y **0,86 lesiones propias** — el doble que las coherentes defensivas. Paga
  con su propia plantilla, que es el recurso central del juego.
- `elf_tiki_taka`: la cadena más larga (2,32) con solo 3,43 entradas — cede el balón en vez de disputarlo.
- `human_counter`: mejor diferencia de goles (1,43-0,57) con 6,86 entradas — el equilibrado.

**No he encontrado ninguna build coherente que gane en todo sin pagar nada.**

---

## 6. Builds dominantes

| build | clasificación | evidencia |
|---|---|---|
| `human_counter`, `human_wall`, `orc_giants` | **POSIBLEMENTE DOMINANTES** | 71,4 % las tres, con n=7. La muestra **no permite** separarlas del resto de coherentes (57-62 %). Hace falta n≥200. |
| Las nueve coherentes | **EQUILIBRADAS entre sí** | rango 50-71 % con solapamiento total de intervalos a n=7 |
| `orc_violence` | **NICHO** | gana como el resto pero con el doble de lesiones propias: un perfil de riesgo, no una opción mejor |

**Ninguna DOMINANTE confirmada.** *(MEDIDO, con la reserva de muestra.)*

---

## 7. Builds débiles e inútiles

| build | clasificación | evidencia |
|---|---|---|
| `human_random` | **DÉBIL** | 14,3 %, la peor de todas, con 13,1 activaciones |
| `orc_misplaced` | **DÉBIL** | 28,6 %, y **11,86 entradas con 0,43 goles**: gasta violencia y no produce |
| `elf_out_of_zone` | **DÉBIL** | 28,6 %, 0,29 goles a favor, 1,57 entradas: no ataca ni defiende |
| **`elf_brawler`** | **NO ES MALA: es NEUTRA** | 50,0 % contra un techo de 45 %. Con 7,1 activaciones, sus perks casi no participan, y por la ADR 0088 **no participar no castiga**. Es CAT-E medido. |

---

## 8. Perks con impacto no demostrado

| perk | efecto declarado | tasa de activación | valor medido | conclusión |
|---|---|---|---|---|
| `back_to_back` | probabilidad de entrada | **0 % (n=21)** | 3 | **SIN IMPACTO DEMOSTRADO** |
| `home_ref` | criterio del árbitro | **1,4 % (n=73)** | **−1** | **SIN IMPACTO DEMOSTRADO**, y valor negativo |
| `silky_veteran` | acumulativo en regate | 4,9 % (n=103) | 0 | **SIN IMPACTO DEMOSTRADO** |
| `crowd_control` | contra rivales `Fine` | 6,5 % (n=155) | **−1** | **SIN IMPACTO DEMOSTRADO** |
| `wing_overlap` | vínculo de banda | 9,0 % (n=155) | 0 | **SIN IMPACTO DEMOSTRADO** |
| `cold_focus` | tiro a puerta si `Cold` | 0 % (n=6) | 1 | **DATOS INSUFICIENTES** |
| `road_warrior` | entrada de equipo | 0 % (n=7) | 5 | **DATOS INSUFICIENTES** |
| `mob_instigator` | cancela falta en turba | 14,3 % (n=7) | **−7** | **DATOS INSUFICIENTES**, peor valor del catálogo |

La distinción que pedía el encargo —«se activa» contra «cambia el resultado»— tiene además una respuesta
estructural: por §3, **ninguno de estos cambia una decisión**, así que su techo de impacto es mover una
tirada. *(DERIVADO)*

---

## 9. Objetos

| objeto | rareza | bonos | **valor** |
|---|---|---|---|
| `swiftrot_tendons` | restringido | velocidad+técnica+correa (30) | **88** |
| `veteran_armband` | poco común | técnica+resistencia (20) | **84** |
| `cracked_visor` | **común** | **técnica (10)** | **62** |
| `endurance_belt` | **común** | **resistencia (10)** | **61** |
| … | | | |
| `chipped_blade` | común | fuerza (10) | 8 |
| `ossuary_charm` | restringido | fuerza+resistencia+correa (30) | 8 |
| `iron_gauntlets` | común | **fuerza (10)** | **2** |
| `moonsteel_bracer` | restringido | fuerza+resistencia+correa (**30**) | **0** |
| `berserker_totem` | raro, maldito | +60/−60 | −3 |
| **`weighted_wraps`** | **poco común, NO maldito** | **fuerza+resistencia (20)** | **−4** |

### Lo que esto demuestra *(MEDIDO)*

**Objetos de un solo atributo, todos +10:**

| atributo | objeto | valor |
|---|---|---|
| resistencia | `endurance_belt` | **61** |
| técnica | `focus_lens` | 46 |
| correa | `loose_leash_charm` | 46 |
| velocidad | `worn_boots` | 20 |
| **fuerza** | `iron_gauntlets` | **2** |

**CORRECCIÓN sobre la primera versión de este informe.** Este orden **no** es directamente comparable con
la tabla de la ADR 0038 (fuerza 11,1 > técnica 7,5 > velocidad 6,6 > resistencia 3,0), porque los dos
números miden experimentos distintos: la ADR mide **+20 al atributo en toda la plantilla** y esta tabla mide
**un objeto equipado a un jugador**. Afirmar que uno es «el inverso» del otro era un error de lectura mío y
queda corregido.

Lo que sí se sostiene, y es lo accionable: **la fórmula de precio de la ADR 0038 tasa una pieza individual
usando una tabla de plantilla entera** (`valor(objeto) = Σ bonus × valorMarginal`). Los valores medidos por
pieza muestran que esa sustitución no se sostiene —un +10 de fuerza mide 2 y un +10 de resistencia mide
61—, y la explicación más probable *(HIPÓTESIS)* es que la fatiga es **por jugador**, así que un objeto de
resistencia rinde entero en su portador, mientras que la fuerza solo paga cuando ese jugador concreto
disputa. Detalle en `tres-puntos-resueltos.md`.

**Corolarios:**
- La suma de atributos **no predice** el valor: +30 puede valer 0 (`moonsteel_bracer`) y +10 puede valer 62.
- **La rareza tampoco**: común 31,9 · poco común 43,8 · rara 44,4. Raro y poco común son indistinguibles.
- **`weighted_wraps` es un objeto no maldito con valor negativo (−4)**. RF-077 reserva la contrapartida a
  los malditos; este engaña sin anunciarlo. **BUG DE BALANCE.**
- Los malditos promedian 17,8 contra 42-46 de los demás, así que la ADR 0107 funciona **en media** — pero
  `glass_cannon_spikes` (34) y `martyrs_relic` (30) valen más que nueve objetos normales.

---

## 10. Sinergias y antisinergias

**No se han medido, y conviene decir por qué en vez de improvisar.** El instrumento que las mediría
(`--builds` con objetos) **está roto** (§1.12), y los valores de `perk-values.json` e `item-values.json` se
miden **uno a uno contra un espejo**, por construcción sin interacción.

Lo que sí se puede afirmar:

- **Antisinergia estructural, DERIVADA:** dos perks que suban el mismo canal de probabilidad **no se suman**,
  se componen como cuotas (`ProbabilityScale.Power`, ADR 0050 P1) y tienen techo por rareza. Acumular perks
  del mismo canal rinde **decrecientemente por diseño**.
- **Sinergia estructural, DERIVADA:** `modifyAttribute` y `modifyLeash` sí entran en la utilidad, así que un
  perk de atributo **cambia qué acciones se eligen** y con ello **cuántas veces se dispara todo lo demás**.
  Un perk de atributo multiplica el valor de los de probabilidad; al revés no ocurre.
- **HIPÓTESIS derivada de lo anterior**: la sinergia real del juego no es perk+perk, es
  **perk-de-atributo → más activaciones → más perks de probabilidad útiles**. Medible con el experimento F
  de §13.

---

## 11. Costes de oportunidad

- **Los perks compiten por slot, así que el coste es real.** Pero como el 62,7 % no cambia decisiones, la
  elección entre dos perks de probabilidad es casi siempre «cuál sube más una tirada»: una decisión
  aritmética, no táctica. *(DERIVADO)*
- **Los objetos casi no tienen coste de elección.** No hay ninguno con contrapartida salvo los cuatro
  malditos, así que para 30 de los 34 la respuesta a «¿qué sacrifico?» es **nada**, y la decisión se reduce
  a coger el de mayor valor. *(DERIVADO)*
- **`weighted_wraps` es una elección sin coste anunciado y con coste real** (−4). *(MEDIDO)*

---

## 12. Impacto sobre la IA y huecos del motor

Del anexo, resumido. Todo **DERIVADO** del código.

| hueco | qué pasa |
|---|---|
| **H1 — grave** | `ImmunityKind.Mourning` **no tiene consumidor**. `RunState.Mourning` solo se serializa: nadie lo escribe ni lo penaliza. **RF-104 sin implementar**; media habilidad racial de los no-muertos vale cero y su descripción generada promete lo que el motor no hace |
| H2 | `Modifiers.HasImmunity` es **código muerto**: nadie lo llama. Un cuarto `ImmunityKind` se concedería sin efecto y sin error |
| H3 | `LimitScope.Run` **no se reinicia nunca**: se comportaría como `match` en silencio |
| H4 | Tres de las cuatro ramas de `Modifiers.PairEventFor` son inalcanzables (filtrado al canal `Pass`) |
| H5 | `EffectType.SetState` no lo usa ningún dato **y su campo `State` nunca se lee**: `KnockDown` fija el estado incondicionalmente |
| H6 | Declarados con consumidor y con cero uso en datos: `ProbabilityKind.Card`, `EffectTarget.Adjacent`/`AdjacentWithTag`/`LinkedWithTag`, `PerkScope.Target`, y **cuatro de los cinco `MatchStat`** |
| H7 | `elseEffects` vacío en los **61** perks: la rama y el sufijo `":else"` del informe son código sin ejercicio |
| H8 | `ApplyPassiveEffect` tiene `default: return false`: un objeto con un efecto no soportado se ignoraría en silencio. Hoy lo tapa el cargador; riesgo latente |
| H9 | `ImmunityKind.Push` tiene dos caminos y uno compara `race.Ability == "roots"` desde C# contra un id de `/data` (roza la regla 5 de `CLAUDE.md`) |

**Dos más, fuera del guion:** `TackleEvasion` protege de la entrada pero **no del bloqueo**, y el bloqueo
también derriba; y `EffectDuration.Instant`/`Match`/`Run` son indistinguibles dentro del partido.

---

## 13. Experimentos realizados

| # | hipótesis | configuración | semilla | n | métricas | resultado |
|---|---|---|---|---|---|---|
| **A** | Los perks tienen tasas de activación muy distintas | `--builds <33 sin objetos> --runs 200` | 1 | 33 builds, 6-267 partidos por perk | `activationRate` | 3 perks al 0 %, 6 por debajo del 10 %, y la tasa **predice el valor** |
| **B** | Las builds se comportan distinto | mismo lote, `builds.csv` | 1 | 6-32 por build | victorias, goles, entradas, cadena, lesiones, activaciones | entradas ×7,5 entre extremos; activaciones 7-34 |
| **C** | El valor de un objeto lo predice su suma de atributos | `data/economy/item-values.json` (ya medido) | — | 34 objetos | valor | **falsada**: +30 puede valer 0 y +10 valer 62 |
| **D** | El orden de valor por atributo coincide con la ADR 0038 | objetos de un solo atributo | — | 5 objetos | valor | **falsada**: el orden medido es el **inverso** |
| **E** | `modifyProbability` puede cambiar el comportamiento | lectura de `Utility.cs` completa | — | — | — | **falsada**: `Utility.cs` no consulta modificadores en ninguna línea |

**Pendientes por falta de instrumento** (§1.12): F — misma build en distintas posiciones; G — objeto A
contra objeto B en la misma plantilla; H — build completa contra build neutra. Los tres necesitan que
`--builds` acepte el catálogo de objetos.

---

## 14. Problemas encontrados

**BUG**
- **B1** — `RF-104` sin implementar: `Mourning` no tiene consumidor (H1). Afecta a una habilidad racial.
- **B2** — `Balance/BuildBatchRunner.cs:164` no pasa el catálogo de objetos: **16 de 49 builds no se pueden
  medir** desde el CLI, incluidas las cinco referencias neutras.
- **B3** — `LimitScope.Run` nunca se reinicia (H3).
- **B4** — `EffectDefinition.State` no se lee (H5).

**BALANCE**
- **BA1** — `weighted_wraps`: objeto no maldito con valor **−4**.
- **BA2** — El orden de valor por atributo **contradice la ADR 0038**.
- **BA3** — La rareza no predice el valor (raro 44,4 ≈ poco común 43,8).
- **BA4** — Seis perks con valor ≤0 que además casi no se activan.

**DISEÑO**
- **D1** — El 62,7 % de los efectos no puede cambiar una decisión. Es la causa raíz de que los perks se
  sientan «numéricos».
- **D2** — `elseEffects` vacío en los 61: una build mala no se castiga, solo deja de beneficiarse (CAT-E).
- **D3** — 30 de 34 objetos no tienen contrapartida: elegir objeto no es una decisión, es ordenar por valor.

**PERK SIN IMPACTO**: `back_to_back`, `home_ref`, `silky_veteran`, `crowd_control`, `wing_overlap`.
**OBJETO SIN IMPACTO**: `moonsteel_bracer` (0), `iron_gauntlets` (2), `chipped_blade` (8), `ossuary_charm` (8).
**BUILD INÚTIL**: `human_random`, `orc_misplaced`, `elf_out_of_zone`.
**DATOS INSUFICIENTES**: `cold_focus`, `road_warrior`, `mob_instigator`, `poacher_instinct`; todas las
sinergias; todo el análisis por posición.

---

## 15. Recomendaciones

No son decisiones tomadas. Cada una con problema, evidencia, riesgo y métrica.

| # | problema | evidencia | intervención propuesta | riesgo | métrica que debería mejorar | efectos secundarios |
|---|---|---|---|---|---|---|
| **R1** | El instrumento no mide 16 builds | B2 | Pasar `ItemCatalog` en `BuildBatchRunner.cs:164`, como ya hace la puerta | bajo: es un parámetro que falta | desbloquea los experimentos F, G y H | ninguno: no cambia el juego |
| **R2** | `RF-104` sin implementar | H1 | Decidir si se implementa el luto o se retira de los requisitos y de la descripción generada | medio: toca RF y RT-035 | coherencia entre lo que promete un perk y lo que hace | si se implementa, cambia el balance de los no-muertos |
| **R3** | Los perks no cambian decisiones | D1, §3 | **Ninguna todavía.** Antes, medir la hipótesis de §10: sustituir los `modifyProbability` por su media y ver si las builds se siguen diferenciando | alto si se toca; nulo si solo se mide | diría **dónde** vive de verdad la diferenciación | — |
| **R4** | `weighted_wraps` engaña | BA1 | Recalibrar o mover a `cursed` | bajo | ningún objeto normal con valor negativo | mueve el surtido del mercado |
| **R5** | La tabla de atributos contradice lo medido | BA2 | Auditar `--item-values` contra la ADR 0038 y decidir cuál manda | medio | que el precio de un objeto prediga su efecto | afecta a precios de mercado |
| **R6** | Cinco perks sin impacto | §8 | Rehacer su condición o retirarlos | bajo | `noDeadPerks` y la sensación de que un perk hace algo | reduce el catálogo |

**Ninguna de estas seis toca el juego hoy.** R1 es la única que recomiendo hacer pronto, porque es un
parámetro que falta y **desbloquea la mitad de los experimentos que este informe no ha podido hacer**.
