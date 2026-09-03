# 0007. Aritmética entera con float en posiciones; punto fijo aplazado

**Fecha:** 2026-09-03 (registra una decisión del documento de requisitos v0.9)
**Estado:** Aceptada
**Requisitos:** RT-023, RT-023b, RT-024

## Contexto

El requisito de lanzamiento es "misma semilla, mismo binario, mismo resultado". El determinismo entre plataformas solo lo necesitaría la Copa diaria (RF-128c), posterior al lanzamiento.

## Decisión

Atributos, probabilidades y contadores en `int`. Posiciones en `float` con las restricciones de `determinismo.md` (sin funciones trascendentes, sin paralelismo). La CI ejecuta la huella de determinismo en Windows y Linux; una divergencia activa la migración a Fix64.

## Alternativas descartadas

- Fix64 desde el principio: coste de legibilidad y rendimiento sin requisito que lo justifique.
- Todo en `float`, incluidas probabilidades: fuente clásica de divergencias.

## Consecuencias

Las posiciones no deben mezclarse con el RNG ni convertirse en probabilidades sin pasar por enteros.
