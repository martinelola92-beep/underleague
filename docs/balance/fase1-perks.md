# Fase 1: medidas de perks, builds y atributos (paquete I)

> **Estos números corresponden al motor anterior a las ADR 0020 (cuerpos con volumen, separación blanda y
> empuje), 0021 (adyacencia resuelta antes del partido y proximidad dinámica) y 0022 (comportamiento sin
> balón: contraste táctico y búsqueda de espacio).** Se publican como **línea base**: cuando esas tres
> cosas estén implementadas hay que
> volver a medir exactamente lo mismo y comparar. Ninguna conclusión de aquí sobre el valor de una
> formación o de una sinergia posicional sobrevivirá intacta al cambio; las tablas de valor por atributo y
> por canal de efecto sí deberían moverse poco, y son la entrada principal del rediseño del catálogo.
>
> Estado del repositorio al medir: catálogo de perks y builds **del paquete G sin retocar**, salvo la
> corrección de `bone_breaker` (§6.1). Motor de fase 0 + paquete F. Semilla base 1 en todas las medidas.

## 1. Resumen ejecutivo

1. **La medida de builds del paquete H no medía builds.** Generaba una sola plantilla por build, así que la
   tasa de victoria dependía sobre todo de qué jugadores le hubieran tocado al generador: la misma build
   contra su misma referencia da entre el **16,5% y el 59,5%** según la plantilla (sd 14,9 puntos). Todos
   los números de fase 1 anteriores a esta corrección hay que descartarlos, incluidos los que motivaron
   este encargo.
2. **Con la medida corregida, la conclusión se invierte en cuatro de las nueve builds.** `human_wall` no
   perdía (37,5%): gana el **62,7%**. `elf_brawler` no ganaba (70,6%): queda en el **48,8%**.
3. **Lo que de verdad hunde a `elf_tiki_taka` (32,9%) y a `orc_violence` (37,3%) es su alineación, no sus
   perks.** Sus `lineup` apiñados pierden **21 y 15 puntos** por sí solos, sin un perk puesto; sus perks,
   con la alineación por defecto, suman **+4,6 y +0,5**. Apiñarse para conseguir adyacencia con radio 1 es
   una trampa mecánica: es la motivación medida de las ADR 0020, 0021 y 0022.
4. **`berserker` con 0 activaciones en 197 partidos no era un defecto del motor**: era el mismo artefacto de
   la plantilla única. Con la medida corregida se activa en el **33% (orc_violence)** y el **94%
   (elf_brawler)** de los partidos.
5. **Sí hay dos perks muertos de verdad**: `bloodlust`, cuya condición no puede ser cierta en fase 1
   (0 activaciones en 500 partidos con cinco portadores), y `guardian_angel`, que se activa siempre y no
   cambia nada porque `severeInjury` no tiene consecuencia. Ninguno es un fallo del motor: son datos.
6. **`bone_breaker` aplicaba su efecto al jugador equivocado** (corregido): subía la probabilidad de
   *lesionar* del rival al que quería romper.
7. **Los atributos valen poco y los canales de probabilidad valen mucho.** +10 de un atributo a los diez
   jugadores mueve entre 0,3 y 4,1 puntos de tasa de victoria; un solo perk sobre `save`, `leash`,
   `injure`, `intercept` o `shotOnTarget` mueve entre 5 y 9. El pase y el regate están **saturados**: un
   bonus se pierde entero, un castigo del mismo tamaño sí muerde.
8. **La velocidad casi no existe como atributo** (+0,3 puntos en humanos con +10 a toda la plantilla).

## 2. Metodología

Toda medida de este documento compara **la misma plantilla generada consigo misma**: los dos equipos de un
partido salen del mismo índice de generación (mismos diez jugadores, mismos atributos, mismos rasgos) y lo
único que cambia entre ellos es lo que se está midiendo (perks, alineación o un delta de atributo). Cada
emparejamiento se juega en las cuatro combinaciones de (local, visitante) × (ids de jugador bajos, ids
altos), y cada celda se promedia sobre 80-150 plantillas distintas.

Sin esas tres cosas, la medida no dice nada:

| Fuente de error | Magnitud medida |
|---|---|
| Plantilla única por build | tasa de victoria de la **misma** build entre 16,5% y 59,5%; sd 14,9 puntos sobre 20 plantillas × 200 partidos |
| Reparto de ids fijo (desempates por id ascendente) | +2,1 puntos al equipo de ids bajos con plantillas idénticas (52,9% → 50,8%, 3.000 partidos). Por raza, 5.000 partidos: Human 53,1→50,7; Orc 52,2→50,5; Elf 52,0→49,9 |
| Cadena de pases medida sobre el partido completo | no distingue a las dos builds; ahora `MatchReport.PassChainsByTeam` |

Error típico: 1,1 puntos con 2.000 partidos por celda, 1,25 con 1.600, 2,2 con 500.

Comandos:

```bash
# matriz de builds contra la referencia sin perks de su raza (una llamada por raza)
dotnet run --project Balance -c Release -- --builds elf_brawler,elf_glass,elf_tiki_taka \
    --vs elf_none --runs 8000 --home-away --rosters 80 --out out/f1_Elf
# campaña con progresión
dotnet run --project Balance -c Release -- --builds all --campaign 8 --runs 300 --home-away --out out/f1camp
```

