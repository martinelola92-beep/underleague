# Auditoría de la IA de jugadores · segunda vuelta

**14 de septiembre de 2026.** Baseline: `docs/auditoria-ia-jugadores.md`. Encargo: hipótesis **medibles**,
sin reemplazar la arquitectura y **sin dejar código cambiado**.

**Lo que se ha hecho:** el experimento del punto 7 completo, con su barrido y su control de seguridad. El
código experimental está **revertido**; el baseline sigue intacto.

**Lo que NO se ha hecho, y se dice:** los puntos 1 (distribuciones completas), 2 (descomposición de
`FindSpace`), 3-6 y 8-10 quedan como **hipótesis con su instrumentación especificada**, no medidos. Y no se
ha consultado ninguna fuente externa (Game AI Pro, StatsBomb, Metrica, Friends of Tracking): lo diré otra
vez para no atribuir a la literatura lo que es medición propia.

---

## 1. El resultado que cambia la recomendación de la primera auditoría

La primera auditoría proponía la histéresis como **cambio mínimo número uno**. Medida, hay que matizarla
bastante.

### La curva (40 partidos, misma semilla, un solo cambio)

| bono | cambios | **reversal** | cambio/decisión | duración mediana | `Ret↔Cov` | `Cov↔Find` |
|---|---|---|---|---|---|---|
| **0** | 47.323 | **58,3 %** | 6,3 % | 5 ticks | **40,5 %** | 17,0 % |
| 25 | 37.050 | 46,1 % | 4,8 % | 8 | 25,0 % | 23,9 % |
| 50 | 34.495 | 44,6 % | 4,3 % | 10 | 21,2 % | 26,1 % |
| 75 | 32.607 | 44,7 % | 4,1 % | 10 | 19,9 % | 27,1 % |
| **100** | 29.041 | **44,5 %** | 3,7 % | 12 | **17,2 %** | 29,5 % |
| 150 | 29.205 | 45,9 % | 3,7 % | 12 | 16,7 % | 31,2 % |
| 200 | 25.975 | **48,0 %** | 3,4 % | 12 | 17,8 % | 31,1 % |

**Tres lecturas:**

**(a) Hay un óptimo interior en 100, no una rampa.** El porcentaje de ida y vuelta baja hasta 44,5 % y
luego **vuelve a subir** (150 → 45,9 %, 200 → 48,0 %). Más compromiso del necesario no estabiliza: cambia
menos veces, pero una fracción mayor de esos cambios son desdecirse.

**(b) La histéresis sola no puede arreglar el jitter.** El reversal **nunca baja de 44,5 %**. Ese suelo es
estructural y el candidato obvio es el hallazgo 2 de la primera auditoría: `FindSpace` gana por 6 puntos.

**(c) El problema no desaparece: se muda.** `Retreat↔CoverSpace` cae de 40,5 % a 17,2 % —el par dominante
queda domado— pero `CoverSpace↔FindSpace` **sube de 17,0 % a 29,5 %**. Al quitar el ruido de alrededor,
queda a la vista el empate real.

### El control de seguridad, que es donde se rompe

| bono | alternancias | **entradas** | cadena | tiros | lesiones |
|---|---|---|---|---|---|
| 0 | 20,31 | **12,87** | 2,04 | 7,34 | 0,87 |
| 100 | 20,67 | **8,55** | 2,10 | 7,52 | 0,71 |
| 200 | 20,71 | **6,04** | 2,18 | 7,56 | **0,53** |

**Las entradas caen un 34 % con bono 100** y tocan el suelo de banda (6,00) con 200. Las lesiones caen con
ellas, de 0,87 a 0,53.

**Primera hipótesis, descartada con datos.** Pensé que el bono plano estaba ganándole a `Tackle`
directamente, porque `Tackle` es un acantilado. Probé la variante que lo arregla —compromiso **solo** para
acciones posicionales (`CoverSpace`, `FindSpace`, `Retreat`, `MarkOpponent`, `OfferSupport`)— y apenas
cambia: **8,55 → 9,11** entradas, todavía un 29 % por debajo.

### La conclusión incómoda

Si restringir el bono a las posicionales no recupera las entradas, el mecanismo no es de puntuación: es
**posicional**. Un jugador que se compromete **se queda donde está** en vez de reposicionarse, y por tanto
**se generan menos ocasiones de contacto**.

Dicho sin rodeos: **el jitter está haciendo trabajo.** El vaivén constante entre replegar y cubrir mueve
gente por el campo, y ese trasiego produce entradas. Las 12,87 entradas del baseline son, en parte, un
**artefacto de un defecto**.

Eso reordena todo: la histéresis no es un cambio barato y gratuito. Su coste se paga en la métrica de la
que depende la identidad del juego —la carnicería— y habría que **reponer las entradas por un mecanismo
real** (la entrada sin balón de la ADR 0105, presión, marcaje que de verdad se ejecute) antes o a la vez.

---

## 2. Tabla de hipótesis

