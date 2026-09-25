# BI-D — Nadie conduce el balón: tiene dueño un tercio del partido y 0,33 s de media

Estado: **CERRADA en lo que era** (25 sep 2026). La conducción con duración existe y está medida
(**ADR 0137**, `driveTicks: 12`); el encargo del revisor —*«alargar la conducción»*— se ha medido en tres
dosis y **se queda en 12**, con el motivo abajo. Lo que sigue abierto no es esta ficha: es
`badBuildsLoseToNone_elf_out_of_zone`, que **sí** es de la conducción y espera una decisión de diseño, y
una idea nueva (el **reencadenamiento**) que necesita su propia `game-design-review`.

**Y una corrección que importa más que el cierre**: `coherentBuildsBeatNone_orc_violence`, que la ADR 0137
se atribuyó, **no es de la conducción**. Medido abajo. Se traslada a [CAT-J](./CAT-J.md).

## De dónde sale

El revisor, jugando la maqueta de modelos 3D: *«aún así el balón no va en sus pies. No se nota como si lo
controlara»*. La primera hipótesis fue de presentación —el balón se apartaba en el eje del campo en vez de
hacia donde corre el jugador, y a 0,51 casillas ≈ 1 m de su centro— **y se arregló** (ver abajo). Pero al
intentar **capturar** a un humano conduciendo para verificarlo, no había ninguno. Eso destapó lo de verdad.

## Medido (partido del club humano, semilla 20260905, 1.200 ticks)

| | |
|---|---|
| ticks con **dueño** del balón | **387 de 1.200 — el 32,3 %** |
| posesiones en el partido | **77** |
| duración media | **5,0 ticks = 0,33 s** |
| posesión más larga de **cualquiera** | **14 ticks = 0,93 s** |
| posesión más larga del equipo propio | 5 ticks = 0,33 s |

**Dos tercios del partido el balón no es de nadie**: está suelto o en vuelo. Y cuando alguien lo tiene, lo
tiene un tercio de segundo.

## Por qué importa, y por qué no lo arregla el render

Ninguna colocación del balón hace que «se note el control» si **no hay control que mostrar**: no existe el
estado de *conducir* con duración. El motor resuelve la jugada como una cadena de toques —`passChainAvgLength`
2,01, `possessionChanges` 21,5 por partido, que son las métricas con banda— y el balón viaja entre pies.

Es coherente con lo que el motor dice de sí mismo, y **puede ser lo correcto**: Underleague resuelve el
partido por decisiones de utilidad tick a tick, no por dribbling. Pero entonces la lectura del jugador
—«esto no parece fútbol, nadie lleva el balón»— es una consecuencia de diseño, no un bug, y merece
decidirse a la vista de estos números.

## Lo que NO se ha hecho, a propósito

Alargar la posesión toca `/Sim`: mueve `possessionChanges` (hoy 21,52 con techo 28), `passChainAvgLength`
(2,01 en banda 1,80-3,50) y probablemente tiros y goles. Es **mecánica, no presentación**, así que pide
`game-design-review` y decisión del revisor antes de tocar una línea — y ADR si se toca (RT-057).

Tres caminos posibles, sin recomendación todavía porque ninguno está medido:

1. **Dejarlo** y aceptar que el balón es el protagonista, no el portador. Gratis, y consistente con la
   identidad del juego (el partido es una carnicería, no una exhibición técnica).
2. **Un estado de conducción con duración** en `/Sim`: el portador mantiene el balón mientras avanza, en vez
   de resolver un toque por tick. Es la que más cambia la sensación y la que más balance mueve.
3. **Solo presentación**: que el balón quede pegado a los pies del último tocador mientras esté suelto y
   cerca. Miente un poco —el balón no es suyo— y choca con *«comportamiento observable > modificadores
   invisibles»*: haría parecer que alguien lleva el balón cuando el motor dice que no.

## Lo que sí se arregló, que era real

El balón se apartaba del portador **en el eje largo del campo** (hacia la portería que ataca) y a **0,51
casillas ≈ 1 m** de su centro, cuando al pie le corresponden ~0,35 m (0,175 casillas). Ahora se aparta
**hacia donde corre** —la misma dirección que ya orienta al modelo— y a `radio × 0,55`, que escala con el
tamaño de la raza. Las cápsulas conservan la separación de siempre porque son gordas y el balón se les
metería dentro.

