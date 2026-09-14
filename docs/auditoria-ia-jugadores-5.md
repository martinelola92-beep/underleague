# Quinta auditoría de la IA de jugadores — corrección de decisión con bandas de experiencia de juego

Continúa `auditoria-ia-jugadores-4.md`. Regla rectora de esta vuelta, del revisor:

> «No queremos minimizar los cambios de acción. Queremos que la IA cambie de acción cuando existe una razón
> táctica/espacial real para hacerlo y que mantenga una acción cuando no existe.»

Prioridad declarada: **corrección de decisión > identidad del juego > seguridad de banda > reducción de churn.**

Etiquetas: **observado** (medido esta vuelta), **inferido** (deducido de código o aritmética verificada),
**hipótesis** (no medido), **confirmado** (hipótesis previa que una medición valida).

---

## 1. Baseline exacto

Árbol al empezar y al terminar: `Sim/Engine/Utility.cs`, `Sim/Engine/MatchEngine.cs` y `data/sim/tuning.json`
sin modificar (§14). Lote de 1.000 partidos, `data/balance/reference.json`:

| | semilla 1 | semilla 2 |
|---|---|---|
| `possessionChanges` | 20,13 | 21,93 |
| `passChainAvgLength` | 2,03 | 2,10 |
| `shotsPerMatch` | 7,36 | 7,25 |
| `tacklesPerMatch` | 12,80 | 10,33 |
| `injuriesPerMatch` | 0,87 | 0,52 |
| `goalsPerMatch` | 2,31 | 2,17 |
| `ballThirdMaxShare` | 47,14 | 48,04 |
| `betterTeamWinRate` (60 vs 40) | 72,29 | 92,17 |

`--full-runs 240`, semilla 1: `runWinRate` 19,58 · `deathsPerRun` 1,56 · `purchasesPerMarket` 1,24 ·
`brokeMarketRunShare` 7,08 · `leftoverGoldShare` 8,92.

**Las 43 puertas parten con 6 en rojo**, por el trabajo de siete filas sin commitear y ajeno a esta
auditoría: `BadBuildsLoseToTheirBaseline`, `BuildsWinDifferently`, `NoGateMetricIsOutOfRange`,
`NoMandatoryMetricIsOutOfRange`, `ShotsPerMatchAreInRange` y
`EquippingAGoodBuildIsWorthSeveralPointsOfWinRate`. Es un dato de partida, no un resultado.

### Instrumento

El instrumento de la auditoría 4 se borró al cerrarla, así que esta vuelta usa uno nuevo
(`Sim.Tests/Analysis/_A5.cs`, temporal, ya retirado), con definiciones fijadas por escrito:

- **cambio**: fotograma en el que la acción de un jugador difiere de la del anterior.
- **reversión**: cambio que devuelve al jugador a la acción que tenía antes de su cambio anterior (A→B→A).
- **improductiva**: reversión sin ningún cambio de poseedor ni de vuelo del balón en todo el tramo A→B→A.
  `unproductiveReversalShare` = improductivas / cambios.
- **oportunidad de entrada**: tramo **maximal** de fotogramas consecutivos con el poseedor rival dentro de
  `tackleDistanceMaxCells`. Se cuenta una vez por tramo, no por tick.
- **aprovechada**: oportunidad en la que el jugador eligió `Tackle` en algún fotograma.
- Porteros excluidos. 200 partidos por semilla.

> **Las cifras absolutas de esta auditoría no son comparables con las de la auditoría 4** (allí: 40 partidos,
> una semilla, muestreo por decisión). Dentro de la auditoría 5 todas las configuraciones usan la misma vara.

Baseline del censo: churn improductivo **16,5 % / 16,3 %**, aprovechamiento **29,2 % / 30,2 %**, y en las
oportunidades de entrada `Tackle` **20,0 / 20,9 %**, `ChaseBall` **31,8 / 32,1 %**, `CoverSpace` **35,9 / 35,1 %**.

---

## 2. Bandas actuales

| Métrica | Banda | s1 | s2 |
|---|---|---|---|
| Alternancias de posesión | 12-28 | 20,13 ✅ | 21,93 ✅ |
| Cadena media de pases | 2-4 | 2,03 ✅ | 2,10 ✅ |
| Tiros por partido | 8-16 | 7,36 ❌* | 7,25 ❌* |
| Entradas por partido | 6-14 | 12,80 ✅ | 10,33 ✅ |
| Lesiones por partido | 0,3-0,9 | 0,87 ✅ | 0,52 ✅ |
| Tiempo del balón por tercio | ≤52 (ADR 0093) | 47,14 ✅ | 48,04 ✅ |
| `betterTeamWinRate` | 70-90 | 72,29 ✅ | 92,17 ❌ |

