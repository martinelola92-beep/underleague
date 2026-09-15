# Decisión sobre los vínculos (RF-100..106)

**Fecha:** 15 sep 2026 · **Encargo:** consultoría de diseño de sistemas, solo lectura sobre el repo.
**Entrada:** el hallazgo ya verificado de que RF-100..106 está declarado, serializado y tarifado y no
ocurre nunca (`docs/analisis/tres-puntos-resueltos.md`, `docs/analisis/perks-design-bible.md` C12).
**Veredicto:** **B — retirar RF-100..106 del alcance de lanzamiento**, con una sustitución barata para no
perder la historia. El razonamiento está abajo; las cifras llevan etiqueta.

Etiquetas: **MEASURED** = medido en este encargo o leído del código · **DERIVED** = aritmética sobre lo
medido · **HYPOTHESIS** = juicio de diseño sin medida que lo respalde.

Mediciones propias de este encargo:
- `/Balance --full-runs 200 --seed 1` (600 runs: 200 × 3 doctrinas), `runs.csv`.
- Arnés de solo lectura fuera del repo (scratchpad, `ProjectReference` a `/Sim`, sin tocar nada):
  200 secuencias de 14 partidos consecutivos con **la misma alineación**, Human_50 contra Human_50,
  contando sobre `MatchResult.Events` los candidatos a vínculo de RF-102. 2.800 partidos.

---

## 1. Qué hace hoy el desgaste, y qué hueco deja

### 1.1 Lo medido

**MEASURED** (600 runs, seed 1):

| | |
|---|---|
| Partidos por run | **13,38** (13,94 la doctrina que compra) |
| Muertes por run | **1,56** (`deathsPerRun` 1,52, en banda 1,5-3) |
| Lesiones graves por run | **2,57** |
| Lesiones propias (todas) por run | **5,79** |
| Runs con ≥1 muerte | **53,3 %** |
| Runs con ≥1 baja permanente (muerte o grave) | **77,2 %** |
| Plantilla al cerrar / disponibles | **10,17 / 9,26** (techo 12, mínimo jugable 5) |
| Nivel medio al cerrar | **~6,1** |
| Fin de run por jefe perdido | **81,5 %**; por quedarse sin plantilla: **2,7 %** |
| Oro por ventas de jugador por run | **0,04** |

### 1.2 Qué cuesta hoy perder a un jugador

Leído en el código, no supuesto:

1. **El nivel invertido.** Nivel medio ~6 (**MEASURED**); la experiencia se reparte por partido jugado
   (`MatchResolution.ApplyProgression`, RF-025) y no se hereda.
2. **El equipo vuelve al inventario**, no se entierra (`MatchResolution.cs`, ADR 0048 condición 4). Es la
   mitad recuperable, deliberada.
3. **La sustitución forzada** es parte del estado inicial del partido siguiente (ADR 0094).
4. **Puede terminar la run en pleno partido** si bajas de 5 disponibles (`RunRules.MinimumAvailablePlayers`,
   RF-002b). Ocurre en el 2,7 % de las runs (**MEASURED**).
5. **Vale 0 de oro muerto** (`playerSaleStatePercent.dead = 0`, ADR 0108) y **25 %** con lesión grave.
6. **La clínica es el sumidero** que el desgaste alimenta (ADR 0099).

El desgaste, por tanto, **sí muerde** —el problema que la ADR 0045 diagnosticó está cerrado— y muerde en
cuatro monedas a la vez: nivel, oro, hueco de plantilla y riesgo de fin de run.

### 1.3 El hueco real

El hueco no es «perder duele poco». Es que **el desgaste es fungible**. Un jugador es hoy un vector de
atributos, un nivel y un slot; perder al #3 y perder al #5 se diferencian por un número y por nada más.
La run no guarda **quién** era. Esa es la única grieta por la que un vínculo podría entrar: convertir a un
jugador en **no intercambiable**.

El problema, y es el eje de todo lo que sigue: **RF-100..106 no produce no-intercambiabilidad de
decisión, solo de coste.** El vínculo se forma solo, no se elige, y lo único que hace —lo único que los
requisitos escriben— es que perder a ese jugador cueste más. Eso es exactamente lo que el listón del
encargo excluye: «perder a alguien duele más» no basta, porque ya duele.