## 3. Matriz final de builds (línea base del motor actual)

Cada build contra la referencia sin perks de su raza, plantillas emparejadas, 2.000-2.667 partidos por
celda, semilla 1. `winRate` es de la build; `lesiones` son las que **causa** al rival por partido; `cadena`
es la cadena media de pases **de la build**.

| build | rival | partidos | winRate | §8 pide | ¿cumple? | goles a favor | goles en contra | lesiones causadas | entradas | cadena | activaciones/partido | winRate del paquete H (16-100 partidos) |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `human_wall` | `human_none` | 2000 | **62,70** | ≥ 58 | **sí** | 1,29 | 0,88 | 0,21 | 5,3 | 3,92 | 4,10 | 37,5 |
| `human_scattered` | `human_none` | 2000 | **61,25** | ≤ 45 | no | 1,57 | 1,19 | 0,21 | 5,3 | 3,05 | 4,00 | 58,8 |
| `elf_glass` | `elf_none` | 2667 | 52,23 | — | — | 1,35 | 1,26 | 0,13 | 4,0 | 3,68 | 3,07 | — |
| `orc_mob` | `orc_none` | 2667 | **51,44** | ≥ 58 | no | 1,08 | 1,02 | 0,43 | 8,0 | 4,64 | 1,68 | 62,5 |
| `human_counter` | `human_none` | 2000 | **50,85** | ≥ 58 | no | 1,23 | 1,22 | 0,20 | 5,4 | 3,92 | 27,33 | 64,7 |
| `human_random` | `human_none` | 2000 | **50,80** | 40-60 | **sí** | 1,18 | 1,18 | 0,20 | 5,5 | 3,83 | 10,46 | 47,1 |
| `elf_brawler` | `elf_none` | 2667 | **48,78** | ≤ 45 | no | 1,27 | 1,29 | 0,18 | 4,0 | 3,74 | 8,52 | 70,6 |
| `orc_misplaced` | `orc_none` | 2667 | **46,94** | ≤ 45 | casi | 1,00 | 1,10 | 0,42 | 8,2 | 4,63 | 3,30 | 56,3 |
| `orc_violence` | `orc_none` | 2666 | **37,32** | ≥ 58 | no | 0,39 | 0,65 | 0,28 | 4,6 | 30,74 | 5,76 | 31,0 |
| `elf_tiki_taka` | `elf_none` | 2666 | **32,86** | ≥ 58 | no | 0,35 | 0,69 | 0,07 | 2,0 | 38,28 | 9,20 | 12,0 |

Las dos cadenas de 30 y 38 pases no son una virtud: son partidos degenerados. `elf_tiki_taka` y
`orc_violence` juegan con el bloque apiñado, casi no llegan al área (0,35 y 0,39 goles por partido frente a
1,2-1,3 del resto) y la posesión se les va en pases cortos que no avanzan.

Criterio de salida de §8 con estos números: **1 de 5 coherentes** cumple, **0 de 3 malas** cumplen, la
aleatoria sí. La fase 1 **no cumple su criterio de salida** con el motor actual.

## 4. Diagnóstico A: formación contra perks

Misma build medida con su `lineup` y con `Lineup.Default`, y con y sin sus perks. Rival: la referencia de
su raza con `Lineup.Default` y sin perks. 1.200 partidos por celda, plantillas emparejadas.

| build | `lineup` + perks | solo `lineup` | solo perks | control | **coste de la formación** | **valor de los perks** |
|---|---|---|---|---|---|---|
| `elf_tiki_taka` | 30,0 | 28,1 | 56,5 | 51,9 | **−23,8** | **+4,6** |
| `orc_violence` | 36,2 | 36,1 | 52,7 | 52,2 | **−16,1** | **+0,5** |
| `human_wall` | 63,5 | 50,0 | 64,7 | 50,8 | **−0,8** | **+13,9** |
| `human_scattered` | 60,7 | 52,0 | 58,3 | 50,8 | +1,2 | +7,5 |
| `elf_glass` | 54,2 | — | 54,2 | 51,9 | (sin lineup propio) | +2,3 |
| `orc_mob` | 53,4 | — | 53,4 | 52,2 | (sin lineup propio) | +1,2 |
| `human_random` | 51,7 | — | 51,7 | 50,8 | (sin lineup propio) | +0,9 |
| `human_counter` | 50,8 | — | 50,8 | 50,8 | (sin lineup propio) | **0,0** |
| `orc_misplaced` | 50,6 | — | 50,6 | 52,2 | (sin lineup propio) | −1,6 |
| `elf_brawler` | 49,8 | — | 49,8 | 51,9 | (sin lineup propio) | −2,1 |

**La hipótesis del orquestador se confirma con margen.** Las tres builds que perdían son exactamente las
que traían `lineup` propio para lograr adyacencia, y en dos de ellas la formación cuesta 16 y 24 puntos
mientras los perks aportan 0,5 y 4,6. `human_wall` es la excepción que lo prueba: su `lineup` apenas se
aparta del de por defecto (mueve un defensa de la fila 1 a la 2), no cuesta nada, y sus perks son los
mejores del catálogo (+13,9).

`human_scattered`, diseñada como mala, gana porque su formación es **la más ancha** (+1,2) y porque sus
perks, aun cayendo en su rama `elseEffects`, son **positivos** (+7,5): dos `anchor` (+1 de correa a cada
defensa) valen mucho más de lo que cuesta el −3 de fuerza de `shield_wall` fallido.