\* Los tiros están bajos por las siete filas ajenas a esta auditoría. **No se usan como criterio de rechazo.**

---

## 3. Interpretación de las bandas como experiencia de juego

El juego es un autobattler agresivo más cercano a Blood Bowl que a un simulador. Las bandas son
**guardarraíles**, no objetivos de optimización. Consecuencias operativas que he seguido:

- **Alternancias 20,13 está sano.** No he buscado bajarlas. Menos alternancias por quitar jitter sería
  empobrecer el juego, no mejorarlo.
- **Cadena 2,03 está cerca del mínimo y es coherente con el juego directo.** Solo vigilo que ninguna
  modificación la baje de 2. Ninguna lo hizo (mínimo observado 2,01).
- **Entradas 12,80 cerca del techo es identidad, no defecto.** No reduzco entradas a propósito; el único
  límite es no pasar de 14.
- **Lesiones 0,87 es el guardarraíl que de verdad aprieta.** Cualquier subida significativa necesita
  justificación fuerte. Ha sido el criterio que ha eliminado más candidatos.
- **`betterTeamWinRate` no sirve como criterio fino** y lo explica el propio `balance.md`: mide **dos
  plantillas concretas**, así que su tamaño de muestra efectivo es el número de **semillas**, no el de
  partidos. Con 92,17 en la semilla 2 el baseline ya está fuera por arriba. Lo trato como señal gruesa. (§7)

Y la consecuencia principal: **una modificación no es buena por bajar `unproductiveReversalShare`**, y esta
auditoría acaba demostrando que esa métrica además no es fiable (§7).

---

## 4. Hipótesis H3 — desfase de fase entre equipos

De la auditoría 4: los ids son `i` en casa y `100+i` fuera, `100 mod 2 = 0`, y con
`decisionIntervalTicks = 2` cada jugador y su homólogo rival deciden **siempre en el mismo tick**,
reaccionando a la vez a posiciones de hace un tick. Hipótesis: desplazar un tick al visitante rompe esa
sincronización artificial y elimina churn que no responde a nada del juego.

```csharp
(_tick + player.Id + (visitante ? 1 : 0)) % _tuning.DecisionIntervalTicks == 0
```

---

## 5. Resultados H3

| | baseline s1 | H3 s1 | baseline s2 | H3 s2 |
|---|---|---|---|---|
| churn improductivo | 16,5 % | **14,9 %** | 16,3 % | **16,3 %** |
| reversión productiva | 68,0 % | 70,7 % | 68,4 % | 68,2 % |
| aprovechamiento | 29,2 % | 29,9 % | 30,2 % | 29,9 % |
| `Tackle` en oportunidad | 20,0 % | 20,4 % | 20,9 % | 20,9 % |
| entradas | 12,80 | 12,50 ✅ | 10,33 | 10,73 ✅ |
| lesiones | 0,87 | 0,84 ✅ | 0,52 | 0,50 ✅ |
| alternancias | 20,13 | 20,11 ✅ | 21,93 | 21,83 ✅ |
| cadena | 2,03 | 2,02 ✅ | 2,10 | 2,11 ✅ |
| goles | 2,31 | 2,26 | 2,17 | 2,16 |

**El resultado de la auditoría 4 no replica.** Allí se midió 23,7 % → 16,4 % con 40 partidos, una semilla y
un instrumento más laxo. Con 200 partidos y dos semillas: la semilla 1 mejora 1,6 puntos y **la semilla 2 no
se mueve** (16,3 → 16,3). *(observado)*

El criterio de aceptación exigía reducir el churn **en ambas semillas**. No lo hace.

La causa aritmética del bloqueo de fase **sigue siendo cierta** —`100 mod 2 = 0` es verificable y no depende
de ninguna medición—; lo que no se sostiene es que quitarlo produzca una mejora de comportamiento
apreciable y reproducible. El efecto existe, es pequeño, y depende de las plantillas. *(inferido)*

---

## 6. Hipótesis H1b y su forma suave

