# BI-G — El protocolo de balance mide los perks de entrada con una métrica que no puede responder

Estado: **CONFIRMED** (24 sep 2026). Encontrada al remedir `EndToEndProtocolDemoTests` tras los arreglos de
BC-A y BI-F, no jugando. Es un hallazgo de **instrumento**, no de gameplay: no cambia lo que ve el jugador,
cambia lo que el proyecto **cree estar midiendo** al calibrar un perk.

## Síntoma

`EndToEndProtocolDemoTests.TuningAndValidationMachineryWorksOnRealSimulatedData` afirma que *«más tackle
probability debería subir `tacklesPerMatch` de forma monótona en la tripleta»*. Pasó en verde durante meses
y se puso en rojo con los arreglos de reanudación y balón aéreo.

## Lo medido

Fixture `demo_tackle_fixture`: `ModifyProbability` / `ProbabilityKind.Tackle`, sobre un defensa, con
activación comprobada de **1,00 por partido** — el perk **sí dispara, siempre**.

| valor del candidato | 5 | 15 | 30 | 40 | 60 | 100 |
|---|---|---|---|---|---|---|
| `tacklesPerMatch` del brazo armado | 10,10 | 10,10 | 10,10 | 10,10 | 10,10 | 9,96 |

**Idéntico a dos decimales de 5 a 60.** Y no es ruido de muestra: a 60 plantillas y a **300** (cinco veces
más) los deltas salen iguales entre sí —−0,14, −0,14, −0,17—, no convergiendo a nada distinto.

En `main` antes de estos arreglos los tres deltas eran **exactamente 0,10, 0,10 y 0,10**. El test pasaba
porque `IsMonotonic(increasing: true)` acepta la igualdad: **estaba en verde por un empate, no por una
demostración**. Cualquier perturbación del motor tenía que romperlo tarde o temprano.

## La causa (CONFIRMED, por lectura directa del motor)

`MatchEngine.ResolveTackle`:

```csharp
bool isWin = _rng.ChanceAveraged(Bounded(win)) && carrierHasBall;
if (carrierHasBall)
{
    _report.Tackles++;   // <- incondicional
```

**`Tackles` cuenta entradas INTENTADAS.** Se incrementa siempre que se entra a un portador, gane o pierda.
`ProbabilityKind.Tackle` sólo entra en `win`, que decide `TacklesWon`. Intentar una entrada lo decide la IA
de utilidad; ganarla, la probabilidad. Son dos cosas distintas y la ADR 0125 D1 ya había separado
`tacklesPerMatch` de `offBallTacklesPerMatch` por una razón de la misma familia.

Y sin embargo `PerkBalanceClassifier.ProbabilityMetricName` dice:

```csharp
ProbabilityKind.Tackle or ProbabilityKind.TackleEvasion => MatchMetrics.TacklesPerMatch,
```

**El clasificador elige, como métrica primaria de toda la familia de perks de entrada, la única que por
construcción no puede moverse con su parámetro.** Cualquier perk de entrada que pase por el protocolo se
calibraría contra un número inerte, y el protocolo concluiría «sin efecto» sobre perks que sí lo tienen.

## Por qué no se arregla aquí

`MatchSummary` **ni siquiera lleva `TacklesWon`** al nivel de análisis: el arreglo no es repuntar el
clasificador, es llevar el dato y darle banda RT-056 a una métrica nueva. Eso es cambiar el juego de
métricas del proyecto: toca `docs/balance.md`, las puertas y una ADR (RT-057), y pasa por
`game-design-review` antes que por código. No cabe dentro de un arreglo de reanudación reportado jugando.

## Lo que se hizo con el test

Se le quita la afirmación que **no puede ser cierta** —la respuesta monótona de `tacklesPerMatch` al
parámetro— y se deja escalando a `DESIGN_REVIEW`, que es exactamente lo que su hermano
`ProtocolTraversesClassificationScreeningTuningAndValidation` ya hace en ese caso y documenta como el
comportamiento correcto del protocolo («no elegir un ganador a ciegas»). **No es silenciar una señal: es
retirar una aserción que nunca pudo señalar nada**, y la señal real vive aquí.

A cambio el test pasa a exigir lo que sí es verdad y sí hay que proteger: que el fixture **active**. Si el
perk deja de dispararse, el test se pone en rojo y esta vez por algo.

## Hermanos

Es el tercer caso del mismo patrón en dos días: [BC-D](./BC-D.md) («Último hombre» se activa y no hace
nada) y la **ADR 0146** (`modifyUtility` era inalcanzable). Los tres son «el texto promete lo que el dato o
el código no hacen», el patrón que `docs/project-state.md` ya venía señalando.
