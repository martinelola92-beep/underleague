# Cuarta auditoría de la IA de jugadores — la causa de H1b y el bloqueo de fase de H3

Continúa `auditoria-ia-jugadores.md`, `-2.md` y `-3.md`. Esta vuelta no propone una arquitectura nueva ni
toca el motor: **explica causalmente** los dos resultados que la tercera vuelta dejó abiertos (H1b y H3) y
mide candidatos aislados sin dejar ninguno puesto.

Etiquetas usadas en todo el documento: **observado** (sale de una medición de esta vuelta), **inferido**
(se deduce de código o aritmética verificada), **hipótesis** (no medido), **confirmado** (una hipótesis
previa que una medición de esta vuelta valida), **recomendado** (propuesta de acción, no aplicada).

Métrica principal de jitter: `unproductiveReversalShare` (porcentaje de decisiones que revierten a la
acción anterior **sin** que el estado del balón haya cambiado entremedias). `actionReversalShare` queda
retirada como métrica principal: cuenta reversiones legítimas.

---

## 1. Resumen ejecutivo

1. **El contexto de `Tackle` es correcto; lo que lo tapa es la base.** Con un rival poseedor a menos de una
   casilla, `Tackle` es la acción con **mejor contexto** de las cuatro (+195) y aun así gana solo el 24,3 %.
   `ChaseBall` la supera con contexto **negativo** (−39) porque su peso base es el doble (400 contra 204).
   *(observado, §2)*
2. **Respuesta a H1b: explicación E (B+C combinadas).** No es que `Tackle` puntúe poco: es que `ChaseBall`
   y `CoverSpace` **no tienen precondición** que las invalide cuando el balón ya está al alcance de una
   entrada, y su base las hace ganar por márgenes grandes (+89 y +146). *(observado)*
3. **Confirmado que el problema es semántico/precondicional y no un peso bajo de `Tackle`:** invalidar
   `ChaseBall` cuando el poseedor rival está en rango de entrada baja el churn improductivo de **23,7 % a
   9,1 %** y sube la tasa de aprovechamiento de **19,5 % a 29,4 %**, **sin histéresis ninguna**. *(observado)*
4. **Pero ese arreglo en crudo se pasa de fuerte**: entradas 15,80 (techo de banda 14) y lesiones 0,94
   (techo 0,90). Es un candidato a **suavizar**, no a aplicar. *(observado)*
5. **H3 tiene causa aritmética: bloqueo de fase entre rivales.** Los ids son `i` en casa y `100+i` fuera;
   `100 mod 2 = 0`, así que con `decisionIntervalTicks = 2` **cada jugador y su homólogo rival deciden
   siempre en el mismo tick**, reaccionando a la vez a una posición ya vieja. Con intervalo 3 el bloqueo se
   deshace solo (`100 mod 3 = 1`). *(inferido, aritmética verificada)*
6. **Confirmado experimentalmente:** desplazar al visitante un tick, manteniendo el intervalo en 2, baja el
   churn improductivo de **23,7 % a 16,4 %** (−31 %) y sube el reversal productivo a 70,4 %, **sin mover
   ninguna métrica de banda** (entradas 12,80 → 12,50, alternancias 20,13 → 20,11, goles 2,31 → 2,26).
   *(observado, §3)*
7. La variante que se propuso para H3a, `(tick + Id*7) % 2`, **no puede probar la hipótesis**: 7 es impar,
   así que conserva la paridad y la expresión es idéntica a la actual. *(inferido)*
8. **H2 (bonus de compromiso) sobre el baseline corregido ya casi no aporta**: de 9,1 % a 8,2 % de churn.
   Su efecto útil ahora es otro — a bonus 100 devuelve las entradas de 15,80 a 14,49 y las lesiones a banda
   (0,89). Es un **freno**, no un antijitter. *(observado, §4)*
9. **Las dos correcciones juntas son peores que cada una por separado** (churn 21,7 % contra 9,1 % y 16,4 %;
   lesiones 1,00). Hay interacción real y no se pueden aplicar a ciegas. *(observado, §4)*
10. **Nada de esto está aplicado.** `Sim/Engine/Utility.cs`, `Sim/Engine/MatchEngine.cs` y
    `data/sim/tuning.json` están exactamente como al empezar la vuelta (§6).

