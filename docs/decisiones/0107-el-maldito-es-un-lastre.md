# ADR 0107 · El objeto maldito es un lastre por defecto

**Fecha:** 13 de septiembre de 2026 · **Estado:** aceptada, **con una métrica pendiente**
**Decisión del revisor** · **Toca:** ADR 0036, ADR 0038, RF-077, ADR 0078, el suelo del paquete AY

## El problema, medido

Los cuatro objetos malditos del catálogo **eran una ganga**, todos. Con la tabla de valor marginal de la
ADR 0038 (fuerza 111 · técnica 75 · velocidad 66 · correa 40 · resistencia 30):

| objeto | sube | baja | neto en puntos | **valor medido** |
|---|---|---|---|---|
| `berserker_totem` | +60 | −20 | +40 | **+132** |
| `glass_cannon_spikes` | +40 | −20 | +20 | **+111** |
| `brutes_pauldron` | +20 | −20 | **0** | **+45** |
| `martyrs_relic` | +60 | −20 | +40 | **+34** |

Fíjese en `brutes_pauldron`: **neto cero en puntos y aun así +45**, porque cambia velocidad (66) por fuerza
(111). La regla de la ADR 0036 —«el maldito baja el doble en un atributo»— contaba **puntos**, y los puntos
no valen lo mismo. El arquetipo era **sabor, no decisión**, contra RF-077 («efecto potente y contrapartida
permanente»), y por eso **colocar mal el equipamiento no costaba nada**, que es lo que la ADR 0106 midió con
un experimento controlado y no supo explicar.

## Decisión

**El castigo deja de tener magnitud fija y pasa a elegirse para que el valor medido del objeto sea
negativo.** Un maldito es un **lastre por defecto** y solo compensa cuando el atributo que castiga no le
sirve a su portador — que es exactamente la decisión que el arquetipo promete.

| objeto | antes | ahora | valor |
|---|---|---|---|
| `brutes_pauldron` | +20 fue / −20 vel | +20 fue / **−40 vel** | +45 → **−21** |
| `glass_cannon_spikes` | +20 vel +20 téc / −20 res | +20 vel +20 téc / **−30 fue** | +111 → **−26** |
| `berserker_totem` | +20 fue/vel/res / −20 téc | +20 fue/vel/res / **−60 téc** | +132 → **−18** |
| `martyrs_relic` | +20 téc/res/cor / −20 fue | +20 téc/res/cor / **−60 vel** | +34 → **−53** |

**La regla es auto-verificable.** `ItemCatalog.Validate` ya no comprueba una magnitud: comprueba que
`ItemScale.ValueOf(item) < 0`. Un maldito futuro que en realidad sea una mejora **no se puede cargar**. Es
la misma tabla con la que ya se calcula el precio (ADR 0038), así que no se añade ningún criterio nuevo:
se aplica el que ya existía al sitio donde faltaba.

## Consecuencia: dos bandas que se intercambian

**El suelo de 45 del paquete AY queda derogado por su propio argumento**, que estaba escrito en el código:

> *«desde que un perk mal puesto no hace nada en vez de castigar, construir mal vale lo que no construir.
> Ni pierde (**suelo 45: si perdiera, algo estaría restando**) ni gana (techo 55).»*

Ahora **algo resta**. Así que:

- **Build mala** vuelve al techo de **45** de §8 y de la ADR 0078, sin suelo (queda uno de cortesía en 10
  para que una build que incomparece no pase por «mala» sin que nadie la mire).
- **Build al azar** se queda con la banda **45-55**. La ADR 0078 la juzgaba con el techo de «mala»
  argumentando que «una build al azar es una build mal construida»; eso deja de ser cierto, porque lo que
  hace mala a una build es **algo que resta** —un maldito en el portador equivocado, una colocación que se
  estorba— y la aleatoria no lleva ninguna de las dos. No está mal construida: está construida **sin
  criterio**.

Las dos bandas **no se inventan: se intercambian**, y cada una va a la etiqueta que describe de verdad.

## Medición

`BuildGateTests`, 40 plantillas × 12 partidos × 14 celdas:

| | antes | ahora | criterio |
|---|---|---|---|
| `badBuildsLoseToNone_human_scattered` | 58,12 | **21,67** | ≤ 45 ✓ |
| `badBuildsLoseToNone_orc_misplaced` | — | 38,96 | ≤ 45 ✓ |
| `badBuildsLoseToNone_elf_out_of_zone` | — | 41,25 | ≤ 45 ✓ |
| `badBuildsLoseToNone_elf_brawler` | — | 42,08 | ≤ 45 ✓ |
| `randomBuildLosesToNone_human_random` | 71,25 | 51,67 | 45-55 ✓ |

**Colocar mal el equipamiento cuesta por fin**, que es lo que la ADR 0106 declaró que no se medía.

## Lo que queda fuera, y no se tapa

**`buildsWinDifferently_injuries` = 1,29 contra un mínimo de 1,4.** Es el único rojo y tiene causa
identificada: la referencia neutra ahora **lleva equipo**, y el equipo apropiado a un defensa incluye
**resistencia**, que es justo el atributo con el que `Lethality.ResistancePercent` aguanta la lesión. Un
denominador más duro baja el ratio.

Se intentó quitarle la resistencia a la referencia (los siete con `duelists_gloves`, +10 velocidad y +10
técnica) y **rompió otras tres**: las builds coherentes de orco cayeron a 55,0-55,6 contra un mínimo de 58 y
la aleatoria se fue a 42,50, por debajo de su suelo. Es decir: **el equipo de la referencia es una palanca
que mueve tres métricas en direcciones opuestas**, y ajustarla a ojo hace oscilar la puerta.

No se toca la banda de 1,4 para que pase. Queda abierto como **CAT-G**: o se elige el equipo de la
referencia con un criterio derivado de la tabla de la ADR 0038 en vez de por rol, o se acepta que el ratio
de lesiones se mide contra una referencia equipada y se recalibra el 1,4 con su propia medición.
