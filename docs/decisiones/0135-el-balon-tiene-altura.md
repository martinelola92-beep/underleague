# ADR 0135 — El balón tiene altura, y el toque defensivo puede desviarlo

**Fecha:** 23 sep 2026
**Estado:** Aceptada (decisión del revisor) — **pasos 1, 2 y 2b hechos**; el 3 ampliado por la enmienda 2
**Cierra:** [BB-N](../pendientes/BB-N.md) (el córner que no ocurre nunca), y la decisión abierta 1 de `docs/plan-intercepcion-disparo.md` §8 (anchura de la portería)
**Diseño previo:** `docs/plan-altura-del-balon.md` (`game-design-review`, diez preguntas)
**Requisitos:** RF-050, RF-057, RF-012d · extiende la [ADR 0091](./0091-el-pase-en-profundidad-es-una-carrera.md) sin tocar el pase · reabre la [ADR 0117](./0117-el-balon-suelto-se-persigue.md)

## Lo que decide el revisor

> «En la vida real un tiro que intercepta un jugador rival puede causar dos situaciones: lo intercepta y se
> lo queda, lo toca/pega y se desvía (el balón no debe ser pegajoso). Con un portero pasa lo mismo: puede
> atraparlo o despejarlo. […] el balón debe tener físicas lo más fieles posibles a la realidad y el juego
> tiene que sentirse real.»
>
> «Sobre las físicas del balón igual es el momento de darle altura al balón, esto puede ser importante a la
> hora de decidir cómo se intercepta.»

Y, sobre el alcance, tres decisiones más, tomadas con las alternativas delante:

1. **Física real**, con `z` y velocidad vertical, gravedad y bote — frente a una altura derivada del
   progreso del vuelo, sin estado.
2. **La portería gana alto y ancho, y la puntería se dispersa** — frente a declarar sólo la altura, o
   declarar la geometría y dejar el tiro apuntando al centro.
3. **El alcance es una esfera** — cuanto más alto va el balón, menos lejos se llega en el plano— frente a un
   cilindro, más legible pero menos fiel.

## Contexto: qué hace hoy el motor

Medido en 2 000 partidos de referencia: 5,69 tiros a puerta, 3,18 paradas, 2,00 goles y **0,12 bloqueos**
por partido. De ahí salen tres hechos que el revisor detectó jugando y que la medición confirma:

1. **Ningún toque defensivo produce jamás un balón vivo.** El bloqueo de un tiro hace
   `SetLoose(new Vec2(0f, 0f))` —velocidad **cero**— y la parada hace `SetOwner(goalkeeper)`, siempre. De
   los tres `SetLoose` del motor sólo el del pase fallado da velocidad. No existe la palabra *rechace* en
   `/Sim`. Consecuencia medida: **1 córner en 2 000 partidos** (BB-N).
2. **El balón nunca se despega del suelo.** `Game/Ui/MatchPitchView3D.cs:1364` lo dibuja con altura
   constante, así que en un render 3D con cámara dinámica (ADR 0114) **todos los tiros son rasos**.
3. **Todo tiro a puerta apunta al centro exacto.** No hay dispersión ni vertical ni horizontal; la
   anchura de la portería era una decisión abierta desde AW-A. Sin dispersión, la altura no significaría
   nada: todos los tiros irían a la misma altura del mismo punto.

Y uno más, que es el que hace la decisión atractiva y no sólo correcta: **la ficción del juego ya usa la
altura y el motor no la conoce.** La ficha de los enanos dice «**Bajos**, tercos y difíciles de mover».
`bodyRadius` (28-38 centicasillas) es un radio **horizontal**, para separar cuerpos, y todas las razas
ocupan una casilla.

## Decisión

**El balón pasa a ser un objeto con tres dimensiones**, con altura y velocidad vertical propias, gravedad
y rebote. La portería gana alto y ancho, el tiro apunta a un punto disperso dentro de ella, y el alcance de
porteros y defensas pasa de círculo a **esfera**. Un toque defensivo deja de atrapar siempre: puede
**atrapar o desviar**, y lo desviado sale con velocidad y con altura.

### Lo que NO entra, y por qué

