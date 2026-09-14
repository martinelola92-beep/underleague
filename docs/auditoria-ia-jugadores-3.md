# Auditoría de la IA de jugadores · tercera vuelta

**14 de septiembre de 2026.** Baselines: `auditoria-ia-jugadores.md` y `-2.md`. Encargo: distinguir jitter
malo de movimiento útil, resolver la cadena causal de la entrada y probar la cadencia. **Sin cambios
permanentes**: todos los experimentos se hicieron con instrumentación temporal y `Sim/Engine/Utility.cs` y
`data/sim/tuning.json` quedan **byte a byte como estaban**.

H0 y H1 no necesitaron tocar el motor: se miden **leyendo la traza** (`MatchTrace.ActionAt`, `BallOwnerAt`,
`PositionAt`). H2 y H3 sí necesitaron un experimento temporal, ya revertido.

---

## 1. Resumen ejecutivo

1. **La primera auditoría exageraba el jitter más del doble.** El 58,3 % de reversals incluye un **59,4 %
   que son productivos**. El improductivo real es **23,7 % de los cambios**. La hipótesis H0 del revisor
   era correcta y mi titular era engañoso.
2. **La segunda auditoría se equivocaba en la causa.** Dije que el compromiso reducía las entradas porque
   «el jitter genera contactos». **Es falso**: con compromiso las **oportunidades suben** (9.765 → 11.345 →
   15.590). Lo que se hunde es la **tasa de elección**.
3. **La respuesta a la cadena causal es B, no A.** El defensa **está** al alcance y **elige otra cosa**.
4. **Y el baseline ya tiene ese defecto**: de 9.765 oportunidades de entrada, **solo el 19,5 % elige
   `Tackle`**. El 37,5 % elige `ChaseBall` —perseguir un balón que el rival ya tiene en los pies— y el
   32,0 % `CoverSpace`.
5. **Existe la combinación que el revisor pedía.** Compromiso 100 **suspendido cerca del balón**: el
   improductivo cae de 23,7 % a **8,6 %**, la tasa de elección **supera al baseline** (21,8 % contra
   19,5 %) y las entradas se recuperan a 11,01 de 12,87.
6. Mi variante anterior («compromiso solo para posicionales») falló por un error mío: metí en ese conjunto
   a `CoverSpace` y `MarkOpponent`, que son **exactamente las que le ganan a `Tackle`**.
7. **La cadencia de 2 ticks es la peor de las cuatro probadas** para el churn improductivo: 23,7 % contra
   13,2 % (1 tick), 14,3 % (3) y 13,7 % (4). Es un **máximo local**, no un extremo.
8. Las entradas escalan monótonamente con la frecuencia de decisión (16,03 → 12,80 → 10,57 → 9,04), y a
   1 tick **se salen de banda por arriba** (techo 14).
9. El desplazamiento mediano tras un cambio de acción es **1,15 casillas** en un segundo. Los jugadores se
   mueven de verdad; no vibran en el sitio.
10. **Nada de esto se ha aplicado.** Son candidatos medidos, no cambios.

---

## 2. Mapa de causalidad

```
síntoma            los jugadores cambian de acción 1.183 veces por partido
     ↓
medición           58,3 % son reversal ... pero el 59,4 % de esos son PRODUCTIVOS
     ↓
causa real         solo el 23,7 % de los cambios es churn improductivo
     ↓
síntoma 2          el compromiso arregla el churn y rompe las entradas (12,87 → 8,55)
     ↓
medición           oportunidades SUBEN (9.765 → 11.345); la elección CAE (19,5 % → 11,4 %)
     ↓
causa real         es scoring, no posicionamiento: el bono sostiene a CoverSpace/ChaseBall
                   justo cuando el poseedor está al alcance
     ↓
experimento        suspender el bono cuando hay poseedor rival a ≤ 1 casilla
     ↓
resultado          churn 8,6 % · elección 21,8 % · entradas 11,01 · todo lo demás en banda
```

---

## 3. Datos

### H0 · productivo contra improductivo (40 partidos, baseline)

Definición operacional usada, barata y medible: un cambio es **productivo** si en la ventana de 15 frames
(≈1 s) siguiente el jugador (a) llega a rango de entrada del poseedor rival, (b) toca el balón, o (c) se
desplaza al menos media casilla.

