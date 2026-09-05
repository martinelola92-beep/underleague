# 0035. La escala de valores de un perk depende del canal, no es única

**Fecha:** 2026-09-05
**Estado:** **Retirada** por la ADR 0050 P1 (6 sep 2026), al implementarse los perks multiplicativos sobre cuotas: con cuotas un perk vale lo mismo en cualquier canal por construcción, así que la tabla de escalones deja de tener función y `tuning.probabilityChannels` desaparece de `data/sim/tuning.json`. El diagnóstico de esta ADR sigue siendo correcto y es el que motivó la P1
**Modifica:** `docs/fase1b-diseno.md` §1.4 y `docs/estilo-descripciones.md`
**Requisitos:** RF-065, RF-069, RT-033, RT-057
**Resuelve:** D-30, y la causa de fondo de D-35

## Contexto

Los valores de los perks se declaran en **puntos porcentuales absolutos** con la escala `5, 10, 15, 20, 25, 50`. La idea era que una escala corta obligara a que cada perk tuviera un tamaño reconocible y el balanceo fuera comprensible.

El problema es que **un punto porcentual no vale lo mismo en todos los canales**, porque las bases son de órdenes de magnitud distintos:

| Canal | Base | Qué hace un `+5` |
|---|---|---|
| `intercept` | 250 (2,5%) | **triplica** la probabilidad |
| `injure` | 40 (0,4%) | la multiplica por **trece** |
| `foul` | 320 | la duplica y media |
| `tackle` | 2.800 | +18% relativo |
| `save` | 5.000 | +10% relativo |
| `pass` | 7.700 | +6,5% relativo, y encima cerca del techo |

Es decir: la misma cifra escrita en dos perks distintos produce efectos que se diferencian en dos órdenes de magnitud. El diseñador escribe `5` creyendo que pone un efecto pequeño y según el canal está poniendo un interruptor.

Esto ha causado dos problemas reales, medidos:

- **La habilidad racial de los elfos valía +10,4 puntos** de tasa de victoria, cuatro veces el presupuesto de una habilidad racial. Su mitad de intercepción valía **ella sola +6,6**, no por estar mal ajustada sino porque el valor legal más pequeño de la escala ya era enorme en ese canal.
- **Una build de prueba diseñada para ser mala gana el 54%** porque lleva tres copias de un perk de `+5` a `intercept`: multiplica la probabilidad por siete. La build no era mala, llevaba un arma sin querer.

## Decisión

**Cada canal declara su propio escalón**, y los valores de los perks son múltiplos de ese escalón. El escalón se fija de modo que un paso valga aproximadamente lo mismo en todos los canales, en términos de impacto relativo sobre su base.

En `data/sim/tuning.json`, junto a cada canal de probabilidad: `step` en puntos porcentuales, y la escala permitida pasa a ser `1, 2, 3, 5, 10` **pasos**. El validador rechaza cualquier valor que no sea un múltiplo del escalón de su canal.

Orden de magnitud de partida, a calibrar: `intercept` 1 · `injure` 1 · `foul` 1 · `card` 1 · `tackle` 3 · `dribble` 5 · `save` 5 · `pass` 5 · `shotOnTarget` 5.

**Las descripciones siguen mostrando el valor absoluto** (`estilo-descripciones.md` no cambia): al jugador se le dice "+3% de probabilidad de interceptar", que es verdad y es verificable. Lo que cambia es lo que el **diseñador** puede escribir.

## Alternativas descartadas

- **Valores relativos** ("+50% de la base"): más fiel al impacto, pero la descripción se vuelve ambigua para el jugador —"un 50% más de probabilidad" se confunde con "50 puntos más"— y ya fijamos en `estilo-descripciones.md` que eso es la vía rápida a una descripción que miente.
- **Normalizar las bases** subiendo `intercept`, `injure` y `foul` hasta que la escala única funcione: son constantes de simulación calibradas contra RT-056; moverlas rehace el balance del partido entero para arreglar un problema de la capa de perks.
- **Dejarlo y confiar en el criterio del diseñador**: es lo que había, y ha producido dos fallos de balance que costaron dos paquetes de trabajo detectar.

## Consecuencias

- El validador gana la comprobación por canal; los perks del catálogo actual que violen su escalón hay que reescribirlos (los de `intercept` e `injure` sobre todo, que son los que hoy están inflados).
- Se espera que **bajen** los valores de los perks de intercepción y lesión, y que suban los de pase y parada. Hay que revalidar RT-056 y la puerta de fase 1 después.
- `docs/perks-ejes.md` y `fase1b-diseno.md` §1.4 se actualizan con la tabla de escalones.
