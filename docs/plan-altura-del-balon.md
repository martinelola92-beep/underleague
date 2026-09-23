# Plan: el balón tiene altura, y el toque defensivo puede desviarlo

**Estado: diseño, sin implementar.** Decisión del revisor (23 sep 2026), en dos partes:

> «En la vida real un tiro que intercepta un jugador rival puede causar dos situaciones: lo intercepta y se
> lo queda, lo toca/pega y se desvía (el balón no debe ser pegajoso). Con un portero pasa lo mismo: puede
> atraparlo o despejarlo. […] el balón debe tener físicas lo más fieles posibles a la realidad y el juego
> tiene que sentirse real.»
>
> «Sobre las físicas del balón igual es el momento de darle altura al balón, esto puede ser importante a la
> hora de decidir cómo se intercepta.»

Y, preguntado por el alcance, eligió **física real con `z` y gravedad** frente a una altura derivada sin
estado.

Este documento responde las diez preguntas de `game-design-review` **antes** de tocar código (Regla B), y
propone el troceado. No es el ADR: el ADR sale de aquí una vez el revisor confirme el alcance.

---

## 0. El hecho que lo hace necesario, medido

El balón de Underleague es **pegajoso y plano**. Medido en 2 000 partidos de referencia:

| desenlace | por partido |
|---|---|
| `Shot onTarget` | 5,69 |
| `Save save` | 3,18 |
| `Goal goal` | 2,00 |
| `Shot offTarget` | 1,74 |
| `ShotBlocked blocked` | **0,12** |
| `Save penalty` | 0,02 |

De ahí salen dos hechos:

1. **Ningún toque defensivo produce nunca un balón vivo.** El bloqueo de un tiro hace
   `SetLoose(new Vec2(0f, 0f))` — velocidad **cero**, el balón se queda donde estaba—, y de los tres
   `SetLoose` del motor sólo el del pase fallado da velocidad. Una parada deja el balón en poder del
   portero. Por eso [BB-N](./pendientes/BB-N.md) midió **1 córner en 2 000 partidos**: no hay rechaces, no
   hay segundas jugadas, no hay balones muertos en el área.
2. **El balón nunca se despega del suelo.** `Game/Ui/MatchPitchView3D.cs:1364` lo dibuja con
   `Y = BallRadius` constante. En un render 3D con cámara dinámica (ADR 0114) eso significa que **todos los
   tiros son rasos**, siempre.

Y un tercer hecho, que es el que convierte la altura de capricho en oportunidad: **la ficción del juego ya
usa la altura y el motor no la conoce.** La descripción de los enanos dice «**Bajos**, tercos y difíciles de
mover». Hoy eso no significa nada mecánicamente: `bodyRadius` (28-38 centicasillas según la raza) es un
radio **horizontal**, para separar cuerpos, y todas las razas ocupan `cellsOccupied: 1`.

---

## 1. Las diez preguntas

**1. ¿Qué experimenta el jugador?** Hoy: un tiro sale, o es gol, o el portero lo atrapa, y la jugada muere
ahí. Siempre. Con el cambio: ve el balón subir y bajar, ve al portero despejar a córner, ve un rechace que
cae en el área y a dos criaturas peleándose por él, ve un tiro irse **por encima** del larguero. El partido
deja de ser una sucesión de jugadas limpias y pasa a tener el desorden que tiene un partido de verdad —que
es además el tono del juego: carnicería administrada, no fútbol de salón.

**2. ¿Qué decisión toma el jugador con esto?** Hoy, ninguna: no hay rechaces, así que no hay nada que
anticipar. Con el cambio, la decisión aparece en **la alineación**, que es donde viven todas las decisiones
de Underleague (los partidos se resuelven solos): a quién pongo cerca del área para recoger un rechace, y
qué criatura pongo de portero sabiendo que su alcance vertical decide qué tiros ataja y cuáles sólo puede
despejar.

**3. ¿Qué decisión DEBERÍA tomar?** Exactamente esa, y hoy no puede porque el motor no distingue. Es el
principio de `comportamiento observable > modificadores numéricos invisibles`: un portero bajito que
despeja en vez de atrapar es legible sin leer un número.

**4. ¿Qué regla del juego representa?** Ninguna escrita — **y hay que decirlo claro: esto inventa reglas
nuevas**, no implementa una existente. Toca RF-050 (duración del partido), RF-057 y siguientes (tiro y
parada) y la ADR 0091 (física del pase), que fijó el pase al pie y la intercepción con geometría; este plan
**no toca el pase** (ver §2). La altura de portería no existe hoy: `Pitch.GoalCenter` es un punto y no hay
ni ancho ni alto declarados. Habrá que inventarla, y eso es regla nueva, con ADR.

**5. ¿Qué sistemas intervienen?**
- `/Sim`: `Ball` (estado nuevo `Z`, `VelocityZ`), `UpdateFlight`/`UpdateLooseBall` (gravedad y bote),
  `TryGoalkeeperReach` y `ResolveBlock`/`TryIntercept` (el alcance pasa de círculo a volumen),
  `ResolveShotArrival` (el tiro puede irse por arriba), `CheckOutOfBounds`.
- `MatchTrace`: graba la altura, para que la pantalla pueda dibujarla sin calcular nada (RT-014).
- `/Game`: `MatchPitchView3D.cs:1364`, que hoy fija la altura. Una línea.
- `/data`: `sim/tuning.json` (gravedad, restitución, alcances), y **más adelante** `races/*.json`.

No hay lógica repartida: `/Sim` decide la altura y `/Game` la dibuja. La frontera se respeta.

**6. ¿Hay alternativas?** Sí, y se consideraron tres:
- **Altura derivada sin estado**: la altura es función del progreso del vuelo y del tipo de golpeo. Sin
  gravedad ni botes, cero estado nuevo, determinista por construcción. Da el 80 % (decidir intercepción y
  dibujar) por una fracción del coste. **Rechazada por el revisor** a favor de la física real.
- **Sólo el rechace, en 2D**: más barato y da córners ya, pero las reglas del rechace habría que rehacerlas
  al llegar la altura. Es construir sobre un modelo que se sabe que va a cambiar.
- **No hacer nada**: deja BB-N sin solución y el balón pegado al suelo en un render 3D.

**7. ¿Qué trade-off introduce?** El grande: **más caos**. Un rechace es un balón que nadie controla, y eso
resta previsibilidad a un juego cuyo principio rector es que todo lo malo debe poder anticiparse (RF-012d).
La defensa es que el caos ocurra **donde el jugador lo ha colocado**: si pone gente cerca del área, recoge
rechaces; si no, los recoge el rival. El coste de oportunidad es legible. El segundo trade-off es de
cómputo y de superficie de determinismo (§3).

**8. ¿Cómo cambia las estrategias posibles?** Abre una dimensión de alineación que hoy no existe: el
rematador que vive del rechace, el portero elegido por alcance y no sólo por atributos, la criatura alta que
despeja centros. Y le da sentido mecánico a una ficción que ya está escrita —el enano bajo— en vez de
inventar contenido nuevo.

**9. ¿Puede degenerar?** Sí, y por tres sitios que hay que vigilar desde el primer lote:
- **Los goles se disparan.** Si el portero sólo despeja y nunca atrapa, cada tiro genera una segunda
  ocasión. `goalsPerMatch` y `scorelineShare_1-0_to_3-2` (que es puerta) son las primeras que hay que mirar.
- **El partido se alarga o se embarulla.** Más balones sueltos = más cambios de posesión; RF-050 fija 60-90 s.
- **La altura por raza rompe el calibrado.** La ADR 0092 dejó los sesgos de raza a suma cero con abanico
  6,4. Darle altura a las razas **en el mismo cambio** haría imposible saber qué movió qué. Por eso va
  aparte (§2).

**10. ¿Cómo se demuestra que funciona?** No «se ve a ojo»:
- Tests de motor: que un balón lanzado con altura cae, bota y acaba parándose; que un tiro por encima del
  larguero es saque de puerta; que un portero que no llega arriba no ataja; que dos ejecuciones iguales dan
  el mismo partido (RT-024).
- Lote de `/Balance` con baseline del mismo árbol y **todas** las métricas, no sólo la que motiva el cambio
  (lección de `ChaseBall pen=50`).
- Medición de BB-N: los córners tienen que pasar de ~1/2 000 partidos a una cifra que se parezca al fútbol.
- `visual-review`: capturas del balón en alto, que hoy no puede ocurrir.

---

## 2. Alcance: lo que entra y lo que NO

**Entra**: la altura del balón, la gravedad, el bote, el rechace del portero y del defensa, la altura de
portería, y el render de la altura.

**No entra, y cada exclusión tiene motivo:**

- **Los pases siguen siendo rasos** (`z = 0` durante todo el vuelo). Lo pidió el revisor y además acota el
  cambio: la ADR 0091 calibró la física del pase y su intercepción, y moverla en el mismo paquete mezclaría
  dos calibrados. Un pase raso no es una excepción al modelo, es el caso `z = 0`.
- **La altura por raza no entra en la primera tanda.** Todas las criaturas tienen el mismo alcance vertical
  al principio. Motivo: la ADR 0092 dejó el abanico de razas en 6,4 después de recalibrarlo entero; si la
  altura llega a la vez que la altura por raza, cualquier desviación del lote es inatribuible. Primero se
  mide qué hace la altura con las razas iguales; **después** se le da altura al enano, y eso reabre la ADR
  0092 a conciencia y con su propia medición.
