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