La auditoría 4 dejó confirmado el diagnóstico: con el poseedor rival dentro del rango de entrada, `Tackle`
tiene el **mejor contexto** de las cuatro acciones y aun así pierde, porque `ChaseBall` parte de una base
mucho más alta y **no tiene precondición** que la invalide. El descarte duro lo confirmó, pero se pasaba de
fuerte.

Forma suave medida aquí, la intervención mínima que ataca la causa:

```csharp
// Perseguir un balón que ya puedes disputar no es perseguir. Solo si la entrada es de verdad una
// opción para ESTE jugador (sin cooldown): si no, lo único que se consigue es volverlo pasivo.
if (p.TackleCooldown == 0
    && ball.Owner is { } contested && contested.Team != p.Team
    && Vec2.Distance(p.Position, contested.Position) <= context.TackleDistanceMaxCells)
{
    score -= penalizacion;
}
```

No toca base de `Tackle`, ni base de `ChaseBall`, ni la fórmula, ni `DecisionIntervalTicks`, ni ninguna otra
acción fuera de esa situación.

---

## 7. Sweep H1b

**Corrección de decisión** (200 partidos por semilla):

| pen | aprov. s1 | aprov. s2 | `Tackle` en op. s1 | s2 | `ChaseBall` en op. s1 | s2 | churn s1 | churn s2 |
|---|---|---|---|---|---|---|---|---|
| 0 | 29,2 | 30,2 | 20,0 | 20,9 | 31,8 | 32,1 | 16,5 | 16,3 |
| 25 | 31,4 | — | 20,9 | — | 28,9 | — | **19,6** | — |
| 50 | 33,4 | 33,0 | 23,5 | 23,6 | 26,4 | 26,6 | **18,4** | 16,1 |
| 60 | 33,6 | 33,5 | 23,8 | 23,9 | 26,2 | 25,8 | **17,9** | 14,9 |
| 75 | 35,4 | 34,8 | 25,3 | 25,1 | 24,4 | 24,2 | 16,1 | **17,1** |
| 100 | 37,2 | 36,2 | 26,8 | 26,2 | 21,6 | 22,2 | 15,9 | 15,8 |
| 150 | 40,3 | 39,3 | 29,0 | 28,7 | 18,3 | 19,3 | 15,4 | 14,5 |
| 200 | 41,1 | — | 29,7 | — | 17,1 | — | 13,2 | — |

**Seguridad de banda** (1.000 partidos por semilla):

| pen | entradas s1 | s2 | lesiones s1 | s2 | alternancias s1 | s2 | cadena s1 | s2 |
|---|---|---|---|---|---|---|---|---|
| 0 | 12,80 | 10,33 | 0,87 | 0,52 | 20,13 | 21,93 | 2,03 | 2,10 |
| 25 | 13,38 ✅ | — | 0,89 ✅ | — | 20,43 | — | 2,01 | — |
| 50 | **13,71 ✅** | **10,81 ✅** | **0,86 ✅** | **0,53 ✅** | 20,19 | 21,61 | 2,01 | 2,09 |
| 60 | **13,80 ✅** | **11,11 ✅** | **0,88 ✅** | **0,53 ✅** | 20,11 | 21,48 | 2,02 | 2,10 |
| 75 | 14,07 ❌ | 11,62 ✅ | 0,89 ✅ | 0,53 ✅ | 20,11 | 21,84 | 2,01 | 2,09 |
| 100 | 14,42 ❌ | 12,12 ✅ | 0,94 ❌ | 0,56 ✅ | 20,28 | 21,67 | 2,01 | 2,10 |
| 150 | 15,08 ❌ | 14,17 ❌ | 0,94 ❌ | 0,57 ✅ | 20,12 | 22,13 | 2,02 | 2,07 |
| 200 | 15,73 ❌ | — | 0,95 ❌ | — | 20,41 | — | 2,02 | — |

### Los dos hallazgos del sweep

**1. La corrección de decisión es monótona y replica entre semillas con una fidelidad notable.** El
aprovechamiento sube 29→41 % y la cuota de `ChaseBall` dentro de la oportunidad cae 32→17 %, punto por punto,
y las dos semillas coinciden con menos de 1,5 puntos de diferencia **en todos los niveles**. Esto es señal,
no ruido. *(observado)*

**2. El churn improductivo no es una métrica utilizable.** Una penalización **pequeña lo empeora** (19,6 % a
pen=25 contra 16,5 % del baseline), y entre semillas cambia de signo en casi todos los puntos: a pen=50 la
semilla 1 sube 1,9 y la 2 baja 0,2; a pen=75 la 1 baja 0,4 y la 2 **sube 0,8**. Solo a partir de pen=100
bajan las dos a la vez. *(observado)*

