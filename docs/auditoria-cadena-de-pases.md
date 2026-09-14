# Mini-auditoría de `passChainAvgLength ≥ 2,00`

La pregunta del revisor, textual:

> «¿Queremos realmente que `passChainAvgLength ≥ 2,00` sea una restricción tan rígida que impida corregir
> una conducta espacial claramente errónea?»

**No se ha aplicado ningún cambio.** Etiquetas: **OBSERVADO**, **INFERIDO**, **HIPÓTESIS**, **CONFIRMADO**.

---

## 1. ¿Propiedad de diseño o límite heredado? — **heredado**

*(OBSERVADO — arqueología de git)*

La banda **2-4** se escribió en el **primer commit del repositorio** (`fd65b1c`, «preparación del
repositorio — requisitos v0.9») y **nunca se ha tocado**. `docs/requisitos.md` la presenta bajo el
encabezado literal **«Rango objetivo inicial»**: el propio documento que la creó dice que era un punto de
partida.

Es la **única** métrica de sensación de fútbol que sigue en su valor de v0.9. Todas sus vecinas se han
revisado con ADR según el motor crecía:

| métrica | v0.9 | hoy | revisada por |
|---|---|---|---|
| Alternancias de posesión | 12-25 | 12-28 | **ADR 0081** |
| **Cadena media de pases** | **2-4** | **2-4** | **nunca** |
| Tiros por partido | 8-16 | 7-15 | **ADR 0109** |
| Lesiones por partido | 0,3-0,8 | 0,3-0,9 | **ADR 0082** |
| Tiempo del balón por tercio | > 50 % | ≤ 52 | **ADR 0093** |
| Entradas por partido | 6-14 | 6-14 | nunca (nunca ha apretado) |

**CONFIRMADO: 2,00 no es una propiedad de diseño medida. Es un número de antes de que existiera el motor.**

---

## 2. Qué mide de verdad

*(OBSERVADO — lectura de `MatchEngine.EndPlay`)*

La métrica cuenta una cadena **solo si tuvo al menos un pase** (`if (_playPasses >= 1)`). No es «pases por
posesión»: es **«cuando un equipo encadena pases, cuántos encadena»**, con mínimo posible 1,0.

En el baseline, **el 35,2 % de las jugadas no tienen ningún pase** y por tanto no entran en la métrica. Más
de un tercio del juego es invisible para ella.

---

## 3. La distribución, que es lo que la media esconde

500 partidos, semilla 1, 8.107 cadenas. *(OBSERVADO)*

| cadenas de… | % |
|---|---|
| **1 pase** | **40,5 %** |
| 2 pases | 28,9 % |
| 3 pases | 16,9 % |
| 4 pases | 8,5 % |
| 5 pases | 3,5 % |
| 6 pases | 1,1 % |
| 7 pases | 0,5 % |
| 8 o más | 0,2 % |

Media 2,12. Y por partido:

| p05 | p25 | p50 | p75 | p95 |
|---|---|---|---|---|
| 1,64 | 1,93 | **2,10** | 2,32 | 2,77 |

### Los dos hechos que deciden

1. **El 33,4 % de los partidos ya está por debajo de 2,00.** Uno de cada tres. La puerta no mide una
   propiedad del juego: mide si la **media de mil partidos** cae por encima de la mediana de una
   distribución ancha que ya la cruza constantemente.
2. **El techo de 4 es inalcanzable.** El percentil 95 por partido es **2,77**, y las cadenas de 5 o más
   pases son el **5,3 %** del total. La banda no está centrada en el juego: el suelo corta por el medio y
   el techo no lo ha rozado nunca.

**INFERIDO:** una banda cuyo suelo deja fuera a un tercio de los partidos y cuyo techo sobra por completo no
está describiendo el juego. Está describiendo el sitio donde el baseline se paró.

---

## 4. ¿A elimina pases estériles o pases útiles? — **útiles, y hay que decirlo**

Era la pregunta clave del revisor, y la respuesta **no** es la esperanzada. *(OBSERVADO)*

| | baseline | con A |
|---|---|---|
| cadenas totales | 8.107 | **8.552** (+5,5 %) |
| jugadas totales | 12.513 | **13.523** (+8,1 %) |
| cadena media | 2,12 | 2,01 |
| cadenas que **acaban en tiro** | 31,9 %, media **2,52** | 28,9 %, media **2,32** |
| cadenas **estériles** | 68,1 %, media **1,93** | 71,1 %, media **1,89** |

**A acorta las cadenas productivas cinco veces más que las estériles** (−0,20 contra −0,04). No está podando
posesiones inútiles: está haciendo el ataque **más directo**, con más jugadas y más cadenas pero más cortas.

