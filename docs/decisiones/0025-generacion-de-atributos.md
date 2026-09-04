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

**Modelo de presupuesto.** Los atributos no se sortean uno a uno alrededor de una media: cada jugador recibe un **presupuesto total de puntos** que depende de su rareza y su nivel, y ese presupuesto se **reparte** según posición, etiqueta de estilo y raza. Dos consecuencias buscadas:

- El poder de un jugador queda **acotado por construcción**: no existe el jugador que sale bueno en todo por suerte del generador.
- Los jugadores se diferencian por la **forma** de su perfil, no por su cantidad. Un defensa con 70 de fuerza y 30 de técnica y otro con 55 y 55 valen lo mismo en puntos y juegan distinto, que es exactamente lo que pide RF-024c.

**Baremos por atributo**: además del presupuesto, cada combinación de posición y rareza declara un **mínimo y un máximo** por atributo (por ejemplo, común de nivel 1: ningún atributo por debajo de 50 ni por encima de 70). Los mínimos evitan jugadores inservibles; los máximos evitan que todo el presupuesto se concentre en un solo atributo y produzca perfiles degenerados.

El portero es el caso que más lo necesita: RF-057c define la parada sobre fuerza (tiros lejanos y cargas), velocidad (reflejos y uno contra uno), técnica (colocación y penaltis) y resistencia (que el porcentaje no decaiga), sin atributos exclusivos de portero, así que su reparto carga esos cuatro y su baremo garantiza que un portero pueda ser malo pero nunca inútil.

**Subir de nivel aumenta el presupuesto**, y el reparto del incremento sigue el mismo perfil, de modo que un jugador se hace mejor sin dejar de ser quien era.

**La correa queda fuera del presupuesto** (propuesta pendiente del visto bueno del revisor). Motivo medido: es el atributo de mayor valor marginal —en orcos, +6,7 puntos de tasa de victoria por cada +10, frente a +0,5 de la fuerza (`docs/balance/fase1-perks.md`)—, así que dentro de un presupuesto común cualquier reparto óptimo la maximizaría y los demás atributos se volverían residuales. Conceptualmente tampoco es una capacidad del jugador sino su radio de acción táctico, que RF-043 hace modificable por perks, equipamiento y consumibles. Se determina por raza, posición y estilo, no por sorteo de puntos.

**Especialización sin atributos nuevos.** El revisor describe los perfiles en términos futbolísticos —"defensas más fuerza que remate, centrocampistas más pase, delanteros más remate"—, pero RF-022 fija **cinco** atributos (fuerza, velocidad, técnica, resistencia, correa) e integra la precisión en técnica. Añadir `pase` y `remate` contradiría el requisito y multiplicaría el coste de balanceo. La especialización se consigue con tres mecanismos que ya existen:

- **Sesgo de posición** en la generación (ya implementado): el defensa nace con más fuerza y menos técnica; el centrocampista con más técnica y resistencia; el delantero con más velocidad y técnica.
- **Rasgos** (RF-022c): `Scorer` mejora el tiro, `Cerebral` el pase. Es donde vive la diferencia entre "buen pasador" y "buen rematador" con la misma técnica.
- **Ponderación por posición dentro de las resoluciones**, si hace falta más contraste: la fórmula de tiro puede pesar la técnica de un delantero más que la de un defensa, sin que existan dos atributos distintos.

## El eje de rareza

Resuelto por la **ADR 0027**: el revisor decidió que los legendarios deben ser netamente superiores (un común de nivel 8 equivale a un legendario de nivel 2), lo que modifica RF-024. El presupuesto de atributos por rareza es, junto con los slots de perk, el mecanismo que produce esa diferencia, y `/Balance` vigila el contrapeso: un equipo sin ningún legendario debe seguir pudiendo ganar al jefe final.

## Consecuencias

- `data/races/*.json` gana la distribución de estilos y sus sesgos; `data/sim/tuning.json` gana `generation.rarityBias`, `generation.styleBias` y `generation.positionFloors`.
- La generación es un punto crítico de balance: cuatro ejes multiplicativos producen colas largas. `/Balance` debe poder volcar la distribución de atributos generados por raza, posición y estilo para inspeccionarla.
- RF-024c ("dos jugadores de la misma raza y posición no deben sentirse intercambiables") queda cubierto por estilo + rasgos + desviación individual.
