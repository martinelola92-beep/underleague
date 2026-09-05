# Análisis comparativo con Rune Dice

Contraste entre la estructura de progresión de Rune Dice, tomada como referencia por el revisor, y lo que Underleague tiene hoy implementado. No es una lista de deseos: separa lo que ya coincide, lo que falta y lo que **reorienta** decisiones que ya habíamos tomado.

## 1. Lo que ya coincide

| Rune Dice | Underleague | Estado |
|---|---|---|
| 3 actos, 10-15 nodos, 30-45 etapas | 3 actos, [11,12,12] = 35 nodos, 20 partidos | Implementado (D-2/D-10) |
| Mapa ramificado estilo Slay the Spire | Grafo por capas, sin retroceso | Implementado |
| Combates normales y élites | `LeagueMatch` y `EliteMatch` | Implementado |
| Eventos con riesgo/recompensa | `Event` | Nodo definido, contenido pendiente |
| Hogueras: curar o mejorar | `Clinic` (curar) y `Training` (experiencia dirigida) | Implementado, repartido en dos nodos |
| Jefe al final de cada acto | Tres jefes con modificador de regla | Implementado |
| Tienda: compra, reroll | Mercado con cuatro categorías, reroll de coste creciente | Implementado |
| Ascensión con modificadores acumulativos | Divisiones (RF-128) | Diseñado, no implementado |

## 2. Lo que falta, por orden de importancia

### 2.1 Recompensas diferenciadas por tipo de nodo

Rune Dice escalona la recompensa según lo que has superado: combate normal da oro y elección de tres; **élite** da mucho oro, rareza alta **y un artefacto**; **jefe** da curación completa, un artefacto de jefe que altera el estilo de juego, y elección de rareza máxima.

Underleague da hoy lo mismo tras cualquier victoria (RF-071). Es la carencia más importante de las tres, porque **es lo que hace que elegir ruta sea una decisión**: si el élite no paga más, nadie lo elige, y el mapa ramificado se convierte en decorado.

La **curación completa tras el jefe** merece atención aparte: en un juego cuyo recurso central es el desgaste, un punto de reseteo al final de cada acto cambia por completo el ritmo. Permite exprimir la plantilla durante un acto sabiendo que habrá alivio, en vez de administrar la ruina de forma uniforme durante toda la run.

### 2.2 Artefactos pasivos de run

Reliquias que afectan a **toda la partida** y no a un jugador: no existen en Underleague. Todo lo que tenemos —perks, objetos, consumibles— cuelga de un jugador concreto.

Es una capa distinta con una función distinta: da decisiones que **no** dependen de a quién asignar, y es la vía natural para el escalado de run y para los "artefactos de jefe que alteran el estilo de juego". El documento ya cita a Slay the Spire y a Balatro como referencia, y en ambos las reliquias son la columna vertebral de la variedad entre partidas.

Coste a tener en cuenta: es una capa más sobre perks, objetos, consumibles y habilidades raciales. Si se añade, deben ser **pocos y de efecto grande**, no una cuarta lista de modificadores pequeños.

### 2.3 Poder omitir la recompensa

En Rune Dice puedes no coger nada. En Underleague, RF-071 obliga a elegir una de las tres. Con un perk irreversible (RF-072) y slots limitados, **poder rechazar es una decisión legítima**: coger el menos malo puede ser peor que no coger nada.

### 2.4 La purga: analizada y descartada

Rune Dice deja pagar por **destruir** dados débiles. Es tentador copiarlo, pero **cumple una función que aquí no existe**: en un juego de mazo, un dado malo diluye lo que robas. En Underleague un jugador malo no diluye nada, simplemente no lo alineas — y venderlo ya existe (RF-114f), además dando oro en vez de costarlo.

Donde sí habría dilución real es en los **perks**: un slot gastado en un perk que no sirve es permanente. Pero RF-072 lo prohíbe deliberadamente, y el revisor acaba de decir que la decisión central del juego es **a quién le das lo que te toca**. Poder deshacerla pagando la vaciaría. Se descarta.

## 3. Lo que reorienta: la decisión es asignar, no comprar

El revisor lo dice explícitamente: *"cuando consigues un perk u objeto debes saber a quién equipárselo. Ahí está la decisión del jugador."*

Eso cambia la lectura del problema abierto en la **ADR 0042**. Allí se midió que la doctrina de compra contextual empata con la ahorradora y se concluyó que la tienda no tiene decisión. Pero la métrica de la ADR 0037 mide **doctrinas de compra**, y la decisión que el juego quiere premiar es de **asignación**.

Y esa sí rinde, ya medido: añadir a la política el filtro de "solo compra un perk si encaja en su portador" subió la tasa de victoria del **4,2% al 10,0%**. Casi seis puntos por saber colocar, con el mismo oro y el mismo surtido.

**Propuesta**: el criterio de la ADR 0037 se reformula. En vez de exigir que una doctrina de compra gane a las otras, se exige que **una política que asigna bien gane a una que asigna al azar** por un margen claro, con las mismas compras. Eso mide lo que el juego premia de verdad, y ya sabemos que existe.

La dispersión de precios y el valor por portador (salidas 2 y 3 de la ADR 0042) siguen siendo buenas ideas, pero dejan de ser urgentes: no son el corazón de la decisión.

## 4. Las divisiones: el mercado como palanca de dificultad

El revisor añade a RF-128 una palanca que no estaba: **los precios suben con la división**. Encaja exactamente con la ADR 0037 (la escasez es la dificultad) y es la forma más limpia de endurecer sin tocar el motor: mismo juego, menos margen.

Las otras palancas que menciona —mejores rivales, penalizaciones al equipo propio— ya están en el espíritu de RF-128. La tabla del documento (rivales con consumibles, sin canteranos gratis, dos modificadores por jefe, árbitros no neutros) se puede combinar con el encarecimiento.

## 5. La curva de aprendizaje, traducida a métricas

El revisor la describe así: *un jugador mediocre pierde la run en el primer acto; uno medio llega al tercero; hay que saber jugar para completarla.*

Eso es directamente medible con las doctrinas que ya existen:

| Perfil | Doctrina que lo representa | Objetivo |
|---|---|---|
| Mediocre | gastadora (compra lo primero, asigna sin criterio) | muere en el acto 1 la mayoría de las veces |
| Medio | contextual sin filtro de asignación | llega al acto 3, rara vez lo supera |
| Bueno | contextual con asignación | completa la run el 20-30% |

Medido hoy: contextual 17,8% y gastadora 12,2%. **La separación entre perfiles es demasiado pequeña**: un jugador mediocre y uno bueno terminan la run casi igual de veces. Esa distancia —no la tasa absoluta— es la métrica que dice si el juego premia jugar bien, y es la que hay que abrir.

## 6. Y sobre las muertes

*"Que se te mueran jugadores en una run no es problema. Es la dificultad del juego. Debe castigar lo suficiente."*

Confirma la dirección de la ADR 0034 y del desgaste. El estado actual va **muy corto**: 0,02 muertes por run frente a la banda 0,5-2. El desgaste sigue sin ser el recurso central que el documento declara.
