# Decisiones pendientes e inconsistencias

Registro vivo. Cuando una decisión se toma, se mueve a un ADR en `decisiones/` y aquí queda una línea con el enlace. Las inconsistencias se resuelven actualizando `requisitos.md` a una versión nueva.

## Decisiones abiertas (§10 de requisitos)

| Id | Decisión | Bloquea | Lectura provisional |
|---|---|---|---|
| D-1 | Nombre del proyecto y del universo | Fase 4 | Repositorio: **Underleague** |
| D-2 | Número exacto de nodos por acto y distribución por tipo | Fase 2 | 10-12 nodos, <= 60% partidos (RF-003b), mercado cada 3-4 (RF-011b) |
| D-3 | Modelo económico del salario de mercenarios frente al coste de tienda | Fase 2 | — |
| D-4 | Si la turba introduce peligros de público como entidades o solo estrechamiento | Fase 3 | Solo estrechamiento + casillas invadidas fijas (RF-055b). Nota: el enunciado original habla de "fase 3 del partido", concepto eliminado por RF-055 |
| D-5 | Qué 3 razas entran en el prototipo | **Fase 0** | Humanos, orcos, elfos (recomendación del documento) |
| D-6 | Si el jefe final admite reintento inmediato o exige nueva run | Fase 2 | Nueva run (coherente con ironman RT-061) |
| D-7 | Detalle de la tienda de Rune Dice a replicar | Fase 2 | Solo la estructura por categorías |
| D-8 | Qué logro desbloquea cada raza y en qué orden | Fase 4 | Ejemplos de RF-125 |
| D-9 | Si el jefe final introduce condición de derrota propia y cuál | Fase 2 | RF-001c ya afirma que existe; falta definir **cuál** |
| D-10 | Distribución exacta de tipos de nodo dentro de cada acto | Fase 2 | Ver D-2 |
| D-11 | Precio de lanzamiento y alcance de la demo | Fase 4 | — |

## Decisiones añadidas durante la preparación del repositorio

| Id | Decisión | Bloquea | Lectura provisional |
|---|---|---|---|
| D-12 | Idioma de los identificadores de C# | Fase 0 | Español para dominio (`Jugador`, `Correa`, `Puede`), siguiendo el documento. Confirmar antes de escribir `/Sim` |
| D-17 | `/Game` con `net10.0` en Godot 4.6 (ADR 0008) | Fase 1 | Probar al crear `/Game`; si falla, bajar a `net8.0` |
| D-13 | `/Run` como librería separada de `/Sim` o namespace dentro | Fase 2 | Namespace `Underleague.Sim.Run` hasta la fase 2 (ver `arquitectura.md`) |
| D-14 | Organización de `/data`: un fichero por entidad o uno por tipo | Fase 1 | Un fichero por entidad; facilita revisión y diffs |
| D-15 | Cómo se resuelve el consumible manual sin romper la pureza de `Simulador.Ejecutar` | Fase 1 | Re-ejecución con activación en el estado (ver `arquitectura.md`) |
| D-16 | Analizador para prohibir APIs no deterministas en `/Sim` | Fase 0 | `Microsoft.CodeAnalysis.BannedApiAnalyzers` + test de ensamblado |

## Inconsistencias detectadas en requisitos v0.9

Se aplica la lectura indicada hasta que el documento suba de versión.

| Id | Dónde | Conflicto | Lectura aplicada |
|---|---|---|---|
| I-1 | RT-030 vs RF-022 | El esquema lista `precision` y `agresividad` como atributos; RF-022 integra precisión en técnica y expresa agresividad como rasgo | Cinco atributos: fuerza, velocidad, técnica, resistencia, correa |
| I-2 | RT-030 vs RF-076 | `equipamiento[] (ids, por slot)` frente a "un único objeto equipado" | Un campo `equipo` con un id o null |
| I-3 | RT-030 vs RF-103 | `vinculos[] (…, signo)` frente a "no existen vínculos negativos en el lanzamiento" | Sin campo `signo`; se añade si entran rivalidades |
| I-4 | Glosario vs RF-055 | Glosario: "Fase: uno de los tres tramos en que se divide un partido"; RF-055: el reglamentario es una sola fase | El partido tiene reglamentario + turba opcional. "Fase" se reserva para los tramos de una **jugada** (RF-051) y para el plan de desarrollo |
| I-5 | RT-033 vs RF-055 | Ejemplo de perk con `limite.por = "parte"`; no existen partes | Ámbitos válidos: `jugada`, `partido`, `turba`, `run` |
| I-6 | RF-128 | "Una run recorre los tres actos de una división (C, B, A)" frente a la tabla con cinco divisiones Tercera..Mundial | Cinco divisiones con los nombres de la tabla |
| I-7 | RT-056 vs RF-055c | "Menos del 15% de empates" frente a "los empates no existen como resultado final" | La métrica mide empates **al final del reglamentario**, es decir, frecuencia de turba |
| I-8 | D-9 vs RF-001c | La decisión pendiente pregunta si existe la condición de derrota propia del jefe final; RF-001c ya la afirma | Existe; falta definirla |
| I-9 | RF-066 vs RF-055b | El catálogo tiene `INICIO_TURBA` y `ARBITRO_SE_VA` como eventos distintos aunque ocurren a la vez | Se emiten ambos, en ese orden, en el mismo tick |
| I-10 | Numeración | Secciones 3.6c antes de 3.6b, 3.12b/3.12c | Cosmético; se corrige al subir de versión |