---

## Decisión de diseño del revisor (23 sep 2026)

> «Yo creo que debería existir conducción con duración porque si no regate tampoco entra en juego.»

### La premisa, medida (200 partidos, referencia, semilla 1, `DribbleMeasurementTests`)

| | por partido |
|---|---|
| **regates intentados** | **0,85** |
| ganados / perdidos | 0,69 / 0,16 |
| posesiones | 70,7 |
| ticks con dueño | **29,3 %** |
| duración media de una posesión | **5,49 ticks = 0,37 s** |
| posesión más larga de cada partido | 19,9 ticks = 1,33 s (máximo absoluto 49 ticks = 3,27 s) |

**CONFIRMED**: el regate no entra en juego. 0,85 por partido contra 8,6 tiros, 12,5 entradas, 7,4 faltas y
~43 pases. Existe en `Utility.EvaluateDribble` —con pendientes por técnica y velocidad— y es invisible.
Cuando ocurre funciona (81 % ganados), pero casi nunca ocurre.

**Y el regate no estaba instrumentado**: ni métrica en `/Balance`, ni campo en el informe, y `MatchLogView`
omite `DRIBBLE_ATTEMPTED`. Antes de esta medición, «el regate no entra en juego» era una impresión.

### Las diez preguntas (`game-design-review`)

1. **Qué experimenta el jugador.** Hoy el balón salta de pie en pie: un partido es una cadena de toques y
   disputas, y **nadie lleva el balón**. Con conducción, alguien avanza con él y aparece la tensión de «¿le
   van a entrar?», que es justo donde vive la carnicería de este juego.
2. **Qué decisión toma con esto.** Hoy, **ninguna**: la técnica influye en la utilidad del regate, pero con
   0,85 regates por partido un bicho técnico no se distingue por regatear. Es un modificador invisible, que
   es la señal de alarma del propio principio (`comportamiento observable > modificadores invisibles`).
3. **Qué decisión debería tomar.** Alinear técnica y velocidad arriba debería tener una consecuencia que se
   vea: «este lleva el balón» contra «este lo pierde».
4. **Qué regla representa.** El **regate** sí está en los requisitos (eventos `REGATE_INTENTADO /
   GANADO / PERDIDO`, §218). La **conducción con duración NO está**: es una regla nueva y hay que decirlo.
   Y le aplica RF-012d: si conducir expone a la entrada, tiene que poder anunciarse antes.
5. **Qué sistemas.** `/Sim`: `Utility.EvaluateDribble` y el motor (un estado de conducción con duración
   toca la transferencia de balón y el enfriamiento de la entrada). `/data`: pesos de IA
   (`DribbleOpenSpaceBonus`, `DribbleOpponentAheadPenalty`, pendientes de técnica y velocidad). `/Game`:
   **ya está listo** — la animación y el balón al pie existen desde hoy y no tienen nada que mostrar.
6. **Alternativas.** (a) conducción como acción con duración; (b) **subir el peso del regate en `/data`**,
   sin tocar el motor; (c) que el portador pase menos, atacando `passChainAvgLength` de frente.
7. **Trade-off.** Conducir tiene que **costar riesgo**: más tiempo con el balón, más exposición a la
   entrada y a la lesión. Si conducir es gratis, es poder gratis.
8. **Cómo cambia las estrategias.** Técnica y velocidad pasan a valer; la build violenta gana objetivos que
   antes no existían (alguien a quien entrarle). Riesgo: si todos conducen, se **aplana** la
   diferenciación, que es el patrón que ya rompió tres intentos de la familia BE.
9. **Puede degenerar.** Sí: un buen conductor cruzando el campo sin oposición baja pases, intercepciones y
   sucesos — menos carnicería por partido. `possessionChanges` (hoy 21,52, banda 12-28) puede caer, y
   `passChainAvgLength` (2,01 en banda 1,80-3,50) subir fuera de banda.
10. **Cómo se demuestra.** `DribbleMeasurementTests` como línea base (ya existe), las 43 puertas y un lote
    de `/Balance` con dos semillas, vigilando `possessionChanges`, `passChainAvgLength`, `shotsPerMatch` y
    las métricas de diferenciación de builds.