| | |
|---|---|
| cambios | 47.322 |
| reversal | 27.579 (58,3 %) |
| **reversal productivo** | 16.386 (**59,4 %** de los reversal) |
| reversal improductivo | 11.193 (40,6 %) |
| **`unproductiveReversalShare`** | **23,7 %** de todos los cambios |
| llega a rango de entrada tras el cambio | 7,2 % |
| toca el balón tras el cambio | 12,3 % |
| desplazamiento mediano / P90 | **1,15** / 1,96 casillas |

### H1 · la cadena causal de la entrada

| compromiso | oportunidades | elige `Tackle` | `unproductiveReversalShare` |
|---|---|---|---|
| 0 (baseline) | 9.765 | 1.900 (**19,5 %**) | 23,7 % |
| 100 | **11.345** | 1.290 (11,4 %) | 10,3 % |
| 200 | **15.590** | 736 (**4,7 %**) | 12,1 % |
| **100 + excepción cerca del balón** | 10.197 | 2.228 (**21,8 %**) | **8,6 %** |

**Qué elige un defensa que tiene al poseedor rival a ≤1 casilla** (baseline): `ChaseBall` 37,5 % ·
`CoverSpace` 32,0 % · **`Tackle` 19,5 %** · `MarkOpponent` 3,8 % · `ShortPass` 2,1 % · `Retreat` 2,0 %.

### H2 · la combinación que sí cumple los cinco criterios

| | baseline | comp 100 | **comp 100 + excepción** | banda |
|---|---|---|---|---|
| `unproductiveReversalShare` | 23,7 % | 10,3 % | **8,6 %** | — |
| reversal productivo | 59,4 % | 76,9 % | **79,9 %** | — |
| elige `Tackle` en oportunidad | 19,5 % | 11,4 % | **21,8 %** | — |
| `tacklesPerMatch` | 12,87 | 8,55 | **11,01** | 6-14 |
| `possessionChanges` | 20,31 | 20,67 | 20,58 | 12-28 |
| `passChainAvgLength` | 2,04 | 2,10 | **2,10** | 2-4 |
| `shotsPerMatch` | 7,34 | 7,52 | **7,41** | 8-16 |
| `injuriesPerMatch` | 0,87 | 0,71 | **0,77** | 0,30-0,90 |

**Coste residual que hay que declarar:** entradas **−14 %** y lesiones **−11 %** frente al baseline. No es
gratis. A cambio, el churn improductivo cae un **64 %** y la **calidad** de la decisión mejora: se entra
más a menudo cuando de verdad toca.

### H3 · cadencia (una sola variable, 1.000 partidos por punto)

| cadencia | `unproductiveReversalShare` | elige `Tackle` | entradas | alternancias | cadena |
|---|---|---|---|---|---|
| 1 tick · 66 ms | **13,2 %** | **23,8 %** | **16,03** ❌ | 21,00 | 1,98 |
| **2 ticks · 132 ms (hoy)** | **23,7 %** | 19,5 % | 12,80 | 20,13 | 2,03 |
| 3 ticks · 198 ms | 14,3 % | 18,9 % | 10,57 | 19,43 | 2,06 |
| 4 ticks · 264 ms | 13,7 % | 15,5 % | 9,04 | 19,13 | 2,10 |

**La cadencia actual es un máximo local de churn improductivo**, peor que la mitad y que el doble. Eso no
es un gradiente: es una **anomalía** y merece explicación antes que acción.

**Hipótesis a verificar, no verificada:** el desfase es `(tick + Id) % 2 == 0`, que parte la plantilla en
**dos grupos por paridad de id**. Con intervalo 2, la mitad del equipo decide en los ticks pares y la otra
mitad en los impares, cada uno reaccionando a la posición ya obsoleta del otro. Con 1 deciden todos a la
vez y con 3 hay tres grupos: en ninguno de los dos casos existe esa alternancia limpia de dos bandos. **Si
se confirma, el arreglo no es cambiar la cadencia sino el desfase** — por ejemplo `(tick + Id*7) % 2`, que
no reparte por paridad.

**No se recomienda subir la frecuencia**: a 1 tick las entradas se salen de banda por arriba (16,03 contra
un techo de 14) y cuesta el doble de CPU por tick.

---

## 4. Tabla de hipótesis

