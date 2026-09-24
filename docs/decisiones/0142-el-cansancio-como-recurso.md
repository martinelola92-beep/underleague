# ADR 0142 — El cansancio como recurso

**Fecha**: 24 sep 2026
**Estado**: aceptada — decisión del revisor (*Gameplay AI Foundations Pass*, bloque 9)
**Paquete**: P8
**Contexto del encargo**: `docs/plan-gameplay-ai-foundations.md`

## Contexto

Existía un atributo `Stamina` y existía «fatiga», pero la fatiga era **una rampa del reloj**: a partir de
`movement.fatigueStartTick` todo el mundo se iba frenando, escalado por su aguante, hasta el final. Sólo
tocaba la velocidad.

Eso deja el atributo casi decorativo y, sobre todo, **no hay nada que gestionar**: el jugador no decide
nada sobre el esfuerzo de su equipo porque el esfuerzo no existe como magnitud. El encargo lo rechaza
explícitamente: *«No quiero una barra que simplemente se vacíe linealmente durante todo el partido hasta
dejar a todos exhaustos.»*

## Decisión

### 1 · Energía que se gasta por lo que uno **hace** y se recupera al no hacerlo

`MatchPlayer.Energy`, de 0 a 1000, entero. Cada tick:

- se **recupera** una cantidad base modulada por el **aguante** —siempre, no sólo al estar parado—;
- se **gasta** por el terreno recorrido, más un extra por llevar el balón (conducir o proteger) y un coste
  puntual por pegarse (entrada o carga);
- `FatigueResistancePercent` **abarata el gasto**. Es un escalar de rasgo que existía desde hace mucho y
  cuyo único efecto era modular la rampa que esta ADR retira: sin engancharlo aquí se habría quedado sin
  efecto ninguno, que es exactamente el patrón que este pass viene a cerrar.

Aplicar la recuperación siempre y sumar el gasto encima evita tener que decidir en qué «modo» está cada
jugador: el que está parado recupera, el que trota se mantiene y el que persigue se vacía, y sale solo.

### 2 · El cansancio se aplica **donde se lee el atributo**, no en cada fórmula

El encargo pide que la fatiga llegue a la velocidad, a la aceleración, a la precisión, a la fuerza, al
regate, al pase, al tiro **y a la decisión**. Repartir eso por siete sitios habría sido siete reglas que se
pueden desincronizar y que cualquier consumidor nuevo puede olvidarse de aplicar.

En vez de eso, el cansancio resta puntos a los atributos **en el accesor**: `Strength`, `Speed` y
`Technique` devuelven ya el valor cansado. Todo lo que lee atributos —la velocidad por tick, la calidad del
disparo, los duelos, la cuota del pase, el regate y las propias pendientes por atributo de la utilidad— lo
respeta sin saber que existe. **Una regla, no siete.**

`Stamina` **no** se cansa, y es deliberado: es lo que gobierna cuánto te cansas, así que restárselo a sí
mismo haría una espiral —cuanto más cansado, menos aguante, más te cansas— que ninguna decisión del jugador
podría anticipar (RF-012d).

### 3 · Y una cosa que los atributos no cubren: **el cansado deja de presionar**

Perseguir, presionar, entrar y cargar no tienen pendiente por atributo en la utilidad, así que se habrían
quedado fuera justo donde el cansancio se nota más en un campo de verdad. Un término de contexto propio lo
arregla: no es que el cansado persiga peor, es que **deja de ir**.

### 4 · El esfuerzo se mide contra el paso de un jugador **medio**, no contra el propio

Fue un error de modelo de la primera versión y cambiarlo importa. Cobrando «fracción de tu propia velocidad
máxima», un jugador ya cansado —que corre más despacio porque su velocidad efectiva ha bajado— seguía
pagando el precio completo por ir a tope, así que el cansancio **no se frenaba a sí mismo nunca**. Midiendo
terreno recorrido contra una vara fija, cansarse abarata el tick siguiente, que es lo que le da al recurso
un estado estacionario. Y además el rápido paga más que el lento por el mismo tick, que es lo correcto.

