# ADR 0150 — Un perk negativo se confirma antes de retirarlo

Fecha: 26 sep 2026 · Estado: **aceptada** (autónoma, dentro de la autorización del revisor para mover
rangos). **Enmienda una consecuencia de la [ADR 0087](0087-el-valor-de-un-perk-se-mide-contra-su-control.md).**
Resuelve el punto principal de [BL-B](../pendientes/BL-B.md), que sigue abierta por sus hermanos. Origen: [BL-A](../pendientes/BL-A.md).

## Por qué

La ADR 0087 dice que *«un valor por debajo de −`rowDeviation` es un perjuicio real»*. `rowDeviation` (7) es
la discrepancia **media** entre las dos semillas de la tabla. Pero la varianza no es la misma en todas las
filas: un perk que actúa pocas veces y mucho en cada una, como los de lesión o los de evento raro, se
mueve bastante más que la media.

- `ankle_bite`: −16 con las semillas 5 y 11, retirado en `0380220`. Con ocho semillas a 192 plantillas la
  fila tiene sd ≈ 18 por lote. Con 500 plantillas × 4 semillas vale +9,5. La retirada era ruido (BL-A).
- `bull_rush`: −8 con 5 y 11, y 0 con cuatro semillas.

Dos falsas alarmas en un día, y una de ellas acabó en una decisión de catálogo.

## Decisión

1. Con los dos lotes de la tabla, una fila por debajo de −`rowDeviation` es un **candidato** a perjuicio.
   `tools/perk-values-table.py` la lista en `toConfirm`.
2. Antes de retirar o rediseñar un candidato se confirma con **≥ 4 lotes nuevos**: semillas distintas
   **de las que lo marcaron** (5 y 11), porque esas se eligieron precisamente por salir bajas; el mismo
   número de plantillas en todos, **≤ 500** porque por encima el espacio de semillas del medidor colisiona;
   y `tools/perk-value-confirm.py`, que se niega ante lotes repetidos (misma curva al bit), lotes de más
   de 500 plantillas, tamaños mezclados o campañas más cortas que el horizonte. El ET se calcula **con esa
   fila**: sd entre lotes / √N. Si hay una estimación externa mejor de la sd de la fila (más lotes a menos
   plantillas), se pasa con `--sd-floor` y el ET usa la mayor de las dos.
3. Es **perjuicio** si la media < −`rowDeviation` **y** media + t·ET < 0, con t el cuantil 97,5 % de la
   t de Student con N−1 grados de libertad. El caso simétrico es positivo, y el resto es «sin efecto a
   esta potencia». Los veredictos son **LIKELY** (Regla F), nunca CONFIRMED: son estadísticos, no
   aíslan el mecanismo.
4. **Qué valor se escribe.** Mientras la fila es candidata, conserva el valor de la tabla y no se actúa.
   Tras la confirmación se escribe **la media de los lotes de confirmación, redondeada**, sea cual sea el
   veredicto, y la fila **declara su procedencia** en el `_doc` de `perk-values.json`, porque quien
   regenere la tabla la vería otra vez en `toConfirm`. Si el veredicto es perjuicio, además se retira o se
   rediseña.

**Procedencia de las cifras (Regla H)**: el ≥ 4 y el t al 97,5 % son **convención estadística, sin
medir**; el 4 es el mínimo con el que una sd empieza a significar algo (3 grados de libertad, t = 3,18). El
umbral −`rowDeviation` se mantiene como estaba en la ADR 0087.

## Validación del instrumento (Regla J)

Sobre casos cuya respuesta ya se conoce (lotes de BL-A):

| entrada | resultado | correcto |
|---|---|---|
| `ankle_bite`, semillas 5+11 a 192 (−16) | SIN VEREDICTO (< 4 lotes) | no se habría retirado |
| `ankle_bite`, 8 semillas a 192 | 4,9 ± 6,5, SIN EFECTO | coherente con la sd de la fila |
| `ankle_bite`, 4 semillas a 500 | 9,5 ± 2,2, POSITIVO (LIKELY) | **demasiado optimista**: la sd de 2,2 está en la cola baja con 3 grados de libertad |
| ídem con `--sd-floor 11.4` (sd 18,4 por lote a 192, escalada a 500) | 9,5 ± 5,7, SIN EFECTO | lo honesto: no es negativo, y positivo no está demostrado |
| `f5 f11 rev-192-5 rev-192-11` (dos semillas repetidas) | ERROR: misma curva al bit | antes daba −16,3, PERJUICIO |
| lotes de 768 plantillas (`blp-*`) | ERROR: > 500 plantillas | antes daba +13,9, POSITIVO |
| 500 y 192 plantillas mezcladas | ERROR: tamaños distintos | |
| `dirty_play`, 8 semillas a 192 | 0,2 ± 3,2, SIN EFECTO | coherente |

## Consecuencias

- Hoy no hay ningún candidato: el mínimo de la tabla es `mob_instigator` −7, que no está por debajo de −7.
- Coste: un lote de un perk a 192 plantillas tardó ~1 min por semilla en BL-A (medido por las marcas de
  tiempo de los lotes `bla-*`, con las puertas corriendo en paralelo).
- **Lo que no cambia hoy**: el valor y el peso de oferta (ADR 0038) de todas las filas, porque no hay
  candidatos. A partir de ahora, una fila confirmada cambia de valor, y con él su peso de oferta y el
  encogimiento de `RunPolicy.MeasuredValueFor`.
- **Queda abierto (hermanos en BL-B)**: los arneses de `Sim.Tests` que copian el espacio de semillas
  `*1000 + 500` no tienen guarda contra más de 500 plantillas; `perk-values.json` no tiene un campo de
  procedencia por fila que un validador pueda comprobar; el techo positivo (recorte por encima de +150 en
  la ADR 0087) usa la misma tabla de dos lotes; `RunPolicy.MeasuredValueFor` usa el `rowDeviation` global
  como sigma de todas las filas; y `item-values.json` tiene la misma estructura y no está cubierto.
