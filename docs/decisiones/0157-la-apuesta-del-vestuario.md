# ADR 0157 — La apuesta del vestuario sustituye al partido excelente

Fecha: 27 sep 2026 · Estado: **en implementación** (pasos 1-3 hechos el 29 sep 2026: catálogo, censo, estado de run,
decisión, cobro y política; falta la interfaz, paso 4). **Decisión del revisor**: *«pondría uno aleatorio antes de cada encuentro, si
quieres lo tomas y si quieres lo dejas […] me parece bien que se gane más o no se gane nada si pierdes la
apuesta. Tiene que ser cosas más complejas que "el delantero marca gol"».*
**Enmienda** RF-114h (partido excelente) y **RF-114i** (el oro no escala con el rendimiento).
**Requisitos:** RF-012d, RF-114g, RF-114h, RF-114i, RF-114k, RT-021, RT-022, RT-035, RT-057
**Relacionada:** ADR 0037 (la economía es la dificultad), ADR 0048 (las cinco condiciones), ADR 0100 (la
carta de evento: la apuesta es la elección, no el dado), ADR 0124 (carrera y atribución: de ahí salen los
hechos que resuelven la apuesta), ADR 0154/0156 (la orden táctica: una de las palancas para ganarla)

## El problema

El **partido excelente** (RF-114h, `ExcellentMatchObjectives`) ya es un objetivo **aleatorio por nodo**,
anunciado y determinista por `(semilla, nodo)`. Pero tiene tres defectos:

1. **Paga 1 de oro** (`excellentMatchBonusGold`) cuando una victoria de liga paga 9-13. Nadie cambia una
   decisión por 1 de oro, así que el objetivo no se lee.
2. **No se elige.** Se cumple o no se cumple; no hay nada que aceptar ni que arriesgar.
3. **Cuatro objetivos de resultado** (ganar por 3, portería a cero, en inferioridad, canterano goleador):
   ninguno habla de la identidad del juego, que es la carnicería.

## Decisión

**El partido excelente se sustituye por la apuesta.** No conviven dos sistemas para lo mismo (la pregunta 5
de `CLAUDE.md`: hay un sistema hermano y es este).

1. **Una apuesta por nodo de partido** (liga, élite y jefe), derivada de `RngStreams.Rewards(semilla,
   nodo)` como hoy el objetivo excelente: **no se guarda**, se deriva, y la interfaz la puede enseñar sin
   resolver nada (W-12). Se ve en el ojeo y en el cartel del nodo.
2. **Se toma o se deja** en la pantalla de ojeo o alineación, antes de confirmar el partido. Tomarla cuesta
   una **apuesta fija** en oro que depende del acto.
3. **Se resuelve al terminar el partido** con los hechos del partido (`RunMatchSummary`, su `Report` y los
   créditos de la ADR 0124), **nunca con una tirada nueva**: la incertidumbre es el propio partido.
   - Cumplida: cobra **apuesta × cuota**, además del oro normal del partido.
   - Fallida: se pierde la apuesta. No hay otra penalización.
4. **La cuota se deriva de la frecuencia medida** de cada apuesta en el lote de campaña, con un margen de la
   casa: `cuota ≈ margen / p`. Ninguna cuota se escribe a ojo (Regla H). Hasta medirlas, todas son
   **provisionales, sin medir**.
5. **Condición de diseño, y es la que separa esto de una lotería:** toda apuesta tiene que poder
   **inclinarse** con una decisión del jugador —alineación, orden táctica, consumible, perk—. La esperanza de
   quien apuesta a ciegas es **negativa**; la de quien prepara el partido para ganarla, **positiva**. Una
   apuesta que ninguna decisión mueve no entra en el catálogo.

### Enmienda de RF-114i

> El oro nunca escala con el rendimiento dentro del partido, **salvo por la apuesta**: un riesgo elegido y
> pagado **antes** del partido, con la condición y la cuota a la vista. Lo que se prohíbe sigue prohibido:
> que un gol o una lesión paguen oro por sí solos, sin compromiso previo.

La diferencia de fondo es que la apuesta **cuesta** si falla. El oro por rendimiento que RF-114i quería
evitar era un premio sin riesgo que hacía que ir ganando te hiciera ganar más.

## El catálogo inicial (datos, `data/bets/`)

Condiciones **compuestas**, no «el delantero marca». Cada una declara qué la inclina.

| id | nombre | condición | se inclina con |
|---|---|---|---|
| `blood_before_goals` | Sangre antes que goles | la primera lesión del partido (de cualquiera) llega antes que el primer gol | Brutos arriba, orden defensiva |
| `hunt_the_star` | Cazar a la estrella | el jugador rival de mayor rareza (con nombre en la apuesta) no termina el partido | perks de entrada, colocar a tu Bruto en su banda |
| `eye_for_eye` | Ojo por ojo | si te lesionan a uno, les lesionas a uno en el mismo partido, y ganas | agresivos en el once, consumible sucio |
| `comeback` | La remontada | ir perdiendo en algún momento y ganar | orden ofensiva a tiempo, consumible de marcador por debajo |
| `into_the_mob` | Hasta la turba | se llega al gol de oro y lo ganas | builds de violencia, el consumible que provoca la turba |
| `clean_hands` | Manos limpias | ganar causando al menos una lesión y sin ninguna tarjeta propia | perks que mitigan al árbitro (RF-064f), soborno |
| `thrashing` | Paliza | ganar por 3 o más *(redefinida el 29 sep: la versión «y el rival con 6 o menos» daba 0,43 %)* | violencia más ataque |
| `split_the_goals` | Reparto | ganar con goles de al menos **dos** jugadores propios distintos *(antes `three_names`, tres jugadores: 0,40 %)* | reparto de tiro, no concentrar la build |
| `youth_decides` | gana y algún gol no anulado es de un jugador propio canterano |
| `referee_blind` | El árbitro no se entera | ganar con 3 o más faltas propias **no señaladas** | árbitro tuerto o permisivo, perks sucios |