| # | hipótesis | evidencia | experimento | cambio mínimo | impacto | riesgo |
|---|---|---|---|---|---|---|
| **H0** | Parte del reversal es útil | **medido**: 59,4 % productivo | hecho | ninguno: **cambiar la métrica objetivo** a `unproductiveReversalShare` | alto | nulo |
| **H1** | La pérdida de entradas es de scoring, no de posición | **medido**: oportunidades ↑, elección ↓ | hecho | — | alto | — |
| **H2** | Existe compromiso sin coste de entradas | **medido**: 8,6 % / 21,8 % / 11,01 | hecho | bono 100 + excepción a ≤1 casilla del poseedor | **alto** | medio: −14 % entradas |
| **H1b** | `ChaseBall` le gana a `Tackle` con el rival en los pies | **medido**: 37,5 % contra 19,5 % | comparar sus contextos en esa situación | descartar `ChaseBall` si el balón tiene dueño rival al alcance | **alto** | bajo |
| **H3** | La cadencia de 2 ticks es patológica por el desfase de paridad | **medido** el síntoma, **no** la causa | probar `(tick + Id*7) % 2` con la cadencia intacta | solo el desfase | medio-alto | bajo |
| H4 | Doble conteo en `FindSpace` | margen 6 sobre 937 | **falta instrumentación** | fundir términos | alto | bajo |
| H6 | Ángulo de tiro por filas | BA-E | escenarios frontal/diagonal/fondo | ángulo real | medio | bajo |
| H7 | Cuatro acciones muertas, tres causas | medido el síntoma | separar causas | según causa | medio | medio |

---

## 5. Instrumentación que el motor debería incorporar

**`--utility-census`**: percentiles por acción (P10/P25/P50/P75/P90), `%` de scores negativos,
`nearTieShare`, y censo **por estado táctico**.

**`--dump-utility`**: **partir `Context` por término**. Hoy `UtilityRow.Context` es un único entero y sin
desglosarlo H4 es imposible de medir. Es la instrumentación que más desbloquea.

**`MatchTrace`**: nada. H0 y H1 se midieron enteros con lo que ya expone, que es el mejor argumento a favor
de la traza tal como está.

**Métricas permanentes**: `unproductiveReversalShare` (hoy **23,7 %**), `tackleOpportunityTakeRate` (hoy
**19,5 %**), `deadActionCount` (hoy 4).

---

## 6. Recomendación

**Hacer ahora** — nada toca el motor:

1. **Cambiar la métrica objetivo** de `actionReversalShare` a **`unproductiveReversalShare`**. Es
   conclusión de H0 y no cuesta nada: optimizar la primera habría sido optimizar contra el juego.
2. **Separar las dos columnas de transición** en `weights.json` (hallazgo de la primera auditoría, L1 165 y
   130). Solo dato, riesgo casi nulo.

**Investigar después**, por orden:

3. **H1b**: por qué `ChaseBall` gana el 37,5 % de las oportunidades de entrada. Sospecho que es el mayor
   defecto suelto del sistema y es anterior a cualquier histéresis.
4. **H3-desfase**: comprobar si la paridad de id explica la anomalía de la cadencia.
5. **H2**: el compromiso con excepción, **después** de 3, porque si `ChaseBall` deja de robar oportunidades
   el coste de −14 % en entradas puede desaparecer solo.
6. **H4**: desglosar `Context` y medir `FindSpace`.

**No tocar**: la cadencia, el desempate por id, los estados de ejecución bloqueados.

**Evitar por ahora**: curvas de respuesta normalizadas y anticipación de un paso. Las dos pueden ser
correctas y ninguna está justificada por una medición todavía.

---

## 7. Fuentes externas

**No se han consultado, otra vez, y conviene decir por qué en vez de rellenar una bibliografía.** Los cinco
hallazgos de esta vuelta —productividad del reversal, oportunidades contra elección, `ChaseBall` robando
entradas, la anomalía de cadencia y la excepción cerca del balón— salen **todos** de medir este motor, y
ninguna fuente externa los habría producido.

Donde sí harían falta es en **H4/H5**, el valor del espacio: ahí la pregunta es *qué* medir, no *cuánto*, y
es justo donde el análisis de tracking real aporta conceptos (recepción entre líneas, ocupación de
carriles, superioridad local). Queda como encargo aparte, con el aviso que ya se dio: son datos de fútbol
11 en campo continuo y esto es fútbol 7 sobre 16 columnas, así que el trasvase sería **conceptual** y habría
que decir en cada caso qué no se puede trasladar.