- **La IA no cambia en esta tanda.** Nadie decide «tiro alto para que no lo bloqueen» todavía: la altura de
  salida sale de la situación y de los atributos, no de una decisión de utilidad nueva. Añadir eso a la vez
  sería cambiar el motor de decisión y el de física en el mismo lote.

---

## 3. Los tres riesgos técnicos, dichos antes de empezar

1. **Determinismo entre plataformas (RT-024, CI en Windows y Linux).** Hoy ya hay `float` en posiciones y el
   test pasa, así que la naturaleza del riesgo no cambia — pero la cantidad de aritmética `float` sí crece,
   y con ella la posibilidad de divergencia. La gravedad y el bote son deterministas si el orden de
   operaciones es fijo; hay que escribirlos sin reordenar y sin acumular error entre ticks más de lo
   imprescindible. **Es el primer riesgo y se comprueba antes que el balance.**
2. **Todas las semillas cambian.** Cualquier tirada nueva, o cualquier duelo que deje de ocurrir, desplaza
   el consumo de RNG y con él todos los partidos. No es un fallo: es lo esperado y ya pasó con AW-A. Implica
   que **la referencia de balance hay que rehacerla entera**, y que no se puede comparar byte a byte con
   nada anterior.
3. **El alcance deja de ser un círculo.** `save.reachCells` (0,9) y `pass.interceptRadiusCells` (0,9) son
   radios en el plano. Con altura pasan a ser un volumen, y hay que decidir si es un cilindro (llego igual
   de lejos a cualquier altura por debajo de mi alcance) o una esfera (cuanto más alto, menos lejos llego).
   **El cilindro es más simple y más legible; la esfera es más realista.** Es una decisión de diseño con
   consecuencias medibles, no un detalle.

---

## 3.bis Cuatro hechos del motor que cambian el plan

Levantados leyendo el código, no supuestos:

1. **`Ball.SetLoose` no toca la posición** (`Ball.cs:90-101`): fija velocidad y limpia el vuelo. El rechace
   sale del punto exacto del toque sin ningún cambio extra. Los tres puntos de intervención son de una
   línea: `TryBlockShot` (`MatchEngine.cs:1277`, hoy `SetLoose(Vec2(0,0))`), `ResolveSaveDuel` (`:2055`,
   hoy `SetOwner(goalkeeper)` incondicional — **el único sitio donde se decide el destino de una parada**,
   porque la estirada reentra por ahí) y la rama de tiro fuera de `ResolveShotArrival` (`:1970-1980`).

2. **RIESGO SERIO, y no lo tenía: un balón rápido se salta a quien deba recogerlo.** `UpdateLooseBall`
   avanza la posición de golpe y luego busca a alguien a menos de `PickupRadius = 0.5`. Un rechace a 0,7
   casillas/tick **pasa de largo entre dos ticks** sin que nadie pueda cogerlo, y con
   `looseBallFrictionPercent = 92` recorrería 12,5 veces su velocidad inicial. O la recogida se hace por
   **barrido** (comprobar el segmento recorrido, no el punto final), o el rechace rápido produce balones
   incogibles — que es el síntoma opuesto al que queremos y emparentado con [BB-G](./pendientes/BB-G.md).
   Va en el paso 1, con test propio.

3. **Hoy todo tiro a puerta apunta al centro exacto de la portería.** No hay dispersión ni horizontal ni
   vertical: `docs/plan-intercepcion-disparo.md` §8 lo dejó como **decisión abierta 1**, por ser regla de
   juego. Eso importa aquí: **sin dispersión, la altura no significa nada** — todos los tiros irían a la
   misma altura del centro. La altura de portería y la dispersión del punto de mira son la misma decisión,
   y hay que tomarlas juntas.

4. **El rechace puede no necesitar ninguna tirada nueva.** La dirección se puede derivar geométricamente
   (reflejo del vuelo sobre el punto de contacto, o `(balón − tocador).Normalized`) y la magnitud salir de
   `tuning`: **cero tiradas**, que es lo que presume la ADR 0091 de sus pasos 2-5. Si en cambio *atrapar o
   despejar* se decide con una tirada, hay que decidir conscientemente si pertenece al mundo de las cuatro
   resoluciones decisivas (`ChanceAveraged`, promedio de dos, ADR 0050 P2) o al de las tiradas simples. La
   opción sin tirada es además la más legible: **si el portero llega con margen atrapa, y si llega justo
   despeja** — eso es `comportamiento observable > modificadores invisibles` y no desplaza el RNG.

## 3.ter Lo que este cambio reabre

No son efectos colaterales: son cosas que hoy están dormidas porque el balón no se mueve, y que despertarán.

- **ADR 0117** subió `chaseBallLooseBonus` a 410 para que perseguir el balón suelto ganara la comparación
  de utilidad, calibrado con balones sueltos que apenas recorren 1,25 casillas. **Hay que remedirlo.**
- **[BC-G](./pendientes/BC-G.md)** — el balón que se queda muerto en el córner, con tres mecanismos ya
  CONFIRMED y sin arreglar — pasa de rareza a caso frecuente en cuanto existan los córners.
- **`Sim.Tests/Engine/ShotInterceptionTests.cs:234`** (`ABlockedShotLeavesTheBallLooseAndIsNeverTheGoalkeeper`)
  **asume velocidad cero** y habrá que revisarlo.
- **`LooseBallSpeed` es una constante privada de código** (`MatchEngine.cs:33`), no una clave de tuning. Un
  rechace con velocidad propia pide moverla a `data/sim/tuning.json`, con su esquema y su `_doc`.
- **La métrica que más va a empujar es `possessionChanges`** (banda 12-28, ADR 0081): todo rechace es una
  posesión que cambia. Es el criterio de parada más probable del lote, por delante de los goles.

## 3.quater Veredicto de `architecture-review` (23 sep 2026)

**Aprobado, con dos precisiones que cambian la implementación.**

**1. La altura es del balón, no del espacio. Nada de `Vec3` global.** El campo sigue siendo 2D y los
jugadores siguen corriendo por el suelo: sólo `Ball` gana altura, y los jugadores ganan un **escalar** de
alcance vertical. Migrar `Vec2` a `Vec3` tocaría las posiciones de los catorce jugadores, `Zone`,
`BodySeparation`, `Marking`, `Utility` entero, `ClampToPitch`, la traza y `/Game` — cientos de sitios, de
los cuales **el 99 % no necesita altura para nada**. Y encaja con el patrón que el repositorio ya usa:
cuando un actor necesita una magnitud nueva se le añade un escalar (`BodyRadius`, `SaveBonusClose`), no se
le añade una dimensión al mundo. El alcance esférico sale entonces de una línea:
`sqrt(distancia2D² + altura²) < alcance`.

**2. Las magnitudes verticales siguen el patrón del repo: milésimas enteras en `tuning`, posición en
`float`.** No hace falta inventar nada: `shotSpeedCellsPerTickMilli` (700), `baseCellsPerTickMilli` (131) y
`maxPushPerTickMilli` (60) ya son enteros en milésimas que se convierten a float al aplicarlos, y
`docs/determinismo.md` acepta `float` para posiciones con reglas de orden. La gravedad y la velocidad
vertical inicial entran como `…Milli` enteros; la altura acumulada es `float`, con **exactamente el mismo
estatus de determinismo que la X y la Y de hoy**. Esto rebaja el riesgo 3.1 del plan: no se añade una clase
nueva de aritmética, se añade más de la que ya hay.

**Lo que esta revisión NO puede decir a favor del cambio**, y conviene no fingir lo contrario: **añade
complejidad, no la elimina ni la mueve**. La pregunta 2 del protocolo existe para rechazar abstracciones
que sólo cambian de sitio el problema; aquí la justificación no es la simplicidad sino el producto —una
decisión del revisor con las alternativas delante—. Lo honesto es declararlo, no construirle un argumento
de elegancia que no tiene.

**Efecto de segundo orden que hay que enumerar antes, no arreglar de uno en uno.** Igual que
`LeavePitch → (-1,-1)` afectó a *toda* lectura de posición en el tick de salida, una altura mayor que cero
afecta a **toda pregunta del tipo «¿quién está cerca del balón?»**: `UpdateLooseBall` (¿se recoge un balón
por el aire?), `TryIntercept`, `PickupRadius`, `CheckOutOfBounds` (¿sale por arriba?) y el duelo de regate.
Hay que listarlos y decidir cada uno **a la vez**, no según vayan apareciendo síntomas.

## 4. Troceado propuesto

Cada paso termina con build, tests, lote y commit propio, como manda `CLAUDE.md`. El orden está elegido para
que el riesgo de determinismo se descubra **antes** de haber invertido en el resto.

