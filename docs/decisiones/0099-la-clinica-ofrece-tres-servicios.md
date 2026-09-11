# 0099. La clínica ofrece tres servicios, y uno de ellos puede matar

**Fecha:** 2026-09-12
**Estado:** Aceptada e implementada (`Sim/Run/Systems/Medical/MedicalSystem.cs`, `data/economy/economy.json`, `/Game` pantalla de nodo)
**Decisión del revisor**: «en la clínica dar opción a curar todo el equipo por un coste X o la opción de curar jugadores individuales más barato; añadir opción de curar más barato todavía pero con riesgo de porcentaje de que no se cure o salga más lesionado».
**Amplía RF-094** (la clínica deja de tener un solo servicio) y **usa la puerta que abrió la ADR 0048** (un jugador puede morir). No toca RF-092 ni RF-091.
**Requisitos:** RF-012d, RF-091, RF-092, RF-094, RT-021, RT-024, RT-057
**Relacionada:** ADR 0041, ADR 0048 (las cinco condiciones de la muerte), ADR 0090 (la clínica cura la leve), ADR 0096 (la clínica sube a 14)

## El problema

La clínica tenía **un** servicio: un jugador, precio fijo por gravedad, resultado garantizado. La única
decisión era *a quién*, y con oro de sobra ni eso: se curaba a todos por orden. Un nodo sin dilema.

## Los tres servicios

| Servicio | Precio | Qué garantiza |
|---|---|---|
| **Por pieza** (el de siempre) | `clinicCost` 14 · `clinicMinorCost` 4 | cura, siempre |
| **La plantilla entera** | `clinicSquadCost` **24**, tarifa plana | cura a todos los lesionados |
| **El matasanos** | `clinicRiskyPercent` **40 %** del precio normal | nada |

El matasanos falla el **30 %** (`clinicRiskyFailPercent`: no cura y el oro se pierde igual, porque cobra por
intentarlo) y **empeora un escalón** el **12 %** (`clinicRiskyWorsePercent`): leve → grave, grave →
**muerto**. Los dos porcentajes van escritos en el botón antes de pulsarlo.

La tarifa plana está calibrada para que la cuenta se vea: con 14 la grave y 4 la leve, **a partir de dos
graves** sale a cuenta pasar por caja una vez. Medido con la política automática: a 30 se toma 0,15 veces por
run, a 24 son **0,41** y a 20 son 0,54 —se eligió 24 porque es donde el número dice lo mismo que el diseño—.

## Que el matasanos pueda matar es legítimo, y por qué

Las cinco condiciones de la ADR 0048 se cumplen, y una de ellas mejor que nunca:

1. **Se sabe antes**: el porcentaje está en el botón (RF-012d). No hay daño no anunciado.
2. **Se puede evitar**: pagando el precio normal, que siempre está al lado.
3. **Se puede reducir el riesgo**: eligiendo a quién se lleva al matasanos.
4. **El equipo del muerto vuelve al inventario**: por el mismo camino que cualquier otra muerte.
5. **Es rara**: 12 % de una fracción de los tratamientos.

Lo que añade es que la muerte pueda venir de una **decisión de menú** y no solo de una entrada. Es
exactamente la identidad del juego —carnicería administrada— dicha donde se administra.

## Determinismo

La tirada sale de un flujo propio, `RngStreams.Clinic(semilla, nodo)` (tipo 5), y **no** del de recompensas:
así una consulta al matasanos no desplaza lo que ofrece un nodo de recompensa (RT-022). Dentro de un mismo
nodo, el contador `clinicRolls:<nodo>` dice cuántas tiradas hay que saltar, de modo que dos tratamientos
seguidos no sacan el mismo número y la run se reproduce igual (RT-021, RT-024). Hay test de las dos cosas.

## La política automática

`RunPolicy` mira primero la tarifa plana: si curar uno a uno a los que trataría costara más que la tarifa,
paga la tarifa. Y usa al matasanos **solo en su único hueco honesto**: queda un grave que merece la pena y
no llega el oro para el precio garantizado, pero sí para el suyo. La alternativa ahí no es pagar menos, es
no curar. Medido: 0,03 tratamientos arriesgados por run —el jugador lo usará mucho más, porque puede querer
apostar; la política solo lo hace cuando no tiene otra—.

## Lo que se mide

Filas nuevas de `--full-runs`: `squadTreatmentsPerRun` y `riskyTreatmentsPerRun`. Si salieran a cero, el
precio o el hueco estarían mal puestos y el jugador tendría una opción que nadie toma. Los números del
paquete completo están en la ADR 0100, que se midió con este dentro.