### 4.1 Por qué apiñarse cuesta tanto

Formaciones sin perks contra `Lineup.Default` sin perks, humanos de calidad 50, 2.000 partidos por celda:

| formación | winRate | goles a favor | goles en contra |
|---|---|---|---|
| `Lineup.Default` (control) | 51,0 | 1,23 | 1,20 |
| `mid_adelantado` — un centrocampista a la columna 5 | 52,7 | 1,26 | 1,19 |
| `scattered` — defensas en las filas 0 y 4, delantero en la columna 7 | 52,2 | 1,48 | 1,40 |
| `def_juntos` — defensas en las filas 1 y 2 | 51,1 | 1,24 | 1,20 |
| `wall` (la de `human_wall`) | 50,6 | 1,21 | 1,21 |
| **`ancha_1_3` — centro del campo en las filas 1, 2 y 3** | **34,8** | **0,42** | 0,68 |
| `violence` (la de `orc_violence`) | 34,1 | 0,38 | 0,68 |
| `tiki_taka` (la de `elf_tiki_taka`) | 33,1 | 0,31 | 0,66 |

`ancha_1_3` es el hallazgo importante: es **el cambio mínimo** que hace adyacentes a dos centrocampistas
con radio 1 —mover el centro del campo de las filas 0, 2, 4 a las filas 1, 2, 3— y cuesta **16 puntos**. No
hay ningún bonus en el catálogo que compense eso; el mayor efecto positivo medido de un solo perk es +9.

La causa mecánica es que el motor solo simula la mitad mala de concentrarse: se pierde cobertura (el rival
juega por fuera sin oposición) y no se gana nada, porque los jugadores son puntos sin volumen que no se
estorban ni se bloquean. Es exactamente el problema que ataca la ADR 0020.

## 5. Diagnóstico B: valor marginal por atributo

Dos equipos idénticos salvo **+10 en un atributo a los diez jugadores**, calidad 50, plantillas
emparejadas, 3.000 partidos por celda. La columna Δ es la diferencia contra el control de esa misma raza.

| atributo (+10 a la plantilla) | Human | Δ | Orc | Δ | Elf | Δ |
|---|---|---|---|---|---|---|
| control (sin cambio) | 50,8 | — | 51,9 | — | 50,7 | — |
| **técnica** | 54,9 | **+4,1** | 52,4 | +0,5 | 54,8 | **+4,1** |
| **correa** | 53,9 | **+3,1** | 58,6 | **+6,7** | 52,8 | +2,1 |
| **resistencia** | 53,4 | **+2,6** | 50,8 | −1,1 | 52,1 | +1,4 |
| **fuerza** | 53,0 | **+2,2** | 52,4 | +0,5 | 51,6 | +0,9 |
| **velocidad** | 51,1 | **+0,3** | 50,7 | −1,2 | 53,3 | +2,6 |
| `glass_cannon` (+10 técnica, −10 resistencia) | 52,6 | +1,8 | 52,0 | +0,1 | 52,8 | +2,1 |

Medida independiente con 5.000 partidos por celda (humanos): fuerza +2,4 · velocidad +0,4 · técnica +3,4 ·
resistencia +2,1 · correa +2,3 · `glass_cannon` +1,8. Las dos series coinciden dentro de ±0,8 puntos.

Lecturas:

- **Un atributo entero vale menos que un perk decente.** +10 al mejor atributo de los diez jugadores vale
  4,1 puntos; un solo `goalkeeper_wall` en el portero vale 6,6. Con los márgenes de §7 (±3..±10 a un
  jugador), un perk de atributo aporta del orden de **0,2-0,4 puntos**. Ninguna build puede llegar al 58%
  apilando perks de atributo: haría falta el equivalente a +50 de técnica en toda la plantilla.
- **La velocidad es el atributo muerto**, no la resistencia. En humanos vale +0,3 y en orcos −1,2 (dentro
  del ruido, pero desde luego no positivo). Solo entra en el desplazamiento por tick, y el desplazamiento
  casi nunca decide una jugada: no interviene en ninguna resolución (ni en llegar antes al balón suelto, ni
  en la carrera del pase, ni en el duelo de regate salvo como defensor).
- **La hipótesis 2 del orquestador es falsa: la resistencia no vale "casi nada".** Vale +2,6 en humanos,
  más que la fuerza (+2,2) y ocho veces más que la velocidad. Y `glass_cannon` **ya es un trueque**: +1,8
  frente a los +4,1 de la técnica sola, o sea, cambiar resistencia por técnica se queda con menos de la
  mitad de la ganancia. Lo que hace que `elf_brawler` pareciera ganar no era `glass_cannon`: era la
  plantilla que le tocó.
- **La correa es el atributo más desequilibrado y el más dependiente de la raza**: +6,7 en orcos (que parten
  con la correa más corta, sesgo −1) frente a +2,1 en elfos (sesgo +1). Un solo escalón entero de casilla
  vale entre 3,5 y 4 puntos (§6.2). Es la palanca que el paquete E ya había señalado como la más fuerte del
  motor.
- **Las razas responden a atributos distintos**, lo que es buena señal de diseño: los orcos solo mejoran con
  correa (llegan tarde a todo), los elfos con técnica y velocidad, los humanos con técnica y correa.