**Explicación causal del punto 2** *(inferido, consistente con todas las filas)*: la penalización pequeña no
resuelve el conflicto, lo **iguala**. Con `ChaseBall` y `Tackle` casi empatadas, cualquier microdesplazamiento
del poseedor invierte el ganador, y el jugador oscila entre las dos. Una penalización pequeña compra
indecisión; una decisiva compra una decisión. Es el argumento contra tocar la fórmula «un poquito».

Y la consecuencia metodológica: **`unproductiveReversalShare` mide algo real pero con una relación
no monótona con la intervención y un signo que depende de la semilla. No sirve para elegir.** Lo que sí
sirve es la corrección de decisión. Exactamente el orden de prioridad que fijó el revisor.

---

## 8. Interacción H1b + H3

Obligatoria porque la auditoría 4 encontró interacción no lineal. Se confirma, y sigue siendo no aditiva:

| configuración | churn s1 | churn s2 | aprov. s1 | s2 | entradas s1 | s2 | lesiones s1 | s2 |
|---|---|---|---|---|---|---|---|---|
| baseline | 16,5 | 16,3 | 29,2 | 30,2 | 12,80 | 10,33 | 0,87 | 0,52 |
| H3 | 14,9 | 16,3 | 29,9 | 29,9 | 12,50 | 10,73 | 0,84 | 0,50 |
| pen50 | 18,4 | 16,1 | 33,4 | 33,0 | 13,71 | 10,81 | 0,86 | 0,53 |
| **H3 + pen50** | 15,5 | **18,3** | 33,8 | 33,3 | 13,71 ✅ | 11,67 ✅ | **0,90** | 0,57 |
| **H3 + pen60** | 17,9 | 18,4 | 34,6 | 34,1 | 13,94 ✅ | 11,82 ✅ | **0,93 ❌** | 0,56 |
| **H3 + pen75** | **13,7** | **13,3** | 36,0 | 36,1 | 14,29 ❌ | 12,13 ✅ | **0,92 ❌** | 0,57 |

- `H3 + pen75` es **el mejor comportamiento medido de toda la auditoría**: churn baja claramente en las dos
  semillas (13,7 / 13,3) y el aprovechamiento sube a 36 % en las dos. Pero rompe entradas **y** lesiones en
  la semilla 1. **REJECT — exceso de agresividad.**
- `H3 + pen50` y `H3 + pen60` tienen churn **errático** (una semilla mejora, la otra empeora) y empujan las
  lesiones a 0,90-0,93, justo el guardarraíl que el revisor marcó como el más delicado.
- Sumar H3 **añade lesiones** en todos los casos (+0,04 a +0,05 sobre la misma penalización sin H3) sin
  añadir corrección de decisión. *(observado)*

---

## 9. H2 — no procede

El protocolo dice investigar H2 solo si tras H1b+H3 queda un problema de balance. El candidato elegido
(§13) deja entradas y lesiones **dentro de banda en las dos semillas**, así que no hay exceso que moderar.
**H2 no se ha medido en esta vuelta** y sigue como estaba: herramienta de balance, no explicación causal.

---

## 10. Resultados por semilla

Recogidos en §5, §7 y §8. Resumen de robustez:

| afirmación | semilla 1 | semilla 2 | ¿replica? |
|---|---|---|---|
| H3 baja el churn | sí (−1,6) | no (0,0) | **no** |
| La penalización sube el aprovechamiento | sí | sí | **sí, en los 6 niveles** |
| La penalización baja `ChaseBall` en oportunidad | sí | sí | **sí, en los 6 niveles** |
| La penalización baja el churn | solo ≥75 | solo ≥100 | **no** |
| pen=50 respeta entradas y lesiones | sí | sí | **sí** |

---

## 11. Las 43 puertas

| | rojas | cuáles |
|---|---|---|
| baseline | **6** | `BadBuildsLose`, `BuildsWinDifferently`, `NoGateMetricIsOutOfRange`, `NoMandatoryMetric`, `ShotsPerMatch`, `EquippingAGoodBuild` |
| pen=50 | **7** | las 5 primeras de arriba, **más** `CoherentBuildsBeatTheirBaseline` y `BetterTeamWinRateIsInRange`; **`EquippingAGoodBuild` pasa a verde** |

Las dos nuevas, con su cifra:

- `coherentBuildsBeatNone_orc_violence` = **57,29** contra un listón de 58,00. Falla por **0,71 puntos**, en
  una celda de las catorce.
- `betterTeamWinRate_human_60_vs_human_40` = **67,47**, banda 70-90, desde 72,29 en el baseline.

**El segundo no es una regresión de calidad, es ruido, y está medido.** Barrido de la misma métrica sobre
todo el sweep, semilla 1:

| pen | 0 | 25 | 50 | 75 | 100 | 150 | 200 |
|---|---|---|---|---|---|---|---|
| `betterTeamWinRate` | 72,29 | 72,29 | **67,47** | 70,48 | 69,88 | **80,12** | 71,69 |

No hay tendencia: rebota 13 puntos y la intervención **más fuerte** (pen=150) da el valor **más alto**. Si el
mecanismo fuera «más entradas → más azar → el mejor equipo gana menos», sería monótono. No lo es. Coincide
con lo que `docs/balance.md` ya advierte de esta métrica: mide dos plantillas concretas y su muestra
efectiva son las semillas. Con el baseline a 2,3 puntos del suelo (72,29 contra 70), cualquier perturbación
la cruza. *(observado)*

---

## 12. `--full-runs`

240 runs por doctrina, semilla 1:

| | baseline | pen=50 | banda de diseño |
|---|---|---|---|
| `runWinRate` | 19,58 | **20,83** | 20-30 (entra) |
| `deathsPerRun` | 1,56 | 1,54 | 1,5-3 ✅ |
| `purchasesPerMarket` | 1,24 | 1,22 | 1-2 ✅ |
| `brokeMarketRunShare` | 7,08 | **12,92** | 10-25 (entra) |
| `leftoverGoldShare` | 8,92 | 9,19 | ≤15 ✅ |

Sin regresión; dos métricas entran en su banda de diseño. **RT-024: los cuatro tests de determinismo pasan.**

---

## 13. Decisión final

| candidato | clasificación | motivo |
|---|---|---|
| **H3 (desfase de fase)** | **REJECT — inestable** | El resultado de la auditoría 4 no replica: mejora 1,6 puntos en una semilla y 0,0 en la otra. La causa aritmética es cierta; el beneficio no es reproducible. Sumado a la penalización **añade lesiones sin añadir corrección**. |
| **`ChaseBall` pen = 25** | REJECT — parche de síntoma | Empeora el churn (19,6 %) y apenas corrige la decisión: iguala el conflicto en vez de resolverlo. |
| **`ChaseBall` pen = 50** | **ACCEPT (aplicación diferida, §14)** | Corrige la causa: aprovechamiento 29,2→33,4 / 30,2→33,0 y `ChaseBall` en oportunidad 31,8→26,4 / 32,1→26,6, **replicando en las dos semillas**. Entradas 13,71 / 10,81 y lesiones **0,86 / 0,53** — por debajo del baseline en la semilla 1. Alternancias y cadena intactas. `--full-runs` mejora. Determinismo verde. |
| **`ChaseBall` pen = 60** | INVESTIGATE | Igual de seguro (13,80 / 11,11 · 0,88 / 0,53) y algo más corrector. Alternativa válida si se quiere más mordiente; menos margen al techo. |
| **`ChaseBall` pen ≥ 75** | **REJECT — exceso de agresividad** | Entradas 14,07 y subiendo; a pen=100 lesiones 0,94. |
| **H3 + pen 50/60** | REJECT — inestable | Churn de signo contrario entre semillas y lesiones a 0,90-0,93. |
| **H3 + pen 75** | REJECT — exceso de agresividad | El mejor comportamiento medido (churn 13,7 / 13,3 en las dos semillas), pero rompe entradas **y** lesiones en la semilla 1. |
| **H2** | no procede | No queda exceso que moderar (§9). |

**Nota sobre el criterio §5.1.** El candidato aceptado **no cumple** «disminuya claramente el churn
improductivo»: en la semilla 1 lo empeora (16,5 → 18,4). Lo acepto igualmente y de forma explícita, por dos
razones medidas: la prioridad declarada pone la reducción de churn **en último lugar**, y §7 demuestra que
esa métrica no responde monótonamente a la intervención ni conserva el signo entre semillas. Elegir por ella
habría seleccionado pen=200, que rompe dos guardarraíles.

---

## 14. Cambio aplicado

**Ninguno. El baseline está intacto y esta auditoría no ha dejado código puesto.**

