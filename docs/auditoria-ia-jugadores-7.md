# Séptima auditoría — calibración de la identidad posicional

Continúa `auditoria-ia-jugadores-6.md`. **No se ha aplicado ningún cambio** (§18).

Etiquetas: **OBSERVADO** (medido aquí), **INFERIDO** (deducido de código o aritmética verificable),
**HIPÓTESIS** (no medido), **CONFIRMADO** (hipótesis previa validada por medición).

## 1. Pregunta rectora

> «La posición base importa mucho, pero ¿está demasiado especializada? ¿Podemos permitir que DEF/MID ocupen
> posiciones ofensivas de forma útil sin borrar la identidad de cada posición?»

**Respuesta: sí, y con un solo número.** Subir el peso base de `Shoot` de DEF y MID una cuarta parte del
camino hasta el del delantero lleva la reconversión de 100/48/38 a **100/68/55**, deja todos los
guardarraíles en verde en las dos semillas y **baja las puertas rojas de 4 a 3**. *(OBSERVADO)*

## 2. Estado inicial

Prohibido tocar de forma permanente `Utility.cs`, `MatchEngine.cs`, `MatchPlayer.cs`, `ActionZone*`,
`tuning.json`, la validación de `Simulator`, la composición y la formación base. Permitidos cambios
**temporales** de `data/ai/weights.json`, restaurados al terminar. Instrumento temporal borrado (§18).

## 3. Baseline

Lote de 1.000 partidos con `data/balance/reference.json`. Guardarraíles, no objetivos.

| | semilla 1 | semilla 2 |
|---|---|---|
| `shotsPerMatch` (banda 7-15) | 7,36 | 7,25 |
| `tacklesPerMatch` (6-14) | 12,80 | 10,33 |
| `injuriesPerMatch` (0,3-0,9) | 0,87 | 0,52 |
| `possessionChanges` (12-28) | 20,13 | 21,93 |
| `passChainAvgLength` (2-4) | 2,03 | 2,10 |
| `goalsPerMatch` | 2,31 | 2,17 |
| `ballThirdMaxShare` (≤52) | 47,14 | 48,04 |

43 puertas: **4 rojas** — `BadBuildsLoseToTheirBaseline` (`elf_brawler` 50,62), `BuildsWinDifferently`
(`passChain` 1,09), su agregador `NoGateMetricIsOutOfRange`, y `EquippingAGoodBuild` (1,9 puntos).

## 4. Inspección de `weights.json`

*(OBSERVADO — lectura de datos)*

| acción | GK | DEF | MID | FWD | FWD/DEF | abanico |
|---|---|---|---|---|---|---|
| **`Shoot`** | 0 | **77** | 188 | 385 | **5,00×** | **308** |
| `FindSpace` | 0 | 200 | 380 | 460 | 2,30× | 260 |
| `Dribble` | 50 | 140 | 240 | 300 | 2,14× | 160 |
| `OfferSupport` | 0 | 80 | 140 | 160 | 2,00× | 80 |
| `ThroughPass` | 0 | 90 | 180 | 150 | 1,67× | 90 |
| `ShortPass` | 600 | 500 | 560 | 420 | 0,84× | 140 |
| `LongPass` | 320 | 170 | 250 | 135 | 0,79× | 115 |
| `CoverSpace` | 500 | 420 | 260 | 120 | **0,29×** | 300 |
| `MarkOpponent` | 0 | 400 | 300 | 120 | 0,30× | 280 |
| `Retreat` | 400 | 320 | 220 | 140 | 0,44× | 180 |
| `Tackle` | 165 | 255 | 210 | 128 | 0,50× | 127 |
| `ChaseBall` | 300 | 380 | 420 | 400 | 1,05× | 40 |

Los pases están diferenciados **al revés** (un DEF pasa más que un FWD) y `ChaseBall` casi no lo está. La
diferenciación ofensiva se concentra en `Shoot`, y muy por detrás en `FindSpace` y `Dribble`.

### Una hipótesis mía que resultó falsa

Antes de medir escribí: *el cuello no es que el DEF no pueda elegir acciones ofensivas, sino que
`CoverSpace` 420 le gana a todo*, apoyándome en que A6 midió `FindSpace` casi igual entre DEF y FWD
(45,2 % contra 42,2 %) y `CoverSpace` muy distinto (45,8 % contra 17,2 %).

