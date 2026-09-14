# Octava auditoría — validación del ajuste ofensivo y causa de la dominancia de F3

Continúa `auditoria-ia-jugadores-7.md`. **No se ha aplicado ningún cambio** (§16).

Etiquetas: **OBSERVADO** (medido aquí), **INFERIDO** (deducido de datos o aritmética verificable),
**HIPÓTESIS** (no medido), **CONFIRMADO** (hipótesis previa validada por medición).

## 1. Pregunta rectora

> «¿El ajuste DEF 154 / MID 237 mejora la libertad posicional sin degradar identidad, balance ni
> diferenciación entre builds? Y si es válido, ¿qué pasa con la dominancia de las formaciones con dos FWD?»

**H1: sí, y es estrictamente mejor que el baseline en todo lo comprobable.** **H2: la ventaja de F3 no viene
de que los delanteros sean demasiado buenos, sino de que los centrocampistas no aportan casi nada.**

## 2. Estado inicial

Baseline con `Shoot` DEF 77 / MID 188 / FWD 385. Candidato: DEF **154**, MID **237**, FWD sin tocar. Único
diff previo permitido en el árbol: `shootAnglePenaltyPerRow: 50 → 36`. Prohibido tocar `Utility.cs`,
`MatchEngine.cs`, `MatchPlayer.cs`, `ActionZone`, `tuning.json`, validación, composición, `ChaseBall`,
`Tackle` e intervalo de decisión. Solo se permitieron cambios **temporales** de `weights.json`.

## 3. Reproducción de A7

*(OBSERVADO)* Reproduce **exacto**, sin una décima de diferencia:

| | A7 | A8 |
|---|---|---|
| baseline `shotsPerMatch` s1/s2 | 7,36 / 7,25 | **7,36 / 7,25** |
| candidato `shotsPerMatch` s1/s2 | 7,60 / 7,49 | **7,60 / 7,49** |
| identidad normalizada s1/s2 | 100/67/54 y 100/69/57 | **idéntica** |
| puertas rojas del candidato | 3 | **3** |

RT-024 en verde (4/4). La auditoría sigue.

## 4. Validación del candidato

Lote de 1.000 partidos y `--full-runs 240`, las dos semillas. **Ninguna de estas cifras fue objetivo.**

| métrica | banda | baseline s1/s2 | **candidato s1/s2** |
|---|---|---|---|
| `shotsPerMatch` | 7-15 | 7,36 / 7,25 | **7,60 / 7,49** ✅ |
| `goalsPerMatch` | INFO | 2,31 / 2,17 | 2,38 / 2,21 |
| `possessionChanges` | 12-28 | 20,13 / 21,93 | 20,21 / 22,19 ✅ |
| `passChainAvgLength` | 2-4 | 2,03 / 2,10 | 2,02 / 2,09 ✅ |
| `tacklesPerMatch` | 6-14 | 12,80 / 10,33 | 12,74 / 10,30 ✅ |
| `injuriesPerMatch` | 0,3-0,9 | 0,87 / 0,52 | **0,85 / 0,51** ✅ |
| `ballThirdMaxShare` | ≤52 | 47,14 / 48,04 | 47,60 / 48,30 ✅ |
| `runWinRate` | 20-30 | **19,58** / 22,92 | **20,42 / 25,00** ✅ |
| `deathsPerRun` | 1,5-3 | 1,56 / 1,63 | 1,53 / 1,84 ✅ |
| `purchasesPerMarket` | 1-2 | 1,24 / 1,28 | 1,24 / 1,28 ✅ |
| `brokeMarketRunShare` | 10-25 | 7,08 / 9,17 | 7,92 / 9,17 |
| `leftoverGoldShare` | ≤15 | 8,92 / 8,37 | 8,51 / 8,29 ✅ |

*(OBSERVADO)* Las entradas **bajan** una centésima y las lesiones también, en las dos semillas: el candidato
**no** compra ataque con violencia. Y `runWinRate` entra en banda en la semilla 1 (19,58 → 20,42), donde el
baseline estaba por debajo.

### Puertas

| | baseline | **candidato** |
|---|---|---|
| rojas | **4** | **3** |
| `badBuildsLoseToNone_elf_brawler` | 50,62 | **47,71** |
| `buildsWinDifferently_passChain` | 1,09 | 1,08 |
| `EquippingAGoodBuildIsWorthSeveralPointsOfWinRate` | **roja** (1,9 puntos) | **verde** |
| curva de jefes (ADR 0033) | verde | **verde** |

