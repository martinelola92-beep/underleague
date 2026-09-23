# ADR 0136 — Centrar: el pase alto que se remata sin controlar

**Fecha:** 23 sep 2026
**Estado:** Aceptada (decisión del revisor) — implementada, pendiente del lote
**Cierra / ataca:** [BA-E](../pendientes/BA-E.md) (goles sin ángulo), por la vía que la [ADR 0111](./0111-findspace-global-rechazada.md) dejó escrita: **local al delantero en zona de remate**, no global
**Desbloquea:** el paso 3 de la [ADR 0135](./0135-el-balon-tiene-altura.md) (la portería disponible)
**Diseño previo:** `docs/plan-altura-del-balon.md` §7 (decisión del revisor), §8 (`game-design-review`, diez preguntas) y §9 (`architecture-review`)
**Requisitos:** RF-057, RF-012d · **reabre** la exclusión «los pases siguen rasos» de la ADR 0135 §2 para un único caso · no toca la calibración del pase de la [ADR 0091](./0091-el-pase-en-profundidad-es-una-carrera.md)

## Lo que decide el revisor

> «Eso creo que también arregla el problema de que el delantero se posiciona en la línea de fondo creyendo
> que es el mejor sitio cuando en realidad no lo es. También forzará que centre balones buscando compañeros
> rematadores en mejor posición.»

Y, sobre qué es centrar, tres cosas:

1. **Un pase alto** a un compañero cerca del área.
2. Ese compañero **remata sin controlar**: no recibe y luego dispara, remata de primeras.
3. Para distinguir el remate del tiro, que se apoye en **fuerza** en vez de en técnica.

Y una sobre el orden, que es la que más importa:

> «las puertas están rojas pero hay que ignorarlas hasta implementar acción "centrar"»

**Primero el centro, después el ángulo.** El ángulo en la utilidad de `Shoot` es la vía (B) de BA-E y ya se
midió **sola**: seis puertas en rojo y la cadena de pases más corta, porque le quita el tiro al delantero
sin darle nada a cambio. Con el centro, la jugada no se muere: se transforma.

## Contexto: el problema, medido hace días

`docs/ba-e-goles-sin-angulo.md` §1, censo de 1.912 tiros:

- **32,4 % de los tiros** salen con apertura < 0,5 —más de 60° fuera de la perpendicular— y producen el
  **37,1 % de los goles**: convierten **mejor** que la media (ratio 1,14).
- **30,1 %** se tiran a menos de una casilla de la línea de gol.

No es una impresión del revisor: es un incentivo real del motor. Y las dos correcciones obvias ya están
descartadas con sus números — (A) `FindSpace` hacia la portería, **RECHAZADA** por la ADR 0111 (ocho
puertas en rojo; cambiar la regla de desmarque global aplana el juego de colocación), y (B) el ángulo en la
utilidad de `Shoot`, que sola pone seis.

Lo que faltaba no era un castigo mejor calibrado: era **una alternativa**.

## Decisión

**`Cross` es una acción nueva de la IA de utilidad**: un pase **alto** a un compañero de la zona de remate
que tiene **mejor apertura a portería** que el pasador, y que al llegar el balón lo **remata de primeras**
sin llegar a controlarlo.

### Las tres piezas, y por qué cada una

**1. El pase es alto, y por eso existe.** `UpdateFlight` ya sabía describir una parábola desde el paso 2 de
la ADR 0135; lo que faltaba era que la altura **decidiera** algo. Con este paquete, `TryIntercept` pasa de
medir un **círculo en el plano** a medir una **esfera** (`sqrt(d2D² + z²) < alcance`), que es la línea que
dejó escrita la revisión de arquitectura de la ADR 0135. A mitad de vuelo el centro va por encima del radio
y **no hay intercepción posible**; en los extremos va bajo y sí la hay. Sin esto, un centro es un pase largo
con otro nombre.

**Para los tres pases rasos el cálculo es bit a bit el de antes**, garantizado por un atajo explícito
(`z <= 0` devuelve la distancia en el plano sin pasar por la raíz) y fijado por test. Importa: si la
circulación normal cambiara, ninguna desviación del lote sería atribuible al centro.

