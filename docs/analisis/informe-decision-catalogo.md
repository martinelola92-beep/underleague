# Informe de decisión del catálogo — los 24 `ReadyForScreening`

**19 sep 2026.** No es un lote, ni un veredicto, ni un ranking. Es un mapa para que el diseñador decida
qué perks quiere conservar. **El catálogo actual es de trabajo**: el objetivo no es que pasen.

Evidencia reutilizada sin volver a medir: §33 (geometría con `TerritorialBalance`), §34 (pasada
diagnóstica de los 24), `docs/pendientes/BB-Q.md` (`steamroller`). Mediciones **nuevas**, solo las que
distinguen una causa: `Sim.Tests/Balance/CatalogDecisionProbeTests.cs`.

## Cómo leer las categorías

| | significado | lo que **no** significa |
|---|---|---|
| **A** | evidencia interpretable, lectura neutra | — |
| **B** | no se puede juzgar el diseño: mal expresado, portador incompatible o métrica inadecuada | **no** significa "mal perk" |
| **C** | el instrumento no ha generado evidencia suficiente | **no** significa "perk malo" |
| **D** | hay evidencia y el comportamiento no corresponde a la intención declarada | — |
| **E** | la evidencia es compatible con lo que pretende | **no** significa "aprobado" |

Reparto: **B 5 · C 16 · D 1 · E 2 · A 0.** Que **A** salga vacía no es casual: todo perk con evidencia
interpretable se resolvió a coherente (E) o discrepante (D). No hay término medio porque hay muy poca
evidencia.

## La medición nueva y qué discrimina

Para cada perk por debajo del 50 %: ¿existe **alguna** población (raza × puesto) donde sí se exponga? Si
la exposición se dispara, el perk no es raro — **el banco de pruebas es el equivocado**.

| perk | por defecto | mejor población | factor | lectura |
|---|---|---|---|---|
| `bulwark_stance` | 5,0 % | **80,0 %** Dwarf/Defensa | **16×** | composición racial |
| `back_to_back` | 10,0 % | **90,0 %** Dwarf/Defensa | **9×** | composición racial |
| `grudge` | 12,5 % | **77,5 %** Orc/Delantero | **6,2×** | población |
| `shadow_marker` | 22,5 % | **90,0 %** Orc/Delantero | **4,0×** | composición racial |
| `double_shot` | 2,5 % | **100,0 %** Undead/Delantero | **40×** | **portador** |
| `second_wound` | 7,5 % | 25,0 % Orc/Portero | 3,3× | rareza real |
| `iron_price` | 2,5 % | 7,5 % Human/Delantero | 3,0× | rareza real |
| `game_management` | 25,0 % | 47,5 % Dwarf/Medio | 1,9× | rareza real |
| `iron_gate` | 17,5 % | 17,5 % Dwarf | **1,0×** | rareza real |
| `steamroller` | **0,0 %** | **0,0 %** | — | **roto** |

Los perks que dependen de etiquetas de estilo (`Bulwark`, `Brute`) se están midiendo sobre una plantilla
Human, donde `Bulwark` pesa 6 y `Brute` 10 (`data/races/*.json`). En Dwarf `Bulwark` pesa 75. **No son
perks raros: es el banco de pruebas.**

`cannon` y `double_shot` llevan la misma sospecha —portador que no dispara— y dan respuestas **opuestas**:

| perk | Defensa | Medio | Delantero |
|---|---|---|---|
| `double_shot` expos. / Δ`shotsPerMatch` | 2,5 % / +0,075 | 10,0 % / +0,250 | **90,0 % / +4,425** |
| `cannon` expos. / Δ`shotsPerMatch` | 100 % / **0,000** | 100 % / **0,000** | 100 % / +0,050 |

