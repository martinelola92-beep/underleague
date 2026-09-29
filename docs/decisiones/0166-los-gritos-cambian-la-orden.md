# ADR 0166 — Los gritos del entrenador cambian la conducta, no sólo las probabilidades

Fecha: 29 sep 2026 · Estado: **aceptada**. Origen: revisión independiente de los consumibles (BA-H, 29 sep):
*«¡A por él!», «¡Aguantad!» y «¡Arriba!» son multiplicadores de éxito invisibles; sus nombres prometen una
conducta que no producen*. Principio del proyecto: **comportamiento observable > modificadores numéricos
invisibles**. **Requisitos:** RF-082, RF-084, RT-014, RT-096. **Relacionada:** ADR 0154 y 0156 (la orden
táctica Defensa · Neutro · Ataque, que mueve líneas y éxito), BA-H.

## Decisión

1. **Efecto nuevo de consumible: `shout`** (grito), con una orden (`Defensive`, `Offensive`) o una
   **consigna** (`Press`: todo el equipo presiona al portador) y una **duración en segundos** de juego.
   Mientras dura, el equipo juega con esa orden o consigna; al acabar, vuelve a la orden que tenía. Se
   aplica por el mismo sitio que la orden táctica en vivo (ADR 0154/0156): líneas, pesos de utilidad y
   multiplicadores de éxito ya existentes, sin inventar otra vía.
2. **Los tres gritos pasan a usarlo**: «¡Aguantad!» = defensiva 10 s; «¡Arriba!» = ofensiva 10 s; «¡A por
   él!» = presión al portador 6 s (más peso a perseguir y entrar al portador en los pesos de utilidad del
   equipo, en datos). Duraciones provisionales, sin medir. Se quitan sus multiplicadores invisibles.
3. **Se ve**: el tablero enseña el grito activo y su cuenta atrás junto a la orden; la pizarra de líneas se
   mueve como con la orden; al terminar, vuelve.
4. Determinismo: la activación es estado inicial del partido (`ManualActivation` en un tick), como hasta
   ahora; el motor aplica y retira el grito por ticks.

## Las diez preguntas (`game-design-review`, resumidas)

Qué ve el jugador: su equipo se echa atrás, se vuelca o muerde durante unos segundos cuando grita. Qué
decide: cuándo gritar (con ventaja, en los últimos minutos, cuando el rival tiene la pelota en su campo). Regla:
RF-082 (el manual, ahora con conducta). Sistemas: efecto nuevo en `/Sim` (efecto de consumible y el motor
por ticks, reutilizando la orden), datos de los tres gritos, `/Game` tablero. Alternativas: dejarlos como
multiplicadores (lo que se critica). Trade-off: un grito ofensivo destapa la defensa, como la orden. Degeneración:
gritos encadenados: son de un solo uso por partido (RF-085). Cómo se demuestra: tests de que durante la
duración la orden efectiva es la del grito y después vuelve; lote de partidos con y sin grito en el mismo tick
(goles a favor y en contra en la ventana), captura del tablero con el grito activo.

## Implementación (29 sep 2026)

- **Dato** (`data/consumables/*.json`, esquema `consumables.schema.json`): efecto `{ "type": "shout", "order":
  "Defensive"|"Offensive", "seconds": N }` o `{ "type": "shout", "press": true, "seconds": N }` —exactamente
  uno de `order` y `press`; sin `value`, `target`, `attribute` ni `probability`—. `seconds` 1..30 (el techo, 30 s,
  es provisional: un partido dura 60-90 s). Sólo lo admiten los consumibles; un perk con `shout` es un error de
  carga. La descripción sale de la plantilla (`shoutDefensive`/`shoutOffensive`/`shoutPress`, es/en, RT-035).
- **Motor**: `EffectType.Shout` (`Sim/Perks`). `EffectEngine.ResolveConsumables` llama a
  `MatchEngine.StartShout(equipo, tipo, ticks)` al activarse el consumible (`ticks = seconds × 15`), una vez
  por consumible (RF-085). `_context.Order` sigue siendo **la orden efectiva** que leen las líneas
  (`mentalityShift`, ADR 0154), las cuotas (`mentalityOdds`, ADR 0156) y la utilidad (`mentality`, ADR 0140):
  un grito de orden la pisa, y `_baseOrder` guarda la del jugador. Nada nuevo por el que entrar: es la misma
  vía que un `OrderChange`.
