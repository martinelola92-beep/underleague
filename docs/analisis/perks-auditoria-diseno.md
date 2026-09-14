# Auditoría de diseño del sistema de perks

**Pregunta del encargo:** ¿los perks hacen que el jugador tome decisiones interesantes y que el personaje se
comporte de forma reconocible? Y si no, ¿cómo debería rediseñarse el sistema?

**Regla rectora aplicada:** un perk no es bueno porque tenga lógica interesante; es bueno si hace que quieras
construir un personaje de cierta manera y luego **lo reconozcas jugando**.

Esto es una auditoría de **diseño de juego**, no de balance. **No se ha modificado ni un fichero del
repositorio**: ni código, ni JSON, ni pesos, ni probabilidades. El único fichero escrito es este.

## Etiquetas

- **MEDIDO** (MEASURED) — leído directamente del código, de los datos o de un experimento ya ejecutado.
- **DERIVADO** (DERIVED) — conclusión lógica encadenada sobre algo medido.
- **HIPÓTESIS** (HYPOTHESIS) — interpretación de diseño sin validar. Se puede estar en desacuerdo con ella
  sin estar en desacuerdo con ningún hecho.

Se apoya en tres informes previos y **no repite** sus mediciones: `builds-analisis-sistemico.md`,
`builds-inventario.md` y `builds-matriz-efectos.md`. Hechos heredados que se dan por buenos:
el 62,7 % de los efectos son `modifyProbability` y no pueden cambiar una decisión; solo 8 efectos de 83
entran en la utilidad; `elseEffects` está vacío en los 61 perks; tres perks activan al 0 % y seis más por
debajo del 10 %; las builds sí se diferencian (entradas de 1,57 a 11,86 entre extremos).

---

## 1. Teoría de diseño mínima: qué debería aportar un perk

Un juego de construcción de personaje tiene capas, y cada capa **compra una decisión distinta**. El listado
de capas de Underleague, tal como existe hoy en el código:

| capa | qué aporta | quién la elige | cadencia | ¿cambia comportamiento? |
|---|---|---|---|---|
| Atributos | capacidad bruta (fuerza, velocidad, técnica, resistencia, correa) | nadie: se generan | al crear al jugador | sí, por pendiente (`Utility.cs` lee 4 de 5) |
| Etiqueta de estilo | sesgo de atributos (`data/tags/styles.json`) | nadie: sorteo con peso racial | al crear al jugador | no directamente |
| **Rasgo** | **multiplicadores de acción** (`Tackle 160`, `Shoot 150`, `Retreat 50`…) | indirectamente: al fichar a ese jugador | al crear al jugador | **sí, y es la capa que más lo hace** |
| Raza | identidad + una habilidad automática | al elegir club | una vez por run | poco |
| Posición / rol | tabla de pesos base por acción (`data/ai/weights.json`) | sí, al alinear | antes de cada partido | sí, es el esqueleto |
| Formación / casilla | geometría: dónde vive la zona de acción | sí, al alinear | antes de cada partido | sí (correa + hogar) |
| Objeto | atributos y nada más (ADR 0036) | sí, transferible | antes de cada partido | solo por pendiente de atributo |
| Consumible | un efecto puntual | sí | por partido | no (los 4 son probabilidad/atributo) |
| **Perk** | «una **regla**: cuándo pasa algo distinto» (ADR 0036) | **sí, y es irreversible (RF-072)** | al recibirlo, para siempre | **casi nunca** |

**MEDIDO.** La fórmula de decisión es una sola línea (`Sim/Engine/Utility.cs:159-161`):

```
score = Base(rol, acción) · Táctico(estado, acción)/100 · MultiplicadorDeRasgo/100 + Contexto
```

`MultiplicadorDeRasgo` sale de `MatchPlayer._actionMultipliers`, que se rellena **una sola vez, en el
constructor**, desde `data/traits/traits.json` (`MatchPlayer.cs:97-102`). Ningún `EffectType` escribe en ese
array. Un perk **no puede tocar la única variable del motor que decide qué intenta hacer un jugador.**

### La conclusión que ordena todo el informe

**DERIVADO — la tesis central.** Underleague tiene las capas **invertidas**:

- La capa que el jugador **no elige** (el rasgo, que viene sorteado con el jugador) es la que **define el
  comportamiento**: un `Aggressive` entra un 60 % más y bloquea un 75 % más; un `Coward` entra menos de la
  mitad; un `Lazy` no repliega. Eso sí se ve en pantalla.
- La capa que el jugador **elige, paga e inmoviliza para siempre** (el perk, RF-072) **define números**: 47
  de 61 perks solo multiplican la cuota de una tirada que iba a ocurrir igual.

La separación que declara la ADR 0036 —«perk = regla, objeto = estadística»— **no se ha cumplido en los
datos**. Contrastada contra `/data`:

| ADR 0036 dice | `/data` hace | veredicto |
|---|---|---|
| Perk = una regla: cuándo pasa algo distinto | 77 % de los perks son un multiplicador de tirada | **incumplido** |
| Objeto = una estadística | 34 de 34 objetos suben atributos y nada más | **cumplido** |
| Consumible = un efecto puntual | 4 de 4 | cumplido |

**DERIVADO.** Como el perk multiplica cuotas y el objeto suma atributos, y los atributos **sí** entran en la
utilidad mientras las cuotas **no**, hoy **un objeto cambia más el comportamiento que un perk**. La capa
móvil, barata y reversible (RF-075) pesa más en la conducta que la capa irreversible y cara. Es exactamente
el revés de lo que el diseño quería.

**Qué debería aportar un perk, y solo un perk:** una **excepción legible a la conducta por defecto de la
IA**. Es la única cosa que ninguna otra capa puede dar: los atributos dan grados, los objetos dan grados
movibles, la posición da un esqueleto común a todos los de esa posición, el rasgo da conducta **pero no se
elige**, y las reglas son iguales para todos. El perk es el único sitio donde cabe «este jugador concreto,
por decisión mía, juega distinto al resto de los de su puesto».

---

## 2. Clasificación de los 61 perks

Criterio, tomado del motor y no de la intención declarada:

- **BEHAVIORAL** — su efecto entra en `Utility.cs` de forma que puede cambiar **qué acción se elige**, o
  mueve una precondición dura (correa).
- **BEHAVIORAL-MARGINAL** — entra en la utilidad, pero por una pendiente tan pequeña que casi nunca puede
  voltear una decisión (ver la cuenta abajo).
- **OUTCOME** — cambia **lo bien** que sale una acción ya decidida.
- **COSMETIC-SECONDARY** — no toca el partido: experiencia, estados de run, texto.
- **DEAD** — medido sin impacto demostrable (activación ≈ 0 o valor ≤ 0 con activación baja), o con
  consumidor inexistente.

| categoría | nº | % | perks |
|---|---|---|---|
| **BEHAVIORAL** | **3** | **4,9 %** | `sweeper_keeper`, `long_leash_legacy`, `unlikely_bulwark` |
| BEHAVIORAL-MARGINAL | 4 | 6,6 % | `brute_boots`, `pack_mentality`, `comeback_spirit`, `scar_veteran` |
| **OUTCOME** | **47** | **77,0 %** | el resto del catálogo |
| COSMETIC-SECONDARY | 2 | 3,3 % | `quick_learner`, `numb` |
| DEAD | 5 | 8,2 % | `back_to_back`, `home_ref`, `silky_veteran`, `crowd_control`, `wing_overlap` |

**MEDIDO — la cuenta que degrada a los cuatro «marginales».** Los únicos cinco `modifyAttribute` del
catálogo son **cuatro de fuerza y uno de resistencia**. Cero de técnica, cero de velocidad. Y en
`Utility.cs` las pendientes por atributo valen (`data/ai/weights.json`, `Slope(s,a) = s·(a−pivote)`):

| atributo | pendientes que alimenta | efecto de +10 |
|---|---|---|
| técnica | pase en profundidad 12, pase largo 10, regate 7, tiro 6, pase corto 2 | hasta **+120** de contexto |
| velocidad | regate 4, y la carrera del pase en profundidad | +40 y ticks |
| **fuerza** | **solo `shootStrengthSlope` 3** | **+30 en una sola acción** |
| resistencia | **ninguna** | **0** |

El `Shoot` de un delantero vale `385 · táctico/100 · rasgo/100 + contexto`, con `shootInRangeBonus` 388: del
orden de 700-800 puntos, y compite contra un `FindSpace` de `460 · 210/100 = 966` en posesión. **+30 es un
4 % sobre una diferencia que suele ser de cientos.** *(DERIVADO)*

**DERIVADO.** El catálogo mete sus cinco efectos de atributo exactamente en los dos atributos que **menos**
o **nada** mueven la decisión (fuerza y resistencia), y deja intacto el que más la mueve (técnica). Si
alguien quiso que los perks de atributo cambiaran conducta, eligió los canales equivocados.

**Resultado de la sección: el 4,9 % del catálogo cambia lo que un jugador intenta hacer.** Y de esos tres,
dos (`long_leash_legacy`, `unlikely_bulwark`) solo agrandan la zona; el único que produce una acción nueva y
visible es **`sweeper_keeper`: el portero sale del área**. Un perk de sesenta y uno.

---

## 3. Test «¿qué hace este perk que el personaje antes no hacía?»

