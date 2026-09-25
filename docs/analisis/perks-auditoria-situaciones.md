# Auditoría 13C — El catálogo desde las situaciones, no desde los efectos

**Encargo del revisor (25 sep 2026), y es una corrección a las dos auditorías anteriores:**

> No quiero convertir Knavall en un simulador estadístico con una pequeña capa de caos. Quiero exactamente
> lo contrario: un roguelike deportivo caótico y divertido, donde las estadísticas sirven para **respaldar y
> diferenciar** ese caos. […] No: *«tengo 100 perks y el 60 % modifica estadísticas con condiciones»*. Sino:
> *«tengo un sistema estadístico que permite que 100 perks creen situaciones diferentes durante un
> partido»*.

Con trece ejemplos de perk adjuntos, que esta auditoría trata como **especificación por ejemplo** y
disecciona uno a uno contra el motor real (§2).

Este documento **no modifica código ni `/data`**. Etiquetas: **MEDIDO** (leído del código o de `/data`),
**DERIVADO**, **HIPÓTESIS**.

---

# 0. El error que hay que retirar primero

La auditoría 13B y la reescritura de §1.6 de `perks-design-bible.md` ataron el espectáculo al grado
`ruleBreaker` de RF-069, **que está capado al 10 %**. Eso *es* «estadísticas con una pizca de caos»: el
producto que el revisor acaba de descartar, escrito como arquitectura. **Se retira.**

El fallo fue colapsar dos ejes que son **ortogonales**:

| eje | qué mide | vocabulario | ¿lo limita RF-069? |
|---|---|---|---|
| **Qué escribe el verbo** | número · conducta · física · balón · flujo | el **lenguaje** de diseño | **no** |
| **Cuánto rompe una regla** | relleno · condicional · rompe-reglas | la **potencia** (60/30/10) | **sí** |

**Cabeza de Hierro no rompe ninguna regla del simulador**: cabecear es legal y el motor ya lo hace. Es un
perk **común** que escribe **física**. Pies de Plomo tampoco rompe nada: es una inmunidad, y el motor ya
tiene inmunidad al empuje *(MEDIDO: `roots` la usa hoy)*. **Atar «física» a «rompe-reglas» capa el caos al
10 % por construcción, y ninguno de los trece ejemplos del revisor lo pedía.**

**Lo que RF-069 limita es romper reglas, no crear situaciones.** Un catálogo puede ser **100 % situacional**
con un 10 % de rompe-reglas. Son preguntas distintas y hay que medirlas por separado — hoy sólo se mide una,
y es la que menos importa para el producto (§5).

---

# 1. El censo, con el criterio del revisor

El revisor corrige también el clasificador: *«un perk puede usar `modifyUtility` internamente y seguir
siendo un excelente perk visual si cambia lo que vemos hacer al jugador»*. Correcto, y cambia el reparto.
Tres grados, del más visible al menos:

- **SITUACIÓN** — produce un **suceso** que se puede señalar: alguien cae, algo se repite, algo no ocurre.
- **CONDUCTA** — cambia **lo que ves hacer** al jugador a lo largo del partido: dónde se coloca, a quién
  marca, qué decide. Visible como **patrón**, no como instante.
- **NÚMERO** — cambia las probabilidades de algo que iba a pasar igual. **Invisible incluso en principio**:
  no hay fotograma en el que se note.

*(MEDIDO, censo de los 102 ficheros de `data/perks/`)*

| grado | perks | % |
|---|---:|---:|
| **SITUACIÓN** | 15 | 14,7 % |
| **CONDUCTA** | 15 | 14,7 % |
| **NÚMERO** | **72** | **70,6 %** |

**Con el criterio más generoso posible —contando toda la conducta como visible—, siete de cada diez perks
del catálogo no se pueden ver.** *(DERIVADO)*

**Dos matices que empeoran la cifra, no la mejoran:**

1. **Los 15 de CONDUCTA son casi todos geometría** *(MEDIDO: `deep_run`, `high_line`, `low_block`,
   `line_keeper`, `long_leash_legacy`, `deep_pivot`, `shadow`…)*: cambian **dónde está parado** un jugador.
   Eso se lee en 90 minutos y comparando, no en un instante. Es visible en el sentido de un gráfico, no en
   el de una anécdota.