Las cuatro del partido excelente quedan absorbidas (`thrashing`, `youth_decides`) o desaparecen (portería a
cero sola, demasiado simple; y `short_and_clean`, ver abajo).

**`short_and_clean` («Pocos y limpios») queda descartada** (29 sep 2026, decisión del coordinador): solo se da si el
jugador alinea en inferioridad a propósito, y como apuesta que se le ofrece al azar es una lotería (**frecuencia
medida 0,01 %**: 3 aciertos en 33.325 partidos, porque la política nunca sale con menos de siete). Una
apuesta casi imposible viola el punto 5. Se recupera si algún día hay una forma legible de ofrecerla solo a quien
alinea a 5-6.

**Regla de oferta (29 sep 2026, decisión del coordinador):** una apuesta **no se ofrece** en una dificultad donde su
frecuencia medida sea menor del **2 %** (`BetSystem.MinOfferedBasisPoints`; el 2 % es del revisor, provisional).
La frecuencia se guarda en datos junto a la cuota (`frequencyPercentByDifficulty`).

**Pregunta abierta para medir:** si `referee_blind` y `clean_hands` son legibles para el jugador sin la
traza. Si el informe post-partido no enseña «faltas no señaladas», `referee_blind` no entra.

## Definiciones exactas de las condiciones (implementación, 29 sep 2026)

`BetConditions.Evaluate(kind, contexto)` es puro y lee solo los hechos del partido (`MatchSetup`,
`MatchResult`: eventos ordenados e informe). Equipo del jugador = 0 (W-15). Convenciones comunes: un evento con
`Detail` acabado en `:cancelled` (lo anuló un perk) **no ocurrió**; «ganar» es `Report.Winner == 0`; «lesión» es
un `INJURY` no anulado (leve o grave) con la víctima en `Actor` y el causante en `Opponent`; «tarjeta» es un
`CARD` (amarilla o roja).

| condición | se cumple si |
|---|---|
| `blood_before_goals` | el primer `INJURY` de cualquiera va antes que el primer `GOAL`; sin goles, basta que haya habido una lesión. No exige ganar |
| `hunt_the_star` | el rival nombrado tiene un `INJURY`, un `DEATH` o una `CARD` roja (no anulados). No exige ganar. El nombrado es `BetSystem.TargetFor`: el de mayor rareza entre los rivales de la alineación, desempate por id menor |
| `eye_for_eye` | gana, hay ≥1 lesión propia y ≥1 lesión de un rival con `Opponent` = jugador propio |
| `comeback` | gana y, tras algún gol no anulado, el marcador iba en contra (rival > propio) |
| `into_the_mob` | gana y hubo `MOB_START`. **Incluye** ganar por desempate al agotarse la prórroga sin gol de oro (medido en el test: la semilla 1 de 50 contra 50 termina así) |
| `clean_hands` | gana, ≥1 lesión de un rival atribuida a un jugador propio y 0 tarjetas propias (amarillas o rojas) |
| `thrashing` | gana por ≥3 goles de diferencia |
| `split_the_goals` | gana con goles no anulados de ≥2 jugadores propios distintos |
| `youth_decides` | gana y el gol de la victoria es de un canterano. «Gol de la victoria» = el gol propio número (goles del rival + 1): con 3-1, el segundo. Es el primer gol tras el cual el equipo ya no deja de ir por delante |
| `referee_blind` | gana con ≥3 `FOUL` propios de detalle `unseen` **antes** de `MOB_START` (en la turba el motor emite todas las faltas como no vistas, y ahí no hay árbitro) |

La oferta se deriva con `OfferStream.For(semilla, nodo, 8000)` (desplazamiento nuevo en la tabla de
`OfferStream`), uniforme sobre las apuestas del catálogo ordenadas por id.

## Las diez preguntas (`game-design-review`)

1. **Qué experimenta el jugador.** Antes del partido, un corredor le ofrece «Grok Comecráneos no acaba el
   partido: 4 de oro, cobras 15». Después, el pregón le dice si ha cobrado.
2. **Qué decide.** Tomar o dejar la apuesta, y **sobre todo cómo alinear** para ganarla: la apuesta tira de
   la alineación hacia un sitio que el rival solo no habría pedido.
3. **Qué debería decidir.** Lo mismo. El riesgo real es que se tome siempre (esperanza positiva sin
   preparar) o nunca (cuotas malas). Se mide con `betsTakenPerRun` y `betNetGoldPerRun`, separando la
   política que prepara el partido de la que apuesta a ciegas.
4. **Qué regla representa.** RF-114h reescrita y RF-114i enmendada (arriba). Regla nueva: la apuesta.
5. **Sistemas.** `/Sim`: `Run/Systems/Economy` (sustituye `ExcellentMatchObjectives` por un `BetSystem`
   que resuelve desde `RunMatchSummary`); `RunPolicy` (tomar o dejar); estado: **solo la apuesta aceptada
   del nodo pendiente** (un id y la cantidad), así que el guardado sube de versión. `/data`: `data/bets/` con
   esquema. `/Game`: ojeo o alineación (ofrecer y aceptar), informe (resolución). Ninguna lógica de
   resolución en `/Game` (RT-014).
