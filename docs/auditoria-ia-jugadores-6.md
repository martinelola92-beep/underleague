# Sexta auditoría — formación, posición base y libertad de colocación

Continúa `auditoria-ia-jugadores-5.md` y `cierre-siete-filas.md`. **No se ha aplicado ningún cambio**: el
árbol termina exactamente como empezó (§8).

Pregunta rectora, del revisor:

> «¿Podemos permitir libertad real de formación sin destruir la identidad posicional de los jugadores ni la
> profundidad táctica del juego?»

Etiquetas: **OBSERVADO** (medido aquí), **INFERIDO** (deducido de código o aritmética verificada),
**HIPÓTESIS** (no medido), **CONFIRMADO** (hipótesis previa que una medición valida).

---

## 1. Los tres conceptos ya están separados en el código

Inspección, no medición. *(OBSERVADO — lectura de código)*

| concepto | dónde vive | de qué depende |
|---|---|---|
| **A. Posición base** | `PlayerDefinition.Position` (`Goalkeeper/Defender/Midfielder/Forward`) | del jugador, no de la alineación |
| **B. Posición espacial** | `LineupSlot.HomeCell` → `MatchPlayer.EffectiveHome` → `Position` | de la alineación, libre |
| **C. Rol funcional** | el `PlayerAction` elegido cada decisión | de la utilidad: base × táctica × rasgo + contexto |

**La posición base toca el resto del sistema en exactamente tres puntos**, y los tres son localizados:

1. `ctx.Weights.Base(p.Role, action)` — la tabla de pesos base por rol×acción de `data/ai/weights.json`.
2. `IsDefensiveRole(p.Role)` — habilita la entrada sin balón sobre el marcado (ADR 0105).
3. **`ActionZone.Shape[Position]`** en el constructor de `MatchPlayer` — la forma de la zona de acción
   (adelante / atrás / lados) se elige por posición base.

El tercero es el que no esperaba encontrar, y es el más fuerte de los tres.

### La colocación ya es completamente libre

`Simulator` valida una alineación con **tres** reglas y ninguna más *(OBSERVADO)*:

- la casilla-hogar cae en 0..7 × 0..6;
- el portero está dentro de su propia área;
- no hay dos jugadores en la misma casilla.

**No existe ninguna regla de composición**: ni mínimo ni máximo de DEF/MID/FWD, ni obligación de alinear un
delantero, ni restricción de que un DEF se coloque arriba. La opción A del §2 del encargo —formaciones
estándar obligatorias— **no es el estado actual del juego**: ya estamos en C/D.

### La tabla de pesos base está fuertemente diferenciada

| rol | Shoot | FindSpace | CoverSpace | MarkOpponent | Tackle | Retreat |
|---|---|---|---|---|---|---|
| Defender | **77** | 200 | **420** | **400** | **255** | 320 |
| Midfielder | 188 | 380 | 260 | 300 | 210 | 220 |
| Forward | **385** | **460** | 120 | 120 | 128 | 140 |

`Shoot` tiene un abanico de **5×** entre DEF y FWD. *(OBSERVADO)*

### La forma de zona impone un límite geométrico, no un coste

| base | zona interior (adelante / atrás / lados) | alcance desde la casilla (6,3) con el límite exterior al 200 % |
|---|---|---|
| Defender | 3 / ∞ / 2 | **hasta la columna 12** |
| Midfielder | 5 / 4 / 3 | hasta la 16 |
| Forward | ∞ / 1 / 2 | sin límite |

El área rival empieza en la columna 14 y la portería está en la 16. **Un DEF colocado de delantero no puede
acercarse a la portería rival: su zona se lo impide.** *(INFERIDO — aritmética verificada sobre
`ActionZone` y `outerLimitMultiplier`)*

Matiz importante: `EffectiveHome` se desplaza cada tick con el bloque táctico, así que la zona viaja con el
equipo y el muro es menos absoluto de lo que sugiere el cálculo estático. Por eso se midió en vez de
afirmarse.

---

## 2. Instrumento

