# 0022. Roles derivados de la colocación y comportamiento sin balón

**Fecha:** 2026-09-04
**Estado:** Aceptada (decisión del revisor; implementación pendiente)
**Requisitos:** RF-022b, RF-041..045, RT-089..096

## Contexto

La IA de utilidad de la fase 0 reparte a los jugadores sin balón con cuatro acciones genéricas (`MarkOpponent`, `OfferSupport`, `CoverSpace`, `Retreat`) y pesos por posición. Falta lo que hace reconocible un partido de fútbol: **nadie busca espacio**. `OfferSupport` apunta a un punto geométrico fijo por delante del portador, sin mirar dónde hay hueco; `MarkOpponent` toma al rival más cercano y puede cambiar de objetivo cada decisión; y no existe la presión a la salida del balón.

El revisor pidió explícitamente: delanteros buscando huecos cerca del área rival; centrocampistas ofensivos buscando hueco para recibir; defensas cerrando huecos y marcando al delantero; centrocampistas defensivos intentando robar; y presión a la salida del balón.

## Decisión

1. **El rol efectivo se deriva de la posición nominal y de la columna de la casilla-hogar**, no de un campo nuevo. Cinco roles internos: `Goalkeeper`, `Defender`, `DefensiveMidfielder`, `AttackingMidfielder`, `Forward`; un centrocampista colocado en columna ≤ 3 es defensivo y en columna ≥ 5 ofensivo. **RF-022b no se toca**: siguen existiendo cuatro posiciones; el rol es una derivación interna documentada. Efecto de diseño buscado: la casilla en la que colocas a un jugador cambia lo que hace, no solo dónde empieza.
2. **Pesos por rol** en `data/ai/weights.json`, sustituyendo a los pesos por posición (RT-093 sigue cumpliéndose: la posición determina los pesos base, a través del rol).
3. **Acciones nuevas**: `FindSpace` (moverse al mejor hueco disponible para recibir) y `PressCarrier` (presionar al poseedor o al portero rival en su salida). `MarkOpponent` pasa a usar **asignación estable**: el emparejamiento defensa→atacante se calcula por rol y se mantiene mientras siga siendo válido, en vez de recalcularse cada tick.
4. **Evaluación de espacio**: cada jugador que decide `FindSpace` puntúa un conjunto pequeño y fijo de puntos candidatos dentro de su correa (8 direcciones), con términos enteros: distancia al rival más cercano, avance hacia la portería rival y línea de pase abierta con el poseedor. Los cuerpos de la ADR 0020 hacen que "hueco" sea un concepto físico real.

## Alternativas descartadas

- **Subroles explícitos como dato del jugador** (`AttackingMidfielder` como posición propia): amplía RF-022b, obliga a decidir el subrol en el fichaje en vez de en la colocación, y resta importancia a la cuadrícula.
- **Pathfinding o mapas de influencia completos**: coste desproporcionado para 14 jugadores y un balón, y difícil de depurar frente a una tabla de utilidad volcable (RT-098).

## Consecuencias

- Hay que rehacer los pesos de `data/ai/weights.json` y revalidar RT-056 (se agrupa con las ADR 0020 y 0021 en un solo reajuste).
- El volcado de utilidad (RT-098) gana filas: los candidatos de `FindSpace` con su puntuación, que es la única forma razonable de depurar "por qué se ha ido ahí".
- La colocación pasa a ser una decisión con dos capas: dónde empieza el jugador y qué rol adopta. Refuerza UI-020 (la pantalla de Equipo concentra las decisiones).
