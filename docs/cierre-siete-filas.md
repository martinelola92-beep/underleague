# Cierre del campo de siete filas — informe de decisión

Informe para el revisor. **No propone aplicar nada**: mide qué rompió el paso a siete filas, prueba todas
las palancas para arreglarlo y deja el intercambio sobre la mesa. El árbol queda exactamente como estaba.

Bloquea, además, la aplicación de la corrección `ChaseBall pen=50` de la auditoría 5: esa corrección no se
puede validar sobre un baseline con puertas en rojo.

---

## 1. Qué son las siete filas

Decisión del revisor (13 sep 2026), sucesora de la ADR 0103. Con **seis** filas el centro geométrico del
campo cae **entre** la fila 2 y la 3, así que no existe una fila central: el portero, el delantero único y
el mediocentro quedaban media casilla descentrados. Con **siete** el número es impar y vuelve a haber una
única fila central (`Rows / 2 = 3`).

El trabajo está implementado y sin commitear: `Cell.cs` (Rows 7), formación, `LinkTable`, `PlacementView`,
`RunState` (esquema v4), tres esquemas, seis builds de balance y la pantalla de partido en `/Game`.

---

## 2. Atribución: qué rompió de verdad

Medido en un árbol de trabajo limpio sobre `HEAD` contra el árbol actual. **Las dos columnas son del mismo
commit de código salvo el trabajo de siete filas.**

| | `HEAD` (6 filas) | árbol (7 filas) |
|---|---|---|
| Puertas en rojo | **1** | **6** |
| cuál | `TheThreeDoctrinesBuyDifferently` (parpadeante, BA-L) | seis, ver abajo |
| Unitarios | verde | **689 / 689 verde** |

La puerta parpadeante **pasa** en el árbol de siete filas, lo que confirma que es intermitente y no una
regresión. Descontándola, **las siete filas causan cuatro regresiones reales**, no seis: dos de las seis
rojas son agregadores (`NoMandatoryMetricIsOutOfRange` y `NoGateMetricIsOutOfRange`) que solo repiten lo
que ya dicen las otras.

### Las cuatro regresiones

| # | métrica | valor | banda | tratable |
|---|---|---|---|---|
| 1 | `shotsPerMatch` | **7,36** | 8-16 | **no — es el muro de este informe** |
| 2 | `badBuildsLoseToNone_elf_brawler` | 50,62 | 10-45 | sí: reescribir la build mala para que lo sea en siete filas |
| 3 | `buildsWinDifferently_passChain` | 1,09 | ≥1,11 | sí |
| 4 | `EquippingAGoodBuild` | 1,9 puntos | más | sí, es calibración de objetos |

### Referencia completa de las dos geometrías

| métrica | 6 filas s1 | 6 filas s2 | 7 filas s1 | 7 filas s2 |
|---|---|---|---|---|
| `shotsPerMatch` | 8,40 | 8,22 | **7,36** | **7,25** |
| `goalsPerMatch` | 2,77 | 2,60 | 2,31 | 2,17 |
| `passChainAvgLength` | 2,11 | 2,19 | 2,03 | 2,10 |
| `tacklesPerMatch` | 12,45 | 9,70 | 12,80 | 10,33 |
| `injuriesPerMatch` | 0,85 | 0,49 | 0,87 | 0,52 |
| `possessionChanges` | 20,74 | 21,36 | 20,13 | 21,93 |
| `ballThirdMaxShare` | 49,83 | 49,57 | 47,14 | 48,04 |
| `betterTeamWinRate` | 81,93 | 96,39 | 72,29 | 92,17 |

**Añadir una fila cuesta ~1,0 tiro y ~0,45 goles por partido, en las dos semillas.** Es el efecto estructural
de repartir los mismos siete jugadores sobre un campo un 17 % más alto: el ataque llega menos.

> Aparte: `betterTeamWinRate` está **fuera de banda por arriba en la semilla 2 con las dos geometrías**
> (96,39 y 92,17, techo 90). Esa puerta ya estaba rota como instrumento antes de las siete filas y no
> depende de esta decisión.