### 5.1 Recomendación sobre `tuning.json` (no aplicada)

No se ha tocado `data/sim/tuning.json`. Con los números de arriba, lo que recomendaría, por orden:

1. **Dar a la velocidad una resolución propia.** Hoy `movement.speedCellsPerTickMilliPer99 = 28` sobre una
   base de 131 es todo lo que hace la velocidad: como mucho un 21% más de desplazamiento entre 1 y 99. Subir
   ese factor solo hace que todo el mundo corra más. Lo que le falta es entrar en algún dado: la carrera por
   el balón suelto (hoy la gana el más cercano, empate por id), la intercepción (hoy solo técnica) o la
   recuperación tras perder el duelo. Es un cambio de mecánica, no de `tuning.json`, y encaja con el
   rediseño de la IA sin balón por roles.
2. **No subir el peso de la resistencia**: ya vale más que la fuerza. Si el objetivo es que `glass_cannon`
   duela, el camino barato es dárselo en `data/perks/glass_cannon.json` (añadirle `injury +800`, dentro de
   los márgenes de §7), no mover la fatiga de todo el juego. Medido: `injury +1500` a toda la plantilla vale
   **−11,1 puntos**, así que ese canal tiene recorrido de sobra.
3. **Vigilar la correa**: es la palanca dominante y se mueve a saltos de casilla entera
   (`leash.minCells + leash*cellsPer99/99`). Ya está anotado en D-20. Cualquier perk que dé `+1 de correa`
   está dando entre 3,5 y 4 puntos de tasa de victoria, más que cualquier otro efecto de un solo jugador.

## 6. Diagnóstico C: valor por canal de efecto

Un perk artificial con un solo efecto, humanos de calidad 50, plantillas emparejadas, 2.000 partidos por
celda, control 51,0. Es la tabla que hay que mirar al escribir un perk: dice qué canales tienen recorrido.

| efecto | portadores | winRate | Δ |
|---|---|---|---|
| `intercept +1500` | 7 titulares | 75,0 | **+24,0** |
| `injury +1500` (probabilidad de **ser** lesionado) | 7 | 39,9 | **−11,1** |
| `leash −1` casilla | 7 | 41,3 | **−9,7** |
| `injure +1500` en la entrada | 6 de campo | 60,0 | **+9,0** |
| `leash +1` casilla | 7 | 59,1 | **+8,1** |
| `leash −1` casilla | 2 defensas | 43,4 | **−7,6** |
| `dribble −1500` | 4 de ataque | 43,6 | **−7,4** |
| `dribble +1500` | 4 de ataque | 58,2 | **+7,2** |
| `leash +1` casilla | 2 defensas | 58,1 | **+7,1** |
| `save +800` | portero | 57,6 | **+6,6** |
| `intercept +1500` | 2 defensas | 57,2 | **+6,2** |
| `shotOnTarget +1500` | 4 de ataque | 57,1 | **+6,1** |
| `save −800` | portero | 45,2 | **−5,8** |
| `shotOnTarget +1500` | delantero | 56,2 | **+5,2** |
| `technique +10` | 7 | 54,7 | +3,7 |
| `tackle +1500` | 7 | 54,5 | +3,5 |
| `foul +1500` | 6 | 48,0 | −3,0 |
| `card +1500` | 6 | 48,1 | −2,9 |
| `strength +10` | 7 | 53,1 | +2,1 |
| `pass −1500` | 7 | 49,0 | −2,0 |
| **`pass +1500`** | 7 | 51,4 | **+0,4** |
| **`severeInjury +1500`** | 7 | 51,0 | **0,0** |

### 6.1 Conclusiones de diseño sobre los canales

- **Las probabilidades saturadas solo funcionan hacia abajo.** El pase parte de 9.200/10.000 y el regate de
  8.400: un `+1500` de pase vale 0,4 puntos y un `−1500` vale −2,0; en regate, `+1500` vale +7,2 y `−1500`
  vale −7,4 porque el regate tiene más recorrido. Cualquier perk "mejora tus pases" es, hoy, un perk vacío
  — y ahí están `one_touch` (+800 de pase) y `matador` (+1200 de regate condicionado) del catálogo actual.
- **`intercept` está desequilibrado por dos órdenes de magnitud.** La base es 250/10.000 **por rival y por
  tick de vuelo**, así que +1.500 no es "+15%", es multiplicar por siete la probabilidad de robar cada pase
  del rival. Ningún perk debería tocar ese canal con los valores de §7 sin bajar antes la escala.
- **`severeInjury` no hace nada** (no hay muertes en fase 1 y "grave" solo cambia el `Detail`). Todo perk
  sobre ese canal es letra muerta hasta la fase 3. Anotado en D-24.
- **El canal más expresivo del juego es `injure`**: +1500 en la entrada multiplica por 3,6 las lesiones
  causadas (0,216 → 0,782 por partido) y vale +9 puntos. Es la palanca natural de la identidad "carnicería
  administrada", y la que debería llevar la familia de violencia.
- **`bone_breaker` estaba escrito al revés** (corregido en este paquete): aplicaba `injure` —cuyo sujeto es
  **quien entra**— sobre `opponent`, es decir, hacía más peligroso al rival al que quería romper. Ahora el
  efecto va sobre `actor`. Es el único cambio de `data/perks/` de este paquete.

