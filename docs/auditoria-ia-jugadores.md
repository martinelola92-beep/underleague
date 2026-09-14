# Auditoría de la IA de jugadores

**14 de septiembre de 2026.** Encargo del revisor: entender el sistema, encontrar debilidades y proponer
mejoras **mínimas y medibles**, sin refactorizar todavía.

Todo lo que hay aquí está **medido con el instrumento del propio motor** (RT-098, `--utility-census`, y la
traza `MatchTrace.ActionAt`), no inferido. Cada número dice de dónde sale.

---

## A. Cómo funciona hoy

```
puntuación = Base(rol, acción) × Táctica(estado, acción)/100 × Rasgos/100 + Contexto(situación)
```

- **Base**: 4 roles × 14 acciones, en `data/ai/weights.json`.
- **Táctica**: 4 estados de equipo (`InPossession`, `OutOfPossession`, `OffensiveTransition`,
  `DefensiveTransition`), multiplicador porcentual.
- **Rasgos**: multiplicador por `Aggressive`, `Dirty`, `Cold`…, más el bono de `Leader` adyacente.
- **Contexto**: ~60 términos en código, uno por acción, sumados al final.

**Acciones legales por estado** (`StateMachine`): sin balón ocho, con balón cinco, y los estados de
ejecución (`Passing`, `Shooting`, `Tackling`, `KnockedDown`, `Injured`, `SentOff`) **ninguna**.

**Cadencia**: cada 2 ticks, desfasada por id (`(tick + Id) % 2 == 0`). `SetOwner` fuerza decisión
inmediata al cambiar la posesión (AW-T).

**Dos filtros** antes de competir: `Discarded` (no procede) y `OutsideOuterLimit` (fuera del tope duro de
la correa, ADR 0028).

**Determinismo**: aritmética entera; empates por id ascendente (RT-097) y luego por orden de la lista.

---

## B. Los cinco problemas, por impacto

### 1. Jitter: el 58 % de los cambios de acción son ida y vuelta

**Síntoma.** Los jugadores cambian de idea y se desdicen inmediatamente.

**Evidencia.** 40 partidos, leyendo `MatchTrace.ActionAt` decisión a decisión:

| | |
|---|---|
| cambios de acción | **47.323** (≈1.183 por partido) |
| **ida y vuelta (A→B→A)** | **27.579 = 58,3 %** |
| `Retreat`↔`CoverSpace` | 19.185 = **40,6 % de todos los cambios** |
| `CoverSpace`↔`FindSpace` | 8.067 = 17,0 % |

**Dos pares copan el 58 % de los cambios**, y casi seis de cada diez cambios se revierten acto seguido.

**Causa.** No hay **histéresis**: `Choose` toma el máximo cada 2 ticks sin ningún bono por la acción que ya
está ejecutando. Con dos acciones casi empatadas, el más leve movimiento del contexto las alterna.

**Cambio mínimo.** Un **bono de compromiso**: sumar `commitmentBonus` (dato en `weights.json`) a la acción
actual al puntuarla. Un solo término, en la fórmula que ya existe, sin tocar la arquitectura.

**Qué puede romper.** Un compromiso alto hace lento el repliegue y tardío el achique. Hay que medirlo
contra `possessionChanges` y `tacklesPerMatch`.

**Test.** El instrumento de arriba, convertido en test: el porcentaje de ida y vuelta debe bajar de 58,3 %
a una banda declarada (propongo ≤ 25 %) **sin** que `possessionChanges` salga de 12-28.

---

### 2. `FindSpace` gana una de cada tres decisiones por 6 puntos

**Síntoma.** La decisión más frecuente del juego se toma prácticamente a cara o cruz.

**Evidencia.** Censo de utilidad, 217.554 decisiones:

| acción | descartada | **elegida** | 2ª | **margen medio** | score medio |
|---|---|---|---|---|---|
| CoverSpace | 0,4 % | **45,98 %** | 27,46 % | 260 | 419 |
| **FindSpace** | 64,1 % | **34,49 %** | 1,08 % | **6** | 937 |
| Retreat | 0 % | 9,23 % | 41,66 % | 431 | 247 |