**2. La altura del centro es absoluta, no proporcional a la distancia.** Se implementó primero
proporcional —`arcCellsPerCellMilli`, como el tiro— y **un test lo tumbó antes de medir nada**: un centro
corto desde el cordel (3,6 casillas) hacía pico **0,897** contra un radio de **0,9**, así que se
interceptaba igual que un pase raso y la acción no habría hecho nada. Con una altura fija
(`peakHeightCellsMilli` 1400) la invariante «un centro **siempre** despega por encima del radio» se
comprueba con un solo número, y además es lo que hace un centro de verdad: se levanta para salvar a los
defensas, no en proporción a lo lejos que esté el que centra.

**3. El remate se apoya en la fuerza, y es el mismo sistema que el tiro.** No hay una resolución nueva: el
remate pasa por `LaunchShot` con los **mismos términos y los factores invertidos**
(`volleyStrengthFactor` 18 / `volleyTechniqueFactor` 4, contra los 4/14 del tiro), más
`volleyOffTargetPenalty`, el precio de golpear sin controlar. La tirada de dentro/fuera, el punto de mira,
el marco, el bloqueo y la parada son los del tiro, porque **un remate es un tiro**.

El reparto se fija por test como **invariante de dato**, no como resultado estadístico: es la decisión de
diseño —el tiro es *colocar*, el remate es *llegar y empujarla*—, y medirla con goles sería medir la
calibración, que sí puede moverse.

### Las dos precondiciones duras, y sólo dos

Un compañero es candidato si está **entre `crossMinCells` y `crossMaxCells`** (un centro es un balón largo:
por debajo no es un centro, es un pase corto con comba) y **dentro de `crossTargetGoalDistanceCells` de la
portería rival**. Todo lo demás puntúa y nunca descarta, que es la forma que el repositorio usa desde el
paso 3 de la ADR 0091.

Y una condición que es del diseño, no del alcance: **su apertura tiene que ser estrictamente mejor que la
del pasador**. Un centro a alguien peor colocado no es un centro, es un mal pase. Ese término —pagar por
centésima de apertura ganada— es lo que hace la acción **local a la zona de remate**, que es exactamente lo
que la ADR 0111 exigía: desde el cordel a alguien de frente cobra casi todo; entre dos posiciones parecidas
no cobra nada. La acción sólo existe donde el problema existe.

### Dos decisiones menores, tomadas sin consultar y aquí registradas

- **El centro cuenta como pase completado** a efectos de cadena (`passChainAvgLength`) y de asistencia,
  porque lo es: el balón fue de un jugador a otro. Lo contrario habría hecho que la métrica de cadena
  midiera algo distinto antes y después, justo cuando es una de las que hay que vigilar.
- **Un centro que nadie remata queda suelto igual que cualquier pase fallado**, con la misma velocidad y la
  misma fricción. **No se inventa aquí el despeje**: eso es el paso 4 de la ADR 0135, y adelantarlo
  mezclaría dos calibrados.

## Un defecto encontrado por el camino, y arreglado donde vivía

**`FlightArc` y `FlightTargetZ` los escribía sólo `LaunchShot`, y nadie los limpiaba al recuperar el
balón** (`SetOwner` no los toca). Así que **el primer pase después de una parada volaba con la comba del
tiro parado y terminaba a la altura a la que el tirador había apuntado.** Era inerte mientras ninguna regla
leía `_ball.Z` —sólo contaminaba la traza, y con ella lo que `/Game` dibujará en el paso 5—, pero con la
intercepción en esfera de esta misma ADR se habría convertido en **pases rasos ininterceptables**.

Arreglado donde vive la regla: **un pase declara su altura, no la hereda**. `LaunchPass` fija las dos
siempre, cero para los rasos. Con test propio.

Y un segundo, del mismo tipo: **`MatchPlayer.ActionCount` estaba clavado a `(int)PlayerAction.Block + 1`**,
o sea «la última acción más uno». Eso convertía en trampa silenciosa justamente lo que el propio enum manda
hacer —añadir las acciones nuevas **al final** para no mover el desempate de las anteriores (RT-097)—:
añadir `Cross` reventó 234 tests con `IndexOutOfRange`. Se cuenta del enum, que es el patrón que
`DataLoader` y `EffectEngine` ya usaban.

## El censo de apertura deja de ser un script

La cifra que decide si esto funcionó es la de BA-E: % de tiros con apertura < 0,5, % desde la línea de
fondo, apertura media, y si los tiros sin ángulo siguen convirtiendo mejor que los demás. Se medía con un
censo ad-hoc. Pasa a ser **instrumento del motor**, medido en `LaunchShot` con la posición desde la que se
disparó —después ya no se sabe, el balón se mueve— y con **el mismo umbral exacto** (apertura < 50) con el
que se midió el problema: una métrica que cambia de definición entre el diagnóstico y la cura no demuestra
nada.