> **Corrección (19 sep 2026, [BC-B](../pendientes/BC-B.md)):** el +4,4 de `double_shot` es un artefacto: el perk se
> encadenaba hasta la profundidad de recursión (5 activaciones y 6 `SHOT` en un mismo tick) y no cambiaba el
> resultado ([BC-C](../pendientes/BC-C.md)). La conclusión de abajo no vale; hay que volver a medir.

**`double_shot` funciona**: con el portador correcto dispara al 90 % y mueve los tiros +4,4. **`cannon` no
se mueve en ningún puesto** — pero su mecanismo es `shootRangeBonusCells +3`, o sea *disparar desde más
lejos*, y **el proyecto no tiene ninguna métrica de distancia de tiro**: `MatchReport` cuenta `Shots`,
`ShotsOnTarget`, `ShotsBlocked`, y ninguna registra desde dónde. Es el mismo error de clase que
`ballThirdMaxShare` para geometría (§32): **la métrica primaria no representa el eje del efecto.**

---

## Las 24 fichas

Formato: intención · disparador · portador real · exposición · resultado del auditor · evidencia · causa ·
**diagnóstico** · confianza · sugerencia.

### B — no se puede juzgar el diseño todavía (5)

**`steamroller`** — *"al derribar a alguien no se para, sigue"* · `TACKLE` · Defensa · **0,0 %** ·
`INSUFFICIENT_EVIDENCE` · cero activaciones en **toda** población probada, mientras su gemelo sin
condición (`charge`) encadena 65 veces con el mismo portador · **implementación**: tres causas encadenadas
(`target` sin ligar, `'down'` = lesionado/muerto, evento pre-resolución) · **B** · **alta** ·
*no decidir todavía* — la idea no se ha podido probar ni una vez. Ver `BB-Q.md` y la propuesta
arquitectónica.

**`cannon`** — *"si ve portería, dispara; la distancia le da igual"* · `MATCH_START` · Defensa · 100 % ·
`INSUFFICIENT_EVIDENCE` (Δ = **0,0000**) · el modificador se aplica siempre y `shotsPerMatch` no se mueve
en ningún puesto · **métrica**: mide cuántos tiros, no desde dónde; no existe métrica de distancia ·
**B** · **media** (que la métrica es inadecuada es alta; que el mecanismo sea inerte, no medido) ·
*revisar* — antes de tocar el perk hace falta un instrumento que vea el eje del efecto.

**`double_shot`** — *"si la primera sale mal, la segunda sale sola"* · `SHOT` · Defensa · 1,7 % ·
`INSUFFICIENT_EVIDENCE` · **en Delantero: 90 % de exposición y +4,425 tiros/partido** · **portador**: no
declara `positionOnly`, y la elegibilidad lo da a un Defensa que apenas dispara · **B** · **alta** ·
*conservar* — el mecanismo está **demostrado**; lo que falla es a quién se le da.

**`high_line`** — *"su defensa vive dos casillas más arriba"* · `MATCH_START` · Defensa · 100 % ·
`SCREENING_NEEDS_TUNING` · **el único NEEDS_TUNING del catálogo, y es falso**: sale de
`ballThirdMaxShare`, demostrada inválida para geometría (§32). Con `TerritorialBalance`: +0,210, dentro del
ruido. §30 no le encontró coste; §31 no le encontró poder gratis · **métrica** · **B** · **alta** ·
*no decidir todavía* — no es un problema de balance.

**`shadow`** — *"se descuelga una casilla respecto al vinculado; si él se queda atrás, él también"* ·
`MATCH_START` · Medio · 100 % · `INSUFFICIENT_EVIDENCE` · territorial −0,185 (ruido). **La implementación
es un `shiftHome −1` absoluto**: `links: ["ahead"]` no se consulta nunca para un efecto con
`target: owner` (`EffectEngine.ResolveLinkedTargets` solo entra con `Linked`/`LinkedWithTag`), y la
exposición del 100 % confirma que dispara haya o no compañero vinculado · **implementación** · **B** ·
**alta** · *revisar* — la pregunta de diseño (*¿es buena la idea de una altura relativa?*) **no se puede
hacer** hasta que el mecanismo sea el que el texto describe.

