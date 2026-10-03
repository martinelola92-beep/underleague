# 0190 — Un carnicero se cobra una vida por partido (BX-20)

Fecha: 3 oct 2026 · Estado: **aceptada** · Requisitos: RF-012c, RF-012d, RF-013, RF-093, RT-035, RT-057 ·
Ficha: [BX-20](../pendientes/BX-20.md) · Desarrolla la [ADR 0048](0048-morir-estando-sano.md) (condiciones 1 y 5);
respeta la banda de la [ADR 0170](0170-el-desgaste-es-de-la-run-y-la-run-es-mas-corta.md) · Toca AY-A sin cerrarla

## Problema

El revisor perdió dos jugadores en los minutos 7 y 10 contra Yunque Verde, los dos a manos del mismo orco (Urzag
Tragaclavos, `skullsplitter`), y cinco en la run. Medido (BX-20, 300 runs, semilla 1, política que lee el ojeo):

- La media está en banda (**0,154** muertes por partido; banda 0,11-0,22), pero la **cola** no: el **14,3 %** de las
  runs termina con 4 o más muertos y el 50,7 % con ninguno.
- El **37 %** de todas las muertes son la segunda (o tercera, o cuarta) del **mismo matador en el mismo partido**.
  Contra Partecráneos: **1,75 muertos por partido** y dos o más en el **57 %** de los partidos.
- El ojeo avisa de **una** víctima por portador (`Lethality.MarkedRisks`, un marcado por activación) y con una sola
  tirada: 35 % de media al más expuesto contra Partecráneos. El motor, en cambio, tira **en cada entrada** del
  portador (`limit: null`, disparador `TACKLE`, unas 7 activaciones por partido) y nunca deja de tirar.

La condición 1 de la ADR 0048 («se sabe antes») se cumplía para el primer muerto y no para los siguientes: nada en el
ojeo decía que el mismo jugador podía matar a tres. No es «Sed de médula» (`marrow_thirst`): reparte más muertes en
total, pero de una en una (7 % de partidos con dos).

## Decisión

**Un mismo portador de perk letal se cobra como mucho una vida por partido.** Nuevo dato
`tuning.injury.lethality.killsPerCarrierPerMatch: 1`. Alcanzado el tope el portador **sigue tirando, con
probabilidad 0** (el flujo de dados no cambia, RT-021), y su perk sigue haciendo todo lo demás (la cuota de lesión de
la jugada): la carnicería sigue, la segunda muerte no. Cuenta `MatchPlayer.DeathsCaused`, que sólo sube con una
muerte efectiva de un rival: una muerte anulada (`no_dying`) no gasta el tope. 0 = sin tope, el comportamiento
anterior y el control de la medición.

La ficha del perk lo dice (RT-035, plantilla `lethalContactRisk`): «puede matar al rival implicado en la jugada,
aunque esté sano; una sola vida por partido». Un test ata el texto al dato.

**Y para no salir de la banda de la ADR 0170**, `marrow_thirst.lethalChance` **900 → 1800** (ver Medición).

## Revisión de diseño (game-design-review)

1. **Qué experimenta el jugador.** Antes: el ojeo nombra a Urzag y un riesgo del 35 % en un jugador, y mueren dos o
   cuatro. Ahora: Urzag puede matar a uno; el ojeo dice quién lleva el perk y la ficha dice «una sola vida».
2. **Qué decide.** Cuántos muertos puede costar un partido se lee contando portadores en el ojeo; entrar o rodear el
   nodo (condición 2) pasa a ser un cálculo que se puede hacer.
3. **Qué debería decidir.** Lo mismo, más a quién exponer: eso sigue igual (resistencia, estado, cercanía).
4. **Regla.** RF-093 vía 2 y RF-012d. Es regla nueva («una vida por portador y partido»); no estaba escrita.
5. **Sistemas.** `/data` (`tuning.json`, plantilla), `/Sim` (`EffectEngine`, bloque de víctimas letales;
   `MatchEngine.LethalQuotaSpent`). `/Game` no cambia: el texto sale de la plantilla.
6. **Alternativas.** (a) Bajar `skullsplitter.lethalChance`: baja la media pero conserva el patrón —el mismo jugador
   sigue matando en serie, sólo que menos a menudo— y la cola sigue ahí. (b) Tope por partido y equipo: castiga al
   segundo portador por lo que hizo el primero, y no se lee en el ojeo. (c) Escalar la muerte con la gravedad del
   aviso: es AY-A y cambia el número que ve el jugador; decisión mayor. (d) Reforzar el aviso: se hace en la ficha; el
   número del ojeo queda en AY-A. El tope por portador es lo único que rompe el patrón y hace exacto un recuento.
7. **Trade-off.** Un carnicero propio (build de violencia con letales) también mata menos: uno por partido y
   portador. Las builds de violencia medidas en las puertas (`orc_violence`) no llevan letales; `orc_butchery` sí.
8. **Estrategias.** Un portador que ya mató sigue siendo un pegador (la cuota de lesión no se apaga): el riesgo de
   lesión grave sigue tras la muerte. Sacrificar a un suplente para «gastar» al carnicero es posible y es una
   decisión legible —y cara—: carnicería administrada.
9. **Degeneración.** Un equipo con muchos portadores sigue pudiendo matar a varios: está acotado por el número de
   portadores, que el ojeo enseña. Compensar con `marrow_thirst` (el letal más repartido, actos 2-3) concentra más
   muertes en partidos con un aviso visible.
