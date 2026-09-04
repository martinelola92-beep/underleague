# 0024. Etiqueta de estilo individual con sesgo racial

**Fecha:** 2026-09-04
**Estado:** Aceptada (decisión del revisor)
**Requisitos:** RF-004b, RF-022d, RF-024b, RF-024c, RF-031, RF-032, RF-068, RF-110, UI-012

## Contexto

Hasta ahora `data/races/*.json` define **una** etiqueta por raza (`Elf → Fine`, `Orc → Brute`) que portan todos sus jugadores. Con clubes monoraza (RF-004) eso hace que cualquier condición por etiqueta se cumpla siempre o nunca, según el club con el que empieces: no es una decisión (motivo de la ADR 0023).

Pero el documento de requisitos ya pide lo contrario para los atributos. RF-024b: *"La raza fija un sesgo poblacional que desplaza la media de sus atributos, pero cada jugador generado recibe además un sesgo individual que puede contradecirlo. Debe ser posible un orco técnico y lento, o un elfo agresivo y torpe. La raza describe a la población, nunca al individuo."* La etiqueta se había quedado fuera de ese principio.

## Decisión

**Se desdobla la etiqueta racial en dos conceptos distintos**, hoy mezclados en el mismo campo:

1. **Etiqueta de especie** (`Elf`, `Orc`, `Human`…): determinista, la porta todo jugador de esa raza. Sirve a los perks exclusivos de raza (ADR 0023), a los mercenarios (RF-110/111) y a los desbloqueos. No se usa como condición en los perks universales.
2. **Etiqueta de estilo** (`Fine`, `Brute`, `Bulwark`, `Cold`…): **una por jugador**, sorteada al generarlo con una distribución sesgada por raza. Es la que consultan las sinergias de los perks universales.

Distribución orientativa: **~70% la etiqueta dominante de la raza** y ~30% repartido entre dos o tres alternativas, incluyendo siempre una **opuesta** a la identidad de la raza. Los elfos son mayoritariamente `Fine`, pero existe el elfo `Brute`.

**La etiqueta desplaza los atributos del individuo hacia su estilo.** Un elfo `Brute` recibe un empujón de fuerza sobre la media élfica (sin llegar a la media orca) y una penalización de técnica. Sin esta correlación la etiqueta sería un adorno y el jugador minoritario, basura: tendría el cuerpo de un elfo y una etiqueta que no le sirve. Con ella, un elfo `Brute` es material real para una build física con elfos.

**El club inicial garantiza mayoría de la etiqueta dominante y al menos un jugador con etiqueta minoritaria.** Lo primero mantiene viable la build racial desde el primer partido; lo segundo hace que el jugador descubra la mecánica en su primera run, en vez de tropezarse con ella tres partidas después.

## Alternativas descartadas

- **Etiqueta racial única** (statu quo): convierte toda condición por etiqueta en un adorno dentro de un club monoraza.
- **Suprimir las etiquetas de estilo y usar solo los rasgos**: los rasgos (RF-022c) son 1-3 por jugador y modulan **comportamiento** (pesos de la IA, RT-094); la etiqueta de estilo es **una** y define al jugador de cara a las **sinergias**. Fundirlas haría que cada jugador tuviera de una a tres identidades simultáneas y volvería ilegible cualquier perk de composición.
- **Distribución uniforme entre estilos**: disolvería la identidad racial, que RF-031 y RF-032 exigen mantener.

## Consecuencias

- **Rescata el eje de identidad como decisión** y recalibra la ADR 0023: los perks universales **sí** pueden condicionar por etiqueta de estilo (ahora varía dentro del club); lo que no pueden es condicionar por especie.
- **Da variedad entre runs de la misma raza**, que es lo que un roguelite necesita: si te salen tres elfos `Brute`, la partida empuja hacia una build distinta. Sirve directamente a RF-032 (tres builds viables por raza) en lugar de contradecirlo.
- La etiqueta de estilo debe ser **visible** en la ficha de jugador (UI-012), en el informe de ojeo (RF-012b) y en el mercado (RF-114): sin verla no hay decisión.
- El mercado gana un eje de compra: fichar por etiqueta, no solo por atributos y rareza.
- Encaja con el tono (RA-025): el elfo rompepiernas y el orco que solo sabe dar pases cortos son material de humor negro y de historias que el jugador cuenta.
- `data/races/*.json` cambia de forma: `speciesTag` fijo y `styleTagWeights` con la distribución. La generación de jugadores (RF-024c) sortea el estilo antes que los atributos, porque los modifica.
