# 0006. Sin ECS

**Fecha:** 2026-09-03 (registra una decisión del documento de requisitos v0.9)
**Estado:** Aceptada
**Requisitos:** RT-051, §9

## Contexto

14 jugadores y un balón. El lote de 10.000 partidos debe correr en menos de 60 s.

## Decisión

Objetos y listas ordenadas. Sin ECS.

## Alternativas descartadas

- Friflo, Arch: se reconsiderarían solo si el alcance cambiara radicalmente (cientos de entidades).

## Consecuencias

Si `/Balance` no alcanza RT-051, el primer sospechoso son las asignaciones por tick, no la ausencia de ECS.
