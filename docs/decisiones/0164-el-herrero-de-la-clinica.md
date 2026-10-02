# ADR 0164 — El herrero de la clínica: prótesis con la apuesta a la vista

Fecha: 29 sep 2026 · Estado: **aceptada**. **Decisión del revisor** (`docs/plan-diversion.md` §0): *«el taller de
implantes no funciona actualmente, hay que darle una vuelta al modo de clínica»* y *«taller ok»* a la
propuesta de fusionarlo con la clínica como un servicio más.
**Implementa** RF-095, RF-095b y RF-095c, que nunca se implementaron. **Enmienda RF-011**: el taller deja de
ser un tipo de nodo propio (el generador nunca lo produjo, `MapInvariants`) y pasa a ser un servicio de la
clínica.
**Requisitos:** RF-011, RF-012d, RF-092, RF-094, RF-095, RF-095b, RF-095c, RT-022, RT-030, RT-035
**Relacionada:** ADR 0099 (los tres servicios de la clínica y el matasanos), ADR 0048, ADR 0159 (la muerte
fuera de partido pasa por `DeathConsequences`).

## Lo que había (Regla G)

`RunPlayer.Prostheses` (`RunProsthesis(Slot, Effect)`) existe en el estado y se guarda desde la versión 1 del
esquema, pero nadie lo rellena; las etiquetas `Scrap` y `Automaton` están traducidas y nadie las pone.
`NodeKind.Workshop` existe y el generador de mapas no lo produce nunca. La clínica tiene tres servicios:
por pieza, plantilla entera y el matasanos (ADR 0099).

## Decisión

1. **Cuarto servicio de la clínica: el herrero**, para un jugador con **lesión grave**. Antes de confirmar se
   ven los tres resultados con su probabilidad (RF-095):
   - **curación**: vuelve a sano;
   - **mejora**: prótesis con ventaja (un atributo o la correa) y etiqueta `Scrap`;
   - **empeoramiento**: prótesis con desventaja y etiqueta `Scrap`. Nunca mata: el riesgo de muerte es del
     matasanos, el del herrero es **la identidad**.
   Base provisional, sin medir: 30 / 35 / 35, más barato que el médico.
2. **Invertir oro** desplaza las probabilidades hacia curación y mejora con rendimiento decreciente
   (RF-095b), con la tabla actualizada a la vista en cada paso.
3. **Prótesis de datos**: `data/prostheses/*.json` (esquema propio), cada una con nombre con humor es/en,
   ranura (brazo, pierna, ojo, mandíbula…), efecto sobre atributos o correa y si es de mejora o de
   empeoramiento. Se aplican al instalarse sobre los atributos del jugador y quedan en `Prostheses` para
   verse. Una ranura ocupada no se repite.
4. **Tres prótesis** → el jugador **gana** `Automaton` y **conserva** su etiqueta de especie (RF-095c,
   **enmendada** por esta ADR tras la revisión independiente). El primer texto decía que perdía la especie;
   se retiró porque (a) `Simulator.ValidatePerks` lanza si un perk exige una etiqueta que el jugador no lleva y
   `gentle_giant`, `iron_gate` y `deathless_march` tienen `tagsRequired` con su especie: el partido siguiente
   reventaba (CONFIRMED); (b) 15 objetos restringidos dejaban de aportar y (c) el «premio» era una etiqueta que
   ningún perk consume. **La pérdida de especie se aplaza hasta que exista la familia de perks de autómata**
   que la compense. Lo que consume la etiqueta de especie (Regla G): los perks con `tagsRequired` de su
   especie y los objetos restringidos (`MatchItem.RequiredTag`). La habilidad racial de la ficha **no** la
   consume: `EffectEngine.RacialAbility` y `Progression.ActivePerks` van por `definition.Race`, no por la
   etiqueta (una versión anterior de esta ADR afirmaba lo contrario, y era falso).
5. **Aleatoriedad**: flujo propio derivado del nodo y del jugador (`OfferStream`, desplazamiento 9000),
   nunca el de partido (RT-022).