2. **Ninguno de los 102 es atribuible en pantalla**, ni siquiera los 15 de SITUACIÓN: `PERK_TRIGGERED` se
   emite y se tira, y `MomentKind` no tiene entrada de perk *(MEDIDO, 13B §2.2)*. **Hoy el juego no puede
   decir «ha sido el enano» de ningún perk.**

---

# 2. Los trece ejemplos del revisor, contra el motor real

Es la parte útil del documento: convierte «qué falta» de abstracto a una lista cerrada. Para cada uno, qué
tiene el motor ya escrito, qué le falta y cuánto cuesta.

| # | perk del revisor | qué **ya existe** *(MEDIDO)* | qué **falta** | coste |
|---|---|---|---|---|
| 1 | **Cabeza de Hierro** | duelo aéreo y cabezazo con su propia física (`Ball.Head`, ADR 0139); magnitudes enteras en `tuning` | escalar la fuerza del cabezazo por jugador (un escalar de rasgo más, patrón C4) **+ mandar al rival por los aires** (P1) | **bajo** |
| 2 | **Pies de Plomo** | **`ImmunityKind.Push` existe y `roots` ya la usa**; el búfer de `BodySeparation` con tope por jugador | que el empuje **rebote** al que empuja: hoy se absorbe, no se devuelve (P1 con signo invertido) | **muy bajo** |
| 3 | **Patada del Burro** | disparadores `TACKLE`/`FOUL` con el rival implicado en el evento | saber que el contacto vino **por detrás** (P3) · una reacción **ajena a la acción propia** —`extraAction` sólo rehace el `SHOT`/`TACKLE` del actor— · el impulso (P1) | medio |
| 4 | **Balón Imantado** | `Ball.Velocity`; balón suelto con velocidad; **`UpdateContextCaches()` corre cada tick al principio de `Step()`** y ya reevalúa ahí el bono de zona de `modifyUtility` | un efecto **continuo con duración** que escriba la velocidad del balón cada tick (P2). **No existe en ninguna forma** | medio |
| 5 | **Segundo Balón** | nada | que el motor deje de suponer **un** balón. **MEDIDO: 239 líneas de `MatchEngine.cs` mencionan `_ball`, más 16 de `Utility.cs`**; posesión, intercepción, tiro, gol, portero, reanudaciones **y la traza** suponen uno | **alto — decisión aparte** |
| 6 | **Carga de Jabalí** | el molde `EnterState(estado, ticks)` —`Dribbling`, `Shielding` y `Blocking` son el mismo patrón (ADR 0137)—; `SpeedBonusPercent` ya modula `SpeedPerTick` por jugador; el cansancio como recurso | un acumulador de «ticks a velocidad alta» (forma barata de P2) + el impacto (P1) | medio-bajo |
| 7 | **Rodillo** | `BodySeparation` resuelve pares (i, j) por id ascendente con tope por jugador | una marca que **desactive la separación** para ese jugador N ticks y convierta el solape en impulso al rival (P1 + bandera de estado) | **bajo** |
| 8 | **Botas Pegajosas** | **`ProbabilityKind.TackleEvasion` existe** (ADR 0026) con `duration` y `ticks`; disparador `PASS_COMPLETED` | **nada en motor: es escribible hoy en JSON.** Lo que falta es que se **vea**: hoy sería un número invisible (P4) | **cero motor** |
| 9 | **Pelota de la Suerte** | **siete puntos** donde el motor suelta el balón con dirección (`SetLoose`: parada, palo, bloqueo, despeje, rechace) | sesgar la dirección del rechace (P1 instantáneo sobre el balón). **Los ganchos ya están puestos** | **bajo** |
| 10 | **Falso Muerto** | **todo**: `cancelEvent` sobre `DEATH` ya existe y ya se usa (`no_dying`); `setState(KnockedDown, ticks)` existe. **Los dos efectos en el mismo perk son escribibles hoy** | que el partido lo **cuente** (P4) | **cero motor** |
| 11 | **Borracho** | el movimiento es `TargetPoint` → `SpeedPerTick` → `player.Position = next`; flujos de RNG con semilla explícita | perturbación **per-tick** del vector de paso (P2), con **una tirada por jugador y por tick** que hoy el motor no hace: hay que dimensionarla de frente (RT-021/RT-024) | medio |
| 12 | **Árbitro Sobornado** | **todo**: `modifyBias` (criterio del árbitro, RF-060) y `cancelEvent` sobre `FOUL`, ya usado por `mob_instigator` | que el árbitro **mire hacia otro lado** en pantalla (P4) | **cero motor** |
| 13 | **Público Enloquecido** | la turba (`MobStart`, `RefereeLeaves`); `modifyAttribute` con duración de jugada o partido; disparadores `INJURY`/`DEATH` | que el disparador sea **lo espectacular que fue**, y eso no se mide dentro del partido: el nivel N1-N4 vive en `Sim/Run/View`, **después** | medio |