*(OBSERVADO)* No aparece **ninguna** puerta nueva. La que se arregla no se arregla por casualidad: si un
defensa y un centrocampista pueden rematar, un objeto que sube técnica o fuerza tiene a quién beneficiar, y
el escalón «muy buena» de la ADR 0033 recupera contenido. *(INFERIDO)*

> §14 del encargo pide no decidir por número de puertas sino por la propiedad que cambia. La propiedad es:
> **un objeto ofensivo tiene ahora valor en más de un jugador del once.** `elf_brawler` mejora 2,9 puntos
> por el mismo motivo y `passChain` empeora 0,01, que es ruido.

## 5. Identidad posicional

Misma casilla (6,3), mismos atributos, solo cambia la posición base. Tiros normalizados, F1 = 100.

| | baseline s1/s2 | **candidato s1/s2** |
|---|---|---|
| FWD natural | 100 / 100 | 100 / 100 |
| MID arriba | 48 / 47 | **67 / 69** |
| DEF arriba | 36 / 40 | **54 / 57** |
| hueco MID−DEF | 12 / 7 | **13 / 12** |

*(OBSERVADO, CONFIRMADO en dos semillas)* **FWD > MID > DEF se mantiene inequívoco**, y el hueco entre MID y
DEF **se ensancha**: las tres posiciones se distinguen mejor con el candidato que sin él. No se cumple
ninguno de los motivos de rechazo del §14 (ni FWD≈MID≈DEF, ni DEF por encima de FWD).

## 6. Emergencia por lesión del delantero

Victorias contra el mismo rival.

| | baseline s1/s2 | **candidato s1/s2** |
|---|---|---|
| A — replegarse (E3) | **46,5 / 41,5** | 42,0 / 38,0 |
| B — DEF arriba (F6) | 15,0 / 18,0 | **28,5 / 33,5** |
| C — MID arriba (F7) | 25,0 / 24,0 | **33,5 / 37,5** |
| coste de C frente a A | **−21,5 / −17,5** | **−8,5 / −0,5** |

*(OBSERVADO)* Reconvertir deja de estar **dominado**. Con el candidato, poner al centrocampista arriba
cuesta 8,5 puntos en una semilla y **medio punto** en la otra: es una apuesta, no un error. El defensa
arriba sube de 15-18 a 28,5-33,5 — sigue siendo la peor opción, que es lo deseable, pero ya no es inútil.

Queda exactamente el reparto que pedía el §6: **replegarse = opción segura, MID arriba = apuesta razonable,
DEF arriba = alternativa peor pero funcional.**

## 7. Formaciones extremas

| | baseline s1/s2 | **candidato s1/s2** | ¿problema? |
|---|---|---|---|
| F1 referencia | 42,5 / 53,5 | 46,5 / 54,0 | — |
| E1 seis DEF | 15,5 / 16,5 | 26,5 / 18,5 | **no**: sigue muy por debajo de F1 |
| E2 seis FWD | 48,5 / 50,0 | 51,0 / 47,5 | **no**: plano, y en la semilla 2 **baja** |
| E3 todos atrás | 46,5 / 41,5 | 42,0 / 38,0 | **no**: baja |

*(OBSERVADO)* Ninguna formación extrema se vuelve dominante. E2 (seis delanteros) **no** se agrava por el
cambio: sube 2,5 puntos en una semilla y baja 2,5 en la otra, es decir, no se mueve. La condición de rechazo
del §14 no se dispara.

## 8. H2 — diagnóstico de F1 contra F3

Todo sobre el **baseline** (§11: diagnosticar, no arreglar).

### Descomposición

| formación | qué es | tiros s1/s2 | victorias | goles a favor | en contra |
|---|---|---|---|---|---|
| **F1** | 1 FWD (6,3), 3 MID | 3,36 / 3,38 | 42,5 / 53,5 | 0,92 / 1,06 | 0,94 / 0,89 |
| **F3a** | 2 FWD (6,2)(6,4), 2 MID | **5,28 / 5,28** | **70,5 / 69,0** | 1,61 / 1,50 | 0,88 / 0,89 |
| **F3b** | igual que F3a, pero (6,4) es **MID** de base | 3,67 / 3,60 | 51,0 / 59,0 | 1,03 / 1,11 | 0,88 / 0,80 |
| **F3c** | igual que F3a, pero (6,4) es **DEF** de base | **4,49 / 4,42** | 64,0 / 67,5 | 1,23 / 1,34 | **0,78 / 0,71** |
| **F3e** | casillas de F1, pero el MID de (4,2) es **FWD** de base | 4,18 / 4,30 | 60,0 / 58,5 | 1,33 / 1,23 | 0,92 / 0,90 |