- **Los pases siguen rasos** (`z = 0` todo el vuelo). La ADR 0091 calibró la física del pase y su
  intercepción; moverla en el mismo paquete mezclaría dos calibrados. Un pase raso no es una excepción al
  modelo: es el caso `z = 0`.
- **La altura por raza va en otra tanda.** La ADR 0092 dejó los sesgos de raza a suma cero con el abanico
  en 6,4 después de recalibrarlo entero. Si la altura llega a la vez que la altura *por raza*, ninguna
  desviación del lote será atribuible. Primero se mide la altura con todas las criaturas iguales; después
  se le da estatura al enano, y eso reabre la ADR 0092 a conciencia.
- **La IA no decide todavía la altura de golpeo.** Nadie elige «tiro alto para que no me bloqueen»: la
  altura sale de la situación y los atributos. Cambiar el motor de decisión y el de física a la vez es
  exactamente el error que este proyecto ya ha pagado.

### Dos decisiones menores, tomadas sin consultar y aquí registradas

- **Atrapar o despejar se deduce, no se tira.** Si el portero llega con margen, atrapa; si llega justo,
  despeja. Es más legible (`comportamiento observable > modificadores invisibles`) y **no desplaza el flujo
  de RNG**, así que el lote medirá el efecto de la regla y no el ruido de haber corrido todas las semillas.
  Una probabilidad de atrapar habría sido más fácil de ajustar y habría obligado a decidir si pertenece al
  mundo de `ChanceAveraged` de la ADR 0050 P2.
- **La dirección del rechace se deriva geométricamente**, sin tirada: del vuelo reflejado sobre el punto de
  contacto. El portero añade un sesgo hacia fuera —despejar a córner es lo que hace un portero de verdad—,
  y ese sesgo es un dato de `tuning`, no una tirada.

## Consecuencias

**Todas las semillas cambian.** Cualquier tirada nueva o duelo que deje de ocurrir desplaza el consumo de
RNG. No es un fallo —ya pasó con AW-A— pero implica que la referencia de balance hay que rehacerla entera y
que no habrá comparación byte a byte con nada anterior. Es la diferencia con los dos paquetes de hoy, que
eran inertes por construcción.

**Lo que se reabre, y no es efecto colateral sino cosas dormidas que despiertan:**

- **ADR 0117** subió `chaseBallLooseBonus` a 410 para que perseguir el balón suelto ganara la comparación de
  utilidad, calibrado con balones que recorren como mucho 1,25 casillas. **Hay que remedirlo.**
- **[BC-G](../pendientes/BC-G.md)** —el balón que se queda muerto en el córner, con tres mecanismos ya
  CONFIRMED y sin arreglar— pasa de rareza a caso frecuente en cuanto los córners existan.
- **`possessionChanges`** (banda 12-28, ADR 0081) es la métrica que más va a empujar: todo rechace es una
  posesión que cambia. Es el criterio de parada más probable del lote, por delante de los goles.

**Un riesgo técnico que hay que resolver en el primer paso:** `UpdateLooseBall` mueve el balón de golpe y
luego busca a alguien a menos de `PickupRadius = 0.5`. Un rechace a 0,7 casillas/tick **pasa de largo entre
dos ticks** y nadie puede cogerlo. La recogida tiene que hacerse por **barrido** del segmento recorrido, o
el cambio producirá balones incogibles — el síntoma opuesto al que se busca, y primo de
[BB-G](../pendientes/BB-G.md).

**Determinismo (RT-021, RT-024).** Crece la cantidad de aritmética `float`, y con ella la superficie de
divergencia entre Windows y Linux. La naturaleza del riesgo no cambia —ya hay `float` en posiciones— pero
la cantidad sí. Por eso el paso 1 del plan mete la física **sin que nada la use**: si RT-024 diverge, se
descubre antes de haber invertido en las reglas.

## Enmienda 1 (23 sep 2026): el marco es físico, y la altura se desata de la calidad

A la vista del paso 2, el revisor añade:

> «El alto puede ser malo a su vez porque puede irse por encima del larguero o pegar en él (creo que los
> postes actualmente no son físicos pero deberían serlo)».