## 7. Diagnóstico D: activaciones por perk

Una fila por (perk, build) que lo asigna, sobre los mismos lotes de §3. `noDeadPerks` de §8 pide ≥ 1%.

| perk | build | partidos | % de partidos con activación |
|---|---|---|---|
| `anchor` | `human_scattered`, `human_wall` | 2000 | 100,00 |
| `berserker` | `elf_brawler` | 2667 | **94,38** |
| `berserker` | `orc_violence` | 2666 | **33,46** |
| `bloodline` | `orc_mob` / `orc_violence` | 2667 / 2666 | 10,65 / 5,06 |
| **`bone_breaker`** | `orc_violence` | 2666 | **0,00** |
| `bookworm` | `human_random` | 2000 | 94,85 |
| `counter_punch` | `human_counter` | 2000 | 87,15 |
| `enforcer` | `orc_mob` / `orc_violence` | 2667 / 2666 | 33,03 / 20,29 |
| `glass_cannon` | `elf_brawler`, `elf_glass` | 2667 | 100,00 |
| `goalkeeper_wall` | `human_wall` | 2000 | 100,00 |
| `guardian_angel` | `elf_glass` | 2667 | 100,00 (sin efecto, §6) |
| `heavy_boots` | `orc_mob`, `orc_violence` | 2667 / 2666 | 100,00 |
| `innocent_face` | `elf_glass` / `human_wall` | 2667 / 2000 | 4,12 / 3,65 |
| `iron_lungs_plus` | `human_counter` | 2000 | 100,00 |
| `lone_wolf` | `human_random` | 2000 | 100,00 |
| **`lucky_charm`** | `human_wall` | 2000 | **0,05** |
| **`matador`** | `elf_glass`, `elf_tiki_taka` | 2667 / 2666 | **0,00** |
| **`mob_lawyer`** | `human_random` | 2000 | **0,00** |
| `mob_lawyer` | `orc_mob` | 2667 | 4,01 |
| `one_touch` | `elf_tiki_taka` | 2666 | 99,17 |
| **`one_touch`** | `orc_misplaced` | 2667 | **0,00** |
| `playmaker` | `elf_glass`, `elf_tiki_taka` | 2667 / 2666 | 100,00 |
| `poacher` | `elf_tiki_taka` / `human_counter` / `orc_violence` | — | 23,63 / 64,10 / 24,53 |
| `shield_wall` | `human_scattered`, `human_wall`, `orc_violence` | — | 100,00 |
| `showboat` | `elf_tiki_taka` / `orc_misplaced` | 2666 / 2667 | 44,56 / 71,73 |
| `silk_touch` | `elf_tiki_taka`, `human_random`, `orc_misplaced` | — | 100,00 |
| `sprinter` | `human_counter` | 2000 | 100,00 |
| `street_fighter` | `orc_mob` | 2667 | 7,61 |
| `sweeper` | `elf_glass` / `elf_tiki_taka` / `human_counter` / `human_random` / `human_wall` | — | 2,51 / 2,18 / 8,90 / **1,20** / 5,95 |
| `target_man` | `elf_brawler` / `human_random` / `orc_violence` | — | 95,95 / 95,50 / 67,07 |
| `warpath` | `orc_violence` | 2666 | 100,00 |
| **`bloodlust`** | **ninguna build lo asigna** | — | 0,00 con 5 portadores en 500 partidos |
| **`veteran`** | **ninguna build lo asigna** | — | 100,00 con 7 portadores en 500 partidos |

### 7.1 Qué es un defecto y qué no

- **`berserker`: no es un defecto.** Se publica, se evalúa y se activa correctamente. El 0 de 197 partidos
  del paquete H venía de que esos 197 partidos usaban **una sola plantilla**, en la que el portador del
  perk (un defensa concreto) nunca elegía `Tackle`. Con 2.666 partidos sobre 80 plantillas se activa en el
  33% de ellos. **No se ha tocado nada del motor por esto.**
- **`bloodlust`: perk muerto por construcción, pero por su condición, no por el motor.** Su condición es
  `hasTag(actor,'Brute') && bias() < 0` y `bias()` vale 0 en todos los partidos: el árbitro de `/Balance`
  es neutro con sesgo inicial 0 y **ningún perk del catálogo usa `modifyBias`**, que es la única forma de
  moverlo. Medido: 0 activaciones en 500 partidos con cinco orcos portándolo, frente a 100% de `veteran` en
  el mismo lote. Se deja como está (D-23): al implementar el criterio del árbitro (RF-060, fase 3) la
  condición vuelve a tener sentido.
- **`guardian_angel`: se activa siempre y no hace nada.** `severeInjury` no tiene consecuencia en fase 1
  (D-24). Es un perk muerto en efecto, no en activación, y `noDeadPerks` no puede detectarlo.
- **Condiciones falsas por el emparejamiento, que son correctas por diseño**: `matador`
  (`hasTag(opponent,'Brute')` jugando contra elfos), `bone_breaker` (`hasTag(opponent,'Fine')` contra
  orcos), `one_touch` en `orc_misplaced` (`adjacent(actor,'Fine')` en un equipo de orcos: es justo lo que
  esa build quiere demostrar). Ninguno se ha tocado. Sí conviene saber que **una puerta que solo enfrenta a
  cada build con su propia raza no puede ver nunca los perks condicionados a la raza del rival**.