| paso | qué | por qué en este orden |
|---|---|---|
| **0** | Instrumentación: medir la referencia actual entera (tiros, paradas, bloqueos, córners, duración, posesiones) | Sin baseline no hay nada que comparar, y las semillas van a cambiar todas |
| **1** ✅ | `Ball` gana `Z`/`VelocityZ`, gravedad y bote, y la recogida pasa a medirse por barrido; **ninguna regla del partido los usa todavía** | Aísla el riesgo de determinismo. Si RT-024 diverge, se descubre aquí y no mezclado con reglas nuevas |
| **2** ✅ | El tiro sale con altura y con puntería dispersa; la portería gana alto y ancho; un tiro puede irse por encima | Primera regla nueva visible. Medido: el agregado no se mueve, porque el portero todavía alcanza en el plano |
| **2c** ✅ | **«Centrar»**: pase alto al área, remate sin controlar apoyado en fuerza, e intercepción en esfera. Medido: tiros desde la línea de fondo **a la mitad** (23,3/26,5 → 13,0/13,4 %), a costa de −0,27 goles | [ADR 0136](./decisiones/0136-centrar-el-pase-alto-que-se-remata.md). Va **antes** del 3 y no es un capricho de orden: el ángulo en la utilidad sin el centro es la vía (B) de BA-E, que ya se midió sola y pone seis puertas en rojo |
| **3** | **La portería disponible**: ángulo del tirador, rivales que tapan y colocación del portero deciden la puntería **y si merece la pena tirar**; el alcance de portero y defensas pasa a ser esférico | Ángulo y portero son la misma pregunta vista desde los dos lados. Decisión del revisor, ampliada (§6) |
| **4** | **El rechace**: el toque defensivo atrapa o desvía, y lo desviado sale con velocidad y altura | Lo que cierra [BB-N](./pendientes/BB-N.md). Va al final porque necesita los tres pasos anteriores |
| **5** | `/Game` dibuja la altura | Una línea, pero pide `visual-review` |
| **6** | *(aparte, otra tanda)* altura por raza | Reabre la ADR 0092 y pide su propio calibrado |

---

## 4.bis Paso 1, hecho y medido (23 sep 2026)

`Ball` tiene `Z` y `VelocityZ`; `UpdateBallHeight` aplica gravedad y bote con
`gravityCellsPerTickSqMilli` (20) y `bounceRestitutionPercent` (45), y corta el bote cuando ya no
levantaría el balón más de lo que la gravedad se come en un tick —sin ese corte un balón botaría infinitas
veces cada vez más bajo y nunca quedaría quieto—. La recogida pasa a medirse **contra el segmento
recorrido**, no contra el punto final.

**Lo que se predijo mal, y es la lección del paso:** se dijo que este paso sería **inerte byte a byte**
porque ninguna regla usa todavía la altura. **No lo es**, y no por la altura sino por el barrido: medir
contra el segmento da una distancia **siempre menor o igual** que medir contra el punto final, así que a
veces el balón se recoge un tick antes o lo recoge otro. Eso **es un cambio de regla**, pequeño y
deliberado —«se coge el balón si pasó cerca, no sólo si acabó cerca»—, y presentarlo como parte de «la
física aislada» habría sido colar una regla dentro de un cambio que se anunciaba neutro. Es el mismo error
que ya se pagó hoy en [BB-O](./pendientes/BB-O.md) con una guardia más ancha que el bug.

**Medido**, lote de 10 000 partidos contra el baseline del mismo árbol:

| | baseline | paso 1 |
|---|---|---|
| partidos distintos | — | **7 153 de 10 000** |
| `possessionChanges` (12-28) | 21,75 | **21,81** |
| `scorelineShare_1-0_to_3-2` (50-100) | 86,58 | **86,35** |
| `blockRate` | 1,34 | 1,30 |
| `injuriesPerMatch` (0,30-0,90) | 0,71 | 0,72 |
| métricas fuera de banda | ninguna | **ninguna** |

Los 7 153 partidos distintos **no** quieren decir que el juego haya cambiado: el flujo de RNG se desplaza
desde el primer balón suelto de cada partido y a partir de ahí todo diverge. Lo que dice si el juego cambió
es el agregado, y el agregado está quieto. Conviene no confundir las dos cosas, porque de aquí en adelante
**todos** los pasos van a mover todas las semillas (ADR 0135) y la tentación de leer el recuento de
partidos como si fuera un efecto va a estar siempre ahí.

**RT-024 en verde**, que era la razón de ser del paso: la aritmética nueva no rompe el determinismo.

## 4.ter Paso 2, hecho y medido (23 sep 2026)

La portería gana geometría (`goalHalfWidthCellsMilli` 1000, `goalHeightCellsMilli` 700 — proporción ~1:3,
la de una portería real), el tiro apunta a un punto **disperso** dentro de ella en vez de a su centro
exacto, y el vuelo describe una **parábola** (`arcCellsPerCellMilli` 80). La traza graba la altura para que
`/Game` pueda dibujarla sin calcular nada (RT-014). Un tiro desviado puede ahora irse **por encima del
larguero**, vía que antes no existía: todos se iban por un lado.

**La dispersión sale de la calidad del disparo**, que ya integra técnica, distancia y presión, en vez de
inventar una fórmula nueva que pudiera contradecirla: un tiro malo se queda cerca del centro —donde está el
portero— y uno bueno puede buscar un rincón.

**La tirada de dentro/fuera no se toca.** Sigue decidiendo la calibración de la ADR 0050 P2; lo que se
añade es *dónde* dentro o fuera. Se consideró derivar «fuera» de la propia geometría y eliminar esa tirada
—más elegante—, y se descartó: habría rehecho de cero la calibración del 70,5 % de tiros a puerta en el
mismo paso que introduce la altura, y entonces ninguna desviación del lote sería atribuible.

**Medido**, lote de 10 000 partidos contra el paso 1:

| | paso 1 | paso 2 |
|---|---|---|
| partidos distintos | — | **10 000 de 10 000** |
| `goalsPerMatch` | 2,79 | 2,81 |
| `saveRate` | 52,84 | 52,78 |
| `shotsPerMatch` (7-15) | 8,92 | 8,90 |
| `possessionChanges` (12-28) | 21,75 | 21,66 |
| `scorelineShare_1-0_to_3-2` (50-100) | 86,58 | 86,37 |
| métricas fuera de banda | ninguna | **ninguna** |

Cambian **todos** los partidos —tres tiradas nuevas por disparo desplazan el RNG desde el primer tiro— y el
agregado no se mueve. Era lo previsto: con el portero midiendo todavía su alcance en el plano, la altura no
decide nada.

**Lo que NO se sabe, y conviene que conste.** La explicación natural de por qué los goles no se mueven sería
que la dispersión no saca al balón del alcance del portero (`save.reachCells` 0,9 contra un semiancho de
1,0). **No está verificada**: la sonda que se escribió para comprobarlo medía el último fotograma del
vuelo, que para un tiro parado o bloqueado no es la línea de gol, así que respondía a otra pregunta. Queda
como hipótesis **sin aislar**, y es justo la que el paso 3 tiene que resolver.

**Un hallazgo que sí es firme y que cambia el paso 3.** La altura de llegada de los tiros a puerta tiene
mediana **0,250** y máximo **0,589**, contra una portería de **0,70** de alto: los disparos llegan en la
mitad baja y **casi ninguno se acerca al larguero**. Es consecuencia directa de atar la altura a la calidad
—con calidad media 46, el techo alcanzable es 0,32—. Importa porque el paso 3 da al portero un alcance
vertical: **si casi ningún tiro va alto, ese alcance apenas se ejercitará** y el paso 3 mediría poco. Antes
de darlo hay que decidir si la altura debe depender tanto de la calidad, o si un tiro puede ser alto y malo
a la vez — que es lo que pasa en el fútbol de verdad.

## 4.quater El paso 2b: el marco es físico (23 sep 2026)

A la vista del paso 2, el revisor añade dos cosas:

> «El alto puede ser malo a su vez porque puede irse por encima del larguero o pegar en él (creo que los
> postes actualmente no son físicos pero deberían serlo)».

Las dos van juntas y se hicieron a la vez, porque **sin marco, desatar la altura la haría gratis**: un tiro
alto sería siempre mejor que uno raso. El palo es lo que le pone precio a buscar la escuadra.

**Comprobado antes de tocar nada**: «poste» sólo aparecía en comentarios, nunca como colisión. Y la
medición dio la razón al revisor por partida doble — con la altura atada a la calidad, los disparos
llegaban con **máximo 0,453** contra un larguero a 0,70, así que **el 0,00 % se le acercaba**: el larguero
habría sido decorativo.

**Qué cambió.** La puntería pasa de «alcance proporcional a la calidad» a **intención + error**: el tirador
apunta a un punto de la portería —arriba o abajo, a un lado o a otro, sin que su habilidad decida la
intención— y la calidad gobierna **cuánto falla**. Un delantero malo también *quiere* meterla por la
escuadra; lo que le distingue es si lo consigue. El marco tiene grosor (`postThicknessCellsMilli` 60) y un
disparo que cruza pegado al hierro sale rechazado con velocidad y altura, con evento propio
(`SHOT_POST`, detalle `post` o `crossbar`).

**El primer intento estuvo mal y conviene que conste por qué.** Acotaba el punto de mira al marco y luego
preguntaba si estaba en la banda del borde: eso concentra en el borde exacto **toda** la masa de los tiros
que se habrían ido fuera, así que el palo quedaba garantizado. Resultado medido: **18,5 % de los disparos
al marco** y los goles cayendo de 2,79 a **1,27** por partido. Se arregló guardando el punto **crudo**, sin
acotar, y midiendo una banda **centrada** en el borde: da en la madera lo que pasaba por el filo, no lo que
hubo que recortar.

**Medido**, 10 000 partidos contra el baseline previo a toda la ADR:

| | baseline | paso 2b |
|---|---|---|
| tiros al marco | — | **1,44 %** de los disparos · 0,107 por partido |
| `goalsPerMatch` | 2,79 | 2,73 |
| `shotsPerMatch` (7-15) | 8,92 | 9,00 |
| `saveRate` | 52,84 | 52,38 |
| `possessionChanges` (12-28) | 21,75 | 21,82 |
| `scorelineShare_1-0_to_3-2` (50-100) | 86,58 | 86,66 |
| métricas fuera de banda | ninguna | **ninguna** |

Un palo cada nueve partidos: raro y memorable, que es lo que se buscaba, y cuesta 0,06 goles por partido.

**Decisión tomada aquí**: el marco **siempre** rechaza, nunca mete el balón dentro. En el fútbol de verdad
un tiro al palo puede entrar, pero permitirlo añadiría una tirada más a un camino que ya tiene tres, por
uno de los sucesos más raros de un partido. Si alguna vez interesa, es una línea.

## 6. El paso 3, ampliado por decisión del revisor (23 sep 2026)

> «la dispersión atada a la calidad debería considerar trigonométricamente dónde está el que dispara, qué
> ángulo tiene a portería, qué rivales tiene en frente y dónde está posicionado el portero»

Y, preguntado por el alcance, el revisor elige que eso entre **también en la decisión de disparar**, no sólo
en la puntería.

**Qué falta hoy.** `quality` ya integra técnica, distancia y presión de rivales cercanos. No conoce **nada**
de lo demás: ni el ángulo a portería, ni quién tapa la trayectoria, ni dónde está el portero. Tirar desde
el vértice del área cuesta lo mismo que desde el punto de penalti salvo por la distancia.

**Por qué el ángulo es el que más cambia el juego.** Desde la banda la portería se ve como una rendija: el
margen de error útil se estrecha, así que fallar es geométricamente más probable aunque el tirador sea
igual de bueno. Al entrar también en la utilidad, las criaturas dejarán de disparar desde donde no deben y
buscarán el pase — o sea, **colocar a los tuyos en el centro pasa a importar**, que es una decisión de
alineación y ahí es donde vive este juego.

**El riesgo, dicho antes de empezar: el portero introduce un bucle.** El tirador reaccionaría al portero, y
el portero ya reacciona al balón. Si todos los tiros van siempre al lado contrario, `saveRate` se desploma
y hay que recalibrarlo entero. **Por eso el orden importa: ángulo y oclusión primero, portero después.** El
ángulo es geometría pura, sin realimentación, y se mide limpio; el portero va encima de un modelo ya
estable.

**Lo que va a moverse, y hay que vigilarlo desde el primer lote**: `shotsPerMatch`, `passChainAvgLength`,
`possessionChanges` y, sobre todo, **las puertas de build** — la ADR 0110 calibró los pesos de `Shoot` del
defensa y del centrocampista, y esto los toca de lleno. Necesita `game-design-review` propio antes de
implementar, y enmienda a esta ADR.

## 7. «Centrar»: la acción que falta, y por qué desbloquea el paso 3 (decisión del revisor, 23 sep 2026)

> «Eso creo que también arregla el problema de que el delantero se posiciona en la línea de fondo creyendo
> que es el mejor sitio cuando en realidad no lo es. También forzará que centre balones buscando compañeros
> rematadores en mejor posición.»

**El diagnóstico ya estaba fichado y medido**: [BA-E](./pendientes/BA-E.md), «goles sin ángulo». El
**32,4 % de los tiros** sale con apertura < 0,5 y produce el **37,1 % de los goles** —convierten **mejor**
que la media— y el **30,1 %** se tiran a menos de una casilla de la línea de fondo.

Y ahí está el aviso que cambia el plan: **la palanca que el revisor ha elegido para el paso 3 —el ángulo en
la utilidad de `Shoot`— es la vía (B) de BA-E, y ya se midió: pone SEIS puertas en rojo**, acorta la cadena
de pases y sube los tiros a 8,33. La vía (A), corregir `FindSpace`, está **RECHAZADA** por la ADR 0111 con
ocho puertas en rojo, y dejó escrita la lección: *cambiar la regla de desmarque global aplana el juego de
colocación; la corrección buena tendrá que ser **local** al delantero en zona de remate*.

**Por qué el centro lo cambia todo.** La (B) rompía porque le quitaba el tiro al delantero **y no le daba
nada a cambio**: sin ángulo y sin alternativa, la jugada se moría y la cadena se acortaba. Con el centro,
el delantero sin ángulo no pierde la jugada, **la transforma**. Y es local por definición, que es
exactamente lo que la ADR 0111 pedía.

### Qué es «centrar», según el revisor

1. **Un pase alto** a un compañero cerca del área.
2. Ese compañero **remata sin controlar**: no recibe y luego dispara, remata de primeras.
3. Para distinguir el remate del tiro normal, el revisor sugiere apoyarlo en **fuerza** en vez de técnica.

Eso último es una decisión de diseño con buena pinta: el tiro es **técnica** (colocar) y el remate es
**fuerza** (llegar y empujarla). Da identidad distinta a dos acciones que si no serían la misma con otro
nombre, y encaja con `identidad memorable > bonus genéricos`. Hay criaturas que rematan y criaturas que
disparan, y eso es una decisión de alineación.

### Lo que reabre, y hay que hacerlo a conciencia

**Los pases siguen rasos por decisión explícita de esta misma ADR** (§2), para no mezclar con la
calibración de la ADR 0091. **Un centro es un pase alto, así que esa exclusión se reabre.** No se cuela de
pasada: es el primer pase con altura del motor y necesita su propia medición.

### Autorización del revisor sobre las puertas

> «las puertas están rojas pero hay que ignorarlas hasta implementar acción "centrar"»

**Queda autorizado avanzar con puertas en rojo** mientras el sistema esté a medias. El motivo es sólido: el
ángulo en la utilidad **sin** el centro es un sistema incompleto, y medirlo contra las puertas sería medir
un estado que nadie quiere enviar. Las puertas vuelven a ser criterio de parada **cuando el centro esté
dentro**, no antes.

Dos cosas que conviene no perder de vista mientras tanto:

- **Cuatro puertas ya estaban rojas antes de todo esto** y son de [BF-B](./pendientes/BF-B.md)
  (`orc_misplaced` 45,18, rareza 43,75, doctrinas), no de esta familia. Comprobado contra árbol limpio.
  No confundir unas con otras al leer el rojo.
- **Hay que medir la (B) otra vez, con el centro dentro.** Las seis puertas rojas son de la (B) *sola*.
  Volver a intentarla sin la alternativa sería repetir un experimento cuyo resultado ya conocemos.

## 5. Decidido (23 sep 2026) — ADR 0135

Las cuatro preguntas de abajo ya tienen respuesta y están registradas en
[ADR 0135](./decisiones/0135-el-balon-tiene-altura.md):

1. **La portería gana alto y ancho, y la puntería se dispersa** según técnica y presión. Es lo que hace que
   la altura signifique algo, y de paso cierra la decisión abierta 1 de `plan-intercepcion-disparo.md` §8,
   que llevaba esperando desde AW-A. Aparecen el tiro por la escuadra y el que se va por encima.
2. **El alcance es una esfera**: cuanto más alto va el balón, menos lejos se llega en el plano. Menos
   legible que un cilindro, más fiel — y la fidelidad es el criterio que fijó el revisor.
3. **Atrapar o despejar se deduce del margen**, sin tirada. Cero consumo de RNG añadido por esta rama.
4. **La dirección del rechace se deriva geométricamente**, con un sesgo hacia fuera en el caso del portero
   (despejar a córner es lo que hace un portero de verdad), y ese sesgo es dato de `tuning`.

### Lo que queda por decidir, ahora sí, y no bloquea el paso 1

- **Cuánto**: alto y ancho concretos de la portería, gravedad, restitución del bote, magnitud del rechace y
  radio de la esfera. Son números, y salen del lote, no de una conversación. El plan los fija con valores
  de partida y los mueve **uno por vez** (`balance-measure`).
- **Qué pasa con un tiro que el portero despeja hacia su propia portería**: ¿puede ser gol en propia? Es una
  regla nueva y no hace falta resolverla hasta el paso 4.

## 6. Lo que se decidió antes (histórico)

Preguntas de diseño que no tienen respuesta evidente y que conviene cerrar con el revisor, porque cambian
lo que se implementa:

1. **¿Cuánto mide de alto la portería, y apunta el tiro siempre al centro?** Hoy no hay alto, no hay ancho
   y **todo tiro a puerta apunta al centro exacto**. Son la misma decisión: sin dispersión del punto de
   mira, la altura no cambia nada. De aquí salen «se fue por encima» y «por la escuadra», y también la
   decisión abierta 1 de `plan-intercepcion-disparo.md`, que lleva esperando desde AW-A.
2. **¿Cilindro o esfera para el alcance?** (§3.3)
3. **¿Atrapar o despejar es una tirada, o se deduce?** Lo natural es que **se deduzca**: si el portero llega
   con margen, atrapa; si llega justo, despeja. Es legible, no añade aleatoriedad y —dato que lo refuerza—
   **no desplaza el flujo de RNG**, así que el lote mide el efecto de la regla y no el de haber corrido las
   semillas. La alternativa (una probabilidad de atrapar) es más fácil de ajustar pero menos legible y
   obligaría a decidir si entra en el mundo de `ChanceAveraged` de la ADR 0050 P2.