## Enmienda 1 (23 sep 2026): el rematador marcado PUNTÚA, no descarta — y con eso el centro existe de verdad

> «Tiros sin ángulo y tiros desde línea de fondo me siguen pareciendo demasiados y los centros muy pocos.
> Si esto hace que los goles bajen ya miraremos más adelante si la precisión debe ponderar más» — revisor

Tenía razón, y la causa **no era el peso**: era una precondición que la primera versión **copió del pase
raso sin pensarla**. Un compañero dejaba de ser candidato si tenía un rival a menos de una casilla. Eso es
importar la regla del pase al suelo a la acción cuyo propósito es precisamente **no jugar por el suelo**: un
centro es el balón que se pone **cuando el área está poblada**, y por eso se pone por arriba. Además
contradecía la convención del propio repositorio desde el paso 3 de la ADR 0091: *el pasillo puntúa, nunca
descarta*.

Pasa a ser un término con su propio peso, `crossMarkedTargetPenalty`, **publicado en 0**. Y 0 no es
«todavía sin calibrar»: significa que la **decisión** de centrar no mira si el rematador está marcado,
porque **el precio ya se cobra aguas abajo** — `LaunchShot` aplica `shot.pressurePenalty` contando los
rivales cerca del rematador, así que un remate en disputa ya sale con peor calidad. Penalizarlo también en
la decisión sería cobrarlo dos veces por lo mismo.

**Medido**, 10.000 partidos × 2 semillas, contra el baseline (que es byte a byte HEAD, ver abajo):

| | baseline | filtro duro | pen. 150 | pen. 50 | **pen. 0** |
|---|---|---|---|---|---|
| `crossesPerMatch` | 0 / 0 | 0,54 / 0,71 | 1,52 / 1,77 | 1,88 / 2,09 | **2,13 / 2,33** |
| `lowApertureShotShare` | 25,77 / 29,00 | 24,56 / 27,18 | 18,42 / 19,59 | 15,99 / 16,95 | **14,31 / 14,86** |
| `bylineShotShare` | 23,30 / 26,53 | 22,46 / 25,14 | 16,96 / 17,95 | 14,61 / 15,40 | **13,04 / 13,43** |
| `shotAperture` | 69,11 / 65,59 | 70,18 / 67,19 | 74,54 / 72,70 | 76,94 / 75,50 | **78,52 / 77,35** |
| `goalsPerMatch` | 2,73 / 2,35 | 2,70 / 2,32 | 2,57 / 2,17 | 2,50 / 2,11 | **2,46 / 2,07** |

**Los tiros desde la línea de fondo se quedan en la mitad**, y los tiros sin ángulo bajan un 45-49 %
relativo. Es del mismo orden que la palanca (A) de BA-E —32,4 → 12,2 %—, que la ADR 0111 **rechazó por
poner ocho puertas en rojo**; ésta no pone ninguna.

**El coste, dicho sin adornos: −0,27 goles por partido**, consistente en las dos semillas. Es coherente con
el diagnóstico y no una sorpresa: los tiros sin ángulo convertían **mejor** que la media (ratio 1,14 en
BA-E), así que quitarlos tiene que bajar el marcador. **Queda explícitamente aparcado por decisión del
revisor** —«ya miraremos más adelante si la precisión debe ponderar más»— y es lo primero que hay que mirar
cuando se retome, junto con el paso 3.

Y la zona de remate sube de 4 a 5 casillas en la misma tanda: con 4 el centro salía 0,54 veces por partido
—demasiado raro para ser una alternativa—, con 5 sale 1,4-1,7 y con 6 ya no gana nada (1,62). El codo está
en 5.

## Consecuencias

**Todas las semillas cambian**, como en todos los pasos de la ADR 0135: una acción nueva desplaza el
consumo de RNG desde la primera vez que se elige. El recuento de partidos distintos no es un efecto; lo que
dice si el juego cambió es el agregado.

**Lo que hay que vigilar, dicho antes del lote** (§8.9 del plan, con su guarda cada uno):