6. **Alternativas.** (a) Mantener el partido excelente subiendo la paga: sigue sin elección. (b) Apuestas
   libres, eligiendo entre varias: más control, pero siempre se elige la más fácil y el revisor pidió una
   sola y aleatoria. (c) Apostar contra ti mismo: incentivo perverso a jugar mal, descartada.
7. **Trade-off.** El oro apostado no se gasta en el mercado ni en la clínica, y la alineación que gana la
   apuesta puede no ser la que gana el partido con más margen (una apuesta de violencia expone a tu once).
8. **Estrategias.** Da un motivo de oro a las builds de violencia (`hunt_the_star`, `into_the_mob`) que hoy
   solo tienen el soborno como sostén (RF-064e), y hace que la orden táctica y los consumibles tengan
   objetivo en partidos sin peligro.
9. **Degeneración.** (a) Bola de nieve: se limita con la apuesta **fija** por acto, no proporcional al oro.
   (b) Una apuesta casi segura contra rivales débiles: la cuota se calcula por dificultad del rival, no solo
   por tipo. (c) Choca con la familia del oro parado de la ADR 0100, que grava lo que llevas encima: es
   deseable, porque las dos castigan acumular oro sin usarlo. (d) RF-012d: la apuesta no introduce daño; si
   el jugador expone a su once para cobrar, lo ha elegido.
10. **Cómo se demuestra.**
    - Censo de frecuencia de cada condición en el lote de campaña, por dificultad del rival: de ahí salen
      las cuotas.
    - Filas nuevas `betsTakenPerRun` y `betNetGoldPerRun` (esta última por política: a ciegas < 0, preparada
      > 0).
    - Las puertas de economía (`brokeMarketRunShare`, `leftoverGoldShare`, `sinksAffordablePerAct`) dentro
      de banda.
    - Test: la misma semilla y el mismo nodo ofrecen la misma apuesta (W-12). Test por condición con
      partidos construidos.

## Lo que queda fuera

- Rivales que apuestan (RF-015b habla de sobornos y consumibles, no de apuestas).
- Apuestas ligadas al némesis («Venganza: X no acaba el partido»). Encaja, pero depende de que el némesis
  exista.

## Censo (29 sep 2026, segunda medición)

**Qué se midió.** `Balance --bet-census 1200 --seed 1` y `--seed 7` (semilla de la run `i` = `seed*100000+i`,
para que los dos lotes no compartan runs): 2.400 runs completas con la política **contextual** de `RunPolicy`
(doctrina de apuesta `Never`), repartidas entre las razas de lanzamiento. En **cada** partido de liga, élite y
jefe se evaluaron las diez condiciones (todas, no solo la que se habría ofrecido), incluido el partido que
termina la run (`IRunSystems.OnMatchPlayed`). Instrumento validado antes de la medida (Regla J): el
observador ve exactamente los `Matches` de cada run y no la altera (`BetCensusTests`), la salida con
`DOTNET_PROCESSOR_COUNT=1` es byte a byte la del paralelo, y las condiciones concuerdan con un oráculo
independiente en 600 partidos reales (`BetConditionsRealMatchTests`).

**Cambios respecto a la primera medición** (misma tanda de 2.400 runs, semillas 1 y 7): las tres apuestas casi
imposibles se redefinieron (`thrashing`, `split_the_goals`, `youth_decides`), `short_and_clean` se retiró, y
`excellentMatchBonusGold` (1 de oro) desapareció, lo que mueve levemente las trayectorias de run: las
frecuencias de las apuestas que no cambiaron difieren de la primera medición dentro de ~1-2 errores típicos.

**Muestra.** 33.211 partidos: 10.434 / 9.550 / 9.392 / 2.806 / 1.029 por distintivo de dificultad 1..5 (por
encima de los 200 exigidos en todas las celdas). Las dos semillas coinciden: |z| entre ellas ≤ 2,2 en las 50
celdas apuesta×dificultad (el máximo se da en una celda de `p` pequeña). Error típico **por conglomerados de
run** (los partidos de una misma run no son independientes), no binomial.

**Frecuencia medida `p` en % (± error típico).** Está también en `data/bets/bets.json`
(`frequencyPercentByDifficulty`, sin el error). **En negrita, las celdas por debajo del 2 %: no se ofrece.**

| apuesta | d1 | d2 | d3 | d4 | d5 | todas |
|---|---|---|---|---|---|---|
| `blood_before_goals` | 14,32 ± 0,40 | 36,74 ± 0,58 | 47,46 ± 0,59 | 60,73 ± 0,94 | 50,92 ± 1,56 | 35,19 ± 0,37 |
| `hunt_the_star` | 4,90 ± 0,23 | 8,92 ± 0,33 | 12,11 ± 0,39 | 12,97 ± 0,65 | **0,49 ± 0,22** | 8,64 ± 0,20 |
| `eye_for_eye` | **1,03 ± 0,10** | 7,87 ± 0,29 | 12,11 ± 0,37 | 18,60 ± 0,76 | 9,62 ± 0,92 | 7,88 ± 0,18 |
| `comeback` | 6,25 ± 0,24 | 6,70 ± 0,26 | 7,20 ± 0,27 | 6,81 ± 0,47 | 4,66 ± 0,66 | 6,65 ± 0,14 |
| `into_the_mob` | 17,09 ± 0,37 | 19,98 ± 0,41 | 18,38 ± 0,41 | 15,07 ± 0,67 | 13,31 ± 1,06 | 18,00 ± 0,21 |
| `clean_hands` | 13,02 ± 0,36 | 23,26 ± 0,50 | 21,92 ± 0,50 | 22,77 ± 0,83 | 16,62 ± 1,16 | 19,42 ± 0,31 |
| `thrashing` | 11,11 ± 0,34 | 7,31 ± 0,29 | 4,58 ± 0,24 | 4,81 ± 0,41 | 2,92 ± 0,52 | 7,38 ± 0,18 |
| `split_the_goals` | 10,74 ± 0,31 | 8,35 ± 0,30 | 6,94 ± 0,27 | 8,87 ± 0,54 | 5,64 ± 0,72 | 8,66 ± 0,17 |
| `youth_decides` | **0,00** | **0,77 ± 0,10** | **1,80 ± 0,16** | 2,71 ± 0,30 | 6,03 ± 0,74 | 1,15 ± 0,07 |
| `referee_blind` | 4,77 ± 0,26 | 4,10 ± 0,23 | 4,07 ± 0,26 | 3,96 ± 0,37 | 3,11 ± 0,54 | 4,26 ± 0,18 |

