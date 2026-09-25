# ADR 0148 — El enfriamiento de entrada es disputa, no física

Fecha: 25 sep 2026 · Estado: **implementada y medida en dos semillas**
Enmienda la **ADR 0147** aplicando su propio principio. Ficha del síntoma: la violencia que esa ADR publicó
rota a propósito (`docs/project-state.md`).

## Por qué

La ADR 0147 (el balón parado como fase posicional) dejó el partido **un 68 % más violento** y lo publicó
anotado: faltas 4,73 → **7,96**, entradas sin balón 2,45 → **4,62**, `injuriesPerMatch` 0,55 → **0,82
contra un techo de 0,90**. El dial no se había encontrado, y la única hipótesis que se midió —la compresión
del saque de centro— salió **REJECTED**: con `kickoffPushCells` en 1,5 la violencia *sube*.

La ADR 0147 enunció el principio que ordena esto:

> *lo físico sigue el reloj de pared, lo que es disputa sigue el reloj del partido*

y lo aplicó **bien a la energía** —uno recupera el resuello durante una parada, por eso los equipos pierden
tiempo— pero clasificó los **enfriamientos de entrada** como físicos, así que corren con el tick del motor.

**No son físicos.** Un enfriamiento de entrada no mide cuánto tarda un cuerpo en recuperarse: mide **cuánto
tardas en poder volver a disputar**. Durante una reanudación no hay disputa posible, así que recargarlo ahí
es **regalar entradas**. Y no es marginal: **el 24,3 % del partido es pausa** y
`OffBallTackleCooldownTicks` son 400 ticks, 26,7 segundos. Se recargaba entero gratis.

**Que la ADR 0147 no argumentó.** Conviene decirlo porque cambia el peso de esta enmienda: aquella ADR
afirmó *«un enfriamiento de entrada es físico»* **sin justificarlo**, dos veces (su sección de
consecuencias y `docs/plan-balon-parado-posicional.md:234`). Esta ADR no tumba un argumento: pone uno
donde no había ninguno.

## Qué se cambia

`TickBody` recibe `tackleCooldowns`, y `TackleCooldown` y `OffBallTackleCooldown` solo decrementan cuando
es cierto. El bucle de jugadores lo pasa desde **`wasRestarting`**, que es `_restartTicksLeft > 0`
**fotografiado antes del bucle**.

**La condición no es nueva, y eso importa**: `_restartTicksLeft == 0` es exactamente con la que el reloj
del partido decide si corre (BC-A), y su comentario ya dice *«el reloj del partido sólo corre con el balón
en juego»*, que es literalmente lo que esta ADR quiere. Y la **fotografía** tampoco es nueva: el bucle ya
la tomaba, con su motivo escrito —una falta pitada a mitad de tick llama a `BeginRestart` y cambiaría la
respuesta para los jugadores que vienen detrás—.

**Nada más.** En particular:

- **La energía sigue corriendo en la pausa**, a propósito. Eso sí es físico, y congelarla fue el defecto
  que la revisión independiente encontró en la ADR 0147 (cruzar el campo andando tras un gol no costaba
  nada). Esta ADR no lo reabre.
- Los otros enfriamientos (`DribbleDuelCooldown`, `BlockCooldown`, `AerialCooldown`) **no se tocan**: la
  hipótesis medida era la de la entrada y no se cambian tres cosas a la vez.

## Medido: dos semillas, 2.000 partidos cada una, conjunto de referencia

Línea base tomada **en el mismo árbol**, no de memoria.

| | semilla 1 | | semilla 7 | |
|---|---|---|---|---|
| | base | con el cambio | base | con el cambio |
| `foulsPerMatch` | 7,96 | **7,09** (−10,9 %) | 6,05 | **5,56** (−8,1 %) |
| `offBallTacklesPerMatch` | 4,62 | **3,80** (−17,7 %) | 3,57 | **3,12** (−12,6 %) |
| `tacklesPerMatch` | 8,79 | 8,38 | 10,13 | 9,69 |
| `injuriesPerMatch` | 0,82 | **0,78** | 0,84 | **0,79** |
| `shotsPerMatch` | 8,95 | 8,90 | 6,98 **ya OUT** | 6,87 (sigue OUT, heredada) |
| `possessionChanges` | 24,79 | 24,79 | 25,43 | 25,21 |
| `passChainAvgLength` | 2,02 | 2,01 | 2,19 | 2,18 |

**Replica en dirección y orden de magnitud en las dos semillas. CONFIRMED.**

**El fútbol no se mueve**: tiros, cambios de posesión y cadena de pases quedan donde estaban.