4. **¿El rechace sale hacia donde iba el balón, o hacia donde mira el que lo toca?** Lo primero es físico;
   lo segundo permite que un portero despeje «a córner» a propósito, que es lo que hace un portero de verdad.

## Hermanos

- [BB-N](./pendientes/BB-N.md) — el córner que no ocurre nunca: la causa medida y lo que este plan cierra.
- [BB-G](./pendientes/BB-G.md) y [BC-G](./pendientes/BC-G.md) — «el balón se queda parado / suelto y nadie
  lo coge»: misma familia, y el balón suelto con velocidad les cambia el terreno.
- `docs/plan-intercepcion-disparo.md` — AW-A, la intercepción tick a tick que este plan extiende a tres
  dimensiones. Su estructura por pasos es el modelo de éste.
- `docs/referencia-motores-futbol.md` §1 — por qué la intercepción no es una técnica aparte sino no saltarse
  el bucle normal; el mismo argumento vale para la altura.

---

## 8. `game-design-review` de «centrar» — las diez preguntas (23 sep 2026)

Regla B. Se responde **antes** de tocar código. El §7 recoge la decisión del revisor y el porqué; esto es
el diseño concreto que se va a implementar, con los hechos del motor levantados leyendo el código, no
supuestos.

### Los hechos de partida, comprobados en el código

1. **No existe ninguna acción de pase con altura.** Las tres que hay (`ShortPass`, `LongPass`,
   `ThroughPass`) pasan por `LaunchPass`, que no toca `FlightTargetZ` ni `FlightArc`: el pase es el caso
   `z = 0` del modelo del paso 1.
2. **La intercepción del pase es un círculo en el plano**: `TryIntercept` compara
   `Vec2.Distance(jugador, balón) < pass.interceptRadiusCells` (0,9). **No mira `_ball.Z`.** Si un centro
   se lanzara hoy, volaría alto y lo interceptarían igual: la altura no valdría para nada.
3. **El vuelo ya sabe describir una parábola.** `UpdateFlight` calcula
   `Z = FlightTargetZ·t + FlightArc·4·t·(1−t)`. Con `FlightTargetZ = 0` y `FlightArc > 0` sale
   exactamente un centro: sube, y vuelve al suelo al llegar. **Cero física nueva.**
4. **El receptor de un pase en vuelo ya corre a buscarlo**: `chaseBallIncomingPassBonus` (700) se lo da
   `EvaluateChaseBall` a quien sea `ball.PassReceiver`. Un centro lo hereda gratis: el rematador va a la
   cita sin código nuevo.
5. **La calidad del tiro ya reparte entre técnica y fuerza** (`techniqueFactor` 14, `strengthFactor` 4).
   El remate no necesita una fórmula nueva: necesita **los mismos factores con los pesos al revés**.
6. **`EventType.AerialDuel` existe en el enum y no se emite nunca.** Hueco reservado, no se usa aquí.

### 1. ¿Qué experimenta el jugador?

Hoy ve a su delantero correr hasta el cordel y fusilar desde donde no hay portería. Lo ha dicho él mismo y
está medido: **32,4 % de los tiros con apertura < 0,5**, **30,1 % a menos de una casilla de la línea de
fondo** (BA-E §1). Con el centro ve otra cosa: el delantero llega al fondo, **levanta la cabeza** y pone el
balón al área; el balón **vuela por encima** del defensa que hoy lo interceptaría; y un compañero llega de
frente y **la empuja de primeras**. El gol deja de venir de un ángulo imposible y viene de un remate.

### 2. ¿Qué decisión toma el jugador con esto?

Una de **alineación**, que es donde vive este juego: *a quién pongo para rematar*. Hasta hoy la fuerza
servía para entrar, para aguantar y un poco para tirar (`strengthFactor` 4 contra `techniqueFactor` 14). Con
el remate apoyado en fuerza, un bruto lento pasa a tener un sitio en ataque que antes no tenía, y la
pregunta «¿pongo al técnico o al bestia arriba?» deja de tener una respuesta única.

### 3. ¿Qué decisión DEBERÍA tomar? ¿Coincide?

Sí, y además es la que el proyecto lleva pidiendo desde la ADR 0111: *la corrección buena tendrá que ser
**local** al delantero en zona de remate*. El centro es local por construcción —sólo existe cerca del área—
y no toca la regla global de desmarque que ya se rechazó por aplanar el juego de colocación.

### 4. ¿Qué regla del juego representa?

**Ninguna escrita: es regla nueva, y se dice claro.** RF-057 y siguientes describen tiro y parada, no el
centro. Extiende la ADR 0135 (altura) y **reabre a conciencia su propia exclusión** de §2 —«los pases
siguen rasos»— para un único caso, con su medición propia: es el primer pase con altura del motor. No toca
la calibración del pase de la ADR 0091, que sigue rigiendo los tres pases rasos.

### 5. ¿Qué sistemas intervienen?

- `/Sim`: `PlayerAction.Cross` **al final del enum** (RT-097: añadir al final no altera el desempate de las
  anteriores), `StateMachine.WithBallActions`, `Utility.EvaluateCross`, `MatchEngine.LaunchCross`,
  `ResolveCrossArrival`, `LaunchVolley`, y `TryIntercept` pasando de círculo a **esfera**.
- `/data`: `ai/weights.json` (peso base por puesto, táctico, y los términos de contexto), `sim/tuning.json`
  (comba del centro, alcance, calidad del remate) y sus dos esquemas.
- `/Game`: **nada en este paquete.** El balón alto ya lo dibuja el paso 5 de la ADR 0135, que sigue
  pendiente. La frontera se respeta: `/Sim` decide, `/Game` dibujará.
- `MatchTrace` ya graba la altura desde el paso 2.

### 6. ¿Hay alternativas? Tres, consideradas

- **Un centro que es un pase raso más, sin altura.** Más barato y no reabre nada. **Rechazada**: sin altura
  el centro lo intercepta el mismo defensa que intercepta todo lo demás, y entonces no es un centro, es un
  `LongPass` con otro nombre. Lo que hace que la jugada exista es volar por encima.
- **Que el rematador controle y luego dispare** (un pase normal al área). **Rechazada por el revisor**, y
  con razón mecánica: si controla, la acción no se distingue de un pase y el ángulo lo vuelve a decidir el
  tirador. El remate de primeras es lo que convierte la posición del rematador en la decisión.
- **Que el remate sea una acción sin balón del rematador** (él decide rematar) en vez de una consecuencia
  de la llegada del centro. **Rechazada**: obligaría a que dos jugadores coordinaran decisiones en ticks
  distintos, que es justo la clase de estado compartido que el repositorio evita. El centro es una decisión
  de **uno**; el remate es la resolución de esa decisión.

### 7. ¿Qué trade-off introduce? ¿Hay coste de oportunidad legible?

Sí, y es doble.

- **Para el que centra**: renuncia a tirar. Su tiro sin ángulo convertía al 37,1 % de los goles con el
  32,4 % de los tiros —*mejor* que la media (BA-E)—, así que el centro **tiene que competir contra algo que
  hoy funciona demasiado bien**. Se paga con una tirada de pase más y con que el remate sea de peor calidad
  media que un tiro.
- **Para el que alinea**: un rematador fuerte arriba es un jugador que no está defendiendo ni pasando. La
  fuerza deja de ser sólo el atributo de la violencia.

`identidad memorable > bonus genéricos`: el tiro es **colocar** (técnica) y el remate es **llegar y
empujarla** (fuerza). Dos acciones que si compartieran fórmula serían la misma con otro nombre.

### 8. ¿Cómo cambia las estrategias posibles?

Abre una combinación que hoy no existe: **banda + área**. Un extremo que llega al fondo deja de ser un
error de colocación y pasa a ser la mitad de una jugada; la otra mitad es alguien fuerte en el centro. Y le
da sentido de ataque a rasgos y razas que hoy sólo valen para pegar (`Brute`, orco). No se añade contenido:
se le da uso al que ya hay.

### 9. ¿Puede degenerar? Cuatro sitios, con su guarda

1. **El centro sustituye al pase normal en todo el campo.** Guarda: sólo hay candidato si el compañero está
   **cerca del área rival** y **con mejor apertura que el centrador**, y el centro tiene su propio alcance.
   Fuera de la zona de remate la acción no tiene receptor y se descarta.
2. **El remate se vuelve la máquina de goles y `goalsPerMatch` se dispara.** Es el riesgo real, porque el
   remate ocurre de frente a portería, que es donde mejor se convierte. Guardas: tirada de pase del centro,
   calidad de remate por debajo de la del tiro salvo para los fuertes, penalización propia de puntería
   (rematar sin controlar es más difícil que tirar), y el rematador tiene que estar **libre de presión**,
   como cualquier receptor de pase hoy. Criterio de parada: `goalsPerMatch` y
   `scorelineShare_1-0_to_3-2`, que es puerta.
3. **El centro alarga la cadena artificialmente.** `passChainAvgLength` (banda 2-4) es una de las que la
   vía (B) rompía. El centro **cuenta como pase de la cadena**, porque lo es; lo que hay que vigilar es que
   no la infle. Se mide.
4. **Un centro que nadie remata deja el balón muerto en el área**, que es [BC-G](./pendientes/BC-G.md) sin
   arreglar. Guarda de alcance: un centro fallado queda suelto **igual que cualquier pase fallado**, con la
   misma velocidad y la misma fricción. **No se inventa aquí el despeje**: eso es el paso 4 de la ADR 0135.