- **Activaciones raras por el suceso, no por la condición**: `lucky_charm` (0,05%) y `mob_lawyer` en
  `human_random` (0%) esperan a que **el portador concreto** sufra una lesión o cometa una falta en la
  turba. Con 0,2 lesiones por partido repartidas entre catorce jugadores, eso es ~1,5% por jugador. Si se
  quiere que un rompe-reglas se note, tiene que tener `scope: team` o ir en un portador que sí participe
  del suceso.
- **Dos perks del catálogo (`bloodlust`, `veteran`) no están asignados en ninguna build**, así que
  `noDeadPerks` es ciego a ellos. La puerta que se ha escrito comprueba explícitamente esa cobertura
  (`EveryCatalogPerkIsAssignedInSomeBuild`).

## 8. Campaña y progresión

`--builds all --campaign 8 --runs 300 --home-away`: 300 campañas por build de 8 partidos contra
`human_none` de calidad creciente (46, 48, … 60), arrastrando experiencia, niveles y contadores. 1.200
partidos por mitad.

| build | partidos 1-4 | partidos 5-8 | delta | §8 pide |
|---|---|---|---|---|
| `human_wall` (coherente) | 66,83 | 63,83 | −3,00 | ≥ −10 → **sí** |
| `human_counter` (coherente) | 55,33 | 52,83 | −2,50 | ≥ −10 → **sí** |
| `orc_mob` (coherente) | 53,92 | 49,17 | −4,75 | ≥ −10 → **sí** |
| `elf_tiki_taka` (coherente) | 36,50 | 34,92 | −1,58 | ≥ −10 → **sí** |
| `orc_violence` (coherente) | 36,67 | 32,50 | −4,17 | ≥ −10 → **sí** |
| `human_scattered` (mala) | 63,42 | 61,33 | −2,08 | ≤ −15 → **no** |
| `elf_brawler` (mala) | 54,50 | 50,67 | −3,83 | ≤ −15 → **no** |
| `orc_misplaced` (mala) | 49,83 | 48,25 | −1,58 | ≤ −15 → **no** |
| `human_random` | 54,58 | 51,33 | −3,25 | — |
| `human_none` (referencia) | 54,08 | 48,00 | −6,08 | — |
| `elf_none` (referencia) | 52,50 | 53,67 | +1,17 | — |
| `orc_none` (referencia) | 52,33 | 46,67 | −5,67 | — |

La primera mitad de `scalingRewardsGoodBuilds` **se cumple en las cinco builds coherentes**: ninguna cae más
de 4,75 puntos pese a que el rival sube 14 puntos de calidad. La segunda mitad **no es alcanzable**, y no
por el catálogo:

- En 8 partidos la plantilla propia acumula 800 de experiencia, o sea nivel 5, o sea **+8 a cada atributo**
  (`attributesPerLevel = 2`); el rival sube **+14 de calidad**. La diferencia neta es de −6 puntos de
  calidad, que con la pendiente medida (20 de calidad ≈ 20 puntos de tasa de victoria) da los −6 puntos que
  pierde `human_none`, que no lleva un solo perk.
- Para que una build mala cayera 15 puntos tendría que perder **9 puntos más que un equipo sin perks**, y
  no hay mecánica que la haga decaer: los perks incoherentes son igual de malos en el partido 1 que en el 8.
- Los perks que acumulan **no producen una curva visible** porque el canal que usan (atributos) es el más
  barato del motor: el escalado completo de `warpath` son +8 de fuerza a un jugador, que la tabla de §5
  tasa en **0,2 puntos** de tasa de victoria; `poacher` con el contador al máximo, +12 de técnica a un
  jugador, unos 0,4. Subirles el ritmo o el tope dentro de los márgenes de §7 no cambia el orden de
  magnitud: para que la campaña separe a las builds, los perks de escalado tienen que acumular sobre un
  canal con recorrido (correa, `save`, `injure`) o sobre **todo el equipo**, no sobre un atributo de un
  jugador. Recomendación anotada en D-28; no se ha aplicado.

### 8.1 RF-024: común de nivel 8 con perks frente a legendario de nivel bajo

Medido explícitamente, plantillas emparejadas, 2.000 partidos por celda. El común de nivel 8 lleva +14 a
cada atributo (7 subidas × 2) y **2 perks** por jugador (los slots de rareza común, RF-023); el legendario
lleva **4 perks** por jugador y el nivel que indica la fila.

| enfrentamiento | winRate del equipo común |
|---|---|
| común nivel 8 **con 2 perks** vs legendario **nivel 1** con 4 perks | **59,4%** |
| común nivel 8 **con 2 perks** vs legendario **nivel 2** con 4 perks | **57,8%** |
| común nivel 8 **con 2 perks** vs legendario **nivel 3** con 4 perks | **56,2%** |
| común nivel 8 **sin perks** vs legendario nivel 1 con 4 perks | 45,6% |
| común nivel 8 **sin perks** vs legendario nivel 8 con 4 perks | 34,9% |