10. **Demostración.** Test de valor conocido con control (`ALethalCarrierClaimsOneLifePerMatch`: 1 muerte con el dato,
    3 con el valor viejo), test de ficha, y el lote de abajo con política que lee y que no lee.

## Medición

Sonda de BX-20 (`RunPolicy.Play` con observador), 300 runs por celda, todas las razas, doctrina contextual. «Lee»
= `HeedsLethalScouting` (alinea con el indicador); «no lee» = sin ojeo ni indicador. Ninguna de las dos rodea
nodos peligrosos: la política no evita partidos (condición 2 sin medir, ver Riesgos).

Semilla 1 = runs 1..300, semilla 2 = runs 1001..1300. «base» = antes de esta ADR; «tope» = sólo el tope;
«final» = tope + `marrow_thirst` 1800. El control con `killsPerCarrierPerMatch: 0` reproduce byte a byte las runs
del código anterior (40 runs comparadas).

| política | celda | muertes/partido (s1 / s2) | muertes/run | runs con ≥ 4 | runs con ≥ 3 | partidos con ≥ 2 muertos | `runWinRate` |
|---|---|---|---|---|---|---|---|
| lee | base | 0,154 / 0,132 | 1,37 / 1,19 | 14,3 / 11,7 % | 21,7 / 19,0 % | 3,9 / 3,4 % | 13,3 / 12,0 |
| lee | tope (`marrow` 900) | **0,095** / — | 0,86 | 3,3 % | 10,3 % | 0,07 % | 16,3 |
| lee | tope + `marrow` 1500 | 0,113 / **0,101** | 1,03 / 0,92 | 4,3 / 4,7 % | 12,7 / 11,7 % | 0,07 / 0 % | 17,7 / 15,7 |
| lee | **final** (`marrow` 1800) | **0,128 / 0,108** | 1,17 / 0,98 | **7,7 / 5,3 %** | 18,0 / 13,0 % | 0,04 / 0 % | 20,3 / 14,0 |
| no lee | base | 0,159 / 0,149 | 1,43 / 1,35 | 16,3 / 15,0 % | 25,3 / 22,0 % | 4,0 / 3,7 % | 15,0 / 14,3 |
| no lee | tope (`marrow` 900) | 0,104 / — | 0,94 | 4,3 % | 11,7 % | 0,04 % | 19,3 |
| no lee | **final** | **0,132 / 0,116** | 1,17 / 1,05 | **7,3 / 3,7 %** | 16,7 / 14,0 % | 0,04 / 0,07 % | 19,3 / 19,0 |

Error típico de las muertes por run en cada celda: 0,06-0,11 (≈ 0,007-0,01 por partido). Entre semillas la base
se mueve 0,02 por partido: la diferencia entre semillas es mayor que la de cualquier par de celdas vecinas, por eso
se eligió el valor con la **media** de las dos (0,118) dentro de la banda y no el que roza el suelo en una.

Por rival (semilla 1, lee): contra Partecráneos **1,75 → 0,87** muertos por partido (dos o más: 57 % → 1 %);
`marrow_thirst` solo 0,37 → 0,48; `iron_studs` + `marrow_thirst` (Reyes de Hierro) 1,10 → 0,79. Sin portador
letal, 0,004-0,006 (la vía de la lesión grave alineada). La probabilidad de que muera **alguien** contra
Partecráneos no cambia (84 %): el tope corta la serie, no la primera muerte.

**`marrow_thirst` sube**, y el revisor sospechaba de él. La sonda lo absuelve del patrón (sus muertes llegan de una
en una) y es el único letal que reparte en actos 2 y 3 sin concentrar: subirlo pone las muertes que el tope quita
en partidos **distintos**, cada uno con su portador en el ojeo. Contra él muere alguien en el 28 % → ~40 % de los
partidos. La alternativa —aceptar 0,095-0,101 y bajar el suelo de la banda— es posible (la ADR 0170 la llama
provisional) pero esta ADR no la toma: la cifra de la ADR 0048 se respeta.

**Las dos políticas siguen sin separarse** (0,128/0,108 frente a 0,132/0,116): la alineación casi no mueve la
muerte, como ya midió la ADR 0048. Lo que cambia para quien no lee el ojeo es la cola: de un 15-16 % de runs con
cuatro o más muertos a un 4-7 %.

**Puertas** (`Category=Gate`, una invocación): 42 de 48. Las seis rojas son previas: cuatro están en
`tools/puertas-rapidas.base` (curva de jefes, `orc_violence` y `elf_brawler` en las de build) y
`betterTeamWinRate_human_60_vs_human_40` da **90,36 idéntico** con los datos viejos (`killsPerCarrierPerMatch` 0,
`marrow_thirst` 900). Ni las builds de las puertas de fase 1 ni `human_none` llevan letales, así que el tope no se
ejecuta en ellas: `orc_violence` no se mueve por esta ADR. `deathsPerMatch` de la puerta de run, roja con sólo el
tope, vuelve a verde. Suite rápida 1.939/1.939. RT-024: las huellas no cambian (sus partidos no tienen una segunda
muerte del mismo portador), no hubo que renovarlas.

## Riesgos

- **El número del ojeo sigue sin ser exacto (AY-A).** Contra Partecráneos enseña ~35 % al más expuesto y la
  probabilidad de que muera alguien es ~84 %. Con el tope la escala del error baja de «1,75 muertos» a «uno», pero
  el porcentaje sigue por debajo. Es la siguiente decisión de diseño de esta zona.
- **La condición 2 no se mide**: `RunPolicy.ChooseNode` no mira al rival. Lo que dice esta ADR sobre la política
  «que no lee» vale para quien entra en todos los partidos.
