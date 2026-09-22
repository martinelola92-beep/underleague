# 0125 — La entrada sin balón tiene métrica propia y bono por puesto

Estado: **Aceptada** (22 sep 2026, **decisión del revisor**, ficha `docs/pendientes/BE-A.md`). Cambia una
regla de juego y un instrumento de medida, así que pasa por `game-design-review` (esta nota) y exige
`balance-measure` antes de cerrarse. **Enmienda la ADR 0105 §2.**

## Problema

`Utility.IsDefensiveRole` admitía **solo** `Position.Defender`, mientras su propio comentario decía
«defensa y centrocampista» citando la ADR 0105 §2. El revisor pide que **todos** los jugadores de campo
puedan entrar a su marcado sin balón, con los defensas y los más agresivos como los más probables.

**Medido antes de decidir nada** (500 partidos, semilla 1, contra el control de la misma semilla):

| configuración | `tacklesPerMatch` | `injuriesPerMatch` | reparto por puesto (entradas/partido-jugador) |
|---|---|---|---|
| **base: solo defensa** (el código) | **12,01** | **0,81** | DEF 1,95 · MID 0,64 · FWD 0,20 |
| DEF + MID (lo que la ADR 0105 **declaraba**) | 23,86 | 1,23 | MID **2,76** · DEF 1,73 |
| todos los roles | 27,64 | 1,34 | MID **2,74** · FWD 2,29 · DEF 1,64 |
| todos, bono 40 | 17,37 | 1,04 | MID 1,62 · FWD 1,44 · DEF 1,18 |
| todos, enfriamiento 540 (36 s) | 18,24 | 0,96 | MID 1,74 · FWD 1,49 · DEF 1,20 |

**Dos hallazgos, y los dos contradicen la intuición de partida:**

1. **La regla que el comentario describía habría roto el juego.** DEF+MID da 23,86 entradas contra un techo
   de 14. La implementación más estrecha del código es lo que estaba sosteniendo la banda.
2. **El orden sale invertido en todas las variantes que incluyen al centrocampista.** No lo arregla bajar el
   bono ni subir el enfriamiento. La causa no es el peso de `Tackle` sino **con qué compite y a quién
   marca**: el defensa tiene `CoverSpace` 420 y `Retreat` 320 disputándole la decisión y marca a un
   delantero que suele estar fuera de la jugada activa; el centrocampista marca a otro centrocampista, que
   está casi siempre dentro.

## El error de medida que había debajo

`MatchEngine` **ya distingue** las dos entradas —`TackleOffBall`, `OffBallFoulBase`, enfriamiento propio
(`OffBallTackleCooldownTicks`), detalles de evento `offBallFoul`/`offBallMissed`— pero
**`_report.Tackles++` se incrementa en las dos ramas** (`:2201` y `:2209`), así que `tacklesPerMatch` las
suma juntas.

Y `tacklesPerMatch` tiene banda 6-14 **como métrica de fútbol**: mide disputar el balón. *Golpear a quien no
lo lleva no es disputarlo.* Medir las dos cosas con la misma regla hacía que cualquier apertura de la
entrada sin balón saliera «fuera de banda» por construcción, midiera lo que midiera.

## Decisión

### D1 · La entrada sin balón se mide aparte

- `tacklesPerMatch` pasa a contar **solo la entrada al portador**. Conserva su banda 6-14 de RT-056, que es
  la que se calibró para eso.
- Se añade **`offBallTacklesPerMatch`**, al principio como `INFO` **sin banda**: no se inventa un rango
  antes de tener la distribución. La banda se fija en una ADR posterior, con datos.
- El motor no cambia: la información ya existe, solo deja de perderse al agregar.

*Esto no es un ajuste de balance: es arreglar el instrumento. Cualquier lectura anterior de
`tacklesPerMatch` que incluyera entradas sin balón mezclaba dos poblaciones.*

### D2 · El bono de entrada sin balón pasa a ser por puesto

Hoy `tackleMarkTargetBonus` es **un entero plano** que se suma **fuera** del multiplicador de puesto y
rasgo. Por eso un delantero con `Tackle` base 128 recibe el mismo empujón que un defensa con 255, y por eso
la diferenciación que el revisor pide **no puede salir**: el bono plano la borra.

Pasa a ser un **mapa por puesto**, igual que ya lo es `base` en `data/ai/weights.json` (`Goalkeeper`,
`Defender`, `Midfielder`, `Forward`). Es la única estructura del fichero que ya está parametrizada por
puesto, así que no se inventa un patrón: se aplica el que hay.

La fantasía que expresa, en palabras del revisor: **«tú no pasas de aquí y no vas a participar en la
jugada, puñetazo.»** Es una acción de **negación**, no de recuperación — y por eso merece su propia métrica
(D1) y su propio reparto (D2).

### D3 · Los tres roles de campo pueden, el portero no

`IsDefensiveRole` deja de ser una guarda por rol y pasa a decidirlo el dato: un puesto con bono 0 no entra
nunca. El portero queda fuera por `IsOutfield` —no tiene marca asignada—, no por esta guarda.

## Las diez preguntas, en corto

1. **Qué experimenta**: un jugador que no puede llegar al balón golpea al rival al que marca. Observable,
   con falta propia y sanción propia.
2. **Qué decide hoy**: nada — era invisible y solo del defensa.
3. **Qué debería decidir**: alinear a un agresivo en una banda concreta es aceptar que repartirá leña ahí.
4. **Qué regla representa**: enmienda la ADR 0105 §2. RF-057 sigue acotándola (`IsInActivePlay`).
5. **Sistemas**: `/Sim` (`Utility`, `MatchMetrics`), `/data` (`ai/weights.json`, su esquema). Nada en `/Game`.
6. **Alternativas descartadas**: subir el enfriamiento (medido: a 540 ticks, 36 s, sigue fuera de banda y
   sigue invertido) · bajar el bono plano (satura en 17,4) · dejarlo solo en defensa (pierde la intención).
7. **Trade-off**: más violencia legible a cambio de presupuesto de lesiones, que es el recurso más escaso.
8. **Estrategias**: da al centrocampista un papel en la fase sin balón, que es un hueco abierto del
   proyecto (dispara el 6,5 % de los tiros siendo el 43 % de los jugadores de campo).
9. **Degeneración**: el riesgo real es `injuriesPerMatch` (0,81 con techo 0,90) y, detrás, `deathsPerRun`.
   **Es la restricción que manda, y no es un artefacto de medida**: las entradas sin balón que son falta
   pasan por `ResolveInjury` como cualquier otra.
10. **Cómo se demuestra**: reparto por puesto con el orden pedido (defensa por delante), las siete métricas
    obligatorias `IN` con la definición nueva, y las 43 puertas medidas **contra una línea base tomada con
    el instrumento ya corregido** — no contra las cifras anteriores, que mezclaban poblaciones.

## Riesgo declarado

**`injuriesPerMatch` es la restricción que decide si esto cabe.** Con todos los roles abiertos está en 1,34
contra un techo de 0,90, y **eso no lo arregla D1**. Si con el bono por puesto calibrado no cabe, las
salidas son, por este orden: reducir el bono hasta que quepa, dejar al delantero en 0, o abrir una ADR para
recalibrar la banda —lo que arrastra `deathsPerRun` y es decisión del revisor, no de esta nota.

## Hermanos

`docs/pendientes/BE-A.md` · ADR 0105 (enmendada) · `docs/analisis/auditoria-conceptual-narrativa.md` §E