| # | hipótesis | evidencia actual | qué medir | cambio mínimo | impacto | riesgo |
|---|---|---|---|---|---|---|
| H1 | El jitter se reduce con compromiso, **pero cuesta entradas** | **medido**: 58,3→44,5 % y 12,87→8,55 | reversal + `tacklesPerMatch` juntos | `commitmentBonus` 100, **solo tras reponer contacto** | alto y ambiguo | **alto**: toca la identidad |
| H2 | El suelo de 44,5 % lo pone el empate de `FindSpace` | margen medio **6** sobre 937 | descomponer `FindSpace` por término | fundir términos redundantes | alto | bajo |
| H3 | Varios términos de `FindSpace` miden lo mismo | no medido | correlación entre términos y con distancia a portería | fusión | alto | bajo |
| H4 | Las transiciones no existen como comportamiento | **medido**: L1 165 y 130 | censo por estado táctico | separar dos columnas de `weights.json` | medio-alto | **muy bajo**: es dato |
| H5 | `Shoot`/`Tackle` son acantilados por escala, no por precondición | **medido**: media −399 y −673 | percentiles P10-P90 por acción | ninguno todavía | medio | — |
| H6 | El ángulo de tiro por filas permite goles sin ángulo | BA-E + código | ángulo real vs filas en las mismas jugadas | sustituir el término | medio | bajo |
| H7 | Cuatro acciones muertas por **tres causas distintas** | **medido**: 0,00-0,26 % | separar descartada / pierde / no evaluada | según causa | medio | medio |
| H8 | Tiempo-al-punto supera a distancia | no medido | comparar en `FindSpace` y `MarkOpponent` | `timeToReach = distancia / velocidad` | medio | medio |
| H9 | Mirar 2-3 compañeros basta para parecer listo | no medido | términos locales vs globales | añadir `separación al compañero más cercano` | medio | bajo |
| H10 | Un paso de anticipación sustituye decenas de términos | no medido | `FutureValue` en `FindSpace` | prototipo medido | alto | **alto**: coste por tick |

---

## 3. Top 5 por impacto / esfuerzo / riesgo

1. **H4 · Separar las transiciones.** Es **solo dato**, riesgo casi nulo, y ataca algo que hoy no existe:
   el contragolpe y el repliegue tras pérdida. Debería ir primero justamente porque no toca código.
2. **H2+H3 · Descomponer `FindSpace`.** Es la causa del suelo de 44,5 % y del delantero que se va al
   fondo. **Instrumentar antes de tocar**: volcar cada término por separado y mirar correlaciones.
3. **H6 · Ángulo real de tiro.** Una función, defecto conocido y reproducible (BA-E), y `Vec2` ya es
   `float`, así que no hace falta aritmética entera escalada: RT-023 lo permite y el determinismo de este
   proyecto es «misma semilla, mismo binario».
4. **H7 · Las cuatro acciones muertas**, después de separar las tres causas. `Block` se descarta por
   precondición (CAT-F), `PressCarrier` por diseño, `OfferSupport` **compite y pierde siempre**: son tres
   arreglos distintos y hoy se confunden en un solo síntoma.
5. **H1 · Histéresis**, y **la última**, no la primera. La curva está medida y el valor es 100, pero no se
   toca hasta haber repuesto las entradas por un mecanismo real.

---

## 4. Instrumentación que falta

Para `--utility-census`:

- **Percentiles por acción** (P10/P25/P50/P75/P90) además de media y máximo, y **porcentaje de scores
  negativos**. Responde a H5 sin ambigüedad.
- **Distribución del margen**, no solo su media: `nearTieShare` = porcentaje de decisiones ganadas por
  menos del 2 % de la puntuación.
- **Censo por estado táctico**, para H4.

Para `--dump-utility`:

- **Desglose del Contexto por término** dentro de cada acción. Hoy `UtilityRow` trae `Context` como un
  único entero; H2 y H3 son imposibles de medir sin partirlo.

Permanentes, como test:

- `actionReversalShare` (hoy **58,3 %**), `nearTieShare`, `deadActionCount` (hoy **4**).

---

## 5. Protocolo de experimento

El que pide el revisor y que esta auditoría ya ha seguido:

1. baseline → **un solo cambio** → 40 partidos, misma semilla → métricas → aceptar o rechazar;
2. el baseline se conserva intacto;
3. un cambio no es mejora porque «se vea mejor»: tiene que mover una métrica declarada **antes**;
4. y todo cambio de decisión se mide **también** contra la red de RT-056 —`possessionChanges`,
   `tacklesPerMatch`, `passChainAvgLength`, `shotsPerMatch`, `injuriesPerMatch`—, que es exactamente lo que
   ha salvado a esta sesión de aceptar la histéresis a ciegas.

## 6. Sobre las fuentes, otra vez

No se ha abierto ninguna. Todo lo anterior sale de medir este motor. Si el revisor quiere la parte de
**valor del espacio** y **movimiento sin balón** apoyada en material de tracking real (StatsBomb Open Data,
Metrica, Friends of Tracking), hay que consultarlas de verdad y eso es un encargo aparte — pero conviene
avisar de que el aporte de esas fuentes sería **conceptual** (qué medir), no directamente aplicable: son
datos de fútbol 11 sobre un campo continuo, y este motor es fútbol 7 sobre una rejilla de 16 columnas.
