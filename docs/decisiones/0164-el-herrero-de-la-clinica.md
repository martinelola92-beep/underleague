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

## Enmienda del 2 oct 2026: más semillas y el techo por run (corregida tras la revisión independiente)

**Instrumento (Regla J).** `/Balance --full-runs` gana `--no-blacksmith` (catálogo de prótesis vacío: la clínica vuelve a la
de la ADR 0099), el control del mismo árbol, y `FullRunMetrics.Describe` gana cinco filas INFO sobre el estado final de la
run (`FullRunMetrics.ProstheticCensus`): `maxProsthesesOnOnePlayer`, `maxAttributeProsthetic`/`maxAttributePlain` (el mejor de
fuerza, velocidad, técnica y aguante) y sus medias. **No mira `Leash`** (la prótesis `skull` le suma +6): otra escala, omitida a
propósito. Validado con tests: un caso de valor conocido (brazo +8 y mandíbula +6 sobre fuerza 50 → el censo da 64, es decir
+14) y el control (en las runs donde el herrero real no forjó nada, la rama con catálogo vacío da la misma run, misma semilla).

**Lote (reproducible).** Desde un árbol con el commit `554636a` o posterior, por cada semilla `S` en 1, 1001, 2001, 3001, 4001, 5001
(separadas ≥ 600 porque las runs usan `seed + i`):

```
dotnet run --project Balance -c Release --no-build -- --full-runs 600 --seed S --out <dir>
dotnet run --project Balance -c Release --no-build -- --full-runs 600 --seed S --no-blacksmith --out <dir>
```

600 runs × 3 doctrinas por celda; el resumen es el de la doctrina principal. Pareado por semilla. Cifras por semilla
(con herrero / sin herrero), en el orden de las semillas:

| métrica | 1 | 1001 | 2001 | 3001 | 4001 | 5001 |
|---|---|---|---|---|---|---|
| `runWinRate` | 17,00 / 16,83 | 15,83 / 16,00 | 14,00 / 14,17 | 14,17 / 15,33 | 15,50 / 14,67 | 13,67 / 13,50 |
| `deathsPerRun` | 1,26 / 1,31 | 1,37 / 1,36 | 1,27 / 1,25 | 1,19 / 1,21 | 1,19 / 1,19 | 1,38 / 1,38 |
| `goldSpentClinicPerRun` | 12,81 / 13,61 | 12,64 / 12,74 | 12,34 / 12,80 | 11,66 / 11,90 | 12,93 / 12,82 | 11,62 / 12,18 |
| `maxAttributeProsthetic` / `maxAttributePlain` (con herrero) | 89 / 99 | 90 / 99 | 91 / 99 | 89 / 98 | 91 / 95 | 90 / 99 |

Medias (± error típico de la media entre las 6 semillas): `runWinRate` 15,03 ± 0,53 con herrero y 15,08 ± 0,50 sin él,
**diferencia pareada −0,05 ± 0,27**; `deathsPerRun` 1,28 ± 0,03 en las dos ramas (−0,01 ± 0,01); `goldSpentClinicPerRun`
12,33 ± 0,23 contra 12,67 ± 0,24 (−0,34 ± 0,13). Uso: 0,43 tratamientos y 0,26 prótesis por run (ET < 0,005); autómatas 0,00.

**Etiquetas (Regla F), corregidas:**
- **Sin efecto detectable al nivel de uso de la política** (0,26 prótesis por run): diferencia en victoria −0,05 ± 0,27 y en
  muertes −0,01 ± 0,01. La cota a 2 ET es ±0,6 puntos de victoria por run, es decir **~±1,4 puntos por forja** (0,6 / 0,43).
  Lo demostrado es el **uso bajo**, no la ausencia de efecto: con este uso un efecto por forja menor que esa cota no se ve.
  (Y la lectura de una semilla de la primera medición, +1,3 puntos, era ruido.)