---

## 3. Palancas probadas

Todas con 1.000 partidos, semilla 1, una variable por medición.

### 3.1 Pesos de tiro — **no es palanca**

| ángulo | rango | tiros | goles | alternancias | cadena | entradas | lesiones |
|---|---|---|---|---|---|---|---|
| 36 | 8 | 7,36 | 2,31 | 20,13 | 2,03 | 12,80 | 0,87 |
| 42 | 8 | 7,23 | 2,27 | 20,00 | 2,04 | 12,92 | 0,86 |
| 36 | 9 | 7,44 | 2,32 | 20,06 | 2,02 | 12,76 | 0,89 |
| 42 | 9 | 7,25 | 2,28 | 19,89 | 2,02 | 12,79 | 0,86 |
| 36 | 10 | 7,47 | 2,33 | 20,14 | 2,02 | 12,84 | 0,89 |

Todo el barrido vive entre 7,23 y 7,47. **El tiro no está limitado por la decisión de tirar**: hacer el
disparo mucho más atractivo casi no produce disparos, luego el jugador rara vez llega a una posición en la
que tirar sea siquiera una opción. El cuello está en llegar, no en rematar.

(La paridad angular real para siete filas es 50 × 2,5/3,0 ≈ **42**, no el 36 que trae el árbol: el valor
actual ya penaliza **menos** que con seis filas. No es la causa.)

### 3.2 Velocidad de los jugadores — **palanca negativa**

| `baseCellsPerTickMilli` | tiros | goles | alternancias | cadena | entradas | lesiones |
|---|---|---|---|---|---|---|
| **131 (actual)** | **7,36** | 2,31 | 20,13 | 2,03 | 12,80 | 0,87 |
| 138 | 7,27 | 2,28 | 20,80 | 1,97 | 12,95 | 0,88 |
| 145 | 7,02 | 2,23 | 21,14 | 1,95 | 13,32 | 0,95 |
| 152 | 7,01 | 2,16 | 22,17 | 1,90 | 13,61 | 0,98 |
| 160 | 7,04 | 2,19 | 23,00 | 1,85 | 14,63 | 1,07 |

Subir el tempo **empeora** los tiros y además rompe cosas: la cadena baja a 1,85 (suelo 2) y las lesiones a
1,07 (techo 0,90). La razón es que la velocidad la aprovecha antes el que defiende que el que ataca: los
defensas cierran antes, hay más entradas y las posesiones mueren antes.

Esto contradice la intuición con la que se planteó añadir filas («subir la velocidad para que siga siendo
agresivo»). **Sale al revés, y está medido.**

### 3.3 Ancho de la formación — palanca pequeña pero estructural

La formación de siete filas del árbol es GK (0,3); DEF (2,2),(2,4); MID (3,3),(4,2),(4,4); FWD (6,3):
ocupa **solo las filas 2-4 de siete, el 43 % del ancho**. Con seis filas ocupaba las filas 1-4 de seis, el
67 %. **El equipo se estrechó mientras el campo se ensanchaba.**

| filas de DEF y MID ancho | tiros | goles | alternancias | cadena | entradas | lesiones | tercio |
|---|---|---|---|---|---|---|---|
| 2 y 4 (actual) | 7,36 | 2,31 | 20,13 | 2,03 | 12,80 | 0,87 | 47,14 |
| **1 y 5** | 7,53 | 2,32 | 20,66 | 2,03 | 12,60 | 0,82 | **49,86** |
| 0 y 6 | 8,03 | 2,35 | 20,81 | 1,95 | 12,94 | 0,83 | **52,29 ❌** |

Las filas 1/5 devuelven `ballThirdMaxShare` a **49,86**, clavado al 49,83 de seis filas: es la anchura que
reproduce el reparto espacial original. Las filas 0/6 llegan a 8,03 tiros pero rompen el techo del tercio
(52,29 contra 52).