Regla del test: si no puedo escribir una **acción observable en pantalla** que antes no ocurría, el perk es
problemático. «Gana más entradas» no es una acción observable nueva: es la misma entrada con otro resultado.

**MEDIDO/DERIVADO.** De los 61:

| respuesta | nº | lectura |
|---|---|---|
| Hay una acción nueva y visible | **7** | `sweeper_keeper` (el portero abandona el área), `iron_gate` (una lesión que no ocurre: se levanta), `mob_instigator` (en la turba no se pita), y los **cuatro letales** (`iron_studs`, `marrow_thirst`, `second_wound`, `skullsplitter`: un rival **muere**) |
| Hay un cambio de frecuencia perceptible dentro del partido | 13 | los maestros y los grandes acumuladores (`granite_line`, `killing_range`, `battle_reader`, `captains_voice`, `first_touch_school`, `fine_orchestra`, `natural_leader`, `deathless_march`, `last_ditch`, `blood_tithe`, `unlikely_bulwark`, `roots`, `hot_blooded`) |
| **Ninguna: el jugador hace exactamente lo mismo** | **41** | **PROBLEMÁTICOS** |

Los 41 problemáticos son todos de la misma forma: «al [disparador], si [condición], multiplica por N
[canal]». El personaje no cambia de intención, cambia de dado.

**El caso que mejor lo ilustra:** `own_third_anchor` («si empieza en su tercio, ×2 robar el balón») y
`bulwark_stance` («si es Muro, ×2 robar el balón») describen a dos personajes distintos en el texto y
producen **el mismo partido**: el jugador intenta las mismas entradas en los mismos ticks y gana algunas
más. La fantasía está en el nombre, no en el motor. *(DERIVADO)*

---

## 4. Complejidad de condiciones

Cuatro grados, y la distinción que importa: **complejidad que produce decisiones** (el jugador puede actuar
sobre la condición antes del partido) frente a **complejidad que solo produce condiciones de ejecución** (el
motor comprueba algo que el jugador no puede prever ni provocar).

| grado | definición | nº |
|---|---|---|
| **SIMPLE** | sin condición, o una sola comprobación fija (`hasTag(owner,…)`, distancia a portería) | 21 |
| **CONTEXTUAL** | condición decidible al alinear (zona de partida, vínculo, composición) o estado de partido evidente | 18 |
| **COMPLEJO** | contador + límite + acumulación entre partidos, o efecto sobre un tercero | 15 |
| **OPACO** | el jugador **no puede saber** si se va a cumplir | 7 |

**Los siete OPACOS**, con su motivo *(MEDIDO, de la condición en `/data`)*:

| perk | condición | por qué es opaca |
|---|---|---|
| `back_to_back` | `nearAlly(actor,'Bulwark',2)` | proximidad **en el tick de la entrada**, no en la alineación. Medido: **0 % en 21 partidos** |
| `shadow_marker` | `nearAlly(actor,'Brute',2)` | igual |
| `safety_net` | `nearAlly(actor,'Defender',3)` | igual, y sobre el portero |
| `crowd_control` | `nearOpponent(actor,'Fine',2)` | depende de las **etiquetas del rival**, que el jugador solo ve en el ojeo |
| `high_press_trigger` | `PASS_FAILED` + `zone(actor)=='Opposing'` | se dispara con un **fallo propio** en campo rival |
| `road_warrior` | `stat(actor,'tacklesWon') >= 2` | única invocación de `stat()` en todo el catálogo; el contador no se enseña |
| `home_ref` | `scoreDiff() < 0` en `FOUL` | exige ir perdiendo **y** hacer falta; medido **1,4 % en 73 partidos** |

**DERIVADO, y es el hallazgo de esta sección:** los siete opacos tienen valor medido de **−1 a 5** y tasas
de activación de **0 % a 9 %**. La opacidad no es un defecto estético: **es la causa mecánica de que el perk
no valga nada.** Una condición que el jugador no puede provocar es una condición que casi nunca se cumple.

**Complejidad que produce decisiones:** `Sim/Perks/LineupPerkPreview.cs` sabe resolver exactamente **cinco**
funciones sobre la alineación (`hasTag`, `startsIn`, `startsOn`, `linked`, `teammatesWithTag`/
`adjacentCount`) y solo cuando el sujeto es `owner`. **MEDIDO:** 22 de los 40 perks con condición son
previsibles en la pantalla de Equipo; la pantalla incluso **avisa con un aviso flotante** cuando mover a un
jugador enciende o apaga un perk (`Game/Screens/TeamScreen.cs:388-392`). Eso es buen diseño y hay que
conservarlo. Los **18 restantes** no son previsibles: de ellos salen los siete opacos y todos los
acumuladores, cuyo contador el jugador no ve durante el partido.

**HIPÓTESIS.** El eje `axis` de los datos (`accumulation`, `identity`, `geometry`, `alignment`, `startZone`,
`matchState`, `composition`, `proximity`) y el eje `family`/canal (`tackle`, `shotOnTarget`, `intercept`,
`injure`, `pass`, `dribble`, `save`) revelan cómo se generó el catálogo: **como un producto cartesiano de
ocho ejes de condición por seis canales de efecto**. Cada casilla del cruce es un fichero JSON. Eso explica
a la vez la inflación (§9), la ausencia de conducta (§2) y por qué existen condiciones que solo existen
porque NCalc las puede expresar: de las 21 funciones del lenguaje de condiciones, **8 no las usa ningún
dato** (`attr`, `level`, `position`, `bias`, `adjacent`, `adjacentCount`, `tick`, `detail`) *(MEDIDO, por
`grep` sobre `data/perks` y `data/races`)*. El lenguaje se diseñó antes que el contenido.

---

## 5. Memorabilidad: «este perk hace que mi jugador ______»

Se completa la frase **con un verbo de conducta**. Si solo se puede completar con «…tenga más probabilidad
de X», no se completa.

**MEDIDO/DERIVADO. Se completa con conducta en 10 de 61 (16,4 %):**

| perk | «este perk hace que mi jugador…» |
|---|---|
| `sweeper_keeper` | …**salga del área** a barrer cuando el balón cae en su zona |
| `unlikely_bulwark` | …**cubra más campo** del que un elfo debería cubrir |
| `long_leash_legacy` | …**se despegue de su casilla** un poco más cada cuatro partidos |
| `iron_gate` | …**se levante** de una lesión que habría sido baja |
| `mob_instigator` | …**pueda cometer dos faltas impunes** cuando el árbitro se va |
| `iron_studs`, `marrow_thirst`, `skullsplitter`, `second_wound` | …**mate** |
| `roots` | …**no se mueva** cuando lo empujan |
| `hot_blooded` | …**deje al rival en el suelo** más tiempo |

**Los otros 51 solo se completan con «…tenga más probabilidad de…».** Entre ellos están los cuatro perks de
**más valor medido del juego** (`deathless_march` 178, `battle_reader` 150, `granite_line` 115,
`killing_range` 112). **DERIVADO, y es incómodo:** el catálogo tiene una **correlación inversa entre valor y
memorabilidad**. Lo más potente es lo menos reconocible. Un jugador que gana gracias a `battle_reader` no
tiene forma de saber que ha ganado gracias a `battle_reader`.

---

## 6. Observabilidad

**MEDIDO, y condiciona toda la sección:** las activaciones de perk **no viajan en el flujo de eventos**.
Viven en una lista aparte del informe (`MatchReport.PerkActivations`) que **solo consume la pantalla de
post-partido** (`Sim/Run/View/PostMatchView.cs:201-209`). `Game/Screens/MatchScreen.cs` menciona la palabra
«perk» **cuatro veces y ninguna para dibujar una activación**: las tres primeras son comentarios y la cuarta
resuelve el nombre del perk **culpable de una muerte** en un texto.

**DERIVADO:** durante los 60-90 s de partido, el jugador **no ve ni un solo perk dispararse**. Todo lo que
puede observar es la consecuencia física. Con ese suelo:

| grado | criterio | nº | ejemplos |
|---|---|---|---|
| **OBVIOUS** | produce un suceso que sin el perk no existiría | **7** | los 4 letales, `iron_gate`, `mob_instigator`, `sweeper_keeper` |
| **NOTICEABLE** | cambia la frecuencia de un suceso lo bastante para notarlo en un partido (×3 o más sobre un canal frecuente) | 13 | `granite_line`, `killing_range`, `battle_reader`, `natural_leader`, `first_touch_school`, `fine_orchestra`, `captains_voice`, `deathless_march`, `last_ditch`, `blood_tithe`, `unlikely_bulwark`, `roots`, `hot_blooded` |
| **SUBTLE** | ×1,3-×2 sobre un canal común, o un bono de atributo | 27 | casi todo el relleno |
| **INVISIBLE** | el jugador no puede percibirlo ni acumulando partidos | **14** | `home_ref` (criterio +10 una vez por partido), `bruised_knuckles`, `scar_tissue`, `poacher_instinct`, `iron_lungs` (resistencia: **no entra en la utilidad**), `comeback_spirit`, `high_press_trigger`, `road_warrior`, `back_to_back`, `crowd_control`, `silky_veteran`, `wing_overlap`, `numb`, `quick_learner` |

**41 de 61 (67 %) son SUBTLE o INVISIBLE.** *(DERIVADO)*

