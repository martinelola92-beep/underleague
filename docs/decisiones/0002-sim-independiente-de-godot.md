# 0002. `/Sim` como librería pura sin Godot

**Fecha:** 2026-09-03 (registra una decisión del documento de requisitos v0.9)
**Estado:** Aceptada
**Requisitos:** RT-010..RT-015

## Contexto

El balanceo exige ejecutar decenas de miles de partidos sin abrir el editor. La reproducibilidad exige que nada de la presentación influya en el resultado.

## Decisión

`/Sim` es una librería .NET sin referencia a Godot, sin E/S y sin reloj. Recibe estado + semilla y devuelve eventos + estado final. `/Game` consume eventos. El movimiento se implementa con vectores propios, no con el motor de físicas.

## Alternativas descartadas

- Simular dentro de Godot con `_PhysicsProcess`: ata el lote a la ventana y al reloj de Godot.
- Addons de IA de Godot (LimboAI, Beehave): viven en el árbol de nodos de Godot, incompatibles con RT-011. Pueden usarse en `/Game` para presentación si hace falta.

## Consecuencias

Un test de arquitectura vigila la frontera. El consumible manual durante el partido se resuelve por re-ejecución (ver `arquitectura.md`), no por mutación externa del simulador.
