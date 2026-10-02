# ADR 0182 — La cola de los sumideros tiene techo medido, no un cero exacto

Fecha: 2 oct 2026 · Estado: **aceptada** (autónoma, dentro de la autorización del revisor para mover rangos con datos; RT-057)
Cierra el menor «puerta de cola de los sumideros» (ADR 0165, `project-state.md`). Requisitos: RF-114k, RT-057, RT-056.

## Por qué

`TheGoldOfAnActPaysTwoOrThreeSinksAndNeverAllOfThem` exigía `actsWithAllSinksAffordable` = 0 exacto: ningún acto de la
muestra de la puerta (240 runs contextuales, ~480 actos) podía pagar los cinco sumideros. La ADR 0165 la vio roja con
0,19 % (un acto) y la dejó como hallazgo; en `main` volvía a verde y rojo según qué reordenase las runs. RF-114k habla del
oro **medio** por acto («permite usar dos o tres sumideros, nunca todos»); el cero exacto vigilaba una cola, no la media.

## Hipótesis (Regla A) ordenadas por coste × poder discriminativo

1. **Ruido de cola con muestra pequeña** — una sola corrida más de la puerta con otras semillas; discrimina casi todo.
2. **Instrumento que mide otra cosa** — leer `SinksAffordable` (cuesta cero): usa solo oro ganado, partidos, victorias y
   mercados del acto; cuenta **cinco** costes (la ADR 0165 y el comentario de la puerta decían «cuatro»: el quinto es el hueco de
   plantilla, de la ADR 0046). No mide mal; el texto estaba desfasado.
3. **Cota mal puesta** (cero exacto sobre una cola) — consecuencia de 1 si la cola es estable y no nula.
4. **Cambio real de economía desde que se fijó** — la media (`sinksAffordablePerAct`) se mide en las mismas corridas.

## Medición (Regla F, varias semillas)

`dotnet run --project Balance -c Release --no-build -- --full-runs 240 --seed S` para S = 1, 1001, …, 11001 (12 semillas,
separadas ≥ 240 porque las runs usan `seed + i`), fila del resumen contextual, la misma población que la puerta:

| métrica | resultado |
|---|---|
| `sinksAffordablePerAct` | 2,28-2,40 (media ≈ 2,34, desviación entre semillas ≈ 0,03): dentro de 2-3, estable |
| `actsWithAllSinksAffordable` | 0,00 en 9 de 12 semillas; 0,21 en 2 (S=2001, 3001) y 0,40 en 1 (S=10001) |
| actos con los cinco pagados | 4 de ~5.700 = **0,07 %** (**CONFIRMED** la cola existe y es rara; la puerta de cero exacto sale roja en ~25-30 % de las muestras, lo que predice Poisson con λ ≈ 0,34 por muestra) |

- **Hipótesis 1 CONFIRMED**: la cola es real y estable (~0,07 %); la puerta de 0 exacto es una moneda de ~30 %.
- **Hipótesis 4 REJECTED bajo la economía actual**: la media está en banda en las 12 semillas y su dispersión es pequeña;
  la cola no crece. Tampoco hay problema real de economía: un acto suelto con muchas victorias y mercados que cabe en los
  cinco costes no contradice «el oro medio permite dos o tres». **No se abre ficha en `docs/pendientes/`.**
- Sin medir: qué acto concreto (acto 3 con ventas, racha de victorias) es el que cruza; no cambia la decisión.

## Decisión

La fila `actsWithAllSinksAffordable` pasa de `Info` a banda con **techo `AllSinksAffordableShareMax` = 1,0 %**
(`FullRunMetrics`), y la puerta usa `AssertIn` en vez de `Assert.Equal(0.0, …)`. La media (2-3) no se toca.

**Procedencia del 1,0 % (Regla H):** línea base medida 0,07 % por acto. La puerta ve ~480 actos, así que 1 acto = 0,21 % y
el techo admite **4-5 actos según el tamaño de la muestra** (P(≥5 | λ ≈ 0,34) ≈ 2·10⁻⁵ con la cola medida). **Provisional**: una sola población (contextual) y un solo catálogo.

**Sigue protegiendo (control, `AnEconomyThatTriplesTheGoldTripsTheSinksTail`):** con las mismas runs de la puerta y
`GoldEarnedByAct` × 3 (una economía que sobra), la fila de cola y la media `sinksAffordablePerAct` salen de banda. Y una
regresión real mueve la **media** antes que la cola, que sigue vigilada por 2-3.

Limitación honesta: un 1 % es sensible solo a cambios de ~10× en la cola; los pequeños los ve la media. El control exige
`OUT` (no «distinto de IN») con el oro ×1,5 para la cola y ×3 para la media (comprobado al correr el test; no se afirma
una sensibilidad que no se haya medido).

**Muestra pequeña.** Con `UNDERLEAGUE_GATE_SCALE` bajo hay ~50 actos y uno solo pesa ~2 %: el techo volvería a ser un cero.
Por eso la banda solo se aplica con `MinActsForTailBand` = 400 actos (suelo, no medición); por debajo la fila es
informativa y el control se salta en modo rápido.

## Alcance de «sumideros» y lectura de RF-114k (pase de `game-design-review`)

- Los cinco del modelo (`SinksAffordable`: mercado, clínica, hueco de plantilla, rerolls, salario de un mercenario **poco común**,
  que es lo que calcula el código; la rareza real se sortea al ofrecerlo) son un **subconjunto** de los de RF-114k: taller,
  sobornos y apuesta del vestuario no entran en el instrumento. «Todos» significa aquí los cinco medibles.
- Lectura de diseño: «el oro **medio** por acto permite dos o tres, nunca todos» se lee como una propiedad de la media, no
  de cada acto. Fantasía/decisión: el jugador elige a qué renunciar; un acto excepcional (racha de victorias y ventas) que
  lo paga todo ocurre ~1 vez en 1.400 y no deshace esa elección. Degeneración posible: una economía que sobra, que la
  media y el techo detectan (control). Interacción: ninguna mecánica nueva ni tocada; sólo cambia un instrumento. Riesgo
  residual: si el diseñador quisiera «nunca» como garantía por acto, haría falta un tope de oro, que es una mecánica y
  no una puerta; **decisión de diseño abierta para el revisor**, no tomada aquí.

## Relación con BB-U

[BB-U](../pendientes/BB-U.md) era esta misma puerta: un perk común la había puesto en 0,196 y `Bulwark` se quedó en 2 perks.
Su pregunta «¿cero absoluto demasiado frágil?» queda respondida: sí (cola 0,07 %, una moneda de ~30 % por muestra).

## Reproducible

```
for S in 1 1001 2001 3001 4001 5001 6001 7001 8001 9001 10001 11001; do
  dotnet run --project Balance -c Release --no-build -- --full-runs 240 --seed $S --out <dir>/s$S
done   # ~1 min por semilla; leer sinksAffordablePerAct y actsWithAllSinksAffordable de summary.csv
```