## 2.1 Lo que el cuadro dice, y no es lo que yo esperaba

**Tres de los trece se pueden escribir hoy, en JSON, sin tocar el motor** *(DERIVADO)*: Falso Muerto,
Árbitro Sobornado y Botas Pegajosas. No están en el catálogo. **Lo único que les falta es que se vean.** Es
la prueba más fuerte de la tesis de estas tres auditorías: **el cuello de botella no ha sido nunca el
motor**.

Y **los trece caben en cuatro capacidades**, no en trece:

| | capacidad | la piden | por qué es la que es |
|---|---|---:|---|
| **P1** | **Impulso dirigido** sobre un cuerpo o sobre el balón: dirección + magnitud entera | **6 de 13** | El cuerpo ya tiene acumulador (`BodySeparation`) y el balón ya tiene velocidad y altura, y los dos ya se dibujan |
| **P2** | **Efecto continuo con duración**: actúa **cada tick** mientras viva, sobre el balón o sobre el movimiento | **3 de 13** | Es la única de las cuatro que **no existe en ninguna forma**. El gancho per-tick sí existe (`UpdateContextCaches`) |
| **P3** | **Condición geométrica del contacto**: por detrás · a velocidad · de cabeza | 3 de 13 | Los eventos llevan celda y actores, pero **no la geometría relativa** del contacto |
| **P4** | **Presentación atribuida**: el perk se nombra en el instante en que actúa | **13 de 13** | Y también los 15 perks de SITUACIÓN que ya existen |
| — | *multi-balón* | 1 de 13 | **No es una extensión.** Decisión aparte, con 255 líneas de motor de precio |

**DERIVADO: P4 la piden los trece, la piden los quince que ya existen, y es la más barata.** No hay ninguna
lectura de estos datos en la que P4 no vaya primero.

---

# 3. Las seis categorías del encargo, con veredicto

## 3.1 Cambios físicos

**Hoy: 2 perks de 102** *(MEDIDO: `roots` —inmunidad al empuje— y `earthquake` —derribo en radio real—)*.
**Bloqueado por**: P1. **Lo que hay ya construido es más de lo que parece**: el búfer de empuje de
`BodySeparation` resuelve el problema difícil —N cuerpos empujándose en el mismo tick, con orden
determinista y tope por jugador— y `AddTacklePush` ya demuestra que se le puede inyectar una fuerza desde
fuera de la separación. **P1 es escribir un segundo llamador de una función que ya existe.**

## 3.2 Cambios de comportamiento de jugadores

**Hoy: 15 de 102, casi todos geometría.** **No está bloqueado**: el canal existe (`modifyLeash`, `shiftHome`,
`modifyZoneShape`, `modifyMarkBias`, `modifyTackleBias`) y `modifyUtility` lo amplía. **Está sin escribir**,
que es un problema de catálogo y es justo lo que la biblia ya planificaba. **Advertencia, y es del
revisor**: esta familia se lee como patrón, no como anécdota. Un catálogo que resuelva la visibilidad
**sólo** por aquí produce partidos que se leen distinto en la hoja de datos y **se ven casi iguales**.

## 3.3 Cambios de comportamiento del balón

**Hoy: cero de 102. Ni un solo tipo de efecto de los diecinueve toca el balón** *(MEDIDO)*.
**Bloqueado por**: P1 en su forma instantánea (rechaces, cabezazos, despejes: los ganchos ya están) y P2 en
la continua (imantado). **Es el hueco más grande y a la vez el más barato**, porque la traza ya graba
`BallAt` y `BallHeightAt` por fotograma y `/Game` ya los pinta: **un efecto escrito ahí se ve sin tocar una
línea de `/Game`.** Ninguna otra categoría tiene esa propiedad.