6. El taller como nodo se retira de la leyenda del mapa; `NodeKind.Workshop` se conserva en el enum para no
   invalidar guardados.

## Las diez preguntas (`game-design-review`)

1. **Qué ve el jugador.** Una mesa de herrero con tres destinos posibles y sus números, y un jugador que
   vuelve con una pata de palo o un brazo de hierro.
2. **Qué decide.** Médico caro y seguro, matasanos barato que puede matar, o herrero que no mata pero
   cambia al jugador; y cuánto oro invertir para inclinar la tirada.
3. **Qué debería decidir.** Lo mismo. Si el herrero siempre gana al médico o nunca se usa, se mide.
4. **Regla.** RF-095, RF-095b, RF-095c; enmienda de RF-011.
5. **Sistemas.** `/Sim`: `MedicalSystem` (servicio nuevo), catálogo de prótesis, `RunPolicy`. `/data`:
   `prostheses/`, `economy.json` (precio y probabilidades). `/Game`: pantalla de clínica (`NodeScreen`), ficha
   de Equipo (prótesis visibles).
6. **Alternativas.** Nodo de taller propio: rechazado por el revisor y por el recorte de nodos.
7. **Trade-off.** Más barato que el médico a cambio de perder el control del resultado.
8. **Estrategias.** Una plantilla de chatarra (etiqueta `Scrap`) y el camino al autómata.
9. **Degeneración.** Invertir mucho oro para garantizar mejoras en serie: el rendimiento decreciente lo acota;
   se mide el número de prótesis por run y el atributo máximo.
10. **Cómo se demuestra.** Tests: la tabla que se enseña es la que se tira, invertir desplaza con rendimiento
    decreciente, tres prótesis dan `Automaton`, una ranura no se repite, determinismo. Lote de campaña con la
    política usando el herrero. Captura de la clínica.

## Enmienda del 29 sep 2026 (revisión independiente): qué cambió tras el primer commit

1. **La tercera prótesis conserva la especie** (decisión 4, arriba): el crash del partido siguiente y los costes
   invisibles se retiran; test de partido completo con cada perk racial con `tagsRequired` y cada objeto
   restringido (`AutomatonTests`).
2. **El herrero es una apuesta de verdad.** La primera versión curaba siempre, costaba menos que el médico y
   tenía esperanza de atributos ~0 (mejoras 8..11, empeoramientos 8..11): dominaba al médico. Ahora las
   mejoras valen **+6..+8** y los empeoramientos **−9..−12** (provisional, sin medir: la asimetría es una
   decisión del coordinador, no una medición), de modo que con oro extra 0 la esperanza de atributos de la tirada
   es de unos −1,2 puntos (0,35 × 7 − 0,35 × 10,4) y se vuelve positiva con oro extra; precio base del 60 al
   **75 %** del médico (10 de oro frente a 14). Cada prótesis es permanente y ocupa una ranura, y siempre deja al
   jugador sano: **sigue siendo un servicio que cura**, así que su coste real es la identidad, no el oro.
3. **La mesa enseña el rango** de magnitudes de mejora y de empeoramiento y los atributos que pueden salir según
   las ranuras libres (`BlacksmithQuote.ImproveRange/WorsenRange`, `NodeScreen`).
4. **La política usa el herrero antes que el médico** cuando el grave es un suplente (hay siete jugadores
   disponibles de más valor) o de rareza común, y siempre que el oro no llega al médico. Orden de la clínica:
   tarifa plana; herrero para suplente/común con oro para él; médico para el resto; leves de titulares; herrero
   por falta de oro para el médico; matasanos con lo que quede (no pierde su hueco: sigue viendo los graves y el
   oro que el herrero no tomó).
5. **Instrumento (Regla J).** `IRunSystems.Prostheses` y `Bets` tenían una implementación por defecto que devolvía
   el catálogo vacío y `RunPolicy.RecordingSystems` no los reenviaba: **la medición anterior de esta ADR jugó
   con un catálogo de prótesis vacío** y por eso salió idéntica al lote base. Ahora los dos miembros son
   obligatorios y el compilador exige que cada envoltorio los reenvíe. Único miembro por defecto que queda en
   `IRunSystems`: `OnMatchPlayed` (un gancho de observación sin retorno; ya lo reenvían `BossRunSystems` y
   `RecordingSystems`). Un test valida el instrumento contra un caso conocido (una política jugando runs enteras
   pasa por el herrero).