**Cuotas resultantes** (`payoutPercentByDifficulty`: cobro **bruto** en % de la apuesta, `round(85 / p)`,
margen de la casa 15 %). `*` = **tope de 2.000 %** porque `85 / p` lo supera (no es una cuota medida); `—` = no
se ofrece en esa dificultad (`p < 2 %`; en datos queda el tope 2.000 sin efecto).

| apuesta | d1 | d2 | d3 | d4 | d5 |
|---|---|---|---|---|---|
| `blood_before_goals` | 594 | 231 | 179 | 140 | 167 |
| `hunt_the_star` | 1.736 | 953 | 702 | 655 | — |
| `eye_for_eye` | — | 1.079 | 702 | 457 | 883 |
| `comeback` | 1.360 | 1.268 | 1.181 | 1.249 | 1.822 |
| `into_the_mob` | 497 | 425 | 463 | 564 | 638 |
| `clean_hands` | 653 | 365 | 388 | 373 | 511 |
| `thrashing` | 765 | 1.163 | 1.857 | 1.767 | 2.000 * |
| `split_the_goals` | 791 | 1.019 | 1.224 | 958 | 1.508 |
| `youth_decides` | — | — | — | 2.000 * | 1.411 |
| `referee_blind` | 1.781 | 2.000 * | 2.000 * | 2.000 * | 2.000 * |

La cifra entera es más precisa que la medida: el error relativo de cada cuota es el de su `p` (de ~3 % en las
celdas grandes a ~12 % en `youth_decides` d5 y ~18 % en las de `p` pequeña). En las celdas con tope, el margen
de la casa es **mayor** del 15 % (`youth_decides` d4 devuelve 54 % de lo apostado, `referee_blind` ~80 %):
cotización por debajo de la justa, a revisar si se quiere subir el tope.

**Qué NO mide.** La frecuencia de fondo de la política automática, que **no prepara** la apuesta. Es la
línea de base de quien apuesta a ciegas (su retorno esperado es 0,85 por construcción en las celdas sin
tope); lo que la decisión del jugador (alineación, orden, consumible, perk) suma sobre esa `p` es justo el
margen que el punto 5 exige y **no está medido**. Población: solo los partidos que la política contextual
alcanza (sesgo de supervivencia: d4 y d5 son élites y jefes de los actos 2-3).

**Candidatas a revisar** (decisión del revisor; ninguna se ha tocado):

- **`hunt_the_star` en dificultad 5 (jefe del acto 3): 0,49 %** frente a 5-13 % en d1-d4. Causa **no
  investigada** (LIKELY: el rival de mayor rareza de un jefe es un jugador que casi no cae; sin experimento
  propio). Por la regla del 2 % no se ofrece contra ese jefe, que es lo que se quería evitar.
- **`youth_decides`** solo se ofrece en d4-d5 y con cuota tope en d4: depende de que haya canteranos en el once
  (la política los ficha poco). Es la más condicionada a una decisión de plantilla.
- **`blood_before_goals`** es una moneda al aire en d3-d5 (47-61 %, cuota 140-179 %) y su frecuencia depende
  sobre todo del desgaste por acto (ADR 0043), no de nada que el jugador decida: la candidata más «gratuita».
- **`referee_blind` y `comeback`** (3-7 %, casi planas en dificultad): cuota alta que no sube con el rival; queda
  la pregunta abierta de la sección anterior (si el informe enseña las faltas no señaladas).

**Primera medición (descartada, para el registro).** Con las definiciones anteriores: `thrashing` 0,43 %,
`three_names` 0,40 %, `youth_decides` 1,06 % y `short_and_clean` 0,01 % (3 aciertos en 33.325 partidos), todas
con cuota en el tope 2.000 % y retorno esperado a ciegas de ~8 %.

## Censo tras la revisión independiente (29 sep 2026. tercera medición)

Repetido con las correcciones de la revisión (la estrella de `hunt_the_star` es un jugador de campo. las
muertes cuentan como lesión en las condiciones. cobro redondeado). `Balance --bet-census 1200` con las
semillas 1 y 7. sumadas: 33.211 partidos (10.434 / 9.550 / 9.392 / 2.806 / 1.029 por dificultad 1-5).
Frecuencia en % y. entre paréntesis. la cuota resultante `85/p` con tope 2.000 %. Son las cifras que lleva
`data/bets/bets.json`. **La sección «Censo (segunda medición)» de arriba queda superada**: su
`hunt_the_star` medía una mezcla con porteros (0.49 % contra el jefe final; ahora 8.65 %).