### 1.4 El detalle que decide el caso: el vínculo no tiene mitad positiva escrita

**MEASURED** sobre `docs/requisitos.md` §3.11 completa. RF-102 se titula *«Tipos mínimos de vínculo
positivo»* y **enumera únicamente las condiciones de formación de los tres tipos. No especifica ningún
efecto**. Ni bonificación de atributo, ni de acción conjunta, ni de moral. En todo el documento, las
únicas consecuencias mecánicas de tener un vínculo son:

- **RF-104**: el duelo, que es una **penalización**.
- `playerSalePerBond: 1` en `data/economy/economy.json`: **1 de oro** por vínculo al vender, sobre una base
  de 4/9/18/36 por rareza. Es decir, ≤2 de oro por jugador.
- RF-105 y RF-106: presentación.

Un sistema cuyo único efecto definido es un castigo, colocado encima de un recurso que ya castiga, no es
una mecánica: es un impuesto. La familia positiva de RF-102 es **un nombre sin número**, y quien la
implemente tendrá que **inventar el diseño entero**, no ejecutarlo.

---

## 2. Inventario de eventos reales que podrían formar un vínculo

El flujo de eventos existe y es rico: `Sim/Events/EventType.cs` tiene 24 tipos, y `MatchEvent` lleva
`Actor`, `Target` y `Opponent`, así que la mayoría de las parejas son reconstruibles. En particular
`Emit(EventType.Goal, …, shooter, assistant)` (`MatchEngine.ScoreGoal`) **sí publica la pareja
asistente→goleador**: la Sociedad de RF-102 es técnicamente medible hoy sin tocar el motor.

### 2.1 Los tres candidatos de RF-102

**MEASURED**, 200 secuencias × 14 partidos, alineación fija (cota **superior**: una run real rota):

| Candidato (RF-102) | Frecuencia medida | ¿Verificable? | ¿Causable por el jugador? |
|---|---|---|---|
| **Sociedad** — 3 asistencias de A a gol de B | **1,73 parejas por run**; 91,5 % de las runs forman al menos una. Goles asistidos: **0,80 por partido** | Sí, directamente del evento `GOAL` con `Target` | Solo indirectamente (alinear juntos) |
| **Muro** — 2 partidos consecutivos sin encajar | Portería a cero **32,8 %** de los partidos; **68,5 %** de las secuencias de 14 tienen dos seguidas. Con la cláusula «misma pareja defensiva» baja, pero sigue siendo frecuente | Sí | Sí, alineando la misma pareja |
| **Deuda de sangre** — A hace falta al que dio la entrada dura a B | **0,395 por run** (39,5 % de las runs verían una), con definición generosa: cualquier falta nuestra sobre el agresor, en ventana de 10 s, cometida por alguien que no sea la víctima. Faltas duras del rival sobre nosotros: **3,91 por partido** | Sí | **No.** `grep -rn "revenge\|retaliat"` sobre `/Sim`: **cero coincidencias**. La IA de utilidad no tiene acción de represalia: la coincidencia existe, la intención no |

### 2.2 El dato que rompe el calendario

**MEASURED**: la primera Sociedad se forma, de media, en el **partido 8,96 de 14** (mediana 9). Sumando
todas las parejas, un vínculo de Sociedad está vivo **8,72 partidos-pareja por run**, es decir cada
vínculo vive **~5 partidos**.

**DERIVED**: el umbral de 3 asistencias está calibrado para una temporada, no para una run de 13,4
partidos. Se pagaría el esquema de guardado, la UI de líneas entre retratos, el highlight ilustrado, la
l10n y el balance **para un sistema que solo existe en el último tercio de la partida**.

### 2.3 El tercer disparador de RF-104 está muerto

**MEASURED**: la venta de jugadores aporta **0,04 de oro por run**. La política automática no vende
prácticamente nunca. «Se vende» como disparador del duelo es, bajo la política medida, inerte. (Un humano
venderá más que el autómata — **HYPOTHESIS** — pero no lo suficiente para sostener el sistema.)

### 2.4 Nota de nomenclatura, porque induce a error