## 3.4 Interacciones entre jugadores

**Hoy: los objetivos existen** —incluido `adjacentOpponents`, que mira posición real y no alineación— **y los
vínculos estáticos de alineación**. Lo que falta es que la interacción sea **física y recíproca**: hoy un
efecto se aplica *a* un objetivo, nunca *entre* dos. Pies de Plomo es exactamente eso —el empuje vuelve al
que empuja— y no se puede expresar. **Bloqueado por**: P1 + P3.

## 3.5 Eventos absurdos pero comprensibles

**Las dos mitades tienen dueños distintos, y conviene decirlo claro**: P1/P2 producen lo **absurdo**; **P4
produce lo comprensible**. Y sin la segunda mitad la primera es peor que inútil:

> **Lo único que separa «¿qué coño ha hecho ese enano?» de «el motor ha fallado» es la atribución.** Un
> balón que vuelve solo, sin nombre encima, no es un perk: es un bug.

*(DERIVADO, y es el argumento más fuerte a favor de hacer P4 primero.)*

## 3.6 Efectos combinables y situaciones emergentes

Aquí la arquitectura dicta más de lo que parece, porque **la emergencia depende de si las primitivas
acumulan o sobrescriben** *(DERIVADO)*:

| primitiva | ¿compone? | por qué |
|---|---|---|
| **impulso (P1)** | **sí, por construcción** | El búfer de `BodySeparation` ya **suma** los empujes de varios pares en el mismo tick y los aplica a la vez (esquema de Jacobi, MEDIDO). Dos perks de empuje en la misma jugada **ya se sumarían correctamente y de forma determinista** |
| **efecto continuo (P2)** | **sí**, si se suma como velocidad | Dos fuerzas sobre el balón se suman; dos «pegajosos» no |
| **estado (`setState`)** | **no** | El último escritor gana. Dos derribos no son un derribo doble |
| **número (`modifyProbability`)** | sí, aritméticamente | …y por eso el catálogo actual «combina» sin que se note nada: sumar dos invisibles da un invisible |

**DERIVADO — y es la razón técnica para poner P1 por delante de P2 y P3: el impulso es la única primitiva
del conjunto que ya vive en un acumulador, y acumular es de dónde sale la emergencia.** La frase que el
revisor quiere oír —*«ganamos porque el enano tenía Pies de Plomo y el orco llevaba Carga de Jabalí»*— es
literalmente dos escrituras en el mismo búfer en el mismo tick.

---

# 4. Lo que falta, en una frase por capacidad

No es un catálogo de efectos —13B pedía no fijarlo y sigue sin fijarse—, son las cuatro capacidades que los
trece ejemplos reclaman:

- **P4 · Atribución.** Que el partido pueda decir **quién** ha hecho eso y **por qué**, en el instante.
  La piden 13 de 13 y los 15 perks que ya existen. **Es la más barata y no desbloquea un perk: desbloquea el
  catálogo entero.**
- **P1 · Impulso dirigido** sobre un cuerpo o sobre el balón. La piden 6 de 13, abre las categorías 3.1,
  3.3 y 3.4 enteras, **compone sola** (3.6) y reutiliza un acumulador ya escrito y ya determinista.
- **P3 · Geometría del contacto** (por detrás, a velocidad, de cabeza). La piden 3 de 13 y es lo que hace
  que un impulso sea **una respuesta a algo** y no un evento aleatorio. Barata comparada con lo que habilita.
- **P2 · Efecto continuo con duración.** La piden 3 de 13, es la única que no existe en ninguna forma, y es
  la que abre la clase de fenómeno más memorable de la lista del revisor (el balón que vuelve). El gancho
  per-tick ya existe; lo que no existe es el concepto de «efecto vivo».

Y **una decisión aparte**: el **multi-balón**, que no es una capacidad sino una reforma del motor, con
precio medido (255 líneas de motor). No se recomienda meterlo en el mismo paquete que las cuatro, ni descartarlo
— se recomienda **decidirlo por separado y con su propio presupuesto**.

**Lo que NO hace falta, y conviene decirlo porque yo mismo lo propuse hace dos documentos**: un «lenguaje de
excepción» aparte. **Once de los trece ejemplos del revisor son perks comunes.** No rompen reglas: hacen que
el fútbol sea más bruto. El vocabulario nuevo no va en un registro raro: va en el **mismo** lenguaje que
usará el 60 % del catálogo.