**Pero ensanchar choca con las relaciones de colocación.** `DiagonalAhead` exige diferencia de fila
exactamente ±1, y el ancho pide saltos de 2. Con DEF (2,1),(2,5) y MID (3,3),(4,1),(4,5) **no resuelve
ninguna de las siete relaciones**: los seis perks del eje de colocación quedarían muertos y `noDeadPerks`
es una puerta de fase 1.

Hay una forma que satisface las dos cosas — defensas anchos, medio campo escalonado, simétrica:

```
GK (0,3)   DEF (2,1) (2,5)   MID (3,3) (4,2) (4,4)   FWD (6,3)
```

Verificada relación por relación: `Ahead` 2 pares, `Behind` 2, `Left` 6, `Right` 6, `DiagonalAhead` 2,
`DiagonalBehind` 2. `Beside` sigue sin resolver, que es el estado documentado desde el campo de cinco filas
(ningún perk del catálogo la usa). Con ella y las zonas sin tocar: tiros **7,50**, tercio **50,72**,
entradas 12,73, lesiones 0,83, cadena 2,03.

### 3.4 Ancho de las zonas de acción — **funciona y no se puede usar**

Las zonas (`actionZone.shape.*.sides`) tampoco escalaron con el campo: Defensa 2, Medio 3, Delantero 2, los
mismos valores que con seis filas. Escalarlas ×1,17 (3 / 4 / 3) es lo único que recupera los tiros:

| configuración | tiros | goles | alternancias | cadena | entradas | lesiones | tercio |
|---|---|---|---|---|---|---|---|
| formación 2/4 (actual) + zonas ×1,17 | 7,46 | 2,22 | 20,16 | 2,12 | 13,20 | 0,86 | 48,29 |
| formación ancha + zonas base | 7,50 | 2,35 | 20,72 | 2,03 | 12,73 | 0,83 | 50,72 |
| **formación ancha + zonas ×1,17** | **8,22 ✅** | 2,63 | 20,02 | 2,15 | 10,98 | 0,76 | 48,30 |

La primera fila importa: **con la formación estrecha, ensanchar las zonas no basta** (7,46). Hacen falta las
dos cosas.

Y la tercera fila deja **toda la sensación de juego en banda**. Pero al pasarle las 43 puertas, **las rojas
suben de 4 a 7**:

| puerta nueva | valor | qué significa |
|---|---|---|
| `activationRate_diagonal_press` | **0,00** | el perk **nunca se activa**: está muerto |
| `noDeadPerks` | **1,00** | lo anterior, como agregador |
| `badBuildsLoseToNone_elf_out_of_zone` | **46,67** | la build «fuera de zona» deja de ser mala |
| `coherentBuildsBeatNone_orc_violence` | **54,58** | las builds coherentes pierden ventaja (era 58+) |
| `bossGate_grimhold_guns_correct` / `_good` | 60,78 / 71,11 | dos celdas de la curva de la ADR 0033 |

Más 5 unitarios en rojo por aserciones que dependen de la formación.

**La causa es evidente en cuanto se ve: el tamaño de la zona *es* el juego de colocación.** Si todo el mundo
alcanza más, colocar bien deja de importar, la build «fuera de zona» deja de castigar, el perk de presión
diagonal no encuentra a nadie y la distancia entre una build buena y una mala se encoge. Recuperar los tiros
por esta vía **cuesta la profundidad de la fase 1**, que es el núcleo del juego.

### 3.5 Tamaño de la portería — descartado

No existe: el «a puerta» es una tirada de probabilidad (`Odds(shooter, ProbabilityKind.ShotOnTarget)`), no
geometría. La altura del campo no cambia el tamaño de la portería.

---

## 4. Lectura

**Ninguna palanca devuelve los tiros a 8 sin romper algo estructural.** Los pesos de tiro no muerden, la
velocidad va al revés, el ancho de formación aporta 0,14 y el ancho de zona —la única que llega— se paga
con la diferenciación de builds.