**Nota sobre el texto que lee el jugador.** Las descripciones se generan desde el efecto (RT-035), lo cual
es correcto y hay que conservarlo, pero **la plantilla obliga a que el texto sea aritmética**: «Al entrar,
si el jugador está en zona propia, el jugador multiplica por 3 sus opciones de robar el balón». Ese texto es
fiel y **no dibuja a nadie**. Comparado con «En su área no pasa nadie», dice lo mismo y no significa lo
mismo. *(HIPÓTESIS de diseño; el mecanismo es MEDIDO en `data/l10n/es/templates.json`.)*

---

## 7. Decisión de build

| grado | definición | nº | % |
|---|---|---|---|
| **BUILD-DEFINING** | orienta la run entera: los cuatro maestros (exigen 2 de su línea y cierran otra), los letales y los grandes acumuladores | **14** | 23,0 % |
| **ROLE-DEFINING** | define cómo juega **ese puesto** (portero, banda, punta, pareja de centrales) | 11 | 18,0 % |
| **SITUATIONAL** | solo importa en una situación concreta del partido | 5 | 8,2 % |
| **NUMERIC** | sube un número; la decisión es «¿cuánto sube?» | **16** | 26,2 % |
| **IRRELEVANT** | no cambia la partida de forma detectable | **15** | 24,6 % |

**DERIVADO:** la mitad del catálogo (**31 de 61**, NUMERIC + IRRELEVANT) **no participa en ninguna decisión
de construcción**. Y la parte que sí decide se apoya casi entera en una estructura externa al perk —la línea
de build de la ADR 0051, que es la que exige y bloquea—, no en lo que el perk hace.

---

## 8. Perk → posición: dónde hay fantasía y dónde no

**MEDIDO.** Solo **3 perks de 61** declaran `positionOnly`, los tres del portero (`sweeper_keeper`,
`clean_sheet_legacy`, `safety_net`). La función `position()` **existe en el lenguaje de condiciones y no la
usa ningún dato**. Toda la identidad posicional del catálogo la carga `startsIn`/`startsOn` (9 perks), que
no habla del **rol** sino de la **casilla de partida**.

| puesto | perks que lo definen | veredicto |
|---|---|---|
| **Portero** | 3 dedicados, y uno de ellos (`sweeper_keeper`) es el **único perk verdaderamente conductual del juego** | **la mejor fantasía del catálogo**, y es la más pequeña |
| **Defensa** | la línea `wall` (8) + `startsIn(OwnThird)` | fantasía clara pero **monocanal**: los 8 hablan de entrada o parada. El defensa que **saca el balón jugado** no existe |
| **Delantero** | la línea `aim` (8) | clara: rematar. Pero los 8 son el mismo efecto (`shotOnTarget`) con distinta distancia o etiqueta |
| **Centrocampista** | **ninguno propio** | **hueco**. `captains_voice` y `center_conductor` empiezan en el centro y dan… entrada e intercepción. La línea `craft` (pase y regate) no es del centrocampista: la puede llevar cualquiera |

**DERIVADO — el hueco más grande del catálogo:** el centrocampista es, según los pesos base, el puesto con
el reparto de acciones más rico (`ShortPass 560`, `ChaseBall 420`, `FindSpace 380`, `Dribble 240`,
`MarkOpponent 300`), y **no hay un solo perk que lo empuje hacia una de ellas**. El puesto que más
decisiones toma es el que menos puede personalizarse.

---

## 9. Familias por función real (no por nombre)

Ignorando los nombres y la `family` declarada, y agrupando por **qué hace el efecto**:

| familia real | nº | ¿cuántos hacen algo distinto de verdad? |
|---|---|---|
| **Multiplicar la tirada de entrada (`tackle`)** | **14** | **1**. Los otros 13 son la misma línea con otra condición |
| Multiplicar el tiro a puerta (`shotOnTarget`) | 8 | 1 (y `killing_range` solo se distingue por la magnitud) |
| Multiplicar la lesión (`injure`/`severeInjury`) | 8 | 2: lesionar y **matar** (el flag `lethal` sí es una regla nueva) |
| Multiplicar la intercepción (`intercept`) | 7 | 1 |
| Multiplicar pase / regate / evasión | 8 | 1 |
| Multiplicar la parada (`save`) | 2 | 1 |
| Subir un atributo | 5 | 1, y **duplica la capa de objetos** (ADR 0036) |
| Agrandar la correa | 3 | **1, y es el bueno** |
| Anular un evento | 2 | 1 |
| Inmunidad / criterio / derribo / experiencia | 4 | 4 (uno cada uno; uno de ellos, el luto, **sin consumidor**) |

**Los 14 «×N entrada»** *(MEDIDO, de `/data`)*: `granite_line`, `captains_voice`, `own_third_anchor`,
`pit_veteran`, `last_ditch`, `back_to_back`, `game_management`, `bulwark_stance`, `pivot_duo`,
`diagonal_press`, `road_warrior`, `natural_leader`, `unlikely_bulwark`, `deathless_march`. **El 23 % del
catálogo es una sola frase mecánica dicha de catorce maneras.**

**MEDIDO — inflación cuantificada:** 61 ficheros sostienen **~10 mecánicas distintas** (subir una tirada;
acumular un contador que sube una tirada; subir un atributo; agrandar la zona; anular un evento; conceder
inmunidad; mover el criterio del árbitro; alargar el derribo; dar experiencia; marcar el perk como letal) y
**~16 fantasías distintas**. La ratio es de **3,8 ficheros por fantasía**.

---

## 10. Falsas decisiones

Familias donde tres o más perks distintos en código son **una sola decisión de diseño**. Elegir entre ellos
no es una decisión: es leer el número mayor.

| falsa decisión | perks implicados | cuál es la única decisión real |
|---|---|---|
| **«Quiero robar más»** | `bulwark_stance`, `own_third_anchor`, `pit_veteran`, `last_ditch`, `game_management`, `back_to_back` (6) | **dónde coloco al jugador.** Los seis dan lo mismo si está en el sitio; ninguno da nada si no |
| **«Quiero rematar mejor»** | `box_predator`, `long_range_menace`, `killing_range`, `forward_line`, `cold_focus` (5) | **a qué distancia remata mi delantero**, que ya lo decide su posición y su rasgo `LongShot` |
| **«Quiero un acumulador»** | `battle_reader`, `lane_reader`, `sharpshooter_drill`, `steady_hands`, `silky_veteran`, `clean_sheet_legacy`, `pit_veteran`, `poacher_instinct`, `scar_tissue`, `bruised_knuckles` (10) | **ninguna**: se cogen todos los que caigan, porque su curva es idéntica y solo cambia el canal |
| **«Quiero un vínculo»** | `pivot_duo`, `diagonal_press`, `wing_overlap`, `covering_shadow`, `spearpoint`, `gentle_giant` (6) | **qué dos jugadores pongo juntos**, y eso ya lo decide la formación |

**DERIVADO:** 27 de 61 perks (44 %) viven dentro de una de estas cuatro falsas decisiones. La decisión real
que hay detrás de las cuatro es **la misma que el jugador ya toma al alinear**. El perk no añade una
decisión: **pone precio a una que ya existía**.

Esto encaja con lo que dijo el propio revisor al aceptar la ADR 0088: «un perk mal puesto es un perk que no
tiene efecto (por ejemplo, que no esté en la zona en la que se activa)». **La decisión del sistema es la
colocación.** El problema es que el perk aporta poco más que eso.

---

## 11. Contadores

16 perks con `axis: accumulation`, 15 efectos `addCounter`, y 11 de ellos con `accumulatesAcrossMatches`
*(MEDIDO)*. Cumplen RF-070 («al menos 15 perks acumulan entre partidos»). ¿Crean una escalada que el jugador
entienda?

| clase | definición | nº | perks |
|---|---|---|---|
| **EXPRESIVO** | la escalada se nota y **cuenta una historia** | **0** | — |
| **TÁCTICO** | el jugador puede acelerar el contador con una decisión | 3 | `pit_veteran`, `sharpshooter_drill`, `steady_hands` (jugar a entrar, a tirar, a pasar) |
| **INVISIBLE** | sube solo y no se ve | **10** | `battle_reader`, `deathless_march`, `captains_voice`, `lane_reader`, `scar_veteran`, `iron_lungs`, `long_leash_legacy`, `clean_sheet_legacy`, `scar_tissue`, `poacher_instinct` |
| **INNECESARIO** | el contador no es más que un retardo | 3 | `silky_veteran`, `bruised_knuckles`, `crowd_control` (límite 1/partido sobre un canal ya raro) |

**DERIVADO, y es grave:** los **diez invisibles** incluyen los tres perks de más valor del juego
(`deathless_march` 178, `battle_reader` 150, `captains_voice` 104). Su condición es **«por cada partido
jugado»**: el jugador no hace nada, no decide nada y no ve nada; el número sube solo. Es la definición de
escalada no ganada. Y el motor lo agrava: `LimitScope.Run` no se reinicia nunca y el contador se vuelca
entre partidos sin que ninguna pantalla lo enseñe.

**HIPÓTESIS.** Un contador solo merece existir si el jugador **lo ve subir** y **puede empujarlo**. Los tres
tácticos son el patrón correcto (entrar sube el perk de entrar); los diez invisibles deberían ser, o bien
efectos planos, o bien contadores ligados al **desgaste de la plantilla**, que es el recurso que el juego
dice tener en el centro: partidos jugados **por este jugador**, cicatrices acumuladas, compañeros muertos.
Eso sí es una historia; «partidos jugados por el equipo» no lo es.

---

