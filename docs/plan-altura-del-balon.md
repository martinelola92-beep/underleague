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