`Sim.Tests/Analysis/_A6.cs`, temporal, ya borrado. **Los experimentos 1, 2, 4 y 5 no necesitaron tocar una
sola línea de producción**: una formación es un conjunto de `LineupSlot` y una posición base es un campo de
`PlayerDefinition`, así que se construyen equipos a medida y se juega con el motor tal cual.

**Control**: los siete jugadores de campo comparten atributos **exactamente iguales** (50 en los cinco), sin
rasgos ni perks, y el rival es **siempre** la formación F1. Así, cualquier diferencia entre un DEF y un FWD
colocados en la misma casilla viene de la posición base y de nada más — ni de los atributos, que el
generador reparte por posición, ni del rival.

> **Las cifras absolutas de esta auditoría no son comparables con las del lote de `/Balance`.** Con
> plantillas planas de 50 y sin perks, el partido de referencia produce ~3,4 tiros, no 7,4. Solo valen las
> comparaciones **dentro** de esta tabla, que es para lo que está construida.

### Formaciones (coordenadas exactas, columna × fila)

| id | GK | DEF | MID | FWD |
|---|---|---|---|---|
| **F1** equilibrada | (0,3) | (2,2) (2,4) | (3,3) (4,2) (4,4) | (6,3) |
| **F2** ancha | (0,3) | (2,1) (2,5) | (3,3) (4,1) (4,5) | (6,3) |
| **F3** ofensiva | (0,3) | (2,2) (2,4) | (3,3) (4,2) | (6,2) (6,4) |
| **F4** compacta | (0,3) | (2,3) (3,2) | (3,4) (4,3) (4,2) | (5,3) |
| **F5/F7** MID arriba | (0,3) | (2,2) (2,4) | (3,3) (4,2) (4,4) **(6,3)** | — |
| **F6** DEF arriba | (0,3) | (2,2) (2,4) **(6,3)** | (3,3) (4,2) (4,4) | — |
| **E1** seis defensas | (0,3) | (2,1) (2,3) (2,5) (3,2) (3,4) (4,3) | — | — |
| **E2** seis delanteros | (0,3) | — | — | (5,1) (5,3) (5,5) (6,2) (6,4) (7,3) |
| **E3** todos atrás | (0,3) | (1,1) (1,5) (2,2) | (2,3) (2,4) | (2,1) |

F5 y F7 son la misma formación; se listan por separado porque F7 es la comparación de una sola variable
contra F1 y F6.

---

## 3. Experimento 2 — ¿importa la posición base? (el más importante)

Misma casilla (6,3), mismos atributos, mismo rival, mismas semillas. Solo cambia el campo `Position`.

| arriba juega un… | tiros s1/s2 | goles a favor s1/s2 | victorias s1/s2 |
|---|---|---|---|
| **FWD natural** (F1) | **3,36 / 3,38** | 0,92 / 1,06 | 42,5 / 53,5 |
| **MID** (F7) | 1,62 / 1,58 | 0,39 / 0,37 | 25,0 / 24,0 |
| **DEF** (F6) | **1,20 / 1,34** | 0,23 / 0,28 | 15,0 / 18,0 |

Normalizando los tiros con el FWD natural = 100:

```
FWD natural arriba   100
MID arriba            48   (47 / 47)
DEF arriba            38   (36 / 40)
```

**CONFIRMADO: la posición base no se ignora en absoluto — domina.** El gradiente es monótono, replica en
las dos semillas con menos de 4 puntos de diferencia, y es enorme: reconvertir a un centrocampista cuesta
**la mitad** del ataque y reconvertir a un defensa **casi dos tercios**.

Comparado con la forma que el encargo dibujaba como deseable (FWD 100 / MID 80 / DEF 60), lo medido es
**mucho más severo**: 100 / 48 / 38.

**El riesgo del sistema actual no es que la posición base se ignore. Es que pesa demasiado.** *(OBSERVADO)*

Las acciones lo explican sin ambigüedad. En la casilla (6,3):

