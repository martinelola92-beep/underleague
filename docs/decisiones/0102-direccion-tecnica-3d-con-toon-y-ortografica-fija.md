# ADR 0102 · Dirección técnica: 3D con toon, cámara ortográfica fija en tres cuartos

**Fecha:** 13 de septiembre de 2026 · **Estado:** aceptada · **Decisión del revisor**
**Toca:** §5 de `docs/requisitos.md` (RA-001..027), fase 3 de `docs/plan-fases.md`
**No toca:** `/Sim`. Ni una línea.

## Decisión

Los jugadores, el árbitro y el balón se representan con **modelos 3D sombreados con toon/cel shading**,
vistos por una **cámara ortográfica fija en tres cuartos**. El resultado se lee como 2D; lo que hay debajo
es 3D.

El campo sigue siendo la rejilla de **16×5 casillas** y la simulación sigue siendo plana. La tercera
dimensión es **solo presentación**.

## Por qué

**1. Es lo que RA-001 pedía desde el principio.** «Vista cenital en tres cuartos» está escrito desde la
v0.1 de los requisitos. Lo que cambia no es el encuadre, es cómo se produce.

**2. Resuelve el solapamiento sin tocar la rejilla.** El campo se dibuja entero a 63 px por casilla. Un
humano a 3x (RA-001) mide 36×51 px, un elfo 33×60 y un demonio 60×60: entre el 81 % y el 95 % de la altura
de una fila. En cenital plana, cinco filas de eso es una maraña. En tres cuartos, con sombra en el suelo
(RA-008) y ordenación por profundidad (RA-007), **el mismo solapamiento se lee como profundidad**. La
alternativa que se barajó —ensanchar el campo— no arregla esto (manteniendo el rectángulo de pantalla, más
filas dan filas **más bajas**) y además diluiría la densidad, que es el recurso del juego. Queda como
decisión aparte y aplazada: **CAT-D**.

**3. Cambia la unidad de coste del arte.** Con sprites se paga **por frame y por raza**: 23 frames por raza
(RA-016/017), 115 para las cinco del lanzamiento, 207 para las nueve, más el árbitro. Con modelos se paga
por **modelo, rig y clip**, y los clips se comparten entre razas con rigs compatibles. La proporción de cada
raza (RA-002) deja de multiplicar el número de frames y pasa a ser un parámetro del modelo.

## Qué pasa con cada requisito de §5

| Req. | Qué decía | Qué pasa |
|---|---|---|
| RA-001 | Tres cuartos; escala de trabajo 3x sobre píxel base | **Se parte.** El encuadre **se mantiene** y es la razón de este ADR. La escala «3x sobre píxel base» **se retira**: no hay píxel base |
| RA-002 | Dimensiones por raza; silueta reconocible en B/N | **Se mantiene**, reinterpretado: la tabla pasa a ser **proporción del modelo**, y la regla de la silueta pasa a ser el **criterio de aceptación** de cada modelo |
| RA-003 | Máx. 6 colores por sprite; tono base, sombra y luz | **Se mantiene en espíritu**: base/sombra/luz *es* la rampa de un toon de tres escalones. El tope de 6 colores pasa a ser el tope de la rampa más la equipación |
| RA-004 | Contorno teñido según material, nunca negro puro | **Se mantiene**: parámetro del shader de contorno en vez de píxeles pintados a mano |
| RA-005 | Luz superior izquierda, constante | **Se mantiene y se abarata**: un `DirectionalLight3D` |
| RA-006 | Anclaje al centro inferior | **Se retira**: un modelo se apoya en el plano del suelo |
| RA-007 | Ordenación por coordenada vertical descendente | **Se retira**: lo hace el búfer de profundidad |
| RA-008 | Sombra elíptica en el suelo obligatoria | **Se mantiene**, y es **más importante que antes**: en tres cuartos la sombra es lo que dice en qué casilla está un jugador. Queda abierto si es sombra real o calcomanía |
| RA-009 | Refuerzo de color de equipo en razas pequeñas | **Se mantiene**: sigue siendo un problema de legibilidad, no de técnica |
| RA-010 | Manga del color de equipo en las dos primeras filas | **Se retira**: es una regla de filas de píxeles |
| RA-011 | Desplazamiento vertical de animación de 1 píxel | **Se retira**: pasa a ser una amplitud en unidades de mundo |
| RA-015 | Dos orientaciones, por volteo horizontal | **Se retira**, y es una **ganancia**: un modelo rota, así que un jugador puede orientarse al balón de verdad en vez de mirar a izquierda o derecha |
| RA-016 | Ciclos por raza con número de frames y duración | **Se parte.** La lista de ciclos —reposo, carrera, entrada, caída, celebración— **se mantiene** como clips de animación. Los números de frames y las duraciones **se retiran** |
| RA-017 | 23 frames por raza; 115 / 207 en total | **Se retira entero.** El presupuesto pasa a ser modelos, rigs y clips |
| RA-019 | Variantes de posición, mínimo un distintivo de portero | **Se mantiene**: capa superpuesta pasa a ser malla o material |
| RA-019b | Árbitro: 3 ciclos, ~12 frames, 7 retratos | **Se parte**: los ciclos como arriba; los **7 retratos siguen siendo 2D** y no cambian |
| RA-020..022 | Highlights en estilo cómic, **no pixelart** | **Intactos.** Nunca fueron pixelart, así que este ADR no los toca. Siguen siendo un encargo aparte |
| RA-025..027 | Dirección visual; sangre como calcomanía persistente | **Se mantiene y se abarata**: la calcomanía de RA-027 es literalmente un nodo `Decal` |

