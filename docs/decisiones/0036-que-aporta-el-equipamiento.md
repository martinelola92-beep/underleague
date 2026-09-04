# 0036. El equipamiento sube atributos; el perk cambia reglas

**Fecha:** 2026-09-05
**Estado:** Aceptada (decisión del revisor)
**Requisitos:** RF-072, RF-075..078, RF-080..085, RT-035
**Relacionada con:** ADR 0025 (presupuesto de atributos), ADR 0035 (escala por canal)

## Contexto

Hoy los objetos usan **el mismo formato de efectos que los perks** (`EffectDefinition`), así que un objeto puede hacer exactamente lo mismo que un perk: dispararse con un evento, comprobar una condición y modificar una probabilidad. La consecuencia es que no se distinguen: son perks que se pueden mover de jugador.

El revisor plantea que el equipamiento suba **una o dos estadísticas** del jugador y nada más.

## Decisión

Tres capas con tres funciones, tres cadencias y tres decisiones distintas:

| | Qué es | Cuándo se decide | Se puede quitar |
|---|---|---|---|
| **Perk** | Una **regla**: cuándo pasa algo distinto | Al recibirlo, para siempre (RF-072) | No |
| **Objeto** | Una **estadística**: cuánto vale el jugador | Antes de cada partido, transferible (RF-075) | Sí |
| **Consumible** | Un **efecto puntual** de un partido | Al preparar el partido (RF-080) | Se gasta (RF-085) |

**Un objeto modifica uno o dos atributos, y nada más.** Sin disparador, sin condición, sin canal de probabilidad. Su efecto está activo mientras esté equipado.

Los **tres arquetipos obligatorios de RF-077 se expresan igual de bien** con atributos, que es lo que hace viable la decisión:

- **Maldito**: sube mucho un atributo y baja otro de forma permanente. *"Mucha fuerza. Le cuesta correr."*
- **Frágil**: sube un atributo y se rompe tras N partidos o cuando su portador se lesiona.
- **Restringido**: sube un atributo solo si el portador lleva una etiqueta concreta; sin ella, no aporta nada.

**Excepción acotada**: los objetos **legendarios** pueden llevar un efecto de regla, y son los únicos. Deben ser pocos y memorables. Si esa excepción empieza a crecer, se cierra: la claridad de la capa vale más que un objeto ingenioso.

## Por qué, además de por claridad

**El valor de un objeto pasa a ser predecible antes de medirlo.** Ya existe la tabla de valor marginal por atributo (`docs/balance/fase1b-resultados.md`): fuerza +11,1, técnica +7,5, velocidad +6,6, resistencia +3,0 puntos de tasa de victoria por cada +20. Con eso, diseñar un objeto de +8 de fuerza es aritmética; con efectos condicionales, cada objeto era un experimento que había que medir aparte.

Eso no es teórico: el equipamiento ya nos ha costado dos mediciones contradictorias (+8,2 puntos en un experimento, −0,0/−0,6/−6,4 en otro) precisamente porque su valor dependía de condiciones que se cumplían o no según la build. Y un objeto llegó a tener la contrapartida invertida —subía la probabilidad de lesionar al rival en vez de la de lesionarse— sin que nadie lo notara, porque con efectos condicionales el signo no salta a la vista.

**Y la transferencia se vuelve una decisión de verdad.** Si el objeto es una regla condicional, moverlo de jugador exige recalcular si su condición se sigue cumpliendo. Si es "+8 de técnica", la pregunta *"¿a quién le doy las botas?"* se responde mirando la plantilla, que es exactamente el tipo de decisión que la pantalla de Equipo debe soportar.

## Lo que se pierde

Los objetos rompe-reglas (anular una tarjeta, repetir un evento) dejan de existir salvo en los legendarios. Es un coste asumido: RF-077 no los exige, y esa función ya la cubren los perks y los consumibles.

## Consecuencias

- `data/items/*.json` cambia de forma: `attributeBonus` en vez de `effects`, más el arquetipo. Los 12 objetos actuales se reescriben.
- El validador rechaza efectos con disparador o condición en un objeto no legendario.
- Las descripciones generadas se simplifican mucho: una plantilla por arquetipo.
- Hay que recalibrar la magnitud: siete titulares equipados suben bastante a un equipo, así que los valores serán pequeños (del orden de +5 a +10 en uno o dos atributos), y el efecto conjunto se mide contra la curva de puertas de la ADR 0033.
- El presupuesto de la ADR 0025 acota lo que un jugador **nace** valiendo; el equipamiento es la vía legítima de superar ese techo, y por eso su magnitud es la palanca que decide cuánto pesa el equipamiento frente a la generación.
