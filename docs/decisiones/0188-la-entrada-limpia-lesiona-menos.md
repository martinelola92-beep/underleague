# 0188 — La entrada limpia lesiona menos; la falta, lo mismo (BV-D)

Fecha: 3 oct 2026 · Estado: **aceptada** (decisión del revisor en las ADR 0184/0186: «ya bajaremos las lesiones por otro
sitio, por ejemplo subiendo la dificultad de generar una lesión») · Requisitos: RF-055d, RF-057, RF-093, RT-056, RT-057 ·
Ficha: [BV-D](../pendientes/BV-D.md) · Cambia el reparto de la [ADR 0041](0041-lesiones-relativas-al-nivel.md), no su
fórmula · Va con las [ADR 0184](0184-una-colocacion-se-sostiene.md) y [0186](0186-la-entrada-llega-y-la-falta-tira-a-la-victima.md)

## Problema

Con las ADR 0184 y 0186 el partido tiene más contacto (entradas +22-24 %) y las lesiones lo siguieron: por partido
**0,692 → 0,789** (s1) y **0,395 → 0,464** (s2); lesiones propias por run **3,70 → 4,04**. El revisor quiere devolverlas
sin deshacer esas ADR y **sin quitar entradas**, que son las que dan vida al partido. Lo que hay que cambiar es lo que
lesiona cada contacto.

## Dónde se decide una lesión (Regla G e I, leído)

En un solo sitio: `MatchEngine.ResolveInjury`. Lo llaman la entrada (`ResolveTackle`), el bloqueo (`ResolveBlock`) y el
efecto `injure` de un perk (`ProvokeInjury`, `dirty_play` y `ankle_bite`), que **siempre tira como falta**. La turba
(ADR 0167) y la letalidad de perks (ADR 0048) no pasan por esta fórmula. La cuota (base 10.000) es
`onTackleBase + (falta ? onFoulBase : 0) + 2 × (fuerza − aguante) + 100 × rasgos`, por las cuotas de perk (producto), por
la escala del acto. Las palancas y su efecto en leve, grave y muerte están en la tabla de BV-D.

## Decisión

Mover **50 puntos de `onTackleBase` a `onFoulBase`**: `onTackleBase` 140 → **90**, `onFoulBase` 60 → **110**.

- **La falta, señalada o no, lesiona exactamente lo mismo que antes** (la suma sigue en 200), y con ella el efecto
  `injure` de los perks: `dirty_play`, `ankle_bite`, el rasgo `Dirty` y las cuotas de perk de la violencia no cambian.
- **La entrada o el bloqueo limpio lesiona 50 puntos menos** (0,5 puntos porcentuales): es la «dificultad de generar
  una lesión» que pide el revisor, y cae sólo sobre el contacto que nadie anunciaba.
- Leve/grave no cambia (`severeShare` igual); la muerte por reincidir (RF-093 vía 1) baja con las lesiones; la de los
  perks letales no se toca.

Sin cambio de regla en `/Sim`: la cuota se extrae a `MatchEngine.InjuryChance` (pura, mismos dados) para poder fijarla
en un test. `/Game` no cambia: la lesión es el mismo evento.

**Procedencia del 90 (Regla H).** Las cifras objetivo son las de antes de la ADR 0184, medidas en las ADR 0184/0186
(0,692 y 0,395 por partido con 10.000 partidos; 3,70 ± 0,14 propias por run con 720 runs). **Un solo valor no devuelve
las tres** (CONFIRMED con los lotes: la run responde al contacto limpio con más fuerza que el partido de referencia). El
criterio se fijó antes de medir el valor elegido: las tres pesan igual y se toma el múltiplo de 5 que hace **menor la
peor desviación relativa**. Barrido (s1 / s2 / run): 140 → +14,0 / +17,5 / +9,2 %; **90 → +7,2 / +4,3 / −5,4 %**; 75 →
+4,9 / +0,3 / −12,2 %; 55 → +2,2 / −5,1 / −15,7 %.

## Alternativas descartadas (medidas)

- **Bajar la base común** (`onTackleBase` a 0 con `onFoulBase` 60: 0,573 / 0,277): baja también la falta y el `injure`
  de los perks, lo que el revisor pidió no tocar. Con la falta conservada (`onTackleBase` 0, `onFoulBase` 200: 0,659 /
  0,334) basta, así que no hace falta (BV-D, H2).
- **Bajar `onFoulBase`, `relativeFactor`, el rasgo `Dirty` o las cuotas de perk:** castigan justo la identidad de la
  violencia (RF-055d) o del orco (fuerza +10).
- **Un multiplicador sobre toda la cuota de contacto:** escala igual el bono del rasgo y del perk; la violencia perdería
  en absoluto lo mismo que la limpieza. Además es un dato nuevo.
- **`severeShare` o la escala por acto:** no devuelven el recuento del partido de referencia.

## Efecto medido

Lote de referencia (`--runs 10000 --teams data/balance/reference.json`; media ± e.t. por partido; «antes» = HEAD con
140/60, que reproduce las cifras de la ADR 0186):

| | s1 antes | s1 ahora | s2 antes | s2 ahora |
|---|---|---|---|---|
| lesiones | 0,789 ± 0,009 | **0,742 ± 0,009** | 0,464 ± 0,007 | **0,412 ± 0,007** |
| entradas | 9,27 ± 0,04 | 9,29 ± 0,04 | 11,22 ± 0,04 | 11,24 ± 0,04 |
| entradas sin balón | 3,83 | 3,84 | 2,51 | 2,51 |
| faltas | 6,94 | 6,98 | 4,60 | 4,60 |
| goles | 2,474 ± 0,013 | 2,479 ± 0,013 | 2,087 ± 0,013 | 2,088 ± 0,013 |

