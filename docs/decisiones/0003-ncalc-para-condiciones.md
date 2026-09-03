# 0003. NCalc para condiciones de perks

**Fecha:** 2026-09-03 (registra una decisión del documento de requisitos v0.9)
**Estado:** Aceptada
**Requisitos:** RF-065, RF-068, RT-033, RT-034, RT-035

## Contexto

Los perks son datos, no código. Sus condiciones necesitan expresiones sobre etiquetas y contexto sin permitir código arbitrario ni reflexión en tiempo de partido.

## Decisión

Condiciones como expresiones NCalc ampliadas con funciones propias (`tiene`, `turba`, `criterio`, `zona`, `adyacente`, ...), compiladas una vez al cargar `/data`. Las descripciones se generan desde el efecto, nunca desde texto escrito a mano.

## Alternativas descartadas

- Scripting (Lua, Roslyn scripting): código arbitrario, difícil de validar y de traducir a descripción.
- DSL propia: más trabajo que NCalc para el mismo resultado.
- Condiciones como árbol JSON: verboso para diseñar, sin ventaja real.

## Consecuencias

Un identificador desconocido es error de validación. El *pretty-printer* de condiciones a texto es parte del sistema de descripciones y debe cubrir todas las funciones.
