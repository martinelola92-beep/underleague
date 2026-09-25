# CAT-J — Tres métricas de `BuildGateTests` fuera de rango: **no es ruido de semilla**

**Estado:** Abierta. Medida la dispersión entre semillas (19 sep 2026): las tres fallan en la **mayoría**
de semillas, así que son señales de balance reales, no falsos positivos de muestreo. Sin investigar la
causa todavía. **No se ha tocado ningún rango.**

**Al día (25 sep 2026):** entra una cuarta métrica, `coherentBuildsBeatNone_orc_violence`, que venía mal
atribuida a la conducción — ver la sección del final. Y la puerta ya promedia **ocho semillas** desde la
ADR 0131, así que el punto 2 de la decisión de abajo («no se aplica el arreglo de ocho semillas») quedó
superado por esa ADR: lo que sigue vigente es el punto 1, **no se toca ningún rango**.

## Qué falla HOY

```
buildsWinDifferently_injuries      = 1.26   fuera de 1.40..-
badBuildsLoseToNone_elf_brawler    = 48.96  fuera de 10.00..45.00
badBuildsLoseToNone_elf_out_of_zone = 45.83 fuera de 10.00..45.00
```

`NoGateMetricIsOutOfRange` falla como consecuencia (agrega las anteriores), de ahí que las puertas rojas
sean tres tests y no tres métricas distintas.

**Corrección de las versiones anteriores de esta ficha**: decían que las métricas rojas eran
`elf_brawler` y `buildsWinDifferently_passChain`. Eso quedó **obsoleto**: `passChain` está hoy **dentro de
rango** (1,15 con la semilla de la puerta, 1,14 de media sobre ocho, 0 de 8 fuera), y el fallo real de
`buildsWinDifferently` es **`injuries`**, no `passChain`. Además hay una tercera métrica —
`elf_out_of_zone`— que ninguna versión anterior mencionaba.

## La hipótesis de ruido: REJECTED

Tras la ADR 0118 —que arregló la puerta de equipamiento demostrando que salía roja por medir con una sola
semilla— la hipótesis natural era que CAT-J fuera lo mismo. `BuildGateTests` también mide con **una sola
semilla** (`Seed = 1`, 480 partidos por celda) y su propia documentación declara un error típico de
**2,3 puntos** para una tasa de victoria.

**Medido sobre ocho semillas** (`Sim.Tests/Analysis/CatJSeedDispersionTests.cs`, `Skip` por coste,
~1 m 45 s):

| métrica | rango | valores por semilla 1..8 | media | sd | fuera |
|---|---|---|---|---|---|
| `buildsWinDifferently_injuries` | ≥1,40 | 1,26 · 1,07 · 1,00 · 1,06 · 1,19 · 1,43 · 1,29 · 1,33 | **1,20** | 0,15 | **7/8** |
| `badBuildsLoseToNone_elf_brawler` | ≤45,00 | 48,96 · 44,58 · 45,00 · 46,04 · 44,38 · 45,62 · 45,83 · 50,83 | **46,41** | 2,29 | **5/8** |
| `badBuildsLoseToNone_elf_out_of_zone` | ≤45,00 | 45,83 · 46,04 · 47,29 · 45,62 · 43,54 · 45,21 · 43,12 · 46,46 | **45,39** | 1,41 | **6/8** |
| `buildsWinDifferently_passChain` | ≥1,11 | 1,15 · 1,15 · 1,14 · 1,14 · 1,13 · 1,12 · 1,14 · 1,11 | 1,14 | 0,01 | **0/8** |

**Conclusión: no es ruido.** Las tres fallan en la mayoría de las semillas, y la peor no es marginal:

- **`injuries` es el fallo grande y claro.** Media 1,20 contra un mínimo de 1,40, con error típico de la
  media 0,05: está a **cuatro errores típicos** por debajo del rango. No hay ambigüedad. Es la única de
  las tres que merece el nombre de "problema de balance" sin matices.
- **`elf_brawler` es real pero marginal**: media 46,41 contra un tope de 45,00, a 1,7 errores típicos.
- **`elf_out_of_zone` está prácticamente EN el límite**: media 45,39 contra 45,00, a 0,8 errores típicos.
  Falla 6 de 8 veces por márgenes minúsculos. Es el caso más discutible de los tres.

## Decisión tomada (19 sep 2026)

1. **No se toca ningún rango.** Son señales reales; ajustarlas para que pasen sería exactamente lo que
   `balance-measure` prohíbe.