- **Presión** (`data/ai/weights.json`, `press`): multiplicador porcentual por acción que `Utility.Choose`
  **multiplica** dentro del producto de la mentalidad (`base × táctico × mentalidad × presión × rasgos`). Regla
  I: se leyó `Utility.cs` antes —la mentalidad y el táctico multiplican, sólo `modifyUtility` de perk **suma**—,
  así que `250` es ×2,5 y no +250 %. Valores provisionales, sin calibrar: `ChaseBall` 150, `PressCarrier` 250,
  `Tackle` 150. Un 0 anularía la acción, y el cargador exige positivo.
- **Interacción con `OrderChange` del jugador durante un grito**: la orden que el jugador pulsa **no corta el
  grito ni se pierde**: cambia la orden de **base** en su tick y la efectiva sólo cuando el grito acaba, que es
  a la que se vuelve («la que haya puesto entretanto», no la que había al gritar). Un segundo grito de
  orden sustituye al primero; la consigna de presión es independiente de la orden y puede convivir con ella.
  La urgencia de marcador (ADR 0140) sigue mezclando sobre la orden efectiva, como con una orden normal.
- **Determinismo**: la activación es el `ManualActivation` de siempre (estado inicial); activar en T no cambia
  nada antes de T (test, para los tres gritos); mismo partido dos veces, mismos eventos.
- **`/Game`**: `MatchShoutView` (`Sim/Run/View`) lee los `CONSUMABLE_USED` y la definición para dar los gritos
  activos y su cuenta atrás sin volver a simular; el tablero pinta la orden **efectiva** en oro (con un aro la
  del jugador, a la que se vuelve) y, tras el consumible usado, una placa «¡Aguantad! · 8 s» con una barra
  que se vacía. Captura: `Game/screenshots/grito-{hold_the_line,push_forward,after_him}.png`
  (`-- gritos` en `CapturasRetransmision.tscn`).

## Medición (provisional, sin revisión de balance)

1.200 partidos `TestMatches.Reference`, mismas semillas con y sin grito, activado en el tick 300 por el local
(`ShoutTests.EachShoutMovesTheBehaviourInTheWindowTheWayItsNamePromises`, puerta). Por partido, equipo local:

| | goles + / − en la ventana | tiros | entradas | goles + / − después |
|---|---|---|---|---|
| ninguno, 10 s | 0,098 / 0,078 | 0,374 | 0,705 | 0,720 / 0,740 |
| `hold_the_line` (Defensiva 10 s) | 0,065 / 0,063 | 0,313 | 0,422 | 0,701 / 0,750 |
| `push_forward` (Ofensiva 10 s) | 0,104 / 0,093 | 0,393 | 1,214 | 0,699 / 0,758 |
| ninguno, 6 s | 0,060 / 0,042 | 0,222 | 0,443 | 0,758 / 0,776 |
| `after_him` (presión 6 s) | 0,047 / 0,043 | 0,229 | 0,974 | 0,750 / 0,813 |

- **Los goles en la ventana no discriminan** (LIKELY, no CONFIRMED): con 0,05-0,10 goles por partido en 6-10 s,
  el error típico de cada celda es ~0,008-0,010, del orden de las diferencias. La única lectura firme es la de
  **conducta**: `hold_the_line` tira menos (−16 %) y entra menos (−40 %); `after_him` casi **duplica las
  entradas** (0,44 → 0,97) sin mover los tiros; `push_forward` tira algo más (+5 %) y entra **más** (0,71 →
  1,21), algo que no se esperaba de una orden ofensiva (posible efecto de tener la defensa adelantada: más
  contactos en campo rival; sin aislar).
- **Sin efecto sostenido después de la ventana** dentro del ruido (0,70-0,76 / 0,74-0,81 frente a 0,72 / 0,74),
  que es lo que se pedía: el grito acaba y el equipo vuelve a jugar como antes.
- La puerta sólo afirma la conducta (entradas con la presión, tiros de «¡Arriba!» frente a «¡Aguantad!»); las
  cifras de goles se imprimen y no se afirman. **Pendiente para el balance**: duraciones y multiplicadores de
  presión, y si las faltas de la presión (más entradas) son un coste legible (RF-012d).