| apuesta | d1 | d2 | d3 | d4 | d5 |
|---|---|---|---|---|---|
| `blood_before_goals` | 14,38 (591 %) | 38,03 (223 %) | 49,49 (172 %) | 61,48 (138 %) | 50,92 (167 %) |
| `clean_hands` | 13,05 (651 %) | 23,28 (365 %) | 21,94 (387 %) | 22,77 (373 %) | 16,62 (511 %) |
| `comeback` | 6,25 (1360 %) | 6,70 (1268 %) | 7,20 (1181 %) | 6,81 (1249 %) | 4,66 (1822 %) |
| `eye_for_eye` | 1,03 (2000 %) | 8,77 (969 %) | 13,04 (652 %) | 19,00 (447 %) | 9,62 (883 %) |
| `hunt_the_star` | 6,60 (1287 %) | 13,06 (651 %) | 15,98 (532 %) | 17,78 (478 %) | 8,65 (983 %) |
| `into_the_mob` | 17,09 (497 %) | 19,98 (425 %) | 18,38 (463 %) | 15,07 (564 %) | 13,31 (638 %) |
| `referee_blind` | 4,77 (1781 %) | 4,10 (2000 %) | 4,07 (2000 %) | 3,96 (2000 %) | 3,11 (2000 %) |
| `split_the_goals` | 10,74 (791 %) | 8,35 (1019 %) | 6,94 (1224 %) | 8,87 (958 %) | 5,64 (1508 %) |
| `thrashing` | 11,11 (765 %) | 7,31 (1163 %) | 4,58 (1857 %) | 4,81 (1767 %) | 2,92 (2000 %) |
| `youth_decides` | 0,00 (2000 %) | 0,77 (2000 %) | 1,80 (2000 %) | 2,71 (2000 %) | 6,03 (1411 %) |

Con la regla de no ofrecer si p < 2 %: `eye_for_eye` y `youth_decides` en d1, y `youth_decides` en d2-d3,
no se ofrecen. `referee_blind` y `youth_decides` quedan topadas en 2.000 % en casi todas las dificultades:
su esperanza a ciegas es menor que el 85 % del resto.

## Revisión independiente (29 sep 2026)

**Corregido:** la estrella de `hunt_the_star` era el portero entre el 20 y el 40 % de las veces (100 %
contra el jefe final) por desempatar por id menor (Regla J); las condiciones ignoraban las muertes sin
`INJURY`; el cobro truncaba (esperanza 0,73-0,83 en vez de 0,85); una apuesta tomada podía resguardar oro
de los eventos que gravan un porcentaje (ahora se devuelve, registrada, al entrar en cualquier otro nodo, y
sólo se retira en el mapa); `into_the_mob` decía «gol de oro» y contaba también el desempate; la
estrella nunca es el portero; `IRunSystems.Bets` ya no tiene implementación por defecto (un envoltorio que
no la reenvíe no compila).

**Nota de diseño:** elegir **qué** apuesta tomar con la información visible (el árbitro, los canteranos, lo
igualado del partido) es una decisión legítima del jugador y puede darle esperanza positiva. Lo que no se
admite es un resquicio sin decisión, como el del portero.

**Anotado sin corregir:** la doctrina `Prepared` no está medida; el censo usa la población de la política
automática, que casi no ficha canteranos, así que la regla del 2 % oculta `youth_decides` en d1-d3 por la
política y no por el diseño; Blind contra Never no se ha repetido tras las correcciones; la apuesta fija
3/4/5 sigue provisional.

## Implementación del paso 3 (29 sep 2026)

- **Estado.** `RunState.Bet` (`AcceptedBet`: id de apuesta, nodo, cantidad, cobro %, jugador nombrado y su
  nombre), null si no hay; solo se guarda la **tomada**, la ofrecida se deriva (W-12). `CurrentSchemaVersion` 6 → 7
  (`data/schemas/run-save.schema.json` gana `bet`, obligatorio, objeto o null; un guardado de la 6 se rechaza con
  error explícito, no se migra).
- **Decisión.** `TakeBet(NodeId)`: solo en el mapa, en un nodo de partido accesible que ofrezca apuesta, con oro
  suficiente, una por nodo; **se paga al tomarla**. `DeclineBet` la retira y devuelve lo apostado. Tomar otra para
  otro nodo devuelve la anterior, y **entrar en otro nodo de partido con una apuesta tomada para uno distinto la
  devuelve también** (solo se pierde jugando y fallando: el punto 3). No tomarla es no hacer nada.
- **Oferta.** `BetSystem.OfferFor(state, node, systems, catalog)` (mismo nombre a `hunt_the_star`: el rival que
  devuelve `systems.OpponentFor`, incluido el del jefe). Solo con la regla del 2 %.
- **Resolución.** En `RunEngine.ResolveMatch`, justo tras `MatchResolution.Apply` y con el mismo `MatchSetup` y
  `MatchResult` que ya recibe `OnMatchPlayed`: cumplida, se ingresa `Payout = apuesta × cuota / 100` (bruto: la
  apuesta ya se pagó); fallida, nada. Se resuelve **también si el partido termina la run**, y antes de
  `AfterMatch`, así que el oro del partido y el de la apuesta son canales distintos. `RunMatchSummary.Bet`
  (`BetResult`: condición, apuesta, cobro, `Met`, `GoldPaid`, `Net`) y `PostMatchReport.Bet` lo exponen al informe.