---

## 2. H1b — por qué `ChaseBall` le gana a `Tackle` teniendo el balón al lado

### Instrumento

Volcado de la tabla de utilidad (RT-098) filtrado a las decisiones en las que **existe un rival poseedor a
menos de una casilla** del que decide: 826 decisiones. Para cada acción legal se registran los cuatro
sumandos de la fórmula por separado:

```
score = Base(rol, acción) × Táctica(estadoEquipo, acción)/100 × Rasgo/100 + Contexto(situación)
```

### Desglose (826 decisiones, rival poseedor a < 1 casilla)

| acción | score medio | base | táctica | contexto | descartada | gana |
|---|---|---|---|---|---|---|
| `Tackle` | **523** | **204** | 161 | **+195** | 10,8 % | 24,3 % |
| `ChaseBall` | 489 | **400** | 127 | **−39** | 23,6 % | 32,1 % |
| `CoverSpace` | 495 | 289 | 136 | +76 | 0,5 % | **38,5 %** |
| `MarkOpponent` | 349 | 296 | 144 | −80 | 1,1 % | 2,9 % |

Márgenes cuando gana otra: `ChaseBall` se impone por **+89** de media (252 veces), `CoverSpace` por **+146**
(252 veces).

### Lectura

- **El contexto ya hace su trabajo.** `Tackle` recibe el mejor contexto de la tabla (+195) precisamente en la
  situación en la que debería recibirlo, y `ChaseBall` recibe uno negativo (−39): el evaluador de contexto
  **sabe** que perseguir un balón que ya tienes al lado no vale nada. *(observado)*
- **La base lo anula.** `ChaseBall` parte de 400 contra los 204 de `Tackle`: 196 puntos de ventaja estructural
  contra los 234 que el contexto le da a `Tackle`. La diferencia sobrevive por los pelos y se la come la
  táctica (161 contra 127 es a favor de `Tackle`, pero se aplica *sobre* la base, así que amplifica la
  ventaja de `ChaseBall`, no la corrige). **Multiplicar la táctica y sumar el contexto hace que la base mande
  siempre que la base sea grande.** *(inferido)*
- **`Tackle` además se descarta diez veces más que las posicionales** (10,8 % contra 0,5 % / 1,1 %): tiene
  precondiciones duras (alcance, estado, cooldown de bloqueo) que las posicionales no tienen. Pierde tanto en
  la puntuación como en la legalidad.
- **La `CoverSpace` que más gana (38,5 %) es la que menos derecho tiene a ganar ahí**: 0,5 % de descartes
  significa que prácticamente nunca deja de ser legal, aunque el balón esté a una casilla del rival.

### Respuesta a H1b

De las explicaciones planteadas, la que sostienen los datos es la **E: B y C combinadas**.

> `Tackle` no puntúa poco (su contexto es el mejor de la tabla). Lo que ocurre es que **`ChaseBall` y
> `CoverSpace` carecen de la precondición que las invalidaría** en esa situación y **su peso base las
> mantiene por encima** pese a un contexto que ya las está penalizando.

**No** es la explicación A (peso base de `Tackle` demasiado bajo): subir la base de `Tackle` la haría ganar
también en situaciones donde no toca, que es el error inverso.

### Experimento aislado: invalidar `ChaseBall` en rango de entrada

Única variable: `ChaseBall` deja de ser legal cuando hay un poseedor rival dentro del rango de entrada.
Nada más se toca — ni pesos, ni histéresis, ni cadencia.

| métrica | baseline | sin `ChaseBall` en rango | banda |
|---|---|---|---|
| `unproductiveReversalShare` | 23,7 % | **9,1 %** | — |
| reversal productivo | 59,4 % | **82,1 %** | — |
| oportunidades de entrada | 9.765 | 9.605 | — |
| tasa de aprovechamiento | 19,5 % | **29,4 %** | — |
| entradas por partido | 12,80 | **15,80** | 6-14 ❌ |
| alternancias | 20,13 | 20,57 | 12-28 ✅ |
| cadena de pases | 2,03 | 2,02 | 2-4 ✅ |
| tiros | 7,36 | 7,26 | 8-16 ❌ (ver §5) |
| lesiones | 0,87 | **0,94** | 0,3-0,9 ❌ |