**Era falsa** *(OBSERVADO, §6)*. `Shoot` sí mueve el comportamiento, y mucho. La explicación es que las dos
acciones no compiten: `CoverSpace` se evalúa **sin balón** y `Shoot` **con balón cerca de la portería**. En
esa segunda situación el rival de `Shoot` no es `CoverSpace` sino `ShortPass` (500 para un DEF), y con 77 de
base el defensa nunca tira: **pasa**. *(INFERIDO)*

## 5. Instrumentación

`Sim.Tests/Analysis/_A7.cs`, temporal, ya borrado. Idéntico control que A6: siete jugadores de campo con
atributos **exactamente iguales** (50 en los cinco), sin rasgos ni perks, rival **siempre** F1, y los pesos
barridos editando `weights.json` entre ejecuciones.

> **Las cifras de este instrumento no son comparables con el lote de `/Balance`**: con plantillas planas el
> partido de referencia da ~3,4 tiros, no 7,4. Solo valen las comparaciones internas. Los guardarraíles se
> comprueban aparte, con el lote real (§13).

**Escala del barrido.** Un «+10 %» sobre 77 no es informativo. Se usa `t` = **fracción del camino desde el
valor actual hasta el del delantero**:

| t | `Shoot` DEF | `Shoot` MID |
|---|---|---|
| 0 % | 77 | 188 |
| 25 % | 154 | 237 |
| 35 % | 185 | 257 |
| 50 % | 231 | 286 |
| 75 % | 308 | 336 |
| 100 % | 385 | 385 (igualados) |

## 6. Experimento A — `Shoot` aislado

Misma casilla (6,3), mismos atributos. Tiros normalizados con F1 (delantero natural arriba) = 100.

| t | F1 tiros | F6 (DEF arriba) | F7 (MID arriba) | **DEF norm.** | **MID norm.** |
|---|---|---|---|---|---|
| 0 % | 3,36 | 1,20 | 1,62 | **36** | **48** |
| 25 % | 3,52 | 1,89 | 2,35 | **54** | **67** |
| 35 % | 3,52 | 2,20 | 2,58 | 62 | 73 |
| 50 % | 3,68 | 2,52 | 2,68 | 68 | 73 |
| 75 % | 3,79 | 3,48 | 3,25 | 92 | 86 |
| 100 % | 4,02 | 4,20 | 4,12 | **104** | **102** |

*(OBSERVADO)* **CONFIRMADO: `Shoot` basta.** Y la fila de t=100 marca el límite: con los pesos igualados el
**defensa dispara más que el delantero** y la identidad desaparece. El gradiente existe porque el peso
existe.

Entre t=25 y t=50 hay un detalle que decide: a t=50 el hueco MID−DEF se cierra a **5 puntos** (73 contra 68)
y las dos posiciones dejan de distinguirse entre sí. A t=25 el hueco es de **13**.

## 7. Experimento B — `FindSpace` aislado

| t | F1 | F6 | F7 | DEF norm. | MID norm. |
|---|---|---|---|---|---|
| 0 % | 3,36 | 1,20 | 1,62 | 36 | 48 |
| 25 % | 3,40 | 1,25 | 1,57 | 37 | 46 |
| 50 % | 3,19 | 1,06 | 1,49 | 33 | 47 |
| 100 % | 2,96 | 1,00 | 1,51 | 34 | 51 |

*(OBSERVADO)* **`FindSpace` no es palanca.** La proporción se queda plana en ~35/48 aunque se igualen del
todo los pesos, y los tiros **absolutos bajan** (3,36 → 2,96): más tiempo recolocándose es menos tiempo
haciendo algo productivo.

Esto **confirma y explica** lo que A6 había observado sin interpretar: el DEF colocado arriba ya elegía
`FindSpace` tanto como el delantero. Ya encontraba el espacio. Lo que no hacía era **disparar al llegar**.

## 8. Experimento C — combinación

Solo procedía si A y B mostraban señal; B no la muestra. Punto de control, `Shoot` t=25 + `FindSpace` t=25:

| | `Shoot` t25 solo | combinación |
|---|---|---|
| F6 | 1,89 | 1,85 |
| F7 | 2,35 | 2,33 |

*(OBSERVADO)* Idéntico dentro del ruido: **la combinación no aporta nada**. La decisión es C del §20 —
relajación parcial, una sola acción.

## 9. Misma casilla, las dos semillas

| | t=0 s1/s2 | t=25 s1/s2 | t=35 s1/s2 | t=50 s1/s2 |
|---|---|---|---|---|
| DEF normalizado | 36 / 40 | **54 / 57** | 62 / 62 | 68 / 76 |
| MID normalizado | 48 / 47 | **67 / 69** | 73 / 73 | 73 / 83 |
| hueco MID−DEF | 12 / 7 | **13 / 12** | 11 / 11 | 5 / 7 |

*(OBSERVADO, CONFIRMADO en dos semillas)* El orden **FWD > MID > DEF** se conserva en todos los puntos
salvo t=100. t=25 es el punto con el hueco MID−DEF **más ancho** de todo el barrido relajado: la identidad
de las tres posiciones es más nítida ahí que en el propio baseline (12/7).

## 10. F1 contra F3 (dos delanteros)

| t | F3 tiros s1/s2 | F3 victorias | F1 victorias | ventaja de F3 |
|---|---|---|---|---|
| 0 % | 5,28 / 5,28 | 70,5 / 69,0 | 42,5 / 53,5 | **+28,0 / +15,5** |
| 25 % | 5,32 / 5,29 | 69,5 / 69,5 | 46,5 / 54,0 | **+23,0 / +15,5** |
| 35 % | 5,33 / 5,29 | 70,5 / 70,0 | 51,5 / 53,5 | +19,0 / +16,5 |
| 50 % | 5,48 / 5,32 | 67,0 / 73,0 | 49,5 / 55,5 | +17,5 / +17,5 |

*(OBSERVADO)* La ventaja se reduce un poco y **no desaparece**: relajar `Shoot` **no** resuelve la
dominancia de F3. Es un problema independiente —alinear dos delanteros naturales sigue valiendo ~20 puntos
de victoria— y esta auditoría no lo toca. Lo que sí muestran los datos es que la ventaja de F3 **no** venía
de que MID/DEF fueran inútiles arriba: sigue ahí cuando dejan de serlo. *(INFERIDO)*

## 11. Emergencia por lesión del delantero

Victorias contra el mismo rival. A = replegarse (E3), B = DEF a la punta (F6), C = MID a la punta (F7).

| t | A replegarse s1/s2 | B DEF arriba | C MID arriba | ¿reconvertir es razonable? |
|---|---|---|---|---|
| 0 % | **46,5 / 41,5** | 15,0 / 18,0 | 25,0 / 24,0 | **no**: replegarse gana por 20 puntos |
| 25 % | 42,0 / 38,0 | 28,5 / 33,5 | 33,5 / 37,5 | **casi**: −8,5 / −0,5 |
| 35 % | 41,0 / 38,5 | 28,5 / 35,5 | 39,0 / 40,0 | **sí**: −2,0 / **+1,5** |
| 50 % | 43,5 / 40,0 | 35,5 / 36,5 | 45,5 / 46,5 | sí, pero ya **domina** |

*(OBSERVADO)* Este es el criterio que más se mueve. A t=0 reconvertir está **dominado**. A t=35 las dos
opciones quedan **empatadas** (−2,0 y +1,5), que es exactamente el «coste, pero decisión razonable» del §12.
A t=50 reconvertir pasa a ser mejor y entonces replegarse deja de tener sentido: se ha cambiado una opción
dominada por otra.

**A t=25 la reconversión con un centrocampista cuesta 8,5 puntos en una semilla y 0,5 en la otra.** Sigue
siendo peor de media, pero ya no está dominada: es una apuesta, no un error.

## 12. Formaciones extremas

| t | E1 seis DEF s1/s2 | E2 seis FWD s1/s2 | F1 de referencia |
|---|---|---|---|
| 0 % | 15,5 / 16,5 | 48,5 / 50,0 | 42,5 / 53,5 |
| 25 % | 26,5 / 18,5 | **51,0 / 47,5** | 46,5 / 54,0 |
| 35 % | 31,0 / 23,0 | 51,0 / 48,0 | 51,5 / 53,5 |
| 50 % | 32,0 / 27,0 | 52,0 / 46,5 | 49,5 / 55,5 |