### D — discrepancia entre intención y comportamiento (1)

**`deep_run`** — *"vive al borde del fuera de juego… ya está corriendo a la espalda del central"* ·
`MATCH_START` · Delantero · 100 % · `INSUFFICIENT_EVIDENCE` en `ballThirdMaxShare`, pero con
`TerritorialBalance`: **−4,302, uno de los dos únicos deltas que se separan del ruido** · el equipo juega
4,3 puntos **más atrás**, y los goles no se mueven (+0,030 a favor, +0,045 en contra) · **coherencia**:
paga el coste que su propio `_doc` declara (*"juega solo y de espaldas al juego; si el pase no llega, no
participa"*) sin cobrar el beneficio · **D** · **media** (el signo es firme; lleva **dos** efectos,
`shiftHome +2` y `zoneShape forward +1`, sin separar) · *revisar*.

### E — comportamiento compatible con su intención (2)

**`charge`** — *"repite la entrada dentro del mismo tick"* · `TACKLE` · Defensa · 62,5 % ·
`SCREENING_PASS` (**vacío**: sin parámetro numérico) · 65 activaciones en 20 partidos; hace literalmente
lo que dice · **coherencia** · **E** · **media** · *conservar*.

**`deep_pivot`** — *"baja a buscarla entre sus centrales"* · `MATCH_START` · Medio · 100 % ·
`INSUFFICIENT_EVIDENCE` · territorial **−2,327**, signo correcto y el mayor de los `shiftHome` de portador
único, aunque dentro del ruido a n=200 · **potencia** · **E** · **baja** · *conservar*.

### C — evidencia insuficiente, por tres causas distintas (16)

**C‑población — el banco de pruebas es el equivocado, no el perk (4).** Confianza **alta** en los cuatro;
sugerencia *no decidir todavía* (vuelve a medirse en su población).

| perk | intención | disparador · portador | expos. → mejor |
|---|---|---|---|
| `bulwark_stance` | (sin `_doc`) `tackle` +100 si lleva `Bulwark` | `MATCH_START` · Defensa | 7,5 % → **80 %** Dwarf |
| `back_to_back` | (sin `_doc`) `tackle` +100 con un `Bulwark` cerca | `TACKLE` · Defensa | 9,6 % → **90 %** Dwarf |
| `shadow_marker` | (sin `_doc`) `injure` +100 con un `Brute` cerca | `TACKLE` · Defensa | 24,6 % → **90 %** Orc |
| `grudge` | *"si le hacen una falta, se la devuelve"* | `FOUL` · Defensa | 10,4 % → **77,5 %** Orc |

**C‑rareza — se expone poco en toda población (4).** Puede ser rareza intencionada; el instrumento no lo
distingue de un problema. Confianza **media**; *no decidir todavía*.

`iron_gate` (`INJURY`, Dwarf, 22,1 %, factor 1,0×) · `iron_price` (*"cada rival que rompe se lo cobra su
propio cuerpo"*, `INJURY`, 5,0 % → 7,5 %) · `second_wound` (letal RF-093 vía 2, `INJURY` + `scoreDiff<=0`,
13,3 % → 25 %) · `game_management` (sin `_doc`, `TACKLE` + `scoreDiff>0`, 30,8 % → 47,5 %).

**C‑potencia — se expone de sobra, el efecto no se separa del ruido (5).** Confianza **media**;
*no decidir todavía*.

`kamikaze` (*"entra a todo aunque se rompa"*, 100 %, Δ`tacklesPerMatch` −0,150) · `own_third_anchor` (sin
`_doc`, 100 %, −0,050) · `last_ditch` (sin `_doc`, 53,7 %, +0,150) · `line_keeper` (*"nunca sale de su
área"*, 100 %, territorial −2,628 en ruido **pero goles en contra +0,055**, indicio de signo contrario a
su beneficio declarado — indicio, no conclusión) · `sweeper_keeper` (`RECOVERY`, 56,2 %, territorial
+0,112; es `common` y de duración `play`, así que ser pequeño es coherente).

