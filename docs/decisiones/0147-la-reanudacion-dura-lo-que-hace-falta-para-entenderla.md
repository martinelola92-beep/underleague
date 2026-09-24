# ADR 0147 — La reanudación dura lo que hace falta para entenderla

**Fecha**: 24 sep 2026
**Estado**: aceptada — **decisión del revisor**, firmada explícitamente
**Enmienda**: RF-050, RF-053 y RF-054 de `docs/requisitos.md`
**Nota de diseño**: `docs/plan-balon-parado-posicional.md` (las diez preguntas de `game-design-review`)
**Continúa**: ADR 0143 (el balón parado es una jugada) y su enmienda de BC-A

## Contexto

Tres requisitos funcionales escritos cuando **el balón parado no era una jugada**:

- **RF-053**: las reanudaciones son *instantáneas*, con una animación de 1 s que **no** detiene el reloj.
- **RF-054**: *solo* detienen el partido el penalti y la tarjeta roja.
- **RF-050**: un partido dura entre **60 y 90 segundos** a velocidad x1.

La **ADR 0143** ya empezó a moverlos sin decirlo: hizo que el sacador decidiera dentro de su reanudación, y
su enmienda de BC-A paró el reloj del partido durante el balón muerto para que el equipo pudiera volver a su
formación **andando** en vez de teletransportarse. Esta ADR termina el trabajo y lo escribe.

**El detonante no fue una métrica, fue mirar el juego.** El revisor, sobre la build:

> *«Quiero que córner y puerta paren el reloj para dar tiempo a asimilar que ha pasado algo y que los
> jugadores se reposicionen. Con banda se para pero mucho menos tiempo (viendo el gameplay **hay veces que
> no te enteras qué pasa**).»*

## Decisión

### 1 · Una reanudación no es un trámite: es cuando el espectador se entera

Ése es el motivo, y conviene dejarlo escrito porque **no es el que yo había propuesto**. El análisis de
diseño separaba las reanudaciones por **fidelidad al fútbol** y concluía que el saque de banda no debía
parar el reloj, porque en el fútbol real nadie se recoloca en una banda. El revisor lo corrigió con un
criterio mejor: **legibilidad**. Si el balón sale y vuelve a entrar en un segundo, el jugador no ha visto
por qué salió.

Y ya estaba escrito en los principios de `CLAUDE.md`: *feedback simple > cinemáticas complejas — el jugador
no necesita una escena, necesita que nada desaparezca sin explicación*.

Por eso el **saque de puerta** entra en la lista de pausas largas aunque **no recoloque a nadie**: tras una
parada o un tiro fuera hay algo que entender.

### 2 · Son dos ejes, y hay que no confundirlos

| reanudación | ¿recoloca al equipo? | pausa |
|---|---|---|
| saque de centro | **sí, entero** | **larga** |
| córner | **sí, entero y con papeles** | **larga** |
| saque de puerta | **no** (sólo vaciar el área) | **larga** — por legibilidad |
| falta de tiro | barrera + área | **media** |
| saque de banda | **no** — ajuste local | **corta, pero no instantánea** |
| falta lejana | no | **corta** |

**La colocación sigue al fútbol; la pausa sigue a la comprensión.** Un saque de banda no reorganiza a nadie
—eso es fútbol de verdad, no una simplificación— pero tampoco es instantáneo.

### 3 · Lo que dicen ahora los tres requisitos

- **RF-053** deja de decir «instantáneas» y «no detiene el reloj». Las reanudaciones **duran**, con la tabla
  del §2, y **el reloj del partido está parado mientras tanto**.
- **RF-054** deja de ser una lista cerrada de dos. El penalti y la roja siguen siendo las **pausas
  dramáticas** —eso no cambia—, pero ya no son las únicas que detienen el reloj: la diferencia entre una
  pausa dramática y una reanudación es **qué se cuenta en ella**, no si el reloj corre.
- **RF-050** sube su techo. El revisor, textual: *«no me importa alargar el tiempo si eso produce mejor
  sensación»*.

**RF-055 no se toca**: el tiempo reglamentario sigue siendo **una sola fase** con reglas normales. Que el
reloj se pare en las reanudaciones no lo parte en tramos con reglas distintas, que es lo que RF-055 prohíbe.

### 4 · El reloj de pared, con número y con vigilancia

Estimado a partir de lo medido (~20 reanudaciones por partido, ~445 ticks parados hoy, ~115 s):

| | ticks parados | partido |
|---|---|---|
| hoy | ~445 | ~115 s |
| **con esta ADR** | ~**830** | ~**135 s** |