*(OBSERVADO)* **La dominancia de los seis delanteros NO se agrava**: E2 se queda plano entre 46,5 y 52 en
todo el barrido, siempre en el entorno de la formación equilibrada. La condición de parada del §13 no se
dispara.

Lo que sí cambia es E1: seis defensas pasa de 15,5 a 32,0 puntos de victoria. Sigue claramente por debajo de
F1 —nunca se acerca—, pero amontonar defensas deja de ser un suicidio. Es la consecuencia esperada de que un
defensa pueda rematar.

## 13. Seguridad: lote real y 43 puertas

Con `data/balance/reference.json`, 1.000 partidos por semilla. **Ninguna de estas cifras fue objetivo.**

| | baseline s1/s2 | **t=25** s1/s2 | t=35 s1/s2 | banda |
|---|---|---|---|---|
| `shotsPerMatch` | 7,36 / 7,25 | 7,60 / 7,49 ✅ | 7,70 / 7,59 ✅ | 7-15 |
| `tacklesPerMatch` | 12,80 / 10,33 | 12,74 / 10,30 ✅ | 12,76 / 10,15 ✅ | 6-14 |
| `injuriesPerMatch` | 0,87 / 0,52 | 0,85 / 0,51 ✅ | 0,87 / 0,51 ✅ | 0,3-0,9 |
| `possessionChanges` | 20,13 / 21,93 | 20,21 / 22,19 ✅ | 20,15 / 22,05 ✅ | 12-28 |
| `passChainAvgLength` | 2,03 / 2,10 | 2,02 / 2,09 ✅ | 2,02 / 2,09 ✅ | 2-4 |
| `ballThirdMaxShare` | 47,14 / 48,04 | 47,60 / 48,30 ✅ | 47,70 / 48,38 ✅ | ≤52 |
| `goalsPerMatch` | 2,31 / 2,17 | 2,38 / 2,21 | 2,38 / 2,25 | INFO |

**Puertas:**

| | baseline | **t=25** | t=35 |
|---|---|---|---|
| rojas | 4 | **3** | 4 |
| `elf_brawler` | 50,62 | **47,71** | 49,79 |
| `passChain` spread | 1,09 | 1,08 | 1,06 |
| `EquippingAGoodBuild` | **roja** (1,9 pts) | **verde** | verde |
| curva de jefes (ADR 0033) | verde | **verde** | **roja**: `the_hunt_incoherent` 18,01 (techo 17,5) |

*(OBSERVADO)* **t=25 es estrictamente mejor que el baseline**: arregla la puerta de equipamiento, acerca
`elf_brawler` a su banda (−2,9 puntos) y no rompe nada. **t=35 no**: gana en reconversión pero rompe la
curva de jefes de la ADR 0033, que es la métrica de la fase 2, porque una build incoherente empieza a ganar
demasiado a un jefe. Es la misma erosión de diferenciación, apenas medio punto por encima del techo, pero en
la puerta que menos conviene.

## 14. Análisis causal

*(INFERIDO, apoyado en §6, §7 y §8)*

1. **Lo que cambia por `Shoot`**: un DEF/MID colocado arriba, **con el balón y cerca de la portería**, deja
   de pasar y remata. Todo el efecto medido viene de ahí. La prueba es que `FindSpace` igualado del todo no
   mueve la proporción (§7) y que la combinación no suma (§8).
2. **Lo que no cambia por `Shoot`**: dónde se coloca, cuánto corre y qué hace sin balón. `CoverSpace`,
   `MarkOpponent`, `Tackle` y `Retreat` conservan su gradiente invertido intacto — no se tocan. Un defensa
   arriba sigue cubriendo espacio mucho más que un delantero. **La identidad defensiva queda entera.**
3. **Por qué `Shoot` y no `FindSpace`**: la utilidad es una competición situacional. `FindSpace` compite sin
   balón contra `CoverSpace`, donde el defensa ya ganaba. `Shoot` compite con balón contra `ShortPass`
   (500 para un DEF), y con 77 de base el defensa **nunca** ganaba esa comparación. El abanico 5,00× de
   `Shoot` no era un matiz: era un veto.