- **Partido excelente retirado** (RF-114h enmendada, RF-114i enmendada en `docs/requisitos.md`): fuera
  `ExcellentMatchObjectives`, `excellentMatchBonusGold` (código, datos y esquema) y los campos `Objective*` de
  `GoldForWinBreakdown`. `Game/Screens/ReportScreen.cs` perdió solo la fila del objetivo para seguir compilando;
  quedan claves de texto huérfanas (`ui.report.goldObjective*`, `ui.objective.*`) para el paso 4.
- **Política.** `RunPolicyOptions.BetDoctrine`: `Never` (por defecto, no mueve las puertas), `Blind` (toma siempre
  que pueda pagar) y `Prepared` (toma solo si la build la favorece: **aproximación**, ≥3 titulares con rasgo
  Aggressive o Dirty → `hunt_the_star`, `eye_for_eye`, `blood_before_goals`; no coloca ni alinea; **sin medir**).
  `Balance --full-runs --bet-doctrine never|blind|prepared` añade `betsTakenPerRun`, `betNetGoldPerRun` y
  `betNetReturnPercent` por doctrina de compra, y `betsTaken`/`betNetGold`/`betStaked` a `runs.csv`.

### Medición: apostar a ciegas contra no apostar

`--full-runs 1200` con `--seed 1` y `--seed 7` (2 × 1.200 runs por doctrina de compra), `never` contra `blind`:

| | `never` (s1 / s7) | `blind` (s1 / s7) |
|---|---|---|
| apuestas tomadas por run (contextual) | 0 / 0 | 12,67 / 12,60 |
| **`betNetGoldPerRun`** (contextual) | 0 / 0 | **−8,46 / −9,13** |
| **retorno neto sobre lo apostado** (contextual) | — | **−17,6 % / −19,2 %** |
| ídem doctrina gastadora / ahorradora | — | −26,0 / −22,4 % y −25,8 / −18,6 % |
| `runWinRate` | 21,08 / 20,67 | 17,67 / 18,92 |
| `brokeMarketRunShare` | 10,83 / 11,08 | **45,42 / 42,50** (fuera de banda 10-25) |
| `leftoverGoldShare` | 8,73 / 8,75 | 7,24 / 7,33 |
| `sinksAffordablePerAct` | 2,72 / 2,72 | 2,71 / 2,72 |

Lectura: (1) **el oro neto a ciegas sale negativo**, como pedía la condición: −17,6 % y −19,2 % de lo apostado
(≈ −15 % por el margen de la casa, más allá por las celdas con tope, que pagan por debajo de lo justo; la de la
gastadora, −26 %, se aleja más y **no se ha investigado**). Las cuotas no están mal. (2) La consecuencia
económica es fuerte: quien apuesta en todos los nodos gasta ~48 de oro por run y llega **sin oro al mercado en
el ~44 % de las runs** (contra el 11 %), y pierde 2-3 puntos de tasa de victoria: la apuesta **compite** con el
mercado y la clínica, que es lo que se quería (ADR 0157 punto 7), pero el punto 9a (bola de nieve) se cumple y el
efecto contrario —que arruine— queda para el revisor: la apuesta fija 3/4/5 sigue **provisional, sin medir**
(Regla H). (3) Con `never` las puertas de economía no se mueven salvo por quitar el bonus excelente de 1 de
oro. `Prepared`, sin medir todavía.

## Enmienda del 2 oct 2026: Blind contra Never tras las correcciones, y lo que el juego dice contra lo que ocurre

**Instrumento (Regla J).** `RunPlayResult.BetCells` (tomadas, cumplidas y oro neto por condición × dificultad del nodo) y
`BetHitCensus` (`Sim/Analysis`); `Balance --full-runs --bet-doctrine blind|prepared|comeback-easiest` escribe `bet-hits.csv` (la tabla por palabra y por condición la calcula `tools/bet-aciertos.py`, ya no a mano) (medido, error típico
binomial, anunciado, oro neto). Validado en `Sim.Tests/Analysis/BetHitCensusTests.cs`: (a) valor conocido (10 tomadas, 4 cumplidas,
+7 de neto → 40 %, ET √(0,4·0,6/20)); (b) control: `Never` no deja rastro y en `Blind` las celdas suman exactamente
`betsTaken` y `betNetGold` de la run; (c) **el primer partido de la run es idéntico con `Never` y con `Blind`** (mismos eventos,
mismos ticks, 6 semillas): la oferta sale de su propio flujo (`OfferStream`, `BetSystem`) y tomarla solo mueve oro, **no consume el
RNG del partido**. Después del primer partido las dos ramas divergen, pero por el oro (otras compras), no por el RNG. La política
`Blind` **apuesta de verdad**: 7,82 ± 0,07 apuestas por run (0 runs sin apostar; antes de las correcciones eran 12,6: la regla del 2 %
y las celdas topadas ofrecen menos). La run de `Never` coincide con la de la ADR 0164 «con herrero» semilla a semilla (17,00 /
15,83 / 14,00 / 14,17 / 15,50 / 13,67), lo que prueba que `Never` sigue siendo la línea base, no que sea independiente de ella.

**Lote (reproducible).** Desde el commit `e5fc831` o posterior, por cada semilla `S` en 1, 1001, 2001, 3001, 4001, 5001:

```
dotnet run --project Balance -c Release --no-build -- --full-runs 600 --seed S --bet-doctrine never --out <dir>
dotnet run --project Balance -c Release --no-build -- --full-runs 600 --seed S --bet-doctrine blind --out <dir>
```