**C‑sin instrumento — `SCREENING_PASS` vacíos (3).** Confianza **alta** en que **no sabemos nada**;
*no decidir todavía*.

`blood_scent`, `bloodhound`, `free_man`: los tres son `TargetSelection`, exposición 100 %, y pasan por el
motivo literal *"sin parámetro numérico"*. Su métrica primaria es **"distribución de a quién se
marca/entra (bespoke)"**, que **no está implementada**. No se ha medido nada de su comportamiento.
`free_man` es además el único perk del catálogo que toca la IA del equipo rival.

---

## Parte C — respuestas al diseñador

**1. ¿Qué perks tienen evidencia limpia para decidir?**
Tres, y ninguno del modo esperado: **`deep_run`** (efecto medible y contrario a su intención),
**`double_shot`** (mecanismo demostrado; el problema es el portador) y **`charge`** (hace lo que dice).
`deep_pivot` casi, con confianza baja. **Tres de veinticuatro.**

**2. ¿Cuáles no deben juzgarse todavía?**
Los cinco de **B** — `steamroller` (roto), `cannon` y `high_line` (medidos con una métrica que no
representa su eje), `double_shot` (portador), `shadow` (implementación divergente) — más los tres
`SCREENING_PASS` vacíos, cuya métrica nunca se implementó. **Ocho perks cuyo diseño el auditor no ha
llegado a tocar.**

**3. ¿Cuáles presentan discrepancia concreta entre intención y comportamiento?**
- **`deep_run`**: dice que juega más arriba; el equipo juega 4,3 puntos más atrás. Medible.
- **`shadow`**: el texto promete altura relativa al vinculado; el código hace un desplazamiento absoluto.
  Discrepancia **de implementación**, verificada en el código, no medida en el campo.
- **`line_keeper`**: indicio de que encaja *más* goles cuando su beneficio declarado es no quedar mal
  parado. Dentro del ruido: indicio, no hallazgo.

**4. ¿Cuáles son candidatos a conservar?**
`charge` y `deep_pivot` por evidencia coherente. `double_shot` por mecanismo demostrado, con la reserva
de que hoy se le da al portador equivocado. **No hay ningún otro con evidencia para sostener la
candidatura**, lo que no es un argumento en contra: es ausencia de datos.

**5. ¿Qué NO podemos concluir?**
- Que 19 `INSUFFICIENT_EVIDENCE` sean 19 perks malos. Solo uno (`steamroller`) está demostrado roto.
- Que los 4 `SCREENING_PASS` estén bien. Ninguno se midió.
- Nada sobre los perks que dependen de etiquetas de estilo hasta medirlos en su raza.
- Nada sobre `cannon` hasta que exista una métrica de distancia de tiro.
- Nada sobre `steamroller` ni `shadow` hasta que su mecanismo sea el que su texto describe.
- **Nada sobre potencia relativa entre perks**: ninguna medición de este informe compara perks entre sí.

## Dos observaciones estructurales

**Siete de los 24 no tienen `_doc`** — `own_third_anchor`, `last_ditch`, `game_management`, `iron_gate`,
`shadow_marker`, `bulwark_stance`, `back_to_back`. Para ellos la pregunta *"¿corresponde el comportamiento
a la intención?"* **no se puede formular**: no hay intención escrita contra la que contrastar.

**El portador por defecto es un Defensa en 18 de los 24.** `TeamGenerator.StarterPositions[1]` es
`Defender`, y el arnés elige el primer titular de campo elegible. Todo perk sin `positionOnly` se mide
sobre un Defensa. Es la causa raíz común de `double_shot` y `cannon`, y sesga cualquier perk cuyo
mecanismo dependa de disparar o de atacar.