**Confirmado**: el churn improductivo cae un **62 %** y el aprovechamiento sube la mitad **sin introducir
histéresis**. Eso demuestra que la causa es la falta de precondición, no un peso mal puesto.

**Pero el arreglo en crudo se pasa**: un descarte duro convierte toda oportunidad en entrada, y las entradas
y las lesiones se salen por arriba. Lo que falta medir es la versión suave —penalización de contexto en vez
de descarte— barrida hasta encontrar el punto donde el churn baja sin sacar entradas ni lesiones de banda.

---

## 3. H3 — la anomalía de cadencia es un bloqueo de fase, no una propiedad del intervalo

### Causa

El escalonado de decisiones es:

```csharp
if (StateMachine.IsDecisionState(player.State)
    && (_tick + player.Id) % _tuning.DecisionIntervalTicks == 0)
```

Los ids de jugador son `i` para el equipo de casa y `100 + i` para el visitante. Con
`decisionIntervalTicks = 2`:

- `(100 + i) mod 2 = i mod 2` — **cada jugador y su homólogo rival caen siempre en el mismo grupo de fase**.
- El delantero de casa y el defensa que lo marca deciden **en el mismo tick**, los dos leyendo posiciones
  que llevan un tick sin actualizarse, y los dos actuando sobre esa lectura a la vez.
- Con intervalo 3 el bloqueo desaparece solo, porque `100 mod 3 = 1` reparte a los rivales en grupos
  distintos. Con intervalo 1 no hay escalonado en absoluto.

Eso explica por qué la tabla de cadencia de la tercera auditoría mostraba el intervalo 2 como un máximo
local en lugar de un punto en una tendencia: **2 es el único intervalo del barrido en el que el
desplazamiento de 100 entre rivales conserva el grupo.** La anomalía no es del número 2, es del `100`.
*(inferido; aritmética verificada)*

### H3a — variante que sí rompe el agrupamiento

La variante propuesta, `(tick + Id*7) % 2`, **es un no-op**: 7 es impar, así que `7·Id ≡ Id (mod 2)` y la
expresión es literalmente la misma que la actual. No puede probar nada. *(inferido)*

La variante que sí rompe el bloqueo, manteniendo el intervalo en 2 y sin tocar ninguna otra cosa, es
desplazar un equipo un tick:

```csharp
(_tick + player.Id + (visitante ? 1 : 0)) % _tuning.DecisionIntervalTicks == 0
```

### Resultado (1.000 partidos para banda, 40 para churn, semilla 1)

| métrica | sin desplazar | **desplazando al visitante** | banda |
|---|---|---|---|
| `unproductiveReversalShare` | 23,7 % | **16,4 %** | — |
| reversal productivo | 59,4 % | **70,4 %** | — |
| oportunidades de entrada | 9.765 | 8.540 | — |
| tasa de aprovechamiento | 19,5 % | 20,1 % | — |
| entradas por partido | 12,80 | 12,50 | 6-14 ✅ |
| alternancias | 20,13 | 20,11 | 12-28 ✅ |
| cadena de pases | 2,03 | 2,02 | 2-4 ✅ |
| tiros | 7,36 | 7,22 | 8-16 ❌ (ver §5) |
| lesiones | 0,87 | 0,84 | 0,3-0,9 ✅ |
| goles | 2,31 | 2,26 | INFO |

**Confirmado.** Un tick de desplazamiento, sin tocar pesos, contexto ni histéresis, elimina **el 31 % del
churn improductivo** y no mueve nada más: todas las métricas de banda quedan donde estaban, dentro del ruido
de una semilla. Las 1.225 oportunidades de entrada que desaparecen son, por definición, las que se creaban
porque dos rivales se movían simultáneamente sobre información vieja: **churn que se contaba como juego**.

Esto es lo más barato que ha aparecido en cuatro vueltas de auditoría: una línea, cero parámetros nuevos,
cero coste de balance.

---

## 4. H2 — el bonus de compromiso, reevaluado sobre el baseline corregido

H2 quedó aplazada en la tercera vuelta. Se mide ahora **encima** del arreglo de `ChaseBall`, para saber si
sigue haciendo falta una vez quitada la causa real del jitter. Única variable: el bonus que recibe la acción
actual cuando el jugador **no** tiene un poseedor rival en rango.

