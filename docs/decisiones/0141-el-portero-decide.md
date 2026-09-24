# ADR 0141 — El portero decide: qué pasa tras la parada, a quién se la da y cuándo sale

**Fecha**: 24 sep 2026
**Estado**: aceptada — decisión del revisor (*Gameplay AI Foundations Pass*, bloque 1)
**Paquete**: P5
**Contexto del encargo**: `docs/plan-gameplay-ai-foundations.md`

## Contexto

El portero era el agujero más grande de la IA, y de tres formas a la vez:

1. **Si ganaba el duelo, atrapaba siempre.** `ResolveSaveDuel` terminaba en `SetOwner`, sin excepción. Una
   parada **cerraba la jugada**: no existía el rechace, no existía el desvío a córner, y por tanto no
   existía la segunda jugada como fuente de gol.
2. **A quién le daba el balón no dependía de quién fuera.** `EvaluatePass` **descartaba** a cualquier
   compañero con un rival dentro del radio de presión, así que el pase era binario —libre o inexistente— y
   los atributos del receptor no entraban en la decisión por ningún sitio.
3. **No podía salir del área, y el rasgo que lo promete no podía cumplirse.** `Move` acotaba al área dos
   veces, así que `GoalkeeperLeftArea` era **inalcanzable por construcción** y `Rusher` («Sale mucho») sólo
   multiplicaba su `ChaseBall`, que el propio clamp anulaba. Es el caso más puro del patrón *«el texto
   promete lo que el código no hace»* que la auditoría de identidad fichó.

## Decisión

### 1 · Una parada tiene tres finales, y se deciden en dos pasos

Ganado el duelo:

1. **¿La bloca?** Tirada con el atributo relevante del portero contra lo bueno que fuera el remate, con una
   penalización fuerte si la parada fue una estirada: **estirándose no se atrapa, se saca como se puede**.
2. **Si no la bloca, ¿dónde va?** Lo decide **la geometría del tiro**, no otra tirada: un balón que iba al
   rincón se saca por la línea de fondo —córner—, y uno centrado **vuelve al área**, que es de donde salen
   los goles de rechace.

Usar el desvío que el disparo **ya traía calculado** (`ShotRawOffCentre`, de la ADR 0135) evita inventar
una segunda tirada y hace la regla legible: se ve de dónde sale cada resultado.

El rechace deja el balón **suelto y en juego**, con velocidad hacia el campo. Ahí nace la segunda jugada, y
se conecta con el sistema que la ADR 0139 acababa de montar: quien llega la disputa con sus atributos.

### 2 · El receptor importa: un compañero presionado ya no se descarta, **puntúa peor**

El descarte duro se sustituye por dos términos: la presión sobre el receptor **resta** al compararlo con
los demás candidatos, y **resta** en la puntuación final de la acción. Lo que la compensa es la capacidad
de ese receptor de **aguantar el balón**, definida como la media de su técnica y su fuerza:

> Las dos, y no una: recibir presionado es controlarla *y* que no te la quiten. Un técnico frágil y un
> armario torpe resuelven la misma jugada por caminos distintos, y el motor no tiene motivo para preferir
> a ninguno de los dos.

Con el valor publicado, un receptor totalmente presionado y del montón **anula el bono de «receptor
abierto»**: el pase sigue siendo posible —a veces es el único— pero deja de ser el pase cómodo que era
antes por no mirar a quién se la das.

Es general, no del portero: pero es lo que hace que el saque del portero sea una decisión —«corto si hay
alguien seguro, largo si no»— en vez de una tabla. Y el tercer caso que el encargo pide, *«si no existe una
opción segura, debe poder despejar lejos de su área»*, **ya funciona** sin código nuevo: el portero tiene
`Clear` desde la ADR 0138 y el peligro en su propia área es alto por definición.

### 3 · Salir del área es una excepción acotada, y necesita **las dos mitades**

`ClampToArea` pasa a admitir un ensanche, y el motor decide cada tick cuánto vale para cada portero: cero
—lo normal— o `goalkeeper.exitCells`.

Dos motivos para salir, y ninguno es «porque sí»:

- **El balón suelto cerca del área**, que es lo que significa ser un portero que sale — y por eso **sólo lo
  hace quien lleva el rasgo**, no todos.
- **La urgencia** (ADR 0140): perdiendo y con el partido acabándose. La misma cifra que mueve al resto del
  equipo mueve al portero, así que no hace falta inventar un segundo reloj.

**Hizo falta abrir también la decisión, y casi se me escapa.** Ensanchar el clamp no habría servido de
nada: `EvaluateChaseBall` descartaba al portero salvo que el balón suelto estuviera **dentro** de su área.
El clamp decide hasta dónde puede llegar; la precondición decide si quiere ir. Con una sola de las dos, el
rasgo habría seguido sin poder cumplir su nombre y la ADR habría repetido el defecto que viene a arreglar.
Los dos sitios leen **la misma cifra** a propósito: si pudieran discrepar, el portero querría ir a sitios a
los que no puede llegar. Está fijado por test.

## Lo que NO se ha hecho, y por qué

- **El portero no sube a rematar un córner en el último minuto.** El ensanche está acotado a unas pocas
  casillas; un portero cruzando el campo es otra mecánica, con su propio riesgo y su propia lectura, y no
  cabe en «hacer alcanzable lo que ya estaba prometido».
- **El saque no elige «corto o largo» como decisión de reanudación**: el portero recibe el balón y decide
  con la tabla normal, que ahora **sí** mira a quién se la da. Partir el saque en una fase con opciones es
  balón parado, y eso es P9.
- **`Cat` y `Wall` siguen siendo ±8 en la parada.** Ahora también inclinan el blocaje, porque entran por el
  atributo relevante, pero no se les ha tocado el valor: sería calibrar.

## Valores publicados

Todos **de partida, sin calibrar**:

| dato | valor | por qué ése |
|---|---|---|
| `save.catchBasePercent` | 55 | poco más de la mitad: un portero medio bloca más de lo que rechaza, pero no mucho más |
| `save.catchDivePenaltyPercent` | 45 | una estirada casi nunca acaba en las manos |
| `save.cornerOffCentreCells` | 0,8 | lo que hay entre «iba al muñeco» y «iba al palo» |
| `save.parrySpeedCellsPerTickMilli` | 260 | más rápido que un balón suelto, más lento que un pase |
| `goalkeeper.exitCells` | 2,5 | fuera del área y reconocible, lejos de ser medio campo |
| `goalkeeper.exitUrgencyPercent` | 75 | alto **a propósito**: el encargo prohíbe el portero adelantado permanente |

## Consecuencias

- **El portero recupera muchos más balones** y eso ya se midió en la ADR 0139 (37 → 123 en veinte partidos
  por la rama aérea). Con el rechace habrá además más balones vivos en el área. Es acumulativo y **la fase
  de balance tendrá que mirarlo con todo junto**, que es exactamente para lo que existe.
- `GoalkeeperLeftArea` deja de ser inalcanzable, así que el informe post-partido puede volver a decir la
  verdad sobre ella.
- Cambia la circulación del balón de todo el partido, porque el pase a un compañero presionado deja de ser
  imposible y pasa a ser caro.