Estos cambios exigen una versión nueva de `docs/requisitos.md` (§5 reescrito). Hasta que se haga, **este
ADR manda** sobre el texto de §5.

## Forma técnica

- **Renderizador**: Forward+, que es el que el proyecto ya tiene.
- **Dónde vive**: un `SubViewport` dentro de la pantalla de Partido que ya existe. Los paneles de log,
  línea de tiempo y controles siguen siendo `Control` y **no se tocan**: lo único que cambia es lo que se
  dibuja en el rectángulo del campo. La pantalla 2D actual (`MatchPitchView`) se conserva mientras el 3D no
  la iguale en legibilidad.
- **Escala**: **1 casilla = 1 unidad de mundo**. El campo es 16×5 unidades y la cámara es ortográfica, así
  que el encuadre no depende de la distancia.
- **Tamaño de un jugador**: el **radio sale de `bodyRadius`** de `data/races/` —que es el dato que ya usa
  `BodySeparation` (ADR 0020): enano 30, humano 32, orco 38, no-muerto 28, en centésimas de casilla— y la
  **altura sale de la proporción de RA-002**. Así lo que se ve **es** el volumen que simula el motor, y no
  una aproximación decorativa que pueda mentir sobre quién bloquea a quién.

## La frontera, que no se negocia

`/Sim` no cambia (RT-011): recibe estado y semilla y devuelve eventos y posiciones en el plano. El render
consume esa traza y no calcula ni decide nada (RT-014).

De ahí una regla nueva y explícita: **cualquier altura que el render se invente es decorado y nunca se lee
de vuelta**. Si el 3D dibuja un pase en profundidad describiendo un arco, esa altura no existe para el
motor —el balón sigue siendo un punto sin colisión interceptable por radio (RT-016, ADR 0091)— y no puede
influir en una intercepción, un bloqueo ni una parada. El día que la altura importe para el juego, será un
cambio de `/Sim` con su propio ADR, no un efecto que se cuele por el render.

## Cómo se acepta

El criterio de salida de la fase 3 es **«el partido se lee sin necesidad del log»**, y esta decisión se
valida contra él en tres pasos, sin gastar en arte:

1. Escena de partido con **cápsulas grises** a las proporciones de RA-002, leyendo la traza que ya existe.
2. **Prueba de silueta**: en tres cuartos, con sombra y sin color, ¿se distinguen las cinco razas y se
   sigue el balón? Es RA-002 comprobado antes de encargar nada.
3. Si pasa, el briefing de arte (`docs/ui-partido.md`) se escribe sobre una geometría ya probada.

Si no pasa, lo barato es cambiar la tabla de proporciones o el ángulo de la cámara. Ése es exactamente el
motivo de hacerlo con cápsulas.

## Alternativas descartadas

- **Pixelart 2D como estaba especificado.** Cierra la puerta a que un jugador se oriente al balón (RA-015),
  paga 115 frames por adelantado y deja el solapamiento sin resolver.
- **Ensanchar el campo a 16×6 o 16×7.** No arregla el solapamiento —con el mismo rectángulo, más filas son
  filas más bajas— y diluye la densidad un 20 % o un 40 %. La proporción del campo es una conversación
  legítima aparte: **CAT-D**, aplazada a después de ver la cámara.
- **3D con perspectiva.** Una cámara en perspectiva hace que la misma casilla mida distinto según dónde
  esté, y este juego se decide en distancias de casilla que el jugador tiene que poder estimar a ojo
  (alcance de entrada, radio de intercepción, correa). Ortográfica.