Margen **6** sobre una puntuación media de **937**: un 0,6 %. Es el mismo hecho que el problema 1 visto
desde el otro lado.

**Causa.** Sospecha a verificar: varios términos de `FindSpace` empujan en la **misma dirección** —avanzar,
alejarse del rival, abrir carril— así que su puntuación es alta y plana, y el ganador lo decide el último
decimal en vez de la situación.

**Cambio mínimo.** Antes de tocar pesos, **instrumentar**: volcar los términos de `FindSpace` por separado
en `--dump-utility` para ver cuánto aporta cada uno. Si dos miden lo mismo, fundirlos.

---

### 3. Los cuatro estados tácticos son dos

**Síntoma.** Las transiciones no producen comportamiento propio.

**Evidencia.** Distancia L1 entre las columnas de `tactical` en `weights.json`:

| par | distancia |
|---|---|
| `InPossession` vs `OffensiveTransition` | **165** |
| `OutOfPossession` vs `DefensiveTransition` | **130** |
| cualquier par ofensivo↔defensivo | 1000-1055 |

Las transiciones se separan de su estado hermano un **13 %** de lo que se separan los dos bloques reales.
Son el mismo estado con ruido.

**Por qué importa.** La transición es donde el fútbol se decide: el contragolpe y el repliegue tras
pérdida. Hoy no existen como comportamiento, solo como etiqueta.

**Cambio mínimo.** Es **dato, no código**: separar las dos columnas de transición en `weights.json` y
medir. Coste casi nulo, efecto potencialmente grande.

**Test.** Que `OffensiveTransition` produzca más `ThroughPass` y `FindSpace` profundo, y
`DefensiveTransition` más `Retreat` y `MarkOpponent`, medido con el censo por estado.

---

### 4. Cuatro acciones de catorce están muertas, y dos copan el 80 %

**Evidencia.** Del mismo censo:

| acción | elegida | descartada |
|---|---|---|
| **OfferSupport** | **0,00 %** | 64,1 % |
| PressCarrier | 0,03 % | **88,5 %** |
| Block | 0,06 % | 73,3 % |
| Tackle | 0,26 % | 9,3 % |
| **CoverSpace + FindSpace** | **80,5 %** | |

`OfferSupport` **nunca** se elige, aunque es segunda opción el 8,76 % de las veces. `PressCarrier` se
descarta el 88,5 %. Y dos acciones de catorce deciden cuatro de cada cinco jugadas: el resto es decoración.

**Causa, distinta en cada caso.** `Block` se descarta por su **precondición** (alcance 1,2 casillas con
penalización de 300 por casilla: medido en CAT-F). `PressCarrier` y `ChaseBall` (87,7 %) se descartan por
diseño —solo el más cercano presiona— y eso es correcto. `OfferSupport` compite y **pierde siempre**, que
es otro problema.

**Cambio mínimo.** Ninguno todavía: **primero separar** las tres causas por acción, porque «descartada» y
«pierde» piden arreglos opuestos. Ya está el dato; falta la decisión.

---

### 5. `Shoot` y `Tackle` son funciones acantilado, no rampas

**Evidencia.** Puntuación **media** en el censo: `Tackle` **−673**, `Shoot` **−399**. Negativas. Y sus
márgenes cuando ganan: 1.354 y 1.085 — los dos mayores de la tabla.

**Qué significa.** No compiten: están fuera casi siempre y, cuando entran, arrasan. Es un interruptor
disfrazado de puntuación, y hace imposible calibrarlas con un peso — **medido hoy** con la entrada sin
balón: el bono entre 70 y 120 daba **cifras idénticas hasta la segunda decimal** (ADR 0105).

**Y el defecto conocido encima.** `Shoot` penaliza el ángulo con `|Y − CenterRow|`, **filas de desvío**, no
el ángulo real que abarca la portería. Un delantero en la línea de fondo y descentrado tiene ángulo
prácticamente nulo y el modelo solo le cobra el desvío de filas. Es BA-E, y con siete filas empeora porque
el máximo de ese término crece de 2,5 a 3,5.