**Las lesiones se alejan del techo** en las dos semillas (0,82 → 0,77 y 0,84 → 0,73), que era lo que hacía
urgente el asunto.

## Las 43 puertas: 4 rojas contra las 6 de la base

La foto **completa**, no sólo las que encajan con el relato — la primera versión de esta ADR enumeró dos
métricas de jefe cuando eran tres y omitió la peor, y la revisión independiente lo marcó:

| métrica roja | base (25 sep, mañana) | con el cambio | |
|---|---|---|---|
| `bossGate_grimhold_guns_good` | 72,27 | 71,74 | empeora 0,53 |
| `bossGate_eternal_crown_excellent` | 43,57 | 42,44 | empeora 1,13, **heredada** |
| `coherentBuildsBeatNone_orc_violence` | 57,24 | **55,60** | **empeora 1,64** |
| `RaceBalanceTests` · `undead_none` | 60,45 | 60,33 | mejora, heredada |

**Salen de rojo dos**: `badBuildsLoseToNone_elf_out_of_zone` (45,42 → en banda) y
**`FullRunGateTests…NeverAllOfThem`**, que la ADR 0147 dejó rota (0 → 0,186 contra un RF-114k que dice que
nunca caben los cuatro sumideros).

**Pero ese segundo arreglo hay que leerlo con cuidado**, y lo avisó la revisión independiente: midió que
antes del cambio ganaban **8 de 16** semillas de run y después **12 de 16**. La puerta puede estar volviendo
a verde por la vía *«menos violencia → menos bajas → el jugador gana más»*, que es el **mismo mecanismo
LIKELY** que la ADR 0147 anotó al romperla. **Arreglar una puerta haciendo la run más fácil no es arreglar
la puerta.** Queda como **LIKELY**, no como mérito.

### `orc_violence` cae 1,64, y se anota sin dramatizarlo

`coherentBuildsBeatNone_orc_violence` pasa de 57,24 a **55,60**. Es la build de **contacto** y el mecanismo
de esta ADR le quita entradas disponibles, así que la dirección es la esperada.

**Pero no se convierte en un argumento de diseño, por decisión del revisor (25 sep):** *«no te vuelvas loco
con builds concretas; los perks y objetos no están cerrados todavía, no son definitivos»*. Una build es una
combinación de un catálogo **provisional**: medir la identidad del juego contra ella hoy es medir el árbol
equivocado, que es el mismo error que esta sesión ya cometió al comparar líneas base de distintos árboles.

Se registra por una sola razón: que cuando el catálogo se cierre y alguien retome `orc_violence`, sepa que
esta ADR le costó 1,64 y no lo busque en otro sitio. Las puertas de winrate siguen **aparcadas**.

## Lo que NO arregla, y hay que decirlo

Recupera **1,02 de los 3,23 puntos de falta** que subieron con la ADR 0147: **un tercio de la brecha**. El
resto sigue sin explicación medida. Es el primer mecanismo que alguien aísla, no el último que hace falta.

**Y un aviso sobre el nivel absoluto**: la base varía mucho entre semillas —7,96 en la 1 y **6,05** en la
7—, casi dos puntos. Comparar el 7,96 de hoy con el 4,73 de antes de la ADR 0147 asumiendo la misma semilla
es exactamente el error que este proyecto ya ha pagado tres veces. Lo que replica es el **efecto relativo**.

## La primera versión de esta ADR estaba mal, y la revisión independiente la tumbó

Se envió a revisión con la condición `_phase == MatchPhase.OpenPlay`, y **era incorrecta por dos motivos a
la vez**:

1. **`OpenPlay` no significa «balón en juego».** Deja fuera **`MobGoldenGoal`** —la turba— que es juego
   real (`_phase = _goldenGoal ? MobGoldenGoal : OpenPlay`) y son el **9,25 %** de los fotogramas. Con
   `OffBallTackleCooldownTicks` en 400, cada jugador se habría quedado con **una sola entrada para toda la
   prórroga**, convirtiendo la turba en el tramo **menos** violento del partido. Eso ataca **RF-055d** de
   frente: la turba es la ventana de las builds de violencia, que es donde vive la identidad del juego.
2. **Inventaba una tercera forma de preguntar si el balón está en juego**, cuando el repositorio ya tenía
   `_restartTicksLeft == 0` y `wasRestarting`. Es la pregunta 7 de `CLAUDE.md` —*¿existe ya una convención
   del propio repositorio para esto?*— incumplida.

Y además **el número que justificaba la ADR tenía el mismo error dentro**: el «30 % del partido es pausa»
salía de contar `phase != OpenPlay`, o sea metiendo la turba en la cuenta. La pausa de verdad es
**24,28 %**. El error del argumento y el del código eran el mismo, que es lo que lo convierte en una
confusión conceptual y no en una errata.

