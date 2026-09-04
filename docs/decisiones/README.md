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
| [0012](0012-buildswindifferently-normalizada.md) | `buildsWinDifferently` normalizada contra la referencia de la raza | Propuesta |
| [0020](0020-cuerpos-y-ocupacion.md) | Cuerpos con volumen, separación blanda y empuje | Aceptada, pendiente de implementar |
| [0021](0021-adyacencia-estatica-y-proximidad.md) | Vínculos direccionales resueltos antes del partido y proximidad dinámica | Aceptada, pendiente de implementar |
| [0022](0022-roles-y-ocupacion-de-espacio.md) | Comportamiento sin balón por estado táctico, `FindSpace` y `PressCarrier` | Aceptada, pendiente de implementar |
| [0023](0023-perks-exclusivos-de-raza.md) | Perks universales por defecto y núcleo exclusivo por raza | Aceptada, pendiente de implementar |
| [0024](0024-etiquetas-de-estilo-individuales.md) | Etiqueta de estilo individual con sesgo racial (elfos mayormente `Fine`, pero existe el elfo `Brute`) | Aceptada, pendiente de implementar |
| [0025](0025-generacion-de-atributos.md) | Generación de atributos por raza, posición, rareza y estilo, con baremos por posición | Aceptada, con tensión abierta en el eje de rareza |
| [0026](0026-habilidades-raciales.md) | Habilidades raciales como perks de equipo | Aceptada, diseño pendiente del visto bueno del revisor |
| [0027](0027-rareza-frente-a-nivel.md) | Los legendarios son netamente superiores; común de nivel 8 ≈ legendario de nivel 2 | Aceptada. **Modifica RF-024** |
| [0028](0028-zona-de-accion.md) | La correa pasa de radio duro a zona de acción con forma por posición, tamaño por atributo y disciplina por raza | Aceptada. **Modifica RF-042 y RT-095** |
| [0029](0029-visualizacion-de-la-zona.md) | Zona y margen dibujados al colocar, mapa de cobertura del equipo y vínculos visibles | Aceptada. **Modifica RF-045** |
| [0030](0030-acciones-de-ataque-y-bloqueo.md) | Pase corto y largo como decisiones distintas, regate y tiro según el jugador, bloqueo sin balón y árbitro adelantado | Aceptada. **Matiza RF-057** |
| [0031](0031-correa-fuera-del-presupuesto.md) | La correa sale del presupuesto: su valor marginal medido es negativo | Aceptada. **Modifica ADR 0025 y 0028** |
| [0032](0032-metricas-de-comparacion-de-builds.md) | RT-055 y la progresión se miden contra una referencia equipada, no contra una plantilla desnuda | **Propuesta: decisión del revisor** |
| [0033](0033-los-jefes-como-puertas-de-build.md) | Los jefes son puertas de calidad de build y definen la curva de exigencia de la run | Aceptada. **Sustituye la 2.ª mitad de `scalingRewardsGoodBuilds`** |
| [0034](0034-alinear-con-lesion-grave.md) | Alinear a un lesionado grave es posible y su precio es el riesgo de muerte; falta decidir el coste inmediato | **Propuesta: decisión del revisor** |

El hueco del 0011 corresponde a una decisión sobre el radio de adyacencia que quedó absorbida por la 0021 antes de aceptarse.
| [0012](0012-buildswindifferently-normalizada.md) | `buildsWinDifferently` normalizada contra la referencia de la raza | **Propuesta: decisión del revisor** |
| [0020](0020-cuerpos-y-ocupacion.md) | Cuerpos con volumen, separación blanda y empuje | Aceptada |
| [0021](0021-adyacencia-estatica-y-proximidad.md) | Adyacencia resuelta antes del partido; proximidad dinámica aparte | Aceptada |
| [0022](0022-roles-y-ocupacion-de-espacio.md) | Comportamiento sin balón: contraste táctico y búsqueda de espacio | Aceptada |