## 12. Sinergias y anti-sinergias **de diseño**

No se pregunta si los números se suman (eso ya está: se componen como cuotas con techo por rareza, ADR
0050), sino si **la combinación crea una identidad reconocible**.

**Sinergias que producen identidad (pocas):**

1. **La Carnicería + violencia + árbitro.** `blood_tithe` → `marrow_thirst`/`iron_studs` construye un equipo
   que **mata**, y la muerte es el suceso más visible del juego. Es la única línea donde tres perks suman
   una **identidad narrativa**, no un número. Y tiene contrapeso previsto en los requisitos (el criterio del
   árbitro, RF-064e). **Es el mejor diseño del catálogo actual.** *(DERIVADO)*
2. **Composición de etiquetas.** `pack_mentality` + `brute_boots` + `blood_tithe` premian **fichar brutos**,
   y eso sí es una decisión de plantilla con coste (los brutos son lentos y torpes por `styles.json`).
3. **`sweeper_keeper` + correa.** Un portero que sale, con `loose_leash_charm`, cambia la forma del equipo.
   Es una sinergia real y **está sostenida por un solo perk**.

**Anti-sinergias de diseño (más interesantes que las sinergias, y hoy casi inexistentes):**

- **La única real es estructural, no de contenido:** el maestro de línea **cierra la línea opuesta** para el
  resto de la run (ADR 0051). Carnicería cierra Toque; Muralla cierra Puntería. Eso sí es una decisión
  irreversible con coste. *(MEDIDO en `/data`: los 4 maestros declaran `blocksPerks`.)*
- **Fuera de eso, no hay ninguna anti-sinergia.** `elseEffects` está vacío en los 61 perks (ADR 0088:
  ningún perk es negativo) y ningún perk empeora nada de nadie. **DERIVADO:** dos perks nunca se estorban,
  así que **no existe la pregunta «¿estos dos casan?»**. Solo existe «¿cuál suma más?».

**HIPÓTESIS.** La ADR 0088 resolvió correctamente un problema real (un perk mal colocado no debe castigar),
pero su lectura literal —«ningún perk perjudica en ninguna rama»— ha eliminado de paso **el coste
conductual**, que es cosa distinta de un malus. Que un delantero con «Depredador» deje de replegar **no es
un castigo**: es lo que hace que sea un depredador y no un jugador mejor. Sin coste conductual no hay
identidad, porque una identidad es, por definición, hacer unas cosas **en lugar de** otras.

---

## 13. Coste de oportunidad: ¿«quiero este en vez de aquel» o «el de mayor valor esperado»?

Lo que el jugador elige de verdad, hoy *(MEDIDO, del flujo de la run)*: tras ganar, 1 de 3 opciones
(RF-071); si es perk, **elige además a qué jugador se lo pone**; los slots por jugador son **2 / 3 / 4**
según rareza (`Progression.PerkSlots`), y son irreversibles (RF-072).

| pregunta | respuesta hoy | por qué |
|---|---|---|
| ¿Elijo perk A o perk B? | **valor esperado** | ningún perk estorba a otro, ninguno tiene contrapartida, todos suben algo |
| ¿A quién se lo pongo? | **decisión real** | es la única que existe, y la pantalla de Equipo la sostiene bien con el aviso de perk encendido/apagado |
| ¿Me conviene cerrar una línea? | **decisión real** | los cuatro maestros, y solo ellos |
| ¿Qué sacrifico? | **nada** | 30 de 34 objetos no tienen contrapartida; 61 de 61 perks tampoco |

**DERIVADO:** el coste de oportunidad **existe pero está fuera del perk**: es el slot y el oro (ADR 0072), no
lo que el perk hace. Un sistema donde todas las opciones son positivas y ninguna se estorba tiene
exactamente una estrategia óptima: **ordenar por valor**. Que el juego mida los valores de los perks
(`perk-values.json`) y que el jugador pueda deducirlos es la prueba de que esa estrategia es computable.

**Y hay una consecuencia peor.** Un perk cuyo valor depende de la colocación, unido a que colocar mal no
castiga (ADR 0088), convierte la elección en **gratuita en ambos sentidos**: si aciertas, sube; si fallas,
no pasa nada. La decisión no tiene riesgo, y una decisión sin riesgo no se recuerda.

---

## 14. Arquitectura de efectos: evaluación crítica

### 14.1 Conservar `modifyAttribute` y `modifyLeash` — **sí, pero corrigiendo el canal**

`modifyLeash` es **el mejor efecto del sistema** y hay que conservarlo tal cual: mueve una **precondición
dura** (`Utility.cs:532-537` descarta la acción entera) y produce conducta visible.

`modifyAttribute` hay que conservarlo **y reorientarlo**: hoy los cinco que existen tocan fuerza y
resistencia, los dos atributos que menos mueven la utilidad. Si la intención era conductual, el canal es
**técnica** (hasta +120 de contexto por +10) y **velocidad**. **Riesgo (DERIVADO):** técnica y velocidad son
también los atributos que más pesan en las resoluciones, así que un perk de técnica es a la vez conducta y
potencia; habría que medirlo contra su control emparejado (ADR 0087) antes de fijar magnitudes.

**Aviso de solapamiento:** `modifyAttribute` **duplica la capa de objetos** (ADR 0036). Un perk de +10
fuerza y unos `iron_gauntlets` son el mismo efecto por dos vías, una irreversible y otra transferible. Si se
conserva, debería restringirse a lo que un objeto **no** puede hacer: atributo **condicionado** o
**creciente con la run**, nunca un +10 plano.

### 14.2 Reducir `modifyProbability` — **sí, pero no eliminarlo**

**DERIVADO.** `modifyProbability` no es basura: es el canal del **oficio** («este remata mejor que nadie»),
que es una fantasía legítima y necesaria. El problema no es que exista, es que **es el 62,7 % del catálogo**
y que se usa para expresar fantasías que no son de oficio (la mitad de los 14 «×N entrada» quieren decir
«este defensa es un animal», que es conducta, no puntería).

Propuesta: **techo duro del 40 %** del catálogo, y solo sobre canales cuyo suceso el jugador ve contarse en
el informe (tiros a puerta, entradas ganadas, lesiones causadas, paradas). Los canales invisibles
(`severeInjury` ×1,15, `bias`) no deberían llevar un perk entero.

### 14.3 `ModifyUtility`: ¿resolvería el problema? ¿qué rompería?

**El mecanismo ya existe y no es una invención.** *(MEDIDO)* `MatchPlayer._actionMultipliers` es un array de
14 enteros, uno por acción, que multiplica el score en `Utility.cs:159`. Lo rellenan los rasgos
(`Tackle 160` para `Aggressive`, `Retreat 50` para `Lazy`, `Shoot 150` para `Scorer`). Un
`modifyUtility(action, percent)` sería **la hermana exacta de `AddAttributeDelta`**: un array de deltas, una
llamada en `EffectEngine.ApplyEffects`, una lectura en `ActionMultiplier`.

**¿Resolvería el problema?** **Sí, y es el único candidato que lo resuelve.** *(DERIVADO)* Es la única
variable del motor que determina **qué intenta hacer** un jugador y que hoy ningún perk puede tocar. Con
ella, «este delantero dispara en cuanto cruza el medio campo» deja de ser texto y pasa a ser un número
entero en un array.

**Qué rompería, honestamente:**

1. **Las puertas de balance, y mucho.** Un ×1,6 a `Tackle` es incomparablemente más potente que un ×2 a la
   tirada de entrada: cambia **cuántas entradas hay**, y las entradas son la fuente de
   `injuriesPerMatch`, que ya está en el techo de RT-056 (la ADR 0105 documenta que `Block` tuvo que subir
   poco «porque `injuriesPerMatch` no tiene presupuesto para dos fuentes de violencia nuevas a la vez»).
   Igual con `ballThirdMaxShare`, la cadena de pases (ADR 0111) y los tiros (ADR 0109). **Esto no es un
   parche: es un recalibrado.** *(DERIVADO)*
2. **El instrumento de valor.** `perk-values.json` mide cada perk contra su control emparejado (ADR 0087);
   seguiría funcionando, pero los valores serían mucho mayores y **más dependientes de la posición**, con lo
   que el `rowDeviation` que la ADR 0087 dejó en 7 volvería a abrirse.
3. **La ADR 0088.** Un multiplicador **por debajo de 100** en otra acción es el corazón del diseño
   («dispara más, pasa menos») y hay que decidir si eso es «un perk negativo». **Mi lectura, HIPÓTESIS:** no
   lo es —no reduce ninguna capacidad del equipo, reasigna la intención de un jugador, y el revisor definió
   el perk mal puesto como «un perk que no tiene efecto», que es cosa distinta—, pero **es una decisión del
   revisor y no se puede dar por supuesta.** Si la respuesta es «sigue prohibido», `ModifyUtility` se puede
   implementar **solo al alza** (subir una acción sin bajar otra) y sigue produciendo conducta, aunque con
   menos identidad.
4. **Conducta degenerada.** Nada impide hoy que un ×3 a `Shoot` produzca un portero que chuta desde su
   área. Haría falta un **techo por acción y por posición** en el cargador (RT-032: error explícito, nunca
   silencioso), igual que la rareza pone techo a las cuotas (ADR 0058).
