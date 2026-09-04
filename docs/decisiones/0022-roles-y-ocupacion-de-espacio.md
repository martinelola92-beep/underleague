# 0022. Comportamiento sin balón: contraste por estado táctico y búsqueda de espacio

**Fecha:** 2026-09-04
**Estado:** Aceptada (decisión del revisor; implementación pendiente)
**Requisitos:** RF-022b, RF-041..045, RT-089, RT-090, RT-092..096

## Contexto

La IA de la fase 0 reparte a los jugadores sin balón con cuatro acciones genéricas (`MarkOpponent`, `OfferSupport`, `CoverSpace`, `Retreat`) y pesos por posición modulados por el estado táctico del equipo. Falta lo que hace reconocible un partido de fútbol: **nadie busca espacio**. `OfferSupport` apunta a un punto geométrico fijo por delante del portador sin mirar dónde hay hueco; `MarkOpponent` toma al rival más cercano y puede cambiar de objetivo en cada decisión; y no existe la presión a la salida del balón.

Lo que el revisor pidió —delanteros buscando hueco cerca del área, centrocampistas que atacan cuando su equipo tiene el balón y defienden cuando lo tiene el rival, defensas que cierran y marcan, presión a la salida— **no requiere roles nuevos**: es exactamente el eje que RT-089 ya define en su capa 2, `TacticalState` (`InPossession`, `OutOfPossession`, `OffensiveTransition`, `DefensiveTransition`). El concepto existe; lo que falla es que su efecto es demasiado suave para leerse y que faltan acciones que le den contenido.

## Decisión

1. **El eje principal del comportamiento sin balón es posición × estado táctico**, con contraste marcado. Los multiplicadores actuales (±40% sobre el peso base) no bastan para que un centrocampista se comporte de forma distinta al atacar y al defender; se amplían hasta que la diferencia sea visible en el log y en el volcado de utilidad, y se ajustan con datos.
2. **Acciones nuevas**: `FindSpace` (moverse al mejor hueco disponible para recibir, sustituyendo el punto fijo de `OfferSupport`) y `PressCarrier` (presionar al poseedor o al portero rival en su salida). Con los cuerpos de la ADR 0020, "hueco" pasa a ser un concepto físico real.
3. **Marcaje con asignación estable**: el emparejamiento defensor→atacante se calcula una vez y se mantiene mientras siga siendo válido, en lugar de recalcularse en cada decisión. Un defensa prefiere emparejarse con un delantero rival.
4. **Evaluación de espacio**: el jugador que decide `FindSpace` puntúa un conjunto pequeño y fijo de puntos candidatos dentro de su correa (8 direcciones) con términos enteros: distancia al rival más cercano, avance hacia la portería rival y línea de pase abierta con el poseedor.

## Alternativas descartadas

- **Roles derivados de la columna de la casilla-hogar** (`DefensiveMidfielder` / `AttackingMidfielder` según dónde se coloque al jugador): propuesta inicial, descartada por innecesaria. Dos centrocampistas colocados en columnas distintas **ya** se comportan distinto por geometría (su casilla-hogar, su correa y los términos de contexto de la utilidad), y sus perfiles se diferencian con los **rasgos**, que RT-094 ya usa para modular pesos. Añadir un concepto de rol duplicaba mecanismos existentes.
- **Subroles explícitos como dato del jugador**: ampliaría RF-022b y trasladaría al fichaje una decisión que pertenece a la colocación.
- **Pathfinding o mapas de influencia completos**: coste desproporcionado para 14 jugadores y difícil de depurar frente a una tabla de utilidad volcable (RT-098).

## Consecuencias

- Hay que rehacer `data/ai/weights.json`: la tabla pasa a tener contraste real por estado táctico y dos acciones más. Revalidar RT-056 (se agrupa con las ADR 0020 y 0021 en un solo reajuste).
- El volcado de utilidad (RT-098) gana filas: los candidatos de `FindSpace` con su puntuación, única forma razonable de depurar "por qué se ha ido ahí".
- Riesgo a vigilar: con contraste alto, las transiciones pueden producir movimientos bruscos de todo el bloque. Los estados de transición (`OffensiveTransition`, `DefensiveTransition`) y el desplazamiento gradual del bloque existen precisamente para amortiguarlo.