**Regla 11 (nada malo sin ser previsible):** el centro no añade ninguna vía de daño. No hay lesión, ni
muerte, ni falta nueva. Un remate es un tiro, y un tiro no hiere a nadie.

### 10. ¿Cómo se demuestra que funciona?

- **Tests de motor**: que un centro vuela por encima del radio de intercepción a mitad de vuelo y **no se
  puede interceptar** ahí (y sí al salir y al llegar, donde va bajo); que el receptor remata **sin llegar a
  ser dueño del balón**; que un remate de un jugador fuerte tiene mejor calidad que el del mismo jugador
  con la fuerza baja, y **al revés que el tiro** con la técnica; que un centro sin rematador no se elige;
  que RT-024 sigue en verde.
- **Lote de `/Balance`**, 10.000 partidos, contra el baseline del mismo árbol (HEAD tras el paso 2b), con
  **todas** las métricas. Nuevas: `crossesPerMatch`, `crossCompletionRate`, `volleyShotShare`,
  `volleyGoalShare`.
- **BA-E medida otra vez**: el censo de apertura de `docs/ba-e-goles-sin-angulo.md` §1 —% de tiros con
  apertura < 0,5, % desde la línea de fondo, apertura media— **antes y después**. Es el motivo del cambio y
  es la cifra que decide si funcionó.
- **Las 43 puertas** vuelven a ser criterio de parada en cuanto el centro esté dentro (§7), contando que
  **cuatro ya estaban rojas y son de [BF-B](./pendientes/BF-B.md)**, no de esta familia.
- **La vía (B) se REMIDE con el centro dentro.** Las seis rojas conocidas son de la (B) *sola*; darlas por
  buenas sería repetir un experimento cuyo resultado ya se conoce sin la pieza que lo cambia.

### Veredicto

**Aprobado para implementar**, en dos paquetes y en este orden, que es el que impuso el revisor y tiene
motivo medido:

1. **«Centrar»** (este diseño). Con su lote y su medición de BA-E.
2. **El paso 3** de la ADR 0135 —ángulo y oclusión en la puntería *y* en la utilidad de `Shoot`, portero
   después— sobre un motor que ya tiene la alternativa. Antes del centro, quitarle el tiro al delantero es
   quitarle la jugada; después, es transformarla.

## 9. Veredicto de `architecture-review` de «centrar» (23 sep 2026)

**Aprobado, con una tabla que es el entregable de la revisión.**

**1. Fronteras.** No se toca ninguna: `/Game` no cambia en este paquete (el dibujo del balón alto sigue
siendo el paso 5), `/Sim` sigue sin E/S y el render seguirá consumiendo eventos. `MatchTrace` ya graba la
altura desde el paso 2.

**2. ¿Elimina complejidad o la mueve?** Hay que decirlo como en la revisión de la propia ADR 0135: el
centro **añade** complejidad —una acción más, un camino de llegada más—, y la justificación es el producto,
no la elegancia. Lo que sí **quita** una incoherencia es el punto 5: hoy el balón tiene altura y una de las
preguntas de proximidad finge que no.

**3. El patrón ya existe en el repositorio, y se usa tal cual.** No se inventa nada:
- una acción nueva va **al final del enum** `PlayerAction`, que es lo que su propio comentario manda para
  no alterar el desempate de las anteriores (RT-097);
- el alcance esférico es la línea que ya dejó escrita la revisión de la ADR 0135:
  `sqrt(distancia2D² + altura²) < alcance`, sin `Vec3` y sin tocar las posiciones de los catorce jugadores;
- las magnitudes verticales van como **enteros en milésimas** en `tuning` (`…Milli`), como
  `arcCellsPerCellMilli` y `gravityCellsPerTickSqMilli`;
- la apertura entra en la utilidad como **entero en centésimas**, como todo lo demás (RT-023). El `float`
  se queda en la geometría, igual que hoy.

**4. Determinismo.** Aritmética entera en la utilidad y en la calidad; `float` sólo en posiciones y en la
raíz del alcance, que es la misma clase que `Vec2.Distance` usa en cada tick desde el primer commit. Sin
`Dictionary` iterado, sin RNG nuevo salvo la tirada de pase del centro —que es la que ya hace todo pase— y
las del remate, que son las del tiro. RT-024 se comprueba antes del lote.

**5. Efecto de segundo orden: las cinco preguntas de «¿quién está cerca del balón?», decididas a la vez.**
La ADR 0135 §3.quater exigía enumerarlas y decidirlas juntas, no según fueran apareciendo síntomas. Son
éstas, con el sitio exacto y **cuándo** le toca a cada una:

| sitio | ¿pasa a esfera? | cuándo, y por qué |
|---|---|---|
| `TryIntercept` (pase en vuelo) | **sí, en este paquete** | Es la razón de ser del centro: si el defensa intercepta un balón que le pasa a casi casilla y media por encima, la altura no significa nada y el centro es un `LongPass` con otro nombre. |
| `TryBlockShot` (bloqueo de campo) | sí | **Paso 3** («el alcance de porteros y defensas pasa a ser esférico»). Moverlo aquí mezclaría el efecto del centro con el de un cambio que baja `blockRate`, y ninguna desviación del lote sería atribuible. |
| `TryGoalkeeperReach` y la estirada | sí | **Paso 3**, y además es el que trae el bucle de realimentación que la enmienda 2 de la ADR 0135 manda medir aparte. |
| recogida del balón suelto (`UpdateLooseBall`) | sí | **Paso 4**. Hoy **no hay caso**: todo balón suelto sale con `z = 0` y el único camino que le daría altura es el rechace, que es justamente el paso 4. Cambiarlo ahora sería código sin nada que lo ejercite. |
| llegada del pase y del centro (`PassArrivalRadius`) | **no, nunca** | Un centro llega con `FlightTargetZ = 0`: la parábola vuelve al suelo en el destino. Comparar en el plano es correcto, no una omisión. |

**6. Paralelismo.** Nada de estado compartido nuevo: la decisión es de un jugador y la resolución vive en
el tick. `/Balance` y las puertas siguen como están, cada hilo con su `Catalog`.

**7. Una consecuencia del cargador, a favor.** `EnsureComplete` exige que **toda** pareja (puesto, acción) y
(estado táctico, acción) esté en `data/ai/weights.json`. Añadir `Cross` al enum hace que el juego **no
arranque** hasta que el dato esté completo, con error explícito y ruta (RT-032). No hay forma de colar la
acción a medias.

### 9.bis ¿Y una librería de físicas, en vez de escribirla? (pregunta del revisor, 23 sep 2026)

> «No reinventes la rueda, si hay librerías de físicas opensource que puedas usar hazlo»

**No en `/Sim`, y el motivo no es el gusto por escribirlo todo.**

1. **Ya estaba descartado con motivo registrado.** [ADR 0002](./decisiones/0002-sim-independiente-de-godot.md):
   *«el movimiento se implementa con vectores propios, no con el motor de físicas»*, y `CLAUDE.md` lista
   «motor de físicas de Godot para el partido» entre los descartes. Reabrirlo pide ADR **antes** de
   escribir código.
2. **Aquí no hay física de cuerpos rígidos que resolver.** El balón interpola un vuelo sobre un número
   fijo de ticks, le suma **una línea** de parábola, integra gravedad y bote en ~6 líneas (paso 1) y
   pregunta distancias. No hay contactos, ni masas, ni restricciones, ni *broadphase*. Una librería
   resolvería un problema que este juego no tiene, sobre una cuadrícula de 16×7 a 15 ticks lógicos.
3. **Y el que decide: RT-024 corre en CI en Windows y Linux.** Ninguna de las candidatas
   —BepuPhysics v2, Jitter2, los *ports* de Box2D, la física de Godot— promete resultados bit a bit
   idénticos entre plataformas y compiladores; usan rutas SIMD y órdenes de iteración internos. Cambiar
   una puerta verde por una dependencia que no se puede arreglar cuando falle es exactamente el riesgo
   que el troceado de esta ADR existe para evitar: el paso 1 metió la física **sin que nada la usara**
   para descubrir una divergencia antes de invertir en las reglas.

**Dónde sí cabría algún día**: en `/Game`, para el rebote **visual** del balón, que no decide nada del
partido (RT-014). Anotado, no hecho.

---

## 10. `game-design-review` del paso 3a — «la portería se estrecha» (23 sep 2026)

La ADR 0135 enmienda 2 exige esta revisión antes de implementar. Aquí van las diez preguntas, y la
primera conclusión es de **alcance**: el paso 3 que describe la enmienda son **tres** cambios
(apertura y oclusión en la puntería, apertura en la utilidad, y el portero + alcance esférico), y la
propia enmienda impone el orden. Esta nota cubre **3a**, y dentro de él separa dos lotes.

### El hecho geométrico, comprobado, del que sale todo lo demás

La petición del revisor —*«la dispersión atada a la calidad debería considerar trigonométricamente
dónde está el que dispara, qué ángulo tiene a portería»*— **tiene una respuesta exacta, no una
aproximación de diseño**:

> Un tirador a ángulo θ de la perpendicular falla por un error **angular** en el pie. Un error que le
> desvía el balón una distancia *m* perpendicular a su línea de tiro cruza el plano de la portería
> desplazado **`m / cos θ`** — y `cos θ` es exactamente `ApertureCenti / 100`, la magnitud que ya
> existe en `Utility.ApertureCenti` y con la que se midió BA-E.
>
> *(Demostración: dirección `u = (cos θ, sin θ)`, offset perpendicular `m·(−sin θ, cos θ)`, prolongar
> hasta `x = goal.X` → `Y = m·cos θ + m·tan θ·sin θ = m/cos θ`.)*

O sea que **la trigonometría que pide el revisor es una división por la apertura**. No hay que
inventar una penalización: hay que dejar de suponer que el error de puntería vive en el plano de la
portería cuando en realidad vive en el pie del que dispara. Desde el cordel (apertura 0,2) el mismo
golpeo se va **cinco veces** más lejos del punto buscado. Esa es la razón real de que tirar desde la
línea de fondo sea mala idea, y hasta hoy el motor no la tenía.

Dos consecuencias que hacen el cambio barato y honesto:

- **La intención NO se divide**, sólo el error. El rincón sigue estando donde estaba y el delantero
  malo sigue queriendo meterla por la escuadra — es la misma frase del paso 2b, sin excepción nueva.
- **La vertical se deja en paz**, dicho a propósito: el ángulo que el revisor nombra es el horizontal,
  el vuelo más largo también ensancharía el error vertical, y meter las dos cosas a la vez impide
  atribuir nada. Queda anotado como candidato, no como olvido.

### 1. ¿Qué experimenta el jugador?

Que sus delanteros dejan de marcar desde sitios imposibles, y que cuando tiran desde la esquina **la
tiran fuera o al palo**, que es lo que pasa en el fútbol. Hoy ve lo contrario y lo dijo con estas
palabras: *«El delantero está en la línea de fondo y tira a portería sin ángulo. Es muy irreal.»*

### 2. ¿Qué decisión toma el jugador con esto?

**A quién alinea en el centro.** Si la portería sólo está disponible de frente, un rematador colocado
por dentro vale más que uno que vive en la banda — y eso ya tiene soporte: el centro (ADR 0136) busca
precisamente al compañero con mejor apertura. Es decisión de alineación, no de partido, que es donde
vive este juego.

### 3. ¿Qué decisión DEBERÍA tomar? ¿Coincide?

Sí, y es justo la que hoy **no** puede tomar: hoy da igual dónde coloques al rematador, porque desde
el cordel se marca igual (BA-E: el 32,4 % de los tiros salía con apertura < 0,5 y producía el 37,1 %
de los goles, o sea que **convertían mejor que la media**). Esa inversión es el síntoma.

### 4. ¿Qué regla del juego representa?

**RF-012d** (todo lo malo previsible) leído en positivo: el sitio desde el que disparas es información
que el jugador ve en la pantalla, y hoy no significa nada. No inventa regla nueva: **corrige una que
el motor ya afirmaba mal**. Toca RF-050/RF-057 sin cambiarlos.

### 5. ¿Qué sistemas intervienen?

- `/Sim/Engine/MatchEngine.cs` — `OnTargetAim` (el error se divide por la apertura) y `LaunchShot`
  (la tirada dentro/fuera gana término de apertura). La apertura ya se calcula ahí, para el censo.
- `/Sim/Engine/Utility.cs` — `EvaluateShoot`, lote 2.
- `/data/sim/tuning.json` y `/data/ai/weights.json` — los dos números nuevos.
- `/Game` — **nada**. El render consume eventos (RT-014) y no hay evento nuevo.

### 6. ¿Hay alternativas?

Tres, y las tres se consideraron:

1. **Error angular completo** (convertir toda la puntería a ángulos en el pie, vertical incluida).
   Es lo más fiel, y además metería la distancia en la misma fórmula — pero la distancia ya está
   calibrada por dos sitios (`distancePenaltyPerCell`, `offTargetDistanceFactor`) y rehacerla en el
   mismo paso que la apertura deja el lote sin atribuir. **Aplazada, no descartada.**
2. **Penalización plana por apertura baja** — la vía (C) de BA-E tal y como se midió el 14 sep.
   Rechazada: es un número inventado donde hay una identidad trigonométrica exacta, y no explica por
   qué el castigo crece de golpe cerca del cordel.
3. **Sólo la utilidad, sin tocar la resolución** — la vía (B) sola. Es exactamente el experimento que
   ya se hizo y puso seis puertas en rojo. Se descarta hacerlo **primero**: si la IA aprende a temer
   el mal ángulo antes de que el mal ángulo sea de verdad malo, aprende algo falso.

### 7. ¿Qué trade-off introduce?

**Quita goles.** Cada gol desde el cordel que deja de entrar es un gol menos, y el centro ya se llevó
0,27 (BA-E). El coste es legible y va en la dirección que el revisor pidió, pero `goalsPerMatch` es
la métrica que puede parar el lote — y si la para, **la respuesta correcta no es rebajar la apertura
sino subir `baseQuality`**: la geometría no se negocia, el nivel de gol sí, y eso es una ADR con
datos (RT-057).

A cambio da lo que el §7 de este plan buscaba: **la posición vuelve a significar algo**, y las
criaturas de centro dejan de ser intercambiables con las de banda.

### 8. ¿Cómo cambia las estrategias posibles?

Sube el valor de **centrar** sin tocarlo (el rematador de frente ahora convierte de verdad mejor), y
con él el de la **fuerza** frente a la técnica en el área. Sube el valor de los perks y rasgos de
colocación. **Baja** el del regate hacia la línea de fondo.

### 9. ¿Puede degenerar?

Cuatro sitios mirados:

- **Apertura → 0 cerca de la línea de fondo**: `1/cos θ` es una asíntota. Sin suelo, un tiro desde el
  cordel exacto tendría error infinito. **Guarda**: suelo a la apertura en el divisor
  (`shot.minAimApertureCenti`, valor de partida 20 = error ×5 como máximo).
- **El punto crudo acotado al marco**: hoy un `raw` muy fuera se recorta al poste y sigue contando
  como tiro a puerta. La división lo amplificaría muchísimo — por eso el término de apertura entra
  **también** en `offTargetChance`, que es quien de verdad decide si va dentro. Sin eso, el cambio
  se convertiría en una fábrica de palos.
- **Penalti**: `isPenalty` tiene apertura 100 por construcción (se tira de frente), así que no le
  afecta. Comprobado, no supuesto.
- **Remate de centro** (`volley`): llega de frente por definición de la acción, así que cobra poco.
  Es coherente — es exactamente el comportamiento que queremos premiar.

### 10. ¿Cómo se demuestra que funciona?

Dos lotes separados, **cada uno con su medición**, y el orden no es opcional:

- **Lote 3a-i — la resolución.** La apertura divide el error de puntería y entra en la tirada
  dentro/fuera. La IA **no cambia**, así que `shotsPerMatch` y `passChainAvgLength` no deberían
  moverse y eso es la prueba de que el cambio está donde se dice. Lo que **sí** tiene que moverse es
  la ventaja de conversión de BA-E: `lowApertureGoalShare / lowApertureShotShare` de 1,14 hacia 1,00
  o por debajo. Vigilar `goalsPerMatch`, `saveRate`, `shotsOnTargetPerMatch` y los palos (2b dejó
  1,44 %; esto lo sube y hay que saber cuánto).
- **Lote 3b — la utilidad (la vía (B) remedida).** `shootAnglePenaltyPerRow` (proxy por filas)
  **se sustituye** por un término de apertura; no se suman los dos, porque serían dos caminos que
  pueden contradecirse. Aquí sí se vigilan **las 43 puertas** —la ADR 0110 calibró los pesos de
  `Shoot` del defensa y del centrocampista— y la cadena de pases, que es lo que la (B) rompía.
- **Tests de motor**: un tiro desde el cordel con el mismo golpeo se va más lejos del punto buscado
  que el mismo tiro de frente; el penalti no cambia; el suelo de apertura acota el error.
- RT-024 en verde, y byte a byte **no** habrá: mueve todas las semillas, como todo el paso 3.

### Veredicto

**Adelante con 3a-i, y 3b después y por separado.** Lo que convierte la vía (B) de BA-E en algo
distinto de lo que ya falló no es sólo que ahora exista el centro: es que **primero se hace verdad la
geometría y después se le enseña a la IA**. La (B) sola le pedía al delantero que temiera un peligro
que el motor no aplicaba.

**Una reversión que no puede ser silenciosa**: esto revive la vía **(C)** de BA-E, que el revisor
declinó el 14 sep («no aplicar C»). La enmienda 2 de la ADR 0135 la pide explícitamente y con otra
forma —trigonometría en vez de penalización plana—, así que queda registrada como cambio de decisión
del revisor, no como descuido.

## 11. Paso 3a-i, hecho y medido (23 sep 2026)

**Lo que se implementó**: la división del error horizontal de puntería por la apertura en `OnTargetAim` y
el término de apertura en la tirada de dentro/fuera de `LaunchShot`, gobernados por `minAimApertureCenti` y
`offTargetAperturePenalty` en `tuning.shot`. La IA **no se tocó** — eso es el paso 3b.

> **Lo que se PUBLICA es otra cosa, y conviene no confundirlas al leer las tablas de abajo.** Los dos
> valores van **apagados** en `data/sim/tuning.json` (`100` y `0`, que hacen el divisor exactamente 1 y
> anulan el término). Las columnas «encendido» de aquí en adelante describen el punto de trabajo **medido**
> (20 y 2000), no el build. Motivo en §11.ter.

