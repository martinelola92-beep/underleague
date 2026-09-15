# 0113 — El oro que genera un perk es una inversión, no un premio

Estado: **Aceptada** (15 sep 2026, decisión del revisor). Fija cómo se lee **RF-114g** («perder no paga»)
cuando el oro no viene de la liga sino de un perk. Deriva de la primitiva D del paquete de tanda 1
(`docs/analisis/perks-catalogo-unificado.md` §8.1).

## Problema

Tres perks de la tanda 1 convierten cuerpos en oro: **Ídolo local** (cada gol suyo llena la grada),
**Taquillero** (cada partido que termina de pie llena un poco más la taquilla) y **Máquina de publicidad**
(lo que le hace al rival, se vende). La primitiva que los hace posibles —un contador de partido que paga
oro— se construyó colgada de `GoldCalculator.GoldForWin`, que es el premio de partido.

Y el premio de partido **no se cobra al perder** (RF-114g, `StandardRunSystems.AfterMatch`). Eso dejaba a
los tres perks contra su propia ficha, y a dos de ellos casi sin existir:

- **Taquillero** paga por *sobrevivir*, y perder es exactamente cuando sobrevivir importa. Su coste
  declarado es «alinearlo aunque esté tocado»; si además exigiera ganar, el perk no tendría caso de uso.
- **Máquina de publicidad** vende *lo que le haces al rival*, y se puede destrozar al rival y perder.
- **Ídolo local** dice «cada gol suyo», no «cada gol de una victoria».

## Decisión

**El oro que genera un perk es un canal aparte del premio de partido, y se cobra se gane o se pierda.**

El razonamiento del revisor, que es el que fija la regla: **es una inversión, no una recompensa**. El
jugador ya ha pagado por adelantado —el oro del perk en el mercado y, sobre todo, **un slot**, que es
irreversible— y lo que cobra después es el retorno. Un perk que solo rindiera en las victorias no sería
una inversión: sería una propina, y encima una propina que se cobra cuando menos falta hace, porque ganar
ya paga.

RF-114g **no se toca**: sigue siendo cierto que perder no paga *premio*. Lo que esta ADR decide es que el
oro de perk nunca estuvo dentro de ese «paga».

## Cómo queda

- `GoldCalculator.CounterGold(state, summary, economy)` es su propio canal, con su propio desglose, y
  **no entra** en `GoldForWinBreakdown.Total`.
- `AfterMatch` lo ingresa en las dos ramas. En la derrota se ingresa **después** de la penalización, para
  que el porcentaje de la derrota muerda el oro que la run traía y no el retorno de este partido. *(Hoy
  `defeatGoldPenalty` vale 0 / 0 %, así que el orden todavía no cambia ninguna cifra; se deja así porque
  el día que esa penalización deje de ser cero, gravar la inversión sería justo lo contrario de esta ADR.)*
- El informe post-partido lo enseña **también al perder** (RF-119). Esconderlo en la derrota devolvería a
  la invisibilidad justo a los perks que se cobran perdiendo, que es el problema que la ADR 0112 viene a
  arreglar.
- Sigue callándose si la run ha terminado, por el mismo motivo que el premio: no se ingresó.

## Consecuencias, y lo que hay que vigilar

- **Empuja a alinear gente tocada**, que es deliberado: es la decisión que el juego quiere que duela. Pero
  hay que medir que no se convierta en una renta que haga irrelevante ganar. La métrica a mirar cuando los
  tres perks estén en el pool es si el oro de contador se vuelve comparable al premio de partido.
- **Premia perder con perks de carne ajena.** Máquina de publicidad cobra por lesionar aunque el partido
  se pierda; conviene comprobar que no paga más que el premio de una victoria limpia.
- No toca el balance de hoy: sin ningún perk con tarifa en `data/economy/counter-gold.json`, el canal paga
  cero y la run se comporta exactamente igual.