---

# 5. El conflicto con RF-069, que no puedo resolver yo

*(MEDIDO, `docs/requisitos.md`)* La primera fila de RF-069 dice:

> | Relleno con condición | **60 %** | **Modificadores numéricos condicionados.** Dan grosor a las builds |

Y el encargo del revisor dice, textualmente, que eso es lo que **no** quiere:

> No: *«tengo 100 perks y el 60 % modifica estadísticas con condiciones»*.

**Es una contradicción literal con un requisito, no una cuestión de énfasis.** El catálogo actual —70,6 %
invisible— **no está incumpliendo RF-069: lo está cumpliendo.** Por eso ninguna auditoría de catálogo lo
iba a arreglar: el documento que hay que cambiar está por encima.

**No lo cambio por mi cuenta** (regla del modo de trabajo: una decisión que cambia una regla de
`docs/requisitos.md` se consulta). Queda anotado en [BK-A](../pendientes/BK-A.md) con la lectura
conservadora aplicada mientras tanto: **no se toca `/data`**.

**La forma que propongo para la decisión** *(HIPÓTESIS, es del revisor)* es **no borrar el 60/30/10 sino
añadirle una segunda distribución ortogonal**, porque miden cosas distintas (§0): RF-069 mide **potencia**
(cuánto rompe) y lo que falta es un requisito que mida **visibilidad** (cuánto se ve). Con dos
distribuciones, un perk de relleno **puede y debe** ser visible —Cabeza de Hierro es relleno y es
espectacular— y la contradicción desaparece sin retirar nada de lo que RF-069 protege. Lo que habría que
fijar es el **suelo**: qué fracción del catálogo tiene que ser SITUACIÓN o CONDUCTA. Hoy es 29,4 %.

---

# 6. El orden que propongo, y por qué no es el que propuse ayer

13B §7 y la biblia §6.4 proponían C18 (gesto) y C17 (impulso) **después** de congelar el lenguaje de
intención. **Con los trece ejemplos delante, ese orden está mal**: congelar el lenguaje antes de saber que
seis de trece fantasías piden un impulso es exactamente cómo se cierra el espacio expresivo.

1. **P4 primero, y sola.** Atribuir en pantalla los **15 perks de SITUACIÓN que ya existen**. No toca reglas,
   no toca balance, y es la única forma de saber si el problema es de motor o de catálogo: **si con los
   quince atribuidos el partido ya se cuenta solo, hay menos motor que escribir del que creemos.** Es una
   medición disfrazada de trabajo.
2. **P1 después.** Empezando **por el balón y no por el cuerpo**: el balón no gasta del presupuesto de
   violencia —`injuriesPerMatch` 0,78 contra techo 0,90, con la brecha de la ADR 0147 aún sin explicación
   cerrada— y su canal de dibujo está entero. Los rechaces sesgados (Pelota de la Suerte) son el primer
   candidato: siete ganchos ya puestos, coste bajo, efecto inmediatamente visible.
3. **P3 y P2 después**, en ese orden: P3 es barata y hace que P1 sea *respuesta* y no ruido; P2 es la más
   cara de las cuatro y la que más riesgo de determinismo trae (una tirada por tick y por jugador).
4. **El lenguaje se congela al final, no al principio** — cuando las cuatro primitivas existan y se sepa qué
   hay que saber decir.
5. **Multi-balón**: decisión propia, fuera de este orden.

**Cómo se demuestra que funciona** *(y no es «se ve a ojo»)*: el criterio de éxito de este encargo no es una
banda de RT-056, es **«¿el partido se cuenta solo?»**. Se mide así — **sucesos atribuibles por partido**
(cuántos momentos llevan nombre de perk) y **cuántos perks distintos de la alineación producen al menos un
suceso atribuido**. Las bandas de siempre siguen siendo la restricción, no el objetivo: ninguna primitiva
entra si saca de banda `injuriesPerMatch` o `shotsPerMatch`.

---

# 7. Para el revisor

1. **Tenías razón y el marco anterior se retira**: el espectáculo no es el 10 % de `ruleBreaker`. **Once de
   tus trece ejemplos son perks comunes.**
