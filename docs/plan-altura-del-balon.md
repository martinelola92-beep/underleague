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

## 4. Troceado propuesto

Cada paso termina con build, tests, lote y commit propio, como manda `CLAUDE.md`. El orden está elegido para
que el riesgo de determinismo se descubra **antes** de haber invertido en el resto.

| paso | qué | por qué en este orden |
|---|---|---|
| **0** | Instrumentación: medir la referencia actual entera (tiros, paradas, bloqueos, córners, duración, posesiones) | Sin baseline no hay nada que comparar, y las semillas van a cambiar todas |
| **1** | `Ball` gana `Z`/`VelocityZ`, gravedad y bote; **nada más los usa**. Los tiros salen con `z = 0` | Aísla el riesgo de determinismo. Si RT-024 diverge, se descubre aquí y no mezclado con reglas nuevas |
| **2** | El tiro sale con altura; la portería gana alto; un tiro por encima es saque de puerta | Primera regla nueva visible. Mueve goles: lote obligatorio |
| **3** | El alcance vertical: portero y defensas sólo tocan lo que alcanzan | Es lo que el revisor señaló como el motivo de la altura |
| **4** | **El rechace**: el toque defensivo atrapa o desvía, y lo desviado sale con velocidad y altura | Lo que cierra [BB-N](./pendientes/BB-N.md). Va al final porque necesita los tres pasos anteriores |
| **5** | `/Game` dibuja la altura | Una línea, pero pide `visual-review` |
| **6** | *(aparte, otra tanda)* altura por raza | Reabre la ADR 0092 y pide su propio calibrado |

---

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