Las entradas no bajan (suben unas centésimas: hay menos lesionados en el campo). La s1 queda a mitad de camino del
objetivo (0,742 frente a 0,692), la s2 cerca (≈ 1,7 e.t.).

Runs completas (`--full-runs 240`, semillas 1-3, doctrina contextual, 720 runs por brazo):

| | antes de la 0184 | antes (0184 + 0186) | ahora |
|---|---|---|---|
| lesiones propias / run | 3,70 ± 0,14 | 4,04 ± 0,15 | **3,50 ± 0,14** |
| lesiones graves / run | | 2,02 ± 0,07 | 1,76 ± 0,07 |
| muertes / partido (banda 0,11-0,22) | 0,152 ± 0,008 | 0,155 ± 0,007 | **0,155 ± 0,007** |
| muertes / run | 1,40 | 1,43 ± 0,07 | 1,43 ± 0,07 |
| `runWinRate` | 16,8 | 15,3 (12,9 / 16,7 / 16,2) | 16,2 (14,6 / 18,3 / 15,8) |

**La muerte sigue rara y previsible (ADR 0048):** no se mueve, porque la domina la letalidad de perks, que se anuncia en
el ojeo y no pasa por esta fórmula. Las cinco condiciones no se debilitan: la condición 3 (reducir el riesgo con la
alineación) **gana**, porque ahora más parte de la lesión depende de a quién tienes delante (faltas, rasgo `Dirty`,
perks) y menos del contacto de cualquiera.

**Behavioral audit** (`players.csv`, 10.000 partidos por brazo): las lesiones bajan en proporción en las tres líneas
(s1 recibidas: defensa 2.685 → 2.548, medio 3.604 → 3.369, delantero 1.589 → 1.492) y se concentran algo más en quien
más faltas hace: el 20 % de jugadores con más faltas por partido causa el 82,5 → 83,9 % (s1) y 82,1 → 86,2 % (s2) de las
lesiones. Es lo que se buscaba: lesiona quien juega sucio.

**Puerta de builds** (`BuildGateTests`, ocho semillas, media; `main` → ahora): `coherentBuildsBeatNone_orc_violence`
53,83 → 53,65 (roja en `main` y roja ahora, sin cambio distinguible: la puerta pide ≥ 58); `buildsWinDifferently_injuries`
**1,35 → 1,38** (la build de contacto se separa más de la técnica, como predecía la ADR 0084); `elf_brawler` 45,96 →
45,91; el resto de celdas a menos de 0,5 puntos.

**`Category=Gate` completa una vez con el código final (48 tests, 12 m 34 s): 7 rojas, ninguna nueva.** Todas son las
de `main` (ADR 0184/0186): BossGate curva, BuildGate ×3 (`orc_violence` 53,65, `elf_brawler` 45,91), `RaceBalance`
`elf_none` 39,42 (`main` 39,08), `StatisticalTests` ×2 (`betterTeamWinRate` 60-40 = 90,96, igual que `main`). La curva
del jefe, medida aparte con las bases de antes: `main` tiene tres celdas fuera (`grimhold_guns_correct` 61,56,
`grimhold_guns_good` 72,44, `eternal_crown_excellent` 42,27) y ahora dos (61,62 y 42,40; `grimhold_guns_good` vuelve).
`FullRunGateTests.TheThreeDoctrinesBuyDifferently`, roja en la semilla 1 con la 0184/0186, pasa. Bucle de tests verde
(RT-024 dentro).

## Nota de `game-design-review` (diez preguntas)

1 el jugador ve más lesionados desde la 0184/0186 y la plantilla se gasta antes; 2 alinear (rasgos, perks, quién marca a
quién) y decidir si juega sucio; 3 que la lesión venga de la falta y del jugador que entra a hacer daño, no de cualquier
entrada; 4 RF-057 y la fórmula de la ADR 0041 —no es regla nueva, es otro reparto de sus dos bases—; RF-055d (builds de
violencia) intacto; 5 `/data` (`tuning.injury`, dos números), `/Sim` sin cambio de regla, `/Game` nada; 6 alternativas:
arriba; 7 trade-off: el equipo limpio lesiona menos que antes, el sucio igual —la violencia se distingue más (cociente
1,35 → 1,38)—; 8 las builds de violencia ganan peso relativo y las técnicas sufren menos lesiones de rebote; 9
degeneración: ninguna nueva —la falta no lesiona más que antes, sólo deja de estar diluida—; un perk que convierta
contactos en falta valía ya lo mismo; 10 test de valor conocido con control y lotes de partido, run y builds.

## Tests

- `InjuryChanceTests` (valor conocido con control, par a par sobre los 72 pares de jugadores de campo del partido de
  referencia): la cuota de falta es la misma con 140/60 y con 90/110; la limpia baja exactamente 50 donde no la corta
  el suelo de 0; con las bases de antes, falta − limpia = 60 como en `main`.
- `MobNarrowingTests.WithTheNewRulesOffEveryTraceIsTheOneBeforeAdr0184` (cambiado, justificado): su «motor de antes»
  lleva ahora también las bases de lesión de antes; con las de ahora sus trayectorias cruzan la franja movida.
- Las huellas de RT-024 de `MobNarrowingTests` (60 partidos) **no cambian**, y no es un instrumento ciego (Regla J): la
  cuota limpia en esos partidos está entre 48 y 116 y ningún dado de contacto limpio cae en la franja de 50 puntos
  (21 lesiones con las dos bases; esperable ≈ 2 casos). Por eso el control discriminante es el de la cuota.