2. **Con tu propio criterio —la conducta cuenta como visible— el catálogo es 70,6 % invisible**, y ni uno
   solo de los 102 es atribuible en pantalla.
3. **Tres de tus trece se pueden escribir hoy en JSON sin tocar el motor** (Falso Muerto, Árbitro Sobornado,
   Botas Pegajosas). No están escritos. El cuello de botella nunca ha sido el motor.
4. **Los trece caben en cuatro capacidades**, y la que piden los trece —**atribución**— es la más barata y
   no está en ninguna hoja de ruta como prioridad.
5. **Decisión que necesito y no puedo tomar yo**: RF-069 exige el 60 % de modificadores numéricos
   condicionados, que es literalmente lo que has dicho que no quieres. Ficha [BK-A](../pendientes/BK-A.md).
   **Hasta que la tomes, cualquier rediseño de catálogo va a chocar con el requisito.**
6. **Pregunta que sigue abierta y es tuya**: el **segundo balón**. Es el único de tus trece que no es una
   extensión del motor sino una reforma, con 255 líneas de motor de precio. ¿Entra en la lista o se aparca?

---

# CORRECCIÓN MEDIDA (25 sep 2026) — el cartelito existe, y el dato que importa es otro

**Lo que este documento afirmaba y es falso**: que `PERK_TRIGGERED` se emite y se tira, y que ningún perk
se puede ver durante el partido. **Se puede.** *(MEDIDO, leyendo el código.)*

- **`Sim/Run/View/MatchFlashView.cs`** compone los avisos del partido (`MatchFlash`: fotograma, índice de
  jugador, equipo, id y **nombre del perk**), ordenados por RT-041, con `DurationFrames = 15` (1 s).
- **`MatchMomentView`** los devuelve como `MomentMark`, anotando si el aviso quedó **absorbido** por un
  momento del director.
- **`MatchPitchView.DrawFlashes`** (2D) y **`MatchPitchView3D.DrawMarks`** (3D, la pantalla vigente) los
  pintan: un **cartel de pergamino con el nombre del perk anclado sobre la cabeza del jugador**, tamaño
  fijo en pantalla, con el último tercio desvaneciéndose y **apilado** cuando coinciden dos.

Es exactamente el cartelito del encargo del revisor, y está desde C9.

**Por qué me equivoqué, que es lo aprovechable**: busqué `PerkTriggered` en `/Game` y sólo apareció un
comentario de `MatchEventSounds` diciendo que como **sonido** sería ruido. Generalicé de *no suena* a *no se
ve*. La ruta de dibujo no nombra el tipo de evento —va por `MatchFlash`— así que el `grep` no la tocó. Es
la pregunta 7 de `CLAUDE.md`, *«¿existe ya una abstracción del propio repositorio para esto?»*, saltada.

**Y el dato que sustituye al error es peor para el catálogo, no mejor** *(MEDIDO con un instrumento nuevo,
`BroadcastCapture.FindPerkBurst`, sobre seis partidos de semillas distintas)*:

| partido | avisos de perk | de ellos en los **primeros 6 s** | en los 84 s restantes |
|---|---:|---:|---:|
| 1 | 16 | 15 | **1** |
| 2 | 14 | 14 | **0** |
| 3 | 14 | 14 | **0** |
| 4 | 16 | 14 | 2 |
| 5 | 9 | 8 | **1** |
| 6 | 14 | 14 | **0** |

**Un partido entero produce entre cero y dos activaciones de perk fuera del saque inicial.** La causa está
medida y es la misma de siempre: **48 de los 102 perks se cuelgan de `MATCH_START`**, y ahí el pregón del
saque tapa media pantalla y no hay fútbol que mirar. La única activación en juego abierto que capturé
coincidía además con un momento de lesión grave, que la **absorbe** y la dibuja pequeña.

**DERIVADO, y refuerza la tesis en vez de debilitarla**: el canal de atribución no hay que construirlo —está
construido y funciona—. Lo que falta es **que haya algo que atribuir, y en un momento en el que se pueda
mirar**. Eso no se arregla en `/Game`: se arregla en el catálogo, colgando los actos de eventos de fútbol
en vez de del pitido inicial, que es precisamente lo que hace
[`perks-catalogo-de-actos.md`](./perks-catalogo-de-actos.md).