5. **Lo que NO rompe:** el determinismo (enteros, orden de suscripción ya determinista, sin E/S), la
   separación `/Sim`-`/Game`, la generación de descripciones (necesita una plantilla nueva y los nombres de
   las 14 acciones en `l10n`, que es trabajo mecánico) y la previsión de alineación, que no depende del
   efecto sino de la condición.

**Y una pieza que hay que añadir aunque no se toque nada más** *(DERIVADO, coste bajo, valor alto)*: hoy la
activación de un perk **no llega al flujo de eventos**, así que la pantalla de partido **no puede** decir
«eso lo ha hecho el perk» ni aunque quisiera. Emitir la activación como evento (respetando RT-014: el render
consume, no decide) es condición necesaria para que cualquier rediseño se note. Sin eso, un perk conductual
seguiría siendo un cambio anónimo.

---

## 15. Taxonomía nueva (5 categorías)

Cada categoría compra **una decisión distinta** y tiene **un canal técnico distinto**. Si un perk no encaja
limpiamente en una, no debería existir.

| categoría | qué decide el jugador | canal técnico | cómo se reconoce en pantalla | cuota propuesta |
|---|---|---|---|---|
| **1. INSTINTO** | cómo juega **este** jugador, frente a los demás de su puesto | `modifyUtility` (+ `modifyLeash`) | hace algo que los demás de su puesto no hacen | **35 %** |
| **2. OFICIO** | en qué es **excepcional** | `modifyProbability` sobre un canal contable | el informe lo cuenta: tiros a puerta, entradas ganadas, paradas | 30 % |
| **3. CARNICERÍA** | cuánto está dispuesto a **costarle a la plantilla** (propia y ajena) | `lethal`, `injure`, `cancelEvent`, `modifyBias` | alguien no se levanta; el árbitro señala o no | 15 % |
| **4. HERENCIA** | qué historia acumula **este jugador** a lo largo de la run | contador **visible** ligado al desgaste | el retrato lleva la cuenta y el efecto se nota al subir | 10 % |
| **5. PACTO** | **con quién** juega: la alineación | vínculo / composición, previsible en la pantalla de Equipo | dos jugadores hacen algo juntos | 10 % |

Las cinco cubren lo que hoy existe **sin el cruce cartesiano**: la condición deja de ser un eje libre y pasa
a ser parte de la categoría (Instinto = siempre; Oficio = casi siempre; Carnicería = al contacto; Herencia =
acumulada; Pacto = por alineación).

---

## 16. Perk Design Test

Versión mejorada del borrador. Un perk nuevo **debe pasar los diez**. Los que llevan **[E]** son
eliminatorios por sí solos; los demás admiten discusión documentada en el ADR.

1. **[E] Una frase sin cifras.** Se describe en una frase que **no contenga ningún número**. «En su área no
   pasa nadie.» Si hace falta un número para explicarlo, es un ajuste de balance disfrazado de perk.
2. **[E] Verbo de conducta.** Se completa «este perk hace que mi jugador **[verbo]**» con un verbo que sea
   una acción del motor (salir, entrar, replegar, disparar, marcar, perseguir), no «tenga más probabilidad
   de».
3. **[E] Atribuible en una jugada.** Existe **una jugada concreta** tras la cual el jugador puede decir «eso
   ha sido el perk». Si solo se puede saber sumando cien partidos, es un número, no un perk.
4. **[E] Distinto de todo lo que existe.** No comparte (canal + condición + magnitud) con ninguno del
   catálogo. Una magnitud distinta **no** es un perk distinto.
5. **Puesto natural.** Hay un puesto en el que es evidentemente bueno y otro en el que es evidentemente
   inútil. Si sirve igual en los cuatro, es un atributo y debería ser un objeto.
6. **Coste real.** O reasigna conducta (hace menos de otra cosa), o cierra una puerta (línea, etiqueta,
   colocación), o **arriesga carne**. Un perk sin coste no es una decisión, es un regalo.
7. **El jugador sabe cuándo funciona, antes.** La condición se resuelve **en la pantalla de Equipo** o es
   una situación de partido que el jugador reconoce. Si depende de la proximidad en un tick o de una
   etiqueta del rival, **no pasa** (regla 11 de `CLAUDE.md`: todo lo malo debe ser previsible; y lo bueno
   también, si hay que decidir con ello).
8. **Una sola condición.** Dos condiciones encadenadas solo se admiten si la segunda es la **decisión** del
   jugador (dónde lo coloco) y la primera su identidad (qué es). Tres, nunca.
9. **Sin contador salvo que se vea.** Si acumula, el contador se enseña en el retrato y el jugador puede
   empujarlo con una decisión. Un contador que sube solo y no se ve es una subida de números diferida.
10. **Sobrevive a la prueba ciega.** Un revisor que mira el partido sin saber la alineación debería poder
    señalar al portador. *(Es el test de §J.3 y es el único que no se puede falsear.)*

---

## 17. Reducción del catálogo

**MEDIDO:** 61 ficheros = 56 perks obtenibles + 5 habilidades raciales. Sostienen ~10 mecánicas y ~16
fantasías (§9).

| acción | nº | detalle |
|---|---|---|
| **Eliminar sin sustituto** | **11** | los 5 muertos (`back_to_back`, `home_ref`, `silky_veteran`, `crowd_control`, `wing_overlap`) + 6 variantes numéricas puras (`cold_focus`, `box_predator`, `game_management`, `bulwark_stance`, `scar_tissue`, `poacher_instinct`) |
| **Fusionar** | **24 → 7** | «×N entrada» 6→1 · «×N tiro» 5→1 · acumuladores 10→3 (uno por categoría: oficio, herencia, carnicería) · vínculos 6→2 |
| **Convertir en otro sistema** | **6** | `iron_lungs` y los +10 planos de atributo → **objeto** (es literalmente lo que hace un objeto); `quick_learner` → **rasgo de club/raza**, no perk; `home_ref` → **consumible** o rasgo de árbitro (RF-061); `numb` → requiere implementar RF-104 o retirarse del texto |
| **Conservar como están** | **9** | los 4 letales, `iron_gate`, `mob_instigator`, `sweeper_keeper`, `first_touch_school`, `granite_line` |
| **Rediseñar** | el resto | §18 |

**Propuesta: 32 perks obtenibles + 5 raciales = 37 ficheros** (−39 % de ficheros, ~+100 % de fantasías
distintas). Reparto: **4 líneas × 7** (cada una: 1 maestro + 2 Instinto + 2 Oficio + 1 Herencia + 1 Pacto) +
**4 sin línea** (los letales y el legendario).

**Por qué 32 y no 20 ni 40** *(DERIVADO del flujo de la run + HIPÓTESIS de diseño)*:

- **Suelo (no menos de ~24).** Una run llena entre **14 y 20 slots** (7 titulares × 2-4 slots según rareza,
  `Progression.PerkSlots`). Con menos de 24 el jugador ve casi todo el catálogo en una sola run y la segunda
  no trae sorpresas.
- **Techo (no más de ~36).** Con cuatro líneas, una línea de más de 8 no cabe en una run: el maestro exige
  2 de su línea y una run compra 3-4 de una línea a lo sumo. Todo lo que pase de ahí **tiene que ser**
  variante numérica para caber en el mismo espacio de diseño, que es exactamente el fallo actual.
- **32 = 8 por línea** deja ver 2-3 de cada línea por run, permite cerrar un maestro, y aún deja perks sin
  ver a la tercera run.
- **Y no se pierde ni un concepto:** los 24 que desaparecen son **duplicados medidos**, no fantasías.

---

## 18. Perks rediseñados (14)

Formato: **PERK ACTUAL → PROBLEMA → NUEVA FANTASÍA → NUEVO COMPORTAMIENTO → POSICIÓN → COMPLEJIDAD →
EJEMPLO DE PARTIDO**. El comportamiento se expresa con el canal `modifyUtility` de §14.3 salvo donde se
indique; los porcentajes son **ilustrativos**, no propuestas de balance (HIPÓTESIS en todos los casos).

---

**1. `back_to_back` → «Espalda con espalda»** (Muralla · Pacto)
- **Problema:** 0 % de activación en 21 partidos; condición opaca de proximidad en el tick.
- **Fantasía:** dos centrales que no se mueven de la línea mientras el otro siga en pie.
- **Comportamiento:** mientras su vinculado (`linked`, previsible) esté en el campo y de pie, `Retreat ×40`
  y `MarkOpponent ×140`. Si el compañero cae, se pierde el efecto **hasta que vuelva**.
- **Posición:** defensa. **Complejidad:** SIMPLE (una condición, decidible al alinear).
- **En partido:** el equipo se repliega tras un córner en contra y **dos defensas se quedan clavados** en la
  frontal. Cuando uno se lesiona, el otro empieza a replegar como todos. «Ah, eso lo ha hecho el perk.»

**2. `crowd_control` → «Perro de presa»** (Muralla · Instinto)
- **Problema:** 6,5 % de activación, valor −1; depende de las etiquetas del rival.
- **Fantasía:** le asignas un hombre y no lo suelta en todo el partido.
- **Comportamiento:** `MarkOpponent ×220`, y su objetivo de marca es **siempre el rival de más técnica**
  (selección de objetivo, no multiplicador). `CoverSpace ×50`.
- **Posición:** centrocampista o defensa. **Complejidad:** SIMPLE.
- **En partido:** el crack rival recibe y **el mismo verde está encima otra vez**; a los 40 s ya se ve que
  lo persigue solo a él.