En el repositorio conviven **dos cosas distintas llamadas vínculo**:

- Los **vínculos direccionales de alineación** (ADR 0021, `docs/fase1b-diseno.md` §2.4, `linked(owner,
  'beside')`, `IPerkLinks`): geometría resuelta **antes** del partido. **Existen, están implementados y
  funcionan.**
- Los **vínculos persistentes de run** (`RunBond`, RF-100..106): **no existen**.

La familia Pacto del rediseño de perks usa **los primeros** (`docs/analisis/perks-auditoria-diseno.md`
§5: *«con quién juega: la alineación … previsible en la pantalla de Equipo»*; `perks-design-bible.md` C12
lo dice explícitamente). **No es que no deba usarse como justificación: es que no es una dependencia.**
Retirar RF-100..106 no toca ni un perk del catálogo propuesto.

---

## 3. El problema de previsibilidad del duelo

La regla 11 de `CLAUDE.md` (RF-012d) dejó de apoyarse en una garantía y pasa a apoyarse en las **cinco
condiciones de la ADR 0048**. El duelo se prueba contra ellas:

| Condición ADR 0048 | ¿La cumple el duelo? |
|---|---|
| 1. Se sabe antes de entrar | **Sí**, si RF-106 dibuja las líneas. Sabes quién enviuda si cae X. |
| 2. Se puede evitar el partido | **Parcialmente.** Evitas la muerte, luego evitas el duelo. Es la misma decisión, no una nueva. |
| 3. **Se puede reducir el riesgo con la alineación** | **No.** Y la ADR la llama *«la condición más importante de las cinco»*. Una vez muerto X, el duelo de Y y Z **no admite ninguna decisión**: no hay alineación, oro, consumible ni clínica que lo module. |
| 4. Se puede rehacer | **Parcialmente**: pasa solo, en 3 partidos. No hay nada que hacer, que es otra forma de decir que no hay decisión. |
| 5. Pesa pero no es ruido | Depende de la magnitud. Ver 3.2. |

### 3.1 Es un amplificador de espiral, no una fuente de tensión

**DERIVED.** El duelo tiene una asimetría estructural: **solo actúa cuando ya vas perdiendo**. En una run
limpia no aparece nunca; en una run que pierde a un titular, llega **justo después** de la pérdida, en el
momento en que la plantilla está más corta. Con el 81,5 % de las derrotas en un jefe (**MEASURED**), y
formándose los vínculos en el partido 9 de 14 (**MEASURED**), el duelo aterriza mayoritariamente en el
acto 3: penaliza la recta final de la run a la que peor le ha ido. Es el antipatrón clásico de la espiral
de derrota, y contradice el trabajo de la ADR 0052 (*la agencia está en la formación*): aquí no hay
formación que valga.

### 3.2 Cuánto pesaría, si se implementara tal cual

**DERIVED**, aritmética explícita: sucesos que disparan duelo por run = 1,56 muertes + 2,57 graves ≈ **4,1**
(las ventas son ~0). Si al final de la run entre 4 y 5 de los 7 titulares llevan vínculo (**DERIVED** de
1,73 parejas de Sociedad × 2 + ~0,7 de Muro × 2 sobre plantilla de 10), y cada vinculado tiene 1-1,3
compañeros, salen **~2-3 aplicaciones de duelo por run × 3 partidos = 6-9 partidos-jugador penalizados**,
sobre ~94 partidos-jugador titulares (13,4 × 7). Es decir **7-10 % del tiempo de juego titular bajo
penalización**, concentrado en el acto 3.

No es despreciable, y sale de un sistema que no ofrece ninguna decisión a cambio.

### 3.3 ¿Tiene solución de diseño?

Sí, hay **dos** que convertirían el duelo en decisión, y las dos son sistemas distintos del que RF-104
describe:

- **HYPOTHESIS A — el duelo se elige.** Al morir X, el vinculado Y **ofrece una elección**: jugar de luto
  (penalización, pero juega) o retirarse del once tres partidos (no penaliza, pero pierdes una pieza). Eso
  es una decisión real, tiene coste medible en las dos ramas y es exactamente la forma de las cartas de
  evento de la ADR 0100, que ya funciona.