La conclusión que sostienen los datos es que **7,4 tiros es lo que produce un campo de siete filas con siete
jugadores**, y que la banda 8-16 se calibró para campos más estrechos (cinco y seis filas). No es un defecto
de implementación: es la consecuencia medida de la decisión de diseño de añadir una fila.

Dicho de otro modo: la pregunta no es «¿cómo arreglo los tiros?» sino «¿la banda de tiros sigue describiendo
el juego que queremos, ahora que el campo es otro?».

---

## 5. Opciones

| | qué implica | tiros | coste | puertas |
|---|---|---|---|---|
| **A. Rebajar la banda por ADR** | `shotsPerMatch` 8-16 → 7-15, con la evidencia de §2 y §3. RT-057: cambio de rango explícito. | 7,36 | ninguno estructural | quedan 3 regresiones, todas tratables |
| **B. A + ensanchar la formación** | Además, DEF a las filas 1/5 y medio campo escalonado 2/3/4. Relaciones verificadas. Cambia el valor por defecto de RF-040..045. | 7,50 | cambia una regla de juego | igual que A, y `ballThirdMaxShare` vuelve a 50,7 |
| **C. Aceptar el intercambio de zonas** | B más zonas ×1,17. | **8,22** | mata `diagonal_press`, rompe `elf_out_of_zone`, coherentes 58 → 54,6 | **sube de 4 a 7 rojas** |
| **D. Volver a seis filas** | Revertir. | 8,40 | se pierde la fila central única, que es lo que motivó el cambio | vuelve a 1 roja |

**Mi recomendación es B**, y la razón no es el medio tiro: es que la formación actual usa el 43 % del ancho
de un campo que se ensanchó a propósito. Eso es un descuido del trabajo de siete filas, no una decisión, y
arreglarlo devuelve el reparto espacial (`ballThirdMaxShare` 47,1 → 50,7) al que tenía el juego. La rebaja
de banda va aparte y es honesta: el campo cambió, la métrica lo refleja.

**C la descarto** aunque sea la única que llega a 8: pagar la profundidad de la fase 1 por una métrica de
sensación es invertir la prioridad del juego.

---

## 6. Qué queda después, sea cual sea la opción

1. **Regresión 2** — `elf_brawler` gana 50,62 % cuando debería perder (≤45). Con siete filas los perks
   físicos dejaron de ser un error para los elfos. Es reescritura de la build mala en `/data`.
2. **Regresión 3** — `buildsWinDifferently_passChain` 1,09 contra 1,11. Margen de 0,02.
3. **Regresión 4** — equipar aporta 1,9 puntos de tasa de victoria y hacen falta más. Calibración de objetos.
4. **`TheThreeDoctrinesBuyDifferently`** — parpadeante, y ya estaba en `HEAD`. Anotada como BA-L.
5. **Después de todo lo anterior**, revalidar `ChaseBall pen=50` sobre el baseline limpio: la auditoría 5
   avisa de que el rango de entrada es una distancia y el óptimo puede moverse con la geometría.

Una nota útil para ese paso: la opción B deja las entradas en 12,73 y la C en 10,98. `pen=50` sube las
entradas ~0,9, así que cuanto más bajo quede el baseline, más margen habrá bajo el techo de 14 — y con él,
la posibilidad de que una penalización algo más fuerte que 50 vuelva a ser viable.

---

## 7. Estado del árbol

Intacto. `Sim/Engine/Utility.cs`, `Sim/Engine/MatchEngine.cs`, `data/sim/tuning.json` y
`data/ai/weights.json` sin cambios respecto al inicio de este trabajo; los 689 unitarios en verde; el lote
de 1.000 partidos con semilla 1 reproduce exactamente `shotsPerMatch=7,36 · tacklesPerMatch=12,80 ·
injuriesPerMatch=0,87 · possessionChanges=20,13 · passChainAvgLength=2,03 · goalsPerMatch=2,31 ·
ballThirdMaxShare=47,14`. Las únicas modificaciones del árbol son el trabajo de siete filas que ya estaba.
