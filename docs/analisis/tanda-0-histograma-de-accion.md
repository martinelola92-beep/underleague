# Tanda 0 — el histograma de acción elegida, antes de tocar C1

**Fecha:** 17 sep 2026. **Instrumento:** `Sim.Tests/Analysis/ActionHistogramTests.cs`. **No toca `/Sim` ni
`/data`**: solo lee `MatchTrace.ActionAt` (RT-098, ya existente y ya verificado de solo lectura por RT-024).
Verificado: RT-024 (4/4), 761 tests no-puerta en verde, 43 puertas con las mismas 3 rojas de siempre
(`BuildsWinDifferently`, `BadBuildsLoseToTheirBaseline`, `NoGateMetricIsOutOfRange` — ver `docs/pendientes/BB-P.md`,
ninguna relacionada con este instrumento).

## Por qué existe

`docs/analisis/catalogo-conceptual-fase-a.md`, "Orden de trabajo que se deduce": **Tanda 0 — el
instrumento, antes de tocar nada.** Sin un histograma de qué intenta hacer un jugador, "esto cambia la
conducta" es una opinión, no una medida, y ninguna de las tres pruebas de validación de la Fase C del
mismo documento se puede completar hasta que exista.

## Qué mide

Para un jugador concreto (el portador de un perk), a lo largo de todos los ticks en que está en el campo,
qué fracción de esos ticks corresponde a cada `PlayerAction` elegido (`MatchTrace.ActionAt`, que no cambia
cada tick — se mantiene entre decisiones, así que esto mide "qué está haciendo", no "qué ha decidido en
este tick concreto"). Dos histogramas por perk, con la misma metodología de control emparejado que la ADR
0087 (mismas plantillas, mismas semillas de partido, con el perk puesto y sin él — sin arrastre de
campaña entre partidos, a diferencia de `PerkValueRunner`: para calibrar el instrumento basta con partidos
independientes). La distancia entre los dos histogramas es la **L1** (suma de valores absolutos de la
diferencia por acción, 0 = idénticos, 2 = disjuntos).

## Calibración: 4 perks ya existentes, no un candidato de C1

Elegidos porque el propio catálogo conceptual predice su relación — dos que **no deberían** cambiar el
histograma (`modifyProbability`, no toca la tabla de utilidad) y dos que sí podrían (`modifyLeash`,
`cancelEvent`, con vía plausible a la utilidad):

| perk | efecto | activaciones (de 40 partidos) | L1 (con perk vs control) |
|---|---|---|---|
| `bulwark_stance` | `modifyProbability` `tackle` +100%, si `hasTag(owner,'Bulwark')` | **2** | 0,0000 |
| `own_third_anchor` | `modifyProbability` `tackle` +100%, si `startsIn(owner,'OwnThird')` | **40** | 0,0000 |
| `sweeper_keeper` | `modifyLeash` +2 casillas, portero, si recupera en zona propia | 35 | 0,0013 |
| `iron_gate` | `cancelEvent` sobre `INJURY`, una vez por partido | 3 | 0,0041 |

40 plantillas por perk (20 parejas × 2 direcciones), semilla 1. Cada arm accede a 50.000-55.000 ticks del
portador — muestra amplia para una fracción de tiempo por acción.

## Lo que confirma, con matices que no estaban en el plan original

**`own_third_anchor` es la confirmación fuerte, no `bulwark_stance`.** Las dos son mecánicamente idénticas
(`modifyProbability` sobre `tackle`, sin tocar `_actionMultipliers`), pero `bulwark_stance` solo activa en
2 de 40 partidos: su condición (`hasTag(owner,'Bulwark')`) casi nunca la cumple un portador elegible al
azar, así que su `L1=0` es **poco informativo** — es el mismo límite que ya documentó la ADR 0087 (AT-C:
"un bonus condicionado a una etiqueta... que el portador al azar rara vez cumple"), encontrado otra vez
aquí, con otro instrumento. `own_third_anchor` sí activa en los 40 partidos (su condición de zona de salida
es casi siempre cierta) y **aun con activación completa y ~50.000 ticks de muestra, su `L1` es exactamente
cero**: confirmación sólida de que un `modifyProbability` no mueve un solo punto porcentual del histograma
de acción, no una coincidencia de baja potencia.

`sweeper_keeper` (activación alta, 35/40) sí muestra una diferencia pequeña pero no nula (`L1=0,0013`): el
instrumento **distingue** un efecto real, aunque pequeño, de un cero exacto. `iron_gate` mide `L1=0,0041`
—mayor que `sweeper_keeper`— con solo 3 activaciones: una lesión es un suceso raro y 3 casos no dan
confianza sobre si ese número es real o ruido de muestra pequeña; queda anotado, no interpretado.

## Consecuencia para Tanda 2 (C1)

**El umbral de "esto no cambió nada" no es 0,000 puro, es del orden de lo que mide `own_third_anchor`
(cero, bien potenciado) frente a lo que mide `sweeper_keeper` (0,0013, con una vía real y conocida a la
utilidad).** Y la lección operativa, que hay que llevar a la medición de cualquier candidato real de C1:
**registrar la tasa de activación junto a la L1 en la misma tabla, siempre** — un L1 bajo con activación
baja no es una perk sin efecto, es una medición sin potencia, y confundir las dos cosas es exactamente el
error que la ADR 0087 ya corrigió una vez para el valor en tasa de victoria.

## Qué NO se ha hecho aquí, a propósito

- No se ha tocado `modifyUtility`, ningún peso, ningún perk ni ningún tope. Cero cambios de comportamiento.
- No se ha medido ningún candidato real de C1 (Cazagoles, Ancla): eso es Tanda 2, y solo tras revisar esto.
- No se ha resuelto el confundido de activación para `bulwark_stance`/`iron_gate`: haría falta forzar la
  condición (un carrier con el trait `Bulwark`, o más partidos para acumular más lesiones) para tener una
  segunda pareja de calibración bien potenciada. Queda anotado, no implementado.

## Hermanos

- `docs/analisis/catalogo-conceptual-fase-a.md` — el documento que define Tanda 0 y el orden de trabajo
  completo (Tanda 0 → 1 → 2 → 3 → 4).
- `docs/analisis/perks-catalogo-unificado.md` §3.3/§8.3 — los 22 perks de la Tanda 3 del catálogo que
  necesitan C1/C2 para existir.
- `docs/decisiones/0087-el-valor-de-un-perk-se-mide-contra-su-control.md` — la metodología de control
  emparejado que este instrumento reutiliza, y el límite de activación (AT-C) que este documento vuelve a
  encontrar con un instrumento distinto.
