# BA-E — goles sin ángulo: medido, con tres palancas y ninguna gratis

Informe de decisión. **No se ha aplicado ningún cambio**; el árbol termina como empezó.

> «El delantero está en la línea de fondo y tira a portería sin ángulo. Es muy irreal. El delantero tiende
> demasiado a ir a la línea de fondo.» — revisor, tercera partida

## 1. El problema, medido

Censo de los 1.912 tiros de 300 partidos (semilla 1). **Apertura** = coseno del ángulo entre la línea de
tiro y la perpendicular a la línea de gol (`dx / distancia`): vale **1 de frente a portería** y **0 desde la
propia línea de fondo`, que es exactamente «sin ángulo».

| apertura | % de los tiros |
|---|---|
| **0,0-0,1** | **15,5 %** |
| 0,1-0,2 | 2,5 % |
| 0,2-0,3 | 4,0 % |
| 0,3-0,4 | 5,2 % |
| 0,4-0,5 | 5,3 % |
| 0,5-0,6 | 6,6 % |
| 0,6-0,7 | 7,6 % |
| 0,7-0,8 | 9,8 % |
| 0,8-0,9 | 11,7 % |
| 0,9-1,0 | 31,9 % |

*(OBSERVADO)*

- **32,4 % de los tiros** salen con apertura < 0,5 —más de 60° fuera de la perpendicular— y producen el
  **37,1 % de los goles**.
- **30,1 %** se tiran a menos de **una casilla** de la línea de gol.
- Apertura media: **0,626**.

**Lo importante no es que existan: es que convierten MEJOR que la media** (ratio 1,14). La queja del revisor
no es una impresión, es la lectura correcta de un incentivo real. *(OBSERVADO)*

## 2. Por qué pasa

Dos causas independientes, cada una en un sitio distinto *(INFERIDO, verificado en código)*:

1. **La resolución del tiro no conoce el ángulo.** `offTargetChance` se compone de base + distancia +
   calidad, y nada más (`MatchEngine.ShootAt`). Un remate desde el cordel a dos casillas es, para el motor,
   de los **mejores del partido**: muy cerca y sin ninguna penalización. Por eso convierten por encima de la
   media.
2. **`FindSpace` premia la banda.** Su puntuación es `espacio × bono + avance × bono`, donde *espacio* es la
   distancia al rival más cercano y *avance* es el incremento de la **columna X**. En el córner no hay
   rivales —espacio máximo— y llegar hasta allí cuenta como avance, aunque **aleje** de la portería. El
   recorte de la línea de fuera de juego (AW-Q) limita la X, no la banda.

## 3. Tres palancas, medidas por separado

Todo con 1.000 partidos por semilla para las bandas y 300 para el censo de tiros.

### A. `FindSpace` mide el avance hacia la **portería** y no hacia la banda

Una línea: `advance` pasa de `(candidato.X − posición.X)` a `distancia al gol antes − distancia al gol
después`. En el centro del campo las dos medidas casi coinciden; cerca del área divergen justo donde está el
fallo.

| | baseline | con la palanca |
|---|---|---|
| tiros sin ángulo | 32,4 % | **12,2 %** |
| tiros desde la línea de fondo | 30,1 % | **13,4 %** |
| apertura media | 0,626 | **0,785** |
| `passChainAvgLength` | 2,01 | **1,90 ❌** |
| `injuriesPerMatch` | 0,80 | **0,94 ❌** |
| `tacklesPerMatch` | 12,59 | 13,55 |

**Es con diferencia la mejor corrección geométrica y rompe dos bandas.** *(OBSERVADO)*

### B. La **utilidad** de `Shoot` penaliza el ángulo muerto

Sustituir el desvío de fila por la apertura real. Barrido del peso:

| peso | tiros sin ángulo | `shotsPerMatch` s1/s2 | `passChain` s1/s2 | `tackles` s1 | `injuries` s1 |
|---|---|---|---|---|---|
| 0 | 32,4 % | 7,47 / 7,51 | 2,01 / 2,08 | 12,59 | 0,80 |
| 100 | 24,5 % | **8,33 / 8,29** | 1,96 ❌ / 2,04 | 12,33 | 0,82 |
| 150 | 23,0 % | 8,21 / 8,19 | 1,97 ❌ / 2,04 | 12,58 | 0,80 |
| 200 | 20,8 % | 8,04 / 7,91 | 1,98 ❌ / 2,04 | 12,74 | 0,80 |
| 400 | 8,0 % | 6,86 ❌ | 2,02 | 14,00 ❌ | 0,90 |

Curioso y explicable: **sube los tiros** (7,47 → 8,33). El jugador deja de correr al cordel y remata antes,
desde una posición decente. Por eso mismo **acorta la cadena**: el ataque se vuelve más directo y la
posesión acaba en tiro con menos pases. Con las puertas: a 200 pasan de 4 rojas a **6**. *(OBSERVADO)*

### C. La **resolución** del tiro conoce el ángulo

Añadir un término de ángulo muerto a `offTargetChance`. Es la única que **no toca la decisión**, así que no
mueve ni los tiros ni la cadena:

| peso | goles desde ángulo muerto | `goalsPerMatch` s1/s2 | `shotsOnTargetShare` | `shotsPerMatch` s1 | `passChain` s1 |
|---|---|---|---|---|---|
| 0 | **37,1 %** (con el 32,4 % de los tiros) | 2,30 / 2,20 | 75,0 | 7,47 | 2,01 |
| **1500** | **32,9 %** (con el 31,7 %) | 2,10 / 2,02 | 68,5 | 7,57 | 2,01 |
| 3000 | 25,5 % (con el 29,7 %) | 1,91 / 1,77 | 60,9 | 7,71 | 2,00 |
| 4500 | 19,0 % (con el 29,5 %) | 1,71 / 1,55 | 54,6 | 7,84 | 1,99 |

**1500 es el punto de paridad**: un tiro sin ángulo deja de convertir mejor que los demás (ratio 1,14 → 1,04)
sin llegar a castigarlo. Tiros, cadena, entradas, lesiones y alternancias **no se mueven**.

Con las puertas: **4 rojas, las mismas que hoy, pero otro conjunto** — arregla `CoherentBuildsBeatTheirBaseline`
y `EquippingAGoodBuild`, y rompe `betterTeamWinRate` (68,07 contra un suelo de 70). La causa es que menos
goles es más varianza, y la varianza se come la diferencia entre un equipo bueno y uno malo. *(INFERIDO)*

## 4. El patrón que hay detrás, y que importa más que BA-E

Las tres palancas tropiezan con **la misma pared**, y no es casualidad. El juego está apoyado en varios
filos de cuchillo:

| métrica | valor | límite | margen |
|---|---|---|---|
| `passChainAvgLength` (semilla 1) | **2,01** | ≥ 2,00 | **0,01** |
| `betterTeamWinRate` (semilla 1) | 78,31 | 70-90 | 8, con ±10 de ruido entre semillas |
| `coherentBuildsBeatNone_orc_giants` | 57,92 | ≥ 58 | **0,08** |
| `EquippingAGoodBuild` | 1,8-1,9 | «varios puntos» | ~0,2 |

*(OBSERVADO a lo largo de BA-D, BA-E y la auditoría 9)* **Cualquier cambio de juego tropieza con uno.** Eso
no es un problema de BA-E: es que las bandas ya no dejan sitio para arreglar nada, igual que pasaba con los
tiros antes de la ADR 0109.

## 5. Recomendación

**C, con peso 1500**, y solo C.

Es la única que ataca la mitad del problema que de verdad duele —que esos goles entren— sin tocar los tiros
ni la cadena, y su única puerta rota es la que lleva documentada como ruidosa desde la auditoría 5
(`betterTeamWinRate` rebota 13 puntos entre configuraciones y está **fuera por arriba en la semilla 2 con
todas las geometrías**).

**A queda como la corrección de verdad y bloqueada**: es la que arregla que el delantero *vaya* al cordel, y
hoy no cabe porque la cadena está a 0,01 de su suelo. Desbloquearla pide la misma decisión que pidió la ADR
0109 con los tiros: **mirar si el suelo de 2,00 sigue describiendo el juego que queremos** en un autobattler
agresivo y directo. Si esa banda se revisa, A entra y BA-E se cierra entero.

**B se descarta**: paga seis puertas por una corrección intermedia.

**Nada de esto se aplica sin tu decisión.** C mueve `goalsPerMatch` de 2,30 a 2,10, que es una propiedad
visible del juego.