| riesgo | métrica que lo delata |
|---|---|
| el remate se vuelve la máquina de goles | `goalsPerMatch`, `scorelineShare_1-0_to_3-2` (puerta) |
| el centro infla la cadena | `passChainAvgLength` (banda 2-4) |
| el centro sustituye al pase normal | `crossesPerMatch`, `passCompletionRate` |
| un centro sin rematar deja el balón muerto en el área | `possessionChanges` (banda 12-28), y [BC-G](../pendientes/BC-G.md) |

**Regla 11 (nada malo sin ser previsible):** el centro **no añade ninguna vía de daño**. No hay lesión, ni
muerte, ni falta nueva. Un remate es un tiro, y un tiro no hiere a nadie.

**Las puertas vuelven a ser criterio de parada** con el centro dentro, contando que **cuatro ya estaban
rojas antes de toda esta familia** y son de [BF-B](../pendientes/BF-B.md) (`orc_misplaced` 45,18, rareza
43,75, doctrinas), comprobado contra árbol limpio.

## Alternativas rechazadas

- **Un centro raso**, sin altura. Más barato y no reabre la exclusión de la ADR 0135. **Rechazada**: lo
  intercepta el mismo defensa que intercepta todo lo demás, así que no es un centro, es un `LongPass`.
- **Que el rematador controle y luego dispare.** **Rechazada por el revisor**, y con motivo mecánico: si
  controla, la acción no se distingue de un pase y el ángulo lo vuelve a decidir el tirador. El remate de
  primeras es lo que convierte la **posición del rematador** en la decisión.
- **Que rematar sea una acción sin balón del rematador.** **Rechazada**: obligaría a que dos jugadores
  coordinaran decisiones en ticks distintos, que es la clase de estado compartido que este repositorio
  evita. El centro es una decisión de **uno**; el remate es su resolución.
- **Una librería de físicas opensource** para todo esto (pregunta del revisor, 23 sep). **Rechazada**, con
  el detalle en `docs/plan-altura-del-balon.md` §9.bis: ya estaba descartada con motivo en la ADR 0002, no
  hay física de cuerpos rígidos que resolver —una línea de parábola y unas distancias—, y ninguna candidata
  promete determinismo entre Windows y Linux, que es puerta de CI (RT-024).

## Lo medido (23 sep 2026)

**El experimento salió separable, que era lo que más podía costar.** Con el centro apagado por dato
(`crossTargetGoalDistanceCells = 0`) el paquete entero es **byte a byte HEAD**: **0 de 10.000 partidos
difieren**, columna a columna. Ocurre porque los dos arreglos latentes **se cancelan por construcción** —
antes había intercepción plana con una comba heredada que ninguna regla leía; ahora hay esfera y ninguna
comba heredada—. Consecuencia: todo lo que se mueva en la comparación siguiente es **del centro y de nada
más**, sin necesidad de argumentarlo.

Lo confirma un segundo instrumento, independiente y más fino: con el centro apagado, los cuatro perks de
`RecoveryExtraActionTests` reproducen **exactamente** sus totales históricos (`charge` 11, `lane_reader` 19,
`sweeper_keeper` 17, `road_warrior` 0).

**Los cuatro riesgos que el diseño mandaba vigilar (§8.9 del plan) no se materializan**: `goalsPerMatch` no
se dispara (baja), `passChainAvgLength` 1,97 → 2,00 (banda 1,80-3,50), `possessionChanges` 21,82 → 21,73
(banda 12-28) y `shotsPerMatch` 9,00 → 8,66 (banda 7-15). El remate no es una máquina de goles: aporta el
7,8-8,8 % de los goles con el 49 % de los centros acabando en remate.

**Un efecto de segundo orden que sí apareció, y es benigno.** `road_warrior` —un perk que llevaba **cinco**
desplazamientos de semilla clavado en 0 activaciones— pasa a activarse. Medido que lo causa el centro y no
la deriva (con el centro apagado vuelve a 0), y la vía es **la que el diseño predijo**: un centro que nadie
remata deja el balón suelto en el área, alguien lo recupera, y eso es un `RECOVERY`, que es su disparador.
No es el disparador ensanchándose: es que ahora hay recuperaciones donde antes no las había.

## Las puertas (23 sep 2026)

**41 de 43 en verde.** Las dos rojas son las dos que ya estaban fichadas, en valores indistinguibles de los
que sus propias fichas documentan:

| puerta | ahora | documentado | ficha |
|---|---|---|---|
| `badBuildsLoseToNone_orc_misplaced` | 45,05 | 45,18 | [BF-B](../pendientes/BF-B.md) |
| `badBuildsLoseToNone_elf_brawler` | 47,01 | 46,64 | [BF-A](../pendientes/BF-A.md) |