**Y lo caro no es el córner, es el saque de puerta**: ~7 por partido contra ~2. Con pausa larga se lleva
~315 ticks, la partida mayor de la cuenta, y es la única de las tres largas que no necesita el tiempo para
recolocar a nadie. **Si hay que recortar, se recorta ahí primero**, y queda dicho de antemano para que no se
recorte por donde sea más fácil.

## Consecuencias

- **RF-050 deja de ser una banda y pasa a ser un techo vigilado.** Perder el número del todo sería perder la
  restricción que impide que el partido se vaya a cinco minutos; el compromiso es que el reloj de pared se
  mide en cada lote y un exceso se discute, no se descubre.
- **El cansancio se abarata**, y no es un efecto menor. Cada tick parado es recuperación gratis: con el
  26,3 % del partido ya detenido, un jugador recupera ~26 % más por minuto de fútbol que antes de la ADR
  0143, y estas pausas lo empujan más. Si la **ADR 0142** quiere el cansancio como recurso gestionable, hay
  que decidir entre recuperación reducida en balón parado, separar energía de enfriamientos, o aceptarlo.
  **Las tres salidas están planteadas en la nota de diseño y ninguna decidida aquí.**
- **El principio que ordena qué corre y qué no**: *lo físico sigue el reloj de pared, lo que es disputa
  sigue el reloj del partido*. Un jugador **sí** recupera el resuello en una parada —por eso los equipos
  pierden tiempo— y un enfriamiento de entrada es físico: los dos corren con el tick del motor.
- **El córner pasa a ocurrir de verdad**, y con él el duelo aéreo de la ADR 0139 (Fuerza + radio corporal) y
  el centro de la ADR 0136, que hoy existen y casi nunca pasan donde deberían. No se añade mecánica: se hace
  que ocurra la que ya está pagada.
- **Más cuerpos en el área en cada córner = más contacto.** Temáticamente es el juego, pero
  `injuriesPerMatch` tiene banda RT-056 y hay que medirlo, no suponerlo.

## Lo que esta ADR NO decide

- **Órdenes de balón parado** («cuántos suben al córner»). Es la continuación natural y la decisión de
  gestión que el jugador querría tomar, pero sería una mecánica nueva con su propia ADR. Colarla aquí sería
  exactamente lo que la Regla B prohíbe.
- **`restartClearanceCells`**, más allá de que deje de ser un valor único. El análisis destapó que sus 2,0
  casillas son **13 m en horizontal y 19 m en vertical** —más que la barrera real de 9,15 m en los dos ejes,
  y seis veces los 2 m de un saque de banda—, así que no representa ninguna regla concreta. Merece su propia
  medición.
- **Bajar `regulationTicks`** para compensar el alargamiento. Sigue siendo una decisión abierta del revisor.

---

## Medido (2.000 partidos, semilla 1, contra línea base propia)

**Ninguna métrica fuera de banda.** Y lo que motivó el paquete queda resuelto con holgura:

| métrica | línea base | ahora | |
|---|---|---|---|
| **`tacklesPerMatch`** | 6,86 → **5,69 fuera** | **8,79** | **IN** (suelo 6,00) |
| `shotsPerMatch` | 8,30 | 8,95 | IN |
| `goalsPerMatch` | 2,14 | 2,31 | INFO |
| `possessionChanges` | 22,64 | 24,79 | IN (techo 28) |
| `ballThirdMaxShare` | 40,94 | 39,60 | IN |

### Pero el partido se ha vuelto bastante más violento, y la causa NO es la que parecía

| métrica | línea base | ahora | |
|---|---|---|---|
| `foulsPerMatch` | 4,73 | **7,96** | +68 % |
| `offBallTacklesPerMatch` | 2,45 | **4,62** | +89 % |
| `injuriesPerMatch` | 0,55 | **0,82** | IN, pero el techo es **0,90** |
| `yellowCardsPerMatch` | 0,22 | 0,40 | |
| `redCardsPerMatch` | 0,05 | 0,13 | |

La hipótesis natural era que lo causaban **las pausas largas**: cada tick parado drena enfriamientos sin que
se juegue, así que por minuto de fútbol todo el mundo llega más entero. Se midió apagando **sólo** la pausa
graduada y dejando el resto:

| | línea base | sin pausa graduada | todo |
|---|---|---|---|
| `tacklesPerMatch` | 6,86 | 7,94 | 8,79 |
| `foulsPerMatch` | 4,73 | **7,07** | 7,96 |
| `offBallTacklesPerMatch` | 2,45 | **4,26** | 4,62 |
| `injuriesPerMatch` | 0,55 | **0,72** | 0,82 |

