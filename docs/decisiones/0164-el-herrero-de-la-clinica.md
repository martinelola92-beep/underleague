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

## Medición de campaña (provisional, sin medir hasta el lote del revisor)

`/Balance --full-runs 600 --seed 1`, 1.800 runs de tres doctrinas cada lote, rama `main` (antes) frente a esta rama (después):

| métrica | antes | después |
|---|---|---|
| `runWinRate` | 21,50 | 21,50 |
| `deathsPerRun` | 1,75 | 1,75 |
| `squadTreatmentsPerRun` | 0,30 | 0,30 |
| `riskyTreatmentsPerRun` (matasanos) | 0,01 | 0,01 |
| `goldSpentClinicPerRun` | 20,04 | 20,04 |
| `blacksmithTreatmentsPerRun` | n/a | 0,00 |
| `prosthesesPerRun` | n/a | 0,00 |
| `automatonsPerRun` | n/a | 0,00 |

**Lectura (CONFIRMED para esta política):** los dos lotes salen idénticos porque `RunPolicy.TryTheBlacksmith`
solo actúa en el hueco «hay un grave que merece la pena y no llega el oro para el médico pero sí para el
herrero», y en la política ese hueco casi no ocurre (el matasanos, que ocupa el mismo hueco, ya medía 0,01
por run). El herrero, por tanto, **no está medido**: ni su uso, ni su efecto sobre la victoria o las muertes.
Medirlo exige una decisión de diseño de la política (por ejemplo, preferir al herrero sobre el médico cuando
el jugador tiene un valor bajo) que se deja al revisor; hasta entonces, las probabilidades 30/35/35 y el
precio siguen siendo **provisionales, sin medir** (Regla H). Ficheros: `out/base/`, `out/after/`.