### La verificación que vale más que los tests

Con los dos valores neutralizados por dato (`minAimApertureCenti: 100`, que hace el divisor exactamente 1,
y `offTargetAperturePenalty: 0`), el motor reproduce HEAD **byte a byte** en los 20 000 partidos de las dos
semillas (`matches.csv` idéntico con `cmp`). Eso demuestra dos cosas que ningún test verde demuestra:
**que no se ha colado nada más** en el paquete, y que el contador de palos añadido al informe es
**inerte**. Todas las comparaciones de abajo usan ese baseline por dato, así que la única diferencia entre
las dos columnas son los dos números.

### Lo que se buscaba, medido en las dos semillas

| | base s1 | **3a-i s1** | base s7 | **3a-i s7** |
|---|---|---|---|---|
| `lowApertureShotShare` | 14,31 | 14,09 | 12,96 | 12,86 |
| `lowApertureGoalShare` | 20,25 | **15,41** | 17,03 | **13,23** |
| **ventaja de conversión** (gol/tiro) | **1,42** | **1,09** | **1,31** | **1,03** |
| `shotsOnTargetShare` | 72,10 | 65,84 | 72,50 | 66,04 |
| `goalsPerMatch` | 2,46 | 2,20 | 1,71 | 1,56 |
| `shotPostShare` | 1,33 | 1,60 | 1,50 | 1,65 |
| `passChainAvgLength` | 2,00 | 2,00 | 2,14 | 2,13 |
| `possessionChanges` | 21,73 | 21,98 | 21,43 | 21,80 |

**La ventaja de conversión de BA-E desaparece**, que era la afirmación de diseño: un tiro sin ángulo pasa
de convertir un 42 % / 31 % **mejor** que la media a convertir como la media (+9 % / +3 %). Y lo hace
**sin tocar dónde se dispara** —la proporción de tiros sin ángulo no se mueve, porque la IA no ha
cambiado—, que es la prueba de que el efecto está donde se dijo y no en otro sitio.

**Ninguna métrica cambia de estado en ninguna de las dos semillas.** La única `OUT` (`shotsPerMatch` 6,69
en la semilla 7) **ya lo estaba en el baseline** y el cambio la acerca a la banda (6,88).

**El riesgo declarado no se materializa.** «Fábrica de palos» era la degeneración a vigilar: 1,33 → 1,60 y
1,50 → 1,65 %, del orden del 1,44 % que publicó el paso 2b. El término de `offTargetAperturePenalty` es
justo lo que lo impide, y ahora es medible de forma permanente: el contador de palos pasa de script a
instrumento del informe (`shotPostShare`), por la misma razón por la que la ADR 0136 promovió el censo de
apertura.

### Behavioral audit

El agregado podría estar en banda escondiendo una distribución rota, así que se comprobó el reparto de
goles **por puesto**: 87,3 → 86,9 % del delantero en la semilla 1 (74,8 → 74,3 en la 7), o sea intacto. La
conversión baja en todos los puestos y **baja más en el delantero** (30,3 → 26,7 %) que en el
centrocampista (20,0 → 17,9 %), que es exactamente el reparto que predice la geometría: el que pisa el
cordel es el delantero.

### Lo que queda dicho y no resuelto

**El coste en goles se acumula.** El paso 2b dejó 2,73; el centro se llevó 0,27 y éste otros 0,26, así que
la semilla 1 va de **2,73 a 2,20** desde que empezó esta familia. `goalsPerMatch` no tiene banda y
`scorelineShare_1-0_to_3-2` sigue dentro (88,12 / 85,15), así que **no es criterio de parada** — pero son
dos recortes seguidos en la misma dirección y el revisor ya dejó aparcado el primero («ya miraremos más
adelante si la precisión debe ponderar más»). La palanca para devolverlo, si se decide, es
`shot.baseQuality`, **no** rebajar la apertura: la geometría no se negocia y el nivel de gol sí (RT-057).
Va al informe como pregunta, no como ajuste silencioso.

**`shotsOnTargetShare` baja 6 puntos** (72 → 66). Es el término de dentro/fuera haciendo su trabajo, pero
mueve el 70,5 % que la ADR 0050 P2 calibró. Queda anotado: si se toca `baseQuality` para recuperar goles,
esta cifra vuelve a subir sola y hay que remirar las dos a la vez.

### 11.bis La medición desmiente al diseño en un punto, y conviene leerlo antes de seguir

La nota §10 afirmaba que la **división del error por la apertura** era el mecanismo del paso y que
`offTargetAperturePenalty` era sólo la guarda contra la fábrica de palos. **El barrido dice lo contrario.**
Con el término de dentro/fuera en 0 y la división trigonométrica actuando sola (semilla 1, 10 000
partidos):

| `offTargetAperturePenalty` | base (todo apagado) | **0** | 700 | 1300 | 2000 |
|---|---|---|---|---|---|
| ventaja de conversión | 1,42 | **1,37** | 1,29 | 1,22 | 1,09 |
| `goalsPerMatch` | 2,46 | 2,42 | 2,36 | 2,29 | 2,20 |
| `shotsOnTargetShare` | 72,10 | 72,25 | 70,37 | 68,37 | 65,84 |
| `shotPostShare` | 1,33 | **1,82** | 1,73 | 1,66 | 1,60 |

**La geometría sola es casi inerte**: mueve la ventaja de conversión de 1,42 a 1,37 y los goles cuatro
centésimas. Lo único que hace de verdad es **convertir goles en palos** (1,33 → 1,82 %).

**La causa es el recorte, y es un artefacto que ya estaba ahí.** La tirada de dentro/fuera decide *antes*
de calcular el punto de mira, y `OnTargetAim` **acota** el punto al marco. Así que un tiro cuya mira cruda
se va tres semianchos fuera se recorta al poste y **sigue contando como tiro a puerta**. Multiplicar el
error por `1/apertura` no puede sacar el balón de la portería: sólo lo empuja contra el borde. La
trigonometría es correcta y el motor no la deja hablar.

**Consecuencia honesta: quien hace el trabajo de este paso es el término de dentro/fuera**, que es un
número elegido, no una identidad. La versión limpia —que la mira cruda decida dentro/fuera y la tirada
desaparezca— es la **alternativa 1** de §10 («error angular completo»), que se aplazó por no rehacer la
calibración del 70,5 % en el mismo paso. Ahora hay un motivo medido para retomarla, y no sólo estético.
Queda anotado como lo que es: **la corrección buena de este paso está aplazada, no hecha.**


## 11.ter Por qué el paso 3a se publica APAGADO, y qué queda decidido y qué no (23 sep 2026)

La revisión independiente (Regla E) tumbó dos afirmaciones de §10 y §11, y las dos correcciones están
arriba en su sitio. Resumen de estado, para que la siguiente sesión no tenga que reconstruirlo:

**Lo que está probado:**

- La identidad `ApertureCenti = cos θ` y el álgebra `m/cos θ`, con test propio que no pasa por el motor.
- El baseline byte a byte: neutralizados los dos valores, `matches.csv` es idéntico a HEAD en 20 000
  partidos. `Math.Max(a, 100) = 100` da factor `1.0f` exacto, así que «apagado» es apagado de verdad.
- Encendido, la ventaja de conversión de BA-E muere (1,42 → 1,09), sin tocar la IA.

**Lo que está refutado, y por una medición que yo no hice:**

- **El efecto NO lo produce la trigonometría.** El barrido de §11.bis movía la penalización dejando el
  suelo siempre activo, así que nunca midió el término solo. Medido: penalización sola → ventaja **0,978**
  (89 % del recorrido); trigonometría sola → 1,235 (19 %). El paquete **es la vía (C)** de BA-E con rampa.
- Por tanto **el `game-design-review` de §10 no cubre la mecánica que actúa**: se hizo sobre la división.
  Encenderla exige uno propio, y además la (C) es una vía que el revisor declinó el 14 sep.

**Lo que decide que no se envíe:** cuatro puertas rojas sobre las dos del baseline. Dos de esas dos son
preexistentes ([BF-A](./pendientes/BF-A.md) ya lo dice de `elf_brawler`), una de las nuevas es ruido
(0,03 sobre el tope con error típico 0,92) y `bossGate_grimhold_guns` se resolvió **sin tocar la banda**,
ablandando el jefe (`quality` 31 → 27) como hicieron las ADR 0049, 0050 P2 y 0074 — ése es el precedente
del repositorio para esta puerta, y funciona: vuelve a verde. Queda
**`buildsWinDifferently_injuries`**, de ≥1,10 a **1,04**, y ahí hay dos lecturas honestas y las dos se
dejan escritas: la ADR 0131 dejó dicho que ese umbral **no se arregla bajándolo otra vez**, y a la vez su
sd de 0,17 pone el borde a ~1 error típico, que es también el perfil de una banda estrecha frente al ruido
de semilla. **Decisión del revisor, no de implementación.**

**Deuda que este paso deja anotada y no paga:** el artefacto del recorte ([BH-C](./pendientes/BH-C.md)),
que hay que resolver **antes del paso 3c**; el error vertical por apertura; y los tests que faltan —el de
la división trigonométrica a nivel de `OnTargetAim` con RNG fijo, el del penalti, el del remate, y uno que
fije los valores publicados de `tuning.json`, que hoy no cubre ninguno.