- **HYPOTHESIS B — el duelo tiene reverso.** Luto *o* venganza: penalización general y bonificación contra
  **el club que lo mató**. Encaja con la identidad («carnicería administrada») y con el rival concreto,
  pero exige que la run recuerde quién mató a quién y que el mapa vuelva a cruzarse con él, cosa que hoy
  no está garantizada.

Ambas son **rediseños**, no implementaciones de RF-104. Si se van a diseñar dos sistemas nuevos, la
pregunta del encargo —¿justifica esto otra capa persistente?— se responde sola: no con estos requisitos.

---

## 4. Recomendación: **B**

**Retirar RF-100..106 del alcance de lanzamiento**, y sustituir la función narrativa por la vía barata que
ya existe (§6.3). El razonamiento, en orden de peso:

1. **No hay mitad positiva.** RF-102 nombra tres vínculos «positivos» y no define ni un efecto (§1.4). Lo
   único tarifado es el castigo y 1 de oro. Implementar A no es ejecutar un diseño: es **inventarlo**, y
   el encargo pide justificar la capa **antes** de diseñarla.
2. **Falla la condición 3 de la ADR 0048**, la que esa ADR declara más importante. El duelo no se puede
   reducir con la alineación porque no depende de la alineación.
3. **El calendario no da.** Vínculo medio formado en el partido 9 de 14 y vivo ~5 partidos (**MEASURED**).
   Se paga capa persistente, esquema, UI, highlight y l10n para el último tercio de la run.
4. **Un disparador es incausable y otro está muerto.** Deuda de sangre sin IA de represalia es azar
   disfrazado de narrativa, justo lo que RF-100 dice querer evitar; «se vende» aporta 0,04 de oro por run.
5. **Coste de oportunidad.** El trabajo abierto —el rediseño de perks (`perks-design-bible.md`), PD-1 y la
   escalera de fase 1 que hay que rehacer— toca la **decisión** del jugador. Los vínculos, tal como están
   escritos, no.
6. **C es la peor opción de las tres.** Dejarlo como está mantiene tres deudas activas: una descripción
   **generada** (RT-035) que promete inmunidad a algo imposible, un término de precio que multiplica por
   cero, y siete requisitos que el equipo lee como si estuvieran vivos. RT-057 («nunca un ajuste
   silencioso») aplica también al revés: **nunca una promesa silenciosa**.

Lo que **no** dice esta recomendación: que la idea sea mala. Un sistema de vínculos con mitad positiva
diseñada, con umbrales calibrados a 13 partidos y con el duelo convertido en elección (§3.3) puede ser
bueno. Dice que **RF-100..106, tal como está escrito, no lo es**, y que retirarlo ahora es más honesto y
más barato que arrastrarlo.

---

## 5. Si el revisor elige A: el listón mínimo

No es la recomendación, pero si se implementa, estos son los mínimos no negociables (todo **HYPOTHESIS**
salvo las frecuencias, que son **MEASURED**):

- **Umbrales recalibrados a la run real.** Sociedad a **2 asistencias**, no 3 (con 3 la mediana es el
  partido 9 de 14). Muro exige **misma pareja** y se mide sobre porterías a cero del 32,8 %. Deuda de
  sangre **se retira**: sin IA de represalia no es «concreto y verificable» en el sentido de RF-100, es
  ruido; su hueco lo puede ocupar «sobrevivir juntos a un partido con una muerte propia en campo», que sí
  es un suceso que el jugador entiende y que ocurre en el 53,3 % de las runs.
- **La mitad positiva se define y se mide antes que el duelo**, con el instrumento de valor emparejado de
  la ADR 0087 (control emparejado, `rowDeviation`), no a ojo.
- **El duelo es una elección, no un estado** (§3.3, hipótesis A): jugar de luto o sentar tres partidos.
  Sin eso, incumple la condición 3 de la ADR 0048 y no debe entrar.
- **Magnitud del duelo**: como cota, la penalización no debe superar la de la lesión leve (RF-091), que es
  el castigo de referencia del juego para «juegas mermado». Justificación: si superara a la lesión, el
  duelo pesaría más que la herida que lo causó. Se mide con `--full-runs` contra `runWinRate` 20-30 y
  `deathsPerRun` 1,5-3.
