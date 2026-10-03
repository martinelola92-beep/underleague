# BV-D — Las lesiones subieron con las ADR 0184/0186 y hay que devolverlas sin quitar entradas

**Estado:** abierto (3 oct 2026). Hermanas: [BV-A](./BV-A.md), [BV-B](./BV-B.md), [BV-C](./BV-C.md).

## Observación

Con la sostenida de colocación (ADR 0184) y la entrada que llega (ADR 0186) el partido tiene **más contacto**:
entradas +22-24 %, faltas +13-20 %. Las lesiones lo siguieron (lote de referencia, 10.000 partidos, media ± e.t.):
s1 **0,692 ± 0,009 → 0,789**, s2 **0,395 ± 0,006 → 0,464**; lesiones propias por run 3,70 ± 0,14 → 4,04 ± 0,15
(`--full-runs 240`, semillas 1-3). Decisión del revisor: «ya bajaremos las lesiones por otro sitio, por ejemplo
subiendo la dificultad de generar una lesión» — **sin deshacer esas ADR y sin tocar la frecuencia de entradas**.

No es un defecto: el motor lesiona por contacto, y hay más contacto. Lo que se pide es que **cada contacto lesione
menos**, para que el total vuelva a la cifra de antes.

## Dónde se decide una lesión (leído, Regla I)

Un único sitio: `MatchEngine.ResolveInjury(tackler, victim, isFoul)`. Lo llaman:

1. `ResolveTackle` — entrada al portador, o entrada sin balón que fue falta (`carrierHasBall || isFoul`).
2. `ResolveBlock` — bloqueo que tumba o que es falta (`isWin || isFoul`).
3. `ProvokeInjury` — el efecto `injure` de un perk (`dirty_play` en `FOUL`, `ankle_bite` en `TACKLE`), **siempre con
   `isFoul: true`**.

Aparte, sin pasar por la fórmula: la turba (`MobInjure`, ADR 0167, lesión leve sin autor) y la letalidad de perks
(ADR 0048, tirada propia de muerte).

Fórmula, en base 10.000:

```
chance = onTackleBase (140) + (isFoul ? onFoulBase (60) : 0)
       + relativeFactor (2) × (fuerza del que entra − aguante de la víctima)
       + 100 × injuryChanceBonus (rasgo Dirty +10, perk kamikaze +20)  − 100 × injuryResistanceBonus (Resilient +15)
chance ×= cuotas compuestas Injure (del que entra) · Injury (de la víctima)       ← perks: skullsplitter, marrow_thirst…
chance = clamp(chance, 0, 5000) × InjuryScalePercent / 100                     ← sólo en la run: acto 120/260/420, élite 150
grave si Chance(severeShare 4000 × cuota SevereInjury)
muerte (RF-093 vía 1) si la víctima salió con lesión grave sin tratar
```

## Palancas y su efecto sobre leve / grave / muerte

| Palanca | A quién toca | Leve/grave | Muerte | Identidad de violencia |
|---|---|---|---|---|
| `onTackleBase` ↓ | toda tirada, **también la del perk `injure`** | las dos por igual (reparto fijo) | vía 1 baja con las lesiones; vía 2 igual | conserva el bono absoluto del rasgo y el multiplicador del perk; el cociente de builds **sube** (ADR 0084) |
| `onFoulBase` ↓ | sólo faltas y perk `injure` | igual | vía 1 | **castiga** la falta, que es el oficio de la build de violencia |
| `relativeFactor` ↓ | diferencia fuerza − aguante | igual | vía 1 | quita la identidad del orco (fuerza +10) |
| `severeShare` ↓ | sólo la gravedad | menos graves, mismas lesiones | baja vía 1 | — no devuelve el recuento |
| rasgos (`Dirty`, `Resilient`) / cuotas de perk | lo que se anuncia | igual | — | es justo lo que no se toca |
| `actScalePercent`, `eliteScalePercent` | sólo la run | igual | vía 1 | no devuelve el partido de referencia |
| **`onTackleBase` ↓ y `onFoulBase` ↑ en lo mismo** | sólo el **contacto limpio**; la falta y el perk `injure` quedan **bit a bit** | igual | vía 1 | intacta: la falta lesiona lo mismo, la entrada limpia menos |

## Hipótesis sobre cuánto puede dar cada palanca

- **H1** — la base del contacto limpio pesa lo bastante como para devolver el total sin tocar la falta.
- **H2** — no basta (las lesiones vienen sobre todo de faltas o del rasgo `Dirty`), y hay que bajar también la base
  común, aceptando que el perk `injure` pierda algo.

Discrimina un experimento barato: `onTackleBase` a 0 con la suma de la falta conservada (`onFoulBase` 200) da el
**máximo** que la palanca limpia puede quitar. Si ese máximo no llega al −12/−15 % que hace falta, H1 queda REJECTED.

## Mediciones

(se rellenan abajo)
Lote de referencia (`--runs 10000 --teams data/balance/reference.json`, media ± e.t. por partido; `base` = HEAD, que
reproduce las cifras de la ADR 0186: 0,789 / 0,464):

| brazo | s1 lesiones | s2 lesiones | s1 entradas | s2 entradas | s1 goles | s2 goles |
|---|---|---|---|---|---|---|
| base (140 / 60) | 0,789 ± 0,009 | 0,464 ± 0,007 | 9,27 | 11,22 | 2,474 | 2,087 |
| E1: limpio 0, falta 200 (la falta igual) | 0,659 ± 0,008 | 0,334 ± 0,006 | 9,32 | 11,25 | 2,490 | 2,098 |
| E2: 0 / 60 (base común a 0) | 0,573 ± 0,008 | 0,277 ± 0,005 | 9,37 | 11,28 | 2,508 | 2,109 |
| 55 / 145 | 0,707 ± 0,009 | 0,375 ± 0,006 | 9,30 | 11,25 | 2,485 | 2,093 |
| objetivo (antes de la 0184) | 0,692 | 0,395 | | | | |

**H1 CONFIRMED** (E1 aislado, sólo cambia la base del contacto limpio): el contacto limpio solo puede quitar −0,130
lesiones por partido en las dos semillas, más de lo que hace falta (−0,097 / −0,069). **H2 innecesaria.** Las entradas
no bajan en ningún brazo (suben unas centésimas: menos lesionados, más jugadores en el campo).

Runs completas (`--full-runs 240`, semillas 1-3, contextual, 720 runs; `base` reproduce la ADR 0186):

| brazo | lesiones propias / run | graves / run | muertes / partido | `runWinRate` |
|---|---|---|---|---|
| base | 4,04 ± 0,15 | 2,02 | 0,155 ± 0,007 | 15,3 (12,9 / 16,7 / 16,2) |
| 55 / 145 | 3,12 ± 0,12 | 1,52 | 0,153 ± 0,007 | 16,0 (14,6 / 19,2 / 14,2) |
| objetivo (antes de la 0184) | 3,70 ± 0,14 | | 0,152 ± 0,008 | 16,8 |

**Las dos cifras objetivo no las devuelve el mismo valor** (CONFIRMED con los dos lotes): la run responde al contacto
limpio con más fuerza que el partido de referencia (con 55 la referencia queda en +2 % / −5 % y la run en −16 %).