6. **Guarda del flujo**: `OfferStream` numera `nodo × 10 000 + desplazamiento`, y `9000 + id` con `id ≥ 1000`
   cae en el flujo del nodo siguiente; `Forge` rechaza ids ≥ 1000 (`BlacksmithStreamSpan`). Test de independencia de
   flujo real (la tirada se reproduce a mano con `OfferStream`) en vez del que comparaba `Forge` consigo mismo.

## Medición de campaña (provisional, sin medir hasta el lote del revisor → medida el 29 sep 2026)

> **La medición anterior de esta sección (0,00 herrero, 21,50 idéntico al lote base) medía un catálogo de
> prótesis vacío** (véase el punto 5 de la enmienda, Regla J) y **se descarta**. No decía nada sobre el herrero.

`/Balance --full-runs 600 --seed 1` (1.800 runs de tres doctrinas; una sola semilla, resumen sobre las 600 del
lote principal), rama `main` con esta enmienda; salida en `out/herrero-s1/` (ignorado). Línea base: `out/base/`
(21,50 y 1,75, antes de que existiera el herrero).

| métrica | antes (sin herrero) | después (herrero, política nueva) |
|---|---|---|
| `runWinRate` | 21,50 | 22,83 |
| `deathsPerRun` | 1,75 | 1,77 |
| `squadTreatmentsPerRun` | 0,30 | 0,29 |
| `riskyTreatmentsPerRun` (matasanos) | 0,01 | 0,01 |
| `goldSpentClinicPerRun` | 20,04 | 19,32 |
| `blacksmithTreatmentsPerRun` | n/a | 0,73 |
| `prosthesesPerRun` | n/a | 0,43 |
| `automatonsPerRun` | n/a | 0,00 |

**Lectura (LIKELY, una semilla; Regla F y `feedback_medir_con_varias_semillas`):** el herrero se usa (0,73
tratamientos por run, 0,43 prótesis: el resto son curaciones limpias) y **no mueve las muertes** (1,75 → 1,77).
La diferencia de victoria (+1,3 puntos) cae dentro del error típico de 600 runs (≈ 1,7 puntos con p ≈ 0,22): **no
se puede afirmar que el herrero ayude ni que perjudique**. Nadie llega a autómata en el lote (0,00): la tercera
prótesis es un caso de cola con esta política y el uso actual, así que la etiqueta `Automaton` y la decisión de
conservar la especie no pesan en balance hoy. **No se midió el atributo máximo por run** (el registro por run no lo
lleva; añadirlo exige tocar `RunPlayResult` y el CSV). Las magnitudes, las probabilidades 30/35/35 y el precio siguen
siendo **provisionales, sin medir** (Regla H): falta repetir con más semillas y con un lote donde la política elija
al herrero sobre el médico en distinta medida.

## Enmienda del 2 oct 2026: más semillas y el techo por run

**Instrumento (Regla J).** `/Balance --full-runs` gana `--no-blacksmith` (catálogo de prótesis vacío: la clínica vuelve a la
de la ADR 0099), el control del mismo árbol, y `FullRunMetrics.Describe` gana cinco filas INFO sobre el estado final de la
run: `maxProsthesesOnOnePlayer`, `maxAttributeProsthetic`/`maxAttributePlain` (el mejor de fuerza, velocidad, técnica y
aguante) y sus medias. Un test las valida contra un caso conocido (runs enteras de la política que acaban con un protésico).

**Medido:** 600 runs × 3 doctrinas por semilla, semillas 1, 1001, 2001, 3001, 4001 y 5001 (separadas ≥ 600: `seed + i`),
con y sin herrero sobre las mismas semillas (pareado); media ± error típico de la media entre semillas. Resumen de la
doctrina principal; salida fuera del repositorio.