### Recomendación: (b) antes de (a), y es la regla del proyecto, no pereza

La vía (b) —subir los pesos del regate en `/data`— **no toca el motor, se mide hoy mismo y discrimina**: si
con el regate en 5-6 por partido la técnica ya se nota y la posesión se alarga sola (regatear ocupa ticks
sin soltar el balón), la mecánica nueva no hace falta. Y si no basta —el regate sube pero la posesión sigue
en 0,37 s— entonces (a) está **justificada con datos** en vez de por intuición, que es exactamente lo que
la Regla A pide: *no se modifica código mientras exista una medición de bajo coste capaz de discriminar*.

**Pendiente de la palabra del revisor**: empezar por (b) y medir, o ir directo a (a).

---

## Cierre (25 sep 2026): tres dosis, con su error típico delante

El revisor: *«ponte entonces primero a cerrar conducción»*. Se cierra **remidiendo**, no leyendo la ADR: el
árbol de hoy no es aquel —el balón parado posicional (ADR 0147) entró en medio— así que lo que la ADR 0137
dejó «pendiente de decisión» había que volver a medirlo antes de decidirlo.

**Este cierre tuvo una primera versión que la revisión independiente tumbó, y con razón**: medía las
catorce celdas pero leía sus medias **como si no tuvieran error**. Había una medición de coste cero capaz
de discriminar —la dispersión entre semillas, que `CatJSeedDispersionTests` ya hacía— y no se corrió. Eso
es la Regla A incumplida. Lo que sigue es la versión con el error típico calculado, y **tres de las cuatro
conclusiones de aquella versión no sobrevivieron**.

### Los instrumentos

- `Sim.Tests/Analysis/DribbleGateContrastTests.cs` (nuevo, `Category=Diagnostic`, con `Skip`): vuelca **las
  catorce celdas** de `BuildGateTests` con su media sobre las ocho bases de semilla **y el valor de cada
  semilla**, a un CSV. Los tres volcados están versionados en `docs/balance/bi-d/` — `out/` está en
  `.gitignore`, así que dejarlos ahí habría cerrado el expediente sin su evidencia.
- La variante se mide en un `git worktree` desde el mismo HEAD, cambiando **sólo** `data/sim/tuning.json`
  (verificado con `git diff HEAD --stat` en el worktree), nunca editando el árbol vivo.
- `DribbleMeasurementTests` gana **por qué termina** una conducción: con el balón en los pies (agotó su
  contador) o sin él (se la quitaron), más las que siguen vivas al acabar el partido, con un `Assert` que
  obliga a que las tres sumen las rachas contadas.

**Y corrige un denominador que llevaba mintiendo desde la ADR 0147**: el porcentaje de ticks conduciendo se
dividía por el literal `1200`. Desde que las reanudaciones consumen ticks de motor sin consumir reloj de
partido, **un partido dura 1.825,8 fotogramas**, no 1.200. Todo porcentaje «de 1.200» de este proyecto está
inflado un 52 % desde esa ADR; conviene un `grep` antes de que otra ficha cite uno.

### Lo que dicen los contrastes pareados por semilla (Δ de la dosis 0 a la 12)

| celda | 0 | 12 | 18 | Δ(0→12) | ET | σ |
|---|---|---|---|---|---|---|
| `badBuildsLoseToNone_elf_out_of_zone` | 42,45 | **45,42** | 45,86 | **+2,97** | 0,60 | **+4,9** |
| `coherentBuildsBeatNone_human_counter` | 76,82 | 78,18 | 78,57 | +1,35 | 0,45 | **+3,0** |
| `badBuildsLoseToNone_human_scattered` | 31,48 | 29,90 | 32,14 | **−1,59** | 0,74 | **−2,1** |
| `coherentBuildsBeatNone_orc_giants` | 60,62 | 62,84 | 62,94 | +2,21 | 1,11 | +2,0 |
| `coherentBuildsBeatNone_orc_mob` | 58,52 | 59,69 | 59,01 | +1,17 | 0,92 | +1,3 |
| `randomBuildLosesToNone_human_random` | 45,52 | 46,56 | 48,46 | +1,04 | 0,90 | +1,2 |
| `badBuildsLoseToNone_elf_brawler` | 43,62 | 44,38 | 45,68 | +0,76 | 0,97 | +0,8 |
| `badBuildsLoseToNone_orc_misplaced` | 44,35 | 44,90 | 45,31 | +0,55 | 1,11 | +0,5 |
| `coherentBuildsBeatNone_orc_violence` | 57,50 | 57,24 | 57,53 | **−0,26** | 1,00 | −0,3 |

