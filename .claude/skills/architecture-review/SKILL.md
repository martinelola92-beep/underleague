---
name: architecture-review
description: Revisar un cambio que toca una frontera de proyectos (/Sim <-> /Game), introduce una abstracción nueva, o añade una primitiva de motor — antes de implementarla. Usar cuando el cambio no es "un dato más" sino "una forma nueva de representar algo". Si hay duda de si algo es arquitectura o solo dato, invocar de todos modos (misma regla de precedencia que game-design-review).
---

# Revisar una decisión de arquitectura antes de implementarla

## Primero: ¿ya existe el patrón?

**Antes de diseñar una abstracción nueva, busca si el proyecto ya tiene una para el mismo problema.** La
propia auditoría de organización cometió este fallo: propuso un registro de hipótesis nuevo cuando
`docs/decisiones/` (un fichero por ADR + índice) ya era el patrón correcto, aplicable por analogía a
`docs/pendientes/`. Antes de proponer algo nuevo, pregunta explícitamente: *¿hay ya una convención en este
repositorio que resuelve un problema con la misma forma?*

## El protocolo

1. **¿Qué frontera toca?** `/Sim -> /Game` nunca al revés (RT-011); `/Sim` sin E/S (RT-012); el render
   consume eventos, nunca decide (RT-014). Si el cambio hace que `/Game` "interprete" la simulación de un
   modo que pueda contradecirla, es un fallo de arquitectura, no un detalle de implementación.
2. **¿Elimina complejidad real o la mueve?** Toda abstracción nueva se justifica contra esta pregunta
   (regla 20 del encargo de auditoría). Si la respuesta es "la mueve a otro fichero", no se crea.
3. **¿Determinismo?** Aritmética entera fuera de posiciones (RT-023), orden explícito ante empates
   (RT-041/RT-097), nada de `Dictionary`/`HashSet` sin ordenar si afecta al resultado, ningún generador de
   aleatoriedad no sembrado (RT-021). El test RT-024 tiene que seguir pasando.
4. **¿Paralelismo?** Si toca `/Balance` o las puertas: `Parallel.For` por índice con semilla función pura
   del índice, cada hilo con **su propio** `Catalog` (`/Sim` no es reentrante — `CompiledCondition` guarda
   contexto en la instancia). Aceptación: salida byte a byte idéntica a la secuencial.
5. **Efectos de segundo orden.** ¿Qué otro sistema usa la misma representación y puede sufrir el mismo
   patrón? (Ejemplo real: `LeavePitch → (-1,-1)` afecta a cualquier lectura de posición en el tick de
   salida — lesión, muerte, sustitución, expulsión — no solo al caso que lo hizo visible.) Busca la
   abstracción común antes de arreglar cada síntoma por separado.
6. **¿Rompe una prueba multiplataforma?** RT-024 corre en CI en Windows y Linux; un cambio de orden o de
   fuente de aleatoriedad que "funciona en WSL" puede divergir allí.

## Qué NO hace

- No aprueba una abstracción por elegante que sea si el punto 2 no tiene una respuesta concreta.
- No sustituye a `game-design-review` cuando el cambio también es una decisión de mecánica de juego —
  las dos se encadenan, no compiten.
