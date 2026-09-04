# Registro de decisiones (ADR)

Una decisión por fichero, numerada, nunca se borra: si cambia, se escribe una nueva que la sustituye y la antigua pasa a `Sustituida por NNNN`. Plantilla en `0000-plantilla.md`.

Cuándo escribir un ADR: cambio de librería o stack, frontera entre proyectos, cambio de un rango de `balance.md` (RT-057), nuevo tipo de efecto en el catálogo de perks, cualquier excepción a las "reglas sin excepción" de `CLAUDE.md` (que, por definición, requiere reescribirlas).

| Nº | Decisión | Estado |
|---|---|---|
| [0001](0001-godot-dotnet-csharp.md) | Godot 4.6 .NET con C# | Aceptada |
| [0002](0002-sim-independiente-de-godot.md) | `/Sim` como librería pura sin Godot | Aceptada |
| [0003](0003-ncalc-para-condiciones.md) | NCalc para condiciones de perks | Aceptada |
| [0004](0004-rng-propio-pcg32.md) | RNG propio PCG32 con flujos derivados | Aceptada |
| [0005](0005-ia-de-utilidad-sin-ml.md) | IA de utilidad ponderada, sin aprendizaje automático | Aceptada |
| [0006](0006-sin-ecs.md) | Sin ECS | Aceptada |
| [0007](0007-punto-fijo-aplazado.md) | Aritmética entera + float en posiciones; punto fijo aplazado | Aceptada |
| [0008](0008-net10-lts.md) | .NET 10 LTS como SDK y objetivo; `/Game` se confirma en fase 1 | Aceptada |
| [0009](0009-identificadores-en-ingles.md) | Identificadores en inglés, documentación en español | Aceptada |
| [0010](0010-rango-empates-reglamentario.md) | Rango de empates al final del reglamentario en RT-056 | **Propuesta: decisión del revisor** |
| [0012](0012-buildswindifferently-normalizada.md) | `buildsWinDifferently` normalizada contra la referencia de la raza | **Propuesta: decisión del revisor** |
| [0020](0020-cuerpos-y-ocupacion.md) | Cuerpos con volumen, separación blanda y empuje | Aceptada |
| [0021](0021-adyacencia-estatica-y-proximidad.md) | Adyacencia resuelta antes del partido; proximidad dinámica aparte | Aceptada |