| métrica | con herrero | sin herrero | diferencia pareada ± ET |
|---|---|---|---|
| `runWinRate` | 15,03 ± 0,53 | 15,08 ± 0,50 | **−0,05 ± 0,27** (por semilla: +0,17 −0,17 −0,17 −1,16 +0,83 +0,17) |
| `deathsPerRun` | 1,28 ± 0,03 | 1,28 ± 0,03 | −0,01 ± 0,01 |
| `goldSpentClinicPerRun` | 12,33 ± 0,23 | 12,67 ± 0,24 | −0,34 ± 0,13 (z −2,5) |
| `blacksmithTreatmentsPerRun` / `prosthesesPerRun` | 0,43 / 0,26 (ET < 0,005) | 0 / 0 | |
| `automatonsPerRun` | 0,00 | 0,00 | |

**Etiquetas (Regla F):**
- **CONFIRMED** (6 semillas, pareado): el herrero **no mueve la victoria** (−0,05 ± 0,27: la cota a 2 ET es ±0,6 puntos) ni
  **las muertes** (−0,01 ± 0,01). La lectura de una semilla de arriba (+1,3 puntos) era ruido; ahí el lote ya no es el
  de aquella medición (hoy `runWinRate` ≈ 15, no 22), así que lo que se sostiene es la diferencia pareada, no la cifra absoluta.
- **CONFIRMED**: el servicio se usa (0,43 tratamientos y 0,26 prótesis por run, estable entre semillas) y la clínica sale
  ligeramente más barata (−0,34 de oro por run, z −2,5; **LIKELY** como efecto real, es un único contraste entre muchos).
- **CONFIRMED**: nadie llega a autómata con esta política (0,00 en 6 semillas × 1.800 runs); la tercera prótesis sigue
  siendo de cola.
- **Límite del instrumento:** el censo del techo mira la plantilla **final** (los muertos y vendidos no cuentan) y la
  política elige al herrero sobre todo para suplentes y comunes; un jugador humano concentrando prótesis en un
  titular no está representado. Por eso el techo se sostiene además por aritmética, no solo por el lote.

**Atributo máximo por run: no se implementa tope (game-design-review, respuesta a «degeneración»).**
- Ya existe un techo, **estructural**: una prótesis por ranura (`HasFreeProsthesisSlot`), atributos acotados a 1..99
  (`ProsthesisDefinition.ApplyTo`) y siete ranuras con efectos repartidos entre cinco atributos. Del catálogo (derivado,
  no medido): el techo de **un** atributo es **+14** (fuerza: brazo +8, mandíbula +6) y el total de todas las mejoras
  posibles es **+48**; cada una exige una lesión grave y 35 % de salir bien (más oro, con rendimiento decreciente).
  Un test fija esa aritmética (`TheStructuralCeilingOfTheForgeIsWhatTheDataSays`): tocarla exige pasar por esta ADR.
- Medido en el lote: a lo sumo **2** prótesis en un mismo jugador (6 semillas), y el mejor atributo de un protésico llega a
  90 ± 0,4 de máximo por semilla frente a **98,2 ± 0,7** de los jugadores sin prótesis: el herrero no forja nada por encima de
  lo que ya da la progresión. Media del mejor atributo: 76,2 (protésicos) contra 68,7 (resto), compatible con que el
  herrero se use sobre jugadores de más valor o con que cada mejora aporte ~+7.
- Un tope por run añadiría una regla invisible (viola «comportamiento observable > modificadores invisibles») para
  resolver un caso que no ocurre. **No se toca `/Game`**: no hay tope que enseñar en la pantalla del herrero. **Se reabre
  si** se añaden prótesis, deltas o ranuras que muevan el techo (el test lo avisa), o si la política/jugador concentra
  prótesis (`maxProsthesesOnOnePlayer` ≥ 4).
- Las magnitudes, 30/35/35 y el precio siguen **provisionales, sin medir** como balance de diseño (Regla H): esta medición
  dice que no desequilibran la run ni el techo, no que sean las óptimas.