600 runs × 3 doctrinas de compra por celda; las métricas de run son las de la doctrina contextual (600 runs por semilla y rama),
**pareadas por semilla**; el censo de aciertos usa las 1.800 runs de `blind` de cada semilla (77.221 apuestas en total).

| contextual, por semilla (never / blind) | 1 | 1001 | 2001 | 3001 | 4001 | 5001 | diferencia pareada ± ET (6 semillas) |
|---|---|---|---|---|---|---|---|
| `runWinRate` % | 17,00 / 14,33 | 15,83 / 14,67 | 14,00 / 14,50 | 14,17 / 14,67 | 15,50 / 15,00 | 13,67 / 13,33 | **−0,61 ± 0,49** |
| oro final (`goldLeft`) | 14,65 / 12,04 | 15,66 / 12,22 | 13,77 / 10,64 | 14,08 / 12,21 | 13,39 / 12,05 | 14,18 / 11,57 | **−2,50 ± 0,32** |
| oro neto de la apuesta por run | 0 / −3,95 | 0 / −4,15 | 0 / −6,68 | 0 / −5,99 | 0 / −4,08 | 0 / −6,31 | **−5,19 ± 0,52** |
| oro apostado por run | 0 / 29,44 | 0 / 30,42 | 0 / 28,65 | 0 / 29,75 | 0 / 29,55 | 0 / 28,31 | 29,35 ± 0,31 |
| mercados sin oro por run | 0,16 / 0,58 | 0,15 / 0,54 | 0,15 / 0,57 | 0,13 / 0,57 | 0,12 / 0,62 | 0,11 / 0,53 | **+0,43 ± 0,01** |
| muertes por run | 1,26 / 1,26 | 1,37 / 1,35 | 1,27 / 1,27 | 1,19 / 1,20 | 1,19 / 1,24 | 1,38 / 1,17 | −0,03 ± 0,04 |

Victoria con las otras doctrinas de compra: gastadora −0,28 ± 0,58, ahorradora −0,69 ± 0,69. Retorno neto sobre lo apostado,
contextual: −5,19 / 29,35 = **−17,7 %** (el margen de la casa es −15 %).

**Cumplimiento medido contra lo anunciado.** La UI no muestra un porcentaje: muestra el precio (apuesta y cobro, exactos) y una
palabra por tramo de la frecuencia de `data/bets/bets.json` (`ScoutScreen.FrequencyKey`: «rara vez» < 5 %, «de vez en cuando»
5-12 %, «a menudo» 12-25 %, «casi la mitad» ≥ 25 %). Agrupando las celdas (condición × dificultad) por la palabra que llevan:

| palabra | tomadas | cumplidas | medido | anunciado (ponderado) | z |
|---|---|---|---|---|---|
| rara vez | 13.083 | 470 | 3,59 % | 4,27 % | −3,8 |
| de vez en cuando | 28.806 | 2.317 | 8,04 % | 8,04 % | 0,0 |
| a menudo | 29.458 | 4.660 | 15,82 % | 16,99 % | −5,4 |
| casi la mitad | 5.874 | 2.618 | 44,57 % | 46,38 % | −2,8 |

Por condición (todas las dificultades; medido / anunciado, %; oro neto por apuesta): `blood_before_goals` 34,79 / 35,55 (−0,42);
`clean_hands` 17,32 / 19,05 (−0,74); `comeback` 7,44 / 6,65 (−0,24); `eye_for_eye` 10,72 / 11,85 (−0,79); `hunt_the_star` 12,40 / 12,03
(−0,28); `into_the_mob` 16,04 / 18,06 (−0,92); `referee_blind` 3,69 / 4,29 (−1,15); `split_the_goals` 8,30 / 8,78 (−0,85);
`thrashing` 6,71 / 7,54 (−1,08); `youth_decides` 2,62 / 3,40 (−2,29; solo 956 tomadas). Todas pierden oro por apuesta, ninguna condición
gana a ciegas. Hay dos celdas con oro neto positivo con ≥ 100 tomadas, las dos en dificultad 1: `comeback` (7,96 % medido contra
6,25 %, +766 de oro en 2.888 apuestas, cobro de 1.360 %: ≈ +8 % de retorno) y `hunt_the_star` (+138 en 2.879, ≈ +1,6 %, ruido).
Tres celdas con ≥ 100 tomadas caen en **otra palabra** de la que llevan (`eye_for_eye` d3: 11,35 contra 13,04; `into_the_mob` d5:
7,65 contra 13,31 con 183 tomadas; `split_the_goals` d5: 1,65 contra 5,64 con 242 tomadas); en las tres el juego **sobrestima**
la frecuencia.

**Etiquetas (Regla F):**
- **CONFIRMED** (6 semillas × 600 runs, pareado): apostar a ciegas **pierde oro**, −5,19 ± 0,52 por run (−17,7 % de lo apostado), y
  **apostar a ciegas no es un exploit**: ninguna condición gana oro a ciegas de forma sistemática. Esto NO dice que no exista
  una apuesta explotable para quien elige: ver el hallazgo abierto de `comeback` en dificultad 1 más abajo. Apostar en todas partes deja sin oro al mercado
  cuatro veces más a menudo (0,14 → 0,57 mercados vacíos por run) y 2,5 menos de oro final.
- **CONFIRMED**: la apuesta no consume el RNG del partido (control (c)); `Never` es la línea base.
- **Sin efecto detectable en la victoria** a este uso: −0,61 ± 0,49 puntos (z −1,2; la cota a 2 ET es ≈ ±1 punto). Lo medido es
  el **uso** de una política ciega, no la ausencia de efecto: la ADR 0157 de antes decía «−2 a −3 puntos» con 2 semillas y 1.200 runs
  y las 6 semillas no lo sostienen (**la lectura previa era ruido o dependía de la versión previa a las correcciones**). El
  efecto en oro, sí, es robusto.