Es coherente con lo que A hace —el delantero deja de irse al cordel, así que llega antes a una posición de
remate— y es exactamente la clase de fútbol que un autobattler agresivo quiere. Pero **no** se puede
defender diciendo que solo quita paja: quita pases que iban a algún sitio.

---

## 5. Qué le pasa al ataque

*(OBSERVADO, 1.000 partidos por semilla)*

| | baseline s1/s2 | con A s1/s2 |
|---|---|---|
| `shotsPerMatch` | 7,47 / 7,51 | 7,21 / 7,25 |
| `goalsPerMatch` | 2,30 / 2,20 | 2,16 / 2,11 |
| `shotsOnTargetShare` | 74,96 / 75,06 | 73,77 / 75,36 |
| `ballThirdMaxShare` | 48,10 / 48,50 | 49,29 / 49,18 |
| `possessionChanges` | 19,98 / 22,02 | 20,79 / 22,89 |
| **`passChainAvgLength`** | 2,01 / 2,08 | **1,90 ❌ / 1,98 ❌** |
| **`tacklesPerMatch`** | 12,59 / 10,12 | 13,55 / 11,52 |
| **`injuriesPerMatch`** | 0,80 / 0,49 | **0,94 ❌ / 0,55** |

Y en run completa, que es lo que el revisor pidió comprobar:

| | baseline s1/s2 | con A s1/s2 |
|---|---|---|
| `runWinRate` | 20,42 / 25,00 | **20,42 / 24,58** |
| `deathsPerRun` | 1,53 / 1,84 | 1,66 / 1,68 ✅ |

**La run no se entera.** `runWinRate` es prácticamente idéntico y `deathsPerRun` se queda en banda. El coste
de A vive entero en el partido suelto, no en la progresión.

---

## 6. El hallazgo que cambia el plan

**A no está bloqueada por una puerta. Está bloqueada por dos, y la segunda no es heredada.**

- `passChainAvgLength` **1,90** contra un suelo de 2,00 — la banda de v0.9 que este informe acaba de
  desautorizar.
- `injuriesPerMatch` **0,94** contra un techo de 0,90 — banda **revisada y medida** en la **ADR 0082**, y
  además la que protege el recurso central del juego: el desgaste de la plantilla.

*(OBSERVADO)* La causa de la segunda es la misma que la de la primera: con A el delantero deja de irse a la
esquina vacía y se queda donde hay gente. Más presencia en zona disputada es más duelos —entradas 12,59 →
13,55— y más duelos son más lesiones.

**Revisar el suelo de la cadena está justificado por sí solo, pero NO desbloquea A.** Aunque la banda se
cambiara hoy, A seguiría rompiendo lesiones. Eso invalida la lectura de «una puerta demasiado ajustada
impide arreglar una conducta errónea»: la conducta errónea cuesta **violencia**, y ese presupuesto es
deliberado.

---

## 7. Recomendación

**Dos decisiones separadas, y conviene no mezclarlas.**

### 7.1 Sobre la banda — recomendado revisarla, por sus propios méritos

`passChainAvgLength` 2-4 → algo como **1,8-3,5**, con ADR. No para desbloquear A —no lo hace— sino porque:

- es la única banda de sensación que sigue en su valor de antes del motor, y su propio documento la llama
  «inicial»;
- su suelo deja fuera al **33,4 %** de los partidos ya hoy, sin ningún cambio;
- su techo (4) está a dos desviaciones del p95 real (2,77) y **nunca** ha estado cerca;
- ignora el 35,2 % de las jugadas por construcción;
- y es el filo con el que han tropezado **BA-D, BA-E y la palanca B**, las tres por centésimas.

Sin esa revisión, cualquier trabajo futuro que haga el ataque más directo —que es la identidad declarada del
juego— choca con un número que nadie ha defendido nunca con datos.

### 7.2 Sobre A — sigue bloqueada, y ahora por el motivo correcto

A es **conceptualmente la corrección buena** y los números lo sostienen: sin ángulo 32,4 → 12,2 %, línea de
fondo 30,1 → 13,4 %, apertura 0,626 → 0,785, con más jugadas y sin que la run lo note. Pero cuesta **+0,14
de lesiones** sobre un techo que está a 0,10.

Lo que falta medir antes de aplicarla *(HIPÓTESIS)*: si ese coste se puede devolver por otra vía sin
deshacer la corrección espacial —el enfriamiento de la entrada, el alcance de la entrada, o el propio
`tackleDistanceMaxCells` de 1,0 que la auditoría 9 ya dejó anotado como sospechoso de generar
«oportunidades» que no lo son—. Es una vuelta propia, corta y con una sola variable.

### 7.3 Sobre C — se mantiene lo dicho: **no aplicar**

No corrige la causa visual y mueve `goalsPerMatch` de 2,30 a 2,10. Si A acaba entrando, C sobra.