## El hallazgo: sin punto de equilibrio no hay recurso, hay acantilado

La primera versión de los números dejaba **129 de 280 jugadores acabando exactamente a cero**, con mediana
6 de 1000. Eso no es un recurso que se gestiona: es la barra que el encargo rechaza, y es un **estado
degenerado** —media plantilla pasaba el último tercio clavada en el tope del castigo—, no una cuestión de
balance.

La causa se midió: el **ciclo de trabajo medio** de un jugador en este motor es del **72 %** de lo que
podría moverse, y la recuperación estaba puesta para equilibrar al 56 %. Con eso **no existía equilibrio**:
todo el que jugaba caía a cero y se quedaba ahí.

Con la recuperación en 7 el equilibrio de un aguante medio cae cerca de ese mismo ciclo y la distribución
final pasa a ser **mediana 447 y jugadores repartidos de 0 a 1000**: hay gente entera, gente a medias y
gente fundida, y quien decide de qué lado cae es cuánto ha corrido y cuánto aguanta.

**Elegir ese número no es calibrar una frecuencia de juego**: es que el recurso tenga estado estacionario
en vez de un acantilado, que es justo lo que el encargo manda comprobar con los runs de este pass («que no
haya estados imposibles»). Dónde cae exactamente la mediana **sí** es calibración, y es la fase siguiente.

## Valores publicados

| dato | valor | por qué ése |
|---|---|---|
| `fatigue.runCostPerTick` | 9 | la referencia contra la que se fija todo lo demás |
| `fatigue.carryCostPerTick` | 4 | llevar el balón cansa más que correr suelto: es el precio de la conducción de la ADR 0137 |
| `fatigue.contactCost` | 25 | un esfuerzo puntual; hace que la violencia también se pague con piernas |
| `fatigue.recoverPerTick` | 7 | **medido**: el punto de equilibrio, no un valor a ojo (arriba) |
| `fatigue.maxPenaltyPoints` | 25 | con la energía a cero se pierde un cuarto de atributo, no todo: vaciarse duele, no inutiliza |

## Un defecto latente que salió por el camino, y no es de fatiga

La barrera de reanudación de **BB-B** tenía un hueco: con el punto de saque pegado a una banda, el empujón
que aparta a un rival podía caer **fuera del campo**, y acotarlo al campo lo devolvía **más cerca del balón
que la propia barrera** —medido: 1,26 casillas con la barrera en 1,30, con un rival en la línea de banda
exacta—. La barrera existe precisamente para que nadie pueda entrar ni cargar durante la reanudación, así
que un hueco de cuatro centésimas es el hueco entero.

Arreglado en la causa: se desliza **por la línea** hasta cumplir la distancia, en vez de conformarse con el
punto recortado. Y con un límite aprendido a golpes en el primer intento: **siempre hacia el lado en el que
el jugador ya está**. Probar también el lado contrario cerraba más huecos pero cruzaba al jugador por
delante del balón —medido, un salto de **3,85 casillas en un solo fotograma**—, que es justo el defecto que
BA-K arregló en el render. Una barrera no puede teletransportar a nadie.

Era pre-existente: sólo se hizo visible porque este paquete mueve a los jugadores a sitios nuevos.

## Consecuencias

- **Se retiran** `movement.fatigueStartTick` y `movement.fatigueMaxSlowPercent`. La rampa ya no existe.
- **Cambia todo el partido**, porque los atributos efectivos cambian con el tiempo y los atributos los lee
  medio motor. Es el cambio más transversal del pass y era el propósito.
- `Stamina` deja de ser casi decorativo y pasa a decidir quién llega vivo al final — lo que convierte la
  alineación en una decisión con una dimensión más.
- **No se ha tocado nada de la run**: el desgaste entre partidos (RF-097) es otro sistema y sigue como
  estaba. Esta ADR es sólo dentro del partido.