| bonus | churn improd. | reversal prod. | aprovech. | entradas | alternancias | cadena | tiros | lesiones |
|---|---|---|---|---|---|---|---|---|
| 0 | 9,1 % | 82,1 % | 29,4 % | 15,80 ❌ | 20,57 | 2,02 | 7,26 | 0,94 ❌ |
| 50 | 8,2 % | 81,1 % | 31,5 % | 16,09 ❌ | 20,16 | 2,05 | 7,19 | 0,95 ❌ |
| **100** | **8,2 %** | 80,9 % | 30,6 % | 14,49 ❌ | 20,59 | 2,08 | 7,09 | **0,89 ✅** |
| 150 | 8,8 % | 80,1 % | 27,1 % | 14,51 ❌ | 20,78 | 2,14 | 7,12 | 0,94 ❌ |
| 200 | 10,9 % | 76,4 % | 30,0 % | **14,07** | 20,57 | 2,16 | 6,92 | 0,92 ❌ |

**Lectura (observado):**

- **Como antijitter, H2 ya no aporta.** De 9,1 % a 8,2 % es una décima parte de lo que dio arreglar
  `ChaseBall`. La histéresis estaba compensando un síntoma: quitada la causa, casi no queda nada que compensar.
- **Su efecto útil ahora es de freno.** A bonus 100 las entradas bajan de 15,80 a 14,49 y las lesiones
  vuelven a banda (0,89), porque los jugadores lejos del balón dejan de reconsiderar y no acuden.
- **La curva no es monótona y la señal es débil**: 100 y 150 dan casi lo mismo en entradas (14,49 / 14,51)
  con lesiones muy distintas (0,89 / 0,94). Con una sola semilla eso es indistinguible del ruido. Cualquier
  decisión sobre H2 necesita dos semillas.
- **Ningún punto del barrido mete las entradas en banda** manteniendo el resto. El mejor, bonus 200, deja
  14,07 pero devuelve el churn a 10,9 % y baja los tiros a 6,92.

**Conclusión**: H2 sigue siendo un candidato **pendiente**, y solo tiene sentido *después* de decidir la
forma suave del arreglo de `ChaseBall` — porque lo que está frenando es el exceso que introduce el descarte
duro. Si el arreglo suave no produce ese exceso, H2 no hace falta.

### Interacción entre los dos arreglos (observado, y no es la esperada)

| configuración | churn improd. | aprovech. | entradas | lesiones | goles |
|---|---|---|---|---|---|
| baseline | 23,7 % | 19,5 % | 12,80 | 0,87 | 2,31 |
| solo `ChaseBall` | **9,1 %** | 29,4 % | 15,80 | 0,94 | — |
| solo desplazamiento de fase | 16,4 % | 20,1 % | 12,50 | 0,84 | 2,26 |
| **las dos juntas** | **21,7 %** | 31,0 % | 15,82 | 1,00 | 2,20 |

Las dos juntas dan **peor churn que cualquiera de las dos por separado** y las lesiones se van a 1,00. No
tengo explicación medida para esto —**hipótesis**: al quitar `ChaseBall` cerca del balón, los jugadores
próximos se reparten entre `Tackle` y `CoverSpace`, y el desplazamiento de fase hace que ese reparto se
recalcule en ticks alternos, de modo que cada uno ve al otro a medio movimiento— pero el hecho está medido
y basta para una regla operativa: **estos dos cambios no se aplican a la vez sin volver a medir la
combinación.**

---

## 5. Métricas, instrumentos y límites de esta vuelta

**Instrumentos** (todos temporales, ya retirados): volcado de utilidad por tick (RT-098) filtrado por
proximidad del poseedor rival; conmutadores por variable de entorno para activar cada experimento sin
recompilar la configuración; test de churn sobre 40 partidos; lote de `/Balance` de 1.000 partidos por
configuración.

**Métrica principal**: `unproductiveReversalShare` — reversiones a la acción anterior sin cambio de estado
del balón entremedias. `actionReversalShare` queda retirada: contaba como jitter las reversiones que
responden a un cambio real del juego, y por eso la primera auditoría dio un 58,3 % que no significaba lo que
yo dije que significaba.