Comprobado: los postes **no** eran físicos —«poste» sólo aparecía en comentarios— y la medición dio la
razón por partida doble. Con la altura atada a la calidad, los disparos llegaban con **máximo 0,453**
contra un larguero a 0,70: **el 0,00 % se le acercaba**, así que el larguero habría sido decorativo.

Las dos cosas van juntas porque **sin marco, desatar la altura la haría gratis**: un tiro alto sería
siempre mejor que uno raso. La puntería pasa a ser **intención + error** —el tirador apunta donde quiere y
la calidad gobierna cuánto falla, porque un delantero malo también *quiere* meterla por la escuadra— y el
marco tiene grosor, con evento propio `SHOT_POST`.

**El marco siempre rechaza, nunca mete el balón dentro.** En el fútbol de verdad un tiro al palo puede
entrar; permitirlo añadiría una tirada más a un camino que ya tiene tres, por uno de los sucesos más raros
de un partido.

Medido, 10 000 partidos: **1,44 % de los disparos al marco** (0,107 por partido, uno cada nueve),
`goalsPerMatch` 2,79 → 2,73, ninguna métrica fuera de banda.

## Enmienda 2 (23 sep 2026): el paso 3 pasa a ser «la portería disponible»

> «la dispersión atada a la calidad debería considerar trigonométricamente dónde está el que dispara, qué
> ángulo tiene a portería, qué rivales tiene en frente y dónde está posicionado el portero»

El revisor decide además que eso entre **también en la decisión de disparar**, no sólo en la puntería.

Hoy `quality` integra técnica, distancia y presión, y **no conoce ni el ángulo, ni la oclusión, ni al
portero**: tirar desde el vértice del área cuesta lo mismo que desde el punto de penalti salvo por la
distancia. Al entrar en la utilidad, las criaturas dejarán de disparar desde donde no deben, y **colocar a
los tuyos en el centro pasará a importar** — una decisión de alineación, que es donde vive el juego.

**Orden obligatorio: ángulo y oclusión primero, portero después.** El portero introduce un bucle de
realimentación —el tirador reacciona a él y él ya reacciona al balón—, y si todos los tiros van al lado
contrario, `saveRate` se desploma. El ángulo es geometría pura y se mide limpio.

Toca la IA de utilidad, así que **mueve el estilo de juego**: hay que vigilar `shotsPerMatch`,
`passChainAvgLength`, `possessionChanges` y sobre todo **las puertas de build**, porque la ADR 0110
calibró los pesos de `Shoot` del defensa y del centrocampista. Pide `game-design-review` propio antes de
implementar.

## Alternativas rechazadas

- **Altura derivada, sin estado**: la altura como función del progreso del vuelo y del tipo de golpeo. Cero
  estado nuevo, determinista por construcción, da lo justo para decidir intercepciones y dibujar. Rechazada
  por el revisor a favor de la fidelidad: *«el juego tiene que sentirse real»*.
- **Sólo el rechace, en 2D**: más barato y da córners ya, pero las reglas del rechace habría que rehacerlas
  al llegar la altura — construir sobre un modelo que ya se sabe que va a cambiar.
- **Retirar `RestartKind.Corner`** y aceptar que el córner no existe, que era la otra salida que BB-N dejaba
  abierta. Rechazada: el revisor quiere modelarlo.

## Cómo se demuestra

Por pasos, cada uno con su lote (`docs/plan-altura-del-balon.md` §4). En conjunto:

- Tests de motor: el balón cae, bota y se para; un tiro por encima del larguero es saque de puerta; un
  portero que no llega arriba no ataja; un balón rápido **no** se salta a quien podría recogerlo.
- RT-024 en verde en las dos plataformas, comprobado en el paso 1 antes que nada.
- Lote de `/Balance` con **todas** las métricas, no sólo las que motivan el cambio (lección de
  `ChaseBall pen=50`), vigilando `possessionChanges`, `goalsPerMatch`, `scorelineShare_1-0_to_3-2`,
  `shotsPerMatch` y `blockRate`.
- BB-N medida de nuevo: los córners tienen que pasar de ~1 por cada 2 000 partidos a una cifra de fútbol.
- `visual-review`: capturas del balón en alto, que hoy es imposible.