- **LIKELY**: la clínica sale algo más barata (−0,34 ± 0,13 de oro por run, z −2,5); un contraste entre muchos.
- **CONFIRMED** (6 semillas × 1.800 runs): nadie llega a autómata con esta política; la tercera prótesis es de cola.
- **El 90 contra 98,2 de máximos no se interpreta.** Compara un grupo de ≈0,26 protésicos por run contra ≈10 jugadores sin
  prótesis (el máximo de un grupo grande es mayor por tamaño de muestra) y la política forja sobre todo a suplentes y comunes
  (selección). Está **confundido** por las dos cosas: no dice que el herrero no forje por encima de la progresión. Tampoco
  se interpreta la media 76,2 contra 68,7.
- **Límite del instrumento:** el censo mira la plantilla **final** (muertos y vendidos fuera) y la política no concentra: nunca
  pasa de 2 prótesis en un jugador. El caso que importa (varias prótesis en un titular estrella) **no está representado en el lote**.

### Atributo máximo por run: nota de `game-design-review` sobre «sin tope»

1. **Qué experimenta el jugador / 2. qué decide.** Ve una mesa con tres destinos y sus rangos y decide si arriesgar la identidad
   de un grave en la forja; cada prótesis es permanente y de una ranura. Un tope por run sería una regla que el jugador no ve venir.
3. **Qué debería decidir.** Elegir **a quién** forjar y con cuánto oro; no «cuántas veces me deja el juego».
4. **Regla.** RF-095, RF-095b, RF-095c; no hay requisito de tope.
5. **Sistemas.** `/Sim` (`MedicalSystem.Forge`, catálogo de prótesis); un tope tocaría además `/Game` (decirlo en la mesa).
6. **Alternativas.** (a) Sin tope, techo estructural; (b) tope de prótesis por jugador (hoy 7 ranuras); (c) tope de atributo
   por run; (d) rendimiento decreciente por prótesis ya instalada.
7. **Trade-off.** Hoy el coste de forjar es la identidad (35 % de empeoramiento de −9..−12, una ranura ocupada) y el oro.
   (b)/(c)/(d) añaden un coste, pero invisible hasta que se alcanza.
8. **Estrategias.** Concentrar prótesis en un titular (estrella de chatarra, `Scrap`/`Automaton`) frente a repartirlas por la
   plantilla; ambas legibles.
9. **Degeneración, medida contra la progresión (el criterio de la skill).** `attributesPerLevel` = 2 con nivel máximo 8: la
   progresión da **+14 a cada atributo de campo** (los cuatro, a la vez). La forja da como techo **+14 a un solo atributo**
   (fuerza: brazo 8 + mandíbula 6) y **+48 repartido** entre cinco (cuatro de campo más la correa); estar al techo exige siete
   lesiones graves en **el mismo** jugador (el lote registra ~1,7 graves por run en toda la plantilla: LIKELY inalcanzable para
   esta política y difícil para una persona) y 35 % de acierto cada vez, con cada fallo (−9..−12) restando de lo ganado.
   **Conclusión del pase: no hace falta un tope**; el techo estructural es del orden de la progresión y más caro de alcanzar.
10. **Cómo se demuestra.** El test de aritmética del techo (`TheStructuralCeilingOfTheForgeIsWhatTheDataSays`), el censo con
    valor conocido y el lote de arriba. **Lo que no se ha medido** es la estrella forjada al máximo en partido (un lote
    forzando la concentración): queda como **decisión del revisor** si +48 sobre un titular estrella, aunque inalcanzable hoy,
    merece un tope de seguridad o esa medición. No se implementa nada.

No se toca `/Game`: no hay tope que enseñar en la pantalla del herrero. **Umbral de reapertura (provisional, sin medir, Regla H):**
`maxProsthesesOnOnePlayer` ≥ 4 o un cambio de deltas/ranuras que mueva el techo (el test lo avisa). **La política actual no puede
alcanzar el umbral** (no concentra, máximo 2), así que ese indicador solo se movería con otra política o con una persona; es una
señal para cuando exista una política que concentre, no un guardián efectivo hoy. Las magnitudes, 30/35/35 y el precio siguen
**provisionales, sin medir** como balance de diseño.