> **Error de diseño experimental, anotado.** F3d («dos FWD intercambiando sus casillas») quedó definida
> idéntica a F3e y devolvió las mismas cifras. Además era un no-op por construcción: (6,2) y (6,4) son
> simétricas respecto de la fila central, así que intercambiarlas no cambia nada. La descomposición se
> sostiene con F3a/F3b/F3c/F3e, que sí aíslan las dos variables.

### Aritmética de la ventaja *(INFERIDO)*

Ventaja de F3a sobre F1 en tiros, semilla 1: **+1,92**.

- **Composición sola** (dos FWD, pero el segundo en casilla de centro del campo, F3e): **+0,82**
- **Colocación sola** (segundo jugador arriba, pero de base MID, F3b): **+0,31**
- Suma de las partes: **+1,13**
- Juntas (F3a): **+1,92** → **interacción de +0,79**

**La ventaja es superaditiva**: dos delanteros juntos arriba valen bastante más que «un delantero más» y «un
jugador más arriba» por separado. Respuesta al §9: **G — combinación de A y B**, con interacción fuerte.
*(OBSERVADO)*

### Dónde se producen los tiros (por jugador, semilla 1)

| formación | quién dispara | tiros |
|---|---|---|
| F1 | FWD (6,3) | **3,13 de 3,36 — el 93 %** |
| F1 | los tres MID juntos | **0,22 — el 6,5 %** |
| F3a | FWD (6,2) / FWD (6,4) | 2,43 / 2,77 |
| F3b | FWD (6,2) / **MID** (6,4) | 3,06 / **0,52** |
| F3c | FWD (6,2) / **DEF** (6,4) | **4,04** / 0,28 |

*(OBSERVADO)* Tres lecturas, y la tercera es la que importa:

1. **El segundo delantero no canibaliza**: el remate del único delantero baja de 3,13 a 2,43, pero el equipo
   pasa de 3,36 a 5,28.
2. **Un DEF arriba hace disparar MÁS al delantero que un MID arriba** (4,04 contra 3,06), y el equipo encaja
   menos (0,78 contra 0,88). El defensa adelantado es el jugador más cerca del balón de todo el ataque
   (distancia media 2,69) y cubre espacio el 48,4 % del tiempo: **gana el balón arriba y el delantero lo
   convierte**. El centrocampista, en cambio, compite con el delantero por el mismo papel y lo hace mal
   (0,52 tiros, `Shoot` 0,4 %).
3. **El centrocampista es el eslabón flojo.** En F1 los tres centrocampistas son el 43 % de los jugadores de
   campo y producen el **6,5 %** de los tiros. Su reparto de acciones —`FindSpace` 41 %, `CoverSpace` 21 %,
   `Retreat` 15 %— no es ni el de un defensa (52 % de cobertura) ni el de un delantero (29 % de repliegue,
   1,6 % de remate): **no defiende como un defensa ni ataca como un delantero.**

### Respuesta a §9

**La causa dominante es F** (F1 tiene una desventaja estructural) **más A** (segundo rematador), con
interacción. **No** es C ni D: `FindSpace` es casi idéntico entre las tres posiciones (41-44 %) y `Dribble`
es residual (≤1 %) en todas. **No** es E: F3a encaja **igual** que F1 (0,88 contra 0,94), así que el segundo
delantero no está saliendo gratis defensivamente — sencillamente el coste defensivo no existe porque el
centrocampista al que sustituye tampoco defendía. *(OBSERVADO)*

Dicho en una frase: **cambiar un centrocampista por un delantero gana tanto porque el centrocampista aporta
muy poco, no porque el delantero sea excesivo.**

## 9. Estado de `ChaseBall pen=50`

**Sigue bloqueado y sin aplicar.** Es una rama independiente de A5 y no se ha tocado en esta auditoría. Su
validación se hará sobre el baseline que finalmente se acepte —si se aplica 154/237, sobre ese— porque el
rango de entrada es una distancia y la calibración ofensiva cambia dónde están los jugadores.

## 10. Decisión

### Sobre 154/237: **A — aceptar** (§15)

Los diez criterios del §13, uno a uno:

| # | criterio | resultado |
|---|---|---|
| 1 | reproduce A7 | ✅ exacto (§3) |
| 2 | FWD > MID > DEF | ✅ 100/67/54 y 100/69/57, hueco MID−DEF **ensanchado** |
| 3 | MID arriba razonable | ✅ coste de −8,5 / −0,5 puntos, antes −21,5 / −17,5 |
| 4 | ninguna extrema dominante | ✅ E2 plano, E1 muy por debajo de F1 |
| 5 | guardarraíles | ✅ los siete, en las dos semillas |
| 6 | sin degradación en las puertas | ✅ de 4 rojas a 3, ninguna nueva |
| 7 | determinismo | ✅ RT-024 4/4 |
| 8 | sin código | ✅ dos números en `/data` |
| 9 | sin multiplicador global | ✅ |
| 10 | sin tocar `ActionZone` | ✅ |

