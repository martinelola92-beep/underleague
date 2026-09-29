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
