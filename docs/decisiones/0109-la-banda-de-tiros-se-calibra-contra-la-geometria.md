# 0109 — La banda de tiros se calibra contra la geometría vigente

Estado: **Aceptada** (decisión del revisor, 14 sep 2026). Sustituye el rango de tiros de `balance.md`
fijado en la fase 0.

## Contexto

El campo pasó de seis a **siete filas** (decisión del revisor, sucesora de la ADR 0103): con seis, el centro
geométrico cae entre la fila 2 y la 3 y no existe fila central, así que el portero, el delantero único y el
mediocentro quedaban media casilla descentrados. Con siete el número es impar y vuelve a haber una única
fila central.

Medido sobre 1.000 partidos y dos semillas, con el mismo commit de código salvo el trabajo de siete filas:

| métrica | 6 filas s1 | 6 filas s2 | 7 filas s1 | 7 filas s2 |
|---|---|---|---|---|
| `shotsPerMatch` | 8,40 | 8,22 | **7,36** | **7,25** |
| `goalsPerMatch` | 2,77 | 2,60 | 2,31 | 2,17 |
| `passChainAvgLength` | 2,11 | 2,19 | 2,03 | 2,10 |

**Añadir la fila cuesta ~1,0 tiro y ~0,45 goles por partido, en las dos semillas.** Es aritmética de
reparto: los mismos siete jugadores sobre un campo un 17 % más alto llegan menos.

Con la banda heredada de 8-16, `ShotsPerMatchAreInRange` quedaba en rojo de forma permanente.

## Palancas descartadas

Todas medidas con 1.000 partidos, semilla 1, una variable por medición (`docs/cierre-siete-filas.md` §3):

- **Pesos de tiro** (`shootAnglePenaltyPerRow` 36-42, `shootBaseRangeCells` 8-10): todo el barrido vive
  entre 7,23 y 7,47. **No es palanca**: el tiro no está limitado por la decisión de tirar, sino por llegar.
- **Velocidad** (`baseCellsPerTickMilli` 131 → 160): los tiros **bajan** a 7,04, la cadena a 1,85 (suelo 2)
  y las lesiones suben a 1,07 (techo 0,90). La velocidad la aprovecha antes quien defiende.
- **Zonas de acción** (`actionZone.shape.*.sides` ×1,17): es la única que recupera los tiros (8,22) y no se
  puede usar. Mata `diagonal_press` (activación 0,00), la build mala `elf_out_of_zone` deja de perder y las
  coherentes caen de 58 a 54,6. **El tamaño de la zona es el juego de colocación.**
- **Tamaño de portería**: no existe como geometría; el «a puerta» es una tirada de probabilidad.

## Decisión

1. **`shotsPerMatch` pasa de 8-16 a 7-15** (RT-057). El cambio es de la banda, no del motor: la lógica de
   disparo no se toca para compensar.
2. **La formación por defecto NO se ensancha.** Ver «Lo que se probó y se descartó».
3. No se compensa el déficit con velocidad, zonas de acción ni pesos de tiro.

## Reglas de diseño que quedan registradas

> **La geometría del campo forma parte del modelo de gameplay.** Las métricas de experiencia se calibran
> contra la geometría vigente; no se modifican los pesos de IA, la velocidad o las zonas de acción
> únicamente para devolver una métrica histórica a su banda anterior.

> **Los perks del eje de colocación están acoplados a la formación por defecto.** Las relaciones de
> `LinkGeometry` exigen diferencia de fila de exactamente ±1, y los perks de ese eje viven en slots
> concretos de las builds de referencia. Cualquier cambio de la formación por defecto redistribuye qué
> relaciones existen y **para qué roles**, así que se valida con las 43 puertas —no solo con las métricas de
> sensación— antes de proponerlo.

## Lo que se probó y se descartó: ensanchar la formación

El informe `cierre-siete-filas.md` recomendaba, además de la banda, ensanchar la formación: la de siete
filas ocupaba solo las filas 2-4 de siete (el 43 % del ancho) cuando la de seis ocupaba el 67 %. **Esa
recomendación no sobrevivió a la validación y se retira.** Se probaron cuatro formas, todas con las 43
puertas y dos semillas:

| forma | tiros s1/s2 | tercio | qué rompe |
|---|---|---|---|
| **original** GK(0,3) DEF(2,2)(2,4) MID(3,3)(4,2)(4,4) FWD(6,3) | 7,36 / 7,25 | 47,1 / 48,0 | **nada — 4 rojas, las tres regresiones conocidas** |
| A — defensas a 1/5 | 7,50 / 8,06 | 50,7 / 50,6 | `diagonal_press` muerto **y** 4 celdas de la curva de jefes de la ADR 0033 |
| B — rombo, medios (3,2)(3,4)(4,3) | 7,65 / 8,29 | **52,7 / 52,9** ❌ | techo del tercio, y `betterTeamWinRate` s1 a 59,0 |
| C — defensas 1/5, medios (3,2)(3,4)(5,3) | 7,29 / 7,89 | 49,5 / 51,2 | lesiones **0,91** ❌, `betterTeamWinRate` s1 68,1 |
| D — interiores a 1/5 | 7,28 / 7,02 | 47,3 / 47,9 | `gentle_giant` muerto, cadena **2,00** ❌ |

La causa es la del segundo recuadro de reglas: **la formación original tiene la cobertura de relaciones más
rica de todas las candidatas** —Ahead 4 pares, Behind 4, Left 8, Right 8, DiagonalAhead 4, DiagonalBehind 4,
con diagonal para cinco de los siete jugadores— y las builds de referencia están escritas contra ella.
`diagonal_press` ocupa los **slots 1 y 2, que son los dos defensas**: cualquier forma que deje a los
defensas sin pareja en diagonal lo mata, y `noDeadPerks` es puerta de fase 1.

El argumento del 43 % era correcto como observación y equivocado como recomendación: se apoyaba en una sola
métrica (`ballThirdMaxShare`) medida en una sola variante, sin pasar las puertas. **Ninguna de las cuatro
formas ensanchadas mejora el estado de las puertas, y tres de ellas sacan de banda una métrica de
sensación.**

## Consecuencias

- Con la banda nueva y sin tocar la formación: **689/689 unitarios en verde**, **4 puertas en rojo**, que
  son las tres regresiones de las siete filas pendientes de la fase B (`elf_brawler` 50,62;
  `buildsWinDifferently_passChain` 1,09; `EquippingAGoodBuild` 1,9 puntos) más su agregador. Sin perks
  muertos, curva de jefes en verde, RT-024 en verde.
- `--full-runs 240`: `runWinRate` 19,58 / 22,92, `deathsPerRun` 1,56 / 1,63 (banda 1,5-3).
- El techo baja de 16 a 15 para conservar la anchura de la banda; nunca se ha medido nada por encima de 8,4,
  así que el techo no muerde.
- `betterTeamWinRate` sigue fuera de banda por arriba en la semilla 2 (92,17; techo 90) **con las dos
  geometrías**: es un problema de calibración del instrumento, anterior a las siete filas y ajeno a esta
  decisión. La propia `balance.md` avisa de que su muestra efectiva son las semillas, no los partidos.