**Lo que se evaporó al corregirlo**: partiendo los 2.000 partidos por si entraron en turba, la revisión
midió que **~2/3 de la mejora de lesiones** venía del congelado indebido de la turba, no del mecanismo que
esta ADR defiende. Con la condición correcta, las lesiones bajan a 0,78 / 0,79 en vez de 0,77 / 0,73. El
pedazo que **sí** aguanta —y era el grande— son las **faltas**: de su caída, sólo ~11 % venía de la turba.

**Una versión tenía dependencia de orden y ahora no.** Leer `_phase` en vivo dentro del bucle de jugadores
significaba que una falta pitada a mitad de tick cambiaba la respuesta para los jugadores posteriores. Es
determinista (orden fijo, RT-024 pasaba) pero es una asimetría por índice, justo lo que RT-041/RT-097
existen para evitar. Con `wasRestarting` fotografiado, desaparece.

## Descartado por el camino

**`Shielding` no es el canal DOMINANTE de la violencia. REJECTED**, con el matiz que la revisión
independiente puso y es justo: con n=200 el intervalo de confianza de r es ≈ ±0,14, así que lo que se
descarta es que sea el canal principal, no que contribuya. Y el límite no es de sesgo sino de **potencia**:
el conjunto del censo tiene menos varianza de faltas que el de `/Balance`. Era el sospechoso con nombre —gemelo exacto de
`Dribbling` desde la ADR 0137, misma dosis (`ShieldingTicks` 12), y sin instrumentar—. Se instrumentó
(`Sim.Tests/Balance/CarrierCensusTests.cs`) y la correlación por partido entre ticks protegiendo y faltas
es **0,008** con n=200 (ET ≈ 0,071): **0,1 errores típicos**, cero. Sí correlaciona con las **entradas**
(0,253, 3,6 σ), pero esas entradas no se convierten en faltas.

## Lo que esta ADR deja abierto a propósito, con ficha

Está en [BJ-A](../pendientes/BJ-A.md):

1. **Los otros tres enfriamientos.** Por el principio que esta ADR enuncia, `DribbleDuelCooldown` (un duelo
   de regate **es** disputa), `BlockCooldown` y `AerialCooldown` deberían seguir el mismo reloj, y siguen
   corriendo en la pausa. Aquí no se tocan porque **no se cambian cuatro cosas a la vez y luego se
   atribuye el efecto a una**, que es la disciplina de `balance-measure`. Pero la forma es indistinguible
   de haber aplicado el principio justo donde bajaba el número, así que queda ficha.
2. **Congelar frente a reducir.** La nota de diseño planteaba como salida (a) una **tasa reducida**
   (`deadBallRecoveryPercent`) y decía que era la que más convencía. Esta ADR implementa todo-o-nada y
   **nunca midió la graduada**. No están comparadas.
3. **La regla es invisible para el jugador.** Un enfriamiento que corre jugando y se para en una
   reanudación no se ve, no se lee en ningún sitio y no se puede planificar — y el proyecto ordena
   *comportamiento observable > modificadores numéricos invisibles*. Esta ADR no se pregunta si el jugador
   debería enterarse; la ficha sí.
4. **Una asimetría nueva entre dos ADR**: en una pausa larga la energía recarga (ADR 0142/0147) y el
   enfriamiento de entrada no, así que un jugador vuelve **descansado pero incapaz de entrar**. No está
   decidido si es deseable.

## Hallazgos del censo que quedan abiertos y no son de esta ADR

1. **El portador se pasa el 78 % de sus ticks armando un pase** (`Passing`), contra 9,35 % conduciendo,
   6,75 % protegiendo y 5,86 % disparando. Eso —y no `Shielding`— es lo que explica que se conduzca poco,
   que era el hallazgo sin dueño que dejó [BI-D](../pendientes/BI-D.md).
2. **Proteger dura 50,51 ticks de media con `ShieldingTicks` en 12**: más de cuatro veces su propio
   contador, 3,4 segundos seguidos, con el 84 % terminando *con* el balón. Se reencadena sin freno — justo
   el reencadenamiento que la conducción perdió. Son pocas (0,7 por partido) pero larguísimas.
3. **El censo mide sobre un conjunto distinto al de `/Balance`**: `TestMatches.Reference` genera dos equipos
   de calidad 50 y da 3,91 faltas / 11,77 entradas, mientras `data/balance/reference.json` da 7,96 / 8,79.
   Los niveles absolutos de un arnés no son comparables con los del otro; las correlaciones internas sí.