4. **Lo que no arregla**: la ventaja de F3 (§10) y la tolerancia a seis delanteros (§12) sobreviven a toda
   la relajación. **No son consecuencia de la especialización ofensiva** y necesitan su propia investigación.

## 15. Decisión

**C — Relajación parcial** (§20).

> Subir **únicamente `Shoot`** y **únicamente de `Defender` y `Midfielder`**, una cuarta parte del camino
> hasta el valor del delantero: **DEF 77 → 154** y **MID 188 → 237**.

Lo que lo justifica, todo en dos semillas:

- reconversión de **100/48/38** a **100/68/55**, dentro de la zona que A6 señaló como deseable (§9);
- el hueco MID−DEF se **ensancha** (12/7 → 13/12): las tres posiciones se distinguen mejor que antes;
- reconvertir deja de estar dominado: de −20 puntos a −8,5 / −0,5 contra replegarse (§11);
- **todos** los guardarraíles en verde en las dos semillas, sin que ninguno fuera objetivo (§13);
- las puertas **bajan de 4 rojas a 3**, arreglando la de equipamiento (§13);
- la identidad defensiva no se toca en absoluto (§14.2).

**Por qué 25 y no 35**, que reconvierte mejor: 35 rompe la curva de jefes de la ADR 0033 por medio punto.
Ante la duda, la fase 2 pesa más que medio punto de reconversión.

**Por qué no más de 50**: a t=100 el defensa dispara **más** que el delantero (§6). El peso es lo único que
sostiene la identidad; vaciarlo la borra.

## 16. Propuesta de implementación

**No aplicada.** Si el revisor la acepta:

- **Cambio de datos, dos números** en `data/ai/weights.json`: `base.Defender.Shoot` 77 → 154 y
  `base.Midfielder.Shoot` 188 → 237.
- **Nada de código.** Ni fórmula nueva, ni multiplicador posicional, ni sistema de adecuación, ni
  restricciones de composición, ni cambios de zona.
- Necesita **ADR** por RT-057: mueve métricas medidas (`shotsPerMatch` 7,36 → 7,60) y cambia una propiedad
  de diseño declarada (cuánto vale reconvertir).
- Antes de commitear: las 43 puertas en una invocación, `--full-runs 240` en las dos semillas y RT-024.

## 17. Lo que NO debe hacerse

- **No tocar `FindSpace`**: no es palanca y empeora los tiros absolutos (§7).
- **No tocar acciones defensivas** (`CoverSpace`, `MarkOpponent`, `Tackle`, `Retreat`, `ChaseBall`): son las
  que sostienen la identidad y no hacen falta para este arreglo.
- **No igualar los pesos** (t ≥ 75): destruye la identidad; a t=100 el defensa remata más que el delantero.
- **No pasar de t=35** sin volver a medir la curva de jefes.
- **No introducir restricciones de composición** para resolver F3 o los seis delanteros: son problemas
  independientes y esta relajación ni los causa ni los agrava (§10, §12).
- **No usar la tasa de victoria del instrumento como criterio fino**: F1 oscila 42,5/53,5 entre semillas.
  **Los tiros normalizados son la señal robusta** (36/40, 54/57, 67/69).
- **No mezclar esto con `ChaseBall pen=50`** (§19 del encargo): sigue bloqueado y se medirá aparte, sobre el
  baseline que resulte.

## 18. Estado final del árbol

`data/ai/weights.json` restaurado: el **único** diff vivo es `shootAnglePenaltyPerRow: 50 → 36`, el del
trabajo de siete filas, anterior a esta auditoría. `Sim/Engine/` y `data/sim/` sin tocar. Instrumento
`Sim.Tests/Analysis/_A7.cs` borrado. RT-024 en verde (4/4). El lote de 1.000 partidos con semilla 1
reproduce exactamente `shotsPerMatch=7,36 · tacklesPerMatch=12,80 · injuriesPerMatch=0,87 ·
possessionChanges=20,13 · passChainAvgLength=2,03 · goalsPerMatch=2,31 · ballThirdMaxShare=47,14`.