**Cambio mínimo.** Sustituir el término de filas por el **ángulo real** que subtiende la portería desde la
posición del tirador. `Vec2` ya es `float` —RT-023 permite `float` para posiciones— así que **no hace falta
aritmética entera escalada**: el determinismo de este proyecto es «misma semilla, mismo binario», no
determinismo entre plataformas, que es explícitamente **no** un requisito de lanzamiento.

---

## C. Lo que NO tocaría todavía, y por qué

**El desempate por id.** Es cierto que favorece al id bajo. Pero los empates **exactos** en puntuaciones de
tres y cuatro cifras compuestas por ~60 términos enteros son raros, y el sesgo real medido es
indistinguible del problema 1, que es mil veces mayor. Además RT-041 y RT-097 **exigen** ese orden y de él
depende la reproducibilidad y el flujo de depuración entero. Cambiarlo por un hash de `tick+id` sustituiría
un sesgo **documentado** por uno **oculto** sin resolver nada medible.

**Los estados de ejecución bloqueados.** La crítica de «permitir interrupciones de emergencia» no encaja
con este modelo: `Passing`/`Shooting` son el **ejecutante** durante su acción, y cancelar un pase ya
lanzado no es fútbol. El receptor, que es quien sí debería reaccionar, **no** está bloqueado: está en
`Positioning`/`Chasing` y decide con normalidad.

**Migrar a curvas de respuesta normalizadas (0-1000 multiplicativas).** La crítica de fondo es **correcta**
—el problema 5 es exactamente esa patología— pero es una reescritura de las ~60 funciones de contexto y de
toda la calibración. Antes hay que agotar lo barato: histéresis, transiciones separadas y ángulo real. Si
después sigue habiendo acantilados, se plantea con su ADR.

**Aritmética entera escalada para vectores.** Resuelve un problema que este motor **no tiene**: las
posiciones ya son `float` por RT-023 y el determinismo no depende de ello.

---

## D. Plan mínimo propuesto, por orden de impacto/esfuerzo

| # | cambio | esfuerzo | evidencia que lo pide |
|---|---|---|---|
| 1 | **Histéresis**: bono de compromiso a la acción actual | un término + un dato | 58,3 % de ida y vuelta |
| 2 | **Ángulo real de tiro** en vez de filas de desvío | una función | BA-E + `Shoot` medio −399 |
| 3 | **Separar las dos columnas de transición** en `weights.json` | solo dato | L1 165 y 130 |
| 4 | **Volcar los términos de `FindSpace` por separado** | instrumentación | margen 6 |
| 5 | Decidir qué hacer con las cuatro acciones muertas | decisión | 0,00-0,26 % |

Los cuatro primeros mantienen determinismo, aritmética entera, `weights.json` como dato y `Contexto` como
lógica situacional, y se inspeccionan con `--dump-utility`.

## E. Métricas para no regresar

1. **`actionReversalShare`** — porcentaje de cambios A→B→A. Hoy **58,3 %**. Instrumento ya escrito.
2. **`nearTieShare`** — porcentaje de decisiones ganadas por menos del 2 % de la puntuación. Hoy, para
   `FindSpace`, prácticamente todas.
3. **`deadActionCount`** — acciones elegidas por debajo del 0,5 %. Hoy **cuatro**.
4. Las de RT-056 que ya existen, como red: `possessionChanges`, `passChainAvgLength`, `tacklesPerMatch`.

## F. Sobre las fuentes

El revisor pide apoyarse en material libre (Game AI Pro, StatsBomb Open Data, Metrica Sports, Friends of
Tracking). **En esta auditoría no se ha consultado ninguno**: todo lo de arriba sale de medir este motor.
Se dice explícitamente para no atribuir a la literatura lo que es medición propia.

Distinción que el revisor pide mantener y que aquí se respeta:

- **Práctica habitual de game AI**: la histéresis / bono de compromiso y las curvas de respuesta
  normalizadas. No son ideas de fútbol, son de IA de utilidad.
- **Geometría de fútbol real**: el ángulo que subtiende la portería es la única aquí que sí viene del
  deporte, y es elemental.
- **Propuesta propia derivada de esta arquitectura**: separar las columnas de transición, y separar las
  tres causas por las que una acción no se elige (descartada / pierde / no evaluada), que es una
  distinción que este censo hace posible.