- **LIKELY**: el texto de frecuencia **es fiel dentro de su precisión de palabra**: lo medido está a 0-2 puntos de lo anunciado y
  sólo 3 celdas pequeñas cambian de palabra, siempre por sobrestimar. Pero hay un **sesgo sistemático a la baja**: las condiciones se
  cumplen ~5 % menos de lo anunciado (13,03 % contra 13,73 % global; z negativos en 3 de 4 palabras). Es **LIKELY** que se deba a
  que el censo de la ADR 0157 se hizo con la población de la política `Never` y `Blind` llega a los partidos con menos oro y menos
  compras (otra población); **alternativa, igual de plausible: que el censo base sea anterior a las correcciones** de la ADR
  (regla del 2 %, celdas topadas, portero fuera de `hunt_the_star`) y las frecuencias de `data/bets/bets.json` describan un juego
  que ya no es éste. No se ha aislado con un experimento (haría falta recensar con la versión actual, con `Never` y con `Blind`). Efecto sobre el
  jugador: el cobro es ≈ −17,7 % y no el −15 % anunciado en el diseño.
- **Lo que esto dice del diseño, sin vender de más.** Apostar sin criterio es una **decisión con coste**, no ruido: mueve oro
  (−5,2 por run, ~29 apostados) y compite con el mercado. Que apostar con criterio sea una decisión **con valor** NO está medido:
  `Prepared` sigue sin medir y la política no escoge por árbitro, canteranos ni igualdad (la nota de diseño de esta ADR dice que
  ahí estaría la decisión). El efecto nulo en victoria con 7,8 apuestas por run no prueba que no haya efecto en un jugador que
  apueste distinto, y las dos celdas positivas de dificultad 1 son un indicio de que hay dónde ganar escogiendo (`comeback` en
  dificultad 1: LIKELY, un contraste entre muchos).

**Hallazgo abierto: `comeback` en dificultad 1.** En el lote de `Blind`, 2.888 apuestas cumplen 7,96 % contra el 6,25 % anunciado
(z +3,8 con `tools/bet-aciertos.py`, por encima del umbral de Bonferroni de 3,27 para las 46 celdas con ≥ 100 tomadas; con otra
cuenta de z, +3,4: también por encima). El cobro de 1.360 % lo vuelve positivo (+766 de oro). Para dejar la cifra **medida** y no solo
derivada del censo, la política `ComebackOnEasiest` (`--bet-doctrine comeback-easiest`: toma solo `comeback` en dificultad 1;
validada en `BetHitCensusTests`) se jugó con las mismas 6 semillas × 600 runs: 0,278 ± 0,005 apuestas por run (0,83 de oro
apostado), **oro neto +0,044 ± 0,157 por run** (pareado contra `Never`; por semilla +0,00, −0,25, +0,30, −0,50, +0,14, +0,57),
victoria −0,28 ± 0,29 puntos, oro final +0,06 ± 0,15; cumplimiento 7,76 % en 2.849 apuestas (z +3,3 contra el 6,25 %, +514 de
oro, +0,18 por apuesta). Lectura: el cumplimiento sobre lo anunciado se repite, pero **es la misma población de semillas** (no una
réplica independiente) y el efecto en oro por run es indistinguible de cero (≈ +5 % de retorno sobre 0,83 de oro por run).
**LIKELY** que la celda esté infravalorada; **no probado** que sea explotable en la práctica con esta cuota (el beneficio es de
centésimas de oro por run con un estudio ciego a la build). Un jugador que escoge sí puede aprovechar una celda así: de ahí la
quinta decisión del revisor. Con el mismo criterio, `into_the_mob` en d2 y d3 queda fuera de Bonferroni por **sobrestimar**
(−3,5 y −3,4): el juego anuncia más de lo que da.

**Decisiones del revisor (RT-057), no se ajusta ningún número:**
1. **Cobro algo generoso o no.** El −17,7 % medido es 2,7 puntos peor que el −15 % de diseño porque las condiciones se cumplen un
   5 % menos de lo censado. Opciones: (a) dejarlo (el jugador sólo ve el cobro, no un porcentaje); (b) recensar con `Blind` y
   recalibrar `payoutPercentByDifficulty`; (c) subir el margen aceptado a ~−18 % (documentarlo).
2. **Sobrestimación en dificultad 5.** Las celdas `into_the_mob` d5 y `split_the_goals` d5 anuncian 13,3 % y 5,6 % y cumplen 7,7 % y
   1,7 %, con muestras pequeñas (183 y 242). Opciones: ignorarlo, ampliar el censo de dificultad 5, o no ofrecerlas en d5.
3. **Medir `Prepared`** (o una política que escoja por árbitro y canteranos) antes de afirmar que apostar es una decisión con valor.
4. La apuesta fija 3/4/5 sigue **provisional, sin medir** (Regla H).
5. **`comeback` en dificultad 1** (hallazgo abierto de arriba): opciones: (a) dejarlo (efecto de centésimas de oro por run);
   (b) recensar con la versión actual y recalibrar la cuota de esa celda; (c) no ofrecerla en d1; (d) un lote con una política que
   elija a quién apostar, para ver si la ventaja crece cuando hay criterio.

El «Blind contra Never no se ha repetido tras las correcciones» de «Anotado sin corregir» queda cerrado por esta enmienda.