| | Shoot | FindSpace | CoverSpace | Retreat | tercio de ataque | columna máxima |
|---|---|---|---|---|---|---|
| FWD natural | **1,6 %** | 42,2 % | 17,2 % | 29,4 % | **39,8 %** | **15,94** |
| MID arriba | 0,2 % | 49,0 % | 15,1 % | 13,8 % | 47,0 % | 15,77 |
| DEF arriba | **0,1 %** | 45,2 % | **45,8 %** | 1,1 % | **13,3 %** | 14,02 |

El DEF colocado arriba **cubre espacio el 46 % del tiempo** —tres veces más que el delantero— y pisa el
tercio de ataque **13,3 %** contra el 39,8 % del delantero. No es que dispare peor: es que **no llega**, y
cuando llega hace otra cosa. *(OBSERVADO)*

---

## 4. Experimento 3 — ¿de dónde viene la rigidez: de los pesos o de la zona?

Único cambio, aislado y ya retirado: la forma de la zona se elige por la **columna de colocación**
(≤2 defensiva, 3-4 media, ≥5 ofensiva) en vez de por la posición base. Nada más se toca.

| | tiros (zona por base) | tiros (zona por casilla) | goles | victorias |
|---|---|---|---|---|
| **MID arriba** (F7) | 1,62 / 1,58 | **2,08 / 2,08** | 0,39→0,58 / 0,37→0,59 | 25→39 / 24→37 |
| **DEF arriba** (F6) | 1,20 / 1,34 | **1,35 / 1,58** | 0,23→0,38 / 0,28→0,53 | 15→24 / 18→33 |

*(OBSERVADO, las dos semillas)*

Liberar la zona recupera buena parte de la reconversión —el MID pasa de 48 a **62** sobre 100, el DEF de 38
a **44**— pero **no la elimina**: el gradiente sobrevive entero.

**INFERIDO: la rigidez se reparte. La zona aporta aproximadamente un tercio; los pesos base, el resto.** Y
lo importante para el diseño: **con la zona liberada, la identidad posicional sigue viva** (FWD 3,36 > MID
2,08 > DEF 1,46). No hacen falta las dos capas para conservar la identidad.

Efecto colateral que desaconseja aplicarlo tal cual: las entradas del equipo pasan de ~6,2 a **~11**. Un
jugador cuya zona ya no lo devuelve atrás presiona mucho más. Es un cambio de comportamiento grande y no
medido en las puertas. **Es una sonda de diagnóstico, no una propuesta.**

---

## 5. Experimento 1 — ¿diferencian las formaciones?

| formación | tiros s1/s2 | goles a favor | goles en contra | victorias s1/s2 |
|---|---|---|---|---|
| F1 equilibrada | 3,36 / 3,38 | 0,92 / 1,06 | 0,94 / 0,89 | 42,5 / 53,5 |
| F2 ancha | 3,23 / 3,25 | 0,96 / 0,83 | 1,19 / 1,28 | 40,0 / 31,5 |
| **F3 ofensiva (2 FWD)** | **5,28 / 5,28** | **1,61 / 1,50** | 0,88 / 0,89 | **70,5 / 69,0** |
| F4 compacta | 3,38 / 3,15 | 1,01 / 0,90 | 0,79 / 0,64 | 52,0 / 57,5 |
| F5/F7 sin delantero | 1,62 / 1,58 | 0,39 / 0,37 | 0,81 / 0,90 | 25,0 / 24,0 |

**Sí diferencian, y mucho** *(OBSERVADO)*: de 1,6 a 5,3 tiros, un factor de 3,3 entre la peor y la mejor,
replicando en las dos semillas. La formación **no** es decorativa.

Dos lecturas menos cómodas:

- **F3 domina.** Cambiar un centrocampista por un segundo delantero natural gana **27 puntos** de tasa de
  victoria y sube los tiros un 57 %, sin empeorar los goles encajados (0,88 contra 0,94). Eso es una
  estrategia dominante, y el propio encargo avisa de que una formación que gane en todas las métricas es
  señal de problema. *(OBSERVADO)*