Ninguno de los nueve motivos de rechazo del §14 se cumple.

### Sobre F3: **diagnóstico cerrado, sin cambio** (§11)

La ventaja de dos delanteros **no** es un defecto de los delanteros y **no** se arregla tocando `Shoot`
(A7 ya lo midió: la ventaja sobrevive a todo el barrido). El problema real es el **papel del centrocampista**,
que no tiene una función propia medible. Eso es una **decisión de diseño**, no una calibración, y necesita
su propia vuelta. Tres caminos posibles, ninguno medido todavía *(HIPÓTESIS)*:

- dar al centrocampista una función propia que hoy no tiene (¿construcción de juego, `ThroughPass` 180
  frente a los 150 del delantero, hoy residual en todas las posiciones?);
- aceptar que el centrocampista es un rol de apoyo y que dos delanteros **deben** ganar, midiendo si el coste
  aparece en la run completa y no en el partido suelto;
- revisar si la formación por defecto debe tener tres centrocampistas.

**Ninguno de los tres se toca en esta auditoría.**

## 11. Propuesta de aplicación (no aplicada)

**Cambio**, dos números en `data/ai/weights.json`:

```
base.Defender.Shoot    77 → 154
base.Midfielder.Shoot 188 → 237
```

Nada más: ni código, ni fórmula, ni `ActionZone`, ni composición, ni `FindSpace`.

**ADR necesaria** (RT-057): mueve métricas medidas (`shotsPerMatch` 7,36 → 7,60, `runWinRate` 19,58 → 20,42)
y cambia una propiedad de diseño declarada —cuánto cuesta jugar fuera de posición—, de 100/48/38 a
100/68/55.

**Métricas afectadas**: las de §4. **Puertas verificadas**: las 43, de 4 rojas a 3.

**Comandos de validación:**

```bash
dotnet test Sim.Tests -c Release --filter "Category=Gate" -m:1 -v q
dotnet run --project Balance -c Release -- --runs 1000 --seed 1 --teams data/balance/reference.json --out out/ --quiet
dotnet run --project Balance -c Release -- --runs 1000 --seed 2 --teams data/balance/reference.json --out out/ --quiet
dotnet run --project Balance -c Release -- --full-runs 240 --seed 1 --out out/ --quiet
dotnet test Sim.Tests -c Release --filter "FullyQualifiedName~Determinism" -m:1 -v q
```

**Rollback**: devolver los dos números a 77 y 188. No hay migración de datos ni de guardado; el cambio es
puramente de pesos y no toca el esquema.

## 12. Lo que NO debe hacerse

- **No tocar `FindSpace` ni `Dribble`** para arreglar F3: A8 mide que son casi idénticos entre posiciones
  (41-44 % y ≤1 %) y por tanto no son la causa.
- **No introducir restricciones de composición** (mínimo de centrocampistas, máximo de delanteros): el
  diagnóstico dice que el problema es el valor del centrocampista, no el número de delanteros.
- **No subir más allá de 154/237** sin volver a medir la curva de jefes: A7 midió que t=35 la rompe.
- **No mezclar con `ChaseBall pen=50`.**
- **No usar la tasa de victoria del instrumento como criterio fino**: F1 oscila 42,5/53,5 entre semillas.
  Los tiros normalizados son la señal robusta.

## 13. Estado final del árbol

`data/ai/weights.json` restaurado: el **único** diff vivo es `shootAnglePenaltyPerRow: 50 → 36`, del trabajo
de siete filas, anterior a esta auditoría. `Sim/Engine/` y `data/sim/` sin tocar (lo que aparece en
`Sim/Model/` y `Sim/Run/` es ese mismo trabajo de siete filas, también anterior). Instrumento
`Sim.Tests/Analysis/_A8.cs` borrado. RT-024 en verde (4/4). Los dos lotes de 1.000 partidos reproducen
exactamente el baseline:

- semilla 1: `shotsPerMatch=7,36 · tacklesPerMatch=12,80 · injuriesPerMatch=0,87 · possessionChanges=20,13 ·
  passChainAvgLength=2,03 · goalsPerMatch=2,31 · ballThirdMaxShare=47,14`
- semilla 2: `shotsPerMatch=7,25 · tacklesPerMatch=10,33 · injuriesPerMatch=0,52 · possessionChanges=21,93 ·
  passChainAvgLength=2,10 · goalsPerMatch=2,17 · ballThirdMaxShare=48,04`