### 1. `orc_violence`: la **atribución** de la ADR 0137 no se sostiene — **LIKELY**, no CONFIRMED

Δ(0→12) = **−0,26 ± 1,00**. El intervalo no excluye el «~1 punto» que aquella ADR afirmaba: **este
experimento no tiene potencia para rechazar lo que rechaza**. Y la celda tampoco está establecidamente
roja en ninguna dosis —57,50 contra un mínimo de 58 son **0,5 ET**—: está en la frontera del umbral,
midiéndose como OUT por la media.

Lo que sí sostiene la conclusión, y no es el contraste de dosis:

- **Es plana en las tres dosis**: Δ(0→18) = **+0,03 ± 1,0**, con un 50 % más de conducción. El mecanismo que
  la ADR 0137 propuso (más conducción → más duelos → más defensores derribados) **exige** que el efecto
  crezca con la dosis, y no crece.
- **La ADR 0147 subió las faltas un 68 %** (4,73 → 7,96 por partido) y las lesiones de 0,55 a 0,82, y la
  celda no se movió (57,16 → 57,24). Un tratamiento enorme con efecto nulo: eso sí **REJECTED** para «el
  canal es el volumen de contacto», y es el argumento fuerte, no el contraste de dosis.

**Se traslada a [CAT-J](./CAT-J.md)** con esa etiqueta, no con un CONFIRMED.

### 2. «Las builds malas pierden menos cuanto más se conduce» — **REJECTED**, y era el titular

La primera versión de este cierre presentó cuatro celdas monótonas como «cuatro métricas independientes
moviéndose juntas». Con el error típico delante, la familia **no se mueve junta**:

- sólo `elf_out_of_zone` pasa de 2 σ (**+4,9**);
- `elf_brawler` (+0,8 σ) y `orc_misplaced` (+0,5 σ) están **por debajo del ruido**;
- y la cuarta, `human_scattered`, se mueve **al revés y también significativa** (**−2,1 σ**), dato que
  aquella versión usó para matar una hipótesis rival y **omitió** del párrafo de su propia conclusión.

Entre las coherentes suben dos con señal (`human_counter` +3,0 σ, `orc_giants` +2,0 σ). O sea: **la
conducción mueve celdas en las dos direcciones y sin patrón por familia**. El mecanismo propuesto
—«conducir es una vía de ganar independiente de los perks, luego construir mal se castiga menos»— es
plausible y **no está demostrado**: sin evidencia discriminante.

Lo único establecido de esa familia: **`elf_out_of_zone` +2,97 (4,9 σ)** y **`human_scattered` −1,59
(−2,1 σ)**, las dos sin explicación medida.

### 3. Dos hipótesis rivales sobre `elf_out_of_zone`

- **REJECTED, limpio**: no es que sus perks de zona se activen por accidente al pisar zonas nuevas.
  `forward_line`, `own_third_anchor` y `flank_specialist` valen **0,00 % en las tres dosis**.
- **Sin evidencia discriminante** (la primera versión decía REJECTED, y se apoyaba en medio rango): la
  correa larga, `long_leash_legacy`. Se argumentó que de las dos builds que lo llevan una sube y otra baja
  — cierto en 0→12, **falso en 0→18, donde suben las dos** (+3,41 y +0,65). Con los tres puntos delante la
  hipótesis no queda refutada; queda sin resolver.

### 4. La dosis se queda en **12**, y el dato no la decide sola

| | `driveTicks` 0 | **12** | 18 | ADR 0137 (23 sep, dosis 12) |
|---|---|---|---|---|
| regates intentados / partido | 0,30 | 2,78 | 3,26 | 2,69 |
| conducciones / partido | 4,1 | 4,0 | 3,9 | — |
| duración media de conducir | 4,47 t | 11,49 t (0,77 s) | 15,23 t (1,015 s) | 15,4 t (1,03 s) |
| ticks conduciendo (traza real) | 1,02 % | **2,52 %** | 3,34 % | 9,02 % (sobre 1.200) |
| conducciones cortadas (sin balón) | 3 % | 24,3 % | **35,8 %** | — |