- **F2 (ancha) ≈ F1.** Ensanchar la formación no produce diferencia apreciable (3,23 contra 3,36 tiros;
  las victorias van en direcciones opuestas según la semilla). **CONFIRMADO**, y coincide con lo que la
  ADR 0109 ya midió por otra vía: el ancho de la formación no es una palanca de juego.

---

## 6. Experimentos 4 y 5 — emergencia y formaciones absurdas

### Emergencia por lesión (§11)

Se lesiona el delantero y no queda ningún FWD natural. Las cuatro opciones:

| | tiros s1/s2 | goles | victorias |
|---|---|---|---|
| A — jugar sin nadie arriba (E3, todos atrás) | 2,75 / 2,52 | 0,76 / 0,74 | 46,5 / 41,5 |
| B — **un DEF ocupa la punta** (F6) | 1,20 / 1,34 | 0,23 / 0,28 | 15,0 / 18,0 |
| C — **un MID ocupa la punta** (F7) | 1,62 / 1,58 | 0,39 / 0,37 | 25,0 / 24,0 |
| D — con penalización añadida | no medido: ya hay penalización de sobra (§3) | | |

**El jugador sí tiene una decisión real, y hoy la respuesta correcta es C** (el centrocampista, no el
defensa): 35 % más tiros y 10 puntos más de victorias que con el defensa. *(OBSERVADO)*

Pero hay un resultado incómodo: **A gana a B y a C.** Reagrupar a todo el equipo atrás (46,5 / 41,5 %) es
mejor que poner a alguien reconvertido en la punta (15-25 %). Es decir, hoy **la respuesta óptima a perder
al delantero es renunciar al ataque**, no reconvertir. Eso es lo contrario de la decisión interesante que
busca el diseño. *(OBSERVADO)*

### ¿Castiga el sistema las formaciones absurdas? (§12)

| formación extrema | tiros s1/s2 | victorias s1/s2 | ¿castigada? |
|---|---|---|---|
| E1 — seis defensas | 1,16 / 1,27 | 15,5 / 16,5 | **sí, con claridad** |
| E2 — seis delanteros | 3,73 / 4,18 | 48,5 / 50,0 | **no** — iguala a la equilibrada |
| E3 — los siete en su propio campo | 2,75 / 2,52 | 46,5 / 41,5 | **apenas** |

**Respuesta mixta** *(OBSERVADO)*. Amontonar defensas se castiga solo, sin necesidad de ninguna regla. Pero
**seis delanteros rinde igual que una formación equilibrada**, y meter a todo el equipo en su propio campo
casi no cuesta nada. La utilidad y la geometría **no** castigan por sí solas todas las formaciones absurdas:
castigan las defensivas y toleran las ofensivas.

---

## 7. Decisión de diseño recomendada

Las diez preguntas del §19, respondidas con lo medido.

| # | pregunta | respuesta | apoyo |
|---|---|---|---|
| 1 | ¿Formaciones estándar obligatorias? | **No** | No hacen falta: no existen hoy y el sistema ya diferencia (§5). Además E1 se castiga solo. |
| 2 | ¿Formación completamente libre? | **Sí, y ya lo es** | `Simulator` solo valida rejilla, portero en su área y casillas distintas (§1). |
| 3 | ¿Se puede jugar sin FWD natural? | **Sí** | F5/F7 juegan y compiten (25 % de victorias); E3 llega al 46 %. |
| 4 | ¿Puede un DEF ocupar la punta? | **Sí, pero hoy casi no sirve** | 38 sobre 100 en tiros; peor que renunciar al ataque (§6). |
| 5 | ¿Puede un MID ocupar la punta? | **Sí** | 48 sobre 100; es la reconversión que funciona. |
| 6 | ¿Debe existir penalización por posición? | **No hay que añadirla: ya existe y es demasiado fuerte** | 100/48/38 medido contra el 100/80/60 deseado (§3). |
| 7 | ¿Global o específica por acción? | **Específica, y ya lo es** | La penalización vive en la tabla base por acción: `Shoot` 5× pero `Tackle` solo 2× y `CoverSpace` a favor del DEF. Exactamente lo que pedía el §10 del encargo. |
| 8 | ¿Qué rango parece razonable? | **Aflojar hacia 100 / 60-70 / 50-55**, no apretar | La sonda de la zona por casilla llega a 100/62/44 sin tocar pesos (§4). |
| 9 | ¿Qué demuestra que la base sigue importando? | Tiros normalizados 100/48/38 en dos semillas; `CoverSpace` 17 % contra 46 % en la misma casilla; tercio de ataque 39,8 % contra 13,3 %; columna máxima 15,94 contra 14,02 | §3 |
| 10 | ¿Qué implementar en la vuelta siguiente? | Ver abajo | |