No es una duda sobre el candidato: es que **este árbol no puede validarlo**. Las 43 puertas parten con **6 en
rojo** por el trabajo de siete filas sin commitear, que el revisor puso explícitamente fuera del alcance de
esta auditoría. Aplicar un cambio de IA encima de un baseline rojo hace imposible atribuir después qué
rompió qué, y la convención del proyecto es que un hito cierra con las puertas en verde.

**Para aplicar `pen = 50` hace falta, en este orden:**

1. Cerrar las siete filas y recuperar las 6 puertas (en particular `ShotsPerMatchAreInRange`, con los tiros
   en 7,3 contra un suelo de 8).
2. Volver a medir el sweep sobre ese baseline nuevo: la geometría cambia las distancias y el rango de
   entrada es una distancia. **El punto óptimo puede moverse.**
3. Llevar el valor a `data/ai/weights.json` como peso con nombre (RT-096: los pesos son datos), con su
   entrada de esquema.
4. ADR de la decisión con las cifras de §7 y §11 (RT-057), incluida la subida de entradas 12,80 → 13,71.
5. Puertas en verde en una sola invocación, `--full-runs` y RT-024.

Verificación del estado final: `Sim/Engine/Utility.cs` y `Sim/Engine/MatchEngine.cs` restaurados y ausentes
de `git status`; `data/sim/tuning.json` idéntico byte a byte a la copia inicial; instrumento
`Sim.Tests/Analysis/_A5.cs` borrado; el lote de 1.000 partidos con semilla 1 reproduce exactamente
`tacklesPerMatch=12,80 · injuriesPerMatch=0,87 · possessionChanges=20,13 · passChainAvgLength=2,03 ·
shotsPerMatch=7,36 · goalsPerMatch=2,31`.

---

## 15. Por qué ese cambio mejora la IA

**La situación.** Un rival lleva el balón y está a menos de una casilla de un defensa nuestro. La entrada es
legal, el contexto de `Tackle` lo sabe y le da el mejor bono de la tabla. Pero `ChaseBall` sigue siendo legal
—su única precondición es ser el más cercano al balón, y el defensa que tiene al poseedor pegado **lo es**— y
parte de una base mucho más alta. La fórmula multiplica la táctica y **suma** el contexto, así que una base
grande sobrevive a un contexto que la está penalizando. Resultado: el jugador **corre hacia un balón que ya
podría disputar**. Es el fallo que el revisor describió como «jugadores atrapados en `ChaseBall` cuando ya
deberían disputar el balón», y está confirmado desde la auditoría 4.

**Qué corrige la penalización.** Añade la precondición que faltaba, en forma blanda y en el único sitio
donde falta: perseguir deja de puntuar cuando el balón ya está al alcance. No cambia lo que `ChaseBall` vale
en ninguna otra situación, no toca `Tackle`, y **solo se aplica si la entrada es de verdad una opción para
ese jugador** —`TackleCooldown == 0`—, porque quitarle `ChaseBall` a alguien que no puede entrar solo lo
volvería pasivo, que es el fallo simétrico y estaba explícitamente prohibido.

**Por qué eso es corrección de decisión y no un ajuste de agresividad.** Lo que se mueve no es la cantidad
de entradas en abstracto: es **qué elige el jugador cuando la oportunidad existe**. `ChaseBall` dentro de la
oportunidad cae de 32 % a 26 % y `Tackle` sube de 20 % a 23,5 %, **con las mismas oportunidades**. El jugador
no busca más contacto; deja de desperdiciar el que ya tiene delante. Las entradas por partido suben 0,9 como
**consecuencia** de decidir mejor, no como objetivo, y se quedan dentro de banda en las dos semillas — con
las lesiones **por debajo** del baseline en la semilla 1.

**Por qué 50 y no más.** Porque la corrección es continua y el guardarraíl no. Cada punto de penalización
compra aprovechamiento a un ritmo estable, pero a partir de 75 las entradas cruzan 14 y a partir de 100 las
lesiones cruzan 0,90. 50 es el punto donde la causa está atacada y ningún guardarraíl está tocado: la
intervención mínima que corrige el fallo sin convertir al jugador en una máquina de tacklear.

**Por qué no menos.** Porque 25 es peor que no hacer nada: iguala `ChaseBall` y `Tackle` en vez de
resolverlas, y un empate lo decide el ruido de posición tick a tick. La indecisión es peor que la decisión
equivocada, y está medida (§7).