`orc_misplaced` incluso **mejora**, y `elf_brawler` se mueve +0,37 sobre un error típico de 0,57.

**La atribución está CONFIRMED, no supuesta.** La revisión independiente señaló que atribuirlas a BF-A y
BF-B era *consistente con lo medido pero no aislado*, y que el experimento que discrimina existía y no se
había hecho: correr las 43 puertas **con el centro apagado por dato**, que —al ser byte a byte HEAD— da el
estado de puertas del baseline real. Hecho:

| | puertas rojas |
|---|---|
| **centro apagado (= HEAD byte a byte)** | **2** — `elf_brawler` 45,96 · `elf_out_of_zone` 45,70 |
| **centro puesto** | **2** — `elf_brawler` 47,01 · `orc_misplaced` 45,05 |

**El baseline ya tiene dos rojas y el paquete no añade ninguna.** Las cuatro son de la misma familia
—`badBuildsLoseToNone_*` rozando el techo de 45— y la segunda **intercambia identidad** entre
`elf_out_of_zone` y `orc_misplaced`, que es exactamente lo que [BF-A](../pendientes/BF-A.md) y
[BF-B](../pendientes/BF-B.md) dicen de sí mismas.

**Dos cosas que conviene no maquillar:**

1. La creencia de que **«cuatro puertas ya estaban rojas»** no coincide con lo medido hoy: el baseline real
   tiene **dos**. Esa cifra venía de un árbol anterior.
2. `elf_brawler` sube **45,96 → 47,01**, que son ~1,8 errores típicos (σ 0,57). No es concluyente, pero
   tampoco es ruido puro, y hay un mecanismo plausible: el remate se apoya en la **fuerza**, y
   `elf_brawler` es precisamente una build de elfos técnicos con perks de pelea, así que el centro le da
   una vía que antes no tenía. **Queda anotado en BF-A como hipótesis LIKELY con mecanismo**, no como
   ruido descartado.

## Lo que la revisión independiente corrigió (23 sep 2026)

Le llegó el problema, el diff y los tests, **sin** la argumentación del implementador (Regla E). Encontró
cuatro defectos y desmontó tres afirmaciones. Todo lo de abajo está arreglado o corregido en este mismo
paquete.

### Cuatro defectos, arreglados

1. **El centro secuestrado.** Entre la decisión y el lanzamiento pasan cinco ticks; si el receptor elegido
   deja de estar en el campo, `LaunchPass` lo sustituye por el compañero más adelantado — **y el vuelo
   seguía saliendo como centro**, con comba de 1,4, hacia alguien que no cumplía **ninguna** de las dos
   precondiciones duras, que además remataba de primeras. Ahora, si el receptor no es el elegido, la acción
   **degenera a pase raso**, que es el caso `z = 0` del modelo. Con test.
2. **`RepeatShot` perdía la identidad del remate**: un «Doble disparo» sobre un remate repetía con
   **técnica** en vez de con fuerza, y el gol repetido no contaba en `VolleyGoals`. La primera mitad de la
   jugada era un remate y la segunda otra cosa.
3. **El defecto latente seguía vivo en su hermano.** Esta ADR arregló la herencia de altura en `LaunchPass`,
   pero **el defecto nace en `SetOwner`**, que limpiaba media docena de campos del vuelo y dejaba sin tocar
   los de altura. Arreglarlo sólo en quien lanza deja el patrón vivo para la próxima regla que lea uno de
   esos campos fuera de vuelo. Se limpia donde el vuelo termina.
4. **Dato incoherente**: se añadió la plantilla de `CROSS` a `data/l10n/` pero `perks.schema.json` no
   admitía `CROSS` como disparador, así que **ningún perk podía reaccionar a un centro** y la traducción
   era letra muerta. Añadido al esquema.

### Tres afirmaciones que no estaban demostradas

- **«El remate es de peor calidad que el tiro salvo para los fuertes»**, declarado como guarda contra la
  degeneración. **La revisión tiene razón sobre la fórmula**: con los valores publicados, a 50/50 el remate
  da 54 y el tiro 55, y el punto de corte está en fuerza ≈ 59, así que para cualquier criatura por encima
  de la media el remate tiene **mejor calidad** que su propio tiro. La guarda no está donde la ADR decía.
  **Pero existe, y ahora está medida**: el remate **convierte al 18,3 / 16,6 %** contra el **28,4 / 26,9 %**
  de un tiro medio, **ratio 0,62-0,64**. El precio no es la calidad: es `volleyOffTargetPenalty` más la
  presión que el rematador tiene encima al llegar el balón, que `LaunchShot` ya cobra. La afirmación
  correcta es la de conversión, no la de calidad.