**Límites que hay que tener delante al leer las tablas:**

1. **Una sola semilla.** Todo son 1.000 partidos con semilla 1. La convención del proyecto para decidir es
   dos semillas. Ninguna cifra de aquí basta para cerrar una decisión de balance; bastan para ordenar
   candidatos.
2. **`shotsPerMatch` está por debajo del suelo (8) en todas las filas, el baseline incluido (7,36).** Eso
   **no lo causa ningún experimento de esta auditoría**: viene del trabajo de siete filas que está sin
   commitear en el árbol. Es la misma cifra pendiente de decisión desde entonces; la anoto para que no se
   lea como un efecto de la IA.
3. **Las métricas de churn van sobre 40 partidos**, no 1.000: diferencias de una décima entre filas
   contiguas no son señal.
4. **No hay medición de run completa** (`--full-runs`) para ninguno de los candidatos. El efecto sobre
   `runWinRate` y `deathsPerRun` está sin medir, y las lesiones fuera de banda del arreglo duro lo hacen
   obligatorio antes de aplicar nada.

---

## 6. Decisiones

### Hacer ahora

- **Desplazamiento de fase entre equipos (H3a).** Una línea en `MatchEngine`, sin parámetros nuevos, −31 %
  de churn improductivo y **ninguna métrica de banda movida**. Es el único candidato de esta vuelta con
  relación coste/riesgo favorable. *(recomendado)*
  Antes de commitear: segunda semilla, las 43 puertas en una invocación, y `--full-runs`. No necesita ADR de
  rango porque no cambia ninguna banda; sí una nota en `simulacion.md`, porque cambia una regla de cadencia
  que estaba escrita.

### Investigar antes de tocar

- **`ChaseBall` en rango de entrada, en versión suave**: penalización de contexto en vez de descarte duro,
  barrida hasta encontrar el punto donde el churn baja de 23,7 % sin pasar de 14 entradas ni de 0,90
  lesiones. La causa está **confirmada**; lo que falta es calibrar la dosis. *(recomendado investigar)*
- **La interacción de §4.** Medir la combinación en la versión suave, no en la dura, y con dos semillas.
- **H2 (bonus de compromiso)**, solo después de lo anterior y solo si el arreglo suave deja un exceso de
  entradas que frenar. *(pendiente)*

### No tocar

- **El descarte duro de `ChaseBall` tal cual está medido.** Saca entradas (15,80) y lesiones (0,94) de banda.
  Es un instrumento de diagnóstico, no un cambio.
- **Las dos correcciones juntas.** Medido peor que cada una por separado.
- **`decisionIntervalTicks`.** La anomalía del intervalo 2 ya tiene causa y arreglo propio; cambiar el
  intervalo pagaría latencia para resolver algo que se resuelve con un `+1`.
- **Los pesos base de `Tackle`.** Subirlos arreglaría la situación de §2 rompiendo todas las demás: el
  contexto ya está bien y el problema es de precondición.
- **La estructura de la fórmula de utilidad.** Nada de lo medido en cuatro vueltas señala a la fórmula;
  señala a qué acciones son legales cuándo.

---

## 7. Estado del baseline

**El baseline está intacto.** Al cerrar esta vuelta:

- `Sim/Engine/Utility.cs` — restaurado desde la copia tomada al empezar; `git status` no lo lista.
- `Sim/Engine/MatchEngine.cs` — restaurado; `git status` no lo lista.
- `data/sim/tuning.json` — no se tocó en toda la vuelta; `git status` no lo lista.
- Instrumentos temporales (`Sim.Tests/Analysis/`) — borrados.

Verificado además por comportamiento, no solo por `git`: el lote de 1.000 partidos con semilla 1 sobre el
árbol restaurado devuelve exactamente la fila de referencia de la tercera auditoría —
`tacklesPerMatch=12,80`, `possessionChanges=20,13`, `passChainAvgLength=2,03`, `shotsPerMatch=7,36`,
`injuriesPerMatch=0,87`, `goalsPerMatch=2,31` — y los cuatro tests de determinismo (RT-024) pasan.

Los cambios sin commitear que quedan en el árbol (siete filas, `/Game`, esquemas) son los de antes de esta
auditoría y no los ha tocado.
