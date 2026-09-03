# 0004. RNG propio PCG32 con flujos derivados

**Fecha:** 2026-09-03
**Estado:** Aceptada
**Requisitos:** RT-021, RT-022, RT-060, RT-061

## Contexto

`System.Random` no garantiza la misma secuencia entre versiones de .NET, lo que rompería guardados, repeticiones y la futura Copa diaria. Hacen falta flujos independientes para partido, mapa y recompensas. El documento propone xoshiro256 o PCG32.

## Decisión

PCG32 (estado de 64 bits, salida de 32 bits, unas 20 líneas). Los flujos se derivan de la semilla de la run con splitmix64 sobre `(semilla, tipoDeFlujo, indice)`. Toda la API devuelve enteros.

## Alternativas descartadas

- xoshiro256**: igual de válido; PCG32 se elige por salida de 32 bits, que encaja con la aritmética entera, y por facilidad de derivar flujos (`stream` explícito).
- `System.Random` con semilla: inestable entre versiones.

## Consecuencias

Los tests fijan vectores conocidos de PCG32 para detectar cambios accidentales de implementación.