- **«La decisión es de alineación: ¿el técnico o el bestia arriba?»** — que es la justificación entera de la
  mecánica en §8.2 del plan. **No está en el código**: `Utility.EvaluateCross` puntúa al candidato por
  apertura ganada, pasillo y marcaje, y **no mira la fuerza del rematador en ningún término**. La fantasía
  se realiza sólo aguas abajo, por accidente de quién esté en el área. **No se arregla en este paquete a
  propósito**: meter la fuerza del receptor en la utilidad es un término nuevo de la IA y pide su propio
  `game-design-review` y su propio lote, no un añadido al final de una tanda. Queda como **el primer
  seguimiento**, y hasta entonces la ADR no puede reclamar esa decisión de alineación.
- **«El centro vuela por encima del que lo interceptaría»**, que es la razón de ser de la pieza 1: los
  tests medían el **pico** del vuelo, no la intercepción. Añadidos los dos que faltaban: a media altura un
  defensa justo debajo **no** alcanza el balón, y en los extremos **sí** — y un pase raso es alcanzable
  **siempre**.

### Y dos cosas que la revisión dejó anotadas y no se han hecho

- **El remate no paga «wind-up».** Un tiro normal pasa cinco ticks en `PlayerState.Shooting` antes de
  salir; el remate se dispara dentro de `UpdateFlight`, **instantáneo**. Son cinco ticks menos de exposición
  a una entrada. Puede ser correcto de ficción —un remate de primeras es instantáneo por definición— pero
  es una decisión de diseño que no pasó por revisión ni se midió, y no estaba enumerada entre las guardas.
- **El lote no publica `shotsOnTargetShare`, `saveRate` ni `blockRate`**, y el remate añade una clase de
  tiro con +800 de puntería sobre la base. Con 2,1-2,3 centros por partido y el 49 % rematados es
  ~1 tiro de cada 9. Medidos y publicados abajo.

### Las tres métricas que faltaban, y la conversión del remate

| | baseline (s1/s2) | con el centro |
|---|---|---|
| `shotsOnTargetShare` | 74,93 / 75,29 | 72,10 / 71,99 |
| `saveRate` | 52,38 / 56,16 | 53,53 / 57,19 |
| `blockRate` | 1,32 / 1,49 | 1,45 / 1,72 |
| **conversión del remate** | — | **18,3 % / 16,6 %** |
| conversión de un tiro medio | 28,4 % / 26,9 % | 28,4 % / 26,9 % |

Los tres se mueven en la dirección que predice la mecánica y por la magnitud que le corresponde: el remate
va menos a puerta (paga `volleyOffTargetPenalty`), así que la cuota de tiros a puerta baja ~3 puntos; y los
remates salen desde el área, donde hay cuerpos, así que se bloquean más. Ninguno tiene banda.

## Cómo se demuestra

- **`Sim.Tests/Engine/CrossTests.cs`**, doce tests: el alcance esférico coincide exactamente con el plano
  en el suelo y crece con la altura; un centro supera el radio de intercepción y un pase corto entre los
  mismos puntos no se despega; el centro vuelve al suelo en el destino; **el rematador nunca llega a ser
  dueño del balón**; el reparto fuerza/técnica y la altura contra el radio, como invariantes de dato; se
  centra y se remata en partidos reales; con la zona de remate a cero no se centra nunca; y un pase raso no
  hereda la comba de un centro anterior.
- **Lote de `/Balance`** de 10.000 partidos contra el baseline del mismo árbol, con **todas** las métricas
  (lección de `ChaseBall pen=50`), y las nuevas de centro y de apertura.
- **BA-E medida otra vez**, con el instrumento nuevo y el umbral de siempre.
- **Las 43 puertas**, en una sola invocación.
- **La vía (B) se REMIDE con el centro dentro.** Las seis rojas conocidas son de la (B) *sola*; darlas por
  buenas sería repetir un experimento cuyo resultado ya se conoce sin la pieza que lo cambia.
- **RT-024** en verde: crece la aritmética `float` (una raíz más por intercepción), aunque no su naturaleza.
