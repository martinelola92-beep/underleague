# 0025. Generación de atributos por cuatro ejes, con baremos por posición

**Fecha:** 2026-09-04
**Estado:** Aceptada (decisión del revisor), con una tensión abierta en el eje de rareza
**Requisitos:** RF-005, RF-021..024c, RF-057c, RF-023b

## Contexto

Hoy un jugador se genera con `calidad + sesgo de raza + sesgo de posición + desviación individual`. Faltan dos ejes que el revisor pide: **rareza** y **etiqueta de estilo** (ADR 0024). Y falta garantizar que el resultado sea funcional: nada impide hoy generar un portero con los atributos que la parada usa por los suelos.

## Decisión

**Orden de generación**, en este orden porque cada paso condiciona al siguiente:

1. **Posición**, fijada por la composición del club (RF-005): 1 portero, y el reparto de defensas, centrocampistas y delanteros del club.
2. **Etiqueta de estilo**, sorteada con la distribución de la raza (ADR 0024).
3. **Rareza**, según el club (uno de los diez es superior a común, RF-005) o la fuente (recompensa, mercado).
4. **Atributos** = `calidad base + sesgo de raza + sesgo de posición + sesgo de estilo + sesgo de rareza + desviación individual`, acotado a 1..99 (RF-022) y después a los **baremos** del punto siguiente.

**Baremos por posición**: cada posición declara un mínimo en los atributos de los que depende su función, de modo que no se generen jugadores inservibles. El portero es el caso claro: RF-057c define la parada sobre fuerza (tiros lejanos y cargas), velocidad (reflejos y uno contra uno), técnica (colocación y penaltis) y resistencia (que el porcentaje no decaiga), sin atributos exclusivos de portero; el baremo garantiza que un portero generado no salga por debajo de un mínimo en ese conjunto. Los baremos son mínimos, no medias: un portero puede ser malo, pero no inútil.

**Especialización sin atributos nuevos.** El revisor describe los perfiles en términos futbolísticos —"defensas más fuerza que remate, centrocampistas más pase, delanteros más remate"—, pero RF-022 fija **cinco** atributos (fuerza, velocidad, técnica, resistencia, correa) e integra la precisión en técnica. Añadir `pase` y `remate` contradiría el requisito y multiplicaría el coste de balanceo. La especialización se consigue con tres mecanismos que ya existen:

- **Sesgo de posición** en la generación (ya implementado): el defensa nace con más fuerza y menos técnica; el centrocampista con más técnica y resistencia; el delantero con más velocidad y técnica.
- **Rasgos** (RF-022c): `Scorer` mejora el tiro, `Cerebral` el pase. Es donde vive la diferencia entre "buen pasador" y "buen rematador" con la misma técnica.
- **Ponderación por posición dentro de las resoluciones**, si hace falta más contraste: la fórmula de tiro puede pesar la técnica de un delantero más que la de un defensa, sin que existan dos atributos distintos.

## Tensión abierta: el eje de rareza

RF-023 dice que la rareza determina "el punto de partida y el techo de perks, **nunca** el techo de nivel", y RF-024 exige que *"un jugador común de nivel máximo con buenos perks debe poder superar en rendimiento a un legendario de nivel bajo"*. Si la rareza además sube atributos, ese margen se estrecha.

Ya está medido (`docs/balance/fase1-perks.md`): hoy, sin ningún sesgo de rareza en atributos, un común de nivel 8 con 2 perks vence a un legendario de nivel 1 con 4 perks el **59,4%** de las veces. Ese número es el presupuesto disponible.

**Decisión**: el sesgo de rareza en atributos es **pequeño y acotado** (orden de +0 / +2 / +4 sobre la media para común / raro / legendario), y `/Balance` vigila permanentemente la métrica de RF-024. Si el común de nivel 8 baja del 55%, el sesgo se reduce. La rareza sigue siendo, sobre todo, techo de perks.

## Consecuencias

- `data/races/*.json` gana la distribución de estilos y sus sesgos; `data/sim/tuning.json` gana `generation.rarityBias`, `generation.styleBias` y `generation.positionFloors`.
- La generación es un punto crítico de balance: cuatro ejes multiplicativos producen colas largas. `/Balance` debe poder volcar la distribución de atributos generados por raza, posición y estilo para inspeccionarla.
- RF-024c ("dos jugadores de la misma raza y posición no deben sentirse intercambiables") queda cubierto por estilo + rasgos + desviación individual.