### Cambios recomendados, por tipo

**De diseño (decisión del revisor, ninguno aplicado):**

- **No introducir restricciones de composición** (mínimos/máximos de DEF/MID/FWD). No hay evidencia que las
  justifique y el sistema ya castiga la formación ultradefensiva.
- **Sí hay un problema que decidir: la reconversión no es una decisión interesante hoy.** Perder al
  delantero se responde mejor renunciando al ataque que reconvirtiendo a nadie. Y a la inversa, **acumular
  delanteros naturales es una estrategia dominante** (+27 puntos por cambiar un MID por un FWD). Las dos
  cosas apuntan a lo mismo: el valor de un jugador depende demasiado de su etiqueta de posición.

**De datos:** ninguno todavía. Si se decide aflojar, la palanca más limpia y localizada es la tabla base de
`weights.json` (subir `Shoot` del DEF y del MID, bajar la ventaja del FWD), medible sin tocar código.

**De IA:** ninguno. **No** hace falta una fórmula nueva de utilidad, ni un multiplicador posicional global,
ni ningún sistema de «adecuación posicional». El sistema de adecuación que el encargo imaginaba **ya está
implementado**, repartido entre la tabla base y la forma de zona.

**De formación:** ninguno. F2 confirma por segunda vía que ensanchar no es una palanca.

**Lo que NO debe hacerse:**

- `score *= positionMultiplier` global. La penalización específica por acción que ya existe es mejor que
  cualquier multiplicador global, y el §10 del encargo lo pedía así.
- Ensanchar las `actionZone` (prohibido por el encargo y ya demostrado destructivo en la ADR 0109).
- Cambiar la forma de zona a «por casilla» tal cual: dobla las entradas (§4).
- Tratar la tasa de victoria de este instrumento como fina: tiene ~11 puntos de ruido entre semillas
  (F1 42,5 / 53,5). **Los tiros son la señal robusta** (3,36 / 3,38).

### Aplicación

**NO APLICAR NADA.** Esta vuelta es de diagnóstico y su resultado principal es un hallazgo de diseño —el
sistema de adecuación posicional ya existe y está calibrado más duro de lo que el diseño quiere—, no un
cambio listo. Aflojarlo es una decisión de regla de juego y le corresponde al revisor.

Sigue pendiente y bloqueado `ChaseBall pen=50` (auditoría 5), que hay que volver a medir sobre el baseline
definitivo.

---

## 8. Estado del árbol

Intacto. `Sim/Engine/Utility.cs`, `Sim/Engine/MatchEngine.cs`, `Sim/Engine/MatchPlayer.cs`,
`data/sim/tuning.json` y `data/ai/weights.json` sin cambios respecto al inicio de esta auditoría —el único
diff vivo en `weights.json` es la línea `shootAnglePenaltyPerRow` del trabajo de siete filas, anterior a
esta vuelta—. El instrumento `Sim.Tests/Analysis/_A6.cs` está borrado. RT-024 en verde (4/4) y el lote de
1.000 partidos con semilla 1 reproduce exactamente `shotsPerMatch=7,36 · tacklesPerMatch=12,80 ·
injuriesPerMatch=0,87 · possessionChanges=20,13 · passChainAvgLength=2,03 · goalsPerMatch=2,31 ·
ballThirdMaxShare=47,14`.

> Nota sobre el §14 del encargo: la banda de tiros **ya es 7-15**. La ADR 0109 se aceptó y commiteó antes de
> esta auditoría, así que la puerta de tiros no está pendiente.
