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

**Un objeto sube atributos y nada más.** Sin disparador, sin condición, sin canal de probabilidad, **sin excepciones por rareza**. Su efecto está activo mientras esté equipado.

**La rareza determina cuántos atributos sube:**

| Rareza | Atributos que sube |
|---|---|
| Común | 1 |
| Raro | 2 |
| Legendario | 3 |

Es una regla que el jugador entiende sin que nadie se la explique —un legendario mejora tres cosas— y que el validador comprueba de un vistazo. La magnitud por atributo es la misma en las tres rarezas: lo que escala con la rareza es **cuántos** atributos toca, no cuánto sube cada uno.

Los **tres arquetipos obligatorios de RF-077 se expresan igual de bien** con atributos, que es lo que hace viable la decisión:

- **Maldito**: sube mucho un atributo y baja otro de forma permanente. *"Mucha fuerza. Le cuesta correr."*
- **Frágil**: sube un atributo y se rompe tras N partidos o cuando su portador se lesiona.
- **Restringido**: sube un atributo solo si el portador lleva una etiqueta concreta; sin ella, no aporta nada.

**Sin excepciones.** Ningún objeto, tampoco el legendario, lleva efectos de regla: eso es lo que hace un perk. Una excepción "solo para legendarios" reintroduciría exactamente la complejidad que esta decisión elimina, y con ella la imposibilidad de calcular lo que vale un objeto antes de medirlo.

**Los arquetipos de RF-077 se apilan sobre esa regla**, sin alterar el recuento: el número de atributos que un objeto **sube** lo fija su rareza; lo que el arquetipo añade es su contrapartida. Un maldito común sube uno y baja otro; un maldito legendario sube tres y baja uno. El frágil y el restringido no cambian el recuento, cambian cuándo aporta.

## Por qué, además de por claridad

**El valor de un objeto pasa a ser predecible antes de medirlo.** Ya existe la tabla de valor marginal por atributo (`docs/balance/fase1b-resultados.md`): fuerza +11,1, técnica +7,5, velocidad +6,6, resistencia +3,0 puntos de tasa de victoria por cada +20. Con eso, diseñar un objeto de +8 de fuerza es aritmética; con efectos condicionales, cada objeto era un experimento que había que medir aparte.

Eso no es teórico: el equipamiento ya nos ha costado dos mediciones contradictorias (+8,2 puntos en un experimento, −0,0/−0,6/−6,4 en otro) precisamente porque su valor dependía de condiciones que se cumplían o no según la build. Y un objeto llegó a tener la contrapartida invertida —subía la probabilidad de lesionar al rival en vez de la de lesionarse— sin que nadie lo notara, porque con efectos condicionales el signo no salta a la vista.

**Y la transferencia se vuelve una decisión de verdad.** Si el objeto es una regla condicional, moverlo de jugador exige recalcular si su condición se sigue cumpliendo. Si es "+8 de técnica", la pregunta *"¿a quién le doy las botas?"* se responde mirando la plantilla, que es exactamente el tipo de decisión que la pantalla de Equipo debe soportar.

## Lo que se pierde

Los objetos rompe-reglas (anular una tarjeta, repetir un evento) dejan de existir salvo en los legendarios. Es un coste asumido: RF-077 no los exige, y esa función ya la cubren los perks y los consumibles.

## Consecuencias

- `data/items/*.json` cambia de forma: `attributeBonus` con **tantas entradas como exige la rareza** en vez de `effects`, más el arquetipo y su contrapartida. Los 12 objetos actuales se reescriben.
- El validador rechaza cualquier `effects` en un objeto y comprueba que el número de atributos coincide con la rareza. Es una validación trivial, que es justo la ventaja.
- Las descripciones generadas se simplifican mucho: una plantilla por arquetipo.
- **Magnitud de partida, calculada con la tabla de valor marginal**: esa tabla mide puntos de tasa de victoria por cada +20 repartidos entre los **diez** jugadores, así que +20 a **un** jugador vale en torno a 1,1 puntos con el atributo más valioso. Con eso, un valor del orden de **+10 por atributo** deja un común en ~0,55 puntos, un raro en ~1,1 y un legendario en ~1,65; siete titulares equipados con comunes suman unos 4 puntos y con legendarios unos 11. Es un escalonado razonable y del mismo orden que el +8,2 que se midió con los objetos antiguos. Se confirma midiendo contra la curva de puertas de la ADR 0033.
- **La rareza del objeto se vuelve comparable a la del jugador**: ambas escalan lo mismo (un legendario toca tres atributos, igual que un jugador legendario tiene más presupuesto), lo que permite razonar sobre el precio de mercado de los dos con la misma vara.
- El presupuesto de la ADR 0025 acota lo que un jugador **nace** valiendo; el equipamiento es la vía legítima de superar ese techo, y por eso su magnitud es la palanca que decide cuánto pesa el equipamiento frente a la generación.