2. **No se aplica el arreglo de ocho semillas a `BuildGateTests`.** Con la ADR 0118 recién hecha era la
   tentación obvia, pero los datos dicen que aquí no hace falta: estas métricas **no cambian de lado con
   la semilla**, así que promediar no cambiaría ningún veredicto. Y costaría ~4 minutos sobre unas puertas
   que ya están en 7 m 05 s de un presupuesto de 8. Se aplica el patrón donde la medición lo justifica, no
   por simetría.
3. **Se deja el gancho**: `BuildGateTests.MetricsWithSeed(seed)` queda disponible (la puerta con
   `Seed = 1` devuelve exactamente lo de siempre, verificado: `elf_brawler=48,96` antes y después) y el
   test de dispersión queda en el árbol con `Skip`, para quien retome la causa.

## Lo siguiente, cuando se retome

**Empezar por `injuries`, y solo por eso.** Es el único fallo sin ambigüedad estadística, y el más
informativo: `buildsWinDifferently_injuries` mide que una build **física** se lleve más lesiones que una
**técnica** (`orc_violence` contra `elf_tiki_taka`, ver `BuildMetrics`). Un 1,20 significa que la build
física apenas provoca un 20 % más de lesiones que la técnica, cuando se le exige un 40 %. Es decir: **las
dos formas de jugar se distinguen menos de lo que el diseño pide**, que es justo lo que esta familia de
puertas existe para vigilar (RT-055).

Los otros dos (`elf_brawler`, `elf_out_of_zone`) son builds MALAS que no pierden lo suficiente: rozan el
tope en vez de rebasarlo con claridad, y conviene mirarlos **después** de `injuries`, porque si la
diferenciación física/técnica está aplanada es plausible que sea la misma causa.

## Hermanos

- ADR 0118 / `BB-T` — la puerta que SÍ era ruido de semilla, y el método con el que se midió ésta.
- `BB-P` — "las puertas de un solo partido/semilla se leen como causa cuando son ruido". Esta ficha es el
  caso contrario y conviene que conste: **también se puede descartar ruido con el mismo método**.

---

## Entra una cuarta métrica: `coherentBuildsBeatNone_orc_violence` (25 sep 2026)

Llega desde [BI-D](./BI-D.md), donde estaba **mal atribuida**: la ADR 0137 (conducción con duración) la dio
por rota por la conducción. Medida en tres dosis, mismo árbol, mismas ocho semillas, contraste pareado:

| `driveTicks` | 0 | 12 (enviado) | 18 |
|---|---|---|---|
| `coherentBuildsBeatNone_orc_violence` | 57,50 | 57,24 | 57,53 |

Δ(0→12) = **−0,26 ± 1,00**, Δ(0→18) = **+0,03 ± 1,0**. **LIKELY que no sea de la conducción** — dicho con
precisión: el contraste **no tiene potencia** para rechazar el «~1 punto» que la ADR afirmaba; lo que
sostiene la conclusión es que el efecto es **plano en la dosis** cuando el mecanismo propuesto exigiría que
creciera.

**Ojo al entrar**: las tres métricas originales de esta ficha se validaron con `CatJSeedDispersionTests`.
Ésta entra con su dispersión medida (sd 2,4-2,7 entre semillas, **ET 0,84-0,94**), y con eso **no está
establecidamente roja**: 57,50 contra un mínimo de 58 son **0,5 ET**. Se mide como OUT porque la puerta
juzga la media. Es, de las cuatro, la más frágil.

**Lo que sí está REJECTED, y con un tratamiento grande**: que el canal sea el **volumen de contacto**. La
ADR 0147 subió las faltas un 68 % (4,73 → 7,96 por partido) y las lesiones de 0,55 a 0,82, y la celda no se
movió (57,16 → 57,24). Es el argumento fuerte del expediente, más que cualquier contraste de dosis.

Pista para quien la retome: la build es un **motor de acumulación con disparador**, no de fuerza bruta.
Cinco `bruised_knuckles` (se dispara con `FOUL`, +30 de `injure` por falta acumulada, tope ×3), un
`scar_tissue` (con `INJURY`) y tres `brute_boots`. Si el volumen de faltas no la mueve, lo siguiente es
medir si esos contadores **llegan a acumular** dentro de un partido, o si el tope ×3 se alcanza tan tarde
que el partido ya está decidido.
