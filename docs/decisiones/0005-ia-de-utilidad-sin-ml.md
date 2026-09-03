# 0005. IA de utilidad ponderada, sin aprendizaje automático

**Fecha:** 2026-09-03 (registra una decisión del documento de requisitos v0.9)
**Estado:** Aceptada
**Requisitos:** RT-089..RT-098

## Contexto

El comportamiento debe ser determinista, depurable y ajustable desde datos por un equipo pequeño sin datos de entrenamiento.

## Decisión

Tres capas: máquina de estados del partido, estado táctico del equipo, máquina de estados del jugador. Dentro del estado del jugador, utilidad ponderada (`argmax` sobre una tabla de pesos) con pesos por posición y rasgo en `/data`. Las máquinas van en código en fases 0-1 y se migran a datos solo cuando el número de ajustes lo justifique.

## Alternativas descartadas

- ML / modelos entrenados: rompe determinismo, no hay datos, cada ajuste sería un reentrenamiento, imposible razonar sobre un desequilibrio.
- Árboles de comportamiento con librería: más abstracción de la necesaria para 9 acciones.

## Consecuencias

La tabla de puntuaciones por tick es volcable (RT-098) y es la herramienta de depuración principal.