**RF-024 se cumple**: un común de nivel máximo con buenos perks supera a un legendario de nivel bajo
(56-59%). Las dos últimas filas dan la lectura completa: el nivel **solo** no basta (45,6% sin perks), y a
igualdad de nivel la rareza —sus dos slots extra de perk— gana con claridad (34,9%). Es exactamente lo que
RF-023 pide: la rareza es techo de perks, no techo de nivel, y los perks son la moneda con la que un común
lo compensa.

## 9. Qué sinergias funcionan y cuáles no

| familia | veredicto | evidencia |
|---|---|---|
| **Bloque / muro** (`goalkeeper_wall`, `anchor`, `shield_wall`) | **funciona, y es lo mejor del catálogo** | `human_wall` gana +13,9 puntos solo con perks. `goalkeeper_wall` vale +6,6 él solo; dos `anchor`, +7,1 |
| **Violencia** (`heavy_boots`, `bone_breaker`, `enforcer`, `berserker`, `warpath`) | **no funciona**: +0,5 puntos en `orc_violence` | Todos sus efectos son de fuerza (+0,2 por jugador) y el canal con recorrido, `injure`, solo lo usaba `bone_breaker`, que además estaba escrito al revés |
| **Técnica** (`silk_touch`, `one_touch`, `matador`, `showboat`, `playmaker`) | **no funciona**: +4,6 puntos, y la mitad se la come su formación | El pase está saturado (`one_touch`, +0,4) y `matador` no se activa nunca contra su propia raza |
| **Contragolpe** (`counter_punch`, `sprinter`) | **no funciona**: 0,0 puntos exactos | Los dos reparten **velocidad**, el atributo que no vale nada (+0,3 con +10 a toda la plantilla), y con `duration: play` sobre un evento que puede no volver a tocar el balón |
| **Turba** (`mob_lawyer`, `street_fighter`) | **diluida** | Solo actúan en el gol de oro, que ocurre en el 30% de los partidos; `street_fighter` se activa en el 7,6% |
| **Escalado** (`veteran`, `warpath`, `bloodline`, `poacher`, `bookworm`, `iron_lungs_plus`) | **funciona técnicamente, no se nota** | Los contadores suben y los perks se activan (100%), pero el efecto acumulado máximo vale 0,2-0,4 puntos (§8) |
| **Rompe-reglas** (`innocent_face`, `lucky_charm`, `mob_lawyer`) | **demasiado raros** | 0,05%-4% de los partidos con `scope: actor` |
| **Antisinergias** (`elseEffects`, `tagsRequired`, `positionOnly`) | **no castigan** | Un `−3` de atributo cuesta 0,2 puntos por jugador. Las tres builds "malas" están entre el 46,9% y el 61,3% |
| **Sinergia posicional / adyacencia** (RF-044) | **es una trampa neta** | El bono más grande de adyacencia vale menos de 3 puntos; conseguir la adyacencia cuesta 16 |

## 10. Tensiones con los requisitos que el revisor debe conocer

1. **§7 y §8 son incompatibles entre sí con el motor actual.** §7 acota los efectos a ±3..±10 de atributo y
   ±300..±1500 puntos base; §8 pide que una build coherente gane el 58% (+8 puntos sobre su referencia).
   Con la tabla de §5, +8 puntos son el equivalente a **+20 de técnica a los diez jugadores**, es decir, más
   de veinte perks de atributo. Solo se llega usando los canales de probabilidad con recorrido, y de esos
   `intercept` está fuera de escala. Al rediseñar el catálogo tras las ADR 0020/0021/0022 hay que decidir
   explícitamente cuánto puede valer un perk, y §7 debería decirlo en puntos de tasa de victoria, no solo en
   unidades de atributo.
2. **`buildsWinDifferently` mide la raza, no la build** (ADR 0012, propuesta). Medido con el catálogo
   actual: `orc_violence` causa 0,279 lesiones por partido y `orc_none`, en esos mismos partidos, 0,240
   (×1,16); `elf_tiki_taka` causa 0,074 y `elf_none` 0,050 (×1,47), así que la build "de contacto" multiplica
   **menos** que la "técnica" y el cociente normalizado sale 0,79 en vez del 1,5 que se pide. Sin normalizar
   el cociente es 3,78 y aprueba, pero lo que aprueba es que los orcos lesionan más que los elfos. `orc_none` ya causa 3,9 veces
   las lesiones de `elf_none` sin un solo perk, así que la mitad de lesiones de la métrica aprueba con el
   catálogo vacío; y la cadena media de pases de un bloque orco es estructuralmente **más larga** que la de
   uno élfico (4,5 frente a 3,7 en las referencias sin perks: los orcos son lentos, tienen la correa corta y
   juegan juntos), así que la mitad de cadena no puede aprobar por mucho que se toquen los perks. La ADR
   propone normalizar las dos mitades contra la referencia sin perks de la propia raza y leer "lesiones que
   produce" como las causadas al rival. **Está implementada en `BuildMetrics` y pendiente de aprobación.**
3. **La cadena media de pases no es una palanca de perk.** Ningún tipo de efecto de §2 toca la utilidad de
   las acciones, que es lo que decide si un jugador pasa, regatea o dispara; y subir el peso de `Pass` en
   `weights.json` al doble mueve la cadena de 3,55 a 3,62 mientras hunde los goles. Si el diseño quiere
   perks que cambien **cómo** juega un equipo (y "dos builds ganan de formas distintas" es exactamente eso),
   hace falta un tipo de efecto nuevo que modifique la utilidad de una acción.