**Dos tercios del aumento aparecen sin alargar ninguna pausa.** La conclusión inmediata era que la causa
dominante sería **la forma del saque de centro** —con el equipo comprimido, los dos bloques arrancan más
cerca y el juego empieza en contacto—, y esa conclusión **se escribió aquí y luego se midió**.

### La atribución era falsa, y conviene dejar escrito el experimento que la tumbó

Si la compresión fuera la causa, **reducirla** tenía que bajar la violencia. Se bajó
`kickoffPushCells` de 2,5 a 1,5 y se remidió (2.000 partidos, semilla 1):

| | línea base | `push` 2,5 | `push` **1,5** |
|---|---|---|---|
| `tacklesPerMatch` | 6,86 | 8,79 | **9,23** |
| `foulsPerMatch` | 4,73 | 7,96 | **8,08** |
| `offBallTacklesPerMatch` | 2,45 | 4,62 | **4,67** |
| `injuriesPerMatch` | 0,55 | 0,82 | **0,90 — justo en el techo** |

**Comprimir menos no baja la violencia: la sube un poco.** → La hipótesis «lo causa la forma del saque de
centro» queda **REJECTED**, y con ella el que `kickoffPushCells` sea el dial de la violencia: no lo es.

Lo que queda establecido, y es menos cómodo pero más honesto: **el aumento se ha ido acumulando a lo largo
de toda esta línea de trabajo** —la parada del reloj y las reanudaciones andando de la ADR 0143 primero, la
pausa graduada después— y **no se ha aislado a una sola pieza**. Es una LIKELY con el mecanismo sin
determinar, no una CONFIRMED.

**No se calibra aquí, y es deliberado**: todo lo de partido está en banda y el revisor pidió que no se
balanceara todavía. Pero hay que decir dos cosas:

- **`injuriesPerMatch` en 0,82 contra un techo de 0,90 es lo más ajustado que deja este paquete**, y con
  `push` 1,5 se pone **exactamente en el techo**. De las dos opciones medidas, **2,5 es la más segura**, que
  es por lo que se queda.
- **El dial de la violencia está sin encontrar.** Quien lo busque, que empiece por el enfriamiento en balón
  parado —la salida (a) de la nota de diseño—, que es el mecanismo que sí se anticipó y nunca se aisló.

### Dos hallazgos del camino, que valen más que el cambio

1. **Esperar a una formación exacta es esperar a algo que el motor no puede producir.** La primera versión
   exigía a cada jugador estar a menos de `inPlaceCells` de su punto: con el equipo comprimido, la
   separación de cuerpos (ADR 0020) los empuja unos de otros y **79 de 120 saques de centro agotaban el
   tope** —medidos dos jugadores desplazados a las filas 2,34 y 4,66 empujándose entre sí—. La condición
   correcta no es el punto sino **la regla**: nadie en campo contrario y el que no saca fuera del círculo.
   Es además lo que comprueba un árbitro de verdad.
2. **Un test puede cablear un dato y luego acusar al motor.** `AWhistledFoulRestartsWithAFreeKick...` busca
   el saque de falta en una ventana de 12 ticks. Al subir `freeKickTicks` de 8 a 20, dejó de ver **un solo
   saque de falta de 558** y lo reportó como regresión del motor. No lo era: la reanudación ya no cabía en
   su ventana. Ahora la ventana sale del dato.

### Y una puerta de run que se rompe, con su requisito

`FullRunGateTests.TheGoldOfAnActPaysTwoOrThreeSinksAndNeverAllOfThem` pasa de **0** a **0,186**: en el
18,6 % de los actos caben ya **los cuatro** sumideros, y **RF-114k dice que nunca**.

El mecanismo, **LIKELY y sin aislar**: `SinksAffordable` abarata los rerolls cuando hay menos victorias en
el acto, y el partido se ha vuelto más caótico —`betterTeamWinRate_human_60_vs_human_40` baja de 88,29 a
80,78—, así que el jugador gana menos y el presupuesto del acto da para más. Es la violencia de arriba
propagándose a la economía de la run por la vía del resultado.

**Se publica rota y anotada**, no arreglada de madrugada: tocar la economía para tapar un efecto cuyo
mecanismo todavía es LIKELY sería exactamente el ajuste silencioso que RT-057 prohíbe.

## Foto de puertas

**6 rojas contra las 7 que traía `main`.** Se arreglan las dos de `tacklesPerMatch`, mejoran cuatro
heredadas —`orc_misplaced` y `elf_brawler` vuelven a banda, `undead_none` 62,38 → 60,45,
`grimhold_guns_correct` entra— y **se rompe una nueva**, la de economía de arriba.