**3. `sweeper_keeper` → «Portero líbero»** (sin línea · Instinto) — *el modelo a seguir, ampliado*
- **Problema:** ninguno de diseño; el problema es que **es el único** y solo dura una jugada.
- **Fantasía:** un portero que juega de último defensa.
- **Comportamiento:** `+3` de correa **permanente**, `ChaseBall ×150` fuera del área, `Retreat ×60`.
- **Posición:** portero. **Complejidad:** SIMPLE.
- **En partido:** un pase en profundidad entre centrales y **el portero sale a la frontal a despejar**. Dos
  minutos después, el mismo pase entra por el otro lado y **la portería está vacía**. La decisión tiene
  riesgo y se ve.

**4. `poacher_instinct` → «No vuelve»** (Puntería · Instinto)
- **Problema:** el contador exige **marcar** para mejorar el tiro: llega tarde y es invisible.
- **Fantasía:** el delantero que no defiende. Nunca.
- **Comportamiento:** `ChaseBall ×40` y `PressCarrier ×30` fuera del tercio rival; `FindSpace ×160` dentro.
- **Posición:** delantero. **Complejidad:** SIMPLE (una condición geométrica que el jugador entiende).
- **En partido:** el rival saca de banda en campo propio y **hay un jugador nuestro parado en su área**,
  solo, esperando. Es a la vez la ventaja y el precio.

**5. `long_range_menace` → «Amenaza de larga distancia»** (Puntería · Instinto)
- **Problema:** ×2 sobre un tiro que el jugador **nunca intenta** desde ahí; 7,1 % de activación.
- **Fantasía:** si ve portería, dispara. Le da igual la distancia.
- **Comportamiento:** `shootRangeBonusCells +2` (**el canal ya existe** para el rasgo `LongShot`,
  `MatchPlayer.cs:108`) y `Shoot ×135`, `ShortPass ×75`.
- **Posición:** centrocampista. **Complejidad:** SIMPLE.
- **En partido:** el centrocampista recibe a nueve casillas y, en vez de buscar el pase, **arma la pierna**.
  Falla tres y entra una. Todo el mundo sabe de quién es el perk.

**6. `high_press_trigger` → «Presión alta»** (Muralla · Pacto)
- **Problema:** opaco (se dispara con un fallo propio en campo rival), valor 0.
- **Fantasía:** perder el balón arriba es la señal para saltar todos.
- **Comportamiento:** al perder la posesión en el tercio rival, **todo el equipo** `PressCarrier ×200` y
  `Retreat ×50` durante 5 s.
- **Posición:** cualquiera; es de equipo. **Complejidad:** CONTEXTUAL (un estado de partido legible).
- **En partido:** se pierde un balón en la frontal rival y **cinco jugadores saltan a la vez** en lugar de
  replegar. Se ve en un segundo.

**7. `shadow_marker` → «Olfato de sangre»** (Carnicería · Instinto)
- **Problema:** la proximidad a un Bruto en el tick es invisible e incontrolable.
- **Fantasía:** va a por el que ya está tocado.
- **Comportamiento:** su objetivo de entrada es **siempre el rival lesionado o tocado** si hay uno a su
  alcance; `Tackle ×130` contra él. (Selección de objetivo, no probabilidad.)
- **Posición:** defensa o centrocampista. **Complejidad:** SIMPLE.
- **En partido:** un rival sale renqueando de una entrada y **el mismo jugador va directo hacia él** en la
  jugada siguiente. Es la identidad del juego —carnicería administrada— hecha conducta.

**8. `bulwark_stance` + `own_third_anchor` + `pit_veteran` + `game_management` + `last_ditch` → «Ancla»**
(Muralla · Instinto) — *cinco perks en uno*
- **Problema:** cinco maneras de decir «×N entrada», 44 % del catálogo vive de esta falsa decisión.
- **Fantasía:** en su tercio no pasa nadie; fuera de él no sirve para nada.
- **Comportamiento:** en su propio tercio, `Tackle ×170` y `Block ×150`; fuera, `Tackle ×60` y
  `Retreat ×200` (vuelve corriendo).
- **Posición:** defensa. **Complejidad:** SIMPLE (una condición geométrica, previsible).
- **En partido:** intenta un robo en el medio, lo pierde y **arranca de vuelta a su sitio** como si le
  tiraran de una cuerda. En su área es infranqueable.

**9. `steady_hands` + `fine_touch` → «Primer toque»** (Toque · Instinto)
- **Problema:** dos multiplicadores de la tirada de pase; ninguno cambia cuándo se pasa.
- **Fantasía:** el balón no se le para en los pies.
- **Comportamiento:** `ShortPass ×150` y `Dribble ×50`; además, el pase que sale **en el primer tick tras
  recibir** conserva el bono.
- **Posición:** centrocampista. **Complejidad:** SIMPLE. **Cubre el hueco del centrocampista de §8.**
- **En partido:** el balón le llega y **sale al toque** sin que el jugador llegue a pararse. Al lado, otro
  centrocampista conduce tres casillas antes de pasar. Son dos personajes distintos.

**10. `scar_veteran` → «Veterano de cicatrices»** (Herencia)
- **Problema:** +3 de fuerza por partido **del equipo**, invisible, y la fuerza casi no entra en la utilidad.
- **Fantasía:** cada cicatriz le quita un poco de miedo.
- **Comportamiento:** por cada **lesión propia superada** (no por partido jugado), `Tackle ×+15 %`
  acumulativo hasta ×160, y `Retreat ×−10 %`. El contador se muestra en el retrato.
- **Posición:** cualquiera. **Complejidad:** CONTEXTUAL, contador **visible y ligado al desgaste**.
- **En partido:** el central que volvió de dos lesiones **entra a todo**, y ya no repliega como antes. La
  escalada cuenta una historia que el jugador ha vivido.

**11. `mob_instigator` → «Instigador»** (Carnicería) — *conservar el suceso, cambiar el efecto*
- **Problema:** anular faltas en la turba, donde ya casi no se pitan; valor **−7**, el peor del catálogo.
- **Fantasía:** cuando el árbitro se va, es su momento.
- **Comportamiento:** en la turba, `Tackle ×160` y `Block ×160` para **él y sus adyacentes**, y `Retreat ×0`.
- **Posición:** cualquiera. **Complejidad:** SIMPLE (un estado de partido que el jugador ve llegar).
- **En partido:** suena el silbato final del árbitro, empieza la turba y **tres jugadores nuestros se
  lanzan** mientras el resto sigue jugando al fútbol.

**12. `iron_lungs` → «Pulmones de hierro»** (Herencia)
- **Problema:** sube resistencia, el único atributo que **no aparece ni una vez** en `Utility.cs`.
- **Fantasía:** el único que sigue corriendo al final.
- **Comportamiento:** en el último tercio del partido, `ChaseBall ×140` y `PressCarrier ×140` (el resto del
  equipo ya arrastra la fatiga de movimiento).
- **Posición:** centrocampista. **Complejidad:** SIMPLE.
- **En partido:** minuto final, todo el mundo anda, **uno persigue**. Y a veces roba el balón del empate.

**13. `home_ref` → «Cara de inocente»** (Carnicería) — *ya está pedido en RF-064f*
- **Problema:** 1,4 % de activación, valor −1, efecto invisible (criterio +10 una vez).
- **Fantasía:** hace la entrada y pone cara de no haber roto un plato.
- **Comportamiento:** sus faltas desplazan el criterio del árbitro **la mitad** (RF-064f, literal).
- **Posición:** el que más entra. **Complejidad:** SIMPLE.
- **En partido:** la barra de criterio es **visible todo el partido** (RF-062): el jugador ve que su carnicero
  hace tres faltas y la barra baja lo que bajaría con una y media. Es la mitigación que RF-064f pide para
  que el soborno no sea la única vía. **Observabilidad garantizada por una pantalla que ya existe.**

**14. `iron_gate` → sin cambios** (sin línea · Carnicería) — *el ejemplo de lo que hay que imitar*
- **Por qué se conserva:** una frase sin números («una lesión por partido no ocurre»), suceso atribuible en
  una jugada concreta, identidad racial, coste de oportunidad real (un slot raro), y **OBVIOUS** sin
  necesidad de que la interfaz explique nada. Pasa los diez puntos del test de §16. Es, junto con
  `sweeper_keeper` y los letales, el 10 % del catálogo que ya está bien.

---
---

# A. DIAGNÓSTICO

**Underleague tiene un sistema de construcción de personajes que no construye personajes.**

La ADR 0036 declaró la separación correcta —«el perk es una regla, el objeto es una estadística»— y los
datos la incumplen: **el 77 % de los perks son estadísticas** y solo el **4,9 %** puede cambiar lo que un
jugador intenta hacer. La causa es estructural y está medida: `Utility.cs`, la función que decide cada
acción de cada jugador en cada tick, **no consulta ningún modificador de perk**; y la única variable que
reparte la conducta —el array de multiplicadores por acción— la escriben **los rasgos, que vienen sorteados
con el jugador**, y ningún tipo de efecto puede tocarla.

De ahí salen las capas invertidas: **lo que no eliges define el comportamiento; lo que eliges, pagas e
inmovilizas para siempre define números.** El jugador sí toma una decisión real —**a quién y dónde pongo el
perk**, bien sostenida por la pantalla de Equipo y su aviso de perk encendido/apagado— pero es prácticamente
la única, y como ningún perk estorba a ningún otro (`elseEffects` vacío en los 61) ni tiene contrapartida, la
elección entre dos perks se resuelve ordenando por valor esperado.

