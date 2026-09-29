# ADR 0157 — La apuesta del vestuario sustituye al partido excelente

Fecha: 27 sep 2026 · Estado: **propuesta** (aceptada en su forma por el revisor; pendiente de medir las
cuotas antes de implementar). **Decisión del revisor**: *«pondría uno aleatorio antes de cada encuentro, si
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
| `thrashing` | Paliza | ganar por 3 o más **y** que el rival acabe con 6 o menos en el campo | violencia más ataque |
| `three_names` | Tres firmas | ganar con goles de tres jugadores distintos | reparto de tiro, no concentrar la build |
| `youth_decides` | El chaval decide | el gol de la victoria es de un canterano | alinear canteranos arriba |
| `short_and_clean` | Pocos y limpios | ganar en inferioridad y a portería a cero | la apuesta más cara: alinear a 5 o 6 a propósito |
| `referee_blind` | El árbitro no se entera | ganar con 3 o más faltas propias **no señaladas** | árbitro tuerto o permisivo, perks sucios |

Las cuatro del partido excelente quedan absorbidas (`thrashing`, `short_and_clean`, `youth_decides`) o
desaparecen (portería a cero sola, demasiado simple).

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
| `thrashing` | gana por ≥3 y el rival termina con ≤6 en el campo (jugadores rivales con tiempo de campo y sin `LeftPitchTick`: cuenta suplentes que entraron y descuenta bajas sin reemplazo) |
| `three_names` | gana con goles no anulados de ≥3 jugadores propios distintos |
| `youth_decides` | gana y el gol de la victoria es de un canterano. «Gol de la victoria» = el gol propio número (goles del rival + 1): con 3-1, el segundo. Es el primer gol tras el cual el equipo ya no deja de ir por delante |
| `short_and_clean` | gana, sale con <7 titulares (casillas de la alineación con la que se jugó, no los suplentes que entran) y no encaja |
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
