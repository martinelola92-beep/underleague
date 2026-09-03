# 0001. Godot 4.6 .NET con C#

**Fecha:** 2026-09-03 (registra una decisión del documento de requisitos v0.9)
**Estado:** Aceptada
**Requisitos:** RT-001, RT-002, RT-003, RT-005

## Contexto

Simulación por lotes de 10.000 partidos en menos de 60 s (RT-051), modelo de datos grande y necesidad de librerías (NCalc, JsonSchema.Net, Steamworks) desde NuGet.

## Decisión

Godot 4.6 o superior en su rama .NET. Todo el código en C#. Steam mediante Steamworks.NET o Facepunch.Steamworks.

## Alternativas descartadas

- GDScript: demasiado lento para el lote de balance y sin tipado estático fuerte sobre el modelo.
- Unity: licencia y confianza; no aporta nada al núcleo, que vive fuera del motor.
- Motor propio: el render es lo menos arriesgado del proyecto; no compensa.

## Consecuencias

Godot .NET no exporta a web ni a móvil con la misma facilidad; irrelevante (no objetivos §1). Requiere el SDK de .NET en la máquina de Windows (versión en ADR 0008).