El resultado no es un juego roto: las builds **se diferencian de verdad** (entradas ×7,5 entre extremos) y
el bucle funciona. Pero la diferencia la producen el reparto de casillas, la posición y los pocos efectos de
atributo y correa —**no el catálogo**—, y el jugador no tiene forma de atribuirla a sus decisiones: durante
el partido **no se muestra ni una sola activación de perk**. Se construye a ciegas y se gana sin saber por
qué.

Lo bueno existe y está identificado: la línea de **La Carnicería** (la única que suma una identidad
narrativa en vez de un número), los **cuatro letales** (la única regla que el motor rompe de verdad),
`iron_gate`, `mob_instigator` y `sweeper_keeper`. **Son nueve perks de sesenta y uno.** Esos nueve son la
plantilla de lo que debe ser el resto.

---

# B. LOS CINCO PROBLEMAS MÁS IMPORTANTES (por impacto)

### B1. La IA no puede ser tocada por un perk — *impacto: máximo; causa raíz de B2, B3 y B4*
**MEDIDO.** `Utility.cs` no lee `Odds`, `Modifiers` ni `_effects` en ninguna línea. El array
`_actionMultipliers` que decide la conducta se rellena **solo en el constructor**, desde los rasgos. Ningún
`EffectType` escribe en él. **Consecuencia:** el 77 % del catálogo es aritmética por construcción, no por
falta de imaginación de quien escribió los JSON. *Sin esto, nada de lo demás se puede arreglar con datos.*

### B2. El catálogo es un producto cartesiano, no un elenco — *impacto: alto*
**MEDIDO.** 8 ejes de condición × 6 canales de efecto = 61 ficheros para ~10 mecánicas y ~16 fantasías (3,8
ficheros por fantasía). **14 perks** dicen «×N entrada»; **10 acumuladores** tienen la misma curva con otro
canal; **44 %** del catálogo vive dentro de cuatro falsas decisiones cuya decisión real es la alineación,
que el jugador ya toma. Más ficheros no es más variedad: es más ruido en el pool de recompensas.

### B3. Nada de lo que hace un perk se ve mientras se juega — *impacto: alto, coste de arreglo bajo*
**MEDIDO.** Las activaciones viven en una lista aparte que solo lee la pantalla **de post-partido**;
`MatchScreen` no dibuja ninguna. **41 de 61 perks son SUBTLE o INVISIBLE.** Y los cuatro perks de más valor
del juego son de los menos reconocibles: hay **correlación inversa entre potencia y memorabilidad**. Un
sistema de builds que el jugador no puede leer en pantalla no enseña a construir.

### B4. No existe el coste, así que no existe la decisión — *impacto: alto*
**MEDIDO/DERIVADO.** `elseEffects` vacío en los 61; ningún perk tiene contrapartida; 30 de 34 objetos
tampoco. Dos perks **nunca** se estorban. La única anti-sinergia del juego es estructural (el maestro cierra
la línea opuesta, ADR 0051) y afecta a 4 perks. Con todas las opciones positivas y ninguna excluyente, la
estrategia óptima es **ordenar por valor esperado**, y el juego publica esos valores. La ADR 0088 resolvió
bien un problema real, pero al prohibir el malus eliminó de paso el **coste conductual**, que es otra cosa:
una identidad es hacer unas cosas **en lugar de** otras.

### B5. Hay puestos sin fantasía, y uno de ellos es el centro del campo — *impacto: medio-alto*
**MEDIDO.** Solo 3 perks declaran posición (los tres del portero) y la función `position()` no la usa ningún
dato. El centrocampista —el puesto con el reparto de acciones más rico— **no tiene un solo perk propio**. El
defensa y el delantero tienen ocho cada uno, pero monocanal: entrar y rematar. La pregunta «¿qué clase de
centrocampista quiero?» **no tiene respuesta en el catálogo**.

---

# C. QUÉ CONSERVAR

1. **El perk como dato, no como código** (RF-065, RT-031) y la **descripción generada desde el efecto**
   (RT-035). No se negocia y no es el problema: el problema es el efecto, no la forma de contarlo.
2. **`modifyLeash`.** El mejor efecto del sistema: mueve una precondición dura y produce conducta visible.
3. **La línea de La Carnicería completa** y los **cuatro letales**. Es la única familia donde tres perks
   suman una identidad en vez de un número, y el flag `lethal` es la única regla que el motor rompe de
   verdad. Es la identidad del juego.
4. **`iron_gate`, `mob_instigator`, `sweeper_keeper`.** Los tres pasan el test de §16 y son el patrón.
5. **La estructura de líneas y maestros (ADR 0051):** exigir dos de la línea y **cerrar la opuesta** es la
   única decisión irreversible con coste que hay hoy. Hay que apoyarse más en ella, no menos.
6. **La previsión en la pantalla de Equipo** (`LineupPerkPreviewer` + el aviso flotante al mover a un
   jugador). Es la mejor pieza de comunicación del sistema y hace previsible la decisión de colocación.
7. **La colocación como decisión central.** El revisor ya la nombró en la ADR 0088. Es correcta: lo que
   falta es que el perk aporte algo más **además** de la colocación.

---

# D. QUÉ ELIMINAR

1. **Los cinco perks muertos medidos:** `back_to_back` (0 % en 21 partidos), `home_ref` (1,4 %, valor −1),
   `silky_veteran`, `crowd_control`, `wing_overlap`. Rediseñar (§18) o borrar; mantenerlos como están es
   ensuciar el pool de recompensas.
2. **Las condiciones opacas como clase.** `nearAlly`/`nearOpponent` con radio en el tick y `stat()` sin
   contador visible producen, sin excepción, perks de valor ≈ 0. Si no se pueden hacer previsibles, deben
   salir del lenguaje de condiciones (hoy hay además **8 funciones NCalc que no usa ningún dato**).
3. **Las variantes numéricas de una misma frase:** 6 de los 14 «×N entrada», 4 de los 5 «×N tiro», 7 de los
   10 acumuladores. Son 17 ficheros que no aportan ninguna decisión.
4. **`modifyAttribute` plano de +10.** Es un objeto disfrazado de perk, y encima en los atributos que menos
   pesan. O se condiciona, o se hace crecer con la run, o se va a `/data/items`.
5. **Los contadores «por partido jugado por el equipo».** Diez perks suben solos, sin decisión y sin
   visibilidad. La escalada tiene que ganarse.
6. **`ImmunityKind.Mourning` o su promesa.** RF-104 no está implementado y la descripción generada de `numb`
   promete algo que el motor no hace: eso choca de frente con RT-035 y con la regla 11. **O se implementa el
   luto, o se retira del texto.** No puede quedarse como está.

---

# E. QUÉ REDISEÑAR

| qué | de | a |
|---|---|---|
| **El canal de efecto principal** | `modifyProbability` (62,7 %) | `modifyUtility` (§14.3), con techo del 40 % para `modifyProbability` |
| **El acumulador** | «por cada partido del equipo», invisible | por cada **cicatriz, muerte o partido de este jugador**, con el contador en el retrato |
| **La condición** | 8 ejes libres cruzados con 6 canales | parte de la categoría (§15); máximo una condición, previsible al alinear |
| **El atributo** | +10 plano de fuerza/resistencia | condicionado o creciente, y en **técnica/velocidad**, que son los que mueven la decisión |
| **El vínculo** | 6 perks que multiplican la tirada del vecino | 2 perks que hacen que **dos jugadores actúen juntos** |
| **El centrocampista** | 0 perks propios | 6-8 de Instinto: el que suelta al primer toque, el que conduce, el que persigue, el que dispara de lejos |
| **La activación** | lista en el informe de post-partido | **evento en el flujo**, para que la pantalla de partido pueda atribuirla (RT-014 intacto) |
| **El texto** | «multiplica por 3 sus opciones de robar el balón» | una frase de conducta generada desde el efecto conductual: «no sale de su tercio» |

---

# F. NUEVA FILOSOFÍA (6 principios)

1. **Un perk cambia lo que un jugador INTENTA, no cómo le salen los dados.** Los dados son el oficio, y el
   oficio es como mucho un tercio del catálogo.
2. **Si no se puede señalar en una jugada, no es un perk.** Debe existir un momento del partido tras el cual
   el jugador diga «eso lo ha hecho el perk». Si hace falta agregar cien partidos, es un número.
3. **Toda identidad tiene precio, y el precio es conductual, no numérico.** Hacer una cosa más significa
   hacer otra menos. Eso **no** es un perk negativo: el equipo no pierde capacidad, el jugador gana carácter.
4. **La decisión se toma antes del partido y se comprueba durante.** La condición debe resolverse en la
   pantalla de Equipo; lo que no es previsible no es una decisión, es una lotería (regla 11 de `CLAUDE.md`).
5. **Un concepto, un perk.** Dos perks que se distinguen solo por una magnitud o por qué casilla los
   enciende son el mismo perk escrito dos veces, y el segundo roba sitio en el pool.
6. **La carnicería es la identidad, y el catálogo tiene que notarse en la carne.** Lo que acumula, acumula
   cicatrices; lo que escala, escala con muertos; lo que arriesga, arriesga plantilla. Es lo que el juego
   dice ser, y hoy lo cumplen 4 perks de 61.

---