- **18 restaura la conducción de la ADR 0137** (15,23 t contra 15,4 t) y carga el riesgo que aquella pedía
  (35,8 % cortadas). Su precio: sube **significativamente** dos celdas de builds mal construidas hacia el
  techo (`elf_out_of_zone` +3,41 = 5,1 σ, `elf_brawler` +2,06 = 2,4 σ). **Aviso honesto**: los tres cruces
  concretos del techo de 45 que produce la dosis 18 están **dentro del ruido contra el umbral** (0,4-0,9 ET);
  lo que decide es el contraste pareado, no la posición contra la banda.
- **0 no es opción**: deja `noDeadPerks` en 0,12 — un perk por debajo del 1 % de activación (RF-070). La
  conducción **resucita `crowd_control`**, de 1,80 % a 13,15 % (17,42 % con 18). Ese argumento a favor de la
  mecánica no estaba en la ADR 0137.
- Lo que se compra con 18 sobre 12 son **0,25 s** de conducción. No compensa.

### 5. La conducción se ha encogido sola, por **dos** mecanismos, y ninguno es que la corten

**REJECTED** que el partido más violento de la ADR 0147 las corte: el **75,7 %** de las conducciones
terminan con el balón todavía en los pies, es decir agotando su contador.

Son dos caídas distintas, y la primera es **independiente de la dosis**:

1. **Frecuencia**: con la conducción apagada, hoy se decide conducir **4,1 veces por partido**; la línea
   base de la ADR 0137, con el mismo parámetro a 0, medía **7,3**. Mismo ajuste, distinto árbol: **se
   decide conducir un 44 % menos que hace dos días**, y eso no lo causó la conducción. Sin explicación
   medida.
2. **Duración**: 11,49 t hoy contra 15,4 t entonces, con el contador en 12 las dos veces. Como el 75,7 %
   agota el contador, lo que se perdió es el **reencadenamiento** —entonces las rachas superaban al propio
   contador porque se enlazaban—. **LIKELY**, sin experimento que lo aísle.

Juntas, la cuota real cae de **9,02 % a 2,52 %**: 3,6 veces, no 2,3 como decía la primera versión de este
cierre, que aún usaba el denominador roto.

### Lo que queda abierto, y de quién es

1. **`badBuildsLoseToNone_elf_out_of_zone` = 45,42 contra un techo de 45.** Causa **CONFIRMED** (la
   conducción, +2,97, 4,9 σ), mecanismo **no** demostrado. **No se mueve la banda**: el precedente es CAT-J
   —*«son señales reales; ajustarlas para que pasen sería lo que `balance-measure` prohíbe»*— y mover el
   techo abriría la puerta a la dosis 18, que es la decisión que no se ha tomado. Al revisor se le entrega
   **el efecto que aguanta el muestreo (+2,97, 4,9 σ)**, no la distancia al techo (0,42, que con un ET de
   0,64 es ruido con signo). La pregunta es de diseño: *¿puede una vía de ganar que no consulta los perks
   erosionar el principio de que construir mal sale peor que no construir?*
2. **Por qué se decide conducir un 44 % menos** que en el árbol de la ADR 0137. Es el hallazgo más grande
   de este cierre y no tiene dueño. El hermano a mirar primero es **`Shielding`**, que la ADR 0137 hizo
   gemelo exacto de `Dribbling` (`EnterState(estado, ticks)`, mismo corte por pérdida de balón), **no está
   instrumentado en absoluto**, y compite por las mismas decisiones del portador.
3. **El reencadenamiento** como forma de recuperar la sensación sin subir la dosis: enlazar conducciones
   **sólo cuando no hay presión encima**, que es donde alargar no le regala nada a una build mal
   construida. Mecánica nueva → `game-design-review` y ADR.
4. **Un caso dirigido** que fije la semántica de `RunsEndedStillOwner` / `RunsEndedBallGone`: hoy el 75,7 %
   descansa en una clasificación sin ninguna prueba que la sujete ante un cambio futuro de transiciones.
