# 0110 — Remata quien está donde hay que rematar

Estado: **Aceptada** (decisión del revisor, 14 sep 2026). Deriva de `docs/auditoria-ia-jugadores-7.md` y
`docs/auditoria-ia-jugadores-8.md`.

## Problema

La colocación en Underleague es **libre**: `Simulator` valida que la casilla-hogar caiga en la rejilla, que
el portero esté en su área y que no se repitan casillas, y nada más. No hay mínimos ni máximos de
DEF/MID/FWD ni obligación de alinear un delantero (auditoría 6 §1).

Pero esa libertad no servía de nada. Con los mismos atributos y la **misma casilla** (6,3), los tiros por
partido según la posición base eran:

```
FWD natural   100
MID arriba     48   (47 / 47 en dos semillas)
DEF arriba     38   (36 / 40)
```

Reconvertir a un centrocampista costaba **la mitad** del ataque y a un defensa **casi dos tercios**. La
consecuencia práctica, medida: al perder al delantero, **replegar todo el equipo (46,5 % de victorias) era
mejor que reconvertir a nadie** (25,0 % con un centrocampista, 15,0 % con un defensa). La emergencia no
producía una decisión, producía una respuesta obligada.

## Causa

*(Auditoría 7, §6 y §14.)* La rigidez estaba **entera en una acción**: `Shoot`, con un abanico de 5,00×
entre defensa (77) y delantero (385).

La utilidad es una competición **situacional**. `Shoot` no compite contra `CoverSpace` —esa se evalúa sin
balón— sino contra `ShortPass`, que para un defensa vale **500**. Con 77 de base, un defensa con el balón
delante de la portería **nunca** elegía tirar: pasaba. El abanico de 5,00× no era un matiz de sabor, era un
**veto**.

`FindSpace` se descartó como causa **con datos**: igualarlo del todo no mueve la proporción (se queda plana
en ~35/48) y además **baja** los tiros absolutos, de 3,36 a 2,96. El defensa colocado arriba ya encontraba
el espacio —elegía `FindSpace` tanto como el delantero, 45,2 % contra 42,2 %—; lo que no hacía era **rematar
al llegar**. `Dribble` es residual (≤1 %) en las tres posiciones.

## Decisión

En `data/ai/weights.json`, dos números:

```
base.Defender.Shoot     77 → 154
base.Midfielder.Shoot  188 → 237
```

El delantero **no se toca** (385). La escala es un cuarto del camino desde el valor actual hasta el del
delantero. **Nada más**: ni `FindSpace`, ni `Dribble`, ni las acciones defensivas, ni la fórmula, ni
`ActionZone`, ni reglas de composición, ni una línea de código.

## Por qué un cuarto y no más

El barrido completo está en la auditoría 7 §6. Los dos extremos lo acotan:

- **A un cuarto** la identidad se conserva y **el hueco entre centrocampista y defensa se ensancha**
  (12/7 → 13/12): las tres posiciones se distinguen mejor que antes del cambio.
- **A un tercio** la reconversión mejora más, pero se rompe la curva de jefes de la ADR 0033:
  `bossGate_the_hunt_incoherent` 18,01 contra un techo efectivo de 17,5.
- **Igualando los pesos**, el **defensa dispara más que el delantero** (104 contra 100). El peso es lo único
  que sostiene la identidad; vaciarlo la borra.

## Qué cambia, medido

Auditoría 8, 1.000 partidos y `--full-runs 240` por semilla. **Ninguna de estas cifras fue objetivo.**

| | banda | antes s1/s2 | **después s1/s2** |
|---|---|---|---|
| tiros normalizados FWD/MID/DEF | — | 100/48/38 · 100/47/40 | **100/67/54 · 100/69/57** |
| `shotsPerMatch` | 7-15 | 7,36 / 7,25 | 7,60 / 7,49 |
| `tacklesPerMatch` | 6-14 | 12,80 / 10,33 | **12,74 / 10,30** |
| `injuriesPerMatch` | 0,3-0,9 | 0,87 / 0,52 | **0,85 / 0,51** |
| `possessionChanges` | 12-28 | 20,13 / 21,93 | 20,21 / 22,19 |
| `passChainAvgLength` | 2-4 | 2,03 / 2,10 | 2,02 / 2,09 |
| `ballThirdMaxShare` | ≤52 | 47,14 / 48,04 | 47,60 / 48,30 |
| `runWinRate` | 20-30 | **19,58** / 22,92 | **20,42 / 25,00** |
| `deathsPerRun` | 1,5-3 | 1,56 / 1,63 | 1,53 / 1,84 |

Las entradas y las lesiones **bajan** en las dos semillas: el ajuste no compra ataque con violencia, que era
el riesgo obvio. `runWinRate` entra en banda en la semilla 1, donde estaba por debajo.

**Emergencia por lesión del delantero** (victorias): replegarse 46,5/41,5 → 42,0/38,0; centrocampista arriba
25,0/24,0 → **33,5/37,5**; defensa arriba 15,0/18,0 → **28,5/33,5**. El coste de reconvertir pasa de
−21,5/−17,5 puntos a **−8,5/−0,5**: deja de ser un error y pasa a ser una apuesta.

**Formaciones extremas**: ninguna se vuelve dominante. Seis delanteros se queda plano (48,5/50,0 → 51,0/47,5,
sube en una semilla y baja en la otra) y seis defensas sigue muy por debajo de la formación equilibrada.

**Puertas: de 4 rojas a 3.** Se arregla `EquippingAGoodBuildIsWorthSeveralPointsOfWinRate` y `elf_brawler`
se acerca 2,9 puntos a su banda. No aparece ninguna puerta nueva y la curva de jefes sigue en verde.

La puerta de equipamiento no se arregla por casualidad: **si un defensa y un centrocampista pueden rematar,
un objeto ofensivo tiene a quién beneficiar** y el escalón «muy buena» de la ADR 0033 recupera contenido.

## Lo que esta decisión NO resuelve

Alinear **dos delanteros naturales** sigue valiendo ~20 puntos de tasa de victoria, y la auditoría 7 midió
que esa ventaja **sobrevive a todo el barrido de `Shoot`**: no la causa la especialización ofensiva.

La auditoría 8 le puso nombre: en la formación por defecto el delantero dispara el **93 %** de los tiros y
los **tres** centrocampistas el **6,5 %**, siendo el 43 % de los jugadores de campo. El centrocampista no
defiende como un defensa (21 % de cobertura contra 52 %) ni ataca como un delantero. **Cambiar un
centrocampista por un delantero gana tanto porque el centrocampista aporta muy poco.**

Eso es una decisión de diseño sobre el papel del centrocampista, no una calibración, y se aborda aparte.
**No se resuelve con restricciones de composición**: el problema es el valor del rol, no el número de
delanteros.

## Reversión

Devolver los dos números a 77 y 188. No hay migración de datos ni de guardado: el cambio es de pesos y no
toca ningún esquema.