# G. NUEVA ARQUITECTURA CONCEPTUAL

**Cuatro canales de efecto, en orden de prioridad de diseño:**

```
1. INTENCIÓN   modifyUtility(acción, %)      -> MatchPlayer._actionMultipliers  [NUEVO, gemelo de AddAttributeDelta]
               modifyLeash(celdas)           -> zona de acción                  [YA EXISTE, conservar]
2. OBJETIVO    preferTarget(criterio)        -> a quién entra / a quién pasa    [NUEVO, el más caro; es el canal de la carnicería]
3. OFICIO      modifyProbability(canal, %)   -> la tirada                       [YA EXISTE, techo 40 % del catálogo]
4. REGLA       lethal / cancelEvent /        -> excepciones duras               [YA EXISTE, conservar entero]
               immunity / modifyBias
```

**Tres reglas de arquitectura que acompañan a los canales:**

- **La activación es un evento.** `PerkTriggered(perkId, ownerId, tick)` entra en el flujo de eventos que
  consume el render. `/Sim` sigue sin decidir nada de presentación (RT-014); simplemente deja de esconder lo
  que ya calcula. **Es la pieza más barata y la de mayor efecto sobre la percepción.**
- **Techos declarativos en el cargador.** Igual que la rareza acota las cuotas (ADR 0058), `modifyUtility`
  necesita un techo por acción **y por posición** validado al cargar, con error explícito (RT-032). Un ×3 a
  `Shoot` en un portero debe ser un error de datos, no una anécdota.
- **El contador es una propiedad del jugador, no del perk.** Cicatrices, partidos, compañeros muertos: los
  cuenta el jugador, los enseña el retrato, y los perks los **leen**. Eso resuelve de un golpe la
  invisibilidad de los diez acumuladores y conecta el catálogo con el recurso central de la run.

**Lo que NO cambia:** determinismo (todo entero, orden de suscripción ya determinista, sin E/S), la frontera
`/Sim`↔`/Game`, la generación de descripciones desde el efecto, y la validación de `/data` por esquema.

---

# H. NUEVO CATÁLOGO PROPUESTO

**32 obtenibles + 5 raciales = 37 ficheros** (hoy: 56 + 5 = 61). Reparto por categoría de §15:

| categoría | nº | % | reparto por línea |
|---|---|---|---|
| **INSTINTO** | 11 | 34 % | 2 por línea + 3 sin línea (portero, centro del campo) |
| **OFICIO** | 10 | 31 % | 2 por línea + 2 sin línea |
| **CARNICERÍA** | 5 | 16 % | los 4 letales + `iron_gate` |
| **HERENCIA** | 4 | 13 % | 1 por línea, con contador visible ligado al desgaste |
| **PACTO** | 2 | 6 % | 2 vínculos que hacen actuar a dos jugadores juntos |

**Por línea (4 × 7 = 28) + 4 sin línea:**

| línea | maestro | Instinto | Oficio | Herencia / Pacto |
|---|---|---|---|---|
| **La Muralla** | `granite_line` (conservar) | «Ancla», «Perro de presa» | parada, robo | «Presión alta» (Pacto) |
| **El Toque** | `first_touch_school` (conservar) | «Primer toque», «Conductor» | pase, evasión | «Espalda con espalda» (Pacto) |
| **La Puntería** | `killing_range` (reducir a uno) | «No vuelve», «Amenaza de larga distancia» | tiro a puerta, definición | acumulador de goles **visible** |
| **La Carnicería** | `blood_tithe` (conservar) | «Olfato de sangre», «Instigador» | lesionar | «Veterano de cicatrices» |
| **Sin línea (4)** | — | `sweeper_keeper` | — | `iron_gate`, `skullsplitter`, `second_wound` |

**Cobertura por puesto** (el criterio que hoy falla): portero 3 · defensa 8 · **centrocampista 7** ·
delantero 7 · transversales 7. Ningún puesto por debajo de 3, y el centro del campo deja de estar vacío.

---

# I. PERKS EJEMPLO

Los catorce de **§18**, con su formato completo (perk actual → problema → fantasía → comportamiento →
posición → complejidad → ejemplo de partido). Resumen de la lista:

| # | nuevo nombre | sustituye a | categoría | puesto | lo que se ve |
|---|---|---|---|---|---|
| 1 | Espalda con espalda | `back_to_back` | Pacto | defensa | dos centrales clavados en la línea |
| 2 | Perro de presa | `crowd_control` | Instinto | centro/defensa | persigue a un solo rival todo el partido |
| 3 | Portero líbero | `sweeper_keeper` (ampliado) | Instinto | portero | el portero sale del área — y a veces la deja vacía |
| 4 | No vuelve | `poacher_instinct` | Instinto | delantero | un jugador parado en el área rival mientras los demás defienden |
| 5 | Amenaza de larga distancia | `long_range_menace` | Instinto | centro | arma la pierna desde nueve casillas |
| 6 | Presión alta | `high_press_trigger` | Pacto | equipo | cinco jugadores saltan a la vez al perder el balón |
| 7 | Olfato de sangre | `shadow_marker` | Instinto | defensa/centro | va derecho al rival que cojea |
| 8 | Ancla | **5 perks de entrada** | Instinto | defensa | vuelve corriendo a su tercio como si tiraran de una cuerda |
| 9 | Primer toque | `steady_hands` + `fine_touch` | Instinto | centro | el balón no se le para en los pies |
| 10 | Veterano de cicatrices | `scar_veteran` | Herencia | cualquiera | cada lesión superada lo vuelve más bestia, y se ve el contador |
| 11 | Instigador | `mob_instigator` | Carnicería | cualquiera | cuando el árbitro se va, tres de los nuestros se lanzan |
| 12 | Pulmones de hierro | `iron_lungs` | Herencia | centro | el último que sigue corriendo |
| 13 | Cara de inocente | `home_ref` (y RF-064f) | Carnicería | el que más entra | la barra de criterio baja la mitad de lo que debería |
| 14 | Puerta de hierro | `iron_gate` (**sin cambios**) | Carnicería | enano | una lesión que no ocurre |

---

# J. PLAN DE VALIDACIÓN

Ordenado por dependencia. Nada de esto es una decisión tomada: cada paso 3 en adelante **exige un ADR**
(RT-057) y los que tocan una regla de juego, **decisión del revisor**.

**J.0 — Arreglar el instrumento antes de medir nada.** *(prerrequisito, coste nulo en diseño)*
`Balance/BuildBatchRunner.cs` no pasa el catálogo de objetos, así que **16 de 49 builds no se pueden medir**,
incluidas las cinco referencias neutras. Sin eso no hay línea base contra la que comparar ningún rediseño.

**J.1 — Línea base de conducta, no de victoria.** Lote de `/Balance` sobre las builds coherentes actuales,
registrando el **perfil de acciones por partido** (entradas, tiros, pases, regates, presiones, repliegues,
posesión por tercio) además de la tasa de victoria. Es la métrica que este informe necesita y que hoy no
existe: **la distancia entre perfiles**, no la distancia entre resultados.

**J.2 — Experimento decisivo y barato: ¿dónde vive hoy la diferenciación?** Sustituir **todos** los
`modifyProbability` por su media y volver a medir el perfil de acciones. Si las builds siguen igual de
distintas, queda demostrado que el 62,7 % del catálogo **no aporta diferenciación**, y la reducción de §17 se
vuelve una decisión sin riesgo. Es el experimento R3 que el informe sistémico dejó pendiente y es el que más
información da por lote.

**J.3 — Prueba ciega de reconocimiento.** *(el test que ningún número sustituye)* Se le enseñan al revisor
tres repeticiones de partido sin la alineación y se le pide señalar al portador del perk. **Criterio:** un
perk de categoría INSTINTO que no se pueda señalar en un partido **no pasa** y se rehace. Es el único test
que mide lo que este informe pregunta.

**J.4 — Prototipo de `modifyUtility` con dos perks y nada más.** «Ancla» y «No vuelve», sobre la build de
referencia. Métricas de corte: `injuriesPerMatch` (RT-056, ya en el techo), `ballThirdMaxShare` (≤ 52, ADR
0093), cadena de pases (ADR 0111), tiros (ADR 0109) y `runWinRate` (22-27 según ADR 0095/0099). **Si dos
perks conductuales sacan de banda tres de esas cinco, el canal necesita techo por posición antes de
ampliarlo, no menos ambición.**

**J.5 — Reducción del catálogo por fases, midiendo el pool.** Primero los 11 a eliminar (§17), luego las
fusiones. Métrica: `noDeadPerks` en verde y **activación ≥ 25 %** en al menos una build coherente para
**todos** los perks supervivientes. Un perk que no llega al 25 % en la build que lo quiere es un perk que
sigue sin existir.

**J.6 — Lo que hay que decidir antes de escribir código** *(preguntas al revisor, no propuestas cerradas)*:
1. ¿Un multiplicador de utilidad **por debajo de 100** en otra acción es «un perk negativo» a efectos de la
   ADR 0088? De esa respuesta depende si el sistema puede tener identidades o solo mejoras.
2. RF-104 (el luto): ¿se implementa o se retira del texto de `numb`? Hoy la descripción generada promete
   algo que el motor no hace.
3. ¿Se acepta emitir la activación de perk como evento para que la pantalla de partido pueda atribuirla?
   Es barato, no toca ninguna regla, y sin ello **ningún** rediseño se nota.