4. **La segunda mitad de `scalingRewardsGoodBuilds` no es alcanzable** con la progresión de §6 (D-28).
5. **RF-044 tal y como está implementado es contraproducente**: la adyacencia de casillas-hogar con radio 1
   ni siquiera existe en la alineación por defecto (ninguna pareja de las siete casillas es contigua), de
   modo que el rasgo `Leader` es inerte (ya anotado en D-18) y toda sinergia posicional exige apiñarse.
   Resuelto por las ADR 0020, 0021 y 0022.
6. **RT-055 no se puede evaluar con builds a propósito malas**: `human_scattered` gana el 61% contra
   `human_none`, dentro del rango, pero por el motivo contrario al que su diseño pretendía.

## 11. Estado del criterio de salida de la fase 1

| métrica de §8 | estado | valor |
|---|---|---|
| `coherentBuildsBeatNone` | **no** | 1 de 5 (`human_wall` 62,7%) |
| `badBuildsLoseToNone` | **no** | 0 de 3 (46,9% / 48,8% / 61,3%) |
| `randomBuildNearNone` | **sí** | `human_random` 50,8% |
| `buildsWinDifferently` (normalizado, ADR 0012) | **no** | lesiones **0,79×** (pide ≥ 1,5); cadena **1,20×** (pide ≥ 1,3). Sin normalizar: lesiones 3,78× (aprueba, pero midiendo la raza) y cadena 1,25× (suspende) |
| `noDeadPerks` | **no** | 5 filas por debajo del 1%, más 2 perks sin asignar y 1 sin efecto |
| `scalingRewardsGoodBuilds` (1.ª mitad) | **sí** | las 5 coherentes caen ≤ 4,75 puntos |
| `scalingRewardsGoodBuilds` (2.ª mitad) | **no alcanzable** | ver §8 |
| RF-069 60/30/10 ± 8 | **sí** | 56,7 / 33,3 / 10,0 (17 filler, 10 conditional, 3 ruleBreaker) |
| RT-055 (ninguna catalogada > 70% ni < 30% contra `human_none` 50) | **sí** | máximo `human_wall` 62,7%, mínimo `elf_tiki_taka` 32,9% |
| RF-024 (común nivel 8 con perks > legendario de nivel bajo) | **sí** | 56-59% (§8.1) |

La fase 1 **no cumple su criterio de salida**, y la causa está identificada y no es de balance: es la
mecánica espacial (ADR 0020, 0021 y el rediseño de la IA sin balón). El ajuste de valores del catálogo y de
las builds se hace **después** de esos cambios, con esta medición como línea base.

La puerta `Sim.Tests/Analysis/BuildGateTests.cs` está escrita y probada, pero queda **desactivada con
`Skip`** hasta que las ADR 0020, 0021 y 0022 estén implementadas: sus umbrales dependen de la mecánica que
va a cambiar. Muestra de la puerta: 80 plantillas × 20 partidos = **1.600 partidos por build**, semilla 1,
nueve builds; unos 28 s de ejecución.

## 12. Apéndice: los umbrales de §8 sí son alcanzables (experimento revertido)

Antes de acotar el encargo se llegó a construir un catálogo y unas builds ajustados sobre este mismo motor
que cumplían **las nueve métricas de §8 a la vez**. Ese trabajo se ha revertido —el ajuste fino se hará
sobre la mecánica nueva— pero los números merecen quedar registrados, porque dicen **qué hace falta** para
llegar:

| métrica | valor alcanzado | umbral |
|---|---|---|
| `coherentBuildsBeatNone` | 63,3 - 65,8 (las cinco) | ≥ 58 |
| `badBuildsLoseToNone` | 40,3 / 41,1 / 41,3 | ≤ 45 |
| `randomBuildNearNone` | 49,3 | 40-60 |
| `buildsWinDifferently` lesiones (normalizado) | 3,81 | ≥ 1,5 |
| `buildsWinDifferently` cadena (normalizado) | 1,53 | ≥ 1,3 |
| `noDeadPerks` | 0 perks muertos (mínimo 3,9%) | 0 |
| RT-055 contra `human_none` | máximo 68,2%, mínimo 43,6% | 30-70 |

Lo que hizo falta, y que el rediseño debería conservar:

1. **Cambiar el canal de cada familia** por uno con recorrido (§6): la violencia sobre `injure` en vez de
   sobre fuerza, el muro sobre `save` y `leash`, el contragolpe sobre `intercept` y `shotOnTarget`, el
   tiki-taka sobre el `intercept` del rival. Ningún valor salió de los márgenes de §7.
2. **Quitar los `lineup` apiñados** y ampliar el radio de adyacencia, para que la sinergia posicional deje
   de costar más de lo que da. Es lo que resuelven las ADR 0020 y 0021 por la vía buena.
3. **Castigos de verdad en los `elseEffects`**: un `−1` de correa o un `−1500` de pase, no un `−3` de
   atributo (que vale 0,2 puntos). Es la única forma de que una build incoherente pierda de verdad.
4. **`scope: team` en los rompe-reglas** para que se activen por encima del 1% de los partidos.

Y lo que **no** se consiguió ni con ese catálogo: la segunda mitad de `scalingRewardsGoodBuilds` (§8) y la
mitad de cadena de pases **sin** normalizar (§10.2-10.3).