- **Previsibilidad**: líneas en la pantalla de alineación (RF-106) **antes** de que el sistema pueda
  penalizar, y el indicador de riesgo por jugador (RF-012c) mostrando el duelo potencial en la ficha del
  vinculado, no solo del que puede morir.

---

## 6. Si el revisor elige B: qué se toca exactamente

### 6.1 Retirada limpia, y por qué es numéricamente inocua

El término de precio **vale exactamente 0 hoy** (`Bonds.Count` es siempre 0). Quitarlo es un cambio
**garantizado byte a byte idéntico** en cualquier lote de `/Balance`: no hay que medir nada.

| Qué | Dónde | Acción |
|---|---|---|
| Requisitos | `docs/requisitos.md` §3.11 (RF-100..106) | Marcar **fuera del alcance de lanzamiento**, no borrar: el diseño queda registrado para una actualización, igual que RF-103 hace con las rivalidades |
| Tarifa | `Sim/Run/Systems/Market/MarketSystem.cs:106` | Quitar `+ (economy.Market.PlayerSalePerBond * player.Bonds.Count)` |
| Dato económico | `data/economy/economy.json:55`, `EconomyData.cs:94,453`, `data/schemas/economy.schema.json` | Quitar `playerSalePerBond` |
| Vista | `Sim/Run/View/MarketView.cs:59,298` | Quitar la columna `Bonds` |
| Plan de fases | `docs/plan-fases.md:150` | Los vínculos ya figuran en el bloque de fase 3; anotar que pasan a post-lanzamiento |

### 6.2 El estado serializado: dos salidas, ambas aceptables

`RunPlayer.Bonds`, `.Mourning` y `.BondProgress` se escriben y leen en `RunSave.cs` y están en
`run-save.schema.json` (`required`). Dos opciones:

- **Conservar los campos como reservados** (recomendada): cuesta cero, no sube la versión del guardado
  —que acaba de pasar a v2 con la ADR 0097— y no rompe partidas. Exige **documentarlos como reservados**
  en el esquema y en `RunState.cs`, porque lo que creó este problema no fue el campo, fue la **promesa**
  sin dueño.
- **Eliminarlos y subir a v3**: más limpio, cuesta una versión de esquema y una migración. Defendible si
  se prefiere que el guardado no contenga nada que no ocurra.

La decisión de cuál es **del revisor**; ninguna de las dos afecta al balance.

### 6.3 Lo que se pierde, y cómo no perderlo

Se pierde la única mecánica que iba a generar **historia dentro de una run**, y eso es real: es de lo poco
que este juego tiene y otros no. La sustitución, **HYPOTHESIS**, cuesta una fracción y no añade capa
persistente:

> **El informe post-partido nombra la pareja.** Los datos ya están: el evento `GOAL` lleva el asistente, y
> `MatchReport.Players` lleva las estadísticas por jugador. «Tercera vez que Grix asiste a Volda» es una
> línea de texto calculada **del informe del partido**, sin estado de run, sin esquema, sin penalización y
> sin balance que medir. La historia la construye el jugador; el juego solo tiene que **decirle que se ha
> fijado**.

Eso conserva RF-105 en su versión barata (el highlight de un suceso notable) y tira RF-104, que es el
único que introducía capa persistente y castigo sin decisión.

---

## 7. Qué se hace con la habilidad no-muerta (`numb`)

**MEASURED.** `data/perks/numb.json` tiene dos efectos: inmunidad a `mourning` y a `minorInjuryPenalty`.
El primero es inmunidad a algo imposible. Y como la descripción **se genera** (RT-035), el juego imprime
literalmente *«no entra en duelo cuando pierde a un vinculado»* (`data/l10n/es/templates.json:357`): una
promesa que el motor no puede cumplir y que nadie escribió a mano, luego nadie puede corregir a mano. Es
el caso exacto que la regla 11 prohíbe.

**Recomendación (con B): quitar el efecto `mourning` de `numb` y compensar por el canal ya calibrado.**

Qué se toca:

| Qué | Dónde |
|---|---|
| El efecto | `data/perks/numb.json` (queda solo `minorInjuryPenalty`) |
| El texto de raza | `docs/requisitos.md:130` (RF-031b) y la tabla de razas `:144` — *«inmunes al duelo»* |
| El enum y su nombre | `Sim/Perks/PerkDefinition.cs:94`, `DescriptionGenerator.cs:518`, `PerkLoader.cs:74` |
| El esquema | `data/schemas/perks.schema.json:432` |
| La plantilla l10n | `data/l10n/{es,en}/templates.json:357` |
| El test | `Sim.Tests/Perks/RacialAbilityTests.cs:107`, `DescriptionTests.cs:322` |

**Sobre la compensación.** La ADR 0026 fija que una habilidad racial vale **0 puntos de tasa de victoria
por construcción** y que su presupuesto es de campaña. Al quitar el duelo, a los no-muertos les queda **la
mitad viva** de su habilidad, y esa mitad no es trivial: 5,79 lesiones propias por run (**MEASURED**). No
hay que inventar mecánica: **medir la raza con `--full-runs` por raza y, si se sale del abanico de la ADR
0092 (sesgos de suma cero, abanico 6,4), corregir por los sesgos de atributo**, que es la palanca ya
calibrada. Inventar una segunda inmunidad para tapar el hueco repetiría el error que este informe
diagnostica: declarar antes de poder cumplir.

**Alternativa si el revisor elige A:** `numb` se queda como está y deja de ser una promesa vacía en cuanto
el duelo exista. Es el único argumento **a favor de A** que no depende de diseñar nada nuevo, y es débil:
arreglar media línea de un JSON no justifica una capa persistente.

---

## 8. Riesgos y modos de fallo

**De la recomendación B:**

- **R1 — Se retira la única fuente de historia y no se pone nada.** Es el riesgo real. Mitigación: §6.3 no
  es opcional, es parte de B. Si se retira RF-104 y no se implementa el informe que nombra a la pareja,
  el juego queda estrictamente más pobre. **Impacto alto, coste de mitigación bajo.**
- **R2 — Retirar el requisito y dejar el estado serializado reproduce el problema.** Un campo reservado sin
  documentar es un campo que dentro de seis meses alguien vuelve a tarifar. Mitigación: §6.2 exige
  documentarlo en el esquema **y** en `RunState.cs`; si eso no se hace, elegir la opción de subir a v3.
- **R3 — Los no-muertos se desequilibran al perder media habilidad.** Probabilidad baja (ADR 0026: la
  habilidad vale 0 por construcción), pero hay que **medirlo**, no asumirlo.
- **R4 — Se retira y luego se echa de menos.** Aceptable: RF-103 ya establece el precedente de dejar
  mecánicas para una actualización, y §5 deja escrito el listón para volver a entrar.

**De la alternativa A:**

- **R5 — La espiral de derrota.** El duelo solo actúa cuando la run va mal (§3.1). Modo de fallo: el
  jugador siente que el juego le remata, no que le pone un problema. **No se puede medir con
  `runWinRate`**: una espiral y un ajuste de dificultad dan la misma tasa de victoria y se sienten
  distintos. Este riesgo no tiene instrumento en `/Balance`.
- **R6 — El sistema queda por debajo del umbral de percepción.** Vínculo formado en el partido 9 de 14 y
  duelo en el 7-10 % del tiempo titular: es perfectamente posible pagar la capa entera y que el jugador
  **no note que existe**. Es el modo de fallo más caro: coste completo, valor cero.
- **R7 — La mitad positiva se improvisa.** Al no estar especificada (§1.4), lo probable es que se resuelva
  con un bono de atributo plano, que es exactamente el diagnóstico de `perks-auditoria-diseno.md` B4:
  *«no existe el coste, así que no existe la decisión»*. Se añadiría una capa persistente para producir
  más números.

**De la alternativa C (dejarlo como está):**

- **R8 — El juego miente en texto generado.** `numb` promete inmunidad a algo imposible, y por RT-035 la
  frase no se puede corregir sin corregir el efecto. Es incumplimiento directo de la regla 11.
- **R9 — Deuda que se multiplica.** Siete requisitos activos que nadie puede cumplir, un precio que
  multiplica por cero y tres campos de guardado inertes. Cada sistema nuevo que los lea los dará por
  buenos.
